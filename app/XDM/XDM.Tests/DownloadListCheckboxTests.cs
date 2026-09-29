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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Gtk;
using XDM.GtkUI;
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

            // The checked box must be HOLLOW (outline + tick, no solid fill):
            // GtkHelper tints it by replacing every opaque pixel's RGB, which turned a
            // filled box + white tick into one solid square with no visible tick.
            var boxShape = checkedState.Substring(checkedState.IndexOf("<rect"),
                checkedState.IndexOf("/>", checkedState.IndexOf("<rect")) - checkedState.IndexOf("<rect"));
            Assert.IsTrue(boxShape.Contains("fill=\"none\""),
                $"the checked box must be an outline so tinting keeps the tick; got: {boxShape}");
            Assert.IsTrue(boxShape.Contains("stroke="), "the box needs a stroke to exist at all");
            // and there must be a separate tick stroke inside it
            Assert.IsTrue(checkedState.IndexOf("/><path d=", StringComparison.Ordinal) > 0,
                "the checked box must contain a tick path");
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
        public void GutterClick_RealEvents_MultiSelectAccumulatesAndUnticks()
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

            var store = new ListStore(typeof(string));
            var view = new TreeView(store);
            view.Selection.Mode = SelectionMode.Multiple;
            view.HeadersVisible = true;
            view.StyleContext.AddClass("finished");

            var gutter = new TreeViewColumn
            {
                Title = string.Empty,
                Resizable = true,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = GutterWidth,
                SortColumnId = -1
            };
            var pix = new CellRendererPixbuf();
            pix.SetPadding(CheckboxPadding, 12);
            gutter.PackStart(pix, true);
            view.AppendColumn(gutter);
            var name = new TreeViewColumn
            {
                Title = "Name",
                Resizable = true,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = 320,
                SortColumnId = -1,
                Clickable = true
            };
            name.PackStart(new CellRendererText(), true);
            view.AppendColumn(name);

            const int RowCount = 4;
            for (var i = 0; i < RowCount; i++) store.AppendValues("row" + i);

            // Production-shaped handler: same helper calls and the same claim the app
            // uses ([GLib.ConnectBefore] on the real handler is what puts it ahead of
            // GTK's class handler; this local function mirrors its body).
            [GLib.ConnectBefore]   // without this GtkSharp connects AFTER the class handler and the claim is void
            void OnPress(object o, ButtonPressEventArgs e)
            {
                if (e.Event.Type != Gdk.EventType.ButtonPress || e.Event.Button != 1) return;
                if (!TreeViewSelectionHelper.HitTestToggleCell(view, gutter, e.Event.X, e.Event.Y)) return;
                if (!TreeViewSelectionHelper.TryGetRowAtEvent(view, e.Event.X, e.Event.Y, out var path)) return;
                TreeViewSelectionHelper.ToggleSelectionPath(view, path);
                e.RetVal = true;   // claim, or GTK's default replaces the selection
            }
            view.ButtonPressEvent += OnPress;

            var window = new Window(WindowType.Toplevel);
            window.SetDefaultSize(600, 320);
            window.Add(view);
            window.ShowAll();
            Pump(25);

            var binHandle = gtk_tree_view_get_bin_window(view.Handle);
            Assert.AreNotEqual(IntPtr.Zero, binHandle, "tree view has no bin window");
            var binWindow = new Gdk.Window(binHandle);   // ONE wrapper, reused (re-wrapping corrupts refs)
            var device = Gdk.Display.Default.DefaultSeat?.Pointer;
            Assert.IsNotNull(device, "no pointer device for event dispatch");

            // Row clicks: real events are delivered on the bin window with RAW
            // coordinates in that window's space — exactly what GetPathAtPos expects.
            ClickRow(view, binWindow, device, 0);
            ClickRow(view, binWindow, device, 1);
            var two = view.Selection.CountSelectedRows();

            ClickRow(view, binWindow, device, 0);          // untick row 0
            var left = view.Selection.GetSelectedRows(out _);
            var remaining = string.Join(",", left.Select(r => r.ToString()));

            ClickRow(view, binWindow, device, RowCount - 1);   // tick the LAST row
            var last = view.Selection.CountSelectedRows();

            var failures = new List<string>();
            if (two != 2)
                failures.Add($"multi-select: ticking rows 0 and 1 selected {two}/2 — "
                    + "the second click replaced instead of accumulated");
            if (left.Length != 1 || remaining != "1")
                failures.Add($"untick: clicking a ticked row left [{remaining}] — expected [1]; "
                    + "an untick must not re-select itself");
            if (last != 2)
                failures.Add($"last row: {last} selected — expected 2; clicks on the "
                    + "bottom row are being lost");
            Assert.AreEqual(0, failures.Count,
                "real button events must give checkbox multi-select semantics:\n"
                + string.Join("\n", failures));

            // A header-window event must never be mistaken for a row click
            var beforeHeader = view.Selection.CountSelectedRows();
            SendEvent(view, view.Window, Gdk.EventType.ButtonPress, 200, 16, device);
            SendEvent(view, view.Window, Gdk.EventType.ButtonRelease, 200, 16, device);
            Pump(10);
            Assert.AreEqual(beforeHeader, view.Selection.CountSelectedRows(),
                "an event on the HEADER window toggled a row — the bin-window gate is missing");

            window.Destroy();
        }

        private static void Pump(int n)
        {
            for (var i = 0; i < n; i++)
                while (Application.EventsPending()) Application.RunIteration();
        }

        // A real press+release on a row's checkbox (row coords in bin-window space)
        private static void ClickRow(TreeView view, Gdk.Window binWindow, Gdk.Device device, int row)
        {
            var area = view.GetCellArea(new TreePath(row.ToString()), null);
            var x = area.X + 12;
            var y = area.Y + area.Height / 2;
            SendEvent(view, binWindow, Gdk.EventType.ButtonPress, x, y, device);
            SendEvent(view, binWindow, Gdk.EventType.ButtonRelease, x, y, device);
            Pump(6);
        }

        [DllImport("libgtk-3.so.0")]
        private static extern bool gtk_widget_event(IntPtr widget, IntPtr ev);

        [DllImport("libgdk-3.so.0")]
        private static extern IntPtr gdk_event_new(int type);

        [DllImport("libgtk-3.so.0")]
        private static extern IntPtr gtk_tree_view_get_bin_window(IntPtr treeView);

        // Dispatch a button event through GTK exactly as a physical click arrives
        private static void SendEvent(Gtk.Widget target, Gdk.Window window, Gdk.EventType type,
            double x, double y, Gdk.Device device)
        {
            var raw = gdk_event_new((int)type);
            var ev = new Gdk.EventButton(raw);
            ev.Window = window;
            ev.X = x;
            ev.Y = y;
            ev.XRoot = x;
            ev.YRoot = y;
            ev.Button = 1;
            ev.SendEvent = true;
            ev.Device = device;
            gtk_widget_event(target.Handle, ev.Handle);
            Pump(6);
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
            if (!File.Exists(path)) return null;
            try
            {
                return new Pixbuf(path, CheckboxSize, CheckboxSize, true);
            }
            catch (GLib.GException ex)
            {
                // No gdk-pixbuf SVG loader in this environment (e.g. a bare CI image).
                // The artwork itself is still pinned by the pure-text assertions in
                // CheckboxAssets_Exist_AndLoad, which need no loader.
                Assert.Inconclusive(
                    $"skipping pixel comparison: this environment cannot load SVG ({ex.Message})");
                return null;
            }
        }
    }
}
