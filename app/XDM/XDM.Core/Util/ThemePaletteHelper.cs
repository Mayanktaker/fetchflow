// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace XDM.Core.Util
{
    // Palette model for JSON export and serialization
    public class PaletteModel
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string CssFileName { get; set; } = "";
        public string AccentHex { get; set; } = "";
        public string HoverBackgroundHex { get; set; } = "";
        public string AlternateBackgroundHex { get; set; } = "";
    }

    public static class ThemePaletteHelper
    {
        // Serializes a collection of palette models to formatted JSON
        public static string ExportPalettes(IEnumerable<PaletteModel> palettes)
        {
            return JsonConvert.SerializeObject(palettes, Formatting.Indented);
        }

        // Parses and validates a JSON string of palettes
        public static List<PaletteModel> ImportPalettes(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<PaletteModel>();
            try
            {
                return JsonConvert.DeserializeObject<List<PaletteModel>>(json) ?? new List<PaletteModel>();
            }
            catch
            {
                return new List<PaletteModel>();
            }
        }
    }
}
