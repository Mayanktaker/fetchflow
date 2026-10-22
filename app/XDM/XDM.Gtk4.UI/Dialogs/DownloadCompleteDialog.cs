// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 implementation of download completion dialog conforming to IDownloadCompleteDialog.
using System;
using System.IO;
using System.Threading;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.UI;
using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    // Native GTK 4 notification window displayed when a transfer finishes
    internal sealed class DownloadCompleteDialog : IDownloadCompleteDialog
    {
        private readonly Window window;
        private readonly SynchronizationContext? uiContext;
        private readonly int uiThreadId;
        private readonly Label fileNameLabel = Label.New("");
        private readonly Label folderLabel = Label.New("");
        private readonly Image fileIcon = Gtk4IconHelper.CreateIcon("file-download-line", 48);
        private readonly CheckButton dontShowCheck;
        private readonly Button openFileButton;
        private readonly Button openFolderButton;
        private readonly Button closeButton;
        private string fileName = string.Empty;
        private string folderPath = string.Empty;
        private bool isDisposed;

        public event EventHandler<DownloadCompleteDialogEventArgs>? FileOpenClicked;
        public event EventHandler<DownloadCompleteDialogEventArgs>? FolderOpenClicked;
        public event EventHandler? DontShowAgainClickd;

        public string FileNameText
        {
            get => fileName;
            set
            {
                fileName = value;
                Post(() =>
                {
                    fileNameLabel.SetText(value);
                    var svgName = IconResource.GetSVGNameForFileType(value);
                    Gtk4IconHelper.UpdateIcon(fileIcon, svgName, 48);
                });
            }
        }

        public string FolderText
        {
            get => folderPath;
            set
            {
                folderPath = value;
                Post(() => folderLabel.SetText(value));
            }
        }

        // Constructs the GTK 4 completion dialog layout
        public DownloadCompleteDialog(Window? parentWindow)
        {
            uiContext = SynchronizationContext.Current;
            uiThreadId = Environment.CurrentManagedThreadId;

            window = Window.New();
            window.Title = TextResource.GetText("MSG_DOWNLOAD_COMPLETE") ?? "Download Complete";
            window.SetDefaultSize(500, 200);
            window.Modal = true;
            if (parentWindow != null) window.SetTransientFor(parentWindow);

            openFileButton = Button.New();
            Gtk4IconHelper.ConfigureButton(openFileButton, "external-link-line", TextResource.GetText("CTX_OPEN_FILE") ?? "Open File", "Open downloaded file", "suggested-action", 16);

            openFolderButton = Button.New();
            Gtk4IconHelper.ConfigureButton(openFolderButton, "folder-shared-line", TextResource.GetText("CTX_OPEN_FOLDER") ?? "Open Folder", "Open destination folder", null, 16);

            closeButton = Button.New();
            Gtk4IconHelper.ConfigureButton(closeButton, "check-line", TextResource.GetText("BTN_CLOSE") ?? "Close", "Close dialog", null, 14);

            dontShowCheck = CheckButton.NewWithLabel(TextResource.GetText("MSG_DONT_SHOW_AGAIN") ?? "Don't show this dialog again");

            fileNameLabel.Halign = Align.Start;
            fileNameLabel.AddCssClass("title-4");
            folderLabel.Halign = Align.Start;
            folderLabel.AddCssClass("dim-label");

            window.SetTitlebar(BuildHeaderBar());
            window.SetChild(BuildContent());

            WireEvents();
        }

        // Packs header bar with dialog title and close button
        private Widget BuildHeaderBar()
        {
            var header = HeaderBar.New();
            var titleBox = Box.New(Orientation.Horizontal, 6);
            titleBox.Halign = Align.Center;
            titleBox.Valign = Align.Center;
            var icon = Gtk4IconHelper.CreateIcon("check-line", 18);
            var title = Label.New(TextResource.GetText("MSG_DOWNLOAD_COMPLETE") ?? "Download Complete");
            title.AddCssClass("heading");
            titleBox.Append(icon);
            titleBox.Append(title);
            header.TitleWidget = titleBox;
            header.PackEnd(closeButton);
            return header;
        }

        // Assembles completed file details and action buttons
        private Widget BuildContent()
        {
            var root = Box.New(Orientation.Horizontal, 16);
            root.MarginTop = 18;
            root.MarginBottom = 18;
            root.MarginStart = 20;
            root.MarginEnd = 20;

            fileIcon.Valign = Align.Start;
            fileIcon.MarginTop = 4;
            root.Append(fileIcon);

            var infoBox = Box.New(Orientation.Vertical, 8);
            infoBox.Hexpand = true;
            infoBox.Append(fileNameLabel);
            infoBox.Append(folderLabel);
            infoBox.Append(dontShowCheck);

            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 12;
            buttonBox.Append(openFolderButton);
            buttonBox.Append(openFileButton);
            infoBox.Append(buttonBox);

            root.Append(infoBox);
            return root;
        }

        // Connects user action handlers
        private void WireEvents()
        {
            openFileButton.OnClicked += (_, _) =>
            {
                var fullPath = Path.Combine(folderPath, fileName);
                FileOpenClicked?.Invoke(this, new DownloadCompleteDialogEventArgs
                {
                    Path = fullPath,
                    FileName = fileName
                });
                Close();
            };

            openFolderButton.OnClicked += (_, _) =>
            {
                FolderOpenClicked?.Invoke(this, new DownloadCompleteDialogEventArgs
                {
                    Path = folderPath,
                    FileName = fileName
                });
                Close();
            };

            closeButton.OnClicked += (_, _) => Close();

            dontShowCheck.OnNotify += (sender, args) =>
            {
                if (args.Pspec.GetName() == "active" && dontShowCheck.GetActive())
                {
                    DontShowAgainClickd?.Invoke(this, EventArgs.Empty);
                }
            };

            window.OnCloseRequest += (_, _) =>
            {
                Close();
                return false;
            };
        }

        // Shows the completion dialog
        public void ShowDownloadCompleteDialog() => Post(() => window.Present());

        // Closes the dialog window safely
        private void Close() => Post(() =>
        {
            if (isDisposed) return;
            isDisposed = true;
            try
            {
                window.Close();
                window.Destroy();
            }
            catch { }
        });

        // Dispatches action to GTK UI synchronization context
        private void Post(Action action)
        {
            if (Environment.CurrentManagedThreadId == uiThreadId)
            {
                action();
                return;
            }
            uiContext?.Post(_ => action(), null);
        }
    }
}
