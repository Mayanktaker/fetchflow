// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Minimal com.canonical.dbusmenu implementation for KDE Plasma 6 tray menus.
// KDE's StatusNotifierHost expects a DBusMenu object at the SNI Menu property path;
// without it, right-click does nothing. This serves a flat menu with "Show XDM" and "Quit".
// Based on the freedesktop DBusMenu spec: https://wiki.ubuntu.com/DBusMenu
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace XDM.GtkUI.Utils
{
    // com.canonical.dbusmenu interface methods required by KDE tray hosts.
    [DBusInterface("com.canonical.dbusmenu")]
    public interface IDBusMenu : IDBusObject
    {
        // Returns the menu layout: (revision, (id, properties, children))
        Task<object> GetLayoutAsync(int parentId, int recursionDepth, string[] propertyNames);
        // Returns properties for a list of menu item IDs
        Task<object> GetGroupPropertiesAsync(int[] ids, string[] propertyNames);
        // Returns a single property for a menu item
        Task<object> GetPropertyAsync(int id, string name);
        // Handles a menu item event (click, hover, etc.)
        Task EventAsync(int id, string eventId, object data, uint timestamp);
        // Called before showing a submenu (returns true if layout needs update)
        Task<object> AboutToShowAsync(int id);
    }

    /// <summary>Minimal DBusMenu server for XDM's tray context menu.</summary>
    public class DBusMenuServer : IDBusMenu
    {
        private readonly System.Action onShow;
        private readonly System.Action onQuit;
        private uint revision = 1;

        // Menu item IDs
        private const int RootId = 0;
        private const int ShowId = 1;
        private const int QuitId = 2;

        public DBusMenuServer(System.Action onShow, System.Action onQuit)
        {
            this.onShow = onShow;
            this.onQuit = onQuit;
        }

        public ObjectPath ObjectPath => new("/MenuBar");

        // GetLayout: returns the full menu tree as (revision, (id, props, children)).
        // recursionDepth=-1 means return everything; 0 means just the parent.
        public Task<object> GetLayoutAsync(int parentId, int recursionDepth, string[] propertyNames)
        {
            var children = new List<object>();

            // "Show XDM" item
            children.Add(MakeMenuItem(ShowId, new Dictionary<string, object> {
                { "label", "Show XDM" },
                { "type", "standard" },
                { "enabled", true },
                { "visible", true }
            }));

            // Separator
            children.Add(MakeMenuItem(99, new Dictionary<string, object> {
                { "type", "separator" },
                { "visible", true }
            }));

            // "Quit" item
            children.Add(MakeMenuItem(QuitId, new Dictionary<string, object> {
                { "label", "Quit" },
                { "type", "standard" },
                { "enabled", true },
                { "visible", true }
            }));

            // Root layout: (id, properties, children)
            var rootProps = new Dictionary<string, object> {
                { "children-display", "root" }
            };
            var layout = (RootId, rootProps, children.ToArray());
            var result = (revision, layout);
            return Task.FromResult<object>(result);
        }

        public Task<object> GetGroupPropertiesAsync(int[] ids, string[] propertyNames)
        {
            var result = new List<object>();
            foreach (var id in ids)
            {
                var props = GetItemProperties(id);
                result.Add((id, props));
            }
            return Task.FromResult<object>(result.ToArray());
        }

        public Task<object> GetPropertyAsync(int id, string name)
        {
            var props = GetItemProperties(id);
            if (props.TryGetValue(name, out var value))
                return Task.FromResult<object>(value);
            return Task.FromResult<object>("");
        }

        // Event: called when user clicks a menu item.
        // eventId "clicked" is the standard click event.
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
                // Marshal to the main thread via Application.Invoke
                var h = handler;
                if (h != null)
                    Gtk.Application.Invoke((_, _) => h());
            }
            return Task.CompletedTask;
        }

        public Task<object> AboutToShowAsync(int id)
        {
            // No dynamic menu updates needed; return false (no update needed)
            return Task.FromResult<object>(false);
        }

        // Build a menu item tuple: (id, Dictionary<string, variant>)
        private static object MakeMenuItem(int id, Dictionary<string, object> properties)
        {
            return (id, properties, Array.Empty<object>());
        }

        // Return properties for a given menu item ID.
        private static Dictionary<string, object> GetItemProperties(int id)
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
    }
}
