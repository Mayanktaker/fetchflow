// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Text;
using System.Threading;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Util;
using XDM.GtkUI.Utils;
using IoPath = System.IO.Path;
using SysFileInfo = System.IO.FileInfo;
using SysDateTime = System.DateTime;
using SysTask = System.Threading.Tasks.Task;
using GtkApp = Gtk.Application;

namespace XDM.GtkUI.Dialogs.Checksum
{
    // Standalone checksum computation and verification dialog with Drag & Drop support
    public class ChecksumDialog : Dialog
    {
        private string filePath;
        private ChecksumResult? currentResult;
        private CancellationTokenSource? cts;

        private Image imgFileIcon = null!;
        private Label lblFileName = null!;
        private Label lblFileMeta = null!;
        private ProgressBar prgHash = null!;
        private Label lblStatus = null!;

        private Entry txtSha256 = null!;
        private Entry txtMd5 = null!;
        private Entry txtSha512 = null!;
        private Entry txtSha1 = null!;

        private Entry txtExpected = null!;
        private Label lblMatchBadge = null!;
        private Button btnRecalculate = null!;
        private Button btnCopyAll = null!;
        private Button btnClose = null!;

        // Initializes the Checksum verification dialog and builds UI components
        public ChecksumDialog(Window? parent, string targetPath)
        {
            filePath = targetPath;
            Modal = true;
            TransientFor = parent ?? (ApplicationContext.MainWindow as Window);
            Resizable = true;

            var titleText = TextResource.GetText("LBL_CHECKSUM_TITLE") ?? "Checksum Verification";
            Title = titleText;
            Titlebar = GtkHelper.CreateDialogHeaderBar(titleText);
            GtkHelper.SetWindowAppIcon(this);
            GtkHelper.AttachSafeDispose(this);

            SetDefaultSize(640, 520);
            SetSizeRequest(520, 440);

            BuildUI();
            SetupDragAndDrop();
            StartCalculation();
        }

        // Configures Drag and Drop file target
        private void SetupDragAndDrop()
        {
            var targets = new TargetEntry[]
            {
                new TargetEntry("text/uri-list", TargetFlags.OtherApp, 0)
            };
            Gtk.Drag.DestSet(this, DestDefaults.All, targets, Gdk.DragAction.Copy);
            DragDataReceived += ChecksumDialog_DragDataReceived;
        }

        // Handles dropped files or checksum manifests
        private void ChecksumDialog_DragDataReceived(object o, DragDataReceivedArgs args)
        {
            try
            {
                var rawData = args.SelectionData.Text;
                if (!string.IsNullOrEmpty(rawData))
                {
                    var lines = rawData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var rawUri in lines)
                    {
                        if (rawUri.StartsWith("#")) continue;
                        var target = rawUri.Trim();
                        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.IsFile)
                        {
                            target = uri.LocalPath;
                        }

                        if (System.IO.File.Exists(target))
                        {
                            HandleDroppedPath(target);
                            break;
                        }
                    }
                }
                Gtk.Drag.Finish(args.Context, true, false, args.Time);
            }
            catch
            {
                Gtk.Drag.Finish(args.Context, false, false, args.Time);
            }
        }

        // Processes a dropped path as a checksum file or new target file
        private void HandleDroppedPath(string droppedPath)
        {
            var ext = IoPath.GetExtension(droppedPath).ToLowerInvariant();
            var fileName = IoPath.GetFileName(filePath);

            if (ext == ".sha256" || ext == ".sha512" || ext == ".md5" || ext == ".sha1" || ext == ".sums" || ext == ".digest" ||
                IoPath.GetFileName(droppedPath).IndexOf("checksum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                IoPath.GetFileName(droppedPath).IndexOf("sha256sum", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (ChecksumHelper.TryExtractHashFromChecksumFile(droppedPath, fileName, out var extractedHash))
                {
                    txtExpected.Text = extractedHash;
                    lblMatchBadge.Markup = $"<span size=\"9500\" alpha=\"50000\">(Auto-extracted from {GLib.Markup.EscapeText(IoPath.GetFileName(droppedPath))})</span>";
                    ValidateComparison();
                    return;
                }
            }

            filePath = droppedPath;
            UpdateFileSummary();
            StartCalculation();
        }

        // Updates file summary card when active file changes
        private void UpdateFileSummary()
        {
            var fileName = IoPath.GetFileName(filePath);
            var svgName = IconResource.GetSVGNameForFileType(fileName);
            var (r, g, b) = IconMapHelper.GetFileTypeColor(fileName);
            var rawIcon = GtkHelper.LoadSvg(svgName, 44);
            imgFileIcon.Pixbuf = rawIcon != null ? GtkHelper.TintPixbuf(rawIcon, r, g, b) : null;

            lblFileName.Markup = $"<span weight=\"bold\" size=\"11500\">{GLib.Markup.EscapeText(fileName)}</span>";

            long fileSize = System.IO.File.Exists(filePath) ? new SysFileInfo(filePath).Length : 0;
            var friendlyPath = FormatFriendlyPath(filePath);
            lblFileMeta.Markup = $"<span size=\"9000\" alpha=\"45000\">{FormattingHelper.FormatSize(fileSize)}   ·   {GLib.Markup.EscapeText(friendlyPath)}</span>";
        }

        // Constructs the layout, cards, and input controls
        private void BuildUI()
        {
            var contentArea = ContentArea;
            contentArea.Spacing = 10;
            contentArea.MarginStart = 16;
            contentArea.MarginEnd = 16;
            contentArea.MarginTop = 14;
            contentArea.MarginBottom = 14;

            // --- Top Card: File summary (Icon, Name, Size, Path) ---
            var fileBox = new Box(Orientation.Horizontal, 14);
            fileBox.MarginBottom = 4;

            var fileName = IoPath.GetFileName(filePath);
            var svgName = IconResource.GetSVGNameForFileType(fileName);
            var (r, g, b) = IconMapHelper.GetFileTypeColor(fileName);
            var rawIcon = GtkHelper.LoadSvg(svgName, 44);
            imgFileIcon = new Image { Pixbuf = rawIcon != null ? GtkHelper.TintPixbuf(rawIcon, r, g, b) : null };
            fileBox.PackStart(imgFileIcon, false, false, 0);

            var metaVBox = new Box(Orientation.Vertical, 3);
            lblFileName = new Label { Xalign = 0, UseMarkup = true };
            lblFileName.Markup = $"<span weight=\"bold\" size=\"11500\">{GLib.Markup.EscapeText(fileName)}</span>";
            metaVBox.PackStart(lblFileName, false, false, 0);

            long fileSize = System.IO.File.Exists(filePath) ? new SysFileInfo(filePath).Length : 0;
            var friendlyPath = FormatFriendlyPath(filePath);
            lblFileMeta = new Label { Xalign = 0, UseMarkup = true };
            lblFileMeta.Markup = $"<span size=\"9000\" alpha=\"45000\">{FormattingHelper.FormatSize(fileSize)}   ·   {GLib.Markup.EscapeText(friendlyPath)}</span>";
            metaVBox.PackStart(lblFileMeta, false, false, 0);
            fileBox.PackStart(metaVBox, true, true, 0);
            contentArea.PackStart(fileBox, false, false, 0);

            // --- Progress indicator & Status label ---
            prgHash = new ProgressBar { ShowText = false, Fraction = 0.0 };
            contentArea.PackStart(prgHash, false, false, 0);

            lblStatus = new Label { Xalign = 0, UseMarkup = true };
            lblStatus.Markup = $"<span size=\"9000\" alpha=\"50000\">{TextResource.GetText("LBL_COMPUTING_HASHES") ?? "Computing checksums..."}</span>";
            contentArea.PackStart(lblStatus, false, false, 0);

            // --- Grid of Computed Hashes ---
            var hashGrid = new Grid { RowSpacing = 6, ColumnSpacing = 8 };
            hashGrid.MarginTop = 4;
            hashGrid.MarginBottom = 4;

            txtSha256 = CreateHashRow(hashGrid, 0, "SHA-256", "SHA-256");
            txtMd5 = CreateHashRow(hashGrid, 1, "MD5", "MD5");
            txtSha512 = CreateHashRow(hashGrid, 2, "SHA-512", "SHA-512");
            txtSha1 = CreateHashRow(hashGrid, 3, "SHA-1", "SHA-1");
            contentArea.PackStart(hashGrid, false, false, 0);

            // --- Real-time Compare Card ---
            var compareHeader = new Label { Xalign = 0, UseMarkup = true };
            compareHeader.Markup = $"<b>{TextResource.GetText("LBL_COMPARE_CHECKSUM") ?? "Compare with Expected Hash:"}</b>";
            compareHeader.MarginTop = 6;
            contentArea.PackStart(compareHeader, false, false, 0);

            txtExpected = new Entry
            {
                PlaceholderText = TextResource.GetText("LBL_HASH_PLACEHOLDER") ?? "Paste checksum or drop checksum file (SHA-256, MD5, SHA-512, SHA-1)..."
            };
            txtExpected.Changed += TxtExpected_Changed;
            contentArea.PackStart(txtExpected, false, false, 0);

            lblMatchBadge = new Label { Xalign = 0, UseMarkup = true };
            lblMatchBadge.Markup = "<span size=\"9500\" alpha=\"40000\">Paste a checksum or drop a checksum file to verify authenticity</span>";
            contentArea.PackStart(lblMatchBadge, false, false, 0);

            // --- Action Buttons ---
            var actionBox = new Box(Orientation.Horizontal, 8);
            actionBox.MarginTop = 10;

            btnCopyAll = new Button(TextResource.GetText("LBL_COPY_ALL_HASHES") ?? "Copy All Checksums");
            btnCopyAll.Clicked += BtnCopyAll_Clicked;
            actionBox.PackStart(btnCopyAll, false, false, 0);

            btnRecalculate = new Button(TextResource.GetText("LBL_RECALCULATE") ?? "Recalculate");
            btnRecalculate.Clicked += (_, _) => StartCalculation();
            actionBox.PackStart(btnRecalculate, false, false, 0);

            var spacer = new Box(Orientation.Horizontal, 0);
            actionBox.PackStart(spacer, true, true, 0);

            btnClose = new Button(TextResource.GetText("ND_CANCEL") ?? "Close");
            btnClose.StyleContext.AddClass("suggested-action");
            btnClose.Clicked += (_, _) => Destroy();
            actionBox.PackStart(btnClose, false, false, 0);

            contentArea.PackStart(actionBox, false, false, 0);
            InspectClipboardForHash();
            ShowAll();
        }

        // Checks clipboard for a valid hash string and pre-fills the expected hash box
        private void InspectClipboardForHash()
        {
            try
            {
                var cb = Clipboard.Get(Gdk.Selection.Clipboard);
                var text = cb.WaitForText();
                if (ChecksumHelper.IsProbableHash(text, out var cleanHash))
                {
                    txtExpected.Text = cleanHash;
                    lblMatchBadge.Markup = "<span size=\"9500\" alpha=\"50000\">(Auto-detected hash from clipboard)</span>";
                }
            }
            catch
            {
                // Non-blocking clipboard inspection
            }
        }

        // Helper to construct a label + monospace Entry + copy button row
        private Entry CreateHashRow(Grid grid, int row, string labelText, string algoName)
        {
            var lbl = new Label { Xalign = 1, WidthRequest = 75, UseMarkup = true };
            lbl.Markup = $"<b>{labelText}:</b>";
            grid.Attach(lbl, 0, row, 1, 1);

            var entry = new Entry { IsEditable = false, CanFocus = true, WidthChars = 48 };
            entry.StyleContext.AddClass("monospace");
            grid.Attach(entry, 1, row, 1, 1);

            var btnCopy = new Button();
            var copyIcon = GtkHelper.LoadSvg("file-copy-line", 16);
            if (copyIcon != null)
            {
                btnCopy.Image = new Image { Pixbuf = copyIcon };
            }
            btnCopy.TooltipText = $"Copy {algoName} to clipboard";
            btnCopy.Clicked += (sender, _) =>
            {
                if (!string.IsNullOrEmpty(entry.Text))
                {
                    var cb = Clipboard.Get(Gdk.Selection.Clipboard);
                    cb.Text = entry.Text;
                    if (sender is Button b)
                    {
                        b.TooltipText = "Copied!";
                        GLib.Timeout.Add(2000, () => { b.TooltipText = $"Copy {algoName} to clipboard"; return false; });
                    }
                }
            };
            grid.Attach(btnCopy, 2, row, 1, 1);
            return entry;
        }

        // Asynchronously calculates all hashes in background stream
        private void StartCalculation()
        {
            if (!System.IO.File.Exists(filePath))
            {
                lblStatus.Markup = "<span foreground=\"#ef4444\">File does not exist on disk</span>";
                prgHash.Visible = false;
                return;
            }

            cts?.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            prgHash.Visible = true;
            prgHash.Fraction = 0.0;
            lblStatus.Markup = $"<span size=\"9000\" alpha=\"50000\">{TextResource.GetText("LBL_COMPUTING_HASHES") ?? "Computing checksums..."}</span>";

            txtSha256.Text = "Calculating...";
            txtMd5.Text = "Calculating...";
            txtSha512.Text = "Calculating...";
            txtSha1.Text = "Calculating...";

            var startTime = SysDateTime.UtcNow;
            var progress = new Progress<double>(fraction =>
            {
                GtkApp.Invoke((_, _) =>
                {
                    prgHash.Fraction = fraction;
                });
            });

            SysTask.Run(async () =>
            {
                try
                {
                    var result = await ChecksumHelper.ComputeHashesAsync(filePath, progress, token);
                    var elapsed = (SysDateTime.UtcNow - startTime).TotalSeconds;

                    GtkApp.Invoke((_, _) =>
                    {
                        currentResult = result;
                        txtSha256.Text = result.Sha256;
                        txtMd5.Text = result.Md5;
                        txtSha512.Text = result.Sha512;
                        txtSha1.Text = result.Sha1;

                        prgHash.Fraction = 1.0;
                        prgHash.Visible = false;
                        lblStatus.Markup = $"<span foreground=\"#2ec27e\" size=\"9000\">✔ {TextResource.GetText("LBL_HASHES_COMPUTED") ?? "Checksums calculated"} in {elapsed:F2}s</span>";

                        ValidateComparison();
                    });
                }
                catch (OperationCanceledException)
                {
                    // Task canceled
                }
                catch (Exception ex)
                {
                    GtkApp.Invoke((_, _) =>
                    {
                        lblStatus.Markup = $"<span foreground=\"#ef4444\" size=\"9000\">Failed to compute: {GLib.Markup.EscapeText(ex.Message)}</span>";
                        prgHash.Visible = false;
                    });
                }
            }, token);
        }

        // Live validation handler when user inputs or pastes expected hash
        private void TxtExpected_Changed(object? sender, EventArgs e)
        {
            ValidateComparison();
        }

        // Validates expected hash against current computed result and updates UI badge
        private void ValidateComparison()
        {
            if (currentResult == null) return;

            var match = ChecksumHelper.CompareHash(txtExpected.Text, currentResult.Value);
            switch (match.Status)
            {
                case ChecksumMatchStatus.Match:
                    lblMatchBadge.Markup = $"<span foreground=\"#2ec27e\" weight=\"bold\" size=\"10500\">✔ {TextResource.GetText("LBL_HASH_MATCH") ?? "Checksum Matches"} ({match.MatchedAlgorithm})! File integrity verified.</span>";
                    txtExpected.StyleContext.RemoveClass("error-entry");
                    txtExpected.StyleContext.AddClass("success-entry");
                    break;
                case ChecksumMatchStatus.Mismatch:
                    lblMatchBadge.Markup = $"<span foreground=\"#ef4444\" weight=\"bold\" size=\"10500\">✖ {TextResource.GetText("LBL_HASH_MISMATCH") ?? "Checksum Mismatch"}! Does not match any computed hash.</span>";
                    txtExpected.StyleContext.RemoveClass("success-entry");
                    txtExpected.StyleContext.AddClass("error-entry");
                    break;
                case ChecksumMatchStatus.Empty:
                default:
                    lblMatchBadge.Markup = "<span size=\"9500\" alpha=\"40000\">Paste a checksum or drop a checksum file to verify authenticity</span>";
                    txtExpected.StyleContext.RemoveClass("success-entry");
                    txtExpected.StyleContext.RemoveClass("error-entry");
                    break;
            }
        }

        // Copies formatted report containing all 4 computed hashes to clipboard
        private void BtnCopyAll_Clicked(object? sender, EventArgs e)
        {
            if (currentResult == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"File: {IoPath.GetFileName(filePath)}");
            sb.AppendLine($"Size: {currentResult.Value.FileSizeBytes} bytes");
            sb.AppendLine($"SHA-256: {currentResult.Value.Sha256}");
            sb.AppendLine($"MD5: {currentResult.Value.Md5}");
            sb.AppendLine($"SHA-512: {currentResult.Value.Sha512}");
            sb.AppendLine($"SHA-1: {currentResult.Value.Sha1}");

            var cb = Clipboard.Get(Gdk.Selection.Clipboard);
            cb.Text = sb.ToString();

            if (sender is Button b)
            {
                b.Label = "All Copied!";
                GLib.Timeout.Add(2000, () => { b.Label = TextResource.GetText("LBL_COPY_ALL_HASHES") ?? "Copy All Checksums"; return false; });
            }
        }

        // Formats full paths with ~/ tilde syntax
        private static string FormatFriendlyPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home) && path.StartsWith(home))
            {
                return "~" + path.Substring(home.Length);
            }
            return path;
        }

        // Cleanly cancels ongoing hashing tasks on dialog close
        protected override void OnDestroyed()
        {
            cts?.Cancel();
            cts?.Dispose();
            base.OnDestroyed();
        }
    }

    // Helper to resolve category colors for dialog icons
    internal static class IconMapHelper
    {
        public static (byte R, byte G, byte B) GetFileTypeColor(string fileName)
        {
            var ext = IoPath.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
            var fileType = IconResource.GetFileType(ext);
            return fileType switch
            {
                "Video" => (239, 68, 68),
                "Music" => (168, 85, 247),
                "Document" => (245, 158, 11),
                "Compressed" => (20, 184, 166),
                "Application" or "ApplicationContext.Core" => (99, 102, 241),
                "Image" => (244, 63, 94),
                _ => (56, 189, 248)
            };
        }
    }
}
