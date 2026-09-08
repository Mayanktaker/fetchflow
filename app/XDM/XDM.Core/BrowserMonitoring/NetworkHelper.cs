// © 2026 Mayanktaker | Based on XDM by subhra74 (https://github.com/subhra74/xdm)
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
