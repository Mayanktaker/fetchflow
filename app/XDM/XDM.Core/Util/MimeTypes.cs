using System;
using System.Collections.Generic;
using System.Text;

namespace XDM.Core.Util
{
    internal static class MimeTypes
    {
        static Dictionary<string, string?> mimeBuilder = new();
        public static string? Get(string key)
        {
            if (mimeBuilder.TryGetValue(key, out var ext))
            {
                return ext;
            }
            return null;
        }
        static MimeTypes()
        {
            mimeBuilder["application/x-msdownload"] = "dll";
            mimeBuilder["image/jpeg"] = "jpeg";
            mimeBuilder["image/bmp"] = "bmp";
            mimeBuilder["image/gif"] = "gif";
            mimeBuilder["image/x-icon"] = "ico";
            mimeBuilder["image/svg+xml"] = "svg";
            mimeBuilder["application/x-compressed"] = "tgz";
            mimeBuilder["application/x-shockwave-flash"] = "swf";
            mimeBuilder["video/x-msvideo"] = "avi";
            mimeBuilder["application/postscript"] = "ps";
            mimeBuilder["video/x-flv"] = "flv";
            mimeBuilder["audio/x-wav"] = "wav";
            mimeBuilder["application/vnd.ms-excel"] = "xls";
            mimeBuilder["audio/basic"] = "au";
            mimeBuilder["audio/x-aiff"] = "aiff";
            mimeBuilder["text/plain"] = "txt";
            mimeBuilder["application/x-gzip"] = "gz";
            mimeBuilder["application/msword"] = "doc";
            mimeBuilder["application/pdf"] = "pdf";
            mimeBuilder["application/x-compress"] = "z";
            mimeBuilder["application/x-javascript"] = "js";
            mimeBuilder["video/3gpp"] = "3gp";
            mimeBuilder["audio/mid"] = "mid";
            mimeBuilder["application/x-cpio"] = "cpio";
            mimeBuilder["application/vnd.ms-powerpoint"] = "ppt";
            mimeBuilder["audio/mpeg"] = "mp3";
            mimeBuilder["application/rtf"] = "rtf";
            mimeBuilder["application/x-tar"] = "tar";
            mimeBuilder["video/x-ms-wmv"] = "wmv";
            mimeBuilder["application/x-bcpio"] = "bcpio";
            mimeBuilder["text/html"] = "html";
            mimeBuilder["video/mpeg"] = "mpeg";
            mimeBuilder["image/tiff"] = "tiff";
            mimeBuilder["application/x-stuffit"] = "sit";
            mimeBuilder["application/zip"] = "zip";
            mimeBuilder["text/css"] = "css";
            mimeBuilder["application/x-gtar"] = "gtar";
            mimeBuilder["video/quicktime"] = "qt";
            mimeBuilder["video/flv"] = "flv";
            mimeBuilder["video/mp4"] = "mp4";
            mimeBuilder["video/mp2t"] = "ts";
            mimeBuilder["video/mp2t"] = "ts";
            mimeBuilder["video/x-matroska"] = "mkv";
            mimeBuilder["audio/mp4"] = "mp4";
            mimeBuilder["audio/mp2t"] = "ts";
            mimeBuilder["audio/x-matroska"] = "mkv";
            mimeBuilder["video/webm"] = "webm";
            mimeBuilder["audio/webm"] = "webm";
            // Linux and packaging
            mimeBuilder["application/vnd.appimage"] = "appimage";
            mimeBuilder["application/x-appimage"] = "appimage";
            mimeBuilder["application/vnd.debian.binary-package"] = "deb";
            mimeBuilder["application/x-debian-package"] = "deb";
            mimeBuilder["application/x-deb"] = "deb";
            mimeBuilder["application/x-rpm"] = "rpm";
            mimeBuilder["application/x-redhat-package-manager"] = "rpm";
            mimeBuilder["application/vnd.flatpak"] = "flatpak";
            mimeBuilder["application/vnd.flatpak.ref"] = "flatpakref";
            mimeBuilder["application/vnd.snap"] = "snap";
            mimeBuilder["application/vnd.android.package-archive"] = "apk";
            mimeBuilder["application/x-msdos-program"] = "exe";
            mimeBuilder["application/x-msi"] = "msi";
            mimeBuilder["application/x-sh"] = "sh";
            mimeBuilder["application/java-archive"] = "jar";
            // Modern images
            mimeBuilder["image/png"] = "png";
            mimeBuilder["image/webp"] = "webp";
            mimeBuilder["image/avif"] = "avif";
            mimeBuilder["image/heic"] = "heic";
            mimeBuilder["image/heif"] = "heif";
            mimeBuilder["image/vnd.adobe.photoshop"] = "psd";
            mimeBuilder["image/x-xcf"] = "xcf";
            // Modern archives
            mimeBuilder["application/x-7z-compressed"] = "7z";
            mimeBuilder["application/x-rar-compressed"] = "rar";
            mimeBuilder["application/vnd.rar"] = "rar";
            mimeBuilder["application/x-xz"] = "xz";
            mimeBuilder["application/x-bzip2"] = "bz2";
            mimeBuilder["application/zstd"] = "zst";
            mimeBuilder["application/x-iso9660-image"] = "iso";
            // Modern audio & video
            mimeBuilder["audio/flac"] = "flac";
            mimeBuilder["audio/x-flac"] = "flac";
            mimeBuilder["audio/ogg"] = "ogg";
            mimeBuilder["audio/opus"] = "opus";
            mimeBuilder["audio/aac"] = "aac";
            mimeBuilder["audio/x-m4a"] = "m4a";
            mimeBuilder["video/ogg"] = "ogv";
            mimeBuilder["video/3gpp2"] = "3g2";
            // Documents
            mimeBuilder["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = "docx";
            mimeBuilder["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = "xlsx";
            mimeBuilder["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = "pptx";
            mimeBuilder["application/vnd.oasis.opendocument.text"] = "odt";
            mimeBuilder["application/vnd.oasis.opendocument.spreadsheet"] = "ods";
            mimeBuilder["application/vnd.oasis.opendocument.presentation"] = "odp";
            mimeBuilder["application/epub+zip"] = "epub";
            mimeBuilder["application/x-mobipocket-ebook"] = "mobi";
            mimeBuilder["application/x-cbr"] = "cbr";
            mimeBuilder["application/x-cbz"] = "cbz";
            mimeBuilder["image/vnd.djvu"] = "djvu";
            mimeBuilder["text/markdown"] = "md";
            mimeBuilder["text/csv"] = "csv";
        }
    }
}
