// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// WpfThemeManager — WPF theme engine mirroring XDM.GtkUI.Utils.ThemeManager:
// resolves dark/light + one of the 14 shared color schemes (XDM.Core.UI.ColorSchemeTable),
// swaps the SkinResourceDictionary sources, layers scheme brushes over Application resources
// and recolors live brush instances so already-open windows restyle instantly (no restart).
// UI-thread only; calls from other threads are dispatched to the UI dispatcher.
//
// SCHEME -> BRUSH MAPPING (written into the app-level scheme layer AND recolored in place):
//   AccentBrush           <- AccentHex             (scheme accent)
//   AccentForegroundBrush <- white / near-black    (chosen by accent luminance, see SchemeBrushMapper)
//   RowHoverBrush         <- HoverBackgroundHex    (row hover)
//   RowActiveBrush        <- ActiveBackgroundHex   (row selected)
//   RowAlternateBrush     <- AlternateBackgroundHex (alternating rows)
//   CardBackgroundBrush   <- CardBackgroundHex     (cards / odd rows)
// Existing accent/selection-driven theme keys overridden from scheme tokens so legacy
// StaticResource/DynamicResource consumers follow the active scheme:
//   CategoryHighlight, ListViewSelectedBackcolor, SystemColors.HighlightBrushKey  <- ActiveBackgroundHex
//   ListViewMouseOverBackcolor                                                    <- HoverBackgroundHex
//   TabSelectionColor, ProgressBarForecolor, TextFocusedBorder, TextMouseOverBorder,
//   ButtonFocusedBorder, HyperlinkForecolor                                       <- AccentHex
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using TraceLog;
using XDM.Core;
using XDM.Core.UI;
using WpfApplication = System.Windows.Application;

namespace XDM.Wpf.UI.Utils
{
    // Central theme/scheme controller for the WPF frontend (GTK ThemeManager counterpart)
    public static class WpfThemeManager
    {
        // Registry hive+path exposing the Windows "apps theme" preference
        private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        // DWORD value name inside the Personalize key (0 = apps in dark mode)
        private const string AppsUseLightThemeValueName = "AppsUseLightTheme";
        // AppsUseLightTheme value that means the system runs apps dark
        private const int AppsUseLightThemeDark = 0;
        // Config.ThemeMode codes: 0 = Light, 1 = Dark, 2 = Follow System
        private const int ThemeModeLight = 0;
        private const int ThemeModeDark = 1;
        private const int ThemeModeFollowSystem = 2;

        // Tracks whether the dark theme is actively rendered
        public static bool IsDarkActive { get; private set; }

        // Tracks current active color scheme index
        public static int ActiveColorScheme { get; private set; }

        // Returns the active color scheme definition based on theme mode and scheme index
        public static ColorSchemeDefinition ActiveScheme => ColorSchemeTable.GetScheme(IsDarkActive, ActiveColorScheme);

        // Event raised whenever the active theme or color scheme changes
        public static event Action<bool>? ThemeChanged;

        // Persistent scheme layer dictionary appended to app resources (never re-added)
        private static ResourceDictionary? schemeLayer;
        // Mutable brushes inside the scheme layer, kept live across scheme changes
        private static Dictionary<object, SolidColorBrush>? schemeLayerBrushes;
        // Brush instance sets captured per parsed window generation; recolored on every switch
        private static readonly List<Dictionary<object, SolidColorBrush>> liveBrushSets =
            new List<Dictionary<object, SolidColorBrush>>();

        // Applies theme mode + scheme from persisted config (ThemeMode 2 => follow system)
        public static void ApplyFromConfig()
        {
            EnsureSystemThemeWatcher();
            bool? dark;
            if (Config.Instance.ThemeMode == ThemeModeFollowSystem)
            {
                dark = null;
            }
            else
            {
                dark = Config.Instance.ThemeMode == ThemeModeDark;
            }
            ApplyTheme(dark, Config.Instance.ColorScheme);
        }

        // One-time SystemEvents hook so "Follow System" reacts to the Windows
        // personalization toggle (WM_SETTINGCHANGE) without an app restart
        private static bool systemThemeWatcherAttached;

        // Subscribes the system theme watcher exactly once
        private static void EnsureSystemThemeWatcher()
        {
            if (systemThemeWatcherAttached)
            {
                return;
            }
            systemThemeWatcherAttached = true;
            try
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            }
            catch (Exception ex)
            {
                Log.Debug("Non-fatal: system theme watcher unavailable: " + ex.Message);
            }
        }

        // Re-applies theme when the OS apps-theme flips while in Follow System mode
        private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            try
            {
                if (Config.Instance.ThemeMode != ThemeModeFollowSystem)
                {
                    return;
                }
                var app = WpfApplication.Current;
                var dispatcher = app?.Dispatcher;
                if (dispatcher == null)
                {
                    return;
                }
                dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // Only re-apply when the resolved mode actually flips
                        if (IsSystemAppsThemeDark() != IsDarkActive)
                        {
                            ApplyTheme(null, Config.Instance.ColorScheme);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("Non-fatal: system theme follow failed: " + ex.Message);
                    }
                }));
            }
            catch (Exception ex)
            {
                Log.Debug("Non-fatal: theme change handler failed: " + ex.Message);
            }
        }

        // Toggles between Dark and Light mode, persisting the choice like GTK ThemeManager
        public static void ToggleTheme()
        {
            var newMode = IsDarkActive ? ThemeModeLight : ThemeModeDark;
            Config.Instance.ThemeMode = newMode;
            Config.SaveConfig();
            ApplyTheme(newMode == ThemeModeDark, Config.Instance.ColorScheme);
        }

        // Swaps skin dictionaries, layers scheme brushes and restyles title bars
        public static void ApplyTheme(bool? darkRequested, int? colorSchemeRequested = null)
        {
            var app = WpfApplication.Current;
            var dispatcher = app?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke((Action)(() => ApplyTheme(darkRequested, colorSchemeRequested)));
                return;
            }

            // Resolve mode: null request = follow the Windows system apps theme
            bool dark;
            if (darkRequested.HasValue)
            {
                dark = darkRequested.Value;
            }
            else
            {
                dark = IsSystemAppsThemeDark();
            }

            var schemeIndex = ColorSchemeTable.ClampSchemeIndex(dark, colorSchemeRequested ?? Config.Instance.ColorScheme);
            var scheme = ColorSchemeTable.GetScheme(dark, schemeIndex);

            IsDarkActive = dark;
            ActiveColorScheme = schemeIndex;

            // Keep the legacy Skin enum in sync (drives SkinResourceDictionary + dialog title bars)
            var previousSkin = App.Skin;
            App.Skin = dark ? Skin.Dark : Skin.Light;

            if (app == null)
            {
                ThemeChanged?.Invoke(dark);
                return;
            }

            ApplyThemeToResources(app, previousSkin != App.Skin);
            ApplyTitleBarTheme(app, dark);
            ThemeChanged?.Invoke(dark);
        }

        // Detects the Windows system apps theme via registry; false when unavailable
        private static bool IsSystemAppsThemeDark()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath))
                {
                    if (key != null && key.GetValue(AppsUseLightThemeValueName) is int value)
                    {
                        return value == AppsUseLightThemeDark;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Non-fatal: system theme registry read failed: " + ex.Message);
            }
            return false;
        }

        // Reloads skin dictionaries and recolors every live brush set from the new theme + scheme
        private static void ApplyThemeToResources(WpfApplication app, bool modeChanged)
        {
            var skinDictionaries = FindSkinDictionaries(app.Resources).ToList();
            var firstRegistration = liveBrushSets.Count == 0;

            // Capture pre-swap brushes: they are baked into already-parsed windows
            if (modeChanged || firstRegistration)
            {
                foreach (var skin in skinDictionaries)
                {
                    RegisterBrushSet(skin);
                }
            }

            // Swap theme file content (no-op when the mode did not change)
            if (modeChanged)
            {
                foreach (var skin in skinDictionaries)
                {
                    skin.RefreshSkin();
                }
                foreach (var skin in skinDictionaries)
                {
                    RegisterBrushSet(skin);
                }
            }

            var targets = BuildTargetColors(skinDictionaries, ActiveScheme);
            EnsureSchemeLayer(app);

            // Recolor all captured instances so StaticResource references follow along live
            foreach (var brushSet in liveBrushSets)
            {
                foreach (var pair in brushSet)
                {
                    if (targets.TryGetValue(pair.Key, out var color) && !pair.Value.IsFrozen)
                    {
                        pair.Value.Color = color;
                    }
                }
            }
        }

        // Enumerates every SkinResourceDictionary merged anywhere below the given root
        private static IEnumerable<SkinResourceDictionary> FindSkinDictionaries(ResourceDictionary root)
        {
            foreach (var merged in root.MergedDictionaries)
            {
                if (merged is SkinResourceDictionary skin)
                {
                    yield return skin;
                }
                else
                {
                    foreach (var nested in FindSkinDictionaries(merged))
                    {
                        yield return nested;
                    }
                }
            }
        }

        // Snapshots the mutable SolidColorBrush instances of a dictionary for later recoloring
        private static void RegisterBrushSet(ResourceDictionary dictionary)
        {
            var brushSet = new Dictionary<object, SolidColorBrush>();
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Value is SolidColorBrush brush && !brush.IsFrozen)
                {
                    brushSet[entry.Key] = brush;
                }
            }
            if (brushSet.Count > 0)
            {
                liveBrushSets.Add(brushSet);
            }
        }

        // Merges fresh theme file colors with the scheme overrides into one key->Color map
        private static Dictionary<object, Color> BuildTargetColors(
            IEnumerable<SkinResourceDictionary> skinDictionaries, ColorSchemeDefinition scheme)
        {
            var targets = new Dictionary<object, Color>();
            foreach (var skin in skinDictionaries)
            {
                foreach (DictionaryEntry entry in skin)
                {
                    if (entry.Value is SolidColorBrush brush)
                    {
                        targets[entry.Key] = brush.Color;
                    }
                }
            }

            // Layer the scheme palette over the theme file colors (mapping documented above)
            var accent = SchemeBrushMapper.ParseColor(scheme.AccentHex);
            targets["AccentBrush"] = accent;
            targets["AccentForegroundBrush"] = SchemeBrushMapper.ParseColor(SchemeBrushMapper.ForegroundHexFor(accent));
            targets["RowHoverBrush"] = SchemeBrushMapper.ParseColor(scheme.HoverBackgroundHex);
            targets["RowActiveBrush"] = SchemeBrushMapper.ParseColor(scheme.ActiveBackgroundHex);
            targets["RowAlternateBrush"] = SchemeBrushMapper.ParseColor(scheme.AlternateBackgroundHex);
            targets["CardBackgroundBrush"] = SchemeBrushMapper.ParseColor(scheme.CardBackgroundHex);
            targets["CategoryHighlight"] = targets["RowActiveBrush"];
            targets["ListViewSelectedBackcolor"] = targets["RowActiveBrush"];
            targets[SystemColors.HighlightBrushKey] = targets["RowActiveBrush"];
            targets["ListViewMouseOverBackcolor"] = targets["RowHoverBrush"];
            targets["TabSelectionColor"] = accent;
            targets["ProgressBarForecolor"] = accent;
            targets["TextFocusedBorder"] = accent;
            targets["TextMouseOverBorder"] = accent;
            targets["ButtonFocusedBorder"] = accent;
            targets["HyperlinkForecolor"] = accent;
            return targets;
        }

        // Creates (once) and appends the scheme layer, then recolors it like every other set
        private static void EnsureSchemeLayer(WpfApplication app)
        {
            if (schemeLayer == null)
            {
                schemeLayer = new ResourceDictionary();
                schemeLayerBrushes = new Dictionary<object, SolidColorBrush>();
                foreach (var key in SchemeBrushMapper.SchemeOverrideKeys)
                {
                    // Mutable (not frozen) on purpose: recoloring shared instances keeps
                    // StaticResource references live across scheme changes
                    var brush = new SolidColorBrush(Colors.Transparent);
                    schemeLayer[key] = brush;
                    schemeLayerBrushes[key] = brush;
                }
                liveBrushSets.Add(schemeLayerBrushes);
            }
            if (!app.Resources.MergedDictionaries.Contains(schemeLayer))
            {
                app.Resources.MergedDictionaries.Add(schemeLayer);
            }
        }

        // Applies the immersive dark/light title bar attribute to every open window
        private static void ApplyTitleBarTheme(WpfApplication app, bool dark)
        {
            var windows = new Window[app.Windows.Count];
            app.Windows.CopyTo(windows, 0);
            foreach (var window in windows)
            {
                try
                {
                    var handle = new WindowInteropHelper(window).Handle;
                    if (handle != IntPtr.Zero)
                    {
                        DarkModeHelper.UseImmersiveDarkMode(handle, dark);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug("Non-fatal: title bar theme not applied: " + ex.Message);
                }
            }
        }
    }
}
