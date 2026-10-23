// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 implementation of the New Download dialog conforming to INewDownloadDialog.
using System;
using System.Threading;
using System.Threading.Tasks;
using Gtk;
using TraceLog;
using Translations;
using XDM.Core;
using XDM.Core.UI;
using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    // Native GTK 4 dialog for entering download URLs and destination folders
    internal sealed class NewDownloadDialog : INewDownloadDialog
    {
        private readonly Window window;
        private readonly SynchronizationContext? uiContext;
        private readonly int uiThreadId;
        private readonly Entry urlEntry = Entry.New();
        private readonly Entry fileEntry = Entry.New();
        private readonly Label sizeLabel = Label.New("---");
        private readonly DropDown folderDropDown;
        private readonly Button browseButton;
        private readonly Button downloadNowButton;
        private readonly Button downloadLaterButton;
        private readonly Button cancelButton;
        private string[] folderValues = Array.Empty<string>();
        private int currentFolderIndex;
        private bool isDisposed;

        public event EventHandler? DownloadClicked;
        public event EventHandler? DestroyEvent;
        public event EventHandler? UrlChangedEvent;
        public event EventHandler? UrlBlockedEvent;
        public event EventHandler? QueueSchedulerClicked;
        public event EventHandler<DownloadLaterEventArgs>? DownloadLaterClicked;
        public event EventHandler<FileBrowsedEventArgs>? FileBrowsedEvent;
        public event EventHandler<FileBrowsedEventArgs>? DropdownSelectionChangedEvent;

        public string Url
        {
            get => urlEntry.GetText() ?? string.Empty;
            set => urlEntry.SetText(value);
        }

        public string SelectedFileName
        {
            get => fileEntry.GetText() ?? string.Empty;
            set => fileEntry.SetText(value);
        }

        public int SeletedFolderIndex
        {
            get => currentFolderIndex;
            set
            {
                currentFolderIndex = value;
                if (value >= 0 && value < folderValues.Length)
                {
                    folderDropDown.SetSelected((uint)value);
                }
            }
        }

        public AuthenticationInfo? Authentication { get; set; }
        public ProxyInfo? Proxy { get; set; } = Config.Instance.Proxy;
        public int SpeedLimit { get; set; } = Config.Instance.DefaltDownloadSpeed;
        public bool EnableSpeedLimit { get; set; } = Config.Instance.EnableSpeedLimit;

        // Constructs the GTK 4 dialog layout and wires actions
        public NewDownloadDialog(Window? parentWindow, bool empty)
        {
            uiContext = SynchronizationContext.Current;
            uiThreadId = Environment.CurrentManagedThreadId;

            window = Window.New();
            window.Title = TextResource.GetText("ND_TITLE") ?? "New Download";
            window.SetDefaultSize(560, 260);
            window.Modal = true;
            if (parentWindow != null) window.SetTransientFor(parentWindow);

            folderDropDown = DropDown.NewFromStrings(Array.Empty<string>());
            browseButton = Button.New();
            Gtk4IconHelper.ConfigureButton(browseButton, "folder-shared-line", TextResource.GetText("BTN_BROWSE") ?? "Browse...", "Choose destination folder", null, 14);

            downloadNowButton = Button.New();
            Gtk4IconHelper.ConfigureButton(downloadNowButton, "links-line", TextResource.GetText("ND_DOWNLOAD_NOW") ?? "Download Now", "Start download immediately", "suggested-action", 16);

            downloadLaterButton = Button.New();
            Gtk4IconHelper.ConfigureButton(downloadLaterButton, "time-line", TextResource.GetText("ND_DOWNLOAD_LATER") ?? "Download Later", "Queue for later", null, 14);

            cancelButton = Button.New();
            Gtk4IconHelper.ConfigureButton(cancelButton, "delete-bin-7-line", TextResource.GetText("ND_CANCEL") ?? "Cancel", "Cancel", null, 14);

            urlEntry.Hexpand = true;
            urlEntry.PlaceholderText = TextResource.GetText("ND_ADDRESS") ?? "http://...";
            fileEntry.Hexpand = true;
            fileEntry.PlaceholderText = TextResource.GetText("ND_FILE") ?? "filename";
            folderDropDown.Hexpand = true;

            window.SetTitlebar(BuildHeaderBar());
            window.SetChild(BuildContent());

            WireEvents();
        }

        // Packs header actions and dialog title
        private Widget BuildHeaderBar()
        {
            var header = HeaderBar.New();
            var titleBox = Box.New(Orientation.Horizontal, 6);
            titleBox.Halign = Align.Center;
            titleBox.Valign = Align.Center;
            var icon = Gtk4IconHelper.CreateIcon("links-line", 18);
            var title = Label.New(TextResource.GetText("ND_TITLE") ?? "New Download");
            title.AddCssClass("heading");
            titleBox.Append(icon);
            titleBox.Append(title);
            header.TitleWidget = titleBox;
            header.PackEnd(cancelButton);
            return header;
        }

        // Assembles form controls in a vertical box
        private Widget BuildContent()
        {
            var root = Box.New(Orientation.Horizontal, 16);
            root.MarginTop = 18;
            root.MarginBottom = 18;
            root.MarginStart = 20;
            root.MarginEnd = 20;

            var bigIcon = Gtk4IconHelper.CreateIcon("file-download-line", 48);
            bigIcon.Valign = Align.Start;
            bigIcon.MarginTop = 4;
            root.Append(bigIcon);

            var formBox = Box.New(Orientation.Vertical, 10);
            formBox.Hexpand = true;

            formBox.Append(BuildLabeledRow(TextResource.GetText("ND_ADDRESS") ?? "Address:", urlEntry));
            formBox.Append(BuildLabeledRow(TextResource.GetText("ND_FILE") ?? "File Name:", fileEntry));

            var folderBox = Box.New(Orientation.Horizontal, 8);
            folderBox.Hexpand = true;
            folderBox.Append(folderDropDown);
            folderBox.Append(browseButton);
            formBox.Append(BuildLabeledRow(TextResource.GetText("LBL_SAVE_IN") ?? "Save in:", folderBox));

            var sizeBox = Box.New(Orientation.Horizontal, 8);
            sizeLabel.AddCssClass("dim-label");
            sizeBox.Append(sizeLabel);
            formBox.Append(BuildLabeledRow(TextResource.GetText("ND_FILE_SIZE") ?? "File size:", sizeBox));

            var buttonBox = Box.New(Orientation.Horizontal, 10);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 10;
            buttonBox.Append(downloadLaterButton);
            buttonBox.Append(downloadNowButton);
            formBox.Append(buttonBox);

            root.Append(formBox);
            return root;
        }

        // Helper to format a label and an input widget into a row
        private static Widget BuildLabeledRow(string labelText, Widget widget)
        {
            var box = Box.New(Orientation.Horizontal, 12);
            box.Hexpand = true;
            var label = Label.New(labelText);
            label.WidthRequest = 90;
            label.Halign = Align.Start;
            widget.Hexpand = true;
            box.Append(label);
            box.Append(widget);
            return box;
        }

        // Connects user interaction signals
        private void WireEvents()
        {
            urlEntry.OnChanged += (_, _) => UrlChangedEvent?.Invoke(this, EventArgs.Empty);
            downloadNowButton.OnClicked += (_, _) => UiGuard.Run(window, "Start download", () => DownloadClicked?.Invoke(this, EventArgs.Empty));
            downloadLaterButton.OnClicked += (_, _) => UiGuard.Run(window, "Queue download", () => DownloadLaterClicked?.Invoke(this, new DownloadLaterEventArgs(string.Empty)));
            cancelButton.OnClicked += (_, _) => DisposeWindow();
            browseButton.OnClicked += async (_, _) => await OpenFolderPickerAsync();

            folderDropDown.OnNotify += (sender, args) =>
            {
                if (args.Pspec.GetName() == "selected")
                {
                    var index = (int)folderDropDown.GetSelected();
                    if (index == 1)
                    {
                        _ = OpenFolderPickerAsync();
                    }
                    else if (index >= 0 && index < folderValues.Length)
                    {
                        currentFolderIndex = index;
                        DropdownSelectionChangedEvent?.Invoke(this, new FileBrowsedEventArgs(folderValues[index]));
                    }
                }
            };

            window.OnCloseRequest += (_, _) =>
            {
                DisposeWindow();
                return false;
            };
        }

        // Launches GTK 4 native folder picker via portal
        private async Task OpenFolderPickerAsync()
        {
            try
            {
                var dialog = FileDialog.New();
                dialog.Title = TextResource.GetText("LBL_SAVE_IN") ?? "Select Folder";
                dialog.Modal = true;
                var folder = await dialog.SelectFolderAsync(window);
                if (folder != null)
                {
                    var path = folder.GetPath();
                    if (!string.IsNullOrEmpty(path))
                    {
                        FileBrowsedEvent?.Invoke(this, new FileBrowsedEventArgs(path));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Folder picker error: " + ex.Message);
            }
        }

        // Updates displayed file size
        public void SetFileSizeText(string text) =>
            Invoke(() => sizeLabel.SetText(text));

        // Populates the save-in folder dropdown options
        public void SetFolderValues(string[] values)
        {
            folderValues = values;
            Invoke(() =>
            {
                folderDropDown.SetModel(StringList.New(values));
                if (currentFolderIndex >= 0 && currentFolderIndex < values.Length)
                {
                    folderDropDown.SetSelected((uint)currentFolderIndex);
                }
            });
        }

        // Shows the dialog window
        public void ShowWindow() => window.Present();

        // Closes and cleans up the dialog window
        public void DisposeWindow()
        {
            if (isDisposed) return;
            isDisposed = true;
            DestroyEvent?.Invoke(this, EventArgs.Empty);
            Invoke(() =>
            {
                try
                {
                    window.Close();
                    window.Destroy();
                }
                catch { }
            });
        }

        // Dispatches callback to GTK UI thread
        public void Invoke(Action callback)
        {
            if (Environment.CurrentManagedThreadId == uiThreadId)
            {
                callback();
                return;
            }
            uiContext?.Post(_ => callback(), null);
        }

        // Presents a modal alert dialog for feedback
        public void ShowMessageBox(string message) =>
            Invoke(() => Gtk4DialogHelper.ShowAlert(window, message));
    }
}
