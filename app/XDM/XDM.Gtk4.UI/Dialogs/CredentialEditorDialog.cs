// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 editor for one saved site credential (host + user + password), mirroring
// the GTK 3 PasswordDialog: host and user name are required, the password is
// optional. Returns null when the user cancels.
using Gtk;
using Translations;
using XDM.Core;

namespace XDM.Gtk4UI.Dialogs
{
    // Modal editor window for a single XDM.Core.PasswordEntry
    internal sealed class CredentialEditorDialog
    {
        // Modal window presented to the user
        private readonly Window window;
        // Field values read after the loop ends
        private readonly Entry hostEntry;
        private readonly Entry userEntry;
        private readonly Entry passEntry;
        // Result of the edit; null means the user cancelled
        private XDM.Core.PasswordEntry? result;

        // Builds the editor, pre-filling the existing entry when given
        private CredentialEditorDialog(Window parent, XDM.Core.PasswordEntry? existing)
        {
            window = Window.New();
            window.Title = TextResource.GetText("DESC_PASS") ?? "Password";
            window.Modal = true;
            window.SetDefaultSize(440, 240);
            window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            window.SetTitlebar(header);

            var box = Box.New(Orientation.Vertical, 10);
            box.MarginTop = 16;
            box.MarginBottom = 16;
            box.MarginStart = 20;
            box.MarginEnd = 20;

            var grid = Grid.New();
            grid.ColumnSpacing = 10;
            grid.RowSpacing = 8;

            grid.Attach(Label.New(TextResource.GetText("DESC_HOST") ?? "Host"), 0, 0, 1, 1);
            hostEntry = Entry.New();
            hostEntry.Hexpand = true;
            hostEntry.SetText(existing?.Host ?? "");
            grid.Attach(hostEntry, 1, 0, 1, 1);

            grid.Attach(Label.New(TextResource.GetText("DESC_USER") ?? "User name"), 0, 1, 1, 1);
            userEntry = Entry.New();
            userEntry.Hexpand = true;
            userEntry.SetText(existing?.User ?? "");
            grid.Attach(userEntry, 1, 1, 1, 1);

            grid.Attach(Label.New(TextResource.GetText("DESC_PASS") ?? "Password"), 0, 2, 1, 1);
            passEntry = Entry.New();
            passEntry.Hexpand = true;
            passEntry.Visibility = false;
            passEntry.InputPurpose = InputPurpose.Password;
            passEntry.SetText(existing?.Password ?? "");
            grid.Attach(passEntry, 1, 2, 1, 1);

            box.Append(grid);

            var buttons = Box.New(Orientation.Horizontal, 8);
            buttons.Halign = Align.End;
            buttons.MarginTop = 8;

            var cancelBtn = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            var okBtn = Button.NewWithLabel(TextResource.GetText("MSG_OK") ?? "OK");
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
                var host = hostEntry.GetText()?.Trim() ?? "";
                if (string.IsNullOrEmpty(host))
                {
                    Gtk4DialogHelper.ShowAlert(window, TextResource.GetText("MSG_HOST_NAME_MISSING") ?? "Host name is missing");
                    return;
                }
                var user = userEntry.GetText()?.Trim() ?? "";
                if (string.IsNullOrEmpty(user))
                {
                    Gtk4DialogHelper.ShowAlert(window, TextResource.GetText("MSG_NO_USERNAME") ?? "User name is missing");
                    return;
                }
                result = new XDM.Core.PasswordEntry
                {
                    Host = host,
                    User = user,
                    Password = passEntry.GetText() ?? ""
                };
                window.Close();
            };
        }

        // Shows the editor modally and returns the entered credential, or null
        public static XDM.Core.PasswordEntry? Edit(Window parent, XDM.Core.PasswordEntry? existing)
        {
            var dialog = new CredentialEditorDialog(parent, existing);
            var loop = GLib.MainLoop.New(null, false);
            dialog.window.OnDestroy += (_, _) => loop.Quit();
            dialog.window.Present();
            dialog.hostEntry.GrabFocus();
            loop.Run();
            return dialog.result;
        }
    }
}
