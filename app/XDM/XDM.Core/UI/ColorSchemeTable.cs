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

        // Constructs a new immutable color scheme definition
        public ColorSchemeDefinition(string id, string displayName, string cssFileName, byte r, byte g, byte b, string accentHex, string hoverHex, string activeHex, string alternateHex, string cardHex)
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
        }
    }

    // Shared scheme registry consumed by GTK ThemeManager and WPF theme engine
    public static class ColorSchemeTable
    {
        // Curated dark theme color schemes (1 Default + 6 Curated)
        public static readonly ColorSchemeDefinition[] DarkSchemes = new[]
        {
            new ColorSchemeDefinition("charcoal_blue", "Charcoal Blue", "xdm-dark.css", 53, 132, 228, "#3584e4", "#262c36", "#323b4a", "#212121", "#262626"),
            new ColorSchemeDefinition("midnight_violet", "Midnight Violet", "xdm-dark-violet.css", 139, 92, 246, "#8b5cf6", "#282038", "#382d4e", "#1c1928", "#211e30"),
            new ColorSchemeDefinition("nord_emerald", "Nord Emerald (Default)", "xdm-dark-emerald.css", 16, 185, 129, "#10b981", "#1b302a", "#27443c", "#172421", "#1d2c29"),
            new ColorSchemeDefinition("sunset_amber", "Sunset Amber", "xdm-dark-sunset.css", 244, 63, 94, "#f43f5e", "#332128", "#462e37", "#231c20", "#2b2328"),
            new ColorSchemeDefinition("dracula_orchid", "Dracula Orchid", "xdm-dark-orchid.css", 236, 72, 153, "#ec4899", "#301e38", "#422b4d", "#1c1726", "#221d2e"),
            new ColorSchemeDefinition("cyberpunk_matrix", "Cyberpunk Matrix", "xdm-dark-matrix.css", 6, 182, 212, "#06b6d4", "#162a3d", "#223b55", "#121b2b", "#182236"),
            new ColorSchemeDefinition("espresso_mocha", "Espresso Mocha", "xdm-dark-mocha.css", 245, 158, 11, "#f59e0b", "#30241b", "#443327", "#201b18", "#26211e")
        };

        // Curated light theme color schemes (1 Default + 6 Curated)
        public static readonly ColorSchemeDefinition[] LightSchemes = new[]
        {
            new ColorSchemeDefinition("classic_blue", "Classic Blue", "xdm-light.css", 53, 132, 228, "#3584e4", "#f0f4f9", "#dbe7f7", "#f4f6f9", "#ffffff"),
            new ColorSchemeDefinition("nordic_frost", "Nordic Frost (Default)", "xdm-light-frost.css", 8, 145, 178, "#0891b2", "#e6f4f8", "#cfe2ea", "#edf3f6", "#f8fafb"),
            new ColorSchemeDefinition("solarized_sand", "Solarized Sand", "xdm-light-sand.css", 217, 119, 6, "#d97706", "#f7eee0", "#ecddc5", "#f4eedd", "#fdfbf6"),
            new ColorSchemeDefinition("rose_garden", "Rose Garden", "xdm-light-rose.css", 225, 29, 72, "#e11d48", "#fbe8ee", "#f4d1dc", "#f8ecf1", "#fdf8fa"),
            new ColorSchemeDefinition("matcha_forest", "Matcha Forest", "xdm-light-matcha.css", 5, 150, 105, "#059669", "#e3f3eb", "#cbe7d7", "#edf6f1", "#ffffff"),
            new ColorSchemeDefinition("lavender_bloom", "Lavender Bloom", "xdm-light-lavender.css", 124, 58, 237, "#7c3aed", "#ede7fa", "#ded2f5", "#f1ecf8", "#ffffff"),
            new ColorSchemeDefinition("citrus_peach", "Citrus Peach", "xdm-light-peach.css", 234, 88, 12, "#ea580c", "#fdece0", "#f7d8c0", "#fbeee4", "#ffffff")
        };

        // Default scheme per mode (fresh installs / unset choice)
        public const int DefaultDarkSchemeIndex = 2;   // Nord Emerald
        public const int DefaultLightSchemeIndex = 1;  // Nordic Frost

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
