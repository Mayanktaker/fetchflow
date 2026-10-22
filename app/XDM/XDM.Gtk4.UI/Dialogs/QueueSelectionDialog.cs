// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Native GTK 4 dialog for selecting a destination queue when moving downloads between queues.
using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using Translations;
using XDM.Core.UI;

namespace XDM.Gtk4UI.Dialogs
{
    // Queue destination selection modal dialog
    internal sealed class QueueSelectionDialog : IQueueSelectionDialog
    {
        private readonly Window window;
        private readonly DropDown dropDown;
        private readonly Button btnOk;
        private readonly Button btnCancel;
        private readonly List<string> names = new();
        private readonly List<string> ids = new();
        private string[] downloadIds = Array.Empty<string>();

        public event EventHandler<QueueSelectionEventArgs>? QueueSelected;

        // Constructs the queue selection dialog
        public QueueSelectionDialog(Window? parent)
        {
            window = Window.New();
            var title = TextResource.GetText("LBL_QUEUE_OPT1") ?? "Select Queue";
            window.Title = title;
            window.Modal = true;
            window.SetDefaultSize(380, 160);
            if (parent != null) window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(title);
            window.SetTitlebar(header);

            var rootBox = Box.New(Orientation.Vertical, 12);
            rootBox.MarginTop = 16;
            rootBox.MarginBottom = 16;
            rootBox.MarginStart = 20;
            rootBox.MarginEnd = 20;

            var lblPrompt = Label.New(TextResource.GetText("LBL_QUEUE_PROMPT") ?? "Move selected downloads to queue:");
            lblPrompt.Halign = Align.Start;
            rootBox.Append(lblPrompt);

            dropDown = DropDown.NewFromStrings(Array.Empty<string>());
            dropDown.Hexpand = true;
            rootBox.Append(dropDown);

            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 8;

            btnCancel = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            btnCancel.OnClicked += (_, _) => window.Close();

            btnOk = Button.NewWithLabel(TextResource.GetText("ND_OK") ?? "OK");
            btnOk.AddCssClass("suggested-action");
            btnOk.OnClicked += (_, _) => OnOkClicked();

            buttonBox.Append(btnCancel);
            buttonBox.Append(btnOk);
            rootBox.Append(buttonBox);

            window.SetChild(rootBox);
        }

        // Populates queue choices and cached download IDs
        public void SetData(IEnumerable<string> queueNames, IEnumerable<string> queueIds, IEnumerable<string> downloadIds)
        {
            names.Clear();
            names.AddRange(queueNames);
            ids.Clear();
            ids.AddRange(queueIds);
            this.downloadIds = downloadIds.ToArray();

            dropDown.SetModel(StringList.New(names.ToArray()));
            if (names.Count > 0)
            {
                dropDown.SetSelected(0);
                btnOk.Sensitive = true;
            }
            else
            {
                btnOk.Sensitive = false;
            }
        }

        // Handles OK confirmation and raises QueueSelected event
        private void OnOkClicked()
        {
            var index = (int)dropDown.GetSelected();
            if (index >= 0 && index < ids.Count)
            {
                var selectedId = ids[index];
                QueueSelected?.Invoke(this, new QueueSelectionEventArgs(selectedId, downloadIds));
            }
            window.Close();
        }

        // Displays modal dialog
        public void ShowWindow() => window.Present();

        // Disposes window resources
        public void Dispose() => window.Close();
    }
}
