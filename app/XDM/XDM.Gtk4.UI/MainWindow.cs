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
        private readonly Button themeButton = Button.NewWithLabel("Light");
        // Status bar
        private readonly Label statusLabel = Label.New("");
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

        // Builds the main window and registers its application actions
        public static MainWindow Create(GtkApplication app, bool demo)
        {
            var main = new MainWindow(app);
            AppActions.Register(
                app,
                newDownload: main.OnNewDownload,
                search: main.FocusSearch,
                toggleTheme: main.ToggleTheme,
                refresh: main.Refresh);
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
            NewButton = Button.NewWithLabel(TextResource.GetText("DESC_NEW"));
            DeleteButton = Button.NewWithLabel(TextResource.GetText("DESC_DEL"));
            PauseButton = Button.NewWithLabel(TextResource.GetText("MENU_PAUSE"));
            ResumeButton = Button.NewWithLabel(TextResource.GetText("MENU_RESUME"));
            OpenFileButton = Button.NewWithLabel(TextResource.GetText("CTX_OPEN_FILE"));
            OpenFolderButton = Button.NewWithLabel(TextResource.GetText("CTX_OPEN_FOLDER"));

            Window = ApplicationWindow.New(app);
            Window.Title = "FetchFlow";
            Window.SetDefaultSize(DefaultWidth, DefaultHeight);
            Window.SetTitlebar(BuildHeaderBar());
            Window.SetChild(BuildBody());

            DeleteButton.Visible = false;
            PauseButton.Visible = false;
            ResumeButton.Visible = false;
            OpenFileButton.Visible = false;
            OpenFolderButton.Visible = false;
            downloadList.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);

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

        // Packs the app title, core actions, search and theme toggle
        private Widget BuildHeaderBar()
        {
            var header = HeaderBar.New();
            header.TitleWidget = Label.New("FetchFlow");
            header.PackStart(NewButton);
            header.PackStart(PauseButton);
            header.PackStart(ResumeButton);
            header.PackStart(DeleteButton);
            header.PackStart(OpenFileButton);
            header.PackStart(OpenFolderButton);

            searchEntry.PlaceholderText = TextResource.GetText("LBL_SEARCH");
            searchEntry.WidthRequest = 210;
            searchEntry.OnSearchChanged += (_, _) => ApplyView(currentView);
            header.PackEnd(searchEntry);

            themeButton.TooltipText = "Switch theme (Ctrl+T)";
            themeButton.OnClicked += (_, _) => ToggleTheme();
            header.PackEnd(themeButton);
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
            list.Append(BuildSidebarRow(TextResource.GetText("ALL_UNFINISHED"), InProgressKey));
            list.Append(BuildSidebarRow(TextResource.GetText("ALL_FINISHED"), FinishedKey));

            foreach (var category in Config.Instance.Categories)
            {
                categories[category.Name] = category;
                list.Append(BuildSidebarRow(category.DisplayName, category.Name));
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

        // Creates one accessible sidebar row
        private static ListBoxRow BuildSidebarRow(string text, string key)
        {
            var label = Label.New(text);
            label.Halign = Align.Start;
            label.MarginTop = 8;
            label.MarginBottom = 8;
            label.MarginStart = 12;
            label.MarginEnd = 12;

            var row = ListBoxRow.New();
            row.SetChild(label);
            row.Name = key;
            row.TooltipText = text;
            return row;
        }

        // Builds the status bar below the resizable panes
        private Widget BuildStatusBar()
        {
            statusLabel.Halign = Align.Start;
            statusLabel.MarginTop = 6;
            statusLabel.MarginBottom = 6;
            statusLabel.MarginStart = 12;
            statusLabel.AddCssClass("status-bar");
            return statusLabel;
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
            Progress = Math.Clamp(row.Progress / 100.0, 0.0, 1.0),
            ProgressRow = row
        };

        // Converts a finished core row into the GTK 4 projection
        internal static DownloadItem ToItem(Gtk4FinishedRow row) => new()
        {
            Name = row.Name,
            Status = TextResource.GetText("STAT_FINISHED"),
            SizeText = FormattingHelper.FormatSize(row.Size),
            Progress = 1.0,
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

        // Ctrl+T swaps the GTK 4 stylesheet
        private void ToggleTheme()
        {
            Gtk4Theme.Toggle();
            themeButton.SetLabel(Gtk4Theme.IsDarkActive ? "Light" : "Dark");
        }

        // Updates the footer summary for the active view
        private void UpdateStatus()
        {
            SetStatus($"GTK 4 · {currentView} · {downloadList.Count} downloads · {Config.Instance.Categories.Count()} categories");
        }

        // Sets the footer message
        public void SetStatus(string text) => statusLabel.SetText(text);

        // Presents and activates the main window
        public void ShowAndActivate() => Window.Present();

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

        // Presents the native GTK window
        public void Present() => Window.Present();

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
