// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using TraceLog;
using Translations;
using XDM.Core;

namespace XDM.Wpf.UI
{
    internal static class AppTrayIcon
    {
        private static NotifyIcon? notifyIcon;
        private static readonly object s_trayLock = new();
        private static System.Threading.Timer? s_mediaDebounceTimer;
        private static int s_pendingMediaCount;
        private static DateTime s_lastMediaNotificationTime = DateTime.MinValue;
        private static string? s_lastDownloadedFilePath;
        private static string? s_lastDownloadedFolderPath;

        public static event EventHandler? TrayClick;

        // Displays debounced tray notification when media items are detected
        public static void ShowNotification()
        {
            try
            {
                if (notifyIcon == null || !Config.Instance.ShowNotification || !Config.Instance.ShowMediaNotification) return;

                lock (s_trayLock)
                {
                    var now = DateTime.UtcNow;
                    if ((now - s_lastMediaNotificationTime).TotalSeconds > 60)
                    {
                        s_pendingMediaCount = 0;
                    }
                    s_pendingMediaCount++;

                    var debounceSec = (Config.Instance.MediaNotificationDebounceSeconds > 0)
                        ? Config.Instance.MediaNotificationDebounceSeconds
                        : 2;
                    var debounceMs = debounceSec * 1000;

                    if (s_mediaDebounceTimer == null)
                    {
                        s_mediaDebounceTimer = new System.Threading.Timer(_ => DispatchTrayNotification(), null, debounceMs, System.Threading.Timeout.Infinite);
                    }
                    else
                    {
                        s_mediaDebounceTimer.Change(debounceMs, System.Threading.Timeout.Infinite);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
        }

        // Dispatches the aggregated tray balloon notification
        private static void DispatchTrayNotification()
        {
            try
            {
                int count;
                lock (s_trayLock)
                {
                    count = s_pendingMediaCount;
                    s_lastMediaNotificationTime = DateTime.UtcNow;
                }

                if (notifyIcon != null && Config.Instance.ShowNotification && Config.Instance.ShowMediaNotification)
                {
                    string desc;
                    try
                    {
                        desc = count > 1
                            ? string.Format(TextResource.GetText("MSG_VID_CAP_MULTI") ?? "{0} streaming videos detected, please open Media Grabber", count)
                            : (TextResource.GetText("MSG_DWN_VID_DESC") ?? "Please click here to download streaming video");
                    }
                    catch (Exception)
                    {
                        desc = $"{count} streaming videos detected, please open Media Grabber";
                    }

                    notifyIcon.ShowBalloonTip(30000, TextResource.GetText("MSG_DOWNLOAD_VIDEO") ?? "Download Video",
                        desc, ToolTipIcon.Info);
                    notifyIcon.BalloonTipClicked -= NotifyIcon_DownloadBalloonTipClicked;
                    notifyIcon.BalloonTipClicked -= NotifyIcon_BalloonTipClicked;
                    notifyIcon.BalloonTipClicked += NotifyIcon_BalloonTipClicked;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
        }

        // Arbitrary-text tray balloon (no click action)
        public static void ShowTextNotification(string message)
        {
            try
            {
                if (notifyIcon != null && Config.Instance.ShowNotification)
                {
                    var timeoutMs = Config.Instance.NotificationTimeOut > 0 ? Config.Instance.NotificationTimeOut : 10000;
                    notifyIcon.ShowBalloonTip(timeoutMs, "FetchFlow", message, ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
        }

        // Displays tray balloon for completed download with click-to-open action
        public static void ShowDownloadCompleteNotification(string fileName, string filePath, string folderPath)
        {
            try
            {
                if (notifyIcon == null || !Config.Instance.ShowNotification) return;

                s_lastDownloadedFilePath = filePath;
                s_lastDownloadedFolderPath = folderPath;

                var summary = TextResource.GetText("MSG_DOWNLOAD_COMPLETE") ?? "Download Complete";
                var timeoutMs = Config.Instance.NotificationTimeOut > 0 ? Config.Instance.NotificationTimeOut : 10000;
                notifyIcon.BalloonTipClicked -= NotifyIcon_BalloonTipClicked;
                notifyIcon.BalloonTipClicked -= NotifyIcon_DownloadBalloonTipClicked;
                notifyIcon.BalloonTipClicked += NotifyIcon_DownloadBalloonTipClicked;
                notifyIcon.ShowBalloonTip(timeoutMs, summary, fileName, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
        }

        private static void NotifyIcon_DownloadBalloonTipClicked(object sender, EventArgs e)
        {
            if (notifyIcon != null)
            {
                notifyIcon.BalloonTipClicked -= NotifyIcon_DownloadBalloonTipClicked;
            }
            try
            {
                if (!string.IsNullOrEmpty(s_lastDownloadedFilePath) && File.Exists(s_lastDownloadedFilePath))
                {
                    XDM.Core.Util.PlatformHelper.OpenFile(s_lastDownloadedFilePath);
                }
                else if (!string.IsNullOrEmpty(s_lastDownloadedFolderPath) && Directory.Exists(s_lastDownloadedFolderPath))
                {
                    XDM.Core.Util.PlatformHelper.OpenFolder(s_lastDownloadedFolderPath, Path.GetFileName(s_lastDownloadedFilePath));
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to open file from balloon tip: " + ex.Message);
            }
        }

        private static void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            XDM.Core.ApplicationContext.PlatformUIService.CreateAndShowMediaGrabber();
        }

        public static void AttachToSystemTray()
        {

            var ctx = new ContextMenu();

            var menuExit = new MenuItem
            {
                Text = TextResource.GetText("MENU_EXIT")
            };
            menuExit.Click += (_, _) => Environment.Exit(0);

            var menuRestore = new MenuItem
            {
                Text = TextResource.GetText("MSG_RESTORE")
            };
            menuRestore.Click += (sender, e) => TrayClick?.Invoke(sender, e);

            ctx.MenuItems.Add(menuRestore);
            ctx.MenuItems.Add(menuExit);

            notifyIcon = new NotifyIcon
            {
                Text = "FetchFlow",
                Visible = true,
                Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xdm-logo.ico")),
                ContextMenu = ctx
            };
            notifyIcon.MouseClick += NotifyIcon_MouseClick;

        }

        private static void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                TrayClick?.Invoke(sender, e);
            }
        }

        public static void DetachFromSystemTray()
        {
            notifyIcon?.Dispose();
            notifyIcon = null;
        }
    }
}
