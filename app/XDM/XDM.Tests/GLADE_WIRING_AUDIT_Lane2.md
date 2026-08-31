# Glade ↔ [UI] Wiring Audit — Lane 2 (P3 Code Quality)

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->
<!-- Lane 2 audit: 2026-09-01 — verifies no orphan label / CS0649-style drift remains -->

## Summary

**Result: 0 drift** on all 27 factory-wired dialogs (25 distinct glade files). The only glade-only orphan is the already-whitelisted `TxtDownloadLinks` in `batch-download-dialog.glade`; `BtnChromium`/`BtnYandex` remain correctly split to non-`[UI]` compat stubs in `SettingsDialog.cs` (verified). No new `[UI]` wiring or glade deletions required.

## Build warnings enumeration

Command (as specified):
```bash
export PATH="$HOME/.dotnet8:$PATH" && \
  dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj -c Debug \
  -p:TreatWarningsAsErrors=false -p:WarningLevel=4 -p:RunAnalyzersDuringBuild=true \
  2>&1 | grep -E "CS0649|CS0169|CS0414|warning CS" | head -80
```
**Output: 0 lines** (`grep` exit 1, build exit 0). Variant with `-p:WarningLevel=4` and plain
`dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj -c Debug` also yields:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

`csproj` has no `<NoWarn>`, `<WarningLevel>`, or `<TreatWarningsAsErrors>` overrides
(only `<Nullable>enable</Nullable>`); the 0-warning result is genuine — every private
`[UI]` field is either initialized (`= null`/`= null!`) or is read in `LoadTexts`/handlers
so `CS0649`/`CS0169` do not fire. `CS0414` (unused private field) likewise 0.

## Cross-check (reuse of GladeWiringTests logic)

Python reimplementation of `GladeWiringTests.GladeIds` + `UiAttributedIds` + `IsInterestingWidgetId` /
`IsBoilerplateId` / `IsWhitelisted` / `IsCsOnlyWhitelisted`:

- Factories discovered via `CreateFromGladeFile` + `AddFromFile(..., "glade", "*.glade")`: **27**
  factories across `app/XDM/XDM.Gtk.UI/Dialogs/**/*.cs` (e.g. `SettingsDialog.cs → settings-dialog.glade`,
  `BatchDownloadWindow.cs → batch-download-dialog.glade`, …).
- With current whitelists applied: **glade-only drift = 0**, **cs-only drift = 0**.
- Without any whitelists: **glade-only = 1** (`batch-download-dialog.glade: TxtDownloadLinks`), **cs-only = 0**.
  After applying the existing whitelist that single case is suppressed — no other drift exists.

## Orphan inventory

### A. Glade-only orphans within wired dialogs

| Glade file | Orphan id | Disposition | Reason |
|---|---|---|---|
| `batch-download-dialog.glade` | `TxtDownloadLinks` | **Whitelisted** (`IsCsOnlyWhitelisted`) | Legacy batch-paste `TextView`/`ScrolledWindow` path. Glade object remains but is never referenced in `BatchDownloadWindow.cs`; wiring it would be dead code. Whitelist keeps `GladeWiringTests` green without deleting a potentially reusable glade node. See `BatchDownloadWindow.cs` — no `TxtDownloadLinks` field exists; batch tab now uses `TxtFile1/2/N` + `TxtAddress`. |
| — | — | — | No other glade-only ids beyond `TxtDownloadLinks` (verified by strict no-whitelist run). |

### B. Former `BtnChromium` / `BtnYandex` (settings)

| Dialog | Field | Glade object? | `[UI]`? | Disposition |
|---|---|---|---|---|
| `SettingsDialog.cs` | `BtnChromium` | **No** (absent from `settings-dialog.glade`) | **No** (`private Button BtnChromium, BtnYandex;` without `[UI]`) | Validated. Line 42 keeps them as non-`[UI]` compat stubs so `Builder.Autoconnect` does not fail on missing ids. Glade only carries `BtnChrome/BtnFirefox/BtnEdge/BtnOpera/BtnBrave/BtnVivaldi` (6 browsers). Keeping the stubs avoids a breaking API change; they are never dereferenced in the non-null path. |
| `SettingsDialog.cs` | `BtnYandex` | same | same | same |

### C. Explicit `IsCsOnlyWhitelisted` / `IsWhitelisted` entries — still present but now mostly redundant

| Whitelist entry | Still needed? | Note |
|---|---|---|
| `advanced-download-dialog.glade: LblSpeedLimit / TxtSpeedLimit / tabPage3` | **No** — all three now exist in both glade and `[UI]` (wiring is complete) | Kept for historical safety; removing would still yield 0 drift. |
| `new-video-download-window.glade: mainBox / menu1` | `menu1` is correctly wired; `mainBox` is filtered as non-interesting (`IsInterestingWidgetId` returns false) so the whitelist is redundant | Harmless. |
| `settings-dialog.glade: ActionArea` / `queue-manager-dialog.glade: ActionArea, TabControl` | Filtered by `IsInteresting`/`IsBoilerplate` but retained as explicit wall for layout containers | Harmless. |

### D. Glade files with no `CreateFromGladeFile` factory (dead glades, out of scope for wiring tests)

These 5 glades are on disk in `app/XDM/XDM.Gtk.UI/glade/` but have **no factory method** and are never loaded by `Builder.AddFromFile` in `Dialogs/`:

| Glade file | Ids (sample) | Disposition |
|---|---|---|
| `advanced-download-window.glade` | `window, LblUser, TxtUserName, LblSpeedLimit, TxtSpeedLimit, tabPage3, btnOk ...` | Dead copy of `advanced-download-dialog.glade` (older `window`-based variant). Not wired — left alone per “don’t delete glade objects unless clearly safe”. |
| `new-download.glade` | `new-download-dialog, txt-url, txt-file, btn-download ...` | Dead copy / older `new-download-window.glade`; no factory. |
| `settings-dialog2.glade` | `dialog, SideBar, Stack, BtnChrome ... ChkDarkTheme ...` | Prototype `GtkDialog` + `StackSidebar` variant; superseded by `settings-dialog.glade`. |
| `url-capture.glade` | `url-capture, url-text, cancel-btn, download-btn` | Unused overlay capture UI. |
| `vid-capture.glade` | `vid-win, txt-file, cmb-output-format ...` | Unused video capture UI. |

Per Lane 2 rules, dead glades are **not deleted** (not “clearly safe”). `GladeWiringTests.DiscoverFactories` only audits factory-linked glades, so these do not trigger drift failures.

## Exhaustive filtered-id sanity check

All factory glades contain a small set of `IsInterestingWidgetId == false` ids that are intentionally never `[UI]`-wired (structural containers, adjustments): `window`/`dialog`, `HeaderBox`/`mainBox`/`button-box`, `Header1/2`, `adjustment1/2`, `ActionArea`. Verified these are the only filtered ids — no widget-like ids were missed.

## Files touched

| File | Action |
|---|---|
| `app/XDM/XDM.Tests/GLADE_WIRING_AUDIT_Lane2.md` (this file) | **Created** — audit report (no code change). |
| `app/XDM/XDM.Gtk.UI/*` | **No edits** — 0 drift, so no `[UI]` wiring, whitelist, or glade deletions made. |
| `app/XDM/XDM.Tests/GladeWiringTests.cs` | **No edit** in this lane (whitelists already correct; comment update deferred to avoid gratuitous diff). |
| `app/XDM/XDM.Tests/GtkSmokeTests.cs` | **Untouched** (Lane 1). |

## Validation

| Check | Command | Result |
|---|---|---|
| Build | `export PATH="$HOME/.dotnet8:$PATH" && dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj -c Debug` | `Build succeeded. 0 Warning(s) 0 Error(s) Time 00:00:00.94` |
| Tests | `export PATH="$HOME/.dotnet8:$PATH" && dotnet test app/XDM/XDM.Tests -c Debug --logger "console;verbosity=normal"` | `Passed: 19 / 19` (3 `GladeWiringTests` + 1 `GtkSmokeTests` + 15 others). Smoke passes with DISPLAY=:99; without DISPLAY it would be `Inconclusive` → 18 passed. |
| Headless cross-check | Python re-impl of `GladeWiringTests` (see above) | `glade-only 0, cs-only 0` with whitelists |

## Recommendation (no action required)

No further wiring or cleanup needed. If a future sweep wants to reduce whitelist noise, these three entries can be removed with zero impact (still 0 drift): `advanced-download-dialog.glade: LblSpeedLimit/TxtSpeedLimit/tabPage3` and `new-video-download-window.glade: mainBox` (already filtered). Leave `batch-download-dialog.glade: TxtDownloadLinks` and the `ActionArea`/`TabControl` layout whitelists in place.
