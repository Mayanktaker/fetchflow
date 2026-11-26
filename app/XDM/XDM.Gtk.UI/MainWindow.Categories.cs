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
        private (string iconName, byte r, byte g, byte b) GetCategoryIconConfig(Category cat)
            // Pure decision table lives in Core so it is unit-testable without GTK.
            => CategoryIconPalette.Resolve(cat);

        private Widget CreateCategoryTree()
        {
            // Top status section (Active & Complete)
            statusTree = new TreeView()
            {
                HeadersVisible = false,
                ShowExpanders = false,
                LevelIndentation = 0,
                MarginStart = 4,
                MarginEnd = 4,
                MarginTop = 2,
                MarginBottom = 2
            };
            statusTree.StyleContext.AddClass("sidebar-status-list");

            var statusCols = new TreeViewColumn();
            var statusCellPix = new CellRendererPixbuf();
            statusCellPix.SetPadding(4, 7);
            statusCols.PackStart(statusCellPix, false);
            statusCols.AddAttribute(statusCellPix, "pixbuf", 0);
            statusCols.SetCellDataFunc(statusCellPix, new CellLayoutDataFunc((layout, cell, model, iter) =>
            {
                if (model.GetValue(iter, 0) is Gdk.Pixbuf icon)
                {
                    ((CellRendererPixbuf)cell).Pixbuf = statusTree.Selection.PathIsSelected(model.GetPath(iter))
                        ? selectedSidebarIcons.GetValue(icon, p => GtkHelper.TintPixbuf(p, 255, 255, 255))
                        : icon;
                }
            }));

            var statusCellText = new CellRendererText();
            statusCellText.SetPadding(2, 7);
            statusCols.PackStart(statusCellText, true);
            statusCols.AddAttribute(statusCellText, "text", 1);
            statusTree.AppendColumn(statusCols);

            statusTreeStore = new TreeStore(typeof(Gdk.Pixbuf), typeof(string));
            var rawActive = LoadSvg("arrow-down-line", 20);
            var rawFinished = LoadSvg("check-line", 20);
            var activeIcon = rawActive != null ? GtkHelper.TintPixbuf(rawActive, AccentR, AccentG, AccentB) : null;
            var finishedIcon = rawFinished != null ? GtkHelper.TintPixbuf(rawFinished, SuccessR, SuccessG, SuccessB) : null;

            statusTreeStore.AppendValues(activeIcon, TextResource.GetText("ALL_UNFINISHED"));
            statusTreeStore.AppendValues(finishedIcon, TextResource.GetText("ALL_FINISHED"));
            statusTree.Model = statusTreeStore;
            statusTree.Selection.Mode = SelectionMode.Single;
            statusTree.Selection.Changed += OnStatusChanged;

            // Separate card container for Active & Complete status section
            var statusCard = new EventBox();
            statusCard.StyleContext.AddClass("sidebar-status-card");
            statusCard.MarginStart = 8;
            statusCard.MarginEnd = 8;
            statusCard.MarginTop = 8;
            statusCard.MarginBottom = 8;
            statusCard.Add(statusTree);

            // Categories section collapsible header toggle
            var catHeaderBox = new EventBox();
            catHeaderBox.StyleContext.AddClass("sidebar-heading-box");
            var catTitle = TextResource.GetText("SETTINGS_CAT") ?? "Categories";
            bool isCategoriesExpanded = Config.Instance.CategoriesExpanded;
            var catHeaderLabel = new Label
            {
                Text = isCategoriesExpanded ? $"▾  {catTitle}" : $"▸  {catTitle}",
                Halign = Align.Start,
                MarginStart = 16,
                MarginTop = 8,
                MarginBottom = 4
            };
            catHeaderLabel.StyleContext.AddClass("sidebar-heading");
            catHeaderBox.Add(catHeaderLabel);

            catHeaderBox.ButtonPressEvent += (o, args) =>
            {
                if (args.Event.Button == 1)
                {
                    isCategoriesExpanded = !isCategoriesExpanded;
                    categoryTree.Visible = isCategoriesExpanded;
                    catHeaderLabel.Text = isCategoriesExpanded ? $"▾  {catTitle}" : $"▸  {catTitle}";
                    Config.Instance.CategoriesExpanded = isCategoriesExpanded;
                    Config.SaveConfig();
                }
            };

            // Categories TreeView
            categoryTree = new TreeView()
            {
                Visible = isCategoriesExpanded,
                HeadersVisible = false,
                ShowExpanders = false,
                LevelIndentation = 0,
                MarginStart = 8,
                MarginEnd = 8
            };
            categoryTree.StyleContext.AddClass("sidebar-list");

            var catCols = new TreeViewColumn();
            var catCellPix = new CellRendererPixbuf();
            catCellPix.SetPadding(4, 7);
            catCols.PackStart(catCellPix, false);
            catCols.AddAttribute(catCellPix, "pixbuf", 0);
            catCols.SetCellDataFunc(catCellPix, new CellLayoutDataFunc((layout, cell, model, iter) =>
            {
                if (model.GetValue(iter, 0) is Gdk.Pixbuf icon)
                {
                    ((CellRendererPixbuf)cell).Pixbuf = categoryTree.Selection.PathIsSelected(model.GetPath(iter))
                        ? selectedSidebarIcons.GetValue(icon, p => GtkHelper.TintPixbuf(p, 255, 255, 255))
                        : icon;
                }
            }));

            var catCellText = new CellRendererText();
            catCellText.SetPadding(2, 7);
            catCols.PackStart(catCellText, true);
            catCols.AddAttribute(catCellText, "text", 1);
            categoryTree.AppendColumn(catCols);

            categoryTreeStore = new TreeStore(typeof(Gdk.Pixbuf), typeof(string), typeof(Category));
            foreach (var cat in Config.Instance.Categories)
            {
                var (iconName, r, g, b) = GetCategoryIconConfig(cat);
                var rawIcon = LoadSvg(iconName, 20);
                var coloredIcon = rawIcon != null ? GtkHelper.TintPixbuf(rawIcon, r, g, b) : null;
                categoryTreeStore.AppendValues(coloredIcon, cat.DisplayName, cat);
            }
            categoryTree.Model = categoryTreeStore;
            categoryTree.Selection.Mode = SelectionMode.Single;
            categoryTree.Selection.Changed += OnCategoryChanged;
            
            categoryTree.ButtonPressEvent += (o, args) =>
            {
                if (args.Event.Button == 3)
                {
                    if (TreeViewSelectionHelper.TryGetRowAtEvent(categoryTree, args.Event.X, args.Event.Y, out TreePath path))
                    {
                        categoryTree.Selection.SelectPath(path);
                        if (categoryTree.Model.GetIter(out TreeIter iter, path))
                        {
                            var val = categoryTree.Model.GetValue(iter, 2);
                            if (val is Category cat)
                            {
                                ShowCategoryContextMenu(cat, args.Event);
                            }
                        }
                    }
                }
            };

            // Pack into a sidebar vertical box with a ScrolledWindow wrapper
            var vbSidebar = new VBox(false, 0);
            vbSidebar.PackStart(statusCard, false, false, 0);
            vbSidebar.PackStart(catHeaderBox, false, false, 0);
            vbSidebar.PackStart(categoryTree, true, true, 0);

            var scrolledWindow = new ScrolledWindow
            {
                OverlayScrolling = true,
                Margin = 6,
                MarginEnd = 0
            };
            scrolledWindow.StyleContext.AddClass("sidebar-scroll");
            scrolledWindow.ShadowType = ShadowType.In;
            scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            scrolledWindow.Add(vbSidebar);
            scrolledWindow.SetSizeRequest(196, 200);

            scrolledWindow.ShowAll();
            return scrolledWindow;
        }

        
        private void UpdateStatusListCounts()
        {
            if (this.statusTreeStore == null || this.inprogressDownloadsStore == null || this.finishedDownloadsStore == null)
                return;

            int activeCount = inprogressDownloadsStore.IterNChildren();
            int completeCount = finishedDownloadsStore.IterNChildren();

            if (this.statusTreeStore.GetIterFirst(out TreeIter iter))
            {
                var activeText = TextResource.GetText("ALL_UNFINISHED");
                if (activeCount > 0) activeText += $"  ({activeCount})";
                this.statusTreeStore.SetValue(iter, 1, activeText);

                if (this.statusTreeStore.IterNext(ref iter))
                {
                    var completeText = TextResource.GetText("ALL_FINISHED");
                    if (completeCount > 0) completeText += $"  ({completeCount})";
                    this.statusTreeStore.SetValue(iter, 1, completeText);
                }
            }

            // Update per-category download count badges in the sidebar
            if (this.categoryTreeStore != null && this.categoryTreeStore.GetIterFirst(out TreeIter catIter))
            {
                var names = new List<string>(activeCount + completeCount);
                if (inprogressDownloadsStore.GetIterFirst(out TreeIter inIter))
                {
                    do
                    {
                        if (inprogressDownloadsStore.GetValue(inIter, 0) is string n && !string.IsNullOrEmpty(n))
                            names.Add(n);
                    } while (inprogressDownloadsStore.IterNext(ref inIter));
                }
                if (finishedDownloadsStore.GetIterFirst(out TreeIter finIter))
                {
                    do
                    {
                        if (finishedDownloadsStore.GetValue(finIter, 0) is string n && !string.IsNullOrEmpty(n))
                            names.Add(n);
                    } while (finishedDownloadsStore.IterNext(ref finIter));
                }

                do
                {
                    if (categoryTreeStore.GetValue(catIter, 2) is Category cat)
                    {
                        int count = 0;
                        for (int i = 0; i < names.Count; i++)
                        {
                            if (Helpers.IsOfCategory(names[i], cat)) count++;
                        }
                        var label = cat.DisplayName;
                        if (count > 0) label += $"  ({count})";
                        categoryTreeStore.SetValue(catIter, 1, label);
                    }
                } while (categoryTreeStore.IterNext(ref catIter));
            }
            UpdateEmptyStates();
        }

        private void OnStatusChanged(object? sender, EventArgs e)
        {
            if (isSelectingSidebar || lvInprogress == null || lvFinished == null)
            {
                return;
            }
            if (statusTree.Selection.GetSelected(out ITreeModel model, out TreeIter iter))
            {
                isSelectingSidebar = true;
                categoryTree.Selection.UnselectAll();
                isSelectingSidebar = false;

                var path = model.GetPath(iter);
                var index = path.Indices[0];
                // Reset search when switching views so stale keyword can't hide all rows
                ResetSearch();
                if (index == 0)
                {
                    ShowListView(true);
                    category = null;
                    btnOpenFile.Visible = btnOpenFolder.Visible = false;
                    btnPause.Visible = btnResume.Visible = true;
                    inprogressDownloadFilter.Refilter();
                    SetHeaderSubtitle(TextResource.GetText("ALL_UNFINISHED"));
                }
                else
                {
                    ShowListView(false);
                    category = null;
                    btnOpenFile.Visible = btnOpenFolder.Visible = true;
                    btnPause.Visible = btnResume.Visible = false;
                    finishedDownloadFilter.Refilter();
                    SetHeaderSubtitle(TextResource.GetText("ALL_FINISHED"));
                }
            }
        }

        // Reloads the sidebar categories store from configuration dynamically
        public void ReloadCategories()
        {
            if (categoryTreeStore == null) return;
            categoryTreeStore.Clear();
            foreach (var cat in Config.Instance.Categories)
            {
                var (iconName, r, g, b) = GetCategoryIconConfig(cat);
                var rawIcon = LoadSvg(iconName, 20);
                var coloredIcon = rawIcon != null ? GtkHelper.TintPixbuf(rawIcon, r, g, b) : null;
                categoryTreeStore.AppendValues(coloredIcon, cat.DisplayName, cat);
            }
        }

        private void ShowCategoryContextMenu(Category cat, Gdk.EventButton ev)
        {
            var menu = new Menu();
            
            var openFolderItem = new MenuItem(TextResource.GetText("MENU_OPEN_FOLDER") ?? "Open Folder");
            openFolderItem.Activated += (s, e) => 
            {
                string dir = cat.DefaultFolder;
                if (string.IsNullOrEmpty(dir)) dir = Config.Instance.DefaultDownloadFolder;
                System.IO.Directory.CreateDirectory(dir);
                PlatformHelper.OpenFolder(dir);
            };
            menu.Add(openFolderItem);

            var manageCatItem = new MenuItem(TextResource.GetText("SETTINGS_CAT") ?? "Manage Categories...");
            manageCatItem.Activated += (s, e) =>
            {
                ApplicationContext.PlatformUIService.ShowSettingsDialog(1);
            };
            menu.Add(manageCatItem);
            
            menu.ShowAll();
            menu.Popup();
        }

        private void OnCategoryChanged(object? sender, EventArgs e)
        {
            if (isSelectingSidebar || lvInprogress == null || lvFinished == null)
            {
                return;
            }
            if (categoryTree.Selection.GetSelected(out ITreeModel model, out TreeIter iter))
            {
                isSelectingSidebar = true;
                statusTree.Selection.UnselectAll();
                isSelectingSidebar = false;

                ShowListView(false);
                category = (Category)model.GetValue(iter, 2);
                btnOpenFile.Visible = btnOpenFolder.Visible = true;
                btnPause.Visible = btnResume.Visible = false;
                ResetSearch();
                finishedDownloadFilter.Refilter();
                SetHeaderSubtitle(model.GetValue(iter, 1) as string);
            }
        }

        // Switches Active/Complete views including their header strips
        private void ShowListView(bool inprogress)
        {
            try
            {
                if (inprogress)
                {
                    (listHostInProgress ?? (Widget)swInProgress).ShowAll();
                    (listHostFinished ?? (Widget)swFinished).Hide();
                }
                else
                {
                    (listHostFinished ?? (Widget)swFinished).ShowAll();
                    (listHostInProgress ?? (Widget)swInProgress).Hide();
                }
            }
            catch { }
            // The count and the select-all label describe the list now on screen
            SyncSelectAllTooltip();
            UpdateSelectionCount();
            UpdateEmptyStates();
        }

        // The visible list host is the truth for which view is on screen
        private bool IsInProgressView() => listHostInProgress?.Visible != false;

        // Empty-state clear button: exits the search and shows the list again
        private void ClearSearchFromEmptyState()
        {
            try
            {
                if (searchEntry != null)
                {
                    searchEntry.Text = string.Empty;
                }
                searchKeyword = null;
                try { inprogressDownloadFilter?.Refilter(); } catch { }
                try { finishedDownloadFilter?.Refilter(); } catch { }
                UpdateEmptyStates();
            }
            catch { }
        }

        // Keeps both stacks honest; only the active view is visible but neither goes stale
        private void UpdateEmptyStates()
        {
            try { UpdateEmptyState(true); } catch { }
            try { UpdateEmptyState(false); } catch { }
        }

        // Shows the empty page when the visible (filtered) model has no rows left
        private void UpdateEmptyState(bool inProgress)
        {
            var stack = inProgress ? stackInProgress : stackFinished;
            var panel = inProgress ? emptyInProgress : emptyFinished;
            TreeModelSort? sorted = inProgress ? inprogressDownloadsStoreSorted : finishedDownloadsStoreSorted;
            if (stack == null || panel == null || sorted == null) return;
            if (sorted.IterNChildren() > 0)
            {
                stack.VisibleChildName = "list";
                return;
            }
            panel.ShowFor(!string.IsNullOrWhiteSpace(searchKeyword), searchKeyword);
            stack.VisibleChildName = "empty";
        }
    }
}
