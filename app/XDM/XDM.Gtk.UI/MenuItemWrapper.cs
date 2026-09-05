// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Gtk;
using System;
using XDM.Core.UI;
using XDM.GtkUI.Utils;

namespace XDM.GtkUI
{
    // Wraps Gtk.MenuItem with monochrome icon and IMenuItem state handling
    internal class MenuItemWrapper : IMenuItem
    {
        private MenuItem menuItem;
        private string name;
        private string? iconName;
        private Image? iconImage;
        private Label? textLabel;

        public string Name => name;
        public MenuItem MenuItem => menuItem;

        public bool Enabled
        {
            get => menuItem.IsSensitive;
            set => menuItem.Sensitive = value;
        }

        public event EventHandler? Clicked;

        public MenuItemWrapper(string name, string text, bool visible = true, string? iconName = null)
        {
            this.name = name;
            this.iconName = iconName;
            this.menuItem = new MenuItem();
            this.menuItem.Name = name;

            var box = new HBox(false, 10)
            {
                MarginStart = 2,
                MarginEnd = 6,
                MarginTop = 2,
                MarginBottom = 2
            };

            if (!string.IsNullOrEmpty(iconName))
            {
                var rawPixbuf = GtkHelper.LoadSvg(iconName, 16);
                var tinted = rawPixbuf != null
                    ? (ThemeManager.IsDarkActive ? GtkHelper.TintPixbuf(rawPixbuf, 200, 200, 200) : GtkHelper.TintPixbuf(rawPixbuf, 90, 90, 90))
                    : null;
                this.iconImage = tinted != null ? new Image(tinted) : new Image();
                box.PackStart(this.iconImage, false, false, 0);
            }

            this.textLabel = new Label(text)
            {
                Halign = Align.Start,
                Xalign = 0
            };
            box.PackStart(this.textLabel, true, true, 0);
            this.menuItem.Add(box);

            if (visible)
            {
                this.menuItem.ShowAll();
            }
            this.menuItem.Activated += Mi_Click;
        }

        // Updates row text (used for state dots/ticks in custom submenus)
        public void SetText(string text)
        {
            if (textLabel != null)
            {
                textLabel.Text = text;
            }
        }

        // Refreshes icon tint dynamically on theme switch
        public void UpdateTheme(bool isDark)
        {
            if (iconImage != null && !string.IsNullOrEmpty(iconName))
            {
                var rawPixbuf = GtkHelper.LoadSvg(iconName, 16);
                if (rawPixbuf != null)
                {
                    iconImage.Pixbuf = isDark ? GtkHelper.TintPixbuf(rawPixbuf, 200, 200, 200) : GtkHelper.TintPixbuf(rawPixbuf, 90, 90, 90);
                }
            }
        }

        private void Mi_Click(object? sender, EventArgs e)
        {
            this.Clicked?.Invoke(sender, e);
        }
    }
}
