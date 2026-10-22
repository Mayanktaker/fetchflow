// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 theming. GTK 3 theme files cannot be reused (measured: they fail the
// GTK 4 CSS parser), so this frontend carries its own stylesheet built from the
// same tokens documented in docs/design.md.
using System;
using System.IO;
using Gtk;

namespace XDM.Gtk4UI
{
    internal static class Gtk4Theme
    {
        // Stylesheet directory relative to the executable
        private const string ThemeDir = "theme";
        // Token-matched stylesheets per mode
        private const string DarkCss = "gtk4-dark.css";
        private const string LightCss = "gtk4-light.css";
        // Same provider priority as the GTK 3 frontend
        private const uint ProviderPriority = 800;

        // Provider currently attached to the display (removed on swap)
        private static CssProvider? currentProvider;

        // Tracks the active mode so the toggle label and CSS agree
        public static bool IsDarkActive { get; private set; } = true;

        // Applies the stylesheet for the requested mode to the widget's display
        public static void Apply(Widget anchor, bool dark)
        {
            var display = anchor.GetDisplay() ?? Gdk.Display.GetDefault();
            Apply(display, dark);
        }

        // Flips dark/light using the default display
        public static void Toggle()
        {
            Apply(Gdk.Display.GetDefault(), !IsDarkActive);
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

                // Match the base theme's dark flag to the stylesheet, or unstyled widgets
                // keep the light Adwaita palette inside a dark window. The GTK theme NAME
                // is deliberately left alone so a Plasma (Breeze) or Cinnamon (Mint-Y) user
                // keeps their system theme — see FF-UI-001 in docs/audit/ui-ux-findings.md.
                // Match the base theme's color scheme to the stylesheet.
                // In GTK 4.20+, GtkInterfaceColorScheme replaces the deprecated GtkApplicationPreferDarkTheme (FF-UI-010).
                var settings = Settings.GetDefault();
                if (settings != null)
                {
                    try
                    {
                        settings.GtkInterfaceColorScheme = dark ? InterfaceColorScheme.Dark : InterfaceColorScheme.Light;
                    }
                    catch
                    {
                        // Fallback for GTK 4 environments before 4.20
                        settings.GtkApplicationPreferDarkTheme = dark;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("GTK 4 theme swap failed: " + ex.Message);
            }
        }
    }
}
