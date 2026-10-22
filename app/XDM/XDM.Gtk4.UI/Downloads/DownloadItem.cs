// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// One row in the GTK 4 download list. Plain managed model: the list widget maps
// ListItem.Position back to this object, so no GObject subclassing is required.
using System;
using XDM.Core.UI;

namespace XDM.Gtk4UI.Downloads
{
    internal sealed class DownloadItem
    {
        // File name shown on the primary line
        public string Name { get; init; } = "";
        // Status line ("Downloading · 4.2 MB/s", "Paused", "Complete")
        public string Status { get; init; } = "";
        // Human-readable size ("128.4 MB")
        public string SizeText { get; init; } = "";
        // Raw byte size for sorting
        public long RawSize { get; init; }
        // 0..1 transfer progress (1 for finished rows)
        public double Progress { get; init; }
        // Integer progress (0-100)
        public int RawProgress { get; init; }
        // True once the transfer is finished (drives the progress column)
        public bool IsComplete { get; init; }
        // Creation or finish date for sorting and finished meta display
        public DateTime DateAdded { get; init; }
        // Live download transfer speed ("4.2 MB/s")
        public string DownloadSpeed { get; init; } = "";
        // Live estimated time of arrival ("00:02:15")
        public string ETA { get; init; } = "";
        // Original core row when this entry belongs to the live UI model
        public IInProgressDownloadRow? ProgressRow { get; init; }
        // Original core row for a completed entry
        public IFinishedDownloadRow? FinishedRow { get; init; }

        // Case-insensitive match used by the toolbar search filter
        public bool Matches(string keyword) =>
            string.IsNullOrWhiteSpace(keyword) ||
            Name.Contains(keyword, System.StringComparison.OrdinalIgnoreCase);
    }
}
