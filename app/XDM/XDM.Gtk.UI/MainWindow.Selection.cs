// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Gtk;
using Application = Gtk.Application;
using IoPath = System.IO.Path;
using XDM.Core;
using XDM.Core.Util;
using XDM.Core.UI;
using Translations;
using Menu = Gtk.Menu;
using MenuItem = Gtk.MenuItem;
using XDM.GtkUI.Utils;
using XDM.GtkUI.Dialogs.DeleteConfirm;
using XDM.GtkUI.Dialogs.Language;
using TraceLog;
using XDM.Core.BrowserMonitoring;


namespace XDM.GtkUI
{
    public partial class MainWindow : Window, IApplicationWindow
    {
        // Headline above the list: view name, count, select-all and sort (GTK 4 layout)
        private Widget CreateContentToolbar()
        {
            var bar = new HBox(false, 8);
            bar.StyleContext.AddClass("content-toolbar");

            lblViewTitle = new Label("");
            lblViewTitle.StyleContext.AddClass("view-title");
            bar.PackStart(lblViewTitle, false, false, 0);

            lblViewCount = new Label("");
            lblViewCount.StyleContext.AddClass("view-count");
            bar.PackStart(lblViewCount, false, false, 0);

            var spacer = new VBox();
            bar.PackStart(spacer, true, true, 0);

            // Labeled like GTK 4's control: icon-only left an empty hole when the
            // named icon was missing from svg-icons
            btnSelectAll = CreateButtonWithContent("checkbox-checked",
                TextResource.GetText("LBL_SELECT_ALL") ?? "Select All",
                DimR, DimG, DimB, TextResource.GetText("LBL_SELECT_ALL") ?? "Select All");
            btnSelectAll.Clicked += (_, _) => ToggleSelectAll(IsInProgressView());
            bar.PackEnd(btnSelectAll, false, false, 0);

            // Sort menu, the same six entries GTK 4 offers (Name/Size/Date, both ways)
            btnSort = new MenuButton { TooltipText = "Sort downloads" };
            btnSort.Label = SortCaption();
            btnSort.Popup = BuildSortMenu();
            bar.PackEnd(btnSort, false, false, 0);

            SyncSelectAllTooltip();
            bar.ShowAll();
            return bar;
        }

        // Headline count: the selection while one exists, otherwise the row count
        private void UpdateViewHeading()
        {
            if (lblViewCount == null) return;
            var view = IsInProgressView() ? lvInprogress : lvFinished;
            var selected = view?.Selection.CountSelectedRows() ?? 0;
            var total = view?.Model?.IterNChildren() ?? 0;
            lblViewCount.Text = selected > 0
                ? $"{selected} {TextResource.GetText("LBL_SELECTED") ?? "selected"}"
                : $"{total} {TextResource.GetText("LBL_ITEMS") ?? "items"}";
        }

        // The list that owns the current selection: the Ctrl+C copy source
        private bool ActiveListIsFinished()
        {
            var finishedSelected = lvFinished?.Selection.CountSelectedRows() ?? 0;
            var inprogressSelected = lvInprogress?.Selection.CountSelectedRows() ?? 0;
            return finishedSelected > 0 && inprogressSelected == 0;
        }

        // The select-all tooltip reflects the toggle's next action, like GTK 4
        private void SyncSelectAllTooltip()
        {
            if (btnSelectAll == null) return;
            var view = IsInProgressView() ? lvInprogress : lvFinished;
            var selected = view?.Selection.CountSelectedRows() ?? 0;
            var total = view?.Model?.IterNChildren() ?? 0;
            btnSelectAll.TooltipText = selected == total && total > 0
                ? "Deselect All (Ctrl+A)"
                : "Select All (Ctrl+A)";
            UpdateViewHeading();
        }

        // Sort button caption: the active column and its direction
        private string SortCaption()
        {
            var col = Config.Instance.DownloadSortColumn ?? "Date";
            var caption = col switch
            {
                "Name" => TextResource.GetText("SORT_NAME") ?? "File name",
                "Size" => TextResource.GetText("SORT_SIZE") ?? "Size",
                _ => TextResource.GetText("SORT_DATE") ?? "Date",
            };
            var arrow = Config.Instance.DownloadSortDescending ? "↓" : "↑";
            return caption + " " + arrow;
        }

        // The six sort entries GTK 4 offers, routed through the persisted sort state
        private Menu BuildSortMenu()
        {
            var menu = new Menu();
            void Add(string text, string column, bool descending)
            {
                var item = new MenuItem(text);
                item.Activated += (_, _) => SetDownloadSort(column, descending);
                menu.Append(item);
            }
            Add(TextResource.GetText("SORT_NAME_ASC") ?? "Name (A-Z)", "Name", false);
            Add(TextResource.GetText("SORT_NAME_DESC") ?? "Name (Z-A)", "Name", true);
            Add(TextResource.GetText("SORT_SIZE_ASC") ?? "Size (smallest first)", "Size", false);
            Add(TextResource.GetText("SORT_SIZE_DESC") ?? "Size (largest first)", "Size", true);
            Add(TextResource.GetText("SORT_DATE_DESC") ?? "Date (newest first)", "Date", true);
            Add(TextResource.GetText("SORT_DATE_ASC") ?? "Date (oldest first)", "Date", false);
            menu.ShowAll();
            return menu;
        }

        // Select-all acts as a toggle over the current selection
        private void ToggleSelectAll(bool inprogress)
        {
            var view = inprogress ? lvInprogress : lvFinished;
            if (view == null) return;
            var selected = view.Selection.CountSelectedRows();
            if (DownloadListHeaderPolicy.SelectAllShouldClear(selected, view.Model.IterNChildren()))
            {
                view.Selection.UnselectAll();
            }
            else
            {
                view.Selection.SelectAll();
            }
            UpdateSelectionCount();
        }

        // Bottom-bar counter mirrors the GTK 4 headline: "N selected", hidden at zero
        private void UpdateSelectionCount()
        {
            try
            {
                if (lblSelectionCount == null) return;
                var view = IsInProgressView() ? lvInprogress : lvFinished;
                var selected = view?.Selection.CountSelectedRows() ?? 0;
                lblSelectionCount.Visible = selected > 0;
                if (selected > 0)
                {
                    lblSelectionCount.Text = $"{selected} {TextResource.GetText("LBL_SELECTED") ?? "selected"}";
                }
            }
            catch { }
        }

        // Mirrors the persisted sort state onto both lists' column headers. The active
        // column gets GTK's own direction arrow (GTK4 headerbar behaviour); every other
        // sortable column keeps a faint ↕ hint so the header still advertises sorting.
        private void SyncHeaderSortState()
        {
            try
            {
                var col = Config.Instance.DownloadSortColumn ?? "Date";
                var order = Config.Instance.DownloadSortDescending
                    ? SortType.Descending : SortType.Ascending;
                if (btnSort != null)
                {
                    // The bottom-bar sort button carries the state the header used to
                    btnSort.Label = SortCaption();
                }
            }
            catch { }
        }

        // Row selection drives the bottom-bar select-all tooltip (all/some/none)
        private void SyncSelectAllHeader()
        {
            SyncSelectAllTooltip();
        }

        // Runs BEFORE GTK's default TreeView press handler (ButtonPress is RUN_LAST):
        // claiming the event here is the only way to stop the default handler from
        // collapsing a Ctrl+A / checkbox multi-selection to the single right-clicked row.
        [GLib.ConnectBefore]
        private void OnInprogressButtonPress(object o, ButtonPressEventArgs args)
        {
            var rowEvent = TreeViewSelectionHelper.IsRowEvent(lvInprogress, args.Event.Window);
            if (args.Event.Type == Gdk.EventType.ButtonPress && args.Event.Button == 3
                && rowEvent
                && TreeViewSelectionHelper.ShouldPreserveSelectionOnPress(lvInprogress, args.Event.X, args.Event.Y))
            {
                inprogressRightClickSnapshot = lvInprogress.Selection.GetSelectedRows(out _);
                args.RetVal = true;
            }
            else
            {
                inprogressRightClickSnapshot = null;
            }
        }

        private void OnInprogressButtonRelease(object o, ButtonReleaseEventArgs args)
        {
            if (args.Event.Type == Gdk.EventType.ButtonRelease && args.Event.Button == 3)
            {
                RestoreSnapshotIfCollapsed(lvInprogress, ref inprogressRightClickSnapshot);
                InProgressContextMenuOpening?.Invoke(this, EventArgs.Empty);
                menuInProgress.PopupAtPointer(args.Event);
            }
        }

        [GLib.ConnectBefore]
        private void OnFinishedButtonPress(object o, ButtonPressEventArgs args)
        {
            var rowEvent = TreeViewSelectionHelper.IsRowEvent(lvFinished, args.Event.Window);
            if (args.Event.Type == Gdk.EventType.ButtonPress && args.Event.Button == 3
                && rowEvent
                && TreeViewSelectionHelper.ShouldPreserveSelectionOnPress(lvFinished, args.Event.X, args.Event.Y))
            {
                finishedRightClickSnapshot = lvFinished.Selection.GetSelectedRows(out _);
                args.RetVal = true;
            }
            else
            {
                finishedRightClickSnapshot = null;
            }
        }

        private void OnFinishedButtonRelease(object o, ButtonReleaseEventArgs args)
        {
            if (args.Event.Type == Gdk.EventType.ButtonRelease && args.Event.Button == 3)
            {
                RestoreSnapshotIfCollapsed(lvFinished, ref finishedRightClickSnapshot);
                FinishedContextMenuOpening?.Invoke(this, EventArgs.Empty);
                menuFinished.PopupAtPointer(args.Event);
            }
        }

        // Safety net: if the press claim ever misses (theme hitbox drift, Wayland
        // coordinate skew), the press collapses to 1 row — reselect the snapshot so
        // the context menu and Delete act on every row the user had selected.
        private static void RestoreSnapshotIfCollapsed(TreeView view, ref TreePath[]? snapshot)
        {
            try
            {
                if (snapshot == null || snapshot.Length < 2) { snapshot = null; return; }
                var current = view.Selection.GetSelectedRows(out _);
                if (current != null && current.Length == snapshot.Length) { snapshot = null; return; }
                view.Selection.UnselectAll();
                foreach (var path in snapshot)
                {
                    try { view.Selection.SelectPath(path); } catch { }
                }
            }
            catch { }
            finally { snapshot = null; }
        }

        // Explicit Ctrl+A select-all: GTK's built-in binding is theme/distro dependent,
        // so guarantee it on both lists. Delete key forwards to the toolbar delete.
        private void OnDownloadListKeyPress(object o, KeyPressEventArgs args)
        {
            try
            {
                var isCtrl = (args.Event.State & Gdk.ModifierType.ControlMask) != 0;
                if (isCtrl && (args.Event.Key == Gdk.Key.a || args.Event.Key == Gdk.Key.A))
                {
                    if (o is TreeView view)
                    {
                        view.Selection.SelectAll();
                        args.RetVal = true;
                    }
                }
            }
            catch { }
        }

        // Compares by file extension first, then full name — powers "Sort by Type"
        private static int CompareByTypeThenName(string? a, string? b)
        {
            var extA = System.IO.Path.GetExtension(a ?? string.Empty).ToUpperInvariant();
            var extB = System.IO.Path.GetExtension(b ?? string.Empty).ToUpperInvariant();
            var cmp = string.Compare(extA, extB, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        // Applies the saved sort (Name/Size/Date/Type + direction) to both lists
        private void ApplyDownloadSort()
        {
            try
            {
                var columnId = Config.Instance.DownloadSortColumn switch
                {
                    "Name" => 0,
                    "Size" => 2,
                    "Type" => SortTypeColumnId,
                    _ => 1,
                };
                var order = Config.Instance.DownloadSortDescending ? SortType.Descending : SortType.Ascending;
                inprogressDownloadsStoreSorted?.SetSortColumnId(columnId, order);
                finishedDownloadsStoreSorted?.SetSortColumnId(columnId, order);
                SyncHeaderSortState();
            }
            catch { }
        }

        // Header sort selection: persist + apply to both lists
        private void SetDownloadSort(string column, bool? descending = null)
        {
            try
            {
                Config.Instance.DownloadSortColumn = column;
                if (descending.HasValue)
                {
                    Config.Instance.DownloadSortDescending = descending.Value;
                }
                Config.SaveConfig();
                ApplyDownloadSort();
            }
            catch { }
        }
    }
}
