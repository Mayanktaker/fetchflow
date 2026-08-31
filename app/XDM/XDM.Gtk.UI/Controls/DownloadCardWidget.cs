// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using Gtk;
using XDM.Core;
using XDM.Core.UI;
using XDM.Core.Util;
using XDM.GtkUI.Utils;

namespace XDM.GtkUI.Controls
{
    // Modern 2026 card component for active and completed downloads
    public class DownloadCardWidget : Frame
    {
        public InProgressDownloadItem? InProgressItem { get; private set; }
        public FinishedDownloadItem? FinishedItem { get; private set; }

        private readonly Image imgIcon;
        private readonly Label lblName;
        private readonly Label lblSize;
        private readonly Label lblPercent;
        private readonly Label lblSpeed;
        private readonly Label lblEta;
        private readonly ProgressBar prgBar;
        private readonly Button btnPauseResume;
        private readonly Button btnDelete;
        private readonly Button btnOpenFolder;
        private readonly Image imgPauseResume;

        public event EventHandler? PauseResumeClicked;
        public event EventHandler? DeleteClicked;
        public event EventHandler? OpenFolderClicked;
        public event EventHandler? CardSelected;

        public DownloadCardWidget(InProgressDownloadItem item) : this()
        {
            SetInProgress(item);
        }

        public DownloadCardWidget(FinishedDownloadItem item) : this()
        {
            SetFinished(item);
        }

        public DownloadCardWidget()
        {
            ShadowType = ShadowType.None;
            StyleContext.AddClass("download-card");
            MarginStart = 8;
            MarginEnd = 8;
            MarginTop = 6;
            MarginBottom = 6;

            var mainVBox = new VBox(false, 6)
            {
                Margin = 12
            };

            // Row 1: Icon + Name + Percentage Badge
            var headerHBox = new HBox(false, 10);

            imgIcon = new Image
            {
                Valign = Align.Center
            };
            headerHBox.PackStart(imgIcon, false, false, 0);

            var titleVBox = new VBox(false, 2);
            lblName = new Label
            {
                Xalign = 0f,
                Ellipsize = Pango.EllipsizeMode.Middle,
                MaxWidthChars = 35
            };
            lblName.StyleContext.AddClass("card-title");
            titleVBox.PackStart(lblName, false, false, 0);

            lblSize = new Label
            {
                Xalign = 0f
            };
            lblSize.StyleContext.AddClass("card-subtitle");
            titleVBox.PackStart(lblSize, false, false, 0);

            headerHBox.PackStart(titleVBox, true, true, 0);

            lblPercent = new Label { Text = "0%" };
            lblPercent.StyleContext.AddClass("card-percent-badge");
            headerHBox.PackEnd(lblPercent, false, false, 0);

            mainVBox.PackStart(headerHBox, false, false, 0);

            // Row 2: Smooth Progress Bar
            prgBar = new ProgressBar
            {
                Fraction = 0.0
            };
            prgBar.StyleContext.AddClass("card-progress");
            mainVBox.PackStart(prgBar, false, false, 2);

            // Row 3: Live Speed + ETA + Action Buttons
            var footerHBox = new HBox(false, 8);

            lblSpeed = new Label { Xalign = 0f, Text = "↓ 0 B/s" };
            lblSpeed.StyleContext.AddClass("card-metric");
            footerHBox.PackStart(lblSpeed, false, false, 0);

            lblEta = new Label { Xalign = 0f, Text = "⏱ --" };
            lblEta.StyleContext.AddClass("card-metric-secondary");
            footerHBox.PackStart(lblEta, false, false, 8);

            // Actions: Pause/Resume, Delete, Open Folder
            imgPauseResume = new Image();
            btnPauseResume = new Button(imgPauseResume)
            {
                Relief = ReliefStyle.None,
                Valign = Align.Center
            };
            btnPauseResume.StyleContext.AddClass("card-action-button");
            btnPauseResume.Clicked += (s, e) => PauseResumeClicked?.Invoke(this, EventArgs.Empty);

            var imgDel = new Image(GtkHelper.TintPixbuf(GtkHelper.LoadSvg("delete-bin-7-line", 14), 239, 68, 68));
            btnDelete = new Button(imgDel)
            {
                Relief = ReliefStyle.None,
                Valign = Align.Center
            };
            btnDelete.StyleContext.AddClass("card-action-button");
            btnDelete.Clicked += (s, e) => DeleteClicked?.Invoke(this, EventArgs.Empty);

            var imgFolder = new Image(GtkHelper.TintPixbuf(GtkHelper.LoadSvg("folder-shared-line", 14), 245, 158, 11));
            btnOpenFolder = new Button(imgFolder)
            {
                Relief = ReliefStyle.None,
                Valign = Align.Center
            };
            btnOpenFolder.StyleContext.AddClass("card-action-button");
            btnOpenFolder.Clicked += (s, e) => OpenFolderClicked?.Invoke(this, EventArgs.Empty);

            footerHBox.PackEnd(btnDelete, false, false, 0);
            footerHBox.PackEnd(btnPauseResume, false, false, 0);
            footerHBox.PackEnd(btnOpenFolder, false, false, 0);

            mainVBox.PackStart(footerHBox, false, false, 0);

            var eventBox = new EventBox();
            eventBox.Add(mainVBox);
            eventBox.ButtonPressEvent += (s, e) => CardSelected?.Invoke(this, EventArgs.Empty);

            Add(eventBox);
            ShowAll();
        }

        // Binds an active in-progress download item to the card
        public void SetInProgress(InProgressDownloadItem item)
        {
            this.InProgressItem = item;
            this.FinishedItem = null;

            lblName.Text = item.Name;
            lblSize.Text = item.Size > 0 ? FormattingHelper.FormatSize(item.Size) : "Unknown size";
            lblPercent.Text = $"{item.Progress}%";
            prgBar.Fraction = Math.Clamp(item.Progress / 100.0, 0.0, 1.0);
            prgBar.Visible = true;

            lblSpeed.Text = !string.IsNullOrEmpty(item.DownloadSpeed) ? $"↓ {item.DownloadSpeed}" : "↓ 0 B/s";
            lblEta.Text = !string.IsNullOrEmpty(item.ETA) ? $"⏱ {item.ETA}" : "⏱ --";

            UpdateCategoryIcon(item.Name);
            UpdateStatusState(item.Status);
        }

        // Binds a finished download item to the card
        public void SetFinished(FinishedDownloadItem item)
        {
            this.FinishedItem = item;
            this.InProgressItem = null;

            lblName.Text = item.Name;
            lblSize.Text = item.Size > 0 ? FormattingHelper.FormatSize(item.Size) : "Unknown size";
            lblPercent.Text = "Complete 🟢";
            lblPercent.StyleContext.AddClass("badge-complete");
            prgBar.Fraction = 1.0;
            prgBar.Visible = false;

            lblSpeed.Text = item.DateAdded.ToShortDateString();
            lblEta.Text = "";

            btnPauseResume.Visible = false;
            btnOpenFolder.Visible = true;

            UpdateCategoryIcon(item.Name);
        }

        // Updates status metrics dynamically during download
        public void UpdateProgress(int progress, string? speed, string? eta, DownloadStatus status)
        {
            lblPercent.Text = $"{progress}%";
            prgBar.Fraction = Math.Clamp(progress / 100.0, 0.0, 1.0);
            lblSpeed.Text = !string.IsNullOrEmpty(speed) ? $"↓ {speed}" : "↓ 0 B/s";
            lblEta.Text = !string.IsNullOrEmpty(eta) ? $"⏱ {eta}" : "⏱ --";
            UpdateStatusState(status);
        }

        // Applies colorful semantic category icon tint
        private void UpdateCategoryIcon(string fileName)
        {
            var svgName = IconResource.GetSVGNameForFileType(fileName);
            var (r, g, b) = GetCategoryColorForFile(fileName);
            var rawPix = GtkHelper.LoadSvg(svgName, 26) ?? GtkHelper.LoadSvg("file-line", 26);
            if (rawPix != null)
            {
                imgIcon.Pixbuf = GtkHelper.TintPixbuf(rawPix, r, g, b);
            }
        }

        // Updates pause/resume action icon and badge states
        private void UpdateStatusState(DownloadStatus status)
        {
            if (status == DownloadStatus.Downloading)
            {
                imgPauseResume.Pixbuf = GtkHelper.TintPixbuf(GtkHelper.LoadSvg("pause-line", 14), 249, 115, 22);
                btnPauseResume.TooltipText = "Pause Download";
                lblPercent.StyleContext.RemoveClass("badge-paused");
                lblPercent.StyleContext.AddClass("badge-active");
            }
            else
            {
                imgPauseResume.Pixbuf = GtkHelper.TintPixbuf(GtkHelper.LoadSvg("play-line", 14), 34, 197, 94);
                btnPauseResume.TooltipText = "Resume Download";
                lblPercent.StyleContext.RemoveClass("badge-active");
                lblPercent.StyleContext.AddClass("badge-paused");
            }
        }

        // Color palette mapped to file categories
        private static (byte r, byte g, byte b) GetCategoryColorForFile(string name)
        {
            var ext = System.IO.Path.GetExtension(name)?.ToLowerInvariant() ?? "";
            if (ext is ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a")
                return (168, 85, 247); // Purple / Music
            if (ext is ".mp4" or ".mkv" or ".webm" or ".avi" or ".mov" or ".flv")
                return (239, 68, 68); // Coral / Video
            if (ext is ".zip" or ".tar" or ".gz" or ".7z" or ".rar" or ".xz" or ".bz2")
                return (20, 184, 166); // Teal / Compressed
            if (ext is ".pdf" or ".doc" or ".docx" or ".txt" or ".epub" or ".xlsx" or ".pptx")
                return (245, 158, 11); // Amber / Documents
            if (ext is ".exe" or ".deb" or ".rpm" or ".iso" or ".AppImage" or ".dmg" or ".sh")
                return (99, 102, 241); // Indigo / Binary
            return (56, 189, 248); // Sky Blue / Default
        }
    }
}
