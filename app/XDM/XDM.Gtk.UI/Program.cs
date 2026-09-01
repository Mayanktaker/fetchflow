// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.Collections.Generic;
using System.Net;
using Gtk;
using TraceLog;
using Translations;
using XDM.Core;
using XDM.Core.DataAccess;
using XDMApp = XDM.Core.Application;
using System.Linq;
using XDM.Core.BrowserMonitoring;
using XDM.Core.Util;
using XDM.GtkUI.Utils;

namespace XDM.GtkUI
{
    class Program
    {
        private const string DisableCachingName = @"TestSwitch.LocalAppContext.DisableCaching";
        private const string DontEnableSchUseStrongCryptoName = @"Switch.System.Net.DontEnableSchUseStrongCrypto";

        // Always-on crash sink: writes unhandled exceptions, unobserved task exceptions, and
        // the existing TraceLog stream to a single file. Without this, "app crashed after some
        // time" leaves zero evidence because the default Log.Debug only prints to a console
        // that may already be detached (tray/auto-start).
        private static readonly object CrashLogLock = new();
        private static readonly string CrashLogPath =
            System.IO.Path.Combine(Config.AppDir, "crash.log");

        // Keep the persistent crash capture small — trim only at write time (cap 5 MB,
        // keep the last 3 MB so the most recent context — often Settings or SNI — stays).
        private const long CrashLogMaxBytes = 5L * 1024 * 1024;
        private const long CrashLogKeepBytes = 3L * 1024 * 1024;

        private static void RotateCrashLogIfNeeded()
        {
            try
            {
                var info = new System.IO.FileInfo(CrashLogPath);
                if (!info.Exists || info.Length <= CrashLogMaxBytes) return;
                var text = System.IO.File.ReadAllText(CrashLogPath);
                if (text.Length <= CrashLogKeepBytes) return;
                var keep = text.Substring(text.Length - (int)CrashLogKeepBytes);
                var cut = keep.IndexOf('\n');
                if (cut >= 0) keep = keep.Substring(cut + 1);
                var header = $"[{DateTime.UtcNow:O}] crash.log rotated (kept last {CrashLogKeepBytes / (1024 * 1024)} MB)\n";
                System.IO.File.WriteAllText(CrashLogPath, header + keep);
            }
            catch
            {
                // Rotation is best-effort; a stale oversized log still contains evidence.
            }
        }

        private static void WriteCrashLine(string label, object detail)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Config.AppDir);
                RotateCrashLogIfNeeded();
                var line = $"[{DateTime.UtcNow:O}] {label}: {detail}";
                if (detail is Exception ex) line += Environment.NewLine + ex;
                line += Environment.NewLine;
                lock (CrashLogLock)
                {
                    System.IO.File.AppendAllText(CrashLogPath, line);
                }
            }
            catch
            {
                // Never let the crash sink itself crash the process.
            }
        }

        static void Main(string[] args)
        {
            Config.LoadConfig();
            try
            {
                Log.InitFileBasedTrace(System.IO.Path.Combine(Config.AppDir, "log.txt"));
            }
            catch { }
            // Catch exceptions on background threads and unobserved task faults — the most
            // common cause of "app silently disappears after some time" in this app.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                WriteCrashLine("AppDomain.UnhandledException (IsTerminating=" + e.IsTerminating + ")", e.ExceptionObject);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                WriteCrashLine("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
            Log.Debug("Application_Startup");
            WriteCrashLine("Startup", "pid=" + System.Diagnostics.Process.GetCurrentProcess().Id);
            Environment.SetEnvironmentVariable("GTK_USE_PORTAL", "1");
            Gtk.Application.Init("com.mayanktaker.fetchflow", ref args);
            GLib.Global.ProgramName = "fetchflow";
            GLib.Global.ApplicationName = "FetchFlow Download Manager";
            GLib.ExceptionManager.UnhandledException += ExceptionManager_UnhandledException;

            try
            {
                Gtk.Window.DefaultIconName = "com.mayanktaker.fetchflow";
                var iconList = new List<Gdk.Pixbuf>();
                int[] iconSizes = { 16, 32, 48, 64, 128, 256, 512 };
                foreach (var sz in iconSizes)
                {
                    var p = GtkHelper.LoadSvg("fetchflow-logo", sz);
                    if (p != null) iconList.Add(p);
                }
                var png512 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fetchflow-logo-512.png");
                if (System.IO.File.Exists(png512))
                {
                    try { iconList.Add(new Gdk.Pixbuf(png512)); } catch { }
                }
                if (iconList.Count > 0)
                {
                    Gtk.Window.DefaultIconList = iconList.ToArray();
                }
            }
            catch (Exception iconEx)
            {
                Log.Debug("Non-fatal: default icon list setup: " + iconEx.Message);
            }
            var globalStyleSheet = @"
                                    .large-font{ font-size: 16px; }
                                    .medium-font{ font-size: 14px; }
                                    ";

            // XDM theme layer: semantic buttons + surface colors, picked by dark-mode preference
            // Wayland/Phase1: Gdk.Screen.Default can throw MissingIntPtrCtorException under the
            // GTK3 Wayland backend (GtkSharp binding quirk). The CSS layers are cosmetic font
            // sizing and theming only, so guard them rather than block startup.
            try
            {
                var screen = Gdk.Screen.Default;
                if (screen != null)
                {
                    var provider = new CssProvider();
                    provider.LoadFromData(globalStyleSheet);
                    Gtk.StyleContext.AddProviderForScreen(screen, provider, 800);
                }
                ThemeManager.ApplyTheme(Config.Instance.ThemeMode == 2 ? null : (bool?)(Config.Instance.ThemeMode == 1));
            }
            catch (Exception cssEx)
            {
                Log.Debug("Non-fatal: global CSS provider not applied: " + cssEx.Message);
            }

            // TLS: secure by default; opt-in insecure validation via XDM_ALLOW_INSECURE_TLS=1
            if (Config.AllowInsecureTls)
            {
                ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => true;
            }
            ServicePointManager.DefaultConnectionLimit = 100;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;

            AppContext.SetSwitch(DisableCachingName, true);
            AppContext.SetSwitch(DontEnableSchUseStrongCryptoName, true);

            Log.Debug("Loading languages...");

            LoadLanguageTexts();

            var core = new ApplicationCore();
            var app = new XDMApp();
            var win = new MainWindow();

            Log.Debug("Configuring app context...");

            ApplicationContext.FirstRunCallback += ApplicationContext_FirstRunCallback;
            ApplicationContext.Configurer()
                .RegisterApplicationWindow(win)
                .RegisterApplication(app)
                .RegisterApplicationCore(core)
                .RegisterCapturedVideoTracker(new VideoTracker())
                .RegisterClipboardMonitor(new ClipboardMonitor())
                .RegisterLinkRefresher(new LinkRefresher())
                .RegisterPlatformUIService(new GtkPlatformUIService())
                .Configure();

            Log.Debug("Processing arguments...");

            ArgsProcessor.Process(args);

            Log.Debug("Gtk Run...");

            Gtk.Application.Run();
        }

        private static void ApplicationContext_FirstRunCallback(object? sender, EventArgs e)
        {
            PlatformHelper.EnableAutoStart(true);
        }

        private static void ExceptionManager_UnhandledException(GLib.UnhandledExceptionArgs args)
        {
            Log.Debug("GLib ExceptionManager_UnhandledException: " + args.ExceptionObject);
            // GLib gives us ExceptionObject (object); the real System.Exception usually lives
            // inside it. Persist whatever we can so the next crash is diagnosable.
            WriteCrashLine("GLib.ExceptionManager.UnhandledException", args.ExceptionObject);
            args.ExitApplication = false;
        }

        private static void LoadLanguageTexts()
        {
            Log.Debug("Language loading ...");
            try
            {
                var langDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang");
                var indexFile = System.IO.Path.Combine(langDir, "index.txt");
                var langFile = System.IO.Path.Combine(langDir, "English.txt");

                if (System.IO.File.Exists(indexFile))
                {
                    var lines = System.IO.File.ReadAllLines(indexFile);
                    foreach (var line in lines)
                    {
                        var index = line.IndexOf("=");
                        if (index > 0)
                        {
                            var name = line.Substring(0, index).Trim();
                            var value = line.Substring(index + 1).Trim();
                            if (string.Equals(name, Config.Instance.Language, StringComparison.OrdinalIgnoreCase))
                            {
                                langFile = System.IO.Path.Combine(langDir, value);
                                break;
                            }
                        }
                    }
                }

                if (System.IO.File.Exists(langFile))
                {
                    TextResource.Load(langFile);
                }
                else
                {
                    var fallback = System.IO.Path.Combine(langDir, "English.txt");
                    if (System.IO.File.Exists(fallback))
                    {
                        TextResource.Load(fallback);
                    }
                }
                Log.Debug("Language loaded.");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
        }
    }
}
