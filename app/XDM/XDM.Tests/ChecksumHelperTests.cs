// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.Util;

namespace XDM.Tests
{
    [TestClass]
    public class ChecksumHelperTests
    {
        private string tempTestFile = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            tempTestFile = Path.GetTempFileName();
            File.WriteAllText(tempTestFile, "FetchFlow Download Manager Checksum Test String 2026", Encoding.UTF8);
        }

        [TestCleanup]
        public void Teardown()
        {
            if (File.Exists(tempTestFile))
            {
                File.Delete(tempTestFile);
            }
        }

        [TestMethod]
        public async Task TestComputeHashesAsync()
        {
            var result = await ChecksumHelper.ComputeHashesAsync(tempTestFile);

            Assert.IsFalse(string.IsNullOrEmpty(result.Sha256));
            Assert.AreEqual(64, result.Sha256.Length);

            Assert.IsFalse(string.IsNullOrEmpty(result.Md5));
            Assert.AreEqual(32, result.Md5.Length);

            Assert.IsFalse(string.IsNullOrEmpty(result.Sha1));
            Assert.AreEqual(40, result.Sha1.Length);

            Assert.IsFalse(string.IsNullOrEmpty(result.Sha512));
            Assert.AreEqual(128, result.Sha512.Length);

            Assert.IsTrue(result.FileSizeBytes > 0);
        }

        [TestMethod]
        public async Task TestCompareHashMatches()
        {
            var result = await ChecksumHelper.ComputeHashesAsync(tempTestFile);

            var sha256Match = ChecksumHelper.CompareHash(result.Sha256.ToUpperInvariant(), result);
            Assert.AreEqual(ChecksumMatchStatus.Match, sha256Match.Status);
            Assert.AreEqual("SHA-256", sha256Match.MatchedAlgorithm);

            var md5Match = ChecksumHelper.CompareHash("  " + result.Md5 + " \n", result);
            Assert.AreEqual(ChecksumMatchStatus.Match, md5Match.Status);
            Assert.AreEqual("MD5", md5Match.MatchedAlgorithm);

            var sha512Match = ChecksumHelper.CompareHash(result.Sha512, result);
            Assert.AreEqual(ChecksumMatchStatus.Match, sha512Match.Status);
            Assert.AreEqual("SHA-512", sha512Match.MatchedAlgorithm);

            var sha1Match = ChecksumHelper.CompareHash(result.Sha1, result);
            Assert.AreEqual(ChecksumMatchStatus.Match, sha1Match.Status);
            Assert.AreEqual("SHA-1", sha1Match.MatchedAlgorithm);
        }

        [TestMethod]
        public async Task TestCompareHashMismatchAndEmpty()
        {
            var result = await ChecksumHelper.ComputeHashesAsync(tempTestFile);

            var emptyMatch = ChecksumHelper.CompareHash("", result);
            Assert.AreEqual(ChecksumMatchStatus.Empty, emptyMatch.Status);

            var mismatch = ChecksumHelper.CompareHash("0123456789abcdef0123456789abcdef", result);
            Assert.AreEqual(ChecksumMatchStatus.Mismatch, mismatch.Status);
        }

        [TestMethod]
        public void TestIsProbableHash()
        {
            Assert.IsTrue(ChecksumHelper.IsProbableHash("d41d8cd98f00b204e9800998ecf8427e", out var md5));
            Assert.AreEqual("d41d8cd98f00b204e9800998ecf8427e", md5);

            Assert.IsTrue(ChecksumHelper.IsProbableHash("  da39a3ee5e6b4b0d3255bfef95601890afd80709  ", out var sha1));
            Assert.AreEqual("da39a3ee5e6b4b0d3255bfef95601890afd80709", sha1);

            Assert.IsTrue(ChecksumHelper.IsProbableHash("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", out var sha256));
            Assert.AreEqual("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", sha256);

            Assert.IsFalse(ChecksumHelper.IsProbableHash("not-a-hash", out _));
            Assert.IsFalse(ChecksumHelper.IsProbableHash("d41d8cd98f00b204e9800998ecf8427g", out _)); // 'g' is non-hex
            Assert.IsFalse(ChecksumHelper.IsProbableHash("", out _));
            Assert.IsFalse(ChecksumHelper.IsProbableHash(null, out _));
        }
    }
}
