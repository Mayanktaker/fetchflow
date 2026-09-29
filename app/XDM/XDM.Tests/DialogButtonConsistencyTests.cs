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

        // An unfocused (backdrop) selected download row used to be painted with the
        // FULL header accent in 12 of 14 themes — indistinguishable from the header
        // and it swallowed the accent-coloured tick box. It must be derived from the
        // theme's own focused-selection row instead.
        [TestMethod]
        public void EveryTheme_UnfocusedSelectedRow_IsLighterThanTheHeader()
        {
            var failures = new List<string>();
            foreach (var theme in Directory.GetFiles(ThemeDir, "*.css"))
            {
                var name = Path.GetFileName(theme);
                var rules = Parse(File.ReadAllText(theme));

                string Accent() =>
                    FirstDecl(rules, "treeview.unfinished header button", "background-color");
                string Focused() =>
                    FirstDecl(rules, "treeview.unfinished row:selected", "background-color");
                string Backdrop() =>
                    FirstDecl(rules, "treeview.unfinished row:selected:backdrop", "background-color");
                string BackdropText() =>
                    FirstDecl(rules, "treeview.unfinished row:selected:backdrop", "color");

                var accent = Accent();
                var backdrop = Backdrop();
                var focused = Focused();
                var text = BackdropText();

                if (backdrop == null || accent == null)
                {
                    failures.Add($"{name}: missing backdrop or header rule");
                    continue;
                }
                if (string.Equals(backdrop, accent, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{name}: unfocused selected row ({backdrop}) IS the header "
                        + "accent — the row blends into the header and hides its tick box");
                }
                if (text == null || backdrop == null
                    || string.Equals(text.TrimStart('#'), backdrop.TrimStart('#'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{name}: unfocused row text ({text}) equals its "
                        + "background ({backdrop}) — unreadable");
                }

                if (backdrop.StartsWith("#") && focused.StartsWith("#")
                    && name.StartsWith("xdm-light"))
                {
                    if (Luminance(backdrop) <= Luminance(focused))
                        failures.Add($"{name}: unfocused backdrop {backdrop} is not "
                            + $"lighter than the focused selection {focused}");
                }
                if (backdrop.StartsWith("#") && focused.StartsWith("#")
                    && name.StartsWith("xdm-dark"))
                {
                    if (Luminance(backdrop) >= Luminance(focused))
                        failures.Add($"{name}: unfocused backdrop {backdrop} is not "
                            + $"dimmer than the focused selection {focused}");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "unfocused selected download rows must read as a softer version of the "
                + "focused selection, never as the header colour:\n"
                + string.Join("\n", failures));
        }

        // The header's select-all tick was 11.5px (inherited caption size) — too small
        [TestMethod]
        public void EveryTheme_HeaderSelectAllGlyph_IsBigEnough()
        {
            var failures = new List<string>();
            foreach (var theme in Directory.GetFiles(ThemeDir, "*.css"))
            {
                var name = Path.GetFileName(theme);
                var rules = Parse(File.ReadAllText(theme));
                var size = FirstDecl(rules,
                    "treeview.unfinished header button.list-header-gutter label", "font-size");
                var px = double.TryParse(
                    size?.Replace("px", "").Trim(),
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
                if (px < 14)
                    failures.Add($"{name}: select-all glyph font-size is '{size ?? "<rule missing>"}'"
                        + " (need >= 14px)");
            }

            Assert.AreEqual(0, failures.Count,
                "the header select-all tick must be big enough to see and hit:\n"
                + string.Join("\n", failures));
        }

        // First declaration of a property from the first selector in the group that matches
        private static string FirstDecl(Dictionary<string, Dictionary<string, string>> rules,
            string selectorPrefix, string property)
        {
            foreach (var (sel, props) in rules)
            {
                if (!sel.StartsWith(selectorPrefix, StringComparison.Ordinal)) continue;
                if (props.TryGetValue(property, out var value)) return value;
            }
            return null;
        }

        private static int Luminance(string hex)
        {
            var h = hex.TrimStart('#');
            var r = Convert.ToInt32(h.Substring(0, 2), 16);
            var g = Convert.ToInt32(h.Substring(2, 2), 16);
            var b = Convert.ToInt32(h.Substring(4, 2), 16);
            return (r * 299 + g * 587 + b * 114) / 1000;
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
