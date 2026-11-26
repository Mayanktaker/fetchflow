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
    public partial class MainWindow : Window, IApplicationWindow
    {
        private TreeStore statusTreeStore, categoryTreeStore;
        private TreeView statusTree, categoryTree;
        private bool isSelectingSidebar = false;
        private ListStore inprogressDownloadsStore, finishedDownloadsStore;
        private TreeView lvInprogress, lvFinished;
        private ScrolledWindow swInProgress, swFinished;
        private Widget? listHostInProgress, listHostFinished;
        // Empty-state stacks and pages: each list swaps between "list" and "empty"
        private Gtk.Stack? stackInProgress, stackFinished;
        private EmptyStatePanel? emptyInProgress, emptyFinished;
        // Bottom-bar selection counter, shown only while a selection exists
        private Label? lblSelectionCount;
        private TreeModelFilter finishedDownloadFilter;
        private TreeModelFilter inprogressDownloadFilter;
        private TreeModelSort inprogressDownloadsStoreSorted;
        private TreeModelSort finishedDownloadsStoreSorted;
        private string? searchKeyword;
        private Category? category;
        private Button btnNew, btnDel, btnOpenFile, btnOpenFolder, btnResume, btnPause, btnMenu, btnScheduler, btnSpeedLimit, btnSelectAll;
        private MenuButton? btnSort;
        private IButton newButton, deleteButton, pauseButton, resumeButton, openFileButton, openFolderButton;
        private IMenuItem[] menuItems;
        private Menu newDownloadMenu;
        private Menu mainMenu;
        private CheckMenuItem? menuCompletionSound;
        private WindowGroup windowGroup;
        private CheckButton btnMonitoring;
        private Label lblExtensionStatus;
        private Label? lblTotalSpeed;
        private Label? lblViewTitle, lblViewCount;
        private Button? btnActivity;
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
        // Ink the branded primary action draws with: white or near-black, per scheme
        private static byte PrimaryInkR => PrimaryActionInk().R;
        private static byte PrimaryInkG => PrimaryActionInk().G;
        private static byte PrimaryInkB => PrimaryActionInk().B;
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
        private const int ButtonContentSpacing = DesignTokens.ButtonContentSpacing;
        private const int ButtonBoxMargin = DesignTokens.ButtonBoxMargin;
        private const int DownloadColumnSpacing = 0;
        private const int DownloadIconSize = 28;
        private const int DownloadIconHorizontalPadding = DesignTokens.RowIconHorizontalPadding;
        private const int DownloadNameHorizontalPadding = DesignTokens.RowNameHorizontalPadding;
        private const int DownloadMetaHorizontalPadding = DesignTokens.RowMetaHorizontalPadding;
        // Row vertical padding: GTK3 sizes a row from its cell renderers (CSS row
        // margin/padding only inset the painted card), so this is what separates the
        // list items. Sized against the 12px card inset in the theme CSS.
        private const int DownloadRowToggleVerticalPadding = DesignTokens.RowTogglePadding;
        private const int DownloadRowIconVerticalPadding = DesignTokens.RowIconPadding;
        private const int DownloadRowNameVerticalPadding = DesignTokens.RowNamePadding;
        private const int DownloadRowMetaVerticalPadding = DesignTokens.RowMetaPadding;
        // Size column: default width + the clamp its header drag handle respects
        private const int DownloadMetaActiveWidth = 196;
        private const int DownloadMetaFinishedWidth = 176;
        private const int DownloadSizeColumnMinWidth = 110;
        private const int DownloadSizeColumnMaxWidth = 720;
        // File name column: starting width, then it absorbs the leftover space
        private const int DownloadNameColumnWidth = 420;
        // File name column: takes the leftover width, clamped so drag stays usable
        private const int DownloadNameColumnMinWidth = 160;
        private const int DownloadNameColumnMaxWidth = 1200;
        // Header caption insets are theme CSS (list-header-name / list-header-size)

        private Menu menuInProgress, menuFinished;
        private IPlatformClipboardMonitor clipboarMonitor;
        private TreePath? hoveredInprogressPath;
        private TreePath? hoveredFinishedPath;
        private TreeViewColumn? inprogressNameCol, inprogressSizeCol, finishedNameCol, finishedSizeCol;
        private TreePath[]? inprogressRightClickSnapshot, finishedRightClickSnapshot;
        private const int SortTypeColumnId = 100;

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

            // Tray: install the tray glyph into the user icon theme first (repairs installs whose
            // package predates it), then register via the desktop's preferred protocol (SNI).
            PlatformHelper.EnsureTrayIconInstalled();
            trayManager = new TrayIconManager();
            trayManager.Init(GtkHelper.LoadSvg(PlatformHelper.TrayIconName, 22)
                             ?? GtkHelper.LoadSvg("fetchflow-logo", 22),
                             "FetchFlow Download Manager", ShowAndActivate, QuitFromTray);

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

        private void BtnMenu_Clicked(object? sender, EventArgs e)
        {
            OpenMainMenu();
        }

        // Pango alpha for secondary list text: 60% opacity on the 0-65535 scale
        private const int SecondaryTextAlpha = 39321;
        // Domain · folder sub-line under the file title: deliberately smaller and more
        // faded than the title so the download name stays the visual focus.
        // Glyphs are monochrome geometric shapes (they inherit the faded span
        // color) instead of colorful emoji, so the meta line never shouts.
        private const string MetaSubLineSize = "7500";
        private const int MetaSubLineAlpha = 22000;
        private const int MetaSubLineAlphaDark = 33000;

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

        public void ShowAndActivate()
        {
            // Minimized windows report Visible=true, and Present() alone is only an
            // urgency hint on Wayland — de-iconify first or "Show" appears to do nothing.
            this.Deiconify();
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
