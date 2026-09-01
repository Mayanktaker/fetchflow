// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;
using TraceLog;
using XDM.Core.Util;

namespace XDM.GtkUI.Utils
{
    // DBus interface for standard FreeDesktop notifications (org.freedesktop.Notifications)
    [DBusInterface("org.freedesktop.Notifications")]
    public interface IFreedesktopNotifications : IDBusObject
    {
        Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expireTimeout);
        Task CloseNotificationAsync(uint id);
        Task<string[]> GetCapabilitiesAsync();
        Task<(string name, string vendor, string version, string specVersion)> GetServerInformationAsync();
    }

    // Sends native FreeDesktop notifications over the session D-Bus
    public static class DesktopNotificationHelper
    {
        private static Connection? sessionBus;
        private static IFreedesktopNotifications? notifyProxy;
        private static bool isInitialized;

        // Initializes connection to session bus notification service
        private static async Task EnsureConnectionAsync()
        {
            if (isInitialized && sessionBus != null) return;

            try
            {
                sessionBus = Connection.Session;
                await sessionBus.ConnectAsync();
                notifyProxy = sessionBus.CreateProxy<IFreedesktopNotifications>("org.freedesktop.Notifications", new ObjectPath("/org/freedesktop/Notifications"));
                isInitialized = true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to connect to D-Bus notification service: " + ex.Message);
            }
        }

        // Sends desktop notification for completed download
        public static void ShowDownloadComplete(string fileName, string folderPath)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await EnsureConnectionAsync();
                    if (notifyProxy != null)
                    {
                        var summary = Translations.TextResource.GetText("MSG_DOWNLOAD_COMPLETE") ?? "Download Complete";
                        var body = fileName;
                        var actions = Array.Empty<string>();
                        var hints = new Dictionary<string, object>
                        {
                            { "urgency", (byte)1 },
                            { "category", "transfer.complete" }
                        };

                        await notifyProxy.NotifyAsync(
                            "FetchFlow",
                            0,
                            "com.mayanktaker.fetchflow",
                            summary,
                            body,
                            actions,
                            hints,
                            5000
                        );
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to send desktop notification: " + ex.Message);
                }
            });
        }
    }
}
