// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 main window presentation: header bar, category sidebar, and status bar.
// Kept apart from MainWindow.cs so window behaviour and window chrome stay readable.
using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.BrowserMonitoring;
using XDM.Core.UI;
using XDM.Core.Util;
using XDM.Gtk4UI.Utils;
using GtkApplication = Gtk.Application;

namespace XDM.Gtk4UI
{
    internal sealed partial class MainWindow
    {
        // Sidebar lists, kept so the two sections stay mutually exclusive
        private ListBox? statusList;
        private ListBox? categoryList;
        // Guards cross-list selection syncing
        private bool syncingSidebar;
        // Header title/subtitle, updated as the sidebar view changes
        private Adw.WindowTitle? windowTitle;
        // Sidebar rows, kept so icons can turn white while their row is selected
        private readonly List<SidebarEntry> sidebarEntries = new();
        // Sidebar row labels and their base captions, for live counts
        private readonly Dictionary<string, Label> sidebarLabels = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> sidebarBaseText = new(StringComparer.Ordinal);

        // Packs the app title, core actions, search, theme toggle and application menu
        private Widget BuildHeaderBar()
        {
            // AdwHeaderBar draws GNOME's window controls; the title keeps the app mark
            var header = Adw.HeaderBar.New();
            // GNOME layouts with an "icon" element (e.g. icon:minimize,maximize,close)
            // draw the app mark at the start of the header, duplicating our title logo.
            // Hide the start buttons only when that side carries no real window control.
            header.ShowStartTitleButtons = StartSideHasWindowControls();
            windowTitle = Adw.WindowTitle.New(AppInfo.APP_NAME,
                TextResource.GetText("ALL_UNFINISHED") ?? "Active");
            var titleBox = Box.New(Orientation.Horizontal, 10);
            titleBox.Valign = Align.Center;
            titleBox.Append(Gtk4IconHelper.CreateIcon("fetchflow-logo", 28));
            titleBox.Append(windowTitle);
            header.SetTitleWidget(titleBox);

            // Transfer actions stay in the header; Open/Open Folder live in the menu
            header.PackStart(NewButton);
            header.PackStart(PauseButton);
            header.PackStart(ResumeButton);
            header.PackStart(DeleteButton);

            searchEntry.PlaceholderText = TextResource.GetText("LBL_SEARCH") ?? "Search";
            searchEntry.TooltipText = "Search downloads (Ctrl+F)";
            // Search filters the list in place; it never rebuilds the view from scratch
            searchEntry.OnSearchChanged += (_, _) => downloadList.Filter(searchEntry.GetText() ?? "");

            updateDot.TooltipText = TextResource.GetText("MSG_UPDATE_AVAILABLE") ?? "Update available";

            var menuButton = MenuButton.New();
            menuButton.IconName = "open-menu-symbolic";
            menuButton.TooltipText = TextResource.GetText("LBL_MENU") ?? "Main Menu";
            menuButton.SetMenuModel(BuildMainMenu());

            // pack_end fills right-to-left: add the rightmost control first
            header.PackEnd(updateDot);
            header.PackEnd(menuButton);
            header.PackEnd(searchToggle);

            return header;
        }

        // True when the user's decoration layout puts a real control on the start side
        private static bool StartSideHasWindowControls()
        {
            try
            {
                var layout = Settings.GetDefault()?.GtkDecorationLayout;
                if (string.IsNullOrEmpty(layout))
                {
                    return true;
                }
                var start = layout.Split(':')[0];
                return start.Contains("close", StringComparison.Ordinal)
                    || start.Contains("minimize", StringComparison.Ordinal)
                    || start.Contains("maximize", StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Decoration layout read failed");
                return true;
            }
        }

        // Sort options for the bottom-bar menu
        private static Gio.MenuModel BuildSortMenu()
        {
            var menu = Gio.Menu.New();
            menu.Append(TextResource.GetText("SORT_NAME_ASC") ?? "Name (A-Z)", "app." + AppActions.SortNameAsc);
            menu.Append(TextResource.GetText("SORT_NAME_DESC") ?? "Name (Z-A)", "app." + AppActions.SortNameDesc);
            menu.Append(TextResource.GetText("SORT_SIZE_ASC") ?? "Size (smallest first)", "app." + AppActions.SortSizeAsc);
            menu.Append(TextResource.GetText("SORT_SIZE_DESC") ?? "Size (largest first)", "app." + AppActions.SortSizeDesc);
            menu.Append(TextResource.GetText("SORT_DATE_DESC") ?? "Date (newest first)", "app." + AppActions.SortDateDesc);
            menu.Append(TextResource.GetText("SORT_DATE_ASC") ?? "Date (oldest first)", "app." + AppActions.SortDateAsc);
            return menu;
        }

        // Builds the primary menu, grouped the way GNOME header menus are
        private Gio.MenuModel BuildMainMenu()
        {
            var root = Gio.Menu.New();

            var downloads = Gio.Menu.New();
            downloads.Append(TextResource.GetText("MENU_BATCH_DOWNLOAD") ?? "Batch Download...", "app." + AppActions.BatchDownload);
            downloads.Append(TextResource.GetText("LBL_VIDEO_DOWNLOAD") ?? "Video Downloader...", "app." + AppActions.VideoDownload);
            downloads.Append(TextResource.GetText("MSG_MEDIA_CAPTURE") ?? "Media Grabber", "app." + AppActions.MediaGrabber);
            downloads.Append(TextResource.GetText("DESC_Q_TITLE") ?? "Queue & Scheduler...", "app." + AppActions.Scheduler);
            downloads.Append(TextResource.GetText("MENU_SPEED_LIMITER") ?? "Speed Limiter...", "app." + AppActions.SpeedLimiter);
            downloads.Append(TextResource.GetText("CTX_CHECKSUM") ?? "Calculate Checksum...", "app." + AppActions.VerifyChecksum);
            downloads.Append(TextResource.GetText("MENU_IMPORT_EXPORT") ?? "Import / Export...", "app." + AppActions.ImportExport);
            downloads.Append(TextResource.GetText("MENU_DELETE_COMPLETED") ?? "Remove Finished Downloads", "app." + AppActions.RemoveFinished);
            root.AppendSection(null, downloads);

            var help = Gio.Menu.New();
            help.Append(TextResource.GetText("MENU_CHECK_UPDATE") ?? "Check for Update", "app." + AppActions.CheckForUpdate);
            help.Append(TextResource.GetText("LBL_SUPPORT_PAGE") ?? "Help and Support", "app." + AppActions.HelpSupport);
            help.Append(TextResource.GetText("LBL_REPORT_PROBLEM") ?? "Report a Problem", "app." + AppActions.ReportProblem);
            root.AppendSection(null, help);

            var appSection = Gio.Menu.New();
            appSection.Append(TextResource.GetText("CTX_OPEN_FILE") ?? "Open File", "app." + AppActions.OpenFile);
            appSection.Append(TextResource.GetText("CTX_OPEN_FOLDER") ?? "Open Folder", "app." + AppActions.OpenFolder);
            appSection.Append(TextResource.GetText("MENU_TOGGLE_THEME") ?? "Switch Theme", "app." + AppActions.ToggleTheme);
            appSection.Append(TextResource.GetText("MENU_LANG") ?? "Language", "app." + AppActions.Language);
            appSection.Append(TextResource.GetText("TITLE_SETTINGS") ?? "Settings...", "app." + AppActions.Settings);
            appSection.Append(TextResource.GetText("MENU_ABOUT") ?? "About FetchFlow", "app." + AppActions.About);
            appSection.Append(TextResource.GetText("MENU_EXIT") ?? "Exit", "app." + AppActions.Quit);
            root.AppendSection(null, appSection);

            return root;
        }

        // Builds the resizable category pane and status bar
        private Widget BuildBody()
        {
            var paned = Paned.New(Orientation.Horizontal);
            paned.StartChild = BuildSidebar();
            paned.EndChild = downloadList.Root;
            paned.Position = SidebarWidth;
            paned.ResizeStartChild = false;
            paned.ShrinkStartChild = false;

            var root = Box.New(Orientation.Vertical, 0);
            root.Append(searchBar);
            root.Append(paned);
            root.Append(BuildStatusBar());
            return root;
        }

        // Creates the status card plus the user's configured download categories
        private Widget BuildSidebar()
        {
            // Active / Complete sit in their own card, matching the GTK 3 sidebar
            statusList = ListBox.New();
            // Native GNOME sidebar rows + a grouped card for the Active/Complete block
            statusList.AddCssClass("navigation-sidebar");
            statusList.SelectionMode = SelectionMode.Browse;
            statusList.Append(BuildSidebarRow(TextResource.GetText("ALL_UNFINISHED") ?? "Active", InProgressKey, "arrow-down-line", 53, 132, 228));
            statusList.Append(BuildSidebarRow(TextResource.GetText("ALL_FINISHED") ?? "Complete", FinishedKey, "check-line", 53, 132, 228));
            statusList.OnRowSelected += (_, args) => OnSidebarRowSelected(args, statusList, true);
            // Preselect Active so the sidebar always shows where the list is pointing
            if (statusList.GetRowAtIndex(0) is { } activeRow)
            {
                statusList.SelectRow(activeRow);
            }

            var statusCard = Box.New(Orientation.Vertical, 0);
            statusCard.AddCssClass("sidebar-status-card");
            statusCard.Append(statusList);

            categoryList = ListBox.New();
            categoryList.AddCssClass("navigation-sidebar");
            categoryList.SelectionMode = SelectionMode.Browse;
            foreach (var category in Config.Instance.Categories)
            {
                categories[category.Name] = category;
                var (iconName, r, g, b) = CategoryIconPalette.Resolve(category);
                categoryList.Append(BuildSidebarRow(category.DisplayName, category.Name, iconName, r, g, b));
            }
            categoryList.OnRowSelected += (_, args) => OnSidebarRowSelected(args, categoryList, false);

            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            scrolled.SetChild(categoryList);
            scrolled.Vexpand = true;

            var sidebar = Box.New(Orientation.Vertical, 0);
            sidebar.Append(statusCard);
            sidebar.Append(BuildSidebarHeading());
            sidebar.Append(scrolled);
            sidebar.WidthRequest = SidebarWidth;
            UpdateSidebarIcons();
            return sidebar;
        }

        // Keeps every control in tune with the active colour scheme
        private void UpdateBottomBarIcons()
        {
            var (ar, ag, ab) = AccentForState();
            TintButtonIcon(btnSelectAll, "checkbox-line", ar, ag, ab, 14);
            TintButtonIcon(btnScheduler, "list-settings-line", ar, ag, ab, 14);
            TintButtonIcon(btnSpeedLimit, "time-line", ar, ag, ab, 14);
        }

        // Active accent from the shared scheme table, matching Gtk4Theme.ApplyColorScheme
        private static (byte R, byte G, byte B) AccentForState()
        {
            try
            {
                var index = ColorSchemeTable.ClampSchemeIndex(Gtk4Theme.IsDarkActive, Config.Instance.ColorScheme);
                var scheme = ColorSchemeTable.GetScheme(Gtk4Theme.IsDarkActive, index);
                return Gtk4IconHelper.ParseHex(scheme.AccentHex);
            }
            catch
            {
                return (53, 132, 228);
            }
        }

        // Keeps sidebar icons legible: white on the accent row, own colour otherwise
        private void UpdateSidebarIcons()
        {
            foreach (var entry in sidebarEntries)
            {
                var selected = entry.Row.IsSelected();
                Gtk4IconHelper.UpdateTintedIcon(entry.Icon, entry.IconName,
                    selected ? (byte)255 : entry.R,
                    selected ? (byte)255 : entry.G,
                    selected ? (byte)255 : entry.B, 18);
            }
        }

        // Appends live row counts to the Active/Complete and category rows
        private void UpdateSidebarCounts()
        {
            SetSidebarCount(InProgressKey, allRows.Count(row => !row.IsComplete));
            SetSidebarCount(FinishedKey, allRows.Count(row => row.IsComplete));

            foreach (var pair in categories)
            {
                var category = pair.Value;
                SetSidebarCount(pair.Key,
                    allRows.Count(row => Helpers.IsOfCategoryOrMatchesKeyword(row.Name, null, category)));
            }
        }

        // Shows "Label (n)" when a view has rows, otherwise the plain caption
        private void SetSidebarCount(string key, int count)
        {
            if (!sidebarLabels.TryGetValue(key, out var label) || !sidebarBaseText.TryGetValue(key, out var text))
            {
                return;
            }
            label.SetText(count > 0 ? $"{text}  ({count})" : text);
        }

        // Small section heading above the category list
        private static Widget BuildSidebarHeading()
        {
            var label = Label.New(TextResource.GetText("SETTINGS_CAT") ?? "Categories");
            label.Halign = Align.Start;
            label.MarginStart = 16;
            label.MarginTop = 8;
            label.MarginBottom = 4;
            return label;
        }

        // Applies a sidebar selection and clears the other section
        private void OnSidebarRowSelected(ListBox.RowSelectedSignalArgs args, ListBox source, bool isStatusSection)
        {
            if (syncingSidebar)
            {
                return;
            }
            if (args.Row is not ListBoxRow row || string.IsNullOrEmpty(row.Name))
            {
                return;
            }

            syncingSidebar = true;
            try
            {
                if (isStatusSection)
                {
                    categoryList?.UnselectAll();
                }
                else
                {
                    statusList?.UnselectAll();
                }
            }
            finally
            {
                syncingSidebar = false;
            }

            currentView = row.Name;
            windowTitle?.SetSubtitle(row.TooltipText ?? AppInfo.APP_NAME);
            UpdateSidebarIcons();
            ApplyView(currentView);
            ViewChanged?.Invoke(currentView);
        }

        // Moves the sidebar highlight when the core switches the view programmatically
        private void SelectSidebarRow(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var inStatus = FindRow(statusList, key);
            var row = inStatus ?? FindRow(categoryList, key);
            if (row == null)
            {
                return;
            }

            syncingSidebar = true;
            try
            {
                if (inStatus != null)
                {
                    categoryList?.UnselectAll();
                }
                else
                {
                    statusList?.UnselectAll();
                }
                (inStatus != null ? statusList : categoryList)?.SelectRow(row);
            }
            finally
            {
                syncingSidebar = false;
            }

            windowTitle?.SetSubtitle(row.TooltipText ?? AppInfo.APP_NAME);
            UpdateSidebarIcons();
        }

        // Finds a sidebar row by its view key
        private static ListBoxRow? FindRow(ListBox? list, string key)
        {
            if (list == null)
            {
                return null;
            }

            for (var child = list.GetFirstChild(); child != null; child = child.GetNextSibling())
            {
                if (child is ListBoxRow row && row.Name == key)
                {
                    return row;
                }
            }
            return null;
        }

        // One sidebar row plus the icon it owns, for selection-driven tinting
        private sealed class SidebarEntry
        {
            public ListBoxRow Row { get; }
            public Image Icon { get; }
            public string IconName { get; }
            public byte R { get; }
            public byte G { get; }
            public byte B { get; }

            public SidebarEntry(ListBoxRow row, Image icon, string iconName, byte r, byte g, byte b)
            {
                Row = row;
                Icon = icon;
                IconName = iconName;
                R = r;
                G = g;
                B = b;
            }
        }

        // First image inside a widget subtree (buttons wrap their icons in a box)
        private static Image? FirstImage(Widget widget)
        {
            if (widget is Image image)
            {
                return image;
            }
            for (var child = widget.GetFirstChild(); child != null; child = child.GetNextSibling())
            {
                var found = FirstImage(child);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        // Applies one RGB tone to a button's icon widget
        private static void TintButtonIcon(Button button, string iconName, byte r, byte g, byte b, int size)
        {
            var image = FirstImage(button);
            if (image != null)
            {
                Gtk4IconHelper.UpdateTintedIcon(image, iconName, r, g, b, size);
            }
        }

        // Creates one accessible sidebar row with tinted category icon and text
        private ListBoxRow BuildSidebarRow(string text, string key, string iconName, byte r, byte g, byte b)
        {
            var rowBox = Box.New(Orientation.Horizontal, 10);
            rowBox.MarginTop = 6;
            rowBox.MarginBottom = 6;
            rowBox.MarginStart = 8;
            rowBox.MarginEnd = 8;

            var icon = Gtk4IconHelper.CreateTintedIcon(iconName, r, g, b, 18);
            icon.Valign = Align.Center;

            var label = Label.New(text);
            label.Halign = Align.Start;
            label.Valign = Align.Center;
            label.Hexpand = true;
            sidebarLabels[key] = label;
            sidebarBaseText[key] = text;

            rowBox.Append(icon);
            rowBox.Append(label);

            var row = ListBoxRow.New();
            row.SetChild(rowBox);
            row.Name = key;
            row.TooltipText = text;
            sidebarEntries.Add(new SidebarEntry(row, icon, iconName, r, g, b));
            return row;
        }

        // Builds the status and action bar at the bottom of the window
        private Widget BuildStatusBar()
        {
            var bar = Box.New(Orientation.Horizontal, 10);
            bar.AddCssClass("bottom-bar");

            bar.Append(btnSelectAll);
            bar.Append(lblSelectionCount);
            bar.Append(btnMonitoring);
            bar.Append(lblExtensionStatus);

            // Flexible spacer keeps the trailing buttons pinned to the right edge
            var spacer = Box.New(Orientation.Horizontal, 0);
            spacer.Hexpand = true;
            bar.Append(spacer);

            UpdateBottomBarIcons();
            TintButtonIcon(btnSelectAll, "checkbox-line", 53, 132, 228, 14);
            TintButtonIcon(btnScheduler, "list-settings-line", 53, 132, 228, 14);
            TintButtonIcon(btnSpeedLimit, "time-line", 53, 132, 228, 14);

            bar.Append(btnSort);
            bar.Append(btnSpeedLimit);
            bar.Append(btnScheduler);

            return bar;
        }

        // Updates the extension connectivity badge from the live IPC state
        private void UpdateExtensionStatus()
        {
            try
            {
                var count = IpcHttpMessageProcessor.ActiveWebSocketSessionsCount;
                var isConnected = IpcHttpMessageProcessor.IsConnected;

                SetExtensionStateClass(count > 0 ? "ext-active" : isConnected ? "ext-ready" : "ext-idle");

                if (count > 0)
                {
                    var active = TextResource.GetText("MSG_EXT_ACTIVE") ?? "Extension Active";
                    lblExtensionStatus.SetMarkup($"<span size=\"small\">● {active} ({count})</span>");
                    lblExtensionStatus.TooltipText = $"Browser extension connected via WebSocket\nIPC port: {IpcHttpMessageProcessor.EffectivePort}\nActive sessions: {count}";
                }
                else if (isConnected)
                {
                    var ready = TextResource.GetText("MSG_EXT_READY") ?? "Extension Ready";
                    lblExtensionStatus.SetMarkup($"<span size=\"small\">● {ready}</span>");
                    lblExtensionStatus.TooltipText = $"Browser extension connected via HTTP relay\nIPC port: {IpcHttpMessageProcessor.EffectivePort}";
                }
                else
                {
                    var listening = TextResource.GetText("MSG_EXT_LISTENING") ?? "Extension Listening";
                    lblExtensionStatus.SetMarkup($"<span size=\"small\">○ {listening}</span>");
                    lblExtensionStatus.TooltipText = $"FetchFlow IPC server listening on 127.0.0.1:{IpcHttpMessageProcessor.EffectivePort}";
                }
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Extension status refresh failed: " + ex.Message);
            }
        }

        // Applies exactly one connectivity style class to the extension badge
        private void SetExtensionStateClass(string cssClass)
        {
            foreach (var name in new[] { "ext-active", "ext-ready", "ext-idle" })
            {
                lblExtensionStatus.RemoveCssClass(name);
            }
            lblExtensionStatus.AddCssClass(cssClass);
        }

        // Reflects the bandwidth limiter state on its bottom-bar button
        private void UpdateSpeedLimitButtonState()
        {
            var enabled = Config.Instance.EnableSpeedLimit && Config.Instance.DefaltDownloadSpeed > 0;
            if (enabled)
            {
                var limit = FormattingHelper.FormatSize(Config.Instance.DefaltDownloadSpeed * 1024.0) + "/s";
                btnSpeedLimit.TooltipText = $"Speed Limiter: {limit} (click to change)";
            }
            else
            {
                btnSpeedLimit.TooltipText = "Speed Limiter: Off (click to change)";
            }

            if (enabled)
            {
                btnSpeedLimit.AddCssClass("speed-limit-enabled");
            }
            else
            {
                btnSpeedLimit.RemoveCssClass("speed-limit-enabled");
            }
        }
    }
}
