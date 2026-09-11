// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.BrowserMonitoring;

namespace XDM.Tests
{
    [TestClass]
    public class MediaRedirectTests
    {
        static readonly string[] VideoExts = new string[]
            { "MP4", "MKV", "WEBM", "M3U8", "MPD", "TS", "FLV", "AVI", "MOV" };

        // BunnyCDN episode burst must list in the extension menu, not popup
        [TestMethod]
        public void PlayboxMp4File_IsStreamable()
        {
            Assert.IsTrue(NetworkHelper.IsStreamableMedia(
                "https://playbox-videos.b-cdn.net/69b72b9ff36abc0c85292e6e/videofd4adak",
                "videofd4adab-1fc7-4663-b8cd-e32e569de93f.mp4", "video/mp4", VideoExts));
        }

        // Browser may send URL + filename without MIME; ext alone must redirect
        [TestMethod]
        public void Mp4UrlWithoutMime_IsStreamable()
        {
            Assert.IsTrue(NetworkHelper.IsStreamableMedia(
                "https://playbox-videos.b-cdn.net/v/videofd4adak.mp4", null, null, VideoExts));
        }

        // Real file downloads must keep the immediate New Download dialog
        [TestMethod]
        public void RarFile_IsNotStreamable()
        {
            Assert.IsFalse(NetworkHelper.IsStreamableMedia(
                "https://example.com/files/game.rar", "game.rar",
                "application/x-rar-compressed", VideoExts));
        }
    }
}
