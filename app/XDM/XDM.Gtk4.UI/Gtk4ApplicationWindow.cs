// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Adapter from the GTK 4 widgets to the existing IApplicationWindow seam.
// Full implementation of vertical slice actions: add, progress, complete, delete.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.DataAccess;
using XDM.Core.Downloader;
using XDM.Core.UI;
using XDM.Gtk4UI.Dialogs;
using XDM.Gtk4UI.Downloads;

namespace XDM.Gtk4UI
{
    // Bridge connecting GTK 4 MainWindow to core IApplicationWindow lifecycle
    internal sealed class Gtk4ApplicationWindow : IApplicationWindow
    {
        private readonly MainWindow window;
        private readonly SynchronizationContext uiContext;
        private readonly int uiThreadId;
        private readonly Gtk4ButtonAdapter newButton;
        private readonly Gtk4ButtonAdapter deleteButton;
        private readonly Gtk4ButtonAdapter pauseButton;
        private readonly Gtk4ButtonAdapter resumeButton;
        private readonly Gtk4ButtonAdapter openFileButton;
        private readonly Gtk4ButtonAdapter openFolderButton;
        private readonly Gtk4MenuItemAdapter[] menuItems;
        private readonly Dictionary<string, IMenuItem> menuItemMap;
        private readonly Gtk4ClipboardMonitor clipboardMonitor = new();
        private readonly List<Gtk4InProgressRow> inProgress = new();
        private readonly List<Gtk4FinishedRow> finished = new();
        private bool inProgressView = true;

        public Gtk4ApplicationWindow(MainWindow window)
        {
            this.window = window;
            uiContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException("GTK 4 host must create the adapter inside the GTK synchronization context.");
            uiThreadId = Environment.CurrentManagedThreadId;
            newButton = new Gtk4ButtonAdapter(window.NewButton);
            deleteButton = new Gtk4ButtonAdapter(window.DeleteButton);
            pauseButton = new Gtk4ButtonAdapter(window.PauseButton);
            resumeButton = new Gtk4ButtonAdapter(window.ResumeButton);
            openFileButton = new Gtk4ButtonAdapter(window.OpenFileButton);
            openFolderButton = new Gtk4ButtonAdapter(window.OpenFolderButton);
            menuItems = new[]
            {
                "pause", "resume", "delete", "saveAs", "refresh", "moveToQueue", "showProgress",
                "copyURL", "copyURL1", "properties", "open", "openFolder", "verifyChecksum",
                "deleteDownloads", "copyFile", "properties1", "downloadAgain", "restart", "schedule"
            }.Select(name => new Gtk4MenuItemAdapter(name)).ToArray();
            menuItemMap = menuItems.ToDictionary(item => item.Name, item => (IMenuItem)item);

            window.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
            window.ViewChanged += OnViewChanged;
            window.RefreshRequested += ReloadFromDatabase;
            window.BrowserMonitoringButtonClicked += (_, _) => BrowserMonitoringButtonClicked?.Invoke(this, EventArgs.Empty);
            window.SchedulerClicked += (_, _) => SchedulerClicked?.Invoke(this, EventArgs.Empty);
            // Header-menu commands forwarded to the core handlers that already exist
            window.ClearAllFinishedClicked += (_, _) => ClearAllFinishedClicked?.Invoke(this, EventArgs.Empty);
            window.UpdateClicked += (_, _) => UpdateClicked?.Invoke(this, EventArgs.Empty);
            window.CheckForUpdateClicked += (_, _) => CheckForUpdateClicked?.Invoke(this, EventArgs.Empty);
            window.HelpClicked += (_, _) => HelpClicked?.Invoke(this, EventArgs.Empty);
            window.BugReportClicked += (_, _) => BugReportClicked?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<FinishedDownloadItem> FinishedDownloads
        {
            get => finished.Select(row => row.DownloadEntry).ToArray();
            set
            {
                finished.Clear();
                finished.AddRange(value.Select(entry => new Gtk4FinishedRow(entry)));
                RenderRows();
            }
        }

        public IEnumerable<InProgressDownloadItem> InProgressDownloads
        {
            get => inProgress.Select(row => row.DownloadEntry).ToArray();
            set
            {
                inProgress.Clear();
                inProgress.AddRange(value.Select(entry => new Gtk4InProgressRow(entry)));
                RenderRows();
            }
        }

        public IInProgressDownloadRow? FindInProgressItem(string id)
        {
            var row = inProgress.FirstOrDefault(row => row.DownloadEntry.Id == id);
            if (row != null)
            {
                // Core mutates this row's status right after finding it (started,
                // cancelled, failed) with no progress tick to follow, so re-sync
                // the visible cells once that mutation has run
                GLib.Functions.IdleAdd(0, () =>
                {
                    window.RefreshRowState(id);
                    return false;
                });
            }
            return row;
        }

        public IFinishedDownloadRow? FindFinishedItem(string id) =>
            finished.FirstOrDefault(row => row.DownloadEntry.Id == id);

        public IList<IInProgressDownloadRow> SelectedInProgressRows => window.GetSelectedItems()
            .Where(item => item.ProgressRow != null).Select(item => item.ProgressRow!).ToList();

        public IList<IFinishedDownloadRow> SelectedFinishedRows => window.GetSelectedItems()
            .Where(item => item.FinishedRow != null).Select(item => item.FinishedRow!).ToList();

        public IButton NewButton => newButton;
        public IButton DeleteButton => deleteButton;
        public IButton PauseButton => pauseButton;
        public IButton ResumeButton => resumeButton;
        public IButton OpenFileButton => openFileButton;
        public IButton OpenFolderButton => openFolderButton;
        public bool IsInProgressViewSelected => inProgressView;
        public IMenuItem[] MenuItems => menuItems.Cast<IMenuItem>().ToArray();
        public Dictionary<string, IMenuItem> MenuItemMap => menuItemMap;

        public event EventHandler<CategoryChangedEventArgs>? CategoryChanged;
        public event EventHandler? InProgressContextMenuOpening;
        public event EventHandler? FinishedContextMenuOpening;
        public event EventHandler? SelectionChanged;
        public event EventHandler? NewDownloadClicked;
        public event EventHandler? YoutubeDLDownloadClicked;
        public event EventHandler? BatchDownloadClicked;
        public event EventHandler? SettingsClicked;
        public event EventHandler? ClearAllFinishedClicked;
        public event EventHandler? ExportClicked;
        public event EventHandler? ImportClicked;

        // Triggers import flow programmatically
        public void TriggerImport() => ImportClicked?.Invoke(this, EventArgs.Empty);

        // Triggers export flow programmatically
        public void TriggerExport() => ExportClicked?.Invoke(this, EventArgs.Empty);

        // Triggers queue scheduler window programmatically
        public void TriggerScheduler() => SchedulerClicked?.Invoke(this, EventArgs.Empty);

        // Triggers batch download window programmatically
        public void TriggerBatchDownload() => BatchDownloadClicked?.Invoke(this, EventArgs.Empty);
        public event EventHandler? BrowserMonitoringButtonClicked;
        public event EventHandler? BrowserMonitoringSettingsClicked;
        public event EventHandler? UpdateClicked;
        public event EventHandler? HelpClicked;
        public event EventHandler? SupportPageClicked;
        public event EventHandler? BugReportClicked;
        public event EventHandler? CheckForUpdateClicked;
        public event EventHandler? SchedulerClicked;
        public event EventHandler? DownloadListDoubleClicked;
        public event EventHandler? WindowCreated;

        public void AddToTop(InProgressDownloadItem entry)
        {
            inProgress.RemoveAll(row => row.DownloadEntry.Id == entry.Id);
            inProgress.Insert(0, new Gtk4InProgressRow(entry));
            RenderRows();
        }

        public void AddToTop(FinishedDownloadItem entry)
        {
            finished.RemoveAll(row => row.DownloadEntry.Id == entry.Id);
            finished.Insert(0, new Gtk4FinishedRow(entry));
            RenderRows();
        }

        public void SwitchToInProgressView() => SelectView(MainWindow.InProgressKey, true);
        public void SwitchToFinishedView() => SelectView(MainWindow.FinishedKey, false);
        public void ClearInProgressViewSelection() => window.ClearSelection();
        public void ClearFinishedViewSelection() => window.ClearSelection();

        // Shows modal GTK 4 confirmation dialog and blocks until user responds
        public bool Confirm(object? parent, string text) =>
            Gtk4DialogHelper.Confirm((parent as Window) ?? window.Window, text);

        // Prompts user with options to delete from list or disk
        public void ConfirmDelete(string text, out bool approved, out bool deleteFiles) =>
            Gtk4DialogHelper.ConfirmDelete(window.Window, text, out approved, out deleteFiles);

        public void RunOnUIThread(Action action) => Post(action);

        // Progress ticks mutate the core row and refresh that row's cells in place.
        // Repaints are throttled per transfer so a fast download cannot rebuild the
        // list (or clear the selection) on every event.
        public void RunOnUIThread(Action<string, int, double, long> action, string id, int progress, double speed, long eta) =>
            Post(() =>
            {
                action(id, progress, speed, eta);
                if (window.ShouldRepaintProgress(id, progress))
                {
                    window.UpdateProgressRow(id);
                }
            });

        public void Delete(IInProgressDownloadRow row)
        {
            inProgress.RemoveAll(item => item.DownloadEntry.Id == row.DownloadEntry.Id);
            RenderRows();
        }

        public void Delete(IFinishedDownloadRow row)
        {
            finished.RemoveAll(item => item.DownloadEntry.Id == row.DownloadEntry.Id);
            RenderRows();
        }

        public void Delete(IEnumerable<IInProgressDownloadRow> rows)
        {
            var ids = new HashSet<string>(rows.Select(row => row.DownloadEntry.Id));
            inProgress.RemoveAll(row => ids.Contains(row.DownloadEntry.Id));
            RenderRows();
        }

        public void Delete(IEnumerable<IFinishedDownloadRow> rows)
        {
            var ids = new HashSet<string>(rows.Select(row => row.DownloadEntry.Id));
            finished.RemoveAll(row => ids.Contains(row.DownloadEntry.Id));
            RenderRows();
        }

        public void DeleteAllFinishedDownloads()
        {
            finished.Clear();
            RenderRows();
        }

        public string? GetUrlFromClipboard() => clipboardMonitor.GetClipboardText();
        // Update-available state drives the header dot, not a status-bar string
        public void ShowUpdateAvailableNotification() => window.ShowUpdateDot(true);
        public void OpenNewDownloadMenu() => NewDownloadClicked?.Invoke(this, EventArgs.Empty);
        public void SetClipboardText(string text) => clipboardMonitor.SetClipboardText(text);
        public void SetClipboardFile(string file) => clipboardMonitor.SetClipboardText(file);
        public void UpdateBrowserMonitorButton() => window.RefreshBrowserMonitorButton();
        public void ClearUpdateInformation() => window.ShowUpdateDot(false);
        public IPlatformClipboardMonitor GetClipboardMonitor() => clipboardMonitor;
        public void ShowAndActivate() => window.ShowAndActivate();

        // Raises the core WindowLoaded event after context setup is complete
        public void NotifyWindowCreated() => WindowCreated?.Invoke(this, EventArgs.Empty);

        // Switches the list and updates the core's view-state flag
        private void SelectView(string key, bool isInProgress)
        {
            inProgressView = isInProgress;
            window.SelectView(key);
        }

        // Mirrors current core rows into the GTK 4 list projection
        private void RenderRows()
        {
            var items = inProgress.Select(ToItem).Concat(finished.Select(ToItem));
            window.SetRows(items, inProgressView ? MainWindow.InProgressKey : MainWindow.FinishedKey);
        }

        // Projects an in-progress row to GTK 4 list data
        private static DownloadItem ToItem(Gtk4InProgressRow row) => MainWindow.ToItem(row);

        // Projects a finished row to GTK 4 list data
        private static DownloadItem ToItem(Gtk4FinishedRow row) => MainWindow.ToItem(row);

        // Applies sidebar selection to the core's view and category event
        private void OnViewChanged(string key)
        {
            inProgressView = key == MainWindow.InProgressKey;
            var category = Config.Instance.Categories.FirstOrDefault(item => item.Name == key);
            CategoryChanged?.Invoke(this, new CategoryChangedEventArgs
            {
                Level = key == MainWindow.InProgressKey ? 0 : key == MainWindow.FinishedKey ? 1 : 2,
                Index = 0,
                Category = string.IsNullOrEmpty(category.Name) ? null : category
            });
        }

        // Reloads the core-owned SQLite data, retaining the selected view
        private void ReloadFromDatabase()
        {
            if (!AppDB.Instance.Downloads.LoadDownloads(out var active, out var complete))
            {
                return;
            }
            inProgress.Clear();
            inProgress.AddRange(active.Select(entry => new Gtk4InProgressRow(entry)));
            finished.Clear();
            finished.AddRange(complete.Select(entry => new Gtk4FinishedRow(entry)));
            RenderRows();
        }

        // Runs immediately on the GTK thread, otherwise posts to GLib's main context
        private void Post(Action action)
        {
            if (Environment.CurrentManagedThreadId == uiThreadId)
            {
                try { action(); }
                catch (Exception ex) { TraceLog.Log.Debug(ex, "RunOnUIThread error: " + ex.Message); }
                return;
            }
            GLib.Functions.IdleAdd(0, () =>
            {
                try { action(); }
                catch (Exception ex) { TraceLog.Log.Debug(ex, "RunOnUIThread error: " + ex.Message); }
                return false;
            });
        }
    }

    // GTK 4 native clipboard integration using Gdk.Clipboard
    internal sealed class Gtk4ClipboardMonitor : IPlatformClipboardMonitor
    {
        private readonly Gdk.Clipboard? clipboard;
        private string? lastText;
        private bool isMonitoring;

        public event EventHandler? ClipboardChanged;

        public Gtk4ClipboardMonitor()
        {
            try
            {
                clipboard = Gdk.Display.GetDefault()?.GetClipboard();
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Failed to initialize Gdk.Clipboard: " + ex.Message);
            }
        }

        // Starts listening to clipboard change notifications
        public void StartClipboardMonitoring()
        {
            if (isMonitoring || clipboard == null) return;
            isMonitoring = true;
            clipboard.OnChanged += OnClipboardChanged;
        }

        // Stops listening to clipboard change notifications
        public void StopClipboardMonitoring()
        {
            if (!isMonitoring || clipboard == null) return;
            isMonitoring = false;
            clipboard.OnChanged -= OnClipboardChanged;
        }

        // Returns the cached clipboard URL or text
        public string? GetClipboardText() => lastText;

        // Sets text on the platform clipboard
        public void SetClipboardText(string text)
        {
            lastText = text;
            clipboard?.SetText(text);
        }

        // Handles clipboard changes and fires ClipboardChanged event
        private void OnClipboardChanged(Gdk.Clipboard sender, EventArgs args)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var text = await sender.ReadTextAsync();
                    if (!string.IsNullOrWhiteSpace(text) && text != lastText)
                    {
                        lastText = text;
                        ClipboardChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    TraceLog.Log.Debug(ex, "Failed to read clipboard text: " + ex.Message);
                }
            });
        }
    }
}
