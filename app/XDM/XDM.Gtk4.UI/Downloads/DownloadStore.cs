// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// List model for the GTK 4 download list. Gtk.StringList is the Gio.ListModel
// GTK 4 consumes; the parallel Items list keeps the rich row data in the same
// order, so a row's ListItem.Position resolves back to its DownloadItem.
using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using XDM.Core;

namespace XDM.Gtk4UI.Downloads
{
    // Sorting criteria supported by the download list
    internal enum DownloadSortColumn
    {
        None,
        Name,
        Size,
        ProgressOrDate
    }

    internal sealed class DownloadStore
    {
        // Backing data, always in the same order as the exposed model
        private readonly List<DownloadItem> items = new();
        // Full (unfiltered) set, so search can restore rows
        private readonly List<DownloadItem> allItems = new();
        // Current keyword from the toolbar search (empty = no filter)
        private string keyword = "";

        // Current sort column and direction
        public DownloadSortColumn CurrentSortColumn { get; private set; } = DownloadSortColumn.None;
        public bool SortAscending { get; private set; } = true;

        // Model handed to the list widget; replaced on every change
        public StringList Model { get; private set; } = StringList.New(Array.Empty<string>());

        // Rows currently displayed (filtered view)
        public IReadOnlyList<DownloadItem> Items => items;

        // Row count of the filtered view
        public int Count => items.Count;

        // Initializes sort preferences from persistent application configuration
        public DownloadStore()
        {
            var savedCol = Config.Instance.DownloadSortColumn;
            CurrentSortColumn = savedCol switch
            {
                "Name" => DownloadSortColumn.Name,
                "Size" => DownloadSortColumn.Size,
                _ => DownloadSortColumn.ProgressOrDate
            };
            SortAscending = !Config.Instance.DownloadSortDescending;
        }

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

        // Sorts by the specified column and direction and saves preference
        public void SortBy(DownloadSortColumn col, bool ascending)
        {
            CurrentSortColumn = col;
            SortAscending = ascending;
            Config.Instance.DownloadSortColumn = col switch
            {
                DownloadSortColumn.Name => "Name",
                DownloadSortColumn.Size => "Size",
                _ => "Date"
            };
            Config.Instance.DownloadSortDescending = !ascending;
            try { Config.SaveConfig(); } catch { }
            Rebuild();
        }

        // Toggles sorting on the specified column
        public void ToggleSort(DownloadSortColumn col)
        {
            if (CurrentSortColumn == col)
            {
                SortBy(col, !SortAscending);
            }
            else
            {
                SortBy(col, true);
            }
        }

        // Rebuilds items + StringList from allItems, keyword, and active sort
        private void Rebuild()
        {
            items.Clear();
            IEnumerable<DownloadItem> filtered = allItems.Where(i => i.Matches(keyword));

            if (CurrentSortColumn != DownloadSortColumn.None)
            {
                filtered = (CurrentSortColumn, SortAscending) switch
                {
                    (DownloadSortColumn.Name, true) => filtered.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                    (DownloadSortColumn.Name, false) => filtered.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase),
                    (DownloadSortColumn.Size, true) => filtered.OrderBy(i => i.RawSize),
                    (DownloadSortColumn.Size, false) => filtered.OrderByDescending(i => i.RawSize),
                    (DownloadSortColumn.ProgressOrDate, true) => filtered.OrderBy(i => i.IsComplete ? i.DateAdded.Ticks : (long)(i.Progress * 10000)),
                    (DownloadSortColumn.ProgressOrDate, false) => filtered.OrderByDescending(i => i.IsComplete ? i.DateAdded.Ticks : (long)(i.Progress * 10000)),
                    _ => filtered
                };
            }

            items.AddRange(filtered);
            Model = StringList.New(items.Select(i => i.Name).ToArray());
        }
    }
}
