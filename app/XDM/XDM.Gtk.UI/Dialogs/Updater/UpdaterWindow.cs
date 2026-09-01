// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gtk;
using GLib;
using Application = Gtk.Application;
using IoPath = System.IO.Path;
using XDM.Core;
using Translations;
using UI = Gtk.Builder.ObjectAttribute;
using XDM.GtkUI.Utils;
using XDM.Core.Downloader;

namespace XDM.GtkUI.Dialogs.Updater
{
    public class UpdaterWindow : Window, IUpdaterUI
    {
        [UI] private Label TxtHeading;
        [UI] private ProgressBar Prg;
        [UI] private Button BtnCancel;
        [UI] private Button BtnInstall;
        private bool active = false;

        private UpdaterWindow(Builder builder) : base(builder.GetRawOwnedObject("window"))
        {
            builder.Autoconnect(this);
            var titleText = TextResource.GetText("OPT_UPDATE_FFMPEG");
            Title = titleText;
            Titlebar = GtkHelper.CreateDialogHeaderBar(titleText);
            GtkHelper.SetWindowAppIcon(this);
            // Wayland/Phase1.4: compositor places windows; client centering removed (no-op on Wayland)

            BtnCancel.Label = TextResource.GetText("ND_CANCEL");
            // English fallback covers languages until they add MSG_INSTALL_UPDATE
            BtnInstall.Label = TextResource.GetText("MSG_INSTALL_UPDATE");
            BtnInstall.StyleContext.AddClass("suggested-action");
            BtnInstall.Sensitive = false;
            TxtHeading.Text = TextResource.GetText("STAT_DOWNLOADING");
            SetDefaultSize(600, 220);

            GtkHelper.AttachSafeDispose(this);

            Realized += UpdaterWindow_Realized;
            BtnCancel.Clicked += BtnCancel_Clicked;
            BtnInstall.Clicked += BtnInstall_Clicked;
            DeleteEvent += UpdaterWindow_DeleteEvent;
        }

        // Same updater-script flow as MainWindow.CheckUpdatesInBackground (kept local:
        // shared helper would cross into files outside this change's scope)
        private void BtnInstall_Clicked(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = "gnome-terminal",
                    Arguments = "-- bash -c \"sudo /opt/fetchflow/fetchflow-updater.sh || sudo /opt/xdman/xdm-updater.sh\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not start updater terminal: " + ex);
            }
            CloseWindow();
        }

        private void UpdaterWindow_DeleteEvent(object o, DeleteEventArgs args)
        {
            if (active)
            {
                Cancelled?.Invoke(this, EventArgs.Empty);
            }
        }

        private void BtnCancel_Clicked(object? sender, EventArgs e)
        {
            Cancelled?.Invoke(sender, e);
        }

        private void CloseWindow()
        {
            Close();
            Dispose();
        }

        public void DownloadCancelled(object? sender, EventArgs e)
        {
            active = false;
            Application.Invoke((_, _) => CloseWindow());
        }

        private void UpdaterWindow_Realized(object? sender, EventArgs e)
        {
            Load?.Invoke(this, EventArgs.Empty);
        }

        public string Label
        {
            get => TxtHeading.Text;
            set => Application.Invoke((_, _) => TxtHeading.Text = value);
        }

        public bool Inderminate { get; set; }

        public event EventHandler? Cancelled;
        public event EventHandler? Finished;
        public event EventHandler? Load;

        public static UpdaterWindow CreateFromGladeFile()
        {
            var builder = new Builder();
            builder.AddFromFile(IoPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "glade", "updater-window.glade"));
            return new UpdaterWindow(builder);
        }

        public void DownloadFailed(object? sender, DownloadFailedEventArgs e)
        {
            active = false;
            Application.Invoke((_, _) =>
            {
                GtkHelper.ShowMessageBox(this, TextResource.GetText("MSG_FAILED"));
                CloseWindow();
            });
        }

        public void DownloadFinished(object? sender, EventArgs e)
        {
            active = false;
            Application.Invoke((_, _) =>
            {
                GtkHelper.ShowMessageBox(this, TextResource.GetText("MSG_UPDATED"));
                // Stay open so the user can run the updater via BtnInstall instead of auto-closing
                BtnInstall.Sensitive = true;
            });
            this.Finished?.Invoke(sender, e);
        }

        public void DownloadProgressChanged(object? sender, ProgressResultEventArgs e)
        {
            Application.Invoke((_, _) => Prg.Fraction = e.Progress / 100.0d);
        }

        public void DownloadStarted(object? sender, EventArgs e)
        {
            active = true;
        }

        public void ShowNoUpdateMessage()
        {
            active = false;
            Application.Invoke((_, _) =>
            {
                GtkHelper.ShowMessageBox(this, TextResource.GetText("MSG_NO_UPDATE"));
                CloseWindow();
            });
            this.Finished?.Invoke(this, EventArgs.Empty);
        }
    }
}
