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
        // Secondary line: the source site (plus the extension for finished rows)
        public string Source { get; init; } = "";
        // Tertiary line: the destination folder
        public string Location { get; init; } = "";
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

        // Live speed in bytes per second, parsed from the formatted "4.2 MB/s" text
        public double SpeedBytesPerSecond => ParseSpeed(DownloadSpeed);

        // Parses a human speed string ("900 KB/s", "4.2 MB/s", "1,5 GB/s") into bytes
        public static double ParseSpeed(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            var value = text.Trim();
            var cut = value.IndexOfAny(new[] { ' ', '/' });
            if (cut < 0)
            {
                return 0;
            }

            var number = value.Substring(0, cut).Replace(',', '.');
            if (!double.TryParse(number, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                return 0;
            }

            var unit = value.Substring(cut).Trim().TrimStart('/').ToUpperInvariant();
            var factor = unit switch
            {
                "KB/S" or "KB" => 1024d,
                "MB/S" or "MB" => 1024d * 1024,
                "GB/S" or "GB" => 1024d * 1024 * 1024,
                "TB/S" or "TB" => 1024d * 1024 * 1024 * 1024,
                _ => 1d
            };
            return amount * factor;
        }

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
