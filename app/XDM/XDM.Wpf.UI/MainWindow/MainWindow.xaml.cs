// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TraceLog;
using Translations;
using XDM.Core.UI;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Core.Util;
using XDM.Wpf.UI.Dialogs.About;
using XDM.Wpf.UI.Dialogs.BatchDownload;
using XDM.Wpf.UI.Dialogs.CompletedDialog;
using XDM.Wpf.UI.Dialogs.CredentialDialog;
using XDM.Wpf.UI.Dialogs.DeleteConfirm;
using XDM.Wpf.UI.Dialogs.DownloadSelection;
using XDM.Wpf.UI.Dialogs.ImportExport;
using XDM.Wpf.UI.Dialogs.LanguageSettings;
using XDM.Wpf.UI.Dialogs.NewDownload;
using XDM.Wpf.UI.Dialogs.NewVideoDownload;
using XDM.Wpf.UI.Dialogs.ProgressWindow;
using XDM.Wpf.UI.Dialogs.PropertiesDialog;
using XDM.Wpf.UI.Dialogs.QueuesWindow;
using XDM.Wpf.UI.Dialogs.RefreshLink;
using XDM.Wpf.UI.Dialogs.Settings;
using XDM.Wpf.UI.Dialogs.Updater;
using XDM.Wpf.UI.Dialogs.VideoDownloader;
using XDM.Wpf.UI.Dialogs.Widget;
using XDM.Wpf.UI.Win32;
using XDM.Wpf.UI.Dialogs.MediaCapture;

namespace XDM.Wpf.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IApplicationWindow
    {
        private ObservableCollection<InProgressDownloadEntryWrapper> inProgressList
            = new ObservableCollection<InProgressDownloadEntryWrapper>();
        private ObservableCollection<FinishedDownloadEntryWrapper> finishedList
            = new ObservableCollection<FinishedDownloadEntryWrapper>();

        private IButton newButton, deleteButton, pauseButton, resumeButton, openFileButton, openFolderButton;
        private GridViewColumnHeader? finishedListViewSortCol = null;
        private SortAdorner? finishedListViewSortAdorner = null;
        private GridViewColumnHeader? inProgressListViewSortCol = null;
        private SortAdorner? inProgressListViewSortAdorner = null;
        private MessageLoop messageLoop;
        private Win32ClipboarMonitor clipboarMonitor;

        private IMenuItem[] menuItems;

        private MenuItem? completionSoundMenuItem;

        // Speed limiter quick-menu constants (presets in KB/s) ported from the GTK UI
        private static readonly int[] SpeedLimitPresets = { 256, 512, 1024, 2048, 5120, 10240 };
        private const int BytesPerKilobyte = 1024;
        private const int DefaultSpeedLimitKb = 1024;
        private const double SpeedLimiterDimmedOpacity = 0.5;
        private const double FullOpacity = 1.0;
        private const string SpeedStateSeparator = ": ";
        private const string SpeedRateSuffix = "/s";
        private const string SpeedMenuBullet = "● ";
        private const string SpeedMenuOffLabel = "Unlimited (Off)";
        private const string SpeedMenuCustomLabel = "Custom Limit...";
        private const string SpeedMenuCustomFormat = "● Custom ({0}" + SpeedRateSuffix + ")...";
        private const string ConfigChangedEvent = "ConfigChanged";

        public MainWindow()
        {
            InitializeComponent();

            newButton = new ButtonWrapper(this.BtnNew);
            deleteButton = new ButtonWrapper(this.BtnDelete);
            pauseButton = new ButtonWrapper(this.BtnPause);
            resumeButton = new ButtonWrapper(this.BtnResume);
            openFileButton = new ButtonWrapper(this.BtnOpen);
            openFolderButton = new ButtonWrapper(this.BtnOpenFolder);
            var categories = new List<CategoryWrapper>();
            categories.Add(new CategoryWrapper() { IsTopLevel = true, DisplayName = TextResource.GetText("ALL_UNFINISHED"), VectorIcon = "ri-arrow-down-line" });
            categories.Add(new CategoryWrapper() { IsTopLevel = true, DisplayName = TextResource.GetText("ALL_FINISHED"), VectorIcon = "ri-check-line" });
            categories.AddRange(Config.Instance.Categories.Select(c => new CategoryWrapper(c)
            {
                VectorIcon = IconMap.GetVectorNameForCategory(c.Name)
            }));
            lvCategory.ItemsSource = categories;

            lvInProgress.ItemsSource = inProgressList;
            lvFinished.ItemsSource = finishedList;

            lvInProgress.SelectionChanged += (sender, args) =>
            {
                this.SelectionChanged?.Invoke(sender, args);
            };

            lvFinished.SelectionChanged += (sender, args) =>
            {
                this.SelectionChanged?.Invoke(sender, args);
            };

            lvInProgress.IsVisibleChanged += (_, _) =>
            {
                if (lvInProgress.Visibility == Visibility.Visible)
                {
                    InProgressListViewInitialSortIfNotAlreadySorted();
                }
            };

            SwitchToFinishedView();
            this.Loaded += MainWindow_Loaded;
            CreateMenuItems();
            UpdateSpeedLimitButton();
            ApplicationContext.ApplicationEvent += ApplicationContext_ApplicationEvent;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FinishedListViewInitialSortIfNotAlreadySorted();
            UpdateBrowserMonitorButton();
        }

        private void InProgressListViewInitialSortIfNotAlreadySorted()
        {
            //sort in-progress list view by persisted config sort
            if (inProgressListViewSortCol == null)
            {
                ApplyPersistedSort(lvInProgress, ref inProgressListViewSortCol, ref inProgressListViewSortAdorner);
            }
            EnsureSortAdorner(inProgressListViewSortCol, inProgressListViewSortAdorner);
        }

        private void FinishedListViewInitialSortIfNotAlreadySorted()
        {
            //sort finished list view by persisted config sort
            if (finishedListViewSortCol == null)
            {
                ApplyPersistedSort(lvFinished, ref finishedListViewSortCol, ref finishedListViewSortAdorner);
            }
            EnsureSortAdorner(finishedListViewSortCol, finishedListViewSortAdorner);
        }

        // Applies the persisted config sort to a list view
        private void ApplyPersistedSort(ListView listView, ref GridViewColumnHeader? sortCol, ref SortAdorner? sortAdorner)
        {
            var property = SortPropertyForConfigColumn(Config.Instance.DownloadSortColumn);
            var direction = Config.Instance.DownloadSortDescending
                ? ListSortDirection.Descending : ListSortDirection.Ascending;
            ApplySort(listView, property, direction, ref sortCol, ref sortAdorner);
        }

        // Persists the sort selection and applies it to both download lists (GTK parity)
        private void SetDownloadSort(string property, bool descending)
        {
            Config.Instance.DownloadSortColumn = ConfigColumnForSortProperty(property);
            Config.Instance.DownloadSortDescending = descending;
            Config.SaveConfig();
            var direction = descending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            ApplySort(lvInProgress, property, direction, ref inProgressListViewSortCol, ref inProgressListViewSortAdorner);
            ApplySort(lvFinished, property, direction, ref finishedListViewSortCol, ref finishedListViewSortAdorner);
        }

        // Toggles the direction on the active column or switches columns descending-first
        private void SortByHeader(GridViewColumnHeader column)
        {
            var property = column.Tag as string;
            if (string.IsNullOrEmpty(property))
            {
                return;
            }
            var configColumn = ConfigColumnForSortProperty(property);
            var descending = Config.Instance.DownloadSortColumn == configColumn
                ? !Config.Instance.DownloadSortDescending
                : true;
            SetDownloadSort(property, descending);
        }

        // Replaces the active sort descriptions and header arrow of a list view
        private void ApplySort(ListView listView, string property, ListSortDirection direction,
            ref GridViewColumnHeader? sortCol, ref SortAdorner? sortAdorner)
        {
            if (sortCol != null && sortAdorner != null)
            {
                AdornerLayer.GetAdornerLayer(sortCol)?.Remove(sortAdorner);
            }
            listView.Items.SortDescriptions.Clear();
            listView.Items.SortDescriptions.Add(new SortDescription(property, direction));
            sortCol = FindSortHeader(listView, property);
            sortAdorner = sortCol == null ? null : new SortAdorner(sortCol, direction);
            EnsureSortAdorner(sortCol, sortAdorner);
        }

        // Attaches the sort arrow adorner once its layer is available
        private static void EnsureSortAdorner(GridViewColumnHeader? sortCol, SortAdorner? sortAdorner)
        {
            if (sortCol == null || sortAdorner == null)
            {
                return;
            }
            var layer = AdornerLayer.GetAdornerLayer(sortCol);
            if (layer == null)
            {
                return;
            }
            var existing = layer.GetAdorners(sortCol);
            if (existing == null || Array.IndexOf(existing, sortAdorner) < 0)
            {
                layer.Add(sortAdorner);
            }
        }

        // Locates the header of the column tagged with the sort property
        private static GridViewColumnHeader? FindSortHeader(ListView listView, string property)
        {
            if (listView.View is GridView view)
            {
                foreach (var column in view.Columns)
                {
                    if (column.Header is GridViewColumnHeader header && (header.Tag as string) == property)
                    {
                        return header;
                    }
                }
            }
            return null;
        }

        // Maps a wrapper sort property to its persisted config column key
        private static string ConfigColumnForSortProperty(string property)
        {
            return property == "DateAdded" ? "Date" : property;
        }

        // Maps the persisted config column key to a wrapper sort property
        private static string SortPropertyForConfigColumn(string? column)
        {
            switch (column)
            {
                case "Name":
                case "Size":
                    return column;
                default:
                    return "DateAdded";
            }
        }

        private void lvCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var index = lvCategory.SelectedIndex;
            if (index == 0)
            {
                lvInProgress.Visibility = Visibility.Visible;
                lvFinished.Visibility = Visibility.Collapsed;
                InProgressListViewInitialSortIfNotAlreadySorted();

                CategoryChanged?.Invoke(this, new CategoryChangedEventArgs { Level = 0, Index = 0 });
            }
            else if (index > 0)
            {
                lvInProgress.Visibility = Visibility.Collapsed;
                lvFinished.Visibility = Visibility.Visible;

                ListCollectionView view = (ListCollectionView)
                        CollectionViewSource.GetDefaultView(lvFinished.ItemsSource);
                if (index > 1)
                {
                    CategoryWrapper? cat = (CategoryWrapper)lvCategory.SelectedItem;
                    view.Filter = a => IsCategoryMatched((FinishedDownloadEntryWrapper)a, cat);
                    CategoryChanged?.Invoke(this, new CategoryChangedEventArgs
                    {
                        Level = 1,
                        Index = index - 2,
                        Category = cat.category
                    });
                }
                else
                {
                    view.Filter = a => IsCategoryMatched((FinishedDownloadEntryWrapper)a, null);
                    CategoryChanged?.Invoke(this, new CategoryChangedEventArgs { Level = 0, Index = 1 });
                }
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool IsCategoryMatched(FinishedDownloadEntryWrapper entry, CategoryWrapper? category)
        {
            return Helpers.IsOfCategoryOrMatchesKeyword(entry.Name, TxtSearch.Text, category?.category);
        }

        public event EventHandler<CategoryChangedEventArgs> CategoryChanged;
        public event EventHandler? InProgressContextMenuOpening;
        public event EventHandler? FinishedContextMenuOpening;
        public event EventHandler? SelectionChanged;
        public event EventHandler? NewDownloadClicked;
        public event EventHandler? YoutubeDLDownloadClicked;
        public event EventHandler? BatchDownloadClicked;
        public event EventHandler? SettingsClicked;
        public event EventHandler? ClearAllFinishedClicked;
        public event EventHandler? ExportClicked;
        public event EventHandler? ImportClicked;
        public event EventHandler? BrowserMonitoringButtonClicked;
        public event EventHandler? BrowserMonitoringSettingsClicked;
        public event EventHandler? UpdateClicked;
        public event EventHandler? HelpClicked;
        public event EventHandler? SupportPageClicked;
        public event EventHandler? BugReportClicked;
        public event EventHandler? CheckForUpdateClicked;
        public event EventHandler? SchedulerClicked;
        public event EventHandler? DownloadListDoubleClicked;
        public event EventHandler? ClipboardChanged;
        public event EventHandler? WindowCreated;

        public IEnumerable<FinishedDownloadItem> FinishedDownloads
        {
            get => this.finishedList.Select(x => x.DownloadEntry);
            set
            {
                this.finishedList = new ObservableCollection<FinishedDownloadEntryWrapper>(
                    value.Select(x => new FinishedDownloadEntryWrapper(x)));
                this.lvFinished.ItemsSource = finishedList;
                FinishedListViewInitialSortIfNotAlreadySorted();
            }
        }

        public IEnumerable<InProgressDownloadItem> InProgressDownloads
        {
            get => this.inProgressList.Select(x => x.DownloadEntry);
            set
            {
                this.inProgressList = new ObservableCollection<InProgressDownloadEntryWrapper>(
                    value.Select(x => new InProgressDownloadEntryWrapper(x)));
                this.lvInProgress.ItemsSource = inProgressList;
                InProgressListViewInitialSortIfNotAlreadySorted();
            }
        }

        public IList<IInProgressDownloadRow> SelectedInProgressRows =>
            this.lvInProgress.SelectedItems.OfType<IInProgressDownloadRow>().ToList();

        public IList<IFinishedDownloadRow> SelectedFinishedRows =>
            this.lvFinished.SelectedItems.OfType<IFinishedDownloadRow>().ToList();

        public IButton NewButton => newButton;

        public IButton DeleteButton => deleteButton;

        public IButton PauseButton => pauseButton;

        public IButton ResumeButton => resumeButton;

        public IButton OpenFileButton => openFileButton;

        public IButton OpenFolderButton => openFolderButton;

        public bool IsInProgressViewSelected => lvCategory.SelectedIndex == 0;

        public IMenuItem[] MenuItems => this.menuItems;

        public Dictionary<string, IMenuItem> MenuItemMap { get; private set; }

        public IInProgressDownloadRow FindInProgressItem(string id) =>
            this.lvInProgress.Items.OfType<IInProgressDownloadRow>()
            .Where(x => x.DownloadEntry.Id == id).FirstOrDefault();

        public IFinishedDownloadRow FindFinishedItem(string id) =>
            this.lvFinished.Items.OfType<IFinishedDownloadRow>()
            .Where(x => x.DownloadEntry.Id == id).FirstOrDefault();

        public void AddToTop(InProgressDownloadItem entry)
        {
            this.inProgressList.Add(new InProgressDownloadEntryWrapper(entry));
        }

        public void AddToTop(FinishedDownloadItem entry)
        {
            this.finishedList.Add(new FinishedDownloadEntryWrapper(entry));
        }

        public void SwitchToInProgressView()
        {
            lvCategory.SelectedIndex = 0;
        }

        public void ClearInProgressViewSelection()
        {
            lvInProgress.UnselectAll();
        }

        public void SwitchToFinishedView()
        {
            lvCategory.SelectedIndex = 1;
        }

        public void ClearFinishedViewSelection()
        {
            lvFinished.UnselectAll();
        }

        public bool Confirm(object? window, string text)
        {
            return MessageBox.Show((Window)(window ?? this), text, "FetchFlow", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        }

        public void ConfirmDelete(string text, out bool approved, out bool deleteFiles)
        {
            DeleteConfirmDialog dc = new() { DescriptionText = text, Owner = this };
            approved = false;
            deleteFiles = false;
            bool? ret = dc.ShowDialog(this);
            if (ret.HasValue && ret.Value)
            {
                approved = true;
                deleteFiles = dc.ShouldDeleteFile;
            }
        }

        public void RunOnUIThread(Action action)
        {
            Dispatcher.BeginInvoke(action);
        }

        public void RunOnUIThread(Action<string, int, double, long> action, string id, int progress, double speed, long eta)
        {
            Dispatcher.BeginInvoke(action, id, progress, speed, eta);
        }

        public void Delete(IInProgressDownloadRow row)
        {
            this.inProgressList.Remove((InProgressDownloadEntryWrapper)row);
        }

        public void Delete(IFinishedDownloadRow row)
        {
            this.finishedList.Remove((FinishedDownloadEntryWrapper)row);
        }

        public void DeleteAllFinishedDownloads()
        {
            if (MessageBox.Show(this, TextResource.GetText("MENU_DELETE_COMPLETED"), "FetchFlow", MessageBoxButton.YesNo)
                != MessageBoxResult.Yes)
            {
                return;
            }
            finishedList.Clear();
        }

        public void Delete(IEnumerable<IInProgressDownloadRow> rows)
        {
            foreach (var row in rows)
            {
                inProgressList.Remove((InProgressDownloadEntryWrapper)row);
            }
        }

        public void Delete(IEnumerable<IFinishedDownloadRow> rows)
        {
            foreach (var row in rows)
            {
                finishedList.Remove((FinishedDownloadEntryWrapper)row);
            }
        }

        public string? GetUrlFromClipboard()
        {
            return Clipboard.GetText();
        }

        public void ShowUpdateAvailableNotification()
        {
            RunOnUIThread(() =>
            {
                PathHelp.Data = (Geometry)FindResource("ri-notification-3-fill");
                PathHelp.Fill = (SolidColorBrush)FindResource("color-update-avaliable");
                TxtHelp.Text = TextResource.GetText("MSG_UPDATE_AVAILABLE");
                BtnHelp.Tag = new object();
            });
        }

        public void ClearUpdateInformation()
        {
            RunOnUIThread(() =>
            {
                PathHelp.Data = (Geometry)FindResource("ri-question-line");
                TxtHelp.Text = TextResource.GetText("LBL_SUPPORT_PAGE");
                PathHelp.Fill = (SolidColorBrush)FindResource("StatusbarIconcolor");
                BtnHelp.Tag = null;
            });
        }

        public void OpenNewDownloadMenu()
        {
            var nctx = (ContextMenu)FindResource("newDownloadContextMenu");
            nctx.PlacementTarget = BtnNew;
            nctx.Placement = PlacementMode.Bottom;
            nctx.IsOpen = true;
        }

        public void SetClipboardText(string text)
        {
            Clipboard.SetText(text);
        }

        public void SetClipboardFile(string file)
        {
            var sc = new StringCollection();
            sc.Add(file);
            Clipboard.SetFileDropList(sc);
        }

        public void UpdateBrowserMonitorButton()
        {
            this.MonitoringToggleIcon.Data = (Geometry)FindResource(Config.Instance.IsBrowserMonitoringEnabled ?
                "ri-toggle-fill" : "ri-toggle-line");
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);

#if NET45_OR_GREATER
            if (App.Skin == Skin.Dark)
            {
                helper.EnsureHandle();
                DarkModeHelper.UseImmersiveDarkMode(helper.Handle, true);
            }
#endif
            clipboarMonitor = new Win32ClipboarMonitor(helper.Handle);
            clipboarMonitor.ClipboardChanged += (sender, args) => this.ClipboardChanged?.Invoke(this, EventArgs.Empty);
            this.messageLoop = new MessageLoop(clipboarMonitor);
            messageLoop.Start(helper.Handle);
            WindowCreated?.Invoke(this, EventArgs.Empty);
        }


        private void lvFinished_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader column)
            {
                SortByHeader(column);
            }
        }

        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            if (completionSoundMenuItem != null)
            {
                completionSoundMenuItem.IsChecked = Config.Instance.PlayCompletionSound;
            }
            var nctx = (ContextMenu)FindResource("ctxMainMenu");
            if (nctx.Placement != PlacementMode.Bottom || nctx.PlacementTarget != BtnMenu)
            {
                nctx.Placement = PlacementMode.Bottom;
                nctx.PlacementTarget = BtnMenu;
            }
            nctx.IsOpen = true;
        }

        private void ctxMainMenu_LayoutUpdated(object sender, EventArgs e)
        {
            var ctx = (ContextMenu)FindResource("ctxMainMenu");
            if (ctx.HorizontalOffset != 0) return;
            ctx.HorizontalOffset = BtnMenu.ActualWidth - ctx.ActualWidth;
        }

        private void menuExit_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void menuLanguage_Click(object sender, RoutedEventArgs e)
        {
            var langDlg = new LanguageSettingsWindow
            {
                Owner = this
            };
            langDlg.ShowDialog(this);
        }

        private void BtnQueue_Click(object sender, RoutedEventArgs e)
        {
            this.SchedulerClicked?.Invoke(sender, e);
        }

        private void menuSettings_Click(object sender, RoutedEventArgs e)
        {
            this.SettingsClicked?.Invoke(this, e);
        }

        private void BtnMonitoring_Click(object sender, RoutedEventArgs e)
        {
            BrowserMonitoringButtonClicked?.Invoke(sender, e);
        }

        private void menuClearFinished_Click(object sender, RoutedEventArgs e)
        {
            this.ClearAllFinishedClicked?.Invoke(sender, e);
        }

        private void menuBrowserMonitor_Click(object sender, RoutedEventArgs e)
        {
            BrowserMonitoringSettingsClicked?.Invoke(sender, e);
        }

        private void menuImport_Click(object sender, RoutedEventArgs e)
        {
            OpenImportExportChooser();
        }

        private void menuExport_Click(object sender, RoutedEventArgs e)
        {
            OpenImportExportChooser();
        }

        // Shows the import/export chooser and forwards its requests downstream (GTK parity)
        private void OpenImportExportChooser()
        {
            var chooser = new ImportExportWindow(this);
            chooser.ExportRequested += (_, _) => ExportClicked?.Invoke(this, EventArgs.Empty);
            chooser.ImportRequested += (_, _) => ImportClicked?.Invoke(this, EventArgs.Empty);
            chooser.ShowDialog(this);
        }

        private void menuHelpAndSupport_Click(object sender, RoutedEventArgs e)
        {
            SupportPageClicked?.Invoke(sender, e);
        }

        private void menuReportProblem_Click(object sender, RoutedEventArgs e)
        {
            BugReportClicked?.Invoke(sender, e);
        }

        private void menuCheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdateClicked?.Invoke(sender, e);
        }

        private void menuAbout_Click(object sender, RoutedEventArgs e)
        {
            var win = new AboutWindow
            {
                Owner = this
            };
            win.ShowDialog(this);
        }

        private void lvInProgress_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader column)
            {
                SortByHeader(column);
            }
        }

        private void CreateMenuItems()
        {
            menuItems = new IMenuItem[]
            {
                new MenuItemWrapper("pause",TextResource.GetText("MENU_PAUSE")),
                new MenuItemWrapper("resume",TextResource.GetText("MENU_RESUME")),
                new MenuItemWrapper("delete",TextResource.GetText("DESC_DEL")),
                new MenuItemWrapper("saveAs",TextResource.GetText("CTX_SAVE_AS")),
                new MenuItemWrapper("refresh",TextResource.GetText("MENU_REFRESH_LINK")),
                new MenuItemWrapper("showProgress",TextResource.GetText("LBL_SHOW_PROGRESS")),
                new MenuItemWrapper("copyURL",TextResource.GetText("CTX_COPY_URL")),
                new MenuItemWrapper("restart",TextResource.GetText("MENU_RESTART")),
                new MenuItemWrapper("moveToQueue",TextResource.GetText("Q_MOVE_TO")),
                new MenuItemWrapper("properties",TextResource.GetText("MENU_PROPERTIES")),

                new MenuItemWrapper("open",TextResource.GetText("CTX_OPEN_FILE")),
                new MenuItemWrapper("openFolder",TextResource.GetText("CTX_OPEN_FOLDER")),
                new MenuItemWrapper("verifyChecksum",TextResource.GetText("CTX_CHECKSUM") ?? "Verify Checksum"),
                new MenuItemWrapper("deleteDownloads",TextResource.GetText("MENU_DELETE_DWN")),
                new MenuItemWrapper("copyURL1",TextResource.GetText("CTX_COPY_URL")),
                new MenuItemWrapper("copyFile",TextResource.GetText("CTX_COPY_FILE")),
                new MenuItemWrapper("downloadAgain",TextResource.GetText("MENU_RESTART")),
                new MenuItemWrapper("properties1",TextResource.GetText("MENU_PROPERTIES")),
                new MenuItemWrapper("schedule",TextResource.GetText("Q_SCHEDULE_TXT"),false)
            };

            var dict = new Dictionary<string, IMenuItem>();
            foreach (var mi in menuItems)
            {
                dict[mi.Name] = mi;
            }

            this.MenuItemMap = dict;

            var lvInProgressContextMenu = (ContextMenu)this.FindResource("lvInProgressContextMenu");
            var lvFinishedContextMenu = (ContextMenu)this.FindResource("lvFinishedContextMenu");
            var i = 0;
            foreach (MenuItemWrapper mi in menuItems)
            {
                if (i < 10)
                {
                    lvInProgressContextMenu.Items.Add(mi.Menu);
                }
                else
                {
                    lvFinishedContextMenu.Items.Add(mi.Menu);
                }
                i++;
            }
            lvInProgress.ContextMenuOpening += LvInProgressContextMenu_ContextMenuOpening;
            lvFinished.ContextMenuOpening += LvFinishedContextMenu_ContextMenuOpening;

            var newDownloadMenu = (ContextMenu)FindResource("newDownloadContextMenu");

            var menuNewDownload = (MenuItem)newDownloadMenu.Items[0];
            menuNewDownload.Click += MenuNewDownload_Click;
            menuNewDownload.Header = TextResource.GetText("LBL_NEW_DOWNLOAD");

            var menuVideoDownload = (MenuItem)newDownloadMenu.Items[1];
            menuVideoDownload.Click += MenuVideoDownload_Click;
            menuVideoDownload.Header = TextResource.GetText("LBL_VIDEO_DOWNLOAD");

            var menuBatchDownload = (MenuItem)newDownloadMenu.Items[2];
            menuBatchDownload.Click += MenuBatchDownload_Click;
            menuBatchDownload.Header = TextResource.GetText("MENU_BATCH_DOWNLOAD");

            completionSoundMenuItem = ((ContextMenu)FindResource("ctxMainMenu")).Items
                .OfType<MenuItem>().FirstOrDefault(m => m.Name == "menuCompletionSound");
            if (completionSoundMenuItem != null)
            {
                completionSoundMenuItem.Header = TextResource.GetText("MSG_PLAY_SOUND") ?? "Play sound when download finishes";
                completionSoundMenuItem.IsChecked = Config.Instance.PlayCompletionSound;
            }
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            if (BtnHelp.Tag != null)
            {
                UpdateClicked?.Invoke(sender, e);
            }
            else
            {
                HelpClicked?.Invoke(sender, e);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void ListViewItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DownloadListDoubleClicked?.Invoke(sender, e);
        }

        private void MenuNewDownload_Click(object sender, RoutedEventArgs e)
        {
            this.NewDownloadClicked?.Invoke(sender, e);
        }

        private void MenuVideoDownload_Click(object sender, RoutedEventArgs e)
        {
            this.YoutubeDLDownloadClicked?.Invoke(sender, e);
        }

        private void menuMediaGrabber_Click(object sender, RoutedEventArgs e)
        {
            ApplicationContext.PlatformUIService.CreateAndShowMediaGrabber();
        }

        //private void extRegister_Click(object sender, RoutedEventArgs e)
        //{
        //    ApplicationContext.PlatformUIService.ShowExtensionRegistrationWindow();
        //}

        private void MenuBatchDownload_Click(object sender, RoutedEventArgs e)
        {
            this.BatchDownloadClicked?.Invoke(sender, e);
        }

        private void LvFinishedContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            this.FinishedContextMenuOpening?.Invoke(sender, e);
        }

        private void LvInProgressContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            this.InProgressContextMenuOpening?.Invoke(sender, e);
        }

        // Persists the completion sound preference from the checkable menu item (GTK parity)
        private void menuCompletionSound_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                Config.Instance.PlayCompletionSound = item.IsChecked;
                Config.SaveConfig();
                ApplicationContext.BroadcastConfigChange();
            }
        }

        // Handles application-wide broadcast events to refresh config-driven UI
        private void ApplicationContext_ApplicationEvent(object? sender, ApplicationEvent e)
        {
            if (e.EventType == ConfigChangedEvent)
            {
                RunOnUIThread(RefreshConfigDrivenUi);
            }
        }

        // Refreshes UI elements that mirror persisted config values
        private void RefreshConfigDrivenUi()
        {
            if (completionSoundMenuItem != null)
            {
                completionSoundMenuItem.IsChecked = Config.Instance.PlayCompletionSound;
            }
            UpdateSpeedLimitButton();
        }

        // Toggles global bandwidth throttling on or off (GTK parity)
        private void BtnSpeedLimit_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.EnableSpeedLimit = !Config.Instance.EnableSpeedLimit;
            if (Config.Instance.EnableSpeedLimit && Config.Instance.DefaltDownloadSpeed <= 0)
            {
                Config.Instance.DefaltDownloadSpeed = DefaultSpeedLimitKb;
            }
            Config.SaveConfig();
            ApplicationContext.BroadcastConfigChange();
            UpdateSpeedLimitButton();
        }

        // Opens the quick speed limit preset menu on right click (GTK parity)
        private void BtnSpeedLimit_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ShowSpeedLimiterMenu();
            e.Handled = true;
        }

        // Displays context menu with quick speed limit presets
        private void ShowSpeedLimiterMenu()
        {
            var menu = new ContextMenu();
            var title = new MenuItem { Header = TextResource.GetText("MSG_SPEED_LIMIT"), IsEnabled = false };
            menu.Items.Add(title);
            menu.Items.Add(new Separator());

            var currentLimit = Config.Instance.EnableSpeedLimit ? Config.Instance.DefaltDownloadSpeed : 0;

            var offHeader = currentLimit == 0 ? SpeedMenuBullet + SpeedMenuOffLabel : SpeedMenuOffLabel;
            var offItem = new MenuItem { Header = offHeader };
            offItem.Click += (_, _) => SetSpeedLimit(0);
            menu.Items.Add(offItem);
            menu.Items.Add(new Separator());

            foreach (var preset in SpeedLimitPresets)
            {
                var label = FormattingHelper.FormatSize(preset * (double)BytesPerKilobyte) + SpeedRateSuffix;
                var isSelected = Config.Instance.EnableSpeedLimit && Config.Instance.DefaltDownloadSpeed == preset;
                var item = new MenuItem { Header = isSelected ? SpeedMenuBullet + label : label };
                item.Click += (_, _) => SetSpeedLimit(preset);
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator());

            var isCustom = Config.Instance.EnableSpeedLimit
                && Array.IndexOf(SpeedLimitPresets, Config.Instance.DefaltDownloadSpeed) < 0
                && Config.Instance.DefaltDownloadSpeed > 0;
            var customHeader = isCustom
                ? string.Format(SpeedMenuCustomFormat, FormattingHelper.FormatSize(Config.Instance.DefaltDownloadSpeed * (double)BytesPerKilobyte))
                : SpeedMenuCustomLabel;
            var customItem = new MenuItem { Header = customHeader };
            customItem.Click += (_, _) => ApplicationContext.PlatformUIService.ShowSpeedLimiterWindow();
            menu.Items.Add(customItem);

            menu.PlacementTarget = BtnSpeedLimit;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        // Applies a speed limit preset in KB/s (0 disables the limiter)
        private void SetSpeedLimit(int kilobytesPerSecond)
        {
            Config.Instance.EnableSpeedLimit = kilobytesPerSecond > 0;
            if (kilobytesPerSecond > 0)
            {
                Config.Instance.DefaltDownloadSpeed = kilobytesPerSecond;
            }
            Config.SaveConfig();
            ApplicationContext.BroadcastConfigChange();
            UpdateSpeedLimitButton();
        }

        // Refreshes the speed limiter button visual state and tooltip
        private void UpdateSpeedLimitButton()
        {
            if (BtnSpeedLimit == null)
            {
                return;
            }
            var enabled = Config.Instance.EnableSpeedLimit;
            var limit = Config.Instance.DefaltDownloadSpeed;
            SpeedLimitIcon.Opacity = enabled ? FullOpacity : SpeedLimiterDimmedOpacity;
            BtnSpeedLimit.ToolTip = enabled && limit > 0
                ? TextResource.GetText("MSG_SPEED_LIMIT") + SpeedStateSeparator + FormattingHelper.FormatSize(limit * (double)BytesPerKilobyte) + SpeedRateSuffix
                : TextResource.GetText("MSG_SPEED_LIMIT");
        }

        // Keeps multi-selection when right-clicking a selected row (GTK parity)
        private void DownloadList_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!(sender is ListView listView))
            {
                return;
            }
            var item = FindAncestorListItem(e.OriginalSource as DependencyObject);
            if (item != null && !item.IsSelected)
            {
                listView.UnselectAll();
                item.IsSelected = true;
            }
        }

        // Walks the visual tree up to the containing list item
        private static ListViewItem? FindAncestorListItem(DependencyObject? source)
        {
            while (source != null && !(source is ListViewItem))
            {
                source = VisualTreeHelper.GetParent(source);
            }
            return source as ListViewItem;
        }

        public IPlatformClipboardMonitor GetClipboardMonitor() => this.clipboarMonitor;

        public void ShowAndActivate()
        {
            this.Show();
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }
            this.Activate();
        }
    }
}
