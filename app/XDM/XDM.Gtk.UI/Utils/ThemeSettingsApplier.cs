// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// ThemeSettingsApplier — the GTK-level half of a theme change: which desktop theme
// name and prefer-dark flag the chosen mode is allowed to write. Kept separate
// from ThemeManager so the write rules can be exercised against a real
// Gtk.Settings in the GtkSmoke lane, without pulling Config, SQLite or the whole
// window into the test build. Pure decisions live in XDM.Core/UI/ThemeModePolicy.
using Gtk;

namespace XDM.GtkUI.Utils
{
    public static class ThemeSettingsApplier
    {
        // Standard GTK3 Adwaita base theme name, used only for an explicit mode
        public const string AdwaitaThemeName = "Adwaita";

        // An explicit Light/Dark choice owns the desktop theme; Follow System must not
        public static bool ForcesDesktopTheme(bool? modeDark) => modeDark.HasValue;

        // Flag to write: the explicit choice, else the desktop's own value
        public static bool? PreferDarkToWrite(bool? modeDark, bool? sessionPreference) =>
            modeDark ?? sessionPreference;

        // Writes the mode to the toolkit. Follow System leaves the name alone; it only
        // hands the flag back when the caller switched modes, never on a re-derive.
        public static void Apply(Settings? settings, bool? modeDark, bool? sessionPreference,
            bool restoreSessionPreference = true)
        {
            if (settings == null) return;
            if (ForcesDesktopTheme(modeDark))
            {
                settings.ThemeName = AdwaitaThemeName;
            }
            // A re-derive is read-only: writing the flag back would clobber a desktop
            // change that is arriving in two steps (name first, prefer-dark next).
            var preferDark = PreferDarkToWrite(modeDark,
                restoreSessionPreference ? sessionPreference : null);
            if (preferDark.HasValue)
            {
                settings.ApplicationPreferDarkTheme = preferDark.Value;
            }
        }
    }
}
