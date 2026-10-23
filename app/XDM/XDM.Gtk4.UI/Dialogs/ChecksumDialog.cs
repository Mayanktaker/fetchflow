// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Standalone GTK 4 Checksum verification dialog with live hashing and expected hash matching.
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.Util;

using XDM.Gtk4UI.Utils;

namespace XDM.Gtk4UI.Dialogs
{
    // Native GTK 4 modal checksum computation and verification window
    internal sealed class ChecksumDialog
    {
        private readonly Window window;
        private readonly string filePath;
        private CancellationTokenSource? cts;
        private ChecksumResult? currentResult;

        private readonly Label lblFileName;
        private readonly Label lblFileSize;
        private readonly ProgressBar prgHash;
        private readonly Label lblStatus;

        private readonly Entry txtSha256;
        private readonly Entry txtMd5;
        private readonly Entry txtSha512;
        private readonly Entry txtSha1;

        private readonly Entry txtExpected;
        private readonly Label lblMatchBadge;
        private readonly Button btnRecalculate;
        private readonly Button btnCopyAll;
        private readonly Button btnClose;

        // Builds checksum dialog UI and initiates background hash calculation
        public ChecksumDialog(Window? parent, string targetPath)
        {
            filePath = targetPath;
            window = Window.New();
            window.Title = TextResource.GetText("LBL_CHECKSUM_TITLE") ?? "Checksum Verification";
            window.Modal = true;
            window.SetDefaultSize(600, 520);
            if (parent != null) window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            window.SetTitlebar(header);

            var rootBox = Box.New(Orientation.Vertical, 12);
            rootBox.MarginTop = 16;
            rootBox.MarginBottom = 16;
            rootBox.MarginStart = 20;
            rootBox.MarginEnd = 20;

            var fileInfoBox = Box.New(Orientation.Vertical, 4);
            lblFileName = Label.New(Path.GetFileName(targetPath));
            lblFileName.Halign = Align.Start;
            lblFileName.AddCssClass("title-4");

            long size = 0;
            try { size = new FileInfo(targetPath).Length; } catch { }
            lblFileSize = Label.New($"Path: {targetPath}\nSize: {FormattingHelper.FormatSize(size)}");
            lblFileSize.Halign = Align.Start;
            lblFileSize.AddCssClass("dim-label");

            fileInfoBox.Append(lblFileName);
            fileInfoBox.Append(lblFileSize);
            rootBox.Append(fileInfoBox);

            prgHash = ProgressBar.New();
            prgHash.Fraction = 0;
            rootBox.Append(prgHash);

            lblStatus = Label.New(TextResource.GetText("MSG_CALC_HASHES") ?? "Calculating hashes...");
            lblStatus.Halign = Align.Start;
            rootBox.Append(lblStatus);

            var hashesBox = Box.New(Orientation.Vertical, 8);
            txtSha256 = CreateHashRow(hashesBox, "SHA-256:");
            txtMd5 = CreateHashRow(hashesBox, "MD5:");
            txtSha512 = CreateHashRow(hashesBox, "SHA-512:");
            txtSha1 = CreateHashRow(hashesBox, "SHA-1:");
            rootBox.Append(hashesBox);

            var compareBox = Box.New(Orientation.Vertical, 6);
            var compareHeader = Label.New(TextResource.GetText("LBL_VERIFY_HASH") ?? "Verify against expected hash:");
            compareHeader.Halign = Align.Start;
            compareBox.Append(compareHeader);

            var compareInputRow = Box.New(Orientation.Horizontal, 8);
            txtExpected = Entry.New();
            txtExpected.Hexpand = true;
            txtExpected.PlaceholderText = "Paste expected SHA-256, MD5, SHA-512, or SHA-1 hash...";
            txtExpected.OnChanged += (_, _) => CheckMatch();

            lblMatchBadge = Label.New(TextResource.GetText("MSG_NO_MATCH") ?? "No match yet");
            lblMatchBadge.Halign = Align.End;
            compareInputRow.Append(txtExpected);
            compareInputRow.Append(lblMatchBadge);
            compareBox.Append(compareInputRow);
            rootBox.Append(compareBox);

            var buttonBox = Box.New(Orientation.Horizontal, 8);
            buttonBox.Halign = Align.End;
            buttonBox.MarginTop = 12;

            btnRecalculate = Button.NewWithLabel(TextResource.GetText("BTN_RECALCULATE") ?? "Recalculate");
            btnRecalculate.OnClicked += (_, _) => UiGuard.Run(window, "Recalculate hashes", StartCalculation);
            btnCopyAll = Button.NewWithLabel(TextResource.GetText("BTN_COPY_ALL") ?? "Copy All");
            btnCopyAll.OnClicked += (_, _) => UiGuard.Run(window, "Copy all hashes", CopyAllHashes);
            btnClose = Button.NewWithLabel(TextResource.GetText("BTN_CLOSE") ?? "Close");
            btnClose.OnClicked += (_, _) => Close();

            buttonBox.Append(btnRecalculate);
            buttonBox.Append(btnCopyAll);
            buttonBox.Append(btnClose);
            rootBox.Append(buttonBox);

            window.SetChild(rootBox);
            window.OnCloseRequest += (_, _) =>
            {
                cts?.Cancel();
                return false;
            };

            StartCalculation();
        }

        // Creates a label, read-only hash entry, and copy button row
        private Entry CreateHashRow(Box parent, string labelText)
        {
            var row = Box.New(Orientation.Horizontal, 8);
            var lbl = Label.New(labelText);
            lbl.WidthRequest = 75;
            lbl.Halign = Align.Start;

            var entry = Entry.New();
            entry.Hexpand = true;
            entry.Editable = false;

            var btnCopy = Button.NewWithLabel(TextResource.GetText("BTN_COPY") ?? "Copy");
            btnCopy.OnClicked += (_, _) =>
            {
                var text = entry.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    window.GetClipboard().SetText(text);
                }
            };

            row.Append(lbl);
            row.Append(entry);
            row.Append(btnCopy);
            parent.Append(row);
            return entry;
        }

        // Initiates asynchronous file hashing with progress updates
        private void StartCalculation()
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            prgHash.Fraction = 0;
            lblStatus.SetText("Computing cryptographic digests...");
            txtSha256.SetText("");
            txtMd5.SetText("");
            txtSha512.SetText("");
            txtSha1.SetText("");

            var progress = new Progress<double>(p =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    prgHash.Fraction = p;
                    return false;
                });
            });

            Task.Run(async () =>
            {
                try
                {
                    var result = await ChecksumHelper.ComputeHashesAsync(filePath, progress, token);
                    currentResult = result;

                    GLib.Functions.IdleAdd(0, () =>
                    {
                        prgHash.Fraction = 1.0;
                        lblStatus.SetText("Hashing complete.");
                        txtSha256.SetText(result.Sha256);
                        txtMd5.SetText(result.Md5);
                        txtSha512.SetText(result.Sha512);
                        txtSha1.SetText(result.Sha1);
                        CheckMatch();
                        return false;
                    });
                }
                catch (OperationCanceledException)
                {
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        lblStatus.SetText("Hashing cancelled.");
                        return false;
                    });
                }
                catch (Exception ex)
                {
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        lblStatus.SetText("Error: " + ex.Message);
                        return false;
                    });
                }
            }, token);
        }

        // Compares user-entered expected hash against calculated hashes
        private void CheckMatch()
        {
            if (!currentResult.HasValue) return;

            var expected = txtExpected.GetText()?.Trim();
            if (string.IsNullOrEmpty(expected))
            {
                lblMatchBadge.SetText("Enter expected hash");
                return;
            }

            var match = ChecksumHelper.CompareHash(expected, currentResult.Value);
            if (match.Status == ChecksumMatchStatus.Match)
            {
                lblMatchBadge.SetText($"✔ MATCH ({match.MatchedAlgorithm})");
            }
            else
            {
                lblMatchBadge.SetText("✘ MISMATCH");
            }
        }

        // Copies all hash values formatted as text to clipboard
        private void CopyAllHashes()
        {
            if (!currentResult.HasValue) return;
            var sb = new StringBuilder();
            sb.AppendLine($"File: {Path.GetFileName(filePath)}");
            sb.AppendLine($"SHA-256: {currentResult.Value.Sha256}");
            sb.AppendLine($"MD5:     {currentResult.Value.Md5}");
            sb.AppendLine($"SHA-512: {currentResult.Value.Sha512}");
            sb.AppendLine($"SHA-1:   {currentResult.Value.Sha1}");
            window.GetClipboard().SetText(sb.ToString());
        }

        // Displays the checksum dialog
        public void Show() => window.Present();

        // Closes the checksum dialog
        public void Close()
        {
            cts?.Cancel();
            window.Close();
        }
    }
}
