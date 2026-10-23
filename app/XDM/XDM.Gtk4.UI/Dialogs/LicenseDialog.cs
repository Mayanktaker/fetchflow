// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Read-only license viewer: the project's LICENSE in a scrolled, monospace view
// with Copy and Close actions. The About window's Legal page carries the same text.
using System;
using Gtk;
using Translations;
using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    internal static class LicenseDialog
    {
        // Presents the license window over the given parent
        public static void Show(Window? parent)
        {
            var text = LicenseText.Load() ?? "";
            var window = Window.New();
            window.Title = TextResource.GetText("LBL_LICENSE") ?? "License";
            window.Modal = true;
            window.SetDefaultSize(760, 640);
            if (parent != null)
            {
                window.SetTransientFor(parent);
            }

            // Copy stays in the header so it is reachable while scrolled anywhere
            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);

            var copy = Button.NewWithLabel(TextResource.GetText("LBL_COPY") ?? "Copy");
            copy.OnClicked += (_, _) =>
            {
                window.GetClipboard().SetText(text);
                copy.SetLabel(TextResource.GetText("LBL_COPIED") ?? "Copied");
                GLib.Functions.TimeoutAdd(0, 1400, () =>
                {
                    copy.SetLabel(TextResource.GetText("LBL_COPY") ?? "Copy");
                    return false;
                });
            };
            header.PackEnd(copy);

            var close = Button.NewWithLabel(TextResource.GetText("BTN_CLOSE") ?? "Close");
            close.OnClicked += (_, _) => window.Close();
            header.PackEnd(close);
            window.SetTitlebar(header);

            // Monospace, word-wrapped, non-editable text view
            var view = TextView.New();
            view.Editable = false;
            view.CursorVisible = false;
            view.WrapMode = WrapMode.Word;
            view.MarginTop = 12;
            view.MarginBottom = 12;
            view.MarginStart = 12;
            view.MarginEnd = 12;
            view.AddCssClass("license-view");
            view.GetBuffer()!.SetText(text, -1);

            var scroll = ScrolledWindow.New();
            scroll.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            scroll.SetChild(view);
            scroll.Vexpand = true;
            scroll.Hexpand = true;
            window.SetChild(scroll);

            window.Present();
        }
    }
}
