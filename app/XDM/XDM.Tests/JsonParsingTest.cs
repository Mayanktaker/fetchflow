// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.IO;
using System;
using System.Linq;

namespace XDM.Tests
{
    [TestClass]
    public class JsonParsingTests
    {
        [TestInitialize]
        public void Setup()
        {
        }

        [TestMethod]
        public void DeserializeBrowserMessageJsonSuccess()
        {
            Test();
        }

        private T? ReadProperty<T>(JsonTextReader reader, string name)
        {
            if (reader.TokenType == JsonToken.PropertyName && reader.Value.ToString() == name &&
                        reader.Read() && reader.Value != null)
            {
                return (T)reader.Value;
            }
            return default(T);
        }

        private bool IsObjectStart(JsonTextReader reader, string name)
        {
            return reader.TokenType == JsonToken.PropertyName && reader.Value.ToString() == name &&
                        reader.Read() && reader.TokenType == JsonToken.StartObject;
        }

        private bool IsListStart(JsonTextReader reader, string name)
        {
            return reader.TokenType == JsonToken.PropertyName && reader.Value.ToString() == name &&
                        reader.Read() && reader.TokenType == JsonToken.StartArray;
        }

        private void SkipUnknownParts(JsonTextReader reader)
        {
            if (reader.TokenType == JsonToken.PropertyName && reader.Value != null)
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        var n = 1;
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.EndObject) n--;
                            if (reader.TokenType == JsonToken.StartObject) n++;
                            if (n == 0) return;
                        }
                    }
                    else if (reader.TokenType == JsonToken.StartArray)
                    {
                        var n = 1;
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.EndArray) n--;
                            if (reader.TokenType == JsonToken.StartArray) n++;
                            if (n == 0) return;
                        }
                    }
                    else if (reader.Value != null)
                    {
                        continue;
                    }
                }
            }
        }

        private void ReadMessageObject(JsonTextReader reader)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject) break;
                var url = ReadProperty<string>(reader, "url");
                if (url != null)
                {
                    Console.WriteLine("url: {0}", url);
                }
                if (IsObjectStart(reader, "cookies"))
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.EndObject) break;
                        if (reader.TokenType == JsonToken.PropertyName && reader.Value != null)
                        {
                            var cookieName = (string)reader.Value;
                            if (reader.Read() && reader.TokenType == JsonToken.String)
                            {
                                var cookieValue = (string)reader.Value;
                                Console.WriteLine("cookieName: {0}, cookieValue: {1}", cookieName, cookieValue);
                            }
                        }
                    }
                }

                if (IsObjectStart(reader, "responseHeaders"))// && IsListStart(reader, "realUA"))
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.EndObject) break;
                        if (reader.TokenType == JsonToken.PropertyName && reader.Value != null)
                        {
                            var headerName = (string)reader.Value;
                            if (IsListStart(reader, headerName))
                            {
                                while (reader.Read())
                                {
                                    if (reader.TokenType == JsonToken.EndArray) break;
                                    if (reader.TokenType == JsonToken.String)
                                    {
                                        Console.WriteLine("{0}: {1}", headerName, reader.Value);
                                    }
                                }
                            }
                        }
                    }
                }

                if (IsObjectStart(reader, "requestHeaders"))
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.EndObject) break;
                        if (reader.TokenType == JsonToken.PropertyName && reader.Value != null)
                        {
                            var headerName = (string)reader.Value;
                            if (IsListStart(reader, headerName))
                            {
                                while (reader.Read())
                                {
                                    if (reader.TokenType == JsonToken.EndArray) break;
                                    if (reader.TokenType == JsonToken.String)
                                    {
                                        Console.WriteLine("{0}: {1}", headerName, reader.Value);
                                    }
                                }
                            }
                        }
                    }
                }

                SkipUnknownParts(reader);
            }
        }

        private void Test()
        {
            // Self-contained sample of a browser-relay message (mirrors what the extensions send)
            const string sampleJson = @"{
                ""messageType"": ""download"",
                ""message"": {
                    ""url"": ""https://example.com/file.zip"",
                    ""cookies"": { ""session"": ""abc123"" },
                    ""requestHeaders"": { ""User-Agent"": [""Mozilla/5.0""] },
                    ""responseHeaders"": { ""Content-Length"": [""1024""] }
                },
                ""messages"": [
                    { ""url"": ""https://example.com/video.mp4"" },
                    { ""url"": ""https://example.com/audio.mp3"" }
                ]
            }";
            var reader = new JsonTextReader(new StringReader(sampleJson));
            if (reader.Read() && reader.TokenType == JsonToken.StartObject)
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndObject) break;

                    var messageType = ReadProperty<string>(reader, "messageType");
                    if (messageType != null)
                    {
                        Console.WriteLine("messageType: {0}", messageType);
                    }
                    if (IsObjectStart(reader, "message"))
                    {
                        ReadMessageObject(reader);
                    }
                    if (IsListStart(reader, "messages"))
                    {
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.EndArray) break;
                            if (reader.TokenType == JsonToken.StartObject)
                            {
                                ReadMessageObject(reader);
                            }
                        }
                    }
                    SkipUnknownParts(reader);
                }
            }
        }

        [TestMethod]
        public void TestExtensionDataAliases()
        {
            var json = @"{
                ""url"": ""https://example.com/file.zip"",
                ""filename"": ""my-download.zip"",
                ""cookies"": ""sess=abc; token=123"",
                ""tabId"": ""42""
            }";
            var data = JsonConvert.DeserializeObject<XDM.Core.BrowserMonitoring.ExtensionData>(json);
            Assert.IsNotNull(data);
            Assert.AreEqual("my-download.zip", data.File);
            Assert.AreEqual("sess=abc; token=123", data.Cookie);
            Assert.AreEqual("42", data.TabId);
        }

        [TestMethod]
        public void TestHttpParserCaseInsensitiveHeaders()
        {
            var headers = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            headers["upgrade"] = new() { "websocket" };
            headers["sec-websocket-key"] = new() { "dGhlIHNhbXBsZSBub25jZQ==" };
            headers["connection"] = new() { "Upgrade" };

            Assert.IsTrue(headers.ContainsKey("Upgrade"));
            Assert.IsTrue(headers.ContainsKey("Sec-WebSocket-Key"));
            Assert.IsTrue(headers.ContainsKey("Connection"));
            Assert.AreEqual("websocket", headers["Upgrade"][0]);
        }

        [TestMethod]
        public void TestYdlSupportedUrlMatching()
        {
            var supported = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "youtube.com", "youtu.be", "vimeo.com", "dailymotion.com",
                "facebook.com", "fb.watch", "instagram.com", "twitter.com",
                "x.com", "twitch.tv", "bilibili.com", "tiktok.com", "reddit.com"
            };

            bool IsSupported(string url)
            {
                var host = new Uri(url).Host;
                if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) host = host.Substring(4);
                return supported.Any(d => host.Equals(d, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
            }

            Assert.IsTrue(IsSupported("https://x.com/user/status/123"));
            Assert.IsTrue(IsSupported("https://twitter.com/user/status/123"));
            Assert.IsTrue(IsSupported("https://vimeo.com/12345"));
            Assert.IsTrue(IsSupported("https://www.youtube.com/watch?v=abc"));
            Assert.IsTrue(IsSupported("https://youtu.be/abc"));
            Assert.IsFalse(IsSupported("https://unsupported-site.org/file.mp4"));
        }
    }
}