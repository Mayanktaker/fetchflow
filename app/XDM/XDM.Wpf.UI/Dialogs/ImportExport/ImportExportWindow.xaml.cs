// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Windows;
using System.Windows.Interop;
using Translations;
using XDM.Wpf.UI.Win32;

namespace XDM.Wpf.UI.Dialogs.ImportExport
{
    // Decoupled backup chooser raising export/import requests for external wiring
    public partial class ImportExportWindow : Window
    {
        // Raised when the user picks the export card; wiring subscribes externally
        public event EventHandler? ExportRequested;

        // Raised when the user picks the import card; wiring subscribes externally
        public event EventHandler? ImportRequested;

        // Initializes the chooser window owned by the caller
        public ImportExportWindow(Window? owner)
        {
            InitializeComponent();
            Owner = owner;
            LoadLocalizedTexts();
        }

        // Applies localized labels with English fallbacks
        private void LoadLocalizedTexts()
        {
            Title = TextResource.GetText("MENU_IMPORT_EXPORT") ?? "Import / Export";
            LblHeader.Text = TextResource.GetText("LBL_IMPORT_EXPORT_HEADER") ?? "Backup & Restore Downloads";
            LblSub.Text = TextResource.GetText("LBL_IMPORT_EXPORT_SUB") ?? "Export your download list and settings to a .zip archive, or restore from a backup.";
            LblExportTitle.Text = TextResource.GetText("BTN_EXPORT_TITLE") ?? "Export Download List";
            LblExportDesc.Text = TextResource.GetText("BTN_EXPORT_DESC") ?? "Save download items, queues, and settings to a .zip archive.";
            LblImportTitle.Text = TextResource.GetText("BTN_IMPORT_TITLE") ?? "Import Download List";
            LblImportDesc.Text = TextResource.GetText("BTN_IMPORT_DESC") ?? "Restore download items, queues, and settings from a .zip backup archive.";
            BtnClose.Content = TextResource.GetText("ND_CANCEL") ?? "Close";
        }

        // Raises the export request and closes the chooser
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            ExportRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        // Raises the import request and closes the chooser
        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            ImportRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        // Closes the chooser without raising any request
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Strips the min/max chrome and applies the immersive dark titlebar in dark skin
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            NativeMethods.DisableMinMaxButton(this);
#if NET45_OR_GREATER
            if (App.Skin == Skin.Dark)
            {
                var helper = new WindowInteropHelper(this);
                helper.EnsureHandle();
                DarkModeHelper.UseImmersiveDarkMode(helper.Handle, true);
            }
#endif
        }
    }
}
