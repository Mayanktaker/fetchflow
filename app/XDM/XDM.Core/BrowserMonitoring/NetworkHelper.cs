// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace XDM.Core.BrowserMonitoring
{
    public static class NetworkHelper
    {
        private static Dictionary<string, DateTime> referersToSkip = new();

        // Junk-URL noise (autocomplete, telemetry, SW, sticker CDNs); mirrors extension lists.
        private static readonly string[] NoiseUrlSubstrings = new[]
        {
            "complete/search", "/complete/s",
            "google.com/async/", "google.com/httpservice/",
            "getdatasyncids", "sw.js", "/api/timedtext",
            "generate_204", "gen_204",
            "/api/stats",
            "play.google.com/log", "google.com/log",
            "safebrowsing",
            "fbsbx.com",
            "_next/image",
        };

        public static bool IsNoiseUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            try
            {
                var low = url.ToLowerInvariant();
                foreach (var s in NoiseUrlSubstrings)
                {
                    if (low.Contains(s)) return true;
                }
            }
            catch { }
            return false;
        }

        public static string ComputeHash(string input)
        {
            using var sha1 = new SHA1Managed();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        public static void AddToSkippedRefererList(string? referer)
        {
            if (string.IsNullOrEmpty(referer)) return;
            lock (referersToSkip)
            {
                referersToSkip[ComputeHash(referer!)] = DateTime.Now;
            }
        }

        // True for progressive video/audio captures that belong in the extension
        // menu, not in an immediate New Download dialog (e.g. CDN episode bursts).
        public static bool IsStreamableMedia(string? url, string? file, string? mime, IEnumerable<string> videoExts)
        {
            var lowMime = (mime ?? string.Empty).ToLowerInvariant();
            if (lowMime.StartsWith("video/") || lowMime.StartsWith("audio/") ||
                lowMime.Contains("mpegurl") || lowMime.Contains("m3u8") ||
                lowMime.Contains("dash") || lowMime.Contains("mpd")) return true;

            var lowUrl = (url ?? string.Empty).ToLowerInvariant();
            if (lowUrl.Contains("videoplayback") || lowUrl.Contains(".m3u8") ||
                lowUrl.Contains(".mpd") || lowUrl.Contains("mime=video") ||
                lowUrl.Contains("mime=audio")) return true;

            var ext = GetMediaExtension(url, file);
            if (string.IsNullOrEmpty(ext)) return false;
            foreach (var vid in videoExts)
            {
                if (string.Equals(ext, vid, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // Extension from explicit filename first, else URL path (no query noise).
        public static string GetMediaExtension(string? url, string? file)
        {
            foreach (var candidate in new[] { file, TryGetUrlPath(url) })
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                var dot = candidate.LastIndexOf('.');
                if (dot < 0 || dot == candidate.Length - 1) continue;
                var ext = candidate.Substring(dot + 1);
                var cut = ext.IndexOfAny(new[] { '?', '#', '&', ';' });
                if (cut >= 0) ext = ext.Substring(0, cut);
                if (!string.IsNullOrEmpty(ext)) return ext.ToUpperInvariant();
            }
            return string.Empty;
        }

        // Path portion of a URL without query/fragment (null-safe for matching).
        private static string? TryGetUrlPath(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try { return new Uri(url).AbsolutePath; }
            catch { return null; }
        }

        public static bool IsRefererSkipped(string? referer)
        {
            if (string.IsNullOrEmpty(referer)) return false;
            var sha1 = ComputeHash(referer!);
            lock (referersToSkip)
            {
                if (referersToSkip.ContainsKey(sha1))
                {
                    referersToSkip[sha1] = DateTime.Now;
                    return true;
                }
            }
            return false;
        }
    }
}
