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
