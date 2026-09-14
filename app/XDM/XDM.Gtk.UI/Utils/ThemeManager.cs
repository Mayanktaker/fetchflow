// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// ThemeManager — swaps the XDM CSS theme and color scheme layer live (no restart)
// and keeps the GTK-level dark preference in sync. Cosmetic only: every GTK call
// is guarded because Gdk.Screen.Default can throw under Wayland GtkSharp bindings.
// Scheme data lives in XDM.Core.UI.ColorSchemeTable (shared with the WPF frontend).
using System;
using Gtk;
using TraceLog;
using XDM.Core;
using XDM.Core.UI;

namespace XDM.GtkUI.Utils
{
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

        // Curated dark theme color schemes (shared table alias)
        public static ColorSchemeDefinition[] DarkSchemes => ColorSchemeTable.DarkSchemes;

        // Curated light theme color schemes (shared table alias)
        public static ColorSchemeDefinition[] LightSchemes => ColorSchemeTable.LightSchemes;

        // Provider currently attached to the default screen
        private static CssProvider? currentThemeProvider;

        // Tracks whether the dark theme is actively rendered
        public static bool IsDarkActive { get; private set; }

        // Default scheme per mode (fresh installs / unset choice)
        public static int DefaultDarkSchemeIndex => ColorSchemeTable.DefaultDarkSchemeIndex;   // Nord Emerald
        public static int DefaultLightSchemeIndex => ColorSchemeTable.DefaultLightSchemeIndex;  // Nordic Frost

        // Resolves the default scheme index for the given mode
        public static int DefaultSchemeIndex(bool isDark) =>
            ColorSchemeTable.DefaultSchemeIndex(isDark);

        // Tracks current active color scheme index
        public static int ActiveColorScheme { get; private set; } = 0;

        // Returns the active color scheme definition based on theme mode and scheme index
        public static ColorSchemeDefinition ActiveScheme => GetScheme(IsDarkActive, ActiveColorScheme);

        // Active primary accent RGB tuple
        public static (byte R, byte G, byte B) ActiveAccentColor => ActiveScheme.AccentRgb;

        // Active primary accent hex code
        public static string ActiveAccentHex => ActiveScheme.AccentHex;

        // Active TreeView row hover background color
        public static string ActiveHoverColor => ActiveScheme.HoverBackgroundHex;

        // Active TreeView row active/selected background color
        public static string ActiveRowColor => ActiveScheme.ActiveBackgroundHex;

        // Active TreeView alternating row subtle background color
        public static string ActiveAlternateRowColor => ActiveScheme.AlternateBackgroundHex;

        // Active plain card background color (checkbox gutter rail)
        public static string ActiveCardBackgroundHex => ActiveScheme.CardBackgroundHex;

        // Event raised whenever the active theme or color scheme changes
        public static event Action<bool>? ThemeChanged;

        // Gets scheme definition with safe index clamping (out-of-range falls back
        // to the mode default, not scheme 0)
        public static ColorSchemeDefinition GetScheme(bool isDark, int index) =>
            ColorSchemeTable.GetScheme(isDark, index);

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
                    s.ActiveBackgroundHex,
                    s.AlternateBackgroundHex,
                    s.CardBackgroundHex
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
                    s.ActiveBackgroundHex,
                    s.AlternateBackgroundHex,
                    s.CardBackgroundHex
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
                    string active = dict.TryGetValue("ActiveBackgroundHex", out var actObj) ? actObj?.ToString() ?? "#323b4a" : "#323b4a";
                    string alt = dict.TryGetValue("AlternateBackgroundHex", out var altObj) ? altObj?.ToString() ?? "#1f1f1f" : "#1f1f1f";
                    string card = dict.TryGetValue("CardBackgroundHex", out var cardObj) ? cardObj?.ToString() ?? "#262626" : "#262626";
                    return new ColorSchemeDefinition(id, displayName, css, 53, 132, 228, accent, hover, active, alt, card);
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

            // Resolve color scheme index (-1/unset or out-of-range => mode default)
            int schemeIndex = ColorSchemeTable.ClampSchemeIndex(dark, colorSchemeRequested ?? Config.Instance.ColorScheme);
            ActiveColorScheme = schemeIndex;

            var scheme = ColorSchemeTable.GetScheme(dark, schemeIndex);
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
