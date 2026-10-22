// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 adapters for the small core UI contracts used by Application. These
// keep Gtk types at the frontend seam while preserving the original core row.
using System;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Core.UI;
using Gtk;

namespace XDM.Gtk4UI
{
    internal sealed class Gtk4InProgressRow : IInProgressDownloadRow
    {
        // Core row remains the single source of mutable transfer state
        public InProgressDownloadItem DownloadEntry { get; }
        public string FileIconText => "file";
        public string Name { get => DownloadEntry.Name; set => DownloadEntry.Name = value; }
        public long Size { get => DownloadEntry.Size; set => DownloadEntry.Size = value; }
        public DateTime DateAdded { get => DownloadEntry.DateAdded; set => DownloadEntry.DateAdded = value; }
        public int Progress { get => DownloadEntry.Progress; set => DownloadEntry.Progress = value; }
        public DownloadStatus Status { get => DownloadEntry.Status; set => DownloadEntry.Status = value; }
        public string DownloadSpeed { get => DownloadEntry.DownloadSpeed; set => DownloadEntry.DownloadSpeed = value; }
        public string ETA { get => DownloadEntry.ETA; set => DownloadEntry.ETA = value; }

        // Wraps the transfer row without copying its mutable fields
        public Gtk4InProgressRow(InProgressDownloadItem entry)
        {
            DownloadEntry = entry;
        }
    }

    internal sealed class Gtk4FinishedRow : IFinishedDownloadRow
    {
        // Finished core row remains the source for file actions
        public FinishedDownloadItem DownloadEntry { get; }
        public string FileIconText => "file";
        public string Name => DownloadEntry.Name;
        public long Size => DownloadEntry.Size;
        public DateTime DateAdded => DownloadEntry.DateAdded;

        // Wraps a completed row
        public Gtk4FinishedRow(FinishedDownloadItem entry)
        {
            DownloadEntry = entry;
        }
    }

    internal sealed class Gtk4ButtonAdapter : IButton
    {
        // GTK widget whose state mirrors the core toolbar state
        private readonly Button button;

        public bool Visible { get => button.Visible; set => button.Visible = value; }
        public bool Enable { get => button.Sensitive; set => button.Sensitive = value; }
        public event EventHandler? Clicked;

        // Wraps a real GTK 4 button and forwards click notifications
        public Gtk4ButtonAdapter(Button button)
        {
            this.button = button;
            button.OnClicked += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        }
    }

    internal sealed class Gtk4MenuItemAdapter : IMenuItem
    {
        // Stable key consumed by Application's existing context-menu hooks
        public string Name { get; }
        public bool Enabled { get; set; } = true;
        public event EventHandler? Clicked;

        // Creates a no-op compatibility item for menus outside this vertical slice
        public Gtk4MenuItemAdapter(string name)
        {
            Name = name;
        }

        // Leaves a future menu implementation a single event seam
        public void Activate() => Clicked?.Invoke(this, EventArgs.Empty);
    }
}
