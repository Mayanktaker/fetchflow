// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.Util;

namespace XDM.Tests
{
    [TestClass]
    public class PaletteJsonTests
    {
        [TestMethod]
        public void TestExportAndImportPalettesJson()
        {
            var palettes = new List<PaletteModel>
            {
                new PaletteModel { Id = "dracula_orchid", DisplayName = "Dracula Orchid", CssFileName = "xdm-dark-orchid.css", AccentHex = "#ec4899" },
                new PaletteModel { Id = "matcha_forest", DisplayName = "Matcha Forest", CssFileName = "xdm-light-matcha.css", AccentHex = "#059669" }
            };

            string json = ThemePaletteHelper.ExportPalettes(palettes);
            Assert.IsFalse(string.IsNullOrWhiteSpace(json));
            Assert.IsTrue(json.Contains("dracula_orchid"));
            Assert.IsTrue(json.Contains("matcha_forest"));

            var imported = ThemePaletteHelper.ImportPalettes(json);
            Assert.AreEqual(2, imported.Count);
            Assert.AreEqual("dracula_orchid", imported[0].Id);
            Assert.AreEqual("#ec4899", imported[0].AccentHex);
            Assert.AreEqual("matcha_forest", imported[1].Id);
            Assert.AreEqual("#059669", imported[1].AccentHex);
        }
    }
}
