// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 media grabber: live list of streams the browser extension captured.
// Mirrors the GTK 3 window (list, download selected, clear, always-on-top, how-to)
// and follows VideoTracker's events so a capture appears without reopening.
using System;
using System.Collections.Generic;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.BrowserMonitoring;
using XDM.Core.Util;
using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    internal sealed class MediaGrabberWindow
    {
        // Native window and its list
        private readonly Window window;
        private readonly ListBox list;
        private readonly Label statusLabel;
        private readonly Dictionary<string, ListBoxRow> rows = new(StringComparer.Ordinal);
        // Tracker supplying the captured media (null when the core is not ready)
        private IVideoTracker? tracker;

        // Builds the window and subscribes to tracker events
        public MediaGrabberWindow(Window? parent)
        {
            window = Window.New();
            window.Title = TextResource.GetText("MSG_MEDIA_CAPTURE") ?? "Media Grabber";
            window.SetDefaultSize(720, 460);
            if (parent != null)
            {
                window.SetTransientFor(parent);
            }

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            var closeBtn = Button.NewWithLabel(TextResource.GetText("MSG_CLOSE") ?? "Close");
            closeBtn.OnClicked += (_, _) => window.Close();
            header.PackEnd(closeBtn);
            window.SetTitlebar(header);

            var box = Box.New(Orientation.Vertical, 8);
            box.MarginTop = 10;
            box.MarginBottom = 10;
            box.MarginStart = 14;
            box.MarginEnd = 14;

            // Captured media list (single selection drives Download)
            list = ListBox.New();
            list.SelectionMode = SelectionMode.Single;
            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            scrolled.SetChild(list);
            scrolled.Vexpand = true;
            box.Append(scrolled);

            statusLabel = Label.New("");
            statusLabel.Halign = Align.Start;
            statusLabel.AddCssClass("dim-label");
            box.Append(statusLabel);

            // Actions
            var actions = Box.New(Orientation.Horizontal, 8);
            actions.MarginTop = 4;

            // GTK 4 removed keep-above window hints, so there is no "Always on top"
            // control here (FF-UI-039 records the platform limitation).

            var howTo = Button.NewWithLabel(TextResource.GetText("MSG_HOW_TO_USE_MG") ?? "How to download streaming video");
            howTo.AddCssClass("flat");
            howTo.OnClicked += (_, _) => PlatformHelper.OpenBrowser(Links.VideoDownloadTutorialUrl);
            actions.Append(howTo);

            var spacer = Box.New(Orientation.Horizontal, 0);
            spacer.Hexpand = true;
            actions.Append(spacer);

            var clearBtn = Button.NewWithLabel(TextResource.GetText("MSG_CLEAR") ?? "Clear");
            clearBtn.OnClicked += (_, _) => UiGuard.Run(window, "Clear captured media", Clear);
            actions.Append(clearBtn);

            var downloadBtn = Button.NewWithLabel(TextResource.GetText("MSG_DOWNLOAD") ?? "Download");
            downloadBtn.AddCssClass("suggested-action");
            downloadBtn.OnClicked += (_, _) => UiGuard.Run(window, "Download captured media", DownloadSelected);
            actions.Append(downloadBtn);

            box.Append(actions);
            window.SetChild(box);

            tracker = SafeTracker();
            if (tracker != null)
            {
                tracker.MediaAdded += OnMediaAdded;
                tracker.MediaUpdated += OnMediaUpdated;
                tracker.MediaFetchStarted += OnFetchStarted;
                tracker.MediaFetchCompleted += OnFetchCompleted;
                foreach (var media in tracker.GetVideoList())
                {
                    AddRow(media);
                }
            }

            window.OnCloseRequest += (_, _) =>
            {
                Unsubscribe();
                return false;
            };
        }

        // Presents the window
        public void Show() => window.Present();

        // Resolves the tracker without throwing when the core has not registered it
        private static IVideoTracker? SafeTracker()
        {
            try
            {
                return ApplicationContext.VideoTracker;
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Media grabber: tracker unavailable");
                return null;
            }
        }

        // Adds a captured media row (title + dim description)
        private void AddRow(MediaInfo media)
        {
            if (string.IsNullOrEmpty(media?.ID) || rows.ContainsKey(media.ID))
            {
                return;
            }

            var rowBox = Box.New(Orientation.Horizontal, 10);
            rowBox.MarginTop = 6;
            rowBox.MarginBottom = 6;
            rowBox.MarginStart = 8;
            rowBox.MarginEnd = 8;

            var icon = Gtk4IconHelper.CreateTintedIcon("movie-line", 239, 68, 68, 18);
            icon.Valign = Align.Center;
            rowBox.Append(icon);

            var textBox = Box.New(Orientation.Vertical, 2);
            textBox.Hexpand = true;
            var name = Label.New(media.Name ?? "");
            name.Halign = Align.Start;
            name.Ellipsize = Pango.EllipsizeMode.End;
            var description = Label.New(media.Description ?? "");
            description.Halign = Align.Start;
            description.Ellipsize = Pango.EllipsizeMode.End;
            description.AddCssClass("dim-label");
            textBox.Append(name);
            textBox.Append(description);
            rowBox.Append(textBox);

            var row = ListBoxRow.New();
            row.SetChild(rowBox);
            row.Name = media.ID;
            row.TooltipText = media.Description ?? media.Name ?? "";
            list.Append(row);
            rows[media.ID] = row;
        }

        // Starts downloading the selected capture
        private void DownloadSelected()
        {
            var id = (list.GetSelectedRow() as ListBoxRow)?.Name;
            if (string.IsNullOrEmpty(id) || tracker == null)
            {
                statusLabel.SetText(TextResource.GetText("BAT_SELECT_ITEMS") ?? "Select an item first.");
                return;
            }

            try
            {
                tracker.AddVideoDownload(id);
                statusLabel.SetText(TextResource.GetText("MSG_MEDIA_DOWNLOAD_STARTED")
                    ?? "Download started. It appears in the download list.");
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Media grabber download failed");
                statusLabel.SetText(ex.Message);
            }
        }

        // Clears the captured list in the tracker and the window
        private void Clear()
        {
            tracker?.ClearVideoList();
            rows.Clear();
            Widget? child;
            while ((child = list.GetFirstChild()) != null)
            {
                list.Remove(child);
            }
            statusLabel.SetText("");
        }

        // Tracker events arrive on worker threads
        private void OnMediaAdded(object? sender, MediaInfoEventArgs e) =>
            Post(() => AddRow(e.MediaInfo));

        // A capture's title can improve once the page title is known
        private void OnMediaUpdated(object? sender, MediaInfoEventArgs e) =>
            Post(() =>
            {
                if (!rows.TryGetValue(e.MediaInfo?.ID ?? "", out var row) ||
                    row.GetChild() is not Box box ||
                    box.GetLastChild() is not Box textBox)
                {
                    return;
                }
                if (textBox.GetFirstChild() is Label name)
                {
                    name.SetText(e.MediaInfo!.Name ?? "");
                }
            });

        // yt-dlp is resolving formats for a capture
        private void OnFetchStarted(object? sender, string url) =>
            Post(() => statusLabel.SetText(TextResource.GetText("MSG_MEDIA_FETCHING") ?? "Fetching formats..."));

        // Format resolution finished
        private void OnFetchCompleted(object? sender, string url) =>
            Post(() => statusLabel.SetText(""));

        // Detaches tracker handlers so a closed window is not kept alive
        private void Unsubscribe()
        {
            if (tracker == null)
            {
                return;
            }
            tracker.MediaAdded -= OnMediaAdded;
            tracker.MediaUpdated -= OnMediaUpdated;
            tracker.MediaFetchStarted -= OnFetchStarted;
            tracker.MediaFetchCompleted -= OnFetchCompleted;
            tracker = null;
        }

        // Marshals a tracker callback onto the GTK main loop
        private static void Post(Action action)
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                try { action(); }
                catch (Exception ex) { TraceLog.Log.Debug(ex, "Media grabber update failed"); }
                return false;
            });
        }
    }
}
