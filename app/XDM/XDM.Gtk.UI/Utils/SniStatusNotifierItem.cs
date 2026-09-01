// © 2026 Mayanktaker | Based on XDM by subhra74 (https://github.com/subhra74/xdm)
// D-Bus StatusNotifierItem (SNI) implementation — the modern tray protocol used by
// KDE Plasma 6, GNOME (via AppIndicator extension), Sway/Hyprland (waybar), COSMIC, etc.
// The legacy Gtk.StatusIcon (XEmbed) tray does not work on Wayland, so SNI is the
// cross-DE tray mechanism for FetchFlow on Wayland. See TrayIconManager for the fallback chain.
using System;
using System.Threading.Tasks;
using Tmds.DBus;

namespace XDM.GtkUI.Utils
{
    // Shape of the SNI properties — Tmds exposes these via GetAll and uses them for introspection.
    [Dictionary]
    public class SniProperties
    {
        public string Id = "";
        public string Category = "ApplicationStatus";
        public string Title = "";
        public string Status = "Active"; // Active | Passive | NeedsAttention
        public uint WindowId = 0;
        public ObjectPath Menu = new("/");
        public string IconName = "";
        public string IconThemePath = "";
        public string AttentionIconName = "";
        public string OverlayIconName = "";
        public bool ItemIsMenu = false;
        public (int width, int height, byte[] pixels)[] IconPixmap = Array.Empty<(int, int, byte[])>();
        public (int width, int height, byte[] pixels)[] AttentionIconPixmap = Array.Empty<(int, int, byte[])>();
        public (int width, int height, byte[] pixels)[] OverlayIconPixmap = Array.Empty<(int, int, byte[])>();
        public (int iconHint, int reserved, byte[] pix, string title, string subtitle) ToolTip =
            (0, 0, Array.Empty<byte>(), "", "");
    }

    // org.kde.StatusNotifierItem — methods + GetAll so the tray host can read our state.
    [DBusInterface("org.kde.StatusNotifierItem", PropertyType = typeof(SniProperties))]
    public interface IStatusNotifierItem : IDBusObject
    {
        Task<SniProperties> GetAllAsync();
        Task ActivateAsync(int x, int y);          // left-click
        Task SecondaryActivateAsync(int x, int y); // middle-click
        Task ContextMenuAsync(int x, int y);       // right-click
    }

    // org.kde.StatusNotifierWatcher — the tray host's registration service.
    [DBusInterface("org.kde.StatusNotifierWatcher")]
    public interface IStatusNotifierWatcher : IDBusObject
    {
        Task RegisterStatusNotifierItemAsync(string service);
    }

    /// <summary>Concrete SNI item exported on the session bus.</summary>
    public class XdmSniItem : IStatusNotifierItem
    {
        private readonly SniProperties props;
        private readonly System.Action onActivate;
        private readonly Action<int, int> onContextMenu;
        public XdmSniItem(SniProperties props, System.Action onActivate, Action<int, int> onContextMenu)
        {
            this.props = props;
            this.onActivate = onActivate;
            this.onContextMenu = onContextMenu;
        }
        public ObjectPath ObjectPath => new("/StatusNotifierItem");
        public Task<SniProperties> GetAllAsync() => Task.FromResult(props);
        public Task ActivateAsync(int x, int y) { onActivate?.Invoke(); return Task.CompletedTask; }
        public Task SecondaryActivateAsync(int x, int y) { onActivate?.Invoke(); return Task.CompletedTask; }
        public Task ContextMenuAsync(int x, int y) { onContextMenu?.Invoke(x, y); return Task.CompletedTask; }
    }
}
