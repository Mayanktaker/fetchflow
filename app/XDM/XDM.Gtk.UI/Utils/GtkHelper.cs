// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Gtk;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Translations;
using XDM.Core;
using TraceLog;

namespace XDM.GtkUI.Utils
{
    internal static class GtkHelper
    {
        public static void ShowMessageBox(Window? window, string text, string? title = null)
        {
            var parent = window ?? (ApplicationContext.MainWindow as Window);
            using var msgBox = new MessageDialog(parent, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, text);
            msgBox.Title = title ?? parent?.Title ?? "FetchFlow";
            if (parent?.Group != null)
            {
                parent.Group.AddWindow(msgBox);
            }
            msgBox.Run();
            if (parent?.Group != null)
            {
                parent.Group.RemoveWindow(msgBox);
            }
            msgBox.Destroy();
        }

        public static bool ShowConfirmMessageBox(Window? window, string text, string? title = null)
        {
            var parent = window ?? (ApplicationContext.MainWindow as Window);
            using var msgBox = new MessageDialog(parent, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo, text);
            msgBox.Title = title ?? parent?.Title ?? "FetchFlow";
            if (parent?.Group != null)
            {
                parent.Group.AddWindow(msgBox);
            }
            var ret = msgBox.Run();
            if (parent?.Group != null)
            {
                parent.Group.RemoveWindow(msgBox);
            }
            msgBox.Destroy();
            return ret == (int)ResponseType.Yes;
        }

        public static T GetComboBoxSelectedItem<T>(ComboBox comboBox)
        {
            comboBox.GetActiveIter(out TreeIter tree);
            return (T)comboBox.Model.GetValue(tree, 0);
        }

        //public static int GetSelectedIndex(ComboBox comboBox)
        //{
        //    comboBox.GetActiveIter(out TreeIter tree);
        //    var path = comboBox.Model.GetPath(tree);
        //    return path?.Indices?.Length > 0 ? path.Indices[0] : -1;
        //}

        //public static void SetSelectedIndex(ComboBox comboBox, int index)
        //{
        //    if (!comboBox.Model.GetIterFirst(out TreeIter iter))
        //    {
        //        return;
        //    }
        //    var i = 0;
        //    do
        //    {
        //        if (index == i)
        //        {
        //            comboBox.SetActiveIter(iter);
        //            return;
        //        }
        //        i++;
        //    }
        //    while (comboBox.Model.IterNext(ref iter));
        //}

        public static int GetSelectedIndex(TreeView treeView)
        {
            var paths = treeView.Selection.GetSelectedRows();
            if (paths != null && paths.Length > 0)
            {
                return paths[0].Indices[0];
            }
            return -1;
        }

        public static int[] GetSelectedIndices(TreeView treeView)
        {
            var paths = treeView.Selection.GetSelectedRows();
            if (paths != null && paths.Length > 0)
            {
                return paths.Select(path => path.Indices[0]).ToArray();
            }
            return new int[0];
        }

        public static void SetSelectedIndex(TreeView treeView, int index)
        {
            if (!treeView.Model.GetIterFirst(out TreeIter iter))
            {
                return;
            }
            var i = 0;
            do
            {
                if (index == i)
                {
                    treeView.Selection.SelectIter(iter);
                    return;
                }
                i++;
            }
            while (treeView.Model.IterNext(ref iter));
        }

        public static T? GetSelectedValue<T>(TreeView treeView, int dataIndex)
        {
            var index = GetSelectedIndex(treeView);
            if (!treeView.Model.GetIterFirst(out TreeIter iter))
            {
                return default(T);
            }
            var i = 0;
            do
            {
                if (index == i)
                {
                    return (T)treeView.Model.GetValue(iter, dataIndex);
                }
                i++;
            }
            while (treeView.Model.IterNext(ref iter));
            return default(T);
        }

        public static T? GetValueAt<T>(TreeView treeView, int index, int dataIndex)
        {
            if (!treeView.Model.GetIterFirst(out TreeIter iter))
            {
                return default(T);
            }
            var i = 0;
            do
            {
                if (index == i)
                {
                    return (T)treeView.Model.GetValue(iter, dataIndex);
                }
                i++;
            }
            while (treeView.Model.IterNext(ref iter));
            return default(T);
        }

        public static List<T> GetSelectedValues<T>(TreeView treeView, int dataIndex)
        {
            var list = new List<T>();
            if (!treeView.Model.GetIterFirst(out TreeIter iter))
            {
                return list;
            }
            do
            {
                if (treeView.Selection.IterIsSelected(iter))
                {
                    list.Add((T)treeView.Model.GetValue(iter, dataIndex));
                }
            }
            while (treeView.Model.IterNext(ref iter));
            return list;
        }

        public static void RemoveAt(ListStore model, int index)
        {
            if (!model.GetIterFirst(out TreeIter iter))
            {
                return;
            }
            var i = 0;
            do
            {
                if (index == i)
                {
                    model.Remove(ref iter);
                    break;
                }
                i++;
            }
            while (model.IterNext(ref iter));
        }

        public static List<T> GetListStoreValues<T>(ITreeModel model, int dataIndex)
        {
            var list = new List<T>();
            if (!model.GetIterFirst(out TreeIter iter))
            {
                return list;
            }
            do
            {
                list.Add((T)model.GetValue(iter, dataIndex));
            }
            while (model.IterNext(ref iter));
            return list;
        }

        public static void ListStoreForEach(ITreeModel model, Action<TreeIter> iterCallback)
        {
            if (!model.GetIterFirst(out TreeIter iter))
            {
                return;
            }
            do
            {
                iterCallback.Invoke(iter);
            }
            while (model.IterNext(ref iter));
        }

        public static ListStore PopulateComboBox(ComboBox comboBox, params string[] values)
        {
            var cmbStore = new ListStore(typeof(string));
            foreach (var text in values)
            {
                var iter = cmbStore.Append();
                cmbStore.SetValue(iter, 0, text);
            }
            comboBox.Model = cmbStore;
            var cell = new CellRendererText();
            cell.Ellipsize = Pango.EllipsizeMode.End;
            comboBox.PackStart(cell, true);
            comboBox.AddAttribute(cell, "text", 0);
            return cmbStore;
        }

        public static ListStore PopulateComboBoxGeneric<T>(ComboBox comboBox, params T[] values)
        {
            var cmbStore = new ListStore(typeof(string), typeof(T));
            foreach (var text in values)
            {
                var iter = cmbStore.Append();
                cmbStore.SetValue(iter, 0, $"{text}");
                cmbStore.SetValue(iter, 1, text);
            }
            comboBox.Model = cmbStore;
            var cell = new CellRendererText();
            cell.Ellipsize = Pango.EllipsizeMode.End;
            comboBox.PackStart(cell, true);
            comboBox.AddAttribute(cell, "text", 0);
            return cmbStore;
        }

        public static T? GetSelectedComboBoxValue<T>(ComboBox comboBox)
        {
            var index = comboBox.Active;
            var count = 0;
            if (!comboBox.Model.GetIterFirst(out TreeIter iter))
            {
                return default(T);
            }
            do
            {

                if (index == count)
                {
                    return (T)comboBox.Model.GetValue(iter, 1);
                }
                count++;
            }
            while (comboBox.Model.IterNext(ref iter));
            return default(T);
        }

        public static void SetSelectedComboBoxValue<T>(ComboBox comboBox, T value)
        {
            var count = 0;
            if (!comboBox.Model.GetIterFirst(out TreeIter iter))
            {
                return;
            }
            do
            {
                var val = (T)comboBox.Model.GetValue(iter, 1);
                if (EqualityComparer<T>.Default.Equals(val, value))
                {
                    comboBox.Active = count;
                    return;
                }
                count++;
            }
            while (comboBox.Model.IterNext(ref iter));
        }

        public static Gdk.Pixbuf? LoadSvg(string name, int dimension = 16)
        {
            try
            {
                var candidates = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "svg-icons", $"{name}.svg"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{name}.svg"),
                    Path.Combine(Directory.GetCurrentDirectory(), "svg-icons", $"{name}.svg"),
                    Path.Combine(Directory.GetCurrentDirectory(), $"{name}.svg"),
                    Path.Combine(Directory.GetCurrentDirectory(), "app", "XDM", "XDM.Gtk.UI", "svg-icons", $"{name}.svg"),
                    Path.Combine(Directory.GetCurrentDirectory(), "build_output", "xdm-app", "svg-icons", $"{name}.svg")
                };

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return new Gdk.Pixbuf(candidate, dimension, dimension, true);
                    }
                }
            }
            catch { }
            return null;
        }

        // Tinted copy of a monochrome pixbuf: RGB channels replaced with (r,g,b),
        // alpha silhouette preserved — used for selected sidebar row icons
        public static Gdk.Pixbuf? TintPixbuf(Gdk.Pixbuf? source, byte r, byte g, byte b)
        {
            if (source == null)
            {
                return null;
            }
            var copy = source.Copy();
            var rowstride = copy.Rowstride;
            var channels = copy.NChannels;
            var hasAlpha = copy.HasAlpha;
            var pixels = new byte[rowstride * copy.Height];
            Marshal.Copy(copy.Pixels, pixels, 0, pixels.Length);
            for (var y = 0; y < copy.Height; y++)
            {
                var rowOffset = y * rowstride;
                for (var x = 0; x < copy.Width; x++)
                {
                    var offset = rowOffset + x * channels;
                    if (!hasAlpha || pixels[offset + 3] != 0)
                    {
                        pixels[offset] = r;
                        pixels[offset + 1] = g;
                        pixels[offset + 2] = b;
                    }
                }
            }
            Marshal.Copy(pixels, 0, copy.Pixels, pixels.Length);
            return copy;
        }

        public static string? SelectFolder(Window parent)
        {
            using var fc = new FileChooserNative("XDM", parent, FileChooserAction.SelectFolder, 
                TextResource.GetText("MSG_SELECT_FOLDER"), TextResource.GetText("ND_CANCEL"));
            if (fc.Run() == (int)ResponseType.Accept)
            {
                return fc.Filename;
            }
            return null;

            //using var fc = new FileChooserDialog("XDM", parent, FileChooserAction.SelectFolder);
            //try
            //{
            //    if (parent.Group != null)
            //    {
            //        parent.Group.AddWindow(fc);
            //    }
            //    fc.AddButton(Stock.Save, ResponseType.Accept);
            //    fc.AddButton(Stock.Cancel, ResponseType.Cancel);
            //    if (fc.Run() == (int)ResponseType.Accept)
            //    {
            //        return fc.Filename;
            //    }
            //    return null;
            //}
            //finally
            //{
            //    if (parent.Group != null)
            //    {
            //        parent.Group.RemoveWindow(fc);
            //    }
            //    fc.Destroy();
            //    fc.Dispose();
            //}
        }

        public static string? SelectFile(Window parent)
        {
            using var fc = new FileChooserNative("XDM", parent, FileChooserAction.Open,
                TextResource.GetText("MSG_SELECT_FOLDER"), TextResource.GetText("ND_CANCEL"));
            if (fc.Run() == (int)ResponseType.Accept)
            {
                return fc.Filename;
            }
            return null;

            //using var fc = new FileChooserDialog("XDM", parent, FileChooserAction.Open);
            //try
            //{
            //    if (parent.Group != null)
            //    {
            //        parent.Group.AddWindow(fc);
            //    }
            //    fc.AddButton(Stock.Save, ResponseType.Accept);
            //    fc.AddButton(Stock.Cancel, ResponseType.Cancel);
            //    if (fc.Run() == (int)ResponseType.Accept)
            //    {
            //        return fc.Filename;
            //    }
            //    return null;
            //}
            //finally
            //{
            //    if (parent.Group != null)
            //    {
            //        parent.Group.RemoveWindow(fc);
            //    }
            //    fc.Destroy();
            //    fc.Dispose();
            //}
        }

        // Export/Save path: use SetCurrentFolder (string) rather than SetCurrentFolderFile
        // (GFile) — the latter throws GLib.GException "Cannot change to folder because it is
        // not local" when dir is empty, non-existent, or a GIO-unresolvable path, which then
        // escapes as TargetInvocationException through GLib.SignalClosure (crash in crash.log).
        public static string? SaveFile(Window parent, string? path)
        {
            using var fc = new FileChooserNative("XDM", parent, FileChooserAction.Save,
                TextResource.GetText("DESC_SAVE_Q"), TextResource.GetText("ND_CANCEL"));
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    fc.SetFilename(Path.GetFileName(path));
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        fc.SetCurrentFolder(dir);
                    else if (!string.IsNullOrEmpty(dir))
                        Log.Debug($"SaveFile: dir does not exist, skipping SetCurrentFolder: {dir}");
                }
                catch (Exception ex)
                {
                    Log.Debug("SaveFile SetCurrentFolder: " + ex.Message);
                }
            }
            if (fc.Run() == (int)ResponseType.Accept)
            {
                return fc.Filename;
            }
            return null;

            //using var fc = new FileChooserDialog("XDM", parent, FileChooserAction.Save);
            //if (!string.IsNullOrEmpty(path))
            //{
            //    var dir = Path.GetDirectoryName(path);
            //    fc.SetFilename(Path.GetFileName(path));
            //    fc.SetCurrentFolderFile(GLib.FileFactory.NewForPath(dir));
            //}
            //try
            //{
            //    if (parent.Group != null)
            //    {
            //        parent.Group.AddWindow(fc);
            //    }
            //    fc.AddButton(Stock.Save, ResponseType.Accept);
            //    fc.AddButton(Stock.Cancel, ResponseType.Cancel);
            //    if (fc.Run() == (int)ResponseType.Accept)
            //    {
            //        return fc.Filename;
            //    }
            //    return null;
            //}
            //finally
            //{
            //    if (parent.Group != null)
            //    {
            //        parent.Group.RemoveWindow(fc);
            //    }
            //    fc.Destroy();
            //    fc.Dispose();
            //}
        }

        public static void AttachSafeDispose(Window window)
        {
            window.DeleteEvent += (s, _) =>
            {
                try
                {
                    if (s is Window w)
                    {
                        var g = w.Group;
                        if (g != null)
                        {
                            g.RemoveWindow(w);
                        }
                    }
                }
                catch { }
            };

            window.Destroyed += (s, _) =>
            {
                try
                {
                    if (s is Window w)
                    {
                        w.Dispose();
                    }
                }
                catch { }
            };
        }

        public static void ConfigurePasswordField(Entry? entry)
        {
            if (entry == null)
            {
                return;
            }
            entry.Visibility = false;
            entry.InvisibleChar = '*';
            entry.InputPurpose = InputPurpose.Password;
        }

        public static TreeIter ConvertViewToModel(TreeIter iter, TreeModelSort sortedModel, TreeModelFilter filterModel)
        {
            var iter1 = sortedModel.ConvertIterToChildIter(iter);
            return filterModel.ConvertIterToChildIter(iter1);
        }

        // Creates a standard themed CSD dialog HeaderBar with close button, title, and left app icon
        public static HeaderBar CreateDialogHeaderBar(string title, string? subtitle = null)
        {
            var hb = new HeaderBar
            {
                ShowCloseButton = true,
                DecorationLayout = ":close",
                Title = title
            };
            if (!string.IsNullOrEmpty(subtitle))
            {
                hb.Subtitle = subtitle;
            }

            try
            {
                var appIcon = new Image
                {
                    Pixbuf = LoadSvg("fetchflow-mark", 18) ?? LoadSvg("fetchflow-logo", 18),
                    MarginStart = 4,
                    MarginEnd = 2,
                    Valign = Align.Center
                };
                hb.PackStart(appIcon);
            }
            catch { }
            hb.ShowAll();
            return hb;
        }

        // Applies the FetchFlow app icon to a specific window/dialog
        public static void SetWindowAppIcon(Window window)
        {
            try
            {
                window.IconName = "com.mayanktaker.fetchflow";
                var icon = LoadSvg("fetchflow-logo", 64);
                if (icon != null)
                {
                    window.Icon = icon;
                }
            }
            catch { }
        }
    }
}
