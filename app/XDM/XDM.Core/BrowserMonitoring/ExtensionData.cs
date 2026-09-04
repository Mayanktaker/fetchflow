// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Linq;
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

        // Support plural 'cookies' sent by some extension paths. Extensions may send
        // a joined string OR an object/array (e.g. request-watcher initializes
        // cookies: {}) — a strict string binding threw JsonReaderException and
        // silently dropped the whole /media capture. Tolerant converter below.
        [JsonProperty("cookies")]
        [JsonConverter(typeof(CookiesStringConverter))]
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

    // Normalizes 'cookies' payloads of any JSON shape into a semicolon-joined string
    public sealed class CookiesStringConverter : JsonConverter<string?>
    {
        public override string? ReadJson(JsonReader reader, Type objectType, string? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.String:
                    return (string?)reader.Value;
                case JsonToken.Null:
                    return null;
                case JsonToken.StartObject:
                    {
                        // {"name":"value",...} → "name=value; ..." (empty object → null)
                        var dict = serializer.Deserialize<Dictionary<string, object?>>(reader);
                        if (dict == null || dict.Count == 0) return null;
                        return string.Join("; ", dict.Select(kv => kv.Key + "=" + kv.Value));
                    }
                case JsonToken.StartArray:
                    {
                        // ["a=b","c=d"] → "a=b; c=d" (empty array → null)
                        var arr = serializer.Deserialize<List<object?>>(reader);
                        if (arr == null || arr.Count == 0) return null;
                        return string.Join("; ", arr);
                    }
                default:
                    return reader.Value?.ToString();
            }
        }

        public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }
}
