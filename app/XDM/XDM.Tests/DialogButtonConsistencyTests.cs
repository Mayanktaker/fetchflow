// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Guards the button consistency the UI was fixed for: peer buttons in one dialog
// action row (Add / Edit / Delete / Defaults next to Cancel / Save) must have the
// same border and the same hover, and no button style may be so faint on hover
// that it looks dead.
//
// The action-row treatment (`dialog-action-button`) is generated from each theme's
// OWN `button` rules, so these assertions are what prove a theme cannot drift.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XDM.Tests
{
    [TestClass]
    public class DialogButtonConsistencyTests
    {
        private static string ThemeDir
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                    dir = dir.Parent;
                Assert.IsNotNull(dir, "Could not locate repo root (AGENTS.md)");
                return Path.Combine(dir.FullName, "app", "XDM", "XDM.Gtk.UI", "theme");
            }
        }

        // selector -> property -> value. Later declarations win, which is how the
        // cascade behaves; a multi-selector rule contributes to each of its parts.
        private static Dictionary<string, Dictionary<string, string>> Parse(string css)
        {
            var stripped = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
            var result = new Dictionary<string, Dictionary<string, string>>();
            foreach (Match rule in Regex.Matches(stripped, @"([^{}]+)\{([^{}]*)\}"))
            {
                foreach (var rawSelector in rule.Groups[1].Value.Split(','))
                {
                    var selector = rawSelector.Trim();
                    if (selector.Length == 0) continue;
                    if (!result.TryGetValue(selector, out var props))
                    {
                        props = new Dictionary<string, string>();
                        result[selector] = props;
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
            string selector, string property)
        {
            return rules.TryGetValue(selector, out var props)
                && props.TryGetValue(property, out var value) ? value : null;
        }

        [TestMethod]
        public void EveryTheme_ActionRowButtons_MatchTheThemeBaseButton()
        {
            var themes = Directory.GetFiles(ThemeDir, "*.css");
            Assert.IsTrue(themes.Length > 0, "no themes found");

            var failures = new List<string>();
            foreach (var theme in themes)
            {
                var name = Path.GetFileName(theme);
                var rules = Parse(File.ReadAllText(theme));
                var same = new (string What, string A, string B)[]
                {
                    ("normal background",
                        Prop(rules, "button.dialog-action-button", "background-color"),
                        Prop(rules, "button", "background-color")),
                    ("border",
                        Prop(rules, "button.dialog-action-button", "border"),
                        Prop(rules, "button", "border")),
                    ("hover background",
                        Prop(rules, "button.dialog-action-button:hover", "background-color"),
                        Prop(rules, "button:hover", "background-color")),
                    ("active background",
                        Prop(rules, "button.dialog-action-button:active", "background-color"),
                        Prop(rules, "button:active", "background-color")),
                    ("disabled background",
                        Prop(rules, "button.dialog-action-button:disabled", "background-color"),
                        Prop(rules, "button:disabled", "background-color")),
                };
                foreach (var (what, a, b) in same)
                {
                    if (a != b)
                    {
                        failures.Add($"{name}: action-row {what} '{a}' != base button '{b}'");
                    }
                }

                // The destructive variant must keep the subtle red of the old flat
                // style, not become a loud filled button.
                foreach (var (what, actionSel, baseSel) in new[]
                {
                    ("colour", "button.dialog-action-button.destructive-action",
                        "button.flat.destructive-action"),
                    ("hover background", "button.dialog-action-button.destructive-action:hover",
                        "button.flat.destructive-action:hover"),
                })
                {
                    var a = Prop(rules, actionSel, "color")
                        ?? Prop(rules, actionSel, "background-color");
                    var b = Prop(rules, baseSel, "color")
                        ?? Prop(rules, baseSel, "background-color");
                    if (a != b)
                    {
                        failures.Add($"{name}: destructive {what} '{a}' != '{b}'");
                    }
                }
            }

            Assert.AreEqual(0, failures.Count,
                "dialog action-row buttons must match the theme's base button in every "
                + "theme, or the row reads as links next to the dialog's other buttons:\n"
                + string.Join("\n", failures));
        }

        [TestMethod]
        public void EveryTheme_FlatHover_IsVisibleEnough()
        {
            var failures = new List<string>();
            foreach (var theme in Directory.GetFiles(ThemeDir, "*.css"))
            {
                var name = Path.GetFileName(theme);
                var rules = Parse(File.ReadAllText(theme));
                var hover = Prop(rules, "button.flat:hover", "background-color");
                var match = hover != null
                    ? Regex.Match(hover, @"([\d.]+)\)")
                    : Match.Empty;
                if (!match.Success)
                {
                    failures.Add($"{name}: button.flat:hover has no alpha to read " +
                        $"(got '{hover}')");
                    continue;
                }
                if (double.Parse(match.Groups[1].Value) < 0.10)
                {
                    failures.Add($"{name}: button.flat:hover alpha " +
                        $"{match.Groups[1].Value} is too faint to see");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "a borderless button must still show a hover a user can see:\n"
                + string.Join("\n", failures));
        }
    }
}
