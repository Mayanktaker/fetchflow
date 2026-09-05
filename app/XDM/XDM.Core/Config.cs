// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TraceLog;
using XDM.Core.IO;
using XDM.Core.Util;

namespace XDM.Core
{
    public class Config
    {
        private static Config instance;
        private static object lockObj = new();
        public static Config Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObj)
                    {
                        if (instance == null)
                        {
                            LoadConfig();
                        }
                    }
                }

                return instance!;
            }

            private set
            {
                instance = value;
            }
        }

        public static string DataDir { get; set; }
        public static string AppDir { get; set; }

        // TLS security gate: opt-in insecure validation via FETCHFLOW_ALLOW_INSECURE_TLS (legacy XDM_ALLOW_INSECURE_TLS still works)
        public static bool AllowInsecureTls =>
            (Environment.GetEnvironmentVariable("FETCHFLOW_ALLOW_INSECURE_TLS") ?? Environment.GetEnvironmentVariable("XDM_ALLOW_INSECURE_TLS")) == "1";

        // IPC port for browser-monitoring HTTP relay; env-overridable, default 8597 (Phase2.3)
        public static int IpcPort =>
            int.TryParse(Environment.GetEnvironmentVariable("FETCHFLOW_IPC_PORT") ?? Environment.GetEnvironmentVariable("XDM_IPC_PORT"), out var p) && p > 0 ? p : 8597;

        // Ports scanned above IpcPort for the relay bind and the instance-forwarding probe
        public const int IpcPortRangeSize = 7;

        public static int DefaultNotificationTimeOut => 30000;

        public int NotificationTimeOut { get; set; }

        public bool IsBrowserMonitoringEnabled { get; set; } = true;

        public static bool DefaultShowNotification => true;

        public bool ShowNotification { get; set; } = true;

        public static string DefaultFallbackUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/97.0.4692.99 Safari/537.36";

        public string FallbackUserAgent { get; set; } = DefaultFallbackUserAgent;

        public static string[] DefaultVideoExtensions => new string[]
            {
                "MP4", "MKV", "WEBM", "M3U8", "F4M", "MPD", "TS", "MTS", "M2TS", "FLV", "F4V",
                "AVI", "MOV", "WMV", "MPG", "MPEG", "VOB", "DIVX", "XVID", "3GP", "3G2", "OGV", "RMVB"
            };

        public string[] VideoExtensions { get; set; }

        public static string[] DefaultFileExtensions => new string[]
            {
                "3GP", "7Z", "AAC", "ACE", "AI", "AIFF", "ALAC", "APK", "APPIMAGE", "AVI", "AVIF", "AZW3",
                "BIN", "BMP", "BZ2", "CAB", "CBR", "CBZ", "CSV", "DEB", "DJVU", "DMG", "DOC", "DOCX",
                "EPUB", "EXE", "F4V", "FLAC", "FLATPAK", "FLATPAKREF", "FLV", "GIF", "GZ", "HEIC", "HEIF",
                "ICO", "IMG", "ISO", "JAR", "JPEG", "JPG", "KEY", "M2TS", "M4A", "M4V", "MD", "MID",
                "MIDI", "MKV", "MOBI", "MOV", "MP3", "MP4", "MPEG", "MPG", "MSI", "MSIX", "ODP", "ODS",
                "ODT", "OGA", "OGG", "OGV", "OPUS", "PAGES", "PDF", "PKG", "PNG", "PPT", "PPTX", "PSD",
                "QCOW2", "RAR", "RAW", "RPM", "RTF", "RUN", "SH", "SIT", "SITX", "SNAP", "SVG", "TAR",
                "TGZ", "TIFF", "TS", "TXT", "VHD", "VHDX", "VMDK", "VOB", "WAV", "WEBM", "WEBP", "WMA",
                "WMV", "XLS", "XLSX", "XZ", "ZIP", "ZIPX", "ZST"
            };

        public string[] FileExtensions { get; set; }

        public static string[] DefaultBlockedHosts => new string[]
            {
                "update.microsoft.com","windowsupdate.com","thwawte.com"
            };

        public string[] BlockedHosts { get; set; }

        public string Language { get; set; } = "English";

        public bool AllowSystemDarkTheme { get; set; } = true;
        // ThemeMode: 0 = Light, 1 = Dark, 2 = Follow System
        public int ThemeMode { get; set; } = 2;
        // ColorScheme: 0 = Default, 1 = Palette 1, 2 = Palette 2, 3 = Palette 3
        // ColorScheme: -1 = unset (per-mode default: Nord Emerald dark, Nordic Frost light);
        // 0..6 = explicit user choice, persisted as-is.
        public int ColorScheme { get; set; } = -1;

        private Config()
        {
            VideoExtensions = DefaultVideoExtensions;
            FileExtensions = DefaultFileExtensions;
            BlockedHosts = DefaultBlockedHosts;
            if(Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                AllowSystemDarkTheme = Environment.OSVersion.Version.Major >= 10;
            }
        }

        public List<string> RecentFolders { get; set; } = new List<string>();

        public FolderSelectionMode FolderSelectionMode { get; set; }

        public FileConflictResolution FileConflictResolution { get; set; }

        public int MaxRetry { get; set; } = 10;

        public int RetryDelay { get; set; } = 10;

        public int MaxParallelDownloads { get; set; } = 3;

        public bool ShowProgressWindow { get; set; } = true;

        public bool ShowDownloadCompleteWindow { get; set; } = true;

        // Auto-dismiss duration in seconds for download complete dialog (0 = disabled)
        public int AutoDismissCompleteDialogSeconds { get; set; } = 10;

        // Plays system sound on download completion
        public bool PlayCompletionSound { get; set; } = true;

        // Whether the sidebar categories section is expanded
        public bool CategoriesExpanded { get; set; } = true;

        public bool StartDownloadAutomatically { get; set; } = false;

        public bool FetchServerTimeStamp { get; set; } = false;

        public bool MonitorClipboard { get; set; } = false;

        public int MinVideoSize { get; set; } = 1 * 1024;

        public string TempDir { get; set; }

        public int NetworkTimeout { get; set; } = 30;

        public int MaxSegments { get; set; } = 8;

        public int DefaltDownloadSpeed { get; set; } = 0;

        public bool EnableSpeedLimit { get; set; } = false;

        public bool ShutdownAfterAllFinished { get; set; } = false;

        public bool KeepPCAwake { get; set; } = true;

        public bool RunCommandAfterCompletion { get; set; } = false;

        public string AfterCompletionCommand { get; set; }

        public bool ScanWithAntiVirus { get; set; } = false;

        public string AntiVirusExecutable { get; set; }

        public string AntiVirusArgs { get; set; }

        public ProxyInfo? Proxy { get; set; }

        public bool DoubleClickOpenFile { get; set; } = false;

        public long BlobMaxBytes { get; set; } = 256 * 1024 * 1024; // 256 MiB default cap for blob transfers

        public bool RunOnLogon
        {
            get => PlatformHelper.IsAutoStartEnabled();
            set => PlatformHelper.EnableAutoStart(value);
        }

        public string UserSelectedDownloadFolder { get; set; }

        // All FetchFlow downloads live under ~/Downloads/FetchFlow (with per-category
        // subfolders below) so app downloads never mix with the user's other files.
        public static string FetchFlowDownloadRoot =>
            Path.Combine(PlatformHelper.GetOsDefaultDownloadFolder(), "FetchFlow");

        public string DefaultDownloadFolder { get; set; } = FetchFlowDownloadRoot;

        // Download list sort: column is Name/Size/Date/Type, applied to both
        // Active and Complete lists and persisted across restarts
        public string DownloadSortColumn { get; set; } = "Date";
        public bool DownloadSortDescending { get; set; } = true;

        public static IEnumerable<Category> DefaultCategories = new[]
        {
            new Category
            {
                Name="CAT_DOCUMENTS",
                DisplayName="Document",
                FileExtensions=new HashSet<string>
                {
                    ".DOC", ".DOCX", ".DOCM", ".DOT", ".DOTX", ".PDF", ".ODT", ".OTT",
                    ".RTF", ".TXT", ".MD", ".TEX", ".LOG", ".PAGES", ".XLS", ".XLSX",
                    ".XLSM", ".XLSB", ".ODS", ".OTS", ".CSV", ".TSV", ".NUMBERS",
                    ".PPT", ".PPTX", ".PPS", ".PPSX", ".ODP", ".OTP", ".KEY",
                    ".EPUB", ".MOBI", ".AZW", ".AZW3", ".FB2", ".CBZ", ".CBR", ".DJVU"
                },
                DefaultFolder=Path.Combine(FetchFlowDownloadRoot, "Documents"),
                IsPredefined=true
            },
            new Category
            {
                Name="CAT_MUSIC",
                DisplayName="Music",
                FileExtensions=new HashSet<string>
                {
                    ".MP3", ".AAC", ".FLAC", ".ALAC", ".WAV", ".AIFF", ".AIF", ".APE",
                    ".M4A", ".OGG", ".OGA", ".OPUS", ".WMA", ".MPA", ".AMR", ".AC3",
                    ".DTS", ".EAC3", ".MKA", ".MID", ".MIDI", ".WV", ".TTA"
                },
                DefaultFolder=Path.Combine(FetchFlowDownloadRoot, "Music"),
                IsPredefined=true
            },
            new Category
            {
                Name="CAT_VIDEOS",
                DisplayName="Video",
                FileExtensions=new HashSet<string>
                {
                    ".MP4", ".MKV", ".WEBM", ".AVI", ".MOV", ".WMV", ".FLV", ".M4V",
                    ".F4V", ".TS", ".MTS", ".M2TS", ".TP", ".TRP", ".MPG", ".MPEG",
                    ".M2V", ".MPV", ".VOB", ".DIVX", ".XVID", ".3GP", ".3G2", ".OGV",
                    ".RM", ".RMVB", ".ASF"
                },
                DefaultFolder=Path.Combine(FetchFlowDownloadRoot, "Video"),
                IsPredefined=true
            },
            new Category
            {
                Name="CAT_COMPRESSED",
                DisplayName="Compressed",
                FileExtensions=new HashSet<string>
                {
                    ".7Z", ".ZIP", ".ZIPX", ".RAR", ".TAR", ".GZ", ".TGZ", ".BZ2",
                    ".TBZ2", ".XZ", ".TXZ", ".ZST", ".TZST", ".LZ", ".LZ4", ".LZH",
                    ".CAB", ".SIT", ".SITX", ".ACE", ".ARJ", ".Z", ".ISO", ".IMG",
                    ".MDF", ".NRG", ".VHD", ".VHDX", ".VMDK", ".QCOW2"
                },
                DefaultFolder=Path.Combine(FetchFlowDownloadRoot, "Compressed"),
                IsPredefined=true
            },
            new Category
            {
                Name="CAT_PROGRAMS",
                DisplayName="Application",
                FileExtensions=new HashSet<string>
                {
                    ".APPIMAGE", ".DEB", ".RPM", ".FLATPAKREF", ".FLATPAK", ".SNAP",
                    ".APK", ".RUN", ".BIN", ".SH", ".EXE", ".MSI", ".MSIX", ".APPX",
                    ".BAT", ".CMD", ".PKG", ".DMG", ".JAR", ".WAR"
                },
                DefaultFolder=Path.Combine(FetchFlowDownloadRoot, "Programs"),
                IsPredefined=true
            },
            new Category
            {
                Name="CAT_IMAGES",
                DisplayName="Image",
                FileExtensions=new HashSet<string>
                {
                    ".JPG", ".JPEG", ".PNG", ".GIF", ".WEBP", ".SVG", ".SVGZ", ".BMP",
                    ".ICO", ".TIFF", ".TIF", ".AVIF", ".HEIC", ".HEIF", ".PSD", ".AI",
                    ".EPS", ".RAW", ".CR2", ".NEF", ".DNG", ".XCF"
                },
                DefaultFolder=Path.Combine(FetchFlowDownloadRoot, "Pictures"),
                IsPredefined=true
            }
        };

        public IEnumerable<Category> Categories = DefaultCategories;

        public IEnumerable<PasswordEntry> UserCredentials { get; set; } = new List<PasswordEntry>();

        public static void LoadConfig(string? path = null)
        {
            Log.Debug("Loading config...");

#if NET35
            DataDir = path ?? Path.Combine(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".xdm-app-data"), "Data");
            AppDir = path ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".xdm-app-data");
#else
            // Wayland/Phase1.5: explicit path wins; otherwise honor XDG_CONFIG_HOME /
            // XDG_DATA_HOME when set (sandbox/relocatable), defaulting to legacy ~/.xdm-app-data.
            if (path != null)
            {
                AppDir = path;
                DataDir = Path.Combine(path, "Data");
            }
            else
            {
                var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var legacyRoot = Path.Combine(userProfile, ".xdm-app-data");
                var fetchflowRoot = Path.Combine(userProfile, ".fetchflow-app-data");
                var rootDir = Directory.Exists(fetchflowRoot) ? fetchflowRoot : (Directory.Exists(legacyRoot) ? legacyRoot : fetchflowRoot);
                AppDir = !string.IsNullOrEmpty(xdgConfig) ? Path.Combine(xdgConfig, "fetchflow") : rootDir;
                DataDir = !string.IsNullOrEmpty(xdgData) ? Path.Combine(xdgData, "fetchflow") : Path.Combine(rootDir, "Data");
            }
#endif
            instance = new Config
            {
                TempDir = Path.Combine(DataDir, "temp")
            };
            try
            {
                if (!Directory.Exists(DataDir))
                {
                    Directory.CreateDirectory(DataDir);
                }

                var bytes = TransactedIO.ReadBytes("settings.dat", AppDir);
                if (bytes != null)
                {
                    using var ms = new MemoryStream(bytes);
                    using var reader = new BinaryReader(ms);
                    ConfigIO.DeserializeConfig(instance, reader);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }


            //var json = TransactedIO.Read("settings.json", Config.DataDir);
            //Config? instance = null;
            //if (json != null)
            //{
            //    instance = JsonConvert.DeserializeObject<Config>(
            //                json, new JsonSerializerSettings
            //                {
            //                    MissingMemberHandling = MissingMemberHandling.Ignore,
            //                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            //                });
            //}
            //if (instance == null)
            //{
            //    Instance = new Config
            //    {
            //        TempDir = Path.Combine(Config.DataDir, "temp")
            //    };
            //}
            //else
            //{
            //    Instance = instance;
            //}

            //var path = Path.Combine(Config.DataDir, "settings.json");
            //if (File.Exists(path))
            //{
            //    try
            //    {
            //        var instance = JsonConvert.DeserializeObject<Config>(
            //                File.ReadAllText(path), new JsonSerializerSettings
            //                {
            //                    MissingMemberHandling = MissingMemberHandling.Ignore,
            //                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            //                });
            //        if (instance != null)
            //        {
            //            Instance = instance;
            //            return;
            //        }
            //    }
            //    catch (Exception exx)
            //    {
            //        Log.Debug(exx, "Error loading config");
            //    }
            //}
            //Instance = new Config
            //{
            //    TempDir = Path.Combine(Config.DataDir, "temp")
            //};
        }

        //private static void PopulateConfig32(Config instance, BinaryReader r)
        //{
        //    instance.AfterCompletionCommand = XDM.Messaging.StreamHelper.ReadString(r);
        //    instance.AntiVirusArgs = XDM.Messaging.StreamHelper.ReadString(r);
        //    instance.AntiVirusExecutable = XDM.Messaging.StreamHelper.ReadString(r);
        //    var count = r.ReadInt32();
        //    instance.BlockedHosts = new string[count];
        //    for (int i = 0; i < count; i++)
        //    {
        //        instance.BlockedHosts[i] = r.ReadString();
        //    }
        //    count = r.ReadInt32();
        //    var list = new List<Category>(count);
        //    for (int i = 0; i < count; i++)
        //    {
        //        var category = new Category
        //        {
        //            DefaultFolder = XDM.Messaging.StreamHelper.ReadString(r),
        //            DisplayName = XDM.Messaging.StreamHelper.ReadString(r),
        //            FileExtensions = new HashSet<string>(),
        //        };
        //        var c2 = r.ReadInt32();
        //        for (int j = 0; j < c2; j++)
        //        {
        //            category.FileExtensions.Add(r.ReadString());
        //        }
        //        category.IsPredefined = r.ReadBoolean();
        //        category.Name = r.ReadString();
        //        list.Add(category);
        //    }
        //    instance.Categories = list;
        //    instance.DefaultDownloadFolder = XDM.Messaging.StreamHelper.ReadString(r);
        //    instance.EnableSpeedLimit = r.ReadBoolean();
        //    instance.FetchServerTimeStamp = r.ReadBoolean();
        //    instance.FileConflictResolution = (FileConflictResolution)r.ReadInt32();
        //    count = r.ReadInt32();
        //    instance.FileExtensions = new string[count];
        //    for (int i = 0; i < count; i++)
        //    {
        //        instance.FileExtensions[i] = r.ReadString();
        //    }
        //    instance.FolderSelectionMode = (FolderSelectionMode)r.ReadInt32();
        //    instance.DefaltDownloadSpeed = r.ReadInt32();
        //    instance.IsBrowserMonitoringEnabled = r.ReadBoolean();
        //    instance.KeepPCAwake = r.ReadBoolean();
        //    instance.Language = r.ReadString();
        //    instance.MaxParallelDownloads = r.ReadInt32();
        //    instance.MaxRetry = r.ReadInt32();
        //    instance.MaxSegments = r.ReadInt32();
        //    instance.MinVideoSize = r.ReadInt32();
        //    instance.MonitorClipboard = r.ReadBoolean();
        //    instance.NetworkTimeout = r.ReadInt32();
        //    count = r.ReadInt32();
        //    instance.RecentFolders = new List<string>(count);
        //    for (int i = 0; i < count; i++)
        //    {
        //        instance.RecentFolders.Add(r.ReadString());
        //    }
        //    instance.RetryDelay = r.ReadInt32();
        //    instance.RunCommandAfterCompletion = r.ReadBoolean();
        //    instance.RunOnLogon = r.ReadBoolean();
        //    instance.ScanWithAntiVirus = r.ReadBoolean();
        //    instance.ShowDownloadCompleteWindow = r.ReadBoolean();
        //    instance.ShowProgressWindow = r.ReadBoolean();
        //    instance.ShutdownAfterAllFinished = r.ReadBoolean();
        //    instance.StartDownloadAutomatically = r.ReadBoolean();
        //    instance.TempDir = XDM.Messaging.StreamHelper.ReadString(r);
        //    count = r.ReadInt32();
        //    var list2 = new List<PasswordEntry>(count);
        //    for (int i = 0; i < count; i++)
        //    {
        //        var passwordEntry = new PasswordEntry
        //        {
        //            Host = XDM.Messaging.StreamHelper.ReadString(r),
        //            User = XDM.Messaging.StreamHelper.ReadString(r),
        //            Password = XDM.Messaging.StreamHelper.ReadString(r)
        //        };
        //        list2.Add(passwordEntry);
        //    }
        //    instance.UserCredentials = list2;
        //    count = r.ReadInt32();
        //    instance.VideoExtensions = new string[count];
        //    for (int i = 0; i < count; i++)
        //    {
        //        instance.VideoExtensions[i] = r.ReadString();
        //    }
        //    instance.Proxy = ProxyInfoSerializer.Deserialize(r);
        //    instance.AllowSystemDarkTheme = r.ReadBoolean();
        //}

        public static void SaveConfig()
        {
            ConfigIO.SerializeConfig();
        }

        //public static void SaveConfig3()
        //{
        //    using var ms = new MemoryStream();
        //    using var writer = new BinaryWriter(ms);
        //    writer.Write(Instance.AfterCompletionCommand ?? string.Empty);
        //    writer.Write(Instance.AntiVirusArgs ?? string.Empty);
        //    writer.Write(Instance.AntiVirusExecutable ?? string.Empty);
        //    var count = Instance.BlockedHosts?.Length ?? 0;
        //    writer.Write(count);
        //    for (int i = 0; i < count; i++)
        //    {
        //        writer.Write(Instance.BlockedHosts![i]);
        //    }
        //    count = Instance.Categories.Count();
        //    writer.Write(count);
        //    foreach (var category in Instance.Categories)
        //    {
        //        writer.Write(category.DefaultFolder);
        //        writer.Write(category.DisplayName ?? string.Empty);
        //        count = category.FileExtensions.Count();
        //        writer.Write(count);
        //        foreach (var ext in category.FileExtensions)
        //        {
        //            writer.Write(ext);
        //        }
        //        writer.Write(category.IsPredefined);
        //        writer.Write(category.Name);
        //    }
        //    writer.Write(Instance.DefaultDownloadFolder ?? string.Empty);
        //    writer.Write(Instance.EnableSpeedLimit);
        //    writer.Write(Instance.FetchServerTimeStamp);
        //    writer.Write((int)Instance.FileConflictResolution);
        //    count = Instance.FileExtensions.Length;
        //    writer.Write(count);
        //    foreach (var ext in Instance.FileExtensions)
        //    {
        //        writer.Write(ext);
        //    }
        //    writer.Write((int)Instance.FolderSelectionMode);
        //    writer.Write(Instance.DefaltDownloadSpeed);
        //    writer.Write(Instance.IsBrowserMonitoringEnabled);
        //    writer.Write(Instance.KeepPCAwake);
        //    writer.Write(Instance.Language);
        //    writer.Write(Instance.MaxParallelDownloads);
        //    writer.Write(Instance.MaxRetry);
        //    writer.Write(Instance.MaxSegments);
        //    writer.Write(Instance.MinVideoSize);
        //    writer.Write(Instance.MonitorClipboard);
        //    writer.Write(Instance.NetworkTimeout);
        //    count = Instance.RecentFolders.Count;
        //    writer.Write(count);
        //    foreach (var recentFolder in Instance.RecentFolders)
        //    {
        //        writer.Write(recentFolder);
        //    }
        //    writer.Write(Instance.RetryDelay);
        //    writer.Write(Instance.RunCommandAfterCompletion);
        //    writer.Write(Instance.RunOnLogon);
        //    writer.Write(Instance.ScanWithAntiVirus);
        //    writer.Write(Instance.ShowDownloadCompleteWindow);
        //    writer.Write(Instance.ShowProgressWindow);
        //    writer.Write(Instance.ShutdownAfterAllFinished);
        //    writer.Write(Instance.StartDownloadAutomatically);
        //    writer.Write(Instance.TempDir);
        //    count = Instance.UserCredentials.Count();
        //    writer.Write(count);
        //    foreach (var pe in Instance.UserCredentials)
        //    {
        //        writer.Write(pe.Host ?? string.Empty);
        //        writer.Write(pe.User ?? string.Empty);
        //        writer.Write(pe.Password ?? string.Empty);
        //    }
        //    count = Instance.VideoExtensions.Length;
        //    writer.Write(count);
        //    foreach (var ext in Instance.VideoExtensions)
        //    {
        //        writer.Write(ext);
        //    }
        //    //ProxyInfoSerializer.Serialize(Instance.Proxy, writer);
        //    //writer.Write(Instance.AllowSystemDarkTheme);
        //    writer.Close();
        //    ms.Close();
        //    TransactedIO.WriteBytes(ms.ToArray(), "settings.db", Config.DataDir);
        //    //TransactedIO.Write(JsonConvert.SerializeObject(Config.Instance), "settings.json", Config.DataDir);
        //    //File.WriteAllText(Path.Combine(Config.DataDir, "settings.json"), JsonConvert.SerializeObject(Config.Instance));
        //}
    }

    public enum FolderSelectionMode
    {
        Auto, Manual
    }

    public enum FileConflictResolution
    {
        AutoRename,
        Overwrite
    }
}
