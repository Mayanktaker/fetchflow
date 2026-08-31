// © 2026 Mayanktaker | Based on XDM by subhra74 (https://github.com/subhra74/xdm)
// TrayIconManager — chooses the best tray mechanism for the current desktop and keeps a
// backward-compatible fallback chain:
//   1. StatusNotifierItem (SNI) over D-Bus  -> KDE Plasma 6, GNOME (+AppIndicator ext),
//                                                Sway/Hyprland/COSMIC (waybar), X11 DEs with SNI
//   2. Legacy Gtk.StatusIcon (XEmbed)       -> X11-only DEs without an SNI host (back-compat)
//   3. None                                  -> Wayland with no SNI host (e.g. stock GNOME);
//                                                MainWindow falls back to minimize-to-taskbar.
// Right-click context menu: "Show XDM" (restore) + "Quit" (exit).
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Gdk;
using Gtk;
using Tmds.DBus;
using TraceLog;
using XDM.GtkUI.Utils;
using GlSource = GLib.Source;
using GlTimeout = GLib.Timeout;

namespace XDM.GtkUI.Utils
{
    public class TrayIconManager
    {
        private const string WellKnownName = "org.xdmapp.Tray";
        private const string WatcherService = "org.kde.StatusNotifierWatcher";
        private const string WatcherPath = "/StatusNotifierWatcher";

        private Connection? connection;
        private XdmSniItem? sniItem;
        private DBusMenuServer? dbusMenuServer;
        private StatusIcon? legacyIcon;
        private System.Action? onActivate;
        private Pixbuf? icon;
        private string? appName;
        private uint sniRetryTimerId;
        private int sniRetryCount;
        private const int MaxSniRetries = 24; // ~2 minutes at 5s intervals

        // True when ANY tray mechanism is active (MainWindow uses this to allow hide-to-tray on close)
        public bool IsTrayActive { get; private set; }
        public TrayKind ActiveKind { get; private set; } = TrayKind.None;

        public enum TrayKind { None, StatusNotifierItem, LegacyStatusIcon }

        /// <summary>Initialize the best available tray. Never throws — failures degrade to None.</summary>
        public void Init(Pixbuf icon, string appName, System.Action onActivate, System.Action onQuit)
        {
            this.icon = icon;
            this.appName = appName;
            this.onActivate = onActivate;
            try
            {
                if (TryInitSni(icon, appName, onActivate, onQuit)) return;
            }
            catch (Exception ex) { Log.Debug("SNI tray init failed: " + ex.Message); }

            // Fallback: legacy XEmbed tray only makes sense on X11 (it's invisible on Wayland)
            if (!RunningOnWayland)
            {
                try { TryInitLegacy(icon, appName, onActivate, onQuit); return; }
                catch (Exception ex) { Log.Debug("Legacy tray init failed: " + ex.Message); }
            }

            // The SNI host (plasmashell, waybar, ...) can start after XDM at login — retry in
            // the background so close-to-tray keeps working instead of silently missing the tray.
            Log.Debug("No system tray available yet; retrying SNI in the background.");
            ScheduleSniRetry(onActivate, onQuit);
        }

        // Re-attempt SNI registration every 5s until a host appears or the retry budget runs out.
        // The GLib timeout callback itself never blocks — it launches the D-Bus work on a
        // ThreadPool thread and marshals the result back, so the GTK main loop is never held
        // for seconds (which triggers compositor "not responding" and Wayland protocol timeouts).
        private void ScheduleSniRetry(System.Action onActivate, System.Action onQuit)
        {
            if (sniRetryTimerId != 0 || icon == null || appName == null) return;
            sniRetryTimerId = GlTimeout.Add(5000, () =>
            {
                try
                {
                    sniRetryCount++;
                    var currentIcon = icon;
                    var currentAppName = appName;
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        bool ok = false;
                        try
                        {
                            ok = await TryInitSniAsync(currentIcon, currentAppName, onActivate, onQuit)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Log.Debug("SNI tray retry failed: " + ex);
                            try { connection?.Dispose(); } catch { }
                            connection = null;
                        }
                        if (ok)
                        {
                            Gtk.Application.Invoke((_, _) =>
                            {
                                if (sniRetryTimerId != 0)
                                {
                                    GlSource.Remove(sniRetryTimerId);
                                    sniRetryTimerId = 0;
                                }
                            });
                        }
                        else if (sniRetryCount >= MaxSniRetries)
                        {
                            Gtk.Application.Invoke((_, _) =>
                            {
                                if (sniRetryTimerId != 0)
                                {
                                    GlSource.Remove(sniRetryTimerId);
                                    sniRetryTimerId = 0;
                                }
                            });
                        }
                    });
                    // Keep the GLib source alive until the async attempt signals it should stop.
                    return sniRetryCount < MaxSniRetries;
                }
                catch (Exception ex)
                {
                    // Hard guard: GLib timeout callbacks must not propagate.
                    Log.Debug("SNI retry outer guard: " + ex);
                    return sniRetryCount < MaxSniRetries;
                }
            });
        }

        // Wayland session heuristic (no SNI host + Wayland => no legacy tray either)
        private static bool RunningOnWayland =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

        private bool TryInitSni(Pixbuf icon, string appName, System.Action onActivate, System.Action onQuit)
            => TryInitSniAsync(icon, appName, onActivate, onQuit).GetAwaiter().GetResult();

        // Async core — does the real D-Bus work without blocking the GTK thread. Called from
        // ScheduleSniRetry's ThreadPool path; the synchronous wrapper above stays for Init().
        private async Task<bool> TryInitSniAsync(Pixbuf icon, string appName, System.Action onActivate, System.Action onQuit)
        {
            // Each attempt gets a fresh Connection so a half-open/failed one never leaks
            // into the retry.
            try { connection?.Dispose(); } catch { }
            connection = new Connection(Address.Session);
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                await connection.ConnectAsync().WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Debug("SNI connect failed: " + ex.Message);
                return false;
            }

            // Detect an SNI host (KDE / GNOME+AppIndicator / waybar provide this service)
            bool watcherPresent;
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
                watcherPresent = await connection.IsServiceActiveAsync(WatcherService)
                    .WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Debug("SNI watcher probe failed: " + ex.Message);
                return false;
            }
            if (!watcherPresent)
            {
                Log.Debug("No StatusNotifierWatcher on the bus (GNOME without AppIndicator extension, etc.).");
                try { connection.Dispose(); } catch { }
                connection = null;
                return false;
            }

            var props = new SniProperties
            {
                Id = "xdm-app",
                Title = appName,
                // Provide the DBusMenu path so KDE can render the right-click menu natively
                ItemIsMenu = false,
                Menu = new ObjectPath("/MenuBar"),
                IconName = "xdm-app",
                IconPixmap = new[] { PixbufToRgba(icon) },
                ToolTip = (0, 0, Array.Empty<byte>(), appName, ""),
            };
            sniItem = new XdmSniItem(
                props,
                () => Gtk.Application.Invoke((_, _) => onActivate()),
                (x, y) => Gtk.Application.Invoke((_, _) => ShowContextMenu(onActivate, onQuit)));

            dbusMenuServer = new DBusMenuServer(onActivate, onQuit);

            // Time-box every registration so a wedged bus doesn't lock the caller.
            try
            {
                using var cts1 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                await connection.RegisterObjectAsync(dbusMenuServer).WaitAsync(cts1.Token).ConfigureAwait(false);
                using var cts2 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                await connection.RegisterObjectAsync(sniItem).WaitAsync(cts2.Token).ConfigureAwait(false);
                using var cts3 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                await connection.RegisterServiceAsync(WellKnownName, ServiceRegistrationOptions.Default).WaitAsync(cts3.Token).ConfigureAwait(false);

                var watcher = connection.CreateProxy<IStatusNotifierWatcher>(WatcherService, WatcherPath);
                using var cts4 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                await watcher.RegisterStatusNotifierItemAsync(WellKnownName).WaitAsync(cts4.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Log.Debug("SNI registration timed out");
                return false;
            }
            catch (Exception ex)
            {
                Log.Debug("SNI registration failed: " + ex.Message);
                return false;
            }

            IsTrayActive = true;
            ActiveKind = TrayKind.StatusNotifierItem;
            Log.Debug("Tray: registered StatusNotifierItem (org.xdmapp.Tray).");
            return true;
        }

        private void TryInitLegacy(Pixbuf icon, string appName, System.Action onActivate, System.Action onQuit)
        {
            legacyIcon = new StatusIcon(icon);
            legacyIcon.TooltipText = appName;
            legacyIcon.Activate += (_, _) => onActivate();
            legacyIcon.PopupMenu += (_, _) => ShowContextMenu(onActivate, onQuit);
            legacyIcon.Visible = true;
            IsTrayActive = true;
            ActiveKind = TrayKind.LegacyStatusIcon;
            Log.Debug("Tray: using legacy Gtk.StatusIcon (X11/XEmbed).");
        }

        // Build and show a GTK popup menu at the given screen coordinates.
        // Menu items: "Show XDM" (restore window) + separator + "Quit" (exit app).
        private static void ShowContextMenu(System.Action onActivate, System.Action onQuit)
        {
            var menu = new Menu();

            var showItem = new MenuItem("Show XDM");
            showItem.Activated += (_, _) => { onActivate?.Invoke(); };
            menu.Append(showItem);

            menu.Append(new SeparatorMenuItem());

            var quitItem = new MenuItem("Quit");
            quitItem.Activated += (_, _) => onQuit?.Invoke();
            menu.Append(quitItem);

            menu.ShowAll();
            // Use PopupAtPointer for Wayland (uses current pointer position);
            // fall back to Popup at the reported coordinates for X11.
            menu.PopupAtPointer(null);
        }

        // Non-blocking teardown: never block the GTK thread on D-Bus. The synchronous
        // Dispose() fires the unregister on the ThreadPool and returns immediately;
        // DisposeAsync() is available for callers that can await.
        public void Dispose()
        {
            try
            {
                if (sniRetryTimerId != 0)
                {
                    GlSource.Remove(sniRetryTimerId);
                    sniRetryTimerId = 0;
                }
                legacyIcon?.Dispose();
                legacyIcon = null;
                // Snapshot connection and clear fields synchronously so state is reset
                // immediately and double-dispose is harmless.
                var conn = connection;
                var hasSni = sniItem != null && conn != null;
                connection = null;
                sniItem = null;
                dbusMenuServer = null;
                if (hasSni && conn != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await conn.UnregisterServiceAsync(WellKnownName)
                                .WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        }
                        catch (Exception ex) { Log.Debug("Tray unregister on dispose: " + ex.Message); }
                        finally { try { conn.Dispose(); } catch (Exception ex2) { Log.Debug("Tray connection dispose: " + ex2.Message); } }
                    });
                }
                else if (conn != null)
                {
                    try { conn.Dispose(); } catch (Exception ex) { Log.Debug("Tray connection dispose: " + ex.Message); }
                }
            }
            catch (Exception ex) { Log.Debug("TrayIconManager dispose: " + ex.Message); }
            IsTrayActive = false;
            ActiveKind = TrayKind.None;
        }

        // Awaitable variant for future callers that can await shutdown.
        public async Task DisposeAsync()
        {
            try
            {
                if (sniRetryTimerId != 0)
                {
                    try { GlSource.Remove(sniRetryTimerId); } catch { }
                    sniRetryTimerId = 0;
                }
                legacyIcon?.Dispose();
                legacyIcon = null;
                var conn = connection;
                connection = null;
                sniItem = null;
                dbusMenuServer = null;
                if (conn != null)
                {
                    try
                    {
                        await conn.UnregisterServiceAsync(WellKnownName)
                            .WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    }
                    catch (Exception ex) { Log.Debug("Tray unregister on dispose: " + ex.Message); }
                    finally { try { conn.Dispose(); } catch (Exception ex2) { Log.Debug("Tray connection dispose: " + ex2.Message); } }
                }
            }
            catch (Exception ex) { Log.Debug("TrayIconManager dispose: " + ex.Message); }
            IsTrayActive = false;
            ActiveKind = TrayKind.None;
        }

        // Convert a Gdk.Pixbuf to the SNI IconPixmap RGBA byte[] (handles RGB->RGBA + rowstride).
        private static (int, int, byte[]) PixbufToRgba(Pixbuf pb)
        {
            int w = pb.Width, h = pb.Height;
            int chans = pb.NChannels, rowstride = pb.Rowstride, hasAlpha = pb.HasAlpha ? 1 : 0;
            var rgba = new byte[w * h * 4];
            var row = new byte[rowstride];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(IntPtr.Add(pb.Pixels, y * rowstride), row, 0, rowstride);
                for (int x = 0; x < w; x++)
                {
                    int si = x * chans, di = (y * w + x) * 4;
                    rgba[di] = row[si];
                    rgba[di + 1] = row[si + 1];
                    rgba[di + 2] = row[si + 2];
                    rgba[di + 3] = hasAlpha != 0 ? row[si + 3] : (byte)255;
                }
            }
            return (w, h, rgba);
        }
    }
}
