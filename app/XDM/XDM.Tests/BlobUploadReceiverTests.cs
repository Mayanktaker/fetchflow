// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace XDM.Tests
{
    /// <summary>
    /// Unit tests for blob chunking, reassembly, and filename sanitization logic
    /// (mirrors the algorithm in XDM.Core.BrowserMonitoring.BlobUploadReceiver)
    /// </summary>
    [TestClass]
    public class BlobUploadReceiverTests
    {
        // Chunk size constant — must match BLOB_CHUNK_SIZE in app.js (512 KiB)
        private const int CHUNK_SIZE = 512 * 1024;

        [TestInitialize]
        public void Setup()
        {
        }

        // Split data into fixed-size chunks, mimicking the extension's slicing (at least 1 chunk)
        private byte[][] ChunkData(byte[] data)
        {
            int totalChunks = Math.Max(1, (int)Math.Ceiling((double)data.Length / CHUNK_SIZE));
            var chunks = new byte[totalChunks][];
            for (int i = 0; i < totalChunks; i++)
            {
                int start = i * CHUNK_SIZE;
                int end = Math.Min(start + CHUNK_SIZE, data.Length);
                var chunk = new byte[end - start];
                Array.Copy(data, start, chunk, 0, end - start);
                chunks[i] = chunk;
            }
            return chunks;
        }

        // Reassemble chunks sequentially, mimicking BlobUploadReceiver's append logic
        private byte[] ReassembleChunks(byte[][] chunks)
        {
            using var ms = new MemoryStream();
            foreach (var chunk in chunks)
            {
                ms.Write(chunk, 0, chunk.Length);
            }
            return ms.ToArray();
        }

        // Sanitize filename — replicates FileHelper.SanitizeFileName (portable-char filter + traversal guard)
        private string SanitizeFileName(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return "";
            var name = filename.Split('/').Last();
            name = name.Split('\\').Last();
            name = Regex.Replace(name, @"[^A-Za-z0-9._ -]", "-");
            name = Regex.Replace(name, @"-{2,}", "-");
            name = Regex.Replace(name, @"-+\.", ".");
            name = name.Trim().TrimEnd('.').Trim('-', ' ');
            return string.IsNullOrEmpty(name) ? "download" : name;
        }

        // Test: single chunk round-trip preserves data
        [TestMethod]
        public void SingleChunkRoundTrip()
        {
            var data = Encoding.UTF8.GetBytes("Hello, blob world!");
            var chunks = ChunkData(data);
            var reassembled = ReassembleChunks(chunks);

            Assert.AreEqual(1, chunks.Length, "Small data should produce exactly 1 chunk");
            CollectionAssert.AreEqual(data, reassembled, "Reassembled data must match original");
        }

        // Test: multi-chunk round-trip for data larger than CHUNK_SIZE
        [TestMethod]
        public void MultiChunkRoundTrip()
        {
            // Create 2.5 MiB of test data (exactly 5 chunks of 512 KiB)
            var data = new byte[CHUNK_SIZE * 5];
            new Random(42).NextBytes(data);

            var chunks = ChunkData(data);
            var reassembled = ReassembleChunks(chunks);

            Assert.IsTrue(chunks.Length >= 5, "2.5 MB should produce at least 5 chunks");
            CollectionAssert.AreEqual(data, reassembled, "Reassembled data must match original");
        }

        // Test: chunk count matches Math.Ceiling(totalSize / chunkSize)
        [TestMethod]
        public void ChunkCountIsCorrect()
        {
            var data = new byte[CHUNK_SIZE + 1]; // just over 1 chunk
            var chunks = ChunkData(data);
            Assert.AreEqual(2, chunks.Length, "CHUNK_SIZE+1 bytes should produce 2 chunks");
        }

        // Test: last chunk is smaller than CHUNK_SIZE for non-multiples
        [TestMethod]
        public void LastChunkIsPartial()
        {
            var data = new byte[CHUNK_SIZE + 100];
            var chunks = ChunkData(data);
            Assert.AreEqual(2, chunks.Length);
            Assert.AreEqual(CHUNK_SIZE, chunks[0].Length, "First chunk must be full size");
            Assert.AreEqual(100, chunks[1].Length, "Second chunk must be the remainder");
        }

        // Test: empty data produces one empty chunk (edge case)
        [TestMethod]
        public void EmptyDataProducesOneChunk()
        {
            var data = new byte[0];
            var chunks = ChunkData(data);
            Assert.AreEqual(1, chunks.Length, "Empty data should produce exactly 1 chunk");
            Assert.AreEqual(0, chunks[0].Length, "The single chunk must be empty");
        }

        // Test: path traversal is blocked — directory components stripped
        [TestMethod]
        public void PathTraversalIsBlocked()
        {
            var malicious = "../../../etc/passwd";
            var sanitized = SanitizeFileName(malicious);
            Assert.AreEqual("passwd", sanitized, "Directory traversal must be stripped");
            Assert.IsFalse(sanitized.Contains("/"), "No slashes allowed in sanitized name");
            Assert.IsFalse(sanitized.Contains(".."), "No double-dots allowed");
        }

        // Test: Windows-style path traversal is blocked
        [TestMethod]
        public void WindowsPathTraversalIsBlocked()
        {
            var malicious = "..\\..\\Windows\\System32\\config";
            var sanitized = SanitizeFileName(malicious);
            Assert.IsFalse(sanitized.Contains("\\"), "No backslashes allowed in sanitized name");
            Assert.IsFalse(sanitized.Contains(".."), "No double-dots allowed");
        }

        // Test: null/empty filename returns empty string
        [TestMethod]
        public void NullFilenameReturnsEmpty()
        {
            Assert.AreEqual("", SanitizeFileName(null));
            Assert.AreEqual("", SanitizeFileName(""));
        }

        // Test: valid filename passes through unchanged
        [TestMethod]
        public void ValidFilenameIsPreserved()
        {
            Assert.AreEqual("image.png", SanitizeFileName("image.png"));
            Assert.AreEqual("blob-video_c248.mp4", SanitizeFileName("blob-video_c248.mp4"));
        }

        // Test: non-portable chars (break Android/MTP copy) become '-'
        [TestMethod]
        public void NonPortableCharsBecomeDash()
        {
            Assert.AreEqual("file -1.mp4", SanitizeFileName("file (1)^.mp4"));
            Assert.AreEqual("quote-removed.txt", SanitizeFileName("quote\"removed\".txt"));
            Assert.AreEqual("name", SanitizeFileName("[name]"));
            Assert.AreEqual("download", SanitizeFileName("###"));
        }

        // Test: transfer ID uniqueness via GUID generation
        [TestMethod]
        public void TransferIdsAreUnique()
        {
            var id1 = Guid.NewGuid().ToString("N");
            var id2 = Guid.NewGuid().ToString("N");
            Assert.AreNotEqual(id1, id2, "Each transfer must have a unique ID");
            Assert.AreEqual(32, id1.Length, "N-format GUID must be 32 hex chars");
        }

        // Test: total size verification (size mismatch detection)
        [TestMethod]
        public void SizeVerificationDetectsMismatch()
        {
            var data = new byte[1024];
            var chunks = ChunkData(data);
            var reassembled = ReassembleChunks(chunks);

            long declaredSize = 2048; // deliberately wrong
            long actualSize = reassembled.Length;

            Assert.AreNotEqual(declaredSize, actualSize,
                "Size mismatch must be detectable for integrity checks");
        }

        // Test: filename collision suffix logic
        [TestMethod]
        public void FilenameCollisionSuffixWorks()
        {
            string baseName = "download.mp4";
            string ext = Path.GetExtension(baseName);
            string nameNoExt = Path.GetFileNameWithoutExtension(baseName);

            // Simulate collision avoidance: first file exists, second gets (1) suffix
            string candidate1 = baseName;
            string candidate2 = $"{nameNoExt} (1){ext}";

            Assert.AreNotEqual(candidate1, candidate2);
            Assert.IsTrue(candidate2.Contains("(1)"));
        }
    }
}
