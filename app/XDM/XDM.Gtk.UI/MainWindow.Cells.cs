// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Gtk;
using Application = Gtk.Application;
using IoPath = System.IO.Path;
using XDM.Core;
using XDM.Core.Util;
using XDM.Core.UI;
using Translations;
using Menu = Gtk.Menu;
using MenuItem = Gtk.MenuItem;
using XDM.GtkUI.Utils;
using XDM.GtkUI.Dialogs.DeleteConfirm;
using XDM.GtkUI.Dialogs.Language;
using TraceLog;
using XDM.Core.BrowserMonitoring;


namespace XDM.GtkUI
{
    public partial class MainWindow : Window, IApplicationWindow
    {
        // Resolves design token RGB tint for file type categories
        private static (byte R, byte G, byte B) GetCategoryColorForFileType(string filename)
        {
            var ext = System.IO.Path.GetExtension(filename)?.ToLowerInvariant() ?? string.Empty;
            var fileType = IconResource.GetFileType(ext);
            return fileType switch
            {
                "Video" => (DestructR, DestructG, DestructB),
                "Music" => (PurpleR, PurpleG, PurpleB),
                "Document" => (AmberR, AmberG, AmberB),
                "Compressed" => (TealR, TealG, TealB),
                "Application" or "ApplicationContext.Core" => (IndigoR, IndigoG, IndigoB),
                "Image" => (RoseR, RoseG, RoseB),
                _ => (SkyR, SkyG, SkyB)
            };
        }

        // Formats absolute paths into user-friendly ~/ paths
        private static string FormatFriendlyPath(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return string.Empty;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home) && folder.StartsWith(home))
            {
                return "~" + folder.Substring(home.Length);
            }
            return folder;
        }

        // Extracts clean hostname from URL
        private static string ExtractDomain(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            try
            {
                if (url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
                {
                    return "blob";
                }
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var host = uri.Host;
                    if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    {
                        host = host.Substring(4);
                    }
                    return host;
                }
            }
            catch { }
            return string.Empty;
        }

        // Rich two-line renderer for completed downloads
        private void SetFinishedNameColumn(TreeViewColumn column, CellRendererText renderer, TreeView view)
        {
            column.SetCellDataFunc(renderer, new CellLayoutDataFunc((_, cell, model, iter) =>
            {
                var path = model.GetPath(iter);
                var selected = view.Selection.PathIsSelected(path);
                var isHovered = !selected && hoveredFinishedPath != null && hoveredFinishedPath.Compare(path) == 0;
                var isAlternate = !selected && !isHovered && path.Indices != null && path.Indices.Length > 0 && (path.Indices[0] % 2 == 1);

                ((CellRendererText)cell).CellBackground = TreeViewSelectionHelper.RowCellBackground(isHovered, isAlternate, ThemeManager.ActiveAlternateRowColor);

                var name = model.GetValue(iter, 0) as string ?? string.Empty;
                var item = model.GetValue(iter, FINISHED_DATA_INDEX) as FinishedDownloadItem;

                var title = GLib.Markup.EscapeText(name);
                var metaParts = new List<string>();

                if (item != null)
                {
                    var folder = FormatFriendlyPath(item.TargetDir);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        metaParts.Add($"◫ {GLib.Markup.EscapeText(folder)}");
                    }

                    var domain = ExtractDomain(item.PrimaryUrl);
                    if (!string.IsNullOrEmpty(domain))
                    {
                        metaParts.Add($"◉ {GLib.Markup.EscapeText(domain)}");
                    }

                    var ext = System.IO.Path.GetExtension(name)?.TrimStart('.')?.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ext))
                    {
                        metaParts.Add(GLib.Markup.EscapeText(ext));
                    }
                }

                var metaLine = string.Join("   ·   ", metaParts);

                if (selected)
                {
                    var isDark = ThemeManager.IsDarkActive;
                    var titleCol = isDark ? "#ffffff" : "#0f172a";
                    var metaCol = isDark ? "#ffffff" : "#334155";
                    var metaAlpha = isDark ? $"{MetaSubLineAlphaDark}" : $"{MetaSubLineAlpha}";
                    ((CellRendererText)cell).Markup = string.IsNullOrEmpty(metaLine)
                        ? $"<span weight=\"bold\" color=\"{titleCol}\">{title}</span>"
                        : $"<span weight=\"bold\" color=\"{titleCol}\">{title}</span>\n<span size=\"{MetaSubLineSize}\" alpha=\"{metaAlpha}\" color=\"{metaCol}\">{metaLine}</span>";
                }
                else
                {
                    // Hover feedback lives in the title (accent color) because GTK3
                    // TreeView rows never match :hover CSS — a cell fill would render
                    // square and cover the theme's rounded row shape.
                    var titleMarkup = isHovered
                        ? $"<span weight=\"bold\" color=\"{ThemeManager.ActiveAccentHex}\">{title}</span>"
                        : $"<span weight=\"bold\">{title}</span>";
                    ((CellRendererText)cell).Markup = string.IsNullOrEmpty(metaLine)
                        ? titleMarkup
                        : $"{titleMarkup}\n<span size=\"{MetaSubLineSize}\" alpha=\"{MetaSubLineAlpha}\">{metaLine}</span>";
                }
            }));
        }

        // Rich two-line renderer for in-progress downloads
        private void SetInProgressNameColumn(TreeViewColumn column, CellRendererText renderer, TreeView view)
        {
            column.SetCellDataFunc(renderer, new CellLayoutDataFunc((_, cell, model, iter) =>
            {
                var path = model.GetPath(iter);
                var selected = view.Selection.PathIsSelected(path);
                var isHovered = !selected && hoveredInprogressPath != null && hoveredInprogressPath.Compare(path) == 0;
                var isAlternate = !selected && !isHovered && path.Indices != null && path.Indices.Length > 0 && (path.Indices[0] % 2 == 1);

                ((CellRendererText)cell).CellBackground = TreeViewSelectionHelper.RowCellBackground(isHovered, isAlternate, ThemeManager.ActiveAlternateRowColor);

                var name = model.GetValue(iter, 0) as string ?? string.Empty;
                var item = model.GetValue(iter, INPROGRESS_DATA_INDEX) as InProgressDownloadItem;

                var title = GLib.Markup.EscapeText(name);
                var metaParts = new List<string>();

                if (item != null)
                {
                    var domain = ExtractDomain(item.PrimaryUrl);
                    if (!string.IsNullOrEmpty(domain))
                    {
                        metaParts.Add($"◉ {GLib.Markup.EscapeText(domain)}");
                    }

                    var folder = FormatFriendlyPath(item.TargetDir);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        metaParts.Add($"◫ {GLib.Markup.EscapeText(folder)}");
                    }
                }

                var metaLine = string.Join("   ·   ", metaParts);

                if (selected)
                {
                    var isDark = ThemeManager.IsDarkActive;
                    var titleCol = isDark ? "#ffffff" : "#0f172a";
                    var metaCol = isDark ? "#ffffff" : "#334155";
                    var metaAlpha = isDark ? $"{MetaSubLineAlphaDark}" : $"{MetaSubLineAlpha}";
                    ((CellRendererText)cell).Markup = string.IsNullOrEmpty(metaLine)
                        ? $"<span weight=\"bold\" color=\"{titleCol}\">{title}</span>"
                        : $"<span weight=\"bold\" color=\"{titleCol}\">{title}</span>\n<span size=\"{MetaSubLineSize}\" alpha=\"{metaAlpha}\" color=\"{metaCol}\">{metaLine}</span>";
                }
                else
                {
                    // Hover feedback lives in the title (accent color) because GTK3
                    // TreeView rows never match :hover CSS — a cell fill would render
                    // square and cover the theme's rounded row shape.
                    var titleMarkup = isHovered
                        ? $"<span weight=\"bold\" color=\"{ThemeManager.ActiveAccentHex}\">{title}</span>"
                        : $"<span weight=\"bold\">{title}</span>";
                    ((CellRendererText)cell).Markup = string.IsNullOrEmpty(metaLine)
                        ? titleMarkup
                        : $"{titleMarkup}\n<span size=\"{MetaSubLineSize}\" alpha=\"{MetaSubLineAlpha}\">{metaLine}</span>";
                }
            }));
        }

        // Right-aligned card metadata for finished downloads (Size on top, Date on bottom)
        private void SetFinishedMetaColumn(TreeViewColumn column, CellRendererText renderer, TreeView view)
        {
            column.SetCellDataFunc(renderer, new CellLayoutDataFunc((_, cell, model, iter) =>
            {
                var path = model.GetPath(iter);
                var selected = view.Selection.PathIsSelected(path);
                var isHovered = !selected && hoveredFinishedPath != null && hoveredFinishedPath.Compare(path) == 0;
                var isAlternate = !selected && !isHovered && path.Indices != null && path.Indices.Length > 0 && (path.Indices[0] % 2 == 1);

                ((CellRendererText)cell).CellBackground = TreeViewSelectionHelper.RowCellBackground(isHovered, isAlternate, ThemeManager.ActiveAlternateRowColor);

                var sizeText = model.GetValue(iter, 2) as string ?? string.Empty;
                var dateText = model.GetValue(iter, 1) as string ?? string.Empty;

                var isDark = ThemeManager.IsDarkActive;
                var textCol = isDark ? "#ffffff" : "#0f172a";
                var dateCol = isDark ? "#ffffff" : "#475569";
                // Date matches the meta sub-line treatment: smaller size, faded alpha
                var dateAlpha = isDark ? $"{MetaSubLineAlphaDark}" : $"{MetaSubLineAlpha}";

                var sizeMarkup = selected
                    ? $"<span weight=\"bold\" color=\"{textCol}\">{GLib.Markup.EscapeText(sizeText)}</span>"
                    : $"<span weight=\"bold\">{GLib.Markup.EscapeText(sizeText)}</span>";
                var dateMarkup = selected
                    ? $"<span size=\"7500\" alpha=\"{dateAlpha}\" color=\"{dateCol}\">{GLib.Markup.EscapeText(dateText)}</span>"
                    : $"<span size=\"7500\" alpha=\"{MetaSubLineAlpha}\">{GLib.Markup.EscapeText(dateText)}</span>";

                ((CellRendererText)cell).Markup = $"{sizeMarkup}\n{dateMarkup}";
            }));
        }

        // Right-aligned card metadata for in-progress downloads (Progress % on top, Live Status/Speed on bottom)
        private void SetInProgressMetaColumn(TreeViewColumn column, CellRendererText renderer, TreeView view)
        {
            column.SetCellDataFunc(renderer, new CellLayoutDataFunc((_, cell, model, iter) =>
            {
                var path = model.GetPath(iter);
                var selected = view.Selection.PathIsSelected(path);
                var isHovered = !selected && hoveredInprogressPath != null && hoveredInprogressPath.Compare(path) == 0;
                var isAlternate = !selected && !isHovered && path.Indices != null && path.Indices.Length > 0 && (path.Indices[0] % 2 == 1);

                ((CellRendererText)cell).CellBackground = TreeViewSelectionHelper.RowCellBackground(isHovered, isAlternate, ThemeManager.ActiveAlternateRowColor);

                var item = model.GetValue(iter, INPROGRESS_DATA_INDEX) as InProgressDownloadItem;
                var sizeText = model.GetValue(iter, 2) as string ?? string.Empty;
                var progress = item?.Progress ?? 0;
                var statusText = model.GetValue(iter, 4) as string ?? string.Empty;

                var isDark = ThemeManager.IsDarkActive;
                var metaTextCol = isDark ? "#ffffff" : "#0f172a";
                var speedCol = isDark ? "#ffffff" : "#0284c7";
                var subAlpha = isDark ? "55000" : $"{SecondaryTextAlpha}";

                var line1 = selected
                    ? $"<span weight=\"bold\" color=\"{metaTextCol}\">{progress}%</span>  <span size=\"9000\" alpha=\"{subAlpha}\" color=\"{metaTextCol}\">({GLib.Markup.EscapeText(sizeText)})</span>"
                    : $"<span weight=\"bold\">{progress}%</span>  <span size=\"9000\" alpha=\"{SecondaryTextAlpha}\">({GLib.Markup.EscapeText(sizeText)})</span>";
                var line2 = selected
                    ? $"<span size=\"9000\" alpha=\"{subAlpha}\" color=\"{speedCol}\">{GLib.Markup.EscapeText(statusText)}</span>"
                    : $"<span size=\"9000\" color=\"#38bdf8\">{GLib.Markup.EscapeText(statusText)}</span>";

                ((CellRendererText)cell).Markup = $"{line1}\n{line2}";
            }));
        }

        // Category-tinted 28px file icon with white selection tint
        void GetFileIcon(ICellLayout cell_layout,
                CellRenderer cell, ITreeModel tree_model, TreeIter iter)
        {
            var name = (string)tree_model.GetValue(iter, 0);
            var (r, g, b) = GetCategoryColorForFileType(name);
            var svgName = IconResource.GetSVGNameForFileType(name);
            var rawPix = LoadSvg(svgName, DownloadIconSize);
            var path = tree_model.GetPath(iter);
            var view = (cell_layout as TreeViewColumn)?.TreeView as TreeView;
            var isSelected = view != null && view.Selection.PathIsSelected(path);
            var isHovered = !isSelected && ((view == lvFinished && hoveredFinishedPath != null && hoveredFinishedPath.Compare(path) == 0) ||
                                            (view == lvInprogress && hoveredInprogressPath != null && hoveredInprogressPath.Compare(path) == 0));
            var isAlternate = !isSelected && !isHovered && path.Indices != null && path.Indices.Length > 0 && (path.Indices[0] % 2 == 1);

            ((CellRendererPixbuf)cell).CellBackground = TreeViewSelectionHelper.RowCellBackground(isHovered, isAlternate, ThemeManager.ActiveAlternateRowColor);

            if (rawPix != null)
            {
                var isDark = ThemeManager.IsDarkActive;
                ((CellRendererPixbuf)cell).Pixbuf = (isSelected && isDark)
                    ? GtkHelper.TintPixbuf(rawPix, 255, 255, 255)
                    : GtkHelper.TintPixbuf(rawPix, r, g, b);
            }
        }
    }
}
