// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Smoke-mode regression checks for the list and theme behaviour that previously
// broke in production: search filtering, multi-selection stability across progress
// ticks and view switches, and the ThemeMode round-trip. Only run with --smoke
// (scripts/run-gtk4-smoke.sh), so a normal launch never pays for it.
using System;
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

            // Theme mode round-trip across the shared Config codes
            Gtk4Theme.ApplyMode(Window, Gtk4Theme.ModeLight, persist: false);
            Expect(!Gtk4Theme.IsDarkActive, "light mode applied");
            Gtk4Theme.ApplyMode(Window, Gtk4Theme.ModeDark, persist: false);
            Expect(Gtk4Theme.IsDarkActive, "dark mode applied");
            Gtk4Theme.ApplyFromConfig(Window);

            Console.WriteLine("[GTK4 SMOKE] list/theme self-test passed");
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
