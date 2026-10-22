// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// TrayIconManager for GTK 4 — implements the StatusNotifierItem (SNI) D-Bus protocol
// for KDE Plasma, GNOME (AppIndicator), Sway/Waybar, and COSMIC desktops.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GdkPixbuf;
using Tmds.DBus;
using TraceLog;
using XDM.Core.Util;

namespace XDM.Gtk4UI.Utils
{
    // Manages system tray integration via StatusNotifierItem over D-Bus
    public sealed class Gtk4TrayIconManager : IDisposable
    {
        private const string WellKnownName = "com.mayanktaker.fetchflow.Tray";
        private const string WatcherService = "org.kde.StatusNotifierWatcher";
        private const string WatcherPath = "/StatusNotifierWatcher";

        private Connection? connection;
        private Gtk4SniItem? sniItem;
        private Gtk4DBusMenuServer? dbusMenuServer;
        private Action? onActivate;
        private Action? onQuit;
        private string? appName;
        private uint sniRetryTimerId;
        private int sniRetryCount;
        private const int MaxSniRetries = 24;

        // True when the tray registration succeeded and is active
        public bool IsTrayActive { get; private set; }

        // Initializes tray connection and registers on the bus
        public void Init(string appName, Action onActivate, Action onQuit)
        {
            this.appName = appName;
            this.onActivate = onActivate;
            this.onQuit = onQuit;

            try
            {
                if (TryInitSni(appName, onActivate, onQuit))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "SNI tray init failed: " + ex.Message);
            }

            Log.Debug("No StatusNotifierWatcher on the bus; scheduling background SNI retry.");
            ScheduleSniRetry(onActivate, onQuit);
        }

        // Synchronous wrapper attempting initial SNI registration
        private bool TryInitSni(string appName, Action onActivate, Action onQuit)
        {
            return Task.Run(async () => await TryInitSniAsync(appName, onActivate, onQuit).ConfigureAwait(false))
                .GetAwaiter().GetResult();
        }

        // Asynchronously connects to D-Bus and registers the SNI item
        private async Task<bool> TryInitSniAsync(string appName, Action onActivate, Action onQuit)
        {
            try
            {
                var conn = new Connection(Address.Session);
                await conn.ConnectAsync().ConfigureAwait(false);

                if (!await conn.IsServiceActiveAsync(WatcherService).ConfigureAwait(false))
                {
                    conn.Dispose();
                    return false;
                }

                await conn.RegisterServiceAsync(WellKnownName).ConfigureAwait(false);

                var themedResolves = ThemedIconResolves(PlatformHelper.TrayIconName);
                var props = new SniProperties
                {
                    Id = "fetchflow",
                    Title = appName,
                    ItemIsMenu = false,
                    Menu = new ObjectPath("/MenuBar"),
                    IconName = TrayIconPolicy.ResolveAdvertisedIconName(PlatformHelper.TrayIconName, themedResolves),
                    IconThemePath = TrayIconPolicy.ResolveIconThemePath(),
                    IconPixmap = BuildSniPixmaps(),
                    ToolTip = (0, 0, Array.Empty<byte>(), appName, "")
                };

                sniItem = new Gtk4SniItem(
                    props,
                    () => GLib.Functions.IdleAdd(0, () => { onActivate(); return false; }),
                    (x, y) => { });

                dbusMenuServer = new Gtk4DBusMenuServer(
                    () => GLib.Functions.IdleAdd(0, () => { onActivate(); return false; }),
                    () => GLib.Functions.IdleAdd(0, () => { onQuit(); return false; }));

                await conn.RegisterObjectAsync(sniItem).ConfigureAwait(false);
                await conn.RegisterObjectAsync(dbusMenuServer).ConfigureAwait(false);

                var watcher = conn.CreateProxy<IStatusNotifierWatcher>(WatcherService, new ObjectPath(WatcherPath));
                await watcher.RegisterStatusNotifierItemAsync(WellKnownName).ConfigureAwait(false);

                connection = conn;
                IsTrayActive = true;
                Log.Debug("Tray: registered GTK 4 StatusNotifierItem (com.mayanktaker.fetchflow.Tray).");
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "TryInitSniAsync error: " + ex.Message);
                return false;
            }
        }

        // Schedules periodic SNI retry until the tray host appears or retry limit is reached
        private void ScheduleSniRetry(Action onActivate, Action onQuit)
        {
            if (sniRetryTimerId != 0 || appName == null) return;

            sniRetryTimerId = GLib.Functions.TimeoutAdd(0, 5000, () =>
            {
                sniRetryCount++;
                var currentAppName = appName;

                _ = Task.Run(async () =>
                {
                    bool ok = false;
                    try
                    {
                        ok = await TryInitSniAsync(currentAppName, onActivate, onQuit).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "SNI tray background retry failed: " + ex.Message);
                    }

                    if (ok)
                    {
                        GLib.Functions.IdleAdd(0, () =>
                        {
                            if (sniRetryTimerId != 0)
                            {
                                GLib.Functions.SourceRemove(sniRetryTimerId);
                                sniRetryTimerId = 0;
                            }
                            return false;
                        });
                    }
                });

                if (sniRetryCount >= MaxSniRetries)
                {
                    sniRetryTimerId = 0;
                    return false;
                }

                return true;
            });
        }

        // Checks whether the tray icon resolves in the active GTK icon theme
        private static bool ThemedIconResolves(string iconName)
        {
            try
            {
                var display = Gdk.Display.GetDefault();
                if (display == null) return false;

                var theme = Gtk.IconTheme.GetForDisplay(display);
                if (theme == null) return false;
                if (theme.HasIcon(iconName)) return true;

                if (!PlatformHelper.EnsureTrayIconInstalled()) return false;
                return theme.HasIcon(iconName);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Tray: icon theme probe failed: " + ex.Message);
                return false;
            }
        }

        // Builds multi-resolution SNI pixmaps (16, 22, 24, 32, 48) from SVG for crisp rendering
        private static (int, int, byte[])[] BuildSniPixmaps()
        {
            var list = new List<(int, int, byte[])>();
            int[] sizes = { 16, 22, 24, 32, 48 };

            var svgCandidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "svg-icons", "fetchflow-tray.svg"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "icons", "hicolor", "scalable", "apps", "fetchflow-tray.svg"),
                "/opt/fetchflow/svg-icons/fetchflow-tray.svg"
            };

            string? svgPath = null;
            foreach (var cand in svgCandidates)
            {
                if (File.Exists(cand))
                {
                    svgPath = cand;
                    break;
                }
            }

            if (svgPath == null) return list.ToArray();

            foreach (var sz in sizes)
            {
                try
                {
                    var pb = Pixbuf.NewFromFileAtSize(svgPath, sz, sz);
                    if (pb != null)
                    {
                        list.Add(PixbufToSniArgb32(pb));
                    }
                }
                catch { }
            }

            return list.ToArray();
        }

        // Converts a GdkPixbuf into ARGB32 network byte order required by StatusNotifierItem
        private static (int, int, byte[]) PixbufToSniArgb32(Pixbuf pb)
        {
            int w = pb.Width;
            int h = pb.Height;
            int chans = pb.NChannels;
            int rowstride = pb.Rowstride;
            bool hasAlpha = pb.HasAlpha;

            var argb = new byte[w * h * 4];
            var row = new byte[rowstride];

            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(IntPtr.Add(pb.Pixels, y * rowstride), row, 0, rowstride);
                for (int x = 0; x < w; x++)
                {
                    int si = x * chans;
                    int di = (y * w + x) * 4;
                    argb[di + 0] = hasAlpha ? row[si + 3] : (byte)255;
                    argb[di + 1] = row[si];
                    argb[di + 2] = row[si + 1];
                    argb[di + 3] = row[si + 2];
                }
            }

            return (w, h, argb);
        }

        // Disposes D-Bus connection and cleans up tray registration
        public void Dispose()
        {
            if (sniRetryTimerId != 0)
            {
                try { GLib.Functions.SourceRemove(sniRetryTimerId); } catch { }
                sniRetryTimerId = 0;
            }

            if (connection != null)
            {
                try
                {
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Tray connection dispose: " + ex.Message);
                }
                connection = null;
            }

            IsTrayActive = false;
        }
    }
}
