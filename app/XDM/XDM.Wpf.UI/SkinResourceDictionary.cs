// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.Windows;

namespace XDM.Wpf.UI
{
    // Merged dictionary that serves the light or dark theme file based on App.Skin
    public class SkinResourceDictionary : ResourceDictionary
    {
        private Uri _darkSource;
        private Uri _lightSource;

        // Dark theme dictionary source
        public Uri DarkSource
        {
            get { return _darkSource; }
            set
            {
                _darkSource = value;
                UpdateSource();
            }
        }

        // Light theme dictionary source
        public Uri LightSource
        {
            get { return _lightSource; }
            set
            {
                _lightSource = value;
                UpdateSource();
            }
        }

        // Re-evaluates the active source after App.Skin changed (live theme switching)
        public void RefreshSkin()
        {
            UpdateSource();
        }

        // Applies the source matching the current App.Skin (no-op when unchanged)
        private void UpdateSource()
        {
            var val = App.Skin == Skin.Dark ? DarkSource : LightSource;
            if (val != null && base.Source != val)
            {
                base.Source = val;
            }
        }
    }
}
