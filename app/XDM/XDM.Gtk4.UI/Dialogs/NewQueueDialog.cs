// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Native GTK 4 dialog for creating a new download queue or adding downloads to an existing queue.
using System;
using System.Collections.Generic;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Core.Util;

namespace XDM.Gtk4UI.Dialogs
{
    // Modal dialog for naming a queue and choosing downloads to assign
    internal sealed class NewQueueDialog
    {
        private readonly Window window;
        private readonly Entry txtQueueName;
        private readonly List<(CheckButton Check, InProgressDownloadItem Item)> items = new();
        private readonly Action<DownloadQueue, bool> okAction;
        private readonly DownloadQueue? modifyingQueue;

        // Constructs the NewQueueDialog with layout and download list
        public NewQueueDialog(Window? parent, Action<DownloadQueue, bool> onConfirm, DownloadQueue? existingQueue = null)
        {
            okAction = onConfirm;
            modifyingQueue = existingQueue;

            window = Window.New();
            var titleText = existingQueue == null
                ? (TextResource.GetText("LBL_QUEUE_OPT1") ?? "New Download Queue")
                : (TextResource.GetText("MENU_ADD_QUEUE") ?? "Add to Queue");
            window.Title = titleText;
            window.Modal = true;
            window.SetDefaultSize(520, 420);
            if (parent != null) window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(titleText);
            window.SetTitlebar(header);

            var rootBox = Box.New(Orientation.Vertical, 12);
            rootBox.MarginTop = 16;
            rootBox.MarginBottom = 16;
            rootBox.MarginStart = 20;
            rootBox.MarginEnd = 20;

            // Queue name row
            var nameBox = Box.New(Orientation.Horizontal, 8);
            var lblName = Label.New(TextResource.GetText("MSG_QUEUE_NAME") ?? "Queue Name:");
            lblName.Halign = Align.Start;
            lblName.WidthRequest = 100;

            txtQueueName = Entry.New();
            txtQueueName.Hexpand = true;
            txtQueueName.SetText(existingQueue?.Name ?? $"Queue {QueueManager.Queues.Count + 1}");
            nameBox.Append(lblName);
            nameBox.Append(txtQueueName);
            rootBox.Append(nameBox);

            // Downloads list prompt
            var lblSelect = Label.New(TextResource.GetText("MSG_QUEUE_SELECT_ITEMS") ?? "Select downloads to include:");
            lblSelect.Halign = Align.Start;
            lblSelect.AddCssClass("dim-label");
            rootBox.Append(lblSelect);

            // Scrolled downloads list
            var scroll = ScrolledWindow.New();
            scroll.Vexpand = true;
            scroll.Hexpand = true;
            scroll.SetMinContentHeight(200);

            var listBox = ListBox.New();
            listBox.SelectionMode = SelectionMode.None;
            listBox.AddCssClass("rich-list");

            PopulateDownloadItems(listBox);
            scroll.SetChild(listBox);
            rootBox.Append(scroll);

            // Button row
            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 8;

            var btnCancel = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            btnCancel.OnClicked += (_, _) => window.Close();

            var btnOk = Button.NewWithLabel(TextResource.GetText("MSG_OK") ?? "OK");
            btnOk.AddCssClass("suggested-action");
            btnOk.OnClicked += (_, _) => OnOkClicked();

            buttonBox.Append(btnCancel);
            buttonBox.Append(btnOk);
            rootBox.Append(buttonBox);

            window.SetChild(rootBox);
        }

        // Populates unassigned or pending in-progress downloads
        private void PopulateDownloadItems(ListBox listBox)
        {
            var allItems = ApplicationContext.Application?.GetAllInProgressDownloads() ?? Array.Empty<InProgressDownloadItem>();
            foreach (var ent in allItems)
            {
                if (modifyingQueue != null && modifyingQueue.DownloadIds.Contains(ent.Id))
                {
                    continue;
                }

                var row = ListBoxRow.New();
                var rowBox = Box.New(Orientation.Horizontal, 8);
                rowBox.MarginTop = 6;
                rowBox.MarginBottom = 6;
                rowBox.MarginStart = 10;
                rowBox.MarginEnd = 10;

                var chk = CheckButton.New();
                var lblName = Label.New(ent.Name);
                lblName.Halign = Align.Start;
                lblName.Hexpand = true;
                lblName.Ellipsize = Pango.EllipsizeMode.End;

                var lblSize = Label.New(FormattingHelper.FormatSize(ent.Size));
                lblSize.Halign = Align.End;
                lblSize.AddCssClass("dim-label");

                rowBox.Append(chk);
                rowBox.Append(lblName);
                rowBox.Append(lblSize);
                row.SetChild(rowBox);

                items.Add((chk, ent));
                listBox.Append(row);
            }
        }

        // Handles OK button click, collects selected IDs, and fires the callback
        private void OnOkClicked()
        {
            var name = txtQueueName.GetText()?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            var selectedIds = new List<string>();
            foreach (var (chk, ent) in items)
            {
                if (chk.Active)
                {
                    selectedIds.Add(ent.Id);
                }
            }

            if (modifyingQueue == null)
            {
                var newQueue = new DownloadQueue(Guid.NewGuid().ToString(), name)
                {
                    DownloadIds = selectedIds
                };
                okAction(newQueue, true);
            }
            else
            {
                modifyingQueue.Name = name;
                modifyingQueue.DownloadIds.AddRange(selectedIds);
                okAction(modifyingQueue, false);
            }

            window.Close();
        }

        // Shows the modal dialog
        public void Show() => window.Present();
    }
}
