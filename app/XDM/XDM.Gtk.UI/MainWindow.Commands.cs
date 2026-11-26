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
        public IInProgressDownloadRow? FindInProgressItem(string id)
        {
            if (!inprogressDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                return null;
            }
            do
            {
                var ent = (InProgressDownloadItem)inprogressDownloadsStore.GetValue(iter, INPROGRESS_DATA_INDEX);
                if (ent.Id == id)
                {
                    return new InProgressEntryWrapper(ent, iter, inprogressDownloadsStore);
                }
            }
            while (inprogressDownloadsStore.IterNext(ref iter));
            return null;
        }

        public TreeIter? FindInProgressItemIterById(string id)
        {
            if (!inprogressDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                return null;
            }
            do
            {
                var ent = (InProgressDownloadItem)inprogressDownloadsStore.GetValue(iter, INPROGRESS_DATA_INDEX);
                if (ent.Id == id)
                {
                    return iter;
                }
            }
            while (inprogressDownloadsStore.IterNext(ref iter));
            return null;
        }

        public IFinishedDownloadRow? FindFinishedItem(string id)
        {
            if (!this.finishedDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                return null;
            }
            do
            {
                var ent = (FinishedDownloadItem)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
                if (ent.Id == id)
                {
                    return new FinishedEntryWrapper(ent, iter, finishedDownloadsStore);
                }
            }
            while (finishedDownloadsStore.IterNext(ref iter));
            return null;
        }

        public TreeIter? FindFinishedItemIterById(string id)
        {
            if (!this.finishedDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                return null;
            }
            do
            {
                var ent = (FinishedDownloadItem)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
                if (ent.Id == id)
                {
                    return iter;
                }
            }
            while (finishedDownloadsStore.IterNext(ref iter));
            return null;
        }

        public void AddToTop(InProgressDownloadItem entry)
        {
            var iter = inprogressDownloadsStore.Insert(0);
            inprogressDownloadsStore.SetValue(iter, 0, entry.Name);
            inprogressDownloadsStore.SetValue(iter, 1, entry.DateAdded.ToString("MMM d, yyyy · HH:mm"));
            inprogressDownloadsStore.SetValue(iter, 2, FormattingHelper.FormatSize(entry.Size));
            inprogressDownloadsStore.SetValue(iter, 3, entry.Progress);
            inprogressDownloadsStore.SetValue(iter, 4, entry.Status.ToString());
            inprogressDownloadsStore.SetValue(iter, 5, entry);
            UpdateStatusListCounts();
        }

        public void AddToTop(FinishedDownloadItem entry)
        {
            finishedDownloadsStore.AppendValues(
                entry.Name,
                entry.DateAdded.ToString("MMM d, yyyy · HH:mm"),
                FormattingHelper.FormatSize(entry.Size),
                entry);
            finishedDownloadFilter.Refilter();
            //finishedDownloadsStoreSorted.AppendValues()
            //sortedStore.SetSortColumnId(1, SortType.Descending);
            UpdateStatusListCounts();
        }

        public void SwitchToInProgressView()
        {
            if (this.statusTreeStore.GetIterFirst(out TreeIter iter))
            {
                this.statusTree.Selection.SelectIter(iter);
            }
        }

        public void ClearInProgressViewSelection()
        {
            this.lvInprogress.Selection.UnselectAll();
        }

        public void SwitchToFinishedView()
        {
            if (this.statusTreeStore.GetIterFirst(out TreeIter iter) &&
                this.statusTreeStore.IterNext(ref iter))
            {
                this.statusTree.Selection.SelectIter(iter);
            }
        }

        public void ClearFinishedViewSelection()
        {
            this.lvFinished.Selection.UnselectAll();
        }

        public bool Confirm(object? window, string text)
        {
            if (window is not Window owner)
            {
                owner = this;
            }
            using var msg = new MessageDialog(owner, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo, text);
            msg.Title = "FetchFlow";
            if (msg.Run() == (int)ResponseType.Yes)
            {
                return true;
            }
            return false;
        }

        public void RunOnUIThread(System.Action action)
        {
            Application.Invoke((a, b) => action.Invoke());
        }

        public void RunOnUIThread(Action<string, int, double, long> action, string id, int progress, double speed, long eta)
        {
            Application.Invoke((a, b) =>
            {
                action.Invoke(id, progress, speed, eta);
                UpdateSpeedTracking(id, progress >= 100 ? 0 : speed, eta);
            });
        }

        // Updates aggregate download throughput across all active transfers and tracks shortest ETA
        private void UpdateSpeedTracking(string id, double speed, long eta = 0)
        {
            lock (activeSpeeds)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    if (speed > 0)
                    {
                        activeSpeeds[id] = speed;
                        if (eta > 0)
                        {
                            activeEtas[id] = eta;
                        }
                        else
                        {
                            activeEtas.Remove(id);
                        }
                    }
                    else
                    {
                        activeSpeeds.Remove(id);
                        activeEtas.Remove(id);
                    }
                }

                if (lblTotalSpeed == null) return;

                double total = 0;
                foreach (var s in activeSpeeds.Values)
                {
                    total += s;
                }
                var count = activeSpeeds.Count;

                if (total > 0 && count > 0)
                {
                    var formatted = FormattingHelper.FormatSize(total) + "/s";
                    long minEta = 0;
                    foreach (var e in activeEtas.Values)
                    {
                        if (e > 0 && (minEta == 0 || e < minEta))
                            minEta = e;
                    }
                    var etaText = minEta > 0 ? $" · ETA: {FormattingHelper.ToHMS(minEta)}" : "";
                    // Queued rows wait behind the running ones, counted from the status column
                    int queued = 0;
                    try
                    {
                        if (inprogressDownloadsStore != null && inprogressDownloadsStore.GetIterFirst(out TreeIter qiter))
                        {
                            do
                            {
                                var st = inprogressDownloadsStore.GetValue(qiter, 4) as string;
                                if (!string.IsNullOrEmpty(st) && st.IndexOf("Wait", StringComparison.OrdinalIgnoreCase) >= 0)
                                    queued++;
                            } while (inprogressDownloadsStore.IterNext(ref qiter));
                        }
                    }
                    catch { }
                    var activeText = $"{count} {TextResource.GetText("LBL_ACTIVE_COUNT") ?? "active"}";
                    var queuedText = queued > 0 ? $" · {queued} {TextResource.GetText("LBL_QUEUED_COUNT") ?? "queued"}" : "";

                    lblTotalSpeed.Markup = $"<span color='#f97316'><b>⚡ {formatted}</b></span>{etaText} <span color='#888888' size='small'>({activeText}{queuedText})</span>";
                    lblTotalSpeed.TooltipText = $"Aggregate live download speed: {formatted}{etaText} across {count} transfer(s)";
                    lblTotalSpeed.Visible = true;
                    trayManager?.UpdateSpeedStatus($"{formatted}{etaText} ({activeText}{queuedText})");
                }
                else
                {
                    // Idle names the reason when capture is off, matching the GTK 4 footer
                    var idle = TextResource.GetText("LBL_IDLE") ?? "Idle";
                    if (!Config.Instance.IsBrowserMonitoringEnabled)
                    {
                        idle += " — " + (TextResource.GetText("MSG_EXT_DISABLED") ?? "Monitoring Off");
                    }
                    lblTotalSpeed.Markup = $"<span color='#888888' size='small'>{GLib.Markup.EscapeText(idle)}</span>";
                    lblTotalSpeed.TooltipText = TextResource.GetText("LBL_SHOW_PROGRESS") ?? "Show progress";
                    lblTotalSpeed.Visible = true;
                    trayManager?.UpdateSpeedStatus("");
                }
            }
        }

        public void Delete(IInProgressDownloadRow row)
        {
            var id = row.DownloadEntry.Id;
            UpdateSpeedTracking(id, 0);
            // Primary: remove via the selection's own iter converted to the child store —
            // immune to any id mismatch; only selection wrappers carry sort iters (wrappers
            // from FindInProgressItem hold child iters and must go straight to the id scan)
            if (row is InProgressEntryWrapper wrapper
                && ReferenceEquals(wrapper.GetStore(), inprogressDownloadsStoreSorted))
            {
                try
                {
                    var childIter = GtkHelper.ConvertViewToModel(wrapper.TreeIter, inprogressDownloadsStoreSorted, inprogressDownloadFilter);
                    if (inprogressDownloadsStore!.IterIsValid(childIter)
                        && inprogressDownloadsStore.GetValue(childIter, INPROGRESS_DATA_INDEX) is InProgressDownloadItem verify
                        && verify.Id == id)
                    {
                        inprogressDownloadsStore.Remove(ref childIter);
                        UpdateStatusListCounts();
                        Log.Debug($"Delete: removed in-progress row {id} via selection iter.");
                        return;
                    }
                    Log.Debug($"Delete: selection iter unusable for {id}; falling back to id scan.");
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, $"Delete: iter conversion failed for {id}; falling back to id scan.");
                }
            }
            var modelIter = FindInProgressItemIterById(id);
            if (modelIter.HasValue)
            {
                var iter = modelIter.Value;
                inprogressDownloadsStore.Remove(ref iter);
                UpdateStatusListCounts();
                Log.Debug($"Delete: removed in-progress row {id} via id scan.");
            }
            else
            {
                Log.Debug($"Delete: in-progress row {id} NOT found in store ({inprogressDownloadsStore.IterNChildren()} rows) — UI row not removed.");
            }
        }

        public void Delete(IFinishedDownloadRow row)
        {
            var id = row.DownloadEntry.Id;
            var modelIter = FindFinishedItemIterById(id);
            if (modelIter.HasValue)
            {
                var iter = modelIter.Value;
                finishedDownloadsStore.Remove(ref iter);
                UpdateStatusListCounts();
            }
            //var iter = GtkHelper.ConvertViewToModel(((FinishedEntryWrapper)row).TreeIter,
            //    finishedDownloadsStoreSorted, finishedDownloadFilter);
        }

        private void ResetSearch()
        {
            var raw = searchEntry?.Text;
            searchKeyword = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            if (searchEntry != null && !string.IsNullOrEmpty(searchEntry.Text))
            {
                searchEntry.Text = string.Empty;
            }
            // Make ResetSearch self-healing: callers can't forget to refilter.
            // Filters may not exist yet during early sidebar init, so guard.
            try { inprogressDownloadFilter?.Refilter(); } catch { }
            try { finishedDownloadFilter?.Refilter(); } catch { }
            searchKeyword = null;
        }

        public void DeleteAllFinishedDownloads()
        {
            if (!GtkHelper.ShowConfirmMessageBox(this, TextResource.GetText("MENU_DELETE_COMPLETED"), "FetchFlow"))
            {
                return;
            }
            finishedDownloadsStore.Clear();
            UpdateStatusListCounts();
        }

        public void Delete(IEnumerable<IInProgressDownloadRow> rows)
        {
            var ids = rows
                .Where(row => row?.DownloadEntry != null)
                .Select(row => row.DownloadEntry.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();
            var removed = 0;
            foreach (var id in ids)
            {
                UpdateSpeedTracking(id, 0);
                // Re-scan the child store for every ID; selection/sort iters become stale
                // after the first removal and must never be reused in a batch.
                var modelIter = FindInProgressItemIterById(id);
                if (!modelIter.HasValue)
                {
                    Log.Debug($"Delete batch: in-progress row {id} not found in store.");
                    continue;
                }
                var iter = modelIter.Value;
                if (inprogressDownloadsStore!.Remove(ref iter))
                {
                    removed++;
                    Log.Debug($"Delete batch: removed in-progress row {id}.");
                }
            }
            UpdateStatusListCounts();
            lvInprogress?.Selection.UnselectAll();
            Log.Debug($"Delete batch: removed {removed} of {ids.Count} selected row(s).");
        }

        public void Delete(IEnumerable<IFinishedDownloadRow> rows)
        {
            foreach (var row in rows)
            {
                Delete(row);
                //var iter = ((FinishedEntryWrapper)row).TreeIter;
                //inprogressDownloadsStore.Remove(ref iter);
            }
        }

        public string GetUrlFromClipboard()
        {
            var cb = Clipboard.Get(Gdk.Selection.Clipboard);
            return cb.WaitForText();
        }

        public void OpenNewDownloadMenu()
        {
            newDownloadMenu.PopupAtWidget(this.btnNew, Gdk.Gravity.SouthWest, Gdk.Gravity.NorthWest, null);
        }

        private void OpenMainMenu()
        {
            mainMenu.PopupAtWidget(this.btnMenu, Gdk.Gravity.SouthEast, Gdk.Gravity.NorthEast, null);
        }

        public void SetClipboardText(string text)
        {
            var cb = Clipboard.Get(Gdk.Selection.Clipboard);
            if (cb != null)
            {
                cb.Text = text;
            }
        }

        public void SetClipboardFile(string file)
        {
            var cbcp = new ClipboardFileCopy(file);
            cbcp.Exec();
        }

        public void UpdateBrowserMonitorButton()
        {
            btnMonitoring.Active = Config.Instance.IsBrowserMonitoringEnabled;
        }

        public void ShowUpdateAvailableNotification()
        {
            isUpdateAvailable = true;
            if (updateDot != null)
            {
                updateDot.Visible = true;
            }
        }

        public void ClearUpdateInformation()
        {
            RunOnUIThread(() =>
            {
                isUpdateAvailable = false;
                if (updateDot != null)
                {
                    updateDot.Visible = false;
                }
            });
        }

        private IEnumerable<FinishedDownloadItem> GetAllFinishedDownloads()
        {
            if (!finishedDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                yield break;
            }
            yield return (FinishedDownloadItem)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
            while (finishedDownloadsStore.IterNext(ref iter))
            {
                yield return (FinishedDownloadItem)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
            }
        }

        private IEnumerable<InProgressDownloadItem> GetAllInProgressDownloads()
        {
            if (!inprogressDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                yield break;
            }
            yield return (InProgressDownloadItem)inprogressDownloadsStore.GetValue(iter, INPROGRESS_DATA_INDEX);
            while (inprogressDownloadsStore.IterNext(ref iter))
            {
                yield return (InProgressDownloadItem)inprogressDownloadsStore.GetValue(iter, INPROGRESS_DATA_INDEX);
            }
        }

        private void SetFinishedDownloads(IEnumerable<FinishedDownloadItem> finishedDownloads)
        {
            finishedDownloadsStore.Clear();
            foreach (var item in finishedDownloads)
            {
                finishedDownloadsStore.AppendValues(item.Name,
                    item.DateAdded.ToString("MMM d, yyyy · HH:mm"),
                    FormattingHelper.FormatSize(item.Size),
                    item);
            }
            finishedDownloadFilter?.Refilter();
            UpdateStatusListCounts();
        }

        private void SetInProgressDownloads(IEnumerable<InProgressDownloadItem> incompleteDownloads)
        {
            inprogressDownloadsStore.Clear();
            foreach (var item in incompleteDownloads)
            {
                inprogressDownloadsStore.AppendValues(item.Name,
                    item.DateAdded.ToString("MMM d, yyyy · HH:mm"),
                    FormattingHelper.FormatSize(item.Size),
                    item.Progress,
                    Helpers.GenerateStatusText(item),
                    item);
            }
            inprogressDownloadFilter?.Refilter();
            UpdateStatusListCounts();
        }

        private IList<IInProgressDownloadRow> GetSelectedInProgressDownloads()
        {
            var list = new List<IInProgressDownloadRow>(0);
            var rows = lvInprogress.Selection.GetSelectedRows(out ITreeModel model);
            if (rows != null && rows.Length > 0)
            {
                list.Capacity = rows.Length;
                foreach (var row in rows)
                {
                    if (model.GetIter(out TreeIter iter, row))
                    {
                        if (model.GetValue(iter, INPROGRESS_DATA_INDEX) is not InProgressDownloadItem ent)
                        {
                            Log.Debug($"Selection: in-progress path {row} did not resolve to an item (row skipped).");
                            continue;
                        }
                        list.Add(new InProgressEntryWrapper(ent, iter, model));
                    }
                    else
                    {
                        Log.Debug($"Selection: in-progress path {row} could not be resolved to an iter (row skipped).");
                    }
                }
            }
            return list;
        }

        private IList<IFinishedDownloadRow> GetSelectedFinishedDownloads()
        {
            var list = new List<IFinishedDownloadRow>(0);
            var rows = lvFinished.Selection.GetSelectedRows(out ITreeModel model);
            if (rows != null && rows.Length > 0)
            {
                list.Capacity = rows.Length;
                foreach (var row in rows)
                {
                    if (model.GetIter(out TreeIter iter, row))
                    {
                        if (model.GetValue(iter, FINISHED_DATA_INDEX) is not FinishedDownloadItem ent)
                        {
                            Log.Debug($"Selection: finished path {row} did not resolve to an item (row skipped).");
                            continue;
                        }
                        list.Add(new FinishedEntryWrapper(ent, iter, model));
                    }
                    else
                    {
                        Log.Debug($"Selection: finished path {row} could not be resolved to an iter (row skipped).");
                    }
                }
            }
            return list;
        }

        private int GetSelectedCategory()
        {
            if (statusTree != null && statusTree.Selection.GetSelected(out ITreeModel model, out TreeIter iter))
            {
                return model.GetPath(iter).Indices[0];
            }
            return -1;
        }

        public void ConfirmDelete(string text, out bool approved, out bool deleteFiles)
        {
            approved = false;
            deleteFiles = false;
            using var dlg = DeleteConfirmDialog.CreateFromGladeFile(this, this.windowGroup);
            if (!string.IsNullOrEmpty(text))
            {
                dlg.DescriptionText = text;
            }
            dlg.Run();
            if (dlg.Result)
            {
                approved = true;
                deleteFiles = dlg.ShouldDeleteFile;
            }
            dlg.Destroy();
        }

        public IPlatformClipboardMonitor GetClipboardMonitor() => this.clipboarMonitor;
    }
}
