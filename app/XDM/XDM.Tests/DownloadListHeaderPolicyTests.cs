// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Pure decision tests for the download list column header (gutter | File name | Size):
// what a header click means for the sort, and what the selection means for the
// gutter's select-all glyph. GTK owns the header buttons; this is the app's logic.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.GtkUI;

namespace XDM.Tests
{
    [TestClass]
    public class DownloadListHeaderPolicyTests
    {
        [TestMethod]
        public void NextSort_SwitchingColumn_StartsDescending()
        {
            var (column, descending) = DownloadListHeaderPolicy.NextSort("Size", "Name", false);
            Assert.AreEqual("Size", column);
            Assert.IsTrue(descending, "a newly chosen column always starts descending");
        }

        [TestMethod]
        public void NextSort_SameColumn_FlipsDirection()
        {
            var (_, first) = DownloadListHeaderPolicy.NextSort("Name", "Name", false);
            Assert.IsTrue(first, "ascending flips to descending");
            var (_, second) = DownloadListHeaderPolicy.NextSort("Name", "Name", first);
            Assert.IsFalse(second, "descending flips back to ascending");
        }

        [TestMethod]
        public void NextSort_SameColumnFromDifferentSortState_StillFlips()
        {
            var (column, descending) = DownloadListHeaderPolicy.NextSort("Size", "Size", true);
            Assert.AreEqual("Size", column);
            Assert.IsFalse(descending);
        }

        [TestMethod]
        public void Caption_ActiveSortColumnHasNoHint()
        {
            Assert.AreEqual("File name",
                DownloadListHeaderPolicy.Caption("File name", isActiveSort: true),
                "the active sort column shows GTK's own direction arrow, not a hint");
        }

        [TestMethod]
        public void Caption_IdleSortableColumnCarriesTheHint()
        {
            Assert.AreEqual($"Size {DownloadListHeaderPolicy.SortableHint}",
                DownloadListHeaderPolicy.Caption("Size", isActiveSort: false),
                "idle sortable columns must still advertise that they sort");
        }

        [TestMethod]
        public void SelectAllGlyph_NoneSomeAll()
        {
            Assert.AreEqual(DownloadListHeaderPolicy.GlyphNone,
                DownloadListHeaderPolicy.SelectAllGlyph(0, 5));
            Assert.AreEqual(DownloadListHeaderPolicy.GlyphSome,
                DownloadListHeaderPolicy.SelectAllGlyph(2, 5));
            Assert.AreEqual(DownloadListHeaderPolicy.GlyphAll,
                DownloadListHeaderPolicy.SelectAllGlyph(5, 5));
        }

        [TestMethod]
        public void SelectAllGlyph_EmptyListReadsAsNone()
        {
            Assert.AreEqual(DownloadListHeaderPolicy.GlyphNone,
                DownloadListHeaderPolicy.SelectAllGlyph(0, 0));
        }

        [TestMethod]
        public void SelectAllShouldClear_OnlyWhenEverythingIsSelected()
        {
            Assert.IsFalse(DownloadListHeaderPolicy.SelectAllShouldClear(0, 5),
                "nothing selected -> the header selects");
            Assert.IsFalse(DownloadListHeaderPolicy.SelectAllShouldClear(3, 5),
                "partial selection -> the header completes it");
            Assert.IsTrue(DownloadListHeaderPolicy.SelectAllShouldClear(5, 5),
                "everything selected -> the header clears");
        }

        [TestMethod]
        public void SelectAllShouldClear_EmptyListDoesNotClear()
        {
            Assert.IsFalse(DownloadListHeaderPolicy.SelectAllShouldClear(0, 0));
        }
    }
}
