// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Covers the launch-at-login (autostart) contract: the generated .desktop entry, and the
// liveness check that keeps Settings honest when an install path goes stale.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.Util;

namespace XDM.Tests
{
    [TestClass]
    public class AutoStartTests
    {
        private const string LiveBinary = "/opt/fetchflow/fetchflow";
        private const string StaleBinary = "/tmp/opencode/ff15/opt/fetchflow/fetchflow";

        // Simulates the filesystem: only LiveBinary is on disk.
        private static readonly Func<string, bool> FileExists = p => p == LiveBinary;

        [TestMethod]
        public void AutoStartEntry_PointsAtTheGivenExecutable()
        {
            var entry = DesktopEntry.BuildAutoStartEntry(LiveBinary, "/opt/fetchflow/fetchflow-logo.svg");

            // The command runs via `env`, so the real binary is the quoted token, not "env".
            Assert.AreEqual(LiveBinary, DesktopEntry.ExtractCommandPath(DesktopEntry.GetValueOrNull(entry, "Exec")));
            Assert.AreEqual(LiveBinary, DesktopEntry.GetValueOrNull(entry, "TryExec"));
        }

        [TestMethod]
        public void AutoStartEntry_StartsInBackground()
        {
            var entry = DesktopEntry.BuildAutoStartEntry(LiveBinary, "/icon.svg");
            var exec = DesktopEntry.GetValueOrNull(entry, "Exec");

            StringAssert.Contains(exec, "--background");
            StringAssert.Contains(exec, "GTK_USE_PORTAL=1");
        }

        [TestMethod]
        public void AutoStartEntry_OmitsBackgroundFlagWhenNotRequested()
        {
            var entry = DesktopEntry.BuildAutoStartEntry(LiveBinary, "/icon.svg", startInBackground: false);

            Assert.IsFalse(DesktopEntry.GetValueOrNull(entry, "Exec").Contains("--background"));
        }

        [TestMethod]
        public void AutoStartEntry_IsOptedInForGnome()
        {
            var entry = DesktopEntry.BuildAutoStartEntry(LiveBinary, "/icon.svg");

            Assert.AreEqual("true", DesktopEntry.GetValueOrNull(entry, "X-GNOME-Autostart-enabled"));
            Assert.AreEqual("Application", DesktopEntry.GetValueOrNull(entry, "Type"));
            Assert.AreEqual("/icon.svg", DesktopEntry.GetValueOrNull(entry, "Icon"));
        }

        [TestMethod]
        public void GeneratedEntry_IsLive()
        {
            var entry = DesktopEntry.BuildAutoStartEntry(LiveBinary, "/icon.svg");

            Assert.IsTrue(DesktopEntry.IsEntryLive(entry, FileExists));
        }

        [TestMethod]
        public void StalePath_IsNotLive()
        {
            // Regression: an entry that merely *mentions* fetchflow used to read as enabled,
            // so Settings showed "on" while the dead Exec path launched nothing at login.
            var entry = DesktopEntry.BuildAutoStartEntry(StaleBinary, "/tmp/icon.svg");

            Assert.IsTrue(DesktopEntry.GetValueOrNull(entry, "Exec").Contains("fetchflow"));
            Assert.IsFalse(DesktopEntry.IsEntryLive(entry, FileExists));
        }

        [TestMethod]
        public void GnomeOptOut_IsNotLive()
        {
            var entry = DesktopEntry.BuildAutoStartEntry(LiveBinary, "/icon.svg")
                .Replace("X-GNOME-Autostart-enabled=true", "X-GNOME-Autostart-enabled=false");

            Assert.IsFalse(DesktopEntry.IsEntryLive(entry, FileExists));
        }

        [TestMethod]
        public void HiddenOptOut_MarksTheEntryHidden()
        {
            var entry = DesktopEntry.BuildAutoStartOptOutEntry();

            Assert.IsTrue(DesktopEntry.GetValueOrNull(entry, "Hidden")
                .Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(DesktopEntry.IsEntryLive(entry, FileExists));
        }

        [TestMethod]
        public void HiddenOptOut_OverLiveEntry_IsNotLive()
        {
            // The per-user override shadows a system-wide /etc/xdg/autostart copy, so it still
            // carries a perfectly valid, live Exec. Liveness must therefore come from Hidden —
            // without that check the Settings box would flip back to "on" while the app kept
            // starting at login after the user explicitly turned it off.
            var live = DesktopEntry.BuildAutoStartEntry(LiveBinary, "/icon.svg");
            var hidden = live.Replace(
                "X-GNOME-Autostart-enabled=true",
                "X-GNOME-Autostart-enabled=true\r\nHidden=true");

            Assert.IsTrue(DesktopEntry.IsEntryLive(live, FileExists));
            Assert.IsFalse(DesktopEntry.IsEntryLive(hidden, FileExists));
        }

        [TestMethod]
        public void IsExplicitOptOut_DetectsDeliberateOptOuts()
        {
            // Startup self-heal must only repair dead paths, never flip a deliberate "off"
            // back to "on", so both opt-out spellings have to be recognised.
            var hidden = DesktopEntry.BuildAutoStartOptOutEntry();
            var gnome = DesktopEntry.BuildAutoStartEntry(LiveBinary, "/icon.svg")
                .Replace("X-GNOME-Autostart-enabled=true", "X-GNOME-Autostart-enabled=false");
            var live = DesktopEntry.BuildAutoStartEntry(LiveBinary, "/icon.svg");

            Assert.IsTrue(DesktopEntry.IsExplicitOptOut(hidden));
            Assert.IsTrue(DesktopEntry.IsExplicitOptOut(gnome));
            Assert.IsFalse(DesktopEntry.IsExplicitOptOut(live));
            Assert.IsFalse(DesktopEntry.IsExplicitOptOut(""));
            Assert.IsFalse(DesktopEntry.IsExplicitOptOut(null));
        }

        [TestMethod]
        public void EmptyEntry_IsNotLive()
        {
            Assert.IsFalse(DesktopEntry.IsEntryLive("", FileExists));
            Assert.IsFalse(DesktopEntry.IsEntryLive(null, FileExists));
        }

        [TestMethod]
        public void MissingExec_ButLiveTryExec_IsStillLive()
        {
            var entry = DesktopEntry.BuildAutoStartEntry(LiveBinary, "/icon.svg")
                .Replace($"Exec=env GTK_USE_PORTAL=1 \"{LiveBinary}\" --background\r\n", "");

            Assert.IsTrue(DesktopEntry.IsEntryLive(entry, FileExists));
        }

        [TestMethod]
        public void TryGetValue_ToleratesCrlfAndValueWhitespace()
        {
            // Per the XDG spec there is no whitespace around "=", but a trailing \r from CRLF
            // and accidental padding in the value must still parse cleanly.
            var text = "[Desktop Entry]\r\nExec=/bin/true  \r\nIcon=x\r\n";

            Assert.IsTrue(DesktopEntry.TryGetValue(text, "Exec", out var exec));
            Assert.AreEqual("/bin/true", exec);
            Assert.IsFalse(DesktopEntry.TryGetValue(text, "Nope", out _));
        }

        [TestMethod]
        public void ExtractCommandPath_PrefersQuotedPath()
        {
            // "env" is the first token, but the real binary is the quoted one.
            Assert.AreEqual("/opt/fetchflow/fetchflow",
                DesktopEntry.ExtractCommandPath("env GTK_USE_PORTAL=1 \"/opt/fetchflow/fetchflow\" --background"));
        }

        [TestMethod]
        public void ExtractCommandPath_FallsBackToFirstToken()
        {
            Assert.AreEqual("/usr/bin/fetchflow", DesktopEntry.ExtractCommandPath("/usr/bin/fetchflow --background"));
            Assert.IsNull(DesktopEntry.ExtractCommandPath("   "));
            Assert.IsNull(DesktopEntry.ExtractCommandPath(null));
        }

        [TestMethod]
        public void ExtractCommandPath_HandlesPathsWithSpaces()
        {
            var path = "/opt/my apps/fetchflow";
            Assert.AreEqual(path, DesktopEntry.ExtractCommandPath($"env \"{path}\" --background"));
        }
    }
}
