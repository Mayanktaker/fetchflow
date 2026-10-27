// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Guards the GTK 4 stylesheets against the two scales every other frontend is
// held to: spacing/type from DesignTokens, plus the shared corner-radius list.
// Off-scale values had drifted in (3px padding, 10px and 10.5px type, a 99px
// pill) because GTK 4 CSS had no test at all.
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
    public class Gtk4DesignTokenTests
    {
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

        private static string Gtk4Dir =>
            Path.Combine(Root, "app", "XDM", "XDM.Gtk4.UI", "theme");

        // Comments removed so prose numbers cannot be read as CSS
        private static string StripComments(string css) =>
            Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);

        // Every px number declared by the given property names
        private static IEnumerable<int> Pixels(string css, string propertyPattern)
        {
            var stripped = StripComments(css);
            foreach (Match declaration in Regex.Matches(stripped,
                @"(?<![\w-])" + propertyPattern + @"\s*:\s*([^;]+);"))
            {
                foreach (Match px in Regex.Matches(declaration.Groups[1].Value, @"(\d+)px"))
                {
                    yield return int.Parse(px.Groups[1].Value);
                }
            }
        }

        // Every gap, type size and radius must come from the shared scales
        [TestMethod]
        public void EveryGtk4Theme_UsesOnlyScaleSpacingTypeAndRadius()
        {
            var spacing = new HashSet<int>(DesignTokens.Spacing);
            var type = new HashSet<int>(DesignTokens.TypeScale);
            var radius = new HashSet<int>(DesignTokens.Radius);
            var failures = new List<string>();

            foreach (var file in Directory.GetFiles(Gtk4Dir, "gtk4-*.css"))
            {
                var name = Path.GetFileName(file);
                var css = File.ReadAllText(file);

                foreach (var px in Pixels(css, @"(?:padding|margin)(?:-(?:top|right|bottom|left))?")
                    .Where(px => !spacing.Contains(px)).Distinct())
                {
                    failures.Add($"{name}: {px}px is not on the spacing scale ("
                        + string.Join(", ", DesignTokens.Spacing) + ")");
                }

                foreach (var px in Pixels(css, "font-size")
                    .Where(px => !type.Contains(px)).Distinct())
                {
                    failures.Add($"{name}: {px}px is not on the type scale ("
                        + string.Join(", ", DesignTokens.TypeScale) + ")");
                }

                foreach (var px in Pixels(css, "border-radius")
                    .Where(px => !radius.Contains(px)).Distinct())
                {
                    failures.Add($"{name}: {px}px is not a design-system radius ("
                        + string.Join(", ", DesignTokens.Radius) + ")");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "GTK 4 themes must draw every gap, type size and radius from DesignTokens:\n"
                + string.Join("\n", failures.Take(20)));
        }

        // A defined colour token that is never referenced is dead weight
        [TestMethod]
        public void EveryGtk4Theme_HasNoUnusedColorToken()
        {
            var failures = new List<string>();

            foreach (var file in Directory.GetFiles(Gtk4Dir, "gtk4-*.css"))
            {
                var name = Path.GetFileName(file);
                var css = StripComments(File.ReadAllText(file));
                foreach (Match token in Regex.Matches(css, @"@define-color\s+([a-z_]+)\s+"))
                {
                    var uses = Regex.Matches(css, "@" + token.Groups[1].Value + @"\b").Count;
                    if (uses == 0)
                    {
                        failures.Add($"{name}: @{token.Groups[1].Value} is defined but never used");
                    }
                }
            }

            Assert.AreEqual(0, failures.Count,
                "GTK 4 themes must not define unused colour tokens:\n"
                + string.Join("\n", failures));
        }

        // Both modes must expose the same token names, or a scheme will half-apply
        [TestMethod]
        public void EveryGtk4Theme_DefinesTheSameColorTokens()
        {
            var sets = new List<(string Name, HashSet<string> Tokens)>();

            foreach (var file in Directory.GetFiles(Gtk4Dir, "gtk4-*.css"))
            {
                var css = StripComments(File.ReadAllText(file));
                var tokens = Regex.Matches(css, @"@define-color\s+([a-z_]+)\s+")
                    .Select(m => m.Groups[1].Value)
                    .ToHashSet(StringComparer.Ordinal);
                sets.Add((Path.GetFileName(file), tokens));
            }

            Assert.AreEqual(2, sets.Count, "Expected one GTK 4 stylesheet per mode");
            var expected = sets[0].Tokens;
            foreach (var (name, tokens) in sets.Skip(1))
            {
                CollectionAssert.AreEquivalent(expected.ToList(), tokens.ToList(),
                    name + " declares a different token set than " + sets[0].Name);
            }
        }
    }
}
