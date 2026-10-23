// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 download list. A header-less Gtk.ListView: each row is one card carrying the
// select checkbox, file-type icon, name + source + destination, size and progress.
// There is no column header and no column resizing (the bottom bar owns Select All
// and Sort), so the list stays calm and native.
//
// Selection: the Gio.ListStore and the MultiSelection are created once; only their
// contents change on a view/search/sort switch. Progress updates never touch the
// model, they refresh the already-bound row widgets, so a running download cannot
// clear the selection.
using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Downloads
{
    internal sealed class DownloadListView
    {
        // Fixed geometry tokens
        private const int CheckboxWidth = 34;
        private const int SizeWidth = 90;
        private const int ProgressWidth = 160;

        // Row data behind the model
        private readonly DownloadStore store = new();
        // Row identity model: one Gtk.StringObject per row holding the transfer id
        private readonly Gio.ListStore rowStore = Gio.ListStore.New(StringObject.GetGType());
        // The list itself
        private readonly ListView view;
        // Selection model is created once and reused for every view/search/sort change
        private MultiSelection selection;
        // Swaps between the empty-state page and the list page
        private readonly Stack stack;
        // Empty page and its two possible actions (add download / clear search)
        private Adw.StatusPage? emptyPage;
        private Button? addAction;
        private Button? clearAction;
        // Active search keyword, so the empty page can explain a zero-result filter
        private string keyword = "";

        // Authoritative selection, kept as transfer ids so it survives reloads
        private readonly HashSet<string> selectedIds = new(StringComparer.Ordinal);

        // Currently bound cell widgets, so a progress tick can refresh them in place
        private readonly Dictionary<string, RowCells> boundCells = new(StringComparer.Ordinal);

        // Root widget to pack into the window
        public Widget Root => stack;

        // Current row count (filtered)
        public int Count => store.Count;
        // Current filtered items backing the visible rows
        public IReadOnlyList<DownloadItem> Items => store.Items;
        // Fires after the GTK selection changes
        public event EventHandler? SelectionChanged;
        // Fires when the sort column or direction changes
        public event EventHandler? SortChanged;
        // Fires when the empty state's primary action is pressed
        public event EventHandler? AddDownloadRequested;
        // Fires when the empty state's "clear search" action is pressed
        public event EventHandler? ClearSearchRequested;

        // Short caption for the bottom-bar sort button
        public string SortCaption
        {
            get
            {
                var arrow = store.SortAscending ? "↑" : "↓";
                return store.CurrentSortColumn switch
                {
                    DownloadSortColumn.Name => (TextResource.GetText("SORT_NAME") ?? "Name") + " " + arrow,
                    DownloadSortColumn.Size => (TextResource.GetText("SORT_SIZE") ?? "Size") + " " + arrow,
                    _ => (TextResource.GetText("SORT_DATE") ?? "Date") + " " + arrow
                };
            }
        }

        // Builds the list widget and its empty state
        public DownloadListView()
        {
            selection = MultiSelection.New(rowStore);
            selection.OnSelectionChanged += (_, _) => OnSelectionChanged();

            var factory = SignalListItemFactory.New();
            factory.OnSetup += (_, args) => SetupRow((ListItem)args.Object);
            factory.OnBind += (_, args) => BindRow((ListItem)args.Object);
            factory.OnUnbind += (_, args) => UnbindRow((ListItem)args.Object);

            view = ListView.New(selection, factory);
            view.AddCssClass("download-list");
            view.ShowSeparators = false;
            // Leave a strip so the overlay scrollbar never sits on a card
            view.MarginEnd = 8;
            view.Hexpand = true;
            view.Vexpand = true;

            // Double click / Enter activates the standard action (open file/folder/progress)
            view.OnActivate += (_, args) =>
            {
                var pos = args.Position;
                if (pos != uint.MaxValue && pos < (uint)store.Count)
                {
                    selection.SelectItem(pos, true);
                    UIActions.OnDblClick();
                }
            };

            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            scrolled.SetChild(view);
            scrolled.Hexpand = true;
            scrolled.Vexpand = true;

            stack = Stack.New();
            stack.AddNamed(scrolled, "list");
            stack.AddNamed(BuildEmptyState(), "empty");
            stack.VisibleChildName = "empty";
            UpdateEmptyState();
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
            this.keyword = keyword ?? "";
            store.Filter(this.keyword);
            SyncModel();
        }

        // Refreshes one row's visible cells in place (never touches selection or model)
        public void UpdateRow(string id)
        {
            var item = store.Lookup(id);
            if (!boundCells.TryGetValue(id, out var cells) || item == null)
            {
                return;
            }

            cells.Name.SetText(item.Name);
            var meta = CombineMeta(item);
            cells.Meta.SetText(meta);
            cells.Meta.SetVisible(!string.IsNullOrEmpty(meta));
            cells.Size.SetText(item.SizeText);
            BindProgress(cells, item);
        }

        // Joins the source site and destination folder into one quiet meta line
        private static string CombineMeta(DownloadItem item)
        {
            var parts = new List<string>(2);
            if (!string.IsNullOrEmpty(item.Source))
            {
                parts.Add(item.Source);
            }
            if (!string.IsNullOrEmpty(item.Location))
            {
                parts.Add(item.Location);
            }
            return string.Join("   ·   ", parts);
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

            if (selectedIds.Count >= store.Count)
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
            SortChanged?.Invoke(this, EventArgs.Empty);
            SyncModel();
        }

        // Toggles sorting on the specified column
        public void ToggleSort(DownloadSortColumn col)
        {
            store.ToggleSort(col);
            SortChanged?.Invoke(this, EventArgs.Empty);
            SyncModel();
        }

        // Mirrors store.Items into the row model while keeping the selection by id
        private void SyncModel()
        {
            var keep = new HashSet<string>(selectedIds, StringComparer.Ordinal);
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

            CaptureSelection();
            stack.VisibleChildName = store.Count == 0 ? "empty" : "list";
            UpdateEmptyState();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        // Applies an explicit selection set to the visible rows, leaving other
        // views' saved selections untouched
        private void ApplySelection(HashSet<string> ids)
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

            CaptureSelection();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        // Recomputes the id set from the model and notifies listeners
        private void OnSelectionChanged()
        {
            CaptureSelection();
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

        // Resolves the transfer id behind a list item
        private static string? IdOf(ListItem item) =>
            (item.Item as StringObject)?.GetString();

        // Builds the widget tree for one row
        private void SetupRow(ListItem item)
        {
            var rowBox = Box.New(Orientation.Horizontal, 12);
            rowBox.AddCssClass("download-row");
            rowBox.MarginTop = 6;
            rowBox.MarginBottom = 6;
            rowBox.MarginStart = 8;
            rowBox.MarginEnd = 8;

            // File-type icon
            var icon = Gtk4IconHelper.CreateIcon("file-download-line", 28);
            icon.Valign = Align.Center;
            rowBox.Append(icon);

            // Name + one quiet meta line (source site, file type, destination)
            var textBox = Box.New(Orientation.Vertical, 3);
            textBox.Hexpand = true;
            textBox.Valign = Align.Center;

            var name = Label.New("");
            name.Halign = Align.Start;
            name.Ellipsize = Pango.EllipsizeMode.End;
            name.AddCssClass("download-name");

            var meta = Label.New("");
            meta.Halign = Align.Start;
            meta.Ellipsize = Pango.EllipsizeMode.Middle;
            meta.AddCssClass("download-meta");

            textBox.Append(name);
            textBox.Append(meta);
            rowBox.Append(textBox);

            // State pill: Downloading / Stopped / Waiting (hidden once complete)
            var statusPill = Label.New("");
            statusPill.AddCssClass("status-pill");
            statusPill.Valign = Align.Center;
            statusPill.Halign = Align.Center;
            statusPill.Visible = false;
            rowBox.Append(statusPill);

            // Size
            var size = Label.New("");
            size.WidthRequest = SizeWidth;
            size.Halign = Align.End;
            size.Valign = Align.Center;
            size.AddCssClass("download-size");
            rowBox.Append(size);

            // Progress: finished badge or live bar
            var finishedBox = Box.New(Orientation.Horizontal, 6);
            finishedBox.WidthRequest = ProgressWidth;
            finishedBox.Valign = Align.Center;
            finishedBox.Append(Gtk4IconHelper.CreateTintedIcon("check-line", 46, 194, 126, 16));
            var finishedLabel = Label.New(TextResource.GetText("STAT_FINISHED") ?? "Complete");
            finishedLabel.AddCssClass("download-meta");
            finishedBox.Append(finishedLabel);

            var bar = ProgressBar.New();
            bar.Valign = Align.Center;
            bar.AddCssClass("download-progress");

            var percent = Label.New("");
            percent.WidthRequest = 42;
            percent.Halign = Align.End;
            percent.AddCssClass("download-meta");

            var speedEta = Label.New("");
            speedEta.Halign = Align.Start;
            speedEta.Ellipsize = Pango.EllipsizeMode.End;
            speedEta.AddCssClass("download-meta");

            var inProgressBox = Box.New(Orientation.Vertical, 1);
            inProgressBox.WidthRequest = ProgressWidth;
            inProgressBox.Valign = Align.Center;

            var barRow = Box.New(Orientation.Horizontal, 6);
            bar.Hexpand = true;
            barRow.Append(bar);
            barRow.Append(percent);
            inProgressBox.Append(barRow);
            inProgressBox.Append(speedEta);

            var progressHost = Box.New(Orientation.Vertical, 0);
            progressHost.WidthRequest = ProgressWidth;
            progressHost.Valign = Align.Center;
            progressHost.Append(finishedBox);
            progressHost.Append(inProgressBox);
            rowBox.Append(progressHost);

            item.SetChild(rowBox);
            AttachContextMenu(item, rowBox);
        }

        // Fills a row's widgets from the store and registers them for in-place updates
        private void BindRow(ListItem item)
        {
            var id = IdOf(item);
            if (item.Child is not Box rowBox || string.IsNullOrEmpty(id) || store.Lookup(id) is not { } row)
            {
                return;
            }

            var icon = rowBox.GetFirstChild() as Image;
            var textBox = GetChildAt(rowBox, 1) as Box;
            var statusPill = GetChildAt(rowBox, 2) as Label;
            var size = GetChildAt(rowBox, 3) as Label;
            var progressHost = GetChildAt(rowBox, 4) as Box;
            if (icon == null || textBox == null || statusPill == null || size == null || progressHost == null)
            {
                return;
            }

            var (r, g, b) = GetCategoryColorForFileType(row.Name);
            if (Gtk4Theme.IsDarkActive)
            {
                (r, g, b) = Gtk4IconHelper.BrightenForDark(r, g, b);
            }
            Gtk4IconHelper.UpdateTintedIcon(icon, IconResource.GetSVGNameForFileType(row.Name), r, g, b, 28);

            if (textBox.GetFirstChild() is Label name &&
                GetChildAt(textBox, 1) is Label meta)
            {
                name.SetText(row.Name);
                var metaText = CombineMeta(row);
                meta.SetText(metaText);
                meta.SetVisible(!string.IsNullOrEmpty(metaText));

                size.SetText(row.SizeText);

                var cells = new RowCells(name, meta, statusPill, size,
                    progressHost.GetFirstChild() as Box ?? Box.New(Orientation.Vertical, 0),
                    progressHost.GetLastChild() as Box ?? Box.New(Orientation.Vertical, 0));
                if (cells.FinishedBox.GetLastChild() is Label finishedLabel &&
                    cells.InProgressBox.GetFirstChild() is Box barRow &&
                    barRow.GetFirstChild() is ProgressBar bar &&
                    barRow.GetLastChild() is Label percent &&
                    cells.InProgressBox.GetLastChild() is Label speedEta)
                {
                    cells.Bar = bar;
                    cells.Percent = percent;
                    cells.SpeedEta = speedEta;
                    boundCells[id] = cells;
                    BindProgress(cells, row);
                }
            }
        }

        // Drops the row binding when the row scrolls out of view
        private void UnbindRow(ListItem item)
        {
            var id = IdOf(item);
            if (!string.IsNullOrEmpty(id))
            {
                boundCells.Remove(id);
            }
        }

        // Renders the state pill and the progress area for finished or active rows
        private static void BindProgress(RowCells cells, DownloadItem row)
        {
            cells.FinishedBox.Visible = row.IsComplete;
            cells.InProgressBox.Visible = !row.IsComplete;
            BindStatusPill(cells.Status, row);

            if (row.IsComplete)
            {
                if (cells.FinishedBox.GetLastChild() is Label label)
                {
                    label.SetText(row.DateAdded != default
                        ? row.DateAdded.ToString("yyyy-MM-dd")
                        : TextResource.GetText("STAT_FINISHED") ?? "Complete");
                }
                return;
            }

            if (cells.Bar != null)
            {
                cells.Bar.Fraction = Math.Clamp(row.Progress, 0.0, 1.0);
            }
            cells.Percent?.SetText($"{row.RawProgress}%");
            // The pill already names the state, so this line carries live numbers only
            cells.SpeedEta?.SetText(!string.IsNullOrWhiteSpace(row.DownloadSpeed)
                ? $"{row.DownloadSpeed} · {row.ETA}"
                : row.ETA);
        }

        // Maps the transfer state to one pill caption and tone
        private static void BindStatusPill(Label pill, DownloadItem row)
        {
            foreach (var cssTone in new[] { "downloading", "stopped", "waiting", "failed" })
            {
                pill.RemoveCssClass(cssTone);
            }

            if (row.IsComplete)
            {
                pill.Visible = false;
                return;
            }

            var status = row.Status ?? "";
            string text;
            string tone;
            if (status.Contains("Fail", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("Error", StringComparison.OrdinalIgnoreCase))
            {
                text = TextResource.GetText("STAT_FAILED") ?? "Failed";
                tone = "failed";
            }
            else if (status.Contains("Stop", StringComparison.OrdinalIgnoreCase) ||
                     status.Contains("Paus", StringComparison.OrdinalIgnoreCase))
            {
                text = TextResource.GetText("STAT_PAUSED") ?? "Paused";
                tone = "stopped";
            }
            else if (!string.IsNullOrWhiteSpace(row.DownloadSpeed) ||
                     status.Contains("Download", StringComparison.OrdinalIgnoreCase))
            {
                text = TextResource.GetText("STAT_DOWNLOADING") ?? "Downloading";
                tone = "downloading";
            }
            else
            {
                text = TextResource.GetText("STAT_QUEUED") ?? "Queued";
                tone = "waiting";
            }

            pill.SetText(text);
            pill.AddCssClass(tone);
            pill.Visible = true;
        }

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

        // Empty state: a starting point when the view is empty, an explanation when
        // a search simply matched nothing
        private Widget BuildEmptyState()
        {
            emptyPage = Adw.StatusPage.New();
            emptyPage.SetIconName("folder-download-symbolic");
            emptyPage.Vexpand = true;

            // Primary call to action so the empty view is a starting point, not a dead end
            addAction = Button.NewWithLabel(TextResource.GetText("LBL_ADD_DOWNLOAD") ?? "Add download");
            addAction.AddCssClass("suggested-action");
            addAction.AddCssClass("pill");
            addAction.Halign = Align.Center;
            addAction.OnClicked += (_, _) => AddDownloadRequested?.Invoke(this, EventArgs.Empty);

            clearAction = Button.NewWithLabel(TextResource.GetText("LBL_CLEAR_SEARCH") ?? "Clear search");
            clearAction.AddCssClass("pill");
            clearAction.Halign = Align.Center;
            clearAction.OnClicked += (_, _) =>
            {
                Filter("");
                ClearSearchRequested?.Invoke(this, EventArgs.Empty);
            };

            emptyPage.SetChild(addAction);
            return emptyPage;
        }

        // Explains a zero-result search instead of claiming the list is empty
        private void UpdateEmptyState()
        {
            if (emptyPage == null)
            {
                return;
            }

            var searching = !string.IsNullOrWhiteSpace(keyword);
            emptyPage.SetIconName(searching ? "system-search-symbolic" : "folder-download-symbolic");
            emptyPage.SetTitle(searching
                ? string.Format(TextResource.GetText("LBL_NO_RESULTS") ?? "No results for \u201c{0}\u201d", keyword)
                : TextResource.GetText("LBL_NO_DOWNLOADS") ?? "No downloads to show");
            emptyPage.SetDescription(searching
                ? TextResource.GetText("LBL_NO_RESULTS_DESC") ?? "Try a different search term."
                : TextResource.GetText("DESC_NO_DOWNLOADS") ?? "Downloads you start will appear here.");
            emptyPage.SetChild(searching ? clearAction! : addAction!);
        }

        private PopoverMenu? activeContextMenu;

        // Attaches right-click context gesture to a row
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

        // Nth child of a box
        private static Widget? GetChildAt(Box box, int index)
        {
            var child = box.GetFirstChild();
            for (var i = 0; i < index && child != null; i++)
            {
                child = child.GetNextSibling();
            }
            return child;
        }

        // Widgets behind one bound row, refreshed on a progress tick
        private sealed class RowCells
        {
            public Label Name { get; }
            public Label Meta { get; }
            public Label Status { get; }
            public Label Size { get; }
            public Box FinishedBox { get; }
            public Box InProgressBox { get; }
            public ProgressBar? Bar { get; set; }
            public Label? Percent { get; set; }
            public Label? SpeedEta { get; set; }

            public RowCells(Label name, Label meta, Label status, Label size,
                Box finishedBox, Box inProgressBox)
            {
                Name = name;
                Meta = meta;
                Status = status;
                Size = size;
                FinishedBox = finishedBox;
                InProgressBox = inProgressBox;
            }
        }
    }
}
