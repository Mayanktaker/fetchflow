// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 credentials prompt used when a server asks for authentication. Returns
// null when the user cancels, so the download is abandoned instead of silently
// failing as it did while this was a stub.
using System;
using Gtk;
using Translations;
using XDM.Core;

namespace XDM.Gtk4UI.Dialogs
{
    internal sealed class CredentialsDialog
    {
        // Modal window presented to the user
        private readonly Window window;
        // Field values read after the loop ends
        private readonly Entry userEntry;
        private readonly Entry passEntry;
        // Result of the prompt
        private AuthenticationInfo? result;

        // Builds the prompt for the given server message
        private CredentialsDialog(Window parent, string message)
        {
            window = Window.New();
            window.Title = TextResource.GetText("LBL_CRED_TITLE") ?? "Authentication Required";
            window.Modal = true;
            window.SetDefaultSize(420, 220);
            window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            window.SetTitlebar(header);

            var box = Box.New(Orientation.Vertical, 10);
            box.MarginTop = 16;
            box.MarginBottom = 16;
            box.MarginStart = 20;
            box.MarginEnd = 20;

            var messageLabel = Label.New(message);
            messageLabel.Wrap = true;
            messageLabel.Halign = Align.Start;
            box.Append(messageLabel);

            var grid = Grid.New();
            grid.ColumnSpacing = 10;
            grid.RowSpacing = 8;
            grid.MarginTop = 6;

            grid.Attach(Label.New(TextResource.GetText("LBL_CRED_USER") ?? "User name"), 0, 0, 1, 1);
            userEntry = Entry.New();
            userEntry.Hexpand = true;
            grid.Attach(userEntry, 1, 0, 1, 1);

            grid.Attach(Label.New(TextResource.GetText("LBL_CRED_PASS") ?? "Password"), 0, 1, 1, 1);
            passEntry = Entry.New();
            passEntry.Hexpand = true;
            passEntry.Visibility = false;
            passEntry.InputPurpose = InputPurpose.Password;
            grid.Attach(passEntry, 1, 1, 1, 1);

            box.Append(grid);

            var buttons = Box.New(Orientation.Horizontal, 8);
            buttons.Halign = Align.End;
            buttons.MarginTop = 8;

            var cancelBtn = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            var okBtn = Button.NewWithLabel(TextResource.GetText("BTN_OK") ?? "OK");
            okBtn.AddCssClass("suggested-action");
            buttons.Append(cancelBtn);
            buttons.Append(okBtn);
            box.Append(buttons);

            window.SetChild(box);
            window.SetDefaultWidget(okBtn);
            window.OnCloseRequest += (_, _) =>
            {
                result = null;
                return false;
            };
            cancelBtn.OnClicked += (_, _) => window.Close();
            okBtn.OnClicked += (_, _) =>
            {
                result = new AuthenticationInfo
                {
                    UserName = userEntry.GetText() ?? "",
                    Password = passEntry.GetText() ?? ""
                };
                window.Close();
            };
        }

        // Shows the prompt modally and returns the entered credentials, or null
        public static AuthenticationInfo? Prompt(Window parent, string message)
        {
            var dialog = new CredentialsDialog(parent, message);
            var loop = GLib.MainLoop.New(null, false);
            dialog.window.OnDestroy += (_, _) => loop.Quit();
            dialog.window.Present();
            dialog.userEntry.GrabFocus();
            loop.Run();
            return dialog.result;
        }
    }
}
