// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using XDM.Core;
using XDM.Wpf.UI.Win32;
using WinForms = System.Windows.Forms;
using XDM.Core.UI;
using XDM.Core.Util;
using XDM.Wpf.UI.Utils;
using Translations;
using Newtonsoft.Json;
using IoPath = System.IO.Path;

namespace XDM.Wpf.UI.Dialogs.Settings
{
    /// <summary>
    /// Interaction logic for GeneralSettingsView.xaml
    /// </summary>
    public partial class GeneralSettingsView : UserControl, ISettingsPage
    {
        // ThemeMode combo indices (Config.ThemeMode: 0 Light, 1 Dark, 2 Follow System)
        private const int ThemeModeLight = 0;
        private const int ThemeModeDark = 1;
        private const int ThemeModeFollowSystem = 2;
        // Highest scheme index carried over verbatim when switching theme mode (GTK parity)
        private const int MaxPortableSchemeIndex = 3;
        // Default export filename for palette JSON files
        private const string PalettesFileName = "fetchflow-palettes.json";
        // File dialog filter for palette JSON files
        private const string PaletteJsonFilter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
        // Palette JSON schema version written on export
        private const string PaletteSchemaVersion = "1.0";
        // Palette export/import result message templates (GTK SettingsDialog parity)
        private const string ExportedPalettesFormat = "Theme palettes successfully exported to:\n{0}";
        private const string ExportFailedPrefix = "Failed to export palettes: ";
        private const string ImportedPalettesFormat = "Successfully validated and loaded {0} custom theme palette(s) from:\n{1}";
        private const string ImportedSinglePaletteFormat = "Successfully validated custom theme palette:\n{0}";
        private const string InvalidPaletteMessage = "The selected file is not a valid FetchFlow palette JSON file.";
        private const string ImportFailedPrefix = "Failed to import palette: ";

        public Window Window { get; set; }

        private ObservableCollection<Category> categories = new ObservableCollection<Category>();

        // Guards SelectionChanged storms while theme combos are rebuilt programmatically
        private bool isUpdatingThemeCombos;

        public GeneralSettingsView()
        {
            InitializeComponent();
            CmbMaxParallalDownloads.ItemsSource = Enumerable.Range(1, 50);
            LoadThemeComboItems();
            LoadLocalizedTexts();
            CmbTheme.SelectionChanged += CmbTheme_SelectionChanged;
            CmbColorScheme.SelectionChanged += CmbColorScheme_SelectionChanged;
        }

        // Fills the theme mode combo: Light / Dark / Follow System
        private void LoadThemeComboItems()
        {
            CmbTheme.Items.Clear();
            CmbTheme.Items.Add("Light");
            CmbTheme.Items.Add("Dark");
            CmbTheme.Items.Add("Follow System");
        }

        // Applies localized labels whose Lang keys may be absent (fallbacks kept inline)
        private void LoadLocalizedTexts()
        {
            LblTheme.Text = TextResource.GetText("SETTINGS_THEME") ?? "Theme:";
            LblColorScheme.Text = TextResource.GetText("SETTINGS_COLOR_SCHEME") ?? "Color scheme:";
            BtnExportPalette.Content = TextResource.GetText("BTN_EXPORT_PALETTES") ?? "Export Palettes";
            BtnImportPalette.Content = TextResource.GetText("BTN_IMPORT_PALETTES") ?? "Import Palettes";
        }

        public void PopulateUI()
        {
            ChkShowPrg.IsChecked = Config.Instance.ShowProgressWindow;
            ChkShowComplete.IsChecked = Config.Instance.ShowDownloadCompleteWindow;
            ChkPlaySound.IsChecked = Config.Instance.PlayCompletionSound;
            ChkStartAuto.IsChecked = Config.Instance.StartDownloadAutomatically;
            ChkOverwrite.IsChecked = Config.Instance.FileConflictResolution == FileConflictResolution.Overwrite;
            ChkDarkTheme.IsChecked = Config.Instance.AllowSystemDarkTheme;
            TxtTempFolder.Text = Config.Instance.TempDir;
            CmbMaxParallalDownloads.SelectedItem = Config.Instance.MaxParallelDownloads;
            ChkAutoCat.IsChecked = Config.Instance.FolderSelectionMode == FolderSelectionMode.Auto;
            TxtDownloadFolder.Text = Config.Instance.DefaultDownloadFolder;
            CmbDblClickAction.SelectedIndex = Config.Instance.DoubleClickOpenFile ? 1 : 0;

            isUpdatingThemeCombos = true;
            CmbTheme.SelectedIndex = ClampThemeMode(Config.Instance.ThemeMode);
            PopulateColorSchemeOptions(IsDarkSelected());
            CmbColorScheme.SelectedIndex = WpfThemeManager.ActiveColorScheme;
            isUpdatingThemeCombos = false;

            foreach (var cat in Config.Instance.Categories)
            {
                categories.Add(cat);
            }
            LvCategories.ItemsSource = categories;
        }

        public void UpdateConfig()
        {
            Config.Instance.ShowProgressWindow = ChkShowPrg.IsChecked ?? false;
            Config.Instance.ShowDownloadCompleteWindow = ChkShowComplete.IsChecked ?? false;
            Config.Instance.PlayCompletionSound = ChkPlaySound.IsChecked ?? false;
            Config.Instance.StartDownloadAutomatically = ChkStartAuto.IsChecked ?? false;
            Config.Instance.FileConflictResolution =
                ChkOverwrite.IsChecked.HasValue && ChkOverwrite.IsChecked.Value ? FileConflictResolution.Overwrite : FileConflictResolution.AutoRename;
            Config.Instance.TempDir = TxtTempFolder.Text;
            Config.Instance.MaxParallelDownloads = CmbMaxParallalDownloads.SelectedItem is int maxParallel ? maxParallel : Config.Instance.MaxParallelDownloads;

            Config.Instance.Categories = new List<Category>(this.categories);
            Config.Instance.FolderSelectionMode = ChkAutoCat.IsChecked.HasValue && ChkAutoCat.IsChecked.Value ?
                FolderSelectionMode.Auto : FolderSelectionMode.Manual;
            Config.Instance.DefaultDownloadFolder = TxtDownloadFolder.Text;
            Config.Instance.AllowSystemDarkTheme = ChkDarkTheme.IsChecked ?? false;
            Config.Instance.DoubleClickOpenFile = CmbDblClickAction.SelectedIndex == 1;
            Config.Instance.ThemeMode = CmbTheme.SelectedIndex >= 0 ? CmbTheme.SelectedIndex : Config.Instance.ThemeMode;
            Config.Instance.ColorScheme = CmbColorScheme.SelectedIndex >= 0 ? CmbColorScheme.SelectedIndex : 0;
            WpfThemeManager.ApplyTheme(
                Config.Instance.ThemeMode == ThemeModeFollowSystem ? null : (bool?)(Config.Instance.ThemeMode == ThemeModeDark),
                Config.Instance.ColorScheme);
        }

        // Clamps a raw ThemeMode value into the valid combo index range
        private static int ClampThemeMode(int mode)
        {
            if (mode < ThemeModeLight || mode > ThemeModeFollowSystem)
            {
                return ThemeModeFollowSystem;
            }
            return mode;
        }

        // Returns whether dark theme is resolved for the current combo selection
        private bool IsDarkSelected()
        {
            if (CmbTheme.SelectedIndex == ThemeModeDark) return true;
            if (CmbTheme.SelectedIndex == ThemeModeLight) return false;
            return WpfThemeManager.IsDarkActive;
        }

        // Rebuilds the scheme combo from the mode-specific scheme list (caller holds guard)
        private void PopulateColorSchemeOptions(bool isDark)
        {
            CmbColorScheme.Items.Clear();
            var schemes = isDark ? ColorSchemeTable.DarkSchemes : ColorSchemeTable.LightSchemes;
            foreach (var scheme in schemes)
            {
                CmbColorScheme.Items.Add(scheme.DisplayName);
            }
        }

        // Applies the live theme preview whenever either combo changes
        private void ApplySelectedTheme()
        {
            WpfThemeManager.ApplyTheme(
                CmbTheme.SelectedIndex == ThemeModeFollowSystem ? null : (bool?)(CmbTheme.SelectedIndex == ThemeModeDark),
                CmbColorScheme.SelectedIndex);
        }

        private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingThemeCombos || CmbTheme.SelectedIndex < 0) return;
            var wasScheme = CmbColorScheme.SelectedIndex;
            isUpdatingThemeCombos = true;
            PopulateColorSchemeOptions(IsDarkSelected());
            CmbColorScheme.SelectedIndex = wasScheme >= 0 && wasScheme <= MaxPortableSchemeIndex ? wasScheme : 0;
            isUpdatingThemeCombos = false;
            ApplySelectedTheme();
        }

        private void CmbColorScheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingThemeCombos || CmbColorScheme.SelectedIndex < 0) return;
            ApplySelectedTheme();
        }

        // Serializes all shared schemes to palette JSON (GTK ThemeManager.ExportPalettesJson shape)
        private static string BuildPalettesJson()
        {
            var data = new
            {
                Version = PaletteSchemaVersion,
                DarkSchemes = ColorSchemeTable.DarkSchemes.Select(s => new
                {
                    s.Id,
                    s.DisplayName,
                    s.CssFileName,
                    AccentR = s.AccentRgb.R,
                    AccentG = s.AccentRgb.G,
                    AccentB = s.AccentRgb.B,
                    s.AccentHex,
                    s.HoverBackgroundHex,
                    s.ActiveBackgroundHex,
                    s.AlternateBackgroundHex,
                    s.CardBackgroundHex
                }),
                LightSchemes = ColorSchemeTable.LightSchemes.Select(s => new
                {
                    s.Id,
                    s.DisplayName,
                    s.CssFileName,
                    AccentR = s.AccentRgb.R,
                    AccentG = s.AccentRgb.G,
                    AccentB = s.AccentRgb.B,
                    s.AccentHex,
                    s.HoverBackgroundHex,
                    s.ActiveBackgroundHex,
                    s.AlternateBackgroundHex,
                    s.CardBackgroundHex
                })
            };
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        // Exports the current theme palettes to a user-selected JSON file
        private void BtnExportPalette_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = PalettesFileName,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Filter = PaletteJsonFilter
                };
                if (dialog.ShowDialog(Window) == true)
                {
                    System.IO.File.WriteAllText(dialog.FileName, BuildPalettesJson());
                    MessageBox.Show(Window, string.Format(ExportedPalettesFormat, dialog.FileName));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window, ExportFailedPrefix + ex.Message);
            }
        }

        // Imports and validates custom theme palettes from a user-selected JSON file
        private void BtnImportPalette_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = PaletteJsonFilter
                };
                if (dialog.ShowDialog(Window) == true && System.IO.File.Exists(dialog.FileName))
                {
                    var json = System.IO.File.ReadAllText(dialog.FileName);
                    var imported = ThemePaletteHelper.ImportPalettes(json);
                    if (imported != null && imported.Count > 0)
                    {
                        MessageBox.Show(Window, string.Format(ImportedPalettesFormat, imported.Count, IoPath.GetFileName(dialog.FileName)));
                        return;
                    }
                    var paletteName = ParsePaletteDisplayName(json);
                    if (paletteName != null)
                    {
                        MessageBox.Show(Window, string.Format(ImportedSinglePaletteFormat, paletteName));
                    }
                    else
                    {
                        MessageBox.Show(Window, InvalidPaletteMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window, ImportFailedPrefix + ex.Message);
            }
        }

        // Minimal single-palette validation (GTK ThemeManager.ParsePaletteJson counterpart)
        private static string? ParsePaletteDisplayName(string json)
        {
            try
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (dict != null && dict.TryGetValue("Id", out var idObj) && dict.TryGetValue("DisplayName", out var nameObj))
                {
                    var id = idObj?.ToString();
                    var displayName = nameObj?.ToString();
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(displayName))
                    {
                        return displayName;
                    }
                }
            }
            catch (Exception)
            {
                // Not a JSON object: fall through to the invalid-palette branch
            }
            return null;
        }

        private void CatAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CategoryEditWindow { Owner = Window };
            var ret = dlg.ShowDialog(Window);
            if (ret.HasValue && ret.Value)
            {
                categories.Add(new Category
                {
                    Name = Guid.NewGuid().ToString(),
                    DisplayName = dlg.CategoryName,
                    DefaultFolder = dlg.Folder,
                    FileExtensions = new HashSet<string>(dlg.FileTypes.Replace("\r\n", "")
                    .Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                });
            }
        }

        private void CatEdit_Click(object sender, RoutedEventArgs e)
        {
            var index = LvCategories.SelectedIndex;
            if (index >= 0)
            {
                var cat = categories[index];
                var dlg = new CategoryEditWindow { Owner = Window };
                dlg.SetCategory(categories[index]);
                var ret = dlg.ShowDialog(Window);
                if (ret.HasValue && ret.Value)
                {
                    categories[index] = new Category
                    {
                        Name = cat.Name,
                        DisplayName = dlg.CategoryName,
                        DefaultFolder = dlg.Folder,
                        FileExtensions = new HashSet<string>(dlg.FileTypes.Replace("\r\n", "")
                        .Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                    };
                }
            }
        }

        private void CatDel_Click(object sender, RoutedEventArgs e)
        {
            var index = LvCategories.SelectedIndex;
            if (index >= 0)
            {
                categories.RemoveAt(index);
            }
        }

        private void CatDef_Click(object sender, RoutedEventArgs e)
        {
            var items = new List<Category>(this.categories);
            foreach (var cat in items)
            {
                this.categories.Remove(cat);
            }
            foreach (var cat in Config.DefaultCategories)
            {
                this.categories.Add(cat);
            }
        }

        private void BtnTempFolderBrowse_Click(object sender, RoutedEventArgs e)
        {
            using var folderBrowser = new WinForms.FolderBrowserDialog();
            if (folderBrowser.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtTempFolder.Text = folderBrowser.SelectedPath;
            }
        }

        private void BtnDownloadFolderBrowse_Click(object sender, RoutedEventArgs e)
        {
            using var folderBrowser = new WinForms.FolderBrowserDialog();
            if (folderBrowser.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtDownloadFolder.Text = folderBrowser.SelectedPath;
            }
        }
    }
}
