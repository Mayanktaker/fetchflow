// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// SchemeBrushMapper — color utilities for the WPF scheme layer: hex parsing via
// ColorConverter and luminance-based accent foreground selection (white text on
// dark accents, near-black text on bright accents like amber).
using System.Windows;
using System.Windows.Media;

namespace XDM.Wpf.UI.Utils
{
    // Maps ColorSchemeDefinition tokens to WPF colors and scheme layer brush keys
    public static class SchemeBrushMapper
    {
        // Rec. 601 luma weights used for accent contrast evaluation
        private const double LuminanceRedWeight = 0.299;
        private const double LuminanceGreenWeight = 0.587;
        private const double LuminanceBlueWeight = 0.114;
        // Accents brighter than this read better with near-black text
        private const double AccentForegroundLuminanceThreshold = 0.55;
        // Foreground over a light/bright accent (near-black)
        private const string ForegroundOnBrightAccentHex = "#1A1A1A";
        // Foreground over a dark/deep accent (white)
        private const string ForegroundOnDeepAccentHex = "#FFFFFF";
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

        // Picks the readable foreground hex for the given accent color
        public static string ForegroundHexFor(Color accent)
        {
            var luminance = (LuminanceRedWeight * accent.R
                + LuminanceGreenWeight * accent.G
                + LuminanceBlueWeight * accent.B) / 255.0;
            return luminance > AccentForegroundLuminanceThreshold
                ? ForegroundOnBrightAccentHex
                : ForegroundOnDeepAccentHex;
        }
    }
}
