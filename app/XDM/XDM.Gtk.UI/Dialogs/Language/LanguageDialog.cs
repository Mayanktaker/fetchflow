// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.Collections.Generic;
using System.IO;
using Gtk;
using Translations;
using UI = Gtk.Builder.ObjectAttribute;
using XDM.Core;
using XDM.GtkUI.Utils;
using IoPath = System.IO.Path;

namespace XDM.GtkUI.Dialogs.Language
{
    // Modal dialog allowing the user to select the UI display language
    public class LanguageDialog : Dialog
    {
        [UI] private Label Label1, Label2;
        [UI] private ComboBox CmbLanguage;
        [UI] private Button BtnOk, BtnCancel;

        public bool Result { get; set; } = false;

        private WindowGroup group;

        // Initializes language chooser dialog and populates available languages
        private LanguageDialog(Builder builder, Window parent, WindowGroup group) : base(builder.GetRawOwnedObject("dialog"))
        {
            builder.Autoconnect(this);

            Modal = true;
            TransientFor = parent;
            this.group = group;
            this.group.AddWindow(this);

            GtkHelper.AttachSafeDispose(this);
            var titleText = TextResource.GetText("MENU_LANG");
            Title = titleText;
            Titlebar = GtkHelper.CreateDialogHeaderBar(titleText);
            GtkHelper.SetWindowAppIcon(this);

            Label1.Text = TextResource.GetText("MSG_LANG1");
            Label2.Text = TextResource.GetText("MSG_LANG2");

            BtnOk.Clicked += BtnOk_Clicked;
            BtnCancel.Clicked += BtnCancel_Clicked;
            BtnOk.StyleContext.AddClass("suggested-action");

            BtnOk.Label = TextResource.GetText("MSG_OK");
            BtnCancel.Label = TextResource.GetText("ND_CANCEL");

            SetDefaultSize(480, 240);
            SetSizeRequest(400, 200);
            Resizable = true;

            PopulateLanguageList();
        }

        // Resolves index.txt path and populates combo box with available locales with flag indicators
        private void PopulateLanguageList()
        {
            var searchPaths = new[]
            {
                IoPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang", "index.txt"),
                IoPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Lang", "index.txt"),
                "/opt/fetchflow/Lang/index.txt"
            };

            var items = new List<string>();
            string? indexFile = null;
            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    indexFile = path;
                    break;
                }
            }

            var selectedIndex = 0;
            var currentIndex = 0;
            var currentLang = Config.Instance.Language ?? "English";

            if (indexFile != null && File.Exists(indexFile))
            {
                var lines = File.ReadAllLines(indexFile);
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                    var eqIdx = line.IndexOf('=');
                    if (eqIdx > 0)
                    {
                        var name = line.Substring(0, eqIdx).Trim();
                        items.Add(name);

                        // Match active language by exact name, prefix, or filename stem
                        if (string.Equals(name, currentLang, StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith(currentLang + " ", StringComparison.OrdinalIgnoreCase) ||
                            currentLang.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = currentIndex;
                        }
                        currentIndex++;
                    }
                }
            }

            // Fallback to core default languages if index.txt is unavailable
            if (items.Count == 0)
            {
                items.Add("English");
                items.Add("Hindi (हिन्दी)");
                items.Add("Hinglish (Hindi - Latin)");
            }

            // Populate combo box with visual flags while storing internal key in column 1
            var store = new ListStore(typeof(string), typeof(string));
            foreach (var name in items)
            {
                var flag = GetLanguageFlag(name);
                var displayText = string.IsNullOrEmpty(flag) ? name : $"{flag}  {name}";
                var iter = store.Append();
                store.SetValue(iter, 0, displayText);
                store.SetValue(iter, 1, name);
            }

            CmbLanguage.Model = store;
            var cell = new CellRendererText { Ellipsize = Pango.EllipsizeMode.End };
            CmbLanguage.PackStart(cell, true);
            CmbLanguage.AddAttribute(cell, "text", 0);
            CmbLanguage.Active = selectedIndex >= 0 && selectedIndex < items.Count ? selectedIndex : 0;
        }

        // Returns regional flag emoji or regional indicator for given language name
        private static string GetLanguageFlag(string name)
        {
            if (name.StartsWith("English", StringComparison.OrdinalIgnoreCase)) return "🇬🇧";
            if (name.StartsWith("Hindi", StringComparison.OrdinalIgnoreCase)) return "🇮🇳";
            if (name.StartsWith("Hinglish", StringComparison.OrdinalIgnoreCase)) return "🇮🇳";
            if (name.StartsWith("Arabic", StringComparison.OrdinalIgnoreCase)) return "🇸🇦";
            if (name.StartsWith("Chinese simplified", StringComparison.OrdinalIgnoreCase)) return "🇨🇳";
            if (name.StartsWith("Chinese Traditional", StringComparison.OrdinalIgnoreCase)) return "🇨🇳";
            if (name.StartsWith("Traditional Chinese", StringComparison.OrdinalIgnoreCase)) return "🇹🇼";
            if (name.StartsWith("Czech", StringComparison.OrdinalIgnoreCase)) return "🇨🇿";
            if (name.StartsWith("Farsi", StringComparison.OrdinalIgnoreCase)) return "🇮🇷";
            if (name.StartsWith("French", StringComparison.OrdinalIgnoreCase)) return "🇫🇷";
            if (name.StartsWith("German", StringComparison.OrdinalIgnoreCase)) return "🇩🇪";
            if (name.StartsWith("Hungarian", StringComparison.OrdinalIgnoreCase)) return "🇭🇺";
            if (name.StartsWith("Indonesian", StringComparison.OrdinalIgnoreCase)) return "🇮🇩";
            if (name.StartsWith("Italian", StringComparison.OrdinalIgnoreCase)) return "🇮🇹";
            if (name.StartsWith("Korea", StringComparison.OrdinalIgnoreCase)) return "🇰🇷";
            if (name.StartsWith("Malagasy", StringComparison.OrdinalIgnoreCase)) return "🇲🇬";
            if (name.StartsWith("Malayalam", StringComparison.OrdinalIgnoreCase)) return "🇮🇳";
            if (name.StartsWith("Nepali", StringComparison.OrdinalIgnoreCase)) return "🇳🇵";
            if (name.StartsWith("Polish", StringComparison.OrdinalIgnoreCase)) return "🇵🇱";
            if (name.StartsWith("Portuguese", StringComparison.OrdinalIgnoreCase)) return "🇧🇷";
            if (name.StartsWith("Romanian", StringComparison.OrdinalIgnoreCase)) return "🇷🇴";
            if (name.StartsWith("Russian", StringComparison.OrdinalIgnoreCase)) return "🇷🇺";
            if (name.StartsWith("Serbian", StringComparison.OrdinalIgnoreCase)) return "🇷🇸";
            if (name.StartsWith("Spanish", StringComparison.OrdinalIgnoreCase)) return "🇪🇸";
            if (name.StartsWith("Turkish", StringComparison.OrdinalIgnoreCase)) return "🇹🇷";
            if (name.StartsWith("Ukrainian", StringComparison.OrdinalIgnoreCase)) return "🇺🇦";
            if (name.StartsWith("Vietnamese", StringComparison.OrdinalIgnoreCase)) return "🇻🇳";
            return "🌐";
        }

        // Closes dialog on cancel click
        private void BtnCancel_Clicked(object? sender, EventArgs e)
        {
            Result = false;
            this.group.RemoveWindow(this);
            Visible = false;
        }

        // Saves selected language to configuration and closes dialog
        private void BtnOk_Clicked(object? sender, EventArgs e)
        {
            Result = true;
            var name = GtkHelper.GetSelectedComboBoxValue<string>(CmbLanguage);
            if (!string.IsNullOrEmpty(name))
            {
                Config.Instance.Language = name;
                Config.SaveConfig();
            }
            this.group.RemoveWindow(this);
            Visible = false;
        }

        // Factory method building the dialog from Glade template
        public static LanguageDialog CreateFromGladeFile(Window parent, WindowGroup group)
        {
            var builder = new Builder();
            builder.AddFromFile(IoPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "glade", "language-dialog.glade"));
            return new LanguageDialog(builder, parent, group);
        }
    }
}
