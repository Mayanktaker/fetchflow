// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace XDM.Core.BrowserMonitoring
{
    // DTO for browser extension IPC payload deserialization
    public class ExtensionData
    {
        public string? Url { get; set; }

        private string? _cookie;
        public string? Cookie
        {
            get => _cookie;
            set => _cookie = value;
        }

        // Support plural 'cookies' sent by some extension paths
        [JsonProperty("cookies")]
        public string? Cookies
        {
            get => _cookie;
            set => _cookie = value;
        }

        public Dictionary<string, List<string>>? RequestHeaders { get; set; }
        public Dictionary<string, List<string>>? ResponseHeaders { get; set; }

        private string? _file;
        public string? File
        {
            get => _file;
            set => _file = value;
        }

        // Support 'filename' sent by extension download handlers
        [JsonProperty("filename")]
        public string? Filename
        {
            get => _file;
            set => _file = value;
        }

        public string? Method { get; set; }
        public string? UserAgent { get; set; }
        public string? TabUrl { get; set; }
        public string? TabId { get; set; }
        public string? TabTitle { get; set; }
        public string? Referer { get; set; }
        public long? FileSize { get; set; }
        public string? MimeType { get; set; }
        public string? Vid { get; set; }
    }
}
