// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 main window scaffold: headerbar, category sidebar, download-list area.
// Scope is the shell only — the GTK 3 frontend keeps shipping until list/action
// parity lands (docs/audit/gtk4-coupling-inventory.md).
using System;
using System.Linq;
using Gtk;
using XDM.Core;
using GtkApplication = Gtk.Application;

namespace XDM.Gtk4UI
{
    internal static class MainWindow
    {
        // Sidebar width token from docs/design.md spacing/radius scale
        private const int SidebarWidth = 200;
        // Default window size and responsive floor
        private const int DefaultWidth = 900;
        private const int DefaultHeight = 560;

        private static Label? statusLabel;

        // Builds the GTK 4 application window (one per activation)
        public static ApplicationWindow Create(GtkApplication app)
        {
            var window = ApplicationWindow.New(app);
            window.Title = "FetchFlow";
            window.SetDefaultSize(DefaultWidth, DefaultHeight);

            window.SetTitlebar(BuildHeaderBar());
            window.SetChild(BuildBody());

            Gtk4Theme.Apply(window, Gtk4Theme.IsDarkActive);
            return window;
        }

        // Title bar: brand title on the left, theme toggle on the right
        private static HeaderBar BuildHeaderBar()
        {
            var header = HeaderBar.New();
            header.TitleWidget = Label.New("FetchFlow");

            var themeButton = Button.NewWithLabel(Gtk4Theme.IsDarkActive ? "Light" : "Dark");
            themeButton.TooltipText = "Switch theme";
            themeButton.OnClicked += (_, _) =>
            {
                Gtk4Theme.Toggle();
                themeButton.SetLabel(Gtk4Theme.IsDarkActive ? "Light" : "Dark");
            };
            header.PackEnd(themeButton);
            return header;
        }

        // Two-pane body: category sidebar + content area over a status bar
        private static Widget BuildBody()
        {
            var paned = Paned.New(Orientation.Horizontal);
            paned.StartChild = BuildSidebar();
            paned.EndChild = BuildContent();
            paned.Position = SidebarWidth;
            paned.ResizeStartChild = false;
            paned.ShrinkStartChild = false;

            var root = Box.New(Orientation.Vertical, 0);
            root.Append(paned);
            root.Append(BuildStatusBar());
            return root;
        }

        // Category sidebar: the two fixed views plus every configured category
        private static Widget BuildSidebar()
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
                    SetStatus("Selected: " + (row.TooltipText ?? "view"));
                }
            };

            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            scrolled.SetChild(list);
            scrolled.WidthRequest = SidebarWidth;
            return scrolled;
        }

        // One selectable sidebar row (label + dim view key)
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

        // Content area: list widget that replaces the GTK 3 TreeView
        private static Widget BuildContent()
        {
            var factory = SignalListItemFactory.New();
            factory.OnSetup += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var label = Label.New("");
                label.Halign = Align.Start;
                label.MarginStart = 12;
                label.MarginEnd = 12;
                item.SetChild(label);
            };
            factory.OnBind += (_, args) =>
            {
                var item = (ListItem)args.Object;
                if (item.Child is Label label && item.Item is StringObject text)
                {
                    label.SetText(text.GetString());
                }
            };

            // Placeholder rows: the real model arrives with the list-parity port
            var model = SingleSelection.New(StringList.New(new[]
            {
                "GTK 4 frontend scaffold",
                "Download list parity pending",
                "GTK 3 frontend remains the shipping UI"
            }));

            var list = ListView.New(model, factory);
            list.AddCssClass("download-list");
            list.Vexpand = true;

            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            scrolled.SetChild(list);
            scrolled.Hexpand = true;
            scrolled.Vexpand = true;
            return scrolled;
        }

        // Status bar: proves shared-config access from the GTK 4 frontend
        private static Widget BuildStatusBar()
        {
            statusLabel = Label.New($"GTK 4 scaffold · {Config.Instance.Categories.Count()} categories");
            statusLabel.Halign = Align.Start;
            statusLabel.MarginTop = 6;
            statusLabel.MarginBottom = 6;
            statusLabel.MarginStart = 12;
            statusLabel.AddCssClass("status-bar");
            return statusLabel;
        }

        // Updates the status bar text (sidebar selection feedback)
        private static void SetStatus(string text)
        {
            statusLabel?.SetText(text);
        }
    }
}
