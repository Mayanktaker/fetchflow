// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Locks in the tray-icon contract that caused the GNOME "..." placeholder: the file every
// packager ships and SNI looks up must stay a valid, renderable, container-less brand mark.
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XDM.Tests
{
    [TestClass]
    public class TrayIconAssetTests
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

        private static string ReadSvg(string relativePath)
        {
            var path = Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), $"Missing icon file: {relativePath}");
            return File.ReadAllText(path);
        }

        // Shape geometry (path `d` / polygon `points`), ignoring any container <rect>.
        private static string[] Shapes(string svg) => Regex.Matches(
            svg, "<(path|polygon)\\b[^>]*?(?:d|points)=\"([^\"]+)\"", RegexOptions.Singleline)
            .Cast<Match>()
            .Select(m => m.Groups[2].Value.Replace(" ", "").Trim())
            .ToArray();

        [TestMethod]
        public void TrayIcon_UsesTheCurrentBrandGeometry()
        {
            var brand = Shapes(ReadSvg("app/XDM/fetchflow-logo.svg"));
            var tray = Shapes(ReadSvg("app/XDM/XDM.Gtk.UI/svg-icons/fetchflow-tray.svg"));

            CollectionAssert.AreEqual(brand, tray,
                "Tray icon geometry drifted from the brand mark. Copy the new brand artwork, " +
                "keeping the tray variant container-less.");
        }

        [TestMethod]
        public void TrayIcon_HasNoSolidContainer()
        {
            // A filled background rect makes the tray icon drop to ~1.3:1 on a dark panel
            // (WCAG 1.4.11 requires 3:1 for a non-text graphic), so the tray variant must
            // stay container-less. The app/window icon keeps its navy squircle.
            var tray = ReadSvg("app/XDM/XDM.Gtk.UI/svg-icons/fetchflow-tray.svg");

            Assert.IsFalse(Regex.IsMatch(tray, "<rect\\b[^>]*fill="),
                "Tray icon must not paint a container rect — it is invisible on dark panels.");
        }

        [TestMethod]
        public void TrayIcon_IsValidSvg()
        {
            var tray = ReadSvg("app/XDM/XDM.Gtk.UI/svg-icons/fetchflow-tray.svg");

            StringAssert.Contains(tray, "<svg");
            StringAssert.Contains(tray, "viewBox=");
            StringAssert.Contains(tray, "</svg>");
        }

        [TestMethod]
        public void TrayIcon_IsTrackedByGit()
        {
            // Regression: the tray icon was once untracked, so brand updates never reached
            // GitHub and every install kept showing the stale artwork.
            var root = RepoRoot();
            var tracked = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files --error-unmatch app/XDM/XDM.Gtk.UI/svg-icons/fetchflow-tray.svg",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            var output = tracked!.StandardOutput.ReadToEnd();
            tracked.WaitForExit(10_000);

            Assert.AreEqual(0, tracked.ExitCode,
                "fetchflow-tray.svg is not tracked by git — the tray icon would never update on install.");
            StringAssert.Contains(output, "fetchflow-tray.svg");
        }
    }
}
