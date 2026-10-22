// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Native GTK 4 Settings dialog providing configuration for General, Network, and Monitoring.
using System;
using System.IO;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Util;

namespace XDM.Gtk4UI.Dialogs
{
    // Preferences and application settings window
    internal sealed class SettingsDialog
    {
        private readonly Window window;

        private readonly Entry txtDownloadFolder;
        private readonly Entry txtTempFolder;
        private readonly CheckButton chkAutoStart;
        private readonly CheckButton chkNotification;
        private readonly CheckButton chkShowComplete;
        private readonly CheckButton chkMonitorClipboard;

        private readonly Entry txtNetworkTimeout;
        private readonly Entry txtMaxRetries;
        private readonly CheckButton chkEnableSpeedLimit;
        private readonly Entry txtSpeedLimit;

        // Constructs the Settings dialog with current configuration values
        public SettingsDialog(Window? parent, int initialPage = 0)
        {
            window = Window.New();
            window.Title = TextResource.GetText("TITLE_SETTINGS") ?? "Settings";
            window.Modal = true;
            window.SetDefaultSize(640, 500);
            if (parent != null) window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            window.SetTitlebar(header);

            var mainBox = Box.New(Orientation.Vertical, 12);
            mainBox.MarginTop = 16;
            mainBox.MarginBottom = 16;
            mainBox.MarginStart = 20;
            mainBox.MarginEnd = 20;

            var notebook = Notebook.New();
            notebook.Vexpand = true;

            // --- Page 1: General ---
            var generalBox = Box.New(Orientation.Vertical, 12);
            generalBox.MarginTop = 12;
            generalBox.MarginStart = 8;
            generalBox.MarginEnd = 8;

            var downloadFolderRow = Box.New(Orientation.Horizontal, 8);
            var lblDownload = Label.New(TextResource.GetText("SETTINGS_DEF_DWN_FLD") ?? "Download Folder:");
            lblDownload.WidthRequest = 140;
            lblDownload.Halign = Align.Start;
            txtDownloadFolder = Entry.New();
            txtDownloadFolder.Hexpand = true;
            txtDownloadFolder.SetText(Config.Instance.DefaultDownloadFolder ?? string.Empty);
            var btnBrowseDownload = Button.NewWithLabel("Browse...");
            btnBrowseDownload.OnClicked += (_, _) => BrowseFolder(txtDownloadFolder);
            downloadFolderRow.Append(lblDownload);
            downloadFolderRow.Append(txtDownloadFolder);
            downloadFolderRow.Append(btnBrowseDownload);
            generalBox.Append(downloadFolderRow);

            var tempFolderRow = Box.New(Orientation.Horizontal, 8);
            var lblTemp = Label.New(TextResource.GetText("SETTINGS_TEMP_FLD") ?? "Temporary Folder:");
            lblTemp.WidthRequest = 140;
            lblTemp.Halign = Align.Start;
            txtTempFolder = Entry.New();
            txtTempFolder.Hexpand = true;
            txtTempFolder.SetText(Config.Instance.TempDir ?? string.Empty);
            var btnBrowseTemp = Button.NewWithLabel("Browse...");
            btnBrowseTemp.OnClicked += (_, _) => BrowseFolder(txtTempFolder);
            tempFolderRow.Append(lblTemp);
            tempFolderRow.Append(txtTempFolder);
            tempFolderRow.Append(btnBrowseTemp);
            generalBox.Append(tempFolderRow);

            chkAutoStart = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_AUTOSTART") ?? "Start FetchFlow automatically on system login");
            chkAutoStart.Active = PlatformHelper.IsAutoStartEnabled();
            generalBox.Append(chkAutoStart);

            chkNotification = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_SHOW_NOTIFICATION") ?? "Show desktop notifications on completion");
            chkNotification.Active = Config.Instance.ShowNotification;
            generalBox.Append(chkNotification);

            chkShowComplete = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_SHOW_COMPLETE") ?? "Show download complete popup dialog");
            chkShowComplete.Active = Config.Instance.ShowDownloadCompleteWindow;
            generalBox.Append(chkShowComplete);

            notebook.AppendPage(generalBox, Label.New("General"));

            // --- Page 2: Network ---
            var networkBox = Box.New(Orientation.Vertical, 12);
            networkBox.MarginTop = 12;
            networkBox.MarginStart = 8;
            networkBox.MarginEnd = 8;

            var timeoutRow = Box.New(Orientation.Horizontal, 8);
            var lblTimeout = Label.New("Network Timeout (sec):");
            lblTimeout.WidthRequest = 160;
            lblTimeout.Halign = Align.Start;
            txtNetworkTimeout = Entry.New();
            txtNetworkTimeout.WidthRequest = 80;
            txtNetworkTimeout.SetText(Config.Instance.NetworkTimeout.ToString());
            timeoutRow.Append(lblTimeout);
            timeoutRow.Append(txtNetworkTimeout);
            networkBox.Append(timeoutRow);

            var retriesRow = Box.New(Orientation.Horizontal, 8);
            var lblRetries = Label.New("Max Retry Attempts:");
            lblRetries.WidthRequest = 160;
            lblRetries.Halign = Align.Start;
            txtMaxRetries = Entry.New();
            txtMaxRetries.WidthRequest = 80;
            txtMaxRetries.SetText(Config.Instance.MaxRetry.ToString());
            retriesRow.Append(lblRetries);
            retriesRow.Append(txtMaxRetries);
            networkBox.Append(retriesRow);

            var speedLimitBox = Box.New(Orientation.Vertical, 6);
            chkEnableSpeedLimit = CheckButton.NewWithLabel("Enable maximum download speed limit");
            chkEnableSpeedLimit.Active = Config.Instance.EnableSpeedLimit;

            var speedRow = Box.New(Orientation.Horizontal, 8);
            var lblSpeed = Label.New("Speed Limit (KB/s):");
            lblSpeed.WidthRequest = 160;
            lblSpeed.Halign = Align.Start;
            txtSpeedLimit = Entry.New();
            txtSpeedLimit.WidthRequest = 120;
            txtSpeedLimit.SetText(Config.Instance.DefaltDownloadSpeed.ToString());
            speedRow.Append(lblSpeed);
            speedRow.Append(txtSpeedLimit);

            speedLimitBox.Append(chkEnableSpeedLimit);
            speedLimitBox.Append(speedRow);
            networkBox.Append(speedLimitBox);

            notebook.AppendPage(networkBox, Label.New("Network"));

            // --- Page 3: Monitoring ---
            var monitoringBox = Box.New(Orientation.Vertical, 12);
            monitoringBox.MarginTop = 12;
            monitoringBox.MarginStart = 8;
            monitoringBox.MarginEnd = 8;

            chkMonitorClipboard = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_MONITOR_CLIPBOARD") ?? "Monitor clipboard for downloadable URLs");
            chkMonitorClipboard.Active = Config.Instance.MonitorClipboard;
            monitoringBox.Append(chkMonitorClipboard);

            var lblBrowserInfo = Label.New("Browser Integration:\nFetchFlow intercepts downloads automatically via its browser extension.\nExtensions are available for Google Chrome, Mozilla Firefox, Brave, and Edge.");
            lblBrowserInfo.Halign = Align.Start;
            lblBrowserInfo.Wrap = true;
            monitoringBox.Append(lblBrowserInfo);

            var storeButtonsRow = Box.New(Orientation.Horizontal, 10);
            var btnCws = Button.NewWithLabel("Chrome Web Store Extension");
            btnCws.OnClicked += (_, _) => PlatformHelper.OpenBrowser("https://chromewebstore.google.com");
            var btnAmo = Button.NewWithLabel("Firefox Add-ons (AMO)");
            btnAmo.OnClicked += (_, _) => PlatformHelper.OpenBrowser("https://addons.mozilla.org");

            storeButtonsRow.Append(btnCws);
            storeButtonsRow.Append(btnAmo);
            monitoringBox.Append(storeButtonsRow);

            notebook.AppendPage(monitoringBox, Label.New("Monitoring"));

            if (initialPage >= 0 && initialPage < 3)
            {
                notebook.SetCurrentPage(initialPage);
            }

            mainBox.Append(notebook);

            // --- Action Buttons ---
            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 8;

            var cancelBtn = Button.NewWithLabel(TextResource.GetText("BTN_CANCEL") ?? "Cancel");
            cancelBtn.OnClicked += (_, _) => window.Close();

            var saveBtn = Button.NewWithLabel(TextResource.GetText("BTN_SAVE") ?? "Save");
            saveBtn.AddCssClass("suggested-action");
            saveBtn.OnClicked += (_, _) => SaveSettings();

            buttonBox.Append(cancelBtn);
            buttonBox.Append(saveBtn);
            mainBox.Append(buttonBox);

            window.SetChild(mainBox);
        }

        // Asynchronously opens file portal folder chooser and updates the target entry
        private void BrowseFolder(Entry targetEntry)
        {
            var dialog = FileDialog.New();
            dialog.Title = "Select Folder";
            dialog.Modal = true;
            var currentPath = targetEntry.GetText();
            if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath))
            {
                dialog.SetInitialFolder(Gio.FileHelper.NewForPath(currentPath));
            }
            dialog.SelectFolderAsync(window).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result != null)
                {
                    var selected = t.Result.GetPath();
                    if (!string.IsNullOrEmpty(selected))
                    {
                        GLib.Functions.IdleAdd(0, () =>
                        {
                            targetEntry.SetText(selected);
                            return false;
                        });
                    }
                }
            });
        }

        // Commits modified settings to Config instance and filesystem
        private void SaveSettings()
        {
            var downloadFolder = txtDownloadFolder.GetText()?.Trim();
            if (!string.IsNullOrEmpty(downloadFolder))
            {
                Config.Instance.DefaultDownloadFolder = downloadFolder;
            }

            var tempFolder = txtTempFolder.GetText()?.Trim();
            if (!string.IsNullOrEmpty(tempFolder))
            {
                Config.Instance.TempDir = tempFolder;
            }

            Config.Instance.ShowNotification = chkNotification.Active;
            Config.Instance.ShowDownloadCompleteWindow = chkShowComplete.Active;
            Config.Instance.MonitorClipboard = chkMonitorClipboard.Active;

            if (int.TryParse(txtNetworkTimeout.GetText(), out int timeout))
            {
                Config.Instance.NetworkTimeout = Math.Max(5, timeout);
            }

            if (int.TryParse(txtMaxRetries.GetText(), out int retries))
            {
                Config.Instance.MaxRetry = Math.Max(0, retries);
            }

            Config.Instance.EnableSpeedLimit = chkEnableSpeedLimit.Active;
            if (int.TryParse(txtSpeedLimit.GetText(), out int speed))
            {
                Config.Instance.DefaltDownloadSpeed = Math.Max(0, speed);
            }

            // Sync autostart configuration with system desktop entry
            try
            {
                PlatformHelper.EnableAutoStart(chkAutoStart.Active);
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Failed to update autostart setting: " + ex.Message);
            }

            Config.SaveConfig();
            window.Close();
        }

        // Displays the settings window
        public void Show() => window.Present();
    }
}
