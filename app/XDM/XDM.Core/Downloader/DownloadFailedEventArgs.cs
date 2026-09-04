using System;
using XDM.Core;

namespace XDM.Core.Downloader
{
    public class DownloadFailedEventArgs : EventArgs
    {
        public DownloadFailedEventArgs(ErrorCode errorCode, string? detail = null)
        {
            ErrorCode = errorCode;
            Detail = detail;
        }
        public ErrorCode ErrorCode { get; }
        public string? Detail { get; }
    }
}
