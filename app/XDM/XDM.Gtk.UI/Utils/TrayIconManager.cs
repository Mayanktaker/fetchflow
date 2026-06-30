// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// TrayIconManager — chooses the best tray mechanism for the current desktop and keeps a
// backward-compatible fallback chain:
//   1. StatusNotifierItem (SNI) over D-Bus  -> KDE Plasma 6, GNOME (+AppIndicator ext),
//                                                Sway/Hyprland/COSMIC (waybar), X11 DEs with SNI
//   2. Legacy Gtk.StatusIcon (XEmbed)       -> X11-only DEs without an SNI host (back-compat)
//   3. None                                  -> Wayland with no SNI host (e.g. stock GNOME);
//                                                MainWindow falls back to minimize-to-taskbar.
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Gdk;
using Gtk;
using Tmds.DBus;
using TraceLog;
using XDM.GtkUI.Utils;

namespace XDM.GtkUI.Utils
{
    public class TrayIconManager
    {
        private const string WellKnownName = "org.xdmapp.Tray";
        private const string WatcherService = "org.kde.StatusNotifierWatcher";
        private const string WatcherPath = "/StatusNotifierWatcher";

        private Connection? connection;
        private XdmSniItem? sniItem;
        private StatusIcon? legacyIcon;

        // True when ANY tray mechanism is active (MainWindow uses this to allow hide-to-tray on close)
        public bool IsTrayActive { get; private set; }
        public TrayKind ActiveKind { get; private set; } = TrayKind.None;

        public enum TrayKind { None, StatusNotifierItem, LegacyStatusIcon }

        /// <summary>Initialize the best available tray. Never throws — failures degrade to None.</summary>
        public void Init(Pixbuf icon, string appName, System.Action onActivate)
        {
            try
            {
                if (TryInitSni(icon, appName, onActivate)) return;
            }
            catch (Exception ex) { Log.Debug("SNI tray init failed: " + ex.Message); }

            // Fallback: legacy XEmbed tray only makes sense on X11 (it's invisible on Wayland)
            if (!RunningOnWayland)
            {
                try { TryInitLegacy(icon, appName, onActivate); return; }
                catch (Exception ex) { Log.Debug("Legacy tray init failed: " + ex.Message); }
            }

            Log.Debug("No system tray available; using minimize-to-taskbar fallback.");
        }

        // Wayland session heuristic (no SNI host + Wayland => no legacy tray either)
        private static bool RunningOnWayland =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

        private bool TryInitSni(Pixbuf icon, string appName, System.Action onActivate)
        {
            connection = new Connection(Address.Session);
            connection.ConnectAsync().GetAwaiter().GetResult();

            // Detect an SNI host (KDE / GNOME+AppIndicator / waybar provide this service)
            if (!connection.IsServiceActiveAsync(WatcherService).GetAwaiter().GetResult())
            {
                Log.Debug("No StatusNotifierWatcher on the bus (GNOME without AppIndicator extension, etc.).");
                connection.Dispose();
                connection = null;
                return false;
            }

            var props = new SniProperties
            {
                Id = "xdm-app",
                Title = appName,
                IconName = "xdm-app",     // themed icon (installed into hicolor by the packages)
                IconPixmap = new[] { PixbufToRgba(icon) },
                ToolTip = (0, 0, Array.Empty<byte>(), appName, ""),
            };
            sniItem = new XdmSniItem(props, () => Gtk.Application.Invoke((_, _) => onActivate()));

            connection.RegisterObjectAsync(sniItem).GetAwaiter().GetResult();
            connection.RegisterServiceAsync(WellKnownName, ServiceRegistrationOptions.Default).GetAwaiter().GetResult();

            var watcher = connection.CreateProxy<IStatusNotifierWatcher>(WatcherService, WatcherPath);
            // Per SNI spec: when the item lives at the default path /StatusNotifierItem,
            // register with the bus name only (no path suffix).
            watcher.RegisterStatusNotifierItemAsync(WellKnownName).GetAwaiter().GetResult();

            IsTrayActive = true;
            ActiveKind = TrayKind.StatusNotifierItem;
            Log.Debug("Tray: registered StatusNotifierItem (org.xdmapp.Tray).");
            return true;
        }

        private void TryInitLegacy(Pixbuf icon, string appName, System.Action onActivate)
        {
            legacyIcon = new StatusIcon(icon);
            legacyIcon.TooltipText = appName;
            legacyIcon.Activate += (_, _) => onActivate();
            legacyIcon.Visible = true;
            IsTrayActive = true;
            ActiveKind = TrayKind.LegacyStatusIcon;
            Log.Debug("Tray: using legacy Gtk.StatusIcon (X11/XEmbed).");
        }

        public void Dispose()
        {
            try
            {
                legacyIcon?.Dispose();
                legacyIcon = null;
                if (sniItem != null && connection != null)
                {
                    try { connection.UnregisterServiceAsync(WellKnownName).GetAwaiter().GetResult(); } catch { }
                }
                connection?.Dispose();
                connection = null;
                sniItem = null;
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
