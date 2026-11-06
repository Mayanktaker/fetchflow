// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using XDM.Core.Util;
using XDM.Core;
using TraceLog;
using XDM.Core.Clients.Http;
using XDM.Core.MediaProcessor;
using XDM.Core.IO;

namespace XDM.Core.Downloader.Progressive.SingleHttp
{
    public class SingleSourceHTTPDownloader : HTTPDownloaderBase
    {
        private SingleSourceHTTPDownloaderState? state;
        private bool init;
        // Every piece writes into one part file at its absolute offset; assembly is then a move, not a copy
        private bool sharedPartFile;
        public override Uri? PrimaryUrl => this.state?.Url;
        public override bool SharesPieceFile => sharedPartFile;
        private string PartFilePath => Path.Combine(state!.TempDir!, "part-" + Id);

        public SingleSourceHTTPDownloader(SingleSourceHTTPDownloadInfo info, IHttpClient? hc = null,
            AuthenticationInfo? authentication = null, ProxyInfo? proxy = null,
            BaseMediaProcessor mediaProcessor = null, bool convertToMp3 = false)
        {
            Id = Guid.NewGuid().ToString();
            sharedPartFile = true;

            this.state = new SingleSourceHTTPDownloaderState
            {
                Url = new Uri(info.Uri),
                Id = this.Id,
                Cookies = info.Cookies,
                Headers = info.Headers,
                TempDir = Path.Combine(Config.Instance.TempDir, Id),
                Authentication = authentication,
                Proxy = proxy,
                ConvertToMp3 = convertToMp3,
                AudioBitrate = info.AudioBitrate,
                Checksum = info.Checksum,
                SharedPartFile = true
            };

            if (this.state.Headers == null)
            {
                this.CreateDefaultHeaders();
            }

            if (this.state.Authentication == null)
            {
                this.state.Authentication = Helpers.GetAuthenticationInfoFromConfig(this.state.Url);
            }

            this.TargetFileName = FileHelper.SanitizeFileName(info.File);
            this.http = hc;
            this.mediaProcessor = mediaProcessor;
        }

        public SingleSourceHTTPDownloader(string id, IHttpClient? http = null,
            BaseMediaProcessor mediaProcessor = null)
        {
            Id = id;
            cancelFlag = new();
            this.http = http;
            this.mediaProcessor = mediaProcessor;
        }

        public void SetDownloadInfo(SingleSourceHTTPDownloadInfo info)
        {
            this.state!.Url = new Uri(info.Uri);
            this.state.Cookies = info.Cookies;
            this.state.Headers = info.Headers;
            this.SaveState();
        }

        public override void Start()
        {
            Start(true);
        }

        private void Start(bool start)
        {
            if (state?.TempDir == null)
            {
                throw new InvalidOperationException("Temp dir should not be null");
            }
            Directory.CreateDirectory(state.TempDir);
            // No progress is loaded here: a part file left from an unusable state would publish a stale tail
            if (sharedPartFile && File.Exists(PartFilePath))
            {
                File.Delete(PartFilePath);
            }
            ticksAtDownloadStartOrResume = Helpers.TickCount();
            var chunk = new Piece
            {
                Offset = 0,
                Length = -1,
                Downloaded = 0,
                State = SegmentState.NotStarted,
                Id = Guid.NewGuid().ToString()
            };
            pieces[chunk.Id] = chunk;
            init = false;
            grabberDict[chunk.Id] = new PieceGrabber(chunk.Id, this);
            SaveState();
            if (start)
            {
                Log.Debug("SingleSourceHTTPDownloader start");
                OnStarted();
                this.http ??= http = HttpClientFactory.NewHttpClient(Config.Instance.Proxy);
                http.Timeout = TimeSpan.FromSeconds(Config.Instance.NetworkTimeout);
                grabberDict[chunk.Id].Download();
            }
        }

        public override void SaveForLater()
        {
            Start(false);
        }

        public override void Resume()
        {
            new Thread(() =>
            {
                try
                {
                    PrepareForResume();
                    Log.Debug("SingleSourceHTTPDownloader Resume");
                    RestoreState();
                    Directory.CreateDirectory(state.TempDir);
                    if (SharedPartFileMissing())
                    {
                        throw new DownloadException(ErrorCode.DiskError, "Part file is missing: " + PartFilePath);
                    }
                    if (pieces.Count != 0)
                    {
                        OnStarted();
                        Log.Debug("Chunks found: " + pieces.Count);
                        if (this.AllFinished())
                        {
                            this.AssemblePieces();
                            Console.WriteLine("Download finished");
                            base.OnFinished();
                            return;
                        }
                        else
                        {
                            this.http ??= HttpClientFactory.NewHttpClient(Config.Instance.Proxy);
                            http.Timeout = TimeSpan.FromSeconds(Config.Instance.NetworkTimeout);
                            init = true;
                            if (sharedPartFile)
                            {
                                EnsureSharedPartFile(state.FileSize > 0 ? state.FileSize : null);
                            }
                            CreatePiece();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Starting new download");
                        Start();
                    }
                }
                catch (Exception e)
                {
                    Log.Debug(e, e.Message);
                    base.OnFailed(e is DownloadException de ? de.ErrorCode : ErrorCode.Generic, e.Message);
                }
            }).Start();
        }

        // A shared-layout download with progress but no part file has lost its bytes; resuming would publish holes
        private bool SharedPartFileMissing()
        {
            if (!sharedPartFile || File.Exists(PartFilePath)) return false;
            return pieces.Values.Any(p => p.Downloaded > 0);
        }

        protected override void SaveChunkState()
        {
            try
            {
                rwLock.EnterWriteLock();
                if (pieces.Count == 0) return;
                if (sharedPartFile) ForcePartFile();
                TransactedIO.WriteStream("chunks.db", state!.TempDir!, base.ChunkStateToBytes);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        // Fsyncs the part file first: progress saved next must never count bytes the disk does not hold
        private void ForcePartFile()
        {
            if (!File.Exists(PartFilePath)) return;
            try
            {
                using var fs = new FileStream(PartFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                fs.Flush(true);
            }
            catch (IOException e)
            {
                Log.Debug(e, "Unable to flush part file");
            }
        }

        protected override void SaveState()
        {
            state!.SharedPartFile = sharedPartFile;
            DownloadStateIO.Save(state);
            //TransactedIO.WriteStream(Id + ".state", Config.DataDir, s => DownloadStateStore.StateToBytes(state!, s));
            //TransactedIO.WriteBytes(DownloadStateStore.StateToBytes(state), Id + ".state", Config.DataDir);
        }

        public override void RestoreState()
        {
            state = DownloadStateIO.LoadSingleSourceHTTPDownloaderState(Id!);
            sharedPartFile = state.SharedPartFile;
            //if (!TransactedIO.ReadStream(Id + ".state", Config.DataDir, s =>
            //{
            //    state = DownloadStateStore.SingleSourceHTTPDownloaderStateFromBytes(s);
            //}))
            //{
            //    throw new FileNotFoundException(Path.Combine(Config.DataDir, Id + ".state"));
            //}

            //var bytes = TransactedIO.ReadBytes(Id + ".state", Config.DataDir);
            //if (bytes == null)
            //{
            //    throw new FileNotFoundException(Path.Combine(Config.DataDir, Id + ".state"));
            //}

            //state = DownloadStateStore.SingleSourceHTTPDownloaderStateFromBytes(bytes);
            try
            {
                //var chunkBytes = TransactedIO.ReadBytes("chunks.db", state.TempDir);
                //if (chunkBytes == null)
                //{
                //    throw new FileNotFoundException(Path.Combine(state.TempDir, "chunks.json"));
                //}
                //pieces = ChunkStateFromBytes(chunkBytes);
                if (!TransactedIO.ReadStream("chunks.db", state!.TempDir!, s =>
                {
                    pieces = ChunkStateFromBytes(s);
                }))
                {
                    throw new FileNotFoundException(Path.Combine(state.TempDir, "chunks.db"));
                }
                Log.Debug("Total size: " + state.FileSize);
                //foreach (var item in pieces.Keys)
                //{
                //    Log.Debug("Chunk id: " + item + " offset: " + pieces[item].Offset + " downloaded: " +
                //        pieces[item].Downloaded + " length: " + pieces[item].Length + " state: " + pieces[item].State);
                //}
            }
            catch
            {
                // ignored
                Log.Debug("Chunk restore failed");
                state.FileSize = -1;
            }
            TicksAndSizeAtResume();
        }

        public override bool IsFirstRequest(StreamType streamType)
        {
            return !init;
        }

        // A shared part file takes every piece: its bytes are already at their absolute offsets
        public override string GetPieceFile(string pieceId)
        {
            return sharedPartFile ? PartFilePath : base.GetPieceFile(pieceId);
        }

        // Creates the part file (sparse on NTFS) for a new download; an existing one is checked, never grown
        private void EnsureSharedPartFile(long? size)
        {
            var exists = File.Exists(PartFilePath);
            using var fs = new FileStream(PartFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            if (!exists)
            {
                if (!Win32NativeMethods.TryMarkSparse(fs))
                {
                    Log.Debug("Part file is not sparse");
                }
                if (size.HasValue && size.Value > 0)
                {
                    fs.SetLength(size.Value);
                }
                return;
            }
            // Growing a damaged file would hide the damage: the bytes recorded as downloaded would read as zeros
            var recorded = pieces.Values.Select(p => p.Offset + p.Downloaded).DefaultIfEmpty(0).Max();
            if (recorded > 0 && fs.Length < recorded)
            {
                throw new DownloadException(ErrorCode.DiskError,
                    "Part file is shorter than its recorded progress: " + PartFilePath);
            }
        }

        public override void PieceConnected(string pieceId, ProbeResult? result)
        {
            if (this.cancelFlag.IsCancellationRequested) return;
            try
            {
                rwLock.EnterWriteLock();
                if (result != null) //probe result is not null for first request only, for subsequent requests its always null
                {
                    Log.Debug("connected: " + result.ResourceSize + " init...");
                    state.LastModified = result.LastModified;
                    this.totalSize = result.ResourceSize ?? -1;
                    this.resumable = result.Resumable;
                    if (sharedPartFile)
                    {
                        EnsureSharedPartFile(result.ResourceSize);
                    }
                    var piece = this.pieces[pieceId];
                    piece.Length = result.ResourceSize ?? -1;
                    piece.Offset = 0;
                    piece.Downloaded = 0;
                    piece.State = SegmentState.NotStarted;
                    this.init = true;

                    Log.Debug("fileNameFetchMode: " + fileNameFetchMode);
                    Log.Debug("Attachment: " + result.AttachmentName);
                    Log.Debug("ContentType: " + result.ContentType);
                    switch (fileNameFetchMode)
                    {
                        case FileNameFetchMode.FileNameAndExtension:
                            if (result.AttachmentName != null)
                            {
                                this.TargetFileName = FileHelper.SanitizeFileName(result.AttachmentName);
                            }
                            else
                            {
                                this.TargetFileName = FileHelper.GetFileName(
                                        result.FinalUri, result.ContentType);
                            }
                            break;
                        case FileNameFetchMode.ExtensionOnly:
                            var name = string.Empty;
                            if (FileHelper.AddFileExtension(this.TargetFileName, result.ContentType, out name))
                            {
                                this.TargetFileName = name;
                            }
                            break;
                    }

                    state.Url = result.FinalUri;
                    state.FileSize = result.ResourceSize ?? -1;
                    SaveState();
                    SaveChunkState();
                    base.OnProbed();

                    //check if required disk space is available
                    if (result.ResourceSize.HasValue &&
                        result.ResourceSize.Value > 0 &&
                        Helpers.GetFreeSpace(this.state.TempDir, out long freespace) &&
                        freespace < result.ResourceSize.Value)
                    {
                        throw new AssembleFailedException(ErrorCode.DiskError);
                    }
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
            CreatePiece();
        }

        private List<Piece> SortAndValidatePieces()
        {
            var pieces = this.pieces.Select(p => p.Value).ToList();
            pieces.Sort((a, b) =>
            {
                var diff = a.Offset - b.Offset;
                if (diff == 0) return 0;
                return diff > 0 ? 1 : -1;
            });
            if (this.cancelFlag.IsCancellationRequested) return null;
            if (string.IsNullOrEmpty(this.TargetDir))
            {
                this.TargetDir = FileHelper.GetDownloadFolderByFileName(this.TargetFileName);
            }
            if (!Directory.Exists(this.TargetDir))
            {
                Directory.CreateDirectory(this.TargetDir);
            }
            if (Config.Instance.FileConflictResolution == FileConflictResolution.AutoRename)
            {
                this.TargetFileName = FileHelper.GetUniqueFileName(this.TargetFileName, this.TargetDir);
            }
            return pieces;
        }

        // A copy needs room for the whole file next to the temporary data; a move needs none
        private void EnsureFreeSpaceForCopy()
        {
            if (FileSize > 0 && Helpers.GetFreeSpace(this.TargetDir, out long freespace) && freespace < FileSize)
            {
                throw new AssembleFailedException(ErrorCode.DiskError);
            }
        }

        protected override void AssemblePieces()
        {
            Log.Debug("Assembling...");
            try
            {
                rwLock.EnterWriteLock();
                try
                {
                    var pieces = SortAndValidatePieces();
                    if (this.cancelFlag.IsCancellationRequested) return;

                    if (sharedPartFile)
                    {
                        AssembleSharedPartFile(pieces);
                        return;
                    }
                    AssemblePieceFiles(pieces);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Error in AssemblePieces");
                    throw new AssembleFailedException(ex is DownloadException de ? de.ErrorCode : ErrorCode.Generic, ex);
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        // True when the pieces cover the file exactly and each holds all its bytes
        private bool PiecesMakeUpFile(List<Piece> pieces, out long totalBytes)
        {
            totalBytes = 0;
            if (state!.FileSize <= 0)
            {
                foreach (var pc in pieces)
                {
                    totalBytes += pc.Downloaded;
                }
                var single = pieces.Count == 1 && pieces[0].Offset == 0;
                return single;
            }
            var next = 0L;
            foreach (var pc in pieces)
            {
                if (pc.Offset != next || pc.Downloaded < pc.Length) return false;
                next += pc.Length;
            }
            totalBytes = next;
            return next == state.FileSize;
        }

        // The part file already holds every byte at its offset: rename it into place, copy only across volumes
        private void AssembleSharedPartFile(List<Piece> pieces)
        {
            if (!PiecesMakeUpFile(pieces, out var totalBytes))
            {
                Log.Debug("Part file pieces do not make up the file");
                throw new AssembleFailedException(ErrorCode.Generic);
            }
            var partFile = PartFilePath;
            if (!File.Exists(partFile) || new FileInfo(partFile).Length < totalBytes)
            {
                Log.Debug("Part file is missing or short: " + partFile);
                throw new AssembleFailedException(ErrorCode.DiskError);
            }
            if (this.cancelFlag.IsCancellationRequested) return;

            if (state!.ConvertToMp3)
            {
                EnsureFreeSpaceForCopy();
                totalBytes = ConvertToMp3(partFile);
            }
            else
            {
                this.OnAssembleProgressChanged(100);
                var targetFile = this.TargetFile!;
                try
                {
                    if (File.Exists(targetFile)) File.Delete(targetFile);
                    File.Move(partFile, targetFile);
                }
                catch (IOException moveError)
                {
                    // Temp folder on another volume (or a move the OS refused): copy the bytes instead
                    Log.Debug(moveError, "Part file move failed, copying instead");
                    EnsureFreeSpaceForCopy();
                    CopyToTarget(partFile, targetFile, totalBytes);
                    if (this.cancelFlag.IsCancellationRequested) return;
                }
            }
            Log.Debug("Deleting file parts");
            DeleteFileParts();
            FinishAssembled(totalBytes);
        }

        // Converts a downloaded audio file to MP3, reporting progress in the upper half of the bar
        private long ConvertToMp3(string source)
        {
            if (mediaProcessor == null) throw new AssembleFailedException(ErrorCode.Generic);
            mediaProcessor.ProgressChanged += (s, e) =>
            {
                var prg = 50 + e.Progress / 2;
                if (prg > 100) prg = 100;
                this.OnAssembleProgressChanged(prg);
            };
            var res = mediaProcessor.ConvertToMp3Audio(source, TargetFile!, this.cancelFlag,
                out long totalBytes, state!.AudioBitrate);
            if (res != MediaProcessingResult.Success)
            {
                throw new AssembleFailedException(
                    res == MediaProcessingResult.AppNotFound ? ErrorCode.FFmpegNotFound : ErrorCode.FFmpegError);
            }
            this.totalSize = totalBytes;
            return totalBytes;
        }

        // Copies the part file to the target, reporting progress, then removes the temporary data
        private void CopyToTarget(string partFile, string targetFile, long totalBytes)
        {
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(5 * 1024 * 1024);
            var copied = 0L;
            try
            {
                using var infs = new FileStream(partFile, FileMode.Open, FileAccess.Read);
                using var outfs = new FileStream(targetFile, FileMode.Create, FileAccess.Write);
                while (copied < totalBytes)
                {
                    if (this.cancelFlag.IsCancellationRequested) return;
                    var x = infs.Read(buffer, 0, (int)Math.Min(buffer.Length, totalBytes - copied));
                    if (x == 0)
                    {
                        Log.Debug("EOF :: File corrupted");
                        throw new AssembleFailedException(ErrorCode.Generic);
                    }
                    try
                    {
                        outfs.Write(buffer, 0, x);
                    }
                    catch (IOException ioe)
                    {
                        Log.Debug(ioe, "CopyToTarget");
                        throw new AssembleFailedException(ErrorCode.DiskError, ioe);
                    }
                    copied += x;
                    this.OnAssembleProgressChanged((int)(copied * 100 / totalBytes));
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // The old layout: a file per piece, concatenated into the target in offset order
        private void AssemblePieceFiles(List<Piece> pieces)
        {
            if (this.cancelFlag.IsCancellationRequested) return;
            EnsureFreeSpaceForCopy();

            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(5 * 1024 * 1024);
            var totalBytes = 0L;
            var outFile = state!.ConvertToMp3 ? Path.Combine(this.GetState().TempDir!, Guid.NewGuid().ToString())
                : this.TargetFile;
            try
            {
                // Closed before the MP3 conversion reads it or DeleteFileParts removes it (Windows)
                using (var outfs = new FileStream(outFile!, FileMode.Create, FileAccess.Write))
                {
                    foreach (var pc in pieces)
                    {
                        if (this.cancelFlag.IsCancellationRequested) return;
                        using var infs = new FileStream(GetPieceFile(pc.Id), FileMode.Open, FileAccess.Read);
                        var len = pc.Length;
                        if (this.FileSize < 1)
                        {
                            while (!this.cancelFlag.IsCancellationRequested)
                            {
                                var x = infs.Read(buffer, 0, buffer.Length);
                                if (x == 0)
                                {
                                    break;
                                }
                                try
                                {
                                    outfs.Write(buffer, 0, x);
                                }
                                catch (IOException ioe)
                                {
                                    Log.Debug(ioe, "AssemblePieces");
                                    throw new AssembleFailedException(ErrorCode.DiskError, ioe);
                                }
                                totalBytes += x;
                            }
                        }
                        else
                        {
                            while (len > 0)
                            {
                                if (this.cancelFlag.IsCancellationRequested) return;
                                var x = infs.Read(buffer, 0, (int)Math.Min(buffer.Length, len));
                                if (x == 0)
                                {
                                    Log.Debug("EOF :: File corrupted");
                                    throw new Exception("EOF :: File corrupted");
                                }
                                try
                                {
                                    outfs.Write(buffer, 0, x);
                                }
                                catch (IOException ioe)
                                {
                                    Log.Debug(ioe, "AssemblePieces");
                                    throw new AssembleFailedException(ErrorCode.DiskError, ioe);
                                }
                                len -= x;
                                totalBytes += x;
                                var prg = (int)(totalBytes * 100 / FileSize);
                                if (state!.ConvertToMp3) prg /= 2;
                                this.OnAssembleProgressChanged(prg);
                            }
                        }
                    }
                }

                if (this.cancelFlag.IsCancellationRequested) return;
                if (state!.ConvertToMp3)
                {
                    totalBytes = ConvertToMp3(outFile!);
                }
                Log.Debug("Deleting file parts");
                DeleteFileParts();
                FinishAssembled(totalBytes);
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // Sets the timestamp and size after the bytes are in the target
        private void FinishAssembled(long totalBytes)
        {
            // A short file must never leave as a successful download (an MP3 conversion changes the size)
            if (state!.FileSize > 0 && !state.ConvertToMp3 && totalBytes != state.FileSize)
            {
                Log.Debug("Assembled size mismatch: expected " + state.FileSize + ", got " + totalBytes);
                throw new AssembleFailedException(ErrorCode.InvalidResponse);
            }
            // A digest the user supplied is the only proof the bytes are right
            if (!string.IsNullOrWhiteSpace(state.Checksum))
            {
                VerifyChecksum(state.Checksum!);
            }
            if (this.totalSize < 1)
            {
                this.totalSize = totalBytes;
            }
            if (Config.Instance.FetchServerTimeStamp)
            {
                try
                {
                    File.SetLastWriteTime(TargetFile, state.LastModified);
                }
                catch { }
            }
        }

        // Fails the download when the assembled file does not match the expected digest
        private void VerifyChecksum(string expected)
        {
            try
            {
                var computed = ChecksumHelper.ComputeHashesAsync(TargetFile!).GetAwaiter().GetResult();
                var match = ChecksumHelper.CompareHash(expected, computed);
                Log.Debug($"Checksum {match.Status} ({match.MatchedAlgorithm}) for {TargetFile}");
                if (match.Status == ChecksumMatchStatus.Mismatch)
                {
                    // The bytes stay on disk on purpose: a mistyped hash must not delete a good file
                    throw new AssembleFailedException(ErrorCode.ChecksumMismatch,
                        "Checksum mismatch: expected " + expected);
                }
            }
            catch (AssembleFailedException) { throw; }
            catch (Exception ex)
            {
                Log.Debug(ex, "Checksum could not be verified");
                throw new AssembleFailedException(ErrorCode.ChecksumMismatch, ex);
            }
        }

        public override HeaderData? GetHeaderUrlAndCookies(string pieceId)
        {
            if (this.grabberDict.ContainsKey(pieceId) && pieces.ContainsKey(pieceId))
            {
                return
                    new HeaderData
                    {
                        Headers = this.state!.Headers,
                        Cookies = this.state!.Cookies,
                        Url = this.state!.Url,
                        Authentication = this.state!.Authentication,
                        Proxy = this.state!.Proxy
                    };
            }
            return null;
        }

        protected override BaseHTTPDownloaderState GetState()
        {
            return this.state;
        }

        public override bool IsTextRedirectionAllowed() { return false; }

        public override bool IsFileChangedOnServer(StreamType streamType, long streamSize, DateTime? lastModified)
        {
            if (state!.FileSize > 0)
            {
                return state!.FileSize != streamSize;
            }
            return false;
        }

        private void CreateDefaultHeaders()
        {
            this.state!.Headers = new Dictionary<string, List<string>>
            {
                ["User-Agent"] = new() { Config.Instance.FallbackUserAgent },
                ["Accept"] = new() { "*/*", },
                ["Accept-Encoding"] = new() { "identity", },
                ["Accept-Language"] = new() { "en-US", },
                ["Accept-Charset"] = new() { "*", },
                ["Referer"] = new() { new Uri(this.state.Url, ".").ToString() }
            };
        }
    }

    public class SingleSourceHTTPDownloaderState : BaseHTTPDownloaderState
    {
        public Uri Url;
        public Dictionary<string, List<string>> Headers;
        public string Cookies;
        public bool ConvertToMp3;
        public string? AudioBitrate;
        // Progress lives in one part file written at absolute offsets; false for downloads from older versions
        public bool SharedPartFile;
        // Optional expected digest, checked once the file is assembled
        public string? Checksum;
    }
}
