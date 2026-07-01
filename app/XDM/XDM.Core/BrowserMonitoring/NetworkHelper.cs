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
