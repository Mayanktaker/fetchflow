// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Pins Config.ThemeMode's shared codes and the two conversions every frontend used
// to re-derive by hand. Config stores an int, so a wrong code or a transposed
// ternary is a silent wrong-theme at runtime, never a build error — which is why
// these are asserted directly, including the out-of-range clamp.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.UI;

namespace XDM.Tests
{
    [TestClass]
    public class ThemeModesTests
    {
        // Config.cs, ConfigIO.cs and the three frontends all depend on these values
        [TestMethod]
        public void ThemeModeCodes_MatchThePersistedConfigContract()
        {
            Assert.AreEqual(0, ThemeModes.Light);
            Assert.AreEqual(1, ThemeModes.Dark);
            Assert.AreEqual(2, ThemeModes.FollowSystem);
        }

        // Follow System must ask the desktop (null), not ask for light
        [TestMethod]
        public void ToDarkRequest_ReturnsNullOnlyForFollowSystem()
        {
            Assert.IsFalse(ThemeModes.ToDarkRequest(ThemeModes.Light));
            Assert.IsTrue(ThemeModes.ToDarkRequest(ThemeModes.Dark));
            Assert.IsNull(ThemeModes.ToDarkRequest(ThemeModes.FollowSystem));
        }

        // An unreadable stored value must fall back to following the desktop
        [TestMethod]
        public void ToDarkRequest_TreatsAnUnknownModeAsFollowSystem()
        {
            Assert.IsNull(ThemeModes.ToDarkRequest(-1));
            Assert.IsNull(ThemeModes.ToDarkRequest(7));
            Assert.IsNull(ThemeModes.ToDarkRequest(int.MaxValue));
        }

        [TestMethod]
        public void FromDark_PinsTheChoiceAndEndsFollowSystem()
        {
            Assert.AreEqual(ThemeModes.Dark, ThemeModes.FromDark(true));
            Assert.AreEqual(ThemeModes.Light, ThemeModes.FromDark(false));
        }

        [TestMethod]
        public void Clamp_KeepsValidModesAndDefaultsEverythingElseToFollowSystem()
        {
            Assert.AreEqual(ThemeModes.Light, ThemeModes.Clamp(ThemeModes.Light));
            Assert.AreEqual(ThemeModes.Dark, ThemeModes.Clamp(ThemeModes.Dark));
            Assert.AreEqual(ThemeModes.FollowSystem, ThemeModes.Clamp(ThemeModes.FollowSystem));
            Assert.AreEqual(ThemeModes.FollowSystem, ThemeModes.Clamp(-1));
            Assert.AreEqual(ThemeModes.FollowSystem, ThemeModes.Clamp(3));
            Assert.AreEqual(ThemeModes.FollowSystem, ThemeModes.Clamp(99));
        }

        // The two conversions must not disagree: a pinned mode round-trips exactly
        [TestMethod]
        public void Conversions_RoundTripForPinnedModes()
        {
            foreach (var mode in new[] { ThemeModes.Light, ThemeModes.Dark })
            {
                var dark = ThemeModes.ToDarkRequest(mode);
                Assert.IsNotNull(dark, $"{mode} must pin a dark flag");
                Assert.AreEqual(mode, ThemeModes.FromDark(dark.Value),
                    $"{mode} must survive ToDarkRequest -> FromDark");
            }
        }
    }
}
