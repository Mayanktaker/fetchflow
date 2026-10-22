// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 implementation of the download progress window conforming to IProgressWindow.
using System;
using System.Collections.Generic;
using System.Threading;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.UI;
using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    // Native GTK 4 window displaying real-time download progress and controls
    internal sealed class ProgressWindow : IProgressWindow
    {
        private readonly Window window;
        private readonly SynchronizationContext? uiContext;
        private readonly int uiThreadId;
        private readonly Label fileNameLabel = Label.New("");
        private readonly Label urlLabel = Label.New("");
        private readonly Image fileIcon = Gtk4IconHelper.CreateIcon("file-download-line", 44);
        private readonly ProgressBar progressBar = ProgressBar.New();
        private readonly Label sizeLabel = Label.New("");
        private readonly Label speedLabel = Label.New("");
        private readonly Label etaLabel = Label.New("");
        private readonly Label statusLabel = Label.New("");
        private readonly Button pauseButton;
        private readonly Button cancelButton;
        private string downloadId = string.Empty;
        private bool isPaused;
        private bool isDisposed;

        public string DownloadId
        {
            get => downloadId;
            set => downloadId = value;
        }

        public string FileNameText
        {
            get => fileNameLabel.GetText() ?? string.Empty;
            set => Post(() =>
            {
                fileNameLabel.SetText(value);
                window.Title = value;
                var svgName = IconResource.GetSVGNameForFileType(value);
                Gtk4IconHelper.UpdateIcon(fileIcon, svgName, 44);
            });
        }

        public string UrlText
        {
            get => urlLabel.GetText() ?? string.Empty;
            set => Post(() => urlLabel.SetText(value));
        }

        public string FileSizeText
        {
            get => sizeLabel.GetText() ?? string.Empty;
            set => Post(() => sizeLabel.SetText(value));
        }

        public string DownloadSpeedText
        {
            get => speedLabel.GetText() ?? string.Empty;
            set => Post(() => speedLabel.SetText(value));
        }

        public string DownloadETAText
        {
            get => etaLabel.GetText() ?? string.Empty;
            set => Post(() => etaLabel.SetText(value));
        }

        public int DownloadProgress
        {
            get => (int)Math.Round(progressBar.Fraction * 100);
            set => Post(() =>
            {
                var fraction = Math.Clamp(value / 100.0, 0.0, 1.0);
                progressBar.Fraction = fraction;
                progressBar.SetText($"{value}%");
            });
        }

        // Constructs the GTK 4 progress window layout
        public ProgressWindow(Window? parentWindow, string id)
        {
            downloadId = id;
            uiContext = SynchronizationContext.Current;
            uiThreadId = Environment.CurrentManagedThreadId;

            window = Window.New();
            window.Title = TextResource.GetText("TITLE_PROGRESS") ?? "Download Progress";
            window.SetDefaultSize(520, 220);
            if (parentWindow != null) window.SetTransientFor(parentWindow);

            pauseButton = Button.New();
            Gtk4IconHelper.ConfigureButton(pauseButton, "pause-line", TextResource.GetText("MENU_PAUSE") ?? "Pause", "Pause download", null, 14);

            cancelButton = Button.New();
            Gtk4IconHelper.ConfigureButton(cancelButton, "delete-bin-7-line", TextResource.GetText("ND_CANCEL") ?? "Cancel", "Cancel download", "destructive-action", 14);

            progressBar.Hexpand = true;
            progressBar.SetShowText(true);
            progressBar.MarginTop = 6;
            progressBar.MarginBottom = 6;

            fileNameLabel.Halign = Align.Start;
            fileNameLabel.AddCssClass("title-4");
            urlLabel.Halign = Align.Start;
            urlLabel.AddCssClass("dim-label");

            window.SetTitlebar(BuildHeaderBar());
            window.SetChild(BuildContent());

            WireEvents();
        }

        // Packs header bar with dialog title
        private Widget BuildHeaderBar()
        {
            var header = HeaderBar.New();
            var titleBox = Box.New(Orientation.Horizontal, 6);
            titleBox.Halign = Align.Center;
            titleBox.Valign = Align.Center;
            var icon = Gtk4IconHelper.CreateIcon("download-2-line", 18);
            var title = Label.New(TextResource.GetText("TITLE_PROGRESS") ?? "Downloading");
            title.AddCssClass("heading");
            titleBox.Append(icon);
            titleBox.Append(title);
            header.TitleWidget = titleBox;
            return header;
        }

        // Assembles progress information and control buttons
        private Widget BuildContent()
        {
            var root = Box.New(Orientation.Horizontal, 16);
            root.MarginTop = 16;
            root.MarginBottom = 16;
            root.MarginStart = 20;
            root.MarginEnd = 20;

            fileIcon.Valign = Align.Start;
            fileIcon.MarginTop = 4;
            root.Append(fileIcon);

            var infoBox = Box.New(Orientation.Vertical, 8);
            infoBox.Hexpand = true;

            infoBox.Append(fileNameLabel);
            infoBox.Append(urlLabel);
            infoBox.Append(progressBar);

            var statsBox = Box.New(Orientation.Horizontal, 16);
            statsBox.Append(sizeLabel);
            statsBox.Append(speedLabel);
            statsBox.Append(etaLabel);
            infoBox.Append(statsBox);

            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 8;
            buttonBox.Append(pauseButton);
            buttonBox.Append(cancelButton);
            infoBox.Append(buttonBox);

            root.Append(infoBox);
            return root;
        }

        // Connects button click signals
        private void WireEvents()
        {
            pauseButton.OnClicked += (_, _) =>
            {
                if (isPaused)
                {
                    ApplicationContext.Application.ResumeDownload(downloadId);
                    DownloadStarted();
                }
                else
                {
                    ApplicationContext.CoreService.StopDownloads(new List<string> { downloadId }, false);
                    DownloadCancelled();
                }
            };

            cancelButton.OnClicked += (_, _) =>
            {
                ApplicationContext.CoreService.StopDownloads(new List<string> { downloadId }, true);
                DestroyWindow();
            };

            window.OnCloseRequest += (_, _) =>
            {
                DestroyWindow();
                return false;
            };
        }

        // Shows the progress window
        public void ShowProgressWindow() => Post(() => window.Present());

        // Updates UI when transfer starts
        public void DownloadStarted() => Post(() =>
        {
            isPaused = false;
            Gtk4IconHelper.ConfigureButton(pauseButton, "pause-line", TextResource.GetText("MENU_PAUSE") ?? "Pause", "Pause download", null, 14);
            statusLabel.SetText(TextResource.GetText("STAT_DOWNLOADING") ?? "Downloading...");
        });

        // Updates UI when transfer is cancelled or paused
        public void DownloadCancelled() => Post(() =>
        {
            isPaused = true;
            Gtk4IconHelper.ConfigureButton(pauseButton, "play-line", TextResource.GetText("MENU_RESUME") ?? "Resume", "Resume download", "suggested-action", 14);
            statusLabel.SetText(TextResource.GetText("MSG_DWN_STOP") ?? "Paused");
            speedLabel.SetText("");
            etaLabel.SetText("");
        });

        // Displays error details on transfer failure
        public void DownloadFailed(ErrorDetails error) => Post(() =>
        {
            isPaused = true;
            Gtk4IconHelper.ConfigureButton(pauseButton, "play-line", TextResource.GetText("MENU_RESUME") ?? "Resume", "Resume download", "suggested-action", 14);
            statusLabel.SetText(error.Message);
            speedLabel.SetText("");
            etaLabel.SetText("");
        });

        // Closes and cleans up the progress window
        public void DestroyWindow() => Post(() =>
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
