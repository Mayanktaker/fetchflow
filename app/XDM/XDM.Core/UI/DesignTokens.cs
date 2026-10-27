// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// DesignTokens — the app's spacing and type scales, shared so one list drives
// the GTK 3 theme generator (scripts/theme-native.py parses these arrays, the
// same way it parses ColorSchemeTable), the GTK 3 window metrics and the guards
// in XDM.Tests. Values are device-independent px; spacing sits on a 2px grid so
// compact chrome (2px headerbar) and roomy chrome (24px sections) share a scale
// (docs/design.md "Spacing").
namespace XDM.Core.UI
{
    public static class DesignTokens
    {
        // Every spacing step a stylesheet or widget may use, smallest first
        public static readonly int[] Spacing = { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 24 };

        // Type steps the UI paints, matching the typography table (no 10px, no 18px)
        public static readonly int[] TypeScale = { 11, 12, 13, 14, 15, 16, 17 };

        // Every corner radius a stylesheet may use, smallest first (999 = pill)
        public static readonly int[] Radius = { 0, 4, 6, 7, 8, 10, 12, 17, 999 };

        // Named spacing steps, so call sites read as intent rather than a number
        public const int Space2 = 2;
        public const int Space4 = 4;
        public const int Space6 = 6;
        public const int Space8 = 8;
        public const int Space10 = 10;
        public const int Space12 = 12;
        public const int Space16 = 16;

        // Download-row cell-renderer padding: GTK 3 sizes a row from these, not CSS
        public const int RowTogglePadding = Space12;
        public const int RowIconPadding = Space12;
        public const int RowNamePadding = Space10;
        public const int RowMetaPadding = Space16;

        // Download-row cell-renderer horizontal insets
        public const int RowIconHorizontalPadding = Space12;
        public const int RowNameHorizontalPadding = Space12;
        public const int RowMetaHorizontalPadding = Space16;

        // Labeled toolbar button: gap between icon and text, and its side breathing room
        public const int ButtonContentSpacing = Space10;
        public const int ButtonBoxMargin = Space2;
    }
}
