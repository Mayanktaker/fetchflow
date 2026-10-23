// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Modal dialog providing unified Export and Import options for downloads and configurations in GTK 4.
using System;
using Gtk;
using Translations;
using XDM.Core;

using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    // Native GTK 4 modal backup and restore action dialog
    internal sealed class ImportExportDialog
    {
        private readonly Window window;

        // Initializes the ImportExportDialog with headerbar and action cards
        public ImportExportDialog(Window? parent)
        {
            window = Window.New();
            var titleText = TextResource.GetText("MENU_IMPORT_EXPORT") ?? "Import / Export";
            window.Title = titleText;
            window.Modal = true;
            window.SetDefaultSize(480, 290);
            if (parent != null) window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(titleText);
            window.SetTitlebar(header);

            var rootBox = Box.New(Orientation.Vertical, 12);
            rootBox.MarginTop = 16;
            rootBox.MarginBottom = 16;
            rootBox.MarginStart = 20;
            rootBox.MarginEnd = 20;

            // Header instruction label
            var lblTitle = Label.New(TextResource.GetText("LBL_IMPORT_EXPORT_HEADER") ?? "Backup & Restore Downloads");
            lblTitle.Halign = Align.Start;
            lblTitle.AddCssClass("title-4");
            rootBox.Append(lblTitle);

            var lblSub = Label.New(TextResource.GetText("LBL_IMPORT_EXPORT_SUB") ?? "Export your download list and settings to a .zip archive, or restore from a backup.");
            lblSub.Halign = Align.Start;
            lblSub.Wrap = true;
            lblSub.AddCssClass("dim-label");
            lblSub.MarginBottom = 6;
            rootBox.Append(lblSub);

            // Export action card
            var btnExport = CreateActionCard(
                TextResource.GetText("BTN_EXPORT_TITLE") ?? "Export Download List",
                TextResource.GetText("BTN_EXPORT_DESC") ?? "Save download items, queues, and settings to a .zip archive.",
                "suggested-action");
            btnExport.OnClicked += (_, _) => UiGuard.Run(window, "Export backup", () =>
            {
                window.Close();
                (ApplicationContext.MainWindow as Gtk4ApplicationWindow)?.TriggerExport();
            });
            rootBox.Append(btnExport);

            // Import action card
            var btnImport = CreateActionCard(
                TextResource.GetText("BTN_IMPORT_TITLE") ?? "Import Download List",
                TextResource.GetText("BTN_IMPORT_DESC") ?? "Restore download items, queues, and settings from a .zip backup archive.",
                "accent");
            btnImport.OnClicked += (_, _) => UiGuard.Run(window, "Import backup", () =>
            {
                window.Close();
                (ApplicationContext.MainWindow as Gtk4ApplicationWindow)?.TriggerImport();
            });
            rootBox.Append(btnImport);

            // Bottom action area
            var bottomBox = Box.New(Orientation.Horizontal, 8);
            bottomBox.Halign = Align.End;
            bottomBox.MarginTop = 8;

            var btnClose = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Close");
            btnClose.OnClicked += (_, _) => window.Close();
            bottomBox.Append(btnClose);

            rootBox.Append(bottomBox);
            window.SetChild(rootBox);
        }

        // Creates a styled action button card with title and descriptive subtext
        private static Button CreateActionCard(string title, string description, string styleClass)
        {
            var btn = Button.New();
            btn.AddCssClass("card");
            btn.AddCssClass(styleClass);

            var cardBox = Box.New(Orientation.Vertical, 4);
            cardBox.MarginTop = 10;
            cardBox.MarginBottom = 10;
            cardBox.MarginStart = 12;
            cardBox.MarginEnd = 12;

            var lblCardTitle = Label.New(title);
            lblCardTitle.Halign = Align.Start;
            lblCardTitle.AddCssClass("heading");

            var lblCardDesc = Label.New(description);
            lblCardDesc.Halign = Align.Start;
            lblCardDesc.Wrap = true;
            lblCardDesc.AddCssClass("caption");

            cardBox.Append(lblCardTitle);
            cardBox.Append(lblCardDesc);
            btn.SetChild(cardBox);
            return btn;
        }

        // Shows the import/export modal dialog
        public void Show() => window.Present();

        // Closes the import/export dialog
        public void Close() => window.Close();
    }
}
