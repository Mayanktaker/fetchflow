// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// ThemeModes — the one definition of Config.ThemeMode and of the conversions each
// frontend used to re-derive inline: GTK 3 had `== 2 ? null : (bool?)(== 1)` in six
// places, GTK 4 declared its own Mode* constants, WPF declared them twice. Config
// stores an int, so a wrong code is a silent runtime wrong-theme, not a build error.
// Keep this plain C# 7.3 syntax: the same file is compiled into the net4.7.2 WPF
// frontend.
namespace XDM.Core.UI
{
    public static class ThemeModes
    {
        // Mode that always paints light, ignoring the desktop
        public const int Light = 0;
        // Mode that always paints dark, ignoring the desktop
        public const int Dark = 1;
        // Mode that follows the desktop's own preference (the default)
        public const int FollowSystem = 2;

        // Dark flag a mode asks for: null means "ask the desktop", not "light"
        public static bool? ToDarkRequest(int mode)
        {
            if (mode == Dark) return true;
            if (mode == Light) return false;
            return null;
        }

        // Mode that pins this light/dark choice, ending Follow System
        public static int FromDark(bool isDark) => isDark ? Dark : Light;

        // Stored value -> valid mode; unset or out-of-range follows the desktop
        public static int Clamp(int mode) =>
            mode == Light || mode == Dark ? mode : FollowSystem;
    }
}
