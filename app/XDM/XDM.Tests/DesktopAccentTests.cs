// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Guards the Plasma accent parser that lets a KDE session style the app.
//
// What went wrong and is pinned here:
//  - the GTK 4 frontend only followed light/dark, so a Plasma user with the Breeze
//    accent got FetchFlow's own palette on progress bars, checks and selected rows,
//  - Plasma 5 has no AccentColor at all and only records the selection colour, and a
//    missing fallback silently means "no accent" rather than "the closest one",
//  - a malformed value must never reach the stylesheet: "@define-color accent #";
//    is a parse error that would drop the whole provider.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.UI;

namespace XDM.Tests
{
    [TestClass]
    public class DesktopAccentTests
    {
        // Real kdeglobals shape: sections, unrelated keys, CRLF-tolerant
        private const string Plasma6Globals = @"[Colors:Selection]
BackgroundNormal=99,145,173
ForegroundNormal=255,255,255

[Colors:Window]
BackgroundNormal=27,30,32

[General]
AccentColor=33,98,138
LastUsedCustomAccentColor=33,98,138
";

        // Plasma 5 shape: the accent key does not exist yet
        private const string Plasma5Globals = @"[Colors:Selection]
BackgroundNormal=61,174,233
ForegroundNormal=255,255,255

[General]
BrowserApplication=firefox
";

        [TestMethod]
        public void AccentColor_WinsOverTheSelectionColour()
        {
            Assert.AreEqual("#21628a", DesktopAccent.FromKdeGlobals(Plasma6Globals));
        }

        [TestMethod]
        public void AccentColor_FallsBackToTheSelectionColour()
        {
            Assert.AreEqual("#3daee9", DesktopAccent.FromKdeGlobals(Plasma5Globals));
        }

        [TestMethod]
        public void AccentColor_IgnoresKeysOutsideTheirGroup()
        {
            // A key of the same name in another group must not win over the real one
            const string globals = @"[KDE]
AccentColor=1,2,3

[General]
AccentColor=33,98,138
";
            Assert.AreEqual("#21628a", DesktopAccent.FromKdeGlobals(globals));
        }

        [TestMethod]
        public void AccentColor_RejectsMalformedValues()
        {
            Assert.IsNull(DesktopAccent.FromKdeGlobals("[General]\nAccentColor=33,98\n"));
            Assert.IsNull(DesktopAccent.FromKdeGlobals("[General]\nAccentColor=33,98,300\n"));
            Assert.IsNull(DesktopAccent.FromKdeGlobals("[General]\nAccentColor=-1,0,0\n"));
            Assert.IsNull(DesktopAccent.FromKdeGlobals("[General]\nAccentColor=\n"));
            Assert.IsNull(DesktopAccent.FromKdeGlobals("[General]\nNothing=1,2,3\n"));
        }

        [TestMethod]
        public void AccentColor_ReturnsNullWithoutKdeGlobals()
        {
            Assert.IsNull(DesktopAccent.FromKdeGlobals(null));
            Assert.IsNull(DesktopAccent.FromKdeGlobals(""));
            Assert.IsNull(DesktopAccent.FromKdeGlobals("   "));
        }

        [TestMethod]
        public void NormalizeHex_ProducesStylesheetCaseAndPadding()
        {
            Assert.AreEqual("#010203", DesktopAccent.NormalizeHex("1,2,3"));
            Assert.AreEqual("#ffffff", DesktopAccent.NormalizeHex(" 255 , 255 , 255 "));
            Assert.IsNull(DesktopAccent.NormalizeHex("255,255"));
            Assert.IsNull(DesktopAccent.NormalizeHex(null));
        }

        [TestMethod]
        public void IsPlasmaSession_MatchesEveryDesktopIdKdeWrites()
        {
            Assert.IsTrue(DesktopAccent.IsPlasmaSession("KDE"));
            Assert.IsTrue(DesktopAccent.IsPlasmaSession("KDE:Plasma"));
            Assert.IsTrue(DesktopAccent.IsPlasmaSession("plasma"));
            Assert.IsTrue(DesktopAccent.IsPlasmaSession("kde"));
            Assert.IsFalse(DesktopAccent.IsPlasmaSession("GNOME"));
            Assert.IsFalse(DesktopAccent.IsPlasmaSession("ubuntu:GNOME"));
            Assert.IsFalse(DesktopAccent.IsPlasmaSession(null));
            Assert.IsFalse(DesktopAccent.IsPlasmaSession(""));
        }
    }
}
