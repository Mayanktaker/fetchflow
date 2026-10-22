// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 platform UI service providing dialogs, notifications and portals to core.
using System;
using System.Collections.Generic;
using System.IO;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Core.UI;
using XDM.Core.Util;
using XDM.Gtk4UI.Dialogs;
using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI
{
    // Native GTK 4 implementation of IPlatformUIService
    internal sealed class Gtk4PlatformUIService : IPlatformUIService
    {
        private readonly MainWindow mainWindow;

        public Gtk4PlatformUIService(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        // Creates the native download completion dialog
        public IDownloadCompleteDialog CreateDownloadCompleteDialog() =>
            new DownloadCompleteDialog(mainWindow.Window);

        // Creates the native new-download URL entry dialog
        public INewDownloadDialog CreateNewDownloadDialog(bool empty) =>
            new NewDownloadDialog(mainWindow.Window, empty);

        // Creates the native progress dialog tracking an active transfer
        public IProgressWindow CreateProgressWindow(string downloadId) =>
            new ProgressWindow(mainWindow.Window, downloadId);

        // Displays a modal alert dialog with OK button
        public void ShowMessageBox(object? window, string message)
        {
            var parent = (window as Window) ?? mainWindow.Window;
            Gtk4DialogHelper.ShowAlert(parent, message);
        }

        // Sends user-configured desktop notification
        public void ShowDesktopNotification(string message)
        {
            if (!Config.Instance.ShowNotification) return;
            DesktopNotificationHelper.Show("FetchFlow", message);
        }

        // Sends media capture notification
        public void ShowMediaNotification() =>
            DesktopNotificationHelper.Show("FetchFlow", TextResource.GetText("MSG_VID_CAP") ?? "Video download captured");

        // Opens native GTK 4 / portal file save dialog synchronously
        public string? SaveFileDialog(string? initialPath, string? defaultExt, string? filter)
        {
            try
            {
                var dialog = FileDialog.New();
                dialog.Title = TextResource.GetText("MENU_SAVE_AS") ?? "Save As";
                dialog.Modal = true;
                if (!string.IsNullOrEmpty(initialPath))
                {
                    dialog.SetInitialName(Path.GetFileName(initialPath));
                    var dir = Path.GetDirectoryName(initialPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        dialog.SetInitialFolder(Gio.FileHelper.NewForPath(dir));
                    }
                }
                string? selected = null;
                var loop = GLib.MainLoop.New(null, false);
                dialog.SaveAsync(mainWindow.Window).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && t.Result != null)
                    {
                        selected = t.Result.GetPath();
                    }
                    loop.Quit();
                });
                loop.Run();
                return selected;
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "SaveFileDialog error: " + ex.Message);
                return null;
            }
        }

        // Opens native GTK 4 / portal file chooser dialog synchronously
        public string? OpenFileDialog(string? initialPath, string? defaultExt, string? filter)
        {
            try
            {
                var dialog = FileDialog.New();
                dialog.Title = TextResource.GetText("CTX_OPEN_FILE") ?? "Select File";
                dialog.Modal = true;
                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                {
                    dialog.SetInitialFolder(Gio.FileHelper.NewForPath(initialPath));
                }
                string? selected = null;
                var loop = GLib.MainLoop.New(null, false);
                dialog.OpenAsync(mainWindow.Window).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && t.Result != null)
                    {
                        selected = t.Result.GetPath();
                    }
                    loop.Quit();
                });
                loop.Run();
                return selected;
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "OpenFileDialog error: " + ex.Message);
                return null;
            }
        }

        // Prompts credentials dialog (stubbed until credentials dialog is ported)
        public AuthenticationInfo? PromtForCredentials(object window, string message) => null;

        // Shows link refresher dialog (stubbed for Step 6.2)
        public void ShowRefreshLinkDialog(InProgressDownloadItem entry) =>
            ShowMessageBox(mainWindow.Window, "Link refresher is scheduled for GTK 4 parity in Step 6.2.");

        // Displays download properties
        public void ShowPropertiesDialog(DownloadItemBase ent, string cookies, Dictionary<string, List<string>> headers)
        {
            var dlg = new PropertiesDialog(mainWindow.Window, ent, cookies, headers);
            dlg.Show();
        }

        // Shows YouTube downloader (stubbed for Step 6.2)
        public void ShowYoutubeDLDialog() =>
            ShowMessageBox(mainWindow.Window, "YouTube stream grabber is scheduled for GTK 4 parity in Step 6.2.");

        // Shows batch downloader (stubbed for Step 6.2)
        public void ShowBatchDownloadWindow() =>
            ShowMessageBox(mainWindow.Window, "Batch downloader is scheduled for GTK 4 parity in Step 6.2.");

        // Displays settings window
        public void ShowSettingsDialog(int page = 0)
        {
            var dlg = new SettingsDialog(mainWindow.Window, page);
            dlg.Show();
        }

        // Displays browser monitoring settings
        public void ShowBrowserMonitoringDialog() =>
            ShowSettingsDialog(2);

        // Displays speed limiter dialog (stubbed for Step 6.2)
        public void ShowSpeedLimiterWindow() =>
            ShowMessageBox(mainWindow.Window, "Speed limiter dialog is scheduled for GTK 4 parity in Step 6.2.");

        // Creates and shows media grabber popup (stubbed for Step 6.2)
        public void CreateAndShowMediaGrabber() =>
            ShowMessageBox(mainWindow.Window, "Media grabber is scheduled for GTK 4 parity in Step 6.2.");

        // Shows browser extension registration window (stubbed for Step 6.2)
        public void ShowExtensionRegistrationWindow() =>
            ShowMessageBox(mainWindow.Window, "Extension registration is scheduled for GTK 4 parity in Step 6.2.");

        // Displays checksum verification dialog
        public void ShowChecksumDialog(object? window, string filePath)
        {
            var parent = (window as Window) ?? mainWindow.Window;
            var dlg = new ChecksumDialog(parent, filePath);
            dlg.Show();
        }

        // Displays download selection dialog (stubbed for Step 6.2)
        public void ShowDownloadSelectionWindow(FileNameFetchMode mode, IEnumerable<IRequestData> downloads) =>
            ShowMessageBox(mainWindow.Window, "Download selection dialog is scheduled for GTK 4 parity in Step 6.2.");

        // Creates video download dialog stub
        public INewVideoDownloadDialog CreateNewVideoDialog() =>
            new Gtk4NewVideoDownloadDialogStub(mainWindow.Window);

        // Creates queue scheduler window stub
        public IQueuesWindow CreateQueuesAndSchedulerWindow() =>
            new Gtk4QueuesWindowStub();

        // Creates queue selection dialog stub
        public IQueueSelectionDialog CreateQueueSelectionDialog() =>
            new Gtk4QueueSelectionDialogStub();

        // Creates updater dialog stub
        public IUpdaterUI CreateUpdateUIDialog() =>
            new Gtk4UpdaterUIStub();
    }

    // Stub implementations for secondary interfaces scheduled for Step 6.2
    internal sealed class Gtk4NewVideoDownloadDialogStub : INewVideoDownloadDialog
    {
        private readonly Window parent;
        public Gtk4NewVideoDownloadDialogStub(Window parent) => this.parent = parent;
        public string FileSize { get; set; } = "---";
        public AuthenticationInfo? Authentication { get; set; }
        public ProxyInfo? Proxy { get; set; }
        public int SpeedLimit { get; set; }
        public bool EnableSpeedLimit { get; set; }
        public bool ShowMp3Checkbox { get; set; }
        public bool IsMp3CheckboxChecked { get; set; }
        public string SelectedFileName { get; set; } = string.Empty;
        public int SeletedFolderIndex { get; set; }
        public event EventHandler? DownloadClicked;
        public event EventHandler? DestroyEvent;
        public event EventHandler? QueueSchedulerClicked;
        public event EventHandler? Mp3CheckChanged;
        public event EventHandler<DownloadLaterEventArgs>? DownloadLaterClicked;
        public event EventHandler<FileBrowsedEventArgs>? FileBrowsedEvent;
        public event EventHandler<FileBrowsedEventArgs>? DropdownSelectionChangedEvent;
        public void DisposeWindow() { }
        public void Invoke(Action callback) => callback();
        public void SetFolderValues(string[] values) { }
        public void ShowMessageBox(string text) { }
        public void ShowWindow() { }
    }

    internal sealed class Gtk4QueuesWindowStub : IQueuesWindow
    {
        public event EventHandler<QueueListEventArgs>? QueuesModified;
        public event EventHandler<DownloadListEventArgs>? QueueStartRequested;
        public event EventHandler<DownloadListEventArgs>? QueueStopRequested;
        public event EventHandler? WindowClosing;
        public void RefreshView() { }
        public void SetData(IEnumerable<DownloadQueue> queues) { }
        public void ShowWindow(object window) { }
    }

    internal sealed class Gtk4QueueSelectionDialogStub : IQueueSelectionDialog
    {
        public event EventHandler<QueueSelectionEventArgs>? QueueSelected;
        public void Dispose() { }
        public void SetData(IEnumerable<string> queueNames, IEnumerable<string> queueIds, IEnumerable<string> downloadIds) { }
        public void ShowWindow() { }
    }

    internal sealed class Gtk4UpdaterUIStub : IUpdaterUI
    {
        public event EventHandler? Cancelled;
        public event EventHandler? Finished;
        public event EventHandler? Load;
        public string Label { get; set; } = string.Empty;
        public bool Inderminate { get; set; }
        public void DownloadCancelled(object? sender, EventArgs e) { }
        public void DownloadFailed(object? sender, DownloadFailedEventArgs e) { }
        public void DownloadFinished(object? sender, EventArgs e) { }
        public void DownloadProgressChanged(object? sender, ProgressResultEventArgs e) { }
        public void DownloadStarted(object? sender, EventArgs e) { }
        public void Show() { }
        public void ShowNoUpdateMessage() { }
    }
}
