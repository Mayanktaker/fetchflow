// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// FreeDesktop notification helper over D-Bus with notify-send fallback for GTK 4.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Tmds.DBus;
using TraceLog;
using Translations;
using XDM.Core;

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
    }

    // Sends desktop notifications via session bus with shell fallback
    internal static class DesktopNotificationHelper
    {
        private static Connection? sessionBus;
        private static IFreedesktopNotifications? notifyProxy;
        private static bool isInitialized;
        private const string AppIconName = "com.mayanktaker.fetchflow";

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
                        await notifyProxy.NotifyAsync(
                            "FetchFlow", 0, AppIconName, title, message,
                            Array.Empty<string>(), hints, 5000);
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

        // Sends download completion notice with file details
        public static void ShowDownloadComplete(string fileName, string folderPath)
        {
            var summary = TextResource.GetText("MSG_DOWNLOAD_COMPLETE") ?? "Download Complete";
            Show(summary, fileName);
        }

        // Shells out to notify-send when D-Bus proxy is unavailable
        private static void FallbackNotifySend(string title, string message)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = $"--app-name=\"FetchFlow\" --icon=\"{AppIconName}\" \"{EscapeShell(title)}\" \"{EscapeShell(message)}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "notify-send fallback failed: " + ex.Message);
            }
        }

        // Escapes quotes for shell execution
        private static string EscapeShell(string text) =>
            text.Replace("\"", "\\\"").Replace("$", "\\$");
    }
}
