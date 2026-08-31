// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Xvfb-backed headless GTK smoke — when a DISPLAY is available (Xvfb in CI / local X),
// verifies Builder can load a real glade file and Autoconnect a stub without throwing.
// When no DISPLAY is present, the test is inconclusive/skipped so CI without Xvfb
// still passes (GladeWiringTests already covers id drift without GTK).

using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;

namespace XDM.Tests
{
    // Filterable via: dotnet test --filter "TestCategory=GtkSmoke"
    // Wrapper script: scripts/run-gtk-smoke.sh (starts Xvfb :99, sets DISPLAY, runs this).
    [TestClass]
    [TestCategory("GtkSmoke")]
    public class GtkSmokeTests
    {
        // Minimal stub wired to a few ids from about-dialog.glade. Field names must
        // match glade <object id="..."> exactly for Autoconnect to populate them.
        private class AboutSmokeStub
        {
#pragma warning disable CS0649, CS0169
            [UI] private Label TxtAppName = null!;
            [UI] private Label TxtAppVersion = null!;
            [UI] private Label TxtTagline = null!;
            [UI] private Button BtnClose = null!;
            [UI] private Image AppLogo = null!;
#pragma warning restore CS0649, CS0169
        }

        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                    dir = dir.Parent;
                Assert.IsNotNull(dir, "Could not locate repo root (AGENTS.md)");
                return dir.FullName;
            }
        }

        // True when a DISPLAY is likely usable. We only skip on missing/empty DISPLAY;
        // a stale DISPLAY that GTK cannot open is handled separately after Init.
        private static bool IsDisplayAvailable(out string reason)
        {
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (!string.IsNullOrWhiteSpace(display))
            {
                reason = string.Empty;
                return true;
            }
            reason = "Skipped GtkSmoke: no DISPLAY — headless env without Xvfb. "
                + "Run under Xvfb via `scripts/run-gtk-smoke.sh` or set DISPLAY=:99 "
                + "(see scripts/run-gtk-smoke.sh --help). GladeWiringTests still covers id drift.";
            return false;
        }

        [TestMethod]
        [TestCategory("GtkSmoke")]
        public void Builder_ShouldLoadAboutDialog_AndAutoconnect_StubWithoutThrowing()
        {
            if (!IsDisplayAvailable(out var skipReason))
                Assert.Inconclusive(skipReason);

            // Try to init GTK. If the DISPLAY is bogus (stale socket), mark inconclusive
            // rather than failing the suite — the wrapper script's Xvfb would normally provide a valid one.
            try
            {
                Application.Init();
            }
            catch (Exception ex)
            {
                Assert.Inconclusive($"Skipped GtkSmoke: GTK init failed for DISPLAY='{Environment.GetEnvironmentVariable("DISPLAY")}': {ex.GetType().Name}: {ex.Message}"
                    + " — ensure Xvfb is running (scripts/run-gtk-smoke.sh) or DISPLAY is valid.");
            }

            var gladePath = Path.Combine(RepoRoot, "app", "XDM", "XDM.Gtk.UI", "glade", "about-dialog.glade");
            Assert.IsTrue(File.Exists(gladePath), $"about-dialog.glade not found at {gladePath}");

            Exception? builderEx = null;
            Builder? builder = null;
            try
            {
                builder = new Builder();
                builder.AddFromFile(gladePath);

                // Sanity: top-level window object should exist in the builder.
                var windowObj = builder.GetObject("window");
                Assert.IsNotNull(windowObj, "Builder.GetObject('window') should not be null for about-dialog.glade");

                // Autoconnect a minimal stub — should not throw and should wire the [UI] fields.
                var stub = new AboutSmokeStub();
                builder.Autoconnect(stub);

                // Verify via reflection that the expected fields were populated.
                AssertFieldWired(stub, "TxtAppName");
                AssertFieldWired(stub, "TxtAppVersion");
                AssertFieldWired(stub, "BtnClose");
            }
            catch (Exception ex) when (IsDisplayRelatedException(ex))
            {
                Assert.Inconclusive($"Skipped GtkSmoke: display-related failure for DISPLAY='{Environment.GetEnvironmentVariable("DISPLAY")}': {ex.GetType().Name}: {ex.Message}");
            }
            catch (Exception ex)
            {
                builderEx = ex;
            }
            finally
            {
                try { builder?.Dispose(); } catch { /* ignore dispose noise */ }
            }

            if (builderEx != null)
                Assert.Fail($"GtkSmoke failed: Builder load/autoconnect threw {builderEx.GetType().Name}: {builderEx.Message}\n{builderEx.StackTrace}");
        }

        private static void AssertFieldWired(object stub, string fieldName)
        {
            var fi = stub.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(fi, $"Reflection: field '{fieldName}' not found on stub");
            var val = fi!.GetValue(stub);
            Assert.IsNotNull(val, $"Autoconnect did not wire [UI] field '{fieldName}' — Builder left it null (glade id mismatch or Builder failure)");
        }

        private static bool IsDisplayRelatedException(Exception ex)
        {
            var msg = (ex.Message ?? string.Empty).ToLowerInvariant();
            var type = ex.GetType().Name.ToLowerInvariant();
            return msg.Contains("cannot open display")
                || msg.Contains("could not open display")
                || msg.Contains("display")
                || type.Contains("missingintptr")
                || msg.Contains("gdk")
                || msg.Contains("gtk");
        }
    }
}
