// © 2026 Mayanktaker | Based on XDM by subhra74 (https://github.com/subhra74/xdm)
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using XDM.Core;

namespace XDM.GtkUI
{
    public static class UpdateChecker
    {
        private const string RepoUrl = "https://api.github.com/repos/Mayanktaker/xdm/releases/latest";

        /// <summary>
        /// Checks GitHub for a newer version than AppInfo.APP_VERSION.
        /// Returns the new version tag if available, or null if up-to-date.
        /// </summary>
        public static async Task<string> CheckForUpdateAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "XDM-Update-Checker");

                var response = await client.GetStringAsync(RepoUrl);
                using var doc = JsonDocument.Parse(response);
                
                if (doc.RootElement.TryGetProperty("tag_name", out var tagElement))
                {
                    string latestVersionStr = tagElement.GetString()?.TrimStart('v');
                    if (Version.TryParse(latestVersionStr, out Version latestVersion) && 
                        Version.TryParse(AppInfo.APP_VERSION, out Version currentVersion))
                    {
                        if (latestVersion > currentVersion)
                        {
                            return latestVersionStr;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update check failed: {ex.Message}");
            }
            return null;
        }
    }
}
