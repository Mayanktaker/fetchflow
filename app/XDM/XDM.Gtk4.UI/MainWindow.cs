// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 main window: headerbar with search + theme toggle, category sidebar,
// ColumnView download list, status bar, and the app action/accelerator map.
// Rows are read from the shared download database (read-only) until the download
// engine itself is ported — see docs/audit/gtk4-coupling-inventory.md.
using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using XDM.Core;
using XDM.Core.Util;
using XDM.Gtk4UI.Downloads;
using GtkApplication = Gtk.Application;

namespace XDM.Gtk4UI
{
    internal sealed class MainWindow
    {
        // Sidebar width token from docs/design.md spacing scale
        private const int SidebarWidth = 200;
        // Default window size
        private const int DefaultWidth = 900;
        private const int DefaultHeight = 560;
        // Sidebar keys for the two fixed views
        private const string InProgressKey = "inprogress";
        private const string FinishedKey = "finished";

        // The window this instance owns
        private readonly ApplicationWindow window;
        // Download list (ColumnView) widget
        private readonly DownloadListView downloadList = new();
        // Toolbar search entry (Ctrl+F target)
        private readonly SearchEntry searchEntry = SearchEntry.New();
        // Header theme toggle (Ctrl+T target)
        private readonly Button themeButton = Button.NewWithLabel("Light");
        // Status bar text
        private readonly Label statusLabel = Label.New("");
        // Every loaded row (unfiltered by view)
        private List<DownloadItem> allRows = new();
        // Sidebar key currently applied to the list
        private string currentView = InProgressKey;

        // Builds the window, wires actions and shows the initial view
        public static ApplicationWindow Create(GtkApplication app, bool demo)
        {
            var main = new MainWindow(app, demo);
            AppActions.Register(
                app,
                newDownload: main.OnNewDownload,
                search: main.FocusSearch,
                toggleTheme: main.ToggleTheme,
                refresh: main.Refresh);
            return main.window;
        }

        // Assembles the window once; all wiring happens here
        private MainWindow(GtkApplication app, bool demo)
        {
            window = ApplicationWindow.New(app);
            window.Title = "FetchFlow";
            window.SetDefaultSize(DefaultWidth, DefaultHeight);

            window.SetTitlebar(BuildHeaderBar());
            window.SetChild(BuildBody());

            // Real rows come from the read-only DB bridge; --demo keeps a sample set
            allRows = (demo ? DemoRows() : DownloadRepository.LoadAll()).ToList();
            ApplyView(currentView);

            Gtk4Theme.Apply(window, Gtk4Theme.IsDarkActive);
        }

        // Title bar: brand title, search entry, theme toggle
        private Widget BuildHeaderBar()
        {
            var header = HeaderBar.New();
            header.TitleWidget = Label.New("FetchFlow");

            searchEntry.PlaceholderText = "Search downloads";
            searchEntry.WidthRequest = 220;
            searchEntry.OnSearchChanged += (_, _) => downloadList.Filter(searchEntry.GetText());
            header.PackStart(searchEntry);

            themeButton.TooltipText = "Switch theme (Ctrl+T)";
            themeButton.OnClicked += (_, _) => ToggleTheme();
            header.PackEnd(themeButton);
            return header;
        }

        // Two-pane body: category sidebar + list over a status bar
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

        // Category sidebar: the two fixed views plus every configured category
        private Widget BuildSidebar()
        {
            var list = ListBox.New();
            list.AddCssClass("sidebar");
            list.SelectionMode = SelectionMode.Browse;

            list.Append(BuildSidebarRow("All Unfinished", InProgressKey));
            list.Append(BuildSidebarRow("All Finished", FinishedKey));
            foreach (var category in Config.Instance.Categories)
            {
                list.Append(BuildSidebarRow(category.DisplayName, category.Name));
            }

            list.OnRowSelected += (_, args) =>
            {
                if (args.Row is ListBoxRow row)
                {
                    ApplyView(row.TooltipText ?? InProgressKey);
                }
            };

            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            scrolled.SetChild(list);
            scrolled.WidthRequest = SidebarWidth;
            return scrolled;
        }

        // One selectable sidebar row (label + view key in the tooltip)
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
            row.TooltipText = key;
            return row;
        }

        // Status bar: view, row count and category count
        private Widget BuildStatusBar()
        {
            statusLabel.Halign = Align.Start;
            statusLabel.MarginTop = 6;
            statusLabel.MarginBottom = 6;
            statusLabel.MarginStart = 12;
            statusLabel.AddCssClass("status-bar");
            return statusLabel;
        }

        // Applies a sidebar view key to the loaded rows
        private void ApplyView(string key)
        {
            currentView = key;
            IEnumerable<DownloadItem> rows = key switch
            {
                InProgressKey => allRows.Where(r => !r.IsComplete),
                FinishedKey => allRows.Where(r => r.IsComplete),
                _ => FilterByCategory(key)
            };
            downloadList.Reload(rows);
            UpdateStatus();
        }

        // Category rows use the same keyword/category matcher as the GTK 3 frontend
        private IEnumerable<DownloadItem> FilterByCategory(string key)
        {
            // Category is a struct, so "not found" is an empty Name, not null
            var category = Config.Instance.Categories.FirstOrDefault(c => c.Name == key);
            if (string.IsNullOrEmpty(category.Name))
            {
                return allRows;
            }
            return allRows.Where(r => Helpers.IsOfCategoryOrMatchesKeyword(r.Name, null, category));
        }

        // F5: re-read the download database (engine still belongs to the GTK 3 frontend)
        private void Refresh()
        {
            allRows = DownloadRepository.LoadAll().ToList();
            ApplyView(currentView);
        }

        // Ctrl+N: download engine is not wired yet, so report it instead of doing nothing
        private void OnNewDownload()
        {
            SetStatus("New download: engine wiring pending");
        }

        // Ctrl+F: focus the toolbar search
        private void FocusSearch()
        {
            searchEntry.GrabFocus();
        }

        // Ctrl+T / header button: swap the stylesheet
        private void ToggleTheme()
        {
            Gtk4Theme.Toggle();
            themeButton.SetLabel(Gtk4Theme.IsDarkActive ? "Light" : "Dark");
        }

        // Updates the status bar with the active view and row count
        private void UpdateStatus()
        {
            SetStatus($"GTK 4 · {currentView} · {downloadList.Count} of {allRows.Count} downloads · {Config.Instance.Categories.Count()} categories");
        }

        // Sets the status bar text
        private void SetStatus(string text)
        {
            statusLabel.SetText(text);
        }

        // Sample rows for --demo runs (no engine is attached to this frontend yet)
        private static IEnumerable<DownloadItem> DemoRows() => new List<DownloadItem>
        {
            new() { Name = "ubuntu-24.04-desktop-amd64.iso", Status = "Downloading · 4.2 MB/s", SizeText = "1.8 GB", Progress = 0.42 },
            new() { Name = "annual-report-2026.pdf", Status = "Paused", SizeText = "12.4 MB", Progress = 0.68 },
            new() { Name = "conference-talk.mp4", Status = "Complete", SizeText = "740 MB", Progress = 1.0, IsComplete = true },
            new() { Name = "dataset-2026-10.zip", Status = "Downloading · 900 KB/s", SizeText = "3.1 GB", Progress = 0.11 }
        };
    }
}
