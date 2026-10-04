// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System.Globalization;

namespace XDM.Core.UI
{
    /// <summary>
    /// Resolves a category's tree icon name and its RGB colour. Pure and GTK-free so it
    /// is unit-testable, extracted from MainWindow.GetCategoryIconConfig which mixed
    /// this decision table into the 3000-line window. Colours are the same bytes the
    /// GTK CSS palette uses; keep them in sync with ThemePaletteHelper.
    /// </summary>
    public static class CategoryIconPalette
    {
        // Default category icon (Remix Icon line icons).
        private const string IconDocuments = "file-text-line";
        private const string IconMusic = "file-music-line";
        private const string IconVideos = "movie-line";
        private const string IconCompressed = "file-zip-line";
        private const string IconPrograms = "function-line";
        private const string IconImages = "image-line";
        private const string IconOther = "folder-shared-line";

        /// <summary>Resolves the icon name for a category, honouring a custom override.</summary>
        public static string GetIconName(Category cat)
        {
            if (!string.IsNullOrEmpty(cat.CustomIcon)) return cat.CustomIcon!;

            switch (cat.Name)
            {
                case "CAT_DOCUMENTS": return IconDocuments;
                case "CAT_MUSIC": return IconMusic;
                case "CAT_VIDEOS": return IconVideos;
                case "CAT_COMPRESSED": return IconCompressed;
                case "CAT_PROGRAMS": return IconPrograms;
                case "CAT_IMAGES": return IconImages;
                default: return IconOther;
            }
        }

        /// <summary>
        /// True when <paramref name="hex"/> is a #RRGGBB triple. Length and prefix are
        /// checked before parsing so a malformed value can never fall through as a
        /// partially-decoded colour.
        /// </summary>
        public static bool TryParseHexColor(string? hex, out byte r, out byte g, out byte b)
        {
            r = g = b = 0;
            if (string.IsNullOrEmpty(hex)) return false;
            if (hex![0] != '#' || hex.Length != 7) return false;

            if (!byte.TryParse(hex.Substring(1, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out r)) return false;
            if (!byte.TryParse(hex.Substring(3, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out g)) return false;
            if (!byte.TryParse(hex.Substring(5, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out b)) return false;

            return true;
        }

        /// <summary>The built-in colour for a category name; Indigo for anything unknown.</summary>
        public static (byte r, byte g, byte b) GetDefaultColor(string? categoryName)
        {
            switch (categoryName)
            {
                case "CAT_DOCUMENTS": return (245, 158, 11);    // Amber / Orange
                case "CAT_MUSIC": return (168, 85, 247);       // Purple / Violet
                case "CAT_VIDEOS": return (239, 68, 68);       // Coral / Red
                case "CAT_COMPRESSED": return (20, 184, 166);  // Teal / Cyan
                case "CAT_PROGRAMS": return (99, 102, 241);    // Indigo / Blue
                case "CAT_IMAGES": return (244, 63, 94);       // Rose / Pink
                default: return (99, 102, 241);                // Indigo / Blue
            }
        }

        /// <summary>
        /// Full resolution: a valid custom #RRGGBB wins, otherwise the built-in colour
        /// for the category is used. Icon name and colour are resolved independently,
        /// so a category can override just one of them.
        /// </summary>
        public static (string iconName, byte r, byte g, byte b) Resolve(Category cat)
        {
            var iconName = GetIconName(cat);
            if (TryParseHexColor(cat.CustomColor, out var cr, out var cg, out var cb))
            {
                return (iconName, cr, cg, cb);
            }

            var (r, g, b) = GetDefaultColor(cat.Name);
            return (iconName, r, g, b);
        }
    }
}