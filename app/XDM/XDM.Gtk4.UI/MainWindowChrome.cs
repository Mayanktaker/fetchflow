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
        // Content headline: current view name, beside its row/selection count
        private Label? viewTitleLabel;
        // Contextual header actions, shown only while rows are selected
        private Box? rowActionsBox;
        // Live transfer pulse beside the window title, visible while work is running
        private readonly Label titlePulse = Label.New("●");
        // Footer activity summary (active count and aggregate speed); clicking it
        // opens the progress window
        private readonly Label lblActivity = Label.New("");
        private readonly Button btnActivity = Button.New();
        // Search debounce generation: stale timers exit without filtering
        private int searchGeneration;
        // Sidebar rows, kept so icons can turn white while their row is selected
        private readonly List<SidebarEntry> sidebarEntries = new();
        // Right-aligned live count labels, one per sidebar row
        private readonly Dictionary<string, Label> sidebarCounts = new(StringComparer.Ordinal);

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
                TextResource.GetText("LBL_DOWNLOADS") ?? "Downloads");
            var titleBox = Box.New(Orientation.Horizontal, 10);
            titleBox.Valign = Align.Center;
            titlePulse.AddCssClass("title-pulse");
            titlePulse.TooltipText = "Downloading";
            titlePulse.Visible = false;
            titleBox.Append(Gtk4IconHelper.CreateIcon("fetchflow-logo", 28));
            titleBox.Append(windowTitle);
            titleBox.Append(titlePulse);
            header.SetTitleWidget(titleBox);

            // Transfer actions stay in the header; Open/Open Folder live in the menu
            header.PackStart(NewButton);

            // Row actions read as one quiet group after the primary action; the
            // group only appears while rows are actually selected
            rowActionsBox = Box.New(Orientation.Horizontal, 2);
            rowActionsBox.Valign = Align.Center;
            rowActionsBox.Visible = false;
            rowActionsBox.Append(PauseButton);
            rowActionsBox.Append(ResumeButton);
            rowActionsBox.Append(OpenFileButton);
            rowActionsBox.Append(OpenFolderButton);
            rowActionsBox.Append(DeleteButton);
            header.PackStart(rowActionsBox);

            searchEntry.PlaceholderText = TextResource.GetText("LBL_SEARCH_HINT")
                ?? "Search files — try site: or folder:";
            searchEntry.TooltipText = "Search downloads (Ctrl+F)";
            // Search filters the list in place; it never rebuilds the view from scratch
            searchEntry.OnSearchChanged += (_, _) => ApplySearchDebounced();

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

        // Builds the primary menu, grouped the way GNOME header menus are.
        // Capture and management flows live in hover submenus so the top level
        // stays short; frequent one-shot actions stay flat.
        private Gio.MenuModel BuildMainMenu()
        {
            var root = Gio.Menu.New();

            var capture = Gio.Menu.New();
            capture.Append(TextResource.GetText("MENU_BATCH_DOWNLOAD") ?? "Batch Download...", "app." + AppActions.BatchDownload);
            capture.Append(TextResource.GetText("LBL_VIDEO_DOWNLOAD") ?? "Video Downloader...", "app." + AppActions.VideoDownload);
            capture.Append(TextResource.GetText("MSG_MEDIA_CAPTURE") ?? "Media Grabber", "app." + AppActions.MediaGrabber);
            root.AppendSubmenu(TextResource.GetText("LBL_DOWNLOADS") ?? "Downloads", capture);

            var manage = Gio.Menu.New();
            manage.Append(TextResource.GetText("DESC_Q_TITLE") ?? "Queue & Scheduler...", "app." + AppActions.Scheduler);
            manage.Append(TextResource.GetText("MENU_SPEED_LIMITER") ?? "Speed Limiter...", "app." + AppActions.SpeedLimiter);
            manage.Append(TextResource.GetText("CTX_CHECKSUM") ?? "Calculate Checksum...", "app." + AppActions.VerifyChecksum);
            manage.Append(TextResource.GetText("MENU_IMPORT_EXPORT") ?? "Import / Export...", "app." + AppActions.ImportExport);
            manage.Append(TextResource.GetText("MENU_DELETE_COMPLETED") ?? "Remove Finished Downloads", "app." + AppActions.RemoveFinished);
            root.AppendSubmenu(TextResource.GetText("MENU_GROUP_MANAGE") ?? "Manage", manage);

            var open = Gio.Menu.New();
            open.Append(TextResource.GetText("CTX_OPEN_FILE") ?? "Open File", "app." + AppActions.OpenFile);
            open.Append(TextResource.GetText("CTX_OPEN_FOLDER") ?? "Open Folder", "app." + AppActions.OpenFolder);
            root.AppendSection(null, open);

            var help = Gio.Menu.New();
            help.Append(TextResource.GetText("MENU_CHECK_UPDATE") ?? "Check for Update", "app." + AppActions.CheckForUpdate);
            help.Append(TextResource.GetText("LBL_SUPPORT_PAGE") ?? "Help and Support", "app." + AppActions.HelpSupport);
            help.Append(TextResource.GetText("LBL_REPORT_PROBLEM") ?? "Report a Problem", "app." + AppActions.ReportProblem);
            help.Append(TextResource.GetText("LBL_LICENSE") ?? "License", "app." + AppActions.License);
            root.AppendSubmenu(TextResource.GetText("MENU_GROUP_HELP") ?? "Help", help);

            var appSection = Gio.Menu.New();
            appSection.Append(TextResource.GetText("MENU_TOGGLE_THEME") ?? "Switch Theme", "app." + AppActions.ToggleTheme);
            appSection.Append(TextResource.GetText("MENU_LANG") ?? "Language", "app." + AppActions.Language);
            // Settings lives in the sidebar now, so the menu does not repeat it
            appSection.Append(TextResource.GetText("MENU_ABOUT") ?? "About FetchFlow", "app." + AppActions.About);
            appSection.Append(TextResource.GetText("MENU_EXIT") ?? "Exit", "app." + AppActions.Quit);
            root.AppendSection(null, appSection);

            return root;
        }

        // Settings entry pinned under the category list, as in the GNOME sidebar pattern
        private Widget BuildSidebarFooter()
        {
            var (r, g, b) = AccentForState();
            var button = Button.New();
            button.AddCssClass("flat");
            button.AddCssClass("sidebar-action");
            button.TooltipText = TextResource.GetText("TITLE_SETTINGS") ?? "Settings";

            var box = Box.New(Orientation.Horizontal, 10);
            box.MarginStart = 10;
            box.MarginEnd = 10;
            var icon = Gtk4IconHelper.CreateTintedIcon("settings-3-line", r, g, b, 18);
            icon.Valign = Align.Center;
            var label = Label.New(TextResource.GetText("TITLE_SETTINGS") ?? "Settings");
            label.Halign = Align.Start;
            label.Hexpand = true;
            box.Append(icon);
            box.Append(label);
            button.SetChild(box);
            button.OnClicked += (_, _) => OnSettings();

            var footer = Box.New(Orientation.Vertical, 0);
            footer.MarginTop = 8;
            footer.MarginBottom = 8;
            footer.Append(button);
            return footer;
        }

        // Applies the search keyword after a short pause, so typing does not rebuild
        // the list once per keystroke
        private void ApplySearchDebounced()
        {
            var generation = ++searchGeneration;
            GLib.Functions.TimeoutAdd(0, 180, () =>
            {
                if (generation != searchGeneration)
                {
                    return false;
                }
                downloadList.Filter(searchEntry.GetText() ?? "");
                UpdateViewHeading();
                return false;
            });
        }

        // Clears the pending search and applies an empty keyword at once
        private void ClearSearchImmediately()
        {
            searchGeneration++;
            downloadList.Filter("");
            UpdateViewHeading();
        }

        // Footer status: how many transfers run and how fast, in total
        private void UpdateActivitySummary()
        {
            try
            {
                var active = allRows.Where(row => !row.IsComplete).ToList();
                if (active.Count == 0)
                {
                    lblActivity.SetText(TextResource.GetText("LBL_IDLE") ?? "Idle");
                    btnActivity.TooltipText = TextResource.GetText("LBL_SHOW_PROGRESS") ?? "Show progress";
                    titlePulse.Visible = false;
                    windowTitle?.SetSubtitle(TextResource.GetText("LBL_DOWNLOADS") ?? "Downloads");
                    return;
                }

                var speed = active.Sum(row => row.SpeedBytesPerSecond);
                var queued = active.Count(row => (row.Status ?? "")
                    .Contains("Wait", StringComparison.OrdinalIgnoreCase));
                var running = active.Count - queued;
                var speedText = speed > 0 ? " · " + FormattingHelper.FormatSize(speed) + "/s" : "";
                var activeText = $"{active.Count} {TextResource.GetText("LBL_ACTIVE_COUNT") ?? "active"}";
                var queuedText = queued > 0
                    ? $" · {queued} {TextResource.GetText("LBL_QUEUED_COUNT") ?? "queued"}"
                    : "";

                lblActivity.SetText(activeText + queuedText + speedText);
                btnActivity.TooltipText = lblActivity.GetText() + " · " +
                    (TextResource.GetText("LBL_SHOW_PROGRESS") ?? "Show progress");

                // The title pulses only while a transfer is actually running, not
                // when everything left is merely queued
                titlePulse.Visible = running > 0;
                windowTitle?.SetSubtitle(activeText + speedText);
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Activity summary failed: " + ex.Message);
                lblActivity.SetText("");
            }
        }

        // Shows the header action group only when one action can actually run
        private void UpdateRowActionsVisibility(int selectedCount)
        {
            if (rowActionsBox == null)
            {
                return;
            }

            rowActionsBox.Visible = selectedCount > 0 &&
                (PauseButton.Sensitive || ResumeButton.Sensitive ||
                 OpenFileButton.Sensitive || OpenFolderButton.Sensitive || DeleteButton.Sensitive);
        }

        // Builds the resizable category pane, the content headline and the status footer
        private Widget BuildBody()
        {
            var content = Box.New(Orientation.Vertical, 0);
            content.Append(BuildContentToolbar());
            content.Append(searchBar);
            content.Append(downloadList.Root);

            var paned = Paned.New(Orientation.Horizontal);
            paned.StartChild = BuildSidebar();
            paned.EndChild = content;
            paned.Position = SidebarWidth;
            paned.ResizeStartChild = false;
            paned.ShrinkStartChild = false;

            var root = Box.New(Orientation.Vertical, 0);
            root.Append(paned);
            root.Append(BuildStatusBar());
            return root;
        }

        // Headline above the list: view name, count, select-all and sort
        private Widget BuildContentToolbar()
        {
            var bar = Box.New(Orientation.Horizontal, 8);
            bar.AddCssClass("content-toolbar");

            viewTitleLabel = Label.New("");
            viewTitleLabel.AddCssClass("view-title");
            viewTitleLabel.Halign = Align.Start;
            viewTitleLabel.Xalign = 0;
            viewTitleLabel.Valign = Align.Center;
            bar.Append(viewTitleLabel);

            lblSelectionCount.AddCssClass("view-count");
            lblSelectionCount.Valign = Align.Center;
            bar.Append(lblSelectionCount);

            var spacer = Box.New(Orientation.Horizontal, 0);
            spacer.Hexpand = true;
            bar.Append(spacer);

            btnSelectAll.Valign = Align.Center;
            btnSelectAll.AddCssClass("caption");
            btnSort.Valign = Align.Center;
            btnSort.AddCssClass("caption");
            bar.Append(btnSelectAll);
            bar.Append(btnSort);
            return bar;
        }

        // Creates the status card plus the user's configured download categories
        private Widget BuildSidebar()
        {
            // Active / Complete lead the sidebar, matching the GTK 3 sections
            statusList = ListBox.New();
            statusList.AddCssClass("navigation-sidebar");
            statusList.MarginStart = 8;
            statusList.MarginEnd = 8;
            statusList.SelectionMode = SelectionMode.Browse;
            statusList.Append(BuildSidebarRow(TextResource.GetText("ALL_UNFINISHED") ?? "Active", InProgressKey, "arrow-down-line", 53, 132, 228));
            statusList.Append(BuildSidebarRow(TextResource.GetText("ALL_FINISHED") ?? "Complete", FinishedKey, "check-line", 53, 132, 228));
            statusList.OnRowSelected += (_, args) => OnSidebarRowSelected(args, statusList, true);
            // Preselect Active so the sidebar always shows where the list is pointing
            if (statusList.GetRowAtIndex(0) is { } activeRow)
            {
                statusList.SelectRow(activeRow);
            }

            categoryList = ListBox.New();
            categoryList.AddCssClass("navigation-sidebar");
            categoryList.SelectionMode = SelectionMode.Browse;
            foreach (var category in Config.Instance.Categories)
            {
                categories[category.Name] = category;
                var (iconName, r, g, b) = CategoryIconPalette.Resolve(category);
                // Category tints are tuned for light surfaces; lift them on dark
                if (Gtk4Theme.IsDarkActive)
                {
                    (r, g, b) = Gtk4IconHelper.BrightenForDark(r, g, b);
                }
                categoryList.Append(BuildSidebarRow(category.DisplayName, category.Name, iconName, r, g, b));
            }
            categoryList.OnRowSelected += (_, args) => OnSidebarRowSelected(args, categoryList, false);

            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            scrolled.MarginStart = 8;
            scrolled.MarginEnd = 8;
            scrolled.SetChild(categoryList);
            scrolled.Vexpand = true;

            var sidebar = Box.New(Orientation.Vertical, 0);
            sidebar.AddCssClass("app-sidebar");
            sidebar.Append(BuildSidebarHeading("LBL_DOWNLOADS", "Downloads"));
            sidebar.Append(statusList);
            sidebar.Append(BuildSidebarHeading("LBL_CATEGORIES", "Categories"));
            sidebar.Append(scrolled);
            sidebar.Append(BuildSidebarFooter());
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
        private static (byte R, byte G, byte B) AccentForState() => Gtk4IconHelper.CurrentAccent();

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

        // Shows the count beside a view caption, or nothing when the view is empty
        private void SetSidebarCount(string key, int count)
        {
            if (sidebarCounts.TryGetValue(key, out var label))
            {
                label.SetText(count > 0 ? count.ToString() : "");
            }
        }

        // Refreshes the headline title and its count (rows, or the selection)
        private void UpdateViewHeading()
        {
            if (viewTitleLabel == null)
            {
                return;
            }

            viewTitleLabel.SetText(currentView switch
            {
                InProgressKey => TextResource.GetText("ALL_UNFINISHED") ?? "Active",
                FinishedKey => TextResource.GetText("ALL_FINISHED") ?? "Complete",
                _ => categories.TryGetValue(currentView, out var category) ? category.DisplayName : AppInfo.APP_NAME
            });

            var selected = downloadList.GetSelectedItems().Count;
            lblSelectionCount.SetText(selected > 0
                ? $"{selected} {TextResource.GetText("LBL_SELECTED") ?? "selected"}"
                : $"{downloadList.Count} {TextResource.GetText("LBL_ITEMS") ?? "items"}");
        }

        // Small section heading above a sidebar group
        private static Widget BuildSidebarHeading(string key, string fallback)
        {
            var label = Label.New(TextResource.GetText(key) ?? fallback);
            label.AddCssClass("sidebar-heading");
            label.Halign = Align.Start;
            label.Xalign = 0;
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

        // Creates one accessible sidebar row with tinted icon, caption and count
        private ListBoxRow BuildSidebarRow(string text, string key, string iconName, byte r, byte g, byte b)
        {
            var rowBox = Box.New(Orientation.Horizontal, 10);
            rowBox.MarginTop = 6;
            rowBox.MarginBottom = 6;
            rowBox.MarginStart = 10;
            rowBox.MarginEnd = 10;

            var icon = Gtk4IconHelper.CreateTintedIcon(iconName, r, g, b, 18);
            icon.Valign = Align.Center;

            var label = Label.New(text);
            label.Halign = Align.Start;
            label.Valign = Align.Center;
            label.Hexpand = true;
            label.Ellipsize = Pango.EllipsizeMode.End;

            var count = Label.New("");
            count.AddCssClass("sidebar-count");
            count.Valign = Align.Center;
            sidebarCounts[key] = count;

            rowBox.Append(icon);
            rowBox.Append(label);
            rowBox.Append(count);

            var row = ListBoxRow.New();
            row.SetChild(rowBox);
            row.Name = key;
            row.TooltipText = text;
            sidebarEntries.Add(new SidebarEntry(row, icon, iconName, r, g, b));
            return row;
        }

        // Slim status footer: connectivity on the left, quiet controls on the right
        private Widget BuildStatusBar()
        {
            var bar = Box.New(Orientation.Horizontal, 10);
            bar.AddCssClass("bottom-bar");

            lblActivity.AddCssClass("caption");
            lblActivity.AddCssClass("activity-label");
            lblActivity.Valign = Align.Center;
            btnActivity.AddCssClass("flat");
            btnActivity.AddCssClass("activity-button");
            btnActivity.Valign = Align.Center;
            btnActivity.SetChild(lblActivity);
            btnActivity.TooltipText = TextResource.GetText("LBL_SHOW_PROGRESS") ?? "Show progress";
            btnActivity.OnClicked += (_, _) => UIActions.ShowProgressWindow();
            bar.Append(btnActivity);
            bar.Append(lblExtensionStatus);

            // Flexible spacer keeps the trailing controls pinned to the right edge
            var spacer = Box.New(Orientation.Horizontal, 0);
            spacer.Hexpand = true;
            bar.Append(spacer);

            // Monitoring reads as a caption beside its switch, not a checkbox label
            var monitoring = Box.New(Orientation.Horizontal, 6);
            monitoring.Valign = Align.Center;
            btnMonitoring.Valign = Align.Center;
            var monitoringLabel = Label.New(TextResource.GetText("SETTINGS_MONITORING") ?? "Browser Monitoring");
            monitoringLabel.AddCssClass("caption");
            monitoringLabel.AddCssClass("dim-label");
            monitoringLabel.Valign = Align.Center;
            monitoring.Append(monitoringLabel);
            monitoring.Append(btnMonitoring);
            bar.Append(monitoring);

            bar.Append(btnSpeedLimit);
            bar.Append(btnScheduler);

            UpdateBottomBarIcons();
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
