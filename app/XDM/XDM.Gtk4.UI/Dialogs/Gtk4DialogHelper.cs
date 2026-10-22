// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Native GTK 4 dialog helpers for message alerts, confirmations and deletions.
using System;
using Gtk;
using Translations;

namespace XDM.Gtk4UI.Dialogs
{
    // Utility for presenting modal GTK 4 alert and confirmation windows
    internal static class Gtk4DialogHelper
    {
        // Shows a modal informational message dialog
        public static void ShowAlert(Window? parent, string message, string title = "FetchFlow")
        {
            var win = Window.New();
            win.Title = title;
            win.Modal = true;
            win.SetDefaultSize(420, 160);
            if (parent != null) win.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(title);
            win.SetTitlebar(header);

            var box = Box.New(Orientation.Vertical, 12);
            box.MarginTop = 16;
            box.MarginBottom = 16;
            box.MarginStart = 20;
            box.MarginEnd = 20;

            var label = Label.New(message);
            label.Wrap = true;
            label.Halign = Align.Start;
            box.Append(label);

            var btn = Button.NewWithLabel(TextResource.GetText("BTN_OK") ?? "OK");
            btn.AddCssClass("suggested-action");
            btn.Halign = Align.End;
            btn.OnClicked += (_, _) => win.Close();
            box.Append(btn);

            win.SetChild(box);
            win.Present();
        }

        // Prompts user for confirmation and blocks until responded
        public static bool Confirm(Window? parent, string message, string title = "Confirm")
        {
            var win = Window.New();
            win.Title = title;
            win.Modal = true;
            win.SetDefaultSize(440, 180);
            if (parent != null) win.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(title);
            win.SetTitlebar(header);

            var box = Box.New(Orientation.Vertical, 12);
            box.MarginTop = 16;
            box.MarginBottom = 16;
            box.MarginStart = 20;
            box.MarginEnd = 20;

            var label = Label.New(message);
            label.Wrap = true;
            label.Halign = Align.Start;
            box.Append(label);

            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 8;

            var cancelBtn = Button.NewWithLabel(TextResource.GetText("BTN_CANCEL") ?? "Cancel");
            var okBtn = Button.NewWithLabel(TextResource.GetText("BTN_OK") ?? "OK");
            okBtn.AddCssClass("suggested-action");

            buttonBox.Append(cancelBtn);
            buttonBox.Append(okBtn);
            box.Append(buttonBox);

            win.SetChild(box);

            bool result = false;
            var loop = GLib.MainLoop.New(null, false);

            cancelBtn.OnClicked += (_, _) =>
            {
                result = false;
                win.Close();
                loop.Quit();
            };

            okBtn.OnClicked += (_, _) =>
            {
                result = true;
                win.Close();
                loop.Quit();
            };

            win.OnCloseRequest += (_, _) =>
            {
                loop.Quit();
                return false;
            };

            win.Present();
            loop.Run();
            return result;
        }

        // Prompts user for download deletion confirmation with disk delete option
        public static void ConfirmDelete(Window? parent, string message, out bool approved, out bool deleteFiles)
        {
            var win = Window.New();
            win.Title = TextResource.GetText("DESC_DEL") ?? "Delete Download";
            win.Modal = true;
            win.SetDefaultSize(480, 180);
            if (parent != null) win.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(win.Title);
            win.SetTitlebar(header);

            var box = Box.New(Orientation.Vertical, 12);
            box.MarginTop = 16;
            box.MarginBottom = 16;
            box.MarginStart = 20;
            box.MarginEnd = 20;

            var label = Label.New(message);
            label.Wrap = true;
            label.Halign = Align.Start;
            box.Append(label);

            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 8;

            var cancelBtn = Button.NewWithLabel(TextResource.GetText("BTN_CANCEL") ?? "Cancel");
            var deleteListBtn = Button.NewWithLabel(TextResource.GetText("DESC_DEL") ?? "Delete from list");
            var deleteFileBtn = Button.NewWithLabel(TextResource.GetText("LBL_DEL_FILE") ?? "Delete file and entry");
            deleteFileBtn.AddCssClass("destructive-action");

            buttonBox.Append(cancelBtn);
            buttonBox.Append(deleteListBtn);
            buttonBox.Append(deleteFileBtn);
            box.Append(buttonBox);

            win.SetChild(box);

            bool app = false;
            bool del = false;
            var loop = GLib.MainLoop.New(null, false);

            cancelBtn.OnClicked += (_, _) =>
            {
                app = false;
                del = false;
                win.Close();
                loop.Quit();
            };

            deleteListBtn.OnClicked += (_, _) =>
            {
                app = true;
                del = false;
                win.Close();
                loop.Quit();
            };

            deleteFileBtn.OnClicked += (_, _) =>
            {
                app = true;
                del = true;
                win.Close();
                loop.Quit();
            };

            win.OnCloseRequest += (_, _) =>
            {
                loop.Quit();
                return false;
            };

            win.Present();
            loop.Run();

            approved = app;
            deleteFiles = del;
        }
    }
}
