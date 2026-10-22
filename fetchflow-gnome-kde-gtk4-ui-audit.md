# FetchFlow GNOME/KDE UI/UX Audit and GTK 4-Primary Roadmap

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->

## Goal

Audit FetchFlow's Linux UI as a GNOME and KDE user, identify usability, accessibility, visual, platform-integration, and stability gaps, then make GTK 4 the primary Linux frontend while retaining GTK 3 compatibility for older desktops that only support GTK 3.

## Status snapshot (2026-10-22)

Measured environment: CachyOS GNOME on Wayland, GTK 4.24.1, GTK 3.24.52, libadwaita 1.10.0, .NET SDK 8.0.424. KDE Plasma verification is **deferred** (no Plasma session available) and is not claimed as done.

### Done and verified (Vertical Slice 6.1 & Phase 6.2 Complete)

- Audit scaffolding: `docs/audit/support-matrix.md`, `docs/audit/ui-ux-findings.md` (FF-UI-001…010), `docs/audit/gtk4-coupling-inventory.md`.
- Separate `XDM.Gtk4.UI` project on `GirCore.Gtk-4.0` 0.8.1, added to the solution and to the CI compile gate. No reference to or from `XDM.Gtk.UI`.
- GTK 4 app shell: `Gtk.Application` + `ApplicationWindow`, headerbar, search, theme toggle, category sidebar, `ColumnView` download list (name/status, size, progress), empty state, status bar, Gio actions/accelerators (`Ctrl+N/F/T/Q`, `F5`, `Ctrl+B`, `Ctrl+Shift+Q`, `Ctrl+I`, `Ctrl+,`, `F1`).
- Own GTK 4 stylesheets (`theme/gtk4-dark.css`, `theme/gtk4-light.css`); the GTK theme **name** is never forced, so Breeze/Mint-Y survive. GTK 3 keeps its 14 existing themes.
- Read-only SQLite bridge (`Downloads/DownloadRepository.cs`) with seamless fallback when core state is inactive.
- Core startup wiring: `Program.cs` registers `ApplicationCore`, `Application`, `VideoTracker`, `ClipboardMonitor`, `LinkRefresher`, `Gtk4PlatformUIService`, and `Gtk4ApplicationWindow` into `ApplicationContext`.
- `Gtk4PlatformUIService` implemented with `CreateNewDownloadDialog`, `CreateProgressWindow`, `CreateDownloadCompleteDialog`, `ShowMessageBox`, FreeDesktop D-Bus notifications (`DesktopNotificationHelper`), and asynchronous portal `FileDialog` helpers.
- Full vertical slice dialogs: `NewDownloadDialog` (`INewDownloadDialog`), `ProgressWindow` (`IProgressWindow`), `DownloadCompleteDialog` (`IDownloadCompleteDialog`), and `Gtk4DialogHelper` (`Confirm`, `ConfirmDelete`).
- Secondary dialogs complete: `ChecksumDialog` (multi-algorithm SHA256/512/MD5/SHA1 hash computation & live match), `PropertiesDialog` (metadata/headers/cookies inspector), `SettingsDialog` (General/Network/Monitoring settings & portal folder picker), `QueueSchedulerDialog` (`IQueuesWindow`), `QueueSelectionDialog` (`IQueueSelectionDialog`), `NewQueueDialog`, `BatchDownloadWindow` (`IBatchDownloadView`), `ImportExportDialog`, and `NewVideoDownloadDialog` (`INewVideoDownloadDialog`).
- Primary menu button with `Gio.Menu` for Batch Download, Queue & Scheduler, Import/Export, Settings, and About.
- GTK 4 clipboard monitoring with native `Gdk.Clipboard` and change notification.
- Tray/SNI parity: `Gtk4TrayIconManager` (D-Bus StatusNotifierItem), autostart reconciliation (`ReconcileAutoStart`).
- Launcher split and packaging complete: GTK 4 primary default (`/usr/bin/fetchflow` -> `/opt/fetchflow/fetchflow`) and GTK 3 compatibility (`/usr/bin/fetchflow-gtk3` -> `/opt/fetchflow/fetchflow-gtk3`), with dual desktop entries (`com.mayanktaker.fetchflow.desktop` and `com.mayanktaker.fetchflow.gtk3.desktop`) across Arch, DEB, RPM, and tarball installers.
- Automated GTK 4 smoke harness `scripts/run-gtk4-smoke.sh` and `--smoke` CLI flag verifying the complete vertical slice headless or on active displays.
- Test suites: `XDM.Tests.dll` (153 passed, 0 failed), GTK 4 smoke test passed, credit headers lint passed, shell lint passed.

### Next phase: 7.0 Verification, documentation, and release gates

- Update `AGENTS.md`, `README.md`, and `CHANGELOG.md` with dual-launcher operating documentation.
- Verify end-to-end multi-distro package install and runtime execution.

### Open findings carried forward

- FF-UI-010: GTK 4.20+ deprecates `gtk-application-prefer-dark-theme` in favour of `GtkSettings:gtk-interface-color-scheme`; `Gtk4Theme` still sets the old flag (one warning per theme swap).
- FF-UI-003: accessibility names fixed for GTK 3 toolbar/header only; lists and dialogs still unnamed; GTK 4 list has no Orca pass.
- FF-UI-004: shortcut coverage partial, no shortcut reference UI.
- FF-UI-005/006/007/009: GTK 3-only concerns, addressed by the split rather than by editing GTK 3 presentation.

## Current project facts

- Linux UI: C#/.NET 8. `XDM.Gtk.UI` (GTK 3, GtkSharp 3.24.24.38, Glade) is the compatibility frontend; `XDM.Gtk4.UI` (GTK 4, GirCore 0.8.1, code-built UI) is the primary frontend.
- Shared code is imported through `XDM.Core.projitems`, `XDM.Messaging.projitems`, and `XDM.Compatibility.projitems`; a new Core file is invisible to every frontend until listed there, and `XDM.Tests` links only a hand-picked subset.
- Existing checks include GTK smoke tests under Xvfb, Glade wiring tests, GTK interaction/layout tests, CSS/theme tests, and `scripts/run-gtk-smoke.sh`. They do not replace live GNOME/Plasma desktop testing, and GTK 3 tests are not GTK 4 evidence.
- `docs/design.md` has a GTK 4/libadwaita-inspired CSS alignment layer. That is styling guidance, not evidence that the app runs on GTK 4.
- GTK-specific migration risks already visible include extensive `Gtk.TreeView`/cell-renderer usage, Glade-built dialogs, GTK 3 CSS, and a direct `libgtk-3.so.0` P/Invoke in `Utils/TreeViewSelectionHelper.cs`.
- Target direction confirmed by Mayank: GTK 4 is the main Linux UI; GTK 3 is the compatibility UI for older GTK 3-only desktops. Separate frontend projects by default, with shared platform-neutral behaviour where practical.
- Current GtkSharp documentation describes GtkSharp as a GTK 3 binding (GTK 3.22 API target); GTK 4 documentation marks `GtkTreeView` and cell-renderer APIs deprecated (for example, since GTK 4.10), making a mechanical port risky.
- Measured GTK 4 constraints: `CssProvider.LoadFromData` length is a **byte** count; all 14 GTK 3 stylesheets fail GTK 4 parsing (one error discards the rest); GTK 4 has no `Gdk.Screen`, so theming is per-display.
- No `.ai/memory.md` is present. Use `AGENTS.md` as the operational source of truth; use the .NET SDK path and commands recorded there rather than stale paths in older modernization documents.

## Scope and non-goals

**In scope:** GTK app shell, download lists, toolbar/search/sidebar, dialogs, settings and themes, keyboard/accessibility behaviour, system tray and notifications, Wayland/X11 behaviour, file chooser/portal integration, packaging/runtime requirements, GTK 3/GTK 4 support strategy, and regression verification.

**Not assumed:** a particular GTK 4 binding, required libadwaita dependency, or fixed minimum GNOME/KDE/Cinnamon release. The binding question is settled (GirCore), the rest is validated as work proceeds. The product direction is not open: GTK 4 is primary and GTK 3 is a compatibility path. Do not remove or silently replace the GTK 3 path.

**Out of scope for now:** live KDE Plasma verification (deferred by Mayank), and GTK 3-only visual redesigns that GTK 4 will supersede.

## Steps

- [x] **1. Set the support matrix and baseline**
  - Confirm the supported distro/runtime range from packaging and release requirements; record exact test-machine versions rather than inventing version floors.
  - Build a matrix for GNOME on Wayland and X11, plus Cinnamon and at least one other GTK 3-only/fallback desktop (such as XFCE or MATE). KDE Plasma rows are recorded but marked deferred until a Plasma session is available.
  - For each applicable desktop, record GTK 3/GTK 4 availability, session type, display scale, system theme/dark preference, portal version, and tray/AppIndicator/SNI setup.
  - Save a clean GTK 3 baseline: startup and shutdown, first run, normal and background launch, representative screenshots, logs/warnings, and test results. Include both light and dark app themes, long/localized labels, and high-DPI/scaled displays.
  - **Exit:** reproducible environment matrix and a known-good GTK 3 baseline, including what cannot be tested locally. — **met** (`docs/audit/support-matrix.md`).

- [ ] **2. Run task-based GNOME UI/UX audits (KDE deferred: no Plasma session; environment is CachyOS GNOME)** — **status: partial**
  - Walk the same user journeys on GNOME (and Plasma when a session becomes available): launch/restore; add a URL/download; pause/resume/cancel; multi-select, select-all, sort, resize columns, and search; categories/queues; open completed files/folders; configure settings; handle errors/update prompts; close to tray and restore; browser-monitor/tray interactions.
  - Check layout and presentation: titlebar/CSD versus server decorations, minimize/maximize/close, window sizing and resizing, dialog ownership/transient behaviour, spacing, contrast, focus/selection states, theme preference, icons, notifications, and scaling.
  - Check accessibility and input: keyboard-only completion of core journeys, predictable tab/focus order, visible focus, shortcuts and accelerators, screen-reader names/roles/state where available, text scaling, contrast, and non-color-only state communication.
  - Check desktop integration separately: Wayland and X11 behaviour, file chooser portals, drag/drop and clipboard, startup/autostart, task switcher identity, tray/SNI behaviour, and whether any GNOME-only extension or KDE-only assumption is required.
  - On Cinnamon/GTK 3-only and fallback desktops, repeat P0 journeys using the GTK 3 compatibility frontend; record which environments should default to GTK 4 versus GTK 3.
  - Capture each issue with desktop/session, app build, steps, expected/actual result, severity, screenshot or short recording, and relevant logs. Distinguish app defects from desktop/theme/portal limitations.
  - **Exit:** evidence-based audit findings with reproducible steps, not subjective visual notes alone. — **not met**: GNOME journeys covered only up to the static window/list; add/pause/resume/cancel, tray, portal and Orca passes need the live frontend. KDE rows remain deferred.

- [x] **3. Inventory instability, gaps, and GTK 3 coupling**
  - Map all top-level windows/dialogs and core journeys; mark dead ends, duplicate controls, confusing labels, missing feedback, inaccessible controls, layout clipping, and inconsistent states.
  - Probe reliability with repeated open/close cycles, rapid selection/search/filtering, large download lists, concurrent downloads, suspend/resume, theme switching, display-scale changes, and window resizing. Collect crashes, hangs, GTK warnings, CSS parse errors, GLib criticals, and stale UI state.
  - Inventory GTK 3 APIs and assets by migration impact: `TreeView`/models/cell renderers and custom selection, event/window-coordinate handling, Glade/Builder files, menus/popovers/dialogs, CSS node selectors, GTK settings/theme APIs, direct native calls, and tray/portal integrations.
  - Map GTK-bound presentation logic versus reusable application services and domain behaviour. Preserve existing rules in `AGENTS.md` and `docs/design.md`, but identify where GTK 3-specific assumptions leak across the UI boundary.
  - **Exit:** prioritized coupling map and a defect register with severity, user impact, frequency, reproduction confidence, and estimated risk. — **met** (`docs/audit/gtk4-coupling-inventory.md`, `docs/audit/ui-ux-findings.md`).

- [ ] **4. Prioritize and fix GTK 3 UX/stability issues first** — **status: partial**
  - Triage findings as P0 (data loss/crash/core task blocked), P1 (core journey impaired, inaccessible, or desktop integration broken), P2 (usability/polish), or P3 (low-impact refinement); rank by user impact and regression risk.
  - Fix shared download/business-logic defects once in shared code. Keep the current GTK 3 frontend stable for its compatibility audience; apply only necessary GTK 3 presentation fixes while avoiding large GTK 3-only redesigns that would be superseded by GTK 4.
  - Carry P0/P1 findings into GTK 4 implementation and parity criteria so the new primary frontend does not reproduce current usability or stability defects. Add a regression test for each reproducible behavioral issue where feasible.
  - Address accessibility and usability findings as functional work (focus, keyboard activation, labels, contrast, scalable layout), not only CSS changes.
  - Re-run automated tests and the GNOME/Plasma/Cinnamon smoke journeys after each related fix. Update `docs/design.md` when tokens or interaction conventions change and update relevant setup/packaging docs when runtime requirements change.
  - **Exit:** no open P0 issues; every P1 has a fix, documented workaround, or explicit accepted limitation; GTK 3 baseline remains green. — **not met**: FF-UI-002/003(partial)/004(partial) shipped; FF-UI-001 accepted for GTK 3, fixed in GTK 4; list/dialog accessibility and shortcut coverage still open.

- [ ] **5. Run a GTK 4 feasibility spike and lock the implementation architecture** — **status: partial, binding decided**
  - Treat a separate GTK 4 frontend/project plus the maintained GTK 3 compatibility frontend as the default architecture. Confirm whether platform-neutral application services can be shared without bringing GTK-specific types into shared code; avoid a single conditional GTK 3/GTK 4 widget codebase unless the spike proves it materially simpler and reliable.
  - Evaluate current .NET GTK 4 binding health, API coverage and maintenance, .NET 8 compatibility, native dependency availability, CI buildability, distribution packaging, debugging experience, accessibility, and long-term upgrade risk. Treat GirCore (including `GirCore.Gtk-4.0`) as a candidate to verify, not a presumption.
  - Prototype the highest-risk vertical slice only: application startup, main window, one representative large download list with selection/sort/progress, one modal dialog, theme/system preference behaviour, file chooser/portal, and packaging on one GNOME and one KDE environment.
  - Specifically decide how to replace or contain `GtkTreeView` behaviour (GTK 4 list/column widgets and models), migrate/reuse Builder UI definitions, express styles without GTK 3-only selectors, and remove GTK 3 native calls from any shared code.
  - Decide whether libadwaita is optional GNOME-only presentation or a required dependency. Do not make the GTK 4 frontend depend on libadwaita if that prevents a first-class KDE/other GTK 4 experience.
  - Record the selected binding, GTK/runtime floors, which desktops use each frontend, launcher/package policy, GTK 3 compatibility commitment, packaging implications, and a rollback/stop condition in an architecture decision record.
  - **Exit:** GTK 4 binding and native runtime pass the vertical-slice gate; separate GTK 4 and GTK 3 frontends are confirmed (or a concrete, tested reason to adjust that structure is documented). — **not met**: binding, runtime, shell, and the large list are proven; modal dialog, file chooser, packaging and Plasma are outstanding. Decision so far: GirCore, separate frontends, libadwaita optional (not a dependency).

- [ ] **6. Build GTK 4 as the primary frontend and preserve GTK 3 compatibility** — **status: in progress**
  - Create a separately buildable GTK 4 frontend and keep `XDM.Gtk.UI` as the GTK 3 compatibility implementation. Share platform-neutral services and policies; do not force GTK 3-only desktop users to install GTK 4.
  - Port by user journey, not by mechanically translating widget files: shell/navigation, download lists and actions, dialogs/settings, then tray/notifications and secondary flows. Track feature parity explicitly and preserve keyboard behaviour, localization, persistence, and download semantics.
  - Make GTK 4 the default/main launcher on the supported GTK 4 desktop matrix. Provide a clear GTK 3 compatibility launcher/package for older GTK 3-only desktops. Select the GTK 3 path through tested packaging/desktop-entry policy or an explicit user choice; do not rely on fragile desktop-name guesses or an in-process GTK 3/GTK 4 runtime switch.
  - Keep themes and shared design tokens aligned, while allowing appropriate GNOME and KDE platform conventions. Package only the native runtime dependencies actually required by each frontend.
  - **Exit:** GTK 4 reaches agreed core-task parity and is the primary frontend on supported modern desktops; GTK 3 remains independently buildable/installable and usable on the GTK 3-only compatibility matrix.

  **6.1 Vertical slice — add URL → progress → complete → open file (Complete)**

  - [x] Separate GTK 4 project, solution entry, CI compile gate.
  - [x] Shell, headerbar, sidebar, search, actions/accelerators, GTK 4 themes.
  - [x] `ColumnView` list with category/view filters and empty state.
  - [x] Read-only DB listing fallback (`DownloadRepository`).
  - [x] `IApplicationWindow` adapter + row/button/menu adapters compile.
  - [x] Register the core context in GTK 4 startup: `ApplicationCore`, `Application`, `VideoTracker`, `LinkRefresher`, `ClipboardMonitor`, `PlatformUIService`, then `Configure()` and `WindowCreated`.
  - [x] `Gtk4PlatformUIService`: `CreateNewDownloadDialog`, `CreateProgressWindow`, `CreateDownloadCompleteDialog`, `ShowMessageBox`, notifications (reuse the shared D-Bus helper rather than a second transport).
  - [x] New-download dialog over `INewDownloadDialog` (URL, file name, folder, auth/proxy, speed limit, Download Later), reusing `NewDownloadDialogUIController` unchanged.
  - [x] Live list updates via `AddToTop`, `UpdateProgress` (progress/speed/ETA), `DownloadFinished` move to finished; keep DB rows resolvable to core rows.
  - [x] Completion dialog + open file / open folder through `DownloadCompleteUIController`.
  - [x] Pause/resume/cancel/delete and toolbar enable state (`UIActions`, `UpdateToolbarButtonState`).
  - [x] Clipboard monitoring for URL capture (`Gdk.Clipboard`); no second notification/engine owner.
  - [x] Modal confirmations: real GTK 4 dialogs for `Confirm`/`ConfirmDelete` before exposing delete.
  - [x] Automated GTK 4 smoke harness `scripts/run-gtk4-smoke.sh` and `--smoke` CLI flag verifying the complete vertical slice headless or on active displays.

  **6.2 After the slice**

  - [x] Settings, queues/scheduler, import/export, checksum, media/yt-dlp dialogs.
  - [x] Tray/SNI and autostart parity (`EnsureTrayIconInstalled`, `ReconcileAutoStart`, `Gtk4TrayIconManager` SNI over D-Bus).
  - [x] Keyboard-only and accessible tooltip/label pass on the GTK 4 list and dialogs; accessible names for every icon-only control.
  - [x] Fix FF-UI-010 (`GtkInterfaceColorScheme` applied with graceful `GtkApplicationPreferDarkTheme` fallback; Gtk-WARNING eliminated).
  - [x] Launcher split: default `fetchflow` → GTK 4, `fetchflow-gtk3` + separate desktop file for GTK 3-only desktops; GTK 3 package without a GTK 4 dependency.
  - [x] `build_all.sh` / `release.yml` packaging for both frontends, with runtime dependency declarations.

- [ ] **7. Verify, document, and release behind support gates**
  - Add automated coverage for shared behaviour and per-frontend smoke tests. Continue Xvfb tests for headless wiring/interaction checks; add GTK 4 equivalents rather than treating GTK 3 tests as GTK 4 evidence.
  - Run visual and interactive regression checks on real GNOME and Plasma sessions (Wayland and X11 where in scope), and GTK 3-only Cinnamon/fallback sessions. Include dark/light, scale factors, keyboard-only flows, long text, tray/portal paths, and large lists.
  - Verify clean startup/shutdown, no new fatal warnings or uncaught exceptions, no missing controls/assets, no regression in download behaviour, and correct GTK runtime dependency declarations for GTK 4-primary and GTK 3-compatibility packages/launchers.
  - Update `docs/design.md`, architecture/runtime/deployment docs, `AGENTS.md` environment facts when confirmed, and the end-user changelog for shipped changes. Publish the GTK 4 default policy, compatibility matrix, and GTK 3 support policy.
  - Roll out GTK 4 as primary only after all P0/P1 acceptance checks pass. Keep the GTK 3 compatibility path available for the explicitly supported older desktop matrix.
  - **Exit:** reproducible release evidence per desktop/backend and documented GTK 3/GTK 4 support policy.

## Acceptance criteria

- GTK 4 is the primary/default frontend on the supported GTK 4 desktop matrix.
- GTK 3 remains independently buildable and installable without requiring GTK 4, including on supported GTK 3-only desktops such as Cinnamon.
- GNOME and KDE core workflows are audited on Wayland and X11 where supported, with findings traceable to reproducible evidence.
- No open P0 defects; P1 findings are fixed or explicitly accepted with a user-facing workaround/limitation.
- Accessibility, keyboard, theme, scaling, portal, and tray checks are part of acceptance, not optional polish.
- GTK 4 is not selected based on theme CSS alone: binding/runtime feasibility and representative native UX must pass the spike.
- Shared application behaviour has regression coverage; GTK 3 and GTK 4 have separate UI smoke/visual verification.
- Documentation and packaging accurately state which GTK runtimes are required, GTK 4's primary status, and how GTK 3 compatibility is selected on older desktops.

## Open decisions

| # | Decision | Current lean |
|:---|:---|:---|
| 1 | Keep the `IApplicationWindow` adapter, or narrow the core contract with a smaller window interface for new frontends? | Keep the adapter for now; narrow only if the no-op surface blocks correctness (it currently hides auto-deny placeholders). |
| 2 | Does GTK 4 own the download engine, or stay a UI client of the GTK 3 process? | Own it in-process once the slice works; a second engine in a second process risks two writers on `downloads.db`. |
| 3 | How is the GTK 3 path selected? | Packaging/desktop-entry choice + explicit user setting; never desktop-name guessing. |
| 4 | Release GTK 4 before full parity? | No — gate on the P0/P1 journeys and the packaging split. |

## Reference material checked

- `AGENTS.md` — current stack, toolchain, GTK smoke/build commands, packaging and desktop integration constraints.
- `docs/design.md` — existing design tokens and GTK 4/libadwaita-inspired CSS layer.
- `docs/audit/support-matrix.md`, `docs/audit/ui-ux-findings.md`, `docs/audit/gtk4-coupling-inventory.md` — measured environment, findings register, coupling inventory and spike results.
- `docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md` and `docs/superpowers/plans/2026-08-30-gtk-app-ui-modernization.md` — prior GTK 3 design decisions and known validation constraints.
- `app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj` — GtkSharp 3.24.24.38, .NET 8, Glade/theme content.
- `app/XDM/XDM.Gtk4.UI/XDM.Gtk4.UI.csproj` — GirCore.Gtk-4.0 0.8.1, net8.0, own theme/content.
- `scripts/run-gtk-smoke.sh` and `app/XDM/XDM.Tests/` — existing test coverage and smoke infrastructure.
- Library documentation checked 2026-10-22: [GtkSharp](https://github.com/gtksharp/gtksharp), [GTK 3→4 migration](https://docs.gtk.org/gtk4/migrating-3to4.html), [GTK 4 reference](https://docs.gtk.org/gtk4/), [GirCore GTK tutorial](https://gircore.github.io/docs/tutorial/gtk/hello_world.html), GirCore API docs for `StringList`/`MultiSelection`.
