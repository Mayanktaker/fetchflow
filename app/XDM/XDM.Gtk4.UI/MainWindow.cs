// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 main window: headerbar actions, category sidebar, ColumnView download
// list, search, and status. Core rows enter through Gtk4ApplicationWindow.
using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Core.UI;
using XDM.Core.Util;
using XDM.Gtk4UI.Dialogs;
using XDM.Gtk4UI.Downloads;
using XDM.Gtk4UI.Utils;
using GtkApplication = Gtk.Application;

namespace XDM.Gtk4UI
{
    internal sealed class MainWindow
    {
        // Sidebar width token from docs/design.md spacing scale
        private const int SidebarWidth = 200;
        // Default desktop window size
        private const int DefaultWidth = 900;
        private const int DefaultHeight = 560;
        // Fixed sidebar view keys
        internal const string InProgressKey = "inprogress";
        internal const string FinishedKey = "finished";

        // Native GTK window presented by the host
        public ApplicationWindow Window { get; }
        // Download list (ColumnView) widget
        private readonly DownloadListView downloadList = new();
        // Search action target
        private readonly SearchEntry searchEntry = SearchEntry.New();
        // Toolbar buttons exposed through GTK-neutral core adapters
        public Button NewButton { get; }
        public Button DeleteButton { get; }
        public Button PauseButton { get; }
        public Button ResumeButton { get; }
        public Button OpenFileButton { get; }
        public Button OpenFolderButton { get; }
        // Header theme toggle
        private readonly Button themeButton = Button.New();
        private readonly Image themeIcon;
        // Status bar elements
        private readonly Label statusLabel = Label.New("");
        private readonly Label lblExtensionStatus = Label.New("");
        private readonly CheckButton btnMonitoring;
        private readonly Button btnSpeedLimit;
        private readonly Button btnScheduler;
        private readonly Button btnSelectAll;
        private readonly Label lblSelectionCount = Label.New("");
        // Every row in the active sidebar view
        private List<DownloadItem> allRows = new();
        // Current sidebar view key
        private string currentView = InProgressKey;
        // Sidebar category values keyed by their persisted category name
        private readonly Dictionary<string, Category> categories = new();
        // GTK application host and system tray manager
        private readonly GtkApplication app;
        private readonly Gtk4TrayIconManager trayManager;

        // Events consumed by the core window adapter
        public event EventHandler? SelectionChanged;
        public event Action<string>? ViewChanged;
        public event Action? RefreshRequested;
        public event EventHandler? BrowserMonitoringButtonClicked;
        public event EventHandler? SchedulerClicked;

        // Builds the main window and registers its application actions
        public static MainWindow Create(GtkApplication app, bool demo)
        {
            var main = new MainWindow(app);
            AppActions.Register(
                app,
                newDownload: main.OnNewDownload,
                search: main.FocusSearch,
                toggleTheme: main.ToggleTheme,
                refresh: main.Refresh,
                batchDownload: main.OnBatchDownload,
                scheduler: main.OnScheduler,
                importExport: main.OnImportExport,
                settings: main.OnSettings,
                about: main.OnAbout,
                selectAll: main.downloadList.ToggleSelectAll,
                sortNameAsc: () => main.downloadList.SortBy(DownloadSortColumn.Name, true),
                sortNameDesc: () => main.downloadList.SortBy(DownloadSortColumn.Name, false),
                sortSizeAsc: () => main.downloadList.SortBy(DownloadSortColumn.Size, true),
                sortSizeDesc: () => main.downloadList.SortBy(DownloadSortColumn.Size, false),
                sortDateAsc: () => main.downloadList.SortBy(DownloadSortColumn.ProgressOrDate, true),
                sortDateDesc: () => main.downloadList.SortBy(DownloadSortColumn.ProgressOrDate, false),
                openFile: () => UIActions.OpenSelectedFile(),
                openFolder: () => UIActions.OpenSelectedFolder(),
                pauseDownload: () => UIActions.StopSelectedDownloads(),
                resumeDownload: () => UIActions.ResumeDownloads(),
                deleteDownload: () => UIActions.DeleteDownloads(main.IsInProgressView, null),
                copyUrl: () => { if (main.IsInProgressView) UIActions.CopyURL1(); else UIActions.CopyURL2(); },
                copyFile: () => UIActions.CopyFile(),
                downloadProperties: () => UIActions.ShowSeletectedItemProperties(),
                verifyChecksum: () => UIActions.VerifySelectedFileChecksum(),
                restartDownload: () => UIActions.RestartDownload(),
                saveAs: () => UIActions.SaveAs(),
                refreshLink: () => UIActions.RefreshLink(),
                moveToQueue: () => UIActions.MoveToQueue(),
                showProgress: () => UIActions.ShowProgressWindow(),
                speedLimiter: () => (ApplicationContext.PlatformUIService as Gtk4PlatformUIService)?.ShowSettingsDialog(3),
                videoDownload: () => ApplicationContext.PlatformUIService?.ShowYoutubeDLDialog());
            if (demo)
            {
                main.SetRows(DemoRows(), InProgressKey);
            }
            return main;
        }

        // Assembles widgets before the core context is initialized
        private MainWindow(GtkApplication app)
        {
            this.app = app;
            NewButton = Button.New();
            Gtk4IconHelper.ConfigureButton(
                NewButton,
                "links-line",
                TextResource.GetText("DESC_NEW") ?? "New",
                TextResource.GetText("DESC_NEW") ?? "New Download (Ctrl+N)",
                "suggested-action",
                16);

            DeleteButton = Button.New();
            Gtk4IconHelper.ConfigureButton(
                DeleteButton,
                "delete-bin-7-line",
                TextResource.GetText("DESC_DEL") ?? "Delete",
                TextResource.GetText("DESC_DEL") ?? "Delete selected downloads",
                "destructive-action",
                16);

            PauseButton = Button.New();
            Gtk4IconHelper.ConfigureButton(
                PauseButton,
                "pause-line",
                TextResource.GetText("MENU_PAUSE") ?? "Pause",
                TextResource.GetText("MENU_PAUSE") ?? "Pause selected downloads",
                null,
                16);

            ResumeButton = Button.New();
            Gtk4IconHelper.ConfigureButton(
                ResumeButton,
                "play-line",
                TextResource.GetText("MENU_RESUME") ?? "Resume",
                TextResource.GetText("MENU_RESUME") ?? "Resume selected downloads",
                null,
                16);

            OpenFileButton = Button.New();
            Gtk4IconHelper.ConfigureButton(
                OpenFileButton,
                "external-link-line",
                TextResource.GetText("CTX_OPEN_FILE") ?? "Open",
                TextResource.GetText("CTX_OPEN_FILE") ?? "Open downloaded file",
                null,
                16);

            OpenFolderButton = Button.New();
            Gtk4IconHelper.ConfigureButton(
                OpenFolderButton,
                "folder-shared-line",
                TextResource.GetText("CTX_OPEN_FOLDER") ?? "Folder",
                TextResource.GetText("CTX_OPEN_FOLDER") ?? "Open destination folder",
                null,
                16);

            // Theme toggle with sun/moon icon
            themeIcon = Gtk4IconHelper.CreateIcon(Gtk4Theme.IsDarkActive ? "sun-line" : "moon-line", 16);
            themeButton.SetChild(themeIcon);
            themeButton.AddCssClass("flat");
            themeButton.TooltipText = "Switch theme (Ctrl+T)";

            // Bottom bar controls
            btnMonitoring = CheckButton.NewWithLabel(TextResource.GetText("SETTINGS_MONITORING") ?? "Browser Monitoring");
            btnMonitoring.Active = Config.Instance.IsBrowserMonitoringEnabled;
            btnMonitoring.OnToggled += (_, _) =>
            {
                Config.Instance.IsBrowserMonitoringEnabled = btnMonitoring.Active;
                Config.SaveConfig();
                BrowserMonitoringButtonClicked?.Invoke(this, EventArgs.Empty);
            };

            lblExtensionStatus.SetText("Extension: Ready");
            lblExtensionStatus.AddCssClass("extension-status-label");

            btnSpeedLimit = Gtk4IconHelper.CreateActionButton(
                "time-line",
                "Speed Limiter",
                "Speed Limiter (Click for settings)",
                "flat",
                14);
            btnSpeedLimit.OnClicked += (_, _) =>
            {
                (ApplicationContext.PlatformUIService as Gtk4PlatformUIService)?.ShowSettingsDialog(3);
            };

            btnScheduler = Gtk4IconHelper.CreateActionButton(
                "list-settings-line",
                TextResource.GetText("DESC_Q_TITLE") ?? "Queue & Scheduler",
                "Queue and Scheduler (Ctrl+Shift+Q)",
                "flat",
                14);
            btnScheduler.OnClicked += (_, _) => OnScheduler();

            btnSelectAll = Gtk4IconHelper.CreateActionButton(
                "checkbox-line",
                "Select All",
                "Select all downloads (Ctrl+A)",
                "flat",
                14);
            btnSelectAll.OnClicked += (_, _) => downloadList.ToggleSelectAll();

            lblSelectionCount.AddCssClass("dim-label");

            Window = ApplicationWindow.New(app);
            Window.Title = "FetchFlow";
            Window.SetDefaultSize(DefaultWidth, DefaultHeight);
            Window.SetTitlebar(BuildHeaderBar());
            Window.SetChild(BuildBody());

            DeleteButton.Visible = true;
            DeleteButton.Sensitive = false;
            PauseButton.Visible = false;
            ResumeButton.Visible = false;
            OpenFileButton.Visible = false;
            OpenFolderButton.Visible = false;
            downloadList.SelectionChanged += (_, _) =>
            {
                var selCount = downloadList.GetSelectedItems().Count;
                lblSelectionCount.SetText(selCount > 0 ? $"{selCount} selected" : "");
                btnSelectAll.TooltipText = selCount == downloadList.Count && selCount > 0
                    ? "Deselect All (Ctrl+A)"
                    : "Select All (Ctrl+A)";
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            };

            PlatformHelper.EnsureTrayIconInstalled();
            trayManager = new Gtk4TrayIconManager();
            trayManager.Init("FetchFlow Download Manager", ShowAndActivate, QuitFromTray);

            Window.OnCloseRequest += (sender, args) =>
            {
                if (trayManager.IsTrayActive)
                {
                    Window.Hide();
                    return true;
                }
                return false;
            };

            Gtk4Theme.Apply(Window, Gtk4Theme.IsDarkActive);
        }

        // Packs the app title, core actions, search, theme toggle and application menu
        private Widget BuildHeaderBar()
        {
            var header = HeaderBar.New();

            var titleBox = Box.New(Orientation.Horizontal, 6);
            titleBox.Halign = Align.Center;
            titleBox.Valign = Align.Center;
            var logoIcon = Gtk4IconHelper.CreateIcon("fetchflow-logo", 20);
            var titleLabel = Label.New("FetchFlow");
            titleLabel.AddCssClass("heading");
            titleBox.Append(logoIcon);
            titleBox.Append(titleLabel);
            header.TitleWidget = titleBox;

            header.PackStart(NewButton);
            header.PackStart(PauseButton);
            header.PackStart(ResumeButton);
            header.PackStart(DeleteButton);
            header.PackStart(OpenFileButton);
            header.PackStart(OpenFolderButton);

            searchEntry.PlaceholderText = TextResource.GetText("LBL_SEARCH") ?? "Search";
            searchEntry.TooltipText = "Search downloads (Ctrl+F)";
            searchEntry.WidthRequest = 210;
            searchEntry.AddCssClass("search-entry");
            searchEntry.OnSearchChanged += (_, _) => ApplyView(currentView);
            header.PackEnd(searchEntry);

            themeButton.TooltipText = "Switch theme (Ctrl+T)";
            themeButton.OnClicked += (_, _) => ToggleTheme();
            header.PackEnd(themeButton);

            var sortMenuButton = MenuButton.New();
            sortMenuButton.IconName = "view-sort-ascending-symbolic";
            sortMenuButton.TooltipText = "Sort Downloads";
            var sortMenu = Gio.Menu.New();
            sortMenu.Append("File Name (A to Z)", "app." + AppActions.SortNameAsc);
            sortMenu.Append("File Name (Z to A)", "app." + AppActions.SortNameDesc);
            sortMenu.Append("Size (Smallest First)", "app." + AppActions.SortSizeAsc);
            sortMenu.Append("Size (Largest First)", "app." + AppActions.SortSizeDesc);
            sortMenu.Append("Date / Progress (Newest)", "app." + AppActions.SortDateDesc);
            sortMenu.Append("Date / Progress (Oldest)", "app." + AppActions.SortDateAsc);
            sortMenuButton.SetMenuModel(sortMenu);
            header.PackEnd(sortMenuButton);

            var menuButton = MenuButton.New();
            menuButton.IconName = "open-menu-symbolic";
            menuButton.TooltipText = "Main Menu";

            var menu = Gio.Menu.New();
            menu.Append(TextResource.GetText("MENU_BATCH_DOWNLOAD") ?? "Batch Download...", "app." + AppActions.BatchDownload);
            menu.Append(TextResource.GetText("LBL_VIDEO_DOWNLOAD") ?? "Video Downloader...", "app." + AppActions.VideoDownload);
            menu.Append(TextResource.GetText("DESC_Q_TITLE") ?? "Queue & Scheduler...", "app." + AppActions.Scheduler);
            menu.Append(TextResource.GetText("MENU_SPEED_LIMITER") ?? "Speed Limiter...", "app." + AppActions.SpeedLimiter);
            menu.Append(TextResource.GetText("CTX_CHECKSUM") ?? "Calculate Checksum...", "app." + AppActions.VerifyChecksum);
            menu.Append(TextResource.GetText("MENU_IMPORT_EXPORT") ?? "Import / Export...", "app." + AppActions.ImportExport);
            menu.Append(TextResource.GetText("TITLE_SETTINGS") ?? "Settings...", "app." + AppActions.Settings);
            menu.Append("About FetchFlow", "app." + AppActions.About);
            menuButton.SetMenuModel(menu);
            header.PackEnd(menuButton);

            return header;
        }

        // Builds the resizable category pane and status bar
        private Widget BuildBody()
        {
            var paned = Paned.New(Orientation.Horizontal);
            paned.StartChild = BuildSidebar();
            paned.EndChild = downloadList.Root;
            paned.Position = SidebarWidth;
            paned.ResizeStartChild = false;
            paned.ShrinkStartChild = false;

            var root = Box.New(Orientation.Vertical, 0);
            root.Append(paned);
            root.Append(BuildStatusBar());
            return root;
        }

        // Creates fixed views plus the user's configured download categories
        private Widget BuildSidebar()
        {
            var list = ListBox.New();
            list.AddCssClass("sidebar");
            list.SelectionMode = SelectionMode.Browse;
            // Tinted status views: blue for incomplete, green for complete
            list.Append(BuildSidebarRow(TextResource.GetText("ALL_UNFINISHED") ?? "All Incomplete", InProgressKey, "download-2-line", 59, 130, 246));
            list.Append(BuildSidebarRow(TextResource.GetText("ALL_FINISHED") ?? "All Finished", FinishedKey, "check-line", 46, 194, 126));

            foreach (var category in Config.Instance.Categories)
            {
                categories[category.Name] = category;
                var (iconName, r, g, b) = CategoryIconPalette.Resolve(category);
                list.Append(BuildSidebarRow(category.DisplayName, category.Name, iconName, r, g, b));
            }

            list.OnRowSelected += (_, args) =>
            {
                if (args.Row is ListBoxRow row && !string.IsNullOrEmpty(row.Name))
                {
                    currentView = row.Name;
                    ApplyView(currentView);
                    ViewChanged?.Invoke(currentView);
                }
            };

            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            scrolled.SetChild(list);
            scrolled.WidthRequest = SidebarWidth;
            return scrolled;
        }

        // Creates one accessible sidebar row with tinted category icon and text
        private static ListBoxRow BuildSidebarRow(string text, string key, string iconName, byte r, byte g, byte b)
        {
            var rowBox = Box.New(Orientation.Horizontal, 10);
            rowBox.MarginTop = 6;
            rowBox.MarginBottom = 6;
            rowBox.MarginStart = 8;
            rowBox.MarginEnd = 8;

            var icon = Gtk4IconHelper.CreateTintedIcon(iconName, r, g, b, 18);
            icon.Valign = Align.Center;

            var label = Label.New(text);
            label.Halign = Align.Start;
            label.Valign = Align.Center;
            label.Hexpand = true;

            rowBox.Append(icon);
            rowBox.Append(label);

            var row = ListBoxRow.New();
            row.SetChild(rowBox);
            row.Name = key;
            row.TooltipText = text;
            return row;
        }

        // Builds the rich status and action bar matching the GTK 3 bottom bar
        private Widget BuildStatusBar()
        {
            var bar = Box.New(Orientation.Horizontal, 10);
            bar.AddCssClass("bottom-bar");

            bar.Append(btnSelectAll);
            bar.Append(lblSelectionCount);
            bar.Append(btnMonitoring);
            bar.Append(lblExtensionStatus);

            statusLabel.Halign = Align.Start;
            statusLabel.Hexpand = true;
            statusLabel.AddCssClass("status-bar-text");
            bar.Append(statusLabel);

            bar.Append(btnSpeedLimit);
            bar.Append(btnScheduler);

            return bar;
        }

        // Sets all rows from the core model and selects the requested view
        public void SetRows(IEnumerable<DownloadItem> rows, string view)
        {
            allRows = rows.ToList();
            currentView = view;
            ApplyView(view);
        }

        // Adds or replaces one in-progress core row
        public void AddInProgress(InProgressDownloadItem entry)
        {
            allRows.RemoveAll(row => row.ProgressRow?.DownloadEntry.Id == entry.Id || row.FinishedRow?.DownloadEntry.Id == entry.Id);
            allRows.Insert(0, ToItem(new Gtk4InProgressRow(entry)));
            ApplyView(currentView);
        }

        // Adds or replaces one finished core row
        public void AddFinished(FinishedDownloadItem entry)
        {
            allRows.RemoveAll(row => row.ProgressRow?.DownloadEntry.Id == entry.Id || row.FinishedRow?.DownloadEntry.Id == entry.Id);
            allRows.Insert(0, ToItem(new Gtk4FinishedRow(entry)));
            ApplyView(currentView);
        }

        // Removes the row with the specified transfer id
        public void Remove(string id)
        {
            allRows.RemoveAll(row => row.ProgressRow?.DownloadEntry.Id == id || row.FinishedRow?.DownloadEntry.Id == id);
            ApplyView(currentView);
        }

        // Resolves visible list selection to the core row adapters
        public IReadOnlyList<DownloadItem> GetSelectedItems() => downloadList.GetSelectedItems();

        // Selects the fixed sidebar view and refreshes the displayed rows
        public void SelectView(string key)
        {
            currentView = key;
            ApplyView(key);
            ViewChanged?.Invoke(key);
        }

        // Returns whether the active view is the in-progress list
        public bool IsInProgressView => currentView == InProgressKey;

        // Clears current row selection
        public void ClearSelection() => downloadList.ClearSelection();

        // Applies the current category and search filters
        private void ApplyView(string key)
        {
            IEnumerable<DownloadItem> rows = key switch
            {
                InProgressKey => allRows.Where(row => !row.IsComplete),
                FinishedKey => allRows.Where(row => row.IsComplete),
                _ => FilterByCategory(key)
            };
            downloadList.Reload(rows);
            UpdateStatus();
        }

        // Filters category rows using the same predicate as the GTK 3 UI
        private IEnumerable<DownloadItem> FilterByCategory(string key)
        {
            if (!categories.TryGetValue(key, out var category))
            {
                return allRows;
            }
            return allRows.Where(row => Helpers.IsOfCategoryOrMatchesKeyword(row.Name, null, category));
        }

        // Converts a mutable core row into the GTK 4 projection
        internal static DownloadItem ToItem(Gtk4InProgressRow row) => new()
        {
            Name = row.Name,
            Status = FormatStatus(row),
            SizeText = FormattingHelper.FormatSize(row.Size),
            RawSize = row.Size,
            Progress = Math.Clamp(row.Progress / 100.0, 0.0, 1.0),
            RawProgress = row.Progress,
            DateAdded = row.DateAdded,
            DownloadSpeed = row.DownloadSpeed,
            ETA = row.ETA,
            ProgressRow = row
        };

        // Converts a finished core row into the GTK 4 projection
        internal static DownloadItem ToItem(Gtk4FinishedRow row) => new()
        {
            Name = row.Name,
            Status = TextResource.GetText("STAT_FINISHED") ?? "Complete",
            SizeText = FormattingHelper.FormatSize(row.Size),
            RawSize = row.Size,
            Progress = 1.0,
            RawProgress = 100,
            DateAdded = row.DateAdded,
            IsComplete = true,
            FinishedRow = row
        };

        // Builds a compact status line from core transfer state
        private static string FormatStatus(Gtk4InProgressRow row)
        {
            var status = row.Status.ToString();
            if (!string.IsNullOrWhiteSpace(row.DownloadSpeed))
            {
                status += " · " + row.DownloadSpeed;
            }
            else if (!string.IsNullOrWhiteSpace(row.ETA))
            {
                status += " · " + row.ETA;
            }
            return status;
        }

        // Ctrl+F focuses the search entry
        private void FocusSearch() => searchEntry.GrabFocus();

        // Ctrl+N activates the core New button path
        private void OnNewDownload() => NewButton.Activate();

        // F5 delegates to the core database-backed row adapter
        private void Refresh() => RefreshRequested?.Invoke();

        // Ctrl+B opens Batch Download pattern and list window
        private void OnBatchDownload() =>
            (ApplicationContext.PlatformUIService as Gtk4PlatformUIService)?.ShowBatchDownloadWindow();

        // Ctrl+Shift+Q opens Queue and Scheduler window
        private void OnScheduler() =>
            (ApplicationContext.MainWindow as Gtk4ApplicationWindow)?.TriggerScheduler();

        // Ctrl+I opens Import/Export backup and restore dialog
        private void OnImportExport() =>
            (ApplicationContext.PlatformUIService as Gtk4PlatformUIService)?.ShowImportExportDialog();

        // Ctrl+, opens Settings preferences dialog
        private void OnSettings() =>
            (ApplicationContext.PlatformUIService as Gtk4PlatformUIService)?.ShowSettingsDialog(0);

        // F1 displays About FetchFlow information
        private void OnAbout() =>
            Gtk4DialogHelper.ShowAlert(
                Window,
                "FetchFlow Download Manager (Wayland & GTK 4 Edition)\nVersion 9.1.16.0\n\n© Mayanktaker Computers & Web Development\nhttps://mayanktaker.com",
                "About FetchFlow");

        // Ctrl+T swaps the GTK 4 stylesheet
        private void ToggleTheme()
        {
            Gtk4Theme.Toggle();
            Gtk4IconHelper.UpdateIcon(themeIcon, Gtk4Theme.IsDarkActive ? "sun-line" : "moon-line", 16);
            themeButton.TooltipText = Gtk4Theme.IsDarkActive
                ? "Switch to Light Theme (Ctrl+T)"
                : "Switch to Dark Theme (Ctrl+T)";
        }

        // Updates the footer summary for the active view
        private void UpdateStatus()
        {
            SetStatus($"GTK 4 · {currentView} · {downloadList.Count} downloads · {Config.Instance.Categories.Count()} categories");
        }

        // Sets the footer message
        public void SetStatus(string text) => statusLabel.SetText(text);

        // Presents and activates the main window, ensuring it is visible
        public void ShowAndActivate()
        {
            Window.Visible = true;
            Window.Present();
        }

        // Quits the application from the tray context menu
        private void QuitFromTray()
        {
            var active = CountActiveDownloads();
            if (active > 0 && !Gtk4DialogHelper.Confirm(
                Window,
                active == 1
                    ? "1 download is in progress. Quit FetchFlow anyway?"
                    : $"{active} downloads are in progress. Quit FetchFlow anyway?",
                "Quit FetchFlow"))
            {
                return;
            }

            try
            {
                trayManager.Dispose();
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Tray dispose on quit: " + ex.Message);
            }

            app.Quit();
        }

        // Counts downloads that are currently active in the core engine
        private static int CountActiveDownloads()
        {
            try
            {
                return ApplicationContext.CoreService?.ActiveDownloadCount ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // Presents the native GTK window, ensuring it is visible
        public void Present()
        {
            Window.Visible = true;
            Window.Present();
        }

        // Formats an in-progress row for display
        private static IEnumerable<DownloadItem> DemoRows() => new List<DownloadItem>
        {
            new() { Name = "ubuntu-24.04-desktop-amd64.iso", Status = "Downloading · 4.2 MB/s", SizeText = "1.8 GB", Progress = 0.42 },
            new() { Name = "annual-report-2026.pdf", Status = "Paused", SizeText = "12.4 MB", Progress = 0.68 },
            new() { Name = "conference-talk.mp4", Status = "Complete", SizeText = "740 MB", Progress = 1.0, IsComplete = true },
            new() { Name = "dataset-2026-10.zip", Status = "Downloading · 900 KB/s", SizeText = "3.1 GB", Progress = 0.11 }
        };
    }
}
