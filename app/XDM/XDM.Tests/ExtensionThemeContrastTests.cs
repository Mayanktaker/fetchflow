// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Guards the browser extensions' design tokens.
//
// Both extensions carry one palette (Charcoal Blue / Classic Blue) as CSS custom
// properties. What went wrong and is pinned here:
//  - the primary button was white on the brand-orange gradient (2.6:1), and the
//    queued-state button white on the success green (2.3:1) — a bright fill has
//    to take dark ink,
//  - muted caption text (`--text-faint`) was 2.8:1 on white and 3.4:1 on cards,
//  - `--accent-hover` was used both as a fill and as text, so the moment it was
//    deepened for white text it became unreadable as text; the roles are now
//    `--accent` (fill) and `--accent-text` (text),
//  - the two extension stylesheets drifted apart.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core.UI;

namespace XDM.Tests
{
    [TestClass]
    public class ExtensionThemeContrastTests
    {
        // Body copy and captions in the popup
        private const double BodyFloor = 4.5;
        // A label painted on a saturated accent/brand fill
        private const double LabelFloor = 3.0;

        private static string Root
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                    dir = dir.Parent;
                Assert.IsNotNull(dir, "Could not locate repo root (AGENTS.md)");
                return dir.FullName;
            }
        }

        private static string[] Stylesheets => new[]
        {
            Path.Combine(Root, "app", "XDM", "chrome-extension", "styles.css"),
            Path.Combine(Root, "app", "XDM", "firefox-amo", "styles.css"),
        };

        private static readonly Regex CustomProperty =
            new Regex(@"(?<name>--[\w-]+)\s*:\s*(?<value>[^;]+);");

        // Custom properties declared by one rule body
        private static Dictionary<string, string> Declarations(string body)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in CustomProperty.Matches(body))
                map[m.Groups["name"].Value] = m.Groups["value"].Value.Trim();
            return map;
        }

        // Resolves --token chains (var(--a, fallback)) to a literal colour
        private static string Resolve(string value, Dictionary<string, string> tokens)
        {
            for (var hop = 0; hop < 8 && value != null; hop++)
            {
                var m = Regex.Match(value.Trim(), @"^var\(\s*(--[\w-]+)\s*(?:,\s*([^)]+))?\)$");
                if (!m.Success) break;
                if (!tokens.TryGetValue(m.Groups[1].Value, out var next))
                    next = m.Groups[2].Success ? m.Groups[2].Value : null;
                value = next;
            }
            return value?.Trim();
        }

        // Dark palette, plus the light override block, for one stylesheet
        private static (Dictionary<string, string> Dark, Dictionary<string, string> Light) Palettes(string css)
        {
            var dark = Declarations(Regex.Match(css, @":root\s*\{(.*?)\n\}", RegexOptions.Singleline)
                .Groups[1].Value);
            var light = new Dictionary<string, string>(dark, StringComparer.Ordinal);
            var block = Regex.Match(css,
                @"@media \(prefers-color-scheme: light\)\s*\{\s*:root\s*\{(.*?)\n\s{2}\}",
                RegexOptions.Singleline);
            if (block.Success)
            {
                foreach (var pair in Declarations(block.Groups[1].Value))
                    light[pair.Key] = pair.Value;
            }
            return (dark, light);
        }

        // Every mode of every extension stylesheet
        private static IEnumerable<(string File, string Mode, Dictionary<string, string> Tokens)> Modes()
        {
            foreach (var file in Stylesheets)
            {
                var (dark, light) = Palettes(File.ReadAllText(file));
                yield return (file, "dark", dark);
                yield return (file, "light", light);
            }
        }

        // Contrast of one resolved token against another, per mode
        private static List<string> Failures(string foreground, params string[] surfaces)
        {
            var failures = new List<string>();
            foreach (var (file, mode, tokens) in Modes())
            {
                var fg = Resolve($"var({foreground})", tokens);
                foreach (var surface in surfaces)
                {
                    var bg = Resolve($"var({surface})", tokens);
                    if (fg == null || bg == null) continue;
                    if (!ThemeContrast.TryContrast(fg, bg, out var ratio) || ratio < BodyFloor)
                    {
                        failures.Add($"{Path.GetFileName(Path.GetDirectoryName(file))}/{mode}: "
                            + $"{foreground} {fg} on {surface} {bg} is {ratio:0.00}:1");
                    }
                }
            }
            return failures;
        }

        // Muted caption text must stay readable on every surface it lands on
        [TestMethod]
        public void EveryMode_MutedTextClearsTheBodyFloor()
        {
            var failures = Failures("--text-faint", "--bg", "--bg-view", "--bg-card", "--bg-elevated");
            failures.AddRange(Failures("--text-dim", "--bg", "--bg-view", "--bg-card", "--bg-elevated"));

            Assert.AreEqual(0, failures.Count,
                "extension caption text must clear the body floor:\n" + string.Join("\n", failures));
        }

        // Accent-coloured text is its own token, and it must read in both modes
        [TestMethod]
        public void EveryMode_AccentTextClearsTheLabelFloor()
        {
            var failures = new List<string>();
            foreach (var (file, mode, tokens) in Modes())
            {
                if (!tokens.ContainsKey("--accent-text"))
                {
                    failures.Add($"{Path.GetFileName(file)}/{mode}: no --accent-text token");
                    continue;
                }
                var fg = Resolve("var(--accent-text)", tokens);
                foreach (var surface in new[] { "--bg-card", "--bg-elevated", "--border-soft" })
                {
                    var bg = Resolve($"var({surface})", tokens);
                    if (fg == null || bg == null) continue;
                    if (!ThemeContrast.TryContrast(fg, bg, out var ratio) || ratio < LabelFloor)
                        failures.Add($"{Path.GetFileName(Path.GetDirectoryName(file))}/{mode}: "
                            + $"accent text {fg} on {surface} {bg} is {ratio:0.00}:1");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "accent-coloured text must clear the label floor:\n" + string.Join("\n", failures));
        }

        // No rule may paint text below the label floor on a fill it declares
        [TestMethod]
        public void NoRulePaintsTextBelowTheLabelFloor()
        {
            var failures = new List<string>();

            foreach (var file in Stylesheets)
            {
                var css = Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", "", RegexOptions.Singleline);
                var (dark, light) = Palettes(css);

                foreach (var (mode, tokens) in new[] { ("dark", dark), ("light", light) })
                {
                    foreach (Match rule in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}", RegexOptions.Singleline))
                    {
                        var selector = rule.Groups[1].Value.Trim();
                        var body = rule.Groups[2].Value;
                        if (selector.EndsWith(":disabled")) continue;

                        // The fill this rule paints, worst case (lightest gradient stop)
                        var background = Regex.Match(body,
                            @"(?<![\w-])background(?:-color)?:\s*([^;]+);");
                        if (!background.Success) continue;
                        var value = Resolve(background.Groups[1].Value.Replace("!important", "").Trim(), tokens);
                        if (value == null) continue;

                        var gradient = Regex.Match(value, @"linear-gradient\([^)]*\)");
                        var fills = gradient.Success
                            ? Regex.Matches(gradient.Value, @"#[0-9a-fA-F]{6}").Cast<Match>()
                                .Select(m => m.Value).ToList()
                            : new List<string> { value };
                        fills = fills.Where(f => ThemeContrast.TryParseHex(f, out _, out _, out _)).ToList();
                        if (fills.Count == 0) continue;

                        var color = Regex.Match(body, @"(?<![\w-])color:\s*([^;]+);");
                        if (!color.Success) continue;
                        var ink = Resolve(color.Groups[1].Value.Replace("!important", "").Trim(), tokens);
                        if (ink == null || !ThemeContrast.TryParseHex(ink, out _, out _, out _)) continue;

                        var worst = fills
                            .Select(f => ThemeContrast.TryContrast(ink, f, out var r) ? r : double.MaxValue)
                            .Min();
                        if (worst < LabelFloor)
                        {
                            failures.Add($"{Path.GetFileName(Path.GetDirectoryName(file))}/{mode}: "
                                + $"{selector} paints {ink} on {fills[0]} ({worst:0.00}:1)");
                        }
                    }
                }
            }

            Assert.AreEqual(0, failures.Count,
                "extension text must clear the label floor on the fill it declares:\n"
                + string.Join("\n", failures.Take(20)));
        }

        // The two extension stylesheets are twins apart from the documented extra block
        [TestMethod]
        public void ExtensionStylesheetsStayInSync()
        {
            var chrome = File.ReadAllText(Stylesheets[0]);
            var firefox = File.ReadAllText(Stylesheets[1]);

            var chromeTokens = Declarations(Regex.Match(chrome, @":root\s*\{(.*?)\n\}", RegexOptions.Singleline)
                .Groups[1].Value);
            var firefoxTokens = Declarations(Regex.Match(firefox, @":root\s*\{(.*?)\n\}", RegexOptions.Singleline)
                .Groups[1].Value);

            var differing = chromeTokens
                .Where(p => !firefoxTokens.TryGetValue(p.Key, out var other) || other != p.Value)
                .Select(p => p.Key)
                .Concat(firefoxTokens.Keys.Where(k => !chromeTokens.ContainsKey(k)))
                .Distinct()
                .ToList();

            Assert.AreEqual(0, differing.Count,
                "extension token sets drifted apart: " + string.Join(", ", differing));
        }
    }
}
