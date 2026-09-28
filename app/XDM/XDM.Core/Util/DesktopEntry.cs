// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Pure, dependency-free XDG .desktop-entry helpers for launch-at-login.
// Kept separate from PlatformHelper so the parsing rules are unit-testable.
using System;

namespace XDM.Core.Util
{
    public static class DesktopEntry
    {
        /// <summary>Builds the autostart entry for a Linux login session.</summary>
        public static string BuildAutoStartEntry(string execPath, string iconPath, bool startInBackground = true)
        {
            var exec = startInBackground
                ? $"Exec=env GTK_USE_PORTAL=1 \"{execPath}\" --background\r\n"
                : $"Exec=env GTK_USE_PORTAL=1 \"{execPath}\"\r\n";

            // Encoding is omitted: UTF-8 is the desktop-entry default and the key is deprecated.
            return "[Desktop Entry]\r\n" +
                "Version=1.0\r\n" +
                "Type=Application\r\n" +
                "Terminal=false\r\n" +
                // TryExec lets the session skip a stale entry after a reinstall/move instead of erroring.
                $"TryExec={execPath}\r\n" +
                exec +
                "Name=FetchFlow Download Manager\r\n" +
                "Comment=FetchFlow Download Manager (Wayland Edition)\r\n" +
                "Categories=Network;\r\n" +
                $"Icon={iconPath}\r\n" +
                // Explicit opt-in marker: GNOME ignores bare .desktop files in autostart/ otherwise.
                "X-GNOME-Autostart-enabled=true\r\n" +
                "StartupNotify=false\r\n" +
                "X-FetchFlow-Autostart=1\r\n";
        }

        /// <summary>Reads a key from a desktop-entry file, tolerating whitespace and CRLF endings.</summary>
        public static bool TryGetValue(string? text, string key, out string? value)
        {
            value = null;
            if (text == null) return false;
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith(key + "=", StringComparison.Ordinal))
                {
                    value = line.Substring(key.Length + 1).Trim();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Convenience wrapper returning null instead of using an out parameter.</summary>
        public static string? GetValueOrNull(string? text, string key)
            => TryGetValue(text, key, out var value) ? value : null;

        /// <summary>Extracts the first quoted path, else the first bare token, from a command line.</summary>
        public static string? ExtractCommandPath(string? command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;

            var first = command!.IndexOf('"');
            if (first >= 0)
            {
                var second = command.IndexOf('"', first + 1);
                if (second > first)
                {
                    var quoted = command.Substring(first + 1, second - first - 1);
                    if (!string.IsNullOrEmpty(quoted)) return quoted;
                }
            }

            var tokens = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length > 0 ? tokens[0] : null;
        }

        /// <summary>
        /// Decides whether an autostart entry will really launch: it must be opted in, and its
        /// command must resolve to an existing file. A stale entry (moved or removed install)
        /// must read as disabled, otherwise Settings shows "on" while nothing happens at login.
        /// </summary>
        public static bool IsEntryLive(string? text, Func<string, bool> fileExists)
        {
            if (fileExists == null) return false;
            if (string.IsNullOrEmpty(text)) return false;

            // Respect an explicit opt-out written by GNOME's "Startup Applications" preferences.
            if (TryGetValue(text, "X-GNOME-Autostart-enabled", out var gnomeFlag)
                && string.Equals(gnomeFlag, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var target = ExtractCommandPath(GetValueOrNull(text, "Exec"))
                ?? ExtractCommandPath(GetValueOrNull(text, "TryExec"));
            return target != null && target.Length > 0 && fileExists(target);
        }
    }
}
