// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using XDM.Core;
using XDM.Core.Util;
using XDM.Core.UI;
using XDM.Wpf.UI.Win32;

namespace XDM.Wpf.UI.Dialogs.CompletedDialog
{
    // Notification dialog shown when a download completes
    public partial class DownloadCompleteWindow : Window, IDownloadCompleteDialog
    {
        public event EventHandler<DownloadCompleteDialogEventArgs>? FileOpenClicked;
        public event EventHandler<DownloadCompleteDialogEventArgs>? FolderOpenClicked;
        public event EventHandler? DontShowAgainClickd;

        private DispatcherTimer? autoCloseTimer;
        private int remainingSeconds = 10;
        private bool isMouseHovering = false;

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

        // Initializes dialog and attaches mouse-aware auto-close timer
        public DownloadCompleteWindow()
        {
            InitializeComponent();
            MouseEnter += (_, _) => isMouseHovering = true;
            MouseLeave += (_, _) => isMouseHovering = false;
            Closed += (_, _) => StopAutoCloseTimer();
            Loaded += (_, _) => StartAutoCloseTimer();
        }

        // Updates file icon geometry based on extension
        private void UpdateFileIcon(string fileName)
        {
            try
            {
                var ext = System.IO.Path.GetExtension(fileName);
                var iconKey = IconMap.GetVectorNameForFileType(ext);
                if (TryFindResource(iconKey) is Geometry geo)
                {
                    ImgFileIcon.Data = geo;
                }
            }
            catch { }
        }

        // Starts 10-second countdown timer for auto-close
        private void StartAutoCloseTimer()
        {
            autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            autoCloseTimer.Tick += (s, e) =>
            {
                if (isMouseHovering) return;
                remainingSeconds--;
                if (remainingSeconds <= 0)
                {
                    StopAutoCloseTimer();
                    Close();
                }
            };
            autoCloseTimer.Start();
        }

        // Stops the auto-close timer
        private void StopAutoCloseTimer()
        {
            if (autoCloseTimer != null)
            {
                autoCloseTimer.Stop();
                autoCloseTimer = null;
            }
        }

        // Disables future completion dialogs and closes
        private void TxtDontShowCompleteDialog_MouseDown(object sender, MouseButtonEventArgs e)
        {
            StopAutoCloseTimer();
            DontShowAgainClickd?.Invoke(this, EventArgs.Empty);
            Close();
        }

        // Opens the downloaded file and closes dialog
        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            StopAutoCloseTimer();
            FileOpenClicked?.Invoke(sender, new DownloadCompleteDialogEventArgs
            {
                Path = System.IO.Path.Combine(TxtLocation.Text, TxtFileName.Text)
            });
            Close();
        }

        // Opens the Checksum Verification dialog for the downloaded file
        private void BtnChecksum_Click(object sender, RoutedEventArgs e)
        {
            StopAutoCloseTimer();
            var fullPath = System.IO.Path.Combine(TxtLocation.Text, TxtFileName.Text);
            if (System.IO.File.Exists(fullPath))
            {
                var dlg = new XDM.Wpf.UI.Dialogs.Checksum.ChecksumWindow(this, fullPath);
                dlg.Show();
            }
        }

        // Opens the download folder in Explorer and closes dialog
        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            StopAutoCloseTimer();
            FolderOpenClicked?.Invoke(sender, new DownloadCompleteDialogEventArgs
            {
                Path = TxtLocation.Text,
                FileName = TxtFileName.Text
            });
            Close();
        }

        // Displays the completion window
        public void ShowDownloadCompleteDialog()
        {
            this.Show();
        }

        // Configures dark mode and window buttons on init
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            NativeMethods.DisableMinMaxButton(this);
#if NET45_OR_GREATER
            if (XDM.Wpf.UI.App.Skin == Skin.Dark)
            {
                var helper = new WindowInteropHelper(this);
                helper.EnsureHandle();
                DarkModeHelper.UseImmersiveDarkMode(helper.Handle, true);
            }
#endif
        }
    }
}
