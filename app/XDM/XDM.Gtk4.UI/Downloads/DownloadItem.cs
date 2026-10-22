// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// One row in the GTK 4 download list. Plain managed model: the list widget maps
// ListItem.Position back to this object, so no GObject subclassing is required.
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
        // 0..1 transfer progress (1 for finished rows)
        public double Progress { get; init; }
        // True once the transfer is finished (drives the progress column)
        public bool IsComplete { get; init; }

        // Case-insensitive match used by the toolbar search filter
        public bool Matches(string keyword) =>
            string.IsNullOrWhiteSpace(keyword) ||
            Name.Contains(keyword, System.StringComparison.OrdinalIgnoreCase);
    }
}
