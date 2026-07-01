// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.BrowserMonitoring;

namespace XDM.Tests
{
    [TestClass]
    public class YdlParserTests
    {
        [TestMethod]
        public void TestParseValidJson()
        {
            var json = @"{
                ""id"": ""test_id"",
                ""title"": ""Test Video Title"",
                ""formats"": [
                    {
                        ""format_id"": ""137"",
                        ""url"": ""https://example.com/video.mp4"",
                        ""ext"": ""mp4"",
                        ""acodec"": ""none"",
                        ""vcodec"": ""avc1.640028"",
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
                Assert.AreEqual(""Test Video Title"", result[0].Title);
                Assert.AreEqual(1, result[0].Formats.Count);
                Assert.AreEqual(""https://example.com/video.mp4"", result[0].Formats[0].VideoUrl);
                Assert.AreEqual(1080, result[0].Formats[0].Height);
                Assert.AreEqual(YDLEntryType.Dash, result[0].Formats[0].YDLEntryType); // Since it has video but no audio natively in same stream, actually it might be Http depending on parser logic.
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
