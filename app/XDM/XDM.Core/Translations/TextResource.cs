// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.Collections.Generic;
using System.IO;

namespace Translations
{
    // Global static resource provider for localized UI strings with English fallback
    public static class TextResource
    {
        private static readonly Dictionary<string, string> texts = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object syncLock = new();

        // Static constructor loading default English strings on startup
        static TextResource()
        {
            Load("English.txt");
        }

        // Loads base English strings and overlays specified language on top
        public static void Load(string language)
        {
            lock (syncLock)
            {
                // Always load English as baseline so missing keys fall back cleanly
                LoadFileFromSearchPaths("English.txt", overwrite: true);

                if (!string.IsNullOrEmpty(language) &&
                    !language.Equals("English.txt", StringComparison.OrdinalIgnoreCase) &&
                    !language.Equals("English", StringComparison.OrdinalIgnoreCase))
                {
                    LoadFileFromSearchPaths(language, overwrite: true);
                }
            }
        }

        // Searches candidate directories for language file and loads its contents
        private static bool LoadFileFromSearchPaths(string target, bool overwrite)
        {
            if (File.Exists(target))
            {
                LoadTexts(target, overwrite);
                return true;
            }

            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang", target),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, target),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Lang", target),
                Path.Combine("/opt/fetchflow/Lang", target)
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    LoadTexts(candidate, overwrite);
                    return true;
                }
            }

            return false;
        }

        // Retrieves localized string for given key or returns key itself as ultimate fallback
        public static string GetText(string key)
        {
            lock (syncLock)
            {
                if (texts.TryGetValue(key, out string? label) && !string.IsNullOrEmpty(label))
                {
                    return label;
                }
            }
            return key ?? string.Empty;
        }

        // Parses key-value pairs from text file into string dictionary
        private static void LoadTexts(string path, bool overwrite)
        {
            try
            {
                var lines = File.ReadAllLines(path);
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    {
                        continue;
                    }
                    var index = line.IndexOf('=');
                    if (index > 0)
                    {
                        var key = line.Substring(0, index).Trim();
                        var val = line.Substring(index + 1);
                        if (overwrite || !texts.ContainsKey(key))
                        {
                            texts[key] = val;
                        }
                    }
                }
            }
            catch
            {
                // Resilient loading: keep existing strings if reading fails
            }
        }

        // Returns all currently loaded translation keys
        public static IEnumerable<string> GetKeys()
        {
            lock (syncLock)
            {
                return new List<string>(texts.Keys);
            }
        }
    }
}
