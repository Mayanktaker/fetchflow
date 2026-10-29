// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Translations;

namespace XDM.Tests
{
    // Verifies notification text formatting and multi-video notification keys
    [TestClass]
    public class MediaNotificationPolicyTests
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

        // Verifies MSG_VID_CAP_MULTI key exists in English, Hindi, and Hinglish dictionaries
        [TestMethod]
        public void Test_LanguageDictionaries_HaveMultiVideoNotificationKey()
        {
            var langDir = GetLangDirectory();
            var english = Path.Combine(langDir, "English.txt");
            var hindi = Path.Combine(langDir, "Hindi.txt");
            var hinglish = Path.Combine(langDir, "Hinglish.txt");

            TextResource.Load(english);
            var engMulti = TextResource.GetText("MSG_VID_CAP_MULTI");
            Assert.IsNotNull(engMulti);
            Assert.IsTrue(engMulti.Contains("{0}"), "English MSG_VID_CAP_MULTI must contain {0} placeholder");

            TextResource.Load(hindi);
            var hindiMulti = TextResource.GetText("MSG_VID_CAP_MULTI");
            Assert.IsNotNull(hindiMulti);
            Assert.IsTrue(hindiMulti.Contains("{0}"), "Hindi MSG_VID_CAP_MULTI must contain {0} placeholder");

            TextResource.Load(hinglish);
            var hinglishMulti = TextResource.GetText("MSG_VID_CAP_MULTI");
            Assert.IsNotNull(hinglishMulti);
            Assert.IsTrue(hinglishMulti.Contains("{0}"), "Hinglish MSG_VID_CAP_MULTI must contain {0} placeholder");
        }

        // Verifies aggregated video notification message formatting
        [TestMethod]
        public void Test_MultiVideoNotification_FormatMessage()
        {
            var langDir = GetLangDirectory();
            TextResource.Load(Path.Combine(langDir, "English.txt"));

            var singleMsg = TextResource.GetText("MSG_VID_CAP") ?? "Streaming video detected, please open Media Grabber";
            Assert.AreEqual("Streaming video detected, please open Media Grabber", singleMsg);

            var multiPattern = TextResource.GetText("MSG_VID_CAP_MULTI") ?? "{0} streaming videos detected, please open Media Grabber";
            var formatted = string.Format(multiPattern, 42);
            Assert.AreEqual("42 streaming videos detected, please open Media Grabber", formatted);
        }

        // Verifies debounce settings keys exist in English, Hindi, and Hinglish dictionaries
        [TestMethod]
        public void Test_LanguageDictionaries_HaveDebounceSettingsKeys()
        {
            var langDir = GetLangDirectory();
            var english = Path.Combine(langDir, "English.txt");
            var hindi = Path.Combine(langDir, "Hindi.txt");
            var hinglish = Path.Combine(langDir, "Hinglish.txt");

            TextResource.Load(english);
            Assert.IsFalse(string.IsNullOrWhiteSpace(TextResource.GetText("SETTINGS_MEDIA_NOTIFICATION_DEBOUNCE")));
            Assert.IsFalse(string.IsNullOrWhiteSpace(TextResource.GetText("SETTINGS_MEDIA_NOTIFICATION_DEBOUNCE_DESC")));
            Assert.AreEqual("Dismiss", TextResource.GetText("BTN_DISMISS"));

            TextResource.Load(hindi);
            Assert.IsFalse(string.IsNullOrWhiteSpace(TextResource.GetText("SETTINGS_MEDIA_NOTIFICATION_DEBOUNCE")));
            Assert.IsFalse(string.IsNullOrWhiteSpace(TextResource.GetText("SETTINGS_MEDIA_NOTIFICATION_DEBOUNCE_DESC")));
            Assert.AreEqual("खारिज करें", TextResource.GetText("BTN_DISMISS"));

            TextResource.Load(hinglish);
            Assert.IsFalse(string.IsNullOrWhiteSpace(TextResource.GetText("SETTINGS_MEDIA_NOTIFICATION_DEBOUNCE")));
            Assert.IsFalse(string.IsNullOrWhiteSpace(TextResource.GetText("SETTINGS_MEDIA_NOTIFICATION_DEBOUNCE_DESC")));
            Assert.AreEqual("Dismiss karein", TextResource.GetText("BTN_DISMISS"));
        }
    }
}
