// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 download list: ColumnView with multi-select gutter checkbox, category-tinted
// file type icon + name, size, and dynamic progress (date badge for finished rows,
// live speed/ETA and progress bar for active rows).
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

        // Row data + its StringList projection
        private readonly DownloadStore store = new();
        // The column view itself
        private readonly ColumnView view;
        // Selection model is retained for IApplicationWindow row queries
        private MultiSelection selection;
        // Swaps between the empty-state page and the list page
        private readonly Stack stack;

        // Individual columns
        private readonly ColumnViewColumn colCheckbox;
        private readonly ColumnViewColumn colName;
        private readonly ColumnViewColumn colSize;
        private readonly ColumnViewColumn colProgress;

        // Guard against recursive selection toggles
        private bool isUpdatingSelection;

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

            // Double click / Enter key activation: executes standard double-click action (open file/folder/progress)
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

        // Selects all rows
        public void SelectAll() => selection.SelectAll();

        // Clears current row selection
        public void ClearSelection() => selection.UnselectAll();

        // Toggles select-all: unselects if all selected, otherwise selects all
        public void ToggleSelectAll()
        {
            if (store.Count == 0) return;
            var selectedCount = GetSelectedItems().Count;
            if (selectedCount == store.Count)
            {
                selection.UnselectAll();
            }
            else
            {
                selection.SelectAll();
            }
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
            Bind();
        }

        // Toggles sorting on the specified column
        public void ToggleSort(DownloadSortColumn col)
        {
            store.ToggleSort(col);
            UpdateSortHeaderTitles();
            Bind();
        }

        // Updates sort arrows on column titles
        private void UpdateSortHeaderTitles()
        {
            var arrow = store.SortAscending ? " ▲" : " ▼";
            colName.Title = store.CurrentSortColumn == DownloadSortColumn.Name ? "File name" + arrow : "File name";
            colSize.Title = store.CurrentSortColumn == DownloadSortColumn.Size ? "Size" + arrow : "Size";
            colProgress.Title = store.CurrentSortColumn == DownloadSortColumn.ProgressOrDate ? "Progress" + arrow : "Progress";
        }

        // Sets up header menus with sort options
        private void WireColumnSorting()
        {
            var nameMenu = Gio.Menu.New();
            nameMenu.Append("Sort Ascending (A-Z)", "app." + AppActions.SortNameAsc);
            nameMenu.Append("Sort Descending (Z-A)", "app." + AppActions.SortNameDesc);
            colName.SetHeaderMenu(nameMenu);

            var sizeMenu = Gio.Menu.New();
            sizeMenu.Append("Sort Smallest First", "app." + AppActions.SortSizeAsc);
            sizeMenu.Append("Sort Largest First", "app." + AppActions.SortSizeDesc);
            colSize.SetHeaderMenu(sizeMenu);

            var progressMenu = Gio.Menu.New();
            progressMenu.Append("Sort Newest First", "app." + AppActions.SortDateDesc);
            progressMenu.Append("Sort Oldest First", "app." + AppActions.SortDateAsc);
            colProgress.SetHeaderMenu(progressMenu);
        }

        // Points the view at the store's current model and updates the empty state
        private void Bind()
        {
            selection = MultiSelection.New(store.Model);
            selection.OnSelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
            view.SetModel(selection);
            stack.VisibleChildName = store.Count == 0 ? "empty" : "list";
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
                check.AddCssClass("selection-checkbox");

                check.OnToggled += (_, _) =>
                {
                    if (isUpdatingSelection) return;
                    var pos = item.Position;
                    if (pos != uint.MaxValue && pos < (uint)store.Count)
                    {
                        if (check.Active)
                        {
                            selection.SelectItem(pos, false);
                        }
                        else
                        {
                            selection.UnselectItem(pos);
                        }
                    }
                };

                item.SetChild(check);
            };
            factory.OnBind += (_, args) =>
            {
                var item = (ListItem)args.Object;
                if (item.Child is CheckButton check)
                {
                    var pos = item.Position;
                    if (pos != uint.MaxValue && pos < (uint)store.Count)
                    {
                        isUpdatingSelection = true;
                        check.Active = selection.IsSelected(pos);
                        isUpdatingSelection = false;
                    }
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
                name.AddCssClass("download-name");

                var status = Label.New("");
                status.Halign = Align.Start;
                status.AddCssClass("dim-label");
                status.AddCssClass("download-status");

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
                if (item.Child is not Box rowBox || !TryGetItem(item, out var row))
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
                        status.SetText(row.Status);
                    }
                }
            };

            var column = ColumnViewColumn.New("File name", factory);
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
                label.AddCssClass("download-size");
                item.SetChild(label);
                AttachContextMenu(item, label);
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
                var finishedLabel = Label.New("Complete");
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
                bar.AddCssClass("download-progress");

                var percentLabel = Label.New("0%");
                percentLabel.WidthRequest = 44;
                percentLabel.Halign = Align.End;
                percentLabel.AddCssClass("download-percent");

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
                if (item.Child is not Box rootBox || !TryGetItem(item, out var row))
                {
                    return;
                }

                var finishedBox = rootBox.GetFirstChild() as Box;
                var inprogressBox = rootBox.GetLastChild() as Box;

                if (row.IsComplete)
                {
                    if (finishedBox != null)
                    {
                        finishedBox.Visible = true;
                        if (finishedBox.GetLastChild() is Label finishedLabel)
                        {
                            var dateStr = row.DateAdded != default ? row.DateAdded.ToString("yyyy-MM-dd") : "Complete";
                            finishedLabel.SetText(dateStr);
                        }
                    }
                    if (inprogressBox != null)
                    {
                        inprogressBox.Visible = false;
                    }
                }
                else
                {
                    if (finishedBox != null)
                    {
                        finishedBox.Visible = false;
                    }
                    if (inprogressBox != null)
                    {
                        inprogressBox.Visible = true;
                        if (inprogressBox.GetFirstChild() is Box barRow)
                        {
                            if (barRow.GetFirstChild() is ProgressBar bar)
                            {
                                bar.Fraction = Math.Clamp(row.Progress, 0.0, 1.0);
                            }
                            if (barRow.GetLastChild() is Label percentLabel)
                            {
                                percentLabel.SetText($"{row.RawProgress}%");
                            }
                        }
                        if (inprogressBox.GetLastChild() is Label speedEtaLabel)
                        {
                            var info = !string.IsNullOrWhiteSpace(row.DownloadSpeed)
                                ? $"{row.DownloadSpeed} · {row.ETA}"
                                : row.Status;
                            speedEtaLabel.SetText(info);
                        }
                    }
                }
            };

            var column = ColumnViewColumn.New("Progress", factory);
            column.FixedWidth = ProgressColumnWidth;
            column.Resizable = true;
            return column;
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

        private PopoverMenu? activeContextMenu;

        // Attaches right-click context gesture to a cell's widget
        private void AttachContextMenu(ListItem item, Widget child)
        {
            var gesture = GestureClick.New();
            gesture.SetButton(3);
            gesture.OnPressed += (_, clickArgs) =>
            {
                var pos = item.Position;
                if (pos != uint.MaxValue && pos < (uint)store.Count)
                {
                    if (!selection.IsSelected(pos))
                    {
                        selection.SelectItem(pos, true);
                    }
                    ShowContextMenu(store.Items[(int)pos], clickArgs.X, clickArgs.Y, child);
                }
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
    }
}
