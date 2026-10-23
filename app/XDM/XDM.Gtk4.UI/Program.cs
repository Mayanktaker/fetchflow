// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 frontend entry point. The GTK 3 frontend (XDM.Gtk.UI) remains the
// compatibility build for GTK 3-only desktops; this project is the primary UI.
using System;
using System.Collections.Generic;
using System.IO;
using Gtk;
using TraceLog;
using Translations;
using XDM.Core;
using XDM.Core.BrowserMonitoring;
using XDM.Core.Downloader;
using XDM.Core.Util;
using XDM.Gtk4UI.Dialogs;
using GtkApplication = Gtk.Application;

namespace XDM.Gtk4UI
{
    internal static class Program
    {
        // Must match the desktop file's StartupWMClass / app id
        private const string AppId = "com.mayanktaker.fetchflow";
        private static bool isContextConfigured;
        private static MainWindow? activeMainWindow;
        private static readonly object CrashLogLock = new();
        private static readonly string CrashLogPath = Path.Combine(Config.AppDir, "crash.log");

        // Appends fatal and unhandled exceptions to crash.log
        private static void WriteCrashLine(string label, object detail)
        {
            try
            {
                Directory.CreateDirectory(Config.AppDir);
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {label}: {detail}";
                if (detail is Exception ex) line += Environment.NewLine + ex;
                line += Environment.NewLine;
                lock (CrashLogLock)
                {
                    File.AppendAllText(CrashLogPath, line);
                }
            }
            catch { }
        }

        private static int Main(string[] args)
        {
            // Burn assembly reference to prevent single-file trimming
            _ = typeof(System.IO.Pipes.PipeStream);

            // Shared configuration load
            try
            {
                Config.LoadConfig();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Config load failed: " + ex.Message);
            }

            try
            {
                Log.InitFileBasedTrace(Path.Combine(Config.AppDir, "log.txt"));
            }
            catch { }

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                WriteCrashLine("AppDomain.UnhandledException (IsTerminating=" + e.IsTerminating + ")", e.ExceptionObject);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                WriteCrashLine("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };

            LoadLanguageTexts();

            var app = Adw.Application.New(AppId, Gio.ApplicationFlags.FlagsNone);
            var demo = Array.Exists(args, a => a == "--demo");
            var smoke = Array.Exists(args, a => a == "--smoke");
            var background = Array.Exists(args, a => a == "--background");
            if (smoke)
            {
                Environment.SetEnvironmentVariable("FETCHFLOW_SKIP_SINGLE_INSTANCE", "1");
            }

            app.OnActivate += (sender, _) =>
            {
                var gtkApp = (GtkApplication)sender!;
                if (!isContextConfigured)
                {
                    activeMainWindow = MainWindow.Create(gtkApp, demo);
                    var windowAdapter = new Gtk4ApplicationWindow(activeMainWindow);
                    var uiService = new Gtk4PlatformUIService(activeMainWindow);
                    var core = new ApplicationCore();
                    var xdmApp = new XDM.Core.Application();
                    var videoTracker = new VideoTracker();
                    var clipboardMonitor = new ClipboardMonitor();
                    var linkRefresher = new LinkRefresher();

                    ApplicationContext.FirstRunCallback += (_, _) => PlatformHelper.ReconcileAutoStart();

                    ApplicationContext.Configurer()
                        .RegisterApplicationWindow(windowAdapter)
                        .RegisterApplication(xdmApp)
                        .RegisterApplicationCore(core)
                        .RegisterCapturedVideoTracker(videoTracker)
                        .RegisterClipboardMonitor(clipboardMonitor)
                        .RegisterLinkRefresher(linkRefresher)
                        .RegisterPlatformUIService(uiService)
                        .Configure();

                    try { PlatformHelper.MigrateLegacyAutoStart(); }
                    catch (Exception ex) { Log.Debug(ex, "AutoStart migration: " + ex.Message); }
                    PlatformHelper.ReconcileAutoStart();
                    ArgsProcessor.Process(args);

                    windowAdapter.NotifyWindowCreated();

                    if (smoke)
                    {
                        RunSmokeVerification(gtkApp, windowAdapter, uiService, activeMainWindow);
                        return;
                    }

                    if (!background)
                    {
                        activeMainWindow.ShowAndActivate();
                    }
                    isContextConfigured = true;
                }
                else
                {
                    activeMainWindow?.ShowAndActivate();
                }
            };

            return app.RunWithSynchronizationContext(null);
        }

        // Executes headless verification of GTK 4 vertical slice components
        private static void RunSmokeVerification(
            GtkApplication gtkApp,
            Gtk4ApplicationWindow windowAdapter,
            Gtk4PlatformUIService uiService,
            MainWindow mainWindow)
        {
            Console.WriteLine("[GTK4 SMOKE] Starting GTK 4 vertical slice verification...");

            if (ApplicationContext.MainWindow == null)
                throw new InvalidOperationException("MainWindow not registered in ApplicationContext");
            if (ApplicationContext.PlatformUIService == null)
                throw new InvalidOperationException("PlatformUIService not registered in ApplicationContext");
            if (ApplicationContext.CoreService == null)
                throw new InvalidOperationException("CoreService not registered in ApplicationContext");

            var testInProgress = new InProgressDownloadItem
            {
                Id = "smoke-test-1",
                Name = "test-download.iso",
                Size = 104857600,
                Progress = 15,
                Status = DownloadStatus.Downloading,
                DateAdded = DateTime.Now
            };
            windowAdapter.AddToTop(testInProgress);
            if (windowAdapter.FindInProgressItem("smoke-test-1") == null)
                throw new InvalidOperationException("Failed to find added in-progress item");

            windowAdapter.RunOnUIThread((id, prg, spd, eta) =>
            {
                var row = windowAdapter.FindInProgressItem(id);
                if (row != null)
                {
                    row.Progress = prg;
                    row.DownloadSpeed = "5.0 MB/s";
                    row.ETA = "1m";
                }
            }, "smoke-test-1", 50, 5242880, 60);

            var testFinished = new FinishedDownloadItem
            {
                Id = "smoke-test-1",
                Name = "test-download.iso",
                Size = 104857600,
                DateAdded = DateTime.Now
            };
            windowAdapter.AddToTop(testFinished);
            var inProgRow = windowAdapter.FindInProgressItem("smoke-test-1");
            if (inProgRow != null) windowAdapter.Delete(inProgRow);
            if (windowAdapter.FindFinishedItem("smoke-test-1") == null)
                throw new InvalidOperationException("Failed to find finished item");

            var newDlg = uiService.CreateNewDownloadDialog(false);
            if (newDlg == null) throw new InvalidOperationException("CreateNewDownloadDialog returned null");
            newDlg.Url = "https://example.com/test.iso";
            newDlg.SelectedFileName = "test.iso";
            newDlg.SetFileSizeText("100 MB");
            newDlg.DisposeWindow();

            var prgWin = uiService.CreateProgressWindow("smoke-test-1");
            if (prgWin == null) throw new InvalidOperationException("CreateProgressWindow returned null");
            prgWin.FileNameText = "test.iso";
            prgWin.DownloadProgress = 75;
            prgWin.DownloadStarted();
            prgWin.DownloadCancelled();
            prgWin.DestroyWindow();

            var cmpldDlg = uiService.CreateDownloadCompleteDialog();
            if (cmpldDlg == null) throw new InvalidOperationException("CreateDownloadCompleteDialog returned null");
            cmpldDlg.FileNameText = "test.iso";
            cmpldDlg.FolderText = "/tmp";

            // Verify secondary dialog components
            var checksumDlg = new ChecksumDialog(null, typeof(Program).Assembly.Location);
            checksumDlg.Close();

            var propDlg = new PropertiesDialog(null, testFinished, "session=123", new Dictionary<string, List<string>> { { "User-Agent", new List<string> { "TestAgent" } } });

            var settingsDlg = new SettingsDialog(null, 0);

            var importExportDlg = new ImportExportDialog(null);
            importExportDlg.Close();

            var qSelectDlg = uiService.CreateQueueSelectionDialog();
            qSelectDlg.SetData(new[] { "Default queue" }, new[] { "q-1" }, new[] { "d-1" });
            qSelectDlg.Dispose();

            var qSchedDlg = uiService.CreateQueuesAndSchedulerWindow();
            qSchedDlg.SetData(new[] { new DownloadQueue("q-1", "Default queue") { DownloadIds = new List<string> { testInProgress.Id } } });
            qSchedDlg.RefreshView();

            var newQDlg = new NewQueueDialog(null, (_, _) => { });

            var batchWin = new BatchDownloadWindow(null);
            batchWin.Url = "https://example.com/file_*.png";
            batchWin.IsLetterMode = true;
            batchWin.SetStartLetterRange(new[] { "A", "B", "C" });
            batchWin.SetEndLetterRange(new[] { "A", "B", "C" });
            batchWin.DestroyWindow();

            var vidDlg = uiService.CreateNewVideoDialog();
            vidDlg.SelectedFileName = "sample.mp4";
            vidDlg.FileSize = "50 MB";
            vidDlg.ShowMp3Checkbox = true;
            vidDlg.IsMp3CheckboxChecked = false;
            vidDlg.DisposeWindow();

            // List/theme regression checks for the P0 fixes (search, selection, ThemeMode)
            mainWindow.RunSelfTest();

            Console.WriteLine("[GTK4 SMOKE PASS] Vertical slice & secondary dialog components verified successfully.");
            gtkApp.Quit();
        }

        // Loads localized UI strings from the application Lang directory
        private static void LoadLanguageTexts()
        {
            try
            {
                var langDirCandidates = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Lang")
                };

                string? langDir = null;
                foreach (var dir in langDirCandidates)
                {
                    if (Directory.Exists(dir))
                    {
                        langDir = dir;
                        break;
                    }
                }

                var targetLang = Config.Instance.Language ?? "English";
                var targetFileName = "English.txt";

                if (langDir != null)
                {
                    var indexFile = Path.Combine(langDir, "index.txt");
                    if (File.Exists(indexFile))
                    {
                        var lines = File.ReadAllLines(indexFile);
                        foreach (var rawLine in lines)
                        {
                            var line = rawLine.Trim();
                            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                            var eqIdx = line.IndexOf('=');
                            if (eqIdx > 0)
                            {
                                var name = line.Substring(0, eqIdx).Trim();
                                var file = line.Substring(eqIdx + 1).Trim();

                                if (string.Equals(name, targetLang, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(file, targetLang, StringComparison.OrdinalIgnoreCase) ||
                                    name.StartsWith(targetLang + " ", StringComparison.OrdinalIgnoreCase) ||
                                    targetLang.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase))
                                {
                                    targetFileName = file;
                                    break;
                                }
                            }
                        }
                    }
                    var fullLangPath = Path.Combine(langDir, targetFileName);
                    if (File.Exists(fullLangPath))
                    {
                        TextResource.Load(fullLangPath);
                        return;
                    }
                }
                TextResource.Load(targetFileName);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to load localization: " + ex.Message);
            }
        }
    }
}
