// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using GLib;
using IoPath = System.IO.Path;
using XDM.Core;
using Translations;
using UI = Gtk.Builder.ObjectAttribute;
using XDM.GtkUI.Utils;

namespace XDM.GtkUI.Dialogs.Settings
{
    // Category add and edit dialog with custom icon and accent color selection
    public class CategoryEditDialog : Dialog
    {
        [UI] private Label Label1, Label2, Label3, LabelIcon, LabelColor;
        [UI] private Entry TxtName, TxtFileTypes, TxtFolder;
        [UI] private ComboBoxText CmbIcon, CmbColor;
        [UI] private Button Browse, BtnOk, BtnCancel;

        public string? DisplayName { get; private set; }
        public string? FileTypes { get; private set; }
        public string? Folder { get; private set; }
        public string? CustomIcon { get; private set; }
        public string? CustomColor { get; private set; }

        public bool Result { get; set; } = false;

        private WindowGroup group;

        private CategoryEditDialog(Builder builder, Window parent, WindowGroup group) : base(builder.GetRawOwnedObject("dialog"))
        {
            builder.Autoconnect(this);

            Modal = true;
            TransientFor = parent;
            this.group = group;
            this.group.AddWindow(this);

            GtkHelper.AttachSafeDispose(this);
            var titleText = TextResource.GetText("MSG_CATEGORY") ?? "Category";
            Title = titleText;
            Titlebar = GtkHelper.CreateDialogHeaderBar(titleText);
            GtkHelper.SetWindowAppIcon(this);

            Label1.Text = TextResource.GetText("SORT_NAME") ?? "Name:";
            Label2.Text = TextResource.GetText("SETTINGS_CAT_TYPES") ?? "File Extensions:";
            Label3.Text = TextResource.GetText("SETTINGS_CAT_FOLDER") ?? "Folder:";
            LabelIcon.Text = "Icon:";
            LabelColor.Text = "Accent Color:";

            PopulateIconAndColorCombos();

            BtnOk.Clicked += BtnOk_Clicked;
            BtnCancel.Clicked += BtnCancel_Clicked;
            BtnOk.StyleContext.AddClass("suggested-action");
            Browse.Clicked += Browse_Clicked;

            BtnOk.Label = TextResource.GetText("MSG_OK") ?? "OK";
            BtnCancel.Label = TextResource.GetText("ND_CANCEL") ?? "Cancel";

            SetDefaultSize(520, 310);
            SetSizeRequest(440, 260);
            Resizable = true;
        }

        // Populates the icon selection and color palette dropdowns
        private void PopulateIconAndColorCombos()
        {
            CmbIcon.Append("folder-shared-line", "Folder (Default)");
            CmbIcon.Append("file-text-line", "Document");
            CmbIcon.Append("image-line", "Image / Graphic");
            CmbIcon.Append("file-music-line", "Audio / Music");
            CmbIcon.Append("movie-line", "Video / Stream");
            CmbIcon.Append("file-zip-line", "Archive / Zip");
            CmbIcon.Append("function-line", "Application / Executable");
            CmbIcon.Append("links-line", "Link / Web");
            CmbIcon.Append("download-2-line", "Download");
            CmbIcon.Append("file-line", "General File");
            CmbIcon.ActiveId = "folder-shared-line";

            CmbColor.Append("#6366f1", "Indigo (Default)");
            CmbColor.Append("#38bdf8", "Sky Blue");
            CmbColor.Append("#10b981", "Emerald");
            CmbColor.Append("#f59e0b", "Amber");
            CmbColor.Append("#f43f5e", "Rose");
            CmbColor.Append("#a855f7", "Purple");
            CmbColor.Append("#14b8a6", "Teal");
            CmbColor.Append("#ef4444", "Coral");
            CmbColor.ActiveId = "#6366f1";
        }

        private void Browse_Clicked(object? sender, EventArgs e)
        {
            var folder = GtkHelper.SelectFolder(this);
            if (!string.IsNullOrEmpty(folder))
            {
                this.TxtFolder.Text = folder;
            }
        }

        // Initializes fields from an existing Category instance
        public void SetCategory(Category category)
        {
            this.TxtName.Text = category.DisplayName;
            this.TxtFileTypes.Text = string.Join(",", category.FileExtensions.ToArray());
            this.TxtFolder.Text = category.DefaultFolder;

            if (!string.IsNullOrEmpty(category.CustomIcon))
            {
                CmbIcon.ActiveId = category.CustomIcon;
            }
            if (!string.IsNullOrEmpty(category.CustomColor))
            {
                CmbColor.ActiveId = category.CustomColor;
            }
        }

        private void BtnOk_Clicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(TxtName.Text))
            {
                GtkHelper.ShowMessageBox(this, TextResource.GetText("MSG_CAT_NAME_MISSING"));
                return;
            }
            if (string.IsNullOrEmpty(TxtFileTypes.Text))
            {
                GtkHelper.ShowMessageBox(this, TextResource.GetText("MSG_CAT_FILE_TYPES_MISSING"));
                return;
            }
            if (string.IsNullOrEmpty(TxtFolder.Text))
            {
                GtkHelper.ShowMessageBox(this, TextResource.GetText("MSG_CAT_FOLDER_MISSING"));
                return;
            }

            this.DisplayName = this.TxtName.Text;
            this.FileTypes = this.TxtFileTypes.Text;
            this.Folder = this.TxtFolder.Text;
            this.CustomIcon = CmbIcon.ActiveId ?? "folder-shared-line";
            this.CustomColor = CmbColor.ActiveId ?? "#6366f1";
            Result = true;

            try { this.group?.RemoveWindow(this); } catch { }
            Destroy();
        }

        private void BtnCancel_Clicked(object? sender, EventArgs e)
        {
            Result = false;
            try { this.group?.RemoveWindow(this); } catch { }
            Destroy();
        }

        public static CategoryEditDialog CreateFromGladeFile(Window parent, WindowGroup group)
        {
            var builder = new Builder();
            builder.AddFromFile(IoPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "glade", "category-edit-dialog.glade"));
            return new CategoryEditDialog(builder, parent, group);
        }
    }
}
