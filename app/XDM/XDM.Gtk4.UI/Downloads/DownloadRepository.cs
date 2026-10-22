// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Read-only bridge to the download database the GTK 3 frontend maintains.
// The GTK 4 frontend does not own the download engine yet, so this reads the
// same SQLite store with a read-only connection: no writes, no schema changes,
// no lock contention with a running FetchFlow instance.
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using XDM.Core;
using XDM.Core.DataAccess;
using XDM.Core.Util;

namespace XDM.Gtk4UI.Downloads
{
    internal static class DownloadRepository
    {
        // Database file name (same as XDM.Core.Application uses)
        private const string DbFileName = "downloads.db";

        // Loads every stored download as a display row; empty when the DB is absent
        public static IReadOnlyList<DownloadItem> LoadAll()
        {
            var path = Path.Combine(Config.AppDir, DbFileName);
            if (!File.Exists(path))
            {
                return Array.Empty<DownloadItem>();
            }

            try
            {
                using var connection = new SQLiteConnection($"Data Source={path};Read Only=True;");
                connection.Open();
                var list = new DownloadList(connection);
                if (!list.LoadDownloads(out var inProgress, out var finished))
                {
                    return Array.Empty<DownloadItem>();
                }

                var rows = new List<DownloadItem>(inProgress.Count + finished.Count);
                foreach (var entry in inProgress)
                {
                    rows.Add(new DownloadItem
                    {
                        Name = entry.Name ?? "",
                        Status = "Paused",
                        SizeText = FormattingHelper.FormatSize(entry.Size),
                        Progress = Math.Clamp(entry.Progress / 100.0, 0.0, 1.0)
                    });
                }
                foreach (var entry in finished)
                {
                    rows.Add(new DownloadItem
                    {
                        Name = entry.Name ?? "",
                        Status = "Complete",
                        SizeText = FormattingHelper.FormatSize(entry.Size),
                        Progress = 1.0,
                        IsComplete = true
                    });
                }
                return rows;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Download list read failed: " + ex.Message);
                return Array.Empty<DownloadItem>();
            }
        }
    }
}
