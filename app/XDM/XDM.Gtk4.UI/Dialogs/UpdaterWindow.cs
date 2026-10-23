// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 component updater UI (yt-dlp / ffmpeg refresh). Implements the shared
// IUpdaterUI contract so Application's ComponentUpdaterUIController can drive it:
// Load starts the update, progress updates the bar, Finished/Cancelled close it.
using System;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Core.Util;

namespace XDM.Gtk4UI.Dialogs
{
    internal sealed class UpdaterWindow : IUpdaterUI
    {
        // Modal window and its live controls
        private readonly Window window;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;
        private readonly Button cancelButton;
        // Prevents Finished and Cancelled from both firing
        private bool completed;

        public event EventHandler? Cancelled;
        public event EventHandler? Finished;
        public event EventHandler? Load;

        // Status line shown above the progress bar
        public string Label
        {
            get => statusLabel.GetText() ?? string.Empty;
            set => SetOnUi(() => statusLabel.SetText(value ?? string.Empty));
        }

        // Switches the bar to indeterminate (pulse) mode
        public bool Inderminate
        {
            get => progressBar.PulseStep > 0;
            set => SetOnUi(() => progressBar.PulseStep = value ? 1 : 0);
        }

        // Builds the updater window
        public UpdaterWindow(Window? parent)
        {
            window = Window.New();
            window.Title = TextResource.GetText("MSG_UPDATE_AVAILABLE") ?? "Update";
            window.Modal = true;
            window.SetDefaultSize(460, 200);
            if (parent != null)
            {
                window.SetTransientFor(parent);
            }

            var header = HeaderBar.New();
            header.TitleWidget = Gtk.Label.New(window.Title);
            window.SetTitlebar(header);

            var box = Box.New(Orientation.Vertical, 10);
            box.MarginTop = 16;
            box.MarginBottom = 16;
            box.MarginStart = 20;
            box.MarginEnd = 20;

            statusLabel = Gtk.Label.New("");
            statusLabel.Wrap = true;
            statusLabel.Halign = Align.Start;
            box.Append(statusLabel);

            progressBar = ProgressBar.New();
            progressBar.ShowText = true;
            box.Append(progressBar);

            var buttons = Box.New(Orientation.Horizontal, 8);
            buttons.Halign = Align.End;
            buttons.MarginTop = 8;
            cancelButton = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            cancelButton.OnClicked += (_, _) => Cancel();
            buttons.Append(cancelButton);
            box.Append(buttons);

            window.SetChild(box);
            // Closing the window counts as cancelling, exactly once
            window.OnCloseRequest += (_, _) =>
            {
                FireCancelled();
                return false;
            };
        }

        // Presents the window and tells the controller to start downloading
        public void Show()
        {
            window.Present();
            Load?.Invoke(this, EventArgs.Empty);
        }

        // Reports that no component update is available and closes the window
        public void ShowNoUpdateMessage()
        {
            SetOnUi(() =>
            {
                completed = true;
                window.Close();
                Gtk4DialogHelper.ShowAlert(null, TextResource.GetText("MSG_NO_UPDATE") ?? "You are using the latest version.");
            });
        }

        public void DownloadStarted(object? sender, EventArgs e) =>
            SetOnUi(() => statusLabel.SetText(TextResource.GetText("MSG_UPDATE_STARTED") ?? "Downloading update..."));

        public void DownloadProgressChanged(object? sender, ProgressResultEventArgs e) =>
            SetOnUi(() =>
            {
                progressBar.Fraction = Math.Clamp(e.Progress / 100.0, 0.0, 1.0);
                statusLabel.SetText($"{e.Progress}% · {FormattingHelper.FormatSize((long)e.DownloadSpeed)}/s");
            });

        public void DownloadFailed(object? sender, DownloadFailedEventArgs e) =>
            SetOnUi(() =>
            {
                completed = true;
                window.Close();
                Gtk4DialogHelper.ShowAlert(null, TextResource.GetText("MSG_UPDATE_FAILED") ?? "The update could not be downloaded.");
            });

        public void DownloadFinished(object? sender, EventArgs e)
        {
            var first = !completed;
            completed = true;
            SetOnUi(() =>
            {
                progressBar.Fraction = 1.0;
                statusLabel.SetText(TextResource.GetText("MSG_UPDATE_FINISHED") ?? "Update installed.");
                window.Close();
            });
            if (first)
            {
                Finished?.Invoke(this, EventArgs.Empty);
            }
        }

        public void DownloadCancelled(object? sender, EventArgs e) => Cancel();

        // Cancels the running update exactly once and closes the window
        private void Cancel()
        {
            if (FireCancelled())
            {
                window.Close();
            }
        }

        // Raises Cancelled at most once; returns true when it fired
        private bool FireCancelled()
        {
            if (completed)
            {
                return false;
            }
            completed = true;
            Cancelled?.Invoke(this, EventArgs.Empty);
            return true;
        }

        // Marshals a control update onto the GTK main loop
        private static void SetOnUi(Action action)
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                try { action(); }
                catch (Exception ex) { TraceLog.Log.Debug(ex, "Updater UI update failed: " + ex.Message); }
                return false;
            });
        }
    }
}
