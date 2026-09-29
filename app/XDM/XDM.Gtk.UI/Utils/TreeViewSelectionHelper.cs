// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Runtime.InteropServices;
using Gtk;

namespace XDM.GtkUI
{
    // Right-click selection semantics + checkbox-column hit testing shared by both
    // download lists (and the Xvfb GTK harness): file-manager behavior where a
    // context-menu press inside the current multi-selection must preserve it, plus
    // pure-toggle checkbox clicks that accumulate selection without Ctrl.
    internal static class TreeViewSelectionHelper
    {
        // GTK3 keeps rows in a child GdkWindow ("bin window") whose origin sits BELOW
        // the column header, and gtk_tree_view_get_path_at_pos() documents that its
        // coordinates must be the RAW coordinates of an event delivered on that
        // window. Real button-press/motion events on rows already carry those
        // coordinates, so they must be passed through untouched — converting them
        // (e.g. widget -> bin) shifts every hit by the header height and makes the
        // last row in the list resolve to nothing at all.
        private static bool TryPathAtPos(TreeView view, double x, double y,
            out TreePath path, out TreeViewColumn? column)
        {
            path = null;
            column = null;
            return view.GetPathAtPos((int)x, (int)y, out path, out column, out _, out _)
                && path != null;
        }

        // True when an event window is the view's ROW window (bin window), i.e. the
        // event is a genuine row event. Header-window events (column headings) arrive
        // with the header's own coordinates and must never be mistaken for a row click.
        private static IntPtr binWindowCache = IntPtr.Zero;
        private static TreeView? binWindowCacheOwner;

        internal static bool IsRowEvent(TreeView view, Gdk.Window? eventWindow)
        {
            if (eventWindow == null) return false;
            try
            {
                if (binWindowCacheOwner != view)
                {
                    binWindowCache = gtk_tree_view_get_bin_window(view.Handle);
                    binWindowCacheOwner = view;
                }
                return binWindowCache != IntPtr.Zero && eventWindow.Handle == binWindowCache;
            }
            catch (DllNotFoundException)
            {
                // Different GTK soname than libgtk-3.so.0 (exotic distros/musl builds):
                // behave like before the gate existed — treat the event as a row event
                // rather than silently disabling the checkbox.
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                return true;
            }
        }

        [DllImport("libgtk-3.so.0")]
        private static extern IntPtr gtk_tree_view_get_bin_window(IntPtr treeView);

        // True when a right-click press at (x,y) lands on a row that is already part of
        // the view's current selection — the press must preserve the multi-selection
        // (context menu actions then apply to every selected row).
        internal static bool ShouldPreserveSelectionOnPress(TreeView view, double x, double y)
        {
            if (!TryPathAtPos(view, x, y, out TreePath hit, out _))
            {
                return false;
            }
            var selected = view.Selection.GetSelectedRows(out _);
            if (selected == null || selected.Length < 2)
            {
                return false; // single/empty selection: GTK default behavior is correct
            }
            foreach (var path in selected)
            {
                if (path.Compare(hit) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        // True when (x,y) lands inside the dedicated checkbox COLUMN of the row under
        // the cursor. Used to decide whether the press belongs to the checkbox: if it
        // does, MainWindow toggles the row itself and claims the event so GTK's default
        // "click replaces the selection" handler cannot undo it.
        internal static bool HitTestToggleCell(TreeView view, TreeViewColumn checkboxColumn, double x, double y)
        {
            if (!TryPathAtPos(view, x, y, out TreePath path, out TreeViewColumn? hitColumn)
                || path == null || hitColumn == null)
            {
                return false;
            }
            return ReferenceEquals(hitColumn, checkboxColumn);
        }

        // Row under a row-window event's coordinates
        internal static bool TryGetRowAtEvent(TreeView view, double x, double y, out TreePath path)
        {
            return TryPathAtPos(view, x, y, out path, out _);
        }

        // Toggle one row's membership in the multi-selection (checkbox semantics)
        internal static void ToggleSelectionPath(TreeView view, TreePath path)
        {
            if (view.Selection.PathIsSelected(path))
            {
                view.Selection.UnselectPath(path);
            }
            else
            {
                view.Selection.SelectPath(path);
            }
        }

        // Row cell background: hovered rows paint NOTHING so the theme's rounded
        // row:hover CSS shows through (a cell rect would cover it square); only
        // non-hovered alternate rows get the striping tint.
        internal static string? RowCellBackground(bool isHovered, bool isAlternate, string alternateColor)
        {
            return !isHovered && isAlternate ? alternateColor : null;
        }

        // Checkbox gutter rail: always the plain card base (even/odd aware), never
        // the hover/selected fill — permanent visual separation between the
        // checkbox and the card, in every row state.
        internal static string GutterCellBackground(TreePath path, string cardHex, string alternateHex)
        {
            var isEvenRow = path == null || path.Indices == null || path.Indices.Length == 0
                || (path.Indices[0] % 2 == 0);
            return isEvenRow ? cardHex : alternateHex;
        }
    }
}
