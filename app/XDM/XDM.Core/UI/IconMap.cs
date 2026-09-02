using System;
using System.Collections.Generic;
using System.IO;

namespace XDM.Core.UI
{
    public static class IconMap
    {
        private static Dictionary<string, HashSet<string>> imageTypes = new()
        {
            ["CAT_COMPRESSED"] = new HashSet<string> { ".zip", ".zipx", ".gz", ".tgz", ".tar", ".xz", ".txz", ".7z", ".rar", ".bz2", ".tbz2", ".zst", ".tzst", ".lz", ".lz4", ".lzh", ".cab", ".sit", ".sitx", ".ace", ".arj", ".z", ".iso", ".img", ".mdf", ".nrg", ".vhd", ".vhdx", ".vmdk", ".qcow2" },
            ["CAT_MUSIC"] = new HashSet<string> { ".mp3", ".aac", ".flac", ".alac", ".wav", ".aiff", ".aif", ".ape", ".m4a", ".ogg", ".oga", ".opus", ".wma", ".mpa", ".amr", ".ac3", ".dts", ".eac3", ".mka", ".mid", ".midi", ".wv", ".tta" },
            ["CAT_VIDEOS"] = new HashSet<string> { ".mp4", ".mkv", ".webm", ".avi", ".mov", ".wmv", ".flv", ".m4v", ".f4v", ".ts", ".mts", ".m2ts", ".tp", ".trp", ".mpg", ".mpeg", ".m2v", ".mpv", ".vob", ".divx", ".xvid", ".3gp", ".3g2", ".ogv", ".rm", ".rmvb", ".asf" },
            ["CAT_DOCUMENTS"] = new HashSet<string> { ".doc", ".docx", ".docm", ".dot", ".dotx", ".pdf", ".odt", ".ott", ".rtf", ".txt", ".md", ".tex", ".log", ".pages", ".xls", ".xlsx", ".xlsm", ".xlsb", ".ods", ".ots", ".csv", ".tsv", ".numbers", ".ppt", ".pptx", ".pps", ".ppsx", ".odp", ".otp", ".key", ".epub", ".mobi", ".azw", ".azw3", ".fb2", ".cbz", ".cbr", ".djvu", ".html" },
            ["CAT_PROGRAMS"] = new HashSet<string> { ".appimage", ".deb", ".rpm", ".flatpakref", ".flatpak", ".snap", ".apk", ".run", ".bin", ".sh", ".exe", ".msi", ".msix", ".appx", ".bat", ".cmd", ".pkg", ".dmg", ".jar", ".war", ".ApplicationContext.Core" },
            ["CAT_IMAGES"] = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".svgz", ".bmp", ".ico", ".tiff", ".tif", ".avif", ".heic", ".heif", ".psd", ".ai", ".eps", ".raw", ".cr2", ".nef", ".dng", ".xcf" }
        };

        public static string GetVectorNameForCategory(string categoryname)
        {
            return categoryname switch
            {
                "CAT_COMPRESSED" => "ri-file-zip-line",
                "CAT_MUSIC" => "ri-file-music-line",
                "CAT_VIDEOS" => "ri-movie-line",
                "CAT_DOCUMENTS" => "ri-file-text-line",
                "CAT_PROGRAMS" => "ri-microsoft-line",
                "CAT_IMAGES" => "ri-image-line",
                _ => "ri-file-line",
            };
        }

        private static string GetFileType(string ext)
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

        public static string GetVectorNameForFileType(string? file)
        {
            var ext = Path.GetExtension(file)?.ToLowerInvariant() ?? string.Empty;
            var fileType = GetFileType(ext);
            return fileType switch
            {
                "CAT_COMPRESSED" => "ri-file-zip-fill",
                "CAT_MUSIC" => "ri-file-music-fill",
                "CAT_VIDEOS" => "ri-movie-fill",
                "CAT_DOCUMENTS" => "ri-file-text-fill",
                "CAT_PROGRAMS" => "ri-microsoft-fill",
                "CAT_IMAGES" => "ri-image-fill",
                _ => "ri-file-fill",
            };
        }
    }
}
