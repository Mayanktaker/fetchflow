// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Native GTK 4 Queue and Scheduler management dialog implementing IQueuesWindow.
using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Downloader;
using XDM.Core.UI;
using XDM.Core.Util;

namespace XDM.Gtk4UI.Dialogs
{
    // Native GTK 4 window for managing download queues and schedules
    internal sealed class QueueSchedulerDialog : IQueuesWindow
    {
        private readonly Window window;
        private readonly List<DownloadQueue> queues = new();
        private DownloadQueue? selectedQueue;
        private bool isSuppressingEvents;

        private readonly ListBox queueListBox;
        private readonly ListBox downloadsListBox;
        private readonly List<(ListBoxRow Row, InProgressDownloadItem Item)> currentDownloadRows = new();

        private readonly Button btnNewQueue;
        private readonly Button btnDeleteQueue;
        private readonly Button btnStartQueue;
        private readonly Button btnStopQueue;

        private readonly Button btnMoveUp;
        private readonly Button btnMoveDown;
        private readonly Button btnAddDownloads;
        private readonly Button btnRemoveDownloads;
        private readonly Button btnMoveToQueue;

        private readonly CheckButton chkEnableScheduler;
        private readonly CheckButton chkEveryday;
        private readonly CheckButton[] dayChecks;
        private readonly DropDown cmbStartHour;
        private readonly DropDown cmbStartMin;
        private readonly DropDown cmbStartAmPm;
        private readonly DropDown cmbStopHour;
        private readonly DropDown cmbStopMin;
        private readonly DropDown cmbStopAmPm;
        private readonly Box schedulerContainer;

        public event EventHandler<QueueListEventArgs>? QueuesModified;
        public event EventHandler<DownloadListEventArgs>? QueueStartRequested;
        public event EventHandler<DownloadListEventArgs>? QueueStopRequested;
        public event EventHandler? WindowClosing;

        // Constructs the QueueSchedulerDialog with dual-pane layout
        public QueueSchedulerDialog(Window? parent)
        {
            window = Window.New();
            var titleText = TextResource.GetText("DESC_Q_TITLE") ?? "Queue & Scheduler";
            window.Title = titleText;
            window.Modal = true;
            window.SetDefaultSize(840, 520);
            if (parent != null) window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(titleText);
            window.SetTitlebar(header);

            window.OnCloseRequest += (sender, args) =>
            {
                WindowClosing?.Invoke(this, EventArgs.Empty);
                return false;
            };

            var rootBox = Box.New(Orientation.Vertical, 0);

            var paned = Paned.New(Orientation.Horizontal);
            paned.Position = 240;
            paned.ResizeStartChild = false;
            paned.ShrinkStartChild = false;

            // --- Left Pane: Queues List ---
            var leftBox = Box.New(Orientation.Vertical, 8);
            leftBox.MarginTop = 12;
            leftBox.MarginBottom = 12;
            leftBox.MarginStart = 12;
            leftBox.MarginEnd = 8;
            leftBox.WidthRequest = 230;

            var lblQueues = Label.New(TextResource.GetText("MENU_QUEUE") ?? "Queues");
            lblQueues.Halign = Align.Start;
            lblQueues.AddCssClass("title-4");
            leftBox.Append(lblQueues);

            var queueScroll = ScrolledWindow.New();
            queueScroll.Vexpand = true;
            queueScroll.Hexpand = true;

            queueListBox = ListBox.New();
            queueListBox.SelectionMode = SelectionMode.Browse;
            queueListBox.AddCssClass("rich-list");
            queueListBox.OnRowSelected += (_, args) => OnQueueRowSelected(args.Row);
            queueScroll.SetChild(queueListBox);
            leftBox.Append(queueScroll);

            var queueToolbar = Box.New(Orientation.Horizontal, 6);
            btnNewQueue = Button.NewWithLabel(TextResource.GetText("BTN_NEW_QUEUE_SHORT") ?? "+ New");
            btnNewQueue.TooltipText = "Create a new queue";
            btnNewQueue.OnClicked += (_, _) => OnNewQueueClicked();

            btnDeleteQueue = Button.NewWithLabel(TextResource.GetText("DESC_DEL") ?? "Delete");
            btnDeleteQueue.TooltipText = "Delete selected queue";
            btnDeleteQueue.OnClicked += (_, _) => OnDeleteQueueClicked();

            btnStartQueue = Button.NewWithLabel(TextResource.GetText("BTN_START") ?? "Start");
            btnStartQueue.TooltipText = "Start all downloads in this queue";
            btnStartQueue.OnClicked += (_, _) => OnStartQueueClicked();

            btnStopQueue = Button.NewWithLabel(TextResource.GetText("BTN_STOP") ?? "Stop");
            btnStopQueue.TooltipText = "Stop all downloads in this queue";
            btnStopQueue.OnClicked += (_, _) => OnStopQueueClicked();

            queueToolbar.Append(btnNewQueue);
            queueToolbar.Append(btnDeleteQueue);
            queueToolbar.Append(btnStartQueue);
            queueToolbar.Append(btnStopQueue);
            leftBox.Append(queueToolbar);

            paned.StartChild = leftBox;

            // --- Right Pane: Notebook (Downloads & Scheduler) ---
            var notebook = Notebook.New();
            notebook.Vexpand = true;
            notebook.Hexpand = true;
            notebook.MarginTop = 8;
            notebook.MarginBottom = 8;
            notebook.MarginStart = 8;
            notebook.MarginEnd = 12;

            // Tab 1: Queue Items
            var downloadsTab = Box.New(Orientation.Vertical, 8);
            downloadsTab.MarginTop = 8;
            downloadsTab.MarginStart = 8;
            downloadsTab.MarginEnd = 8;

            var downloadsToolbar = Box.New(Orientation.Horizontal, 6);
            btnMoveUp = Button.NewWithLabel(TextResource.GetText("BTN_MOVE_UP") ?? "Move Up");
            btnMoveUp.OnClicked += (_, _) => OnMoveUpClicked();

            btnMoveDown = Button.NewWithLabel(TextResource.GetText("BTN_MOVE_DOWN") ?? "Move Down");
            btnMoveDown.OnClicked += (_, _) => OnMoveDownClicked();

            btnAddDownloads = Button.NewWithLabel(TextResource.GetText("BTN_ADD_SHORT") ?? "+ Add...");
            btnAddDownloads.OnClicked += (_, _) => OnAddDownloadsClicked();

            btnRemoveDownloads = Button.NewWithLabel(TextResource.GetText("BTN_REMOVE") ?? "Remove");
            btnRemoveDownloads.OnClicked += (_, _) => OnRemoveDownloadsClicked();

            btnMoveToQueue = Button.NewWithLabel(TextResource.GetText("BTN_MOVE_TO") ?? "Move To...");
            btnMoveToQueue.OnClicked += (_, _) => OnMoveToQueueClicked();

            downloadsToolbar.Append(btnMoveUp);
            downloadsToolbar.Append(btnMoveDown);
            downloadsToolbar.Append(btnAddDownloads);
            downloadsToolbar.Append(btnRemoveDownloads);
            downloadsToolbar.Append(btnMoveToQueue);
            downloadsTab.Append(downloadsToolbar);

            var downloadsScroll = ScrolledWindow.New();
            downloadsScroll.Vexpand = true;
            downloadsScroll.Hexpand = true;

            downloadsListBox = ListBox.New();
            downloadsListBox.SelectionMode = SelectionMode.Single;
            downloadsListBox.AddCssClass("rich-list");
            downloadsScroll.SetChild(downloadsListBox);
            downloadsTab.Append(downloadsScroll);

            notebook.AppendPage(downloadsTab, Label.New(TextResource.GetText("LBL_DOWNLOADS") ?? "Downloads"));

            // Tab 2: Scheduler
            var schedulerTab = Box.New(Orientation.Vertical, 12);
            schedulerTab.MarginTop = 12;
            schedulerTab.MarginStart = 12;
            schedulerTab.MarginEnd = 12;

            chkEnableScheduler = CheckButton.NewWithLabel(TextResource.GetText("LBL_ENABLE_SCHEDULER") ?? "Enable Scheduler for this queue");
            chkEnableScheduler.OnToggled += (_, _) => OnEnableSchedulerToggled();
            schedulerTab.Append(chkEnableScheduler);

            schedulerContainer = Box.New(Orientation.Vertical, 12);
            schedulerContainer.Sensitive = false;

            // Days selection
            var daysHeader = Label.New(TextResource.GetText("LBL_ACTIVE_DAYS") ?? "Active Days:");
            daysHeader.Halign = Align.Start;
            daysHeader.AddCssClass("heading");
            schedulerContainer.Append(daysHeader);

            chkEveryday = CheckButton.NewWithLabel(TextResource.GetText("LBL_EVERY_DAY") ?? "Every day");
            chkEveryday.OnToggled += (_, _) => OnEverydayToggled();
            schedulerContainer.Append(chkEveryday);

            var daysBox = Box.New(Orientation.Horizontal, 12);
            var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            dayChecks = new CheckButton[7];
            for (var i = 0; i < 7; i++)
            {
                dayChecks[i] = CheckButton.NewWithLabel(dayNames[i]);
                dayChecks[i].OnToggled += (_, _) => OnDayCheckToggled();
                daysBox.Append(dayChecks[i]);
            }
            schedulerContainer.Append(daysBox);

            // Time controls
            var hours = Enumerable.Range(1, 12).Select(h => h.ToString()).ToArray();
            var minutes = Enumerable.Range(0, 60).Select(m => m.ToString("00")).ToArray();
            var ampm = new[] { "AM", "PM" };

            var startBox = Box.New(Orientation.Horizontal, 8);
            var lblStart = Label.New(TextResource.GetText("Q_START_TIME") ?? "Start Time:");
            lblStart.WidthRequest = 100;
            lblStart.Halign = Align.Start;

            cmbStartHour = DropDown.NewFromStrings(hours);
            cmbStartMin = DropDown.NewFromStrings(minutes);
            cmbStartAmPm = DropDown.NewFromStrings(ampm);
            cmbStartHour.OnNotify += (_, args) => { if (args.Pspec.GetName() == "selected") OnScheduleTimeChanged(); };
            cmbStartMin.OnNotify += (_, args) => { if (args.Pspec.GetName() == "selected") OnScheduleTimeChanged(); };
            cmbStartAmPm.OnNotify += (_, args) => { if (args.Pspec.GetName() == "selected") OnScheduleTimeChanged(); };

            startBox.Append(lblStart);
            startBox.Append(cmbStartHour);
            startBox.Append(Label.New(":"));
            startBox.Append(cmbStartMin);
            startBox.Append(cmbStartAmPm);
            schedulerContainer.Append(startBox);

            var stopBox = Box.New(Orientation.Horizontal, 8);
            var lblStop = Label.New(TextResource.GetText("Q_STOP_TIME") ?? "Stop Time:");
            lblStop.WidthRequest = 100;
            lblStop.Halign = Align.Start;

            cmbStopHour = DropDown.NewFromStrings(hours);
            cmbStopMin = DropDown.NewFromStrings(minutes);
            cmbStopAmPm = DropDown.NewFromStrings(ampm);
            cmbStopHour.OnNotify += (_, args) => { if (args.Pspec.GetName() == "selected") OnScheduleTimeChanged(); };
            cmbStopMin.OnNotify += (_, args) => { if (args.Pspec.GetName() == "selected") OnScheduleTimeChanged(); };
            cmbStopAmPm.OnNotify += (_, args) => { if (args.Pspec.GetName() == "selected") OnScheduleTimeChanged(); };

            stopBox.Append(lblStop);
            stopBox.Append(cmbStopHour);
            stopBox.Append(Label.New(":"));
            stopBox.Append(cmbStopMin);
            stopBox.Append(cmbStopAmPm);
            schedulerContainer.Append(stopBox);

            schedulerTab.Append(schedulerContainer);
            notebook.AppendPage(schedulerTab, Label.New(TextResource.GetText("LBL_SCHEDULER") ?? "Scheduler"));

            paned.EndChild = notebook;
            rootBox.Append(paned);

            // --- Footer: Save and Close ---
            var footer = Box.New(Orientation.Horizontal, 8);
            footer.Halign = Align.End;
            footer.MarginTop = 8;
            footer.MarginBottom = 12;
            footer.MarginStart = 16;
            footer.MarginEnd = 16;

            var btnClose = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Close");
            btnClose.OnClicked += (_, _) => window.Close();

            var btnSave = Button.NewWithLabel(TextResource.GetText("MSG_OK") ?? "Save");
            btnSave.AddCssClass("suggested-action");
            btnSave.OnClicked += (_, _) => OnSaveClicked();

            footer.Append(btnClose);
            footer.Append(btnSave);
            rootBox.Append(footer);

            window.SetChild(rootBox);
        }

        // Populates the dialog with data from core QueueManager
        public void SetData(IEnumerable<DownloadQueue> initialQueues)
        {
            queues.Clear();
            queues.AddRange(initialQueues);
            RenderQueues();
            if (queues.Count > 0)
            {
                SelectQueue(queues[0]);
            }
        }

        // Refreshes displayed queue items and status
        public void RefreshView()
        {
            if (selectedQueue != null)
            {
                RenderQueueDownloads(selectedQueue);
            }
        }

        // Renders all queue entries into the left sidebar
        private void RenderQueues()
        {
            // Clear existing rows
            while (queueListBox.GetFirstChild() is Widget child)
            {
                queueListBox.Remove(child);
            }

            foreach (var q in queues)
            {
                var row = ListBoxRow.New();
                var label = Label.New(q.Name);
                label.Halign = Align.Start;
                label.MarginTop = 8;
                label.MarginBottom = 8;
                label.MarginStart = 12;
                label.MarginEnd = 12;
                row.SetChild(label);
                queueListBox.Append(row);
            }
        }

        // Selects a specific queue
        private void SelectQueue(DownloadQueue queue)
        {
            selectedQueue = queue;
            RenderQueueDownloads(queue);
            LoadScheduleData(queue);
        }

        // Handles queue row selection
        private void OnQueueRowSelected(ListBoxRow? row)
        {
            if (row == null) return;
            var index = row.GetIndex();
            if (index >= 0 && index < queues.Count)
            {
                SelectQueue(queues[index]);
            }
        }

        // Renders downloads for the active queue
        private void RenderQueueDownloads(DownloadQueue queue)
        {
            while (downloadsListBox.GetFirstChild() is Widget child)
            {
                downloadsListBox.Remove(child);
            }
            currentDownloadRows.Clear();

            foreach (var id in queue.DownloadIds)
            {
                var ent = ApplicationContext.Application?.GetInProgressDownloadEntry(id);
                if (ent == null) continue;

                var row = ListBoxRow.New();
                var rowBox = Box.New(Orientation.Horizontal, 8);
                rowBox.MarginTop = 6;
                rowBox.MarginBottom = 6;
                rowBox.MarginStart = 10;
                rowBox.MarginEnd = 10;

                var lblName = Label.New(ent.Name);
                lblName.Halign = Align.Start;
                lblName.Hexpand = true;
                lblName.Ellipsize = Pango.EllipsizeMode.End;

                var lblSize = Label.New(FormattingHelper.FormatSize(ent.Size));
                lblSize.WidthRequest = 90;
                lblSize.Halign = Align.End;
                lblSize.AddCssClass("dim-label");

                var lblStatus = Label.New(ent.Status.ToString());
                lblStatus.WidthRequest = 100;
                lblStatus.Halign = Align.End;
                lblStatus.AddCssClass("dim-label");

                rowBox.Append(lblName);
                rowBox.Append(lblSize);
                rowBox.Append(lblStatus);
                row.SetChild(rowBox);

                downloadsListBox.Append(row);
                currentDownloadRows.Add((row, ent));
            }
        }

        // Loads schedule into Tab 2
        private void LoadScheduleData(DownloadQueue queue)
        {
            isSuppressingEvents = true;
            try
            {
                if (queue.Schedule.HasValue)
                {
                    chkEnableScheduler.Active = true;
                    schedulerContainer.Sensitive = true;
                    var schedule = queue.Schedule.Value;

                    dayChecks[0].Active = (schedule.Days & WeekDays.Sun) != 0;
                    dayChecks[1].Active = (schedule.Days & WeekDays.Mon) != 0;
                    dayChecks[2].Active = (schedule.Days & WeekDays.Tue) != 0;
                    dayChecks[3].Active = (schedule.Days & WeekDays.Wed) != 0;
                    dayChecks[4].Active = (schedule.Days & WeekDays.Thu) != 0;
                    dayChecks[5].Active = (schedule.Days & WeekDays.Fri) != 0;
                    dayChecks[6].Active = (schedule.Days & WeekDays.Sat) != 0;
                    chkEveryday.Active = dayChecks.All(d => d.Active);

                    TimeHelper.ConvertH24ToH12(schedule.StartTime, out var sh, out var sm, out var sam);
                    cmbStartHour.SetSelected((uint)Math.Clamp(sh - 1, 0, 11));
                    cmbStartMin.SetSelected((uint)Math.Clamp(sm, 0, 59));
                    cmbStartAmPm.SetSelected(sam ? 0u : 1u);

                    TimeHelper.ConvertH24ToH12(schedule.EndTime, out var eh, out var em, out var eam);
                    cmbStopHour.SetSelected((uint)Math.Clamp(eh - 1, 0, 11));
                    cmbStopMin.SetSelected((uint)Math.Clamp(em, 0, 59));
                    cmbStopAmPm.SetSelected(eam ? 0u : 1u);
                }
                else
                {
                    chkEnableScheduler.Active = false;
                    schedulerContainer.Sensitive = false;
                    chkEveryday.Active = false;
                    foreach (var d in dayChecks) d.Active = false;
                }
            }
            finally
            {
                isSuppressingEvents = false;
            }
        }

        // Toggles scheduler container sensitivity
        private void OnEnableSchedulerToggled()
        {
            if (isSuppressingEvents || selectedQueue == null) return;
            schedulerContainer.Sensitive = chkEnableScheduler.Active;
            if (!chkEnableScheduler.Active)
            {
                selectedQueue.Schedule = null;
            }
            else
            {
                UpdateScheduleFromControls();
            }
        }

        // Handles everyday check toggle
        private void OnEverydayToggled()
        {
            if (isSuppressingEvents) return;
            isSuppressingEvents = true;
            try
            {
                var val = chkEveryday.Active;
                foreach (var d in dayChecks) d.Active = val;
            }
            finally
            {
                isSuppressingEvents = false;
            }
            UpdateScheduleFromControls();
        }

        // Handles individual day check toggle
        private void OnDayCheckToggled()
        {
            if (isSuppressingEvents) return;
            isSuppressingEvents = true;
            try
            {
                chkEveryday.Active = dayChecks.All(d => d.Active);
            }
            finally
            {
                isSuppressingEvents = false;
            }
            UpdateScheduleFromControls();
        }

        // Handles schedule time dropdown changes
        private void OnScheduleTimeChanged()
        {
            if (isSuppressingEvents) return;
            UpdateScheduleFromControls();
        }

        // Reads UI state into selected queue's schedule
        private void UpdateScheduleFromControls()
        {
            if (selectedQueue == null || !chkEnableScheduler.Active) return;

            var days = WeekDays.None;
            if (dayChecks[0].Active) days |= WeekDays.Sun;
            if (dayChecks[1].Active) days |= WeekDays.Mon;
            if (dayChecks[2].Active) days |= WeekDays.Tue;
            if (dayChecks[3].Active) days |= WeekDays.Wed;
            if (dayChecks[4].Active) days |= WeekDays.Thu;
            if (dayChecks[5].Active) days |= WeekDays.Fri;
            if (dayChecks[6].Active) days |= WeekDays.Sat;

            var sh = (int)cmbStartHour.GetSelected() + 1;
            var sm = (int)cmbStartMin.GetSelected();
            var sam = cmbStartAmPm.GetSelected() == 0;
            var startTime = TimeHelper.ConvertH12ToH24(sh, sm, sam);

            var eh = (int)cmbStopHour.GetSelected() + 1;
            var em = (int)cmbStopMin.GetSelected();
            var eam = cmbStopAmPm.GetSelected() == 0;
            var endTime = TimeHelper.ConvertH12ToH24(eh, em, eam);

            selectedQueue.Schedule = new DownloadSchedule
            {
                Days = days,
                StartTime = startTime,
                EndTime = endTime
            };
        }

        // Moves selected download item up
        private void OnMoveUpClicked()
        {
            if (selectedQueue == null) return;
            var selectedRow = downloadsListBox.GetSelectedRow();
            if (selectedRow == null) return;
            var index = selectedRow.GetIndex();
            if (index > 0 && index < selectedQueue.DownloadIds.Count)
            {
                var id = selectedQueue.DownloadIds[index];
                selectedQueue.DownloadIds.RemoveAt(index);
                selectedQueue.DownloadIds.Insert(index - 1, id);
                RenderQueueDownloads(selectedQueue);
            }
        }

        // Moves selected download item down
        private void OnMoveDownClicked()
        {
            if (selectedQueue == null) return;
            var selectedRow = downloadsListBox.GetSelectedRow();
            if (selectedRow == null) return;
            var index = selectedRow.GetIndex();
            if (index >= 0 && index < selectedQueue.DownloadIds.Count - 1)
            {
                var id = selectedQueue.DownloadIds[index];
                selectedQueue.DownloadIds.RemoveAt(index);
                selectedQueue.DownloadIds.Insert(index + 1, id);
                RenderQueueDownloads(selectedQueue);
            }
        }

        // Opens NewQueueDialog to add more downloads to the selected queue
        private void OnAddDownloadsClicked()
        {
            if (selectedQueue == null) return;
            var dlg = new NewQueueDialog(window, (q, _) =>
            {
                RenderQueueDownloads(selectedQueue);
            }, selectedQueue);
            dlg.Show();
        }

        // Removes selected download from queue
        private void OnRemoveDownloadsClicked()
        {
            if (selectedQueue == null) return;
            var selectedRow = downloadsListBox.GetSelectedRow();
            if (selectedRow == null) return;
            var index = selectedRow.GetIndex();
            if (index >= 0 && index < selectedQueue.DownloadIds.Count)
            {
                selectedQueue.DownloadIds.RemoveAt(index);
                RenderQueueDownloads(selectedQueue);
            }
        }

        // Moves selected download to another queue
        private void OnMoveToQueueClicked()
        {
            if (selectedQueue == null || queues.Count < 2) return;
            var selectedRow = downloadsListBox.GetSelectedRow();
            if (selectedRow == null) return;
            var index = selectedRow.GetIndex();
            if (index < 0 || index >= selectedQueue.DownloadIds.Count) return;

            var downloadId = selectedQueue.DownloadIds[index];
            var otherQueues = queues.Where(q => q.ID != selectedQueue.ID).ToList();

            var dlg = new QueueSelectionDialog(window);
            dlg.SetData(otherQueues.Select(q => q.Name), otherQueues.Select(q => q.ID), new[] { downloadId });
            dlg.QueueSelected += (_, args) =>
            {
                var targetQueue = queues.FirstOrDefault(q => q.ID == args.SelectedQueueId);
                if (targetQueue != null)
                {
                    selectedQueue.DownloadIds.Remove(downloadId);
                    targetQueue.DownloadIds.Add(downloadId);
                    RenderQueueDownloads(selectedQueue);
                }
            };
            dlg.ShowWindow();
        }

        // Creates a new queue
        private void OnNewQueueClicked()
        {
            var dlg = new NewQueueDialog(window, (newQueue, isNew) =>
            {
                queues.Add(newQueue);
                RenderQueues();
                SelectQueue(newQueue);
            });
            dlg.Show();
        }

        // Deletes currently selected queue
        private void OnDeleteQueueClicked()
        {
            if (selectedQueue == null || queues.Count <= 1) return;
            var index = queues.IndexOf(selectedQueue);
            if (index >= 0)
            {
                queues.RemoveAt(index);
                RenderQueues();
                if (queues.Count > 0)
                {
                    SelectQueue(queues[Math.Min(index, queues.Count - 1)]);
                }
            }
        }

        // Starts all downloads in active queue
        private void OnStartQueueClicked()
        {
            if (selectedQueue == null) return;
            OnSaveClicked();
            QueueStartRequested?.Invoke(this, new DownloadListEventArgs(selectedQueue.DownloadIds));
        }

        // Stops all downloads in active queue
        private void OnStopQueueClicked()
        {
            if (selectedQueue == null) return;
            OnSaveClicked();
            QueueStopRequested?.Invoke(this, new DownloadListEventArgs(selectedQueue.DownloadIds));
        }

        // Saves modified queues and closes the window
        private void OnSaveClicked()
        {
            UpdateScheduleFromControls();
            QueuesModified?.Invoke(this, new QueueListEventArgs(queues));
            window.Close();
        }

        // Presents the queue and scheduler window
        public void ShowWindow(object window) => this.window.Present();
    }
}
