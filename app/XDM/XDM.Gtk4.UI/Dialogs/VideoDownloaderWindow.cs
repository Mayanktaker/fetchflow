// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 media stream downloader (yt-dlp). Implements IVideoDownloadView, so the
// shared VideoDownloaderUIController owns validation, the yt-dlp process, format
// selection and download start — this file is only the four-page presentation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.UI;
using XDM.Core.Util;

using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    internal sealed class VideoDownloaderWindow : IVideoDownloadView
    {
        // Stack page names
        private const string PageInitial = "initial";
        private const string PageProcessing = "processing";
        private const string PageFinal = "final";
        private const string PageError = "error";

        // Native window and its pages
        private readonly Window window;
        private readonly Stack pages;
        private readonly Spinner spinner;

        // Initial page controls
        private readonly Entry urlEntry;
        private readonly Entry folderEntry;
        private readonly Switch cookieSwitch;
        private readonly DropDown browserDrop;
        private readonly Entry userEntry;
        private readonly Entry passEntry;

        // Final page controls
        private readonly ListBox videoList;
        private readonly DropDown formatDrop;
        private readonly CheckButton selectAll;
        private readonly List<string> videos = new();
        private readonly List<CheckButton> videoChecks = new();
        private readonly List<string> formats = new();

        // Browser names supplied by the controller, in dropdown order
        private List<string> allowedBrowsers = new();
        // Credentials/proxy state kept for the shared controller
        private AuthenticationInfo? authentication;
        private ProxyInfo? proxy = Config.Instance.Proxy;

        public event EventHandler? CancelClicked;
        public event EventHandler? BrowseClicked;
        public event EventHandler? SearchClicked;
        public event EventHandler? DownloadClicked;
        public event EventHandler? QueueSchedulerClicked;
        public event EventHandler<DownloadLaterEventArgs>? DownloadLaterClicked;
        public event EventHandler? WindowClosed;

        public string DownloadLocation
        {
            get => folderEntry.GetText() ?? "";
            set => folderEntry.SetText(value ?? "");
        }

        public string Url
        {
            get => urlEntry.GetText() ?? "";
            set => urlEntry.SetText(value ?? "");
        }

        // Only report a browser when the user asked to reuse its cookies
        public string? SelectedBrowser =>
            cookieSwitch.Active && browserDrop.Selected != uint.MaxValue && allowedBrowsers.Count > 0
                ? allowedBrowsers[(int)browserDrop.Selected]
                : null;

        public List<string> AllowedBrowsers
        {
            set
            {
                allowedBrowsers = value ?? new List<string>();
                browserDrop.SetModel(StringList.New(allowedBrowsers.ToArray()));
                if (allowedBrowsers.Count > 0)
                {
                    browserDrop.Selected = 0;
                }
            }
        }

        public AuthenticationInfo? Authentication
        {
            get
            {
                var user = userEntry.GetText() ?? "";
                var pass = passEntry.GetText() ?? "";
                if (!string.IsNullOrEmpty(user) || !string.IsNullOrEmpty(pass))
                {
                    return new AuthenticationInfo { UserName = user, Password = pass };
                }
                return authentication;
            }
            set
            {
                authentication = value;
                userEntry.SetText(value?.UserName ?? "");
                passEntry.SetText(value?.Password ?? "");
            }
        }

        public ProxyInfo? Proxy
        {
            get => proxy;
            set => proxy = value;
        }

        public int SpeedLimit { get; set; } = Config.Instance.DefaltDownloadSpeed;
        public bool EnableSpeedLimit { get; set; } = Config.Instance.EnableSpeedLimit;

        // Format dropdown index; -1 when no formats are loaded
        public int SelectedFormat
        {
            get => formatDrop.Selected == uint.MaxValue ? -1 : (int)formatDrop.Selected;
            set
            {
                if (formats.Count == 0)
                {
                    return;
                }
                formatDrop.Selected = (uint)Math.Clamp(value, 0, formats.Count - 1);
            }
        }

        // Indices of the ticked videos, in display order
        public IEnumerable<int> SelectedRows
        {
            get
            {
                for (var i = 0; i < videoChecks.Count; i++)
                {
                    if (videoChecks[i].Active)
                    {
                        yield return i;
                    }
                }
            }
        }

        public int SelectedItemCount => videoChecks.Count(check => check.Active);

        // Builds the window shell and its four pages
        public VideoDownloaderWindow(Window? parent)
        {
            window = Window.New();
            window.Title = TextResource.GetText("LBL_VIDEO_DOWNLOAD") ?? "Video Download";
            window.Modal = false;
            window.SetDefaultSize(720, 520);
            if (parent != null)
            {
                window.SetTransientFor(parent);
            }

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            var cancelBtn = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            cancelBtn.OnClicked += (_, _) => CancelClicked?.Invoke(this, EventArgs.Empty);
            header.PackEnd(cancelBtn);
            window.SetTitlebar(header);

            urlEntry = Entry.New();
            folderEntry = Entry.New();
            cookieSwitch = Switch.New();
            browserDrop = DropDown.NewFromStrings(Array.Empty<string>());
            browserDrop.Sensitive = false;
            userEntry = Entry.New();
            passEntry = Entry.New();
            passEntry.Visibility = false;

            videoList = ListBox.New();
            videoList.SelectionMode = SelectionMode.None;
            formatDrop = DropDown.NewFromStrings(Array.Empty<string>());
            selectAll = CheckButton.NewWithLabel(TextResource.GetText("LBL_SELECT_ALL") ?? "Select All");
            spinner = Spinner.New();

            cookieSwitch.OnStateSet += (_, args) =>
            {
                browserDrop.Sensitive = args.State;
                return false;
            };
            selectAll.OnToggled += (_, _) =>
            {
                foreach (var check in videoChecks)
                {
                    check.Active = selectAll.Active;
                }
            };

            pages = Stack.New();
            pages.AddNamed(BuildInitialPage(), PageInitial);
            pages.AddNamed(BuildProcessingPage(), PageProcessing);
            pages.AddNamed(BuildFinalPage(), PageFinal);
            pages.AddNamed(BuildErrorPage(), PageError);
            pages.VisibleChildName = PageInitial;

            window.SetChild(pages);
            window.OnCloseRequest += (_, _) =>
            {
                WindowClosed?.Invoke(this, EventArgs.Empty);
                return false;
            };
        }

        // Page 1: URL, optional browser cookies, destination, optional credentials
        private Widget BuildInitialPage()
        {
            var box = Box.New(Orientation.Vertical, 10);
            box.MarginTop = 16;
            box.MarginBottom = 16;
            box.MarginStart = 20;
            box.MarginEnd = 20;

            var intro = Label.New(TextResource.GetText("VID_PASTE_URL") ?? "Paste the video page URL:");
            intro.Halign = Align.Start;
            box.Append(intro);

            urlEntry.PlaceholderText = "https://";
            urlEntry.ActivatesDefault = true;
            urlEntry.OnActivate += (_, _) => SearchClicked?.Invoke(this, EventArgs.Empty);
            box.Append(urlEntry);

            // Cookies from the user's browser (yt-dlp --cookies-from-browser)
            var cookieRow = Box.New(Orientation.Horizontal, 10);
            cookieRow.Append(Label.New(TextResource.GetText("LBL_USE_COOKIES") ?? "Use cookies from a browser"));
            cookieSwitch.Hexpand = true;
            cookieSwitch.Halign = Align.End;
            cookieRow.Append(cookieSwitch);
            cookieRow.Append(browserDrop);
            box.Append(cookieRow);

            // Destination folder
            var folderRow = Box.New(Orientation.Horizontal, 8);
            folderRow.Append(Label.New(TextResource.GetText("LBL_SAVE_IN") ?? "Save in"));
            folderEntry.Hexpand = true;
            folderRow.Append(folderEntry);
            var browseBtn = Button.NewWithLabel(TextResource.GetText("CTX_BROWSE") ?? "Browse...");
            browseBtn.OnClicked += (_, _) =>
            {
                BrowseClicked?.Invoke(this, EventArgs.Empty);
                folderEntry.SetText(DownloadLocation);
            };
            folderRow.Append(browseBtn);
            box.Append(folderRow);

            // Optional HTTP credentials for restricted media
            var userRow = Box.New(Orientation.Horizontal, 10);
            userRow.Append(Label.New(TextResource.GetText("LBL_CRED_USER") ?? "User name"));
            userEntry.Hexpand = true;
            userRow.Append(userEntry);
            box.Append(userRow);

            var passRow = Box.New(Orientation.Horizontal, 10);
            passRow.Append(Label.New(TextResource.GetText("LBL_CRED_PASS") ?? "Password"));
            passEntry.Hexpand = true;
            passRow.Append(passEntry);
            box.Append(passRow);

            var actions = Box.New(Orientation.Horizontal, 8);
            actions.Halign = Align.End;
            actions.MarginTop = 8;
            var searchBtn = Button.NewWithLabel(TextResource.GetText("LBL_SEARCH_BUTTON") ?? "Fetch Links");
            searchBtn.AddCssClass("suggested-action");
            searchBtn.OnClicked += (_, _) => SearchClicked?.Invoke(this, EventArgs.Empty);
            actions.Append(searchBtn);
            box.Append(actions);

            return box;
        }

        // Page 2: yt-dlp is resolving the media
        private Widget BuildProcessingPage()
        {
            var box = Box.New(Orientation.Vertical, 12);
            box.Halign = Align.Center;
            box.Valign = Align.Center;

            spinner.Hexpand = false;
            box.Append(spinner);

            var label = Label.New(TextResource.GetText("LBL_ANALYZING") ?? "Analyzing the link...");
            label.AddCssClass("dim-label");
            box.Append(label);

            var cancel = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            cancel.Halign = Align.Center;
            cancel.OnClicked += (_, _) => CancelClicked?.Invoke(this, EventArgs.Empty);
            box.Append(cancel);

            return box;
        }

        // Page 3: resolved videos, quality choice and download actions
        private Widget BuildFinalPage()
        {
            var box = Box.New(Orientation.Vertical, 10);
            box.MarginTop = 12;
            box.MarginBottom = 12;
            box.MarginStart = 16;
            box.MarginEnd = 16;

            var scrolled = ScrolledWindow.New();
            scrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic);
            scrolled.SetChild(videoList);
            scrolled.Vexpand = true;
            box.Append(scrolled);

            var options = Box.New(Orientation.Horizontal, 10);
            options.Append(selectAll);
            var spacer = Box.New(Orientation.Horizontal, 0);
            spacer.Hexpand = true;
            options.Append(spacer);
            options.Append(Label.New(TextResource.GetText("LBL_QUALITY") ?? "Quality"));
            options.Append(formatDrop);
            box.Append(options);

            var actions = Box.New(Orientation.Horizontal, 8);
            actions.Halign = Align.End;
            actions.MarginTop = 6;

            var schedulerBtn = Button.NewWithLabel(TextResource.GetText("DESC_Q_TITLE") ?? "Queue & Scheduler");
            schedulerBtn.OnClicked += (_, _) => QueueSchedulerClicked?.Invoke(this, EventArgs.Empty);

            var laterBtn = Button.NewWithLabel(TextResource.GetText("LBL_DOWNLOAD_LATER") ?? "Download Later");
            laterBtn.OnClicked += (_, _) => UiGuard.Run(window, "Queue video", () => DownloadLaterClicked?.Invoke(this, new DownloadLaterEventArgs(null)));

            var downloadBtn = Button.NewWithLabel(TextResource.GetText("LBL_DOWNLOAD") ?? "Download");
            downloadBtn.AddCssClass("suggested-action");
            downloadBtn.OnClicked += (_, _) => UiGuard.Run(window, "Download video", () => DownloadClicked?.Invoke(this, EventArgs.Empty));

            actions.Append(schedulerBtn);
            actions.Append(laterBtn);
            actions.Append(downloadBtn);
            box.Append(actions);

            return box;
        }

        // Page 4: yt-dlp could not resolve the link
        private Widget BuildErrorPage()
        {
            var box = Box.New(Orientation.Vertical, 12);
            box.Halign = Align.Center;
            box.Valign = Align.Center;

            var label = Label.New(TextResource.GetText("MSG_VIDEO_FETCH_FAILED")
                ?? "No downloadable streams were found for this link.");
            label.Wrap = true;
            label.Justify = Justification.Center;
            box.Append(label);

            var back = Button.NewWithLabel(TextResource.GetText("LBL_BACK") ?? "Back");
            back.Halign = Align.Center;
            back.OnClicked += (_, _) => CancelClicked?.Invoke(this, EventArgs.Empty);
            box.Append(back);

            return box;
        }

        public void SwitchToInitialPage() => ShowPage(PageInitial);
        public void SwitchToProcessingPage() => ShowPage(PageProcessing);
        public void SwitchToFinalPage() => ShowPage(PageFinal);
        public void SwitchToErrorPage() => ShowPage(PageError);

        // Switches pages and runs the spinner only while resolving
        private void ShowPage(string page)
        {
            spinner.Stop();
            if (page == PageProcessing)
            {
                spinner.Start();
            }
            pages.VisibleChildName = page;
        }

        // Fills the video rows (pre-ticked, like the GTK 3 window) and the quality list
        public void SetVideoResultList(IEnumerable<string> items, IEnumerable<string> formatItems)
        {
            videos.Clear();
            videos.AddRange(items ?? Enumerable.Empty<string>());
            videoChecks.Clear();
            RemoveAllRows();

            for (var i = 0; i < videos.Count; i++)
            {
                var check = CheckButton.New();
                check.Active = true;
                var row = ListBoxRow.New();
                var rowBox = Box.New(Orientation.Horizontal, 10);
                rowBox.MarginTop = 4;
                rowBox.MarginBottom = 4;
                rowBox.MarginStart = 6;
                rowBox.MarginEnd = 6;
                rowBox.Append(check);

                var title = Label.New(videos[i]);
                title.Halign = Align.Start;
                title.Ellipsize = Pango.EllipsizeMode.End;
                title.Hexpand = true;
                rowBox.Append(title);
                row.SetChild(rowBox);

                videoList.Append(row);
                videoChecks.Add(check);
            }

            formats.Clear();
            formats.AddRange(formatItems ?? Enumerable.Empty<string>());
            formatDrop.SetModel(StringList.New(formats.ToArray()));
            if (formats.Count > 0)
            {
                formatDrop.Selected = 0;
            }

            selectAll.Active = videos.Count > 0;
        }

        // Picks the destination folder through the desktop portal
        public string? SelectFolder()
        {
            try
            {
                var dialog = FileDialog.New();
                dialog.Title = TextResource.GetText("LBL_SAVE_IN") ?? "Select Folder";
                dialog.Modal = true;
                if (!string.IsNullOrEmpty(DownloadLocation) && Directory.Exists(DownloadLocation))
                {
                    dialog.SetInitialFolder(Gio.FileHelper.NewForPath(DownloadLocation));
                }

                string? selected = null;
                var loop = GLib.MainLoop.New(null, false);
                dialog.SelectFolderAsync(window).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && t.Result != null)
                    {
                        selected = t.Result.GetPath();
                    }
                    loop.Quit();
                });
                loop.Run();
                return selected;
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "Video folder picker failed: " + ex.Message);
                return null;
            }
        }

        public void CloseWindow() => window.Close();

        public void ShowWindow() => window.Present();

        // Removes every row from the video list
        private void RemoveAllRows()
        {
            Widget? child;
            while ((child = videoList.GetFirstChild()) != null)
            {
                videoList.Remove(child);
            }
        }
    }
}
