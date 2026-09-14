// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Translations;
using XDM.Core.UI;
using XDM.Core.Util;
using XDM.Wpf.UI.Win32;

namespace XDM.Wpf.UI.Dialogs.Checksum
{
    // Standalone checksum computation and verification window with drag & drop support
    public partial class ChecksumWindow : Window
    {
        // Status colors for match/error feedback (non-theme semantic colors)
        private static readonly SolidColorBrush MatchGreen = CreateFrozenBrush(0x2E, 0xC2, 0x7E);
        private static readonly SolidColorBrush ErrorRed = CreateFrozenBrush(0xEF, 0x44, 0x44);
        private static readonly SolidColorBrush FallbackIconBrush = CreateFrozenBrush(0x80, 0x80, 0x80);
        // Algorithm display names used in labels, tooltips and the copy report
        private const string AlgoSha256 = "SHA-256";
        private const string AlgoMd5 = "MD5";
        private const string AlgoSha512 = "SHA-512";
        private const string AlgoSha1 = "SHA-1";
        // Resource keys and UI format strings
        private const string IconColorKeyPrefix = "color-";
        private const string DefaultFileVectorKey = "ri-file-fill";
        private const string StatusTextColorKey = "ControlForecolor";
        private const string PlaceholderColorKey = "PlaceHolderForecolor";
        private const string CopyTooltipFormat = "Copy {0} to clipboard";
        private const string ComputedInFormat = "✔ {0} in {1}s";
        private const string MatchFormat = "✔ {0} ({1})! File integrity verified.";
        private const string MismatchFormat = "✖ {0}! Does not match any computed hash.";
        private const string FailedFormat = "Failed to compute: {0}";
        private const string AutoExtractedNoteFormat = "(Auto-extracted from {0})";
        private const string AutoDetectedNote = "(Auto-detected hash from clipboard)";
        private const string EmptyBadgeText = "Paste a checksum or drop a checksum file to verify authenticity";
        private const string CalculatingText = "Calculating...";
        private const string FileMissingText = "File does not exist on disk";
        private const string CopiedFeedbackText = "Copied!";
        private const string AllCopiedFeedbackText = "All Copied!";
        private const string HashMetaSeparator = "   ·   ";
        private static readonly string[] ChecksumManifestExtensions = { ".sha256", ".sha512", ".md5", ".sha1", ".sums", ".digest" };
        private static readonly TimeSpan FeedbackDelay = TimeSpan.FromSeconds(2);

        private string filePath;
        private ChecksumResult? currentResult;
        private CancellationTokenSource? calculationCts;
        private bool isClosed;
        private string copyAllLabel = string.Empty;
        private readonly DispatcherTimer feedbackTimer = new DispatcherTimer();
        private Action? pendingFeedbackRestore;

        // Initializes the checksum window for a target file and starts hashing
        public ChecksumWindow(Window? owner, string targetFilePath)
        {
            InitializeComponent();
            filePath = targetFilePath; Owner = owner;
            Title = TextResource.GetText("LBL_CHECKSUM_TITLE") ?? "Checksum Verification";
            feedbackTimer.Interval = FeedbackDelay; feedbackTimer.Tick += FeedbackTimer_Tick;
            LoadLocalizedTexts(); UpdateFileSummary(); InspectClipboardOnLoad(); StartCalculation();
        }

        // Applies localized labels with English fallbacks
        private void LoadLocalizedTexts()
        {
            LblStatus.Text = TextResource.GetText("LBL_COMPUTING_HASHES") ?? "Computing checksums...";
            LblCompareHeader.Text = TextResource.GetText("LBL_COMPARE_CHECKSUM") ?? "Compare with Expected Hash:";
            LblHint.Text = TextResource.GetText("LBL_HASH_PLACEHOLDER") ?? "Paste checksum or drop checksum file (SHA-256, MD5, SHA-512, SHA-1)...";
            BtnCopyAll.Content = copyAllLabel = TextResource.GetText("LBL_COPY_ALL_HASHES") ?? "Copy All Checksums";
            BtnRecalculate.Content = TextResource.GetText("LBL_RECALCULATE") ?? "Recalculate";
            BtnClose.Content = TextResource.GetText("ND_CANCEL") ?? "Close";
            LblSha256.Text = AlgoSha256 + ":";
            LblMd5.Text = AlgoMd5 + ":";
            LblSha512.Text = AlgoSha512 + ":";
            LblSha1.Text = AlgoSha1 + ":";
            var copyRows = new[] { (BtnCopySha256, AlgoSha256), (BtnCopyMd5, AlgoMd5), (BtnCopySha512, AlgoSha512), (BtnCopySha1, AlgoSha1) };
            foreach (var (copyButton, algoName) in copyRows) { copyButton.ToolTip = string.Format(CopyTooltipFormat, algoName); }
        }

        // Refreshes the header card for the active file
        private void UpdateFileSummary()
        {
            var fileName = Path.GetFileName(filePath);
            SetFileIcon(fileName);
            LblFileName.Text = fileName;
            LblFileMeta.Text = FormattingHelper.FormatSize(GetFileSizeBytes()) + HashMetaSeparator + FormatFriendlyPath(filePath);
        }

        // Resolves the file-type vector icon and its category color from app resources
        private void SetFileIcon(string fileName)
        {
            var vectorName = IconMap.GetVectorNameForFileType(fileName);
            ImgFileIcon.Data = FindResource<Geometry>(vectorName) ?? FindResource<Geometry>(DefaultFileVectorKey) ?? Geometry.Empty;
            ImgFileIcon.Fill = FindResource<Brush>(IconColorKeyPrefix + vectorName) ?? FindResource<Brush>(IconColorKeyPrefix + DefaultFileVectorKey) ?? FallbackIconBrush;
        }

        // Looks up a typed application resource or returns null
        private static T? FindResource<T>(string resourceKey) where T : class
        {
            return Application.Current?.TryFindResource(resourceKey) as T;
        }

        // Safely reads the active file size in bytes
        private long GetFileSizeBytes()
        {
            try { return File.Exists(filePath) ? new FileInfo(filePath).Length : 0; }
            catch { return 0; } // Unreadable file metadata shows as unknown size
        }

        // Formats full paths with ~/ tilde syntax
        private static string FormatFriendlyPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return !string.IsNullOrEmpty(home) && path.StartsWith(home) ? "~" + path.Substring(home.Length) : path;
        }

        // Creates a frozen solid brush from RGB components
        private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b)); brush.Freeze(); return brush;
        }

        // Starts an asynchronous hash computation for the active file
        private async void StartCalculation()
        {
            if (!File.Exists(filePath)) { ShowErrorStatus(FileMissingText); return; }
            calculationCts?.Cancel();
            calculationCts?.Dispose();
            calculationCts = new CancellationTokenSource();
            var runCts = calculationCts;
            var token = runCts.Token;
            currentResult = null;
            ShowComputingState();
            var progress = new Progress<double>(fraction => { if (!isClosed) PrgHash.Value = fraction * 100.0; });
            var startedAt = DateTime.UtcNow;
            try
            {
                var result = await Task.Run(() => ChecksumHelper.ComputeHashesAsync(filePath, progress, token), token);
                if (isClosed || !ReferenceEquals(calculationCts, runCts)) return; // Superseded by a newer run
                ApplyResult(result, (DateTime.UtcNow - startedAt).TotalSeconds);
            }
            catch (OperationCanceledException) { /* Cancelled or superseded run: leave UI as-is */ }
            catch (Exception ex)
            {
                if (!isClosed && ReferenceEquals(calculationCts, runCts)) ShowErrorStatus(string.Format(FailedFormat, ex.Message));
            }
        }

        // Resets the UI into the busy computing state
        private void ShowComputingState()
        {
            PrgHash.Visibility = Visibility.Visible; PrgHash.Value = 0;
            LblStatus.Text = TextResource.GetText("LBL_COMPUTING_HASHES") ?? "Computing checksums...";
            LblStatus.SetResourceReference(TextBlock.ForegroundProperty, StatusTextColorKey);
            foreach (var hashBox in new[] { TxtSha256, TxtMd5, TxtSha512, TxtSha1 }) { hashBox.Text = CalculatingText; }
            SetBadge(EmptyBadgeText, null, false);
        }

        // Publishes a finished computation result into the hash rows
        private void ApplyResult(ChecksumResult result, double elapsedSeconds)
        {
            currentResult = result;
            TxtSha256.Text = result.Sha256; TxtMd5.Text = result.Md5;
            TxtSha512.Text = result.Sha512; TxtSha1.Text = result.Sha1;
            PrgHash.Value = 100; PrgHash.Visibility = Visibility.Collapsed;
            LblStatus.Text = string.Format(ComputedInFormat,
                TextResource.GetText("LBL_HASHES_COMPUTED") ?? "Checksums calculated", elapsedSeconds.ToString("F2"));
            LblStatus.Foreground = MatchGreen;
            ValidateComparison();
        }

        // Shows a red error status and hides the progress bar
        private void ShowErrorStatus(string message)
        {
            LblStatus.Text = message;
            LblStatus.Foreground = ErrorRed;
            PrgHash.Visibility = Visibility.Collapsed;
        }

        // Validates the expected hash against the computed result and updates the badge
        private void ValidateComparison()
        {
            if (!currentResult.HasValue)
            {
                SetBadge(EmptyBadgeText, null, false); TxtExpected.ClearValue(TextBox.BorderBrushProperty); return;
            }

            var match = ChecksumHelper.CompareHash(TxtExpected.Text, currentResult.Value);
            if (match.Status == ChecksumMatchStatus.Match)
            {
                LblBadge.Text = string.Format(MatchFormat, TextResource.GetText("LBL_HASH_MATCH") ?? "Checksum Matches", match.MatchedAlgorithm);
                LblBadge.Foreground = MatchGreen; LblBadge.FontWeight = FontWeights.Bold; TxtExpected.BorderBrush = MatchGreen;
                return;
            }
            if (match.Status == ChecksumMatchStatus.Mismatch)
            {
                LblBadge.Text = string.Format(MismatchFormat, TextResource.GetText("LBL_HASH_MISMATCH") ?? "Checksum Mismatch");
                LblBadge.Foreground = ErrorRed; LblBadge.FontWeight = FontWeights.Bold; TxtExpected.BorderBrush = ErrorRed;
                return;
            }
            SetBadge(EmptyBadgeText, null, false);
            TxtExpected.ClearValue(TextBox.BorderBrushProperty);
        }

        // Writes badge text with either a status brush or the themed placeholder color
        private void SetBadge(string text, Brush? foreground, bool bold)
        {
            LblBadge.Text = text;
            if (foreground == null) { LblBadge.SetResourceReference(TextBlock.ForegroundProperty, PlaceholderColorKey); LblBadge.FontWeight = FontWeights.Normal; }
            else { LblBadge.Foreground = foreground; LblBadge.FontWeight = bold ? FontWeights.Bold : FontWeights.Normal; }
        }

        // Toggles the watermark hint and revalidates as the expected hash changes
        private void TxtExpected_TextChanged(object sender, TextChangedEventArgs e)
        {
            LblHint.Visibility = string.IsNullOrEmpty(TxtExpected.Text) ? Visibility.Visible : Visibility.Collapsed;
            ValidateComparison();
        }

        // Copies the digest of the clicked row to the clipboard with feedback
        private void BtnCopyHash_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button)) return;
            var (hashText, algoName) = button.Name switch
            {
                nameof(BtnCopySha256) => (TxtSha256.Text, AlgoSha256),
                nameof(BtnCopyMd5) => (TxtMd5.Text, AlgoMd5),
                nameof(BtnCopySha512) => (TxtSha512.Text, AlgoSha512),
                nameof(BtnCopySha1) => (TxtSha1.Text, AlgoSha1),
                _ => (string.Empty, string.Empty)
            };
            if (string.IsNullOrEmpty(hashText) || string.IsNullOrEmpty(algoName) || hashText == CalculatingText) return;
            if (!CopyToClipboardSafe(hashText)) return;
            button.ToolTip = CopiedFeedbackText;
            ScheduleRestore(() => button.ToolTip = string.Format(CopyTooltipFormat, algoName));
        }

        // Copies the full multi-line hash report with transient button feedback
        private void BtnCopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (!currentResult.HasValue) return;
            if (!CopyToClipboardSafe(BuildHashReport())) return;
            var originalLabel = copyAllLabel;
            BtnCopyAll.Content = AllCopiedFeedbackText;
            ScheduleRestore(() => BtnCopyAll.Content = originalLabel);
        }

        // Builds the multi-line report identical to the GTK dialog
        private string BuildHashReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"File: {Path.GetFileName(filePath)}");
            sb.AppendLine($"Size: {currentResult.Value.FileSizeBytes} bytes");
            sb.AppendLine($"{AlgoSha256}: {currentResult.Value.Sha256}");
            sb.AppendLine($"{AlgoMd5}: {currentResult.Value.Md5}");
            sb.AppendLine($"{AlgoSha512}: {currentResult.Value.Sha512}");
            sb.AppendLine($"{AlgoSha1}: {currentResult.Value.Sha1}");
            return sb.ToString();
        }

        // Clipboard write guarded against a locked clipboard
        private static bool CopyToClipboardSafe(string text)
        {
            try { System.Windows.Clipboard.SetText(text); return true; }
            catch { return false; } // Clipboard may be held by another process
        }

        // Schedules a single delayed UI restore for copy feedback
        private void ScheduleRestore(Action restoreAction)
        {
            pendingFeedbackRestore = restoreAction; feedbackTimer.Stop(); feedbackTimer.Start();
        }

        // Runs the pending feedback restore when the delay elapses
        private void FeedbackTimer_Tick(object? sender, EventArgs e)
        {
            feedbackTimer.Stop();
            var restore = pendingFeedbackRestore; pendingFeedbackRestore = null;
            restore?.Invoke();
        }

        // Restarts hashing for the active file
        private void BtnRecalculate_Click(object sender, RoutedEventArgs e) => StartCalculation();

        // Closes the window
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // Accepts file or text drag payloads hovering anywhere on the window
        private void Window_PreviewDragOver(object sender, DragEventArgs e)
        {
            var isContent = e.Data.GetDataPresent(DataFormats.FileDrop) ||
                            e.Data.GetDataPresent(DataFormats.UnicodeText) ||
                            e.Data.GetDataPresent(DataFormats.Text);
            e.Effects = isContent ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
        }

        // Handles a drop as a file list or a raw hash string
        private void Window_PreviewDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    foreach (var file in files ?? Array.Empty<string>())
                    {
                        if (!string.IsNullOrEmpty(file) && File.Exists(file)) { HandleDroppedPath(file); return; }
                    }
                }
                ApplyDroppedText(e.Data.GetData(DataFormats.UnicodeText) as string ?? e.Data.GetData(DataFormats.Text) as string);
            }
            catch { /* Malformed drop payloads are ignored */ }
        }

        // Fills the expected box when a dropped plain text looks like a hash
        private void ApplyDroppedText(string? text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!ChecksumHelper.IsProbableHash(text.Trim(), out var cleanHash)) return;
            TxtExpected.Text = cleanHash; SetBadge(AutoDetectedNote, null, false); ValidateComparison();
        }

        // Treats a dropped path as a checksum manifest or a new target file
        private void HandleDroppedPath(string droppedPath)
        {
            var activeFileName = Path.GetFileName(filePath);
            if (IsChecksumManifest(droppedPath) &&
                ChecksumHelper.TryExtractHashFromChecksumFile(droppedPath, activeFileName, out var extractedHash))
            {
                TxtExpected.Text = extractedHash;
                SetBadge(string.Format(AutoExtractedNoteFormat, Path.GetFileName(droppedPath)), null, false);
                ValidateComparison();
                return;
            }
            filePath = droppedPath;
            currentResult = null;
            UpdateFileSummary();
            StartCalculation();
        }

        // Detects checksum manifest files by extension or by name keywords
        private static bool IsChecksumManifest(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? string.Empty;
            var name = Path.GetFileName(path);
            return Array.IndexOf(ChecksumManifestExtensions, ext) >= 0 ||
                   name.IndexOf("checksum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("sha256sum", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Pre-fills the expected hash when the clipboard already holds one
        private void InspectClipboardOnLoad()
        {
            try
            {
                var text = System.Windows.Clipboard.GetText();
                if (ChecksumHelper.IsProbableHash(text, out var cleanHash))
                {
                    TxtExpected.Text = cleanHash; SetBadge(AutoDetectedNote, null, false);
                }
            }
            catch { /* Clipboard may be locked by another process */ }
        }

        // Applies the immersive dark titlebar in dark skin
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
#if NET45_OR_GREATER
            if (App.Skin == Skin.Dark)
            {
                var helper = new WindowInteropHelper(this); helper.EnsureHandle();
                DarkModeHelper.UseImmersiveDarkMode(helper.Handle, true);
            }
#endif
        }

        // Cancels and disposes hashing work and timers on close
        protected override void OnClosed(EventArgs e)
        {
            isClosed = true;
            feedbackTimer.Stop();
            pendingFeedbackRestore = null;
            calculationCts?.Cancel(); calculationCts?.Dispose(); calculationCts = null;
            base.OnClosed(e);
        }
    }
}
