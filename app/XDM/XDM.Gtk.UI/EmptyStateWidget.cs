// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using Gtk;
using Translations;
using XDM.Core.UI;
using XDM.GtkUI.Utils;

namespace XDM.GtkUI
{
    // Empty page for a download list: icon, title, hint and the next action
    internal sealed class EmptyStatePanel
    {
        // Root widget added to the list's Stack under the name "empty"
        public Widget Page { get; }
        private readonly Image icon;
        private readonly Label title;
        private readonly Label description;
        private readonly Button addButton;
        private readonly Button clearButton;

        // onAdd opens the new-download menu, onClearSearch exits the fruitless search
        public EmptyStatePanel(System.Action onAdd, System.Action onClearSearch)
        {
            icon = new Image { Halign = Align.Center };
            title = new Label { Halign = Align.Center };
            description = new Label { Halign = Align.Center };
            description.LineWrap = true;
            description.StyleContext.AddClass("view-count");
            addButton = new Button(TextResource.GetText("LBL_ADD_DOWNLOAD") ?? "Add download");
            addButton.StyleContext.AddClass("suggested-action");
            addButton.Halign = Align.Center;
            addButton.Clicked += (_, _) => onAdd?.Invoke();
            clearButton = new Button(TextResource.GetText("LBL_CLEAR_SEARCH") ?? "Clear search");
            clearButton.Halign = Align.Center;
            clearButton.Clicked += (_, _) => onClearSearch?.Invoke();
            var box = new VBox(false, DesignTokens.Space12)
            {
                Halign = Align.Center,
                Valign = Align.Center,
                Hexpand = true,
                Vexpand = true
            };
            box.PackStart(icon, false, false, 0);
            box.PackStart(title, false, false, 0);
            box.PackStart(description, false, false, 0);
            box.PackStart(addButton, false, false, 0);
            box.PackStart(clearButton, false, false, 0);
            Page = box;
        }

        // searching explains the zero-result query, otherwise the list invites a download
        public void ShowFor(bool searching, string? keyword)
        {
            try
            {
                var term = (keyword ?? string.Empty).Trim();
                if (searching)
                {
                    var pix = GtkHelper.LoadSvg("search-line", 48);
                    icon.Visible = pix != null;
                    if (pix != null) icon.Pixbuf = pix;
                    title.Text = string.Format(TextResource.GetText("LBL_NO_RESULTS") ?? "No results for \u201c{0}\u201d", term.Length > 0 ? term : keyword);
                    description.Text = TextResource.GetText("LBL_NO_RESULTS_DESC") ?? "Try a different search term.";
                }
                else
                {
                    var pix = GtkHelper.LoadSvg("download-2-line", 48) ?? GtkHelper.LoadSvg("file-download-line", 48);
                    icon.Visible = pix != null;
                    if (pix != null) icon.Pixbuf = pix;
                    title.Text = TextResource.GetText("LBL_NO_DOWNLOADS") ?? "No downloads to show";
                    description.Text = TextResource.GetText("DESC_NO_DOWNLOADS") ?? "Downloads you start will appear here.";
                }
                addButton.Visible = !searching;
                clearButton.Visible = searching;
            }
            catch { }
        }
    }
}
