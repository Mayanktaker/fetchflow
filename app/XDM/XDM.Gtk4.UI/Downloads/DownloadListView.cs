// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 download list: ColumnView with a name/status column, a size column and a
// progress column. Replaces the GTK 3 TreeView + cell renderers, which GTK 4
// deprecates. Rows resolve through DownloadStore.Items by list position.
using System;
using System.Collections.Generic;
using Gtk;

namespace XDM.Gtk4UI.Downloads
{
    internal sealed class DownloadListView
    {
        // Fixed geometry from docs/design.md (gutter / progress column widths)
        private const int ProgressColumnWidth = 120;
        private const int SizeColumnWidth = 96;

        // Row data + its StringList projection
        private readonly DownloadStore store = new();
        // The column view itself
        private readonly ColumnView view;
        // Selection model is retained for IApplicationWindow row queries
        private MultiSelection selection;
        // Swaps between the empty-state page and the list page
        private readonly Stack stack;

        // Root widget to pack into the window
        public Widget Root => stack;

        // Current row count (filtered)
        public int Count => store.Count;
        // Current filtered items backing the visible rows
        public IReadOnlyList<DownloadItem> Items => store.Items;
        // Fires after the GTK selection changes
        public event EventHandler? SelectionChanged;

        // Builds the list widget and its empty state
        public DownloadListView()
        {
            selection = MultiSelection.New(store.Model);
            view = ColumnView.New(selection);
            selection.OnSelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
            view.AddCssClass("download-list");
            view.Hexpand = true;
            view.Vexpand = true;

            view.AppendColumn(BuildNameColumn());
            view.AppendColumn(BuildSizeColumn());
            view.AppendColumn(BuildProgressColumn());

            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            scrolled.SetChild(view);
            scrolled.Hexpand = true;
            scrolled.Vexpand = true;

            stack = Stack.New();
            stack.AddNamed(scrolled, "list");
            stack.AddNamed(BuildEmptyState(), "empty");
            stack.VisibleChildName = "empty";
        }

        // Replaces the data set and re-binds the model
        public void Reload(IEnumerable<DownloadItem> items)
        {
            store.SetAll(items);
            Bind();
        }

        // Applies the toolbar search keyword
        public void Filter(string keyword)
        {
            store.Filter(keyword);
            Bind();
        }

        // Returns the selected rows in visible order
        public IReadOnlyList<DownloadItem> GetSelectedItems()
        {
            var selected = new List<DownloadItem>();
            for (uint i = 0; i < store.Count; i++)
            {
                if (selection.IsSelected(i))
                {
                    selected.Add(store.Items[(int)i]);
                }
            }
            return selected;
        }

        // Clears selection before switching sidebar views
        public void ClearSelection() => selection.UnselectAll();

        // Points the view at the store's current model and updates the empty state
        private void Bind()
        {
            selection = MultiSelection.New(store.Model);
            selection.OnSelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
            view.SetModel(selection);
            stack.VisibleChildName = store.Count == 0 ? "empty" : "list";
        }

        // Name + dim status, two lines (matches the GTK 3 row layout)
        private ColumnViewColumn BuildNameColumn()
        {
            var factory = SignalListItemFactory.New();
            factory.OnSetup += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var name = Label.New("");
                name.Halign = Align.Start;
                name.AddCssClass("download-name");

                var status = Label.New("");
                status.Halign = Align.Start;
                status.AddCssClass("dim-label");
                status.AddCssClass("download-status");

                var box = Box.New(Orientation.Vertical, 0);
                box.MarginTop = 6;
                box.MarginBottom = 6;
                box.MarginStart = 12;
                box.MarginEnd = 12;
                box.Append(name);
                box.Append(status);
                item.SetChild(box);
            };
            factory.OnBind += (_, args) =>
            {
                var item = (ListItem)args.Object;
                if (item.Child is not Box box || !TryGetItem(item, out var row))
                {
                    return;
                }
                // First child is the name label, last child the dim status label
                if (box.GetFirstChild() is Label name)
                {
                    name.SetText(row.Name);
                }
                if (box.GetLastChild() is Label status)
                {
                    status.SetText(row.Status);
                }
            };

            var column = ColumnViewColumn.New("File name", factory);
            column.Expand = true;
            column.Resizable = true;
            return column;
        }

        // Right-aligned size text
        private ColumnViewColumn BuildSizeColumn()
        {
            var factory = SignalListItemFactory.New();
            factory.OnSetup += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var label = Label.New("");
                label.Halign = Align.End;
                label.MarginStart = 8;
                label.MarginEnd = 12;
                label.AddCssClass("download-size");
                item.SetChild(label);
            };
            factory.OnBind += (_, args) =>
            {
                var item = (ListItem)args.Object;
                if (item.Child is Label label && TryGetItem(item, out var row))
                {
                    label.SetText(row.SizeText);
                }
            };

            var column = ColumnViewColumn.New("Size", factory);
            column.FixedWidth = SizeColumnWidth;
            column.Resizable = true;
            return column;
        }

        // Pill progress bar (finished rows read as full)
        private ColumnViewColumn BuildProgressColumn()
        {
            var factory = SignalListItemFactory.New();
            factory.OnSetup += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var bar = ProgressBar.New();
                bar.SetShowText(false);
                bar.Valign = Align.Center;
                bar.MarginStart = 8;
                bar.MarginEnd = 12;
                bar.AddCssClass("download-progress");
                item.SetChild(bar);
            };
            factory.OnBind += (_, args) =>
            {
                var item = (ListItem)args.Object;
                if (item.Child is ProgressBar bar && TryGetItem(item, out var row))
                {
                    bar.Fraction = row.IsComplete ? 1.0 : Math.Clamp(row.Progress, 0.0, 1.0);
                }
            };

            var column = ColumnViewColumn.New("Progress", factory);
            column.FixedWidth = ProgressColumnWidth;
            return column;
        }

        // Empty state shown when there are no rows (or the search matched nothing)
        private static Widget BuildEmptyState()
        {
            var title = Label.New("No downloads to show");
            title.AddCssClass("empty-title");

            var hint = Label.New("Downloads you start will appear here.");
            hint.AddCssClass("dim-label");

            var box = Box.New(Orientation.Vertical, 6);
            box.Valign = Align.Center;
            box.Halign = Align.Center;
            box.Vexpand = true;
            box.Append(title);
            box.Append(hint);
            return box;
        }

        // Resolves a list item's position back to its DownloadItem
        private bool TryGetItem(ListItem item, out DownloadItem row)
        {
            row = null!;
            var position = item.Position;
            if (position == uint.MaxValue || position >= store.Items.Count)
            {
                return false;
            }
            row = store.Items[(int)position];
            return true;
        }
    }
}
