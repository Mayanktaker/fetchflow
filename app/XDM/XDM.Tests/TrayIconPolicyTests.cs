// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Covers the tray-icon decisions that caused the GNOME "..." placeholder regression, and the
// user-theme self-install rule. Pure logic, so it runs in the headless test runner.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.Util;

namespace XDM.Tests
{
    [TestClass]
    public class TrayIconPolicyTests
    {
        private const string TrayName = "fetchflow-tray";

        [TestMethod]
        public void AdvertisesName_WhenItResolvesInTheme()
        {
            Assert.AreEqual(TrayName, TrayIconPolicy.ResolveAdvertisedIconName(TrayName, true));
        }

        [TestMethod]
        public void AdvertisesEmptyName_WhenItDoesNotResolve()
        {
            // Regression: advertising an unresolvable name made GNOME ignore our IconPixmap
            // and draw its generic "..." placeholder instead of the app icon.
            Assert.AreEqual("", TrayIconPolicy.ResolveAdvertisedIconName(TrayName, false));
        }

        [TestMethod]
        public void IconThemePath_IsAlwaysEmpty()
        {
            // A non-empty IconThemePath makes GNOME build a private theme from that path alone
            // and ignore the system theme, which is exactly what broke icon lookup before.
            Assert.AreEqual("", TrayIconPolicy.ResolveIconThemePath());
        }

        [TestMethod]
        public void NeedsInstall_WhenTargetMissing()
        {
            Assert.IsTrue(TrayIconPolicy.NeedsInstall(sourceExists: true, targetExists: false));
        }

        [TestMethod]
        public void DoesNotNeedInstall_WhenUpToDate()
        {
            Assert.IsFalse(TrayIconPolicy.NeedsInstall(
                sourceExists: true, targetExists: true, sizeDiffers: false, sourceIsNewer: false));
        }

        [TestMethod]
        public void NeedsInstall_WhenSourceChanged()
        {
            Assert.IsTrue(TrayIconPolicy.NeedsInstall(
                sourceExists: true, targetExists: true, sizeDiffers: true, sourceIsNewer: false));
            Assert.IsTrue(TrayIconPolicy.NeedsInstall(
                sourceExists: true, targetExists: true, sizeDiffers: false, sourceIsNewer: true));
        }

        [TestMethod]
        public void NeverNeedsInstall_WhenSourceMissing()
        {
            // Nothing to copy from: a missing svg must not create empty/dangling theme files.
            Assert.IsFalse(TrayIconPolicy.NeedsInstall(sourceExists: false, targetExists: false));
            Assert.IsFalse(TrayIconPolicy.NeedsInstall(sourceExists: false, targetExists: true));
        }
    }
}
