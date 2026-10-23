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
        // Sidebar width for the page list (design token)
        private const int SidebarWidth = 220;
        // Form label column width shared by every row on every page
        private const int LabelWidth = 190;
        private readonly Window window;

        // Icon sidebar navigation
        private readonly ListBox settingsNav = ListBox.New();
        private readonly List<(ListBoxRow Row, Image Icon, string IconName)> navEntries = new();
        private bool syncingNav;

        // Stable page names, in sidebar order
        private static readonly string[] PageNames = { "general", "categories", "network", "speed", "monitoring", "advanced" };
        // Sidebar page titles, icons and accent tint
        private static readonly (string Title, string Icon)[] PageMeta =
        {
            ("General settings", "settings-3-line"),
            ("Download categories", "folder-shared-line"),
            ("Network settings", "global-line"),
            ("Speed limiter", "time-line"),
            ("Browser Monitoring", "links-line"),
            ("Advanced settings", "function-line"),
        };

        // General tab controls
        private readonly Entry txtDownloadFolder;
        private readonly Entry txtTempFolder;
        private readonly DropDown dropMaxDownloads;
        private readonly DropDown dropMaxSegments;
        private readonly DropDown dropDoubleClick;
        private readonly DropDown dropTheme;
        private readonly DropDown dropScheme;
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
            window.SetDefaultSize(860, 620);
            if (parent != null) window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            window.SetTitlebar(header);

            var mainBox = Box.New(Orientation.Vertical, 12);
            mainBox.MarginTop = 16;
            mainBox.MarginBottom = 16;
            mainBox.MarginStart = 20;
            mainBox.MarginEnd = 20;

            var stack = Stack.New();
            stack.Vexpand = true;
            stack.Hexpand = true;
            BuildNav(stack);

            // -----------------------------------------------------------------
            // --- Page 1: General ---
            // -----------------------------------------------------------------
            var generalBox = Box.New(Orientation.Vertical, 10);
            generalBox.MarginTop = 12;
            generalBox.MarginStart = 8;
            generalBox.MarginEnd = 8;

            generalBox.Append(Section(TextResource.GetText("SETTINGS_LOCATIONS") ?? "Locations"));

            var downloadFolderRow = FormRow(
                TextResource.GetText("SETTINGS_DEF_DWN_FLD") ?? "Download Folder:",
                txtDownloadFolder = Entry.New(),
                Button.NewWithLabel(TextResource.GetText("CTX_BROWSE") ?? "Browse..."));
            txtDownloadFolder.Hexpand = true;
            txtDownloadFolder.SetText(Config.Instance.DefaultDownloadFolder ?? string.Empty);
            var btnBrowseDownload = (Button)GetChildAt(downloadFolderRow, 2)!;
            btnBrowseDownload.OnClicked += (_, _) => BrowseFolder(txtDownloadFolder);
            generalBox.Append(downloadFolderRow);

            var tempFolderRow = FormRow(
                TextResource.GetText("SETTINGS_TEMP_FLD") ?? "Temporary Folder:",
                txtTempFolder = Entry.New(),
                Button.NewWithLabel(TextResource.GetText("CTX_BROWSE") ?? "Browse..."));
            txtTempFolder.Hexpand = true;
            txtTempFolder.SetText(Config.Instance.TempDir ?? string.Empty);
            var btnBrowseTemp = (Button)GetChildAt(tempFolderRow, 2)!;
            btnBrowseTemp.OnClicked += (_, _) => BrowseFolder(txtTempFolder);
            generalBox.Append(tempFolderRow);

            generalBox.Append(Section(TextResource.GetText("SETTINGS_GROUP_DOWNLOADS") ?? "Downloads"));

            // Simultaneous downloads
            var simOptions = new[] { "1", "2", "3", "4", "5", "8", "10" };
            dropMaxDownloads = DropDown.NewFromStrings(simOptions);
            var currentSim = Config.Instance.MaxParallelDownloads.ToString();
            var simIdx = Array.IndexOf(simOptions, currentSim);
            dropMaxDownloads.Selected = (uint)(simIdx >= 0 ? simIdx : 1);
            generalBox.Append(FormRow(
                TextResource.GetText("MSG_MAX_DOWNLOAD") ?? "Max Simultaneous Downloads:",
                dropMaxDownloads));

            // Segments per download
            var segOptions = new[] { "1", "2", "4", "8", "16", "32" };
            dropMaxSegments = DropDown.NewFromStrings(segOptions);
            var currentSeg = Config.Instance.MaxSegments.ToString();
            var segIdx = Array.IndexOf(segOptions, currentSeg);
            dropMaxSegments.Selected = (uint)(segIdx >= 0 ? segIdx : 3);
            generalBox.Append(FormRow(
                TextResource.GetText("SETTINGS_MAX_SEGMENTS") ?? "Max Segments per Download:",
                dropMaxSegments));

            // Double click action
            var dblOptions = new[] { "Open downloaded file", "Open destination folder" };
            dropDoubleClick = DropDown.NewFromStrings(dblOptions);
            dropDoubleClick.Selected = Config.Instance.DoubleClickOpenFile ? 0u : 1u;
            generalBox.Append(FormRow(
                TextResource.GetText("MSG_DOUBLE_CLICK_ACTION") ?? "Double-Click Action:",
                dropDoubleClick));

            generalBox.Append(CheckRow(chkStartAuto = CheckButton.NewWithLabel(TextResource.GetText("LBL_START_AUTO") ?? "Start download automatically without prompt")));
            chkStartAuto.Active = Config.Instance.StartDownloadAutomatically;

            generalBox.Append(CheckRow(chkTimestamp = CheckButton.NewWithLabel(TextResource.GetText("LBL_GET_TIMESTAMP") ?? "Get file creation timestamp from server")));
            chkTimestamp.Active = Config.Instance.FetchServerTimeStamp;

            generalBox.Append(CheckRow(chkKeepAwake = CheckButton.NewWithLabel(TextResource.GetText("MSG_AWAKE") ?? "Prevent hibernate or sleep during downloads")));
            chkKeepAwake.Active = Config.Instance.KeepPCAwake;

            generalBox.Append(Section(TextResource.GetText("SETTINGS_GROUP_APPEARANCE") ?? "Appearance"));

            // Theme selection: indices follow Config.ThemeMode (0 light, 1 dark, 2 follow system)
            var themeOptions = new[] { "Light Theme", "Dark Theme", "Follow System" };
            dropTheme = DropDown.NewFromStrings(themeOptions);
            dropTheme.Selected = (uint)Math.Clamp(Config.Instance.ThemeMode, 0, 2);
            generalBox.Append(FormRow(
                TextResource.GetText("SETTINGS_THEME") ?? "Appearance Theme:",
                dropTheme));

            // Color scheme: one of the 7 curated palettes for the active mode
            dropScheme = DropDown.NewFromStrings(ColorSchemeNames(Gtk4Theme.IsDarkActive));
            dropScheme.Selected = (uint)ColorSchemeTable.ClampSchemeIndex(
                Gtk4Theme.IsDarkActive, Config.Instance.ColorScheme);
            generalBox.Append(FormRow(
                TextResource.GetText("SETTINGS_COLOR_SCHEME") ?? "Color Scheme:",
                dropScheme));

            // Keep the scheme list in step with the mode, and preview both live
            dropTheme.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() == "selected")
                {
                    RepopulateSchemes();
                    ApplySchemePreview();
                }
            };
            dropScheme.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() == "selected")
                {
                    ApplySchemePreview();
                }
            };

            generalBox.Append(Section(TextResource.GetText("SETTINGS_GROUP_NOTIFICATIONS") ?? "Notifications"));

            generalBox.Append(CheckRow(chkNotification = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_SHOW_NOTIFICATION") ?? "Show desktop notifications on completion")));
            chkNotification.Active = Config.Instance.ShowNotification;

            generalBox.Append(CheckRow(chkShowComplete = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_SHOW_COMPLETE") ?? "Show download complete popup dialog")));
            chkShowComplete.Active = Config.Instance.ShowDownloadCompleteWindow;

            generalBox.Append(CheckRow(chkShowProgress = CheckButton.NewWithLabel(TextResource.GetText("LBL_SHOW_PROGRESS") ?? "Show download progress window")));
            chkShowProgress.Active = Config.Instance.ShowProgressWindow;

            generalBox.Append(CheckRow(chkPlaySound = CheckButton.NewWithLabel(TextResource.GetText("MSG_PLAY_SOUND") ?? "Play sound when download finishes")));
            chkPlaySound.Active = Config.Instance.PlayCompletionSound;

            generalBox.Append(Section(TextResource.GetText("SETTINGS_GROUP_SYSTEM") ?? "System"));

            generalBox.Append(CheckRow(chkAutoStart = CheckButton.NewWithLabel(TextResource.GetText("AUTO_START") ?? "Launch FetchFlow when system starts")));
            chkAutoStart.Active = PlatformHelper.IsAutoStartEnabled();

            var generalScroll = ScrolledWindow.New();
            generalScroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            generalScroll.SetChild(generalBox);
            stack.AddTitled(generalScroll, "general", TextResource.GetText("SETTINGS_GENERAL") ?? "General");

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

            var catFolderRow = FormRow(
                TextResource.GetText("SETTINGS_TARGET_FOLDER") ?? "Target Folder:",
                txtCatFolder = Entry.New(),
                Button.NewWithLabel(TextResource.GetText("CTX_BROWSE") ?? "Browse..."));
            txtCatFolder.Hexpand = true;
            var btnBrowseCatFolder = (Button)GetChildAt(catFolderRow, 2)!;
            btnBrowseCatFolder.OnClicked += (_, _) => BrowseFolder(txtCatFolder);
            catDetailBox.Append(catFolderRow);

            txtCatExts = Entry.New();
            txtCatExts.Hexpand = true;
            catDetailBox.Append(FormRow(
                TextResource.GetText("SETTINGS_FILE_EXT") ?? "File Extensions:",
                txtCatExts));

            var catHint = Label.New(TextResource.GetText("MSG_EXT_HINT") ?? "Separate extensions with spaces or commas (e.g. mp4, mkv, webm)");
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
            var btnResetCats = Button.NewWithLabel(TextResource.GetText("BTN_RESTORE_CATEGORIES") ?? "Restore Default Categories");
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
            var btnResetExceptions = Button.NewWithLabel(TextResource.GetText("BTN_RESTORE_SITES") ?? "Restore Default Sites");
            btnResetExceptions.OnClicked += (_, _) =>
            {
                txtBlockedHosts.SetText(string.Join(", ", Config.DefaultBlockedHosts));
            };
            exceptionsRow.Append(txtBlockedHosts);
            exceptionsRow.Append(btnResetExceptions);
            categoriesBox.Append(exceptionsRow);

            var excHint = Label.New(TextResource.GetText("MSG_DOMAIN_HINT") ?? "Separate domains with commas or spaces (e.g. *.bank.com, example.com)");
            excHint.AddCssClass("dim-label");
            excHint.Halign = Align.Start;
            categoriesBox.Append(excHint);

            LoadCategoryFields(0);

            var catScroll = ScrolledWindow.New();
            catScroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            catScroll.SetChild(categoriesBox);
            stack.AddTitled(catScroll, "categories", TextResource.GetText("SETTINGS_CAT") ?? "Categories");

            // -----------------------------------------------------------------
            // --- Page 3: Network & Proxy ---
            // -----------------------------------------------------------------
            var networkBox = Box.New(Orientation.Vertical, 12);
            networkBox.MarginTop = 12;
            networkBox.MarginStart = 8;
            networkBox.MarginEnd = 8;

            networkBox.Append(Section(TextResource.GetText("SETTINGS_GROUP_CONNECTION") ?? "Connection"));

            txtNetworkTimeout = Entry.New();
            txtNetworkTimeout.WidthRequest = 80;
            txtNetworkTimeout.SetText(Config.Instance.NetworkTimeout.ToString());
            networkBox.Append(FormRow(
                TextResource.GetText("SETTINGS_NET_TIMEOUT") ?? "Network Timeout (sec):",
                txtNetworkTimeout));

            txtMaxRetries = Entry.New();
            txtMaxRetries.WidthRequest = 80;
            txtMaxRetries.SetText(Config.Instance.MaxRetry.ToString());
            networkBox.Append(FormRow(
                TextResource.GetText("SETTINGS_RETRY") ?? "Max Retry Attempts:",
                txtMaxRetries));

            var uaRow = FormRow(
                TextResource.GetText("SETTINGS_FALLBACK_UA") ?? "Fallback User-Agent:",
                txtFallbackUserAgent = Entry.New(),
                Button.NewWithLabel(TextResource.GetText("BTN_RESET") ?? "Reset"));
            txtFallbackUserAgent.Hexpand = true;
            txtFallbackUserAgent.SetText(Config.Instance.FallbackUserAgent ?? Config.DefaultFallbackUserAgent);
            var btnResetUa = (Button)GetChildAt(uaRow, 2)!;
            btnResetUa.OnClicked += (_, _) => txtFallbackUserAgent.SetText(Config.DefaultFallbackUserAgent);
            networkBox.Append(uaRow);

            networkBox.Append(Section(TextResource.GetText("SETTINGS_PROXY_CONF") ?? "Proxy"));

            var proxyOptions = new[] { "No Proxy (Direct Connection)", "Use System Proxy", "Manual Proxy Configuration" };
            var initialProxyType = Config.Instance.Proxy.HasValue ? Config.Instance.Proxy.Value.ProxyType : ProxyType.Direct;
            var proxyTypeRow = FormRow(
                TextResource.GetText("SETTINGS_PROXY_MODE") ?? "Proxy Mode:",
                dropProxyType = DropDown.NewFromStrings(proxyOptions));
            dropProxyType.Selected = initialProxyType switch
            {
                ProxyType.Direct => 0u,
                ProxyType.System => 1u,
                ProxyType.Custom => 2u,
                _ => 0u
            };
            networkBox.Append(proxyTypeRow);

            proxyManualBox = Box.New(Orientation.Vertical, 8);
            proxyManualBox.MarginStart = 16;

            var hostRow = FormRow(
                TextResource.GetText("SETTINGS_PROXY_HOST") ?? "Proxy Host:",
                txtProxyHost = Entry.New());
            txtProxyHost.Hexpand = true;
            txtProxyHost.SetText(Config.Instance.Proxy?.Host ?? "");
            proxyManualBox.Append(hostRow);

            var portRow = FormRow(
                TextResource.GetText("SETTINGS_PROXY_PORT") ?? "Proxy Port:",
                txtProxyPort = Entry.New());
            txtProxyPort.WidthRequest = 100;
            txtProxyPort.SetText((Config.Instance.Proxy?.Port ?? 8080).ToString());
            proxyManualBox.Append(portRow);

            var userRow = FormRow(
                TextResource.GetText("LBL_USERNAME") ?? "Username:",
                txtProxyUser = Entry.New());
            txtProxyUser.Hexpand = true;
            txtProxyUser.SetText(Config.Instance.Proxy?.UserName ?? "");
            proxyManualBox.Append(userRow);

            var passRow = FormRow(
                TextResource.GetText("LBL_PASSWORD") ?? "Password:",
                txtProxyPassword = Entry.New());
            txtProxyPassword.Hexpand = true;
            txtProxyPassword.Visibility = false;
            txtProxyPassword.SetText(Config.Instance.Proxy?.Password ?? "");
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
            stack.AddTitled(netScroll, "network", TextResource.GetText("SETTINGS_NETWORK") ?? "Network");

            // -----------------------------------------------------------------
            // --- Page 4: Speed Limiter ---
            // -----------------------------------------------------------------
            var speedBox = Box.New(Orientation.Vertical, 12);
            speedBox.MarginTop = 12;
            speedBox.MarginStart = 8;
            speedBox.MarginEnd = 8;

            chkEnableSpeedLimit = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_ENABLE_LIMIT") ?? "Enable maximum download speed limit");
            chkEnableSpeedLimit.Active = Config.Instance.EnableSpeedLimit;
            speedBox.Append(Section(TextResource.GetText("SETTINGS_GROUP_LIMIT") ?? "Limit"));
            speedBox.Append(chkEnableSpeedLimit);

            txtSpeedLimit = Entry.New();
            txtSpeedLimit.WidthRequest = 120;
            txtSpeedLimit.SetText(Config.Instance.DefaltDownloadSpeed.ToString());
            speedBox.Append(FormRow(
                TextResource.GetText("LBL_SPEED_KB") ?? "Speed Limit (KB/s):",
                txtSpeedLimit));

            var presetBox = Box.New(Orientation.Horizontal, 8);
            var lblPresets = Label.New(TextResource.GetText("LBL_PRESETS") ?? "Quick Presets:");
            lblPresets.WidthRequest = LabelWidth;
            lblPresets.Halign = Align.Start;
            lblPresets.Valign = Align.Center;
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

            stack.AddTitled(speedBox, "speed", TextResource.GetText("MENU_SPEED_LIMITER") ?? "Speed Limiter");

            // -----------------------------------------------------------------
            // --- Page 5: Monitoring ---
            // -----------------------------------------------------------------
            var monitoringBox = Box.New(Orientation.Vertical, 12);
            monitoringBox.MarginTop = 12;
            monitoringBox.MarginStart = 8;
            monitoringBox.MarginEnd = 8;

            chkMonitorClipboard = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_MONITOR_CLIPBOARD") ?? "Monitor clipboard for downloadable URLs");
            chkMonitorClipboard.Active = Config.Instance.MonitorClipboard;
            monitoringBox.Append(Section(TextResource.GetText("SETTINGS_GROUP_CLIPBOARD") ?? "Clipboard"));
            monitoringBox.Append(chkMonitorClipboard);

            monitoringBox.Append(Section(TextResource.GetText("SETTINGS_GROUP_BROWSER") ?? "Browser Integration"));

            var lblBrowserInfo = Label.New(TextResource.GetText("MSG_BROWSER_INTEGRATION") ?? "Browser Integration: FetchFlow intercepts downloads automatically via its browser extension. Extensions are available for Google Chrome, Mozilla Firefox, Brave, and Edge.");
            lblBrowserInfo.Halign = Align.Start;
            lblBrowserInfo.Wrap = true;
            monitoringBox.Append(lblBrowserInfo);

            var storeButtonsRow = Box.New(Orientation.Horizontal, 10);
            var btnCws = Button.NewWithLabel(TextResource.GetText("LBL_CWS_EXT") ?? "Chrome Web Store Extension");
            btnCws.OnClicked += (_, _) => PlatformHelper.OpenBrowser("https://chromewebstore.google.com");
            var btnAmo = Button.NewWithLabel(TextResource.GetText("LBL_AMO_EXT") ?? "Firefox Add-ons (AMO)");
            btnAmo.OnClicked += (_, _) => PlatformHelper.OpenBrowser("https://addons.mozilla.org");

            storeButtonsRow.Append(btnCws);
            storeButtonsRow.Append(btnAmo);
            monitoringBox.Append(storeButtonsRow);

            monitoringBox.Append(Section(TextResource.GetText("SETTINGS_GROUP_IPC") ?? "IPC Relay"));

            var ipcBox = Box.New(Orientation.Horizontal, 10);
            lblIpcStatus = Label.New($"● IPC Relay: 127.0.0.1:{IpcHttpMessageProcessor.EffectivePort}");
            lblIpcStatus.Halign = Align.Start;
            lblIpcStatus.Hexpand = true;
            lblIpcStatus.AddCssClass("dim-label");

            btnRestartIpc = Button.NewWithLabel(TextResource.GetText("BTN_RESTART_IPC") ?? "Restart IPC Relay");
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

            stack.AddTitled(monitoringBox, "monitoring", TextResource.GetText("SETTINGS_MONITORING") ?? "Monitoring");

            // -----------------------------------------------------------------
            // --- Page 6: Advanced & Automation ---
            // -----------------------------------------------------------------
            var advancedBox = Box.New(Orientation.Vertical, 12);
            advancedBox.MarginTop = 12;
            advancedBox.MarginStart = 8;
            advancedBox.MarginEnd = 8;

            var automationHeader = Section(TextResource.GetText("SETTINGS_AUTOMATION") ?? "Post-Download Automation");
            advancedBox.Append(automationHeader);

            chkShutdownAll = CheckButton.NewWithLabel(TextResource.GetText("MSG_HALT") ?? "Shutdown computer after all downloads complete");
            chkShutdownAll.Active = Config.Instance.ShutdownAfterAllFinished;
            advancedBox.Append(chkShutdownAll);

            chkRunCmd = CheckButton.NewWithLabel(TextResource.GetText("EXEC_CMD") ?? "Run a program after all downloads finish");
            chkRunCmd.Active = Config.Instance.RunCommandAfterCompletion;
            advancedBox.Append(chkRunCmd);

            var cmdRow = FormRow(
                TextResource.GetText("LBL_COMMAND") ?? "Command:",
                txtCustomCmd = Entry.New());
            txtCustomCmd.Hexpand = true;
            txtCustomCmd.SetText(Config.Instance.AfterCompletionCommand ?? "");
            cmdRow.MarginStart = 24;
            advancedBox.Append(cmdRow);

            var sepAdv = Separator.New(Orientation.Horizontal);
            sepAdv.MarginTop = 6;
            sepAdv.MarginBottom = 6;
            advancedBox.Append(sepAdv);

            advancedBox.Append(Section(TextResource.GetText("SETTINGS_ANTIVIRUS") ?? "Antivirus Scanning"));

            chkRunAntivirus = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_SCAN_FILES") ?? "Scan downloaded files with antivirus scanner");
            chkRunAntivirus.Active = Config.Instance.ScanWithAntiVirus;
            advancedBox.Append(chkRunAntivirus);

            var avRow = FormRow(
                TextResource.GetText("ANTIVIR_CMD") ?? "Antivirus Executable:",
                txtAntiVirusCmd = Entry.New(),
                Button.NewWithLabel(TextResource.GetText("CTX_BROWSE") ?? "Browse..."));
            txtAntiVirusCmd.Hexpand = true;
            txtAntiVirusCmd.SetText(Config.Instance.AntiVirusExecutable ?? "");
            var btnBrowseAv = (Button)GetChildAt(avRow, 2)!;
            btnBrowseAv.OnClicked += (_, _) => BrowseFile(txtAntiVirusCmd);
            avRow.MarginStart = 24;
            advancedBox.Append(avRow);

            var argsRow = FormRow(
                TextResource.GetText("ANTIVIR_ARGS") ?? "Command line parameters:",
                txtAntiVirusArgs = Entry.New());
            txtAntiVirusArgs.Hexpand = true;
            txtAntiVirusArgs.SetText(Config.Instance.AntiVirusArgs ?? "[file]");
            argsRow.MarginStart = 24;
            advancedBox.Append(argsRow);

            var avHint = Label.New(TextResource.GetText("MSG_AV_HINT") ?? "Use [file] placeholder for the downloaded file path (e.g. clamscan [file])");
            avHint.AddCssClass("dim-label");
            avHint.MarginStart = 24;
            avHint.Halign = Align.Start;
            advancedBox.Append(avHint);

            var advScroll = ScrolledWindow.New();
            advScroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            advScroll.SetChild(advancedBox);
            stack.AddTitled(advScroll, "advanced", TextResource.GetText("SETTINGS_ADV") ?? "Advanced settings");

            if (initialPage >= 0 && initialPage < PageNames.Length)
            {
                stack.SetVisibleChildName(PageNames[initialPage]);
            }
            SelectNavRow(PageNames[Math.Clamp(initialPage, 0, PageNames.Length - 1)]);

            // Icon sidebar + page content, like the main window
            var contentRow = Box.New(Orientation.Horizontal, 0);
            var navScroll = ScrolledWindow.New();
            navScroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            navScroll.SetChild(settingsNav);
            navScroll.WidthRequest = SidebarWidth;
            contentRow.Append(navScroll);
            contentRow.Append(stack);
            contentRow.Vexpand = true;
            mainBox.Append(contentRow);

            // --- Action Buttons: header bar, as GNOME dialogs do ---
            var cancelBtn = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            cancelBtn.OnClicked += (_, _) => window.Close();

            var saveBtn = Button.NewWithLabel(TextResource.GetText("DESC_SAVE_Q") ?? "Save");
            saveBtn.AddCssClass("suggested-action");
            saveBtn.OnClicked += (_, _) => SaveSettings();

            header.PackStart(cancelBtn);
            header.PackEnd(saveBtn);

            window.SetChild(mainBox);
        }

        // One scheme name per curated palette for the given mode
        private static string[] ColorSchemeNames(bool dark) =>
            (dark ? ColorSchemeTable.DarkSchemes : ColorSchemeTable.LightSchemes)
                .Select(scheme => scheme.DisplayName)
                .ToArray();

        // Repopulates the scheme list after a theme-mode change, keeping the index valid
        private void RepopulateSchemes()
        {
            var mode = dropTheme.Selected == uint.MaxValue ? Gtk4Theme.ModeFollowSystem : (int)dropTheme.Selected;
            var dark = mode switch
            {
                Gtk4Theme.ModeDark => true,
                Gtk4Theme.ModeLight => false,
                _ => Gtk4Theme.IsDarkActive
            };

            var previous = dropScheme.Selected == uint.MaxValue ? 0 : (int)dropScheme.Selected;
            dropScheme.SetModel(StringList.New(ColorSchemeNames(dark)));
            dropScheme.Selected = (uint)ColorSchemeTable.ClampSchemeIndex(dark, previous);
        }

        // Applies the selected mode and scheme to the running window immediately
        private void ApplySchemePreview()
        {
            if (dropScheme.Selected != uint.MaxValue)
            {
                Config.Instance.ColorScheme = (int)dropScheme.Selected;
            }
            var mode = dropTheme.Selected == uint.MaxValue ? Gtk4Theme.ModeFollowSystem : (int)dropTheme.Selected;
            Gtk4Theme.ApplyMode(window, mode, persist: false);
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

        // Commits the dialog without letting a config/IO failure kill the app:
        // an exception escaping a GObject signal handler terminates the process.
        private void SaveSettings()
        {
            try
            {
                SaveSettingsCore();
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Saving settings failed: " + ex.Message);
                Gtk4DialogHelper.ShowAlert(window,
                    (TextResource.GetText("MSG_SETTINGS_SAVE_FAILED") ?? "Settings could not be saved.")
                    + "\n\n" + ex.Message);
            }
        }

        // Writes every control into Config and persists it
        private void SaveSettingsCore()
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

            // Theme mode and colour scheme
            Config.Instance.ThemeMode = (int)dropTheme.Selected;
            if (dropScheme.Selected != uint.MaxValue)
            {
                Config.Instance.ColorScheme = (int)dropScheme.Selected;
            }

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

        // Icon sidebar row for one settings page
        private void BuildNav(Stack stack)
        {
            settingsNav.AddCssClass("navigation-sidebar");
            settingsNav.SelectionMode = SelectionMode.Browse;
            for (var i = 0; i < PageNames.Length; i++)
            {
                var (title, iconName) = PageMeta[i];
                var rowBox = Box.New(Orientation.Horizontal, 10);
                rowBox.MarginTop = 6;
                rowBox.MarginBottom = 6;
                rowBox.MarginStart = 8;
                rowBox.MarginEnd = 8;

                var (ar, ag, ab) = AccentRgb();
                var icon = Gtk4IconHelper.CreateTintedIcon(iconName, ar, ag, ab, 18);
                icon.Valign = Align.Center;

                var label = Label.New(title);
                label.Halign = Align.Start;
                label.Valign = Align.Center;
                label.Hexpand = true;

                rowBox.Append(icon);
                rowBox.Append(label);

                var row = ListBoxRow.New();
                row.SetChild(rowBox);
                row.Name = PageNames[i];
                row.TooltipText = title;
                navEntries.Add((row, icon, iconName));
                settingsNav.Append(row);
            }

            settingsNav.OnRowSelected += (_, args) =>
            {
                if (syncingNav)
                {
                    return;
                }
                if (args.Row is ListBoxRow row && !string.IsNullOrEmpty(row.Name))
                {
                    stack.SetVisibleChildName(row.Name);
                    UpdateNavIcons();
                }
            };
        }

        // Selects a sidebar row without re-triggering the page switch
        private void SelectNavRow(string key)
        {
            syncingNav = true;
            try
            {
                for (var child = settingsNav.GetFirstChild(); child != null; child = child.GetNextSibling())
                {
                    if (child is ListBoxRow row && row.Name == key)
                    {
                        settingsNav.SelectRow(row);
                        break;
                    }
                }
            }
            finally
            {
                syncingNav = false;
            }
            UpdateNavIcons();
        }

        // Keeps nav icons legible: white on the accent row, accent otherwise
        private void UpdateNavIcons()
        {
            var (ar, ag, ab) = AccentRgb();
            foreach (var (row, icon, iconName) in navEntries)
            {
                var selected = row.IsSelected();
                Gtk4IconHelper.UpdateTintedIcon(icon, iconName,
                    selected ? (byte)255 : ar,
                    selected ? (byte)255 : ag,
                    selected ? (byte)255 : ab, 18);
            }
        }

        // Accent for the active colour scheme, matching Gtk4Theme.ApplyColorScheme
        private static (byte R, byte G, byte B) AccentRgb()
        {
            try
            {
                var index = ColorSchemeTable.ClampSchemeIndex(Gtk4Theme.IsDarkActive, Config.Instance.ColorScheme);
                return Gtk4IconHelper.ParseHex(ColorSchemeTable.GetScheme(Gtk4Theme.IsDarkActive, index).AccentHex);
            }
            catch
            {
                return (53, 132, 228);
            }
        }

        // Section heading inside a settings page
        private static Label Section(string text)
        {
            var label = Label.New(text);
            label.AddCssClass("heading");
            label.Halign = Align.Start;
            label.MarginTop = 8;
            return label;
        }

        // Uniform two-column form row: fixed label column, expanding control, optional action
        private static Box FormRow(string labelText, Widget control, Widget? action = null)
        {
            var row = Box.New(Orientation.Horizontal, 12);
            var label = Label.New(labelText);
            label.WidthRequest = LabelWidth;
            label.Halign = Align.Start;
            label.Valign = Align.Center;
            row.Append(label);
            row.Append(control);
            if (action != null)
            {
                row.Append(action);
            }
            return row;
        }

        // Checkbox aligned with the control column so it lines up with the rows above
        private static Box CheckRow(CheckButton check)
        {
            var row = Box.New(Orientation.Horizontal, 12);
            var spacer = Label.New("");
            spacer.WidthRequest = LabelWidth;
            row.Append(spacer);
            row.Append(check);
            return row;
        }

        // Nth child of a widget, used to reach a button placed by FormRow
        private static Widget? GetChildAt(Widget widget, int index)
        {
            var child = widget is Box box ? box.GetFirstChild() : widget.GetFirstChild();
            for (var i = 0; i < index && child != null; i++)
            {
                child = child.GetNextSibling();
            }
            return child;
        }
    }
}
