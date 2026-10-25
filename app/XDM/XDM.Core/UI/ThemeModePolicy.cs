// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// ThemeModePolicy — resolves "is this desktop in its dark variant?" for the
// Follow System theme mode. GTK 3 exposes that choice two ways and desktops pick
// different ones: a "-dark" theme name (Breeze-Dark, Mint-Y-Dark) or
// gtk-application-prefer-dark-theme (GNOME 42+, where the theme name stays
// "Adwaita"). Reading the name alone made Follow System paint light surfaces on
// a dark GNOME session, so both signals are consulted here. Pure function: no
// toolkit dependency, so the resolution is unit-testable without a display.
using System;

namespace XDM.Core.UI
{
    public static class ThemeModePolicy
    {
        // True when either signal reports the desktop's dark variant
        public static bool IsSessionDark(string? themeName, bool preferDarkTheme)
        {
            if (preferDarkTheme)
            {
                return true;
            }
            return !string.IsNullOrEmpty(themeName)
                && themeName.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
