// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using TraceLog;
using XDM.Core;
using XDM.Core.Downloader;

namespace XDM.Core.DataAccess
{
    public class AppDB
    {
        private static object lockObj = new();
        private bool init = false;
        private SQLiteConnection db;
        
        static AppDB()
        {
#if NETCOREAPP || NET5_0_OR_GREATER
            try
            {
                System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(typeof(SQLiteConnection).Assembly, (libraryName, assembly, searchPath) =>
                {
                    if (libraryName.Equals("SQLite.Interop.dll", StringComparison.OrdinalIgnoreCase) ||
                        libraryName.Equals("SQLite.Interop", StringComparison.OrdinalIgnoreCase) ||
                        libraryName.Equals("libSQLite.Interop.so", StringComparison.OrdinalIgnoreCase))
                    {
                        var localDir = AppDomain.CurrentDomain.BaseDirectory;
                        var candidates = new[]
                        {
                            Path.Combine(localDir, "SQLite.Interop.dll"),
                            Path.Combine(localDir, "libSQLite.Interop.so"),
                            Path.Combine(localDir, "runtimes", "linux-x64", "native", "SQLite.Interop.dll")
                        };
                        foreach (var c in candidates)
                        {
                            if (File.Exists(c) && System.Runtime.InteropServices.NativeLibrary.TryLoad(c, out var handle))
                            {
                                return handle;
                            }
                        }
                    }
                    return IntPtr.Zero;
                });
            }
            catch
            {
                // Non-fatal if already registered
            }
#endif
        }

        private AppDB() { }
        private DownloadList downloadsDB;
        public DownloadList Downloads => downloadsDB;
        private static AppDB instance;
        public static AppDB Instance
        {
            get
            {
                lock (lockObj)
                {
                    if (instance == null)
                    {
                        instance = new AppDB();
                    }
                }
                return instance;
            }
        }

        public bool Init(string file)
        {
            lock (this)
            {
                try
                {
                    string cs = $"URI=file:{file}";
                    if (!File.Exists(file))
                    {
                        SQLiteConnection.CreateFile(file);
                    }
                    db = new SQLiteConnection(cs);
                    db.Open();
                    SchemaInitializer.Init(db);
                    this.downloadsDB = new DownloadList(db);
                    init = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, ex.Message);
                    return false;
                }
            }
        }

        public bool Export(string file)
        {
            try
            {
                DataImportExport.CopyToFile(db, file);
                return true;
            }
            catch (Exception e)
            {
                Log.Debug(e, e.Message);
                return false;
                throw;
            }
        }

        public bool Import(string file)
        {
            try
            {
                DataImportExport.CopyFromFile(db, file);
                return true;
            }
            catch (Exception e)
            {
                Log.Debug(e, e.Message);
                return false;
                throw;
            }
        }
    }
}
