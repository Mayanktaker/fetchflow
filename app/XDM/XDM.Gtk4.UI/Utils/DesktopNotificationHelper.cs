// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// FreeDesktop notification helper over D-Bus with notify-send fallback for GTK 4.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Tmds.DBus;
using TraceLog;
using Translations;
using XDM.Core;
using XDM.Core.Util;

namespace XDM.Gtk4UI.Utils
{
    // Standard org.freedesktop.Notifications DBus contract
    [DBusInterface("org.freedesktop.Notifications")]
    public interface IFreedesktopNotifications : IDBusObject
    {
        Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expireTimeout);
        Task CloseNotificationAsync(uint id);
        Task<string[]> GetCapabilitiesAsync();
        Task<(string name, string vendor, string version, string specVersion)> GetServerInformationAsync();
        Task<IDisposable> WatchActionInvokedAsync(Action<(uint id, string actionKey)> handler, Action<Exception>? onError = null);
        Task<IDisposable> WatchNotificationClosedAsync(Action<(uint id, uint reason)> handler, Action<Exception>? onError = null);
    }

    // Sends desktop notifications via session bus with shell fallback
    internal static class DesktopNotificationHelper
    {
        private static Connection? sessionBus;
        private static IFreedesktopNotifications? notifyProxy;
        private static bool isInitialized;
        private const string AppIconName = "com.mayanktaker.fetchflow";
        private static readonly object s_mediaLock = new();
        private static readonly ConcurrentDictionary<uint, (string filePath, string folderPath)> s_activeDownloadNotifications = new();
        private static System.Threading.Timer? s_mediaDebounceTimer;
        private static int s_pendingMediaCount;
        private static uint s_lastMediaNotificationId;
        private static DateTime s_lastMediaNotificationTime = DateTime.MinValue;
        private static DateTime s_firstMediaBurstTime = DateTime.MinValue;
        private const int MediaDebounceMs = 1200;
        private const int MaxBurstDelayMs = 3000;
        private const int MediaSessionResetSeconds = 60;

        // Connects to the session D-Bus notification service
        private static async Task EnsureConnectionAsync()
        {
            if (isInitialized && sessionBus != null) return;
            try
            {
                sessionBus = Connection.Session;
                await sessionBus.ConnectAsync();
                notifyProxy = sessionBus.CreateProxy<IFreedesktopNotifications>(
                    "org.freedesktop.Notifications",
                    new ObjectPath("/org/freedesktop/Notifications"));
                await notifyProxy.WatchActionInvokedAsync(
                    OnActionInvoked,
                    ex => Log.Debug(ex, "Notification ActionInvoked watcher error: " + ex.Message));
                await notifyProxy.WatchNotificationClosedAsync(
                    OnNotificationClosed,
                    ex => Log.Debug(ex, "Notification NotificationClosed watcher error: " + ex.Message));
                isInitialized = true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to connect to D-Bus notification service: " + ex.Message);
            }
        }

        // Sends a desktop notification with title and message body
        public static void Show(string title, string message)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await EnsureConnectionAsync();
                    if (notifyProxy != null)
                    {
                        var hints = new Dictionary<string, object> { ["urgency"] = (byte)1 };
                        var timeoutMs = Config.Instance.NotificationTimeOut > 0 ? Config.Instance.NotificationTimeOut : 10000;
                        await notifyProxy.NotifyAsync(
                            "FetchFlow", 0, AppIconName, title, message,
                            Array.Empty<string>(), hints, timeoutMs);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "D-Bus notification failed, trying notify-send fallback: " + ex.Message);
                }
                FallbackNotifySend(title, message);
            });
        }

        // Handles notification click actions (both body click "default", action button "grab", and "dismiss")
        private static void OnActionInvoked((uint id, string actionKey) e)
        {
            if (s_activeDownloadNotifications.TryRemove(e.id, out var dlInfo))
            {
                if (e.actionKey == "dismiss")
                {
                    _ = notifyProxy?.CloseNotificationAsync(e.id);
                    return;
                }

                if (e.actionKey == "default" || e.actionKey == "open")
                {
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(dlInfo.filePath) && File.Exists(dlInfo.filePath))
                            {
                                PlatformHelper.OpenFile(dlInfo.filePath);
                            }
                            else if (!string.IsNullOrEmpty(dlInfo.folderPath) && Directory.Exists(dlInfo.folderPath))
                            {
                                PlatformHelper.OpenFolder(dlInfo.folderPath, Path.GetFileName(dlInfo.filePath));
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Failed to open file from notification action: " + ex.Message);
                        }
                        return false;
                    });
                    return;
                }

                if (e.actionKey == "open_folder")
                {
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(dlInfo.folderPath) && Directory.Exists(dlInfo.folderPath))
                            {
                                PlatformHelper.OpenFolder(dlInfo.folderPath, Path.GetFileName(dlInfo.filePath));
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Failed to open folder from notification action: " + ex.Message);
                        }
                        return false;
                    });
                    return;
                }
            }

            if (e.actionKey == "dismiss")
            {
                lock (s_mediaLock)
                {
                    if (s_lastMediaNotificationId == e.id)
                    {
                        s_pendingMediaCount = 0;
                        s_lastMediaNotificationId = 0;
                    }
                }
                _ = notifyProxy?.CloseNotificationAsync(e.id);
                return;
            }

            bool match;
            lock (s_mediaLock)
            {
                match = (s_lastMediaNotificationId > 0 && e.id == s_lastMediaNotificationId);
            }

            if (match || e.actionKey == "grab")
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    try
                    {
                        ApplicationContext.PlatformUIService?.CreateAndShowMediaGrabber();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to open Media Grabber from notification: " + ex.Message);
                    }
                    return false;
                });
            }
        }

        // Cleans up tracking when a notification is closed by the server or user
        private static void OnNotificationClosed((uint id, uint reason) e)
        {
            s_activeDownloadNotifications.TryRemove(e.id, out _);
            lock (s_mediaLock)
            {
                if (s_lastMediaNotificationId == e.id)
                {
                    s_pendingMediaCount = 0;
                    s_lastMediaNotificationId = 0;
                }
            }
        }

        // Coalesces and debounces media capture notifications to avoid flooding desktop on multi-video pages
        public static void ShowMediaDetected()
        {
            lock (s_mediaLock)
            {
                var now = DateTime.UtcNow;
                if ((now - s_lastMediaNotificationTime).TotalSeconds > MediaSessionResetSeconds)
                {
                    s_pendingMediaCount = 0;
                    s_lastMediaNotificationId = 0;
                }

                s_pendingMediaCount++;

                if (s_firstMediaBurstTime == DateTime.MinValue)
                {
                    s_firstMediaBurstTime = now;
                }

                var debounceSec = (Config.Instance.MediaNotificationDebounceSeconds > 0)
                    ? Config.Instance.MediaNotificationDebounceSeconds
                    : 2;
                var debounceMs = debounceSec * 1000;
                var maxBurstMs = Math.Max(debounceMs * 2, 3000);

                var elapsedBurst = (now - s_firstMediaBurstTime).TotalMilliseconds;
                var delay = (elapsedBurst >= maxBurstMs) ? 0 : debounceMs;

                if (s_mediaDebounceTimer == null)
                {
                    s_mediaDebounceTimer = new System.Threading.Timer(_ => DispatchMediaNotification(), null, delay, System.Threading.Timeout.Infinite);
                }
                else
                {
                    s_mediaDebounceTimer.Change(delay, System.Threading.Timeout.Infinite);
                }
            }
        }

        // Dispatches the aggregated media notification over D-Bus using replacesId to update in-place
        private static void DispatchMediaNotification()
        {
            int count;
            uint replacesId;
            lock (s_mediaLock)
            {
                count = s_pendingMediaCount;
                replacesId = s_lastMediaNotificationId;
                s_firstMediaBurstTime = DateTime.MinValue;
            }

            if (count <= 0) return;

            string title = "FetchFlow";
            string message;
            try
            {
                message = count > 1
                    ? string.Format(TextResource.GetText("MSG_VID_CAP_MULTI") ?? "{0} streaming videos detected, please open Media Grabber", count)
                    : (TextResource.GetText("MSG_VID_CAP") ?? "Streaming video detected, please open Media Grabber");
            }
            catch (Exception)
            {
                message = $"{count} streaming videos detected, please open Media Grabber";
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await EnsureConnectionAsync();
                    if (notifyProxy != null)
                    {
                        var hints = new Dictionary<string, object>
                        {
                            ["urgency"] = (byte)1,
                            ["category"] = "transfer"
                        };
                        var actions = new[]
                        {
                            "default", "Open",
                            "grab", TextResource.GetText("MSG_MEDIA_CAPTURE") ?? "Media Grabber",
                            "dismiss", TextResource.GetText("BTN_DISMISS") ?? "Dismiss"
                        };
                        var timeoutMs = Config.Instance.NotificationTimeOut > 0 ? Config.Instance.NotificationTimeOut : 10000;
                        var newId = await notifyProxy.NotifyAsync(
                            "FetchFlow", replacesId, AppIconName, title, message,
                            actions, hints, timeoutMs);

                        lock (s_mediaLock)
                        {
                            s_lastMediaNotificationId = newId;
                            s_lastMediaNotificationTime = DateTime.UtcNow;
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "D-Bus notification failed, trying notify-send fallback: " + ex.Message);
                }

                FallbackNotifySend(title, message, replacesId);
            });
        }

        // Sends rich download completion notice with clickable open actions
        public static void ShowDownloadComplete(string fileName, string filePath, string folderPath)
        {
            if (!Config.Instance.ShowNotification) return;

            var summary = TextResource.GetText("MSG_DOWNLOAD_COMPLETE") ?? "Download Complete";
            var body = fileName;

            _ = Task.Run(async () =>
            {
                try
                {
                    await EnsureConnectionAsync();
                    if (notifyProxy != null)
                    {
                        var actions = new[]
                        {
                            "default", TextResource.GetText("CTX_OPEN_FILE") ?? "Open",
                            "open", TextResource.GetText("CTX_OPEN_FILE") ?? "Open",
                            "open_folder", TextResource.GetText("CTX_OPEN_FOLDER") ?? "Open Folder",
                            "dismiss", TextResource.GetText("BTN_DISMISS") ?? "Dismiss"
                        };
                        var hints = new Dictionary<string, object>
                        {
                            ["urgency"] = (byte)1,
                            ["category"] = "transfer.complete",
                            ["desktop-entry"] = AppIconName
                        };

                        var timeoutMs = Config.Instance.NotificationTimeOut > 0 ? Config.Instance.NotificationTimeOut : 10000;
                        var newId = await notifyProxy.NotifyAsync(
                            "FetchFlow", 0, AppIconName, summary, body,
                            actions, hints, timeoutMs);

                        if (newId > 0)
                        {
                            if (s_activeDownloadNotifications.Count > 50)
                            {
                                s_activeDownloadNotifications.Clear();
                            }
                            s_activeDownloadNotifications[newId] = (filePath, folderPath);
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "D-Bus download complete notification failed, trying notify-send fallback: " + ex.Message);
                }

                FallbackNotifySend(summary, body);
            });
        }

        // Overload forwarding to full path resolver
        public static void ShowDownloadComplete(string fileName, string folderPath) =>
            ShowDownloadComplete(fileName, Path.Combine(folderPath, fileName), folderPath);

        // Shells out to notify-send when D-Bus proxy is unavailable
        private static void FallbackNotifySend(string title, string message, uint replacesId = 0)
        {
            try
            {
                var replaceArg = replacesId > 0 ? $"-r {replacesId} " : "";
                var psi = new ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = $"--app-name=\"FetchFlow\" {replaceArg}--icon=\"{AppIconName}\" -p \"{EscapeShell(title)}\" \"{EscapeShell(message)}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(2000);
                    if (uint.TryParse(output.Trim(), out var parsedId) && parsedId > 0)
                    {
                        lock (s_mediaLock)
                        {
                            s_lastMediaNotificationId = parsedId;
                            s_lastMediaNotificationTime = DateTime.UtcNow;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "notify-send fallback failed: " + ex.Message);
            }
        }

        // Escapes quotes and expansion tokens for shell execution
        private static string EscapeShell(string text) =>
            text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`");
    }
}
