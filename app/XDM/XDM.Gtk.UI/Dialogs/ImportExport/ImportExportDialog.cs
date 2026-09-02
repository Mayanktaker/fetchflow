// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using Gtk;
using Translations;
using XDM.Core;
using XDM.GtkUI.Utils;

namespace XDM.GtkUI.Dialogs.ImportExport
{
    // Modal dialog providing unified Export and Import options for downloads and configurations
    public class ImportExportDialog : Dialog
    {
        // Reference to parent window
        private readonly Window? parentWindow;

        // Initializes the ImportExportDialog with headerbar and layout
        public ImportExportDialog(Window? parent)
        {
            parentWindow = parent ?? (ApplicationContext.MainWindow as Window);
            Modal = true;
            TransientFor = parentWindow;
            Resizable = false;

            var titleText = TextResource.GetText("MENU_IMPORT_EXPORT") ?? "Import / Export";
            Title = titleText;
            Titlebar = GtkHelper.CreateDialogHeaderBar(titleText);
            GtkHelper.SetWindowAppIcon(this);
            GtkHelper.AttachSafeDispose(this);

            SetDefaultSize(480, 290);
            SetSizeRequest(440, 260);

            BuildUI();
        }

        // Constructs the layout with header text and two action cards
        private void BuildUI()
        {
            var contentArea = ContentArea;
            contentArea.Spacing = 12;
            contentArea.MarginStart = 18;
            contentArea.MarginEnd = 18;
            contentArea.MarginTop = 16;
            contentArea.MarginBottom = 16;

            // Header instruction label
            var lblDescription = new Label
            {
                Markup = "<span size=\"10500\" weight=\"bold\">" +
                         GLib.Markup.EscapeText(TextResource.GetText("LBL_IMPORT_EXPORT_HEADER") ?? "Backup & Restore Downloads") +
                         "</span>\n<span size=\"9000\" alpha=\"55000\">" +
                         GLib.Markup.EscapeText(TextResource.GetText("LBL_IMPORT_EXPORT_SUB") ?? "Export your download list and settings to a .zip archive, or restore from a backup.") +
                         "</span>",
                Halign = Align.Start,
                Xalign = 0,
                Wrap = true,
                MarginBottom = 4
            };
            contentArea.PackStart(lblDescription, false, false, 0);

            // Container box for action cards
            var cardsBox = new VBox(false, 10);

            // Export action card
            var btnExport = CreateActionCard(
                "upload-2-line",
                TextResource.GetText("BTN_EXPORT_TITLE") ?? "Export Download List",
                TextResource.GetText("BTN_EXPORT_DESC") ?? "Save download items, queues, and settings to a .zip archive.",
                53, 132, 228
            );
            btnExport.Clicked += (s, e) =>
            {
                Respond(ResponseType.Ok);
                Destroy();
                (parentWindow as MainWindow)?.TriggerExport();
            };
            cardsBox.PackStart(btnExport, false, false, 0);

            // Import action card
            var btnImport = CreateActionCard(
                "download-2-line",
                TextResource.GetText("BTN_IMPORT_TITLE") ?? "Import Download List",
                TextResource.GetText("BTN_IMPORT_DESC") ?? "Restore download items, queues, and settings from a .zip backup archive.",
                16, 185, 129
            );
            btnImport.Clicked += (s, e) =>
            {
                Respond(ResponseType.Ok);
                Destroy();
                (parentWindow as MainWindow)?.TriggerImport();
            };
            cardsBox.PackStart(btnImport, false, false, 0);

            contentArea.PackStart(cardsBox, true, true, 0);

            // Close button in action area
            var btnClose = new Button(TextResource.GetText("ND_CANCEL") ?? "Close")
            {
                Halign = Align.End,
                MarginTop = 6
            };
            btnClose.Clicked += (s, e) =>
            {
                Respond(ResponseType.Cancel);
                Destroy();
            };
            ActionArea.PackEnd(btnClose, false, false, 0);

            ShowAll();
        }

        // Creates an interactive card button with icon, title, and descriptive text
        private static Button CreateActionCard(string iconName, string title, string description, byte r, byte g, byte b)
        {
            var btn = new Button
            {
                Halign = Align.Fill,
                Valign = Align.Center
            };
            btn.StyleContext.AddClass("card-button");

            var hbox = new HBox(false, 14)
            {
                MarginStart = 12,
                MarginEnd = 12,
                MarginTop = 10,
                MarginBottom = 10
            };

            // Card icon
            var rawIcon = GtkHelper.LoadSvg(iconName, 26);
            var tintedIcon = rawIcon != null ? GtkHelper.TintPixbuf(rawIcon, r, g, b) : null;
            var img = tintedIcon != null ? new Image(tintedIcon) : new Image();
            img.Valign = Align.Center;
            hbox.PackStart(img, false, false, 0);

            // Text container
            var vboxText = new VBox(false, 2)
            {
                Valign = Align.Center
            };
            var lblTitle = new Label
            {
                Markup = $"<span weight=\"bold\" size=\"10000\">{GLib.Markup.EscapeText(title)}</span>",
                Halign = Align.Start,
                Xalign = 0
            };
            var lblDesc = new Label
            {
                Markup = $"<span size=\"8500\" alpha=\"55000\">{GLib.Markup.EscapeText(description)}</span>",
                Halign = Align.Start,
                Xalign = 0,
                Wrap = true
            };
            vboxText.PackStart(lblTitle, false, false, 0);
            vboxText.PackStart(lblDesc, false, false, 0);
            hbox.PackStart(vboxText, true, true, 0);

            btn.Add(hbox);
            return btn;
        }
    }
}
