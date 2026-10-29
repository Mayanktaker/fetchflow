// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 Settings dialog, GNOME-native layout: an icon tab strip under the header,
// grouped boxed-card rows (title + subtitle + control) and immediate apply — there
// is no Save button, matching GNOME preferences. Text fields commit on Enter, on
// focus loss and when the window closes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gtk;
using Newtonsoft.Json;
using Translations;
using XDM.Core;
using XDM.Core.BrowserMonitoring;
using XDM.Core.UI;
using XDM.Core.Util;
using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    internal sealed class SettingsDialog
    {
        private const int PageWidth = 720;

        private readonly Window window;
        private readonly Stack stack;
        private readonly Box tabs;

        // Page identifiers, in tab order
        private static readonly string[] PageNames = { "general", "categories", "network", "speed", "monitoring", "credentials", "advanced" };
        private static readonly (string Title, string Icon)[] PageMeta =
        {
            ("General settings", "settings-3-line"),
            ("Download categories", "folder-shared-line"),
            ("Network settings", "global-line"),
            ("Speed limiter", "time-line"),
            ("Browser Monitoring", "links-line"),
            ("Credentials", "dialog-password"),
            ("Advanced settings", "function-line"),
        };

        // General: folder paths shown as row subtitles
        private string downloadFolderPath = "";
        private string tempFolderPath = "";
        private readonly Label lblDownloadPath = Label.New("");
        private readonly Label lblTempPath = Label.New("");

        private readonly DropDown dropMaxDownloads;
        private readonly DropDown dropMaxSegments;
        private readonly DropDown dropDoubleClick;
        private readonly DropDown dropTheme;
        private readonly DropDown dropScheme;
        private readonly DropDown dropMediaDebounce;
        private static readonly string[] DebounceLabels = new[] { "1 second", "2 seconds (Recommended)", "3 seconds", "5 seconds" };
        private static readonly int[] DebounceValues = new[] { 1, 2, 3, 5 };
        private readonly DropDown dropNotificationTimeout;
        private static readonly string[] TimeoutLabels = new[] { "5 seconds", "10 seconds (Recommended)", "15 seconds", "30 seconds" };
        private static readonly int[] TimeoutValues = new[] { 5000, 10000, 15000, 30000 };
        private readonly Switch chkAutoStart;
        private readonly Switch chkNotification;
        private readonly Switch chkMediaNotification;
        private readonly Switch chkShowComplete;
        private readonly Switch chkShowProgress;
        private readonly Switch chkPlaySound;
        private readonly Switch chkStartAuto;
        private readonly Switch chkTimestamp;
        private readonly Switch chkKeepAwake;
        private readonly Switch chkOverwrite;
        private readonly Switch chkAutoCat;
        private readonly Label lblPaletteStatus;

        // Categories
        private readonly ListBox categoryList;
        private readonly Entry txtCatFolder;
        private readonly Entry txtCatExts;
        private readonly Label lblSelectedCat;
        private readonly List<Category> workingCategories;
        private int selectedCategoryIndex;
        private readonly Entry txtBlockedHosts;

        // Network
        private readonly Entry txtNetworkTimeout;
        private readonly Entry txtMaxRetries;
        private readonly Entry txtFallbackUserAgent;
        private readonly DropDown dropProxyType;
        private readonly Entry txtProxyHost;
        private readonly Entry txtProxyPort;
        private readonly Entry txtProxyUser;
        private readonly Entry txtProxyPassword;
        private readonly ListBox proxyManualGroup;

        // Speed limiter
        private readonly Switch chkEnableSpeedLimit;
        private readonly Entry txtSpeedLimit;

        // Monitoring
        private readonly Switch chkMonitorClipboard;
        private readonly Label lblIpcStatus;

        // Credentials (working copy, written back on Persist)
        private readonly List<XDM.Core.PasswordEntry> workingCredentials;
        private readonly ListBox credentialList;
        private readonly Label lblCredentialEmpty;
        private readonly Button btnCredentialEdit;
        private readonly Button btnCredentialDelete;

        // Advanced
        private readonly Switch chkShutdownAll;
        private readonly Switch chkRunCmd;
        private readonly Entry txtCustomCmd;
        private readonly Switch chkRunAntivirus;
        private readonly Entry txtAntiVirusCmd;
        private readonly Entry txtAntiVirusArgs;

        // Guards config writes while widgets are being populated
        private bool loading = true;
        // Last height FitToPage applied; smaller deltas are skipped as flicker
        private int lastFittedHeight;
        // Resizes below this many pixels read as jitter, not as fitting
        private const int FitStep = 48;

        // Builds the dialog from current configuration
        public SettingsDialog(Window? parent, int initialPage = 0)
        {
            window = Window.New();
            window.Title = TextResource.GetText("TITLE_SETTINGS") ?? "Settings";
            window.Modal = true;
            window.SetDefaultSize(900, 600);
            if (parent != null) window.SetTransientFor(parent);

            // Header: title only. Closing uses the native window control, and the
            // close request persists first, so no duplicate Close button is needed.
            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            window.SetTitlebar(header);
            window.OnCloseRequest += (_, _) =>
            {
                Persist();
                return false;
            };

            // Tab strip (linked toggle buttons with icons), like GNOME view switchers
            tabs = Box.New(Orientation.Horizontal, 0);
            tabs.AddCssClass("linked");
            tabs.Halign = Align.Center;
            tabs.MarginTop = 8;
            tabs.MarginBottom = 8;

            // Category/network/etc. controls are created by the page builders
            workingCategories = Config.Instance.Categories.Select(c => new Category
            {
                Name = c.Name,
                DisplayName = c.DisplayName,
                DefaultFolder = c.DefaultFolder,
                FileExtensions = new HashSet<string>(c.FileExtensions),
                CustomIcon = c.CustomIcon,
                CustomColor = c.CustomColor
            }).ToList();

            downloadFolderPath = Config.Instance.DefaultDownloadFolder ?? "";
            tempFolderPath = Config.Instance.TempDir ?? "";
            lblDownloadPath.SetText(downloadFolderPath);
            lblDownloadPath.AddCssClass("caption");
            lblDownloadPath.AddCssClass("dim-label");
            lblDownloadPath.Ellipsize = Pango.EllipsizeMode.Middle;
            lblDownloadPath.Halign = Align.Start;
            lblTempPath.SetText(tempFolderPath);
            lblTempPath.AddCssClass("caption");
            lblTempPath.AddCssClass("dim-label");
            lblTempPath.Ellipsize = Pango.EllipsizeMode.Middle;
            lblTempPath.Halign = Align.Start;

            var simOptions = new[] { "1", "2", "3", "4", "5", "8", "10" };
            dropMaxDownloads = DropDown.NewFromStrings(simOptions);
            var simIdx = Array.IndexOf(simOptions, Config.Instance.MaxParallelDownloads.ToString());
            dropMaxDownloads.Selected = (uint)(simIdx >= 0 ? simIdx : 1);

            var segOptions = new[] { "1", "2", "4", "8", "16", "32" };
            dropMaxSegments = DropDown.NewFromStrings(segOptions);
            var segIdx = Array.IndexOf(segOptions, Config.Instance.MaxSegments.ToString());
            dropMaxSegments.Selected = (uint)(segIdx >= 0 ? segIdx : 3);

            dropDoubleClick = DropDown.NewFromStrings(new[] { "Open downloaded file", "Open destination folder" });
            dropDoubleClick.Selected = Config.Instance.DoubleClickOpenFile ? 0u : 1u;

            dropTheme = DropDown.NewFromStrings(new[] { "Light Theme", "Dark Theme", "Follow System" });
            dropTheme.Selected = (uint)ThemeModes.Clamp(Config.Instance.ThemeMode);

            dropScheme = DropDown.NewFromStrings(ColorSchemeNames(Gtk4Theme.IsDarkActive));
            dropScheme.Selected = (uint)ColorSchemeTable.ClampSchemeIndex(Gtk4Theme.IsDarkActive, Config.Instance.ColorScheme);

            chkAutoStart = Toggle("AUTO_START", "Launch FetchFlow when system starts");
            chkAutoStart.Active = PlatformHelper.IsAutoStartEnabled();
            chkNotification = Toggle("SETTINGS_SHOW_NOTIFICATION", "Show desktop notifications on completion");
            chkNotification.Active = Config.Instance.ShowNotification;
            chkMediaNotification = Toggle("MSG_SHOW_MEDIA_NOTIFICATION", "Show notification when video is detected");
            chkMediaNotification.Active = Config.Instance.ShowMediaNotification;

            dropMediaDebounce = DropDown.NewFromStrings(DebounceLabels);
            var curDebounce = Config.Instance.MediaNotificationDebounceSeconds;
            var dIdx = Array.IndexOf(DebounceValues, curDebounce);
            dropMediaDebounce.Selected = (uint)(dIdx >= 0 ? dIdx : 1);

            dropNotificationTimeout = DropDown.NewFromStrings(TimeoutLabels);
            var curTimeout = Config.Instance.NotificationTimeOut > 0 ? Config.Instance.NotificationTimeOut : 10000;
            var tIdx = Array.IndexOf(TimeoutValues, curTimeout);
            dropNotificationTimeout.Selected = (uint)(tIdx >= 0 ? tIdx : 1);
            chkShowComplete = Toggle("SETTINGS_SHOW_COMPLETE", "Show download complete popup dialog");
            chkShowComplete.Active = Config.Instance.ShowDownloadCompleteWindow;
            chkShowProgress = Toggle("LBL_SHOW_PROGRESS", "Show download progress window");
            chkShowProgress.Active = Config.Instance.ShowProgressWindow;
            chkPlaySound = Toggle("MSG_PLAY_SOUND", "Play sound when download finishes");
            chkPlaySound.Active = Config.Instance.PlayCompletionSound;
            chkStartAuto = Toggle("LBL_START_AUTO", "Start download automatically without prompt");
            chkStartAuto.Active = Config.Instance.StartDownloadAutomatically;
            chkTimestamp = Toggle("LBL_GET_TIMESTAMP", "Get file creation timestamp from server");
            chkTimestamp.Active = Config.Instance.FetchServerTimeStamp;
            chkKeepAwake = Toggle("MSG_AWAKE", "Prevent hibernate or sleep during downloads");
            chkKeepAwake.Active = Config.Instance.KeepPCAwake;
            chkOverwrite = Toggle("LBL_OVERWRITE_EXISTING", "Overwrite existing files");
            chkOverwrite.Active = Config.Instance.FileConflictResolution == FileConflictResolution.Overwrite;
            chkAutoCat = Toggle("SETTINGS_ATUO_CAT", "Automatically select folder by category");
            chkAutoCat.Active = Config.Instance.FolderSelectionMode == FolderSelectionMode.Auto;
            lblPaletteStatus = Label.New("");
            lblPaletteStatus.AddCssClass("caption");
            lblPaletteStatus.AddCssClass("dim-label");
            lblPaletteStatus.Halign = Align.Start;
            lblPaletteStatus.Wrap = true;

            // Categories
            categoryList = ListBox.New();
            categoryList.AddCssClass("navigation-sidebar");
            categoryList.SelectionMode = SelectionMode.Browse;
            categoryList.WidthRequest = 190;
            foreach (var cat in workingCategories)
            {
                var (iconName, r, g, b) = CategoryIconPalette.Resolve(cat);
                var rowBox = Box.New(Orientation.Horizontal, 8);
                rowBox.MarginTop = 4;
                rowBox.MarginBottom = 4;
                rowBox.Append(Gtk4IconHelper.CreateTintedIcon(iconName, r, g, b, 16));
                var lbl = Label.New(cat.DisplayName);
                lbl.Halign = Align.Start;
                rowBox.Append(lbl);
                var row = ListBoxRow.New();
                row.SetChild(rowBox);
                categoryList.Append(row);
            }
            lblSelectedCat = Label.New("");
            lblSelectedCat.AddCssClass("heading");
            lblSelectedCat.Halign = Align.Start;
            txtCatFolder = Entry.New();
            txtCatFolder.Hexpand = true;
            txtCatExts = Entry.New();
            txtCatExts.Hexpand = true;
            txtBlockedHosts = Entry.New();
            txtBlockedHosts.Hexpand = true;
            txtBlockedHosts.SetText(string.Join(", ", Config.Instance.BlockedHosts ?? Config.DefaultBlockedHosts));

            // Network
            txtNetworkTimeout = IntEntry(Config.Instance.NetworkTimeout);
            txtMaxRetries = IntEntry(Config.Instance.MaxRetry);
            txtFallbackUserAgent = Entry.New();
            txtFallbackUserAgent.Hexpand = true;
            txtFallbackUserAgent.SetText(Config.Instance.FallbackUserAgent ?? Config.DefaultFallbackUserAgent);
            dropProxyType = DropDown.NewFromStrings(new[]
            {
                "No Proxy (Direct Connection)", "Use System Proxy", "Manual Proxy Configuration"
            });
            var initialProxyType = Config.Instance.Proxy.HasValue ? Config.Instance.Proxy.Value.ProxyType : ProxyType.Direct;
            dropProxyType.Selected = initialProxyType switch
            {
                ProxyType.Direct => 0u,
                ProxyType.System => 1u,
                ProxyType.Custom => 2u,
                _ => 0u
            };
            txtProxyHost = TextEntry(Config.Instance.Proxy?.Host ?? "");
            txtProxyPort = IntEntry(Config.Instance.Proxy?.Port ?? 8080);
            txtProxyUser = TextEntry(Config.Instance.Proxy?.UserName ?? "");
            txtProxyPassword = TextEntry(Config.Instance.Proxy?.Password ?? "");
            txtProxyPassword.Visibility = false;
            proxyManualGroup = PrefGroup();

            // Speed
            chkEnableSpeedLimit = Toggle("SETTINGS_ENABLE_LIMIT", "Enable maximum download speed limit");
            chkEnableSpeedLimit.Active = Config.Instance.EnableSpeedLimit;
            txtSpeedLimit = IntEntry(Config.Instance.DefaltDownloadSpeed);

            // Monitoring
            chkMonitorClipboard = Toggle("SETTINGS_MONITOR_CLIPBOARD", "Monitor clipboard for downloadable links");
            chkMonitorClipboard.Active = Config.Instance.MonitorClipboard;
            lblIpcStatus = Label.New("");
            lblIpcStatus.AddCssClass("caption");
            lblIpcStatus.AddCssClass("dim-label");
            lblIpcStatus.Halign = Align.Start;

            // Advanced
            chkShutdownAll = Toggle("MSG_HALT", "Shutdown computer after all downloads complete");
            chkShutdownAll.Active = Config.Instance.ShutdownAfterAllFinished;
            chkRunCmd = Toggle("EXEC_CMD", "Run a program after all downloads finish");
            chkRunCmd.Active = Config.Instance.RunCommandAfterCompletion;
            txtCustomCmd = TextEntry(Config.Instance.AfterCompletionCommand ?? "");
            chkRunAntivirus = Toggle("SETTINGS_SCAN_FILES", "Scan downloaded files with antivirus scanner");
            chkRunAntivirus.Active = Config.Instance.ScanWithAntiVirus;
            txtAntiVirusCmd = TextEntry(Config.Instance.AntiVirusExecutable ?? "");
            txtAntiVirusArgs = TextEntry(Config.Instance.AntiVirusArgs ?? "[file]");

            // Credentials working copy (GTK 3 loads LvPasswords the same way)
            workingCredentials = new List<XDM.Core.PasswordEntry>(Config.Instance.UserCredentials ?? new List<XDM.Core.PasswordEntry>());
            credentialList = ListBox.New();
            credentialList.AddCssClass("boxed-list");
            credentialList.SelectionMode = SelectionMode.Single;
            lblCredentialEmpty = Label.New(TextResource.GetText("MSG_NO_CREDENTIALS") ?? "No saved credentials");
            lblCredentialEmpty.AddCssClass("caption");
            lblCredentialEmpty.AddCssClass("dim-label");
            lblCredentialEmpty.Halign = Align.Start;
            btnCredentialEdit = Button.NewWithLabel(TextResource.GetText("SETTINGS_CAT_EDIT") ?? "Edit");
            btnCredentialEdit.OnClicked += (_, _) => OnEditCredential();
            btnCredentialDelete = Button.NewWithLabel(TextResource.GetText("DESC_DEL") ?? "Delete");
            btnCredentialDelete.OnClicked += (_, _) => OnDeleteCredential();
            credentialList.OnRowSelected += (_, _) => UpdateCredentialButtons();
            RefreshCredentialRows();

            // Build pages
            stack = Stack.New();
            stack.Vexpand = true;
            stack.Hexpand = true;
            stack.AddTitled(Page(BuildGeneralPage()), "general", PageMeta[0].Title);
            stack.AddTitled(Page(BuildCategoriesPage()), "categories", PageMeta[1].Title);
            stack.AddTitled(Page(BuildNetworkPage()), "network", PageMeta[2].Title);
            stack.AddTitled(Page(BuildSpeedPage()), "speed", PageMeta[3].Title);
            stack.AddTitled(Page(BuildMonitoringPage()), "monitoring", PageMeta[4].Title);
            stack.AddTitled(Page(BuildCredentialsPage()), "credentials", PageMeta[5].Title);
            stack.AddTitled(Page(BuildAdvancedPage()), "advanced", PageMeta[6].Title);

            BuildTabs();
            var index = Math.Clamp(initialPage, 0, PageNames.Length - 1);
            stack.VisibleChildName = PageNames[index];

            var root = Box.New(Orientation.Vertical, 0);
            root.Append(tabs);
            root.Append(stack);
            window.SetChild(root);

            loading = false;
            SelectTab(PageNames[index]);
            UpdateIpcStatus();

            // Live preview + immediate persistence for appearance
            dropTheme.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() == "selected")
                {
                    RepopulateSchemes();
                    ApplySchemePreview();
                    Persist();
                }
            };
            dropScheme.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() == "selected")
                {
                    ApplySchemePreview();
                    Persist();
                }
            };
        }

        // ---------------- page builders ----------------

        // General: folders, download behaviour, appearance, notifications, system
        private Widget BuildGeneralPage()
        {
            var box = Column();

            box.Append(Section(TextResource.GetText("SETTINGS_LOCATIONS") ?? "Locations"));
            var locations = PrefGroup();
            locations.Append(PrefRow(TextResource.GetText("SETTINGS_DEF_DWN_FLD") ?? "Download folder",
                null, lblDownloadPath, ChangeButton(() => PickFolder(lblDownloadPath, v => downloadFolderPath = v))));
            locations.Append(PrefRow(TextResource.GetText("SETTINGS_TEMP_FLD") ?? "Temporary folder",
                null, lblTempPath, ChangeButton(() => PickFolder(lblTempPath, v => tempFolderPath = v))));
            box.Append(locations);

            box.Append(Section(TextResource.GetText("SETTINGS_GROUP_DOWNLOADS") ?? "Downloads"));
            var downloads = PrefGroup();
            downloads.Append(PrefRow(TextResource.GetText("MSG_MAX_DOWNLOAD") ?? "Simultaneous downloads",
                TextResource.GetText("SETTINGS_MAX_DOWNLOAD_DESC") ?? "Transfers running at the same time", dropMaxDownloads));
            downloads.Append(PrefRow(TextResource.GetText("SETTINGS_MAX_SEGMENTS") ?? "Segments per download",
                TextResource.GetText("SETTINGS_MAX_SEGMENTS_DESC") ?? "Parallel connections used for one file", dropMaxSegments));
            downloads.Append(PrefRow(TextResource.GetText("MSG_DOUBLE_CLICK_ACTION") ?? "Double-click action",
                null, dropDoubleClick));
            downloads.Append(SwitchRow(chkStartAuto));
            downloads.Append(SwitchRow(chkTimestamp));
            downloads.Append(SwitchRow(chkKeepAwake));
            downloads.Append(SwitchRow(chkOverwrite));
            downloads.Append(SwitchRow(chkAutoCat));
            box.Append(downloads);

            box.Append(Section(TextResource.GetText("SETTINGS_GROUP_APPEARANCE") ?? "Appearance"));
            var appearance = PrefGroup();
            appearance.Append(PrefRow(TextResource.GetText("SETTINGS_THEME") ?? "Theme", null, dropTheme));
            appearance.Append(PrefRow(TextResource.GetText("SETTINGS_COLOR_SCHEME") ?? "Colour scheme", null, dropScheme));
            appearance.Append(PrefRow(TextResource.GetText("SETTINGS_EXPORT_PALETTES") ?? "Export palettes",
                TextResource.GetText("SETTINGS_EXPORT_PALETTES_DESC") ?? "Save all colour schemes to a JSON file",
                ButtonWithLabel(TextResource.GetText("SETTINGS_EXPORT") ?? "Export", () => ExportPalettes())));
            appearance.Append(PrefRow(TextResource.GetText("SETTINGS_IMPORT_PALETTES") ?? "Import palettes",
                TextResource.GetText("SETTINGS_IMPORT_PALETTES_DESC") ?? "Validate a palette JSON file",
                ButtonWithLabel(TextResource.GetText("SETTINGS_IMPORT") ?? "Import", () => ImportPalette())));
            box.Append(appearance);
            box.Append(lblPaletteStatus);

            box.Append(Section(TextResource.GetText("SETTINGS_GROUP_NOTIFICATIONS") ?? "Notifications"));
            var notifications = PrefGroup();
            notifications.Append(SwitchRow(chkNotification));
            notifications.Append(SwitchRow(chkMediaNotification));
            notifications.Append(PrefRow(TextResource.GetText("SETTINGS_MEDIA_NOTIFICATION_DEBOUNCE") ?? "Video detection alert delay",
                TextResource.GetText("SETTINGS_MEDIA_NOTIFICATION_DEBOUNCE_DESC") ?? "Coalesce multiple video alerts within this window", dropMediaDebounce));
            notifications.Append(PrefRow(TextResource.GetText("SETTINGS_NOTIFICATION_TIMEOUT") ?? "Notification display duration",
                TextResource.GetText("SETTINGS_NOTIFICATION_TIMEOUT_DESC") ?? "How long desktop notifications remain visible", dropNotificationTimeout));
            notifications.Append(SwitchRow(chkShowComplete));
            notifications.Append(SwitchRow(chkShowProgress));
            notifications.Append(SwitchRow(chkPlaySound));
            box.Append(notifications);

            box.Append(Section(TextResource.GetText("SETTINGS_GROUP_SYSTEM") ?? "System"));
            var system = PrefGroup();
            system.Append(SwitchRow(chkAutoStart));
            box.Append(system);

            return box;
        }

        // Categories: inspector on the left, details and blocked sites on the right
        private Widget BuildCategoriesPage()
        {
            var box = Column();

            var split = Box.New(Orientation.Horizontal, 12);
            var listScroll = ScrolledWindow.New();
            listScroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            listScroll.SetChild(categoryList);
            listScroll.WidthRequest = 190;
            split.Append(listScroll);

            var details = Box.New(Orientation.Vertical, 8);
            details.Hexpand = true;
            details.Append(lblSelectedCat);

            var detailGroup = PrefGroup();
            detailGroup.Append(PrefRow(TextResource.GetText("SETTINGS_TARGET_FOLDER") ?? "Target folder", null,
                txtCatFolder, ChangeButton(() => PickFolderInto(txtCatFolder))));
            detailGroup.Append(PrefRow(TextResource.GetText("SETTINGS_FILE_EXT") ?? "File extensions", null, txtCatExts));
            details.Append(detailGroup);

            var hint = Label.New(TextResource.GetText("MSG_EXT_HINT")
                ?? "Separate extensions with spaces or commas (e.g. mp4, mkv, webm)");
            hint.AddCssClass("caption");
            hint.AddCssClass("dim-label");
            hint.Halign = Align.Start;
            hint.Wrap = true;
            details.Append(hint);

            var restore = Button.NewWithLabel(TextResource.GetText("BTN_RESTORE_CATEGORIES") ?? "Restore Default Categories");
            restore.Halign = Align.Start;
            restore.OnClicked += (_, _) =>
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
                LoadCategoryFields(Math.Min(selectedCategoryIndex, workingCategories.Count - 1));
                Persist();
            };
            details.Append(restore);
            split.Append(details);
            box.Append(split);

            categoryList.OnRowSelected += (_, args) =>
            {
                if (args.Row is ListBoxRow r)
                {
                    SaveCurrentCategoryFields();
                    selectedCategoryIndex = r.GetIndex();
                    LoadCategoryFields(selectedCategoryIndex);
                }
            };

            box.Append(Section(TextResource.GetText("SETTINGS_GROUP_SITELIST") ?? "Automatic capture exceptions"));
            var exceptions = PrefGroup();
            exceptions.Append(PrefRow(TextResource.GetText("DESC_SITEEXCEPTIONS") ?? "Do not capture from these sites",
                null, txtBlockedHosts));
            box.Append(exceptions);

            var excHint = Label.New(TextResource.GetText("MSG_DOMAIN_HINT")
                ?? "Separate domains with commas or spaces (e.g. *.bank.com, example.com)");
            excHint.AddCssClass("caption");
            excHint.AddCssClass("dim-label");
            excHint.Halign = Align.Start;
            box.Append(excHint);

            LoadCategoryFields(0);
            return box;
        }

        // Network: timeouts, user agent, proxy
        private Widget BuildNetworkPage()
        {
            var box = Column();

            box.Append(Section(TextResource.GetText("SETTINGS_GROUP_CONNECTION") ?? "Connection"));
            var connection = PrefGroup();
            connection.Append(PrefRow(TextResource.GetText("SETTINGS_NET_TIMEOUT") ?? "Network timeout",
                TextResource.GetText("SETTINGS_NET_TIMEOUT_DESC") ?? "Seconds before a stalled connection is dropped", txtNetworkTimeout));
            connection.Append(PrefRow(TextResource.GetText("SETTINGS_RETRY") ?? "Retry attempts",
                null, txtMaxRetries));
            connection.Append(PrefRow(TextResource.GetText("SETTINGS_FALLBACK_UA") ?? "Fallback user agent",
                null, txtFallbackUserAgent, ChangeButton(() => txtFallbackUserAgent.SetText(Config.DefaultFallbackUserAgent))));
            box.Append(connection);

            box.Append(Section(TextResource.GetText("SETTINGS_PROXY_CONF") ?? "Proxy"));
            var proxy = PrefGroup();
            proxy.Append(PrefRow(TextResource.GetText("SETTINGS_PROXY_MODE") ?? "Mode", null, dropProxyType));
            box.Append(proxy);

            proxyManualGroup.Append(PrefRow(TextResource.GetText("SETTINGS_PROXY_HOST") ?? "Host", null, txtProxyHost));
            proxyManualGroup.Append(PrefRow(TextResource.GetText("SETTINGS_PROXY_PORT") ?? "Port", null, txtProxyPort));
            proxyManualGroup.Append(PrefRow(TextResource.GetText("LBL_USERNAME") ?? "User name", null, txtProxyUser));
            proxyManualGroup.Append(PrefRow(TextResource.GetText("LBL_PASSWORD") ?? "Password", null, txtProxyPassword));
            proxyManualGroup.Visible = dropProxyType.Selected == 2u;
            box.Append(proxyManualGroup);

            dropProxyType.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() == "selected")
                {
                    proxyManualGroup.Visible = dropProxyType.Selected == 2u;
                }
            };

            return box;
        }

        // Speed limiter: single switch plus the limit and presets
        private Widget BuildSpeedPage()
        {
            var box = Column();

            box.Append(Section(TextResource.GetText("SETTINGS_GROUP_LIMIT") ?? "Bandwidth limit"));
            var group = PrefGroup();
            group.Append(SwitchRow(chkEnableSpeedLimit));
            group.Append(PrefRow(TextResource.GetText("LBL_SPEED_KB") ?? "Limit (KB/s)", null, txtSpeedLimit));
            box.Append(group);

            var presets = Box.New(Orientation.Horizontal, 6);
            presets.MarginStart = 12;
            foreach (var (label, value) in new[]
            {
                ("512 KB/s", "512"), ("1 MB/s", "1024"), ("2 MB/s", "2048"), ("5 MB/s", "5120")
            })
            {
                var b = Button.NewWithLabel(label);
                b.AddCssClass("pill");
                b.OnClicked += (_, _) =>
                {
                    txtSpeedLimit.SetText(value);
                    chkEnableSpeedLimit.Active = true;
                    Persist();
                };
                presets.Append(b);
            }
            box.Append(presets);
            return box;
        }

        // Monitoring: clipboard, store links, IPC relay state
        private Widget BuildMonitoringPage()
        {
            var box = Column();

            box.Append(Section(TextResource.GetText("SETTINGS_GROUP_CLIPBOARD") ?? "Clipboard"));
            var clipboard = PrefGroup();
            clipboard.Append(SwitchRow(chkMonitorClipboard));
            box.Append(clipboard);

            box.Append(Section(TextResource.GetText("SETTINGS_GROUP_BROWSER") ?? "Browser integration"));
            var browser = PrefGroup();
            browser.Append(PrefRow(TextResource.GetText("LBL_CWS_EXT") ?? "Chrome Web Store extension",
                TextResource.GetText("SETTINGS_GROUP_BROWSER") ?? "Install the helper to capture downloads", LinkButton("https://chromewebstore.google.com")));
            browser.Append(PrefRow(TextResource.GetText("LBL_AMO_EXT") ?? "Firefox Add-ons (AMO)",
                null, LinkButton("https://addons.mozilla.org")));
            box.Append(browser);

            box.Append(Section(TextResource.GetText("SETTINGS_GROUP_IPC") ?? "IPC relay"));
            var ipc = PrefGroup();
            ipc.Append(PrefRow(TextResource.GetText("SETTINGS_GROUP_IPC") ?? "IPC relay", null, lblIpcStatus,
                ButtonWithLabel(TextResource.GetText("BTN_RESTART_IPC") ?? "Restart IPC Relay", () => RestartIpc())));
            box.Append(ipc);

            return box;
        }

        // Credentials: saved site logins (host + user rows, editor dialog for add/edit)
        private Widget BuildCredentialsPage()
        {
            var box = Column();

            box.Append(Section(TextResource.GetText("SETTINGS_CRED") ?? "Saved credentials"));
            box.Append(credentialList);
            box.Append(lblCredentialEmpty);

            var buttons = Box.New(Orientation.Horizontal, 8);
            buttons.Halign = Align.Start;
            buttons.MarginTop = 8;
            var add = Button.NewWithLabel(TextResource.GetText("SETTINGS_CAT_ADD") ?? "Add");
            add.OnClicked += (_, _) => OnAddCredential();
            buttons.Append(add);
            buttons.Append(btnCredentialEdit);
            buttons.Append(btnCredentialDelete);
            box.Append(buttons);

            UpdateCredentialButtons();
            return box;
        }

        // Rebuilds the credential rows from the working copy
        private void RefreshCredentialRows()
        {
            var child = credentialList.GetFirstChild();
            while (child != null)
            {
                var next = child.GetNextSibling();
                credentialList.Remove(child);
                child = next;
            }
            foreach (var entry in workingCredentials)
            {
                var text = Box.New(Orientation.Vertical, 1);
                text.Hexpand = true;
                text.Valign = Align.Center;
                var host = Label.New(entry.Host);
                host.Halign = Align.Start;
                host.Xalign = 0;
                host.Ellipsize = Pango.EllipsizeMode.End;
                text.Append(host);
                var user = Label.New(entry.User);
                user.Halign = Align.Start;
                user.Xalign = 0;
                user.Ellipsize = Pango.EllipsizeMode.End;
                user.AddCssClass("caption");
                user.AddCssClass("dim-label");
                text.Append(user);
                var row = ListBoxRow.New();
                row.SetChild(text);
                credentialList.Append(row);
            }
            var hasItems = workingCredentials.Count > 0;
            credentialList.Visible = hasItems;
            lblCredentialEmpty.Visible = !hasItems;
            UpdateCredentialButtons();
        }

        // Enables edit/delete only when a row is selected
        private void UpdateCredentialButtons()
        {
            var hasSelection = credentialList.GetSelectedRow() != null && workingCredentials.Count > 0;
            btnCredentialEdit.Sensitive = hasSelection;
            btnCredentialDelete.Sensitive = hasSelection;
        }

        // Selected credential index, or -1 when nothing is selected
        private int SelectedCredentialIndex()
        {
            var row = credentialList.GetSelectedRow();
            if (row == null)
            {
                return -1;
            }
            var index = row.GetIndex();
            return index >= 0 && index < workingCredentials.Count ? index : -1;
        }

        // Adds a new credential via the editor dialog
        private void OnAddCredential()
        {
            var created = CredentialEditorDialog.Edit(window, null);
            if (created.HasValue)
            {
                workingCredentials.Add(created.Value);
                RefreshCredentialRows();
                Persist();
            }
        }

        // Edits the selected credential via the editor dialog
        private void OnEditCredential()
        {
            var index = SelectedCredentialIndex();
            if (index < 0)
            {
                return;
            }
            var updated = CredentialEditorDialog.Edit(window, workingCredentials[index]);
            if (updated.HasValue)
            {
                workingCredentials[index] = updated.Value;
                RefreshCredentialRows();
                Persist();
            }
        }

        // Deletes the selected credential after confirmation
        private void OnDeleteCredential()
        {
            var index = SelectedCredentialIndex();
            if (index < 0)
            {
                return;
            }
            var host = workingCredentials[index].Host;
            var prompt = (TextResource.GetText("MSG_DELETE_CREDENTIAL") ?? "Delete saved credentials for \"{host}\"?")
                .Replace("{host}", host);
            if (!Gtk4DialogHelper.Confirm(window, prompt, TextResource.GetText("DESC_DEL") ?? "Delete"))
            {
                return;
            }
            workingCredentials.RemoveAt(index);
            RefreshCredentialRows();
            Persist();
        }

        // Advanced: post-download automation and antivirus
        private Widget BuildAdvancedPage()
        {
            var box = Column();

            box.Append(Section(TextResource.GetText("SETTINGS_AUTOMATION") ?? "After all downloads"));
            var automation = PrefGroup();
            automation.Append(SwitchRow(chkShutdownAll));
            automation.Append(SwitchRow(chkRunCmd));
            automation.Append(PrefRow(TextResource.GetText("LBL_COMMAND") ?? "Command", null, txtCustomCmd));
            box.Append(automation);

            box.Append(Section(TextResource.GetText("SETTINGS_ANTIVIRUS") ?? "Antivirus scanning"));
            var antivirus = PrefGroup();
            antivirus.Append(SwitchRow(chkRunAntivirus));
            antivirus.Append(PrefRow(TextResource.GetText("ANTIVIR_CMD") ?? "Scanner executable", null, txtAntiVirusCmd,
                ChangeButton(() => PickFile(txtAntiVirusCmd))));
            antivirus.Append(PrefRow(TextResource.GetText("ANTIVIR_ARGS") ?? "Arguments", null, txtAntiVirusArgs));
            box.Append(antivirus);

            var hint = Label.New(TextResource.GetText("MSG_AV_HINT")
                ?? "Use [file] placeholder for the downloaded file path (e.g. clamscan [file])");
            hint.AddCssClass("caption");
            hint.AddCssClass("dim-label");
            hint.Halign = Align.Start;
            box.Append(hint);
            return box;
        }

        // ---------------- widgets ----------------

        // Scrollable page column
        private static ScrolledWindow Page(Widget content)
        {
            var scroll = ScrolledWindow.New();
            scroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            // Report the content height, so FitToPage can size the dialog to it
            scroll.SetPropagateNaturalHeight(true);
            scroll.SetChild(content);
            return scroll;
        }

        // Sizes the dialog to the page on screen, within sane bounds: a short page
        // (Advanced) must not open with empty space below its last card. Deltas
        // under FitStep are skipped so hopping between similar pages never flickers.
        private void FitToPage(string name)
        {
            if (stack.GetChildByName(name) is not Widget page)
            {
                return;
            }

            page.Measure(Orientation.Vertical, PageWidth, out _, out var natural, out _, out _);
            if (natural <= 0)
            {
                return;
            }
            const int chrome = 104;
            const int minHeight = 380;
            const int maxHeight = 700;
            var height = Math.Clamp(natural + chrome, minHeight, maxHeight);
            if (lastFittedHeight > 0 && Math.Abs(height - lastFittedHeight) < FitStep)
            {
                return;
            }
            lastFittedHeight = height;
            window.SetDefaultSize(900, height);
        }

        // Fits one page and reports the applied height (used by the self-test
        // parity check, which never presents the dialog)
        internal int FittedHeight(string name)
        {
            SelectTab(name);
            return lastFittedHeight;
        }

        // Vertical page column with consistent width and margins
        private static Box Column()
        {
            var box = Box.New(Orientation.Vertical, 10);
            box.MarginTop = 8;
            box.MarginBottom = 16;
            box.MarginStart = 28;
            box.MarginEnd = 28;
            box.WidthRequest = PageWidth;
            box.Halign = Align.Center;
            return box;
        }

        // Section heading above a card group
        private static Label Section(string text)
        {
            var label = Label.New(text);
            label.AddCssClass("heading");
            label.Halign = Align.Start;
            label.MarginStart = 2;
            label.MarginTop = 6;
            return label;
        }

        // Grouped card container (libadwaita boxed list)
        private static ListBox PrefGroup()
        {
            var list = ListBox.New();
            list.AddCssClass("boxed-list");
            list.SelectionMode = SelectionMode.None;
            return list;
        }

        // Tile row: title (and optional subtitle) on the left, control on the right
        private static ListBoxRow PrefRow(string title, string? subtitle, params Widget[] controls)
        {
            title = title.TrimEnd(':', ' ');

            var box = Box.New(Orientation.Horizontal, 12);
            box.MarginTop = 8;
            box.MarginBottom = 8;
            box.MarginStart = 14;
            box.MarginEnd = 12;

            var text = Box.New(Orientation.Vertical, 1);
            text.Hexpand = true;
            text.Valign = Align.Center;

            var titleLabel = Label.New(title);
            titleLabel.Halign = Align.Start;
            titleLabel.Xalign = 0;
            titleLabel.Ellipsize = Pango.EllipsizeMode.End;
            text.Append(titleLabel);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = Label.New(subtitle);
                sub.Halign = Align.Start;
                sub.Xalign = 0;
                sub.Ellipsize = Pango.EllipsizeMode.End;
                sub.AddCssClass("caption");
                sub.AddCssClass("dim-label");
                text.Append(sub);
            }
            box.Append(text);

            foreach (var control in controls)
            {
                control.Valign = Align.Center;
                box.Append(control);
            }

            var row = ListBoxRow.New();
            row.SetChild(box);
            row.Activatable = false;
            return row;
        }

        // Tile row wrapping a GNOME switch; the switch applies immediately
        private ListBoxRow SwitchRow(Switch toggle)
        {
            toggle.Valign = Align.Center;
            toggle.Halign = Align.End;
            toggle.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() == "active")
                {
                    Persist();
                }
            };
            return PrefRow(ExtractLabel(toggle), null, toggle);
        }

        // Switch whose tooltip carries the row title (the switch itself shows no text)
        private static Switch Toggle(string key, string fallback)
        {
            var toggle = Switch.New();
            toggle.TooltipText = TextResource.GetText(key) ?? fallback;
            return toggle;
        }

        // Row title for a switch created by Switch()
        private static string ExtractLabel(Switch toggle) => toggle.TooltipText ?? "";

        // Small secondary button used as a row action
        private static Button ButtonWithLabel(string text, Action handler)
        {
            var b = Button.NewWithLabel(text);
            b.OnClicked += (_, _) => handler();
            return b;
        }

        // "Change…" action button
        private static Button ChangeButton(Action action)
        {
            var b = Button.NewWithLabel(TextResource.GetText("SETTINGS_CHANGE") ?? "Change...");
            b.OnClicked += (_, _) => action();
            return b;
        }

        // Opens a browser link
        private static Button LinkButton(string url)
        {
            var b = Button.NewWithLabel(TextResource.GetText("MENU_OPEN_FOLDER") ?? "Open");
            b.OnClicked += (_, _) => PlatformHelper.OpenBrowser(url);
            return b;
        }

        // Text entry that commits on Enter and on focus loss
        private Entry TextEntry(string value)
        {
            var entry = Entry.New();
            entry.SetText(value);
            entry.OnActivate += (_, _) => Persist();
            var focus = EventControllerFocus.New();
            focus.OnLeave += (_, _) => Persist();
            entry.AddController(focus);
            return entry;
        }

        // Narrow numeric entry
        private Entry IntEntry(int value)
        {
            var entry = TextEntry(value.ToString());
            entry.WidthRequest = 90;
            entry.MaxWidthChars = 8;
            return entry;
        }

        // ---------------- tabs ----------------

        // Builds the linked icon tab strip and selects the first page
        private void BuildTabs()
        {
            var accent = AccentRgb();
            for (var i = 0; i < PageNames.Length; i++)
            {
                var (title, iconName) = PageMeta[i];
                var toggle = ToggleButton.New();
                toggle.AddCssClass("flat");
                toggle.TooltipText = title;

                // Icon + label; GNOME switchers stay compact, so the label is the
                // short first word of the page title
                var box = Box.New(Orientation.Horizontal, 6);
                box.Halign = Align.Center;
                box.MarginStart = 6;
                box.MarginEnd = 6;
                box.Append(Gtk4IconHelper.CreateTintedIcon(iconName, accent.R, accent.G, accent.B, 16));
                box.Append(Label.New(title.Split(' ')[0]));
                toggle.SetChild(box);

                var name = PageNames[i];
                toggle.OnToggled += (_, _) =>
                {
                    if (toggle.Active)
                    {
                        SelectTab(name);
                    }
                };
                tabs.Append(toggle);
            }
        }

        // Shows one page and makes its tab the only active one
        private void SelectTab(string name)
        {
            stack.VisibleChildName = name;
            FitToPage(name);
            var index = Array.IndexOf(PageNames, name);
            var child = tabs.GetFirstChild();
            for (var i = 0; child != null && i < PageNames.Length; child = child.GetNextSibling(), i++)
            {
                if (child is ToggleButton toggle)
                {
                    toggle.Active = i == index;
                }
            }
        }

        // ---------------- persistence ----------------

        // Applies the current control values without closing the dialog; safe to
        // call on every change because a config/IO failure must not kill the app
        private void Persist()
        {
            if (loading)
            {
                return;
            }

            try
            {
                SaveSettingsCore();
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Applying settings failed: " + ex.Message);
                Gtk4DialogHelper.ShowAlert(window,
                    (TextResource.GetText("MSG_SETTINGS_SAVE_FAILED") ?? "Settings could not be saved.")
                    + "\n\n" + ex.Message);
            }
        }

        // Writes every control into Config and persists it (no window close)
        private void SaveSettingsCore()
        {
            SaveCurrentCategoryFields();

            if (!string.IsNullOrEmpty(downloadFolderPath))
            {
                Config.Instance.DefaultDownloadFolder = downloadFolderPath;
            }
            if (!string.IsNullOrEmpty(tempFolderPath))
            {
                Config.Instance.TempDir = tempFolderPath;
            }

            Config.Instance.ShowNotification = chkNotification.Active;
            Config.Instance.ShowMediaNotification = chkMediaNotification.Active;
            var dSel = (int)dropMediaDebounce.Selected;
            Config.Instance.MediaNotificationDebounceSeconds = (dSel >= 0 && dSel < DebounceValues.Length) ? DebounceValues[dSel] : 2;
            var tSel = (int)dropNotificationTimeout.Selected;
            Config.Instance.NotificationTimeOut = (tSel >= 0 && tSel < TimeoutValues.Length) ? TimeoutValues[tSel] : 10000;
            Config.Instance.ShowDownloadCompleteWindow = chkShowComplete.Active;
            Config.Instance.ShowProgressWindow = chkShowProgress.Active;
            Config.Instance.PlayCompletionSound = chkPlaySound.Active;
            Config.Instance.StartDownloadAutomatically = chkStartAuto.Active;
            Config.Instance.FetchServerTimeStamp = chkTimestamp.Active;
            Config.Instance.KeepPCAwake = chkKeepAwake.Active;
            Config.Instance.FileConflictResolution =
                chkOverwrite.Active ? FileConflictResolution.Overwrite : FileConflictResolution.AutoRename;
            Config.Instance.FolderSelectionMode =
                chkAutoCat.Active ? FolderSelectionMode.Auto : FolderSelectionMode.Manual;
            Config.Instance.MonitorClipboard = chkMonitorClipboard.Active;
            Config.Instance.UserCredentials = new List<XDM.Core.PasswordEntry>(workingCredentials);

            var simOptions = new[] { 1, 2, 3, 4, 5, 8, 10 };
            if (dropMaxDownloads.Selected < (uint)simOptions.Length)
            {
                Config.Instance.MaxParallelDownloads = simOptions[(int)dropMaxDownloads.Selected];
            }

            var segOptions = new[] { 1, 2, 4, 8, 16, 32 };
            if (dropMaxSegments.Selected < (uint)segOptions.Length)
            {
                Config.Instance.MaxSegments = segOptions[(int)dropMaxSegments.Selected];
            }

            Config.Instance.DoubleClickOpenFile = dropDoubleClick.Selected == 0u;

            Config.Instance.ThemeMode = dropTheme.Selected == uint.MaxValue
                ? Config.Instance.ThemeMode
                : (int)dropTheme.Selected;
            if (dropScheme.Selected != uint.MaxValue)
            {
                Config.Instance.ColorScheme = (int)dropScheme.Selected;
            }

            Config.Instance.Categories = new List<Category>(workingCategories);

            var blockedText = txtBlockedHosts.GetText() ?? "";
            Config.Instance.BlockedHosts = blockedText
                .Split(new[] { ',', ' ', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(h => h.Trim().ToLowerInvariant())
                .Where(h => !string.IsNullOrEmpty(h))
                .ToArray();

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

            Config.Instance.EnableSpeedLimit = chkEnableSpeedLimit.Active;
            if (int.TryParse(txtSpeedLimit.GetText(), out int speed))
            {
                Config.Instance.DefaltDownloadSpeed = Math.Max(0, speed);
            }

            Config.Instance.ShutdownAfterAllFinished = chkShutdownAll.Active;
            Config.Instance.RunCommandAfterCompletion = chkRunCmd.Active;
            Config.Instance.AfterCompletionCommand = txtCustomCmd.GetText()?.Trim() ?? "";
            Config.Instance.ScanWithAntiVirus = chkRunAntivirus.Active;
            Config.Instance.AntiVirusExecutable = txtAntiVirusCmd.GetText()?.Trim() ?? "";
            Config.Instance.AntiVirusArgs = txtAntiVirusArgs.GetText()?.Trim() ?? "";

            try
            {
                PlatformHelper.EnableAutoStart(chkAutoStart.Active);
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Failed to update autostart setting: " + ex.Message);
            }

            Config.SaveConfig();

            if (!loading)
            {
                Gtk4Theme.ApplyMode(window, Config.Instance.ThemeMode, persist: false);
                ApplicationContext.BroadcastConfigChange();
            }
        }

        // ---------------- helpers ----------------

        // Opens the portal folder picker for an entry control
        private void PickFolderInto(Entry target) =>
            PickFolderCore(target.GetText(), selected =>
            {
                target.SetText(selected);
                Persist();
            });

        // Opens the portal folder picker and stores the result in a subtitle label
        private void PickFolder(Label target, Action<string> store) =>
            PickFolderCore(target.GetText(), selected =>
            {
                target.SetText(selected);
                store(selected);
                Persist();
            });

        // Shared portal folder picker
        private void PickFolderCore(string? currentValue, Action<string> apply)
        {
            var dialog = FileDialog.New();
            dialog.Title = TextResource.GetText("SETTINGS_TARGET_FOLDER") ?? "Select Folder";
            dialog.Modal = true;
            var current = currentValue;
            if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
            {
                dialog.SetInitialFolder(Gio.FileHelper.NewForPath(current));
            }

            dialog.SelectFolderAsync(window).ContinueWith(t =>
            {
                if (!t.IsCompletedSuccessfully || t.Result == null)
                {
                    return;
                }
                var selected = t.Result.GetPath();
                if (string.IsNullOrEmpty(selected))
                {
                    return;
                }
                GLib.Functions.IdleAdd(0, () =>
                {
                    apply(selected);
                    return false;
                });
            });
        }

        // Opens the portal file picker for an executable
        private void PickFile(Entry target)
        {
            var dialog = FileDialog.New();
            dialog.Title = TextResource.GetText("ANTIVIR_CMD") ?? "Select Executable";
            dialog.Modal = true;

            dialog.OpenAsync(window).ContinueWith(t =>
            {
                if (!t.IsCompletedSuccessfully || t.Result == null)
                {
                    return;
                }
                var selected = t.Result.GetPath();
                if (string.IsNullOrEmpty(selected))
                {
                    return;
                }
                GLib.Functions.IdleAdd(0, () =>
                {
                    target.SetText(selected);
                    Persist();
                    return false;
                });
            });
        }

        // Exports all colour schemes to a user-chosen JSON file (same shape as GTK 3)
        private void ExportPalettes()
        {
            var dialog = FileDialog.New();
            dialog.Title = TextResource.GetText("SETTINGS_EXPORT_PALETTES") ?? "Export palettes";
            dialog.Modal = true;
            dialog.SetInitialName("fetchflow-palettes.json");

            dialog.SaveAsync(window).ContinueWith(t =>
            {
                if (!t.IsCompletedSuccessfully || t.Result == null)
                {
                    return;
                }
                var selected = t.Result.GetPath();
                if (string.IsNullOrEmpty(selected))
                {
                    return;
                }
                string report;
                try
                {
                    File.WriteAllText(selected, ExportPalettesJson());
                    report = (TextResource.GetText("MSG_PALETTES_EXPORTED") ?? "Theme palettes successfully exported to:\n{file}")
                        .Replace("{file}", selected);
                }
                catch (Exception ex)
                {
                    report = (TextResource.GetText("MSG_PALETTES_EXPORT_FAILED") ?? "Failed to export palettes: {error}")
                        .Replace("{error}", ex.Message);
                }
                GLib.Functions.IdleAdd(0, () =>
                {
                    lblPaletteStatus.SetText(report);
                    return false;
                });
            });
        }

        // Imports and validates a palette JSON file, reporting the result inline
        private void ImportPalette()
        {
            var dialog = FileDialog.New();
            dialog.Title = TextResource.GetText("SETTINGS_IMPORT_PALETTES") ?? "Import palettes";
            dialog.Modal = true;

            dialog.OpenAsync(window).ContinueWith(t =>
            {
                if (!t.IsCompletedSuccessfully || t.Result == null)
                {
                    return;
                }
                var selected = t.Result.GetPath();
                if (string.IsNullOrEmpty(selected) || !File.Exists(selected))
                {
                    return;
                }
                string report;
                try
                {
                    var json = File.ReadAllText(selected);
                    var imported = ThemePaletteHelper.ImportPalettes(json);
                    if (imported != null && imported.Count > 0)
                    {
                        report = (TextResource.GetText("MSG_PALETTES_IMPORTED") ?? "Successfully validated and loaded {count} custom theme palette(s) from:\n{file}")
                            .Replace("{count}", imported.Count.ToString())
                            .Replace("{file}", Path.GetFileName(selected));
                    }
                    else if (TryParseSinglePalette(json, out var displayName))
                    {
                        report = (TextResource.GetText("MSG_PALETTE_IMPORTED") ?? "Successfully validated custom theme palette:\n{name}")
                            .Replace("{name}", displayName);
                    }
                    else
                    {
                        report = TextResource.GetText("MSG_PALETTE_INVALID") ?? "The selected file is not a valid FetchFlow palette JSON file.";
                    }
                }
                catch (Exception ex)
                {
                    report = (TextResource.GetText("MSG_PALETTES_IMPORT_FAILED") ?? "Failed to import palette: {error}")
                        .Replace("{error}", ex.Message);
                }
                GLib.Functions.IdleAdd(0, () =>
                {
                    lblPaletteStatus.SetText(report);
                    return false;
                });
            });
        }

        // Serializes every scheme in the GTK 3 export shape (version + dark/light lists)
        private static string ExportPalettesJson()
        {
            var data = new
            {
                Version = "1.0",
                DarkSchemes = ColorSchemeTable.DarkSchemes.Select(DescribeScheme),
                LightSchemes = ColorSchemeTable.LightSchemes.Select(DescribeScheme),
            };
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        // One scheme row of the palette export payload
        private static object DescribeScheme(ColorSchemeDefinition scheme) => new
        {
            scheme.Id,
            scheme.DisplayName,
            scheme.CssFileName,
            AccentR = scheme.AccentRgb.R,
            AccentG = scheme.AccentRgb.G,
            AccentB = scheme.AccentRgb.B,
            scheme.AccentHex,
            scheme.HoverBackgroundHex,
            scheme.ActiveBackgroundHex,
            scheme.AlternateBackgroundHex,
            scheme.CardBackgroundHex,
            scheme.BaseSurfaceHex,
            scheme.SidebarHex,
            scheme.HeaderBarHex,
        };

        // Single-palette fallback: a lone object with Id + DisplayName still counts
        private static bool TryParseSinglePalette(string json, out string displayName)
        {
            displayName = "";
            try
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (dict != null && dict.ContainsKey("Id") && dict.ContainsKey("DisplayName"))
                {
                    displayName = dict["DisplayName"]?.ToString() ?? "Custom scheme";
                    return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        // Refreshes the IPC relay line from the running monitor
        private void UpdateIpcStatus()
        {
            try
            {
                var port = IpcHttpMessageProcessor.EffectivePort;
                var sessions = IpcHttpMessageProcessor.ActiveWebSocketSessionsCount;
                lblIpcStatus.SetText(TextResource.GetText("SETTINGS_IPC_STATE")?.Replace("{port}", port.ToString())
                    ?? $"Listening on 127.0.0.1:{port}" + (sessions > 0 ? $" · {sessions} active" : ""));
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "IPC status refresh failed");
                lblIpcStatus.SetText("");
            }
        }

        // Restarts the IPC relay from the settings row
        private void RestartIpc()
        {
            try
            {
                BrowserMonitor.Restart();
                UpdateIpcStatus();
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Failed to restart IPC relay");
            }
        }

        // Loads one category into the detail fields
        private void LoadCategoryFields(int index)
        {
            if (index < 0 || index >= workingCategories.Count)
            {
                return;
            }
            var cat = workingCategories[index];
            lblSelectedCat.SetText(cat.DisplayName);
            txtCatFolder.SetText(cat.DefaultFolder ?? "");
            txtCatExts.SetText(string.Join(", ", cat.FileExtensions ?? new HashSet<string>()));
        }

        // Writes the detail fields back into the selected category
        private void SaveCurrentCategoryFields()
        {
            if (selectedCategoryIndex < 0 || selectedCategoryIndex >= workingCategories.Count)
            {
                return;
            }
            var cat = workingCategories[selectedCategoryIndex];
            cat.DefaultFolder = txtCatFolder.GetText()?.Trim() ?? "";
            cat.FileExtensions = new HashSet<string>((txtCatExts.GetText() ?? "")
                .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().TrimStart('.').ToLowerInvariant())
                .Where(e => !string.IsNullOrEmpty(e)));
            workingCategories[selectedCategoryIndex] = cat;
        }

        // Scheme names for the active mode
        private static string[] ColorSchemeNames(bool dark) =>
            (dark ? ColorSchemeTable.DarkSchemes : ColorSchemeTable.LightSchemes)
                .Select(scheme => scheme.DisplayName)
                .ToArray();

        // Rebuilds the scheme list after a mode change, keeping the index valid
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

        // Applies mode and scheme live while the dialog is open
        private void ApplySchemePreview()
        {
            if (dropScheme.Selected != uint.MaxValue)
            {
                Config.Instance.ColorScheme = (int)dropScheme.Selected;
            }
            var mode = dropTheme.Selected == uint.MaxValue ? Gtk4Theme.ModeFollowSystem : (int)dropTheme.Selected;
            Gtk4Theme.ApplyMode(window, mode, persist: false);
        }

        // Accent for the active colour scheme
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

        // Displays the settings window, pre-fitted so it never flashes at the
        // default size first (a zero measure means "not laid out yet": skip it)
        public void Show()
        {
            FitToPage(stack.VisibleChildName ?? PageNames[0]);
            window.Present();
        }
    }
}
