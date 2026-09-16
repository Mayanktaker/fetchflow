// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using XDM.Core;
using XDM.Core.Util;
using XDM.Wpf.UI.Common;
using XDM.Wpf.UI.Win32;

namespace XDM.Wpf.UI.Dialogs.About
{
    // Modern About dialog displaying FetchFlow branding, version, and links
    public partial class AboutWindow : Window, IDialog
    {
        public bool Result { get; set; }

        // Initializes modern about window with branding metadata
        public AboutWindow()
        {
            InitializeComponent();
            TxtAppName.Text = AppInfo.APP_FULL_NAME;
            TxtAppVersion.Text = AppInfo.APP_VERSION_ONLY;
            TxtCopyright.Text = AppInfo.APP_COPYRIGHT_TEXT;
            TxtOriginalCredit.Text = AppInfo.APP_ORIGINAL_AUTHOR_CREDIT;
            LoadAppLogo();
        }

        // Loads the highest resolution FetchFlow logo available on disk
        private void LoadAppLogo()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = {
                    Path.Combine(baseDir, "images", "fetchflow-logo-128.png"),
                    Path.Combine(baseDir, "images", "fetchflow-logo.png"),
                    Path.Combine(baseDir, "images", "fetchflow-logo-512.png"),
                    Path.Combine(baseDir, "fetchflow-logo-512.png"),
                    Path.Combine(baseDir, "fetchflow-logo.png")
                };

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(path, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        AppLogo.Source = bmp;
                        break;
                    }
                }
            }
            catch { }
        }

        // Configures dark mode and disables maximize/minimize
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

        // Opens official product website in default browser
        private void BtnAppWebsite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PlatformHelper.OpenBrowser(AppInfo.APP_PRODUCT_URL);
        }

        // Opens developer company website in default browser
        private void BtnDevWebsite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PlatformHelper.OpenBrowser(AppInfo.APP_DEVELOPER_URL);
        }

        // Opens GitHub repository and issue tracker
        private void BtnGitHub_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PlatformHelper.OpenBrowser(Links.SupportUrl);
        }

        // Closes the About dialog
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
