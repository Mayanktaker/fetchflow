// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace XDM.Tests
{
    // Guards the theme-selector surface strings: every language file must define the
    // three theme keys with non-empty values (TextResource returns "" for missing keys,
    // which would render an empty combo in the Settings dialog).
    public class LangThemeKeysTests
    {
        private static IEnumerable<string> LanguageFiles()
        {
            // test assembly lives at app/XDM/XDM.Tests/bin/Debug/net8.0 → Lang is 4 levels up
            var langDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Lang"));
            foreach (var file in Directory.GetFiles(langDir, "*.txt"))
            {
                if (Path.GetFileName(file) != "index.txt")
                {
                    yield return file;
                }
            }
        }

        [Test]
        public void EveryLanguageDefinesThemeKeysWithNonEmptyValues()
        {
            var problems = new List<string>();
            foreach (var file in LanguageFiles())
            {
                var map = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(file))
                {
                    var idx = line.IndexOf('=');
                    if (idx <= 0)
                    {
                        continue;
                    }
                    map[line.Substring(0, idx)] = line.Substring(idx + 1);
                }
                foreach (var key in new[] { "SETTINGS_DARK_THEME", "THEME_DARK", "THEME_LIGHT" })
                {
                    if (!map.TryGetValue(key, out var value) || value.Trim().Length == 0)
                    {
                        problems.Add($"{Path.GetFileName(file)}: missing/empty {key}");
                    }
                }
            }
            Assert.That(problems, Is.Empty, "Theme keys incomplete:\n" + string.Join("\n", problems));
        }
    }
}
