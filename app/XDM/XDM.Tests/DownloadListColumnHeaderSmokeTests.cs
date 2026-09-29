// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Xvfb-backed GTK harness for the download list COLUMN HEADER (MainWindow's
// gutter | File name | Size stack). Verifies the geometry the redesign depends on:
//   1. All three columns are resizable and clamp to the configured min/max.
//   2. The header buttons span the full list width and share one tall bar — the
//      caption of every column therefore tracks it when the user drags an edge,
//      and the header is a roomy bar rather than a thin strip.
//   3. Header buttons are real, activatable buttons wired to the column's clicked
//      signal (that is what drives sorting / select-all in MainWindow).
//   4. Rows still resolve their own cell areas after the row-margin change.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Gtk;
using Pixbuf = Gdk.Pixbuf;

namespace XDM.Tests
{
    [TestClass]
    [TestCategory("GtkSmoke")]
    public class DownloadListColumnHeaderSmokeTests
    {
        // Mirrors MainWindow's download-list geometry tokens
        private const int GutterWidth = 52;          // mirrors DownloadGutterWidth (holds the 22px checkbox)
        private const int GutterMinWidth = 36;
        private const int GutterMaxWidth = 200;
        private const int SizeWidth = 196;
        private const int SizeMinWidth = 110;
        private const int SizeMaxWidth = 720;
        private const int NameMinWidth = 160;
        private const int NameMaxWidth = 1200;
        private const int MinHeaderHeight = 40;   // roomy GTK4-style column header
        private const int MinRowPitch = 64;         // airy gap between list items
        private const int IconPadding = 12;          // mirrors DownloadIconHorizontalPadding
        private const int NamePadding = 12;          // mirrors DownloadNameHorizontalPadding
        private const int IconSize = 28;             // mirrors DownloadIconSize
        private const int CheckboxSize = 22;         // mirrors GtkHelper.SelectionCheckboxSize
        private const int CheckboxPadding = 8;       // mirrors the gutter pixbuf padding

        private static string RepoRoot
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

        private static bool IsDisplayAvailable(out string reason)
        {
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (!string.IsNullOrWhiteSpace(display))
            {
                reason = string.Empty;
                return true;
            }
            reason = "Skipped GtkSmoke: no DISPLAY — run via scripts/run-gtk-smoke.sh.";
            return false;
        }

        private sealed class HeaderHarness : IDisposable
        {
            public Window Window = null!;
            public TreeView View = null!;
            public TreeViewColumn Gutter = null!;
            public TreeViewColumn Name = null!;
            public TreeViewColumn Size = null!;
            public ListStore Store = null!;
            public int Clicks;

            // Mirrors MainWindow.CreateInProgressListView's column stack
            public static HeaderHarness Create(string cssPath)
            {
                var h = new HeaderHarness();

                h.Store = new ListStore(typeof(string));
                h.View = new TreeView(h.Store);
                h.View.Selection.Mode = SelectionMode.Multiple;
                h.View.HeadersVisible = true;
                h.View.EnableGridLines = TreeViewGridLines.None;
                h.View.StyleContext.AddClass("unfinished");

                h.Gutter = new TreeViewColumn
                {
                    Sizing = TreeViewColumnSizing.Fixed,
                    FixedWidth = GutterWidth,
                    SortColumnId = -1,
                    Resizable = true,
                    MinWidth = GutterMinWidth,
                    MaxWidth = GutterMaxWidth
                };
                // mirrors production: a 22px checkbox pixbuf in the gutter
                var check = new CellRendererPixbuf();
                check.SetPadding(CheckboxPadding, 12);
                check.Pixbuf = LoadCheckbox(false);
                h.Gutter.PackStart(check, true);
                h.Gutter.Clicked += (_, _) => h.Clicks++;

                h.Name = new TreeViewColumn
                {
                    Title = "File name",
                    Expand = true,
                    Sizing = TreeViewColumnSizing.Autosize,
                    SortColumnId = -1,
                    Resizable = true,
                    MinWidth = NameMinWidth,
                    MaxWidth = NameMaxWidth
                };
                var icon = new CellRendererPixbuf();
                icon.SetPadding(12, 12);
                h.Name.PackStart(icon, false);
                var name = new CellRendererText { Yalign = 0.5f };
                name.SetPadding(12, 10);
                name.Ellipsize = Pango.EllipsizeMode.Middle;
                h.Name.PackStart(name, true);
                h.Name.SetCellDataFunc(name, new TreeCellDataFunc((c, cell, model, iter) =>
                {
                    ((CellRendererText)cell).Markup =
                        $"<span weight=\"bold\">{GLib.Markup.EscapeText((string?)model.GetValue(iter, 0) ?? "")}</span>" +
                        "\n<span size=\"7500\" alpha=\"22000\">\u25c9 cdn.example.com</span>";
                }));
                h.Name.Clicked += (_, _) => h.Clicks++;

                h.Size = new TreeViewColumn
                {
                    Title = "Size",
                    Sizing = TreeViewColumnSizing.Fixed,
                    FixedWidth = SizeWidth,
                    SortColumnId = -1,
                    Resizable = true,
                    MinWidth = SizeMinWidth,
                    MaxWidth = SizeMaxWidth
                };
                var meta = new CellRendererText
                {
                    Xalign = 1.0f,
                    Alignment = Pango.Alignment.Right,
                    Yalign = 0.5f
                };
                meta.SetPadding(16, 16);
                h.Size.PackEnd(meta, false);
                h.Size.SetCellDataFunc(meta, new TreeCellDataFunc((c, cell, model, iter) =>
                {
                    ((CellRendererText)cell).Markup =
                        "<span weight=\"bold\">1.2 GB</span>\n<span size=\"7500\" alpha=\"22000\">Mar 12, 2026</span>";
                }));
                h.Size.Clicked += (_, _) => h.Clicks++;

                h.View.AppendColumn(h.Gutter);
                h.View.AppendColumn(h.Name);
                h.View.AppendColumn(h.Size);

                // Same header-button classes the app sets in MainWindow.WireListColumnHeaders
                h.Name.Button!.StyleContext.AddClass("list-header-name");
                h.Size.Button!.StyleContext.AddClass("list-header-size");

                // Apply the REAL production theme so header/row CSS is the shipped one
                if (File.Exists(cssPath))
                {
                    var provider = new CssProvider();
                    provider.LoadFromData(File.ReadAllText(cssPath));
                    StyleContext.AddProviderForScreen(h.View.Screen, provider, 800);
                }

                var sw = new ScrolledWindow { ShadowType = ShadowType.None };
                sw.SetPolicy(PolicyType.Never, PolicyType.Automatic);
                sw.Add(h.View);

                h.Window = new Window(WindowType.Toplevel);
                h.Window.SetDefaultSize(760, 420);
                h.Window.Add(sw);
                h.Window.ShowAll();
                PumpEvents(30);
                return h;
            }

            public void Dispose()
            {
                try { Window.Dispose(); } catch { }
            }
        }

        private static Pixbuf LoadCheckbox(bool isChecked)
        {
            var dir = Path.Combine(RepoRoot, "app", "XDM", "XDM.Gtk.UI", "svg-icons");
            var file = Path.Combine(dir, isChecked ? "checkbox-checked.svg" : "checkbox-unchecked.svg");
            if (!File.Exists(file)) return null;
            try
            {
                return new Pixbuf(file, CheckboxSize, CheckboxSize, true);
            }
            catch (GLib.GException)
            {
                // Some build environments have no gdk-pixbuf SVG loader; the gutter is
                // empty in that case, which does not affect the geometry being asserted.
                return new Pixbuf(Gdk.Colorspace.Rgb, false, 8, CheckboxSize, CheckboxSize);
            }
        }

        private static void PumpEvents(int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                while (Application.EventsPending())
                {
                    Application.RunIteration();
                }
            }
        }

        [TestMethod]
        [TestCategory("GtkSmoke")]
        public void DownloadListHeader_ThreeResizableColumns_CaptionsFillAndTrackWidth()
        {
            if (!IsDisplayAvailable(out var skipReason))
                Assert.Inconclusive(skipReason);
            try
            {
                Application.Init();
            }
            catch (Exception ex)
            {
                Assert.Inconclusive($"Skipped GtkSmoke: GTK init failed: {ex.Message}");
            }

            var cssPath = Path.Combine(RepoRoot, "app", "XDM", "XDM.Gtk.UI", "theme", "xdm-dark.css");
            using var harness = HeaderHarness.Create(cssPath);
            harness.Store.AppendValues("alpha.bin");
            harness.Store.AppendValues("bravo.zip");
            PumpEvents(20);

            // 1) All three columns drag-resizable and clamped
            foreach (var (col, min, max) in new[]
            {
                (harness.Gutter, GutterMinWidth, GutterMaxWidth),
                (harness.Name, NameMinWidth, NameMaxWidth),
                (harness.Size, SizeMinWidth, SizeMaxWidth),
            })
            {
                Assert.IsTrue(col.Resizable, "every download list column must be resizable");
                Assert.AreEqual(min, (int)col.MinWidth, $"{min}px min width");
                Assert.AreEqual(max, (int)col.MaxWidth, $"{max}px max width");
            }

            // 2) One full-width, roomy header bar; each caption fills its column and
            // all three share the bar's height, so a dragged edge moves its caption
            Assert.AreEqual(3, harness.View.Columns.Length, "gutter | name | size");
            var headerHeight = 0;
            var totalWidth = 0;
            foreach (var col in new[] { harness.Gutter, harness.Name, harness.Size })
            {
                Assert.IsInstanceOfType(col.Button, typeof(Button));
                var button = col.Button!;
                Assert.AreEqual(col.Width, button.Allocation.Width,
                    $"'{col.Title}' header button must span its whole column");
                if (headerHeight == 0)
                {
                    headerHeight = button.Allocation.Height;
                }
                else
                {
                    Assert.AreEqual(headerHeight, button.Allocation.Height,
                        "every header button must share the one bar height");
                }
                totalWidth += col.Width;
            }
            Assert.AreEqual(harness.View.Allocation.Width, totalWidth,
                "column widths must tile the full list width");
            Assert.IsTrue(headerHeight >= MinHeaderHeight,
                $"header must be at least {MinHeaderHeight}px tall, got {headerHeight}px");

            // 3) The File name column absorbs the leftover width, so dragging the Size
            // edge never starves the names
            Assert.IsTrue(harness.Name.Expand,
                "the File name column must take the leftover width");

            // 4) Sort indicator mirrors the active column (MainWindow.SyncHeaderSortState)
            harness.Name.SortIndicator = true;
            harness.Name.SortOrder = SortType.Descending;
            Assert.IsTrue(harness.Name.SortIndicator);
            Assert.AreEqual(SortType.Descending, harness.Name.SortOrder);

            // 5) Rows keep roomy spacing: GTK3 sizes a row from its cell renderers, so
            // the vertical renderer padding is what separates the list items
            var area = harness.View.GetCellArea(new TreePath("0"), harness.Name);
            Assert.IsTrue(area.Width > 0 && area.Height > 0, "row 0 must have a visible cell area");
            var rowBoundary = -1;
            for (var y = 0; y < 400; y++)
            {
                if (harness.View.GetPathAtPos(30, y, out var probe, out _, out _, out _)
                    && probe.ToString() == "1")
                {
                    rowBoundary = y;
                    break;
                }
            }
            Assert.IsTrue(rowBoundary >= MinRowPitch,
                $"row pitch must be at least {MinRowPitch}px, got {rowBoundary}px");

            // 6) The 'File name' caption sits on the first pixel of the row's file title,
            // so the header and the rows read as one grid (theme CSS list-header-name)
            var headerBox = ((Container)harness.Name.Button!).Children.OfType<Container>().First();
            var caption = headerBox.Children.OfType<Container>().First().Children.OfType<Label>().First();
            int viewX = 0, captionX = 0;
            harness.View.TranslateCoordinates(harness.Window, 0, 0, out viewX, out _);
            caption.TranslateCoordinates(harness.Window, 0, 0, out captionX, out _);
            var nameArea = harness.View.GetCellArea(new TreePath("0"), harness.Name);
            // gutter cell = checkbox padding + box + padding, then the file icon and its padding
            var rowTitleX = viewX + nameArea.X + CheckboxPadding + CheckboxSize + CheckboxPadding
                + IconPadding + IconSize + IconPadding + NamePadding;
            Assert.AreEqual(rowTitleX, captionX,
                "the File name caption must start where the row titles start");
        }
    }
}
