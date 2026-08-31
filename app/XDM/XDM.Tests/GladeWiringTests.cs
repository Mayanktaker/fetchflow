// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Regression for SettingsDialog LoadTexts NRE (crash.log: TargetInvocationException @ line 508
// ChkMonitorClipboard was null because [UI] was missing and Builder.Autoconnect silently
// skipped it — GH/GTK headless CI can't run Gtk.Builder, so this test parses glade XML and
// C# [UI] declarations and fails on any id drift in any dialog under app/XDM/XDM.Gtk.UI).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XDM.Tests
{
    [TestClass]
    public class GladeWiringTests
    {
        // Resolve paths relative to the test dll — works both `dotnet test` and VS.
        private static string RepoRoot
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

        [TestMethod]
        public void EveryDialogFactory_ShouldHaveMatchingGladeAnd_UI_Wiring()
        {
            var gtkUiRoot = Path.Combine(RepoRoot, "app", "XDM", "XDM.Gtk.UI");
            var gladeDir = Path.Combine(gtkUiRoot, "glade");
            var dialogsDir = Path.Combine(gtkUiRoot, "Dialogs");

            // Map glade file -> dialog C# file via naming convention (settings-dialog.glade -> SettingsDialog.cs)
            var factories = DiscoverFactories(dialogsDir);
            Assert.IsTrue(factories.Count >= 10,
                $"Expected at least 10 CreateFromGladeFile factories, found {factories.Count}");

            var failures = new List<string>();

            foreach (var (csPath, gladeFile) in factories)
            {
                if (!File.Exists(Path.Combine(gladeDir, gladeFile)))
                {
                    failures.Add($"{Path.GetFileName(csPath)} -> {gladeFile}: glade file missing");
                    continue;
                }

                var xmlIds = GladeIds(Path.Combine(gladeDir, gladeFile));
                var uiIds = UiAttributedIds(csPath);

                // Every non-builtin glade id that looks like a widget (Button/Label/Combo/Check/Entry/Tree/Notebook/LinkButton)
                // should be wired via [UI] unless it's intentionally accessed via Builder.GetObject (whitelisted).
                var interesting = xmlIds.Where(id => IsInterestingWidgetId(id) && !IsWhitelisted(gladeFile, id) && !IsCsOnlyWhitelisted(gladeFile, id));
                foreach (var id in interesting)
                {
                    if (!uiIds.Contains(id))
                        failures.Add($"{gladeFile}: glade id '{id}' has no [UI] field in {Path.GetFileName(csPath)} (Autoconnect will leave it null -> NRE in LoadTexts/other init)");
                }
            }

            if (failures.Count > 0)
                Assert.Fail("Glade wiring drift (" + failures.Count + "):\n" + string.Join("\n", failures));
        }

        [TestMethod]
        public void SettingsDialog_MustWire_ChkMonitorClipboard_ChkTimestamp_AndBtnDefaults()
        {
            // Pin the exact bug that caused the 17:50 crash: these three ids were in the glade but
            // their fields lacked [UI], so Builder.Autoconnect left them null and LoadTexts at line
            // 506/508 threw TargetInvocationException wrapping NullReferenceException.
            var gtkUiRoot = Path.Combine(RepoRoot, "app", "XDM", "XDM.Gtk.UI");
            var csPath = Path.Combine(gtkUiRoot, "Dialogs", "Settings", "SettingsDialog.cs");
            var gladePath = Path.Combine(gtkUiRoot, "glade", "settings-dialog.glade");

            Assert.IsTrue(File.Exists(csPath), "SettingsDialog.cs missing");
            Assert.IsTrue(File.Exists(gladePath), "settings-dialog.glade missing");

            var uiIds = UiAttributedIds(csPath);
            var mustExist = new[] { "ChkMonitorClipboard", "ChkTimestamp", "BtnDefault1", "BtnDefault2", "BtnDefault3" };
            foreach (var id in mustExist)
                Assert.IsTrue(uiIds.Contains(id),
                    $"SettingsDialog.cs must have [UI] {id} — missing this caused the LoadTexts NRE in crash.log");
        }

        [TestMethod]
        public void EveryGladeIdUsedInCode_HasMatchingGladeObject()
        {
            // Converse drift: a [UI] field that has no glade object will also stay null.
            // This test is intentionally scoped to the factories' own glade files (the AddFromFile
            // target) — cross-matching by basename produces false positives for dialogs that share
            // generic ids like Label1 across glades.
            var gtkUiRoot = Path.Combine(RepoRoot, "app", "XDM", "XDM.Gtk.UI");
            var gladeDir = Path.Combine(gtkUiRoot, "glade");
            var dialogsDir = Path.Combine(gtkUiRoot, "Dialogs");

            var gladeIdsByFile = Directory.GetFiles(gladeDir, "*.glade")
                .ToDictionary(f => Path.GetFileName(f), f => GladeIds(f));
            var factoryGladeByCs = DiscoverFactories(dialogsDir)
                .GroupBy(f => f.CsPath)
                .ToDictionary(g => g.Key, g => g.Select(f => f.GladeFile).Distinct().ToList());

            var failures = new List<string>();
            foreach (var kv in factoryGladeByCs)
            {
                var csFile = kv.Key;
                var uiIds = UiAttributedIds(csFile);
                if (uiIds.Count == 0) continue;

                var gladeCandidates = kv.Value;
                foreach (var gladeFile in gladeCandidates)
                {
                    if (!gladeIdsByFile.TryGetValue(gladeFile, out var gladeSet)) continue;
                    foreach (var id in uiIds)
                    {
                        if (IsBoilerplateId(id)) continue;
                        // Shared helpers like GtkHelper create menus programmatically; ignore those.
                        if (id.StartsWith("Gtk", StringComparison.Ordinal)) continue;
                        if (!gladeSet.Contains(id) && !IsWhitelisted(gladeFile, id) && !IsCsOnlyWhitelisted(gladeFile, id))
                            failures.Add($"{Path.GetFileName(csFile)}: [UI] '{id}' has no <object id=\"{id}\"> in {gladeFile}");
                    }
                }
            }

            if (failures.Count > 0)
                Assert.Fail("Glade -> [UI] mismatch (field has no glade object):\n" + string.Join("\n", failures));
        }

        // ---- helpers ----

        private record Factory(string CsPath, string GladeFile);

        private static List<Factory> DiscoverFactories(string dialogsDir)
        {
            var list = new List<Factory>();
            // Each dialog's CreateFromGladeFile does: builder.AddFromFile(..., "glade", "<file>.glade")
            var re = new Regex(@"AddFromFile\([^)]*""glade""[^)]*,\s*""([^""]+\.glade)""", RegexOptions.Compiled);
            foreach (var cs in Directory.GetFiles(dialogsDir, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(cs);
                if (!text.Contains("CreateFromGladeFile")) continue;
                foreach (Match m in re.Matches(text))
                    list.Add(new Factory(cs, m.Groups[1].Value));
            }
            return list;
        }

        private static HashSet<string> GladeIds(string gladePath)
        {
            var doc = XDocument.Load(gladePath);
            return doc.Descendants("object")
                .Attributes("id")
                .Select(a => a.Value)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static HashSet<string> UiAttributedIds(string csPath)
        {
            // Collect every identifier on a line block that follows a [UI] attribute.
            // Handles two forms in this repo:
            //   1) `private Label TabHeader1, TabHeader2;` (multi-id)
            //   2) `[UI] private Button btnOk = null;` (single-id with initializer)
            // Also handles SettingsDialog's comma-list with naked `Button BtnChrome, ...` after [UI].
            var text = File.ReadAllText(csPath);
            var set = new HashSet<string>(StringComparer.Ordinal);
            // Split on [UI] blocks, then parse the following declaration up to ; .
            var blocks = Regex.Split(text, @"\[UI\]");
            var typeLike = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Label","Button","CheckButton","Entry","ComboBox","ComboBoxText","TreeView","Notebook","ListBox","LinkButton","Image","Box","Menu","MenuButton","SpinButton","Window","MenuItem","Gtk","System","private","public","protected","internal","readonly","static" };
            foreach (var block in blocks.Skip(1))
            {
                var semi = block.IndexOf(';');
                if (semi < 0) continue;
                var decl = block.Substring(0, semi);
                // decl is like " private Button btnOk = null" or " private Label A, B, C" or " Button BtnChrome, ..." .
                // Extract identifiers that look like field names (exclude type names and keywords).
                var candidates = Regex.Matches(decl, @"\b([A-Za-z_][A-Za-z0-9_]*)\b");
                foreach (Match m in candidates)
                {
                    var tok = m.Groups[1].Value;
                    if (tok == "null" || tok.Contains('.')) continue;
                    if (typeLike.Contains(tok)) continue;
                    if (!char.IsLetter(tok[0]) && tok[0] != '_') continue;
                    // First token that is not a type is a field name; for multi-id we get all of them.
                    set.Add(tok);
                }
                // For the "= null" single-field form we captured the type as well; remove it if it leaked.
                // That's fine — it'll be filtered by typeLike, but keep the field itself.
            }
            // Remove any type that leaked as a field (e.g. Button in `Button BtnChrome` -> both matched, Button is filtered above).
            return set;
        }

        private static bool IsInterestingWidgetId(string id)
        {
            // Skip Gtk boilerplate containers; keep anything that looks like a widget we'd assign a field to.
            // Layout-only ids (HeaderBox, mainBox, button-box) are structural and never [UI]-wired.
            if (id is "window" or "dialog" or "ActionArea" or "HeaderBox" or "mainBox" or "button-box") return false;
            if (id is "ScrolledWindow" or "ProgressBar" or "TextView") return false;
            if (id.StartsWith("adjustment", StringComparison.OrdinalIgnoreCase)) return false;
            if (id.StartsWith("Header", StringComparison.OrdinalIgnoreCase) && id.Length <= 8) return false;
            return true;
        }

        private static bool IsBoilerplateId(string id)
            => id is "window" or "dialog" or "ActionArea" or "TabControl" or "mainBox" or "menu1" or "HeaderBox" or "button-box"
            || id is "ScrolledWindow" or "ProgressBar" or "TextView" // Gtk type names used as generic ids in some glades; not [UI]-wired
            || id.StartsWith("adjustment", StringComparison.OrdinalIgnoreCase);

        private static bool IsWhitelisted(string gladeFile, string id)
        {
            // Known ids that are intentionally not wired via [UI] (accessed via Builder.GetObject or not needed).
            // Note: SettingsDialog intentionally carries BtnChromium/BtnYandex fields with no matching glade object
            // (legacy browser set); they are handled below as "cs-only" exclusions, not glade-only ones here.
            if (gladeFile == "settings-dialog.glade" && id == "ActionArea") return true;
            if (gladeFile == "queue-manager-dialog.glade" && id is "ActionArea" or "TabControl") return true;
            // This path is only for "glade id has no [UI]" — do not whitelist cs-only mismatches here.
            return false;
        }

        private static bool IsCsOnlyWhitelisted(string gladeFile, string id)
        {
            // C# fields that intentionally have no matching glade object (orphaned/unfinished wiring
            // that is kept for compat or future use; verified not dereferenced in the non-null path).
            // SettingsDialog.BtnChromium/BtnYandex are now non-[UI] compat stubs (not in this set).
            if (gladeFile == "advanced-download-dialog.glade" && id is "LblSpeedLimit" or "TxtSpeedLimit" or "tabPage3") return true;
            if (gladeFile == "batch-download-dialog.glade" && id == "TxtDownloadLinks") return true;
            if (gladeFile == "new-video-download-window.glade" && id is "mainBox" or "menu1") return true;
            return false;
        }
    }
}
