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
        // Settings notebook page indices (must match SettingsDialog's page order)
        private const int PageGeneral = 0;
        private const int PageSpeedLimiter = 3;
        private const int PageMonitoring = 4;

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

        // Prompts for server credentials; null means the user cancelled the download
        public AuthenticationInfo? PromtForCredentials(object window, string message)
        {
            var parent = (window as Window) ?? mainWindow.Window;
            return CredentialsDialog.Prompt(parent, message);
        }

        // Opens the replacement-link watcher for an interrupted transfer
        public void ShowRefreshLinkDialog(InProgressDownloadItem entry)
        {
            var dialog = new LinkRefreshWindow(mainWindow.Window);
            if (!LinkRefreshDialogUIController.RefreshLink(entry, dialog))
            {
                ShowMessageBox(mainWindow.Window,
                    TextResource.GetText("NO_REFRESH_LINK") ?? "No replacement link could be captured.");
            }
        }

        // Displays download properties
        public void ShowPropertiesDialog(DownloadItemBase ent, string cookies, Dictionary<string, List<string>> headers)
        {
            var dlg = new PropertiesDialog(mainWindow.Window, ent, cookies, headers);
            dlg.Show();
        }

        // Opens the media stream downloader
        public void ShowYoutubeDLDialog() =>
            ShowMessageBox(mainWindow.Window,
                TextResource.GetText("MSG_YTDLP_UNAVAILABLE")
                ?? "The media stream downloader is not part of the GTK 4 build yet.");

        // Shows batch downloader
        public void ShowBatchDownloadWindow()
        {
            var view = new BatchDownloadWindow(mainWindow.Window);
            var controller = new BatchDownloadUIController(view);
            controller.Run();
        }

        // Displays settings window
        public void ShowSettingsDialog(int page = PageGeneral)
        {
            var dlg = new SettingsDialog(mainWindow.Window, page);
            dlg.Show();
        }

        // Displays browser monitoring settings
        public void ShowBrowserMonitoringDialog() => ShowSettingsDialog(PageMonitoring);

        // Displays Import/Export backup and restore dialog
        public void ShowImportExportDialog()
        {
            var dlg = new ImportExportDialog(mainWindow.Window);
            dlg.Show();
        }

        // Displays the bandwidth limiter window
        public void ShowSpeedLimiterWindow()
        {
            var window = new SpeedLimiterWindow(mainWindow.Window);
            SpeedLimiterUIController.Run(window);
        }

        // Creates and shows media grabber popup
        public void CreateAndShowMediaGrabber() => ShowYoutubeDLDialog();

        // Shows browser extension registration window
        public void ShowExtensionRegistrationWindow() =>
            ShowMessageBox(mainWindow.Window,
                TextResource.GetText("MSG_EXT_REG_UNAVAILABLE")
                ?? "Extension registration is available from Settings > Browser Monitoring.");

        // Displays checksum verification dialog
        public void ShowChecksumDialog(object? window, string filePath)
        {
            var parent = (window as Window) ?? mainWindow.Window;
            var dlg = new ChecksumDialog(parent, filePath);
            dlg.Show();
        }

        // Displays the batch link-selection confirmation dialog
        public void ShowDownloadSelectionWindow(FileNameFetchMode mode, IEnumerable<IRequestData> downloads)
        {
            var view = new DownloadSelectionWindow(mainWindow.Window);
            var controller = new DownloadSelectionUIController(view, mode, downloads);
            controller.Run();
        }

        // Creates video download dialog
        public INewVideoDownloadDialog CreateNewVideoDialog() =>
            new NewVideoDownloadDialog(mainWindow.Window);

        // Creates queue scheduler window
        public IQueuesWindow CreateQueuesAndSchedulerWindow() =>
            new QueueSchedulerDialog(mainWindow.Window);

        // Creates queue selection dialog
        public IQueueSelectionDialog CreateQueueSelectionDialog() =>
            new QueueSelectionDialog(mainWindow.Window);

        // Creates the component updater window driven by ComponentUpdaterUIController
        public IUpdaterUI CreateUpdateUIDialog() =>
            new UpdaterWindow(mainWindow.Window);
    }
}
