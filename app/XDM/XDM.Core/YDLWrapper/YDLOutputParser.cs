using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YDLWrapper
{
    public static class YDLOutputParser
    {
        public static List<YDLVideoEntry> Parse(string ydlJsonOutputFile)
        {
            var res = new List<YDLVideoEntry>();

            try
            {
                var pl = Deserialize<YDLPlaylist>(ydlJsonOutputFile);

                if (pl.Entries != null && pl.Entries.Length > 0)
                {
                    foreach (var formatList in pl.Entries)
                    {
                        var items = ProcessFormatList(formatList);
                        res.Add(new YDLVideoEntry
                        {
                            Title = formatList.Title,
                            Formats = items
                        });
                    }
                    return res;
                }
            }
            catch { }

            try
            {
                var pl2 = Deserialize<YDLFormatList>(ydlJsonOutputFile);

                if (pl2.Formats != null && pl2.Formats.Length > 0)
                {
                    res.Add(new YDLVideoEntry
                    {
                        Title = pl2.Title,
                        Formats = ProcessFormatList(pl2)
                    });
                }
            }
            catch { }

            try
            {
                var format = Deserialize<YDLFormat>(ydlJsonOutputFile);
                if (format.Url != null)
                {
                    var formatList = new YDLFormatList { Title = format.Title, Formats = new YDLFormat[] { format } };
                    res.Add(new YDLVideoEntry
                    {
                        Title = format.Title,
                        Formats = ProcessFormatList(formatList)
                    });
                    //res.Add(new YDLVideoEntry
                    //{
                    //    Title = format.Title,
                    //    Formats = new List<YDLVideoFormatEntry>
                    //    {
                    //        new YDLVideoFormatEntry
                    //        {
                    //            VideoUrl = format.Url,
                    //            Title = format.Title,
                    //            YDLEntryType = YDLEntryType.Http,
                    //            VideoFormat = format.Format
                    //        }
                    //    }
                    //});
                }
            }
            catch { }

            return res;
        }

        private static List<YDLVideoFormatEntry> ProcessFormatList(YDLFormatList formatList)
        {
            var list = new List<YDLVideoFormatEntry>();
            var videoOnlyList = new List<YDLFormat>();
            var audioOnlyList = new List<YDLFormat>();

            foreach (var format in formatList.Formats)
            {
                // Skip storyboard / mhtml preview thumbnail sheets
                var ext = (format.Ext ?? string.Empty).ToLowerInvariant();
                var protocol = (format.Protocol ?? string.Empty).ToLowerInvariant();
                var note = (format.Format_Note ?? string.Empty).ToLowerInvariant();
                if (ext == "mhtml" || protocol == "mhtml" || note.Contains("storyboard"))
                {
                    continue;
                }

                var acodec = GetStringValue(format.Acodec);
                var vcodec = GetStringValue(format.Vcodec);

                // Both codecs null/none indicates image sheets or unplayable manifests
                if (vcodec == null && acodec == null)
                {
                    continue;
                }

                if (vcodec != null && acodec != null)
                {
                    list.Add(new YDLVideoFormatEntry
                    {
                        VideoUrl = format.Url,
                        VideoFragments = format.Fragments,
                        Title = formatList.Title,
                        YDLEntryType = GetEntryType(format),
                        VideoFormat = format.Format,
                        FileExt = format.Ext,
                        VideoCodec = format.Vcodec,
                        AudioCodec = format.Acodec,
                        Abr = format.Abr,
                        Width = format.Width,
                        Height = format.Height,
                        FragmentBaseUrl = format.Fragment_Base_Url
                    });
                }
                else if (vcodec != null)
                {
                    videoOnlyList.Add(format);
                }
                else if (acodec != null)
                {
                    audioOnlyList.Add(format);
                }
            }

            foreach (var video in videoOnlyList)
            {
                var videotype = GetEntryType(video);
                foreach (var audio in audioOnlyList)
                {
                    var audioType = GetEntryType(audio);
                    if (videotype == audioType)
                    {
                        list.Add(new YDLVideoFormatEntry
                        {
                            AudioFormat = audio.Format,
                            VideoFormat = video.Format,
                            AudioFragments = audio.Fragments,
                            VideoFragments = video.Fragments,
                            AudioUrl = audio.Url,
                            VideoUrl = video.Url,
                            Title = formatList.Title,
                            YDLEntryType = videotype,
                            FileExt = "MKV",
                            VideoCodec = video.Vcodec,
                            AudioCodec = audio.Acodec,
                            Abr = audio.Abr,
                            Width = video.Width,
                            Height = video.Height,
                            FragmentBaseUrl = video.Fragment_Base_Url
                        });
                    }
                }
            }

            // Always provide high-quality standalone audio streams (e.g. M4A / WebM / Opus)
            var seenAudioExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sortedAudios = audioOnlyList.OrderByDescending(a => double.TryParse(a.Abr, out var abr) ? abr : 0);
            foreach (var audio in sortedAudios)
            {
                var audioExt = audio.Ext ?? "m4a";
                if (!seenAudioExts.Contains(audioExt))
                {
                    seenAudioExts.Add(audioExt);
                    var audioType = GetEntryType(audio);
                    list.Add(new YDLVideoFormatEntry
                    {
                        AudioFormat = audio.Format,
                        AudioFragments = audio.Fragments,
                        AudioUrl = audio.Url,
                        Title = formatList.Title,
                        YDLEntryType = audioType,
                        FileExt = audioExt,
                        AudioCodec = audio.Acodec,
                        Abr = audio.Abr,
                        FragmentBaseUrl = audio.Fragment_Base_Url
                    });
                }
            }

            return list;
        }

        private static YDLEntryType GetEntryType(YDLFormat format)
        {
            if (HasFragments(format)) return YDLEntryType.MpegDash;
            var protocol = format.Protocol?.ToLowerInvariant() ?? string.Empty;
            if (protocol.Contains("dash")) return YDLEntryType.Dash;
            if (protocol.Contains("m3u")) return YDLEntryType.Hls;
            var container = format.Container?.ToLowerInvariant() ?? string.Empty;
            if (container.Contains("dash")) return YDLEntryType.Dash;
            if (container.Contains("m3u")) return YDLEntryType.Hls;
            return YDLEntryType.Http;
        }

        private static bool HasFragments(YDLFormat format)
        {
            return format.Fragments != null && format.Fragments.Count > 0;
        }

        private static string? GetStringValue(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if ("none".Equals(text)) return null;
            if ("'none'".Equals(text)) return null;
            if ("\"none\"".Equals(text)) return null;
            return text;
        }

        private static T? Deserialize<T>(string file)
        {
            return JsonConvert.DeserializeObject<T>(
                File.ReadAllText(file),
                new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore
                });
        }
    }
}
