// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// SchemeBrushMapper — color utilities for the WPF scheme layer: hex parsing via
// ColorConverter, plus the shared contrast rule (XDM.Core.UI.ThemeContrast) that
// decides the label colour for an accent instead of a hand-tuned luminance guess.
using System.Windows;
using System.Windows.Media;
using XDM.Core.UI;

namespace XDM.Wpf.UI.Utils
{
    // Maps ColorSchemeDefinition tokens to WPF colors and scheme layer brush keys
    public static class SchemeBrushMapper
    {
        // Neutral fallback when a scheme hex cannot be parsed
        private const string FallbackAccentHex = "#3584E4";

        // Every resource key the runtime scheme layer overrides (strings + SystemColors key)
        public static readonly object[] SchemeOverrideKeys = new object[]
        {
            "AccentBrush",
            "AccentForegroundBrush",
            "RowHoverBrush",
            "RowActiveBrush",
            "RowAlternateBrush",
            "CardBackgroundBrush",
            "CardBorderBrush",
            "HeaderBarBackgroundBrush",
            "BaseSurfaceBrush",
            "CategoryHighlight",
            "ListViewSelectedBackcolor",
            SystemColors.HighlightBrushKey,
            "ListViewMouseOverBackcolor",
            "TabSelectionColor",
            "ProgressBarForecolor",
            "TextFocusedBorder",
            "TextMouseOverBorder",
            "ButtonFocusedBorder",
            "HyperlinkForecolor"
        };

        // Parses a scheme hex string (#RRGGBB) into a WPF Color
        public static Color ParseColor(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return (Color)ColorConverter.ConvertFromString(FallbackAccentHex);
            }
        }

        // Picks the readable foreground hex for the given accent, using the same
        // WCAG rule as the GTK frontends so a scheme cannot differ across platforms
        public static string ForegroundHexFor(Color accent)
        {
            var hex = $"#{accent.R:x2}{accent.G:x2}{accent.B:x2}";
            return ThemeContrast.InkFor(hex);
        }
    }
}
