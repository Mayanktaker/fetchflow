// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Guards the two scales and the accent promise the GTK 3 design system makes.
//
// What went wrong and is pinned here:
//  - spacing was documented as a 4/8/12/16/24 scale but the generated themes and
//    the window metrics used 2, 3, 6, 10, 14, 18 and 20px, so "the system" was
//    guidance rather than something a change could be held to;
//  - a scheme's accent painted selections and progress but never the primary
//    action, because buttons belong to Adwaita — so a scheme's accent and its
//    primary button disagreed. `button.suggested-action` is now the one branded
//    control, and its ink must clear the shared 3.0:1 label floor on accent,
//    hover and active fills.
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
    public class Gtk3DesignTokenTests
    {
        // Contrast a control label must reach on a scheme accent fill
        private const double LabelFloor = 3.0;
        // Selector competing with Adwaita's own suggested-action paint
        private const string PrimaryAction = "button.suggested-action";

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

        // Selector -> property -> value, comments removed so prose cannot be read as CSS
        private static Dictionary<string, Dictionary<string, string>> Parse(string css)
        {
            var stripped = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (Match rule in Regex.Matches(stripped, @"([^{}]+)\{([^{}]*)\}"))
            {
                foreach (var raw in rule.Groups[1].Value.Split(','))
                {
                    var selector = raw.Trim();
                    if (selector.Length == 0) continue;
                    if (!result.TryGetValue(selector, out var props))
                    {
                        result[selector] = props = new Dictionary<string, string>(StringComparer.Ordinal);
                    }
                    foreach (Match decl in Regex.Matches(rule.Groups[2].Value,
                        @"(?<![\w-])([\w-]+)\s*:\s*([^;]+);"))
                    {
                        props[decl.Groups[1].Value] =
                            string.Join(" ", decl.Groups[2].Value.Split(' '));
                    }
                }
            }
            return result;
        }

        private static string Prop(Dictionary<string, Dictionary<string, string>> rules,
            string selector, string property) =>
            rules.TryGetValue(selector, out var props) && props.TryGetValue(property, out var v) ? v : null;

        // Every px number declared by the given properties
        private static IEnumerable<int> Pixels(string css, string propertyPattern)
        {
            var stripped = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
            var declarations = Regex.Matches(stripped,
                @"(?<![\w-])" + propertyPattern + @"\s*:\s*([^;]+);");
            foreach (Match declaration in declarations)
            {
                foreach (Match px in Regex.Matches(declaration.Groups[1].Value, @"(\d+)px"))
                {
                    yield return int.Parse(px.Groups[1].Value);
                }
            }
        }

        // Every gap and type size in every theme must come from the shared scales
        [TestMethod]
        public void EveryGtk3Theme_UsesOnlyScaleSpacingAndType()
        {
            var spacing = new HashSet<int>(DesignTokens.Spacing);
            var type = new HashSet<int>(DesignTokens.TypeScale);
            var failures = new List<string>();

            foreach (var file in Directory.GetFiles(Gtk3Dir, "*.css"))
            {
                var name = Path.GetFileName(file);
                var css = File.ReadAllText(file);

                var gaps = Pixels(css, @"(?:padding|margin)(?:-(?:top|right|bottom|left))?")
                    .Where(px => !spacing.Contains(px)).Distinct();
                var sizes = Pixels(css, "font-size")
                    .Where(px => !type.Contains(px)).Distinct();

                foreach (var px in gaps)
                    failures.Add($"{name}: {px}px is not on the spacing scale ("
                        + string.Join(", ", DesignTokens.Spacing) + ")");
                foreach (var px in sizes)
                    failures.Add($"{name}: {px}px is not on the type scale ("
                        + string.Join(", ", DesignTokens.TypeScale) + ")");
            }

            Assert.AreEqual(0, failures.Count,
                "GTK 3 themes must draw every gap and type size from DesignTokens:\n"
                + string.Join("\n", failures.Take(20)));
        }

        // The primary action is the one branded control: accent fill, readable ink
        [TestMethod]
        public void EveryGtk3Theme_PrimaryActionCarriesTheSchemeAccent()
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
                        failures.Add($"{mode}/{scheme.Id}: missing {scheme.CssFileName}");
                        continue;
                    }

                    var rules = Parse(File.ReadAllText(file));
                    var fill = Prop(rules, PrimaryAction, "background-color");
                    var ink = Prop(rules, PrimaryAction, "color");
                    if (fill == null || ink == null)
                    {
                        failures.Add($"{scheme.CssFileName}: primary action has no fill/ink");
                        continue;
                    }
                    if (!string.Equals(fill, scheme.AccentHex, StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add($"{scheme.CssFileName}: primary action fill {fill} is not "
                            + $"the scheme accent {scheme.AccentHex}");
                    }

                    // Ink must read on the base, hover and active fills
                    var fills = new List<string> { fill };
                    var hover = Prop(rules, PrimaryAction + ":hover", "background-color");
                    var active = Prop(rules, PrimaryAction + ":active", "background-color");
                    if (hover != null) fills.Add(hover);
                    if (active != null) fills.Add(active);

                    foreach (var surface in fills)
                    {
                        if (!ThemeContrast.TryContrast(ink, surface, out var ratio) || ratio < LabelFloor)
                        {
                            failures.Add($"{scheme.CssFileName}: primary action ink {ink} on "
                                + $"{surface} is {ratio:0.00}:1 (< {LabelFloor:0.0})");
                        }
                    }
                }
            }

            Assert.AreEqual(0, failures.Count,
                "every scheme's primary action must carry its accent with readable ink:\n"
                + string.Join("\n", failures.Take(20)));
        }

        // The scale itself must stay a documented, ordered set (no stray steps)
        [TestMethod]
        public void DesignTokenScales_AreOrderedAndDistinct()
        {
            foreach (var (name, values) in new[]
            {
                ("Spacing", DesignTokens.Spacing),
                ("TypeScale", DesignTokens.TypeScale),
            })
            {
                Assert.IsTrue(values.Length > 1, $"{name} must hold more than one step");
                CollectionAssert.AreEqual(values.Distinct().OrderBy(v => v).ToArray(), values,
                    $"{name} must be distinct and ascending");
            }

            // Spacing alone has a "no gap" step; type starts at the smallest readable size
            Assert.AreEqual(0, DesignTokens.Spacing.First(), "Spacing must start at 0 (no gap)");
            Assert.IsTrue(DesignTokens.TypeScale.First() >= 11,
                "TypeScale must not go below the 11px caption step");
        }
    }
}
