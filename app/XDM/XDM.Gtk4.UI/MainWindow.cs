// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 main window: headerbar with search + theme toggle, category sidebar,
// ColumnView download list, status bar, and the app action/accelerator map.
// Scope is the shell + list widget; download-engine wiring is the next step
// (docs/audit/gtk4-coupling-inventory.md).
using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using XDM.Core;
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

        // Builds the window, wires actions and shows the initial view
        public static ApplicationWindow Create(GtkApplication app, bool demo)
        {
            var main = new MainWindow(app, demo);
            AppActions.Register(
                app,
                newDownload: main.OnNewDownload,
                search: main.FocusSearch,
                toggleTheme: main.ToggleTheme);
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

            if (demo)
            {
                downloadList.Reload(DemoRows());
            }
            UpdateStatus();

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

            list.Append(BuildSidebarRow("All Unfinished", "inprogress"));
            list.Append(BuildSidebarRow("All Finished", "finished"));
            foreach (var category in Config.Instance.Categories)
            {
                list.Append(BuildSidebarRow(category.DisplayName, category.Name));
            }

            list.OnRowSelected += (_, args) =>
            {
                if (args.Row is ListBoxRow row)
                {
                    SetStatus($"Selected: {row.TooltipText} · {downloadList.Count} downloads");
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

        // Status bar: row count + last action feedback
        private Widget BuildStatusBar()
        {
            statusLabel.Halign = Align.Start;
            statusLabel.MarginTop = 6;
            statusLabel.MarginBottom = 6;
            statusLabel.MarginStart = 12;
            statusLabel.AddCssClass("status-bar");
            return statusLabel;
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

        // Updates the status bar with the current row count
        private void UpdateStatus()
        {
            SetStatus($"GTK 4 · {downloadList.Count} downloads · {Config.Instance.Categories.Count()} categories");
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
