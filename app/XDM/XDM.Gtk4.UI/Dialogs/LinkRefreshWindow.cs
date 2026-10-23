// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 "refresh download link" status window. LinkRefreshDialogUIController opens
// the referer page and watches for the replacement link, so all this window has to do
// is explain what is happening, report success, and let the user cancel.
using System;
using Gtk;
using Translations;
using XDM.Core;

namespace XDM.Gtk4UI.Dialogs
{
    internal sealed class LinkRefreshWindow : IRefreshLinkDialog
    {
        // Status window and its message line
        private readonly Window window;
        private readonly Label messageLabel;

        public event EventHandler? WatchingStopped;

        // Builds the status window
        public LinkRefreshWindow(Window? parent)
        {
            window = Window.New();
            window.Title = TextResource.GetText("MENU_REFRESH_LINK") ?? "Refresh Download Link";
            window.Modal = false;
            window.SetDefaultSize(460, 170);
            if (parent != null)
            {
                window.SetTransientFor(parent);
            }

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            window.SetTitlebar(header);

            var box = Box.New(Orientation.Vertical, 10);
            box.MarginTop = 16;
            box.MarginBottom = 16;
            box.MarginStart = 20;
            box.MarginEnd = 20;

            messageLabel = Label.New(TextResource.GetText("MSG_REFRESH_WAITING")
                ?? "Waiting for the new link. Start the download in your browser; FetchFlow will pick the new link up automatically.");
            messageLabel.Wrap = true;
            messageLabel.Halign = Align.Start;
            box.Append(messageLabel);

            var buttons = Box.New(Orientation.Horizontal, 8);
            buttons.Halign = Align.End;
            var cancelBtn = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            cancelBtn.OnClicked += (_, _) =>
            {
                WatchingStopped?.Invoke(this, EventArgs.Empty);
                window.Close();
            };
            buttons.Append(cancelBtn);
            box.Append(buttons);

            window.SetChild(box);
            window.OnCloseRequest += (_, _) =>
            {
                WatchingStopped?.Invoke(this, EventArgs.Empty);
                return false;
            };
        }

        // Presents the window
        public void ShowWindow() => window.Present();

        // Reports that a replacement link was captured
        public void LinkReceived()
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                window.Close();
                return false;
            });
        }
    }
}
