// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XDM.Tests
{
    [TestClass]
    public class BrandingIconTests
    {
        private const string CanonicalSvgSha256 = "c3576c0244c1e957cf36f7c818222c1f9f60e7567bf71eca2cfcdebda4f25875";
        private const string Canonical512PngSha256 = "4e35e9e7efc6b9a17fc61364793f949a67b89e479a12c657df8b67a1a031e6f4";

        // Compute sha256 checksum of a file
        private static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // Find git root directory relative to current test execution
        private static string GetRepoRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "build_all.sh")))
            {
                dir = Directory.GetParent(dir)?.FullName;
            }
            return dir ?? Directory.GetCurrentDirectory();
        }

        [TestMethod]
        public void CanonicalLogoSvg_MustHaveMatchingHashAcrossRepo()
        {
            var root = GetRepoRoot();
            string[] svgFiles =
            {
                Path.Combine(root, "app", "XDM", "fetchflow-logo.svg"),
                Path.Combine(root, "app", "XDM", "XDM.Gtk.UI", "fetchflow-logo.svg"),
                Path.Combine(root, "app", "XDM", "XDM.Gtk.UI", "svg-icons", "fetchflow-logo.svg"),
                Path.Combine(root, "app", "XDM", "XDM.Gtk.UI", "svg-icons", "fetchflow-mark.svg"),
                Path.Combine(root, "docs", "fetchflow-logo.svg")
            };

            foreach (var file in svgFiles)
            {
                Assert.IsTrue(File.Exists(file), $"Expected SVG file to exist: {file}");
                var hash = ComputeSha256(file);
                Assert.AreEqual(CanonicalSvgSha256, hash, $"Hash mismatch for {file}. Stale logo detected!");
            }
        }

        [TestMethod]
        public void CanonicalLogo512Png_MustHaveMatchingHashAcrossRepo()
        {
            var root = GetRepoRoot();
            string[] pngFiles =
            {
                Path.Combine(root, "app", "XDM", "XDM.Gtk.UI", "fetchflow-logo-512.png"),
                Path.Combine(root, "docs", "fetchflow-logo.png")
            };

            foreach (var file in pngFiles)
            {
                Assert.IsTrue(File.Exists(file), $"Expected PNG file to exist: {file}");
                var hash = ComputeSha256(file);
                Assert.AreEqual(Canonical512PngSha256, hash, $"Hash mismatch for {file}. Stale logo detected!");
            }
        }

        [TestMethod]
        public void MultiResolutionIcons_MustExistForAllStandardSizes()
        {
            var root = GetRepoRoot();
            int[] sizes = { 16, 22, 24, 32, 48, 64, 128, 256, 512 };

            foreach (var sz in sizes)
            {
                var path = Path.Combine(root, "app", "XDM", "XDM.Gtk.UI", $"fetchflow-logo-{sz}.png");
                Assert.IsTrue(File.Exists(path), $"Expected icon size {sz}x{sz} at {path}");
            }
        }
    }
}
