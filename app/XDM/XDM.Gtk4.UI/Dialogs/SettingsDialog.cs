// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Native GTK 4 Settings dialog providing comprehensive configuration for
// General, File Types / Categories, Network & Proxy, Speed Limiter, Monitoring, and Advanced.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.BrowserMonitoring;
using XDM.Core.UI;
using XDM.Core.Util;
using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    // Preferences and application settings window
    internal sealed class SettingsDialog
    {
        private readonly Window window;

        // General tab controls
        private readonly Entry txtDownloadFolder;
        private readonly Entry txtTempFolder;
        private readonly DropDown dropMaxDownloads;
        private readonly DropDown dropMaxSegments;
        private readonly DropDown dropDoubleClick;
        private readonly DropDown dropTheme;
        private readonly CheckButton chkAutoStart;
        private readonly CheckButton chkNotification;
        private readonly CheckButton chkShowComplete;
        private readonly CheckButton chkShowProgress;
        private readonly CheckButton chkPlaySound;
        private readonly CheckButton chkStartAuto;
        private readonly CheckButton chkTimestamp;
        private readonly CheckButton chkKeepAwake;

        // Categories & File Types tab controls
        private readonly ListBox categoryList;
        private readonly Entry txtCatFolder;
        private readonly Entry txtCatExts;
        private readonly Label lblSelectedCat;
        private readonly List<Category> workingCategories;
        private int selectedCategoryIndex = 0;
        private readonly Entry txtBlockedHosts;

        // Network & Proxy tab controls
        private readonly Entry txtNetworkTimeout;
        private readonly Entry txtMaxRetries;
        private readonly Entry txtFallbackUserAgent;
        private readonly DropDown dropProxyType;
        private readonly Entry txtProxyHost;
        private readonly Entry txtProxyPort;
        private readonly Entry txtProxyUser;
        private readonly Entry txtProxyPassword;
        private readonly Box proxyManualBox;

        // Speed Limiter tab controls
        private readonly CheckButton chkEnableSpeedLimit;
        private readonly Entry txtSpeedLimit;

        // Monitoring tab controls
        private readonly CheckButton chkMonitorClipboard;
        private readonly Label lblIpcStatus;
        private readonly Button btnRestartIpc;

        // Advanced & Automation tab controls
        private readonly CheckButton chkShutdownAll;
        private readonly CheckButton chkRunCmd;
        private readonly Entry txtCustomCmd;
        private readonly CheckButton chkRunAntivirus;
        private readonly Entry txtAntiVirusCmd;
        private readonly Entry txtAntiVirusArgs;

        // Constructs the Settings dialog with current configuration values
        public SettingsDialog(Window? parent, int initialPage = 0)
        {
            window = Window.New();
            window.Title = TextResource.GetText("TITLE_SETTINGS") ?? "Settings";
            window.Modal = true;
            window.SetDefaultSize(740, 580);
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

            // -----------------------------------------------------------------
            // --- Page 1: General ---
            // -----------------------------------------------------------------
            var generalBox = Box.New(Orientation.Vertical, 10);
            generalBox.MarginTop = 12;
            generalBox.MarginStart = 8;
            generalBox.MarginEnd = 8;

            var downloadFolderRow = Box.New(Orientation.Horizontal, 8);
            var lblDownload = Label.New(TextResource.GetText("SETTINGS_DEF_DWN_FLD") ?? "Download Folder:");
            lblDownload.WidthRequest = 180;
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
            lblTemp.WidthRequest = 180;
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

            // Simultaneous downloads
            var simRow = Box.New(Orientation.Horizontal, 8);
            var lblSim = Label.New(TextResource.GetText("MSG_MAX_DOWNLOAD") ?? "Max Simultaneous Downloads:");
            lblSim.WidthRequest = 180;
            lblSim.Halign = Align.Start;
            var simOptions = new[] { "1", "2", "3", "4", "5", "8", "10" };
            dropMaxDownloads = DropDown.NewFromStrings(simOptions);
            var currentSim = Config.Instance.MaxParallelDownloads.ToString();
            var simIdx = Array.IndexOf(simOptions, currentSim);
            dropMaxDownloads.Selected = (uint)(simIdx >= 0 ? simIdx : 1);
            simRow.Append(lblSim);
            simRow.Append(dropMaxDownloads);
            generalBox.Append(simRow);

            // Segments per download
            var segRow = Box.New(Orientation.Horizontal, 8);
            var lblSeg = Label.New("Max Segments per Download:");
            lblSeg.WidthRequest = 180;
            lblSeg.Halign = Align.Start;
            var segOptions = new[] { "1", "2", "4", "8", "16", "32" };
            dropMaxSegments = DropDown.NewFromStrings(segOptions);
            var currentSeg = Config.Instance.MaxSegments.ToString();
            var segIdx = Array.IndexOf(segOptions, currentSeg);
            dropMaxSegments.Selected = (uint)(segIdx >= 0 ? segIdx : 3);
            segRow.Append(lblSeg);
            segRow.Append(dropMaxSegments);
            generalBox.Append(segRow);

            // Double click action
            var dblRow = Box.New(Orientation.Horizontal, 8);
            var lblDbl = Label.New(TextResource.GetText("MSG_DOUBLE_CLICK_ACTION") ?? "Double-Click Action:");
            lblDbl.WidthRequest = 180;
            lblDbl.Halign = Align.Start;
            var dblOptions = new[] { "Open downloaded file", "Open destination folder" };
            dropDoubleClick = DropDown.NewFromStrings(dblOptions);
            dropDoubleClick.Selected = Config.Instance.DoubleClickOpenFile ? 0u : 1u;
            dblRow.Append(lblDbl);
            dblRow.Append(dropDoubleClick);
            generalBox.Append(dblRow);

            // Theme selection: indices follow Config.ThemeMode (0 light, 1 dark, 2 follow system)
            var themeRow = Box.New(Orientation.Horizontal, 8);
            var lblTheme = Label.New(TextResource.GetText("SETTINGS_THEME") ?? "Appearance Theme:");
            lblTheme.WidthRequest = 180;
            lblTheme.Halign = Align.Start;
            var themeOptions = new[] { "Light Theme", "Dark Theme", "Follow System" };
            dropTheme = DropDown.NewFromStrings(themeOptions);
            dropTheme.Selected = (uint)Math.Clamp(Config.Instance.ThemeMode, 0, 2);
            themeRow.Append(lblTheme);
            themeRow.Append(dropTheme);
            generalBox.Append(themeRow);

            // General options checkboxes
            chkAutoStart = CheckButton.NewWithLabel(TextResource.GetText("AUTO_START") ?? "Launch FetchFlow when system starts");
            chkAutoStart.Active = PlatformHelper.IsAutoStartEnabled();
            generalBox.Append(chkAutoStart);

            chkNotification = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_SHOW_NOTIFICATION") ?? "Show desktop notifications on completion");
            chkNotification.Active = Config.Instance.ShowNotification;
            generalBox.Append(chkNotification);

            chkShowComplete = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_SHOW_COMPLETE") ?? "Show download complete popup dialog");
            chkShowComplete.Active = Config.Instance.ShowDownloadCompleteWindow;
            generalBox.Append(chkShowComplete);

            chkShowProgress = CheckButton.NewWithLabel(TextResource.GetText("LBL_SHOW_PROGRESS") ?? "Show download progress window");
            chkShowProgress.Active = Config.Instance.ShowProgressWindow;
            generalBox.Append(chkShowProgress);

            chkPlaySound = CheckButton.NewWithLabel(TextResource.GetText("MSG_PLAY_SOUND") ?? "Play sound when download finishes");
            chkPlaySound.Active = Config.Instance.PlayCompletionSound;
            generalBox.Append(chkPlaySound);

            chkStartAuto = CheckButton.NewWithLabel(TextResource.GetText("LBL_START_AUTO") ?? "Start download automatically without prompt");
            chkStartAuto.Active = Config.Instance.StartDownloadAutomatically;
            generalBox.Append(chkStartAuto);

            chkTimestamp = CheckButton.NewWithLabel(TextResource.GetText("LBL_GET_TIMESTAMP") ?? "Get file creation timestamp from server");
            chkTimestamp.Active = Config.Instance.FetchServerTimeStamp;
            generalBox.Append(chkTimestamp);

            chkKeepAwake = CheckButton.NewWithLabel(TextResource.GetText("MSG_AWAKE") ?? "Prevent hibernate or sleep during downloads");
            chkKeepAwake.Active = Config.Instance.KeepPCAwake;
            generalBox.Append(chkKeepAwake);

            var generalScroll = ScrolledWindow.New();
            generalScroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            generalScroll.SetChild(generalBox);
            notebook.AppendPage(generalScroll, Label.New(TextResource.GetText("SETTINGS_GENERAL") ?? "General"));

            // -----------------------------------------------------------------
            // --- Page 2: File Types & Categories ---
            // -----------------------------------------------------------------
            var categoriesBox = Box.New(Orientation.Vertical, 12);
            categoriesBox.MarginTop = 12;
            categoriesBox.MarginStart = 8;
            categoriesBox.MarginEnd = 8;

            workingCategories = Config.Instance.Categories.Select(c => new Category
            {
                Name = c.Name,
                DisplayName = c.DisplayName,
                DefaultFolder = c.DefaultFolder,
                FileExtensions = new HashSet<string>(c.FileExtensions),
                CustomIcon = c.CustomIcon,
                CustomColor = c.CustomColor
            }).ToList();

            var catPaned = Paned.New(Orientation.Horizontal);
            catPaned.Position = 200;

            categoryList = ListBox.New();
            categoryList.AddCssClass("sidebar");
            categoryList.SelectionMode = SelectionMode.Single;

            foreach (var cat in workingCategories)
            {
                var (iconName, r, g, b) = CategoryIconPalette.Resolve(cat);
                var rowBox = Box.New(Orientation.Horizontal, 8);
                rowBox.MarginStart = 6;
                rowBox.MarginEnd = 6;
                rowBox.MarginTop = 4;
                rowBox.MarginBottom = 4;
                var icon = Gtk4IconHelper.CreateTintedIcon(iconName, r, g, b, 16);
                var lbl = Label.New(cat.DisplayName);
                lbl.Halign = Align.Start;
                rowBox.Append(icon);
                rowBox.Append(lbl);
                var row = ListBoxRow.New();
                row.SetChild(rowBox);
                categoryList.Append(row);
            }

            var catListScroll = ScrolledWindow.New();
            catListScroll.SetChild(categoryList);
            catListScroll.WidthRequest = 180;
            catPaned.StartChild = catListScroll;

            var catDetailBox = Box.New(Orientation.Vertical, 10);
            catDetailBox.MarginStart = 12;
            catDetailBox.MarginEnd = 8;

            lblSelectedCat = Label.New("");
            lblSelectedCat.AddCssClass("heading");
            lblSelectedCat.Halign = Align.Start;
            catDetailBox.Append(lblSelectedCat);

            var catFolderRow = Box.New(Orientation.Horizontal, 8);
            var lblCatFolder = Label.New("Target Folder:");
            lblCatFolder.WidthRequest = 110;
            lblCatFolder.Halign = Align.Start;
            txtCatFolder = Entry.New();
            txtCatFolder.Hexpand = true;
            var btnBrowseCatFolder = Button.NewWithLabel("Browse...");
            btnBrowseCatFolder.OnClicked += (_, _) => BrowseFolder(txtCatFolder);
            catFolderRow.Append(lblCatFolder);
            catFolderRow.Append(txtCatFolder);
            catFolderRow.Append(btnBrowseCatFolder);
            catDetailBox.Append(catFolderRow);

            var catExtsRow = Box.New(Orientation.Horizontal, 8);
            var lblCatExts = Label.New("File Extensions:");
            lblCatExts.WidthRequest = 110;
            lblCatExts.Halign = Align.Start;
            txtCatExts = Entry.New();
            txtCatExts.Hexpand = true;
            catExtsRow.Append(lblCatExts);
            catExtsRow.Append(txtCatExts);
            catDetailBox.Append(catExtsRow);

            var catHint = Label.New("Separate extensions with spaces or commas (e.g. mp4, mkv, webm)");
            catHint.AddCssClass("dim-label");
            catHint.Halign = Align.Start;
            catDetailBox.Append(catHint);

            categoryList.OnRowSelected += (_, args) =>
            {
                if (args.Row is ListBoxRow r)
                {
                    SaveCurrentCategoryFields();
                    selectedCategoryIndex = r.GetIndex();
                    LoadCategoryFields(selectedCategoryIndex);
                }
            };

            catPaned.EndChild = catDetailBox;
            categoriesBox.Append(catPaned);

            var catActionsRow = Box.New(Orientation.Horizontal, 8);
            var btnResetCats = Button.NewWithLabel("Restore Default Categories");
            btnResetCats.OnClicked += (_, _) =>
            {
                workingCategories.Clear();
                foreach (var def in Config.DefaultCategories)
                {
                    workingCategories.Add(new Category
                    {
                        Name = def.Name,
                        DisplayName = def.DisplayName,
                        DefaultFolder = def.DefaultFolder,
                        FileExtensions = new HashSet<string>(def.FileExtensions),
                        CustomIcon = def.CustomIcon,
                        CustomColor = def.CustomColor
                    });
                }
                LoadCategoryFields(selectedCategoryIndex);
            };
            catActionsRow.Append(btnResetCats);
            categoriesBox.Append(catActionsRow);

            var sepCats = Separator.New(Orientation.Horizontal);
            sepCats.MarginTop = 6;
            sepCats.MarginBottom = 6;
            categoriesBox.Append(sepCats);

            var lblExceptions = Label.New(TextResource.GetText("DESC_SITEEXCEPTIONS") ?? "Do not automatically capture downloads from below web sites:");
            lblExceptions.AddCssClass("heading");
            lblExceptions.Halign = Align.Start;
            categoriesBox.Append(lblExceptions);

            var exceptionsRow = Box.New(Orientation.Horizontal, 8);
            txtBlockedHosts = Entry.New();
            txtBlockedHosts.Hexpand = true;
            txtBlockedHosts.SetText(string.Join(", ", Config.Instance.BlockedHosts ?? Config.DefaultBlockedHosts));
            var btnResetExceptions = Button.NewWithLabel("Restore Default Sites");
            btnResetExceptions.OnClicked += (_, _) =>
            {
                txtBlockedHosts.SetText(string.Join(", ", Config.DefaultBlockedHosts));
            };
            exceptionsRow.Append(txtBlockedHosts);
            exceptionsRow.Append(btnResetExceptions);
            categoriesBox.Append(exceptionsRow);

            var excHint = Label.New("Separate domains with commas or spaces (e.g. *.bank.com, example.com)");
            excHint.AddCssClass("dim-label");
            excHint.Halign = Align.Start;
            categoriesBox.Append(excHint);

            LoadCategoryFields(0);

            var catScroll = ScrolledWindow.New();
            catScroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            catScroll.SetChild(categoriesBox);
            notebook.AppendPage(catScroll, Label.New(TextResource.GetText("SETTINGS_CAT") ?? "Categories"));

            // -----------------------------------------------------------------
            // --- Page 3: Network & Proxy ---
            // -----------------------------------------------------------------
            var networkBox = Box.New(Orientation.Vertical, 12);
            networkBox.MarginTop = 12;
            networkBox.MarginStart = 8;
            networkBox.MarginEnd = 8;

            var timeoutRow = Box.New(Orientation.Horizontal, 8);
            var lblTimeout = Label.New("Network Timeout (sec):");
            lblTimeout.WidthRequest = 180;
            lblTimeout.Halign = Align.Start;
            txtNetworkTimeout = Entry.New();
            txtNetworkTimeout.WidthRequest = 80;
            txtNetworkTimeout.SetText(Config.Instance.NetworkTimeout.ToString());
            timeoutRow.Append(lblTimeout);
            timeoutRow.Append(txtNetworkTimeout);
            networkBox.Append(timeoutRow);

            var retriesRow = Box.New(Orientation.Horizontal, 8);
            var lblRetries = Label.New("Max Retry Attempts:");
            lblRetries.WidthRequest = 180;
            lblRetries.Halign = Align.Start;
            txtMaxRetries = Entry.New();
            txtMaxRetries.WidthRequest = 80;
            txtMaxRetries.SetText(Config.Instance.MaxRetry.ToString());
            retriesRow.Append(lblRetries);
            retriesRow.Append(txtMaxRetries);
            networkBox.Append(retriesRow);

            var uaRow = Box.New(Orientation.Horizontal, 8);
            var lblUa = Label.New("Fallback User-Agent:");
            lblUa.WidthRequest = 180;
            lblUa.Halign = Align.Start;
            txtFallbackUserAgent = Entry.New();
            txtFallbackUserAgent.Hexpand = true;
            txtFallbackUserAgent.SetText(Config.Instance.FallbackUserAgent ?? Config.DefaultFallbackUserAgent);
            var btnResetUa = Button.NewWithLabel("Reset");
            btnResetUa.OnClicked += (_, _) => txtFallbackUserAgent.SetText(Config.DefaultFallbackUserAgent);
            uaRow.Append(lblUa);
            uaRow.Append(txtFallbackUserAgent);
            uaRow.Append(btnResetUa);
            networkBox.Append(uaRow);

            var proxyHeader = Label.New("Proxy Configuration:");
            proxyHeader.AddCssClass("heading");
            proxyHeader.Halign = Align.Start;
            networkBox.Append(proxyHeader);

            var proxyTypeRow = Box.New(Orientation.Horizontal, 8);
            var lblProxyType = Label.New("Proxy Mode:");
            lblProxyType.WidthRequest = 180;
            lblProxyType.Halign = Align.Start;
            var proxyOptions = new[] { "No Proxy (Direct Connection)", "Use System Proxy", "Manual Proxy Configuration" };
            dropProxyType = DropDown.NewFromStrings(proxyOptions);
            var initialProxyType = Config.Instance.Proxy.HasValue ? Config.Instance.Proxy.Value.ProxyType : ProxyType.Direct;
            dropProxyType.Selected = initialProxyType switch
            {
                ProxyType.Direct => 0u,
                ProxyType.System => 1u,
                ProxyType.Custom => 2u,
                _ => 0u
            };
            proxyTypeRow.Append(lblProxyType);
            proxyTypeRow.Append(dropProxyType);
            networkBox.Append(proxyTypeRow);

            proxyManualBox = Box.New(Orientation.Vertical, 8);
            proxyManualBox.MarginStart = 16;

            var hostRow = Box.New(Orientation.Horizontal, 8);
            var lblHost = Label.New("Proxy Host:");
            lblHost.WidthRequest = 120;
            lblHost.Halign = Align.Start;
            txtProxyHost = Entry.New();
            txtProxyHost.Hexpand = true;
            txtProxyHost.SetText(Config.Instance.Proxy?.Host ?? "");
            hostRow.Append(lblHost);
            hostRow.Append(txtProxyHost);
            proxyManualBox.Append(hostRow);

            var portRow = Box.New(Orientation.Horizontal, 8);
            var lblPort = Label.New("Proxy Port:");
            lblPort.WidthRequest = 120;
            lblPort.Halign = Align.Start;
            txtProxyPort = Entry.New();
            txtProxyPort.WidthRequest = 100;
            txtProxyPort.SetText((Config.Instance.Proxy?.Port ?? 8080).ToString());
            portRow.Append(lblPort);
            portRow.Append(txtProxyPort);
            proxyManualBox.Append(portRow);

            var userRow = Box.New(Orientation.Horizontal, 8);
            var lblUser = Label.New("Username:");
            lblUser.WidthRequest = 120;
            lblUser.Halign = Align.Start;
            txtProxyUser = Entry.New();
            txtProxyUser.Hexpand = true;
            txtProxyUser.SetText(Config.Instance.Proxy?.UserName ?? "");
            userRow.Append(lblUser);
            userRow.Append(txtProxyUser);
            proxyManualBox.Append(userRow);

            var passRow = Box.New(Orientation.Horizontal, 8);
            var lblPass = Label.New("Password:");
            lblPass.WidthRequest = 120;
            lblPass.Halign = Align.Start;
            txtProxyPassword = Entry.New();
            txtProxyPassword.Hexpand = true;
            txtProxyPassword.Visibility = false;
            txtProxyPassword.SetText(Config.Instance.Proxy?.Password ?? "");
            passRow.Append(lblPass);
            passRow.Append(txtProxyPassword);
            proxyManualBox.Append(passRow);

            proxyManualBox.Visible = dropProxyType.Selected == 2u;
            dropProxyType.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() == "selected")
                {
                    proxyManualBox.Visible = dropProxyType.Selected == 2u;
                }
            };
            networkBox.Append(proxyManualBox);

            var netScroll = ScrolledWindow.New();
            netScroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            netScroll.SetChild(networkBox);
            notebook.AppendPage(netScroll, Label.New(TextResource.GetText("SETTINGS_NETWORK") ?? "Network"));

            // -----------------------------------------------------------------
            // --- Page 4: Speed Limiter ---
            // -----------------------------------------------------------------
            var speedBox = Box.New(Orientation.Vertical, 12);
            speedBox.MarginTop = 12;
            speedBox.MarginStart = 8;
            speedBox.MarginEnd = 8;

            chkEnableSpeedLimit = CheckButton.NewWithLabel("Enable maximum download speed limit");
            chkEnableSpeedLimit.Active = Config.Instance.EnableSpeedLimit;
            speedBox.Append(chkEnableSpeedLimit);

            var speedRow = Box.New(Orientation.Horizontal, 8);
            var lblSpeed = Label.New("Speed Limit (KB/s):");
            lblSpeed.WidthRequest = 160;
            lblSpeed.Halign = Align.Start;
            txtSpeedLimit = Entry.New();
            txtSpeedLimit.WidthRequest = 120;
            txtSpeedLimit.SetText(Config.Instance.DefaltDownloadSpeed.ToString());
            speedRow.Append(lblSpeed);
            speedRow.Append(txtSpeedLimit);
            speedBox.Append(speedRow);

            var presetBox = Box.New(Orientation.Horizontal, 8);
            var lblPresets = Label.New("Quick Presets:");
            lblPresets.WidthRequest = 160;
            lblPresets.Halign = Align.Start;
            presetBox.Append(lblPresets);

            var p512 = Button.NewWithLabel("512 KB/s");
            p512.OnClicked += (_, _) => txtSpeedLimit.SetText("512");
            var p1024 = Button.NewWithLabel("1 MB/s");
            p1024.OnClicked += (_, _) => txtSpeedLimit.SetText("1024");
            var p2048 = Button.NewWithLabel("2 MB/s");
            p2048.OnClicked += (_, _) => txtSpeedLimit.SetText("2048");
            var p5120 = Button.NewWithLabel("5 MB/s");
            p5120.OnClicked += (_, _) => txtSpeedLimit.SetText("5120");

            presetBox.Append(p512);
            presetBox.Append(p1024);
            presetBox.Append(p2048);
            presetBox.Append(p5120);
            speedBox.Append(presetBox);

            notebook.AppendPage(speedBox, Label.New(TextResource.GetText("MENU_SPEED_LIMITER") ?? "Speed Limiter"));

            // -----------------------------------------------------------------
            // --- Page 5: Monitoring ---
            // -----------------------------------------------------------------
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

            var ipcBox = Box.New(Orientation.Horizontal, 10);
            lblIpcStatus = Label.New($"● IPC Relay: 127.0.0.1:{IpcHttpMessageProcessor.EffectivePort}");
            lblIpcStatus.Halign = Align.Start;
            lblIpcStatus.Hexpand = true;
            lblIpcStatus.AddCssClass("dim-label");

            btnRestartIpc = Button.NewWithLabel("Restart IPC Relay");
            btnRestartIpc.OnClicked += (_, _) =>
            {
                try
                {
                    btnRestartIpc.Sensitive = false;
                    BrowserMonitor.Restart();
                    lblIpcStatus.SetText($"● IPC Relay: 127.0.0.1:{IpcHttpMessageProcessor.EffectivePort} (Restarted)");
                    GLib.Functions.TimeoutAdd(0, 1000, () =>
                    {
                        btnRestartIpc.Sensitive = true;
                        return false;
                    });
                }
                catch (Exception ex)
                {
                    TraceLog.Log.Debug(ex, "Failed to restart IPC relay");
                }
            };
            ipcBox.Append(lblIpcStatus);
            ipcBox.Append(btnRestartIpc);
            monitoringBox.Append(ipcBox);

            notebook.AppendPage(monitoringBox, Label.New(TextResource.GetText("SETTINGS_MONITORING") ?? "Monitoring"));

            // -----------------------------------------------------------------
            // --- Page 6: Advanced & Automation ---
            // -----------------------------------------------------------------
            var advancedBox = Box.New(Orientation.Vertical, 12);
            advancedBox.MarginTop = 12;
            advancedBox.MarginStart = 8;
            advancedBox.MarginEnd = 8;

            var automationHeader = Label.New("Post-Download Automation:");
            automationHeader.AddCssClass("heading");
            automationHeader.Halign = Align.Start;
            advancedBox.Append(automationHeader);

            chkShutdownAll = CheckButton.NewWithLabel(TextResource.GetText("MSG_HALT") ?? "Shutdown computer after all downloads complete");
            chkShutdownAll.Active = Config.Instance.ShutdownAfterAllFinished;
            advancedBox.Append(chkShutdownAll);

            chkRunCmd = CheckButton.NewWithLabel(TextResource.GetText("EXEC_CMD") ?? "Run a program after all downloads finish");
            chkRunCmd.Active = Config.Instance.RunCommandAfterCompletion;
            advancedBox.Append(chkRunCmd);

            var cmdRow = Box.New(Orientation.Horizontal, 8);
            cmdRow.MarginStart = 24;
            var lblCmd = Label.New("Command:");
            lblCmd.WidthRequest = 90;
            lblCmd.Halign = Align.Start;
            txtCustomCmd = Entry.New();
            txtCustomCmd.Hexpand = true;
            txtCustomCmd.SetText(Config.Instance.AfterCompletionCommand ?? "");
            cmdRow.Append(lblCmd);
            cmdRow.Append(txtCustomCmd);
            advancedBox.Append(cmdRow);

            var sepAdv = Separator.New(Orientation.Horizontal);
            sepAdv.MarginTop = 6;
            sepAdv.MarginBottom = 6;
            advancedBox.Append(sepAdv);

            var avHeader = Label.New("Antivirus Scanning:");
            avHeader.AddCssClass("heading");
            avHeader.Halign = Align.Start;
            advancedBox.Append(avHeader);

            chkRunAntivirus = CheckButton.NewWithLabel("Scan downloaded files with antivirus scanner");
            chkRunAntivirus.Active = Config.Instance.ScanWithAntiVirus;
            advancedBox.Append(chkRunAntivirus);

            var avRow = Box.New(Orientation.Horizontal, 8);
            avRow.MarginStart = 24;
            var lblAv = Label.New(TextResource.GetText("ANTIVIR_CMD") ?? "Antivirus Executable:");
            lblAv.WidthRequest = 160;
            lblAv.Halign = Align.Start;
            txtAntiVirusCmd = Entry.New();
            txtAntiVirusCmd.Hexpand = true;
            txtAntiVirusCmd.SetText(Config.Instance.AntiVirusExecutable ?? "");
            var btnBrowseAv = Button.NewWithLabel("Browse...");
            btnBrowseAv.OnClicked += (_, _) => BrowseFile(txtAntiVirusCmd);
            avRow.Append(lblAv);
            avRow.Append(txtAntiVirusCmd);
            avRow.Append(btnBrowseAv);
            advancedBox.Append(avRow);

            var argsRow = Box.New(Orientation.Horizontal, 8);
            argsRow.MarginStart = 24;
            var lblArgs = Label.New(TextResource.GetText("ANTIVIR_ARGS") ?? "Command line parameters:");
            lblArgs.WidthRequest = 160;
            lblArgs.Halign = Align.Start;
            txtAntiVirusArgs = Entry.New();
            txtAntiVirusArgs.Hexpand = true;
            txtAntiVirusArgs.SetText(Config.Instance.AntiVirusArgs ?? "[file]");
            argsRow.Append(lblArgs);
            argsRow.Append(txtAntiVirusArgs);
            advancedBox.Append(argsRow);

            var avHint = Label.New("Use [file] placeholder for the downloaded file path (e.g. clamscan [file])");
            avHint.AddCssClass("dim-label");
            avHint.MarginStart = 24;
            avHint.Halign = Align.Start;
            advancedBox.Append(avHint);

            var advScroll = ScrolledWindow.New();
            advScroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            advScroll.SetChild(advancedBox);
            notebook.AppendPage(advScroll, Label.New(TextResource.GetText("SETTINGS_ADV") ?? "Advanced settings"));

            if (initialPage >= 0 && initialPage < 6)
            {
                notebook.SetCurrentPage(initialPage);
            }

            mainBox.Append(notebook);

            // --- Action Buttons ---
            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 8;

            var cancelBtn = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            cancelBtn.OnClicked += (_, _) => window.Close();

            var saveBtn = Button.NewWithLabel(TextResource.GetText("DESC_SAVE_Q") ?? "Save");
            saveBtn.AddCssClass("suggested-action");
            saveBtn.OnClicked += (_, _) => SaveSettings();

            buttonBox.Append(cancelBtn);
            buttonBox.Append(saveBtn);
            mainBox.Append(buttonBox);

            window.SetChild(mainBox);
        }

        private void LoadCategoryFields(int index)
        {
            if (index < 0 || index >= workingCategories.Count) return;
            var cat = workingCategories[index];
            lblSelectedCat.SetText(cat.DisplayName);
            txtCatFolder.SetText(cat.DefaultFolder ?? "");
            txtCatExts.SetText(string.Join(", ", cat.FileExtensions ?? new HashSet<string>()));
        }

        private void SaveCurrentCategoryFields()
        {
            if (selectedCategoryIndex < 0 || selectedCategoryIndex >= workingCategories.Count) return;
            var cat = workingCategories[selectedCategoryIndex];
            cat.DefaultFolder = txtCatFolder.GetText()?.Trim() ?? "";
            var rawExts = txtCatExts.GetText() ?? "";
            var exts = rawExts.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(e => e.Trim().TrimStart('.').ToLowerInvariant())
                              .Where(e => !string.IsNullOrEmpty(e));
            cat.FileExtensions = new HashSet<string>(exts);
            workingCategories[selectedCategoryIndex] = cat;
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

        // Asynchronously opens file chooser and updates the target entry with selected file
        private void BrowseFile(Entry targetEntry)
        {
            var dialog = FileDialog.New();
            dialog.Title = "Select Executable";
            dialog.Modal = true;
            dialog.OpenAsync(window).ContinueWith(t =>
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
            SaveCurrentCategoryFields();

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
            Config.Instance.ShowProgressWindow = chkShowProgress.Active;
            Config.Instance.PlayCompletionSound = chkPlaySound.Active;
            Config.Instance.StartDownloadAutomatically = chkStartAuto.Active;
            Config.Instance.FetchServerTimeStamp = chkTimestamp.Active;
            Config.Instance.KeepPCAwake = chkKeepAwake.Active;
            Config.Instance.MonitorClipboard = chkMonitorClipboard.Active;

            // Simultaneous downloads
            var simOptions = new[] { 1, 2, 3, 4, 5, 8, 10 };
            if (dropMaxDownloads.Selected < (uint)simOptions.Length)
            {
                Config.Instance.MaxParallelDownloads = simOptions[(int)dropMaxDownloads.Selected];
            }

            // Max segments
            var segOptions = new[] { 1, 2, 4, 8, 16, 32 };
            if (dropMaxSegments.Selected < (uint)segOptions.Length)
            {
                Config.Instance.MaxSegments = segOptions[(int)dropMaxSegments.Selected];
            }

            // Double click action
            Config.Instance.DoubleClickOpenFile = dropDoubleClick.Selected == 0u;

            // Theme mode
            Config.Instance.ThemeMode = (int)dropTheme.Selected;

            // Categories
            Config.Instance.Categories = new List<Category>(workingCategories);

            // Blocked hosts / exceptions
            var blockedText = txtBlockedHosts.GetText() ?? "";
            Config.Instance.BlockedHosts = blockedText.Split(new[] { ',', ' ', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(h => h.Trim().ToLowerInvariant())
                .Where(h => !string.IsNullOrEmpty(h))
                .ToArray();

            // Network timeout and retries
            if (int.TryParse(txtNetworkTimeout.GetText(), out int timeout))
            {
                Config.Instance.NetworkTimeout = Math.Max(5, timeout);
            }

            if (int.TryParse(txtMaxRetries.GetText(), out int retries))
            {
                Config.Instance.MaxRetry = Math.Max(0, retries);
            }

            var ua = txtFallbackUserAgent.GetText()?.Trim();
            if (!string.IsNullOrEmpty(ua))
            {
                Config.Instance.FallbackUserAgent = ua;
            }

            // Proxy settings
            var proxyMode = dropProxyType.Selected;
            if (proxyMode == 0u)
            {
                Config.Instance.Proxy = new ProxyInfo { ProxyType = ProxyType.Direct };
            }
            else if (proxyMode == 1u)
            {
                Config.Instance.Proxy = new ProxyInfo { ProxyType = ProxyType.System };
            }
            else
            {
                int.TryParse(txtProxyPort.GetText(), out int port);
                Config.Instance.Proxy = new ProxyInfo
                {
                    ProxyType = ProxyType.Custom,
                    Host = txtProxyHost.GetText()?.Trim() ?? "",
                    Port = port > 0 ? port : 8080,
                    UserName = txtProxyUser.GetText()?.Trim() ?? "",
                    Password = txtProxyPassword.GetText() ?? ""
                };
            }

            // Speed limit
            Config.Instance.EnableSpeedLimit = chkEnableSpeedLimit.Active;
            if (int.TryParse(txtSpeedLimit.GetText(), out int speed))
            {
                Config.Instance.DefaltDownloadSpeed = Math.Max(0, speed);
            }

            // Advanced & Automation settings
            Config.Instance.ShutdownAfterAllFinished = chkShutdownAll.Active;
            Config.Instance.RunCommandAfterCompletion = chkRunCmd.Active;
            Config.Instance.AfterCompletionCommand = txtCustomCmd.GetText()?.Trim() ?? "";
            Config.Instance.ScanWithAntiVirus = chkRunAntivirus.Active;
            Config.Instance.AntiVirusExecutable = txtAntiVirusCmd.GetText()?.Trim() ?? "";
            Config.Instance.AntiVirusArgs = txtAntiVirusArgs.GetText()?.Trim() ?? "";

            // Autostart
            try
            {
                PlatformHelper.EnableAutoStart(chkAutoStart.Active);
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Failed to update autostart setting: " + ex.Message);
            }

            Config.SaveConfig();
            // Apply the chosen theme immediately, then let the window re-read config
            Gtk4Theme.ApplyMode(window, Config.Instance.ThemeMode, persist: false);
            ApplicationContext.BroadcastConfigChange();
            window.Close();
        }

        // Displays the settings window
        public void Show() => window.Present();
    }
}
