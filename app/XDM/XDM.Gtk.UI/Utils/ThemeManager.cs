// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// ThemeManager — swaps the XDM CSS theme layer live (no restart) and keeps the GTK-level
// dark preference in sync. Cosmetic only: every GTK call is guarded the same way as the
// startup CSS in Program.cs, because Gdk.Screen.Default / Gtk.Settings.Default can throw
// MissingIntPtrCtorException under the GTK3 Wayland backend (GtkSharp binding quirk).
using System;
using Gtk;
using TraceLog;

namespace XDM.GtkUI.Utils
{
    public static class ThemeManager
    {
        // Theme CSS lives next to the app binary; priority 800 matches the font-size layer
        private const string ThemeDir = "theme";
        private const string DarkThemeCssFile = "xdm-dark.css";
        private const string LightThemeCssFile = "xdm-light.css";
        private const uint ThemeProviderPriority = 800;
        private const string AdwaitaThemeName = "Adwaita";

        // Provider currently attached to the default screen (null = none attached yet)
        private static CssProvider? currentThemeProvider;

        // Tracks whether the dark theme is actively rendered
        public static bool IsDarkActive { get; private set; }

        // Event raised whenever the active theme changes
        public static event Action<bool>? ThemeChanged;

        // Toggles between Dark and Light mode, persisting choice to Config
        public static void ToggleTheme()
        {
            var newMode = IsDarkActive ? 0 : 1; // 0 = Light, 1 = Dark
            XDM.Core.Config.Instance.ThemeMode = newMode;
            XDM.Core.Config.SaveConfig();
            ApplyTheme(newMode == 1);
        }

        // Swaps the theme provider on the default screen and applies the GTK dark preference
        public static void ApplyTheme(bool? darkRequested)
        {
            bool dark = darkRequested ?? false;
            if (!darkRequested.HasValue)
            {
                try
                {
                    string themeName = Gtk.Settings.Default.ThemeName?.ToLowerInvariant() ?? "";
                    if (themeName.Contains("dark"))
                        dark = true;
                }
                catch { }
            }
            IsDarkActive = dark;
            var cssFile = dark ? DarkThemeCssFile : LightThemeCssFile;
            var cssPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ThemeDir, cssFile);
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

            // GTK-level dark preference: lets libadwaita-style dark variants resolve per mode
            try
            {
                if (darkRequested == true || (darkRequested == null && dark))
                {
                    Gtk.Settings.Default.ThemeName = AdwaitaThemeName;
                    Gtk.Settings.Default.ApplicationPreferDarkTheme = true;
                }
                else
                {
                    if (darkRequested != null) Gtk.Settings.Default.ApplicationPreferDarkTheme = false;
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
