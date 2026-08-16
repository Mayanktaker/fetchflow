// © 2026 Mayanktaker | Based on XDM by subhra74 (https://github.com/subhra74/xdm)
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Collections.Generic;
using TraceLog;
using XDM.Core.Util;
using XDM.Core.Downloader;
using XDM.Core.HttpServer;
using XDM.Core.DataAccess;

namespace XDM.Core.BrowserMonitoring
{
    /// <summary>
    /// Receives chunked binary blob uploads from the browser extension,
    /// reassembles them on disk, and registers a completed download entry.
    /// </summary>
    public class BlobUploadReceiver
    {
        private readonly ConcurrentDictionary<string, TransferState> transfers = new();
        private static readonly TimeSpan TransferTTL = TimeSpan.FromMinutes(10);

        public void HandleUpload(RequestContext context)
        {
            var headers = context.RequestHeaders;

            string transferId = GetHeader(headers, "X-Blob-Transfer-Id");
            string filename = GetHeader(headers, "X-Filename");
            string mime = GetHeader(headers, "X-Mime");
            string totalSizeStr = GetHeader(headers, "X-Total-Size");
            string chunkIndexStr = GetHeader(headers, "X-Chunk-Index");
            string totalChunksStr = GetHeader(headers, "X-Total-Chunks");
            string blobUrl = GetHeader(headers, "X-Blob-Url");

            if (string.IsNullOrEmpty(transferId) || string.IsNullOrEmpty(filename))
            {
                SendJson(context, 400, "{\"error\":\"missing required headers\"}");
                return;
            }

            if (!int.TryParse(chunkIndexStr, out int chunkIndex) ||
                !int.TryParse(totalChunksStr, out int totalChunks) ||
                !long.TryParse(totalSizeStr, out long totalSize))
            {
                SendJson(context, 400, "{\"error\":\"invalid chunk metadata\"}");
                return;
            }

            // Sanitize filename to prevent path traversal
            var safeFilename = FileHelper.SanitizeFileName(filename);
            if (string.IsNullOrEmpty(safeFilename))
            {
                safeFilename = "blob-download";
            }

            // Create or retrieve transfer state
            var state = transfers.GetOrAdd(transferId, _ => new TransferState
            {
                Filename = safeFilename,
                Mime = mime,
                TotalSize = totalSize,
                TotalChunks = totalChunks,
                BlobUrl = blobUrl,
                CreatedAt = DateTime.UtcNow
            });

            // Append chunk bytes to temp file
            var tempDir = Config.Instance.TempDir;
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, $"xdm-blob-{transferId}.part");

            try
            {
                lock (state.Lock)
                {
                    using var fs = new FileStream(tempFile,
                        chunkIndex == 0 ? FileMode.Create : FileMode.Append,
                        FileAccess.Write, FileShare.None);
                    if (context.RequestBody != null && context.RequestBody.Length > 0)
                    {
                        fs.Write(context.RequestBody, 0, context.RequestBody.Length);
                    }
                    state.ReceivedChunks++;

                    Log.Debug($"Blob chunk {chunkIndex + 1}/{totalChunks} received for {transferId} " +
                              $"({context.RequestBody?.Length ?? 0} bytes)");
                }

                // On final chunk: move file to download folder and register
                if (chunkIndex + 1 >= totalChunks)
                {
                    FinalizeTransfer(state, tempFile, transferId);
                    SendJson(context, 200, $"{{\"ok\":true,\"transferId\":\"{transferId}\",\"filename\":\"{safeFilename}\"}}");
                }
                else
                {
                    SendJson(context, 200, $"{{\"ok\":true,\"transferId\":\"{transferId}\",\"chunk\":{chunkIndex + 1}}}");
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Blob upload chunk error");
                SendJson(context, 500, "{\"error\":\"" + ex.Message.Replace("\"", "'") + "\"}");
            }
        }

        private void FinalizeTransfer(TransferState state, string tempFile, string transferId)
        {
            try
            {
                // Resolve a valid, non-empty download directory.
                // Category-based folder lookup takes priority over manual/OS defaults.
                var targetDir = FileHelper.GetDownloadFolderByFileName(state.Filename);
                if (string.IsNullOrWhiteSpace(targetDir))
                    targetDir = Config.Instance.UserSelectedDownloadFolder;
                if (string.IsNullOrWhiteSpace(targetDir))
                    targetDir = Config.Instance.DefaultDownloadFolder;
                if (string.IsNullOrWhiteSpace(targetDir))
                    targetDir = PlatformHelper.GetOsDefaultDownloadFolder();
                if (string.IsNullOrWhiteSpace(targetDir))
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    targetDir = string.IsNullOrWhiteSpace(home) ? "/tmp" : Path.Combine(home, "Downloads");
                }
                Directory.CreateDirectory(targetDir);

                var finalPath = Path.Combine(targetDir, state.Filename);

                // Avoid collision: append suffix if exists
                if (File.Exists(finalPath))
                {
                    var nameNoExt = Path.GetFileNameWithoutExtension(state.Filename);
                    var ext = Path.GetExtension(state.Filename);
                    for (int i = 1; File.Exists(finalPath); i++)
                    {
                        finalPath = Path.Combine(targetDir, $"{nameNoExt} ({i}){ext}");
                    }
                }

                File.Move(tempFile, finalPath);

                // Register completed download in the database
                var entryId = Guid.NewGuid().ToString("N");
                var entry = new InProgressDownloadItem
                {
                    Id = entryId,
                    Name = state.Filename,
                    DateAdded = DateTime.Now,
                    Size = state.TotalSize,
                    Status = DownloadStatus.Finished,
                    Progress = 100,
                    DownloadType = "Http",
                    FileNameFetchMode = FileNameFetchMode.FileNameAndExtension,
                    TargetDir = targetDir,
                    PrimaryUrl = state.BlobUrl ?? $"blob:{state.Filename}"
                };

                AppDB.Instance.Downloads.AddNewDownload(entry);

                // Notify the UI via Application.DownloadFinished — this marks the
                // download as finished in the DB and adds it to the finished list view
                ApplicationContext.Application.DownloadFinished(entryId, state.TotalSize, finalPath);

                Log.Debug($"Blob download finalized: {finalPath} (id={entryId})");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Blob finalize error");
            }
            finally
            {
                // Cleanup temp file if it still exists (moved successfully = already gone)
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                transfers.TryRemove(transferId, out _);
            }
        }

        private static string GetHeader(Dictionary<string, System.Collections.Generic.List<string>> headers, string name)
        {
            // Exact match first, then case-insensitive fallback (HTTP/2 may lowercase headers)
            if (headers.TryGetValue(name, out var values) && values.Count > 0)
                return values[0];
            foreach (var kvp in headers)
            {
                if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase) && kvp.Value.Count > 0)
                    return kvp.Value[0];
            }
            return string.Empty;
        }

        private static void SendJson(RequestContext context, int statusCode, string json)
        {
            context.ResponseStatus = new ResponseStatus
            {
                StatusCode = statusCode,
                StatusMessage = statusCode == 200 ? "OK" : "Error"
            };
            context.AddResponseHeader("Content-Type", "application/json");
            context.AddResponseHeader("Cache-Control", "no-cache");
            context.ResponseBody = System.Text.Encoding.UTF8.GetBytes(json);
            context.SendResponse();
        }

        /// <summary>
        /// Periodically purge stale transfers (e.g. if extension disconnects mid-upload).
        /// Call from a timer in IpcHttpMessageProcessor.
        /// </summary>
        public void PurgeStaleTransfers()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in transfers)
            {
                if (now - kvp.Value.CreatedAt > TransferTTL)
                {
                    if (transfers.TryRemove(kvp.Key, out var st))
                    {
                        var tempFile = Path.Combine(Config.Instance.TempDir, $"xdm-blob-{kvp.Key}.part");
                        try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                        Log.Debug($"Purged stale blob transfer: {kvp.Key}");
                    }
                }
            }
        }

        private class TransferState
        {
            public string Filename { get; set; } = "";
            public string Mime { get; set; } = "";
            public long TotalSize { get; set; }
            public int TotalChunks { get; set; }
            public string? BlobUrl { get; set; }
            public DateTime CreatedAt { get; set; }
            public int ReceivedChunks { get; set; }
            public object Lock { get; } = new();
        }
    }
}
