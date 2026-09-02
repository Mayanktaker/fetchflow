// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// ThemeManager — swaps the XDM CSS theme and color scheme layer live (no restart)
// and keeps the GTK-level dark preference in sync. Cosmetic only: every GTK call
// is guarded because Gdk.Screen.Default can throw under Wayland GtkSharp bindings.
using System;
using Gtk;
using TraceLog;
using XDM.Core;

namespace XDM.GtkUI.Utils
{
    // Color scheme metadata definition for UI palettes
    public readonly struct ColorSchemeDefinition
    {
        // Unique scheme identifier
        public string Id { get; }
        // User-facing display title in settings
        public string DisplayName { get; }
        // CSS filename inside theme/ directory
        public string CssFileName { get; }
        // Primary accent RGB components for pixbuf tinting
        public (byte R, byte G, byte B) AccentRgb { get; }
        // Primary accent hex code for Pango markup
        public string AccentHex { get; }
        // TreeView row hover highlight hex code
        public string HoverBackgroundHex { get; }
        // TreeView alternating row subtle background hex code
        public string AlternateBackgroundHex { get; }

        // Constructs a new immutable color scheme definition
        public ColorSchemeDefinition(string id, string displayName, string cssFileName, byte r, byte g, byte b, string accentHex, string hoverHex, string alternateHex)
        {
            Id = id;
            DisplayName = displayName;
            CssFileName = cssFileName;
            AccentRgb = (r, g, b);
            AccentHex = accentHex;
            HoverBackgroundHex = hoverHex;
            AlternateBackgroundHex = alternateHex;
        }
    }

    public static class ThemeManager
    {
        // Theme CSS directory relative to application executable
        private const string ThemeDir = "theme";
        // Default base theme fallbacks
        private const string FallbackDarkCss = "xdm-dark.css";
        private const string FallbackLightCss = "xdm-light.css";
        // Provider priority matching font-size layer
        private const uint ThemeProviderPriority = 800;
        // Standard GTK3 Adwaita base theme name
        private const string AdwaitaThemeName = "Adwaita";

        // Curated dark theme color schemes (1 Default + 6 Curated)
        public static readonly ColorSchemeDefinition[] DarkSchemes = new[]
        {
            new ColorSchemeDefinition("charcoal_blue", "Charcoal Blue (Default)", "xdm-dark.css", 53, 132, 228, "#3584e4", "#262c36", "#212121"),
            new ColorSchemeDefinition("midnight_violet", "Midnight Violet", "xdm-dark-violet.css", 139, 92, 246, "#8b5cf6", "#282038", "#1c1928"),
            new ColorSchemeDefinition("nord_emerald", "Nord Emerald", "xdm-dark-emerald.css", 16, 185, 129, "#10b981", "#1b302a", "#172421"),
            new ColorSchemeDefinition("sunset_amber", "Sunset Amber", "xdm-dark-sunset.css", 244, 63, 94, "#f43f5e", "#332128", "#231c20"),
            new ColorSchemeDefinition("dracula_orchid", "Dracula Orchid", "xdm-dark-orchid.css", 236, 72, 153, "#ec4899", "#301e38", "#1c1726"),
            new ColorSchemeDefinition("cyberpunk_matrix", "Cyberpunk Matrix", "xdm-dark-matrix.css", 6, 182, 212, "#06b6d4", "#162a3d", "#121b2b"),
            new ColorSchemeDefinition("espresso_mocha", "Espresso Mocha", "xdm-dark-mocha.css", 245, 158, 11, "#f59e0b", "#30241b", "#201b18")
        };

        // Curated light theme color schemes (1 Default + 6 Curated)
        public static readonly ColorSchemeDefinition[] LightSchemes = new[]
        {
            new ColorSchemeDefinition("classic_blue", "Classic Blue (Default)", "xdm-light.css", 53, 132, 228, "#3584e4", "#f0f4f9", "#f4f6f9"),
            new ColorSchemeDefinition("nordic_frost", "Nordic Frost", "xdm-light-frost.css", 8, 145, 178, "#0891b2", "#e6f4f8", "#edf3f6"),
            new ColorSchemeDefinition("solarized_sand", "Solarized Sand", "xdm-light-sand.css", 217, 119, 6, "#d97706", "#f7eee0", "#f4eedd"),
            new ColorSchemeDefinition("rose_garden", "Rose Garden", "xdm-light-rose.css", 225, 29, 72, "#e11d48", "#fbe8ee", "#f8ecf1"),
            new ColorSchemeDefinition("matcha_forest", "Matcha Forest", "xdm-light-matcha.css", 5, 150, 105, "#059669", "#e3f3eb", "#edf6f1"),
            new ColorSchemeDefinition("lavender_bloom", "Lavender Bloom", "xdm-light-lavender.css", 124, 58, 237, "#7c3aed", "#ede7fa", "#f1ecf8"),
            new ColorSchemeDefinition("citrus_peach", "Citrus Peach", "xdm-light-peach.css", 234, 88, 12, "#ea580c", "#fdece0", "#fbeee4")
        };

        // Provider currently attached to the default screen
        private static CssProvider? currentThemeProvider;

        // Tracks whether the dark theme is actively rendered
        public static bool IsDarkActive { get; private set; }

        // Tracks current active color scheme index (0..3)
        public static int ActiveColorScheme { get; private set; } = 0;

        // Returns the active color scheme definition based on theme mode and scheme index
        public static ColorSchemeDefinition ActiveScheme => GetScheme(IsDarkActive, ActiveColorScheme);

        // Active primary accent RGB tuple
        public static (byte R, byte G, byte B) ActiveAccentColor => ActiveScheme.AccentRgb;

        // Active primary accent hex code
        public static string ActiveAccentHex => ActiveScheme.AccentHex;

        // Active TreeView row hover background color
        public static string ActiveHoverColor => ActiveScheme.HoverBackgroundHex;

        // Active TreeView alternating row subtle background color
        public static string ActiveAlternateRowColor => ActiveScheme.AlternateBackgroundHex;

        // Event raised whenever the active theme or color scheme changes
        public static event Action<bool>? ThemeChanged;

        // Gets scheme definition with safe index clamping
        public static ColorSchemeDefinition GetScheme(bool isDark, int index)
        {
            var schemes = isDark ? DarkSchemes : LightSchemes;
            if (index < 0 || index >= schemes.Length)
            {
                index = 0;
            }
            return schemes[index];
        }

        // Exports all available color scheme definitions to a formatted JSON string
        public static string ExportPalettesJson()
        {
            var data = new
            {
                Version = "1.0",
                DarkSchemes = System.Linq.Enumerable.Select(DarkSchemes, s => new
                {
                    s.Id,
                    s.DisplayName,
                    s.CssFileName,
                    AccentR = s.AccentRgb.R,
                    AccentG = s.AccentRgb.G,
                    AccentB = s.AccentRgb.B,
                    s.AccentHex,
                    s.HoverBackgroundHex,
                    s.AlternateBackgroundHex
                }),
                LightSchemes = System.Linq.Enumerable.Select(LightSchemes, s => new
                {
                    s.Id,
                    s.DisplayName,
                    s.CssFileName,
                    AccentR = s.AccentRgb.R,
                    AccentG = s.AccentRgb.G,
                    AccentB = s.AccentRgb.B,
                    s.AccentHex,
                    s.HoverBackgroundHex,
                    s.AlternateBackgroundHex
                })
            };
            return Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
        }

        // Validates and parses a custom scheme definition from a JSON string
        public static ColorSchemeDefinition? ParsePaletteJson(string json)
        {
            try
            {
                var dict = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(json);
                if (dict != null && dict.TryGetValue("Id", out var idObj) && dict.TryGetValue("DisplayName", out var nameObj))
                {
                    string id = idObj?.ToString() ?? "custom";
                    string displayName = nameObj?.ToString() ?? "Custom Scheme";
                    string css = dict.TryGetValue("CssFileName", out var cssObj) ? cssObj?.ToString() ?? "xdm-dark.css" : "xdm-dark.css";
                    string accent = dict.TryGetValue("AccentHex", out var accObj) ? accObj?.ToString() ?? "#3584e4" : "#3584e4";
                    string hover = dict.TryGetValue("HoverBackgroundHex", out var hovObj) ? hovObj?.ToString() ?? "#262c36" : "#262c36";
                    string alt = dict.TryGetValue("AlternateBackgroundHex", out var altObj) ? altObj?.ToString() ?? "#1f1f1f" : "#1f1f1f";
                    return new ColorSchemeDefinition(id, displayName, css, 53, 132, 228, accent, hover, alt);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Palette JSON parsing error: " + ex.Message);
            }
            return null;
        }

        // Toggles between Dark and Light mode, persisting choice to Config
        public static void ToggleTheme()
        {
            var newMode = IsDarkActive ? 0 : 1; // 0 = Light, 1 = Dark
            Config.Instance.ThemeMode = newMode;
            Config.SaveConfig();
            ApplyTheme(newMode == 1, Config.Instance.ColorScheme);
        }

        // Swaps the theme provider on the default screen and applies the GTK dark preference
        public static void ApplyTheme(bool? darkRequested, int? colorSchemeRequested = null)
        {
            bool dark = darkRequested ?? false;
            if (!darkRequested.HasValue)
            {
                try
                {
                    string themeName = Gtk.Settings.Default.ThemeName?.ToLowerInvariant() ?? "";
                    if (themeName.Contains("dark"))
                    {
                        dark = true;
                    }
                }
                catch { }
            }
            IsDarkActive = dark;

            // Resolve color scheme index
            int schemeIndex = colorSchemeRequested ?? Config.Instance.ColorScheme;
            var schemes = dark ? DarkSchemes : LightSchemes;
            if (schemeIndex < 0 || schemeIndex >= schemes.Length)
            {
                schemeIndex = 0;
            }
            ActiveColorScheme = schemeIndex;

            var scheme = schemes[schemeIndex];
            var cssFile = scheme.CssFileName;
            var cssPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ThemeDir, cssFile);

            // Fallback to base theme if specific scheme file is missing
            if (!System.IO.File.Exists(cssPath))
            {
                var fallbackFile = dark ? FallbackDarkCss : FallbackLightCss;
                cssPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ThemeDir, fallbackFile);
            }

            try
            {
                var screen = Gdk.Screen.Default;
                if (screen == null)
                {
                    Log.Debug("Non-fatal: no default screen; theme CSS not applied");
                }
                else if (!System.IO.File.Exists(cssPath))
                {
                    Log.Debug("Theme CSS not found: " + cssPath);
                }
                else
                {
                    // Load the new provider first so a failed load keeps the old theme intact
                    var provider = new CssProvider();
                    provider.LoadFromData(System.IO.File.ReadAllText(cssPath));
                    if (currentThemeProvider != null)
                    {
                        Gtk.StyleContext.RemoveProviderForScreen(screen, currentThemeProvider);
                        currentThemeProvider.Dispose();
                        currentThemeProvider = null;
                    }
                    Gtk.StyleContext.AddProviderForScreen(screen, provider, ThemeProviderPriority);
                    currentThemeProvider = provider;
                }
            }
            catch (Exception cssEx)
            {
                Log.Debug("Non-fatal: theme CSS provider not applied: " + cssEx.Message);
            }

            // GTK-level theme and dark preference: ensures Adwaita icons and CSD decorations resolve per mode
            try
            {
                Gtk.Settings.Default.ThemeName = AdwaitaThemeName;
                if (darkRequested == true || (darkRequested == null && dark))
                {
                    Gtk.Settings.Default.ApplicationPreferDarkTheme = true;
                }
                else
                {
                    if (darkRequested != null)
                    {
                        Gtk.Settings.Default.ApplicationPreferDarkTheme = false;
                    }
                }
            }
            catch (Exception settingsEx)
            {
                Log.Debug("Non-fatal: could not apply theme preference: " + settingsEx.Message);
            }

            ThemeChanged?.Invoke(dark);
        }
    }
}
