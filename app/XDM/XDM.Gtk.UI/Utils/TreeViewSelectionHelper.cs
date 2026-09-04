// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Gtk;

namespace XDM.GtkUI
{
    // Right-click selection semantics + checkbox-column hit testing shared by both
    // download lists (and the Xvfb GTK harness): file-manager behavior where a
    // context-menu press inside the current multi-selection must preserve it, plus
    // pure-toggle checkbox clicks that accumulate selection without Ctrl.
    internal static class TreeViewSelectionHelper
    {
        // True when a right-click press at (x,y) lands on a row that is already part of
        // the view's current selection — the press must preserve the multi-selection
        // (context menu actions then apply to every selected row).
        internal static bool ShouldPreserveSelectionOnPress(TreeView view, double x, double y)
        {
            if (!view.GetPathAtPos((int)x, (int)y, out TreePath hit, out _, out _, out _)
                || hit == null)
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
        // the cursor. Used to claim button presses so checkbox clicks toggle selection
        // membership instead of GTK's default "replace selection with this row".
        // Column-reference comparison avoids all coordinate/allocator quirks.
        internal static bool HitTestToggleCell(TreeView view, TreeViewColumn checkboxColumn, double x, double y)
        {
            if (!view.GetPathAtPos((int)x, (int)y, out TreePath path, out TreeViewColumn? hitColumn, out _, out _)
                || path == null || hitColumn == null)
            {
                return false;
            }
            return ReferenceEquals(hitColumn, checkboxColumn);
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
    }
}
