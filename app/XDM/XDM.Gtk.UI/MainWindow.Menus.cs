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
                RefreshPrimaryActionButton();
                SyncHeaderSortState();
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
    }
}
