// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 theming. GTK 3 theme files cannot be reused (measured: they fail the
// GTK 4 CSS parser), so this frontend carries its own stylesheet built from the
// same tokens documented in docs/design.md.
//
// ThemeMode is the shared, cross-frontend value (Config.cs): 0 = Light, 1 = Dark,
// 2 = Follow System. Follow System reads the session preference and keeps tracking
// it, so the app matches GNOME/KDE instead of forcing a mode on the user.
using System;
using System.IO;
using Gtk;
using XDM.Core;

namespace XDM.Gtk4UI
{
    internal static class Gtk4Theme
    {
        // Shared Config.ThemeMode codes (must match Config.cs and the WPF frontend)
        public const int ModeLight = 0;
        public const int ModeDark = 1;
        public const int ModeFollowSystem = 2;

        // Stylesheet directory relative to the executable
        private const string ThemeDir = "theme";
        // Token-matched stylesheets per mode
        private const string DarkCss = "gtk4-dark.css";
        private const string LightCss = "gtk4-light.css";
        // Same provider priority as the GTK 3 frontend
        private const uint ProviderPriority = 800;

        // Provider currently attached to the display (removed on swap)
        private static CssProvider? currentProvider;

        // GtkSettings watched while following the system preference
        private static Settings? followedSettings;

        // Tracks the active mode so the toggle label and CSS agree
        public static bool IsDarkActive { get; private set; } = true;

        // Raw Config.ThemeMode value currently applied
        public static int ActiveMode { get; private set; } = ModeFollowSystem;

        // Applies the mode saved in configuration
        public static void ApplyFromConfig(Widget anchor)
        {
            ApplyMode(anchor, Config.Instance.ThemeMode, persist: false);
        }

        // Applies a mode, optionally persisting it, and re-applies on system changes
        public static void ApplyMode(Widget anchor, int mode, bool persist)
        {
            ActiveMode = Math.Clamp(mode, ModeLight, ModeFollowSystem);
            if (persist)
            {
                Config.Instance.ThemeMode = ActiveMode;
                try { Config.SaveConfig(); } catch { }
            }

            WatchSystemPreference(anchor);
            var dark = ActiveMode switch
            {
                ModeDark => true,
                ModeLight => false,
                _ => IsSystemDark()
            };
            Apply(anchor.GetDisplay() ?? Gdk.Display.GetDefault(), dark);
        }

        // Flips dark/light and persists the choice, which ends Follow System
        public static void Toggle(Widget anchor)
        {
            var next = IsDarkActive ? ModeLight : ModeDark;
            ApplyMode(anchor, next, persist: true);
        }

        // Reads the session's dark preference (GTK 4.20+ property, older fallback)
        private static bool IsSystemDark()
        {
            var settings = Settings.GetDefault();
            if (settings == null)
            {
                return true;
            }

            try
            {
                // Default means "no session preference": fall back to the legacy flag
                return settings.GtkInterfaceColorScheme switch
                {
                    InterfaceColorScheme.Dark => true,
                    InterfaceColorScheme.Light => false,
                    _ => settings.GtkApplicationPreferDarkTheme
                };
            }
            catch
            {
                return settings.GtkApplicationPreferDarkTheme;
            }
        }

        // Re-applies the stylesheet whenever the session preference changes while following it
        private static void WatchSystemPreference(Widget anchor)
        {
            var settings = Settings.GetDefault();
            if (settings == null)
            {
                return;
            }

            if (followedSettings != settings)
            {
                if (followedSettings != null)
                {
                    followedSettings.OnNotify -= OnSettingsNotify;
                }
                followedSettings = settings;
                followedSettings.OnNotify += OnSettingsNotify;
            }
            anchorOnSystemFollow = ActiveMode == ModeFollowSystem;
        }

        // Set by WatchSystemPreference: only Follow System needs live re-application
        private static bool anchorOnSystemFollow;

        // Session preference changed: re-apply only while following the system
        private static void OnSettingsNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
        {
            if (!anchorOnSystemFollow)
            {
                return;
            }
            if (args.Pspec.GetName() != "gtk-interface-color-scheme" &&
                args.Pspec.GetName() != "gtk-application-prefer-dark-theme")
            {
                return;
            }
            GLib.Functions.IdleAdd(0, () =>
            {
                Apply(Gdk.Display.GetDefault(), IsSystemDark());
                return false;
            });
        }

        // Loads the mode's stylesheet, swaps the display provider, syncs the base theme flag
        private static void Apply(Gdk.Display? display, bool dark)
        {
            IsDarkActive = dark;
            if (display == null)
            {
                return;
            }

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ThemeDir,
                dark ? DarkCss : LightCss);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine("GTK 4 theme not found: " + path);
                return;
            }

            try
            {
                // Load first, then swap: a failed load keeps the previous theme
                var provider = CssProvider.New();
                var css = File.ReadAllText(path);
                // Length is a BYTE count: non-ASCII (—, ·) in comments would truncate
                provider.LoadFromData(css, System.Text.Encoding.UTF8.GetByteCount(css));

                if (currentProvider != null)
                {
                    StyleContext.RemoveProviderForDisplay(display, currentProvider);
                }
                StyleContext.AddProviderForDisplay(display, provider, ProviderPriority);
                currentProvider = provider;

                // Keep the base theme's dark flag in step with the stylesheet, or unstyled
                // widgets keep the light Adwaita palette inside a dark window. The GTK theme
                // NAME is deliberately left alone so a Plasma (Breeze) or Cinnamon (Mint-Y)
                // user keeps their system theme — see FF-UI-001 in docs/audit/ui-ux-findings.md.
                // In Follow System mode the session property is left untouched on purpose.
                if (ActiveMode != ModeFollowSystem)
                {
                    ApplySessionPreference(dark);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("GTK 4 theme swap failed: " + ex.Message);
            }
        }

        // Forces the base theme's color scheme without changing the theme name
        private static void ApplySessionPreference(bool dark)
        {
            var settings = Settings.GetDefault();
            if (settings == null)
            {
                return;
            }

            try
            {
                // GTK 4.20+ replaces the deprecated GtkApplicationPreferDarkTheme (FF-UI-010)
                settings.GtkInterfaceColorScheme = dark ? InterfaceColorScheme.Dark : InterfaceColorScheme.Light;
            }
            catch
            {
                settings.GtkApplicationPreferDarkTheme = dark;
            }
        }
    }
}
