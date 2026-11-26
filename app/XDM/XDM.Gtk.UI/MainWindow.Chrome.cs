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
        // Left-aligned in-app headerbar: bold "FetchFlow" + dim "· <view>" custom title (not the
        // centered default title/subtitle); hexpand + halign-start pushes the title left.
        // Wayland CSD: headerbar supplies the close button; no min/max buttons by convention.
        private HeaderBar CreateHeaderBar()
        {
            var hb = new HeaderBar
            {
                ShowCloseButton = true,
                DecorationLayout = ":minimize,maximize,close",
                HasSubtitle = false
            };
            hb.StyleContext.AddClass("main-headerbar");

            var appIcon = new Image
            {
                Pixbuf = LoadSvg("fetchflow-mark", 20) ?? LoadSvg("fetchflow-logo", 20),
                MarginEnd = 4,
                Valign = Align.Center
            };

            var appLabel = new Label { Text = HeaderAppName };
            appLabel.StyleContext.AddClass("header-title-app");
            subtitleLabel = new Label
            {
                Text = HeaderSubtitleSeparator + TextResource.GetText("ALL_UNFINISHED"),
                Ellipsize = Pango.EllipsizeMode.End
            };
            subtitleLabel.StyleContext.AddClass("header-title-view");

            // Update-available dot: the single update indicator (bottom bar stays a plain
            // "Help and support"); hidden until an update lands, click opens the updater
            updateDot = new Button { Visible = false };
            var dotLabel = new Label { Text = UpdateDotGlyph };
            dotLabel.StyleContext.AddClass("update-dot");
            updateDot.Add(dotLabel);
            updateDot.Relief = ReliefStyle.None;
            updateDot.Valign = Align.Center;
            updateDot.TooltipText = TextResource.GetText("MSG_UPDATE_AVAILABLE");
            updateDot.Accessible.Name = TextResource.GetText("MSG_UPDATE_AVAILABLE");
            updateDot.StyleContext.AddClass("flat");
            updateDot.StyleContext.AddClass("update-dot-button");
            updateDot.Clicked += (_, _) => UpdateClicked?.Invoke(this, EventArgs.Empty);

            var titleBox = new HBox(false, ButtonContentSpacing) { Hexpand = false, Halign = Align.Start };
            titleBox.PackStart(appIcon, false, false, 0);
            titleBox.PackStart(appLabel, false, false, 0);
            titleBox.PackStart(subtitleLabel, false, false, 0);
            titleBox.PackStart(updateDot, false, false, 0);
            hb.PackStart(titleBox);
            hb.CustomTitle = new Label("");

            // Theme toggle button: allows instant switching between Dark and Light mode
            var themeToggleBtn = new Button { Visible = true, Relief = ReliefStyle.None, Valign = Align.Center };
            themeToggleBtn.StyleContext.AddClass("flat");
            themeToggleBtn.StyleContext.AddClass("theme-toggle-button");

            var themeIcon = new Image();
            void UpdateThemeIcon(bool isDark)
            {
                themeIcon.Pixbuf = LoadSvg(isDark ? "sun-line" : "moon-line", 18);
                themeToggleBtn.TooltipText = isDark ? "Switch to Light theme" : "Switch to Dark theme";
                // Icon-only: the ATK name is the only label assistive tech can read
                themeToggleBtn.Accessible.Name = themeToggleBtn.TooltipText;
            }
            UpdateThemeIcon(ThemeManager.IsDarkActive);
            ThemeManager.ThemeChanged += isDark => Gtk.Application.Invoke((_, _) => UpdateThemeIcon(isDark));
            themeToggleBtn.Add(themeIcon);
            themeToggleBtn.Clicked += (_, _) => ThemeManager.ToggleTheme();

            hb.PackEnd(themeToggleBtn);
            hb.ShowAll();
            return hb;
        }

        // Headerbar view subtitle tracks the active sidebar selection (e.g. "· All Finished")
        private void SetHeaderSubtitle(string? viewName)
        {
            if (subtitleLabel == null || string.IsNullOrEmpty(viewName))
            {
                return;
            }
            subtitleLabel.Text = HeaderSubtitleSeparator + viewName;
            if (lblViewTitle != null)
            {
                lblViewTitle.Text = viewName ?? "";
            }
            UpdateViewHeading();
        }

        private Widget CreateMainPanel()
        {
            var vbMain = new VBox();
            vbMain.PackStart(CreateToolbar(), false, false, 0);
            vbMain.PackStart(CreateContentToolbar(), false, false, 0);
            WireKeyboardShortcuts();
            vbMain.PackStart(CreateInProgressListView(), true, true, 0);
            vbMain.PackStart(CreateFinishedListView(), true, true, 0);
            vbMain.PackStart(CreateBottombar(), false, false, 0);
            vbMain.Show();
            return vbMain;
        }

        private Button CreateButtonWithContent(string icon, string? text = null, byte? r = null, byte? g = null, byte? b = null, string? accessibleName = null, bool primary = false)
        {
            Label? lbl = null;
            if (!string.IsNullOrEmpty(text))
            {
                lbl = new Label { Text = text };
            }
            var rawPixbuf = LoadSvg(icon, 16);
            var pixbuf = (r.HasValue && g.HasValue && b.HasValue && rawPixbuf != null)
                ? GtkHelper.TintPixbuf(rawPixbuf, r.Value, g.Value, b.Value)
                : rawPixbuf;
            var image = pixbuf != null ? new Image(pixbuf) : new Image();
            return CreateButtonWithContent(image, lbl, accessibleName, primary);
        }

        // Ink the branded primary action draws with, derived from the active accent
        private static (byte R, byte G, byte B) PrimaryActionInk()
        {
            if (ThemeContrast.TryParseHex(ThemeContrast.InkFor(ThemeManager.ActiveAccentHex),
                out var r, out var g, out var b))
            {
                return ((byte)r, (byte)g, (byte)b);
            }
            return (0xff, 0xff, 0xff);
        }

        // Re-tints a button's icon in place. Button.Image REPLACES the whole child,
        // which silently dropped a labeled button's text; swap the pixbuf instead.
        private static void SetButtonIcon(Button button, Gdk.Pixbuf? pixbuf)
        {
            if (pixbuf == null) return;
            if (button.Child is HBox box && box.Children.Length > 0 && box.Children[0] is Image image)
            {
                image.Pixbuf = pixbuf;
                return;
            }
            button.Image = new Image(pixbuf);
        }

        // Re-tints the primary action's glyph when the scheme, and so its ink, changes
        private void RefreshPrimaryActionButton()
        {
            if (btnNew == null) return;
            var pix = LoadSvg("links-line", 16);
            if (pix == null) return;
            var (r, g, b) = PrimaryActionInk();
            SetButtonIcon(btnNew, GtkHelper.TintPixbuf(pix, r, g, b));
        }

        private Button CreateButtonWithContent(Image image, Label? label, string? accessibleName = null, bool primary = false)
        {
            // Icon-only buttons: no side margins/spacing; the image expands and GtkMisc's
            // default 0.5 xalign centers the glyph inside the button. Labeled buttons unchanged.
            var hbox = new HBox(false, label != null ? ButtonContentSpacing : 0)
            {
                MarginStart = label != null ? ButtonBoxMargin : 0,
                MarginEnd = label != null ? ButtonBoxMargin : 0
            };

            hbox.PackStart(image, label == null, label == null, 0);
            if (label != null)
            {
                hbox.PackStart(label, false, false, 0);
            }

            var button = new Button
            {
                Relief = primary ? ReliefStyle.Normal : ReliefStyle.None,
                Valign = Align.Center,

            };
            button.Add(hbox);
            // The primary action carries the scheme accent (GTK 4 parity); the rest stay
            // flat so the theme's toolbar hover/active states apply
            button.StyleContext.AddClass(primary ? "suggested-action" : "flat");
            // Screen readers announce icon-only buttons by this name (GNOME/KDE both
            // read the ATK name; an Image child contributes nothing readable)
            var name = accessibleName ?? label?.Text;
            if (!string.IsNullOrEmpty(name))
            {
                button.Accessible.Name = name;
            }
            return button;
        }

        private Widget CreateBottombar()
        {
            var hbox = new HBox(false, 10);
            hbox.StyleContext.AddClass("bottombar");
            hbox.Margin = 4;
            hbox.MarginStart = 8;
            hbox.MarginEnd = 8;
            btnMonitoring = new CheckButton { MarginStart = 5 };
            btnMonitoring.Clicked += BtnMonitoring_Clicked;
            hbox.PackStart(btnMonitoring, false, false, 0);

            var lblMonitoring = new Label { Text = TextResource.GetText("SETTINGS_MONITORING") };
            hbox.PackStart(lblMonitoring, false, false, 0);

            lblExtensionStatus = new Label { MarginStart = 10 };
            lblExtensionStatus.StyleContext.AddClass("extension-status-label");
            hbox.PackStart(lblExtensionStatus, false, false, 0);
            UpdateExtensionStatus();

            lblTotalSpeed = new Label { Visible = false };
            lblTotalSpeed.StyleContext.AddClass("total-speed-label");
            btnActivity = new Button();
            btnActivity.Add(lblTotalSpeed);
            btnActivity.StyleContext.AddClass("flat");
            btnActivity.TooltipText = TextResource.GetText("LBL_SHOW_PROGRESS") ?? "Show progress";
            btnActivity.Clicked += (_, _) => UIActions.ShowProgressWindow();
            var activityBox = new HBox(false, 0) { MarginStart = 10 };
            activityBox.Add(btnActivity);
            hbox.PackStart(activityBox, false, false, 0);

            // Labeled like GTK 4's control: icon-only left a bare clock in the bar
            btnSpeedLimit = CreateButtonWithContent("time-line",
                TextResource.GetText("MENU_SPEED_LIMITER"), DimR, DimG, DimB,
                TextResource.GetText("MENU_SPEED_LIMITER"));
            btnSpeedLimit.Clicked += BtnSpeedLimit_Clicked;
            btnSpeedLimit.ButtonPressEvent += (o, args) =>
            {
                if (args.Event.Button == 3)
                {
                    ShowSpeedLimiterMenu(args.Event);
                }
            };
            btnSpeedLimit.StyleContext.AddClass("bottombar-button");
            btnSpeedLimit.MarginStart = 6;
            hbox.PackStart(btnSpeedLimit, false, false, 0);
            UpdateSpeedLimitButton();

            btnScheduler = CreateButtonWithContent("list-settings-line", TextResource.GetText("DESC_Q_TITLE"), CyanR, CyanG, CyanB);
            btnScheduler.Clicked += BtnScheduler_Clicked;
            btnScheduler.StyleContext.AddClass("bottombar-button");

            hbox.PackEnd(btnScheduler, false, false, 0);

            // Selection counter on the bar's right side, visible only while selecting
            lblSelectionCount = new Label { Visible = false };
            lblSelectionCount.StyleContext.AddClass("view-count");
            hbox.PackEnd(lblSelectionCount, false, false, 0);

            // Container card for bottom statusbar matching the top action toolbar surface
            var bottombarCard = new EventBox
            {
                Margin = 6,
                MarginTop = 2
            };
            bottombarCard.StyleContext.AddClass("main-bottombar");
            bottombarCard.StyleContext.AddClass("main-toolbar");
            bottombarCard.Add(hbox);
            bottombarCard.ShowAll();
            return bottombarCard;
        }
    }
}
