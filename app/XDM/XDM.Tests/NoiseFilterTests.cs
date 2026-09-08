// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.BrowserMonitoring;

namespace XDM.Tests
{
    [TestClass]
    public class NoiseFilterTests
    {
        // Every URL from the junk-capture report must be rejected at the core gate
        [TestMethod]
        public void ReportedFbsbxGif_IsNoise()
        {
            Assert.IsTrue(NetworkHelper.IsNoiseUrl(
                "https://cdn.fbsbx.com/v/t59.2708-21/535444823_1992905721527548_4606810233348303977_n.gif?x=1"));
        }

        [TestMethod]
        public void ReportedGoogleAutocomplete_IsNoise()
        {
            Assert.IsTrue(NetworkHelper.IsNoiseUrl(
                "https://www.google.com/complete/search?q=test&cp=0&client=gws-wiz-serp"));
        }

        [TestMethod]
        public void ReportedYoutubeDatasync_IsNoise()
        {
            Assert.IsTrue(NetworkHelper.IsNoiseUrl("https://www.youtube.com/getDatasyncIdsEndpoint"));
        }

        [TestMethod]
        public void ReportedYoutubeServiceWorker_IsNoise()
        {
            Assert.IsTrue(NetworkHelper.IsNoiseUrl("https://www.youtube.com/sw.js_data"));
        }

        // Live-DB junk families: Google background-sync and validation telemetry
        [TestMethod]
        public void GoogleAsyncBgasy_IsNoise()
        {
            Assert.IsTrue(NetworkHelper.IsNoiseUrl(
                "https://www.google.com/async/bgasy?ei=abc&client=firefox-b-d&async=_fmt:jspb"));
        }

        [TestMethod]
        public void GoogleValidationAsyncService_IsNoise()
        {
            Assert.IsTrue(NetworkHelper.IsNoiseUrl(
                "https://www.google.com/httpservice/retry/ValidationAsyncService/Validate?sca_esv=1"));
        }

        // Live-DB junk: YouTube player auto-fetches caption tracks (never user downloads)
        [TestMethod]
        public void YoutubeTimedText_IsNoise()
        {
            Assert.IsTrue(NetworkHelper.IsNoiseUrl(
                "https://www.youtube.com/api/timedtext?v=dQw4w9WgXcQ&caps=asr&lang=en"));
        }

        // Live-DB junk: YouTube search-suggest autocomplete on its suggest host
        [TestMethod]
        public void YoutubeSuggestAutocomplete_IsNoise()
        {
            Assert.IsTrue(NetworkHelper.IsNoiseUrl(
                "https://suggestqueries-clients6.youtube.com/complete/search?client=youtube&q=test"));
        }

        // Matching is case-insensitive (core lowercases before comparing)
        [TestMethod]
        public void NoiseMatch_IsCaseInsensitive()
        {
            Assert.IsTrue(NetworkHelper.IsNoiseUrl("https://www.YOUTUBE.com/SW.JS_DATA"));
            Assert.IsTrue(NetworkHelper.IsNoiseUrl("https://CDN.FBSBX.COM/x_n.GIF"));
        }

        // Legit captures must keep passing: watch pages, segments, file hosts, plain files
        [TestMethod]
        public void LegitUrls_AreNotNoise()
        {
            Assert.IsFalse(NetworkHelper.IsNoiseUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
            Assert.IsFalse(NetworkHelper.IsNoiseUrl("https://rr1---sn.googlevideo.com/videoplayback?itag=22&id=abc"));
            Assert.IsFalse(NetworkHelper.IsNoiseUrl("https://bzzhr.to/wjwse1a5544o"));
            Assert.IsFalse(NetworkHelper.IsNoiseUrl("https://example.com/files/game.rar?token=abc"));
            Assert.IsFalse(NetworkHelper.IsNoiseUrl("https://cdn.example.com/video.mp4"));
        }

        // Null/empty input is never noise (callers treat it as invalid elsewhere)
        [TestMethod]
        public void NullOrEmpty_IsNotNoise()
        {
            Assert.IsFalse(NetworkHelper.IsNoiseUrl(null));
            Assert.IsFalse(NetworkHelper.IsNoiseUrl(string.Empty));
        }
    }
}
