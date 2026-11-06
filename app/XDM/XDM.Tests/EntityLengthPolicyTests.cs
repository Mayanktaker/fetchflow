// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.Clients.Http;

namespace XDM.Tests
{
    // Guards the rule that stopped gzip from shrinking a download's planned size
    [TestClass]
    public class EntityLengthPolicyTests
    {
        [TestMethod]
        public void FileSize_CompressedRangeTotal_IsNeverTheFileSize()
        {
            // Google's Chrome RPM: a gzip'd 150,994,773 byte file reports 150,942,050
            var size = EntityLengthPolicy.ResolveFileSize(150_942_050, -1, true);
            Assert.AreEqual(-1L, size);
        }

        [TestMethod]
        public void FileSize_RangeTotalWins_WhenBothAreKnown()
        {
            Assert.AreEqual(150_994_773L, EntityLengthPolicy.ResolveFileSize(150_994_773, 200_000, false));
        }

        [TestMethod]
        public void FileSize_FallsBackToContentLength()
        {
            Assert.AreEqual(150_994_773L, EntityLengthPolicy.ResolveFileSize(-1, 150_994_773, false));
        }

        [TestMethod]
        public void FileSize_UnknownStaysUnknown()
        {
            Assert.AreEqual(-1L, EntityLengthPolicy.ResolveFileSize(-1, -1, false));
            Assert.AreEqual(-1L, EntityLengthPolicy.ResolveFileSize(0, 0, false));
        }

        [TestMethod]
        public void BodyLength_KeepsTheSliceOfARangedResponse()
        {
            // maxByteRange is built from this: the total would make a piece read past its slice
            Assert.AreEqual(200_000L, EntityLengthPolicy.ResolveBodyLength(200_000, 150_994_773, false));
        }

        [TestMethod]
        public void BodyLength_IsUnknownWhileCompressed()
        {
            Assert.AreEqual(-1L, EntityLengthPolicy.ResolveBodyLength(200_000, 150_994_773, true));
        }
    }
}
