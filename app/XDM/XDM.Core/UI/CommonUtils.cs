// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.IO;
using TraceLog;
using Translations;
using XDM.Core;
using XDM.Core.Clients.Http;
using XDM.Core.UI;
using XDM.Core.Util;

namespace XDM.Core.UI
{
    internal static class CommonUtils
    {
        private static string AutoSelectText = TextResource.GetText("ND_AUTO_CAT");
        private static string BrowseText = TextResource.GetText("BTN_BROWSE");

        internal static string[] GetFolderValues()
        {
            if (!Config.Instance.RecentFolders.Contains(Config.Instance.DefaultDownloadFolder))
            {
                Config.Instance.RecentFolders.Insert(0, Config.Instance.DefaultDownloadFolder);
            }
            var arr = new string[Config.Instance.RecentFolders.Count + 2];
            arr[0] = AutoSelectText;
            arr[1] = BrowseText;
            var k = 2;
            for (var i = 0; i < Config.Instance.RecentFolders.Count; i++, k++)
            {
                arr[k] = Config.Instance.RecentFolders[i];
            }
            return arr;
        }

        internal static void OnFileBrowsed(object? sender, FileBrowsedEventArgs args)
        {
            var folder = args.SelectedFile;
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }
            Helpers.UpdateRecentFolderList(folder);
            if (sender != null)
            {
                var fileSelectable = (IFileSelectable)sender;
                fileSelectable.SetFolderValues(GetFolderValues());
                fileSelectable.SeletedFolderIndex = 2;
            }
        }

        internal static void OnDropdownSelectionChanged(object? sender, FileBrowsedEventArgs args)
        {
            if (sender != null)
            {
                var fileSelectable = (IFileSelectable)sender;
                var index = fileSelectable.SeletedFolderIndex;
                if (index == 0)
                {
                    Config.Instance.FolderSelectionMode = FolderSelectionMode.Auto;
                }
                else if (!string.IsNullOrEmpty(args.SelectedFile))
                {
                    Config.Instance.FolderSelectionMode = FolderSelectionMode.Manual;
                    if (index > 1)
                    {
                        Config.Instance.UserSelectedDownloadFolder = args.SelectedFile;
                    }
                }
                Config.SaveConfig();
            }
        }

        internal static string? SelectedFolderFromIndex(int index)
        {
            if (Config.Instance.FolderSelectionMode == FolderSelectionMode.Auto) return null;
            if (index == 0 || index == 1)
            {
                Log.Debug($"Index value {index} is invalid for {Config.Instance.FolderSelectionMode}");
                return null;
            }
            return Config.Instance.RecentFolders.Count > 0 ? Config.Instance.RecentFolders[index - 2] :
                Config.Instance.DefaultDownloadFolder;
        }

        // Asks the server for the entity size without downloading it; 0 when it will not say.
        // The client requests identity, so a compressed body length is never mistaken for the size.
        internal static long ProbeFileSize(string? url,
            Dictionary<string, List<string>>? headers, string? cookies, AuthenticationInfo? authentication)
        {
            if (string.IsNullOrEmpty(url) || !Helpers.IsUriValid(url)) return 0;
            try
            {
                using var http = HttpClientFactory.NewHttpClient(Config.Instance.Proxy);
                http.Timeout = TimeSpan.FromSeconds(Math.Max(5, Math.Min(Config.Instance.NetworkTimeout, 15)));
                var request = http.CreateGetRequest(new Uri(url), headers, cookies, authentication);
                // A one-byte range carries the total in Content-Range and moves no body
                request.AddRange(0, 0);
                using var response = http.Send(request);
                var size = EntityLengthPolicy.ResolveFileSize(
                    response.ContentRangeLength, response.ContentLength, response.Compressed);
                Log.Debug($"Size probe: {url} -> {size}");
                return size > 0 ? size : 0;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Size probe failed: " + url);
                return 0;
            }
        }
    }
}
