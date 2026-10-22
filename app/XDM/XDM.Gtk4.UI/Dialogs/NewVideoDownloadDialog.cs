// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Native GTK 4 Video Download dialog implementing INewVideoDownloadDialog for media streams.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.UI;

namespace XDM.Gtk4UI.Dialogs
{
    // Modal dialog for confirming video download destination and format options
    internal sealed class NewVideoDownloadDialog : INewVideoDownloadDialog
    {
        private readonly Window window;
        private readonly SynchronizationContext? uiContext;
        private readonly int uiThreadId;

        private readonly Entry fileEntry;
        private readonly Label sizeLabel;
        private readonly DropDown folderDropDown;
        private readonly Button browseButton;
        private readonly CheckButton chkMp3;
        private readonly Button downloadNowButton;
        private readonly Button downloadLaterButton;
        private readonly Button cancelButton;

        private string[] folderValues = Array.Empty<string>();
        private int currentFolderIndex;
        private bool isDisposed;

        public event EventHandler? DownloadClicked;
        public event EventHandler? DestroyEvent;
        public event EventHandler? QueueSchedulerClicked;
        public event EventHandler? Mp3CheckChanged;
        public event EventHandler<DownloadLaterEventArgs>? DownloadLaterClicked;
        public event EventHandler<FileBrowsedEventArgs>? FileBrowsedEvent;
        public event EventHandler<FileBrowsedEventArgs>? DropdownSelectionChangedEvent;

        public AuthenticationInfo? Authentication { get; set; }
        public ProxyInfo? Proxy { get; set; }
        public int SpeedLimit { get; set; }
        public bool EnableSpeedLimit { get; set; }

        public string FileSize
        {
            get => sizeLabel.GetText() ?? "---";
            set => RunOnUI(() => sizeLabel.SetText(value ?? "---"));
        }

        public string SelectedFileName
        {
            get => fileEntry.GetText() ?? string.Empty;
            set => RunOnUI(() => fileEntry.SetText(value ?? string.Empty));
        }

        public int SeletedFolderIndex
        {
            get => currentFolderIndex;
            set
            {
                currentFolderIndex = value;
                RunOnUI(() =>
                {
                    if (folderValues.Length > 0 && value >= 0 && value < folderValues.Length)
                    {
                        folderDropDown.SetSelected((uint)value);
                    }
                });
            }
        }

        public bool ShowMp3Checkbox
        {
            get => chkMp3.Visible;
            set => RunOnUI(() => chkMp3.Visible = value);
        }

        public bool IsMp3CheckboxChecked
        {
            get => chkMp3.Active;
            set => RunOnUI(() => chkMp3.Active = value);
        }

        // Constructs the video download dialog
        public NewVideoDownloadDialog(Window? parent)
        {
            uiContext = SynchronizationContext.Current;
            uiThreadId = Environment.CurrentManagedThreadId;

            window = Window.New();
            var title = TextResource.GetText("ND_TITLE") ?? "Video Download";
            window.Title = title;
            window.Modal = true;
            window.SetDefaultSize(560, 260);
            if (parent != null) window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(title);
            window.SetTitlebar(header);

            fileEntry = Entry.New();
            fileEntry.Hexpand = true;

            sizeLabel = Label.New("---");
            sizeLabel.Halign = Align.Start;
            sizeLabel.AddCssClass("dim-label");

            folderDropDown = DropDown.NewFromStrings(Array.Empty<string>());
            folderDropDown.Hexpand = true;

            browseButton = Button.NewWithLabel(TextResource.GetText("ND_BROWSE") ?? "Browse...");
            downloadNowButton = Button.NewWithLabel(TextResource.GetText("ND_DOWNLOAD_NOW") ?? "Download Now");
            downloadNowButton.AddCssClass("suggested-action");

            downloadLaterButton = Button.NewWithLabel(TextResource.GetText("ND_DOWNLOAD_LATER") ?? "Download Later");
            cancelButton = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");

            chkMp3 = CheckButton.NewWithLabel(TextResource.GetText("LBL_CONVERT_MP3") ?? "Extract audio only (MP3)");
            chkMp3.Visible = false;

            BuildUI();
            WireEvents();
        }

        // Assembles dialog layout
        private void BuildUI()
        {
            var root = Box.New(Orientation.Vertical, 12);
            root.MarginTop = 16;
            root.MarginBottom = 16;
            root.MarginStart = 20;
            root.MarginEnd = 20;

            var fileRow = Box.New(Orientation.Horizontal, 8);
            var lblFile = Label.New(TextResource.GetText("ND_FILE") ?? "File Name:");
            lblFile.WidthRequest = 110;
            lblFile.Halign = Align.Start;
            fileRow.Append(lblFile);
            fileRow.Append(fileEntry);
            root.Append(fileRow);

            var sizeRow = Box.New(Orientation.Horizontal, 8);
            var lblSize = Label.New(TextResource.GetText("SORT_SIZE") ?? "Size:");
            lblSize.WidthRequest = 110;
            lblSize.Halign = Align.Start;
            sizeRow.Append(lblSize);
            sizeRow.Append(sizeLabel);
            root.Append(sizeRow);

            var folderRow = Box.New(Orientation.Horizontal, 8);
            var lblFolder = Label.New(TextResource.GetText("LBL_SAVE_IN") ?? "Save in:");
            lblFolder.WidthRequest = 110;
            lblFolder.Halign = Align.Start;
            folderRow.Append(lblFolder);
            folderRow.Append(folderDropDown);
            folderRow.Append(browseButton);
            root.Append(folderRow);

            root.Append(chkMp3);

            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 8;
            buttonBox.Append(cancelButton);
            buttonBox.Append(downloadLaterButton);
            buttonBox.Append(downloadNowButton);
            root.Append(buttonBox);

            window.SetChild(root);
        }

        // Connects user event handlers
        private void WireEvents()
        {
            downloadNowButton.OnClicked += (_, _) => DownloadClicked?.Invoke(this, EventArgs.Empty);
            downloadLaterButton.OnClicked += (_, _) => DownloadLaterClicked?.Invoke(this, new DownloadLaterEventArgs(string.Empty));
            cancelButton.OnClicked += (_, _) => DisposeWindow();
            browseButton.OnClicked += async (_, _) => await OpenFolderPickerAsync();
            chkMp3.OnToggled += (_, _) => Mp3CheckChanged?.Invoke(this, EventArgs.Empty);

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
        }

        // Portal folder picker
        private async Task OpenFolderPickerAsync()
        {
            try
            {
                var dialog = FileDialog.New();
                dialog.Title = TextResource.GetText("LBL_SAVE_IN") ?? "Select Download Directory";
                dialog.Modal = true;
                var folder = await dialog.SelectFolderAsync(window);
                var path = folder?.GetPath();
                if (!string.IsNullOrEmpty(path))
                {
                    FileBrowsedEvent?.Invoke(this, new FileBrowsedEventArgs(path));
                }
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "OpenFolderPicker error: " + ex.Message);
            }
        }

        // Populates folder dropdown items
        public void SetFolderValues(string[] values)
        {
            RunOnUI(() =>
            {
                folderValues = values ?? Array.Empty<string>();
                folderDropDown.SetModel(StringList.New(folderValues));
                if (currentFolderIndex >= 0 && currentFolderIndex < folderValues.Length)
                {
                    folderDropDown.SetSelected((uint)currentFolderIndex);
                }
            });
        }

        // Presents dialog window
        public void ShowWindow() => RunOnUI(() => window.Present());

        // Shows informational alert
        public void ShowMessageBox(string text) =>
            RunOnUI(() => Gtk4DialogHelper.ShowAlert(window, text));

        // Disposes dialog window
        public void DisposeWindow()
        {
            if (isDisposed) return;
            isDisposed = true;
            RunOnUI(() =>
            {
                DestroyEvent?.Invoke(this, EventArgs.Empty);
                window.Close();
            });
        }

        // Invokes action on UI thread
        public void Invoke(Action callback) => RunOnUI(callback);

        // Runs action on GTK thread
        private void RunOnUI(Action action)
        {
            if (uiContext != null && Environment.CurrentManagedThreadId != uiThreadId)
            {
                uiContext.Post(_ => action(), null);
            }
            else
            {
                action();
            }
        }
    }
}
