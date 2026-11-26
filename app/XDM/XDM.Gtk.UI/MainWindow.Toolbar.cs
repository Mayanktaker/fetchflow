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
        // Handles application-wide broadcast events to refresh UI state
        private void ApplicationContext_ApplicationEvent(object? sender, ApplicationEvent e)
        {
            if (e.EventType == "ExtensionConnectionChanged" || e.EventType == "ConfigChanged")
            {
                Application.Invoke((_, _) =>
                {
                    UpdateExtensionStatus();
                    UpdateSpeedLimitButton();
                    if (menuCompletionSound != null)
                    {
                        menuCompletionSound.Active = Config.Instance.PlayCompletionSound;
                    }
                });
            }
        }

        // Updates the extension connectivity badge in the bottom bar
        private void UpdateExtensionStatus()
        {
            try
            {
                if (lblExtensionStatus == null) return;
                var count = IpcHttpMessageProcessor.ActiveWebSocketSessionsCount;
                var isConn = IpcHttpMessageProcessor.IsConnected;
                var port = IpcHttpMessageProcessor.EffectivePort;

                // Monitoring off means nothing will be captured, so the badge
                // must not claim the extension is ready or active
                if (!Config.Instance.IsBrowserMonitoringEnabled)
                {
                    var off = TextResource.GetText("MSG_EXT_DISABLED") ?? "Monitoring Off";
                    lblExtensionStatus.Markup = $"<span color='#94a3b8' size='small'>○ {off}</span>";
                    lblExtensionStatus.TooltipText = TextResource.GetText("MSG_EXT_DISABLED_DESC") ?? "Browser monitoring is off — turn it on to capture downloads";
                    return;
                }

                if (count > 0)
                {
                    lblExtensionStatus.Markup = $"<span color='#22c55e' size='small'>● Extension Active ({count})</span>";
                    lblExtensionStatus.TooltipText = $"Browser Extension connected via WebSocket\nIPC Port: {port}\nActive sessions: {count}";
                }
                else if (isConn)
                {
                    lblExtensionStatus.Markup = $"<span color='#38bdf8' size='small'>● Extension Ready</span>";
                    lblExtensionStatus.TooltipText = $"Browser Extension connected via HTTP Relay\nIPC Port: {port}";
                }
                else
                {
                    lblExtensionStatus.Markup = $"<span color='#94a3b8' size='small'>○ Extension Listening</span>";
                    lblExtensionStatus.TooltipText = $"FetchFlow IPC Server listening on 127.0.0.1:{port}\nWaiting for browser extension connection...";
                }
            }
            catch { }
        }

        // Toggles global bandwidth throttling on or off
        private void BtnSpeedLimit_Clicked(object? sender, EventArgs e)
        {
            Config.Instance.EnableSpeedLimit = !Config.Instance.EnableSpeedLimit;
            if (Config.Instance.EnableSpeedLimit && Config.Instance.DefaltDownloadSpeed <= 0)
            {
                Config.Instance.DefaltDownloadSpeed = 1024; // 1 MB/s default throttle
            }
            Config.SaveConfig();
            ApplicationContext.BroadcastConfigChange();
            UpdateSpeedLimitButton();
        }

        // Refreshes the bandwidth limiter button visual state and tooltip
        private void UpdateSpeedLimitButton()
        {
            if (btnSpeedLimit == null) return;
            bool enabled = Config.Instance.EnableSpeedLimit;
            int limit = Config.Instance.DefaltDownloadSpeed;
            if (enabled && limit > 0)
            {
                var limitStr = FormattingHelper.FormatSize(limit * 1024.0) + "/s";
                btnSpeedLimit.TooltipText = $"Speed Limiter: {limitStr} (Click to disable)";
                var pix = LoadSvg("time-line", 16);
                if (pix != null)
                {
                    SetButtonIcon(btnSpeedLimit, GtkHelper.TintPixbuf(pix, AccentR, AccentG, AccentB));
                }
            }
            else
            {
                btnSpeedLimit.TooltipText = "Speed Limiter: Off (Click to enable bandwidth limit)";
                var pix = LoadSvg("time-line", 16);
                if (pix != null)
                {
                    SetButtonIcon(btnSpeedLimit, GtkHelper.TintPixbuf(pix, DimR, DimG, DimB));
                }
            }
        }

        // Displays context menu with quick speed limit presets
        private void ShowSpeedLimiterMenu(Gdk.EventButton ev)
        {
            var menu = new Menu();
            var titleItem = new MenuItem("⚡ Bandwidth Limit");
            titleItem.Sensitive = false;
            menu.Append(titleItem);
            menu.Append(new SeparatorMenuItem());

            int currentLimit = Config.Instance.EnableSpeedLimit ? Config.Instance.DefaltDownloadSpeed : 0;

            var offItem = new MenuItem(currentLimit == 0 ? "● Unlimited (Off)" : "Unlimited (Off)");
            offItem.Activated += (_, _) =>
            {
                Config.Instance.EnableSpeedLimit = false;
                Config.SaveConfig();
                ApplicationContext.BroadcastConfigChange();
                UpdateSpeedLimitButton();
            };
            menu.Append(offItem);
            menu.Append(new SeparatorMenuItem());

            int[] presets = { 256, 512, 1024, 2048, 5120, 10240 };
            foreach (var preset in presets)
            {
                var label = FormattingHelper.FormatSize(preset * 1024.0) + "/s";
                var isSelected = Config.Instance.EnableSpeedLimit && Config.Instance.DefaltDownloadSpeed == preset;
                var item = new MenuItem(isSelected ? $"● {label}" : label);
                var p = preset;
                item.Activated += (_, _) =>
                {
                    Config.Instance.EnableSpeedLimit = true;
                    Config.Instance.DefaltDownloadSpeed = p;
                    Config.SaveConfig();
                    ApplicationContext.BroadcastConfigChange();
                    UpdateSpeedLimitButton();
                };
                menu.Append(item);
            }

            menu.Append(new SeparatorMenuItem());

            var isCustom = Config.Instance.EnableSpeedLimit && Array.IndexOf(presets, Config.Instance.DefaltDownloadSpeed) < 0 && Config.Instance.DefaltDownloadSpeed > 0;
            var customLabel = isCustom
                ? $"● Custom ({FormattingHelper.FormatSize(Config.Instance.DefaltDownloadSpeed * 1024.0)}/s)..."
                : "Custom Limit...";
            var customItem = new MenuItem(customLabel);
            customItem.Activated += (_, _) =>
            {
                ApplicationContext.PlatformUIService.ShowSpeedLimiterWindow();
            };
            menu.Append(customItem);

            menu.ShowAll();
            menu.PopupAtPointer((Gdk.Event)ev);
        }

        private void BtnScheduler_Clicked(object? sender, EventArgs e)
        {
            SchedulerClicked?.Invoke(sender, e);
        }

        private void BtnMonitoring_Clicked(object? sender, EventArgs e)
        {
            BrowserMonitoringButtonClicked?.Invoke(sender, e);
        }

        // Creates the top action toolbar styled as a card matching the sidebar background
        private Widget CreateToolbar()
        {
            var toolbar = new HBox(false, 5)
            {
                Margin = 4,
                MarginStart = 8,
                MarginEnd = 8
            };
            btnNew = CreateButtonWithContent("links-line", TextResource.GetText("DESC_NEW"),
                PrimaryInkR, PrimaryInkG, PrimaryInkB, primary: true);
            toolbar.PackStart(btnNew, false, false, 0);
            btnDel = CreateButtonWithContent("delete-bin-7-line", TextResource.GetText("DESC_DEL"), DestructR, DestructG, DestructB);
            toolbar.PackStart(btnDel, false, false, 0);
            btnOpenFile = CreateButtonWithContent("external-link-line", TextResource.GetText("CTX_OPEN_FILE"), SuccessR, SuccessG, SuccessB);
            toolbar.PackStart(btnOpenFile, false, false, 0);
            btnOpenFolder = CreateButtonWithContent("folder-shared-line", TextResource.GetText("CTX_OPEN_FOLDER"), AmberR, AmberG, AmberB);
            toolbar.PackStart(btnOpenFolder, false, false, 0);
            btnResume = CreateButtonWithContent("play-line", TextResource.GetText("MENU_RESUME"), SuccessR, SuccessG, SuccessB);
            toolbar.PackStart(btnResume, false, false, 0);
            btnPause = CreateButtonWithContent("pause-line", TextResource.GetText("MENU_PAUSE"), AmberR, AmberG, AmberB);
            toolbar.PackStart(btnPause, false, false, 0);

            btnMenu = CreateButtonWithContent("menu-line", accessibleName: TextResource.GetText("LBL_MENU"));
            toolbar.PackEnd(btnMenu, false, false, 0);

            searchEntry = new Entry()
            {
                WidthChars = 15,
                PlaceholderText = TextResource.GetText("LBL_SEARCH"),
                Valign = Align.Center
            };
            searchEntry.StyleContext.AddClass("toolbar-search-entry");
            searchEntry.Activated += (a, b) =>
            {
                searchKeyword = searchEntry.Text;
                finishedDownloadFilter.Refilter();
                inprogressDownloadFilter.Refilter();
                UpdateEmptyStates();
            };
            toolbar.PackEnd(searchEntry, false, false, 0);

            // Container card for top action toolbar matching the sidebar surface
            var toolbarCard = new EventBox
            {
                Margin = 6,
                MarginBottom = 2
            };
            toolbarCard.StyleContext.AddClass("main-toolbar");
            toolbarCard.Add(toolbar);
            toolbarCard.ShowAll();

            btnOpenFile.Visible = false;
            btnOpenFolder.Visible = false;
            btnResume.Visible = false;
            btnPause.Visible = false;
            newButton = new ButtonWrapper(this.btnNew);
            deleteButton = new ButtonWrapper(this.btnDel);
            pauseButton = new ButtonWrapper(this.btnPause);
            resumeButton = new ButtonWrapper(this.btnResume);
            openFileButton = new ButtonWrapper(this.btnOpenFile);
            openFolderButton = new ButtonWrapper(this.btnOpenFolder);

            btnMenu.Clicked += BtnMenu_Clicked;

            return toolbarCard;
        }

        // Window-level shortcuts (GTK 4 app-action parity): F1 and Alt+Return are
        // handled without Ctrl; everything else below is Ctrl-gated. GTK bubbles
        // unhandled keys up here, so this works whatever widget has focus.
        private void WireKeyboardShortcuts()
        {
            KeyPressEvent += (_, args) =>
            {
                try
                {
                    var key = args.Event.Key;
                    var state = args.Event.State;
                    bool ctrl = (state & Gdk.ModifierType.ControlMask) != 0;
                    bool shift = (state & Gdk.ModifierType.ShiftMask) != 0;
                    bool alt = (state & Gdk.ModifierType.Mod1Mask) != 0;
                    // F1 is NOT Ctrl-gated, so it must precede the Ctrl early-return
                    if (key == Gdk.Key.F1)
                    {
                        MenuAbout_Activated(null, EventArgs.Empty);
                        args.RetVal = true;
                        return;
                    }
                    // Alt+Return opens properties for the selected download
                    if (alt && (key == Gdk.Key.Return || key == Gdk.Key.KP_Enter))
                    {
                        UIActions.ShowSeletectedItemProperties();
                        args.RetVal = true;
                        return;
                    }
                    if (!ctrl)
                    {
                        return;
                    }
                    // Ctrl+Shift chords dispatch before the plain-Ctrl letters below
                    if (shift && (key == Gdk.Key.q || key == Gdk.Key.Q))
                    {
                        SchedulerClicked?.Invoke(this, EventArgs.Empty);
                        args.RetVal = true;
                        return;
                    }
                    if (shift && (key == Gdk.Key.v || key == Gdk.Key.V))
                    {
                        YoutubeDLDownloadClicked?.Invoke(this, EventArgs.Empty);
                        args.RetVal = true;
                        return;
                    }
                    switch (key)
                    {
                        case Gdk.Key.f:
                        case Gdk.Key.F:
                            searchEntry?.GrabFocus();
                            args.RetVal = true;
                            break;
                        case Gdk.Key.n:
                        case Gdk.Key.N:
                            btnNew?.Activate();
                            args.RetVal = true;
                            break;
                        case Gdk.Key.t:
                        case Gdk.Key.T:
                            ThemeManager.ToggleTheme();
                            args.RetVal = true;
                            break;
                        case Gdk.Key.b:
                        case Gdk.Key.B:
                            BatchDownloadClicked?.Invoke(this, EventArgs.Empty);
                            args.RetVal = true;
                            break;
                        case Gdk.Key.i:
                        case Gdk.Key.I:
                            MenuImportExport_Activated(null, EventArgs.Empty);
                            args.RetVal = true;
                            break;
                        case Gdk.Key.comma:
                            MenuSettings_Activated(null, EventArgs.Empty);
                            args.RetVal = true;
                            break;
                        case Gdk.Key.q:
                        case Gdk.Key.Q:
                            MenuExit_Activated(null, EventArgs.Empty);
                            args.RetVal = true;
                            break;
                        case Gdk.Key.o:
                        case Gdk.Key.O:
                            UIActions.OpenSelectedFile();
                            args.RetVal = true;
                            break;
                        case Gdk.Key.c:
                        case Gdk.Key.C:
                            // Copy the row's URL from the list the user is actually looking at
                            if (IsInProgressView()) UIActions.CopyURL1(); else UIActions.CopyURL2();
                            args.RetVal = true;
                            break;
                        case Gdk.Key.r:
                        case Gdk.Key.R:
                            UIActions.RestartDownload();
                            args.RetVal = true;
                            break;
                        case Gdk.Key.s:
                        case Gdk.Key.S:
                            UIActions.SaveAs();
                            args.RetVal = true;
                            break;
                    }
                }
                catch { }
            };
        }
    }
}
