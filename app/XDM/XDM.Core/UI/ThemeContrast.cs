// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// ThemeContrast — WCAG relative-luminance math shared by every frontend, so a
// colour scheme's accent can never be paired with text that cannot be read.
// Pure functions only (no UI toolkit): GTK 3 precomputes its literal hex values
// with the same rule (scripts/theme-contrast.py regenerates them), GTK 4 folds
// InkFor/HoverFor into its accent_foreground and accent_hover tokens, and WPF
// maps the same values onto AccentForegroundBrush.
//
// The floor is 3.0:1 — WCAG AA for large text. Every accent-painted surface in
// this app carries semibold control labels (11-14px), which is the closest
// honest fit; body copy on plain surfaces keeps the stricter 4.5:1 floor and is
// not derived here.
using System;

namespace XDM.Core.UI
{
    public static class ThemeContrast
    {
        // Minimum contrast for a label painted directly on a scheme accent fill
        public const double MinControlContrast = 3.0;
        // An accent hover is a deeper accent, so the fill keeps its identity
        public const double HoverScale = 0.88;
        // Text on a fill too bright for white, and on a fill too deep for ink
        public const string DarkInkHex = "#1a1a1a";
        public const string LightInkHex = "#ffffff";

        // Parses #rgb or #rrggbb into 0-255 components
        public static bool TryParseHex(string hex, out int r, out int g, out int b)
        {
            r = g = b = 0;
            if (string.IsNullOrEmpty(hex))
            {
                return false;
            }

            var value = hex.TrimStart('#');
            if (value.Length == 3)
            {
                value = new string(new[] { value[0], value[0], value[1], value[1], value[2], value[2] });
            }
            if (value.Length != 6)
            {
                return false;
            }

            return int.TryParse(value.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out r)
                && int.TryParse(value.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out g)
                && int.TryParse(value.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out b);
        }

        // WCAG 2.1 relative luminance of a single colour
        public static bool TryLuminance(string hex, out double luminance)
        {
            luminance = 0;
            if (!TryParseHex(hex, out var r, out var g, out var b))
            {
                return false;
            }

            luminance = 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
            return true;

            // One sRGB channel on the linear-light scale
            double Channel(int value)
            {
                var c = value / 255.0;
                return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
        }

        // WCAG contrast ratio between two colours (1.0 = identical, 21.0 = black on white)
        public static bool TryContrast(string foregroundHex, string backgroundHex, out double ratio)
        {
            ratio = 0;
            if (!TryLuminance(foregroundHex, out var a) || !TryLuminance(backgroundHex, out var b))
            {
                return false;
            }

            var lighter = Math.Max(a, b);
            var darker = Math.Min(a, b);
            ratio = (lighter + 0.05) / (darker + 0.05);
            return true;
        }

        // Ink for a label sitting on the given fill: white while it clears the
        // floor, near-black once the fill is too bright for white to carry
        public static string InkFor(string fillHex)
        {
            return TryContrast(LightInkHex, fillHex, out var onWhite) && onWhite >= MinControlContrast
                ? LightInkHex
                : DarkInkHex;
        }

        // A deeper accent used for hover, chosen so InkFor(accent) stays readable
        public static string HoverFor(string accentHex)
        {
            var ink = InkFor(accentHex);
            var deeper = Scale(accentHex, HoverScale);
            if (TryContrast(deeper, ink, out var ratio) && ratio >= MinControlContrast + 0.2)
            {
                return deeper;
            }

            // Fall back to nudging the accent away from its ink until it reads
            var inkIsLight = ink == LightInkHex;
            var candidate = accentHex;
            for (var step = 0; step < 24; step++)
            {
                if (TryContrast(candidate, ink, out var current) && current >= MinControlContrast + 0.2)
                {
                    return candidate;
                }
                candidate = Nudge(candidate, inkIsLight);
            }
            return candidate;
        }

        // Multiplies every channel by factor, clamped to a valid hex
        public static string Scale(string hex, double factor)
        {
            if (!TryParseHex(hex, out var r, out var g, out var b))
            {
                return hex;
            }
            return $"#{Clamp(r * factor):x2}{Clamp(g * factor):x2}{Clamp(b * factor):x2}";

            // Rounds a scaled channel back into 0-255 (round, not truncate, so the
            // value matches scripts/theme-contrast.py bit for bit)
            int Clamp(double value) => value < 0 ? 0 : value > 255 ? 255 : (int)Math.Round(value);
        }

        // Moves a fill one step away from its ink: darker under white, lighter under ink
        private static string Nudge(string hex, bool underLightInk)
        {
            if (!TryParseHex(hex, out var r, out var g, out var b))
            {
                return hex;
            }
            if (underLightInk)
            {
                r = (int)(r * 0.95);
                g = (int)(g * 0.95);
                b = (int)(b * 0.95);
            }
            else
            {
                r = Math.Min(255, r + (int)((255 - r) * 0.08));
                g = Math.Min(255, g + (int)((255 - g) * 0.08));
                b = Math.Min(255, b + (int)((255 - b) * 0.08));
            }
            return $"#{r:x2}{g:x2}{b:x2}";
        }
    }
}
