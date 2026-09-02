using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XDM.Core
{
    public static class IconResource
    {
        private static Dictionary<string, HashSet<string>> imageTypes = new()
        {
            ["Compressed"] = new HashSet<string> { ".zip", ".zipx", ".gz", ".tgz", ".tar", ".xz", ".txz", ".7z", ".rar", ".bz2", ".tbz2", ".zst", ".tzst", ".lz", ".lz4", ".lzh", ".cab", ".sit", ".sitx", ".ace", ".arj", ".z", ".iso", ".img", ".mdf", ".nrg", ".vhd", ".vhdx", ".vmdk", ".qcow2" },
            ["Music"] = new HashSet<string> { ".mp3", ".aac", ".flac", ".alac", ".wav", ".aiff", ".aif", ".ape", ".m4a", ".ogg", ".oga", ".opus", ".wma", ".mpa", ".amr", ".ac3", ".dts", ".eac3", ".mka", ".mid", ".midi", ".wv", ".tta" },
            ["Video"] = new HashSet<string> { ".mp4", ".mkv", ".webm", ".avi", ".mov", ".wmv", ".flv", ".m4v", ".f4v", ".ts", ".mts", ".m2ts", ".tp", ".trp", ".mpg", ".mpeg", ".m2v", ".mpv", ".vob", ".divx", ".xvid", ".3gp", ".3g2", ".ogv", ".rm", ".rmvb", ".asf" },
            ["Document"] = new HashSet<string> { ".doc", ".docx", ".docm", ".dot", ".dotx", ".pdf", ".odt", ".ott", ".rtf", ".txt", ".md", ".tex", ".log", ".pages", ".xls", ".xlsx", ".xlsm", ".xlsb", ".ods", ".ots", ".csv", ".tsv", ".numbers", ".ppt", ".pptx", ".pps", ".ppsx", ".odp", ".otp", ".key", ".epub", ".mobi", ".azw", ".azw3", ".fb2", ".cbz", ".cbr", ".djvu", ".html" },
            ["Application"] = new HashSet<string> { ".appimage", ".deb", ".rpm", ".flatpakref", ".flatpak", ".snap", ".apk", ".run", ".bin", ".sh", ".exe", ".msi", ".msix", ".appx", ".bat", ".cmd", ".pkg", ".dmg", ".jar", ".war", ".ApplicationContext.Core" },
            ["Image"] = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".svgz", ".bmp", ".ico", ".tiff", ".tif", ".avif", ".heic", ".heif", ".psd", ".ai", ".eps", ".raw", ".cr2", ".nef", ".dng", ".xcf" }
        };

        public static string GetFileType(string ext)
        {
            foreach (var key in imageTypes.Keys)
            {
                var extList = imageTypes[key];
                if (extList.Contains(ext))
                {
                    return key;
                }
            }
            return "Other";
        }

        public static string GetFontIconForFileType(string file)
        {
            var ext = Path.GetExtension(file)?.ToLowerInvariant() ?? string.Empty;
            var fileType = GetFileType(ext);
            return fileType switch
            {
                "Compressed" => RemixIcon.GetFontIcon(RemixIcon.ArchiveIcon),
                "Music" => RemixIcon.GetFontIcon(RemixIcon.MusicIcon),
                "Video" => RemixIcon.GetFontIcon(RemixIcon.VideoIcon),
                "Document" => RemixIcon.GetFontIcon(RemixIcon.DocumentIcon),
                "Application" or "ApplicationContext.Core" => RemixIcon.GetFontIcon(RemixIcon.AppIcon),
                "Image" => RemixIcon.GetFontIcon(RemixIcon.ImageIcon),
                _ => RemixIcon.GetFontIcon(RemixIcon.OtherFileIcon),
            };
        }

        public static string GetSVGNameForFileType(string file)
        {
            var ext = Path.GetExtension(file)?.ToLowerInvariant() ?? string.Empty;
            var fileType = GetFileType(ext);
            return fileType switch
            {
                "Compressed" => "file-zip-line",
                "Music" => "file-music-line",
                "Video" => "movie-line",
                "Document" => "file-text-line",
                "Application" or "ApplicationContext.Core" => "function-line",
                "Image" => "image-line",
                _ => "file-line",
            };
        }
    }
}
