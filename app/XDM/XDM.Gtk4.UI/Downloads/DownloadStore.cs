// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// List model for the GTK 4 download list. Gtk.StringList is the Gio.ListModel
// GTK 4 consumes; the parallel Items list keeps the rich row data in the same
// order, so a row's ListItem.Position resolves back to its DownloadItem.
using System.Collections.Generic;
using System.Linq;
using Gtk;

namespace XDM.Gtk4UI.Downloads
{
    internal sealed class DownloadStore
    {
        // Backing data, always in the same order as the exposed model
        private readonly List<DownloadItem> items = new();
        // Full (unfiltered) set, so search can restore rows
        private readonly List<DownloadItem> allItems = new();
        // Current keyword from the toolbar search (empty = no filter)
        private string keyword = "";

        // Model handed to the list widget; replaced on every change
        public StringList Model { get; private set; } = StringList.New(System.Array.Empty<string>());

        // Rows currently displayed (filtered view)
        public IReadOnlyList<DownloadItem> Items => items;

        // Row count of the filtered view
        public int Count => items.Count;

        // Replaces the whole data set (used while core wiring is pending)
        public void SetAll(IEnumerable<DownloadItem> source)
        {
            allItems.Clear();
            allItems.AddRange(source);
            Rebuild();
        }

        // Applies the search keyword and rebuilds the model
        public void Filter(string? searchKeyword)
        {
            keyword = searchKeyword ?? "";
            Rebuild();
        }

        // Rebuilds items + StringList from allItems and the active keyword
        private void Rebuild()
        {
            items.Clear();
            items.AddRange(allItems.Where(i => i.Matches(keyword)));
            Model = StringList.New(items.Select(i => i.Name).ToArray());
        }
    }
}
