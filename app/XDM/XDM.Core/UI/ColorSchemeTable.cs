// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// ColorSchemeTable — single source of truth for the 14 curated color schemes
// (7 dark + 7 light) shared by every UI frontend (GTK CSS layering, WPF brush
// layering). Pure data + clamping helpers only; no UI toolkit dependencies.
using System;

namespace XDM.Core.UI
{
    // Color scheme metadata definition for UI palettes
    public readonly struct ColorSchemeDefinition
    {
        // Unique scheme identifier
        public string Id { get; }
        // User-facing display title in settings
        public string DisplayName { get; }
        // Theme resource filename (CSS for GTK; kept for palette export parity)
        public string CssFileName { get; }
        // Primary accent RGB components for icon tinting
        public (byte R, byte G, byte B) AccentRgb { get; }
        // Primary accent hex code for markup and brushes
        public string AccentHex { get; }
        // Row hover highlight hex code
        public string HoverBackgroundHex { get; }
        // Row active/selected highlight hex code
        public string ActiveBackgroundHex { get; }
        // Alternating row subtle background hex code
        public string AlternateBackgroundHex { get; }
        // Plain card background hex code (odd rows; gutter rail color)
        public string CardBackgroundHex { get; }
        // Main window surface hex code (window / page background)
        public string BaseSurfaceHex { get; }
        // Navigation sidebar surface hex code
        public string SidebarHex { get; }
        // Title bar / header surface hex code
        public string HeaderBarHex { get; }

        // Constructs a new immutable color scheme definition
        public ColorSchemeDefinition(string id, string displayName, string cssFileName, byte r, byte g, byte b, string accentHex, string hoverHex, string activeHex, string alternateHex, string cardHex, string baseSurfaceHex, string sidebarHex, string headerBarHex)
        {
            Id = id;
            DisplayName = displayName;
            CssFileName = cssFileName;
            AccentRgb = (r, g, b);
            AccentHex = accentHex;
            HoverBackgroundHex = hoverHex;
            ActiveBackgroundHex = activeHex;
            AlternateBackgroundHex = alternateHex;
            CardBackgroundHex = cardHex;
            BaseSurfaceHex = baseSurfaceHex;
            SidebarHex = sidebarHex;
            HeaderBarHex = headerBarHex;
        }
    }

    // Shared scheme registry consumed by GTK ThemeManager and WPF theme engine
    public static class ColorSchemeTable
    {
        // Curated dark theme color schemes (1 Default + 6 Curated)
        public static readonly ColorSchemeDefinition[] DarkSchemes = new[]
        {
            new ColorSchemeDefinition("charcoal_blue", "Charcoal Blue (Default)", "xdm-dark.css", 53, 132, 228, "#3584e4", "#262c36", "#323b4a", "#212121", "#262626", "#1d1d1d", "#161616", "#1a1a1a"),
            new ColorSchemeDefinition("midnight_violet", "Midnight Violet", "xdm-dark-violet.css", 139, 92, 246, "#8b5cf6", "#282038", "#382d4e", "#1c1928", "#211e30", "#161420", "#110f1a", "#14121d"),
            new ColorSchemeDefinition("nord_emerald", "Nord Emerald", "xdm-dark-emerald.css", 16, 185, 129, "#10b981", "#1b302a", "#27443c", "#172421", "#1d2c29", "#131c1a", "#0d1513", "#101816"),
            new ColorSchemeDefinition("sunset_amber", "Sunset Amber", "xdm-dark-sunset.css", 244, 63, 94, "#f43f5e", "#332128", "#462e37", "#231c20", "#2b2328", "#1a1618", "#141012", "#171214"),
            new ColorSchemeDefinition("dracula_orchid", "Dracula Orchid", "xdm-dark-orchid.css", 236, 72, 153, "#ec4899", "#301e38", "#422b4d", "#1c1726", "#221d2e", "#191622", "#13101c", "#16131f"),
            new ColorSchemeDefinition("cyberpunk_matrix", "Cyberpunk Matrix", "xdm-dark-matrix.css", 6, 182, 212, "#06b6d4", "#162a3d", "#223b55", "#121b2b", "#182236", "#0f172a", "#0a0f1d", "#0d1324"),
            new ColorSchemeDefinition("espresso_mocha", "Espresso Mocha", "xdm-dark-mocha.css", 245, 158, 11, "#f59e0b", "#30241b", "#443327", "#201b18", "#26211e", "#1c1816", "#14110f", "#181412")
        };

        // Curated light theme color schemes (1 Default + 6 Curated)
        public static readonly ColorSchemeDefinition[] LightSchemes = new[]
        {
            new ColorSchemeDefinition("classic_blue", "Classic Blue (Default)", "xdm-light.css", 53, 132, 228, "#3584e4", "#f0f4f9", "#dbe7f7", "#f4f6f9", "#ffffff", "#fcfcfc", "#ececec", "#f0f0f0"),
            new ColorSchemeDefinition("nordic_frost", "Nordic Frost", "xdm-light-frost.css", 8, 145, 178, "#0891b2", "#e6f4f8", "#cfe2ea", "#edf3f6", "#f8fafb", "#f4f8fa", "#e3edf1", "#eaf1f4"),
            new ColorSchemeDefinition("solarized_sand", "Solarized Sand", "xdm-light-sand.css", 217, 119, 6, "#d97706", "#f7eee0", "#ecddc5", "#f4eedd", "#fdfbf6", "#faf6ee", "#ede6d8", "#f2ece0"),
            new ColorSchemeDefinition("rose_garden", "Rose Garden", "xdm-light-rose.css", 225, 29, 72, "#e11d48", "#fbe8ee", "#f4d1dc", "#f8ecf1", "#fdf8fa", "#fbf5f7", "#f2e4ea", "#f7edf1"),
            new ColorSchemeDefinition("matcha_forest", "Matcha Forest", "xdm-light-matcha.css", 5, 150, 105, "#059669", "#e3f3eb", "#cbe7d7", "#edf6f1", "#ffffff", "#f3f8f5", "#e2ede6", "#e8f2ec"),
            new ColorSchemeDefinition("lavender_bloom", "Lavender Bloom", "xdm-light-lavender.css", 124, 58, 237, "#7c3aed", "#ede7fa", "#ded2f5", "#f1ecf8", "#ffffff", "#f7f5fb", "#eae5f4", "#f0ecf7"),
            new ColorSchemeDefinition("citrus_peach", "Citrus Peach", "xdm-light-peach.css", 234, 88, 12, "#ea580c", "#fdece0", "#f7d8c0", "#fbeee4", "#ffffff", "#fdf6f0", "#f6e7db", "#faeee4")
        };

        // Default scheme per mode (fresh installs / unset choice)
        public const int DefaultDarkSchemeIndex = 0;   // Charcoal Blue
        public const int DefaultLightSchemeIndex = 0;  // Classic Blue

        // Resolves the default scheme index for the given mode
        public static int DefaultSchemeIndex(bool isDark) =>
            isDark ? DefaultDarkSchemeIndex : DefaultLightSchemeIndex;

        // Gets scheme definition with safe index clamping (out-of-range falls back
        // to the mode default, not scheme 0)
        public static ColorSchemeDefinition GetScheme(bool isDark, int index)
        {
            var schemes = isDark ? DarkSchemes : LightSchemes;
            if (index < 0 || index >= schemes.Length)
            {
                index = DefaultSchemeIndex(isDark);
            }
            return schemes[index];
        }

        // Normalizes any raw scheme index (-1/unset/out-of-range) to a valid index
        public static int ClampSchemeIndex(bool isDark, int index)
        {
            var schemes = isDark ? DarkSchemes : LightSchemes;
            if (index < 0 || index >= schemes.Length)
            {
                index = DefaultSchemeIndex(isDark);
            }
            return index;
        }
    }
}
