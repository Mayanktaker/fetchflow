// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Pins the Follow System dark resolution. GTK 3 reports the desktop's dark
// variant two different ways and desktops disagree on which they use:
//   - Breeze-Dark / Mint-Y-Dark put it in the theme name,
//   - GNOME 42+ keeps the name "Adwaita" and only sets prefer-dark.
// Reading the name alone (the code before this test) made Follow System paint
// light surfaces on a dark GNOME session — surfaces and the toolkit's dark
// controls then disagreed on screen.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.UI;

namespace XDM.Tests
{
    [TestClass]
    public class ThemeModePolicyTests
    {
        // Classic desktops: darkness is spelled in the theme name
        [TestMethod]
        public void IsSessionDark_ReadsADarkThemeName()
        {
            Assert.IsTrue(ThemeModePolicy.IsSessionDark("Adwaita-dark", false));
            Assert.IsTrue(ThemeModePolicy.IsSessionDark("Mint-Y-Dark", false));
            Assert.IsTrue(ThemeModePolicy.IsSessionDark("Breeze-Dark", false));
            Assert.IsFalse(ThemeModePolicy.IsSessionDark("Adwaita", false));
            Assert.IsFalse(ThemeModePolicy.IsSessionDark("Mint-Y", false));
        }

        // GNOME 42+ keeps the theme name plain and expresses darkness as prefer-dark
        [TestMethod]
        public void IsSessionDark_ReadsPreferDarkWithAPlainThemeName()
        {
            Assert.IsTrue(ThemeModePolicy.IsSessionDark("Adwaita", true));
            Assert.IsTrue(ThemeModePolicy.IsSessionDark("Mint-Y", true));
            Assert.IsFalse(ThemeModePolicy.IsSessionDark("Adwaita", false));
        }

        // A missing or empty name must not throw, and prefer-dark still decides
        [TestMethod]
        public void IsSessionDark_ToleratesAMissingThemeName()
        {
            Assert.IsFalse(ThemeModePolicy.IsSessionDark(null, false));
            Assert.IsFalse(ThemeModePolicy.IsSessionDark("", false));
            Assert.IsTrue(ThemeModePolicy.IsSessionDark(null, true));
        }

        // Case-insensitive: theme names ship in mixed case
        [TestMethod]
        public void IsSessionDark_IgnoresCase()
        {
            Assert.IsTrue(ThemeModePolicy.IsSessionDark("Dracula-DARK", false));
            Assert.IsTrue(ThemeModePolicy.IsSessionDark("nordic_dark", false));
        }
    }
}
