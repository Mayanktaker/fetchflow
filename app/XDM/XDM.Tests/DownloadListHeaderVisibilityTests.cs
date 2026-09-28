// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Regression guard for the download list column header.
//
// Two defects are pinned here, both of which looked fine until someone dragged:
//
//   1. GtkTreeViewHeader is NOT reachable as a `treeview header` CSS node — the
//      header buttons' parent IS the treeview. Styling `treeview <view> header`
//      therefore matches nothing and the header rendered fully transparent, i.e.
//      invisible on a light surface. The accent has to live on
//      `treeview <view> header button`. This asserts the resolved background of a
//      real header button is OPAQUE in every theme.
//
//   2. Setting `Resizable` while the sizing mode is AUTOSIZE makes GTK silently
//      rewrite the mode to GROW_ONLY, which re-calculates width from cell content
//      and discards the user's drag. Every column must therefore end up FIXED.
//
// The GTK part runs under DISPLAY (scripts/run-gtk-smoke.sh); without a display
// the test reports inconclusive rather than passing vacuously.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Gtk;

namespace XDM.Tests
{
    [TestClass]
    [TestCategory("GtkSmoke")]
    public class DownloadListHeaderVisibilityTests
    {
        // Mirrors MainWindow's download-list column geometry
        private const int GutterWidth = 46;
        private const int GutterMin = 36;
        private const int GutterMax = 200;
        private const int NameWidth = 420;
        private const int NameMin = 160;
        private const int NameMax = 1200;
        private const int SizeWidth = 196;
        private const int SizeMin = 110;
        private const int SizeMax = 720;

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

        private static bool HasDisplay(out string reason)
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

        private static void Pump(int iterations)
        {
            for (var i = 0; i < iterations; i++)
            {
                while (Application.EventsPending()) Application.RunIteration();
            }
        }

        // Builds the header exactly the way MainWindow does: Resizable is set
        // BEFORE Sizing, which is the whole point of the test.
        private static (Window Window, TreeViewColumn Gutter, TreeViewColumn Name,
            TreeViewColumn Size) BuildHeader(CssProvider provider)
        {
            var store = new ListStore(typeof(string));
            var view = new TreeView(store) { HeadersVisible = true };
            view.StyleContext.AddClass("unfinished");

            var gutter = new TreeViewColumn
            {
                Resizable = true,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = GutterWidth,
                SortColumnId = -1,
                MinWidth = GutterMin,
                MaxWidth = GutterMax
            };
            gutter.PackStart(new CellRendererText(), true);
            view.AppendColumn(gutter);

            var name = new TreeViewColumn
            {
                Resizable = true,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = NameWidth,
                Expand = true,
                SortColumnId = -1,
                MinWidth = NameMin,
                MaxWidth = NameMax
            };
            name.PackStart(new CellRendererText(), true);
            view.AppendColumn(name);
            name.Button?.StyleContext.AddClass("list-header-name");

            var size = new TreeViewColumn
            {
                Resizable = true,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = SizeWidth,
                SortColumnId = -1,
                MinWidth = SizeMin,
                MaxWidth = SizeMax
            };
            size.PackEnd(new CellRendererText(), false);
            view.AppendColumn(size);
            size.Button?.StyleContext.AddClass("list-header-size");

            var window = new Window(WindowType.Toplevel);
            window.SetDefaultSize(700, 160);
            window.Add(view);
            window.ShowAll();
            Pump(20);
            return (window, gutter, name, size);
        }

        [TestMethod]
        [TestCategory("GtkSmoke")]
        public void EveryTheme_HeaderButtonsAreOpaque_SoTheBarIsVisible()
        {
            if (!HasDisplay(out var skip)) Assert.Inconclusive(skip);
            try { Application.Init(); }
            catch (Exception ex)
            {
                Assert.Inconclusive($"Skipped GtkSmoke: GTK init failed: {ex.Message}");
            }

            var themeDir = Path.Combine(RepoRoot, "app", "XDM", "XDM.Gtk.UI", "theme");
            var themes = Directory.GetFiles(themeDir, "*.css");
            Assert.IsTrue(themes.Length > 0, "no themes found");

            var failures = new List<string>();
            foreach (var theme in themes)
            {
                var name = Path.GetFileName(theme);
                var provider = new CssProvider();
                try { provider.LoadFromPath(theme); }
                catch (Exception ex)
                {
                    failures.Add($"{name}: theme failed to parse: {ex.Message}");
                    continue;
                }
                // provider first, so widgets are styled from creation
                StyleContext.AddProviderForScreen(Gdk.Screen.Default, provider, 800);

                var (window, gutter, nameCol, size) = BuildHeader(provider);
                foreach (var (col, tag) in new[]
                {
                    (gutter, "gutter"), (nameCol, "name"), (size, "size")
                })
                {
                    if (col.Button == null)
                    {
                        failures.Add($"{name}: {tag} header button is null");
                        continue;
                    }
                    var bg = col.Button.StyleContext.GetBackgroundColor(StateFlags.Normal);
                    if (bg.Alpha < 0.99)
                    {
                        failures.Add($"{name}: {tag} header button background alpha "
                            + $"{bg.Alpha:0.00} — the header bar would be invisible "
                            + "(the accent must be on `treeview <view> header button`, "
                            + "not `treeview <view> header`)");
                    }
                }

                // every column must survive as FIXED + resizable, or a drag is lost
                foreach (var (col, tag) in new[]
                {
                    (gutter, "gutter"), (nameCol, "name"), (size, "size")
                })
                {
                    if (col.Sizing != TreeViewColumnSizing.Fixed)
                    {
                        failures.Add($"{name}: {tag} column sizing is {col.Sizing}, "
                            + "expected Fixed (GrowOnly/Autosize re-derives the width "
                            + "from content and discards the user's drag)");
                    }
                    if (!col.Resizable)
                    {
                        failures.Add($"{name}: {tag} column is not resizable");
                    }
                }

                window.Destroy();
                Pump(2);
            }

            Assert.AreEqual(0, failures.Count,
                "the download list header must be visible and all three columns must "
                + "stay user-resizable in every theme:\n" + string.Join("\n", failures));
        }
    }
}
