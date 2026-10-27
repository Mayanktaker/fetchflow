// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// SvgTint — recolours an SVG document to one RGB tone. Pure string work so the
// GTK 4 icon helper and its tests share the exact same pass list.
using System;
using System.Text.RegularExpressions;

namespace XDM.Core.UI
{
    public static class SvgTint
    {
        // Timeout guard for the unfilled-path regex (pathological input only)
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        // Rewrites fill and stroke colours so the icon follows one RGB tone
        public static string Apply(string svg, byte r, byte g, byte b)
        {
            if (string.IsNullOrEmpty(svg))
            {
                return svg;
            }

            var rgb = $"rgb({r},{g},{b})";
            var tinted = Regex.Replace(
                svg,
                @"fill=""rgba\(\s*128\s*,\s*128\s*,\s*128\s*,\s*1\s*\)""",
                $"fill=\"{rgb}\"");

            if (tinted == svg)
            {
                tinted = Regex.Replace(svg, @"fill=""#[0-9a-fA-F]{3,6}""", $"fill=\"{rgb}\"");
            }

            if (tinted == svg)
            {
                // Some icons carry no fill attribute and inherit black
                tinted = Regex.Replace(
                    svg,
                    @"<path(?![^>]*fill=)",
                    $"<path fill=\"{rgb}\"",
                    RegexOptions.None,
                    RegexTimeout);
            }

            // Checkbox SVGs are stroke-drawn: a fill-only pass leaves them untinted
            tinted = Regex.Replace(tinted, @"stroke=""rgba\([^)]*\)""", $"stroke=\"{rgb}\"");
            tinted = Regex.Replace(tinted, @"stroke=""#[0-9a-fA-F]{3,6}""", $"stroke=\"{rgb}\"");
            return tinted;
        }
    }
}
