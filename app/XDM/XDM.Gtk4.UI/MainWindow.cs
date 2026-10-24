// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 main window: headerbar actions, category sidebar, ColumnView download
// list, search, and status. Core rows enter through Gtk4ApplicationWindow.
// Presentation (header, sidebar, status bar) lives in MainWindowChrome.cs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.BrowserMonitoring;
using XDM.Core.Downloader;
using XDM.Core.UI;
using XDM.Core.Util;
using XDM.Gtk4UI.Dialogs;
using XDM.Gtk4UI.Downloads;
using XDM.Gtk4UI.Utils;
using GtkApplication = Gtk.Application;

namespace XDM.Gtk4UI
{
    internal sealed partial class MainWindow
    {
        // Sidebar width token from docs/design.md spacing scale
        private const int SidebarWidth = 210;
        // Default desktop window size
        private const int DefaultWidth = 980;
        private const int DefaultHeight = 660;
        // Fixed sidebar view keys
        internal const string InProgressKey = "inprogress";
        internal const string FinishedKey = "finished";
        // Progress ticks closer together than this are data-only (no repaint)
        private const int ProgressRepaintIntervalMs = 250;

        // Native GTK window presented by the host
        public Adw.ApplicationWindow Window { get; }
        // Download list (ColumnView) widget
        private readonly DownloadListView downloadList = new();
        // Search action target and its native toggleable bar below the header
        private readonly SearchEntry searchEntry = SearchEntry.New();
        private readonly SearchBar searchBar = SearchBar.New();
        private readonly ToggleButton searchToggle = ToggleButton.New();
        // Toolbar buttons exposed through GTK-neutral core adapters
        public Button NewButton { get; }
        public Button DeleteButton { get; }
        public Button PauseButton { get; }
        public Button ResumeButton { get; }
        public Button OpenFileButton { get; }
        public Button OpenFolderButton { get; }
        // Header controls
        private readonly Button updateDot = Button.New();
        // Status bar elements
        private readonly Label lblExtensionStatus = Label.New("");
        private readonly Switch btnMonitoring;
        private readonly Button btnSpeedLimit;
        private readonly Button btnScheduler;
        private readonly Button btnSelectAll;
        private readonly MenuButton btnSort;
        private readonly Label lblSelectionCount = Label.New("");
        // Guards programmatic monitoring-checkbox updates from re-raising the toggle
        private bool syncingMonitoring;
        // Every row in the active sidebar view
        private List<DownloadItem> allRows = new();
        // Current sidebar view key
        private string currentView = InProgressKey;
        // Sidebar category values keyed by their persisted category name
        private readonly Dictionary<string, Category> categories = new();
        // Last repaint time per transfer, used to throttle progress refreshes
        private readonly Dictionary<string, long> lastRepaintTicks = new(StringComparer.Ordinal);
        // GTK application host and system tray manager
        private readonly GtkApplication app;
        private readonly Gtk4TrayIconManager trayManager;

        // Events consumed by the core window adapter
        public event EventHandler? SelectionChanged;
        public event Action<string>? ViewChanged;
        public event Action? RefreshRequested;
        public event EventHandler? BrowserMonitoringButtonClicked;
        public event EventHandler? SchedulerClicked;
        public event EventHandler? ClearAllFinishedClicked;
        public event EventHandler? UpdateClicked;
        public event EventHandler? CheckForUpdateClicked;
        public event EventHandler? HelpClicked;
        public event EventHandler? BugReportClicked;

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
                videoDownload: () => ApplicationContext.PlatformUIService?.ShowYoutubeDLDialog(),
                checkForUpdate: () => main.CheckForUpdateClicked?.Invoke(main, EventArgs.Empty),
                helpSupport: () => main.HelpClicked?.Invoke(main, EventArgs.Empty),
                reportProblem: () => main.BugReportClicked?.Invoke(main, EventArgs.Empty),
                removeFinished: () => main.ConfirmRemoveFinished(),
                mediaGrabber: () => ApplicationContext.PlatformUIService?.CreateAndShowMediaGrabber(),
                language: () => LanguageDialog.Show(main.Window),
                license: () => LicenseDialog.Show(main.Window));
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
            // Labelled primary action; the icon is tinted white to sit on the accent fill
            NewButton = Button.New();
            var newBox = Box.New(Orientation.Horizontal, 6);
            newBox.Valign = Align.Center;
            newBox.Append(Gtk4IconHelper.CreateTintedIcon("download-2-line", 255, 255, 255, 16));
            newBox.Append(Label.New(TextResource.GetText("LBL_ADD_DOWNLOAD") ?? "Add download"));
            NewButton.SetChild(newBox);
            NewButton.TooltipText = TextResource.GetText("DESC_NEW") ?? "New Download (Ctrl+N)";
            NewButton.AddCssClass("suggested-action");

            // Secondary actions are icon-only so the header stays inside the window width
            DeleteButton = CreateHeaderIconButton("delete-bin-7-line", "destructive-action",
                TextResource.GetText("DESC_DEL") ?? "Delete");
            PauseButton = CreateHeaderIconButton("pause-line", null,
                TextResource.GetText("MENU_PAUSE") ?? "Pause");
            ResumeButton = CreateHeaderIconButton("play-line", null,
                TextResource.GetText("MENU_RESUME") ?? "Resume");
            OpenFileButton = CreateHeaderIconButton("external-link-line", null,
                TextResource.GetText("CTX_OPEN_FILE") ?? "Open");
            OpenFolderButton = CreateHeaderIconButton("folder-shared-line", null,
                TextResource.GetText("CTX_OPEN_FOLDER") ?? "Open Folder");

            // Update-available indicator: hidden until an update lands
            updateDot.SetChild(Label.New("●"));
            updateDot.AddCssClass("flat");
            updateDot.AddCssClass("update-dot-button");
            updateDot.Visible = false;
            updateDot.OnClicked += (_, _) => UpdateClicked?.Invoke(this, EventArgs.Empty);

            // Bottom bar controls
            btnMonitoring = Switch.New();
            btnMonitoring.Valign = Align.Center;
            btnMonitoring.Active = Config.Instance.IsBrowserMonitoringEnabled;
            // Core owns the config flip (Application's handler); raising the event only once
            // avoids the double-toggle that made this control a no-op.
            btnMonitoring.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() != "active" || syncingMonitoring)
                {
                    return;
                }
                BrowserMonitoringButtonClicked?.Invoke(this, EventArgs.Empty);
            };

            lblExtensionStatus.AddCssClass("extension-status-label");

            btnSpeedLimit = Gtk4IconHelper.CreateActionButton(
                "time-line",
                TextResource.GetText("MENU_SPEED_LIMITER") ?? "Speed Limiter",
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
                TextResource.GetText("LBL_SELECT_ALL") ?? "Select All",
                "Select all downloads (Ctrl+A)",
                "flat",
                14);
            btnSelectAll.OnClicked += (_, _) => downloadList.ToggleSelectAll();

            // Bottom bar owns the list controls: select-all on the left, sort on the right
            btnSort = MenuButton.New();
            btnSort.SetIconName("view-sort-descending-symbolic");
            btnSort.SetLabel(downloadList.SortCaption);
            btnSort.SetMenuModel(BuildSortMenu());
            btnSort.AddCssClass("flat");
            btnSort.TooltipText = "Sort downloads";
            downloadList.SortChanged += (_, _) => btnSort.SetLabel(downloadList.SortCaption);

            lblSelectionCount.AddCssClass("dim-label");

            // GNOME pattern: search opens in a bar under the header
            searchEntry.Hexpand = true;
            searchBar.SetChild(searchEntry);
            searchBar.ConnectEntry(searchEntry);
            searchToggle.SetIconName("system-search-symbolic");
            searchToggle.AddCssClass("flat");
            searchToggle.TooltipText = "Search downloads (Ctrl+F)";
            searchToggle.OnToggled += (_, _) => searchBar.SearchModeEnabled = searchToggle.Active;
            searchEntry.OnStopSearch += (_, _) =>
            {
                searchBar.SearchModeEnabled = false;
                searchToggle.Active = false;
            };

            Window = Adw.ApplicationWindow.New(app);
            Window.Title = AppInfo.APP_NAME;
            Window.SetDefaultSize(DefaultWidth, DefaultHeight);

            // libadwaita shell: AdwApplicationWindow forbids SetTitlebar(), so the
            // header bar lives in an AdwToolbarView above the content (libadwaita 1.4+).
            var shell = Adw.ToolbarView.New();
            shell.AddTopBar(BuildHeaderBar());
            shell.SetContent(BuildBody());
            Window.SetContent(shell);

            DeleteButton.Visible = false;
            DeleteButton.Sensitive = false;
            PauseButton.Visible = false;
            ResumeButton.Visible = false;
            OpenFileButton.Visible = false;
            OpenFolderButton.Visible = false;
            downloadList.SelectionChanged += (_, _) =>
            {
                var selCount = downloadList.GetSelectedItems().Count;
                // Core enables/disables the row actions after this handler, so the
                // group is reconciled once its state has settled
                DeleteButton.Visible = true;
                GLib.Functions.IdleAdd(0, () =>
                {
                    UpdateRowActionsVisibility(selCount);
                    return false;
                });
                UpdateViewHeading();
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

            // The empty state offers the same primary action as the header, and can
            // clear a search that matched nothing
            downloadList.AddDownloadRequested += (_, _) => NewButton.Activate();
            downloadList.ClearSearchRequested += (_, _) =>
            {
                searchEntry.SetText("");
                searchToggle.Active = false;
                searchBar.SearchModeEnabled = false;
                ClearSearchImmediately();
            };

            // Browser-extension connectivity drives the live status badge
            ApplicationContext.ApplicationEvent += OnApplicationEvent;

            // Honour the saved theme mode (0 light, 1 dark, 2 follow system)
            Gtk4Theme.ApplyFromConfig(Window);
            UpdateExtensionStatus();
            UpdateSpeedLimitButtonState();
        }

        // Creates an icon-only header button with a tooltip and optional style class
        private static Button CreateHeaderIconButton(string iconName, string? cssClass, string tooltip)
        {
            var button = Gtk4IconHelper.CreateActionButton(iconName, null, tooltip, "flat", 16);
            if (!string.IsNullOrEmpty(cssClass))
            {
                button.AddCssClass(cssClass);
            }
            return button;
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
            lastRepaintTicks.Remove(id);
            ApplyView(currentView);
            UpdateActivitySummary();
        }

        // Re-syncs one row after a core state change (pause/resume/fail), which
        // arrives with no progress tick, so bound cells would otherwise stay frozen
        public void RefreshRowState(string id)
        {
            var item = allRows.FirstOrDefault(row => row.Id == id);
            if (item?.ProgressRow == null)
            {
                return;
            }
            item.CopyLiveStateFrom(item.ProgressRow);
            downloadList.UpdateRow(id);
            UpdateActivitySummary();
        }

        // Refreshes one row's live transfer state without rebuilding the list
        public void UpdateProgressRow(string id)
        {
            var item = allRows.FirstOrDefault(row => row.Id == id);
            if (item?.ProgressRow == null)
            {
                return;
            }

            item.CopyLiveStateFrom(item.ProgressRow);
            downloadList.UpdateRow(id);
            UpdateActivitySummary();
        }

        // Decides whether a progress tick should repaint, throttling per transfer
        public bool ShouldRepaintProgress(string id, int progress)
        {
            if (progress >= 100)
            {
                return true;
            }

            var now = Environment.TickCount64;
            if (lastRepaintTicks.TryGetValue(id, out var last) &&
                now - last < ProgressRepaintIntervalMs)
            {
                return false;
            }
            lastRepaintTicks[id] = now;
            return true;
        }

        // Resolves visible list selection to the core row adapters
        public IReadOnlyList<DownloadItem> GetSelectedItems() => downloadList.GetSelectedItems();

        // Selects the fixed sidebar view and refreshes the displayed rows
        public void SelectView(string key)
        {
            currentView = key;
            SelectSidebarRow(key);
            ApplyView(key);
            ViewChanged?.Invoke(key);
        }

        // Returns whether the active view is the in-progress list
        public bool IsInProgressView => currentView == InProgressKey;

        // Clears current row selection
        public void ClearSelection() => downloadList.ClearSelection();

        // Applies the current category filter; search is owned by the list itself
        private void ApplyView(string key)
        {
            IEnumerable<DownloadItem> rows = key switch
            {
                InProgressKey => allRows.Where(row => !row.IsComplete),
                FinishedKey => allRows.Where(row => row.IsComplete),
                _ => FilterByCategory(key)
            };
            downloadList.SetViewKind(key == FinishedKey);
            downloadList.Reload(rows);
            UpdateSidebarCounts();
            UpdateViewHeading();
            UpdateActivitySummary();
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
        internal static DownloadItem ToItem(Gtk4InProgressRow row)
        {
            var item = new DownloadItem
            {
                Id = row.DownloadEntry.Id,
                Name = row.Name,
                Source = BuildSourceText(row.DownloadEntry, includeExtension: false),
                Location = FriendlyPath(row.DownloadEntry.TargetDir),
                SizeText = FormattingHelper.FormatSize(row.Size),
                RawSize = row.Size,
                DateAdded = row.DateAdded,
                ProgressRow = row
            };
            item.CopyLiveStateFrom(row);
            return item;
        }

        // Converts a finished core row into the GTK 4 projection
        internal static DownloadItem ToItem(Gtk4FinishedRow row) => new()
        {
            Id = row.DownloadEntry.Id,
            Name = row.Name,
            Source = BuildSourceText(row.DownloadEntry, includeExtension: true),
            Location = FriendlyPath(row.DownloadEntry.TargetDir),
            Status = TextResource.GetText("STAT_FINISHED") ?? "Complete",
            SizeText = FormattingHelper.FormatSize(row.Size),
            RawSize = row.Size,
            Progress = 1.0,
            RawProgress = 100,
            DateAdded = row.DateAdded,
            IsComplete = true,
            FinishedRow = row
        };

        // Builds the row's source line: the site name plus the file type for finished rows
        private static string BuildSourceText(DownloadItemBase entry, bool includeExtension)
        {
            var parts = new List<string>();

            var domain = ExtractDomain(entry.PrimaryUrl);
            if (!string.IsNullOrEmpty(domain))
            {
                parts.Add("◉ " + domain);
            }

            if (includeExtension)
            {
                var extension = System.IO.Path.GetExtension(entry.Name)?.TrimStart('.').ToUpperInvariant();
                if (!string.IsNullOrEmpty(extension))
                {
                    parts.Add(extension);
                }
            }

            return string.Join("   ·   ", parts);
        }

        // Shortens an absolute folder to a ~/ path for the row meta line
        private static string FriendlyPath(string? folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return string.Empty;
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return !string.IsNullOrEmpty(home) && folder.StartsWith(home, StringComparison.Ordinal)
                ? "~" + folder.Substring(home.Length)
                : folder;
        }

        // Extracts a display domain from the download's primary URL
        private static string ExtractDomain(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            try
            {
                if (url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
                {
                    return "blob";
                }
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var host = uri.Host;
                    return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host.Substring(4) : host;
                }
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Domain parse failed: " + ex.Message);
            }
            return string.Empty;
        }

        // Ctrl+F opens the search bar and focuses the field
        private void FocusSearch()
        {
            searchToggle.Active = true;
            searchBar.SearchModeEnabled = true;
            searchEntry.GrabFocus();
        }

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

        // F1 displays the GNOME About window using the shared application metadata
        private void OnAbout() => BuildAboutWindow(Window).Present();

        // Builds the About window; static so the smoke self-test can assert the
        // Legal and Credits pages are populated without presenting anything
        internal static Adw.AboutWindow BuildAboutWindow(Window parent)
        {
            var about = Adw.AboutWindow.New();
            about.SetTransientFor(parent);
            about.SetApplicationName(AppInfo.APP_FULL_NAME);
            about.SetApplicationIcon(AppInfo.APP_ID);
            about.SetVersion(AppInfo.APP_VERSION);
            about.SetComments(TextResource.GetText("APP_TAGLINE") ?? "A download manager that just works.");
            about.SetDeveloperName("Mayanktaker Computers & Web Development");
            about.SetWebsite(AppInfo.APP_HOMEPAGE_TEXT);
            about.SetSupportUrl(Links.SupportUrl);
            about.SetIssueUrl(Links.IssueUrl);
            about.SetCopyright(AppInfo.APP_COPYRIGHT_TEXT);
            // Legal page: the project's own LICENSE text (GPL-2.0-or-later). The type
            // alone leaves libadwaita's Legal page empty on newer versions.
            // libadwaita treats the license type and a license body as exclusive: a
            // type-only window renders an empty Legal page here, so the full text wins
            var licenseText = LicenseText.Load();
            if (!string.IsNullOrEmpty(licenseText))
            {
                about.SetLicense(licenseText);
            }
            else
            {
                about.SetLicenseType(Gtk.License.Gpl20);
            }
            // Credits page: the company name stands alone (the Website row already
            // links it); a trailing "(url)" renders as a stray bracket otherwise
            about.SetDevelopers(new[]
            {
                "Mayanktaker Computers & Web Development",
                AppInfo.APP_ORIGINAL_AUTHOR_CREDIT
            });
            about.SetTranslatorCredits(TextResource.GetText("APP_TRANSLATOR_CREDITS") ?? "See the Lang contributors.");
            return about;
        }

        // Ctrl+T swaps light/dark and remembers the choice
        private void ToggleTheme() => Gtk4Theme.Toggle(Window);

        // Asks before clearing the finished list, then raises the core handler
        private void ConfirmRemoveFinished()
        {
            var title = TextResource.GetText("MENU_DELETE_COMPLETED") ?? "Remove Finished Downloads";
            var message = TextResource.GetText("MSG_CONFIRM_CLEAR_FINISHED")
                ?? "Remove all finished downloads from the list?";
            if (Gtk4DialogHelper.Confirm(Window, message, title))
            {
                ClearAllFinishedClicked?.Invoke(this, EventArgs.Empty);
            }
        }

        // Reveals the update-available dot in the header
        public void ShowUpdateDot(bool visible) => updateDot.Visible = visible;

        // Re-reads the browser-monitoring preference into the checkbox
        public void RefreshBrowserMonitorButton()
        {
            syncingMonitoring = true;
            try
            {
                btnMonitoring.Active = Config.Instance.IsBrowserMonitoringEnabled;
            }
            finally
            {
                syncingMonitoring = false;
            }
        }

        // Refreshes the live extension badge and the speed-limiter visual state
        private void OnApplicationEvent(object? sender, ApplicationEvent e)
        {
            if (e.EventType != "ExtensionConnectionChanged" && e.EventType != "ConfigChanged")
            {
                return;
            }
            GLib.Functions.IdleAdd(0, () =>
            {
                UpdateExtensionStatus();
                UpdateSpeedLimitButtonState();
                RefreshBrowserMonitorButton();
                return false;
            });
        }

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

        // Screenshot preview for docs captures: demo rows (and an optional view)
        // instead of the user's real downloads. Driven by --shot=<mode>.
        internal void LoadDemoPreview(string mode)
        {
            SetRows(DemoRows(), InProgressKey);
            if (mode == "complete")
            {
                SelectView(FinishedKey);
            }
        }

        // Formats an in-progress row for display
        private static IEnumerable<DownloadItem> DemoRows() => new List<DownloadItem>
        {
            new() { Id = "demo-1", Name = "ubuntu-24.04-desktop-amd64.iso", Status = "Downloading · 4.2 MB/s", DownloadSpeed = "4.2 MB/s", SizeText = "1.8 GB", Progress = 0.42, RawProgress = 42 },
            new() { Id = "demo-2", Name = "annual-report-2026.pdf", Status = "Paused", SizeText = "12.4 MB", Progress = 0.68, RawProgress = 68 },
            new() { Id = "demo-3", Name = "conference-talk.mp4", Status = "Complete", SizeText = "740 MB", Progress = 1.0, RawProgress = 100, IsComplete = true },
            new() { Id = "demo-4", Name = "dataset-2026-10.zip", Status = "Downloading · 900 KB/s", DownloadSpeed = "900 KB/s", SizeText = "3.1 GB", Progress = 0.11, RawProgress = 11 },
            new() { Id = "demo-5", Name = "design-system.fig", Status = "Waiting", SizeText = "84 MB" }
        };
    }
}
