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
        private Widget CreateInProgressListView()
        {
            var listHost = new VBox();
            // Column header lives inside the TreeView itself (see WireListColumnHeaders)
            inprogressDownloadsStore = new ListStore(typeof(string),        // file name
                typeof(string),                                             // date modified
                typeof(string),                                             // size
                typeof(int),                                                // progress
                typeof(string),                                             // status
                typeof(InProgressDownloadItem)                             // download type
                );

            inprogressDownloadFilter = new TreeModelFilter(inprogressDownloadsStore, null);
            inprogressDownloadFilter.VisibleFunc = (model, iter) =>
            {
                var name = (string)model.GetValue(iter, 0);
                return Helpers.IsOfCategoryOrMatchesKeyword(name, searchKeyword, category);
            };

            var sortedStore = new TreeModelSort(inprogressDownloadFilter);

            sortedStore.SetSortFunc(0, (model, iter1, iter2) =>
            {
                var t1 = (string)model.GetValue(iter1, 0);
                var t2 = (string)model.GetValue(iter2, 0);
                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;
                return t1.CompareTo(t2);
            });

            sortedStore.SetSortFunc(1, (model, iter1, iter2) =>
            {
                var t1 = (InProgressDownloadItem)model.GetValue(iter1, 5);
                var t2 = (InProgressDownloadItem)model.GetValue(iter2, 5);
                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;
                return t1.DateAdded.CompareTo(t2.DateAdded);
            });

            sortedStore.SetSortFunc(2, (model, iter1, iter2) =>
            {
                var t1 = (InProgressDownloadItem)model.GetValue(iter1, 5);
                var t2 = (InProgressDownloadItem)model.GetValue(iter2, 5);
                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;
                return t1.Size.CompareTo(t2.Size);
            });

            sortedStore.SetSortFunc(SortTypeColumnId, (model, iter1, iter2) =>
            {
                var t1 = (InProgressDownloadItem)model.GetValue(iter1, 5);
                var t2 = (InProgressDownloadItem)model.GetValue(iter2, 5);
                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;
                return CompareByTypeThenName(t1.Name, t2.Name);
            });

            inprogressDownloadsStoreSorted = sortedStore;
            lvInprogress = new TreeView(sortedStore);
            // GTK 4 parity: no column header — select-all and sort live in the bottom bar
            lvInprogress.HeadersVisible = false;
            lvInprogress.Selection.Mode = SelectionMode.Multiple;
            // Drag-select (rubber band) — GTK defaults OFF, which silently broke
            // drag-based multi-selection attempts on the Active list.
            lvInprogress.RubberBanding = true;
            lvInprogress.EnableGridLines = TreeViewGridLines.None;
            // Per-view surface tint (see treeview.unfinished in the theme layer)
            lvInprogress.StyleContext.AddClass("unfinished");

            // Card split into three resizable columns:
            // gutter (selection) | File name (icon + title/sub) | Size meta
            var inprogressCardCol = new TreeViewColumn
            {
                // Resizable MUST come before Sizing: setting it while the mode is
                // AUTOSIZE makes GTK silently rewrite the mode to GROW_ONLY, which
                // then re-calculates the width from the cell content and throws the
                // user's drag away. FIXED keeps whatever the user dragged to.
                Resizable = true,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = DownloadNameColumnWidth,
                Expand = true,
                Spacing = DownloadColumnSpacing,
                SortColumnId = -1,
                MinWidth = DownloadNameColumnMinWidth,
                MaxWidth = DownloadNameColumnMaxWidth
            };
            var fileIconRenderer = new CellRendererPixbuf { };
            fileIconRenderer.SetPadding(DownloadIconHorizontalPadding, DownloadRowIconVerticalPadding);
            inprogressCardCol.PackStart(fileIconRenderer, false);
            inprogressCardCol.SetCellDataFunc(fileIconRenderer, new CellLayoutDataFunc(GetFileIcon));

            var inprogressNameRenderer = new CellRendererText();
            inprogressNameRenderer.SetPadding(DownloadNameHorizontalPadding, DownloadRowNameVerticalPadding);
            inprogressNameRenderer.Ellipsize = Pango.EllipsizeMode.Middle;
            inprogressCardCol.PackStart(inprogressNameRenderer, true);
            SetInProgressNameColumn(inprogressCardCol, inprogressNameRenderer, lvInprogress);

            // Size column: right-aligned progress/speed, resized from its header edge
            var inprogressSizeCol = new TreeViewColumn
            {
                Resizable = true,   // before Sizing, or GTK may rewrite the mode
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = DownloadMetaActiveWidth,
                SortColumnId = -1,
                MinWidth = DownloadSizeColumnMinWidth,
                MaxWidth = DownloadSizeColumnMaxWidth
            };
            var inprogressMetaRenderer = new CellRendererText
            {
                Xalign = 1.0f,
                Alignment = Pango.Alignment.Right,
                Yalign = 0.5f
            };
            inprogressMetaRenderer.SetPadding(DownloadMetaHorizontalPadding, DownloadRowMetaVerticalPadding);
            inprogressSizeCol.PackEnd(inprogressMetaRenderer, false);
            SetInProgressMetaColumn(inprogressSizeCol, inprogressMetaRenderer, lvInprogress);

            lvInprogress.AppendColumn(inprogressCardCol);
            lvInprogress.AppendColumn(inprogressSizeCol);
            WireListColumnHeaders(inprogressCardCol, inprogressSizeCol, true);

            lvInprogress.Selection.Changed += (_, _) =>
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                SyncSelectAllHeader();
                UpdateSelectionCount();
                // The gutter checkbox is drawn by a cell-data-func, so the row has to
                // be re-rendered explicitly or the tick can stay stale.
                lvInprogress.QueueDraw();
            };

            lvInprogress.MotionNotifyEvent += (o, args) =>
            {
                if (TreeViewSelectionHelper.IsRowEvent(lvInprogress, args.Event.Window)
                    && TreeViewSelectionHelper.TryGetRowAtEvent(lvInprogress, args.Event.X, args.Event.Y, out TreePath path))
                {
                    if (hoveredInprogressPath == null || hoveredInprogressPath.Compare(path) != 0)
                    {
                        hoveredInprogressPath = path;
                        lvInprogress.QueueDraw();
                    }
                }
                else if (hoveredInprogressPath != null)
                {
                    hoveredInprogressPath = null;
                    lvInprogress.QueueDraw();
                }
            };

            lvInprogress.LeaveNotifyEvent += (o, args) =>
            {
                if (hoveredInprogressPath != null)
                {
                    hoveredInprogressPath = null;
                    lvInprogress.QueueDraw();
                }
            };

            // File-manager semantics for right-click: ConnectBefore press handler claims
            // the event BEFORE GTK's default selection handler runs (ButtonPress is
            // RUN_LAST, so a normal handler is already too late and the multi-selection
            // has collapsed to one row — the reported "Ctrl+A then right-click deletes
            // only one" bug). Snapshot + restore on release is the safety net.
            lvInprogress.ButtonPressEvent += OnInprogressButtonPress;
            lvInprogress.ButtonReleaseEvent += OnInprogressButtonRelease;
            lvInprogress.KeyPressEvent += OnDownloadListKeyPress;

            ApplyDownloadSort();

            swInProgress = new ScrolledWindow { OverlayScrolling = true, Margin = 6, MarginBottom = 2, MarginTop = 2, ShadowType = ShadowType.None };
            swInProgress.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            swInProgress.Add(lvInprogress);
            // Stack swaps the list for the empty page instead of showing bare headers
            stackInProgress = new Gtk.Stack { TransitionType = StackTransitionType.None };
            stackInProgress.AddNamed(swInProgress, "list");
            emptyInProgress = new EmptyStatePanel(
                () => { try { btnNew?.Activate(); } catch { } },
                ClearSearchFromEmptyState);
            stackInProgress.AddNamed(emptyInProgress.Page, "empty");
            stackInProgress.VisibleChildName = "list";
            stackInProgress.ShowAll();
            listHost.PackStart(stackInProgress, true, true, 0);
            listHost.ShowAll();
            listHostInProgress = listHost;
            return listHost;
        }

        private Widget CreateFinishedListView()
        {
            var listHost = new VBox();
            // Column header lives inside the TreeView itself (see WireListColumnHeaders)
            finishedDownloadsStore = new ListStore(typeof(string),          // file name
                typeof(string),                                             // date modified
                typeof(string),                                             // size
                typeof(FinishedDownloadItem)                               // download type
                );

            finishedDownloadFilter = new TreeModelFilter(finishedDownloadsStore, null);
            finishedDownloadFilter.VisibleFunc = (model, iter) =>
            {
                var name = (string)model.GetValue(iter, 0);
                return Helpers.IsOfCategoryOrMatchesKeyword(name, searchKeyword, category);
            };

            var sortedStore = new TreeModelSort(finishedDownloadFilter);

            sortedStore.SetSortFunc(0, (model, iter1, iter2) =>
            {
                var t1 = (string)model.GetValue(iter1, 0);
                var t2 = (string)model.GetValue(iter2, 0);

                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;

                return t1.CompareTo(t2);
            });

            sortedStore.SetSortFunc(1, (model, iter1, iter2) =>
            {
                var t1 = (FinishedDownloadItem)model.GetValue(iter1, 3);
                var t2 = (FinishedDownloadItem)model.GetValue(iter2, 3);

                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;

                return t1.DateAdded.CompareTo(t2.DateAdded);
            });

            sortedStore.SetSortFunc(2, (model, iter1, iter2) =>
            {
                var t1 = (FinishedDownloadItem)model.GetValue(iter1, 3);
                var t2 = (FinishedDownloadItem)model.GetValue(iter2, 3);

                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;

                return t1.Size.CompareTo(t2.Size);
            });

            sortedStore.SetSortFunc(SortTypeColumnId, (model, iter1, iter2) =>
            {
                var t1 = (FinishedDownloadItem)model.GetValue(iter1, 3);
                var t2 = (FinishedDownloadItem)model.GetValue(iter2, 3);

                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;

                return CompareByTypeThenName(t1.Name, t2.Name);
            });

            finishedDownloadsStoreSorted = sortedStore;
            lvFinished = new TreeView(sortedStore);
            lvFinished.HeadersVisible = false;
            lvFinished.Selection.Mode = SelectionMode.Multiple;
            // Drag-select (rubber band) — same as the Active list
            lvFinished.RubberBanding = true;
            lvFinished.EnableGridLines = TreeViewGridLines.None;
            // Per-view surface tint (see treeview.finished in the theme layer)
            lvFinished.StyleContext.AddClass("finished");

            // Card split into three resizable columns:
            // gutter (selection) | File name (icon + title/sub) | Size meta
            var finishedCardCol = new TreeViewColumn
            {
                // Resizable before Sizing — see the in-progress name column above
                Resizable = true,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = DownloadNameColumnWidth,
                Expand = true,
                Spacing = DownloadColumnSpacing,
                SortColumnId = -1,
                MinWidth = DownloadNameColumnMinWidth,
                MaxWidth = DownloadNameColumnMaxWidth
            };
            var fileIconRenderer = new CellRendererPixbuf { };
            fileIconRenderer.SetPadding(DownloadIconHorizontalPadding, DownloadRowIconVerticalPadding);
            finishedCardCol.PackStart(fileIconRenderer, false);
            finishedCardCol.SetCellDataFunc(fileIconRenderer, new CellLayoutDataFunc(GetFileIcon));

            var finishedNameRenderer = new CellRendererText();
            finishedNameRenderer.SetPadding(DownloadNameHorizontalPadding, DownloadRowNameVerticalPadding);
            finishedNameRenderer.Ellipsize = Pango.EllipsizeMode.Middle;
            finishedCardCol.PackStart(finishedNameRenderer, true);
            SetFinishedNameColumn(finishedCardCol, finishedNameRenderer, lvFinished);

            // Size column: right-aligned size/date, resized from its header edge
            var finishedSizeCol = new TreeViewColumn
            {
                Resizable = true,   // before Sizing, or GTK may rewrite the mode
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = DownloadMetaFinishedWidth,
                SortColumnId = -1,
                MinWidth = DownloadSizeColumnMinWidth,
                MaxWidth = DownloadSizeColumnMaxWidth
            };
            var finishedMetaRenderer = new CellRendererText
            {
                Xalign = 1.0f,
                Alignment = Pango.Alignment.Right,
                Yalign = 0.5f
            };
            finishedMetaRenderer.SetPadding(DownloadMetaHorizontalPadding, DownloadRowMetaVerticalPadding);
            finishedSizeCol.PackEnd(finishedMetaRenderer, false);
            SetFinishedMetaColumn(finishedSizeCol, finishedMetaRenderer, lvFinished);

            lvFinished.AppendColumn(finishedCardCol);
            lvFinished.AppendColumn(finishedSizeCol);
            WireListColumnHeaders(finishedCardCol, finishedSizeCol, false);

            lvFinished.Selection.Changed += (_, _) =>
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                SyncSelectAllHeader();
                UpdateSelectionCount();
                // See above: force the gutter checkbox to repaint with the new state
                lvFinished.QueueDraw();
            };

            lvFinished.MotionNotifyEvent += (o, args) =>
            {
                if (TreeViewSelectionHelper.IsRowEvent(lvFinished, args.Event.Window)
                    && TreeViewSelectionHelper.TryGetRowAtEvent(lvFinished, args.Event.X, args.Event.Y, out TreePath path))
                {
                    if (hoveredFinishedPath == null || hoveredFinishedPath.Compare(path) != 0)
                    {
                        hoveredFinishedPath = path;
                        lvFinished.QueueDraw();
                    }
                }
                else if (hoveredFinishedPath != null)
                {
                    hoveredFinishedPath = null;
                    lvFinished.QueueDraw();
                }
            };

            lvFinished.LeaveNotifyEvent += (o, args) =>
            {
                if (hoveredFinishedPath != null)
                {
                    hoveredFinishedPath = null;
                    lvFinished.QueueDraw();
                }
            };

            // Same right-click preservation + checkbox pure-toggle as the in-progress list
            lvFinished.ButtonPressEvent += OnFinishedButtonPress;
            lvFinished.ButtonReleaseEvent += OnFinishedButtonRelease;
            lvFinished.KeyPressEvent += OnDownloadListKeyPress;

            ApplyDownloadSort();

            swFinished = new ScrolledWindow { OverlayScrolling = true, Margin = 6, MarginBottom = 2, MarginTop = 2, ShadowType = ShadowType.None };
            swFinished.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            swFinished.Add(lvFinished);
            // Same list/empty swap as the Active list so both views start somewhere
            stackFinished = new Gtk.Stack { TransitionType = StackTransitionType.None };
            stackFinished.AddNamed(swFinished, "list");
            emptyFinished = new EmptyStatePanel(
                () => { try { btnNew?.Activate(); } catch { } },
                ClearSearchFromEmptyState);
            stackFinished.AddNamed(emptyFinished.Page, "empty");
            stackFinished.VisibleChildName = "list";
            stackFinished.ShowAll();
            listHost.PackStart(stackFinished, true, true, 0);
            listHost.ShowAll();
            listHostFinished = listHost;
            return listHost;
        }

        // Wires a download list's column headers: File name and Size. The headers are
        // GTK's own header buttons, so dragging a column edge
        // resizes it natively and each caption follows its column. sort-column-id stays
        // -1 to detach GTK's built-in click sorting — clicks route through the app's
        // persisted sort state instead (SetDownloadSort is the single source of truth).
        private void WireListColumnHeaders(TreeViewColumn nameCol, TreeViewColumn sizeCol, bool inprogress)
        {
            if (inprogress)
            {
                inprogressNameCol = nameCol;
                inprogressSizeCol = sizeCol;
            }
            else
            {
                finishedNameCol = nameCol;
                finishedSizeCol = sizeCol;
            }

            // Size column keeps its right alignment for the metadata it labels
            sizeCol.Alignment = 1.0f;
        }
    }
}
