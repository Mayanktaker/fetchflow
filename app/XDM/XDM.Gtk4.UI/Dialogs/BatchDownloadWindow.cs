// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Native GTK 4 Batch Download window supporting pattern-based URL generation and batch addressing.
using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using Translations;
using XDM.Core.UI;
using XDM.Core.Util;

namespace XDM.Gtk4UI.Dialogs
{
    // Batch download URL pattern generator and multi-URL entry window
    internal sealed class BatchDownloadWindow : IBatchDownloadView
    {
        private readonly Window window;
        private readonly Notebook notebook;

        private readonly Entry txtAddress;
        private readonly CheckButton chkLetterMode;
        private readonly CheckButton chkNumberMode;
        private readonly DropDown cmbLetterFrom;
        private readonly DropDown cmbLetterTo;
        private readonly Box letterBox;

        private readonly Entry txtNumberFrom;
        private readonly Entry txtNumberTo;
        private readonly CheckButton chkLeadingZero;
        private readonly Entry txtLeadingZeroCount;
        private readonly Box numberBox;

        private readonly Entry txtPreview1;
        private readonly Entry txtPreview2;
        private readonly Entry txtPreviewN;

        private readonly TextView txtBatchList;

        private readonly Button btnOk;
        private readonly Button btnCancel;

        private string[] letterRange = Array.Empty<string>();
        private bool isSuppressingEvents;

        public event EventHandler? PatternChanged;
        public event EventHandler? OkClicked;

        // Constructs the BatchDownloadWindow with pattern configuration and preview UI
        public BatchDownloadWindow(Window? parent)
        {
            window = Window.New();
            var title = TextResource.GetText("MENU_BATCH_DOWNLOAD") ?? "Batch Download";
            window.Title = title;
            window.Modal = true;
            window.SetDefaultSize(640, 520);
            if (parent != null) window.SetTransientFor(parent);

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(title);
            window.SetTitlebar(header);

            var rootBox = Box.New(Orientation.Vertical, 12);
            rootBox.MarginTop = 16;
            rootBox.MarginBottom = 16;
            rootBox.MarginStart = 20;
            rootBox.MarginEnd = 20;

            notebook = Notebook.New();
            notebook.Vexpand = true;

            // --- Tab 1: Pattern Mode ---
            var patternTab = Box.New(Orientation.Vertical, 12);
            patternTab.MarginTop = 12;
            patternTab.MarginStart = 8;
            patternTab.MarginEnd = 8;

            var addrBox = Box.New(Orientation.Vertical, 4);
            var lblAddress = Label.New(TextResource.GetText("BAT_ADDRESS") ?? "Address pattern (use * as placeholder):");
            lblAddress.Halign = Align.Start;
            txtAddress = Entry.New();
            txtAddress.PlaceholderText = "https://example.com/images/photo_*.jpg";
            txtAddress.OnChanged += (_, _) => OnPatternInputChanged();
            addrBox.Append(lblAddress);
            addrBox.Append(txtAddress);
            patternTab.Append(addrBox);

            // Mode Selector
            var modeBox = Box.New(Orientation.Horizontal, 16);
            chkLetterMode = CheckButton.NewWithLabel(TextResource.GetText("BAT_LETTERS") ?? "Letters (A - Z)");
            chkNumberMode = CheckButton.NewWithLabel(TextResource.GetText("BAT_NUMBERS") ?? "Numbers");
            chkNumberMode.SetGroup(chkLetterMode);
            chkLetterMode.Active = true;
            chkLetterMode.OnToggled += (_, _) => OnModeToggled();
            chkNumberMode.OnToggled += (_, _) => OnModeToggled();
            modeBox.Append(chkLetterMode);
            modeBox.Append(chkNumberMode);
            patternTab.Append(modeBox);

            // Letter controls
            letterBox = Box.New(Orientation.Horizontal, 8);
            var lblFromL = Label.New(TextResource.GetText("LBL_FROM") ?? "From:");
            cmbLetterFrom = DropDown.NewFromStrings(Array.Empty<string>());
            cmbLetterFrom.OnNotify += (_, args) => { if (args.Pspec.GetName() == "selected") OnPatternInputChanged(); };

            var lblToL = Label.New(TextResource.GetText("LBL_TO") ?? "To:");
            cmbLetterTo = DropDown.NewFromStrings(Array.Empty<string>());
            cmbLetterTo.OnNotify += (_, args) => { if (args.Pspec.GetName() == "selected") OnPatternInputChanged(); };

            letterBox.Append(lblFromL);
            letterBox.Append(cmbLetterFrom);
            letterBox.Append(lblToL);
            letterBox.Append(cmbLetterTo);
            patternTab.Append(letterBox);

            // Number controls
            numberBox = Box.New(Orientation.Horizontal, 8);
            numberBox.Visible = false;

            var lblFromN = Label.New(TextResource.GetText("LBL_FROM") ?? "From:");
            txtNumberFrom = Entry.New();
            txtNumberFrom.SetText("1");
            txtNumberFrom.WidthRequest = 60;
            txtNumberFrom.OnChanged += (_, _) => OnPatternInputChanged();

            var lblToN = Label.New(TextResource.GetText("LBL_TO") ?? "To:");
            txtNumberTo = Entry.New();
            txtNumberTo.SetText("10");
            txtNumberTo.WidthRequest = 60;
            txtNumberTo.OnChanged += (_, _) => OnPatternInputChanged();

            chkLeadingZero = CheckButton.NewWithLabel(TextResource.GetText("LBL_LEADING_ZEROS") ?? "Leading zeros:");
            chkLeadingZero.OnToggled += (_, _) => OnPatternInputChanged();

            txtLeadingZeroCount = Entry.New();
            txtLeadingZeroCount.SetText("2");
            txtLeadingZeroCount.WidthRequest = 40;
            txtLeadingZeroCount.OnChanged += (_, _) => OnPatternInputChanged();

            numberBox.Append(lblFromN);
            numberBox.Append(txtNumberFrom);
            numberBox.Append(lblToN);
            numberBox.Append(txtNumberTo);
            numberBox.Append(chkLeadingZero);
            numberBox.Append(txtLeadingZeroCount);
            patternTab.Append(numberBox);

            // Preview Box
            var previewBox = Box.New(Orientation.Vertical, 6);
            var lblPreview = Label.New(TextResource.GetText("LBL_PREVIEW_URLS") ?? "Preview URLs:");
            lblPreview.Halign = Align.Start;
            lblPreview.AddCssClass("heading");
            previewBox.Append(lblPreview);

            txtPreview1 = CreateReadonlyPreviewRow(previewBox, "1:");
            txtPreview2 = CreateReadonlyPreviewRow(previewBox, "2:");
            txtPreviewN = CreateReadonlyPreviewRow(previewBox, "N:");
            patternTab.Append(previewBox);

            notebook.AppendPage(patternTab, Label.New(TextResource.GetText("LBL_PATTERN") ?? "Pattern"));

            // --- Tab 2: Batch Address List ---
            var listTab = Box.New(Orientation.Vertical, 8);
            listTab.MarginTop = 12;
            listTab.MarginStart = 8;
            listTab.MarginEnd = 8;

            var lblListPrompt = Label.New(TextResource.GetText("MSG_ONE_URL_PER_LINE") ?? "Enter one URL per line:");
            lblListPrompt.Halign = Align.Start;
            listTab.Append(lblListPrompt);

            var scrollList = ScrolledWindow.New();
            scrollList.Vexpand = true;
            scrollList.Hexpand = true;

            txtBatchList = TextView.New();
            txtBatchList.WrapMode = WrapMode.None;
            scrollList.SetChild(txtBatchList);
            listTab.Append(scrollList);

            notebook.AppendPage(listTab, Label.New(TextResource.GetText("LBL_ADDRESS_LIST") ?? "Address List"));
            rootBox.Append(notebook);

            // --- Footer ---
            var footer = Box.New(Orientation.Horizontal, 8);
            footer.Halign = Align.End;
            footer.MarginTop = 8;

            btnCancel = Button.NewWithLabel(TextResource.GetText("ND_CANCEL") ?? "Cancel");
            btnCancel.OnClicked += (_, _) => DestroyWindow();

            btnOk = Button.NewWithLabel(TextResource.GetText("MSG_OK") ?? "OK");
            btnOk.AddCssClass("suggested-action");
            btnOk.OnClicked += (_, _) => OkClicked?.Invoke(this, EventArgs.Empty);

            footer.Append(btnCancel);
            footer.Append(btnOk);
            rootBox.Append(footer);

            window.SetChild(rootBox);
        }

        // Helper to construct a readonly preview line
        private static Entry CreateReadonlyPreviewRow(Box container, string prefix)
        {
            var row = Box.New(Orientation.Horizontal, 6);
            var lbl = Label.New(prefix);
            lbl.WidthRequest = 24;
            lbl.Halign = Align.Start;
            var entry = Entry.New();
            entry.Editable = false;
            entry.Hexpand = true;
            entry.AddCssClass("dim-label");
            row.Append(lbl);
            row.Append(entry);
            container.Append(row);
            return entry;
        }

        // Toggles between Letter range and Number range inputs
        private void OnModeToggled()
        {
            var isLetters = chkLetterMode.Active;
            letterBox.Visible = isLetters;
            numberBox.Visible = !isLetters;
            OnPatternInputChanged();
        }

        // Raises PatternChanged event when inputs modify
        private void OnPatternInputChanged()
        {
            if (isSuppressingEvents) return;
            PatternChanged?.Invoke(this, EventArgs.Empty);
        }

        // Populates start letter options
        public void SetStartLetterRange(string[] range)
        {
            letterRange = range;
            cmbLetterFrom.SetModel(StringList.New(range));
            if (range.Length > 0) cmbLetterFrom.SetSelected(0);
        }

        // Populates end letter options
        public void SetEndLetterRange(string[] range)
        {
            letterRange = range;
            cmbLetterTo.SetModel(StringList.New(range));
            if (range.Length > 0) cmbLetterTo.SetSelected((uint)(range.Length - 1));
        }

        // Presents window
        public void ShowWindow() => window.Present();

        // Destroys window
        public void DestroyWindow() => window.Close();

        // IBatchDownloadView properties
        public bool IsLetterMode
        {
            get => chkLetterMode.Active;
            set
            {
                isSuppressingEvents = true;
                try
                {
                    chkLetterMode.Active = value;
                    chkNumberMode.Active = !value;
                    letterBox.Visible = value;
                    numberBox.Visible = !value;
                }
                finally
                {
                    isSuppressingEvents = false;
                }
            }
        }

        public bool IsUsingLeadingZero
        {
            get => chkLeadingZero.Active;
            set => chkLeadingZero.Active = value;
        }

        public string Url
        {
            get => txtAddress.GetText() ?? string.Empty;
            set => txtAddress.SetText(value);
        }

        public char? StartLetter
        {
            get
            {
                var idx = (int)cmbLetterFrom.GetSelected();
                return idx >= 0 && idx < letterRange.Length ? letterRange[idx][0] : null;
            }
        }

        public char? EndLetter
        {
            get
            {
                var idx = (int)cmbLetterTo.GetSelected();
                return idx >= 0 && idx < letterRange.Length ? letterRange[idx][0] : null;
            }
        }

        public int StartNumber => ParsingHelper.ParseIntSafe(txtNumberFrom.GetText());
        public int EndNumber => ParsingHelper.ParseIntSafe(txtNumberTo.GetText());
        public int LeadingZeroCount => ParsingHelper.ParseIntSafe(txtLeadingZeroCount.GetText());

        public string BatchAddress1
        {
            get => txtPreview1.GetText() ?? string.Empty;
            set => txtPreview1.SetText(value);
        }

        public string BatchAddress2
        {
            get => txtPreview2.GetText() ?? string.Empty;
            set => txtPreview2.SetText(value);
        }

        public string BatchAddressN
        {
            get => txtPreviewN.GetText() ?? string.Empty;
            set => txtPreviewN.SetText(value);
        }

        public bool IsBatchMode => notebook.GetCurrentPage() == 0;
    }
}
