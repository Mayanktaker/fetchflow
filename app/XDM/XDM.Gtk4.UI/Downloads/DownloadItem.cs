// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// One row in the GTK 4 download list. Plain managed model: the list widget binds
// each row by its stable Id (the StringObject string), so no GObject subclassing
// is required and rows survive sorting, filtering and in-place progress updates.
using System;
using XDM.Core.UI;

namespace XDM.Gtk4UI.Downloads
{
    internal sealed class DownloadItem
    {
        // Stable transfer id: the row widget resolves this back to the item
        public string Id { get; init; } = "";
        // File name shown on the primary line
        public string Name { get; init; } = "";
        // Status line ("Downloading · 4.2 MB/s", "Paused", "Complete")
        public string Status { get; set; } = "";
        // Human-readable size ("128.4 MB")
        public string SizeText { get; init; } = "";
        // Raw byte size for sorting
        public long RawSize { get; init; }
        // 0..1 transfer progress (1 for finished rows)
        public double Progress { get; set; }
        // Integer progress (0-100)
        public int RawProgress { get; set; }
        // True once the transfer is finished (drives the progress column)
        public bool IsComplete { get; init; }
        // Creation or finish date for sorting and finished meta display
        public DateTime DateAdded { get; init; }
        // Live download transfer speed ("4.2 MB/s")
        public string DownloadSpeed { get; set; } = "";
        // Live estimated time of arrival ("00:02:15")
        public string ETA { get; set; } = "";
        // Original core row when this entry belongs to the live UI model
        public IInProgressDownloadRow? ProgressRow { get; init; }
        // Original core row for a completed entry
        public IFinishedDownloadRow? FinishedRow { get; init; }

        // Case-insensitive match used by the toolbar search filter
        public bool Matches(string keyword) =>
            string.IsNullOrWhiteSpace(keyword) ||
            Name.Contains(keyword, StringComparison.OrdinalIgnoreCase);

        // Copies live transfer state from the core row into this projection
        public void CopyLiveStateFrom(IInProgressDownloadRow row)
        {
            Status = row.Status + FormatSuffix(row);
            Progress = Math.Clamp(row.Progress / 100.0, 0.0, 1.0);
            RawProgress = row.Progress;
            DownloadSpeed = row.DownloadSpeed ?? "";
            ETA = row.ETA ?? "";
        }

        // Appends the live speed or ETA to the status text the way the row shows it
        private static string FormatSuffix(IInProgressDownloadRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.DownloadSpeed))
            {
                return " · " + row.DownloadSpeed;
            }
            return string.IsNullOrWhiteSpace(row.ETA) ? "" : " · " + row.ETA;
        }
    }
}
