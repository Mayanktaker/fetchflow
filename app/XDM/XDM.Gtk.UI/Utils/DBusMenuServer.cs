// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Minimal com.canonical.dbusmenu implementation for KDE Plasma 6 tray menus.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace XDM.GtkUI.Utils
{
    [Dictionary]
    public class DBusMenuProperties
    {
        public uint Version = 3;
        public string TextDirection = "ltr";
        public string Status = "normal";
        public string[] IconThemePath = Array.Empty<string>();
    }

    [DBusInterface("com.canonical.dbusmenu", PropertyType = typeof(DBusMenuProperties))]
    public interface IDBusMenu : IDBusObject
    {
        Task<(uint revision, (int id, IDictionary<string, object> properties, object[] children) layout)> GetLayoutAsync(int parentId, int recursionDepth, string[] propertyNames);
        Task<(int id, IDictionary<string, object> properties)[]> GetGroupPropertiesAsync(int[] ids, string[] propertyNames);
        Task<object> GetPropertyAsync(int id, string name);
        Task EventAsync(int id, string eventId, object data, uint timestamp);
        Task<bool> AboutToShowAsync(int id);
        
        // Property fetching
        Task<DBusMenuProperties> GetAllAsync();
        
        // Signals
        Task<IDisposable> WatchLayoutUpdatedAsync(Action<(uint revision, int parent)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchItemActivationRequestedAsync(Action<(int id, uint timestamp)> handler, Action<Exception> onError = null);
    }

    public class DBusMenuServer : IDBusMenu
    {
        private readonly System.Action onShow;
        private readonly System.Action onQuit;
        private uint revision = 1;
        private DBusMenuProperties props = new DBusMenuProperties();

        private const int RootId = 0;
        private const int ShowId = 1;
        private const int QuitId = 2;

        public event Action<(uint revision, int parent)> OnLayoutUpdated;
        public event Action<(int id, uint timestamp)> OnItemActivationRequested;

        public DBusMenuServer(System.Action onShow, System.Action onQuit)
        {
            this.onShow = onShow;
            this.onQuit = onQuit;
        }

        public ObjectPath ObjectPath => new("/MenuBar");

        public Task<DBusMenuProperties> GetAllAsync() => Task.FromResult(props);

        public Task<(uint revision, (int id, IDictionary<string, object> properties, object[] children) layout)> GetLayoutAsync(int parentId, int recursionDepth, string[] propertyNames)
        {
            var children = new List<object>();

            children.Add(MakeMenuItem(ShowId, new Dictionary<string, object> {
                { "label", "Show XDM" },
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

        public Task<(int id, IDictionary<string, object> properties)[]> GetGroupPropertiesAsync(int[] ids, string[] propertyNames)
        {
            var result = new List<(int, IDictionary<string, object>)>();
            foreach (var id in ids)
            {
                var p = GetItemProperties(id);
                result.Add((id, p));
            }
            return Task.FromResult(result.ToArray());
        }

        public Task<object> GetPropertyAsync(int id, string name)
        {
            var p = GetItemProperties(id);
            if (p.TryGetValue(name, out var value))
                return Task.FromResult(value);
            return Task.FromResult<object>("");
        }

        public Task EventAsync(int id, string eventId, object data, uint timestamp)
        {
            if (eventId == "clicked")
            {
                System.Action? handler = id switch
                {
                    ShowId => onShow,
                    QuitId => onQuit,
                    _ => null
                };
                var h = handler;
                if (h != null)
                    Gtk.Application.Invoke((_, _) => h());
            }
            return Task.CompletedTask;
        }

        public Task<bool> AboutToShowAsync(int id)
        {
            return Task.FromResult(false);
        }

        private static object MakeMenuItem(int id, IDictionary<string, object> properties)
        {
            return (id, properties, Array.Empty<object>());
        }

        private static IDictionary<string, object> GetItemProperties(int id)
        {
            return id switch
            {
                ShowId => new Dictionary<string, object> {
                    { "label", "Show XDM" },
                    { "type", "standard" },
                    { "enabled", true },
                    { "visible", true }
                },
                QuitId => new Dictionary<string, object> {
                    { "label", "Quit" },
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

        public Task<IDisposable> WatchLayoutUpdatedAsync(Action<(uint revision, int parent)> handler, Action<Exception> onError = null)
        {
            return SignalWatcher.AddAsync(this, nameof(OnLayoutUpdated), handler);
        }

        public Task<IDisposable> WatchItemActivationRequestedAsync(Action<(int id, uint timestamp)> handler, Action<Exception> onError = null)
        {
            return SignalWatcher.AddAsync(this, nameof(OnItemActivationRequested), handler);
        }
    }
}
