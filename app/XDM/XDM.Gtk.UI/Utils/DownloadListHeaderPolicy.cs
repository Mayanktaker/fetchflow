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
        internal static (string Column, bool Descending) NextSort(
            string clicked, string current, bool currentDescending)
        {
            return current == clicked
                ? (clicked, !currentDescending)
                : (clicked, true);
        }

        // Column header caption: plain text on the active sort column (GTK draws the
        // direction arrow beside it), caption + hint on the idle sortable columns
        internal static string Caption(string text, bool isActiveSort)
        {
            return isActiveSort ? text : $"{text} {SortableHint}";
        }

        // Gutter header glyph for the current selection (all / some / none)
        internal static string SelectAllGlyph(int selected, int total)
        {
            if (selected <= 0) return GlyphNone;
            return total > 0 && selected >= total ? GlyphAll : GlyphSome;
        }

        // Select-all acts as a toggle: it clears only when everything is already
        // selected, otherwise it selects the rest (GTK4 list behaviour)
        internal static bool SelectAllShouldClear(int selected, int total)
        {
            return total > 0 && selected >= total;
        }
    }
}
