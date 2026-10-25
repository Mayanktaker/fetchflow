// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 theming. GTK 3 theme files cannot be reused (measured: they fail the
// GTK 4 CSS parser), so this frontend carries its own stylesheet built from the
// same tokens documented in docs/design.md.
//
// ThemeMode is the shared, cross-frontend value (Config.cs): 0 = Light, 1 = Dark,
// 2 = Follow System. libadwaita's AdwStyleManager owns the session preference, so
// Follow System tracks GNOME/KDE live and only an explicit user choice forces a mode.
using System;
using System.IO;
using Gtk;
using XDM.Core;
using XDM.Core.UI;

namespace XDM.Gtk4UI
{
    internal static class Gtk4Theme
    {
        // Shared Config.ThemeMode codes (single source: XDM.Core/UI/ThemeModes.cs)
        public const int ModeLight = ThemeModes.Light;
        public const int ModeDark = ThemeModes.Dark;
        public const int ModeFollowSystem = ThemeModes.FollowSystem;

        // Stylesheet directory relative to the executable
        private const string ThemeDir = "theme";
        // Token-matched stylesheets per mode
        private const string DarkCss = "gtk4-dark.css";
        private const string LightCss = "gtk4-light.css";
        // Same provider priority as the GTK 3 frontend
        private const uint ProviderPriority = 800;

        // Provider currently attached to the display (removed on swap)
        private static CssProvider? currentProvider;

        // libadwaita style manager driving dark/light and the session preference
        private static Adw.StyleManager? styleManager;
        // Guards against subscribing to the manager more than once
        private static bool listening;

        // Tracks the active mode so the toggle label and CSS agree
        public static bool IsDarkActive { get; private set; } = true;

        // Raw Config.ThemeMode value currently applied
        public static int ActiveMode { get; private set; } = ModeFollowSystem;

        // Scheme index pinned by FETCHFLOW_COLOR_SCHEME for previews and screenshots
        private static int? previewScheme;

        // Applies the mode saved in configuration. FETCHFLOW_THEME_MODE=light|dark
        // and FETCHFLOW_COLOR_SCHEME=<index> override it for previews and
        // screenshots, so a run can pin one theme/scheme without touching the
        // user's settings.
        public static void ApplyFromConfig(Widget anchor)
        {
            var mode = Environment.GetEnvironmentVariable("FETCHFLOW_THEME_MODE") switch
            {
                "light" => ModeLight,
                "dark" => ModeDark,
                _ => Config.Instance.ThemeMode
            };
            previewScheme = int.TryParse(Environment.GetEnvironmentVariable("FETCHFLOW_COLOR_SCHEME"), out var pinned)
                ? pinned
                : (int?)null;
            ApplyMode(anchor, mode, persist: false);
        }

        // Applies a mode, optionally persisting it, and re-applies on system changes
        public static void ApplyMode(Widget anchor, int mode, bool persist)
        {
            ActiveMode = ThemeModes.Clamp(mode);
            if (persist)
            {
                Config.Instance.ThemeMode = ActiveMode;
                try { Config.SaveConfig(); } catch { }
            }

            var manager = EnsureStyleManager();
            // Only an explicit choice forces a scheme; Follow System leaves it to the session
            manager.ColorScheme = ActiveMode switch
            {
                ModeDark => Adw.ColorScheme.ForceDark,
                ModeLight => Adw.ColorScheme.ForceLight,
                _ => Adw.ColorScheme.Default
            };

            Apply(anchor.GetDisplay() ?? Gdk.Display.GetDefault(), manager.Dark);
        }

        // Flips dark/light and persists the choice, which ends Follow System
        public static void Toggle(Widget anchor)
        {
            ApplyMode(anchor, IsDarkActive ? ModeLight : ModeDark, persist: true);
        }

        // Returns the style manager, subscribing to its notifications once
        private static Adw.StyleManager EnsureStyleManager()
        {
            if (styleManager == null)
            {
                styleManager = Adw.StyleManager.GetDefault();
            }
            if (!listening)
            {
                styleManager.OnNotify += OnStyleManagerNotify;
                listening = true;
            }
            return styleManager;
        }

        // Session preference changed: re-apply the stylesheet while following the system
        private static void OnStyleManagerNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
        {
            if (ActiveMode != ModeFollowSystem)
            {
                return;
            }

            var property = args.Pspec.GetName();
            if (property != "dark" && property != "system-supports-color-schemes")
            {
                return;
            }

            var dark = ((Adw.StyleManager)sender).Dark;
            GLib.Functions.IdleAdd(0, () =>
            {
                Apply(Gdk.Display.GetDefault(), dark);
                return false;
            });
        }

        // Rewrites the scheme tokens shared with GTK 3 and WPF: the accent, the
        // readable ink for text painted on it, its hover, and the card surfaces.
        private static string ApplyColorScheme(string css, bool dark)
        {
            try
            {
                var index = ColorSchemeTable.ClampSchemeIndex(dark, previewScheme ?? Config.Instance.ColorScheme);
                var scheme = ColorSchemeTable.GetScheme(dark, index);
                if (string.IsNullOrWhiteSpace(scheme.AccentHex))
                {
                    return css;
                }

                css = ReplaceToken(css, "accent", scheme.AccentHex);
                css = ReplaceToken(css, "accent_hover", ThemeContrast.HoverFor(scheme.AccentHex));
                css = ReplaceToken(css, "accent_foreground", ThemeContrast.InkFor(scheme.AccentHex));
                css = ReplaceToken(css, "row_bg", scheme.CardBackgroundHex);
                css = ReplaceToken(css, "row_hover", scheme.HoverBackgroundHex);
                return css;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("GTK 4 color scheme failed: " + ex.Message);
                return css;
            }
        }

        // Rewrites one @define-color token, leaving every other token untouched
        private static string ReplaceToken(string css, string token, string value)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                css, @"@define-color " + token + @" #[0-9a-fA-F]{3,8};",
                $"@define-color {token} {value};");
        }

        // Loads the mode's stylesheet and swaps the display provider
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
                var css = ApplyColorScheme(File.ReadAllText(path), dark);
                // Length is a BYTE count: non-ASCII (—, ·) in comments would truncate
                provider.LoadFromData(css, System.Text.Encoding.UTF8.GetByteCount(css));

                if (currentProvider != null)
                {
                    StyleContext.RemoveProviderForDisplay(display, currentProvider);
                }
                StyleContext.AddProviderForDisplay(display, provider, ProviderPriority);
                currentProvider = provider;

                // The GTK theme NAME is still never touched: AdwStyleManager only switches
                // the color scheme, so a Plasma (Breeze) or Cinnamon (Mint-Y) user keeps
                // their system theme — see FF-UI-001 in docs/audit/ui-ux-findings.md.
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("GTK 4 theme swap failed: " + ex.Message);
            }
        }
    }
}
