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
using XDM.Core.UI;

namespace XDM.GtkUI.Dialogs.DownloadComplete
{
    // Modernized Download Complete notification dialog
    public class DownloadCompleteDialog : Window, IDownloadCompleteDialog
    {
        // Displayed filename with dynamic file type icon update
        public string FileNameText
        {
            get => TxtFileName.Text;
            set
            {
                TxtFileName.Text = value;
                UpdateFileIcon(value);
            }
        }

        // Displayed download location folder path
        public string FolderText
        {
            get => TxtLocation.Text;
            set => TxtLocation.Text = value;
        }

        public event EventHandler<DownloadCompleteDialogEventArgs>? FileOpenClicked;
        public event EventHandler<DownloadCompleteDialogEventArgs>? FolderOpenClicked;
        public event EventHandler? DontShowAgainClickd;

        [UI] private Image ImgFileIcon;
        [UI] private Label TxtFileName;
        [UI] private Label TxtLocation;
        [UI] private Button BtnOpenFolder;
        [UI] private Button BtnOpen;
        [UI] private Button BtnChecksum;
        [UI] private LinkButton TxtDontShowCompleteDialog;

        private uint autoCloseTimerId = 0;
        private int remainingSeconds = 10;
        private bool isMouseHovering = false;

        // Constructs dialog from builder and wires design system controls and event handlers
        private DownloadCompleteDialog(Builder builder) : base(builder.GetRawOwnedObject("window"))
        {
            builder.Autoconnect(this);
            KeepAbove = true;
            Resizable = false;

            var titleText = TextResource.GetText("MSG_DOWNLOAD_COMPLETE");
            Title = titleText;
            Titlebar = GtkHelper.CreateDialogHeaderBar(titleText);
            GtkHelper.SetWindowAppIcon(this);

            BtnOpen.Label = TextResource.GetText("CTX_OPEN_FILE");
            BtnOpenFolder.Label = TextResource.GetText("CTX_OPEN_FOLDER");
            BtnChecksum.Label = TextResource.GetText("CTX_CHECKSUM") ?? "Verify Checksum";
            TxtDontShowCompleteDialog.Label = TextResource.GetText("MSG_DONT_SHOW_AGAIN");

            BtnOpen.Clicked += BtnOpen_Click;
            BtnOpenFolder.Clicked += BtnOpenFolder_Click;
            BtnChecksum.Clicked += BtnChecksum_Click;
            TxtDontShowCompleteDialog.Clicked += TxtDontShowCompleteDialog_Clicked;
            TxtDontShowCompleteDialog.ActivateLink += TxtDontShowCompleteDialog_ActivateLink;

            AddEvents((int)(Gdk.EventMask.ButtonPressMask | Gdk.EventMask.EnterNotifyMask | Gdk.EventMask.LeaveNotifyMask));
            ButtonPressEvent += DownloadCompleteDialog_ButtonPressEvent;
            EnterNotifyEvent += (_, _) => isMouseHovering = true;
            LeaveNotifyEvent += (_, _) => isMouseHovering = false;
            Destroyed += (_, _) => StopAutoCloseTimer();

            SetDefaultSize(560, 200);
            GtkHelper.AttachSafeDispose(this);
        }

        // Opens the Checksum Verification dialog for the downloaded file
        private void BtnChecksum_Click(object? sender, EventArgs e)
        {
            StopAutoCloseTimer();
            var fullPath = IoPath.Combine(TxtLocation.Text, TxtFileName.Text);
            if (System.IO.File.Exists(fullPath))
            {
                var dlg = new XDM.GtkUI.Dialogs.Checksum.ChecksumDialog(this, fullPath);
                dlg.ShowAll();
            }
        }

        // Starts the auto-close countdown timer if enabled in configuration
        private void StartAutoCloseTimer()
        {
            StopAutoCloseTimer();
            var duration = Config.Instance.AutoDismissCompleteDialogSeconds;
            if (duration <= 0) return;

            remainingSeconds = duration;
            autoCloseTimerId = GLib.Timeout.Add(1000, OnAutoCloseTick);
        }

        // Stops the auto-close countdown timer
        private void StopAutoCloseTimer()
        {
            if (autoCloseTimerId != 0)
            {
                GLib.Source.Remove(autoCloseTimerId);
                autoCloseTimerId = 0;
            }
        }

        // Ticks every second and closes dialog when timer expires unless mouse is hovering
        private bool OnAutoCloseTick()
        {
            if (isMouseHovering) return true;

            remainingSeconds--;
            if (remainingSeconds <= 0)
            {
                autoCloseTimerId = 0;
                Close();
                return false;
            }
            return true;
        }

        // Enables smooth window dragging from window background on Wayland and X11
        [GLib.ConnectBefore]
        private void DownloadCompleteDialog_ButtonPressEvent(object o, ButtonPressEventArgs args)
        {
            if (args.Event.Button == 1)
            {
                BeginMoveDrag((int)args.Event.Button, (int)args.Event.XRoot, (int)args.Event.YRoot, args.Event.Time);
            }
        }

        // Resolves and loads appropriate SVG file icon based on completed file type
        private void UpdateFileIcon(string fileName)
        {
            try
            {
                var iconName = IconResource.GetSVGNameForFileType(fileName);
                ImgFileIcon.Pixbuf = GtkHelper.LoadSvg(iconName, 48) ?? GtkHelper.LoadSvg("file-download-line", 48);
            }
            catch
            {
                ImgFileIcon.Pixbuf = GtkHelper.LoadSvg("file-download-line", 48);
            }
        }

        // Handles link activation for suppressing future completion dialogs
        private void TxtDontShowCompleteDialog_ActivateLink(object o, ActivateLinkArgs args)
        {
            args.RetVal = true;
            HandleDontShowAgain();
        }

        // Handles button click for suppressing future completion dialogs
        private void TxtDontShowCompleteDialog_Clicked(object? sender, EventArgs e)
        {
            HandleDontShowAgain();
        }

        // Persists suppress setting and closes dialog
        private void HandleDontShowAgain()
        {
            StopAutoCloseTimer();
            DontShowAgainClickd?.Invoke(this, EventArgs.Empty);
            Close();
        }

        // Launches downloaded file using default system viewer
        private void BtnOpen_Click(object? sender, EventArgs e)
        {
            StopAutoCloseTimer();
            FileOpenClicked?.Invoke(sender, new DownloadCompleteDialogEventArgs
            {
                Path = IoPath.Combine(TxtLocation.Text, TxtFileName.Text)
            });
            Close();
        }

        // Reveals containing folder in system file manager
        private void BtnOpenFolder_Click(object? sender, EventArgs e)
        {
            StopAutoCloseTimer();
            FolderOpenClicked?.Invoke(sender, new DownloadCompleteDialogEventArgs
            {
                Path = TxtLocation.Text,
                FileName = TxtFileName.Text
            });
            Close();
        }

        // Displays and presents dialog to user, sends desktop notification, plays sound, and starts auto-close timer
        public void ShowDownloadCompleteDialog()
        {
            SetDefaultSize(520, 200);
            ShowAll();
            Present();
            StartAutoCloseTimer();
            DesktopNotificationHelper.ShowDownloadComplete(FileNameText, FolderText);
            if (Config.Instance.PlayCompletionSound)
            {
                SoundHelper.PlayDownloadCompleteSound();
            }
        }

        // Factory method to inflate dialog from glade definition
        public static DownloadCompleteDialog CreateFromGladeFile()
        {
            var builder = new Builder();
            builder.AddFromFile(IoPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "glade", "download-complete-window.glade"));
            return new DownloadCompleteDialog(builder);
        }
    }
}
