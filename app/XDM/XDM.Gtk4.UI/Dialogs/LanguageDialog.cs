// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 language picker. Lists the locales in Lang/index.txt, stores the chosen
// name in Config.Language and lets the next start load that language file (the
// same contract as the GTK 3 dialog).
using System;
using System.Collections.Generic;
using System.IO;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Util;

namespace XDM.Gtk4UI.Dialogs
{
    internal sealed class LanguageDialog
    {
        // Modal window and its language list
        private readonly Window window;
        private readonly ListBox list;
        // Language names in display order
        private readonly List<string> languages = new();
        // Selected language, or null when nothing was chosen
        private string? selected;

        // Builds the picker for the given parent window
        private LanguageDialog(Window parent)
        {
            window = Window.New();
            window.Title = TextResource.GetText("MENU_LANG") ?? "Language";
            window.Modal = true;
            window.SetDefaultSize(420, 460);
            window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);

            var okBtn = Button.NewWithLabel(TextResource.GetText("BTN_OK") ?? "OK");
            okBtn.AddCssClass("suggested-action");
            okBtn.OnClicked += (_, _) =>
            {
                selected = (list.GetSelectedRow() as ListBoxRow)?.Name;
                window.Close();
            };
            var cancelBtn = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            cancelBtn.OnClicked += (_, _) => window.Close();
            header.PackStart(cancelBtn);
            header.PackEnd(okBtn);
            window.SetTitlebar(header);

            var box = Box.New(Orientation.Vertical, 8);
            box.MarginTop = 10;
            box.MarginBottom = 10;
            box.MarginStart = 12;
            box.MarginEnd = 12;

            list = ListBox.New();
            list.SelectionMode = SelectionMode.Single;
            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            scrolled.SetChild(list);
            scrolled.Vexpand = true;
            box.Append(scrolled);

            var hint = Label.New(TextResource.GetText("MSG_LANG_RESTART")
                ?? "The new language is applied the next time FetchFlow starts.");
            hint.AddCssClass("dim-label");
            hint.Wrap = true;
            hint.Halign = Align.Start;
            box.Append(hint);

            window.SetChild(box);
            Populate();
        }

        // Shows the picker and persists the chosen language
        public static void Show(Window parent)
        {
            var dialog = new LanguageDialog(parent);
            var loop = GLib.MainLoop.New(null, false);
            dialog.window.OnDestroy += (_, _) => loop.Quit();
            dialog.window.Present();
            loop.Run();

            if (!string.IsNullOrEmpty(dialog.selected))
            {
                Config.Instance.Language = dialog.selected;
                try { Config.SaveConfig(); } catch { }
            }
        }

        // Fills the list from Lang/index.txt and selects the configured language
        private void Populate()
        {
            var current = Config.Instance.Language ?? "English";
            foreach (var name in ReadLanguageIndex())
            {
                languages.Add(name);
                var row = ListBoxRow.New();
                var label = Label.New(name);
                label.Halign = Align.Start;
                label.Ellipsize = Pango.EllipsizeMode.End;
                label.MarginTop = 6;
                label.MarginBottom = 6;
                label.MarginStart = 10;
                label.MarginEnd = 10;
                row.SetChild(label);
                row.Name = name;
                list.Append(row);

                if (string.Equals(name, current, StringComparison.OrdinalIgnoreCase))
                {
                    list.SelectRow(row);
                }
            }

            // Nothing matched: fall back to the first entry so OK always has a value
            if (list.GetSelectedRow() == null && list.GetRowAtIndex(0) is { } first)
            {
                list.SelectRow(first);
            }
        }

        // Reads the display names from Lang/index.txt, falling back to the core three
        private static List<string> ReadLanguageIndex()
        {
            var names = new List<string>();
            foreach (var candidate in LanguageIndexCandidates())
            {
                try
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    foreach (var line in File.ReadAllLines(candidate))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
                        {
                            continue;
                        }
                        var separator = trimmed.IndexOf('=');
                        var name = separator > 0 ? trimmed.Substring(0, separator).Trim() : trimmed;
                        if (name.Length > 0)
                        {
                            names.Add(name);
                        }
                    }

                    if (names.Count > 0)
                    {
                        return names;
                    }
                }
                catch (Exception ex)
                {
                    TraceLog.Log.Debug(ex, "Language index read failed: " + candidate);
                }
            }

            names.Add("English");
            names.Add("Hindi (हिन्दी)");
            names.Add("Hinglish (Hindi - Latin)");
            return names;
        }

        // Search order for the language index, installed and development layouts
        private static IEnumerable<string> LanguageIndexCandidates()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            yield return Path.Combine(baseDir, "Lang", "index.txt");
            yield return Path.Combine(baseDir, "..", "Lang", "index.txt");
            yield return Path.Combine(baseDir, "Lang", "lang-index.txt");
            yield return "/opt/fetchflow/Lang/index.txt";
            yield return Path.Combine(Directory.GetCurrentDirectory(), "app", "XDM", "Lang", "index.txt");
        }
    }
}
