// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Pins the default color scheme per mode — these defaults are what the settings
// dropdown resolves to while Config.ColorScheme is still unset (-1), which is the
// state every fresh install (and this user's settings.dat) is in.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.UI;

namespace XDM.Tests
{
    [TestClass]
    public class ColorSchemeDefaultsTests
    {
        [TestMethod]
        public void DefaultDarkScheme_IsCharcoalBlue()
        {
            Assert.AreEqual(0, ColorSchemeTable.DefaultDarkSchemeIndex,
                "the default dark scheme must be index 0");
            Assert.IsTrue(ColorSchemeTable.DarkSchemes[ColorSchemeTable.DefaultDarkSchemeIndex]
                    .DisplayName.Contains("Charcoal Blue"),
                $"default dark scheme is '" + ColorSchemeTable
                    .DarkSchemes[ColorSchemeTable.DefaultDarkSchemeIndex].DisplayName
                    + "' — expected Charcoal Blue");
        }

        [TestMethod]
        public void DefaultLightScheme_IsClassicBlue()
        {
            Assert.AreEqual(0, ColorSchemeTable.DefaultLightSchemeIndex,
                "the default light scheme must be index 0");
            Assert.IsTrue(ColorSchemeTable.LightSchemes[ColorSchemeTable.DefaultLightSchemeIndex]
                    .DisplayName.Contains("Classic Blue"),
                $"default light scheme is '" + ColorSchemeTable
                    .LightSchemes[ColorSchemeTable.DefaultLightSchemeIndex].DisplayName
                    + "' — expected Classic Blue");
        }

        // The closed dropdown must show this exact string, so the marker has to live on
        // the defaults (it used to sit on Nord Emerald / Nordic Frost instead)
        [TestMethod]
        public void ExactlyOneSchemePerModeIsMarkedDefault_AndItIsTheDefault()
        {
            foreach (var (schemes, isDark) in new[]
            {
                (ColorSchemeTable.DarkSchemes, true),
                (ColorSchemeTable.LightSchemes, false),
            })
            {
                var marked = 0;
                foreach (var s in schemes)
                {
                    if (s.DisplayName.Contains("(Default)"))
                    {
                        marked++;
                        Assert.AreEqual(
                            ColorSchemeTable.DefaultSchemeIndex(isDark),
                            System.Array.IndexOf(schemes, s),
                            $"the '(Default)' marker sits on '{s.DisplayName}' but the "
                            + "resolved default is a different index");
                    }
                }
                Assert.AreEqual(1, marked,
                    isDark ? "dark" : "light" + " list must mark exactly one scheme (Default)");
            }
        }

        // Unset config (-1) and garbage indices must resolve to the default so the
        // settings dropdown can never land on a blank cell
        [TestMethod]
        public void UnresolvedSchemeIndex_ResolvesToTheModeDefault()
        {
            Assert.AreEqual(ColorSchemeTable.DefaultDarkSchemeIndex,
                ColorSchemeTable.ClampSchemeIndex(true, -1));
            Assert.AreEqual(ColorSchemeTable.DefaultLightSchemeIndex,
                ColorSchemeTable.ClampSchemeIndex(false, -1));
            Assert.AreEqual(ColorSchemeTable.DefaultDarkSchemeIndex,
                ColorSchemeTable.ClampSchemeIndex(true, 99));
            Assert.AreEqual(ColorSchemeTable.DefaultLightSchemeIndex,
                ColorSchemeTable.ClampSchemeIndex(false, 99));
        }

        [TestMethod]
        public void ResolveKeepsAValidExplicitChoice()
        {
            Assert.AreEqual(3, ColorSchemeTable.ClampSchemeIndex(true, 3));
            Assert.AreEqual(5, ColorSchemeTable.ClampSchemeIndex(false, 5));
        }
    }
}
