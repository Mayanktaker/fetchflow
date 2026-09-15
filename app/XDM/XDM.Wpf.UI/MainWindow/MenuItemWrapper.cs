// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using XDM.Core.UI;

namespace XDM.Wpf.UI
{
    // Wraps a MenuItem for the Core menu model; auto-assigns GTK-parity Remix icons by item name
    internal class MenuItemWrapper : IMenuItem
    {
        // Item name -> Remix geometry key (mirrors GTK CreateIconMenuItem assignments)
        private static readonly Dictionary<string, string> IconMap = new Dictionary<string, string>
        {
            ["pause"] = "ri-pause-line",
            ["resume"] = "ri-play-line",
            ["delete"] = "ri-delete-bin-7-line",
            ["deleteDownloads"] = "ri-delete-bin-7-line",
            ["saveAs"] = "ri-download-2-line",
            ["refresh"] = "ri-refresh-line",
            ["restart"] = "ri-refresh-line",
            ["downloadAgain"] = "ri-refresh-line",
            ["showProgress"] = "ri-time-line",
            ["copyURL"] = "ri-links-line",
            ["copyURL1"] = "ri-links-line",
            ["copyFile"] = "ri-file-copy-line",
            ["moveToQueue"] = "ri-arrow-down-line",
            ["properties"] = "ri-settings-3-line",
            ["properties1"] = "ri-settings-3-line",
            ["open"] = "ri-external-link-line",
            ["openFolder"] = "ri-folder-shared-line",
            ["verifyChecksum"] = "ri-check-line",
            ["schedule"] = "ri-time-line"
        };

        private MenuItem menu;

        public MenuItemWrapper(string name, string text) : this(name, text, true)
        { }

        public MenuItemWrapper(string name, string text, bool visible)
        {
            this.menu = new MenuItem
            {
                Name = name,
                Header = text,
                IsEnabled = false,
                Visibility = visible ? Visibility.Visible : Visibility.Collapsed
            };
            if (IconMap.TryGetValue(name, out var iconKey))
            {
                SetMenuItemIcon(this.menu, iconKey);
            }
            this.menu.Click += Mi_Click;
        }

        // Attaches a themed Remix icon to any menu item (shared with MainWindow's static menu)
        internal static void SetMenuItemIcon(MenuItem item, string iconKey)
        {
            if (!(Application.Current?.TryFindResource(iconKey) is Geometry geometry))
            {
                return;
            }
            var icon = new Path
            {
                Data = geometry,
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true
            };
            // Live theme/scheme following for the icon tint
            icon.SetResourceReference(Shape.FillProperty, "ControlForecolor");
            icon.SetResourceReference(Shape.StrokeProperty, "ControlForecolor");
            item.Icon = icon;
        }

        private void Mi_Click(object sender, RoutedEventArgs e)
        {
            this.Clicked?.Invoke(this, e);
        }

        public string Name => menu.Name;

        public bool Enabled
        {
            get => menu.IsEnabled;
            set => menu.IsEnabled = value;
        }

        public event EventHandler? Clicked;

        public MenuItem Menu => menu;
    }
}
