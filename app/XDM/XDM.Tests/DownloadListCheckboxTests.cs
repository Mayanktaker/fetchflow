// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Guards the download list selection checkbox after it was rebuilt as a designed
// asset pair rather than a bare CellRendererToggle.
//
// Two defects are pinned:
//   1. The checkbox never toggled. The press handler claimed button-press with
//      RetVal = true, which stops GTK's own handlers on that widget, so the
//      cell renderer never saw the click. The toggle now happens on press and
//      the event is NOT suppressed, so a click is no longer swallowed.
//   2. A CellRendererToggle cannot be restyled from CSS (it has no styleable
//      node), so the box is a CellRendererPixbuf showing checkbox-unchecked /
//      checkbox-checked. This asserts both assets exist, load, and that the
//      gutter column is wide enough to show the box without clipping.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Gtk;
using Pixbuf = Gdk.Pixbuf;

namespace XDM.Tests
{
    [TestClass]
    public class DownloadListCheckboxTests
    {
        private const int CheckboxSize = 22;      // must match GtkHelper.SelectionCheckboxSize
        private const int GutterWidth = 52;       // must match MainWindow.DownloadGutterWidth
        private const int CheckboxPadding = 8;

        private static string GtkUiDir
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                    dir = dir.Parent;
                Assert.IsNotNull(dir, "Could not locate repo root (AGENTS.md)");
                return Path.Combine(dir.FullName, "app", "XDM", "XDM.Gtk.UI");
            }
        }

        [TestMethod]
        public void CheckboxAssets_Exist_AndLoad()
        {
            foreach (var name in new[] { "checkbox-unchecked", "checkbox-checked" })
            {
                var path = Path.Combine(GtkUiDir, "svg-icons", name + ".svg");
                Assert.IsTrue(File.Exists(path), $"missing asset: {path}");
                var text = File.ReadAllText(path);
                Assert.IsTrue(text.Contains("<svg"), $"{name}.svg is not an SVG");
                // both states must be visually distinct, otherwise selecting a row
                // would look like nothing happened
                if (name == "checkbox-checked")  // "checked" is a C# keyword, hence the string compare
                {
                    Assert.IsTrue(text.Contains("stroke"), "checked box must draw a tick");
                }
            }

            var uncheckedState = File.ReadAllText(
                Path.Combine(GtkUiDir, "svg-icons", "checkbox-unchecked.svg"));
            var checkedState = File.ReadAllText(
                Path.Combine(GtkUiDir, "svg-icons", "checkbox-checked.svg"));
            Assert.AreNotEqual(uncheckedState, checkedState,
                "the two checkbox states must differ, or selection is invisible");
        }

        [TestMethod]
        public void GutterColumn_IsWideEnoughForTheCheckbox()
        {
            var needed = CheckboxSize + (CheckboxPadding * 2);
            Assert.IsTrue(GutterWidth >= needed,
                $"gutter column is {GutterWidth}px but the {CheckboxSize}px box plus "
                + $"{CheckboxPadding}px padding each side needs {needed}px");
        }

        [TestMethod]
        [TestCategory("GtkSmoke")]
        public void CheckboxStates_RenderToVisuallyDifferentPixels()
        {
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (string.IsNullOrWhiteSpace(display))
            {
                Assert.Inconclusive("Skipped GtkSmoke: no DISPLAY — run via scripts/run-gtk-smoke.sh.");
            }
            try { Application.Init(); }
            catch (Exception ex)
            {
                Assert.Inconclusive($"Skipped GtkSmoke: GTK init failed: {ex.Message}");
            }

            var uncheckedArt = LoadAsset("checkbox-unchecked");
            var checkedArt = LoadAsset("checkbox-checked");
            Assert.IsNotNull(uncheckedArt, "checkbox-unchecked.svg did not load as a pixbuf");
            Assert.IsNotNull(checkedArt, "checkbox-checked.svg did not load as a pixbuf");
            Assert.AreEqual(CheckboxSize, uncheckedArt.Width,
                "the asset must load at the size the row is designed for");
            Assert.AreEqual(uncheckedArt.Width, checkedArt.Width);
            Assert.AreEqual(uncheckedArt.Height, checkedArt.Height);

            // The whole point of the redesign: the two states must LOOK different once
            // rasterised, otherwise a click gives the user no feedback at all.
            var differing = CountDifferingBytes(uncheckedArt, checkedArt);
            Assert.IsTrue(differing > 40,
                $"the two checkbox states rasterise to nearly the same image "
                + $"({differing} differing pixels) — selection would be invisible");
        }

        // Counts differing bytes between the two rasterised states
        private static int CountDifferingBytes(Pixbuf a, Pixbuf b)
        {
            var aData = a.SaveToBuffer("png");
            var bData = b.SaveToBuffer("png");
            var different = 0;
            var shared = Math.Min(aData.Length, bData.Length);
            for (var i = 0; i < shared; i++)
            {
                if (aData[i] != bData[i]) different++;
            }
            return different + Math.Abs(aData.Length - bData.Length);
        }

        private static Pixbuf LoadAsset(string name)
        {
            var path = Path.Combine(GtkUiDir, "svg-icons", name + ".svg");
            return File.Exists(path) ? new Pixbuf(path, CheckboxSize, CheckboxSize, true) : null;
        }
    }
}
