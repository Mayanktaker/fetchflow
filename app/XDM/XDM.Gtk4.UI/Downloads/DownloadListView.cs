// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 download list: ColumnView with multi-select gutter checkbox, category-tinted
// file type icon + name, size, and dynamic progress (date badge for finished rows,
// live speed/ETA and progress bar for active rows).
//
// Selection and model stability: the Gio.ListStore and the MultiSelection are created
// once and only their *contents* change on a view/search/sort switch. Progress updates
// never touch the model; they refresh the already-bound cell widgets for that row, so a
// running download cannot clear the user's selection.
using System;
using System.Collections.Generic;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Downloads
{
    internal sealed class DownloadListView
    {
        // Fixed geometry from docs/design.md
        private const int CheckboxColumnWidth = 36;
        private const int SizeColumnWidth = 110;
        private const int ProgressColumnWidth = 180;

        // Tri-state select-all glyphs for the gutter header (same set as GTK 3)
        private const string GutterGlyphNone = "☐";
        private const string GutterGlyphSome = "▣";
        private const string GutterGlyphAll = "☑";

        // Row data behind the model
        private readonly DownloadStore store = new();
        // Row identity model: one Gtk.StringObject per row holding the transfer id
        private readonly Gio.ListStore rowStore = Gio.ListStore.New(StringObject.GetGType());
        // The column view itself
        private readonly ColumnView view;
        // Selection model is created once and reused for every view/search/sort change
        private MultiSelection selection;
        // Swaps between the empty-state page and the list page
        private readonly Stack stack;

        // Individual columns
        private readonly ColumnViewColumn colCheckbox;
        private readonly ColumnViewColumn colName;
        private readonly ColumnViewColumn colSize;
        private readonly ColumnViewColumn colProgress;

        // Authoritative selection, kept as transfer ids so it survives reloads
        private readonly HashSet<string> selectedIds = new(StringComparer.Ordinal);
        // Guard against recursive selection bookkeeping
        private bool isUpdatingSelection;

        // Currently bound cell widgets, so a progress tick can refresh them in place
        private readonly Dictionary<string, NameCells> nameCells = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Label> sizeCells = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ProgressCells> progressCells = new(StringComparer.Ordinal);

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
            selection = MultiSelection.New(rowStore);
            selection.OnSelectionChanged += (_, _) => OnSelectionChanged();
            view = ColumnView.New(selection);
            view.AddCssClass("download-list");
            // Native row/column separators instead of hand-drawn dividers
            view.ShowRowSeparators = true;
            view.ShowColumnSeparators = true;
            view.Hexpand = true;
            view.Vexpand = true;

            colCheckbox = BuildCheckboxColumn();
            colName = BuildNameColumn();
            colSize = BuildSizeColumn();
            colProgress = BuildProgressColumn();

            view.AppendColumn(colCheckbox);
            view.AppendColumn(colName);
            view.AppendColumn(colSize);
            view.AppendColumn(colProgress);

            WireColumnSorting();
            UpdateSortHeaderTitles();
            UpdateGutterGlyph();

            // Double click / Enter key activation: executes standard double-click action
            view.OnActivate += (_, args) =>
            {
                var pos = args.Position;
                if (pos != uint.MaxValue && pos < (uint)store.Count)
                {
                    selection.SelectItem(pos, true);
                    UIActions.OnDblClick();
                }
            };

            view.OnMap += (_, _) => TryWireHeaders();

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
            SyncModel();
        }

        // Applies the toolbar search keyword without leaving the current view
        public void Filter(string keyword)
        {
            store.Filter(keyword);
            SyncModel();
        }

        // Refreshes one row's visible cells in place (never touches selection or model)
        public void UpdateRow(string id)
        {
            var item = store.Lookup(id);
            if (item == null)
            {
                return;
            }

            if (nameCells.TryGetValue(id, out var name))
            {
                name.Name.SetText(item.Name);
                name.Status.SetText(RowSubLine(item));
            }

            if (sizeCells.TryGetValue(id, out var size))
            {
                size.SetText(item.SizeText);
            }

            if (progressCells.TryGetValue(id, out var progress))
            {
                BindProgressCells(progress, item);
            }
        }

        // Marks which surface the list is showing (Active vs Complete)
        public void SetViewKind(bool finished)
        {
            if (finished)
            {
                view.RemoveCssClass("unfinished");
                view.AddCssClass("finished");
                return;
            }
            view.RemoveCssClass("finished");
            view.AddCssClass("unfinished");
        }

        // Returns the selected rows in visible order
        public IReadOnlyList<DownloadItem> GetSelectedItems()
        {
            var selected = new List<DownloadItem>(selectedIds.Count);
            for (uint i = 0; i < (uint)store.Count; i++)
            {
                if (selection.IsSelected(i))
                {
                    selected.Add(store.Items[(int)i]);
                }
            }
            return selected;
        }

        // Selects all rows
        public void SelectAll() => selection.SelectAll();

        // Clears current row selection
        public void ClearSelection()
        {
            ApplySelection(new HashSet<string>(StringComparer.Ordinal));
        }

        // Toggles select-all: unselects if all selected, otherwise selects all
        public void ToggleSelectAll()
        {
            if (store.Count == 0)
            {
                return;
            }

            var allSelected = selectedIds.Count >= store.Count;
            if (allSelected)
            {
                ApplySelection(new HashSet<string>(StringComparer.Ordinal));
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in store.Items)
            {
                ids.Add(item.Id);
            }
            ApplySelection(ids);
        }

        // Sorts downloads by the requested column and direction
        public void SortBy(DownloadSortColumn col, bool ascending)
        {
            if (store.CurrentSortColumn == col && store.SortAscending == ascending)
            {
                return;
            }
            store.SortBy(col, ascending);
            UpdateSortHeaderTitles();
            SyncModel();
        }

        // Toggles sorting on the specified column
        public void ToggleSort(DownloadSortColumn col)
        {
            store.ToggleSort(col);
            UpdateSortHeaderTitles();
            SyncModel();
        }

        // Updates sort arrows on column titles
        private void UpdateSortHeaderTitles()
        {
            var arrow = store.SortAscending ? " ▲" : " ▼";
            var nameCaption = TextResource.GetText("SORT_NAME") ?? "File name";
            var sizeCaption = TextResource.GetText("SORT_SIZE") ?? "Size";
            var progressCaption = TextResource.GetText("LBL_PROGRESS") ?? "Progress";
            colName.Title = store.CurrentSortColumn == DownloadSortColumn.Name ? nameCaption + arrow : nameCaption;
            colSize.Title = store.CurrentSortColumn == DownloadSortColumn.Size ? sizeCaption + arrow : sizeCaption;
            colProgress.Title = store.CurrentSortColumn == DownloadSortColumn.ProgressOrDate ? progressCaption + arrow : progressCaption;
        }

        // Sets up header menus with sort options
        private void WireColumnSorting()
        {
            var nameMenu = Gio.Menu.New();
            nameMenu.Append(TextResource.GetText("SORT_NAME_ASC") ?? "Sort Ascending (A-Z)", "app." + AppActions.SortNameAsc);
            nameMenu.Append(TextResource.GetText("SORT_NAME_DESC") ?? "Sort Descending (Z-A)", "app." + AppActions.SortNameDesc);
            colName.SetHeaderMenu(nameMenu);

            var sizeMenu = Gio.Menu.New();
            sizeMenu.Append(TextResource.GetText("SORT_SIZE_ASC") ?? "Sort Smallest First", "app." + AppActions.SortSizeAsc);
            sizeMenu.Append(TextResource.GetText("SORT_SIZE_DESC") ?? "Sort Largest First", "app." + AppActions.SortSizeDesc);
            colSize.SetHeaderMenu(sizeMenu);

            var progressMenu = Gio.Menu.New();
            progressMenu.Append(TextResource.GetText("SORT_DATE_DESC") ?? "Sort Newest First", "app." + AppActions.SortDateDesc);
            progressMenu.Append(TextResource.GetText("SORT_DATE_ASC") ?? "Sort Oldest First", "app." + AppActions.SortDateAsc);
            colProgress.SetHeaderMenu(progressMenu);
        }

        // Mirrors store.Items into the row model while keeping the selection by id
        private void SyncModel()
        {
            var keep = new HashSet<string>(selectedIds, StringComparer.Ordinal);
            isUpdatingSelection = true;
            try
            {
                selection.UnselectAll();
                rowStore.RemoveAll();
                foreach (var item in store.Items)
                {
                    rowStore.Append(StringObject.New(item.Id));
                }
                for (uint i = 0; i < (uint)store.Count; i++)
                {
                    if (keep.Contains(store.Items[(int)i].Id))
                    {
                        selection.SelectItem(i, false);
                    }
                }
            }
            finally
            {
                isUpdatingSelection = false;
            }

            CaptureSelection();
            UpdateGutterGlyph();
            stack.VisibleChildName = store.Count == 0 ? "empty" : "list";
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        // Applies an explicit selection set to the visible rows, leaving other
        // views' saved selections untouched
        private void ApplySelection(HashSet<string> ids)
        {
            isUpdatingSelection = true;
            try
            {
                selection.UnselectAll();
                foreach (var item in store.Items)
                {
                    selectedIds.Remove(item.Id);
                }
                for (uint i = 0; i < (uint)store.Count; i++)
                {
                    if (ids.Contains(store.Items[(int)i].Id))
                    {
                        selection.SelectItem(i, false);
                    }
                }
            }
            finally
            {
                isUpdatingSelection = false;
            }

            CaptureSelection();
            UpdateGutterGlyph();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        // Recomputes the id set from the model and notifies listeners
        private void OnSelectionChanged()
        {
            if (isUpdatingSelection)
            {
                return;
            }
            CaptureSelection();
            UpdateGutterGlyph();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        // Copies the model selection into the id set. Only ids this view owns are
        // rewritten, so switching to another view keeps the first view's selection.
        private void CaptureSelection()
        {
            foreach (var item in store.Items)
            {
                selectedIds.Remove(item.Id);
            }
            for (uint i = 0; i < (uint)store.Count; i++)
            {
                if (selection.IsSelected(i))
                {
                    selectedIds.Add(store.Items[(int)i].Id);
                }
            }
        }

        // Mirrors the selection into the gutter header glyph (all / some / none)
        private void UpdateGutterGlyph()
        {
            var selected = selectedIds.Count;
            var total = store.Count;
            colCheckbox.Title = selected <= 0
                ? GutterGlyphNone
                : (total > 0 && selected >= total ? GutterGlyphAll : GutterGlyphSome);
        }

        // Resolves the transfer id behind a list item
        private static string? IdOf(ListItem item) =>
            (item.Item as StringObject)?.GetString();

        // True once the column headers carry their click gestures
        internal bool HeadersWired => headersWired;
        private bool headersWired;

        // Gives every column header its click behaviour: the gutter toggles select-all
        // and Name/Size/Progress cycle the sort, like the GTK 3 header buttons.
        // GtkColumnViewColumn exposes no header-click signal and GtkColumnHeader is not
        // bound, so each header widget is located by the caption it renders.
        private void TryWireHeaders()
        {
            if (headersWired)
            {
                return;
            }

            var wired = 0;
            // GTK groups the column headers in one container child of the view, so the
            // individual header widgets are the container's children.
            for (var child = view.GetFirstChild(); child != null; child = child.GetNextSibling())
            {
                if (child is ListView)
                {
                    continue;
                }

                var inner = child.GetFirstChild();
                if (inner == null)
                {
                    wired += WireHeader(child) ? 1 : 0;
                    continue;
                }

                for (var header = inner; header != null; header = header.GetNextSibling())
                {
                    wired += WireHeader(header) ? 1 : 0;
                }
            }

            headersWired = wired > 0;
            if (headersWired)
            {
                TraceLog.Log.Debug($"GTK 4 list headers wired: {wired} (select-all + sorting)");
            }
        }

        // Wires one header widget by the caption (or select-all glyph) it renders
        private bool WireHeader(Widget header)
        {
            if (ContainsGutterGlyph(header))
            {
                AttachClick(header, ToggleSelectAll);
                return true;
            }

            var caption = FirstLabelText(header);
            if (caption == null)
            {
                return false;
            }

            if (caption.StartsWith(TextResource.GetText("SORT_NAME") ?? "File name", StringComparison.Ordinal))
            {
                AttachClick(header, () => ToggleSort(DownloadSortColumn.Name));
                return true;
            }
            if (caption.StartsWith(TextResource.GetText("SORT_SIZE") ?? "Size", StringComparison.Ordinal))
            {
                AttachClick(header, () => ToggleSort(DownloadSortColumn.Size));
                return true;
            }
            if (caption.StartsWith(TextResource.GetText("LBL_PROGRESS") ?? "Progress", StringComparison.Ordinal))
            {
                AttachClick(header, () => ToggleSort(DownloadSortColumn.ProgressOrDate));
                return true;
            }
            return false;
        }

        // Attaches a capture-phase left click so the header reacts before GTK's own handler
        private static void AttachClick(Widget header, Action action)
        {
            var gesture = GestureClick.New();
            gesture.SetButton(1);
            gesture.SetPropagationPhase(PropagationPhase.Capture);
            gesture.OnPressed += (_, _) => action();
            header.AddController(gesture);
        }

        // First label text found in a header subtree
        private static string? FirstLabelText(Widget widget)
        {
            if (widget is Label label)
            {
                return label.GetText();
            }

            for (var child = widget.GetFirstChild(); child != null; child = child.GetNextSibling())
            {
                var text = FirstLabelText(child);
                if (text != null)
                {
                    return text;
                }
            }
            return null;
        }

        // True when a widget subtree renders one of the tri-state select-all glyphs
        private static bool ContainsGutterGlyph(Widget widget)
        {
            if (widget is Label label)
            {
                var text = label.GetText();
                if (text == GutterGlyphNone || text == GutterGlyphSome || text == GutterGlyphAll)
                {
                    return true;
                }
            }

            for (var child = widget.GetFirstChild(); child != null; child = child.GetNextSibling())
            {
                if (ContainsGutterGlyph(child))
                {
                    return true;
                }
            }
            return false;
        }

        // Multi-select gutter checkbox column matching GTK 3
        private ColumnViewColumn BuildCheckboxColumn()
        {
            var factory = SignalListItemFactory.New();
            factory.OnSetup += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var check = CheckButton.New();
                check.Halign = Align.Center;
                check.Valign = Align.Center;
                check.MarginStart = 6;
                check.MarginEnd = 6;

                check.OnToggled += (_, _) =>
                {
                    if (isUpdatingSelection)
                    {
                        return;
                    }
                    var id = IdOf(item);
                    if (string.IsNullOrEmpty(id))
                    {
                        return;
                    }
                    var pos = item.Position;
                    if (check.Active)
                    {
                        selection.SelectItem(pos, false);
                    }
                    else
                    {
                        selection.UnselectItem(pos);
                    }
                };

                item.SetChild(check);
            };
            factory.OnBind += (_, args) =>
            {
                var item = (ListItem)args.Object;
                if (item.Child is CheckButton check)
                {
                    isUpdatingSelection = true;
                    check.Active = selection.IsSelected(item.Position);
                    isUpdatingSelection = false;
                }
            };

            var column = ColumnViewColumn.New("", factory);
            column.FixedWidth = CheckboxColumnWidth;
            column.Resizable = false;
            return column;
        }

        // File type icon + Name + dim status, with category color tinting
        private ColumnViewColumn BuildNameColumn()
        {
            var factory = SignalListItemFactory.New();
            factory.OnSetup += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var rowBox = Box.New(Orientation.Horizontal, 10);
                rowBox.MarginTop = 6;
                rowBox.MarginBottom = 6;
                rowBox.MarginStart = 8;
                rowBox.MarginEnd = 10;

                var icon = Gtk4IconHelper.CreateIcon("file-download-line", 24);
                icon.Valign = Align.Center;

                var textBox = Box.New(Orientation.Vertical, 2);
                textBox.Hexpand = true;
                textBox.Valign = Align.Center;

                var name = Label.New("");
                name.Halign = Align.Start;
                name.Ellipsize = Pango.EllipsizeMode.End;

                var status = Label.New("");
                status.Halign = Align.Start;
                // Meta lines carry long paths: ellipsizing keeps the column's minimum
                // width small so Size/Progress are never pushed out of the window
                status.Ellipsize = Pango.EllipsizeMode.Middle;
                status.Hexpand = true;
                status.AddCssClass("dim-label");

                textBox.Append(name);
                textBox.Append(status);

                rowBox.Append(icon);
                rowBox.Append(textBox);
                item.SetChild(rowBox);
                AttachContextMenu(item, rowBox);
            };
            factory.OnBind += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var id = IdOf(item);
                if (item.Child is not Box rowBox || string.IsNullOrEmpty(id) || store.Lookup(id) is not { } row)
                {
                    return;
                }

                if (rowBox.GetFirstChild() is Image icon)
                {
                    var (r, g, b) = GetCategoryColorForFileType(row.Name);
                    var svgName = IconResource.GetSVGNameForFileType(row.Name);
                    Gtk4IconHelper.UpdateTintedIcon(icon, svgName, r, g, b, 24);
                }

                if (rowBox.GetLastChild() is Box textBox)
                {
                    if (textBox.GetFirstChild() is Label name)
                    {
                        name.SetText(row.Name);
                    }
                    if (textBox.GetLastChild() is Label status)
                    {
                        status.SetText(RowSubLine(row));
                    }
                }

                // Register the widgets so a progress tick can refresh them without a rebind
                if (rowBox.GetFirstChild() is Image boundIcon &&
                    rowBox.GetLastChild() is Box boundBox &&
                    boundBox.GetFirstChild() is Label boundName &&
                    boundBox.GetLastChild() is Label boundStatus)
                {
                    nameCells[id] = new NameCells(boundIcon, boundName, boundStatus);
                }
            };
            factory.OnUnbind += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var id = IdOf(item);
                if (!string.IsNullOrEmpty(id))
                {
                    nameCells.Remove(id);
                }
            };

            var column = ColumnViewColumn.New(TextResource.GetText("SORT_NAME") ?? "File name", factory);
            column.Expand = true;
            column.Resizable = true;
            return column;
        }

        // Right-aligned size text, resizable
        private ColumnViewColumn BuildSizeColumn()
        {
            var factory = SignalListItemFactory.New();
            factory.OnSetup += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var label = Label.New("");
                label.Halign = Align.End;
                label.Valign = Align.Center;
                label.MarginStart = 8;
                label.MarginEnd = 12;
                item.SetChild(label);
                AttachContextMenu(item, label);
            };
            factory.OnBind += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var id = IdOf(item);
                if (item.Child is Label label && !string.IsNullOrEmpty(id) && store.Lookup(id) is { } row)
                {
                    label.SetText(row.SizeText);
                    sizeCells[id] = label;
                }
            };
            factory.OnUnbind += (_, args) =>
            {
                var id = IdOf((ListItem)args.Object);
                if (!string.IsNullOrEmpty(id))
                {
                    sizeCells.Remove(id);
                }
            };

            var column = ColumnViewColumn.New(TextResource.GetText("SORT_SIZE") ?? "Size", factory);
            column.FixedWidth = SizeColumnWidth;
            column.Resizable = true;
            return column;
        }

        // Progress column: clean completion date badge for finished; bar + speed/ETA for in-progress
        private ColumnViewColumn BuildProgressColumn()
        {
            var factory = SignalListItemFactory.New();
            factory.OnSetup += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var rootBox = Box.New(Orientation.Vertical, 2);
                rootBox.Valign = Align.Center;
                rootBox.MarginStart = 8;
                rootBox.MarginEnd = 12;

                // Completed state container: green check icon + date/completion label
                var finishedBox = Box.New(Orientation.Horizontal, 6);
                finishedBox.Valign = Align.Center;
                var checkIcon = Gtk4IconHelper.CreateTintedIcon("check-line", 46, 194, 126, 16);
                var finishedLabel = Label.New(TextResource.GetText("STAT_FINISHED") ?? "Complete");
                finishedLabel.AddCssClass("dim-label");
                finishedBox.Append(checkIcon);
                finishedBox.Append(finishedLabel);

                // In-progress state container: progress bar with percentage, speed/ETA
                var inprogressBox = Box.New(Orientation.Vertical, 2);
                inprogressBox.Valign = Align.Center;

                var barRow = Box.New(Orientation.Horizontal, 6);
                var bar = ProgressBar.New();
                bar.Hexpand = true;
                bar.Valign = Align.Center;

                var percentLabel = Label.New("0%");
                percentLabel.WidthRequest = 44;
                percentLabel.Halign = Align.End;

                barRow.Append(bar);
                barRow.Append(percentLabel);

                var speedEtaLabel = Label.New("");
                speedEtaLabel.Halign = Align.Start;
                speedEtaLabel.AddCssClass("dim-label");

                inprogressBox.Append(barRow);
                inprogressBox.Append(speedEtaLabel);

                rootBox.Append(finishedBox);
                rootBox.Append(inprogressBox);
                item.SetChild(rootBox);
                AttachContextMenu(item, rootBox);
            };
            factory.OnBind += (_, args) =>
            {
                var item = (ListItem)args.Object;
                var id = IdOf(item);
                if (item.Child is not Box rootBox || string.IsNullOrEmpty(id) || store.Lookup(id) is not { } row)
                {
                    return;
                }

                if (rootBox.GetFirstChild() is not Box finishedBox || rootBox.GetLastChild() is not Box inprogressBox)
                {
                    return;
                }

                if (inprogressBox.GetFirstChild() is not Box barRow ||
                    barRow.GetFirstChild() is not ProgressBar bar ||
                    barRow.GetLastChild() is not Label percent ||
                    inprogressBox.GetLastChild() is not Label speedEta)
                {
                    return;
                }

                var cells = new ProgressCells(finishedBox, inprogressBox, bar, percent, speedEta);
                progressCells[id] = cells;
                BindProgressCells(cells, row);
            };
            factory.OnUnbind += (_, args) =>
            {
                var id = IdOf((ListItem)args.Object);
                if (!string.IsNullOrEmpty(id))
                {
                    progressCells.Remove(id);
                }
            };

            var column = ColumnViewColumn.New(TextResource.GetText("LBL_PROGRESS") ?? "Progress", factory);
            column.FixedWidth = ProgressColumnWidth;
            column.Resizable = true;
            return column;
        }

        // Renders one row's progress column state (finished badge or live bar)
        private static void BindProgressCells(ProgressCells cells, DownloadItem row)
        {
            cells.FinishedBox.Visible = row.IsComplete;
            cells.InProgressBox.Visible = !row.IsComplete;

            if (row.IsComplete)
            {
                if (cells.FinishedBox.GetLastChild() is Label finishedLabel)
                {
                    finishedLabel.SetText(row.DateAdded != default
                        ? row.DateAdded.ToString("yyyy-MM-dd")
                        : TextResource.GetText("STAT_FINISHED") ?? "Complete");
                }
                return;
            }

            cells.Bar.Fraction = Math.Clamp(row.Progress, 0.0, 1.0);
            cells.Percent.SetText($"{row.RawProgress}%");
            cells.SpeedEta.SetText(!string.IsNullOrWhiteSpace(row.DownloadSpeed)
                ? $"{row.DownloadSpeed} · {row.ETA}"
                : row.Status);
        }

        // Secondary line: the source/folder meta when known, else the transfer status
        private static string RowSubLine(DownloadItem row) =>
            string.IsNullOrEmpty(row.Meta) ? row.Status : row.Meta;

        // Resolves vibrant category colors matching GTK 3 CategoryIconPalette
        private static (byte R, byte G, byte B) GetCategoryColorForFileType(string filename)
        {
            var ext = System.IO.Path.GetExtension(filename)?.ToLowerInvariant() ?? string.Empty;
            var fileType = IconResource.GetFileType(ext);
            return fileType switch
            {
                "Video" => (239, 68, 68),      // Coral Red
                "Music" => (168, 85, 247),     // Purple / Violet
                "Document" => (245, 158, 11),  // Amber / Orange
                "Compressed" => (20, 184, 166),// Teal / Cyan
                "Application" or "ApplicationContext.Core" => (99, 102, 241), // Indigo
                "Image" => (244, 63, 94),      // Rose / Pink
                _ => (56, 189, 248)            // Sky Blue
            };
        }

        // Empty state shown when there are no rows (or the search matched nothing)
        private static Widget BuildEmptyState()
        {
            // AdwStatusPage is the GNOME empty-state pattern (icon + title + hint)
            var page = Adw.StatusPage.New();
            page.SetIconName("folder-download-symbolic");
            page.SetTitle(TextResource.GetText("LBL_NO_DOWNLOADS") ?? "No downloads to show");
            page.SetDescription(TextResource.GetText("DESC_NO_DOWNLOADS") ?? "Downloads you start will appear here.");
            page.Vexpand = true;
            return page;
        }

        private PopoverMenu? activeContextMenu;

        // Attaches right-click context gesture to a cell's widget
        private void AttachContextMenu(ListItem item, Widget child)
        {
            var gesture = GestureClick.New();
            gesture.SetButton(3);
            gesture.OnPressed += (_, clickArgs) =>
            {
                var id = IdOf(item);
                if (string.IsNullOrEmpty(id) || store.Lookup(id) is not { } row)
                {
                    return;
                }

                var pos = item.Position;
                if (pos != uint.MaxValue && !selection.IsSelected(pos))
                {
                    selection.SelectItem(pos, true);
                }
                ShowContextMenu(row, clickArgs.X, clickArgs.Y, child);
            };
            child.AddController(gesture);
        }

        // Displays context popover menu for the clicked download row
        private void ShowContextMenu(DownloadItem item, double x, double y, Widget anchor)
        {
            if (activeContextMenu != null)
            {
                activeContextMenu.Popdown();
                activeContextMenu.Unparent();
                activeContextMenu = null;
            }

            var menu = Gio.Menu.New();
            if (!item.IsComplete)
            {
                menu.Append(TextResource.GetText("MENU_RESUME") ?? "Resume", "app." + AppActions.ResumeDownload);
                menu.Append(TextResource.GetText("MENU_PAUSE") ?? "Pause", "app." + AppActions.PauseDownload);
                menu.Append(TextResource.GetText("LBL_SHOW_PROGRESS") ?? "Show Progress", "app." + AppActions.ShowProgress);
                menu.Append(TextResource.GetText("CTX_SAVE_AS") ?? "Save As...", "app." + AppActions.SaveAs);
                menu.Append(TextResource.GetText("MENU_REFRESH_LINK") ?? "Refresh Download Link...", "app." + AppActions.RefreshLink);
                menu.Append(TextResource.GetText("Q_MOVE_TO") ?? "Move to Queue...", "app." + AppActions.MoveToQueue);
                menu.Append(TextResource.GetText("CTX_COPY_URL") ?? "Copy Download Link", "app." + AppActions.CopyUrl);
                menu.Append(TextResource.GetText("MENU_PROPERTIES") ?? "Properties...", "app." + AppActions.DownloadProperties);
                menu.Append(TextResource.GetText("DESC_DEL") ?? "Delete", "app." + AppActions.DeleteDownload);
            }
            else
            {
                menu.Append(TextResource.GetText("CTX_OPEN_FILE") ?? "Open File", "app." + AppActions.OpenFile);
                menu.Append(TextResource.GetText("CTX_OPEN_FOLDER") ?? "Open Folder", "app." + AppActions.OpenFolder);
                menu.Append(TextResource.GetText("CTX_CHECKSUM") ?? "Verify Checksum...", "app." + AppActions.VerifyChecksum);
                menu.Append(TextResource.GetText("CTX_COPY_URL") ?? "Copy Download Link", "app." + AppActions.CopyUrl);
                menu.Append(TextResource.GetText("CTX_COPY_FILE") ?? "Copy File", "app." + AppActions.CopyFile);
                menu.Append(TextResource.GetText("MENU_RESTART") ?? "Download Again", "app." + AppActions.RestartDownload);
                menu.Append(TextResource.GetText("MENU_PROPERTIES") ?? "Properties...", "app." + AppActions.DownloadProperties);
                menu.Append(TextResource.GetText("MENU_DELETE_DWN") ?? "Remove from List", "app." + AppActions.DeleteDownload);
            }

            var popover = PopoverMenu.NewFromModel(menu);
            popover.SetParent(anchor);
            popover.SetHasArrow(false);
            var rect = new Gdk.Rectangle { X = (int)x, Y = (int)y, Width = 1, Height = 1 };
            popover.PointingTo = rect;
            popover.OnClosed += (_, _) =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    if (activeContextMenu == popover)
                    {
                        popover.Unparent();
                        activeContextMenu = null;
                    }
                    return false;
                });
            };
            activeContextMenu = popover;
            popover.Popup();
        }

        // Widgets behind one bound name cell, refreshed on a progress tick
        private sealed class NameCells
        {
            public Image Icon { get; }
            public Label Name { get; }
            public Label Status { get; }

            public NameCells(Image icon, Label name, Label status)
            {
                Icon = icon;
                Name = name;
                Status = status;
            }
        }

        // Widgets behind one bound progress cell, refreshed on a progress tick
        private sealed class ProgressCells
        {
            public Box FinishedBox { get; }
            public Box InProgressBox { get; }
            public ProgressBar Bar { get; }
            public Label Percent { get; }
            public Label SpeedEta { get; }

            public ProgressCells(Box finishedBox, Box inProgressBox, ProgressBar bar, Label percent, Label speedEta)
            {
                FinishedBox = finishedBox;
                InProgressBox = inProgressBox;
                Bar = bar;
                Percent = percent;
                SpeedEta = speedEta;
            }
        }
    }
}
