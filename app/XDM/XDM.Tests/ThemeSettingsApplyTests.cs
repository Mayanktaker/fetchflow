// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Exercises the GTK-level half of a theme change against a REAL Gtk.Settings.
// The verify harness that found the Follow System bug was throwaway; these rules
// are the ones that broke, so they get a permanent env-gated test in the GtkSmoke
// lane (`scripts/run-gtk-smoke.sh`), and stay Inconclusive without a DISPLAY.
//
// What is pinned here (FF-UI-001 / FF-UI-044):
//   - Follow System must never rewrite the desktop's gtk-theme-name,
//   - an explicit Light/Dark choice must force Adwaita,
//   - Follow System must hand prefer-dark back to the desktop rather than echo our
//     own last write, which is what made Light -> Follow System stick as Light,
//   - a live re-derive must write nothing at all.
//
// ISOLATION: these tests mutate process-global GtkSettings shared with every other
// GtkSmoke test in the run, so the class is [DoNotParallelize] and each scope
// restores the values it found. Keep new cases inside a SettingsScope.
using System;
using Gtk;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.GtkUI.Utils;

namespace XDM.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class ThemeSettingsApplyTests
    {
        // GTK needs a display; without one the whole class reports Inconclusive
        private static bool IsDisplayAvailable(out string reason)
        {
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (!string.IsNullOrWhiteSpace(display))
            {
                reason = string.Empty;
                return true;
            }
            reason = "Skipped GtkSmoke: no DISPLAY — run via scripts/run-gtk-smoke.sh.";
            return false;
        }

        // The app's settings singleton, with its current values restored afterwards
        private sealed class SettingsScope : IDisposable
        {
            public Settings Settings = null!;
            private string themeName = string.Empty;
            private bool preferDark;

            public static SettingsScope Open()
            {
                // GtkSharp tolerates a repeat Init in most builds; never fail the test on it
                try { Application.Init(); } catch { }
                var scope = new SettingsScope { Settings = Settings.Default };
                if (scope.Settings == null)
                {
                    Assert.Inconclusive("No GtkSettings on this backend.");
                }
                scope.themeName = scope.Settings.ThemeName ?? string.Empty;
                scope.preferDark = scope.Settings.ApplicationPreferDarkTheme;
                return scope;
            }

            public void Dispose()
            {
                try
                {
                    Settings.ThemeName = themeName;
                    Settings.ApplicationPreferDarkTheme = preferDark;
                }
                catch
                {
                    // Restoring is best-effort: a failing backend must not fail the test
                }
            }
        }

        // Follow System leaves Breeze/Mint-Y widget chrome untouched
        [TestMethod]
        [TestCategory("GtkSmoke")]
        public void ApplyTheme_FollowSystem_KeepsTheDesktopThemeName()
        {
            if (!IsDisplayAvailable(out var skip)) Assert.Inconclusive(skip);
            using var scope = SettingsScope.Open();

            scope.Settings.ThemeName = "Mint-Y";
            ThemeSettingsApplier.Apply(scope.Settings, null, false);

            Assert.AreEqual("Mint-Y", scope.Settings.ThemeName,
                "Follow System must not rewrite the desktop's GTK theme name");
        }

        // An explicit choice owns the theme name, so our surfaces and controls agree
        [TestMethod]
        [TestCategory("GtkSmoke")]
        public void ApplyTheme_ExplicitMode_ForcesAdwaita()
        {
            if (!IsDisplayAvailable(out var skip)) Assert.Inconclusive(skip);
            using var scope = SettingsScope.Open();

            scope.Settings.ThemeName = "Breeze";
            ThemeSettingsApplier.Apply(scope.Settings, true, false);
            Assert.AreEqual("Adwaita", scope.Settings.ThemeName);
            Assert.IsTrue(scope.Settings.ApplicationPreferDarkTheme);

            ThemeSettingsApplier.Apply(scope.Settings, false, true);
            Assert.AreEqual("Adwaita", scope.Settings.ThemeName);
            Assert.IsFalse(scope.Settings.ApplicationPreferDarkTheme);
        }

        // Returning to Follow System must restore the desktop's flag, not keep ours
        [TestMethod]
        [TestCategory("GtkSmoke")]
        public void ApplyTheme_FollowSystem_HandsPreferDarkBackToTheDesktop()
        {
            if (!IsDisplayAvailable(out var skip)) Assert.Inconclusive(skip);
            using var scope = SettingsScope.Open();

            // Desktop is light; the user forced Dark, then went back to Follow System
            ThemeSettingsApplier.Apply(scope.Settings, true, false);
            Assert.IsTrue(scope.Settings.ApplicationPreferDarkTheme, "explicit Dark must apply");

            ThemeSettingsApplier.Apply(scope.Settings, null, false);
            Assert.IsFalse(scope.Settings.ApplicationPreferDarkTheme,
                "Follow System must hand prefer-dark back to the desktop, not echo the override");
        }

        // A live re-derive must be read-only. Writing the flag back used to clobber a
        // desktop change arriving in two steps (theme name first, prefer-dark next),
        // which left the follower stuck in the wrong mode.
        [TestMethod]
        [TestCategory("GtkSmoke")]
        public void ApplyTheme_FollowSystemRedrive_LeavesTheDesktopFlagAlone()
        {
            if (!IsDisplayAvailable(out var skip)) Assert.Inconclusive(skip);
            using var scope = SettingsScope.Open();

            scope.Settings.ApplicationPreferDarkTheme = true;
            ThemeSettingsApplier.Apply(scope.Settings, null, false, restoreSessionPreference: false);
            Assert.IsTrue(scope.Settings.ApplicationPreferDarkTheme,
                "a re-derive must not write prefer-dark back");

            scope.Settings.ThemeName = "Breeze";
            ThemeSettingsApplier.Apply(scope.Settings, null, true, restoreSessionPreference: false);
            Assert.AreEqual("Breeze", scope.Settings.ThemeName,
                "a re-derive must never touch the desktop theme name");
            Assert.IsTrue(scope.Settings.ApplicationPreferDarkTheme,
                "a re-derive must not write prefer-dark back even when the captured value differs");
        }
    }
}
