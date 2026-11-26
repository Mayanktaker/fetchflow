// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using XDM.Core;

namespace XDM.Tests
{
    [TestClass]
    public class VersionConsistencyTests
    {
        // Walk up from the test binary until the repo root marker is found
        private static string RepoRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "build_all.sh")))
                dir = Directory.GetParent(dir)?.FullName;
            Assert.IsNotNull(dir, "repo root (build_all.sh) not found above " + AppDomain.CurrentDomain.BaseDirectory);
            return dir!;
        }

        private static string EnvVersion(string root)
        {
            var path = Path.Combine(root, "app", "XDM", "XDM.Linux.Installer", "version.env");
            Assert.IsTrue(File.Exists(path), path + " missing");
            foreach (var line in File.ReadAllLines(path))
            {
                var t = line.Trim();
                if (t.StartsWith("VERSION="))
                    return t.Substring("VERSION=".Length).Trim('"', '\'');
            }
            Assert.Fail("VERSION= not found in " + path);
            return string.Empty;
        }

        // Every shipped version string must equal version.env (the declared source of truth)
        [TestMethod]
        public void AllVersionSourcesAgree()
        {
            var root = RepoRoot();
            var expected = EnvVersion(root);

            Assert.AreEqual(expected, AppInfo.APP_VERSION,
                "AppInfo.APP_VERSION drifts from version.env");
            Assert.IsTrue(Version.TryParse(expected, out var parsed),
                $"version.env value '{expected}' is not a System.Version");
            Assert.AreEqual(parsed, AppInfo.AppVersion,
                "AppInfo.AppVersion must be Version.Parse(APP_VERSION)");

            foreach (var rel in new[]
            {
                Path.Combine("app", "XDM", "chrome-extension", "manifest.json"),
                Path.Combine("app", "XDM", "firefox-amo", "manifest.json")
            })
            {
                var path = Path.Combine(root, rel);
                Assert.IsTrue(File.Exists(path), path + " missing");
                var actual = (string?)JObject.Parse(File.ReadAllText(path))["version"];
                Assert.AreEqual(expected, actual,
                    Path.GetFileName(path) + " version drifts from version.env");
            }

            var wpf = Path.Combine(root, "app", "XDM", "XDM.Wpf.UI", "XDM.Wpf.UI.csproj");
            StringAssert.Contains(File.ReadAllText(wpf), $"<AssemblyVersion>{expected}</AssemblyVersion>",
                "XDM.Wpf.UI.csproj AssemblyVersion drifts from version.env");

            var iss = Path.Combine(root, "app", "XDM", "XDM.Win.Installer", "fetchflow-setup.iss");
            StringAssert.Contains(File.ReadAllText(iss), $"#define AppVersion \"{expected}\"",
                "fetchflow-setup.iss AppVersion drifts from version.env");
        }

        [TestMethod]
        public void AppVersionRoundTripsTheString()
        {
            Assert.AreEqual(AppInfo.APP_VERSION, AppInfo.AppVersion.ToString());
        }
    }
}
