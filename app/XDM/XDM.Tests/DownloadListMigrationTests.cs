// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using XDM.Core;
using XDM.Core.DataAccess;
using XDM.Core.Downloader;

namespace XDM.Tests
{
    // Failure persistence round-trip against a real SQLite database:
    // legacy schema migration, error load, failure update, and clearing
    [TestClass]
    public class DownloadListMigrationTests
    {
        private string dbFile = null!;
        private DownloadList downloads = null!;
        private bool initialized;

        // The console runner invokes only [TestMethod]s, so setup is idempotent per test
        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            TraceLog.Log.InitFileBasedTrace(Path.Combine(Path.GetTempPath(), "fetchflow-downloadlist-test.log"));
            dbFile = Path.Combine(Path.GetTempPath(), $"fetchflow-test-{Guid.NewGuid():N}.db");
            using (var db = new SQLiteConnection($"URI=file:{dbFile}").OpenAndReturn())
            using (var cmd = db.CreateCommand())
            {
                // Pre-migration schema: exactly the original 23 columns
                cmd.CommandText = @"CREATE TABLE downloads(
                                        id TEXT PRIMARY KEY,
                                        completed INT,
                                        name TEXT,
                                        date_added INT,
                                        size INT,
                                        status INT,
                                        progress INT,
                                        download_type TEXT,
                                        filenamefetchmode INT,
                                        maxspeedlimitinkib INT,
                                        targetdir TEXT,
                                        primary_url TEXT,
                                        referer_url TEXT,
                                        auth INT,
                                        user TEXT,
                                        pass TEXT,
                                        proxy INT,
                                        proxy_host TEXT,
                                        proxy_port INT,
                                        proxy_user TEXT,
                                        proxy_pass TEXT,
                                        proxy_type INT
                                    ) WITHOUT ROWID";
                cmd.ExecuteNonQuery();
            }
            var dbConnection = new SQLiteConnection($"URI=file:{dbFile}").OpenAndReturn();
            SchemaInitializer.Init(dbConnection); // migration under test
            downloads = new DownloadList(dbConnection);
        }

        [TestMethod]
        public void Migration_AddsErrorColumnsToLegacyDatabase()
        {
            EnsureInitialized();
            using var db = new SQLiteConnection($"URI=file:{dbFile}").OpenAndReturn();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('downloads') WHERE name IN ('error_code','error_message')";
            Assert.AreEqual(2L, Convert.ToInt64(cmd.ExecuteScalar()));
        }

        [TestMethod]
        public void FailureUpdate_PersistsCodeAndDetail()
        {
            EnsureInitialized();
            downloads.AddNewDownload(NewEntry("id-1"));
            var updated = downloads.UpdateDownloadFailure("id-1", ErrorCode.InvalidResponse, "HTTP 403");
            Assert.IsTrue(updated, "UpdateDownloadFailure must succeed — see fetchflow-downloadlist-test.log");

            using (var db = new SQLiteConnection($"URI=file:{dbFile}").OpenAndReturn())
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT error_code, error_message FROM downloads WHERE id='id-1'";
                using var r = cmd.ExecuteReader();
                Assert.IsTrue(r.Read(), "row must exist");
                Assert.AreEqual((int)ErrorCode.InvalidResponse, Convert.ToInt32(r.GetValue(0)), "raw error_code");
                Assert.AreEqual("HTTP 403", r.GetValue(1), "raw error_message");
            }

            var entry = (InProgressDownloadItem)downloads.GetDownloadById("id-1")!;
            Assert.AreEqual(DownloadStatus.Stopped, entry.Status);
            Assert.AreEqual(ErrorCode.InvalidResponse, entry.LastErrorCode);
            Assert.AreEqual("HTTP 403", entry.LastErrorMessage);
        }

        [TestMethod]
        public void ClearError_ReturnsCleanStoppedState()
        {
            EnsureInitialized();
            downloads.AddNewDownload(NewEntry("id-2"));
            downloads.UpdateDownloadFailure("id-2", ErrorCode.DiskError, "disk full");
            Assert.IsTrue(downloads.ClearDownloadError("id-2"));

            var entry = (InProgressDownloadItem)downloads.GetDownloadById("id-2")!;
            Assert.AreEqual(ErrorCode.None, entry.LastErrorCode);
            Assert.IsNull(entry.LastErrorMessage);
        }

        [TestMethod]
        public void LegacyRows_LoadWithNoError()
        {
            EnsureInitialized();
            downloads.AddNewDownload(NewEntry("id-3"));
            using var db = new SQLiteConnection($"URI=file:{dbFile}").OpenAndReturn();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE downloads SET error_code=NULL, error_message=NULL WHERE id='id-3'";
            cmd.ExecuteNonQuery();

            var entry = (InProgressDownloadItem)downloads.GetDownloadById("id-3")!;
            Assert.AreEqual(ErrorCode.None, entry.LastErrorCode);
            Assert.IsNull(entry.LastErrorMessage);
        }

        [TestMethod]
        public void LoadDownloads_CarriesFailureReasonIntoModel()
        {
            EnsureInitialized();
            downloads.AddNewDownload(NewEntry("id-4"));
            downloads.UpdateDownloadFailure("id-4", ErrorCode.SessionExpired, "token expired");

            downloads.LoadDownloads(out var inProgress, out _, QueryMode.InProgress);
            var entry = inProgress.FirstOrDefault(e => e.Id == "id-4");
            Assert.IsNotNull(entry);
            Assert.AreEqual(ErrorCode.SessionExpired, entry.LastErrorCode);
            Assert.AreEqual("token expired", entry.LastErrorMessage);
        }

        private static InProgressDownloadItem NewEntry(string id) => new()
        {
            Id = id,
            Name = "video.mp4",
            DateAdded = DateTime.Now,
            DownloadType = "Http",
            FileNameFetchMode = FileNameFetchMode.FileNameAndExtension,
            PrimaryUrl = "https://example.com/video.mp4",
            Status = DownloadStatus.Downloading,
            Progress = 50
        };
    }
}
