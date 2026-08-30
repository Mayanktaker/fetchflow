# XDM Design System & UI Modernization — Design Spec

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->

- **Date:** 2026-08-30
- **Status:** Approved in brainstorming (all sections signed off); pending implementation plan
- **Scope:** GTK3 app (`app/XDM/XDM.Gtk.UI/`) + Chrome & Firefox extensions
- **Companion doc:** `docs/design.md` will become the canonical design-language reference (created in Phase 1)

## 1. Problem & Goal

XDM's UI is functional but dated: small radii (4–5px), inconsistent spacing, a fixed 160px TreeView sidebar, dense single-line lists, native title bars, and three unrelated "blues" across app (`#3584e4`), extensions (`#2196F3`), and brand (orange logo). Goal: one modern design language across app and extensions — rounded corners, better spacing/icons/sidebar/lists, responsive layout, themed window controls — **with zero feature loss and indistinguishable performance**.

## 2. Decisions (locked during brainstorming, mockup-validated)

| Decision | Choice |
|---|---|
| Design personality | Blue interactive accent + orange **brand moments** (logo tile, empty states, extension highlights) |
| Main window layout | **Option B** — modern nav sidebar (icons, count badges, rounded selection pill, resizable) + refined TreeView list (two-line rows, pill progress, icon tiles) |
| Window top bar | **Slim HeaderBar + toolbar row** — themed minimize/maximize/close via CSD; toolbar keeps all buttons in place |
| Maintenance approach | Token-disciplined hand-maintained CSS (no generation pipeline), phased delivery |
| Delivery | 3 phases: app shell → dialogs → extensions |

## 3. Design Tokens (single source of truth: `docs/design.md`)

GTK theme files carry token-comment headers (GTK3 CSS has no variables); extension `styles.css` uses real CSS custom properties with the same values.

### 3.1 Radius scale

| Token | Value | Used for |
|---|---|---|
| `radius-sm` | 6px | checkboxes, small chips |
| `radius-md` | 8px | buttons, sidebar selection pills, icon tiles, linked-group outer corners |
| `radius-lg` | 10px | menus, popovers, extension cards, dialogs' prominent boxes |
| `radius-pill` | 999px | search entry, progress bars, switches, count badges |

### 3.2 Spacing scale

`4 / 8 / 12 / 16 / 24` px — applied as GTK margins/paddings and extension gaps. Toolbar/sidebar/list padding derives from this scale only.

### 3.3 Color tokens

| Role | Dark | Light |
|---|---|---|
| window | `#1d1d1d` | `#fcfcfc` |
| sidebar | `#161616` | `#ececec` |
| view | `#232323` | `#ffffff` |
| entry | `#282828` | `#ffffff` |
| elevated | `#262626` | `#ffffff` |
| button surface | `#2e2e2e` | `#ffffff` |
| border | `#404040` | `#cacaca` |
| text | `#f4f4f4` | `#222222` |
| dim text | `#b4b4b4` | `#6b6b6b` |
| accent / hover / active | `#3584e4` / `#5a9bea` / `#2767b8` | same |
| destructive / hover / active | `#e01b24` / `#ef4a52` / `#a5121a` | same |
| **brand** (new) / hover | `#f97316` / `#fb923c` | `#ea580c` / `#f97316` |
| **success** (new) | `#4ade80` | `#16a34a` |

### 3.4 Typography & icons

- System font stack only (Cantarell/Adwaita default in GTK; `system-ui, sans-serif` in extensions). Hierarchy via weight and dim color, not exotic sizes.
- Icon system: existing Remix Icon set + new `svg-icons/xdm-mark.svg` (orange-gradient rounded square, white down arrow) + up to ~3 missing Remix glyphs if new UI elements need them.
- Progress fill: gradient `#3584e4 → #5a9bea` (GTK3 CSS supports `linear-gradient`).

### 3.5 Language rules

1. Blue is the only interactive accent — selection, focus, primary buttons, links, progress.
2. Orange never appears on interactive controls; it marks brand moments only.
3. Destructive stays red, clearly separated from both.
4. Every corner uses exactly one radius from the scale; every gap from the spacing scale.

## 4. Phase 1 — GTK App Shell

Files: `MainWindow.cs`, `Utils/GtkHelper.cs`, `theme/xdm-dark.css`, `theme/xdm-light.css`, `svg-icons/xdm-mark.svg` (new), `docs/design.md` (new).

### 4.1 HeaderBar + CSD

- `Gtk.HeaderBar` via `Window.SetTitlebar()`: orange brand tile (loaded from `xdm-mark.svg`), bold "XDM" label, dim subtitle tracking the selected sidebar category.
- `ShowCloseButton = true` and explicit `DecorationLayout = ":minimize,maximize,close"` so all three window controls appear (incl. GNOME).
- CSD yields rounded top corners; headerbar styled charcoal (dark) / white (light) via existing `headerbar` CSS node. Backdrop states preserved.
- Fallback: WMs that force server-side decorations simply draw their own bar — app remains functional.

### 4.2 Sidebar rebuild

- `Gtk.TreeView` sidebar → `Gtk.ListBox` inside `ScrolledWindow`, inside the left pane of a new `Gtk.Paned` (drag-resizable; sensible initial position ~200px, min ~170px).
- Rows: SVG icon (16px) + localized label + count badge (right-aligned pill, `radius-sm`, translucent background).
- Structure: "All Unfinished" / "All Finished" rows, separator, uppercase dim "CATEGORIES" label, then per-category rows from `Config.Instance.Categories`.
- Selection: rounded blue pill (`radius-md`) with row margins; hover = translucent rounded pill.
- Counts refresh on existing download add/remove/finish/category-change events — **no new timers**.
- Preserve existing behaviors: category selection drives list visibility; category context menu (add/edit/delete) must keep working on the new rows.

### 4.3 Toolbar refinement

- "New" = filled `suggested-action` pill (padding 6×14, `radius-md`).
- Icon buttons: uniform min 28px, `radius-md` hover feedback, existing semantic classes kept (`destructive-action` on Delete).
- Search Entry: full pill (`radius-pill`), right-aligned; existing search behavior unchanged.

### 4.4 Download list refinement (TreeView kept)

- Name column: icon tile + two text renderers packed vertically — filename (primary) over status subtitle (dim): "Downloading · 4.2 MB/s" / "Paused" / etc.
- Icon tiles: `GtkHelper` gains a helper that pre-composites the SVG icon onto a rounded-rect neutral-tinted background (cairo; white @6% on dark, black @6% on light), **cached per (name, size)**.
- Progress: `CellRendererProgress` styled as pill with gradient fill via CSS nodes `treeviewprogressbar`.
- Responsiveness: name column `Expand = true`; other columns autosize; window minimum size ~640×420.
- Rows: increased vertical padding; hover/selection styled via `treeview row` CSS nodes — **known risk:** per-row rounded selection may not render cleanly in GTK3 TreeView; graceful fallback to clean rectangular highlight is acceptable.

### 4.5 Theme CSS updates

- Both theme files: token header extended (brand, success, radius/spacing scales documented); bump radii to scale; style `list`/`list row` nodes (sidebar), headerbar, pill progress, icon-button sizing, search pill.
- `ThemeManager.cs` unchanged in mechanism (priority 800, live dark/light swap continues to work).

## 5. Phase 2 — Dialogs

- Add close-only `Gtk.HeaderBar`s to the ~10 high-traffic dialogs: settings-dialog2, new-download-window, video-downloader-window, batch-download-dialog, advanced-download-window, properties-dialog, queue-manager-dialog, delete-confirm-dialog, about-dialog, language-dialog.
- Global CSS pass benefits all 30 glade dialogs automatically (buttons, entries, notebook tabs, switches already themed).
- Manual glade pass: apply spacing scale to margins on the high-traffic dialogs; bump container radii where glade defines boxes/frames.
- Legacy dead `settings-dialog.glade` untouched.
- Dialog functionality, i18n, and accelerators unchanged.

## 6. Phase 3 — Extensions (Chrome + Firefox)

- One **byte-identical** `styles.css` in both extensions (current pattern kept; verified by `diff -q`).
- `styles.css` rewritten: `:root` CSS custom properties mirroring §3; dark default; light via `prefers-color-scheme`.
- `popup.html`: inline styles removed → semantic classes; header (XDM mark + connected dot), pill toggles, rounded action buttons matching the app language.
- `confirm.html`, `error.html`, `disabled.html`, `register.html`: same language — centered card (`radius-lg`), blue primary / red destructive buttons, orange brand moments.
- **No changes** to `app.js`, `connector.js`, `popup.js` logic, manifest permissions, or messaging — markup/CSS only, aside from class attributes in HTML.

## 7. Performance guardrails

- Download lists remain `TreeView` (virtualized rendering); `ListBox` only for the small sidebar.
- Icon tile pixbufs cached; no per-frame drawing.
- No new timers/loops; counts piggyback on existing events.
- Extensions: no new dependencies, no JS changes.

## 8. Verification plan

| Phase | Verification |
|---|---|
| 1 | `dotnet build` (SDK at `~/.dotnet8`) passes; app launches with no CSS errors on stderr; dark/light toggle works live; sidebar resize, counts, category context menu, search, selection behavior spot-checked; visual check against approved mockup B |
| 2 | Build passes; each reworked dialog opens, closes, and functions; headerbar + spacing visually consistent |
| 3 | Chrome extension loaded unpacked in headless browser (agent-browser skill) — popup, confirm, error pages screenshotted against the design language; `diff -q` chrome-vs-firefox shared files identical; extension still triggers a capture end-to-end |

## 9. Out of scope

- No new features, no core-engine changes (`XDM.Core` untouched), no settings restructure.
- Windows UI, legacy `settings-dialog.glade`, store listing assets.
- Any change to native-messaging protocol or extension permissions.

## 10. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Rounded per-row selection unreliable in GTK3 TreeView | Graceful fallback to rectangular highlight (pre-approved) |
| CSD quirks on unusual WMs | GTK falls back to SSD automatically; functionality unaffected |
| GNOME hides minimize by default | Explicit `DecorationLayout` requests it; verified pattern in other GTK3 apps |
| Sidebar rebuild loses category management UX | Explicit requirement: context menu behaviors preserved (§4.2) |
| Theme CSS regressions on 30 dialogs | CSS changes are additive on existing selectors; Phase 2 visual sweep |
