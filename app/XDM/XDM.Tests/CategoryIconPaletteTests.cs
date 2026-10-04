// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core;
using XDM.Core.UI;

namespace XDM.Tests
{
    /// <summary>
    /// Covers the category icon/colour decision table. These cases were previously
    /// unreachable: the table lived inside MainWindow, which cannot be instantiated
    /// without a GTK display.
    /// </summary>
    [TestClass]
    public class CategoryIconPaletteTests
    {
        private static Category Cat(string name, string? icon = null, string? color = null)
            => new Category { Name = name, CustomIcon = icon!, CustomColor = color! };

        [TestMethod]
        public void GetIconName_MapsEachBuiltInCategory()
        {
            Assert.AreEqual("file-text-line", CategoryIconPalette.GetIconName(Cat("CAT_DOCUMENTS")));
            Assert.AreEqual("file-music-line", CategoryIconPalette.GetIconName(Cat("CAT_MUSIC")));
            Assert.AreEqual("movie-line", CategoryIconPalette.GetIconName(Cat("CAT_VIDEOS")));
            Assert.AreEqual("file-zip-line", CategoryIconPalette.GetIconName(Cat("CAT_COMPRESSED")));
            Assert.AreEqual("function-line", CategoryIconPalette.GetIconName(Cat("CAT_PROGRAMS")));
            Assert.AreEqual("image-line", CategoryIconPalette.GetIconName(Cat("CAT_IMAGES")));
        }

        [TestMethod]
        public void GetIconName_UnknownCategoryFallsBackToFolder()
        {
            Assert.AreEqual("folder-shared-line", CategoryIconPalette.GetIconName(Cat("CAT_WHATEVER")));
            Assert.AreEqual("folder-shared-line", CategoryIconPalette.GetIconName(Cat(string.Empty)));
        }

        [TestMethod]
        public void GetIconName_CustomIconOverridesBuiltIn()
        {
            Assert.AreEqual("my-custom-icon",
                CategoryIconPalette.GetIconName(Cat("CAT_MUSIC", icon: "my-custom-icon")));
        }

        [TestMethod]
        public void GetIconName_EmptyCustomIconFallsBackToBuiltIn()
        {
            Assert.AreEqual("movie-line", CategoryIconPalette.GetIconName(Cat("CAT_VIDEOS", icon: string.Empty)));
        }

        [TestMethod]
        public void TryParseHexColor_ParsesRgbTriple()
        {
            Assert.IsTrue(CategoryIconPalette.TryParseHexColor("#f59e0b", out byte r, out byte g, out byte b));
            Assert.AreEqual((byte)245, r);
            Assert.AreEqual((byte)158, g);
            Assert.AreEqual((byte)11, b);
        }

        [TestMethod]
        public void TryParseHexColor_RejectsMalformedInput()
        {
            // Length/prefix guards: these must not fall through as a partial decode.
            Assert.IsFalse(CategoryIconPalette.TryParseHexColor(null, out _, out _, out _));
            Assert.IsFalse(CategoryIconPalette.TryParseHexColor(string.Empty, out _, out _, out _));
            Assert.IsFalse(CategoryIconPalette.TryParseHexColor("#fff", out _, out _, out _));       // short
            Assert.IsFalse(CategoryIconPalette.TryParseHexColor("f59e0b", out _, out _, out _));      // no '#'
            Assert.IsFalse(CategoryIconPalette.TryParseHexColor("#f59e0bff", out _, out _, out _));   // too long
            Assert.IsFalse(CategoryIconPalette.TryParseHexColor("#gggggg", out _, out _, out _));    // not hex
            Assert.IsFalse(CategoryIconPalette.TryParseHexColor("#ff59zz", out _, out _, out _));    // one bad pair
        }

        [TestMethod]
        public void GetDefaultColor_IsStablePerCategory()
        {
            Assert.AreEqual((byte)245, CategoryIconPalette.GetDefaultColor("CAT_DOCUMENTS").r);
            Assert.AreEqual((byte)168, CategoryIconPalette.GetDefaultColor("CAT_MUSIC").r);
            Assert.AreEqual((byte)239, CategoryIconPalette.GetDefaultColor("CAT_VIDEOS").r);
            // Unknown categories share the Indigo default.
            Assert.AreEqual(CategoryIconPalette.GetDefaultColor("CAT_PROGRAMS"),
                            CategoryIconPalette.GetDefaultColor("CAT_NOPE"));
        }

        [TestMethod]
        public void Resolve_UsesCustomColorWhenValid()
        {
            var (iconName, r, g, b) = CategoryIconPalette.Resolve(Cat("CAT_DOCUMENTS", color: "#123456"));
            Assert.AreEqual("file-text-line", iconName);
            Assert.AreEqual((byte)0x12, r);
            Assert.AreEqual((byte)0x34, g);
            Assert.AreEqual((byte)0x56, b);
        }

        [TestMethod]
        public void Resolve_FallsBackToDefaultOnMalformedCustomColor()
        {
            // An invalid override must not blank the colour — the built-in takes over.
            var (iconName, r, _, _) = CategoryIconPalette.Resolve(Cat("CAT_DOCUMENTS", color: "not-a-color"));
            Assert.AreEqual("file-text-line", iconName);
            Assert.AreEqual((byte)245, r);
        }

        [TestMethod]
        public void Resolve_IconAndColorOverrideIndependently()
        {
            // Custom icon but no custom colour: icon wins, built-in colour stays.
            var (iconName, r, _, _) = CategoryIconPalette.Resolve(Cat("CAT_VIDEOS", icon: "star-line"));
            Assert.AreEqual("star-line", iconName);
            Assert.AreEqual((byte)239, r);
        }
    }
}