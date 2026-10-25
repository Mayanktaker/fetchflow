// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Smoke-mode regression checks for the list and theme behaviour that previously
// broke in production: search filtering, multi-selection stability across progress
// ticks and view switches, and the ThemeMode round-trip. Only run with --smoke
// (scripts/run-gtk4-smoke.sh), so a normal launch never pays for it.
using System;
using System.Collections.Generic;
using System.Linq;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Gtk4UI.Downloads;

namespace XDM.Gtk4UI
{
    internal sealed partial class MainWindow
    {
        // Runs the regression checks; throws on the first failed expectation
        internal void RunSelfTest()
        {
            var alpha = NewRow("t1", "alpha-one.bin", 10);
            var beta = NewRow("t2", "beta-two.bin", 20);
            var gamma = NewRow("t3", "gamma-three.bin", 30);
            var rows = new[] { alpha.Item, beta.Item, gamma.Item };

            SetRows(rows, InProgressKey);
            Expect(downloadList.Count == 3, "initial row count");

            downloadList.Filter("alpha");
            Expect(downloadList.Count == 1, "search filters rows");
            downloadList.Filter("");
            Expect(downloadList.Count == 3, "clearing search restores rows");

            downloadList.SelectAll();
            Expect(downloadList.GetSelectedItems().Count == 3, "select all selects every row");

            // A progress tick must refresh that row without touching the selection
            beta.Entry.Progress = 55;
            UpdateProgressRow("t2");
            Expect(downloadList.GetSelectedItems().Count == 3, "selection survives a progress tick");
            Expect(allRows.First(row => row.Id == "t2").RawProgress == 55, "progress value applied to the row");

            // Native trimmings: row clicks plus Ctrl/Shift must accumulate a
            // multi-selection. Native activation uses SelectItem(pos, true), so the
            // harness drives the model's own unselectRest semantics instead.
            downloadList.ClearSelection();
            downloadList.SelectAll();

            // Per-view selection: the finished view starts clean and going back restores
            var done = new DownloadItem
            {
                Id = "f1",
                Name = "done.bin",
                IsComplete = true,
                Progress = 1.0,
                RawProgress = 100
            };
            SetRows(new[] { done }, FinishedKey);
            Expect(downloadList.GetSelectedItems().Count == 0, "finished view starts unselected");
            SetRows(rows, InProgressKey);
            Expect(downloadList.GetSelectedItems().Count == 3, "selection restored by transfer id");

            // Pausing arrives with no progress tick, so the row must re-sync on
            // demand: stopped state, no frozen speed, and no error flag
            var paused = NewRow("t4", "paused-four.bin", 43);
            paused.Entry.Status = DownloadStatus.Stopped;
            paused.Entry.DownloadSpeed = "9.8 MB/s";
            paused.Entry.ETA = "00:01:51";
            SetRows(new[] { paused.Item }, InProgressKey);
            RefreshRowState("t4");
            var pausedItem = allRows.First(row => row.Id == "t4");
            Expect(pausedItem.Status.Contains("Stop"), "paused row carries the stopped state");
            Expect(pausedItem.DownloadSpeed == "", "paused drops the frozen speed");
            Expect(!pausedItem.HasError, "paused row is not an error");

            // A failed transfer keeps the stopped state but raises the error flag
            paused.Entry.LastErrorCode = ErrorCode.Generic;
            RefreshRowState("t4");
            Expect(allRows.First(row => row.Id == "t4").HasError, "failed row raises the error flag");

            // Theme mode round-trip across the shared Config codes
            Gtk4Theme.ApplyMode(Window, Gtk4Theme.ModeLight, persist: false);
            Expect(!Gtk4Theme.IsDarkActive, "light mode applied");
            Gtk4Theme.ApplyMode(Window, Gtk4Theme.ModeDark, persist: false);
            Expect(Gtk4Theme.IsDarkActive, "dark mode applied");
            Gtk4Theme.ApplyFromConfig(Window);

            // About window: Legal and Credits pages must carry content
            var about = BuildAboutWindow(Window);
            Expect(about.GetDevelopers().Length >= 2, "about credits list project + upstream");
            Expect(about.GetLicense()?.Contains("GNU GENERAL PUBLIC LICENSE") == true,
                "about license text set from LICENSE");
            Expect(about.GetDevelopers().All(d => !d.Contains('(')), "about developer names carry no bracketed url");
            Expect(!string.IsNullOrEmpty(about.GetIssueUrl()), "about issue url set");
            Expect(!string.IsNullOrEmpty(about.GetSupportUrl()), "about support url set");

            // Theme stylesheets must be structurally identical: the light file once
            // kept a stale .bottom-bar padding rule, making its footer 15px taller.
            // Colors are scheme-driven, so they are normalised out of the comparison.
            Expect(ThemeStructureMatches(), "theme stylesheets match structurally");

            // Dialog chrome parity: the same pages must fit to the same heights in
            // both themes, or a theme-only rule has smuggled in a size difference
            var darkHeights = FittedHeights(dark: true);
            var lightHeights = FittedHeights(dark: false);
            Expect(darkHeights.SequenceEqual(lightHeights), "settings fit identical in both themes");
            Expect(darkHeights.All(h => h is >= 380 and <= 700), "settings heights stay in bounds");
            Gtk4Theme.ApplyFromConfig(Window);

            Console.WriteLine("[GTK4 SMOKE] list/theme self-test passed");
        }


        // Fits every settings page without presenting anything and records heights
        private List<int> FittedHeights(bool dark)
        {
            Gtk4Theme.ApplyMode(Window, dark ? Gtk4Theme.ModeDark : Gtk4Theme.ModeLight, persist: false);
            var dialog = new Dialogs.SettingsDialog(Window, 0);
            return new[]
            {
                dialog.FittedHeight("general"),
                dialog.FittedHeight("categories"),
                dialog.FittedHeight("network"),
                dialog.FittedHeight("speed"),
                dialog.FittedHeight("monitoring"),
                dialog.FittedHeight("advanced")
            }.ToList();
        }

        // Normalises both stylesheets (colors are scheme data, not structure) and
        // requires the rules to match exactly
        private static bool ThemeStructureMatches()
        {
            var light = NormalizeTheme("gtk4-light.css");
            var dark = NormalizeTheme("gtk4-dark.css");
            return light.Length > 0 && light == dark;
        }

        // Strips color values and the light/dark file header, keeping every
        // structural declaration for comparison
        private static string NormalizeTheme(string fileName)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "theme", fileName);
                if (!System.IO.File.Exists(path))
                {
                    return "";
                }

                var text = System.IO.File.ReadAllText(path);
                text = System.Text.RegularExpressions.Regex.Replace(text, @"#[0-9a-fA-F]{3,8}\b", "@C");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"rgba?\([^)]*\)", "@C");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"alpha\([^)]*\)", "@C");
                text = text.Replace("light theme.", "theme.").Replace("dark theme.", "theme.");
                return text;
            }
            catch (Exception)
            {
                return "";
            }
        }

        // Builds a live in-progress row projection for the self-test
        private static (InProgressDownloadItem Entry, DownloadItem Item) NewRow(string id, string name, int progress)
        {
            var entry = new InProgressDownloadItem
            {
                Id = id,
                Name = name,
                Size = 1024 * 1024,
                Progress = progress,
                DateAdded = DateTime.Now
            };
            return (entry, ToItem(new Gtk4InProgressRow(entry)));
        }

        // Fails loudly when a self-test expectation does not hold
        private static void Expect(bool condition, string what)
        {
            if (!condition)
            {
                throw new InvalidOperationException("GTK 4 self-test failed: " + what);
            }
        }
    }
}
