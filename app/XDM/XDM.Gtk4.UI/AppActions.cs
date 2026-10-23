// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Application actions + keyboard accelerators for the GTK 4 frontend.
// Gio actions are the GTK 4 way to expose commands: they carry the shortcut,
// appear in the app menu on GNOME, and can be triggered from tests.
using System;
using Gio;
using SysAction = System.Action;

namespace XDM.Gtk4UI
{
    internal static class AppActions
    {
        // Action names (prefixed with "app." in accelerator registration)
        public const string NewDownload = "new-download";
        public const string Search = "search";
        public const string ToggleTheme = "toggle-theme";
        public const string Quit = "quit";
        public const string Refresh = "refresh";
        public const string BatchDownload = "batch-download";
        public const string Scheduler = "scheduler";
        public const string ImportExport = "import-export";
        public const string Settings = "settings";
        public const string About = "about";
        public const string SelectAll = "select-all";
        public const string SortNameAsc = "sort-name-asc";
        public const string SortNameDesc = "sort-name-desc";
        public const string SortSizeAsc = "sort-size-asc";
        public const string SortSizeDesc = "sort-size-desc";
        public const string SortDateAsc = "sort-date-asc";
        public const string SortDateDesc = "sort-date-desc";

        // Row context menu and keyboard command actions
        public const string OpenFile = "open-file";
        public const string OpenFolder = "open-folder";
        public const string PauseDownload = "pause-download";
        public const string ResumeDownload = "resume-download";
        public const string DeleteDownload = "delete-download";
        public const string CopyUrl = "copy-url";
        public const string CopyFile = "copy-file";
        public const string DownloadProperties = "download-properties";
        public const string VerifyChecksum = "verify-checksum";
        public const string RestartDownload = "restart-download";
        public const string SaveAs = "save-as";
        public const string RefreshLink = "refresh-link";
        public const string MoveToQueue = "move-to-queue";
        public const string ShowProgress = "show-progress";
        public const string SpeedLimiter = "speed-limiter";
        public const string VideoDownload = "video-download";
        // Menu-only commands (no accelerator)
        public const string CheckForUpdate = "check-for-update";
        public const string HelpSupport = "help-support";
        public const string ReportProblem = "report-problem";
        public const string RemoveFinished = "remove-finished";

        // Registers every action and its accelerator on the application
        public static void Register(
            Gtk.Application app,
            SysAction newDownload,
            SysAction search,
            SysAction toggleTheme,
            SysAction refresh,
            SysAction batchDownload,
            SysAction scheduler,
            SysAction importExport,
            SysAction settings,
            SysAction about,
            SysAction? selectAll = null,
            SysAction? sortNameAsc = null,
            SysAction? sortNameDesc = null,
            SysAction? sortSizeAsc = null,
            SysAction? sortSizeDesc = null,
            SysAction? sortDateAsc = null,
            SysAction? sortDateDesc = null,
            SysAction? openFile = null,
            SysAction? openFolder = null,
            SysAction? pauseDownload = null,
            SysAction? resumeDownload = null,
            SysAction? deleteDownload = null,
            SysAction? copyUrl = null,
            SysAction? copyFile = null,
            SysAction? downloadProperties = null,
            SysAction? verifyChecksum = null,
            SysAction? restartDownload = null,
            SysAction? saveAs = null,
            SysAction? refreshLink = null,
            SysAction? moveToQueue = null,
            SysAction? showProgress = null,
            SysAction? speedLimiter = null,
            SysAction? videoDownload = null,
            SysAction? checkForUpdate = null,
            SysAction? helpSupport = null,
            SysAction? reportProblem = null,
            SysAction? removeFinished = null)
        {
            Add(app, NewDownload, "<Control>n", newDownload);
            Add(app, Search, "<Control>f", search);
            Add(app, ToggleTheme, "<Control>t", toggleTheme);
            Add(app, Refresh, "F5", refresh);
            Add(app, BatchDownload, "<Control>b", batchDownload);
            Add(app, Scheduler, "<Control><Shift>q", scheduler);
            Add(app, ImportExport, "<Control>i", importExport);
            Add(app, Settings, "<Control>comma", settings);
            Add(app, About, "F1", about);
            Add(app, Quit, "<Control>q", () => app.Quit());

            if (selectAll != null) Add(app, SelectAll, "<Control>a", selectAll);
            if (sortNameAsc != null) Add(app, SortNameAsc, null, sortNameAsc);
            if (sortNameDesc != null) Add(app, SortNameDesc, null, sortNameDesc);
            if (sortSizeAsc != null) Add(app, SortSizeAsc, null, sortSizeAsc);
            if (sortSizeDesc != null) Add(app, SortSizeDesc, null, sortSizeDesc);
            if (sortDateAsc != null) Add(app, SortDateAsc, null, sortDateAsc);
            if (sortDateDesc != null) Add(app, SortDateDesc, null, sortDateDesc);

            if (openFile != null) Add(app, OpenFile, "<Control>o", openFile);
            if (openFolder != null) Add(app, OpenFolder, null, openFolder);
            if (pauseDownload != null) Add(app, PauseDownload, null, pauseDownload);
            if (resumeDownload != null) Add(app, ResumeDownload, null, resumeDownload);
            if (deleteDownload != null) Add(app, DeleteDownload, "Delete", deleteDownload);
            if (copyUrl != null) Add(app, CopyUrl, "<Control>c", copyUrl);
            if (copyFile != null) Add(app, CopyFile, null, copyFile);
            if (downloadProperties != null) Add(app, DownloadProperties, "<Alt>Return", downloadProperties);
            if (verifyChecksum != null) Add(app, VerifyChecksum, null, verifyChecksum);
            if (restartDownload != null) Add(app, RestartDownload, "<Control>r", restartDownload);
            if (saveAs != null) Add(app, SaveAs, "<Control>s", saveAs);
            if (refreshLink != null) Add(app, RefreshLink, null, refreshLink);
            if (moveToQueue != null) Add(app, MoveToQueue, null, moveToQueue);
            if (showProgress != null) Add(app, ShowProgress, null, showProgress);
            if (speedLimiter != null) Add(app, SpeedLimiter, null, speedLimiter);
            if (videoDownload != null) Add(app, VideoDownload, "<Control><Shift>v", videoDownload);
            if (checkForUpdate != null) Add(app, CheckForUpdate, null, checkForUpdate);
            if (helpSupport != null) Add(app, HelpSupport, null, helpSupport);
            if (reportProblem != null) Add(app, ReportProblem, null, reportProblem);
            if (removeFinished != null) Add(app, RemoveFinished, null, removeFinished);
        }

        // Adds one parameterless action with its accelerator
        public static void Add(Gtk.Application app, string name, string? accelerator, SysAction handler)
        {
            var action = SimpleAction.New(name, null);
            action.OnActivate += (_, _) => handler();
            app.AddAction(action);
            if (!string.IsNullOrEmpty(accelerator))
            {
                app.SetAccelsForAction("app." + name, new[] { accelerator });
            }
        }
    }
}
