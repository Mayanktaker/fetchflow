// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Guards the design-system contrast floor across every frontend.
//
// What went wrong and is pinned here:
//  - all 7 GTK 3 dark themes painted the toolbar search text in the SAME hex as
//    the field behind it (1.00:1, invisible) after the field was re-tinted,
//  - accent-filled buttons/rows in the bright schemes paired white labels with
//    fills as low as 1.67:1, and the bevel gradient's light end made it worse,
//  - the GTK 4 accent was applied to native controls while the ink stayed white.
//
// The floor is 3.0:1 (WCAG AA large text) for labels painted on a scheme accent,
// and 4.5:1 for a text field, where the value is body copy rather than a label.
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
    public class ThemeContrastTests
    {
        // Contrast a label on a scheme accent fill must reach
        private const double LabelFloor = 3.0;
        // Contrast text typed into a field must reach (normal-size body text)
        private const double FieldFloor = 4.5;

        // Repo root, located by its AGENTS.md marker
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

        private static string Gtk3Dir => Path.Combine(Root, "app", "XDM", "XDM.Gtk.UI", "theme");
        private static string Gtk4Dir => Path.Combine(Root, "app", "XDM", "XDM.Gtk4.UI", "theme");

        // One parsed declaration block
        private sealed class Rule
        {
            public List<string> Selectors { get; set; }
            public string Body { get; set; }
            public string Source { get; set; }
        }

        private static readonly Regex RulePattern = new Regex(@"([^{}]+)\{([^{}]*)\}", RegexOptions.Singleline);
        private static readonly Regex CommentPattern = new Regex(@"/\*.*?\*/", RegexOptions.Singleline);
        private static readonly Regex BackgroundPattern =
            new Regex(@"(?<![\w-])background-color\s*:\s*(#[0-9a-fA-F]{3,8})\s*;");
        private static readonly Regex ColorPattern =
            new Regex(@"(?<![\w-])color\s*:\s*(#[0-9a-fA-F]{3,8})\s*;");
        private static readonly Regex GradientPattern = new Regex(@"linear-gradient\([^)]*\)");
        private static readonly Regex HexPattern = new Regex(@"#[0-9a-fA-F]{6}");

        // Splits a stylesheet into blocks, with comments removed from selectors
        private static List<Rule> Parse(string css)
        {
            var rules = new List<Rule>();
            foreach (Match match in RulePattern.Matches(css))
            {
                rules.Add(new Rule
                {
                    Selectors = CommentPattern.Replace(match.Groups[1].Value, "")
                        .Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList(),
                    Body = match.Groups[2].Value
                });
            }
            return rules;
        }

        // Painted fills a rule can show: the solid colour plus every gradient stop
        private static List<string> Fills(string body, string fallback)
        {
            var fills = new List<string>();
            var own = BackgroundPattern.Matches(body);
            var solid = own.Count > 0 ? own[own.Count - 1].Groups[1].Value : fallback;
            if (solid != null)
            {
                fills.Add(solid);
            }
            foreach (Match gradient in GradientPattern.Matches(body))
            {
                fills.AddRange(HexPattern.Matches(gradient.Value).Cast<Match>().Select(m => m.Value));
            }
            return fills;
        }

        // The last background a selector declared before (or in) its own block
        private static Dictionary<string, string> BackgroundsBySelector(List<Rule> rules)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                var matches = BackgroundPattern.Matches(rule.Body);
                if (matches.Count == 0) continue;
                foreach (var selector in rule.Selectors)
                {
                    map[selector] = matches[matches.Count - 1].Groups[1].Value;
                }
            }
            return map;
        }

        // Contrast of every readable label/token pair in one stylesheet
        private static List<string> UnreadablePairs(string file, double floor, bool requireOwnBackground)
        {
            var name = Path.GetFileName(file);
            var css = File.ReadAllText(file);
            var rules = Parse(css);
            var backgrounds = BackgroundsBySelector(rules);
            var failures = new List<string>();

            foreach (var rule in rules)
            {
                // Disabled and backdrop text is deliberately quiet
                if (rule.Selectors.Any(s => s.EndsWith(":disabled") || s.EndsWith(":backdrop")))
                    continue;

                var own = BackgroundPattern.Matches(rule.Body);
                if (requireOwnBackground && own.Count == 0)
                    continue;

                var inherited = rule.Selectors
                    .Where(s => backgrounds.ContainsKey(s))
                    .Select(s => backgrounds[s])
                    .FirstOrDefault();
                var fills = Fills(rule.Body, own.Count > 0 ? null : inherited);
                if (fills.Count == 0)
                    continue;

                foreach (Match color in ColorPattern.Matches(rule.Body))
                {
                    var worst = fills
                        .Select(f => ThemeContrast.TryContrast(color.Groups[1].Value, f, out var ratio)
                            ? ratio : double.MaxValue)
                        .DefaultIfEmpty(double.MaxValue)
                        .Min();
                    if (worst < floor)
                    {
                        failures.Add($"{name}: {rule.Selectors[0]} text {color.Groups[1].Value} on "
                            + $"{fills[0]} is {worst:0.00}:1 (< {floor:0.0})");
                    }
                }
            }
            return failures;
        }

        // Every curated accent must pair with ink that clears the label floor
        [TestMethod]
        public void EverySchemeAccentCarriesReadableInkAndHover()
        {
            var failures = new List<string>();
            foreach (var (schemes, mode) in new[]
            {
                (ColorSchemeTable.DarkSchemes, "dark"),
                (ColorSchemeTable.LightSchemes, "light"),
            })
            {
                foreach (var scheme in schemes)
                {
                    var ink = ThemeContrast.InkFor(scheme.AccentHex);
                    var hover = ThemeContrast.HoverFor(scheme.AccentHex);

                    if (!ThemeContrast.TryContrast(ink, scheme.AccentHex, out var onAccent) || onAccent < LabelFloor)
                        failures.Add($"{mode}/{scheme.Id}: ink {ink} on {scheme.AccentHex} is {onAccent:0.00}:1");

                    if (!ThemeContrast.TryContrast(ink, hover, out var onHover) || onHover < LabelFloor)
                        failures.Add($"{mode}/{scheme.Id}: ink {ink} on hover {hover} is {onHover:0.00}:1");

                    if (hover == scheme.AccentHex)
                        failures.Add($"{mode}/{scheme.Id}: hover did not move off the accent");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "every scheme accent must carry readable ink, in both modes:\n"
                + string.Join("\n", failures));
        }

        // No GTK 3 theme may paint text below the floor on its own surface
        [TestMethod]
        public void EveryGtk3Theme_PaintsReadableText()
        {
            var failures = new List<string>();
            foreach (var file in Directory.GetFiles(Gtk3Dir, "*.css"))
            {
                failures.AddRange(UnreadablePairs(file, LabelFloor, requireOwnBackground: false));
            }

            Assert.AreEqual(0, failures.Count,
                "theme text must stay readable against the surface it sits on:\n"
                + string.Join("\n", failures.Take(20)));
        }

        // The toolbar search field is body text: it needs the stricter floor
        [TestMethod]
        public void EveryGtk3Theme_SearchFieldTextIsReadable()
        {
            var failures = new List<string>();
            foreach (var file in Directory.GetFiles(Gtk3Dir, "*.css"))
            {
                var name = Path.GetFileName(file);
                var css = CommentPattern.Replace(File.ReadAllText(file), "");
                var field = Regex.Match(css,
                    @"entry\.toolbar-search-entry\s*\{([^{}]*)\}", RegexOptions.Singleline);
                Assert.IsTrue(field.Success, $"{name}: toolbar search field rule is missing");

                var backgrounds = BackgroundsBySelector(Parse(css));
                var fill = BackgroundPattern.Matches(field.Groups[1].Value)
                    .Cast<Match>().Select(m => m.Groups[1].Value).FirstOrDefault()
                    ?? backgrounds.GetValueOrDefault("entry.toolbar-search-entry");
                Assert.IsNotNull(fill, $"{name}: search field has no background");

                // The ink comes from the later block that only sets the colour
                var ink = Regex.Match(css,
                    @"entry\.toolbar-search-entry\s*\{\s*color:\s*(#[0-9a-fA-F]{3,8})\s*;",
                    RegexOptions.Singleline).Groups[1].Value;
                Assert.IsTrue(ink.Length > 0, $"{name}: search field declares no text colour");

                if (!ThemeContrast.TryContrast(ink, fill, out var ratio) || ratio < FieldFloor)
                    failures.Add($"{name}: search text {ink} on {fill} is {ratio:0.00}:1 (< {FieldFloor:0.0})");
            }

            Assert.AreEqual(0, failures.Count,
                "the search field must be readable in every theme:\n" + string.Join("\n", failures));
        }

        // Resolves @define-color tokens so a rule's real colours can be compared
        private static Dictionary<string, string> Tokens(string css)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(css, @"@define-color\s+([\w-]+)\s+(#[0-9a-fA-F]{3,8});"))
            {
                map[m.Groups[1].Value] = m.Groups[2].Value;
            }
            return map;
        }

        // Contrast of every resolvable label pair in a token-based stylesheet
        private static List<string> UnreadableTokenPairs(string file, double floor)
        {
            var name = Path.GetFileName(file);
            var css = File.ReadAllText(file);
            var tokens = Tokens(css);
            var rules = Parse(css);
            var backgrounds = BackgroundsBySelector(rules);
            var failures = new List<string>();

            // Substitutes @token references; alpha()/mixed values are left alone
            string Resolve(string value) =>
                value != null && value.StartsWith("@") && tokens.ContainsKey(value.Substring(1))
                    ? tokens[value.Substring(1)]
                    : null;

            foreach (var rule in rules)
            {
                if (rule.Selectors.Any(s => s.EndsWith(":disabled") || s.EndsWith(":backdrop")))
                    continue;

                var own = BackgroundPattern.Matches(rule.Body);
                var solid = own.Count > 0
                    ? Resolve(own[own.Count - 1].Groups[1].Value.Trim()) ?? own[own.Count - 1].Groups[1].Value
                    : rule.Selectors.Where(s => backgrounds.ContainsKey(s))
                        .Select(s => Resolve(backgrounds[s])).FirstOrDefault(v => v != null);
                if (solid == null)
                    continue;

                var fills = new List<string> { solid };
                foreach (Match g in GradientPattern.Matches(rule.Body))
                    fills.AddRange(HexPattern.Matches(g.Value).Cast<Match>().Select(m => m.Value));

                foreach (Match color in ColorPattern.Matches(rule.Body))
                {
                    var resolved = Resolve(color.Groups[1].Value.Trim());
                    if (resolved == null) continue;
                    var worst = fills.Select(f => ThemeContrast.TryContrast(resolved, f, out var r) ? r : double.MaxValue)
                        .Min();
                    if (worst < floor)
                        failures.Add($"{name}: {rule.Selectors[0]} {resolved} on {fills[0]} is {worst:0.00}:1");
                }
            }
            return failures;
        }

        // GTK 4 paints accent fills through the shared ink token, not a fixed white
        [TestMethod]
        public void EveryGtk4Theme_UsesTheSchemeInkToken()
        {
            var failures = new List<string>();
            foreach (var file in Directory.GetFiles(Gtk4Dir, "*.css"))
            {
                var name = Path.GetFileName(file);
                var css = File.ReadAllText(file);

                if (!Regex.IsMatch(css, @"@define-color accent_foreground #[0-9a-fA-F]{3,8};"))
                    failures.Add($"{name}: no accent_foreground token");
                if (!Regex.IsMatch(css, @"@define-color row_bg #[0-9a-fA-F]{3,8};"))
                    failures.Add($"{name}: no row_bg token");
                if (!Regex.IsMatch(css, @"@define-color row_hover #[0-9a-fA-F]{3,8};"))
                    failures.Add($"{name}: no row_hover token");

                foreach (var rule in Parse(css))
                {
                    var accentPainted = rule.Selectors.Any(s => s.Contains("suggested-action"))
                        || rule.Selectors.Any(s => s.Contains(":checked"))
                        || rule.Selectors.Any(s => s.Contains("navigation-sidebar") || s.Contains(".sidebar"));
                    if (!accentPainted) continue;
                    if (Regex.IsMatch(rule.Body, @"(?<![\w-])color:\s*#ffffff"))
                        failures.Add($"{name}: {rule.Selectors[0]} hardcodes white on the accent");
                }

                failures.AddRange(UnreadableTokenPairs(file, LabelFloor));
            }

            Assert.AreEqual(0, failures.Count,
                "GTK 4 must derive accent ink from the scheme and keep labels readable:\n"
                + string.Join("\n", failures.Take(20)));
        }

        // The muted row meta text is body copy, so it needs the stricter floor on
        // both list surfaces (it was 3.88:1 on the finished list before the fix)
        [TestMethod]
        public void EveryGtk4Theme_MutedTextClearsBodyFloor()
        {
            var failures = new List<string>();
            foreach (var file in Directory.GetFiles(Gtk4Dir, "*.css"))
            {
                var name = Path.GetFileName(file);
                var css = File.ReadAllText(file);
                var tokens = Tokens(css);
                if (!tokens.TryGetValue("meta_text", out var meta)) continue;

                var surfaces = new List<string>();
                foreach (var surface in new[] { "view_bg", "view_finished_bg", "row_bg" })
                {
                    if (tokens.TryGetValue(surface, out var value)) surfaces.Add(value);
                }

                foreach (var surface in surfaces)
                {
                    if (!ThemeContrast.TryContrast(meta, surface, out var ratio) || ratio < FieldFloor)
                        failures.Add($"{name}: meta_text {meta} on {surface} {surface} is {ratio:0.00}:1");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "muted row text must clear the body-text floor:\n" + string.Join("\n", failures));
        }

        // The stored surface tokens must describe the theme files they stand for
        [TestMethod]
        public void EverySchemeSurfaceTokenMatchesItsStylesheet()
        {
            var failures = new List<string>();
            foreach (var (schemes, mode) in new[]
            {
                (ColorSchemeTable.DarkSchemes, "dark"),
                (ColorSchemeTable.LightSchemes, "light"),
            })
            {
                foreach (var scheme in schemes)
                {
                    var file = Path.Combine(Gtk3Dir, scheme.CssFileName);
                    if (!File.Exists(file))
                    {
                        failures.Add($"{mode}/{scheme.Id}: stylesheet {scheme.CssFileName} is missing");
                        continue;
                    }

                    var css = CommentPattern.Replace(File.ReadAllText(file), "");
                    var window = Regex.Match(css, @"window,\s*dialog\s*\{([^{}]*)\}").Groups[1].Value;
                    var header = Regex.Match(css, @"headerbar\s*\{([^{}]*)\}").Groups[1].Value;
                    var sidebar = Regex.Match(css, @"list,\s*listbox,\s*list\.sidebar\s*\{([^{}]*)\}").Groups[1].Value;

                    var pairs = new (string What, string Token, string Body)[]
                    {
                        ("base surface", scheme.BaseSurfaceHex, window),
                        ("header bar", scheme.HeaderBarHex, header),
                        ("sidebar", scheme.SidebarHex, sidebar),
                    };
                    foreach (var (what, token, body) in pairs)
                    {
                        var actual = BackgroundPattern.Matches(body).Cast<Match>()
                            .Select(m => m.Groups[1].Value).FirstOrDefault();
                        if (actual != null && !string.Equals(actual, token, StringComparison.OrdinalIgnoreCase))
                        {
                            failures.Add($"{mode}/{scheme.Id}: {what} is {actual} in {scheme.CssFileName} "
                                + $"but {token} in ColorSchemeTable");
                        }
                    }
                }
            }

            Assert.AreEqual(0, failures.Count,
                "ColorSchemeTable surfaces must match the shipped stylesheets:\n"
                + string.Join("\n", failures));
        }

        // 14 positional rows means a transposed hex pair is a real risk, so every
        // field is validated for shape and the role groups must stay distinct
        [TestMethod]
        public void EverySchemeTokenIsWellFormedAndConsistent()
        {
            var failures = new List<string>();
            foreach (var (schemes, mode) in new[]
            {
                (ColorSchemeTable.DarkSchemes, "dark"),
                (ColorSchemeTable.LightSchemes, "light"),
            })
            {
                foreach (var scheme in schemes)
                {
                    var hexes = new (string What, string Value)[]
                    {
                        ("accent", scheme.AccentHex),
                        ("hover", scheme.HoverBackgroundHex),
                        ("active", scheme.ActiveBackgroundHex),
                        ("alternate", scheme.AlternateBackgroundHex),
                        ("card", scheme.CardBackgroundHex),
                        ("base surface", scheme.BaseSurfaceHex),
                        ("sidebar", scheme.SidebarHex),
                        ("header bar", scheme.HeaderBarHex),
                    };

                    foreach (var (what, value) in hexes)
                    {
                        if (!ThemeContrast.TryParseHex(value, out _, out _, out _)
                            || value.Length != 7 || value[0] != '#')
                        {
                            failures.Add($"{mode}/{scheme.Id}: {what} '{value}' is not #rrggbb");
                        }
                    }

                    // AccentRgb must agree with AccentHex, or icon tinting drifts
                    if (!ThemeContrast.TryParseHex(scheme.AccentHex, out var r, out var g, out var b)
                        || scheme.AccentRgb.R != r || scheme.AccentRgb.G != g || scheme.AccentRgb.B != b)
                    {
                        failures.Add($"{mode}/{scheme.Id}: AccentRgb {scheme.AccentRgb} != {scheme.AccentHex}");
                    }

                    // A transposed pair shows up as two roles sharing one hex
                    var surfaces = new[] { scheme.BaseSurfaceHex, scheme.SidebarHex,
                        scheme.HeaderBarHex, scheme.CardBackgroundHex };
                    if (surfaces.Distinct(StringComparer.OrdinalIgnoreCase).Count() != surfaces.Length)
                    {
                        failures.Add($"{mode}/{scheme.Id}: base/sidebar/header/card are not distinct ("
                            + string.Join(" ", surfaces) + ")");
                    }

                    var accents = new[] { scheme.AccentHex, scheme.HoverBackgroundHex, scheme.ActiveBackgroundHex };
                    if (accents.Distinct(StringComparer.OrdinalIgnoreCase).Count() != accents.Length)
                    {
                        failures.Add($"{mode}/{scheme.Id}: accent/hover/active are not distinct");
                    }

                    if (string.IsNullOrWhiteSpace(scheme.Id) || string.IsNullOrWhiteSpace(scheme.DisplayName)
                        || !File.Exists(Path.Combine(Gtk3Dir, scheme.CssFileName)))
                    {
                        failures.Add($"{mode}/{scheme.Id}: incomplete metadata");
                    }
                }
            }

            Assert.AreEqual(0, failures.Count,
                "every scheme row must be internally consistent:\n" + string.Join("\n", failures));
        }
    }
}
