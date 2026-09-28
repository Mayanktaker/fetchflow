// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Windows launch-at-login parity: the Inno Setup installer and the app must agree on ONE
// mechanism. They previously disagreed (installer wrote a Startup-folder shortcut, Settings
// wrote an HKCU Run key), which caused double launches, an untick that did nothing, and a
// checkbox that read OFF while autostart was still active. These tests lock that down.
using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.Util;

namespace XDM.Tests
{
    [TestClass]
    public class WindowsAutoStartInstallerTests
    {
        private static string RepoRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "build_all.sh")))
            {
                dir = Directory.GetParent(dir)?.FullName;
            }
            return dir ?? throw new InvalidOperationException("Repo root not found");
        }

        private static string ReadIss() => File.ReadAllText(Path.Combine(
            RepoRoot(), "app", "XDM", "XDM.Win.Installer", "fetchflow-setup.iss"));

        [TestMethod]
        public void Installer_WritesTheSameRunValueAsTheApp()
        {
            var iss = ReadIss();

            // The app writes HKCU\...\Run value "FetchFlow" (AutoStartEntry.WindowsRunValueName).
            StringAssert.Contains(iss, "Software\\Microsoft\\Windows\\CurrentVersion\\Run");
            StringAssert.Contains(iss, $"ValueName: \"{AutoStartEntry.WindowsRunValueName}\"");
        }

        [TestMethod]
        public void Installer_PassesTheBackgroundArgument()
        {
            // Both the app and the installer must launch with --background, or a login start
            // pops the full window in the user's face instead of starting quietly.
            var iss = ReadIss();

            StringAssert.Contains(iss, AutoStartEntry.BackgroundArgument);
        }

        [TestMethod]
        public void Installer_DoesNotCreateAStartupFolderShortcut()
        {
            // Regression: the shortcut and the Run key could not see each other, so unticking in
            // Settings left autostart on. The Run key is now the only mechanism.
            var iss = ReadIss();

            Assert.IsFalse(iss.Contains("{userstartup}"),
                "Inno Setup must not create a {userstartup} shortcut — HKCU Run key is the single source of truth.");
        }

        [TestMethod]
        public void Installer_RunEntryIsGatedOnTheStartupTask()
        {
            // Honour the user's installer choice: only write the Run key when "startupicon" is ticked.
            var iss = ReadIss();
            var runBlock = Regex.Match(iss, @"Root: HKCU;[^\r\n]*CurrentVersion\\Run.*?(?=\r?\n\s*\r?\n|\r?\n\[)", RegexOptions.Singleline);

            Assert.IsTrue(runBlock.Success, "Could not find the [Registry] Run entry in the .iss");
            StringAssert.Contains(runBlock.Value, "Tasks: startupicon");
            StringAssert.Contains(runBlock.Value, "uninsdeletevalue");
        }

        [TestMethod]
        public void App_ExposesTheRegistryValueNamesForParityChecks()
        {
            // Guards the constants the parity test depends on.
            Assert.AreEqual("FetchFlow", AutoStartEntry.WindowsRunValueName);
            Assert.AreEqual("XDM", AutoStartEntry.LegacyWindowsRunValueName);
        }
    }
}
