// © 2026 Mayanktaker | Based on XDM by subhra74 (https://github.com/subhra74/xdm)
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using YDLWrapper;

namespace XDM.Tests
{
    [TestClass]
    public class YdlParserTests
    {
        [TestMethod]
        public void TestParseValidJson()
        {
            // Format with both video and audio codecs — parser routes it directly to the output list.
            // Protocol "https" yields YDLEntryType.Http (no "dash"/"m3u" substring match).
            var json = @"{
                ""id"": ""test_id"",
                ""title"": ""Test Video Title"",
                ""formats"": [
                    {
                        ""format_id"": ""137"",
                        ""url"": ""https://example.com/video.mp4"",
                        ""ext"": ""mp4"",
                        ""acodec"": ""mp4a.40.2"",
                        ""vcodec"": ""avc1.640028"",
                        ""protocol"": ""https"",
                        ""height"": 1080
                    }
                ]
            }";

            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, json);

            try
            {
                var result = YDLOutputParser.Parse(tempFile);
                Assert.IsNotNull(result);
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("Test Video Title", result[0].Title);
                Assert.AreEqual(1, result[0].Formats.Count);
                Assert.AreEqual("https://example.com/video.mp4", result[0].Formats[0].VideoUrl);
                Assert.AreEqual("1080", result[0].Formats[0].Height);
                Assert.AreEqual(YDLEntryType.Http, result[0].Formats[0].YDLEntryType);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
