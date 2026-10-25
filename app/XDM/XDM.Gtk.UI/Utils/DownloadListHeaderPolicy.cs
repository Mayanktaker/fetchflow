// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;

namespace XDM.GtkUI
{
    // Pure decision logic behind the download list column headers (gutter | File name |
    // Size). Kept free of GTK so the header's behaviour is unit-testable: GTK itself
    // owns the header buttons, this only decides what a click/selection means.
    internal static class DownloadListHeaderPolicy
    {
        // Faint "this column is sortable" hint for columns that are NOT the active sort;
        // the active one shows GTK's own direction arrow instead (GTK4 headerbars)
        internal const string SortableHint = "↕";
        // Gutter header select-all glyphs (DejaVu-covered, like the sort glyphs)
        internal const string GlyphNone = "☐";
        internal const string GlyphSome = "▣";
        internal const string GlyphAll = "☑";

        // Clicking File name/Size: switch column, or flip direction when that column
        // is already the active sort (a new column always starts descending)
        internal static bool SelectAllShouldClear(int selected, int total)
        {
            return total > 0 && selected >= total;
        }
    }
}
