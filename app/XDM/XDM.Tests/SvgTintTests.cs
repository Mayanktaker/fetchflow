// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Guards the SVG tint pass: checkbox icons draw with stroke, so a fill-only
// implementation left them untinted and the select-all button ignored the scheme.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.UI;

namespace XDM.Tests
{
    [TestClass]
    public class SvgTintTests
    {
        private const byte R = 236;
        private const byte G = 72;
        private const byte B = 153;
        private const string Rgb = "rgb(236,72,153)";

        private static string Root
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

        private static string Icon(string name) =>
            File.ReadAllText(Path.Combine(Root, "app", "XDM", "XDM.Gtk.UI", "svg-icons", name + ".svg"));

        // Stroke-drawn checkboxes must carry the tint on every stroke
        [TestMethod]
        public void CheckboxIcons_TintTheirStroke()
        {
            foreach (var name in new[] { "checkbox-checked", "checkbox-unchecked" })
            {
                var source = Icon(name);
                var tinted = SvgTint.Apply(source, R, G, B);

                Assert.AreNotEqual(source, tinted, name + " was not tinted");
                StringAssert.Contains(tinted, $"stroke=\"{Rgb}\"", name + " kept its old stroke");
                Assert.IsFalse(tinted.Contains("stroke=\"rgba(", StringComparison.Ordinal),
                    name + " still carries an rgba stroke");
            }
        }

        // Fill-drawn icons keep the original behaviour, and transparent fill stays transparent
        [TestMethod]
        public void FillIcons_StillTintTheirFill()
        {
            var tinted = SvgTint.Apply(Icon("download-2-line"), R, G, B);

            StringAssert.Contains(tinted, $"fill=\"{Rgb}\"");
            StringAssert.Contains(tinted, "fill=\"none\"");
        }

        // Hex colours tint too, while non-colour strokes are left alone
        [TestMethod]
        public void HexColours_TintButNonColourStrokesSurvive()
        {
            const string svg = "<svg><path fill=\"#0f172a\"/><rect stroke=\"#ffffff\"/>"
                + "<path fill=\"none\" stroke=\"none\"/><path stroke=\"currentColor\"/></svg>";

            var tinted = SvgTint.Apply(svg, R, G, B);

            StringAssert.Contains(tinted, $"fill=\"{Rgb}\"");
            StringAssert.Contains(tinted, $"stroke=\"{Rgb}\"");
            StringAssert.Contains(tinted, "stroke=\"none\"");
            StringAssert.Contains(tinted, "stroke=\"currentColor\"");
        }
    }
}
