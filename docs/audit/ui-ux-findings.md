# FetchFlow UI/UX Findings Register

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Audit scope: GNOME (primary), KDE Plasma (primary), Cinnamon/GTK 3-only (compatibility).
Severity: **P0** data loss/crash/core task blocked · **P1** core journey impaired, inaccessible,
or desktop integration broken · **P2** usability/polish · **P3** refinement.

Status values: `open` · `fixed` · `accepted` (documented limitation) · `needs-decision`.

## Register

| ID | Sev | Desktop | Finding | Evidence | Status |
|:---|:---|:---|:---|:---|:---|
| FF-UI-001 | P1 | KDE, Cinnamon | The GTK 3 frontend forces `Gtk.Settings.Default.ThemeName = "Adwaita"` at startup and on every theme change, overriding the user's chosen GTK theme (Breeze on Plasma, Mint-Y on Cinnamon) for every widget the app CSS does not cover. | `Utils/ThemeManager.cs` (`AdwaitaThemeName` assignment in `ApplyTheme`) | fixed in GTK 4 (never sets the theme name, only the dark flag); accepted for the GTK 3 compatibility frontend |
| FF-UI-002 | P1 | GNOME, KDE | Two notification transports: download-complete uses the D-Bus `org.freedesktop.Notifications` helper, while media-capture and generic notifications shell out to `notify-send`. On a session without that binary the second path fails silently. | `GtkPlatformUIService.ShowMediaNotification` / `ShowDesktopNotification` vs `Utils/DesktopNotificationHelper.cs` | fixed |
| FF-UI-003 | P1 | GNOME, KDE | No accessibility names anywhere in the GTK UI. Icon-only controls (main menu, speed limiter, theme toggle, update dot) are announced as unlabeled buttons by Orca/screen readers. | Zero `Atk`/`Accessible` references before this audit | fixed in GTK 4 via tooltips and accessible labels on all actions, controls, and dialog fields; toolbar/header addressed in GTK 3 |
| FF-UI-004 | P2 | GNOME, KDE | Keyboard accelerators are largely absent. GTK 4 now registers Gio actions with Ctrl+N (new download), Ctrl+F (search), Ctrl+T (theme) and Ctrl+Q (quit); GTK 3 handles Ctrl+F and Ctrl+N at window level. No other shortcut exists and there is no shortcut reference UI. | `XDM.Gtk4.UI/AppActions.cs`, `XDM.Gtk.UI/MainWindow.cs` (`WireKeyboardShortcuts`) | fixed in GTK 4 (registered accelerators for Ctrl+N, Ctrl+F, Ctrl+T, Ctrl+Q, F5, Ctrl+B, Ctrl+Shift+Q, Ctrl+I, Ctrl+,, F1) |
| FF-UI-005 | P2 | GNOME | The app runs `Gtk.Application.Init` with a plain `Gtk.Window`, not `Gtk.ApplicationWindow` with registered `GSimpleAction`s. GNOME cannot surface app actions/menus and window/application lifetime is hand-managed. | `Program.cs` startup path | fixed in GTK 4 (`Gtk.Application` + `ApplicationWindow` with registered `Gio.Action`s) |
| FF-UI-006 | P1 | all | `gtk_tree_view_get_bin_window` is P/Invoked directly from `libgtk-3.so.0`. Correct for the GTK 3 frontend, but it must never be compiled into shared or GTK 4 code. | `Utils/TreeViewSelectionHelper.cs` | fixed (`XDM.Gtk4.UI` contains zero references or P/Invokes to GTK 3) |
| FF-UI-007 | P2 | GNOME, KDE | Download-row geometry is hard-coded in device pixels (row renderer paddings 10–16 px, gutter 52 px, header 52 px) rather than derived from font metrics, so row rhythm drifts at 150–200 % scaling and under large-font accessibility settings. | `MainWindow.cs` `DownloadRow*VerticalPadding`, `DownloadGutterWidth`; `docs/design.md` spacing section | fixed in GTK 4 (`ColumnView` layouts handle scalable typography and HiDPI natively) |
| FF-UI-008 | P1 | all | `GtkTreeView` + cell renderers are the core of the GTK 3 download lists and 12 Glade views. GTK 4 deprecates these APIs (since 4.10), so the list layer was the largest single porting cost. | 116 `TreeView` references across 11 GTK 3 C# files; GTK 4 frontend now uses `ColumnView` + `StringList` (`Downloads/DownloadListView.cs`) | replaced in GTK 4 with `ColumnView` + `SignalListItemFactory`; GTK 3 keeps TreeView by design |
| FF-UI-009 | P2 | GNOME, KDE | The base theme is applied to `Gdk.Screen.Default`, which GTK 4 no longer has (per-display style managers instead); theming must be re-plumbed for GTK 4 rather than reused. Measured: all 14 theme files fail GTK 4 CSS parsing (one error discards the remainder), so a GTK 4 stylesheet is required, not a port. | `ThemeManager.ApplyTheme`, `Program.cs` CSS provider setup; GTK 4 CSS probe results in `docs/audit/gtk4-coupling-inventory.md` | fixed in GTK 4 (`Gtk4Theme` uses per-display `CssProvider` with `gtk4-dark.css` and `gtk4-light.css`) |
| FF-UI-010 | P3 | GNOME, KDE | GTK 4.20+ deprecates `gtk-application-prefer-dark-theme` in favour of `GtkSettings:gtk-interface-color-scheme`. The GTK 4 frontend sets the old property, so a session log gets one deprecation warning per theme swap. | `Gtk4Theme.Apply`, runtime warning `Use GtkSettings:gtk-interface-color-scheme instead` | fixed in GTK 4 (`GtkInterfaceColorScheme` set with graceful fallback; warning eliminated) |

| FF-UI-011 | P0 | GNOME, KDE | The GTK 4 search box never filtered the list: the entry re-ran the view, but the keyword never reached the store (`DownloadListView.Filter`/`DownloadStore.Filter` had no callers). | `MainWindow.cs` `OnSearchChanged`; `DownloadStore.Filter` unused | fixed (search now filters in place and keeps the active view) |
| FF-UI-012 | P0 | GNOME, KDE | Every progress event rebuilt the whole GTK 4 list and created a new `MultiSelection`, so a running download cleared the user's multi-selection. `ApplicationCore` does not throttle progress. | `ApplicationCore.DownloadProgressChanged`; `Gtk4ApplicationWindow.RunOnUIThread`; `DownloadListView.Bind` | fixed (one persistent model + selection; progress refreshes only the bound cells, throttled to 4 Hz per transfer) |
| FF-UI-013 | P1 | GNOME, KDE | The theme mode was saved but never applied: the GTK 4 window always started dark, and the settings dropdown used reversed indices (Follow System = 0) against the shared `Config.ThemeMode` (0 light, 1 dark, 2 follow system). | `Gtk4Theme` (no `Config` read), `MainWindow` `Apply(..., IsDarkActive)`, `SettingsDialog` theme options | fixed (mode applied at startup and on save, follow-system tracks the session, Ctrl+T persists) |
| FF-UI-014 | P1 | GNOME, KDE | The browser-monitoring checkbox flipped `Config.IsBrowserMonitoringEnabled` itself and then raised the event whose core handler flips it again, so the toggle was a no-op. | `MainWindow` monitoring handler vs core `Application` handler | fixed (window only raises the event; core owns the flip) |
| FF-UI-015 | P1 | GNOME, KDE | `PromtForCredentials` returned null, so an authentication-protected download could never prompt and failed silently. | `Gtk4PlatformUIService.PromtForCredentials` | fixed (`CredentialsDialog`) |
| FF-UI-016 | P1 | GNOME, KDE | Batch Download and multi-link captures ended at a "scheduled for GTK 4 parity in Step 6.2" dialog instead of a selection window. | `Gtk4PlatformUIService.ShowDownloadSelectionWindow` | fixed (`DownloadSelectionWindow` over the shared `DownloadSelectionUIController`) |
| FF-UI-017 | P1 | GNOME, KDE | The component updater was an empty stub and the main menu had no "Check for Update" entry. | `Gtk4UpdaterUIStub`, main menu | fixed (`UpdaterWindow`, menu entry wired to the core handlers) |
| FF-UI-018 | P1 | GNOME, KDE | "Remove Finished Downloads" existed in the core but had no GTK 4 entry point, and the window's `ClearAllFinishedClicked` event was never raised. | `Gtk4ApplicationWindow` event; main menu | fixed (menu entry with confirmation, forwarded to core) |
| FF-UI-019 | P1 | GNOME, KDE | The Browser Monitoring deep link opened the Network page (index 2) instead of Monitoring (index 4). | `Gtk4PlatformUIService.ShowBrowserMonitoringDialog` | fixed (named page constants) |
| FF-UI-020 | P1 | GNOME, KDE | Refresh-links and the bandwidth-limiter window were stub dialogs. | `Gtk4PlatformUIService.ShowRefreshLinkDialog` / `ShowSpeedLimiterWindow` | fixed (`LinkRefreshWindow`, `SpeedLimiterWindow` over the shared controllers) |
| FF-UI-021 | P2 | GNOME, KDE | The footer printed developer text and an untranslated view key (`GTK 4 · finished · 10 downloads · 6 categories`). | `MainWindow.UpdateStatus` | fixed (footer reserved for messages) |
| FF-UI-022 | P2 | GNOME, KDE | About hardcoded the edition string and `Version 9.1.16.0`. | `MainWindow.OnAbout` | fixed (reads `AppInfo`) |
| FF-UI-023 | P2 | GNOME, KDE | The extension badge was a static "Extension: Ready" and never reflected the live IPC state. | `MainWindow` vs GTK 3 `UpdateExtensionStatus` | fixed (subscribes to `ApplicationEvent`, tri-state badge with colour classes) |
| FF-UI-024 | P2 | GNOME, KDE | The Complete view and the Active view shared one list surface, and the sidebar had no Active/Complete card; both are visually distinct in GTK 3. | `gtk4-dark.css`/`gtk4-light.css` single `view_bg`; sidebar `ListBox` only | fixed (per-view surface token plus a raised `sidebar-status-card`) |
| FF-UI-025 | P2 | GNOME, KDE | The header bar carried six labelled buttons plus search, sort, menu and theme controls, and column headers had no dividers so the resize edge was invisible. | `MainWindow.BuildHeaderBar`, `gtk4-*.css` header rules | fixed (New is the only labelled action; the rest are icon buttons; header dividers added) |
| FF-UI-026 | P2 | GNOME, KDE | ~22 GTK 4 strings still fall back to inline English because the keys are absent from `Lang/English.txt` (queue, batch, video, settings, properties labels). | localization key audit over `XDM.Gtk4.UI` | fixed for raw keys (21 keys added to English/Hindi/Hinglish); inline English literals remain, tracked as FF-UI-036 |
| FF-UI-027 | P2 | GNOME, KDE | The gutter header shows the tri-state select-all glyph but the header is not clickable: `Gtk.ColumnViewColumn` exposes no header-click signal. | `DownloadListView.TryWireSelectAllHeader` | fixed (capture-phase click gesture attached to the gutter header, located by its glyph; runtime-verified under Xvfb) |
| FF-UI-028 | P2 | GNOME, KDE | Settings uses a top tab bar with a bottom Cancel/Save row instead of GNOME's sidebar pages with header-bar actions. | `SettingsDialog` | fixed (sidebar pages via `Gtk.Stack` + `Gtk.StackSidebar`, Cancel/Save moved into the header bar) |
| FF-UI-029 | P2 | GNOME, KDE | The GTK 4 settings dialog has no colour-scheme picker and the stylesheet hardcodes Charcoal Blue, so the 14 shared schemes do not apply. | `SettingsDialog` + `Gtk4Theme.ApplyColorScheme` | fixed (7-scheme picker per mode, accent/hover tokens rewritten from `ColorSchemeTable` when the stylesheet loads) |
| FF-UI-030 | P2 | GNOME, KDE | The yt-dlp media stream downloader has no GTK 4 UI (the menu entry was removed rather than left as a dead end). | `VideoDownloaderWindow` (GTK 4) over the shared `VideoDownloaderUIController` | fixed (four pages: URL/cookies/credentials, resolving spinner, video list with per-row ticks + Select All + quality, error page; smoke-covered and screenshot-verified) |
| FF-UI-031 | P2 | GNOME, KDE | Media grabber and extension-registration entry points are absent in GTK 4. | `Gtk4PlatformUIService` | open (planned: parity phase) |
| FF-UI-032 | P3 | GNOME, KDE | Drag rubber-band multi-selection is missing (`ColumnView` has no built-in equivalent). | GTK 3 `RubberBanding = true` | accepted: `ColumnView` has no rubber-band API and a custom drag hit-test was judged not worth the regression risk; Ctrl/Shift-click, the per-row checkboxes and Ctrl+A cover multi-select |
| FF-UI-033 | P3 | GNOME, KDE | Sidebar rows show no Active/Complete/category counts. | `MainWindowChrome.UpdateSidebarCounts` | fixed (Active/Complete and per-category counts) |
| FF-UI-034 | P3 | GNOME, KDE | The GTK 4 shell is hand-built on plain GTK widgets rather than libadwaita, so it does not follow GNOME's widget styling. | `XDM.Gtk4.UI.csproj` (no `GirCore.Adw-1`) | fixed (`GirCore.Adw-1` 0.8.1: `AdwApplicationWindow`, `AdwHeaderBar` + `AdwToolbarView`, `AdwStyleManager`, `AdwStatusPage`, `AdwAboutWindow`; libadwaita added to all GTK 4 packages) |
| FF-UI-035 | P3 | GNOME, KDE | Download rows show only a status line; GTK 3 adds a domain · folder · extension meta line. | `MainWindow.BuildMetaText` | fixed (domain · folder · extension, ellipsized middle) |

| FF-UI-036 | P3 | GNOME, KDE | ~12 inline English literals remain in the GTK 4 UI (settings row labels such as "Maximum simultaneous downloads", antivirus/automation headings, browse buttons) instead of `TextResource` keys. | `SettingsDialog`, `QueueSchedulerDialog`, `BatchDownloadWindow`, `ChecksumDialog`, `PropertiesDialog` | fixed (65 widget labels keyed across the five dialogs; every referenced key now exists in English/Hindi/Hinglish) |
| FF-UI-037 | P1 | GNOME, KDE | When the core switches to the Finished view programmatically (no active downloads), the list changed but the sidebar still highlighted Active and the title kept the old subtitle, so the two disagreed. | `Gtk4ApplicationWindow.SwitchToFinishedView` vs `MainWindow.SelectView` | fixed (`SelectView` now moves the sidebar highlight and subtitle) |
| FF-UI-038 | P1 | GNOME, KDE | The new domain · folder meta line had no ellipsis, so its natural width pushed the Size and Progress columns outside a 900 px window. | `DownloadListView` name-column label | fixed (middle ellipsis on the meta line, verified by screenshot at 900x560) |

## What is still unverified

These need a live session and are **not** claimed as findings yet:

- **KDE Plasma — deferred.** There is no Plasma session in the current environment
  (CachyOS GNOME), so CSD/SSD behaviour, Breeze contrast, tray/SNI rendering and
  portal file-chooser behaviour are unverified. The GTK 4 frontend now leaves the GTK
  theme name alone, which removes the known Breeze-override cause by design, but this
  is not the same as a live pass.
- GNOME: headerbar button ordering against the platform default, notification
  appearance in the GNOME message tray, Orca navigation through the download list.
- Cinnamon/XFCE/MATE: GTK 3 compatibility run of the P0 journeys.
- Any backend-specific (Wayland vs X11) difference beyond the startup path already
  exercised by the smoke suite.

## Related

- `docs/audit/support-matrix.md` — environment matrix and recorded baseline
- `docs/audit/gtk4-coupling-inventory.md` — migration inventory
