// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Guards the GTK 3 theming policy: a theme paints surfaces only (Adwaita draws
// every control, with the branded primary action as the one allow-listed
// exception), a selected row reads as a softer version of itself when its window
// loses focus, and the app-owned surfaces cannot silently disappear.
//
// The old `button.flat:hover` alpha guard is gone with the hand-drawn buttons it
// measured: no theme declares a `button` rule any more, and
// `EveryTheme_LeavesControlChromeToAdwaita` is what keeps it that way.

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

                string Focused() =>
                    FirstDecl(rules, "treeview.unfinished row:selected", "background-color");
                string Backdrop() =>
                    FirstDecl(rules, "treeview.unfinished row:selected:backdrop", "background-color");
                string BackdropText() =>
                    FirstDecl(rules, "treeview.unfinished row:selected:backdrop", "color");

                var backdrop = Backdrop();
                var focused = Focused();
                var text = BackdropText();

                // The header band is gone (GTK 4 parity); what still matters is that a
                // backdrop row reads as a softer version of its own focused selection
                if (backdrop == null || focused == null)
                {
                    failures.Add($"{name}: missing backdrop or selected-row rule");
                    continue;
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

        // The native-controls policy: a theme paints surfaces, never controls.
        // Adwaita draws buttons, entries, menus, popovers, switches and checks, so
        // a rule for any of them is drift back toward hand-drawn chrome — and it
        // would silently override the user's desktop theme.
        //
        // This replaces the old action-row check: that test proved a generated
        // `dialog-action-button` matched the theme's `button` rules, which only
        // mattered while we painted buttons ourselves.
        [TestMethod]
        public void EveryTheme_LeavesControlChromeToAdwaita()
        {
            var owned = new[] { "button", "entry", "menu", "menuitem", "popover",
                "tooltip", "switch", "check", "radio", "scrollbar", "separator",
                "combobox", "spinbutton", "notebook", "frame" };
            // Selectors we deliberately own (surfaces + the search field's metrics
            // and the branded primary action, which carries the scheme accent)
            var allowed = new[] { "entry.toolbar-search-entry", "button.suggested-action" };

            var failures = new List<string>();
            foreach (var theme in Directory.GetFiles(ThemeDir, "*.css"))
            {
                var name = Path.GetFileName(theme);
                foreach (var selector in Parse(File.ReadAllText(theme)).Keys)
                {
                    if (allowed.Any(a => selector.StartsWith(a, StringComparison.Ordinal)))
                        continue;
                    var node = selector.Split(':', ' ')[0];
                    if (!owned.Any(o => node == o || node.StartsWith(o + ".")))
                        continue;
                    failures.Add($"{name}: '{selector}' styles a control Adwaita owns");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "themes must paint surfaces only; Adwaita draws every control "
                + "(see docs/design.md - Native controls):\n" + string.Join("\n", failures.Take(15)));
        }

        // Every theme must still ship the pieces Adwaita cannot express
        [TestMethod]
        public void EveryTheme_KeepsTheSurfacesOnlyWeCanDraw()
        {
            var required = new[]
            {
                "window", "headerbar", "treeview", "treeview.unfinished row",
                "treeview.finished row", "treeview.unfinished row:selected",
                "treeview.unfinished row:selected:backdrop", "list.sidebar",
                "progressbar progress", "button.suggested-action",
            };

            var failures = new List<string>();
            foreach (var theme in Directory.GetFiles(ThemeDir, "*.css"))
            {
                var name = Path.GetFileName(theme);
                var rules = Parse(File.ReadAllText(theme));
                foreach (var selector in required)
                {
                    if (!rules.ContainsKey(selector))
                        failures.Add($"{name}: missing '{selector}'");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "a theme must still paint the app-owned surfaces:\n"
                + string.Join("\n", failures.Take(15)));
        }
    }
}
