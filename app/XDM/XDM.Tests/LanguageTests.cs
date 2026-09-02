// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Translations;

namespace XDM.Tests
{
    // Verifies language dictionary completeness, syntax validity, and index registration
    [TestClass]
    public class LanguageTests
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

        // Verifies that index.txt exists and contains valid language entries
        [TestMethod]
        public void Test_IndexFile_ExistsAndRegistersLanguages()
        {
            var langDir = GetLangDirectory();
            var indexFile = Path.Combine(langDir, "index.txt");
            Assert.IsTrue(File.Exists(indexFile), "index.txt must exist in Lang folder");

            var lines = File.ReadAllLines(indexFile);
            var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var eqIdx = line.IndexOf('=');
                Assert.IsTrue(eqIdx > 0, $"Line in index.txt is missing '=' delimiter: {line}");

                var name = line.Substring(0, eqIdx).Trim();
                var file = line.Substring(eqIdx + 1).Trim();
                Assert.IsFalse(string.IsNullOrEmpty(name), "Language display name cannot be empty");
                Assert.IsFalse(string.IsNullOrEmpty(file), "Language file name cannot be empty");

                var targetPath = Path.Combine(langDir, file);
                Assert.IsTrue(File.Exists(targetPath), $"Referenced language file does not exist on disk: {targetPath}");

                entries[name] = file;
            }

            // Verify English, Hindi, and Hinglish are all present
            Assert.IsTrue(entries.ContainsKey("English"), "English must be registered in index.txt");
            Assert.IsTrue(entries.Keys.Any(k => k.StartsWith("Hindi", StringComparison.OrdinalIgnoreCase)), "Hindi must be registered in index.txt");
            Assert.IsTrue(entries.Keys.Any(k => k.StartsWith("Hinglish", StringComparison.OrdinalIgnoreCase)), "Hinglish must be registered in index.txt");
        }

        // Verifies that English, Hindi, and Hinglish have 100% key parity
        [TestMethod]
        public void Test_HindiAndHinglish_HaveFullKeyParityWithEnglish()
        {
            var langDir = GetLangDirectory();
            var englishFile = Path.Combine(langDir, "English.txt");
            var hindiFile = Path.Combine(langDir, "Hindi.txt");
            var hinglishFile = Path.Combine(langDir, "Hinglish.txt");

            Assert.IsTrue(File.Exists(englishFile), "English.txt must exist");
            Assert.IsTrue(File.Exists(hindiFile), "Hindi.txt must exist");
            Assert.IsTrue(File.Exists(hinglishFile), "Hinglish.txt must exist");

            var englishKeys = LoadKeys(englishFile);
            var hindiKeys = LoadKeys(hindiFile);
            var hinglishKeys = LoadKeys(hinglishFile);

            Assert.IsTrue(englishKeys.Count >= 270, $"English key count should be >= 270, found {englishKeys.Count}");
            Assert.IsTrue(hindiKeys.Count >= 270, $"Hindi key count should be >= 270, found {hindiKeys.Count}");
            Assert.IsTrue(hinglishKeys.Count >= 270, $"Hinglish key count should be >= 270, found {hinglishKeys.Count}");

            foreach (var key in englishKeys)
            {
                Assert.IsTrue(hindiKeys.Contains(key), $"Hindi dictionary is missing key: {key}");
                Assert.IsTrue(hinglishKeys.Contains(key), $"Hinglish dictionary is missing key: {key}");
            }
        }

        // Verifies TextResource loads keys and falls back gracefully
        [TestMethod]
        public void Test_TextResource_LoadsAndFallsBack()
        {
            var langDir = GetLangDirectory();
            var hindiFile = Path.Combine(langDir, "Hindi.txt");
            var hinglishFile = Path.Combine(langDir, "Hinglish.txt");

            // Load Hindi and verify Hindi string
            TextResource.Load(hindiFile);
            var hindiMenu = TextResource.GetText("MENU_LANG");
            Assert.AreEqual("भाषा", hindiMenu, "Hindi MENU_LANG should be 'भाषा'");

            // Load Hinglish and verify Hinglish string
            TextResource.Load(hinglishFile);
            var hinglishMenu = TextResource.GetText("MENU_LANG");
            Assert.AreEqual("Bhasha (Language)", hinglishMenu, "Hinglish MENU_LANG should be 'Bhasha (Language)'");

            // Non-existent key returns key itself
            var fallback = TextResource.GetText("NON_EXISTENT_KEY_12345");
            Assert.AreEqual("NON_EXISTENT_KEY_12345", fallback, "Non-existent key should return key itself as fallback");
        }

        // Helper to parse unique keys from a dictionary file
        private static HashSet<string> LoadKeys(string path)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(path);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                var eqIdx = line.IndexOf('=');
                if (eqIdx > 0)
                {
                    set.Add(line.Substring(0, eqIdx).Trim());
                }
            }
            return set;
        }
    }
}
