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
        private DBusMenuServer? dbusMenuServer;
        private StatusIcon? legacyIcon;
        private System.Action? onActivate;

        // True when ANY tray mechanism is active (MainWindow uses this to allow hide-to-tray on close)
        public bool IsTrayActive { get; private set; }
        public TrayKind ActiveKind { get; private set; } = TrayKind.None;

        public enum TrayKind { None, StatusNotifierItem, LegacyStatusIcon }

        /// <summary>Initialize the best available tray. Never throws — failures degrade to None.</summary>
        public void Init(Pixbuf icon, string appName, System.Action onActivate, System.Action onQuit)
        {
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

            Log.Debug("No system tray available; using minimize-to-taskbar fallback.");
        }

        // Wayland session heuristic (no SNI host + Wayland => no legacy tray either)
        private static bool RunningOnWayland =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

        private bool TryInitSni(Pixbuf icon, string appName, System.Action onActivate, System.Action onQuit)
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

            connection.RegisterObjectAsync(dbusMenuServer).GetAwaiter().GetResult();
            connection.RegisterObjectAsync(sniItem).GetAwaiter().GetResult();
            connection.RegisterServiceAsync(WellKnownName, ServiceRegistrationOptions.Default).GetAwaiter().GetResult();

            var watcher = connection.CreateProxy<IStatusNotifierWatcher>(WatcherService, WatcherPath);
            watcher.RegisterStatusNotifierItemAsync(WellKnownName).GetAwaiter().GetResult();

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
