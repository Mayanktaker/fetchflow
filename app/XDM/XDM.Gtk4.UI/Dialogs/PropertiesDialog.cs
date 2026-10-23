// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 modal dialog displaying detailed properties, URL, and headers for a download entry.
using System;
using System.Collections.Generic;
using System.Text;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Core.Util;

using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    // Displays metadata, save path, download status, and HTTP request headers
    internal sealed class PropertiesDialog
    {
        private readonly Window window;

        // Constructs the properties dialog with metadata and connection headers
        public PropertiesDialog(Window? parent, DownloadItemBase entry, string cookies, Dictionary<string, List<string>> headers)
        {
            window = Window.New();
            window.Title = $"{TextResource.GetText("MENU_PROPERTIES") ?? "Properties"} — {entry.Name}";
            window.Modal = true;
            window.SetDefaultSize(560, 480);
            if (parent != null) window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            window.SetTitlebar(header);

            var rootBox = Box.New(Orientation.Vertical, 12);
            rootBox.MarginTop = 16;
            rootBox.MarginBottom = 16;
            rootBox.MarginStart = 20;
            rootBox.MarginEnd = 20;

            var grid = Grid.New();
            grid.RowSpacing = 8;
            grid.ColumnSpacing = 12;

            int row = 0;
            AddGridRow(grid, ref row, "File Name:", entry.Name);
            AddGridRow(grid, ref row, "Size:", FormattingHelper.FormatSize(entry.Size));
            AddGridRow(grid, ref row, "URL:", entry.PrimaryUrl);
            AddGridRow(grid, ref row, "Status:", entry is InProgressDownloadItem inProg ? inProg.Status.ToString() : "Finished");
            AddGridRow(grid, ref row, "Folder:", entry.TargetDir ?? "---");
            AddGridRow(grid, ref row, "Date Added:", entry.DateAdded.ToString("yyyy-MM-dd HH:mm:ss"));
            AddGridRow(grid, ref row, "Download Type:", entry.DownloadType ?? "Standard");
            rootBox.Append(grid);

            var headersLabel = Label.New(TextResource.GetText("LBL_HEADERS_COOKIES") ?? "HTTP Headers & Cookies:");
            headersLabel.Halign = Align.Start;
            headersLabel.AddCssClass("title-4");
            headersLabel.MarginTop = 8;
            rootBox.Append(headersLabel);

            var scroll = ScrolledWindow.New();
            scroll.Vexpand = true;
            scroll.Hexpand = true;
            scroll.SetMinContentHeight(140);

            var txtView = TextView.New();
            txtView.Editable = false;
            txtView.WrapMode = WrapMode.WordChar;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(cookies))
            {
                sb.AppendLine($"Cookie: {cookies}");
                sb.AppendLine();
            }
            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    sb.AppendLine($"{kvp.Key}: {string.Join(", ", kvp.Value)}");
                }
            }
            txtView.GetBuffer().SetText(sb.ToString(), -1);
            scroll.SetChild(txtView);
            rootBox.Append(scroll);

            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 8;

            var copyUrlBtn = Button.NewWithLabel(TextResource.GetText("CTX_COPY_URL") ?? "Copy URL");
            copyUrlBtn.OnClicked += (_, _) => UiGuard.Run(window, "Copy URL", () => window.GetClipboard().SetText(entry.PrimaryUrl));

            var closeBtn = Button.NewWithLabel(TextResource.GetText("BTN_CLOSE") ?? "Close");
            closeBtn.AddCssClass("suggested-action");
            closeBtn.OnClicked += (_, _) => window.Close();

            buttonBox.Append(copyUrlBtn);
            buttonBox.Append(closeBtn);
            rootBox.Append(buttonBox);

            window.SetChild(rootBox);
        }

        // Helper appending a key-value row to the properties grid
        private static void AddGridRow(Grid grid, ref int row, string labelText, string valueText)
        {
            var lblKey = Label.New(labelText);
            lblKey.Halign = Align.Start;
            lblKey.AddCssClass("dim-label");

            var lblVal = Label.New(valueText);
            lblVal.Halign = Align.Start;
            lblVal.Selectable = true;
            lblVal.Ellipsize = Pango.EllipsizeMode.Middle;

            grid.Attach(lblKey, 0, row, 1, 1);
            grid.Attach(lblVal, 1, row, 1, 1);
            row++;
        }

        // Displays the properties dialog
        public void Show() => window.Present();
    }
}
