// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// D-Bus StatusNotifierItem (SNI) and DBusMenu definitions for the GTK 4 frontend.
// Modern tray protocol supported on KDE Plasma, GNOME (AppIndicator), Sway/Waybar, and COSMIC.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace XDM.Gtk4UI.Utils
{
    // Properties dictionary exported over org.kde.StatusNotifierItem
    [Dictionary]
    public class SniProperties
    {
        public string Id = "";
        public string Category = "ApplicationStatus";
        public string Title = "";
        public string Status = "Active";
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

    // org.kde.StatusNotifierItem D-Bus interface
    [DBusInterface("org.kde.StatusNotifierItem", PropertyType = typeof(SniProperties))]
    public interface IStatusNotifierItem : IDBusObject
    {
        Task<SniProperties> GetAllAsync();
        Task ActivateAsync(int x, int y);
        Task SecondaryActivateAsync(int x, int y);
        Task ContextMenuAsync(int x, int y);
    }

    // org.kde.StatusNotifierWatcher registration proxy interface
    [DBusInterface("org.kde.StatusNotifierWatcher")]
    public interface IStatusNotifierWatcher : IDBusObject
    {
        Task RegisterStatusNotifierItemAsync(string service);
    }

    // Concrete StatusNotifierItem implementation exported on the session bus
    public sealed class Gtk4SniItem : IStatusNotifierItem
    {
        private readonly SniProperties props;
        private readonly Action onActivate;
        private readonly Action<int, int> onContextMenu;

        // Initializes SNI properties and interaction delegates
        public Gtk4SniItem(SniProperties props, Action onActivate, Action<int, int> onContextMenu)
        {
            this.props = props;
            this.onActivate = onActivate;
            this.onContextMenu = onContextMenu;
        }

        // Canonical object path for this SNI item
        public ObjectPath ObjectPath => new("/StatusNotifierItem");

        // Returns all SNI properties to the host
        public Task<SniProperties> GetAllAsync() => Task.FromResult(props);

        // Activates the window on primary click
        public Task ActivateAsync(int x, int y)
        {
            onActivate?.Invoke();
            return Task.CompletedTask;
        }

        // Activates the window on secondary click
        public Task SecondaryActivateAsync(int x, int y)
        {
            onActivate?.Invoke();
            return Task.CompletedTask;
        }

        // Displays context menu on right click
        public Task ContextMenuAsync(int x, int y)
        {
            onContextMenu?.Invoke(x, y);
            return Task.CompletedTask;
        }
    }

    // Properties dictionary exported over com.canonical.dbusmenu
    [Dictionary]
    public class DBusMenuProperties
    {
        public uint Version = 3;
        public string TextDirection = "ltr";
        public string Status = "normal";
        public string[] IconThemePath = Array.Empty<string>();
    }

    // com.canonical.dbusmenu interface for rendering tray context menus on KDE/Waybar
    [DBusInterface("com.canonical.dbusmenu", PropertyType = typeof(DBusMenuProperties))]
    public interface IDBusMenu : IDBusObject
    {
        Task<(uint revision, (int id, IDictionary<string, object> properties, object[] children) layout)> GetLayoutAsync(int parentId, int recursionDepth, string[] propertyNames);
        Task<(int id, IDictionary<string, object> properties)[]> GetGroupPropertiesAsync(int[] ids, string[] propertyNames);
        Task<object> GetPropertyAsync(int id, string name);
        Task EventAsync(int id, string eventId, object data, uint timestamp);
        Task<bool> AboutToShowAsync(int id);
        Task<DBusMenuProperties> GetAllAsync();
        Task<IDisposable> WatchLayoutUpdatedAsync(Action<(uint revision, int parent)> handler, Action<Exception>? onError = null);
        Task<IDisposable> WatchItemActivationRequestedAsync(Action<(int id, uint timestamp)> handler, Action<Exception>? onError = null);
    }

    // Concrete implementation of com.canonical.dbusmenu
    public sealed class Gtk4DBusMenuServer : IDBusMenu
    {
        private readonly Action onShow;
        private readonly Action onQuit;
        private uint revision = 1;
        private readonly DBusMenuProperties props = new();
        private string? speedStatus;

        private const int RootId = 0;
        private const int ShowId = 1;
        private const int QuitId = 2;
        private const int SpeedStatusId = 3;

        public event Action<(uint revision, int parent)>? OnLayoutUpdated;
        public event Action<(int id, uint timestamp)>? OnItemActivationRequested;

        // Initializes DBusMenu with callbacks for Show and Quit
        public Gtk4DBusMenuServer(Action onShow, Action onQuit)
        {
            this.onShow = onShow;
            this.onQuit = onQuit;
        }

        // Updates optional transfer speed indicator in the menu layout
        public void UpdateSpeedStatus(string? status)
        {
            if (speedStatus == status) return;
            speedStatus = status;
            revision++;
            OnLayoutUpdated?.Invoke((revision, RootId));
        }

        // Object path for menu layout
        public ObjectPath ObjectPath => new("/MenuBar");

        // Returns menu properties
        public Task<DBusMenuProperties> GetAllAsync() => Task.FromResult(props);

        // Returns the full menu layout tree
        public Task<(uint revision, (int id, IDictionary<string, object> properties, object[] children) layout)> GetLayoutAsync(int parentId, int recursionDepth, string[] propertyNames)
        {
            var children = new List<object>();

            if (!string.IsNullOrEmpty(speedStatus))
            {
                children.Add(MakeMenuItem(SpeedStatusId, new Dictionary<string, object> {
                    { "label", $"⚡ {speedStatus}" },
                    { "type", "standard" },
                    { "enabled", false },
                    { "visible", true }
                }));

                children.Add(MakeMenuItem(98, new Dictionary<string, object> {
                    { "type", "separator" },
                    { "visible", true }
                }));
            }

            children.Add(MakeMenuItem(ShowId, new Dictionary<string, object> {
                { "label", "Show FetchFlow" },
                { "icon-name", "view-restore" },
                { "type", "standard" },
                { "enabled", true },
                { "visible", true }
            }));

            children.Add(MakeMenuItem(99, new Dictionary<string, object> {
                { "type", "separator" },
                { "visible", true }
            }));

            children.Add(MakeMenuItem(QuitId, new Dictionary<string, object> {
                { "label", "Quit" },
                { "icon-name", "application-exit" },
                { "type", "standard" },
                { "enabled", true },
                { "visible", true }
            }));

            IDictionary<string, object> rootProps = new Dictionary<string, object> {
                { "children-display", "root" }
            };
            var layout = (RootId, rootProps, children.ToArray());
            return Task.FromResult((revision, layout));
        }

        // Returns grouped properties for menu items
        public Task<(int id, IDictionary<string, object> properties)[]> GetGroupPropertiesAsync(int[] ids, string[] propertyNames)
        {
            var result = new List<(int, IDictionary<string, object>)>();
            foreach (var id in ids)
            {
                result.Add((id, GetItemProperties(id)));
            }
            return Task.FromResult(result.ToArray());
        }

        // Returns individual item property
        public Task<object> GetPropertyAsync(int id, string name)
        {
            var p = GetItemProperties(id);
            if (p.TryGetValue(name, out var value))
                return Task.FromResult(value);
            return Task.FromResult<object>("");
        }

        // Handles menu item activation events
        public Task EventAsync(int id, string eventId, object data, uint timestamp)
        {
            if (eventId == "clicked")
            {
                Action? handler = id switch
                {
                    ShowId => onShow,
                    QuitId => onQuit,
                    _ => null
                };
                if (handler != null)
                {
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        handler();
                        return false;
                    });
                }
            }
            return Task.CompletedTask;
        }

        // Informs host before showing menu
        public Task<bool> AboutToShowAsync(int id) => Task.FromResult(false);

        // Helper building individual menu node
        private static object MakeMenuItem(int id, IDictionary<string, object> properties)
        {
            return (id, properties, Array.Empty<object>());
        }

        // Builds dictionary of properties for a given menu ID
        private IDictionary<string, object> GetItemProperties(int id)
        {
            return id switch
            {
                SpeedStatusId => new Dictionary<string, object> {
                    { "label", $"⚡ {speedStatus}" },
                    { "type", "standard" },
                    { "enabled", false },
                    { "visible", true }
                },
                98 => new Dictionary<string, object> {
                    { "type", "separator" },
                    { "visible", true }
                },
                ShowId => new Dictionary<string, object> {
                    { "label", "Show FetchFlow" },
                    { "icon-name", "view-restore" },
                    { "type", "standard" },
                    { "enabled", true },
                    { "visible", true }
                },
                QuitId => new Dictionary<string, object> {
                    { "label", "Quit" },
                    { "icon-name", "application-exit" },
                    { "type", "standard" },
                    { "enabled", true },
                    { "visible", true }
                },
                99 => new Dictionary<string, object> {
                    { "type", "separator" },
                    { "visible", true }
                },
                _ => new Dictionary<string, object>()
            };
        }

        // Watches layout update signal
        public Task<IDisposable> WatchLayoutUpdatedAsync(Action<(uint revision, int parent)> handler, Action<Exception>? onError = null)
        {
            return SignalWatcher.AddAsync(this, nameof(OnLayoutUpdated), handler);
        }

        // Watches item activation request signal
        public Task<IDisposable> WatchItemActivationRequestedAsync(Action<(int id, uint timestamp)> handler, Action<Exception>? onError = null)
        {
            return SignalWatcher.AddAsync(this, nameof(OnItemActivationRequested), handler);
        }
    }
}
