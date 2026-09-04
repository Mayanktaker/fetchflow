// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Gtk;
using Application = Gtk.Application;
using IoPath = System.IO.Path;
using XDM.Core;
using XDM.Core.Util;
using XDM.Core.UI;
using Translations;
using Menu = Gtk.Menu;
using MenuItem = Gtk.MenuItem;
using XDM.GtkUI.Utils;
using XDM.GtkUI.Dialogs.DeleteConfirm;
using XDM.GtkUI.Dialogs.Language;
using TraceLog;
using XDM.Core.BrowserMonitoring;


namespace XDM.GtkUI
{
    public class MainWindow : Window, IApplicationWindow
    {
        private TreeStore statusTreeStore, categoryTreeStore;
        private TreeView statusTree, categoryTree;
        private bool isSelectingSidebar = false;
        private ListStore inprogressDownloadsStore, finishedDownloadsStore;
        private TreeView lvInprogress, lvFinished;
        private ScrolledWindow swInProgress, swFinished;
        private TreeModelFilter finishedDownloadFilter;
        private TreeModelFilter inprogressDownloadFilter;
        private TreeModelSort inprogressDownloadsStoreSorted;
        private TreeModelSort finishedDownloadsStoreSorted;
        private string? searchKeyword;
        private Category? category;
        private Button btnNew, btnDel, btnOpenFile, btnOpenFolder, btnResume, btnPause, btnMenu, btnScheduler, btnSpeedLimit;
        private IButton newButton, deleteButton, pauseButton, resumeButton, openFileButton, openFolderButton;
        private IMenuItem[] menuItems;
        private Menu newDownloadMenu;
        private Menu mainMenu;
        private CheckMenuItem? menuCompletionSound;
        private WindowGroup windowGroup;
        private CheckButton btnMonitoring;
        private Label lblExtensionStatus;
        private Label? lblTotalSpeed;
        private readonly Dictionary<string, double> activeSpeeds = new();
        private readonly Dictionary<string, long> activeEtas = new();
        private bool isUpdateAvailable;
        private Image helpImage;
        private Label helpLabel;
        private TrayIconManager trayManager;
        private Label subtitleLabel;
        private Button updateDot;
        private Entry? searchEntry;

        // Dark-tinted sidebar icons for selected rows, keyed by source pixbuf (tint once, reuse)
        private static readonly ConditionalWeakTable<Gdk.Pixbuf, Gdk.Pixbuf> selectedSidebarIcons = new();

        internal WindowGroup GetWindowGroup() => this.windowGroup;

        public IEnumerable<FinishedDownloadItem> FinishedDownloads
        {
            get => GetAllFinishedDownloads();
            set => SetFinishedDownloads(value);
        }

        public IEnumerable<InProgressDownloadItem> InProgressDownloads
        {
            get => GetAllInProgressDownloads();
            set => SetInProgressDownloads(value);
        }

        public IList<IInProgressDownloadRow> SelectedInProgressRows => GetSelectedInProgressDownloads();

        public IList<IFinishedDownloadRow> SelectedFinishedRows => GetSelectedFinishedDownloads();

        public IButton NewButton => this.newButton;

        public IButton DeleteButton => this.deleteButton;

        public IButton PauseButton => this.pauseButton;

        public IButton ResumeButton => this.resumeButton;

        public IButton OpenFileButton => this.openFileButton;

        public IButton OpenFolderButton => this.openFolderButton;

        public bool IsInProgressViewSelected => GetSelectedCategory() == 0;

        public IMenuItem[] MenuItems => this.menuItems;

        public Dictionary<string, IMenuItem> MenuItemMap { get; private set; }

        public event EventHandler ClipboardChanged;
        public event EventHandler InProgressContextMenuOpening;
        public event EventHandler FinishedContextMenuOpening;
        public event EventHandler SelectionChanged;
        public event EventHandler NewDownloadClicked;
        public event EventHandler YoutubeDLDownloadClicked;
        public event EventHandler BatchDownloadClicked;
        public event EventHandler SettingsClicked;
        public event EventHandler ClearAllFinishedClicked;
        public event EventHandler ExportClicked;
        public event EventHandler ImportClicked;
        public event EventHandler BrowserMonitoringButtonClicked;
        public event EventHandler BrowserMonitoringSettingsClicked;
        public event EventHandler UpdateClicked;
        public event EventHandler HelpClicked;
        public event EventHandler SupportPageClicked;
        public event EventHandler BugReportClicked;
        public event EventHandler CheckForUpdateClicked;
        public event EventHandler<CategoryChangedEventArgs> CategoryChanged;
        public event EventHandler SchedulerClicked;
        public event EventHandler DownloadListDoubleClicked;
        public event EventHandler WindowCreated;

        private const int FINISHED_DATA_INDEX = 3;
        private const int INPROGRESS_DATA_INDEX = 5;

        // Headerbar title tokens: bold app name + "· <view>" subtitle tracking the sidebar
        private const string HeaderAppName = "FetchFlow";
        private const string HeaderSubtitleSeparator = "· ";
        // Update-available indicator glyph shown in the headerbar title group
        private const string UpdateDotGlyph = "●";
        // Selected sidebar row icon tint (#161616) — reads on the blue accent in both themes
        private const byte SidebarSelectedIconTint = 0x16;
        // Dynamic active theme accent RGB components
        private static byte AccentR => ThemeManager.ActiveAccentColor.R;
        private static byte AccentG => ThemeManager.ActiveAccentColor.G;
        private static byte AccentB => ThemeManager.ActiveAccentColor.B;
        // Destructive red — delete/remove actions  
        private const byte DestructR = 239, DestructG = 68, DestructB = 68;  // #ef4444
        // Success green — completed items, active status
        private const byte SuccessR = 46, SuccessG = 194, SuccessB = 126;   // #2ec27e
        // Warning amber — folder icons
        private const byte AmberR = 245, AmberG = 158, AmberB = 11;         // #f59e0b
        // Info cyan — scheduler icon
        private const byte CyanR = 6, CyanG = 182, CyanB = 212;            // #06b6d4
        // Dim gray — settings/help icons
        private const byte DimR = 148, DimG = 163, DimB = 184;             // #94a3b8
        // Purple — music category icon
        private const byte PurpleR = 168, PurpleG = 85, PurpleB = 247;     // #a855f7
        // Teal — compressed category icon
        private const byte TealR = 20, TealG = 184, TealB = 166;           // #14b8a6
        // Indigo — programs category icon
        private const byte IndigoR = 99, IndigoG = 102, IndigoB = 241;     // #6366f1
        // Rose / Pink — images category icon
        private const byte RoseR = 244, RoseG = 63, RoseB = 94;          // #f43f5e
        // Sky blue — default/other file category
        private const byte SkyR = 56, SkyG = 189, SkyB = 248;             // #38bdf8

        // Button content geometry: labeled buttons keep spacing + symmetric side margins
        private const int ButtonContentSpacing = 10;
        private const int ButtonBoxMargin = 2;
        private const int DownloadColumnSpacing = 0;
        private const int DownloadIconHorizontalPadding = 12;
        private const int DownloadNameHorizontalPadding = 12;
        private const int DownloadMetaHorizontalPadding = 16;
        private const int DownloadMetaActiveWidth = 196;
        private const int DownloadMetaFinishedWidth = 176;

        private Menu menuInProgress, menuFinished;
        private IPlatformClipboardMonitor clipboarMonitor;
        private TreePath? hoveredInprogressPath;
        private TreePath? hoveredFinishedPath;

        public MainWindow() : base("FetchFlow Download Manager")
        {
            // Set window app icon and multi-resolution icon list
            try
            {
                GtkHelper.SetWindowAppIcon(this);
            }
            catch (Exception) { /* Icon is non-critical; continue without it */ }
            // Wayland: compositor places windows; client-side centering is a no-op (Phase1.4)
            DeleteEvent += AppWin1_DeleteEvent;
            this.windowGroup = new WindowGroup();
            this.windowGroup.AddWindow(this);

            var hbMain = new HBox();
            hbMain.PackStart(CreateCategoryTree(), false, true, 0);
            hbMain.PackStart(CreateMainPanel(), true, true, 0);
            Add(hbMain);
            hbMain.Show();
            // CSD headerbar (Wayland-safe): left-aligned title + its own close button only
            Titlebar = CreateHeaderBar();

            // Sidebar default: row 0 "All Unfinished" in statusTree
            if (statusTreeStore!.GetIterFirst(out TreeIter iter))
            {
                statusTree!.Selection.SelectIter(iter);
            }
            UpdateBrowserMonitorButton();
            CreateMenu();
            SetDefaultSize(960, 520);

            clipboarMonitor = new PollingClipboardMonitor();
            clipboarMonitor.ClipboardChanged += (_, _) => this.ClipboardChanged?.Invoke(this, EventArgs.Empty);

            // Tray: register a tray icon via the desktop's preferred protocol (SNI on KDE/GNOME/Wayland)
            trayManager = new TrayIconManager();
            trayManager.Init(GtkHelper.LoadSvg("fetchflow-logo", 22), "FetchFlow Download Manager",
                             ShowAndActivate, QuitFromTray);

            ApplicationContext.ApplicationEvent += ApplicationContext_ApplicationEvent;
            
            _ = CheckUpdatesInBackgroundAsync();
        }

        // Fire-and-forget update check. Returns a Task so exceptions are observed; callers
        // must discard with _ = ... (CS4014) rather than async void, which leaks faults onto
        // the GTK synchronization context and becomes a native abort after a delay.
        private Task CheckUpdatesInBackgroundAsync()
        {
            return Task.Run(async () =>
            {
                string? newVersion = null;
                try
                {
                    newVersion = await UpdateChecker.CheckForUpdateAsync();
                }
                catch (Exception netEx)
                {
                    Log.Debug(netEx, "Error checking updates on startup: " + netEx.Message);
                    return;
                }

                if (newVersion == null) return;

                Application.Invoke(delegate
                {
                    try
                    {
                        if (this.GdkWindow == null || this.IsDestroyed()) return;
                        using var dialog = new MessageDialog(this, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo,
                            $"A new version (v{newVersion}) of FetchFlow is available! Would you like to update now?");
                        dialog.Title = "Update Available";
                        ResponseType response = (ResponseType)dialog.Run();

                        if (response == ResponseType.Yes)
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
                            catch (Exception termEx)
                            {
                                Log.Debug(termEx, "Could not start updater terminal: " + termEx.Message);
                            }
                        }
                    }
                    catch (Exception dlgEx)
                    {
                        Log.Debug(dlgEx, "Update dialog: " + dlgEx.Message);
                    }
                });
            });
        }

        // GTK# extension: Window.IsDestroyed is not public; GdkWindow null implies destroyed.
        private bool IsDestroyed()
        {
            try { return this.Handle == IntPtr.Zero; } catch { return true; }
        }

        private void CreateMenu()
        {
            menuItems = new IMenuItem[]
            {
                new MenuItemWrapper("pause", TextResource.GetText("MENU_PAUSE"), true, "pause-line"),
                new MenuItemWrapper("resume", TextResource.GetText("MENU_RESUME"), true, "play-line"),
                new MenuItemWrapper("delete", TextResource.GetText("DESC_DEL"), true, "delete-bin-7-line"),
                new MenuItemWrapper("saveAs", TextResource.GetText("CTX_SAVE_AS"), true, "download-2-line"),
                new MenuItemWrapper("refresh", TextResource.GetText("MENU_REFRESH_LINK"), true, "refresh-line"),
                new MenuItemWrapper("showProgress", TextResource.GetText("LBL_SHOW_PROGRESS"), true, "external-link-line"),
                new MenuItemWrapper("copyURL", TextResource.GetText("CTX_COPY_URL"), true, "links-line"),
                new MenuItemWrapper("restart", TextResource.GetText("MENU_RESTART"), true, "refresh-line"),
                new MenuItemWrapper("moveToQueue", TextResource.GetText("Q_MOVE_TO"), true, "folder-shared-line"),
                new MenuItemWrapper("properties", TextResource.GetText("MENU_PROPERTIES"), true, "settings-3-line"),

                new MenuItemWrapper("open", TextResource.GetText("CTX_OPEN_FILE"), true, "file-line"),
                new MenuItemWrapper("openFolder", TextResource.GetText("CTX_OPEN_FOLDER"), true, "folder-shared-line"),
                new MenuItemWrapper("verifyChecksum", TextResource.GetText("CTX_CHECKSUM") ?? "Verify Checksum", true, "check-line"),
                new MenuItemWrapper("deleteDownloads", TextResource.GetText("MENU_DELETE_DWN"), true, "delete-bin-7-line"),
                new MenuItemWrapper("copyURL1", TextResource.GetText("CTX_COPY_URL"), true, "links-line"),
                new MenuItemWrapper("copyFile", TextResource.GetText("CTX_COPY_FILE"), true, "file-copy-line"),
                new MenuItemWrapper("downloadAgain", TextResource.GetText("MENU_RESTART"), true, "refresh-line"),
                new MenuItemWrapper("properties1", TextResource.GetText("MENU_PROPERTIES"), true, "settings-3-line"),
                new MenuItemWrapper("schedule", TextResource.GetText("Q_SCHEDULE_TXT"), false, "time-line")
            };

            var dict = new Dictionary<string, IMenuItem>();
            foreach (var mi in menuItems)
            {
                dict[mi.Name] = mi;
            }

            this.MenuItemMap = dict;

            menuFinished = new Menu();
            menuFinished.Append(((MenuItemWrapper)dict["open"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["openFolder"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["verifyChecksum"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["deleteDownloads"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["downloadAgain"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["copyURL1"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["copyFile"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["properties1"]).MenuItem);
            menuFinished.ShowAll();

            menuInProgress = new Menu();
            menuInProgress.Append(((MenuItemWrapper)dict["pause"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["resume"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["delete"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["saveAs"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["refresh"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["restart"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["schedule"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["showProgress"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["copyURL"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["moveToQueue"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["properties"]).MenuItem);
            menuInProgress.ShowAll();

            newDownloadMenu = new Menu();
            var menuNewDownload = CreateIconMenuItem("file-download-line", TextResource.GetText("LBL_NEW_DOWNLOAD") ?? "New Download", out var imgNewDl, out _);
            menuNewDownload.Activated += MenuNewDownload_Click;
            var menuVideoDownload = CreateIconMenuItem("movie-line", TextResource.GetText("LBL_VIDEO_DOWNLOAD") ?? "Video Download", out var imgVideoDl, out _);
            menuVideoDownload.Activated += MenuVideoDownload_Click;
            var menuBatchDownload = CreateIconMenuItem("list-settings-line", TextResource.GetText("MENU_BATCH_DOWNLOAD") ?? "Batch Download", out var imgBatchDl, out _);
            menuBatchDownload.Activated += MenuBatchDownload_Click;
            newDownloadMenu.Append(menuNewDownload);
            newDownloadMenu.Append(menuVideoDownload);
            newDownloadMenu.Append(menuBatchDownload);
            newDownloadMenu.ShowAll();

            mainMenu = new Menu();
            var menuSettings = CreateIconMenuItem("settings-3-line", TextResource.GetText("TITLE_SETTINGS") ?? "Settings", out var imgSettings, out _);
            var menuMediaGrabber = CreateIconMenuItem("movie-line", TextResource.GetText("MSG_MEDIA_CAPTURE") ?? "Media Grabber", out var imgMediaGrabber, out _);
            var menuClearFinished = CreateIconMenuItem("delete-bin-7-line", TextResource.GetText("MENU_DELETE_COMPLETED") ?? "Remove Finished Downloads", out var imgClearFinished, out _);
            var menuImportExport = CreateIconMenuItem("folder-shared-line", TextResource.GetText("MENU_IMPORT_EXPORT") ?? "Import / Export", out var imgImportExport, out _);
            var menuLanguage = CreateIconMenuItem("global-line", TextResource.GetText("MENU_LANG") ?? "Language", out var imgLanguage, out _);
            var menuHelpAndSupport = CreateIconMenuItem("question-line", TextResource.GetText("LBL_SUPPORT_PAGE") ?? "Help And Support", out var imgHelp, out _);
            var menuReportProblem = CreateIconMenuItem("feedback-line", TextResource.GetText("LBL_REPORT_PROBLEM") ?? "Report A Problem", out var imgReport, out _);
            var menuCheckForUpdate = CreateIconMenuItem("refresh-line", TextResource.GetText("MENU_UPDATE") ?? "Check For Update", out var imgUpdate, out _);
            var menuAbout = CreateIconMenuItem("fetchflow-mark", TextResource.GetText("MENU_ABOUT") ?? "About FetchFlow", out var imgAbout, out _);
            var menuExit = CreateIconMenuItem("logout-box-r-line", TextResource.GetText("MENU_EXIT") ?? "Exit", out var imgExit, out _);

            void RefreshMenuIcons(bool isDark)
            {
                void UpdateIcon(Image img, string name)
                {
                    var raw = LoadSvg(name, 16);
                    if (raw != null)
                    {
                        img.Pixbuf = isDark ? GtkHelper.TintPixbuf(raw, 200, 200, 200) : GtkHelper.TintPixbuf(raw, 90, 90, 90);
                    }
                }
                UpdateIcon(imgSettings, "settings-3-line");
                UpdateIcon(imgMediaGrabber, "movie-line");
                UpdateIcon(imgClearFinished, "delete-bin-7-line");
                UpdateIcon(imgImportExport, "folder-shared-line");
                UpdateIcon(imgLanguage, "global-line");
                UpdateIcon(imgHelp, "question-line");
                UpdateIcon(imgReport, "feedback-line");
                UpdateIcon(imgUpdate, "refresh-line");
                UpdateIcon(imgAbout, "fetchflow-mark");
                UpdateIcon(imgExit, "logout-box-r-line");

                UpdateIcon(imgNewDl, "file-download-line");
                UpdateIcon(imgVideoDl, "movie-line");
                UpdateIcon(imgBatchDl, "list-settings-line");

                if (menuItems != null)
                {
                    foreach (var mi in menuItems)
                    {
                        if (mi is MenuItemWrapper miw)
                        {
                            miw.UpdateTheme(isDark);
                        }
                    }
                }
            }
            ThemeManager.ThemeChanged += isDark => Gtk.Application.Invoke((_, _) =>
            {
                RefreshMenuIcons(isDark);
                lvFinished?.QueueDraw();
                lvInprogress?.QueueDraw();
            });

            menuSettings.Activated += MenuSettings_Activated;
            menuClearFinished.Activated += MenuClearFinished_Activated;
            menuImportExport.Activated += MenuImportExport_Activated;
            menuLanguage.Activated += MenuLanguage_Activated;
            menuHelpAndSupport.Activated += MenuHelpAndSupport_Activated;
            menuReportProblem.Activated += MenuReportProblem_Activated;
            menuCheckForUpdate.Activated += MenuCheckForUpdate_Activated;
            menuAbout.Activated += MenuAbout_Activated;
            menuExit.Activated += MenuExit_Activated;
            menuMediaGrabber.Activated += MenuMediaGrabber_Activated;

            menuCompletionSound = new CheckMenuItem(TextResource.GetText("MSG_PLAY_SOUND") ?? "Play sound when download finishes")
            {
                Active = Config.Instance.PlayCompletionSound
            };
            menuCompletionSound.Toggled += (_, _) =>
            {
                Config.Instance.PlayCompletionSound = menuCompletionSound.Active;
                Config.SaveConfig();
                ApplicationContext.BroadcastConfigChange();
            };

            mainMenu.Append(menuSettings);
            mainMenu.Append(menuCompletionSound);
            mainMenu.Append(menuMediaGrabber);
            mainMenu.Append(menuClearFinished);
            mainMenu.Append(menuImportExport);
            mainMenu.Append(menuLanguage);
            mainMenu.Append(menuHelpAndSupport);
            mainMenu.Append(menuReportProblem);
            mainMenu.Append(menuCheckForUpdate);
            mainMenu.Append(menuAbout);
            mainMenu.Append(menuExit);
            mainMenu.ShowAll();
        }

        // Creates a MenuItem with a 16px monochrome icon and left-aligned text
        private static MenuItem CreateIconMenuItem(string iconName, string text, out Image iconImage, out Label label)
        {
            var item = new MenuItem();
            var box = new HBox(false, 8)
            {
                MarginStart = 0,
                MarginEnd = 4,
                MarginTop = 2,
                MarginBottom = 2
            };
            var rawPixbuf = LoadSvg(iconName, 16);
            var tintedPixbuf = rawPixbuf != null
                ? (ThemeManager.IsDarkActive ? GtkHelper.TintPixbuf(rawPixbuf, 200, 200, 200) : GtkHelper.TintPixbuf(rawPixbuf, 90, 90, 90))
                : null;
            iconImage = tintedPixbuf != null ? new Image(tintedPixbuf) : new Image();
            label = new Label(text)
            {
                Halign = Align.Start,
                Xalign = 0
            };
            box.PackStart(iconImage, false, false, 0);
            box.PackStart(label, true, true, 0);
            item.Add(box);
            item.ShowAll();
            return item;
        }

        private void MenuMediaGrabber_Activated(object? sender, EventArgs e)
        {
            ApplicationContext.PlatformUIService.CreateAndShowMediaGrabber();
        }

        private void MenuExit_Activated(object? sender, EventArgs e)
        {
            Application.Quit();
            Environment.Exit(0);
        }

        private void MenuAbout_Activated(object? sender, EventArgs e)
        {
            var win = XDM.GtkUI.Dialogs.About.AboutDialog.CreateFromGladeFile(this, windowGroup);
            win.ShowAll();
        }

        private void MenuCheckForUpdate_Activated(object? sender, EventArgs e)
        {
            UpdateClicked?.Invoke(sender, e);
        }

        private void MenuReportProblem_Activated(object? sender, EventArgs e)
        {
            BugReportClicked?.Invoke(sender, e);
        }

        private void MenuHelpAndSupport_Activated(object? sender, EventArgs e)
        {
            SupportPageClicked?.Invoke(sender, e);
        }

        private void MenuImportExport_Activated(object? sender, EventArgs e)
        {
            using var win = new XDM.GtkUI.Dialogs.ImportExport.ImportExportDialog(this);
            win.Run();
        }

        private void MenuLanguage_Activated(object? sender, EventArgs e)
        {
            using var win = LanguageDialog.CreateFromGladeFile(this, windowGroup);
            win.Run();
            win.Destroy();
        }

        // Triggers import flow programmatically
        public void TriggerImport()
        {
            ImportClicked?.Invoke(this, EventArgs.Empty);
        }

        // Triggers export flow programmatically
        public void TriggerExport()
        {
            ExportClicked?.Invoke(this, EventArgs.Empty);
        }

        private void MenuClearFinished_Activated(object? sender, EventArgs e)
        {
            this.ClearAllFinishedClicked?.Invoke(sender, e);
        }

        private void MenuSettings_Activated(object? sender, EventArgs e)
        {
            this.SettingsClicked?.Invoke(this, e);
        }

        private void MenuBatchDownload_Click(object? sender, EventArgs e)
        {
            this.BatchDownloadClicked?.Invoke(sender, e);
        }

        private void MenuVideoDownload_Click(object? sender, EventArgs e)
        {
            this.YoutubeDLDownloadClicked?.Invoke(sender, e);
        }

        private void MenuNewDownload_Click(object? sender, EventArgs e)
        {
            this.NewDownloadClicked?.Invoke(sender, e);
        }

        // Left-aligned in-app headerbar: bold "FetchFlow" + dim "· <view>" custom title (not the
        // centered default title/subtitle); hexpand + halign-start pushes the title left.
        // Wayland CSD: headerbar supplies the close button; no min/max buttons by convention.
        private HeaderBar CreateHeaderBar()
        {
            var hb = new HeaderBar
            {
                ShowCloseButton = true,
                DecorationLayout = ":minimize,maximize,close",
                HasSubtitle = false
            };
            hb.StyleContext.AddClass("main-headerbar");

            var appIcon = new Image
            {
                Pixbuf = LoadSvg("fetchflow-mark", 20) ?? LoadSvg("fetchflow-logo", 20),
                MarginEnd = 4,
                Valign = Align.Center
            };

            var appLabel = new Label { Text = HeaderAppName };
            appLabel.StyleContext.AddClass("header-title-app");
            subtitleLabel = new Label
            {
                Text = HeaderSubtitleSeparator + TextResource.GetText("ALL_UNFINISHED"),
                Ellipsize = Pango.EllipsizeMode.End
            };
            subtitleLabel.StyleContext.AddClass("header-title-view");

            // Update-available dot: the single update indicator (bottom bar stays a plain
            // "Help and support"); hidden until an update lands, click opens the updater
            updateDot = new Button { Visible = false };
            var dotLabel = new Label { Text = UpdateDotGlyph };
            dotLabel.StyleContext.AddClass("update-dot");
            updateDot.Add(dotLabel);
            updateDot.Relief = ReliefStyle.None;
            updateDot.Valign = Align.Center;
            updateDot.TooltipText = TextResource.GetText("MSG_UPDATE_AVAILABLE");
            updateDot.StyleContext.AddClass("flat");
            updateDot.StyleContext.AddClass("update-dot-button");
            updateDot.Clicked += (_, _) => UpdateClicked?.Invoke(this, EventArgs.Empty);

            var titleBox = new HBox(false, ButtonContentSpacing) { Hexpand = false, Halign = Align.Start };
            titleBox.PackStart(appIcon, false, false, 0);
            titleBox.PackStart(appLabel, false, false, 0);
            titleBox.PackStart(subtitleLabel, false, false, 0);
            titleBox.PackStart(updateDot, false, false, 0);
            hb.PackStart(titleBox);
            hb.CustomTitle = new Label("");

            // Theme toggle button: allows instant switching between Dark and Light mode
            var themeToggleBtn = new Button { Visible = true, Relief = ReliefStyle.None, Valign = Align.Center };
            themeToggleBtn.StyleContext.AddClass("flat");
            themeToggleBtn.StyleContext.AddClass("theme-toggle-button");

            var themeIcon = new Image();
            void UpdateThemeIcon(bool isDark)
            {
                themeIcon.Pixbuf = LoadSvg(isDark ? "sun-line" : "moon-line", 18);
                themeToggleBtn.TooltipText = isDark ? "Switch to Light theme" : "Switch to Dark theme";
            }
            UpdateThemeIcon(ThemeManager.IsDarkActive);
            ThemeManager.ThemeChanged += isDark => Gtk.Application.Invoke((_, _) => UpdateThemeIcon(isDark));
            themeToggleBtn.Add(themeIcon);
            themeToggleBtn.Clicked += (_, _) => ThemeManager.ToggleTheme();

            hb.PackEnd(themeToggleBtn);
            hb.ShowAll();
            return hb;
        }

        // Headerbar view subtitle tracks the active sidebar selection (e.g. "· All Finished")
        private void SetHeaderSubtitle(string? viewName)
        {
            if (subtitleLabel == null || string.IsNullOrEmpty(viewName))
            {
                return;
            }
            subtitleLabel.Text = HeaderSubtitleSeparator + viewName;
        }

        private Widget CreateMainPanel()
        {
            var vbMain = new VBox();
            vbMain.PackStart(CreateToolbar(), false, false, 0);
            vbMain.PackStart(CreateInProgressListView(), true, true, 0);
            vbMain.PackStart(CreateFinishedListView(), true, true, 0);
            vbMain.PackStart(CreateBottombar(), false, false, 0);
            vbMain.Show();
            return vbMain;
        }

        private Button CreateButtonWithContent(string icon, string? text = null, byte? r = null, byte? g = null, byte? b = null)
        {
            Label? lbl = null;
            if (!string.IsNullOrEmpty(text))
            {
                lbl = new Label { Text = text };
            }
            var rawPixbuf = LoadSvg(icon, 16);
            var pixbuf = (r.HasValue && g.HasValue && b.HasValue && rawPixbuf != null)
                ? GtkHelper.TintPixbuf(rawPixbuf, r.Value, g.Value, b.Value)
                : rawPixbuf;
            var image = pixbuf != null ? new Image(pixbuf) : new Image();
            return CreateButtonWithContent(image, lbl);
        }

        private Button CreateButtonWithContent(Image image, Label? label)
        {
            // Icon-only buttons: no side margins/spacing; the image expands and GtkMisc's
            // default 0.5 xalign centers the glyph inside the button. Labeled buttons unchanged.
            var hbox = new HBox(false, label != null ? ButtonContentSpacing : 0)
            {
                MarginStart = label != null ? ButtonBoxMargin : 0,
                MarginEnd = label != null ? ButtonBoxMargin : 0
            };

            hbox.PackStart(image, label == null, label == null, 0);
            if (label != null)
            {
                hbox.PackStart(label, false, false, 0);
            }

            var button = new Button
            {
                Relief = ReliefStyle.None,
                Valign = Align.Center,

            };
            button.Add(hbox);
            // Register as flat so the theme's toolbar hover/active states apply
            button.StyleContext.AddClass("flat");
            return button;
        }

        private Widget CreateBottombar()
        {
            var hbox = new HBox(false, 10);
            hbox.StyleContext.AddClass("bottombar");
            hbox.Margin = 4;
            hbox.MarginStart = 8;
            hbox.MarginEnd = 8;
            btnMonitoring = new CheckButton { MarginStart = 5 };
            btnMonitoring.Clicked += BtnMonitoring_Clicked;
            hbox.PackStart(btnMonitoring, false, false, 0);

            var lblMonitoring = new Label { Text = TextResource.GetText("SETTINGS_MONITORING") };
            hbox.PackStart(lblMonitoring, false, false, 0);

            lblExtensionStatus = new Label { MarginStart = 10 };
            lblExtensionStatus.StyleContext.AddClass("extension-status-label");
            hbox.PackStart(lblExtensionStatus, false, false, 0);
            UpdateExtensionStatus();

            lblTotalSpeed = new Label { MarginStart = 15, Visible = false };
            lblTotalSpeed.StyleContext.AddClass("total-speed-label");
            hbox.PackStart(lblTotalSpeed, false, false, 0);

            btnSpeedLimit = CreateButtonWithContent("time-line", null, DimR, DimG, DimB);
            btnSpeedLimit.Clicked += BtnSpeedLimit_Clicked;
            btnSpeedLimit.ButtonPressEvent += (o, args) =>
            {
                if (args.Event.Button == 3)
                {
                    ShowSpeedLimiterMenu(args.Event);
                }
            };
            btnSpeedLimit.StyleContext.AddClass("bottombar-button");
            btnSpeedLimit.MarginStart = 6;
            hbox.PackStart(btnSpeedLimit, false, false, 0);
            UpdateSpeedLimitButton();

            btnScheduler = CreateButtonWithContent("list-settings-line", TextResource.GetText("DESC_Q_TITLE"), CyanR, CyanG, CyanB);
            btnScheduler.Clicked += BtnScheduler_Clicked;
            btnScheduler.StyleContext.AddClass("bottombar-button");

            hbox.PackEnd(btnScheduler, false, false, 0);

            // Container card for bottom statusbar matching the top action toolbar surface
            var bottombarCard = new EventBox
            {
                Margin = 6,
                MarginTop = 2
            };
            bottombarCard.StyleContext.AddClass("main-bottombar");
            bottombarCard.StyleContext.AddClass("main-toolbar");
            bottombarCard.Add(hbox);
            bottombarCard.ShowAll();
            return bottombarCard;
        }

        // Handles application-wide broadcast events to refresh UI state
        private void ApplicationContext_ApplicationEvent(object? sender, ApplicationEvent e)
        {
            if (e.EventType == "ExtensionConnectionChanged" || e.EventType == "ConfigChanged")
            {
                Application.Invoke((_, _) =>
                {
                    UpdateExtensionStatus();
                    UpdateSpeedLimitButton();
                    if (menuCompletionSound != null)
                    {
                        menuCompletionSound.Active = Config.Instance.PlayCompletionSound;
                    }
                });
            }
        }

        // Updates the extension connectivity badge in the bottom bar
        private void UpdateExtensionStatus()
        {
            try
            {
                if (lblExtensionStatus == null) return;
                var count = IpcHttpMessageProcessor.ActiveWebSocketSessionsCount;
                var isConn = IpcHttpMessageProcessor.IsConnected;
                var port = IpcHttpMessageProcessor.EffectivePort;

                if (count > 0)
                {
                    lblExtensionStatus.Markup = $"<span color='#22c55e' size='small'>● Extension Active ({count})</span>";
                    lblExtensionStatus.TooltipText = $"Browser Extension connected via WebSocket\nIPC Port: {port}\nActive sessions: {count}";
                }
                else if (isConn)
                {
                    lblExtensionStatus.Markup = $"<span color='#38bdf8' size='small'>● Extension Ready</span>";
                    lblExtensionStatus.TooltipText = $"Browser Extension connected via HTTP Relay\nIPC Port: {port}";
                }
                else
                {
                    lblExtensionStatus.Markup = $"<span color='#94a3b8' size='small'>○ Extension Listening</span>";
                    lblExtensionStatus.TooltipText = $"FetchFlow IPC Server listening on 127.0.0.1:{port}\nWaiting for browser extension connection...";
                }
            }
            catch { }
        }

        // Toggles global bandwidth throttling on or off
        private void BtnSpeedLimit_Clicked(object? sender, EventArgs e)
        {
            Config.Instance.EnableSpeedLimit = !Config.Instance.EnableSpeedLimit;
            if (Config.Instance.EnableSpeedLimit && Config.Instance.DefaltDownloadSpeed <= 0)
            {
                Config.Instance.DefaltDownloadSpeed = 1024; // 1 MB/s default throttle
            }
            Config.SaveConfig();
            ApplicationContext.BroadcastConfigChange();
            UpdateSpeedLimitButton();
        }

        // Refreshes the bandwidth limiter button visual state and tooltip
        private void UpdateSpeedLimitButton()
        {
            if (btnSpeedLimit == null) return;
            bool enabled = Config.Instance.EnableSpeedLimit;
            int limit = Config.Instance.DefaltDownloadSpeed;
            if (enabled && limit > 0)
            {
                var limitStr = FormattingHelper.FormatSize(limit * 1024.0) + "/s";
                btnSpeedLimit.TooltipText = $"Speed Limiter: {limitStr} (Click to disable)";
                var pix = LoadSvg("time-line", 16);
                if (pix != null)
                {
                    btnSpeedLimit.Image = new Image(GtkHelper.TintPixbuf(pix, AccentR, AccentG, AccentB));
                }
            }
            else
            {
                btnSpeedLimit.TooltipText = "Speed Limiter: Off (Click to enable bandwidth limit)";
                var pix = LoadSvg("time-line", 16);
                if (pix != null)
                {
                    btnSpeedLimit.Image = new Image(GtkHelper.TintPixbuf(pix, DimR, DimG, DimB));
                }
            }
        }

        // Displays context menu with quick speed limit presets
        private void ShowSpeedLimiterMenu(Gdk.EventButton ev)
        {
            var menu = new Menu();
            var titleItem = new MenuItem("⚡ Bandwidth Limit");
            titleItem.Sensitive = false;
            menu.Append(titleItem);
            menu.Append(new SeparatorMenuItem());

            int currentLimit = Config.Instance.EnableSpeedLimit ? Config.Instance.DefaltDownloadSpeed : 0;

            var offItem = new MenuItem(currentLimit == 0 ? "● Unlimited (Off)" : "Unlimited (Off)");
            offItem.Activated += (_, _) =>
            {
                Config.Instance.EnableSpeedLimit = false;
                Config.SaveConfig();
                ApplicationContext.BroadcastConfigChange();
                UpdateSpeedLimitButton();
            };
            menu.Append(offItem);
            menu.Append(new SeparatorMenuItem());

            int[] presets = { 256, 512, 1024, 2048, 5120, 10240 };
            foreach (var preset in presets)
            {
                var label = FormattingHelper.FormatSize(preset * 1024.0) + "/s";
                var isSelected = Config.Instance.EnableSpeedLimit && Config.Instance.DefaltDownloadSpeed == preset;
                var item = new MenuItem(isSelected ? $"● {label}" : label);
                var p = preset;
                item.Activated += (_, _) =>
                {
                    Config.Instance.EnableSpeedLimit = true;
                    Config.Instance.DefaltDownloadSpeed = p;
                    Config.SaveConfig();
                    ApplicationContext.BroadcastConfigChange();
                    UpdateSpeedLimitButton();
                };
                menu.Append(item);
            }

            menu.Append(new SeparatorMenuItem());

            var isCustom = Config.Instance.EnableSpeedLimit && Array.IndexOf(presets, Config.Instance.DefaltDownloadSpeed) < 0 && Config.Instance.DefaltDownloadSpeed > 0;
            var customLabel = isCustom
                ? $"● Custom ({FormattingHelper.FormatSize(Config.Instance.DefaltDownloadSpeed * 1024.0)}/s)..."
                : "Custom Limit...";
            var customItem = new MenuItem(customLabel);
            customItem.Activated += (_, _) =>
            {
                ApplicationContext.PlatformUIService.ShowSpeedLimiterWindow();
            };
            menu.Append(customItem);

            menu.ShowAll();
            menu.PopupAtPointer((Gdk.Event)ev);
        }

        private void BtnScheduler_Clicked(object? sender, EventArgs e)
        {
            SchedulerClicked?.Invoke(sender, e);
        }

        private void BtnMonitoring_Clicked(object? sender, EventArgs e)
        {
            BrowserMonitoringButtonClicked?.Invoke(sender, e);
        }

        // Creates the top action toolbar styled as a card matching the sidebar background
        private Widget CreateToolbar()
        {
            var toolbar = new HBox(false, 5)
            {
                Margin = 4,
                MarginStart = 8,
                MarginEnd = 8
            };
            btnNew = CreateButtonWithContent("links-line", TextResource.GetText("DESC_NEW"), AccentR, AccentG, AccentB);
            toolbar.PackStart(btnNew, false, false, 0);
            btnDel = CreateButtonWithContent("delete-bin-7-line", TextResource.GetText("DESC_DEL"), DestructR, DestructG, DestructB);
            toolbar.PackStart(btnDel, false, false, 0);
            btnOpenFile = CreateButtonWithContent("external-link-line", TextResource.GetText("CTX_OPEN_FILE"), SuccessR, SuccessG, SuccessB);
            toolbar.PackStart(btnOpenFile, false, false, 0);
            btnOpenFolder = CreateButtonWithContent("folder-shared-line", TextResource.GetText("CTX_OPEN_FOLDER"), AmberR, AmberG, AmberB);
            toolbar.PackStart(btnOpenFolder, false, false, 0);
            btnResume = CreateButtonWithContent("play-line", TextResource.GetText("MENU_RESUME"), SuccessR, SuccessG, SuccessB);
            toolbar.PackStart(btnResume, false, false, 0);
            btnPause = CreateButtonWithContent("pause-line", TextResource.GetText("MENU_PAUSE"), AmberR, AmberG, AmberB);
            toolbar.PackStart(btnPause, false, false, 0);

            btnMenu = CreateButtonWithContent("menu-line");
            toolbar.PackEnd(btnMenu, false, false, 0);

            searchEntry = new Entry()
            {
                WidthChars = 15,
                PlaceholderText = TextResource.GetText("LBL_SEARCH"),
                Valign = Align.Center
            };
            searchEntry.StyleContext.AddClass("toolbar-search-entry");
            searchEntry.Activated += (a, b) =>
            {
                searchKeyword = searchEntry.Text;
                finishedDownloadFilter.Refilter();
                inprogressDownloadFilter.Refilter();
            };
            toolbar.PackEnd(searchEntry, false, false, 0);

            // Container card for top action toolbar matching the sidebar surface
            var toolbarCard = new EventBox
            {
                Margin = 6,
                MarginBottom = 2
            };
            toolbarCard.StyleContext.AddClass("main-toolbar");
            toolbarCard.Add(toolbar);
            toolbarCard.ShowAll();

            btnOpenFile.Visible = false;
            btnOpenFolder.Visible = false;
            btnResume.Visible = false;
            btnPause.Visible = false;
            newButton = new ButtonWrapper(this.btnNew);
            deleteButton = new ButtonWrapper(this.btnDel);
            pauseButton = new ButtonWrapper(this.btnPause);
            resumeButton = new ButtonWrapper(this.btnResume);
            openFileButton = new ButtonWrapper(this.btnOpenFile);
            openFolderButton = new ButtonWrapper(this.btnOpenFolder);

            btnMenu.Clicked += BtnMenu_Clicked;

            return toolbarCard;
        }

        private void BtnMenu_Clicked(object? sender, EventArgs e)
        {
            OpenMainMenu();
        }

        private (string iconName, byte r, byte g, byte b) GetCategoryIconConfig(Category cat)
        {
            string iconName = !string.IsNullOrEmpty(cat.CustomIcon) ? cat.CustomIcon : cat.Name switch
            {
                "CAT_DOCUMENTS" => "file-text-line",
                "CAT_MUSIC" => "file-music-line",
                "CAT_VIDEOS" => "movie-line",
                "CAT_COMPRESSED" => "file-zip-line",
                "CAT_PROGRAMS" => "function-line",
                "CAT_IMAGES" => "image-line",
                _ => "folder-shared-line"
            };

            if (!string.IsNullOrEmpty(cat.CustomColor) && cat.CustomColor.StartsWith("#") && cat.CustomColor.Length == 7)
            {
                try
                {
                    byte cr = Convert.ToByte(cat.CustomColor.Substring(1, 2), 16);
                    byte cg = Convert.ToByte(cat.CustomColor.Substring(3, 2), 16);
                    byte cb = Convert.ToByte(cat.CustomColor.Substring(5, 2), 16);
                    return (iconName, cr, cg, cb);
                }
                catch { }
            }

            switch (cat.Name)
            {
                case "CAT_DOCUMENTS":
                    return (iconName, AmberR, AmberG, AmberB);    // Amber / Orange
                case "CAT_MUSIC":
                    return (iconName, PurpleR, PurpleG, PurpleB);   // Purple / Violet
                case "CAT_VIDEOS":
                    return (iconName, DestructR, DestructG, DestructB);         // Coral / Red
                case "CAT_COMPRESSED":
                    return (iconName, TealR, TealG, TealB);     // Teal / Cyan
                case "CAT_PROGRAMS":
                    return (iconName, IndigoR, IndigoG, IndigoB);     // Indigo / Blue
                case "CAT_IMAGES":
                    return (iconName, RoseR, RoseG, RoseB);             // Rose / Pink
                default:
                    return (iconName, IndigoR, IndigoG, IndigoB);
            }
        }

        private Widget CreateCategoryTree()
        {
            // Top status section (Active & Complete)
            statusTree = new TreeView()
            {
                HeadersVisible = false,
                ShowExpanders = false,
                LevelIndentation = 0,
                MarginStart = 4,
                MarginEnd = 4,
                MarginTop = 2,
                MarginBottom = 2
            };
            statusTree.StyleContext.AddClass("sidebar-status-list");

            var statusCols = new TreeViewColumn();
            var statusCellPix = new CellRendererPixbuf();
            statusCellPix.SetPadding(4, 7);
            statusCols.PackStart(statusCellPix, false);
            statusCols.AddAttribute(statusCellPix, "pixbuf", 0);
            statusCols.SetCellDataFunc(statusCellPix, new CellLayoutDataFunc((layout, cell, model, iter) =>
            {
                if (model.GetValue(iter, 0) is Gdk.Pixbuf icon)
                {
                    ((CellRendererPixbuf)cell).Pixbuf = statusTree.Selection.PathIsSelected(model.GetPath(iter))
                        ? selectedSidebarIcons.GetValue(icon, p => GtkHelper.TintPixbuf(p, 255, 255, 255))
                        : icon;
                }
            }));

            var statusCellText = new CellRendererText();
            statusCellText.SetPadding(2, 7);
            statusCols.PackStart(statusCellText, true);
            statusCols.AddAttribute(statusCellText, "text", 1);
            statusTree.AppendColumn(statusCols);

            statusTreeStore = new TreeStore(typeof(Gdk.Pixbuf), typeof(string));
            var rawActive = LoadSvg("arrow-down-line", 20);
            var rawFinished = LoadSvg("check-line", 20);
            var activeIcon = rawActive != null ? GtkHelper.TintPixbuf(rawActive, AccentR, AccentG, AccentB) : null;
            var finishedIcon = rawFinished != null ? GtkHelper.TintPixbuf(rawFinished, SuccessR, SuccessG, SuccessB) : null;

            statusTreeStore.AppendValues(activeIcon, TextResource.GetText("ALL_UNFINISHED"));
            statusTreeStore.AppendValues(finishedIcon, TextResource.GetText("ALL_FINISHED"));
            statusTree.Model = statusTreeStore;
            statusTree.Selection.Mode = SelectionMode.Single;
            statusTree.Selection.Changed += OnStatusChanged;

            // Separate card container for Active & Complete status section
            var statusCard = new EventBox();
            statusCard.StyleContext.AddClass("sidebar-status-card");
            statusCard.MarginStart = 8;
            statusCard.MarginEnd = 8;
            statusCard.MarginTop = 8;
            statusCard.MarginBottom = 8;
            statusCard.Add(statusTree);

            // Categories section collapsible header toggle
            var catHeaderBox = new EventBox();
            catHeaderBox.StyleContext.AddClass("sidebar-heading-box");
            var catTitle = TextResource.GetText("SETTINGS_CAT") ?? "Categories";
            bool isCategoriesExpanded = Config.Instance.CategoriesExpanded;
            var catHeaderLabel = new Label
            {
                Text = isCategoriesExpanded ? $"▾  {catTitle}" : $"▸  {catTitle}",
                Halign = Align.Start,
                MarginStart = 16,
                MarginTop = 8,
                MarginBottom = 4
            };
            catHeaderLabel.StyleContext.AddClass("sidebar-heading");
            catHeaderBox.Add(catHeaderLabel);

            catHeaderBox.ButtonPressEvent += (o, args) =>
            {
                if (args.Event.Button == 1)
                {
                    isCategoriesExpanded = !isCategoriesExpanded;
                    categoryTree.Visible = isCategoriesExpanded;
                    catHeaderLabel.Text = isCategoriesExpanded ? $"▾  {catTitle}" : $"▸  {catTitle}";
                    Config.Instance.CategoriesExpanded = isCategoriesExpanded;
                    Config.SaveConfig();
                }
            };

            // Categories TreeView
            categoryTree = new TreeView()
            {
                Visible = isCategoriesExpanded,
                HeadersVisible = false,
                ShowExpanders = false,
                LevelIndentation = 0,
                MarginStart = 8,
                MarginEnd = 8
            };
            categoryTree.StyleContext.AddClass("sidebar-list");

            var catCols = new TreeViewColumn();
            var catCellPix = new CellRendererPixbuf();
            catCellPix.SetPadding(4, 7);
            catCols.PackStart(catCellPix, false);
            catCols.AddAttribute(catCellPix, "pixbuf", 0);
            catCols.SetCellDataFunc(catCellPix, new CellLayoutDataFunc((layout, cell, model, iter) =>
            {
                if (model.GetValue(iter, 0) is Gdk.Pixbuf icon)
                {
                    ((CellRendererPixbuf)cell).Pixbuf = categoryTree.Selection.PathIsSelected(model.GetPath(iter))
                        ? selectedSidebarIcons.GetValue(icon, p => GtkHelper.TintPixbuf(p, 255, 255, 255))
                        : icon;
                }
            }));

            var catCellText = new CellRendererText();
            catCellText.SetPadding(2, 7);
            catCols.PackStart(catCellText, true);
            catCols.AddAttribute(catCellText, "text", 1);
            categoryTree.AppendColumn(catCols);

            categoryTreeStore = new TreeStore(typeof(Gdk.Pixbuf), typeof(string), typeof(Category));
            foreach (var cat in Config.Instance.Categories)
            {
                var (iconName, r, g, b) = GetCategoryIconConfig(cat);
                var rawIcon = LoadSvg(iconName, 20);
                var coloredIcon = rawIcon != null ? GtkHelper.TintPixbuf(rawIcon, r, g, b) : null;
                categoryTreeStore.AppendValues(coloredIcon, cat.DisplayName, cat);
            }
            categoryTree.Model = categoryTreeStore;
            categoryTree.Selection.Mode = SelectionMode.Single;
            categoryTree.Selection.Changed += OnCategoryChanged;
            
            categoryTree.ButtonPressEvent += (o, args) =>
            {
                if (args.Event.Button == 3)
                {
                    if (categoryTree.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out TreePath path, out _, out _, out _))
                    {
                        categoryTree.Selection.SelectPath(path);
                        if (categoryTree.Model.GetIter(out TreeIter iter, path))
                        {
                            var val = categoryTree.Model.GetValue(iter, 2);
                            if (val is Category cat)
                            {
                                ShowCategoryContextMenu(cat, args.Event);
                            }
                        }
                    }
                }
            };

            // Pack into a sidebar vertical box with a ScrolledWindow wrapper
            var vbSidebar = new VBox(false, 0);
            vbSidebar.PackStart(statusCard, false, false, 0);
            vbSidebar.PackStart(catHeaderBox, false, false, 0);
            vbSidebar.PackStart(categoryTree, true, true, 0);

            var scrolledWindow = new ScrolledWindow
            {
                OverlayScrolling = true,
                Margin = 6,
                MarginEnd = 0
            };
            scrolledWindow.StyleContext.AddClass("sidebar-scroll");
            scrolledWindow.ShadowType = ShadowType.In;
            scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            scrolledWindow.Add(vbSidebar);
            scrolledWindow.SetSizeRequest(196, 200);

            scrolledWindow.ShowAll();
            return scrolledWindow;
        }

        
        private void UpdateStatusListCounts()
        {
            if (this.statusTreeStore == null || this.inprogressDownloadsStore == null || this.finishedDownloadsStore == null)
                return;

            int activeCount = inprogressDownloadsStore.IterNChildren();
            int completeCount = finishedDownloadsStore.IterNChildren();

            if (this.statusTreeStore.GetIterFirst(out TreeIter iter))
            {
                var activeText = TextResource.GetText("ALL_UNFINISHED");
                if (activeCount > 0) activeText += $"  ({activeCount})";
                this.statusTreeStore.SetValue(iter, 1, activeText);

                if (this.statusTreeStore.IterNext(ref iter))
                {
                    var completeText = TextResource.GetText("ALL_FINISHED");
                    if (completeCount > 0) completeText += $"  ({completeCount})";
                    this.statusTreeStore.SetValue(iter, 1, completeText);
                }
            }

            // Update per-category download count badges in the sidebar
            if (this.categoryTreeStore != null && this.categoryTreeStore.GetIterFirst(out TreeIter catIter))
            {
                var names = new List<string>(activeCount + completeCount);
                if (inprogressDownloadsStore.GetIterFirst(out TreeIter inIter))
                {
                    do
                    {
                        if (inprogressDownloadsStore.GetValue(inIter, 0) is string n && !string.IsNullOrEmpty(n))
                            names.Add(n);
                    } while (inprogressDownloadsStore.IterNext(ref inIter));
                }
                if (finishedDownloadsStore.GetIterFirst(out TreeIter finIter))
                {
                    do
                    {
                        if (finishedDownloadsStore.GetValue(finIter, 0) is string n && !string.IsNullOrEmpty(n))
                            names.Add(n);
                    } while (finishedDownloadsStore.IterNext(ref finIter));
                }

                do
                {
                    if (categoryTreeStore.GetValue(catIter, 2) is Category cat)
                    {
                        int count = 0;
                        for (int i = 0; i < names.Count; i++)
                        {
                            if (Helpers.IsOfCategory(names[i], cat)) count++;
                        }
                        var label = cat.DisplayName;
                        if (count > 0) label += $"  ({count})";
                        categoryTreeStore.SetValue(catIter, 1, label);
                    }
                } while (categoryTreeStore.IterNext(ref catIter));
            }
        }

        private void OnStatusChanged(object? sender, EventArgs e)
        {
            if (isSelectingSidebar || lvInprogress == null || lvFinished == null)
            {
                return;
            }
            if (statusTree.Selection.GetSelected(out ITreeModel model, out TreeIter iter))
            {
                isSelectingSidebar = true;
                categoryTree.Selection.UnselectAll();
                isSelectingSidebar = false;

                var path = model.GetPath(iter);
                var index = path.Indices[0];
                // Reset search when switching views so stale keyword can't hide all rows
                ResetSearch();
                if (index == 0)
                {
                    swInProgress.ShowAll();
                    swFinished.Hide();
                    category = null;
                    btnOpenFile.Visible = btnOpenFolder.Visible = false;
                    btnPause.Visible = btnResume.Visible = true;
                    inprogressDownloadFilter.Refilter();
                    SetHeaderSubtitle(TextResource.GetText("ALL_UNFINISHED"));
                }
                else
                {
                    swFinished.ShowAll();
                    swInProgress.Hide();
                    category = null;
                    btnOpenFile.Visible = btnOpenFolder.Visible = true;
                    btnPause.Visible = btnResume.Visible = false;
                    finishedDownloadFilter.Refilter();
                    SetHeaderSubtitle(TextResource.GetText("ALL_FINISHED"));
                }
            }
        }

        // Reloads the sidebar categories store from configuration dynamically
        public void ReloadCategories()
        {
            if (categoryTreeStore == null) return;
            categoryTreeStore.Clear();
            foreach (var cat in Config.Instance.Categories)
            {
                var (iconName, r, g, b) = GetCategoryIconConfig(cat);
                var rawIcon = LoadSvg(iconName, 20);
                var coloredIcon = rawIcon != null ? GtkHelper.TintPixbuf(rawIcon, r, g, b) : null;
                categoryTreeStore.AppendValues(coloredIcon, cat.DisplayName, cat);
            }
        }

        private void ShowCategoryContextMenu(Category cat, Gdk.EventButton ev)
        {
            var menu = new Menu();
            
            var openFolderItem = new MenuItem(TextResource.GetText("MENU_OPEN_FOLDER") ?? "Open Folder");
            openFolderItem.Activated += (s, e) => 
            {
                string dir = cat.DefaultFolder;
                if (string.IsNullOrEmpty(dir)) dir = Config.Instance.DefaultDownloadFolder;
                System.IO.Directory.CreateDirectory(dir);
                PlatformHelper.OpenFolder(dir);
            };
            menu.Add(openFolderItem);

            var manageCatItem = new MenuItem(TextResource.GetText("SETTINGS_CAT") ?? "Manage Categories...");
            manageCatItem.Activated += (s, e) =>
            {
                ApplicationContext.PlatformUIService.ShowSettingsDialog(1);
            };
            menu.Add(manageCatItem);
            
            menu.ShowAll();
            menu.Popup();
        }

        private void OnCategoryChanged(object? sender, EventArgs e)
        {
            if (isSelectingSidebar || lvInprogress == null || lvFinished == null)
            {
                return;
            }
            if (categoryTree.Selection.GetSelected(out ITreeModel model, out TreeIter iter))
            {
                isSelectingSidebar = true;
                statusTree.Selection.UnselectAll();
                isSelectingSidebar = false;

                swFinished.ShowAll();
                swInProgress.Hide();
                category = (Category)model.GetValue(iter, 2);
                btnOpenFile.Visible = btnOpenFolder.Visible = true;
                btnPause.Visible = btnResume.Visible = false;
                ResetSearch();
                finishedDownloadFilter.Refilter();
                SetHeaderSubtitle(model.GetValue(iter, 1) as string);
            }
        }

        private Widget CreateInProgressListView()
        {
            inprogressDownloadsStore = new ListStore(typeof(string),        // file name
                typeof(string),                                             // date modified
                typeof(string),                                             // size
                typeof(int),                                                // progress
                typeof(string),                                             // status
                typeof(InProgressDownloadItem)                             // download type
                );

            inprogressDownloadFilter = new TreeModelFilter(inprogressDownloadsStore, null);
            inprogressDownloadFilter.VisibleFunc = (model, iter) =>
            {
                var name = (string)model.GetValue(iter, 0);
                return Helpers.IsOfCategoryOrMatchesKeyword(name, searchKeyword, category);
            };

            var sortedStore = new TreeModelSort(inprogressDownloadFilter);

            sortedStore.SetSortFunc(0, (model, iter1, iter2) =>
            {
                var t1 = (string)model.GetValue(iter1, 0);
                var t2 = (string)model.GetValue(iter2, 0);
                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;
                return t1.CompareTo(t2);
            });

            sortedStore.SetSortFunc(1, (model, iter1, iter2) =>
            {
                var t1 = (InProgressDownloadItem)model.GetValue(iter1, 5);
                var t2 = (InProgressDownloadItem)model.GetValue(iter2, 5);
                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;
                return t1.DateAdded.CompareTo(t2.DateAdded);
            });

            sortedStore.SetSortFunc(2, (model, iter1, iter2) =>
            {
                var t1 = (InProgressDownloadItem)model.GetValue(iter1, 5);
                var t2 = (InProgressDownloadItem)model.GetValue(iter2, 5);
                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;
                return t1.Size.CompareTo(t2.Size);
            });

            inprogressDownloadsStoreSorted = sortedStore;
            lvInprogress = new TreeView(sortedStore);
            lvInprogress.Selection.Mode = SelectionMode.Multiple;
            lvInprogress.HeadersVisible = false;
            lvInprogress.EnableGridLines = TreeViewGridLines.None;
            // Per-view surface tint (see treeview.unfinished in the theme layer)
            lvInprogress.StyleContext.AddClass("unfinished");

            // Unified Single Card Column (Icon + Title/Sub on left, Meta on right)
            var inprogressCardCol = new TreeViewColumn
            {
                Expand = true,
                Sizing = TreeViewColumnSizing.Autosize,
                Spacing = DownloadColumnSpacing
            };
            var fileIconRenderer = new CellRendererPixbuf { };
            fileIconRenderer.SetPadding(DownloadIconHorizontalPadding, 8);
            inprogressCardCol.PackStart(fileIconRenderer, false);
            inprogressCardCol.SetCellDataFunc(fileIconRenderer, new CellLayoutDataFunc(GetFileIcon));

            var inprogressNameRenderer = new CellRendererText();
            inprogressNameRenderer.SetPadding(DownloadNameHorizontalPadding, 6);
            inprogressNameRenderer.Ellipsize = Pango.EllipsizeMode.Middle;
            inprogressCardCol.PackStart(inprogressNameRenderer, true);
            SetInProgressNameColumn(inprogressCardCol, inprogressNameRenderer, lvInprogress);

            var inprogressMetaRenderer = new CellRendererText
            {
                Xalign = 1.0f,
                Alignment = Pango.Alignment.Right
            };
            inprogressMetaRenderer.SetPadding(DownloadMetaHorizontalPadding, 12);
            inprogressCardCol.PackEnd(inprogressMetaRenderer, false);
            SetInProgressMetaColumn(inprogressCardCol, inprogressMetaRenderer, lvInprogress);

            lvInprogress.AppendColumn(inprogressCardCol);

            lvInprogress.Selection.Changed += (_, _) =>
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            };

            lvInprogress.MotionNotifyEvent += (o, args) =>
            {
                if (lvInprogress.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out TreePath path, out _, out _, out _))
                {
                    if (hoveredInprogressPath == null || hoveredInprogressPath.Compare(path) != 0)
                    {
                        hoveredInprogressPath = path;
                        lvInprogress.QueueDraw();
                    }
                }
                else if (hoveredInprogressPath != null)
                {
                    hoveredInprogressPath = null;
                    lvInprogress.QueueDraw();
                }
            };

            lvInprogress.LeaveNotifyEvent += (o, args) =>
            {
                if (hoveredInprogressPath != null)
                {
                    hoveredInprogressPath = null;
                    lvInprogress.QueueDraw();
                }
            };

            lvInprogress.ButtonReleaseEvent += (a, b) =>
            {
                if (b.Event.Type == Gdk.EventType.ButtonRelease && b.Event.Button == 3)
                {
                    InProgressContextMenuOpening?.Invoke(this, EventArgs.Empty);
                    menuInProgress.PopupAtPointer(b.Event);
                }
            };

            sortedStore.SetSortColumnId(1, SortType.Descending);

            swInProgress = new ScrolledWindow { OverlayScrolling = true, Margin = 6, MarginBottom = 2, MarginTop = 2, ShadowType = ShadowType.None };
            swInProgress.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            swInProgress.Add(lvInprogress);
            swInProgress.ShowAll();
            return swInProgress;
        }

        private Widget CreateFinishedListView()
        {
            finishedDownloadsStore = new ListStore(typeof(string),          // file name
                typeof(string),                                             // date modified
                typeof(string),                                             // size
                typeof(FinishedDownloadItem)                               // download type
                );

            finishedDownloadFilter = new TreeModelFilter(finishedDownloadsStore, null);
            finishedDownloadFilter.VisibleFunc = (model, iter) =>
            {
                var name = (string)model.GetValue(iter, 0);
                return Helpers.IsOfCategoryOrMatchesKeyword(name, searchKeyword, category);
            };

            var sortedStore = new TreeModelSort(finishedDownloadFilter);

            sortedStore.SetSortFunc(0, (model, iter1, iter2) =>
            {
                var t1 = (string)model.GetValue(iter1, 0);
                var t2 = (string)model.GetValue(iter2, 0);

                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;

                return t1.CompareTo(t2);
            });

            sortedStore.SetSortFunc(1, (model, iter1, iter2) =>
            {
                var t1 = (FinishedDownloadItem)model.GetValue(iter1, 3);
                var t2 = (FinishedDownloadItem)model.GetValue(iter2, 3);

                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;

                return t1.DateAdded.CompareTo(t2.DateAdded);
            });

            sortedStore.SetSortFunc(2, (model, iter1, iter2) =>
            {
                var t1 = (FinishedDownloadItem)model.GetValue(iter1, 3);
                var t2 = (FinishedDownloadItem)model.GetValue(iter2, 3);

                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;

                return t1.Size.CompareTo(t2.Size);
            });

            finishedDownloadsStoreSorted = sortedStore;
            lvFinished = new TreeView(sortedStore);
            lvFinished.Selection.Mode = SelectionMode.Multiple;
            lvFinished.HeadersVisible = false;
            lvFinished.EnableGridLines = TreeViewGridLines.None;
            // Per-view surface tint (see treeview.finished in the theme layer)
            lvFinished.StyleContext.AddClass("finished");

            // Unified Single Card Column (Icon + Title/Sub on left, Meta on right)
            var finishedCardCol = new TreeViewColumn
            {
                Expand = true,
                Sizing = TreeViewColumnSizing.Autosize,
                Spacing = DownloadColumnSpacing,
                SortColumnId = 0
            };
            var fileIconRenderer = new CellRendererPixbuf { };
            fileIconRenderer.SetPadding(DownloadIconHorizontalPadding, 8);
            finishedCardCol.PackStart(fileIconRenderer, false);
            finishedCardCol.SetCellDataFunc(fileIconRenderer, new CellLayoutDataFunc(GetFileIcon));

            var finishedNameRenderer = new CellRendererText();
            finishedNameRenderer.SetPadding(DownloadNameHorizontalPadding, 6);
            finishedNameRenderer.Ellipsize = Pango.EllipsizeMode.Middle;
            finishedCardCol.PackStart(finishedNameRenderer, true);
            SetFinishedNameColumn(finishedCardCol, finishedNameRenderer, lvFinished);

            var finishedMetaRenderer = new CellRendererText
            {
                Xalign = 1.0f,
                Alignment = Pango.Alignment.Right
            };
            finishedMetaRenderer.SetPadding(DownloadMetaHorizontalPadding, 12);
            finishedCardCol.PackEnd(finishedMetaRenderer, false);
            SetFinishedMetaColumn(finishedCardCol, finishedMetaRenderer, lvFinished);

            lvFinished.AppendColumn(finishedCardCol);

            lvFinished.Selection.Changed += (_, _) =>
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            };

            lvFinished.MotionNotifyEvent += (o, args) =>
            {
                if (lvFinished.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out TreePath path, out _, out _, out _))
                {
                    if (hoveredFinishedPath == null || hoveredFinishedPath.Compare(path) != 0)
                    {
                        hoveredFinishedPath = path;
                        lvFinished.QueueDraw();
                    }
                }
                else if (hoveredFinishedPath != null)
                {
                    hoveredFinishedPath = null;
                    lvFinished.QueueDraw();
                }
            };

            lvFinished.LeaveNotifyEvent += (o, args) =>
            {
                if (hoveredFinishedPath != null)
                {
                    hoveredFinishedPath = null;
                    lvFinished.QueueDraw();
                }
            };

            lvFinished.ButtonReleaseEvent += (a, b) =>
            {
                if (b.Event.Type == Gdk.EventType.ButtonRelease && b.Event.Button == 3)
                {
                    FinishedContextMenuOpening?.Invoke(this, EventArgs.Empty);
                    menuFinished.PopupAtPointer(b.Event);
                }
            };

            sortedStore.SetSortColumnId(1, SortType.Descending);

            swFinished = new ScrolledWindow { OverlayScrolling = true, Margin = 6, MarginBottom = 2, MarginTop = 2, ShadowType = ShadowType.None };
            swFinished.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            swFinished.Add(lvFinished);
            swFinished.ShowAll();
            return swFinished;
        }

        // Resolves design token RGB tint for file type categories
        private static (byte R, byte G, byte B) GetCategoryColorForFileType(string filename)
        {
            var ext = System.IO.Path.GetExtension(filename)?.ToLowerInvariant() ?? string.Empty;
            var fileType = IconResource.GetFileType(ext);
            return fileType switch
            {
                "Video" => (DestructR, DestructG, DestructB),
                "Music" => (PurpleR, PurpleG, PurpleB),
                "Document" => (AmberR, AmberG, AmberB),
                "Compressed" => (TealR, TealG, TealB),
                "Application" or "ApplicationContext.Core" => (IndigoR, IndigoG, IndigoB),
                "Image" => (RoseR, RoseG, RoseB),
                _ => (SkyR, SkyG, SkyB)
            };
        }

        // Formats absolute paths into user-friendly ~/ paths
        private static string FormatFriendlyPath(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return string.Empty;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home) && folder.StartsWith(home))
            {
                return "~" + folder.Substring(home.Length);
            }
            return folder;
        }

        // Extracts clean hostname from URL
        private static string ExtractDomain(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            try
            {
                if (url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
                {
                    return "blob";
                }
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var host = uri.Host;
                    if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    {
                        host = host.Substring(4);
                    }
                    return host;
                }
            }
            catch { }
            return string.Empty;
        }

        // Rich two-line renderer for completed downloads
        private void SetFinishedNameColumn(TreeViewColumn column, CellRendererText renderer, TreeView view)
        {
            column.SetCellDataFunc(renderer, new CellLayoutDataFunc((_, cell, model, iter) =>
            {
                var path = model.GetPath(iter);
                var selected = view.Selection.PathIsSelected(path);
                var isHovered = !selected && hoveredFinishedPath != null && hoveredFinishedPath.Compare(path) == 0;
                var isAlternate = !selected && !isHovered && path.Indices != null && path.Indices.Length > 0 && (path.Indices[0] % 2 == 1);

                ((CellRendererText)cell).CellBackground = isHovered ? ThemeManager.ActiveHoverColor : (isAlternate ? ThemeManager.ActiveAlternateRowColor : null);

                var name = model.GetValue(iter, 0) as string ?? string.Empty;
                var item = model.GetValue(iter, FINISHED_DATA_INDEX) as FinishedDownloadItem;

                var title = GLib.Markup.EscapeText(name);
                var metaParts = new List<string>();

                if (item != null)
                {
                    var folder = FormatFriendlyPath(item.TargetDir);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        metaParts.Add($"📁 {GLib.Markup.EscapeText(folder)}");
                    }

                    var domain = ExtractDomain(item.PrimaryUrl);
                    if (!string.IsNullOrEmpty(domain))
                    {
                        metaParts.Add($"🌐 {GLib.Markup.EscapeText(domain)}");
                    }

                    var ext = System.IO.Path.GetExtension(name)?.TrimStart('.')?.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ext))
                    {
                        metaParts.Add(GLib.Markup.EscapeText(ext));
                    }
                }

                var metaLine = string.Join("   ·   ", metaParts);

                if (selected)
                {
                    var isDark = ThemeManager.IsDarkActive;
                    var titleCol = isDark ? "#ffffff" : "#0f172a";
                    var metaCol = isDark ? "#ffffff" : "#334155";
                    var metaAlpha = isDark ? $"{MetaSubLineAlphaDark}" : $"{MetaSubLineAlpha}";
                    ((CellRendererText)cell).Markup = string.IsNullOrEmpty(metaLine)
                        ? $"<span weight=\"bold\" color=\"{titleCol}\">{title}</span>"
                        : $"<span weight=\"bold\" color=\"{titleCol}\">{title}</span>\n<span size=\"{MetaSubLineSize}\" alpha=\"{metaAlpha}\" color=\"{metaCol}\">{metaLine}</span>";
                }
                else
                {
                    ((CellRendererText)cell).Markup = string.IsNullOrEmpty(metaLine)
                        ? $"<span weight=\"bold\">{title}</span>"
                        : $"<span weight=\"bold\">{title}</span>\n<span size=\"{MetaSubLineSize}\" alpha=\"{MetaSubLineAlpha}\">{metaLine}</span>";
                }
            }));
        }

        // Rich two-line renderer for in-progress downloads
        private void SetInProgressNameColumn(TreeViewColumn column, CellRendererText renderer, TreeView view)
        {
            column.SetCellDataFunc(renderer, new CellLayoutDataFunc((_, cell, model, iter) =>
            {
                var path = model.GetPath(iter);
                var selected = view.Selection.PathIsSelected(path);
                var isHovered = !selected && hoveredInprogressPath != null && hoveredInprogressPath.Compare(path) == 0;
                var isAlternate = !selected && !isHovered && path.Indices != null && path.Indices.Length > 0 && (path.Indices[0] % 2 == 1);

                ((CellRendererText)cell).CellBackground = isHovered ? ThemeManager.ActiveHoverColor : (isAlternate ? ThemeManager.ActiveAlternateRowColor : null);

                var name = model.GetValue(iter, 0) as string ?? string.Empty;
                var item = model.GetValue(iter, INPROGRESS_DATA_INDEX) as InProgressDownloadItem;

                var title = GLib.Markup.EscapeText(name);
                var metaParts = new List<string>();

                if (item != null)
                {
                    var domain = ExtractDomain(item.PrimaryUrl);
                    if (!string.IsNullOrEmpty(domain))
                    {
                        metaParts.Add($"🌐 {GLib.Markup.EscapeText(domain)}");
                    }

                    var folder = FormatFriendlyPath(item.TargetDir);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        metaParts.Add($"📁 {GLib.Markup.EscapeText(folder)}");
                    }
                }

                var metaLine = string.Join("   ·   ", metaParts);

                if (selected)
                {
                    var isDark = ThemeManager.IsDarkActive;
                    var titleCol = isDark ? "#ffffff" : "#0f172a";
                    var metaCol = isDark ? "#ffffff" : "#334155";
                    var metaAlpha = isDark ? $"{MetaSubLineAlphaDark}" : $"{MetaSubLineAlpha}";
                    ((CellRendererText)cell).Markup = string.IsNullOrEmpty(metaLine)
                        ? $"<span weight=\"bold\" color=\"{titleCol}\">{title}</span>"
                        : $"<span weight=\"bold\" color=\"{titleCol}\">{title}</span>\n<span size=\"{MetaSubLineSize}\" alpha=\"{metaAlpha}\" color=\"{metaCol}\">{metaLine}</span>";
                }
                else
                {
                    ((CellRendererText)cell).Markup = string.IsNullOrEmpty(metaLine)
                        ? $"<span weight=\"bold\">{title}</span>"
                        : $"<span weight=\"bold\">{title}</span>\n<span size=\"{MetaSubLineSize}\" alpha=\"{MetaSubLineAlpha}\">{metaLine}</span>";
                }
            }));
        }

        // Right-aligned card metadata for finished downloads (Size on top, Date on bottom)
        private void SetFinishedMetaColumn(TreeViewColumn column, CellRendererText renderer, TreeView view)
        {
            column.SetCellDataFunc(renderer, new CellLayoutDataFunc((_, cell, model, iter) =>
            {
                var path = model.GetPath(iter);
                var selected = view.Selection.PathIsSelected(path);
                var isHovered = !selected && hoveredFinishedPath != null && hoveredFinishedPath.Compare(path) == 0;
                var isAlternate = !selected && !isHovered && path.Indices != null && path.Indices.Length > 0 && (path.Indices[0] % 2 == 1);

                ((CellRendererText)cell).CellBackground = isHovered ? ThemeManager.ActiveHoverColor : (isAlternate ? ThemeManager.ActiveAlternateRowColor : null);

                var sizeText = model.GetValue(iter, 2) as string ?? string.Empty;
                var dateText = model.GetValue(iter, 1) as string ?? string.Empty;

                var isDark = ThemeManager.IsDarkActive;
                var textCol = isDark ? "#ffffff" : "#0f172a";
                var dateCol = isDark ? "#ffffff" : "#475569";
                var dateAlpha = isDark ? "55000" : $"{SecondaryTextAlpha}";

                var sizeMarkup = selected
                    ? $"<span weight=\"bold\" color=\"{textCol}\">{GLib.Markup.EscapeText(sizeText)}</span>"
                    : $"<span weight=\"bold\">{GLib.Markup.EscapeText(sizeText)}</span>";
                var dateMarkup = selected
                    ? $"<span size=\"9000\" alpha=\"{dateAlpha}\" color=\"{dateCol}\">{GLib.Markup.EscapeText(dateText)}</span>"
                    : $"<span size=\"9000\" alpha=\"{SecondaryTextAlpha}\">{GLib.Markup.EscapeText(dateText)}</span>";

                ((CellRendererText)cell).Markup = $"{sizeMarkup}\n{dateMarkup}";
            }));
        }

        // Right-aligned card metadata for in-progress downloads (Progress % on top, Live Status/Speed on bottom)
        private void SetInProgressMetaColumn(TreeViewColumn column, CellRendererText renderer, TreeView view)
        {
            column.SetCellDataFunc(renderer, new CellLayoutDataFunc((_, cell, model, iter) =>
            {
                var path = model.GetPath(iter);
                var selected = view.Selection.PathIsSelected(path);
                var isHovered = !selected && hoveredInprogressPath != null && hoveredInprogressPath.Compare(path) == 0;
                var isAlternate = !selected && !isHovered && path.Indices != null && path.Indices.Length > 0 && (path.Indices[0] % 2 == 1);

                ((CellRendererText)cell).CellBackground = isHovered ? ThemeManager.ActiveHoverColor : (isAlternate ? ThemeManager.ActiveAlternateRowColor : null);

                var item = model.GetValue(iter, INPROGRESS_DATA_INDEX) as InProgressDownloadItem;
                var sizeText = model.GetValue(iter, 2) as string ?? string.Empty;
                var progress = item?.Progress ?? 0;
                var statusText = model.GetValue(iter, 4) as string ?? string.Empty;

                var isDark = ThemeManager.IsDarkActive;
                var metaTextCol = isDark ? "#ffffff" : "#0f172a";
                var speedCol = isDark ? "#ffffff" : "#0284c7";
                var subAlpha = isDark ? "55000" : $"{SecondaryTextAlpha}";

                var line1 = selected
                    ? $"<span weight=\"bold\" color=\"{metaTextCol}\">{progress}%</span>  <span size=\"9000\" alpha=\"{subAlpha}\" color=\"{metaTextCol}\">({GLib.Markup.EscapeText(sizeText)})</span>"
                    : $"<span weight=\"bold\">{progress}%</span>  <span size=\"9000\" alpha=\"{SecondaryTextAlpha}\">({GLib.Markup.EscapeText(sizeText)})</span>";
                var line2 = selected
                    ? $"<span size=\"9000\" alpha=\"{subAlpha}\" color=\"{speedCol}\">{GLib.Markup.EscapeText(statusText)}</span>"
                    : $"<span size=\"9000\" color=\"#38bdf8\">{GLib.Markup.EscapeText(statusText)}</span>";

                ((CellRendererText)cell).Markup = $"{line1}\n{line2}";
            }));
        }

        // Category-tinted 28px file icon with white selection tint
        void GetFileIcon(ICellLayout cell_layout,
                CellRenderer cell, ITreeModel tree_model, TreeIter iter)
        {
            var name = (string)tree_model.GetValue(iter, 0);
            var (r, g, b) = GetCategoryColorForFileType(name);
            var svgName = IconResource.GetSVGNameForFileType(name);
            var rawPix = LoadSvg(svgName, 28);
            var path = tree_model.GetPath(iter);
            var view = (cell_layout as TreeViewColumn)?.TreeView as TreeView;
            var isSelected = view != null && view.Selection.PathIsSelected(path);
            var isHovered = !isSelected && ((view == lvFinished && hoveredFinishedPath != null && hoveredFinishedPath.Compare(path) == 0) ||
                                            (view == lvInprogress && hoveredInprogressPath != null && hoveredInprogressPath.Compare(path) == 0));
            var isAlternate = !isSelected && !isHovered && path.Indices != null && path.Indices.Length > 0 && (path.Indices[0] % 2 == 1);

            ((CellRendererPixbuf)cell).CellBackground = isHovered ? ThemeManager.ActiveHoverColor : (isAlternate ? ThemeManager.ActiveAlternateRowColor : null);

            if (rawPix != null)
            {
                var isDark = ThemeManager.IsDarkActive;
                ((CellRendererPixbuf)cell).Pixbuf = (isSelected && isDark)
                    ? GtkHelper.TintPixbuf(rawPix, 255, 255, 255)
                    : GtkHelper.TintPixbuf(rawPix, r, g, b);
            }
        }

        // Pango alpha for secondary list text: 60% opacity on the 0-65535 scale
        private const int SecondaryTextAlpha = 39321;
        // Domain · folder sub-line under the file title: deliberately smaller and more
        // faded than the title so the download name stays the visual focus
        private const string MetaSubLineSize = "8200";
        private const int MetaSubLineAlpha = 30000;
        private const int MetaSubLineAlphaDark = 44000;

        private void AppWin1_DeleteEvent(object o, DeleteEventArgs args)
        {
            // Closing the window never quits FetchFlow: hide to tray when a tray icon is available,
            // otherwise minimize — full quit is only via the tray menu or ☰ → Exit.
            args.RetVal = true;
            if (trayManager != null && trayManager.IsTrayActive)
            {
                this.Hide();
                return;
            }
            this.Iconify();
            try
            {
                PlatformHelper.SpawnSubProcess("notify-send",
                    new[] { "FetchFlow Download Manager",
                            "FetchFlow is still running in the background — to fully quit, use the tray icon or the ☰ menu → Exit." });
            }
            catch { /* notify-send is best-effort */ }
        }

        // Tray-menu "Quit": confirm when downloads are active, remove the icon, then exit cleanly.
        private void QuitFromTray()
        {
            var active = CountActiveDownloads();
            if (active > 0 &&
                !Confirm(this, active == 1
                    ? "1 download is in progress. Quit FetchFlow anyway?"
                    : $"{active} downloads are in progress. Quit FetchFlow anyway?"))
            {
                return;
            }
            try
            {
                trayManager?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Debug("Tray dispose on quit: " + ex.Message);
            }
            Application.Quit();
            Environment.Exit(0);
        }

        // Count downloads that are currently transferring (not queued/paused/finished)
        private int CountActiveDownloads()
        {
            if (inprogressDownloadsStore == null || !inprogressDownloadsStore.GetIterFirst(out var iter))
            {
                return 0;
            }
            var count = 0;
            do
            {
                var item = (InProgressDownloadItem)inprogressDownloadsStore.GetValue(iter, INPROGRESS_DATA_INDEX);
                if (item.Status == DownloadStatus.Downloading)
                {
                    count++;
                }
            }
            while (inprogressDownloadsStore.IterNext(ref iter));
            return count;
        }

        private static Gdk.Pixbuf LoadSvg(string name, int dimension = 16)
        {
            return GtkHelper.LoadSvg(name, dimension);
            //new Gdk.Pixbuf(
            //    IoPath.Combine(
            //        AppDomain.CurrentDomain.BaseDirectory, "svg-icons", $"{name}.svg"), dimension, dimension, true);
        }

        public IInProgressDownloadRow? FindInProgressItem(string id)
        {
            if (!inprogressDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                return null;
            }
            do
            {
                var ent = (InProgressDownloadItem)inprogressDownloadsStore.GetValue(iter, INPROGRESS_DATA_INDEX);
                if (ent.Id == id)
                {
                    return new InProgressEntryWrapper(ent, iter, inprogressDownloadsStore);
                }
            }
            while (inprogressDownloadsStore.IterNext(ref iter));
            return null;
        }

        public TreeIter? FindInProgressItemIterById(string id)
        {
            if (!inprogressDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                return null;
            }
            do
            {
                var ent = (InProgressDownloadItem)inprogressDownloadsStore.GetValue(iter, INPROGRESS_DATA_INDEX);
                if (ent.Id == id)
                {
                    return iter;
                }
            }
            while (inprogressDownloadsStore.IterNext(ref iter));
            return null;
        }

        public IFinishedDownloadRow? FindFinishedItem(string id)
        {
            if (!this.finishedDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                return null;
            }
            do
            {
                var ent = (FinishedDownloadItem)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
                if (ent.Id == id)
                {
                    return new FinishedEntryWrapper(ent, iter, finishedDownloadsStore);
                }
            }
            while (finishedDownloadsStore.IterNext(ref iter));
            return null;
        }

        public TreeIter? FindFinishedItemIterById(string id)
        {
            if (!this.finishedDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                return null;
            }
            do
            {
                var ent = (FinishedDownloadItem)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
                if (ent.Id == id)
                {
                    return iter;
                }
            }
            while (finishedDownloadsStore.IterNext(ref iter));
            return null;
        }

        public void AddToTop(InProgressDownloadItem entry)
        {
            var iter = inprogressDownloadsStore.Insert(0);
            inprogressDownloadsStore.SetValue(iter, 0, entry.Name);
            inprogressDownloadsStore.SetValue(iter, 1, entry.DateAdded.ToString("MMM d, yyyy · HH:mm"));
            inprogressDownloadsStore.SetValue(iter, 2, FormattingHelper.FormatSize(entry.Size));
            inprogressDownloadsStore.SetValue(iter, 3, entry.Progress);
            inprogressDownloadsStore.SetValue(iter, 4, entry.Status.ToString());
            inprogressDownloadsStore.SetValue(iter, 5, entry);
            UpdateStatusListCounts();
        }

        public void AddToTop(FinishedDownloadItem entry)
        {
            finishedDownloadsStore.AppendValues(
                entry.Name,
                entry.DateAdded.ToString("MMM d, yyyy · HH:mm"),
                FormattingHelper.FormatSize(entry.Size),
                entry);
            finishedDownloadFilter.Refilter();
            //finishedDownloadsStoreSorted.AppendValues()
            //sortedStore.SetSortColumnId(1, SortType.Descending);
            UpdateStatusListCounts();
        }

        public void SwitchToInProgressView()
        {
            if (this.statusTreeStore.GetIterFirst(out TreeIter iter))
            {
                this.statusTree.Selection.SelectIter(iter);
            }
        }

        public void ClearInProgressViewSelection()
        {
            this.lvInprogress.Selection.UnselectAll();
        }

        public void SwitchToFinishedView()
        {
            if (this.statusTreeStore.GetIterFirst(out TreeIter iter) &&
                this.statusTreeStore.IterNext(ref iter))
            {
                this.statusTree.Selection.SelectIter(iter);
            }
        }

        public void ClearFinishedViewSelection()
        {
            this.lvFinished.Selection.UnselectAll();
        }

        public bool Confirm(object? window, string text)
        {
            if (window is not Window owner)
            {
                owner = this;
            }
            using var msg = new MessageDialog(owner, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo, text);
            msg.Title = "FetchFlow";
            if (msg.Run() == (int)ResponseType.Yes)
            {
                return true;
            }
            return false;
        }

        public void RunOnUIThread(System.Action action)
        {
            Application.Invoke((a, b) => action.Invoke());
        }

        public void RunOnUIThread(Action<string, int, double, long> action, string id, int progress, double speed, long eta)
        {
            Application.Invoke((a, b) =>
            {
                action.Invoke(id, progress, speed, eta);
                UpdateSpeedTracking(id, progress >= 100 ? 0 : speed, eta);
            });
        }

        // Updates aggregate download throughput across all active transfers and tracks shortest ETA
        private void UpdateSpeedTracking(string id, double speed, long eta = 0)
        {
            lock (activeSpeeds)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    if (speed > 0)
                    {
                        activeSpeeds[id] = speed;
                        if (eta > 0)
                        {
                            activeEtas[id] = eta;
                        }
                        else
                        {
                            activeEtas.Remove(id);
                        }
                    }
                    else
                    {
                        activeSpeeds.Remove(id);
                        activeEtas.Remove(id);
                    }
                }

                if (lblTotalSpeed == null) return;

                double total = 0;
                foreach (var s in activeSpeeds.Values)
                {
                    total += s;
                }
                var count = activeSpeeds.Count;

                if (total > 0 && count > 0)
                {
                    var formatted = FormattingHelper.FormatSize(total) + "/s";
                    long minEta = 0;
                    foreach (var e in activeEtas.Values)
                    {
                        if (e > 0 && (minEta == 0 || e < minEta))
                            minEta = e;
                    }
                    var etaText = minEta > 0 ? $" · ETA: {FormattingHelper.ToHMS(minEta)}" : "";

                    lblTotalSpeed.Markup = $"<span color='#f97316'><b>⚡ {formatted}</b></span>{etaText} <span color='#888888' size='small'>({count} active)</span>";
                    lblTotalSpeed.TooltipText = $"Aggregate live download speed: {formatted}{etaText} across {count} transfer(s)";
                    lblTotalSpeed.Visible = true;
                    trayManager?.UpdateSpeedStatus($"{formatted}{etaText} ({count} active)");
                }
                else
                {
                    lblTotalSpeed.Visible = false;
                    trayManager?.UpdateSpeedStatus("");
                }
            }
        }

        public void Delete(IInProgressDownloadRow row)
        {
            var id = row.DownloadEntry.Id;
            UpdateSpeedTracking(id, 0);
            // Primary: remove via the selection's own iter converted to the child store —
            // immune to any id mismatch; only selection wrappers carry sort iters (wrappers
            // from FindInProgressItem hold child iters and must go straight to the id scan)
            if (row is InProgressEntryWrapper wrapper
                && ReferenceEquals(wrapper.GetStore(), inprogressDownloadsStoreSorted))
            {
                try
                {
                    var childIter = GtkHelper.ConvertViewToModel(wrapper.TreeIter, inprogressDownloadsStoreSorted, inprogressDownloadFilter);
                    if (inprogressDownloadsStore!.IterIsValid(childIter)
                        && inprogressDownloadsStore.GetValue(childIter, INPROGRESS_DATA_INDEX) is InProgressDownloadItem verify
                        && verify.Id == id)
                    {
                        inprogressDownloadsStore.Remove(ref childIter);
                        UpdateStatusListCounts();
                        Log.Debug($"Delete: removed in-progress row {id} via selection iter.");
                        return;
                    }
                    Log.Debug($"Delete: selection iter unusable for {id}; falling back to id scan.");
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, $"Delete: iter conversion failed for {id}; falling back to id scan.");
                }
            }
            var modelIter = FindInProgressItemIterById(id);
            if (modelIter.HasValue)
            {
                var iter = modelIter.Value;
                inprogressDownloadsStore.Remove(ref iter);
                UpdateStatusListCounts();
                Log.Debug($"Delete: removed in-progress row {id} via id scan.");
            }
            else
            {
                Log.Debug($"Delete: in-progress row {id} NOT found in store ({inprogressDownloadsStore.IterNChildren()} rows) — UI row not removed.");
            }
        }

        public void Delete(IFinishedDownloadRow row)
        {
            var id = row.DownloadEntry.Id;
            var modelIter = FindFinishedItemIterById(id);
            if (modelIter.HasValue)
            {
                var iter = modelIter.Value;
                finishedDownloadsStore.Remove(ref iter);
                UpdateStatusListCounts();
            }
            //var iter = GtkHelper.ConvertViewToModel(((FinishedEntryWrapper)row).TreeIter,
            //    finishedDownloadsStoreSorted, finishedDownloadFilter);
        }

        private void ResetSearch()
        {
            var raw = searchEntry?.Text;
            searchKeyword = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            if (searchEntry != null && !string.IsNullOrEmpty(searchEntry.Text))
            {
                searchEntry.Text = string.Empty;
            }
            // Make ResetSearch self-healing: callers can't forget to refilter.
            // Filters may not exist yet during early sidebar init, so guard.
            try { inprogressDownloadFilter?.Refilter(); } catch { }
            try { finishedDownloadFilter?.Refilter(); } catch { }
            searchKeyword = null;
        }

        public void DeleteAllFinishedDownloads()
        {
            if (!GtkHelper.ShowConfirmMessageBox(this, TextResource.GetText("MENU_DELETE_COMPLETED"), "FetchFlow"))
            {
                return;
            }
            finishedDownloadsStore.Clear();
            UpdateStatusListCounts();
        }

        public void Delete(IEnumerable<IInProgressDownloadRow> rows)
        {
            var ids = rows
                .Where(row => row?.DownloadEntry != null)
                .Select(row => row.DownloadEntry.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();
            var removed = 0;
            foreach (var id in ids)
            {
                UpdateSpeedTracking(id, 0);
                // Re-scan the child store for every ID; selection/sort iters become stale
                // after the first removal and must never be reused in a batch.
                var modelIter = FindInProgressItemIterById(id);
                if (!modelIter.HasValue)
                {
                    Log.Debug($"Delete batch: in-progress row {id} not found in store.");
                    continue;
                }
                var iter = modelIter.Value;
                if (inprogressDownloadsStore!.Remove(ref iter))
                {
                    removed++;
                    Log.Debug($"Delete batch: removed in-progress row {id}.");
                }
            }
            UpdateStatusListCounts();
            lvInprogress?.Selection.UnselectAll();
            Log.Debug($"Delete batch: removed {removed} of {ids.Count} selected row(s).");
        }

        public void Delete(IEnumerable<IFinishedDownloadRow> rows)
        {
            foreach (var row in rows)
            {
                Delete(row);
                //var iter = ((FinishedEntryWrapper)row).TreeIter;
                //inprogressDownloadsStore.Remove(ref iter);
            }
        }

        public string GetUrlFromClipboard()
        {
            var cb = Clipboard.Get(Gdk.Selection.Clipboard);
            return cb.WaitForText();
        }

        public void OpenNewDownloadMenu()
        {
            newDownloadMenu.PopupAtWidget(this.btnNew, Gdk.Gravity.SouthWest, Gdk.Gravity.NorthWest, null);
        }

        private void OpenMainMenu()
        {
            mainMenu.PopupAtWidget(this.btnMenu, Gdk.Gravity.SouthEast, Gdk.Gravity.NorthEast, null);
        }

        public void SetClipboardText(string text)
        {
            var cb = Clipboard.Get(Gdk.Selection.Clipboard);
            if (cb != null)
            {
                cb.Text = text;
            }
        }

        public void SetClipboardFile(string file)
        {
            var cbcp = new ClipboardFileCopy(file);
            cbcp.Exec();
        }

        public void UpdateBrowserMonitorButton()
        {
            btnMonitoring.Active = Config.Instance.IsBrowserMonitoringEnabled;
        }

        public void ShowUpdateAvailableNotification()
        {
            isUpdateAvailable = true;
            if (updateDot != null)
            {
                updateDot.Visible = true;
            }
        }

        public void ClearUpdateInformation()
        {
            RunOnUIThread(() =>
            {
                isUpdateAvailable = false;
                if (updateDot != null)
                {
                    updateDot.Visible = false;
                }
            });
        }

        private IEnumerable<FinishedDownloadItem> GetAllFinishedDownloads()
        {
            if (!finishedDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                yield break;
            }
            yield return (FinishedDownloadItem)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
            while (finishedDownloadsStore.IterNext(ref iter))
            {
                yield return (FinishedDownloadItem)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
            }
        }

        private IEnumerable<InProgressDownloadItem> GetAllInProgressDownloads()
        {
            if (!inprogressDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                yield break;
            }
            yield return (InProgressDownloadItem)inprogressDownloadsStore.GetValue(iter, INPROGRESS_DATA_INDEX);
            while (inprogressDownloadsStore.IterNext(ref iter))
            {
                yield return (InProgressDownloadItem)inprogressDownloadsStore.GetValue(iter, INPROGRESS_DATA_INDEX);
            }
        }

        private void SetFinishedDownloads(IEnumerable<FinishedDownloadItem> finishedDownloads)
        {
            finishedDownloadsStore.Clear();
            foreach (var item in finishedDownloads)
            {
                finishedDownloadsStore.AppendValues(item.Name,
                    item.DateAdded.ToString("MMM d, yyyy · HH:mm"),
                    FormattingHelper.FormatSize(item.Size),
                    item);
            }
            finishedDownloadFilter?.Refilter();
            UpdateStatusListCounts();
        }

        private void SetInProgressDownloads(IEnumerable<InProgressDownloadItem> incompleteDownloads)
        {
            inprogressDownloadsStore.Clear();
            foreach (var item in incompleteDownloads)
            {
                inprogressDownloadsStore.AppendValues(item.Name,
                    item.DateAdded.ToString("MMM d, yyyy · HH:mm"),
                    FormattingHelper.FormatSize(item.Size),
                    item.Progress,
                    Helpers.GenerateStatusText(item),
                    item);
            }
            inprogressDownloadFilter?.Refilter();
            UpdateStatusListCounts();
        }

        private IList<IInProgressDownloadRow> GetSelectedInProgressDownloads()
        {
            var list = new List<IInProgressDownloadRow>(0);
            var rows = lvInprogress.Selection.GetSelectedRows(out ITreeModel model);
            if (rows != null && rows.Length > 0)
            {
                list.Capacity = rows.Length;
                foreach (var row in rows)
                {
                    if (model.GetIter(out TreeIter iter, row))
                    {
                        if (model.GetValue(iter, INPROGRESS_DATA_INDEX) is not InProgressDownloadItem ent)
                        {
                            Log.Debug($"Selection: in-progress path {row} did not resolve to an item (row skipped).");
                            continue;
                        }
                        list.Add(new InProgressEntryWrapper(ent, iter, model));
                    }
                    else
                    {
                        Log.Debug($"Selection: in-progress path {row} could not be resolved to an iter (row skipped).");
                    }
                }
            }
            return list;
        }

        private IList<IFinishedDownloadRow> GetSelectedFinishedDownloads()
        {
            var list = new List<IFinishedDownloadRow>(0);
            var rows = lvFinished.Selection.GetSelectedRows(out ITreeModel model);
            if (rows != null && rows.Length > 0)
            {
                list.Capacity = rows.Length;
                foreach (var row in rows)
                {
                    if (model.GetIter(out TreeIter iter, row))
                    {
                        if (model.GetValue(iter, FINISHED_DATA_INDEX) is not FinishedDownloadItem ent)
                        {
                            Log.Debug($"Selection: finished path {row} did not resolve to an item (row skipped).");
                            continue;
                        }
                        list.Add(new FinishedEntryWrapper(ent, iter, model));
                    }
                    else
                    {
                        Log.Debug($"Selection: finished path {row} could not be resolved to an iter (row skipped).");
                    }
                }
            }
            return list;
        }

        private int GetSelectedCategory()
        {
            if (statusTree != null && statusTree.Selection.GetSelected(out ITreeModel model, out TreeIter iter))
            {
                return model.GetPath(iter).Indices[0];
            }
            return -1;
        }

        public void ConfirmDelete(string text, out bool approved, out bool deleteFiles)
        {
            approved = false;
            deleteFiles = false;
            using var dlg = DeleteConfirmDialog.CreateFromGladeFile(this, this.windowGroup);
            if (!string.IsNullOrEmpty(text))
            {
                dlg.DescriptionText = text;
            }
            dlg.Run();
            if (dlg.Result)
            {
                approved = true;
                deleteFiles = dlg.ShouldDeleteFile;
            }
            dlg.Destroy();
        }

        public IPlatformClipboardMonitor GetClipboardMonitor() => this.clipboarMonitor;

        public void ShowAndActivate()
        {
            if (!this.Visible)
            {
                this.Show();
            }
            // Wayland/Phase1.2: focus-stealing from IPC/background is compositor policy.
            // On Wayland, Present() alone usually only sets an urgency hint; that is the
            // correct, non-abusive signal. On X11 keep the direct raise+focus behavior.
            this.Present();
            if (RunningOnWayland)
            {
                // Request attention; the compositor flashes the taskbar entry (xdg-activation
                // is honored by KWin 6.6+/Mutter 49+; urgency hint is the portable fallback).
                this.UrgencyHint = true;
            }
        }

        // Wayland/Phase1.2: detect Wayland session to choose activation strategy
        private static bool RunningOnWayland =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

        // Finalizer to log garbage collection.
        ~MainWindow()
        {
            // Log that MainWindow is being garbage collected.
            Log.Debug("MainWindow GC'ed!!!");
        }
    }
}
