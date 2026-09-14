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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using TraceLog;
using Translations;
using XDM.Core;
using XDM.Core.UI;
using XDM.Core.Util;
using System.Diagnostics;
using XDM.Core.BrowserMonitoring;
using XDM.Wpf.UI.Dialogs.ChromeIntegrator;

namespace XDM.Wpf.UI.Dialogs.Settings
{
    /// <summary>
    /// Interaction logic for BrowserMonitoringView.xaml
    /// </summary>
    public partial class BrowserMonitoringView : UserControl, ISettingsPage
    {
        // IPC relay UI strings (glade literals in GTK — not yet in Lang files)
        private const string RestartIpcLabel = "Restart IPC Relay";
        private const string RestartIpcBusyLabel = "Restarting...";
        private const string IpcLoopbackHost = "127.0.0.1";
        private const string IpcRunningSessionsFormat = "● Running: {0}:{1} ({2} active extensions)";
        private const string IpcRunningReadyFormat = "● Running: {0}:{1} (Ready)";
        private const string IpcListeningFormat = "○ Listening: {0}:{1}";
        private const string IpcActiveColor = "#22c55e";
        private const string IpcReadyColor = "#38bdf8";
        private const string IpcIdleColor = "#94a3b8";
        // Extension shortcut tip + guide (GTK settings-dialog.glade literals)
        private const string ExtensionShortcutsGuideUrl = "https://github.com/Mayanktaker/fetchflow#browser-shortcuts";
        // Delay before the IPC status label refreshes after a restart
        private const int IpcRestartRefreshDelayMs = 600;

        public BrowserMonitoringView()
        {
            InitializeComponent();
            CmbMinVidSize.ItemsSource = new int[] { 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768 };
            UpdateIpcStatus();
        }

        public void PopulateUI()
        {
            TxtChromeWebStoreUrl.Text = Links.ManualExtensionInstallGuideUrl;
            TxtFirefoxAMOUrl.Text = Links.FirefoxExtensionUrl;
            TxtDefaultFileTypes.Text = string.Join(",", Config.Instance.FileExtensions);
            TxtDefaultVideoFormats.Text = string.Join(",", Config.Instance.VideoExtensions);
            TxtExceptions.Text = string.Join(",", Config.Instance.BlockedHosts);
            CmbMinVidSize.SelectedItem = Config.Instance.MinVideoSize;
            ChkMonitorClipboard.IsChecked = Config.Instance.MonitorClipboard;
            ChkTimestamp.IsChecked = Config.Instance.FetchServerTimeStamp;
            ChkShowMediaNotification.IsChecked = Config.Instance.ShowNotification;
            UpdateIpcStatus();
        }

        public void UpdateConfig()
        {
            //Browser monitoring
            Config.Instance.FileExtensions = TxtDefaultFileTypes.Text.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            Config.Instance.VideoExtensions = TxtDefaultVideoFormats.Text.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            Config.Instance.BlockedHosts = TxtExceptions.Text.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            Config.Instance.FetchServerTimeStamp = ChkTimestamp.IsChecked.HasValue ? ChkTimestamp.IsChecked.Value : false;
            Config.Instance.MonitorClipboard = ChkMonitorClipboard.IsChecked.HasValue ? ChkMonitorClipboard.IsChecked.Value : false;
            Config.Instance.MinVideoSize = (int)CmbMinVidSize.SelectedItem;
            Config.Instance.ShowNotification = ChkShowMediaNotification.IsChecked.HasValue ? ChkShowMediaNotification.IsChecked.Value : false;
        }

        // Refreshes the IPC server connectivity badge and port display
        private void UpdateIpcStatus()
        {
            var port = IpcHttpMessageProcessor.EffectivePort;
            var count = IpcHttpMessageProcessor.ActiveWebSocketSessionsCount;
            var isConnected = IpcHttpMessageProcessor.IsConnected;

            string text;
            string color;
            if (count > 0)
            {
                text = string.Format(IpcRunningSessionsFormat, IpcLoopbackHost, port, count);
                color = IpcActiveColor;
            }
            else if (isConnected)
            {
                text = string.Format(IpcRunningReadyFormat, IpcLoopbackHost, port);
                color = IpcReadyColor;
            }
            else
            {
                text = string.Format(IpcListeningFormat, IpcLoopbackHost, port);
                color = IpcIdleColor;
            }
            LblIpcStatus.Text = text;
            LblIpcStatus.Foreground = ParseBrush(color);
        }

        // Converts a hex color string to a brush, falling back to gray
        private static Brush ParseBrush(string hex)
        {
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
            catch (Exception)
            {
                return new SolidColorBrush(Colors.Gray);
            }
        }

        // Restarts the background IPC server and updates the status label
        private void BtnRestartIpc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnRestartIpc.IsEnabled = false;
                BtnRestartIpc.Content = RestartIpcBusyLabel;
                BrowserMonitor.Restart();
                UpdateIpcStatus();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(IpcRestartRefreshDelayMs) };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    BtnRestartIpc.IsEnabled = true;
                    BtnRestartIpc.Content = RestartIpcLabel;
                    UpdateIpcStatus();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                Log.Debug("Error restarting IPC server from settings: " + ex.Message);
                BtnRestartIpc.IsEnabled = true;
                BtnRestartIpc.Content = RestartIpcLabel;
            }
        }

        // Opens the FetchFlow browser shortcut documentation and setup guide
        private void BtnExtensionShortcuts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PlatformHelper.OpenBrowser(ExtensionShortcutsGuideUrl);
            }
            catch (Exception ex)
            {
                Log.Debug("Error opening shortcuts guide: " + ex.Message);
            }
        }

        private void BrowserButtonClick(Browser browser)
        {
            try
            {
                if (MsixHelper.IsAppContainer)
                {
                    MsixHelper.CopyExtension();
                }
                LaunchGuide(MsixHelper.IsAppContainer, browser);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error launching " + browser);
                MessageBox.Show($"{TextResource.GetText("MSG_BROWSER_LAUNCH_FAILED")} {browser}");
            }
        }


        private void BtnChrome_Click(object sender, RoutedEventArgs e)
        {
            BrowserButtonClick(Browser.Chrome);
        }

        private void BtnFirefox_Click(object sender, RoutedEventArgs e)
        {
            if (MsixHelper.IsAppContainer)
            {
                MsixHelper.CopyExtension();
            }
            try
            {
                BrowserLauncher.LaunchFirefox(Links.FirefoxExtensionUrl, null);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error launching Firefox");
                MessageBox.Show($"{TextResource.GetText("MSG_BROWSER_LAUNCH_FAILED")} Firefox");
            }
        }

        private void LaunchGuide(bool isAppContainer, Browser browser)
        {
            var exe = isAppContainer ? System.IO.Path.Combine(System.IO.Path.Combine(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."),
                "XDM.WinForms.IntegrationUI"), "xdm-guide.exe") :
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xdm-guide.exe");
            ProcessStartInfo psi = new()
            {
                FileName = exe,
                UseShellExecute = true,
                Arguments = browser.ToString()
            };
            Log.Debug("Launching " + exe);
            Process.Start(psi);
        }

        private void BtnEdge_Click(object sender, RoutedEventArgs e)
        {
            BrowserButtonClick(Browser.MSEdge);
        }

        private void BtnOpera_Click(object sender, RoutedEventArgs e)
        {
            BrowserButtonClick(Browser.Opera);
        }

        private void BtnBrave_Click(object sender, RoutedEventArgs e)
        {
            BrowserButtonClick(Browser.Brave);
        }

        private void BtnVivaldi_Click(object sender, RoutedEventArgs e)
        {
            BrowserButtonClick(Browser.Vivaldi);
        }

        private void BtnCopy1_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(TxtChromeWebStoreUrl.Text);
        }

        private void BtnCopy2_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(TxtFirefoxAMOUrl.Text);
        }

        private void BtnDefault1_Click(object sender, RoutedEventArgs e)
        {
            TxtDefaultFileTypes.Text = string.Join(",", Config.DefaultFileExtensions);
        }

        private void BtnDefault2_Click(object sender, RoutedEventArgs e)
        {
            TxtDefaultVideoFormats.Text = string.Join(",", Config.DefaultVideoExtensions);
        }

        private void BtnDefault3_Click(object sender, RoutedEventArgs e)
        {
            TxtExceptions.Text = string.Join(",", Config.DefaultBlockedHosts);
        }

        private void VideoWikiLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PlatformHelper.OpenBrowser(Links.VideoDownloadTutorialUrl);
        }
    }
}
