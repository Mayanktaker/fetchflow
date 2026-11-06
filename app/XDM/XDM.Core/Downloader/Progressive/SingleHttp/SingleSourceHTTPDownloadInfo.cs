// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System.Collections.Generic;


namespace XDM.Core.Downloader.Progressive.SingleHttp
{
    public class SingleSourceHTTPDownloadInfo: IRequestData
    {
        public string Uri { get; set; }
        public string? Cookies { get; set; }
        public Dictionary<string, List<string>> Headers { get; set; }
        public string File { get; set; }
        public long ContentLength { get; set; }
        public bool ConvertToMp3 { get; set; }
        public string? AudioBitrate { get; set; }
        public string ContentType { get; set; }
        // Optional expected digest, checked once the file is assembled
        public string? Checksum { get; set; }
    }
}
