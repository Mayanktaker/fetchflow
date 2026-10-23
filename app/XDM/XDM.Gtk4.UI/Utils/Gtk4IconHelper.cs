// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 icon helper: resolves SVG icon assets, creates scalable Gtk.Image widgets,
// and constructs standard action buttons with icon and text.
using System;
using System.Collections.Generic;
using System.IO;
using Gtk;

namespace XDM.Gtk4UI.Utils
{
    // Utility for loading and configuring GTK 4 SVG icons and icon buttons
    internal static class Gtk4IconHelper
    {
        private static readonly Dictionary<string, string?> CachedPaths = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string IconCacheDir = Path.Combine(Path.GetTempPath(), "fetchflow-icons-tinted");
        private static readonly Dictionary<string, string> TintedPaths = new(StringComparer.OrdinalIgnoreCase);

        // Resolves or creates a tinted version of an SVG asset with the given RGB color
        public static string? GetTintedSvgPath(string name, byte r, byte g, byte b)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var cleanName = name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 4)
                : name;

            // v2: the tint pass now also handles fill-less icons, so keys from the
            // previous pass (which cached some icons untinted) must not be reused
            var cacheKey = $"{cleanName}_{r:X2}{g:X2}{b:X2}_v2";
            if (TintedPaths.TryGetValue(cacheKey, out var existing) && File.Exists(existing))
            {
                return existing;
            }

            var sourcePath = ResolveSvgPath(cleanName);
            if (sourcePath == null || !File.Exists(sourcePath))
            {
                return null;
            }

            try
            {
                if (!Directory.Exists(IconCacheDir))
                {
                    Directory.CreateDirectory(IconCacheDir);
                }

                var targetPath = Path.Combine(IconCacheDir, $"{cacheKey}.svg");
                if (File.Exists(targetPath))
                {
                    TintedPaths[cacheKey] = targetPath;
                    return targetPath;
                }

                var text = File.ReadAllText(sourcePath);
                var tintedText = System.Text.RegularExpressions.Regex.Replace(
                    text,
                    @"fill=""rgba\(\s*128\s*,\s*128\s*,\s*128\s*,\s*1\s*\)""",
                    $"fill=\"rgb({r},{g},{b})\"");

                if (tintedText == text)
                {
                    tintedText = System.Text.RegularExpressions.Regex.Replace(
                        text,
                        @"fill=""#[0-9a-fA-F]{3,6}""",
                        $"fill=\"rgb({r},{g},{b})\"");
                }

                if (tintedText == text)
                {
                    // Some icons carry no fill attribute and inherit black: tint the
                    // first unfilled path so every icon follows the active accent
                    tintedText = System.Text.RegularExpressions.Regex.Replace(
                        text,
                        @"<path(?![^>]*fill=)",
                        $"<path fill=\"rgb({r},{g},{b})\"",
                        System.Text.RegularExpressions.RegexOptions.None,
                        TimeSpan.FromSeconds(1));
                }

                File.WriteAllText(targetPath, tintedText);
                TintedPaths[cacheKey] = targetPath;
                return targetPath;
            }
            catch
            {
                return sourcePath;
            }
        }

        // Creates an Image widget with an SVG asset tinted to the given RGB color
        public static Image CreateTintedIcon(string name, byte r, byte g, byte b, int pixelSize = 16)
        {
            var path = GetTintedSvgPath(name, r, g, b);
            var img = path != null ? Image.NewFromFile(path) : CreateIcon(name, pixelSize);
            img.PixelSize = pixelSize;
            return img;
        }

        // Updates an existing Image widget with a tinted SVG asset
        public static void UpdateTintedIcon(Image image, string name, byte r, byte g, byte b, int pixelSize = 16)
        {
            var path = GetTintedSvgPath(name, r, g, b);
            if (path != null)
            {
                image.SetFromFile(path);
            }
            else
            {
                UpdateIcon(image, name, pixelSize);
            }
            image.PixelSize = pixelSize;
        }

        // Resolves the absolute path to an SVG asset from installed and development directories
        public static string? ResolveSvgPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (CachedPaths.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var cleanName = name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 4)
                : name;

            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "svg-icons", $"{cleanName}.svg"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{cleanName}.svg"),
                Path.Combine("/opt/fetchflow/svg-icons", $"{cleanName}.svg"),
                Path.Combine("/opt/fetchflow", $"{cleanName}.svg"),
                Path.Combine(Directory.GetCurrentDirectory(), "svg-icons", $"{cleanName}.svg"),
                Path.Combine(Directory.GetCurrentDirectory(), $"{cleanName}.svg"),
                Path.Combine(Directory.GetCurrentDirectory(), "app", "XDM", "XDM.Gtk.UI", "svg-icons", $"{cleanName}.svg"),
                Path.Combine(Directory.GetCurrentDirectory(), "app", "XDM", $"{cleanName}.svg"),
                Path.Combine(Directory.GetCurrentDirectory(), "build_output", "xdm-app", "svg-icons", $"{cleanName}.svg")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    CachedPaths[name] = candidate;
                    return candidate;
                }
            }

            CachedPaths[name] = null;
            return null;
        }

        // Creates an Image widget from an SVG asset or falls back to system theme icon name
        public static Image CreateIcon(string name, int pixelSize = 16)
        {
            var path = ResolveSvgPath(name);
            Image img;
            if (path != null)
            {
                img = Image.NewFromFile(path);
            }
            else
            {
                img = Image.NewFromIconName(name);
            }
            img.PixelSize = pixelSize;
            return img;
        }

        // Updates an existing Image widget with a new SVG icon asset or icon name
        public static void UpdateIcon(Image image, string name, int pixelSize = 16)
        {
            var path = ResolveSvgPath(name);
            if (path != null)
            {
                image.SetFromFile(path);
            }
            else
            {
                image.SetFromIconName(name);
            }
            image.PixelSize = pixelSize;
        }

        // Creates an action button with an SVG icon, accessible text label, and tooltip
        public static Button CreateActionButton(
            string iconName,
            string? text,
            string tooltip,
            string? cssClass = null,
            int pixelSize = 16)
        {
            var button = Button.New();
            var box = Box.New(Orientation.Horizontal, 6);
            box.Halign = Align.Center;
            box.Valign = Align.Center;

            var icon = CreateIcon(iconName, pixelSize);
            box.Append(icon);

            if (!string.IsNullOrEmpty(text))
            {
                var label = Label.New(text);
                label.Halign = Align.Center;
                label.Valign = Align.Center;
                box.Append(label);
            }

            button.SetChild(box);
            button.TooltipText = tooltip;

            if (!string.IsNullOrEmpty(cssClass))
            {
                button.AddCssClass(cssClass);
            }

            return button;
        }

        // Configures an existing button with an SVG icon and text label
        public static void ConfigureButton(
            Button button,
            string iconName,
            string? text,
            string tooltip,
            string? cssClass = null,
            int pixelSize = 16)
        {
            var box = Box.New(Orientation.Horizontal, 6);
            box.Halign = Align.Center;
            box.Valign = Align.Center;

            var icon = CreateIcon(iconName, pixelSize);
            box.Append(icon);

            if (!string.IsNullOrEmpty(text))
            {
                var label = Label.New(text);
                label.Halign = Align.Center;
                label.Valign = Align.Center;
                box.Append(label);
            }

            button.SetChild(box);
            button.TooltipText = tooltip;

            if (!string.IsNullOrEmpty(cssClass))
            {
                button.AddCssClass(cssClass);
            }
        }

        // Parses a #rrggbb accent hex into bytes (falling back to the default blue)
        public static (byte R, byte G, byte B) ParseHex(string hex)
        {
            try
            {
                var value = hex.TrimStart('#');
                if (value.Length == 6)
                {
                    return (
                        Convert.ToByte(value.Substring(0, 2), 16),
                        Convert.ToByte(value.Substring(2, 2), 16),
                        Convert.ToByte(value.Substring(4, 2), 16));
                }
            }
            catch { }
            return (53, 132, 228);
        }
    }
}