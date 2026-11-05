// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// The desktop's own accent colour, so a Plasma user following the system theme gets
// Breeze's accent in FetchFlow instead of a palette that fights their session.
//
// Only the parsing lives here: the frontends own the file access, so this stays
// pure and unit-testable (XDM.Tests links this file directly).
using System;
using System.Globalization;

namespace XDM.Core.UI
{
    // Reads the accent a Plasma session records, falling back to its selection colour
    public static class DesktopAccent
    {
        // Plasma names itself KDE or Plasma, optionally with a suffix: "KDE:Plasma"
        public static bool IsPlasmaSession(string? xdgCurrentDesktop)
        {
            if (string.IsNullOrWhiteSpace(xdgCurrentDesktop))
            {
                return false;
            }

            foreach (var id in xdgCurrentDesktop!.Split(':'))
            {
                var name = id.Trim();
                if (name.Equals("KDE", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("plasma", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // [General] AccentColor is Plasma 6's real accent; Plasma 5 only records the
        // selection background, which is the closest colour it ever exposed
        public static string? FromKdeGlobals(string? kdeGlobals)
        {
            if (string.IsNullOrWhiteSpace(kdeGlobals))
            {
                return null;
            }

            return NormalizeHex(ReadKey(kdeGlobals!, "General", "AccentColor"))
                ?? NormalizeHex(ReadKey(kdeGlobals!, "Colors:Selection", "BackgroundNormal"));
        }

        // KDE stores colours as "33,98,138"; the stylesheet wants "#21628a"
        public static string? NormalizeHex(string? rgb)
        {
            if (string.IsNullOrWhiteSpace(rgb))
            {
                return null;
            }

            var parts = rgb!.Split(',');
            if (parts.Length < 3)
            {
                return null;
            }

            var channels = new int[3];
            for (var i = 0; i < channels.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var channel) || channel < 0 || channel > 255)
                {
                    return null;
                }
                channels[i] = channel;
            }

            return $"#{channels[0]:x2}{channels[1]:x2}{channels[2]:x2}";
        }

        // First value of a key inside a group; groups and keys are case-insensitive
        private static string? ReadKey(string text, string group, string key)
        {
            var inGroup = false;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                {
                    continue;
                }

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    var name = line.Substring(1, line.Length - 2).Trim();
                    inGroup = name.Equals(group, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inGroup)
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                if (line.Substring(0, separator).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(separator + 1).Trim();
                }
            }

            return null;
        }
    }
}
