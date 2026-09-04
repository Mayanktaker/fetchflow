// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;

namespace XDM.Core
{
    // Pure status-line builder for download rows; kept dependency-free for tests
    public static class DownloadStatusText
    {
        // Builds the row status text: failed rows show reason + safe detail,
        // downloading rows show speed/ETA, everything else shows its state label
        public static string Build(DownloadStatus status, ErrorCode errorCode, string? errorDetail,
            string? speed, string? eta,
            string downloadingText, string stoppedText, string finishedText, string waitingText,
            Func<ErrorCode, string> localizeError)
        {
            if (status == DownloadStatus.Stopped && errorCode != ErrorCode.None)
            {
                var reason = localizeError(errorCode);
                return string.IsNullOrWhiteSpace(errorDetail) ? reason : $"{reason}: {errorDetail}";
            }

            if (status == DownloadStatus.Downloading)
            {
                if (string.IsNullOrEmpty(eta) && string.IsNullOrEmpty(speed)) return downloadingText;
                if (string.IsNullOrEmpty(eta)) return speed ?? string.Empty;
                if (string.IsNullOrEmpty(speed)) return eta ?? string.Empty;
                return $"{speed} - {eta}";
            }

            switch (status)
            {
                case DownloadStatus.Finished: return finishedText;
                case DownloadStatus.Waiting: return waitingText;
                default: return stoppedText;
            }
        }
    }
}
