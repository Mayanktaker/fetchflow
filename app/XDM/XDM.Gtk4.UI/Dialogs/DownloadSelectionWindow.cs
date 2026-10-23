// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 download-selection window: the confirmation step for Batch Download and
// browser-captured link lists. Driven by the shared DownloadSelectionUIController,
// so the selection/download behaviour is identical to the GTK 3 frontend.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Core.UI;

using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    internal sealed class DownloadSelectionWindow : IDownloadSelectionView
    {
        // Modal window and its controls
        private readonly Window window;
        private readonly Entry folderEntry;
        private readonly ListBox list;
        private readonly CheckButton selectAll;
        // Rows currently offered to the user
        private readonly List<SelectionEntry> entries = new();
        // Guards programmatic select-all updates from re-entering the toggle
        private bool syncingSelectAll;

        public event EventHandler? BrowseClicked;
        public event EventHandler? DownloadClicked;
        public event EventHandler? QueueSchedulerClicked;
        public event EventHandler<DownloadLaterEventArgs>? DownloadLaterClicked;

        // Folder the selected downloads will be written to
        public string DownloadLocation
        {
            get => folderEntry.GetText() ?? "";
            set => folderEntry.SetText(value ?? "");
        }

        // Not exposed in this window; kept for the shared view contract
        public AuthenticationInfo? Authentication { get; set; }
        public ProxyInfo? Proxy { get; set; }
        public int SpeedLimit { get; set; }
        public bool EnableSpeedLimit { get; set; }

        // Number of ticked rows
        public int SelectedRowCount => entries.Count(e => e.Wrapper.IsSelected);

        // Ticked rows in display order
        public IEnumerable<IDownloadEntryWrapper> SelectedItems =>
            entries.Where(e => e.Wrapper.IsSelected).Select(e => e.Wrapper);

        // Builds the window shell
        public DownloadSelectionWindow(Window? parent)
        {
            window = Window.New();
            window.Title = TextResource.GetText("MENU_BATCH_DOWNLOAD") ?? "Downloads";
            window.Modal = true;
            window.SetDefaultSize(620, 460);
            if (parent != null)
            {
                window.SetTransientFor(parent);
            }

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            var downloadBtn = Button.NewWithLabel(TextResource.GetText("LBL_DOWNLOAD") ?? "Download");
            downloadBtn.AddCssClass("suggested-action");
            downloadBtn.OnClicked += (_, _) => UiGuard.Run(window, "Download selected links", () => DownloadClicked?.Invoke(this, EventArgs.Empty));
            header.PackEnd(downloadBtn);
            window.SetTitlebar(header);

            var root = Box.New(Orientation.Vertical, 10);
            root.MarginTop = 12;
            root.MarginBottom = 12;
            root.MarginStart = 16;
            root.MarginEnd = 16;

            // Destination row
            var folderRow = Box.New(Orientation.Horizontal, 8);
            folderRow.Append(Label.New(TextResource.GetText("LBL_FOLDER") ?? "Folder"));
            folderEntry = Entry.New();
            folderEntry.Hexpand = true;
            folderRow.Append(folderEntry);
            var browseBtn = Button.NewWithLabel(TextResource.GetText("CTX_BROWSE") ?? "Browse...");
            browseBtn.OnClicked += (_, _) =>
            {
                BrowseClicked?.Invoke(this, EventArgs.Empty);
                // The controller sets DownloadLocation after its own picker returns
                folderEntry.SetText(DownloadLocation);
            };
            folderRow.Append(browseBtn);
            root.Append(folderRow);

            // Select-all row
            var selectRow = Box.New(Orientation.Horizontal, 8);
            selectAll = CheckButton.NewWithLabel(TextResource.GetText("LBL_SELECT_ALL") ?? "Select All");
            selectAll.OnToggled += (_, _) =>
            {
                if (syncingSelectAll)
                {
                    return;
                }
                SetAllSelected(selectAll.Active);
            };
            selectRow.Append(selectAll);
            root.Append(selectRow);

            // Entry list
            list = ListBox.New();
            list.AddCssClass("selection-list");
            list.SelectionMode = SelectionMode.None;
            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            scrolled.SetChild(list);
            scrolled.Vexpand = true;
            root.Append(scrolled);

            // Action row
            var actions = Box.New(Orientation.Horizontal, 8);
            actions.Halign = Align.End;
            var schedulerBtn = Button.NewWithLabel(TextResource.GetText("DESC_Q_TITLE") ?? "Queue & Scheduler");
            schedulerBtn.OnClicked += (_, _) => QueueSchedulerClicked?.Invoke(this, EventArgs.Empty);
            var laterBtn = Button.NewWithLabel(TextResource.GetText("LBL_DOWNLOAD_LATER") ?? "Download Later");
            laterBtn.OnClicked += (_, _) =>
                UiGuard.Run(window, "Queue selected links", () => DownloadLaterClicked?.Invoke(this, new DownloadLaterEventArgs(null)));
            var cancelBtn = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            cancelBtn.OnClicked += (_, _) => window.Close();
            actions.Append(schedulerBtn);
            actions.Append(laterBtn);
            actions.Append(cancelBtn);
            root.Append(actions);

            window.SetChild(root);
        }

        // Fills the window with the entries the controller prepared
        public void SetData(FileNameFetchMode mode, IEnumerable<IRequestData> downloads,
            Func<IRequestData, IDownloadEntryWrapper, bool> populateEntryWrapper)
        {
            entries.Clear();
            RemoveAllRows();

            foreach (var download in downloads)
            {
                var wrapper = new SelectionEntryWrapper();
                if (!populateEntryWrapper(download, wrapper))
                {
                    continue;
                }

                var row = new SelectionEntry(wrapper);
                entries.Add(row);
                list.Append(BuildRow(row));
            }

            selectAll.Active = entries.Count > 0;
        }

        // Picks the destination folder through the desktop portal
        public string? SelectFolder()
        {
            try
            {
                var dialog = FileDialog.New();
                dialog.Title = TextResource.GetText("LBL_FOLDER") ?? "Select Folder";
                dialog.Modal = true;
                if (!string.IsNullOrEmpty(DownloadLocation) && Directory.Exists(DownloadLocation))
                {
                    dialog.SetInitialFolder(Gio.FileHelper.NewForPath(DownloadLocation));
                }

                string? selected = null;
                var loop = GLib.MainLoop.New(null, false);
                dialog.SelectFolderAsync(window).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && t.Result != null)
                    {
                        selected = t.Result.GetPath();
                    }
                    loop.Quit();
                });
                loop.Run();
                return selected;
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Selection folder picker failed: " + ex.Message);
                return null;
            }
        }

        // Presents the window to the user
        public void ShowWindow() => window.Present();

        // Closes the window after a download starts
        public void CloseWindow() => window.Close();

        // Builds one checkable row with the file name and its transfer type
        private Widget BuildRow(SelectionEntry entry)
        {
            var row = ListBoxRow.New();
            var box = Box.New(Orientation.Horizontal, 10);
            box.MarginTop = 4;
            box.MarginBottom = 4;
            box.MarginStart = 6;
            box.MarginEnd = 6;

            var check = CheckButton.New();
            check.Active = entry.Wrapper.IsSelected;
            check.OnToggled += (_, _) =>
            {
                entry.Wrapper.IsSelected = check.Active;
                SyncSelectAll();
            };
            box.Append(check);

            var textBox = Box.New(Orientation.Vertical, 2);
            textBox.Hexpand = true;
            var name = Label.New(entry.Wrapper.Name);
            name.Halign = Align.Start;
            name.Ellipsize = Pango.EllipsizeMode.End;
            var type = Label.New(entry.Wrapper.EntryType ?? "");
            type.Halign = Align.Start;
            type.AddCssClass("dim-label");
            textBox.Append(name);
            textBox.Append(type);
            box.Append(textBox);

            row.SetChild(box);
            return row;
        }

        // Ticks or unticks every entry
        private void SetAllSelected(bool selected)
        {
            syncingSelectAll = true;
            try
            {
                foreach (var entry in entries)
                {
                    entry.Wrapper.IsSelected = selected;
                }

                var child = list.GetFirstChild();
                while (child != null)
                {
                    var next = child.GetNextSibling();
                    if (child is ListBoxRow row && row.GetChild() is Box box &&
                        box.GetFirstChild() is CheckButton check)
                    {
                        check.Active = selected;
                    }
                    child = next;
                }
            }
            finally
            {
                syncingSelectAll = false;
            }
        }

        // Keeps the select-all box in step with the individual rows
        private void SyncSelectAll()
        {
            syncingSelectAll = true;
            try
            {
                selectAll.Active = entries.Count > 0 && entries.All(e => e.Wrapper.IsSelected);
            }
            finally
            {
                syncingSelectAll = false;
            }
        }

        // Clears the list rows
        private void RemoveAllRows()
        {
            Widget? child;
            while ((child = list.GetFirstChild()) != null)
            {
                list.Remove(child);
            }
        }

        // One row in the selection list
        private sealed class SelectionEntry
        {
            public SelectionEntryWrapper Wrapper { get; }

            public SelectionEntry(SelectionEntryWrapper wrapper)
            {
                Wrapper = wrapper;
            }
        }

        // Mutable wrapper handed to the shared controller
        private sealed class SelectionEntryWrapper : IDownloadEntryWrapper
        {
            public string Name { get; set; } = "";
            public bool IsSelected { get; set; } = true;
            public IRequestData DownloadEntry { get; set; } = null!;
            public string EntryType { get; set; } = "";
        }
    }
}
