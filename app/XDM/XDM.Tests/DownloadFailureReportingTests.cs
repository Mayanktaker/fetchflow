// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using XDM.Core;

namespace XDM.Tests
{
    // Failure-reason pipeline: sanitized details, complete error-code mapping, and the
    // row status text that distinguishes a real failure from a clean user pause
    [TestClass]
    public class DownloadFailureReportingTests
    {
        [TestMethod]
        public void UserPause_ShowsPlainStopped()
        {
            var text = DownloadStatusText.Build(DownloadStatus.Stopped, ErrorCode.None, null,
                null, null, "Downloading...", "Stopped", "Finished", "Waiting",
                ErrorMessages.GetLocalizedErrorMessage);
            Assert.AreEqual("Stopped", text);
        }

        [TestMethod]
        public void FailureWithoutDetail_ShowsLocalizedReason()
        {
            var text = DownloadStatusText.Build(DownloadStatus.Stopped, ErrorCode.NonResumable, null,
                null, null, "Downloading...", "Stopped", "Finished", "Waiting",
                ErrorMessages.GetLocalizedErrorMessage);
            StringAssert.Contains(text, ErrorMessages.GetLocalizedErrorMessage(ErrorCode.NonResumable));
            Assert.IsFalse(text.Contains(':'));
        }

        [TestMethod]
        public void FailureWithDetail_AppendsSanitizedDetail()
        {
            var detail = ErrorMessages.SanitizeDetail("Resume not supported :: chunk-1");
            var text = DownloadStatusText.Build(DownloadStatus.Stopped, ErrorCode.InvalidResponse, detail,
                null, null, "Downloading...", "Stopped", "Finished", "Waiting",
                ErrorMessages.GetLocalizedErrorMessage);
            StringAssert.Contains(text, ErrorMessages.GetLocalizedErrorMessage(ErrorCode.InvalidResponse));
            StringAssert.Contains(text, "Resume not supported :: chunk-1");
        }

        [TestMethod]
        public void EveryErrorCode_HasALocalizedMessage()
        {
            foreach (ErrorCode code in Enum.GetValues(typeof(ErrorCode)))
            {
                if (code == ErrorCode.None) continue;
                var message = ErrorMessages.GetLocalizedErrorMessage(code);
                Assert.IsFalse(string.IsNullOrWhiteSpace(message), $"Missing message for {code}");
                Assert.AreNotEqual("ERR_" + code, message, $"Unresolved key for {code}");
            }
        }

        [TestMethod]
        public void SanitizeDetail_StripsNewlinesAndCapsLength()
        {
            var messy = "line1\nline2\r\nline3" + new string('x', 500);
            var safe = ErrorMessages.SanitizeDetail(messy);
            Assert.IsNotNull(safe);
            Assert.IsFalse(safe.Contains('\n'));
            Assert.IsFalse(safe.Contains('\r'));
            Assert.IsTrue(safe.Length <= 240);
            Assert.IsTrue(safe.StartsWith("line1 line2"), "newline variants must collapse to spaces");
        }

        [TestMethod]
        public void SanitizeDetail_NullAndBlankReturnNull()
        {
            Assert.IsNull(ErrorMessages.SanitizeDetail(null));
            Assert.IsNull(ErrorMessages.SanitizeDetail("   "));
        }

        [TestMethod]
        public void DownloadFailedEventArgs_CarriesCodeAndDetail()
        {
            var args = new XDM.Core.Downloader.DownloadFailedEventArgs(ErrorCode.DiskError, "disk full");
            Assert.AreEqual(ErrorCode.DiskError, args.ErrorCode);
            Assert.AreEqual("disk full", args.Detail);

            var bare = new XDM.Core.Downloader.DownloadFailedEventArgs(ErrorCode.Generic);
            Assert.AreEqual(ErrorCode.Generic, bare.ErrorCode);
            Assert.IsNull(bare.Detail);
        }

        [TestMethod]
        public void DownloadingRow_ShowsSpeedAndEta()
        {
            var text = DownloadStatusText.Build(DownloadStatus.Downloading, ErrorCode.None, null,
                "1.5 MB/s", "00:30", "Downloading...", "Stopped", "Finished", "Waiting",
                ErrorMessages.GetLocalizedErrorMessage);
            Assert.AreEqual("1.5 MB/s - 00:30", text);
        }
    }
}
