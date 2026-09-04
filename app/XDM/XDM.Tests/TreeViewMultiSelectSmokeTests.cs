// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Xvfb-backed GTK harness — replicates the exact MainWindow in-progress download
// list stack (ListStore → TreeModelFilter → TreeModelSort, SelectionMode.Multiple,
// theme CSS with row margins) and verifies:
//   1. Hit-testing: GetPathAtPos at each row's visible CellArea center resolves
//      back to the SAME row (guards against CSS row-margin hitbox misalignment,
//      which would make ctrl+clicks toggle the wrong rows and silently collapse
//      multi-selections — the reported "only one of N deleted" bug).
//   2. Multi-selection retrieval: selecting N rows (ctrl+click equivalent) yields
//      N distinct items through the same read path MainWindow uses.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Gtk;
using XDM.Core;

namespace XDM.Tests
{
    [TestClass]
    [TestCategory("GtkSmoke")]
    public class TreeViewMultiSelectSmokeTests
    {
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

        // Minimal stand-in for MainWindow's CreateInProgressListView construction
        private sealed class InprogressHarness : IDisposable
        {
            public Window Window = null!;
            public TreeView View = null!;
            public TreeViewColumn CardColumn = null!;
            public TreeViewColumn CheckColumn = null!;
            public ListStore Store = null!;
            public TreeModelSort Sorted = null!;

            public static InprogressHarness Create(string cssPath)
            {
                var h = new InprogressHarness();

                h.Store = new ListStore(typeof(string), typeof(string), typeof(string),
                    typeof(int), typeof(string), typeof(InProgressDownloadItem));

                var filter = new TreeModelFilter(h.Store, null);
                filter.VisibleFunc = (model, iter) => true;

                var sorted = new TreeModelSort(filter);
                sorted.SetSortFunc(1, (model, i1, i2) =>
                {
                    var t1 = (InProgressDownloadItem?)model.GetValue(i1, 5);
                    var t2 = (InProgressDownloadItem?)model.GetValue(i2, 5);
                    if (t1 == null && t2 == null) return 0;
                    if (t1 == null) return 1;
                    if (t2 == null) return 2;
                    return t1.DateAdded.CompareTo(t2.DateAdded);
                });
                h.Sorted = sorted;

                h.View = new TreeView(sorted);
                h.View.Selection.Mode = SelectionMode.Multiple;
                h.View.HeadersVisible = false;
                h.View.EnableGridLines = TreeViewGridLines.None;
                h.View.StyleContext.AddClass("unfinished");

                var col = new TreeViewColumn
                {
                    Expand = true,
                    Sizing = TreeViewColumnSizing.Autosize,
                    Spacing = 0
                };
                // Dedicated checkbox column (same construction as MainWindow)
                var checkCol = new TreeViewColumn
                {
                    Sizing = TreeViewColumnSizing.Fixed,
                    FixedWidth = 38,
                    Resizable = false
                };
                var check = new CellRendererToggle { Activatable = false };
                check.SetPadding(6, 8);
                checkCol.PackStart(check, true);
                checkCol.SetCellDataFunc(check, new CellLayoutDataFunc((_, cell, model, iter) =>
                {
                    ((CellRendererToggle)cell).Active =
                        h.View.Selection.PathIsSelected(model.GetPath(iter));
                }));
                h.View.AppendColumn(checkCol);
                h.CheckColumn = checkCol;
                var icon = new CellRendererPixbuf { };
                icon.SetPadding(12, 8);
                col.PackStart(icon, false);

                var name = new CellRendererText();
                name.SetPadding(12, 6);
                name.Ellipsize = Pango.EllipsizeMode.Middle;
                col.PackStart(name, true);
                col.SetCellDataFunc(name, new TreeCellDataFunc((c, cell, model, iter) =>
                {
                    ((CellRendererText)cell).Text = (string?)model.GetValue(iter, 0) ?? "";
                }));

                var meta = new CellRendererText { Xalign = 1.0f, Alignment = Pango.Alignment.Right };
                meta.SetPadding(16, 12);
                col.PackEnd(meta, false);
                col.SetCellDataFunc(meta, new TreeCellDataFunc((c, cell, model, iter) =>
                {
                    ((CellRendererText)cell).Text = (string?)model.GetValue(iter, 4) ?? "";
                }));

                h.View.AppendColumn(col);
                h.CardColumn = col;

                sorted.SetSortColumnId(1, SortType.Descending);

                // Apply the REAL production theme so row margins/padding match the app
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
                h.Window.SetDefaultSize(420, 520);
                h.Window.Add(sw);
                h.Window.ShowAll();
                PumpEvents(20);
                return h;
            }

            public void Dispose()
            {
                try { Window.Dispose(); } catch { }
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

        private static InProgressDownloadItem MakeItem(string id, string name, int minutesAgo)
        {
            return new InProgressDownloadItem
            {
                Id = id,
                Name = name,
                DateAdded = DateTime.Now.AddMinutes(-minutesAgo),
                Progress = 42,
                Status = DownloadStatus.Stopped
            };
        }

        [TestMethod]
        [TestCategory("GtkSmoke")]
        public void InprogressList_HitTestAndMultiSelect_RoundTrip()
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
            using var harness = InprogressHarness.Create(cssPath);

            // Seed 4 stopped rows exactly like the app does
            var items = new[]
            {
                MakeItem("id-A", "stopped-alpha.bin", 1),
                MakeItem("id-B", "stopped-bravo.zip", 2),
                MakeItem("id-C", "stopped-charlie.iso", 3),
                MakeItem("id-D", "stopped-delta.mp4", 4),
            };
            foreach (var it in items)
            {
                harness.Store.AppendValues(it.Name,
                    it.DateAdded.ToString("MMM d, yyyy · HH:mm"), "1.2 GB", it.Progress,
                    "Stopped", it);
            }
            PumpEvents(10);

            // Collect the VIEW paths (sorted model order) for all 4 rows
            var viewPaths = new List<TreePath>();
            harness.Sorted.Foreach((model, path, iter) =>
            {
                viewPaths.Add(path.Copy());
                return false;
            });
            Assert.AreEqual(4, viewPaths.Count, "sorted view should expose all 4 rows");

            // 1) HIT TEST: each row's visible CellArea center must resolve to itself
            var misaligned = new List<string>();
            foreach (var path in viewPaths)
            {
                var area = harness.View.GetCellArea(path, harness.CardColumn);
                int cx = Math.Max(1, area.X + Math.Min(60, area.Width / 2));
                int cy = area.Y + Math.Max(2, area.Height / 2);
                if (harness.View.GetPathAtPos(cx, cy, out var hit, out _, out _, out _))
                {
                    if (hit.Compare(path) != 0)
                    {
                        misaligned.Add($"row {path} area=({area.X},{area.Y},{area.Width},{area.Height}) " +
                            $"center=({cx},{cy}) hit={hit}");
                    }
                }
                else
                {
                    misaligned.Add($"row {path} center=({cx},{cy}) resolved to NO row");
                }
            }
            Assert.AreEqual(0, misaligned.Count,
                "GetPathAtPos must resolve each visible row's center to that row — " +
                "CSS row margin/padding must not offset hitboxes (ctrl+click would " +
                "toggle the wrong row and silently collapse multi-selection).\n" +
                string.Join("\n", misaligned));

            // 2) MULTI-SELECT retrieval through the app's read path
            // (SelectPath x3 == ctrl+click accumulation semantics)
            harness.View.Selection.SelectPath(viewPaths[0]);
            harness.View.Selection.SelectPath(viewPaths[1]);
            harness.View.Selection.SelectPath(viewPaths[2]);

            var rows = harness.View.Selection.GetSelectedRows(out var model);
            Assert.AreEqual(3, rows.Length,
                $"SelectionMode.Multiple must retain 3 rows, got {rows.Length}");

            var ids = new HashSet<string>();
            foreach (var row in rows)
            {
                if (model.GetIter(out var iter, row)
                    && model.GetValue(iter, 5) is InProgressDownloadItem ent)
                {
                    ids.Add(ent.Id);
                }
            }
            Assert.AreEqual(3, ids.Count,
                "all 3 selected rows must resolve to distinct download items");
            Assert.IsTrue(ids.Contains("id-A") && ids.Contains("id-B") && ids.Contains("id-C"),
                $"selected ids must be id-A/id-B/id-C, got [{string.Join(",", ids)}]");

            // 3) RIGHT-CLICK PRESERVATION predicate (file-manager semantics):
            // press inside the multi-selection => preserve; press on an unselected
            // row or empty area => let GTK's default (re)select happen.
            var selRowArea = harness.View.GetCellArea(viewPaths[1], harness.CardColumn);
            var selCenterX = (double)(selRowArea.X + 60);
            var selCenterY = (double)(selRowArea.Y + selRowArea.Height / 2);
            Assert.IsTrue(XDM.GtkUI.TreeViewSelectionHelper.ShouldPreserveSelectionOnPress(
                    harness.View, selCenterX, selCenterY),
                "right-click on a row INSIDE the multi-selection must preserve it");

            var unselRowArea = harness.View.GetCellArea(viewPaths[3], harness.CardColumn);
            var unselCenterX = (double)(unselRowArea.X + 60);
            var unselCenterY = (double)(unselRowArea.Y + unselRowArea.Height / 2);
            Assert.IsFalse(XDM.GtkUI.TreeViewSelectionHelper.ShouldPreserveSelectionOnPress(
                    harness.View, unselCenterX, unselCenterY),
                "right-click on a row OUTSIDE the selection must NOT preserve it");

            // Empty area below the last row must not preserve either
            var lastArea = harness.View.GetCellArea(viewPaths[0], harness.CardColumn);
            Assert.IsFalse(XDM.GtkUI.TreeViewSelectionHelper.ShouldPreserveSelectionOnPress(
                    harness.View, 10, lastArea.Y + lastArea.Height + 200),
                "right-click on empty area must NOT preserve the selection");

            // 4) CHECKBOX hit test + pure-toggle semantics: a click in the checkbox
            // column must toggle that row's membership without touching the others
            var rowArea = harness.View.GetCellArea(viewPaths[0], harness.CheckColumn);
            var checkX = rowArea.X + Math.Max(2, rowArea.Width / 2);
            Assert.IsTrue(XDM.GtkUI.TreeViewSelectionHelper.HitTestToggleCell(
                    harness.View, harness.CheckColumn, checkX, rowArea.Y + rowArea.Height / 2),
                "click inside the checkbox column must hit-test true");
            var bodyArea = harness.View.GetCellArea(viewPaths[0], harness.CardColumn);
            Assert.IsFalse(XDM.GtkUI.TreeViewSelectionHelper.HitTestToggleCell(
                    harness.View, harness.CheckColumn, bodyArea.X + 60, bodyArea.Y + bodyArea.Height / 2),
                "click on the row body (card column) must hit-test false");

            var area3 = harness.View.GetCellArea(viewPaths[3], harness.CardColumn);
            XDM.GtkUI.TreeViewSelectionHelper.ToggleSelectionPath(harness.View, viewPaths[3]);
            var afterToggle = harness.View.Selection.GetSelectedRows(out _);
            Assert.AreEqual(4, afterToggle.Length,
                $"checkbox toggle must ADD the row (expected 4 selected, got {afterToggle.Length})");
            XDM.GtkUI.TreeViewSelectionHelper.ToggleSelectionPath(harness.View, viewPaths[3]);
            var afterUntoggle = harness.View.Selection.GetSelectedRows(out _);
            Assert.AreEqual(3, afterUntoggle.Length,
                $"checkbox untoggle must REMOVE the row (expected 3 selected, got {afterUntoggle.Length})");
        }
    }
}
