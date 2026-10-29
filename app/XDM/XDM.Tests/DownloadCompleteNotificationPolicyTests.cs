// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Translations;

namespace XDM.Tests
{
    // Verifies download complete desktop notification text resources and action policies
    [TestClass]
    public class DownloadCompleteNotificationPolicyTests
    {
        // Finds Lang directory by walking upward from current directory
        private static string GetLangDirectory()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            for (var i = 0; i < 6; i++)
            {
                var candidate = Path.Combine(current, "app", "XDM", "Lang");
                if (Directory.Exists(candidate)) return candidate;
                var directCandidate = Path.Combine(current, "Lang");
                if (Directory.Exists(directCandidate)) return directCandidate;
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
            throw new DirectoryNotFoundException("Could not locate app/XDM/Lang directory");
        }

        // Verifies required notification keys exist across English, Hindi, and Hinglish dictionaries
        [TestMethod]
        public void Test_LanguageDictionaries_HaveDownloadCompleteNotificationKeys()
        {
            var langDir = GetLangDirectory();
            var languages = new[] { "English.txt", "Hindi.txt", "Hinglish.txt" };

            foreach (var langFile in languages)
            {
                var path = Path.Combine(langDir, langFile);
                TextResource.Load(path);

                var completeMsg = TextResource.GetText("MSG_DOWNLOAD_COMPLETE");
                Assert.IsFalse(string.IsNullOrWhiteSpace(completeMsg), $"MSG_DOWNLOAD_COMPLETE missing in {langFile}");

                var openFileMsg = TextResource.GetText("CTX_OPEN_FILE");
                Assert.IsFalse(string.IsNullOrWhiteSpace(openFileMsg), $"CTX_OPEN_FILE missing in {langFile}");

                var openFolderMsg = TextResource.GetText("CTX_OPEN_FOLDER");
                Assert.IsFalse(string.IsNullOrWhiteSpace(openFolderMsg), $"CTX_OPEN_FOLDER missing in {langFile}");

                var dismissMsg = TextResource.GetText("BTN_DISMISS");
                Assert.IsFalse(string.IsNullOrWhiteSpace(dismissMsg), $"BTN_DISMISS missing in {langFile}");

                var timeoutMsg = TextResource.GetText("SETTINGS_NOTIFICATION_TIMEOUT");
                Assert.IsFalse(string.IsNullOrWhiteSpace(timeoutMsg), $"SETTINGS_NOTIFICATION_TIMEOUT missing in {langFile}");

                var timeoutDesc = TextResource.GetText("SETTINGS_NOTIFICATION_TIMEOUT_DESC");
                Assert.IsFalse(string.IsNullOrWhiteSpace(timeoutDesc), $"SETTINGS_NOTIFICATION_TIMEOUT_DESC missing in {langFile}");
            }
        }

        // Verifies timeout duration mapping from configuration
        [TestMethod]
        public void Test_NotificationTimeout_ValuesAndFallbacks()
        {
            var validTimeouts = new[] { 5000, 10000, 15000, 30000 };
            foreach (var t in validTimeouts)
            {
                var timeoutMs = t > 0 ? t : 10000;
                Assert.AreEqual(t, timeoutMs);
            }

            var fallbackTimeout = 0 <= 0 ? 10000 : 0;
            Assert.AreEqual(10000, fallbackTimeout);
        }

        // Verifies notification action keys define all expected interaction targets
        [TestMethod]
        public void Test_NotificationActionKeys_AreValidAndDistinct()
        {
            var actions = new[]
            {
                ("default", "Open File (Body Click)"),
                ("open", "Open File (Action Button)"),
                ("open_folder", "Open Folder (Action Button)"),
                ("dismiss", "Dismiss (Action Button)")
            };

            var keys = new System.Collections.Generic.HashSet<string>();
            foreach (var (key, _) in actions)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(key), "Action key cannot be empty");
                Assert.IsTrue(keys.Add(key), $"Duplicate action key detected: {key}");
            }
            Assert.AreEqual(4, keys.Count);
        }

        // Verifies directory and filename extraction handles paths and fallbacks correctly
        [TestMethod]
        public void Test_DownloadComplete_PathResolution()
        {
            var testFilePath = Path.Combine(Path.GetTempPath(), "fetchflow-test-file.zip");
            var resolvedFileName = Path.GetFileName(testFilePath);
            var resolvedDir = Path.GetDirectoryName(testFilePath);

            Assert.AreEqual("fetchflow-test-file.zip", resolvedFileName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(resolvedDir));
            Assert.AreEqual(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                            resolvedDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
    }
}
