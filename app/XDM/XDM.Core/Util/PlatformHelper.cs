using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using TraceLog;

namespace XDM.Core.Util
{
    public static class PlatformHelper
    {
        // Launch-at-login identifiers. Kept as tokens (no magic strings) and reused by every OS branch.
        // Aliases onto the shared tokens in AutoStartEntry, so the app and the Inno Setup
        // script can never drift apart (a mismatch silently resurfaces as double-launch
        // or an untick that does nothing).
        public const string AutoStartRegistryValue = AutoStartEntry.WindowsRunValueName;
        public const string LegacyAutoStartRegistryValue = AutoStartEntry.LegacyWindowsRunValueName;
        private const string LinuxAutoStartFileName = AutoStartEntry.LinuxDesktopFileName;
        private const string LegacyLinuxAutoStartFileName = AutoStartEntry.LegacyLinuxDesktopFileName;

        // Tray glyph for StatusNotifierItem hosts; also installed into the user icon theme on startup.
        public const string TrayIconName = "fetchflow-tray";

        public static bool IsFirstRun()
        {
            var firstRunFile = Path.Combine(Config.AppDir, "xdm-" + AppInfo.APP_VERSION + ".first-run");
            if (!File.Exists(firstRunFile))
            {
                try
                {
                    File.Create(firstRunFile).Close();
                }
                catch { }
                return true;
            }
            return false;
        }

        public static bool KillAll(string processName, out string? processExecutable)
        {
            processExecutable = null;
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes != null)
                {
                    foreach (var proc in processes)
                    {
                        try
                        {
                            if (processExecutable == null)
                            {
                                processExecutable = proc.MainModule?.FileName;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, ex.Message);
                        }

                        try
                        {
                            proc.Kill();
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, ex.Message);
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
            return false;
        }

        public static string GetOsDefaultDownloadFolder()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return Win32NativeMethods.GetDownloadDirectoryPath();
            }
            else
            {
#if NET5_0_OR_GREATER
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#else
                throw new PlatformNotSupportedException("Please use program which was compiled for dotnet5");
#endif
            }
        }

        public static void OpenBrowser(string url)
        {
            var os = Environment.OSVersion.Platform;
            switch (os)
            {
                case PlatformID.Win32NT:
                    var psiShellEx = new ProcessStartInfo
                    {
                        FileName = url,
                    };
                    psiShellEx.UseShellExecute = true;
                    Log.Debug($"Shell execute: {url}");
                    Process.Start(psiShellEx);
                    break;
#if NET5_0_OR_GREATER
                case PlatformID.Unix:
                    var psi = new ProcessStartInfo
                    {
                        FileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open"
                    };
                    psi.Arguments = "\"" + url + "\"";
                    Log.Debug($"{psi.FileName} {psi.Arguments}");
                    Process.Start(psi);
                    break;
#endif
            }
        }

        public static bool OpenFile(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }
            try
            {
                var os = Environment.OSVersion.Platform;
                switch (os)
                {
                    case PlatformID.Win32NT:
                        var psiShellEx = new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        };
                        Log.Debug($"Shell execute: {path}");
                        Process.Start(psiShellEx);
                        return true;
#if NET5_0_OR_GREATER
                    case PlatformID.Unix:
                        var psi = new ProcessStartInfo
                        {
                            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open"
                        };
                        psi.Arguments = "\"" + path + "\"";
                        Log.Debug($"{psi.FileName} {psi.Arguments}");
                        Process.Start(psi);
                        return true;
#endif
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "OpenFile");
            }
            return false;
        }

        public static bool OpenFolder(string path, string? file = null)
        {
            if (file != null)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        var os = Environment.OSVersion.Platform;
                        switch (os)
                        {
                            case PlatformID.Win32NT:
                                {
                                    var psi = new ProcessStartInfo
                                    {
                                        FileName = "explorer",
                                    };
                                    psi.Arguments = $"/select, \"{Path.Combine(path, file)}\"";
                                    Log.Debug($"{psi.FileName} {psi.Arguments}");
                                    Process.Start(psi);
                                    return true;
                                }
#if NET5_0_OR_GREATER
                            case PlatformID.Unix:
                                {
                                    var psi = new ProcessStartInfo
                                    {
                                        FileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open"
                                    };
                                    psi.Arguments = $"\"{path}\"";
                                    Log.Debug($"{psi.FileName} {psi.Arguments}");
                                    Process.Start(psi);
                                    return true;
                                }
#endif
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "OpenFolder");
                    }
                }
                return false;
            }
            return OpenFile(path);
        }

        private static string FindChromeExecutableFromRegistry()
        {
            try
            {
                using var regKey = Registry.ClassesRoot.OpenSubKey(@"ChromeHTML\shell\open\command");
                return FileHelper.GetFileNameFromQuote((string)regKey.GetValue(null));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error fetching chrome location from registry");
            }
            return null;
        }

        public static string GetChromeExecutable()
        {
            var os = Environment.OSVersion.Platform;
            switch (os)
            {
                case PlatformID.Win32NT:
                    var chromeExe = FindChromeExecutableFromRegistry();
                    if (!string.IsNullOrEmpty(chromeExe))
                    {
                        return chromeExe;
                    }
                    var suffix = "Google\\Chrome\\Application\\chrome.exe";
                    foreach (var path in new[] {
                        Path.Combine(Environment.GetEnvironmentVariable("PROGRAMFILES",
                            EnvironmentVariableTarget.Machine),suffix),
                        Path.Combine(Environment.GetEnvironmentVariable("PROGRAMFILES(X86)",
                            EnvironmentVariableTarget.Machine),suffix),
                        Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA",
                            EnvironmentVariableTarget.User),suffix),
                    })
                    {
                        if (File.Exists(path)) return path;
                    }
                    return null;
#if NET5_0_OR_GREATER
                case PlatformID.Unix:
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        const string macChromeExe = "/Applications/Google Chrome.app";
                        if (File.Exists(macChromeExe))
                        {
                            return macChromeExe;
                        }
                    }
                    else
                    {
                        const string linuxChromeExe = "/usr/bin/google-chrome";
                        if (File.Exists(linuxChromeExe))
                        {
                            return linuxChromeExe;
                        }
                    }
                    break;
#endif
            }
            Log.Debug("Chrome executable not found!");
            return null;
        }

        public static string GetFireFoxExecutable()
        {
            var os = Environment.OSVersion.Platform;
            switch (os)
            {
                case PlatformID.Win32NT:
                    var suffix = "Mozilla Firefox\\firefox.exe";
                    foreach (var path in new[] {
                        Path.Combine(Environment.GetEnvironmentVariable("PROGRAMFILES",
                            EnvironmentVariableTarget.Machine),suffix),
                        Path.Combine(Environment.GetEnvironmentVariable("PROGRAMFILES(X86)",
                            EnvironmentVariableTarget.Machine),suffix),
                        Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA",
                            EnvironmentVariableTarget.User),suffix),
                    })
                    {
                        if (File.Exists(path)) return path;
                    }
                    return null;
#if NET5_0_OR_GREATER
                case PlatformID.Unix:
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        const string macFireFoxExe = "/Applications/Firefox.app";
                        if (File.Exists(macFireFoxExe))
                        {
                            return macFireFoxExe;
                        }
                    }
                    else
                    {
                        const string linuxFireFoxExe = "/usr/bin/firefox";
                        if (File.Exists(linuxFireFoxExe))
                        {
                            return linuxFireFoxExe;
                        }
                    }
                    break;
#endif
            }
            Log.Debug("Firefox executable not found!");
            return null;
        }

        public static string GetEdgeExecutable()
        {
            var os = Environment.OSVersion.Platform;
            switch (os)
            {
                case PlatformID.Win32NT:
                    var suffix = @"Edge\Application\msedge.exe";
                    foreach (var path in new[] {
                        Path.Combine(Environment.GetEnvironmentVariable("PROGRAMFILES",
                            EnvironmentVariableTarget.Machine),suffix),
                        Path.Combine(Environment.GetEnvironmentVariable("PROGRAMFILES(X86)",
                            EnvironmentVariableTarget.Machine),suffix),
                        Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA",
                            EnvironmentVariableTarget.User),suffix),
                    })
                    {
                        if (File.Exists(path)) return path;
                    }
                    break;
            }

            return null;
        }

        // Adds/removes the per-user launch-at-login entry (Windows Run key, Linux autostart .desktop).
        // Returns true only when the requested state was actually applied.
        public static bool EnableAutoStart(bool enable)
        {
            try
            {
                var os = Environment.OSVersion.Platform;
                if (os == PlatformID.Win32NT)
                {
                    using var hkcuRun = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                    if (enable)
                    {
                        var appExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fetchflow.exe");
                        if (!File.Exists(appExe)) return false;
                        hkcuRun.SetValue(AutoStartRegistryValue, $"\"{appExe}\" --background", RegistryValueKind.String);

                        // The Run key is now the only autostart mechanism; drop the old Startup
                        // shortcut so enabling from Settings cannot launch the app twice.
                        RemoveLegacyStartupShortcut();
                    }
                    else
                    {
                        hkcuRun.DeleteValue(AutoStartRegistryValue, false);
                        hkcuRun.DeleteValue(LegacyAutoStartRegistryValue, false);

                        // Untick must also clear the installer-created shortcut, otherwise
                        // launch-at-login silently stayed on.
                        RemoveLegacyStartupShortcut();
                    }
                    return true;
                }
#if NET5_0_OR_GREATER
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // No macOS packaging exists yet — report "not enabled" instead of claiming success.
                    Log.Debug("AutoStart: macOS launch agent not implemented; reporting disabled.");
                    return false;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var autoStartDir = GetLinuxDesktopAutoStartDir();
                    var desktopFile = Path.Combine(autoStartDir, LinuxAutoStartFileName);
                    if (!enable)
                    {
                        // Disabling must actually remove the entry — writing it unconditionally
                        // left launch-at-login switched on even after the user unticked the box.
                        if (File.Exists(desktopFile)) File.Delete(desktopFile);
                        var legacy = Path.Combine(autoStartDir, LegacyLinuxAutoStartFileName);
                        if (File.Exists(legacy)) File.Delete(legacy);
                        return !File.Exists(desktopFile);
                    }
                    if (!Directory.Exists(autoStartDir)) Directory.CreateDirectory(autoStartDir);
                    File.WriteAllText(desktopFile, GetLinuxDesktopFile());
                    SetExecutable(desktopFile);
                    return true;
                }
#endif
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
            return false;
        }
#if NET5_0_OR_GREATER
        public static string GetLinuxDesktopAutoStartDir()
        {
            var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(configDir))
            {
                configDir = Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "~", ".config");
            }
            return Path.Combine(configDir, "autostart");
        }

        public static string GetLinuxDesktopFile()
        {
            var appPath = GetExecutablePath();
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fetchflow-logo.svg");
            return DesktopEntry.BuildAutoStartEntry(appPath, iconPath);
        }

        // Absolute path of the running executable, so the autostart entry keeps pointing at a
        // real binary instead of a relative guess that breaks when the install location changes.
        public static string GetExecutablePath()
        {
            var fromProcess = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(fromProcess)) return fromProcess;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fetchflow");
        }

        // True when the autostart .desktop exists AND still points at a binary that is on disk.
        // A file that merely *mentions* fetchflow (stale path from a moved/removed install) is dead.
        public static bool IsLinuxAutoStartEntryLive()
        {
            try
            {
                var file = Path.Combine(GetLinuxDesktopAutoStartDir(), LinuxAutoStartFileName);
                if (!File.Exists(file)) return false;
                return DesktopEntry.IsEntryLive(File.ReadAllText(file), File.Exists);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
            return false;
        }

        // Copies the tray glyph into the per-user hicolor theme when the system theme lacks it.

        // Packages already install it system-wide; this repairs existing installs (and distros
        // whose package predates the glyph) without needing root.
        public static bool EnsureTrayIconInstalled()
        {
            try
            {
                var source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "svg-icons", TrayIconName + ".svg");

                // Icons live under XDG_DATA_HOME (~/.local/share/icons), NOT SpecialFolder.
                // ApplicationData — on Linux that maps to ~/.config, which GTK never searches.
                var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (string.IsNullOrEmpty(dataHome))
                {
                    var home = Environment.GetEnvironmentVariable("HOME");
                    if (string.IsNullOrEmpty(home)) return false;
                    dataHome = Path.Combine(home, ".local", "share");
                }
                var targetDir = Path.Combine(dataHome, "icons", "hicolor", "scalable", "apps");
                var target = Path.Combine(targetDir, TrayIconName + ".svg");

                var srcInfo = new FileInfo(source);
                var needsCopy = TrayIconPolicy.NeedsInstall(
                    srcInfo.Exists,
                    File.Exists(target),
                    File.Exists(target) && new FileInfo(target).Length != srcInfo.Length,
                    File.Exists(target) && File.GetLastWriteTimeUtc(target) < srcInfo.LastWriteTimeUtc);

                if (needsCopy)
                {
                    Directory.CreateDirectory(targetDir);
                    File.Copy(source, target, true);
                    Log.Debug($"Tray icon: installed '{TrayIconName}' into user icon theme ({target}).");
                }
                return File.Exists(target);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
            return false;
        }

#endif


        /// <summary>
        /// True when this platform can actually honour "start at login". macOS has no packaging


        /// <summary>
        /// True when this platform can actually honour "start at login". macOS has no packaging
        /// and no LaunchAgent writer, so Settings hides the checkbox there instead of offering a
        /// control that silently does nothing.
        /// </summary>
        public static bool SupportsAutoStart
        {
            get
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT) return true;
#if NET5_0_OR_GREATER
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return false;
                return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#else
                return true;
#endif
            }
        }

        // Legacy Windows autostart: older installers created a Startup-folder shortcut while
        // Settings wrote an HKCU Run key. The two mechanisms disagreed (duplicate launches, and
        // an untick that left autostart on), so the Run key is now the single source of truth
        // and these helpers migrate leftovers off the shortcut.
        private static string LegacyStartupShortcutPath()
        {
            var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return string.IsNullOrEmpty(startup) ? string.Empty : Path.Combine(startup, "FetchFlow Download Manager.lnk");
        }

        // Deletes the legacy Startup-folder shortcut if present. No-op on non-Windows.
        public static bool RemoveLegacyStartupShortcut()
        {
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;
                var path = LegacyStartupShortcutPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                File.Delete(path);
                Log.Debug("AutoStart: removed legacy Startup-folder shortcut.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
            return false;
        }

        // True when an older installer left a Startup-folder shortcut behind.
        public static bool HasLegacyStartupShortcut()
        {
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;
                var path = LegacyStartupShortcutPath();
                return !string.IsNullOrEmpty(path) && File.Exists(path);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
            return false;
        }

        // One-shot upgrade migration, safe to call on every launch. The current installer writes
        // the HKCU Run key, and Inno Setup re-applies [Registry] on upgrade, so after an upgrade
        // the Run key already reflects the user's chosen task state. Any Startup-folder shortcut
        // left by an older install is therefore redundant: keep it and the app launches twice.
        // Deleting it on startup means an upgrade is self-healing instead of waiting for the
        // user to toggle the setting. No-op on non-Windows.
        public static bool MigrateLegacyAutoStart()
        {
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;
                if (!HasLegacyStartupShortcut()) return false;
                if (RemoveLegacyStartupShortcut())
                {
                    Log.Debug("AutoStart: migrated legacy Startup shortcut to the HKCU Run key.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
            return false;
        }

        public static string GetAppPlatform()
        {
            var os = Environment.OSVersion.Platform;
            if (os == PlatformID.Win32NT)
            {
                return "Windows";
            }
#if NET5_0_OR_GREATER
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "MacOSX";
            }
#endif
            return "UnsupportedOS";
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                var os = Environment.OSVersion.Platform;
                if (os == PlatformID.Win32NT)
                {
                    using (var hkcuRun = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        var command = (string?)hkcuRun?.GetValue(AutoStartRegistryValue)
                            ?? (string?)hkcuRun?.GetValue(LegacyAutoStartRegistryValue);
                        var path = FileHelper.GetFileNameFromQuote(command);
                        if (!string.IsNullOrEmpty(path)) return true;
                    }

                    // An older installer's Startup-folder shortcut also means "on" — reporting
                    // otherwise made the checkbox read OFF while the app still launched at login.
                    return HasLegacyStartupShortcut();
                }
#if NET5_0_OR_GREATER
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Honest state: the entry counts as enabled only when its target binary exists.
                    // A stale entry (moved/removed install) used to read as "on" while doing nothing.
                    if (IsLinuxAutoStartEntryLive()) return true;

                    // Legacy entry from older builds: accept it only if its command is still live.
                    var legacy = Path.Combine(GetLinuxDesktopAutoStartDir(), LegacyLinuxAutoStartFileName);
                    if (!File.Exists(legacy)) return false;
                    return DesktopEntry.IsEntryLive(File.ReadAllText(legacy), File.Exists);
                }
#endif
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }

            return false;
        }

        public static void SpawnSubProcess(string executable,
            string[]? args = null,
            bool useShellExecute = false,
            bool createNoWindow = true)
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = useShellExecute,
                CreateNoWindow = createNoWindow
            };
            if (args != null && args.Length > 0)
            {
                psi.Arguments = string.Join(" ", args);
            }
            Log.Debug($"{psi.FileName} ${psi.Arguments}");
            Process.Start(psi);
        }

        public static void ShutDownPC()
        {
            //dbus-send --system --print-reply --dest=org.freedesktop.login1 /org/freedesktop/login1 "org.freedesktop.login1.Manager.PowerOff" boolean:true
            //https://gitlab.xfce.org/xfce/xfce4-power-manager/-/blob/master/src/xfpm-systemd.c
            //https://askubuntu.com/questions/454039/what-command-is-executed-when-shutdown-from-the-graphical-menu-in-14-04
            Log.Debug("Issuing shutdown command...");
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    SpawnSubProcess("shutdown", new string[] { "/t", "30", "/s" });
                    break;
#if NET5_0_OR_GREATER
                case PlatformID.Unix:
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        var cmd = "org.freedesktop.login1.Manager.PowerOff";
                        SpawnSubProcess("dbus-send", new string[] { "--system", "--print-reply",
                            "--dest=org.freedesktop.login1", "/org/freedesktop/login1", $"\"{cmd}\"","boolean:true"});
                        return;
                    }
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        var cmd = "tell app \"System Events\" to shut down";
                        SpawnSubProcess("osascript", new string[] { "-e", $"\"{cmd}\"" });
                    }
                    break;
#endif
                default:
                    Log.Debug("Operating system not supported");
                    break;
            }
        }

        public static void SendKeepAlivePing()
        {
            Log.Debug("Keep alive ping...");
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    SetThreadExecutionState(EXECUTION_STATE.ES_SYSTEM_REQUIRED);
                    break;
#if NET5_0_OR_GREATER
                case PlatformID.Unix:
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        SpawnSubProcess("dbus-send", new string[] { "--print-reply --type=method_call",
                            "--dest=org.freedesktop.ScreenSaver /ScreenSaver org.freedesktop.ScreenSaver.SimulateUserActivity"});
                        return;
                    }
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        SpawnSubProcess("caffeinate", new string[] { "-i -t 3" });
                    }
                    break;
#endif
                default:
                    Log.Debug("Operating system not supported");
                    break;
            }
        }

        public static void RunCommand(string cmd)
        {
            Log.Debug("Running command: " + cmd);
            SpawnSubProcess(FileHelper.QuoteFilePathIfNeeded(cmd));
        }

        public static void RunAntivirus(string cmd, string options, string file)
        {
            Log.Debug("Running antivirus: " + cmd + " " + options + " " + file);
            SpawnSubProcess(FileHelper.QuoteFilePathIfNeeded(cmd), new string[] { options, FileHelper.QuoteFilePathIfNeeded(file) });
        }

        public static string? FindExecutableFromSystemPath(string executableName)
        {
            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var spath in values?.Split(Path.PathSeparator) ?? new string[] { string.Empty })
            {
                var fullPath = Path.Combine(spath, executableName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        public static void OpenWindowsProxySettings()
        {
            if (Environment.OSVersion.Version.Major == 10)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ms-settings:network-proxy",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            else
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = "inetcpl.cpl,LaunchConnectionDialog",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
        }

        public static bool SetExecutable(string path)
        {
            const int _0755 =
            S_IRUSR | S_IXUSR | S_IWUSR
            | S_IRGRP | S_IXGRP
            | S_IROTH | S_IXOTH;
            return (chmod(Path.GetFullPath(path), (int)_0755) == 0);
        }

        [FlagsAttribute]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
            // Legacy flag, should not be used.
            // ES_USER_PRESENT = 0x00000004
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        // user permissions
        const int S_IRUSR = 0x100;
        const int S_IWUSR = 0x80;
        const int S_IXUSR = 0x40;

        // group permission
        const int S_IRGRP = 0x20;
        const int S_IWGRP = 0x10;
        const int S_IXGRP = 0x8;

        // other permissions
        const int S_IROTH = 0x4;
        const int S_IWOTH = 0x2;
        const int S_IXOTH = 0x1;

        [DllImport("libc", SetLastError = true)]
        public static extern int chmod(string pathname, int mode);

    }
}
