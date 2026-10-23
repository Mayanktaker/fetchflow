// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Data store for the GTK 4 download list. Holds the rows for the active view,
// the search keyword and the persisted sort state. The list widget keeps its own
// Gio.ListStore of row ids and asks this store for the row behind an id, so a row
// never has to be rebuilt just because its progress changed.
using System;
using System.Collections.Generic;
using System.Linq;
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
        // Rows for the active view, in insertion order (stable sort input)
        private readonly List<DownloadItem> allItems = new();
        // Id -> row, so a list row can resolve its data in O(1)
        private readonly Dictionary<string, DownloadItem> byId = new(StringComparer.Ordinal);
        // Rows after the keyword filter and the active sort
        private readonly List<DownloadItem> visible = new();
        // Current keyword from the toolbar search (empty = no filter)
        private string keyword = "";

        // Current sort column and direction
        public DownloadSortColumn CurrentSortColumn { get; private set; } = DownloadSortColumn.None;
        public bool SortAscending { get; private set; } = true;

        // Rows currently displayed (filtered and sorted)
        public IReadOnlyList<DownloadItem> Items => visible;

        // Row count of the filtered view
        public int Count => visible.Count;

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

        // Replaces the whole data set for the active view
        public void SetAll(IEnumerable<DownloadItem> source)
        {
            allItems.Clear();
            byId.Clear();
            foreach (var item in source)
            {
                allItems.Add(item);
                byId[item.Id] = item;
            }
            Rebuild();
        }

        // Applies the search keyword and rebuilds the visible set
        public void Filter(string? searchKeyword)
        {
            keyword = searchKeyword ?? "";
            Rebuild();
        }

        // Returns the row behind an id, or null when it is no longer in this view
        public DownloadItem? Lookup(string id) =>
            byId.TryGetValue(id, out var item) ? item : null;

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

        // Rebuilds the visible list from the keyword and the active sort
        private void Rebuild()
        {
            IEnumerable<DownloadItem> filtered = allItems.Where(i => i.Matches(keyword));

            filtered = (CurrentSortColumn, SortAscending) switch
            {
                (DownloadSortColumn.Name, true) => filtered.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                (DownloadSortColumn.Name, false) => filtered.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase),
                (DownloadSortColumn.Size, true) => filtered.OrderBy(i => i.RawSize),
                (DownloadSortColumn.Size, false) => filtered.OrderByDescending(i => i.RawSize),
                (DownloadSortColumn.ProgressOrDate, true) => filtered.OrderBy(SortKey),
                (DownloadSortColumn.ProgressOrDate, false) => filtered.OrderByDescending(SortKey),
                _ => filtered
            };

            visible.Clear();
            visible.AddRange(filtered);
        }

        // Sort key for the date/progress column: finish date for complete rows,
        // live progress for active rows (matches the GTK 3 frontend)
        private static long SortKey(DownloadItem item) =>
            item.IsComplete ? item.DateAdded.Ticks : (long)(item.Progress * 10000);
    }
}
