// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Covers the interrupted-write recovery order in TransactedIO. Write() saves the new payload
// to "<name>.bak", moves the live file to "~<name>", then moves ".bak" into place — so a crash
// between the last two steps leaves ".bak" holding the newest save while "~<name>" holds the
// one it replaced. Reading the rollback copy first silently discarded that save.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.IO;

namespace XDM.Tests
{
    [TestClass]
    public class TransactedIOTests
    {
        private const string FileName = "settings.dat";

        private static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "fetchflow-transacted-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [TestMethod]
        public void Read_PrefersPendingBak_WhenLiveFileIsMissing()
        {
            // The exact crash state this precedence exists for: no live file, .bak complete.
            var dir = NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, FileName + ".bak"), "NEW");
                File.WriteAllText(Path.Combine(dir, "~" + FileName), "OLD");

                Assert.AreEqual("NEW", TransactedIO.Read(FileName, dir));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Read_LiveFileWins_OverStalePendingBak()
        {
            // A stray .bak must never shadow valid settings, or one interrupted write could
            // make every later load read stale data forever.
            var dir = NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, FileName), "LIVE");
                File.WriteAllText(Path.Combine(dir, FileName + ".bak"), "PENDING");

                Assert.AreEqual("LIVE", TransactedIO.Read(FileName, dir));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Read_FallsBackToRollbackCopy()
        {
            // Pre-existing behaviour kept intact: without a live file or .bak, ~<name> is the
            // only survivor and is still better than nothing.
            var dir = NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "~" + FileName), "ROLLBACK");

                Assert.AreEqual("ROLLBACK", TransactedIO.Read(FileName, dir));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void ReadBytes_FollowsTheSamePrecedence()
        {
            // settings.dat is loaded through ReadBytes, so the byte path must match Read.
            var dir = NewTempDir();
            try
            {
                File.WriteAllBytes(Path.Combine(dir, FileName + ".bak"), new byte[] { 9, 9 });
                File.WriteAllBytes(Path.Combine(dir, "~" + FileName), new byte[] { 1, 1 });

                CollectionAssert.AreEqual(new byte[] { 9, 9 }, TransactedIO.ReadBytes(FileName, dir));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Read_ReturnsNull_WhenNoCopyExists()
        {
            var dir = NewTempDir();
            try
            {
                Assert.IsNull(TransactedIO.Read(FileName, dir));
                Assert.IsNull(TransactedIO.ReadBytes(FileName, dir));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
