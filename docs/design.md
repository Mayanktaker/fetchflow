# FetchFlow Design Language

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Single source of truth for FetchFlow's UI tokens. GTK theme files inline these values
(GTK3 CSS has no variables); browser extensions mirror them as CSS custom properties.
Spec: docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md

## Radius (70% Scale)

| Token | Value | Used for |
|---|---|---|
| sm | 4px | checkboxes, small chips, title buttons |
| md | 6px | buttons, sidebar pills, icon tiles, row cards, linked outer corners |
| lg | 7px | menus, popovers, extension cards, status card container |
| xl | 8px / 10px | dialog containers, card frames |
| badge | 17px | about-dialog logo badge |
| pill | 999px | search entry, progress bars, switches, badges |

## Spacing

4 / 8 / 12 / 16 / 24 px — all margins, paddings and gaps derive from this scale.

Exceptions:

- Download list rows (Active/Complete TreeViews) use a 12px card inset
  (`padding: 12px 12px;` in BOTH the primary and the cascade row block of every
  theme). GTK3 sizes a row from its cell renderers, NOT from CSS `margin`/`padding`,
  so the gap between items comes from two places: the 12px CSS inset that insets the
  painted card, plus the renderers' vertical padding in `MainWindow`
  (`DownloadRowToggleVerticalPadding` 12, `DownloadRowIconVerticalPadding` 12,
  `DownloadRowNameVerticalPadding` 10, `DownloadRowMetaVerticalPadding` 16).
  Result: 67px row pitch, ~43px card, 24px gap between cards. Row `margin` is inert
  in GTK3 — never "fix" list spacing there.
- Download list column header: 14px vertical padding
  (`treeview.<view> header button`) → a 52px header bar, matching a GTK4 headerbar.
  **The accent goes on the header BUTTON, not on `treeview.<view> header`** — GTK3's
  `GtkTreeViewHeader` is not reachable as a `treeview header` CSS node (the header
  buttons' parent IS the treeview), so styling the container matches nothing and the
  bar renders transparent, i.e. invisible on a light surface. Pinned by
  `DownloadListHeaderVisibilityTests`.
- Column resizing needs `Sizing = Fixed` on all three columns, and `Resizable` must be
  set **before** `Sizing`: setting it while the mode is AUTOSIZE makes GTK silently
  rewrite the mode to GROW_ONLY, which re-derives the width from cell content and
  throws the user's drag away. Same test pins this.
  Caption insets live on the `list-header-name` / `list-header-size` header-button
  classes (66px / 18px) so each caption lines up pixel-exact with the row text it
  labels — verified by `DownloadListColumnHeaderSmokeTests`.
  The header is GTK's own (three drag-resizable columns: gutter | File name | Size);
  see `MainWindow.WireListColumnHeaders` and `DownloadListHeaderPolicy`.

## GTK4 (libadwaita) alignment

GTK 4 does not reuse the GTK 3 stylesheets at all (they fail the GTK 4 parser);
`XDM.Gtk4.UI/theme/gtk4-{light,dark}.css` are minimal on purpose and the shell is
drawn by libadwaita. Five `@define-color` tokens are rewritten at load time from
the shared scheme table (`Gtk4Theme.ApplyColorScheme`), so the 14 schemes reach
the primary frontend without a stylesheet per scheme:

| Token | Source |
|---|---|
| `accent` | `ColorSchemeDefinition.AccentHex` |
| `accent_hover` | `ThemeContrast.HoverFor(accent)` |
| `accent_foreground` | `ThemeContrast.InkFor(accent)` — white or `#1a1a1a` |
| `row_bg` | `ColorSchemeDefinition.CardBackgroundHex` |
| `row_hover` | `ColorSchemeDefinition.HoverBackgroundHex` |

Every rule that paints text on the accent uses `@accent_foreground` (never a
literal `#ffffff`), which is what keeps Espresso Mocha / Cyberpunk Matrix / Nord
Emerald readable. `FETCHFLOW_COLOR_SCHEME=<index>` pins a scheme for previews and
screenshots, next to the existing `FETCHFLOW_THEME_MODE=light|dark`.

Every GTK theme ends with one `GTK4 (libadwaita) alignment` layer. Adwaita on GTK4
is flat and generously rounded, so the layer removes the GTK3 bevel gradients
(`background-image: none`) from buttons, selected sidebar rows, the headerbar and
the progress fill, and raises corners to 12px on buttons, entries, menus, popovers,
scrolled windows and cards (10px flat buttons, 6px menu items and checkboxes, 8px
tooltips).

Two GTK3-CSS rules the layer depends on, worth knowing before editing it:

- A later rule only beats an earlier one when the two selectors have **equal
  specificity**, so every rule in the layer repeats the exact selector it overrides
  (`button.suggested-action`, not just `button`).
- The layer is **appended**, so it is always last. Put new GTK4 rules there rather
  than editing the GTK3-era blocks above.

Verified by rendering a real button against the theme and comparing its corner
silhouette with a forced-radius reference.

## Buttons

Buttons come in exactly two looks, and peer buttons in one row must share one:

| Class | Look | Used for |
|---|---|---|
| (none) / `suggested-action` / `destructive-action` | 1px border, themed surface, hover shifts background + border | dialog actions: Cancel, Save, and every `dialog-action-button` |
| `flat` | no border, hover wash only | inline links beside an entry (Copy, Default, Browse) and icon stacks |

`button.dialog-action-button` is the class for a dialog action row (Add / Edit /
Delete / Defaults). It is **generated from each theme's own `button` rules**, so an
action row can never drift from the dialog's Cancel / Save buttons. Its
destructive variant keeps the subtle red of `button.flat.destructive-action` and
gains the row's border and hover.

The download list's selection checkbox is **not** a `CellRendererToggle`: a cell
renderer has no styleable CSS node, so a toggle cannot be themed at all (verified —
`treeview row checkbutton`, `treeview cell` and `treeview toggle` all have zero
effect). The gutter is a `CellRendererPixbuf` showing
`svg-icons/checkbox-unchecked.svg` / `checkbox-checked.svg` (22px, accent-tinted via
`GtkHelper.LoadSelectionCheckbox`, re-tinted when the theme accent changes). The
checked box is deliberately **hollow** (outline + tick, `fill="none"`): tinting
replaces the RGB of every opaque pixel, which is what turned a filled box's white
tick into an invisible solid square — the outline survives tinting, a tick carved
out of a fill does not. Gutter
width 52px = 22px box + 8px padding each side.

Four rules must hold for that checkbox to work. All four are pinned by
`DownloadListCheckboxTests` (the first two through *real* event dispatch):

- **Pass row-event coordinates through RAW.** `gtk_tree_view_get_path_at_pos()`
  documents that its coordinates must be the raw coordinates of an event whose
  `event->window` is the view's **bin window**. The bin window's origin sits
  *below* the column header (verified: `bin origin y == header height`), so GTK
  has already excluded the header — any extra widget→bin conversion shifts every
  hit by the header height and makes the last row resolve to nothing at all.
  `TreeViewSelectionHelper.TryPathAtPos` therefore does no conversion.
- **Gate on the event window.** Header-window events carry header coordinates and
  must never be mistaken for a row click; `IsRowEvent` compares the event's
  window against `gtk_tree_view_get_bin_window()`. Toggle, hover and
  right-click-preservation all check it.
- **Toggle on press and CLAIM the event** (`args.RetVal = true` after toggling).
  Without the claim, GTK's default handler runs afterwards and *replaces* the
  selection with that one row — which made multi-select impossible and made an
  untick instantly re-tick. The claim only works because the handlers carry
  `[GLib.ConnectBefore]`; GtkSharp otherwise connects *after* the class handler
  and the claim is void. Never remove that attribute.
- **Columns must be `Clickable = true`.** GTK only emits a column's `::clicked`
  for a clickable column, otherwise the header select-all and the sort captions
  are dead (`gtk_tree_view_column_clicked`: "will only work if the column is
  clickable"). `MainWindow.WireListColumnHeaders` sets it on all three.
  Keep `SortColumnId = -1` — our own handler owns sorting.
- The row must be re-rendered on selection change — the cell-data-func is not
  re-invoked on its own, so each `Selection.Changed` handler calls `QueueDraw()`.
- The gutter click must **not** be suppressed. A `ConnectBefore` press handler that
  sets `RetVal = true` stops GTK's own handlers on that widget, which is what left
  the checkbox dead. The toggle now happens on press, in our own handler, and the
  event is left unclaimed so ctrl/shift-click still works.

`button.flat:hover` must stay at **alpha >= 0.10** (0.10 light / 0.13 dark): a 6%
wash is invisible on a light surface and reads as a dead button. Both invariants
are enforced by `DialogButtonConsistencyTests` across all 14 themes.

## Unfocused (backdrop) states

- An **unfocused selected download row must never be the header accent.** 12 of 14
  themes painted it with the full accent (frost `#0891b2` = the header colour), which
  made the row blend into the header and swallowed the accent-coloured tick box. The
  `treeview.<view> row:selected:backdrop` block is now derived from that theme's own
  focused selection: light themes mix the focused colour 55% toward white, dark themes
  darken it to 85% — text colour and border are the focused selection's values.
  Pinned by `DialogButtonConsistencyTests.
  EveryTheme_UnfocusedSelectedRow_IsLighterThanTheHeader`.
- The header select-all tick renders at **17px** via the
  `list-header-gutter` class on the gutter header button (11.5px was inherited from
  the caption rule and unreadable). Both halves are pinned: the CSS rule by
  `EveryTheme_HeaderSelectAllGlyph_IsBigEnough`, the actual rendered height by
  `EveryTheme_SelectAllGlyphRendersLarge` (fails at 14px rendered, passes at ~21px).

## Portability

- The GTK UI ships on Linux (Arch/Debian/Fedora/openSUSE/flatpak). Its only
  added native call is `gtk_tree_view_get_bin_window` via `libgtk-3.so.0` — the
  standard soname on all of those; `IsRowEvent` falls back to accepting the event
  if the symbol is unavailable, so an exotic soname can never disable the checkbox.
  New checkbox SVGs ship through the existing `svg-icons/**` content glob
  (`CopyToPublishDirectory`), so packages, tarballs and flatpak all carry them.
- **Windows is WPF** and takes none of the GTK changes; `xdm-wpf-build.yml` is its
  build gate (green). No other OS ships a UI in this repo.

## WPF parity

The Windows list mirrors the Linux spacing: `ListViewItem` padding `12,12` and a
52px accent column header (`ListViewHeaderBackcolor` / `ListViewHeaderTextBrush`
in both themes), and the column-header gripper is now visible (it is the Thumb that
makes a `GridViewColumn` drag-resizable — `GridViewColumn` has no `CanUserResize`). WPF cannot be built on Linux — `xdm-wpf-build.yml` is its
only build gate, so treat WPF changes as CI-verified, not locally verified.

## Color

| Role | Dark | Light |
|---|---|---|
| window | #1d1d1d | #fcfcfc |
| sidebar / main-toolbar | #161616 | #ececec |
| view | #232323 | #ffffff |
| view-finished | #2a2a2a | #f8f8f8 |
| headerbar | #1a1a1a | #f0f0f0 |
| entry | #282828 | #ffffff |
| elevated | #2e2e2e | #ffffff |
| border | #404040 | #cacaca |
| text | #f4f4f4 | #222222 |
| dim text | #b4b4b4 | #5f5f5f |
| accent / hover / active | #3584e4 / #2f74c9 / #2767b8 | same |
| destructive / hover / active | #e01b24 / #ef4a52 / #a5121a | same |
| brand / hover | #f97316 / #fb923c | #ea580c / #f97316 |
| success | #4ade80 | #16a34a |

`dim text` and the accent `hover` are derived, not free choices: see the contrast
floor below. The light `dim text` was `#6b6b6b`, which only reached 3.88:1 on the
finished-list surface.

## Color Schemes & Palettes

FetchFlow provides 7 curated color schemes for Dark mode and 7 curated color schemes for Light mode, selectable from Settings -> General -> Color Scheme.

### Contrast floor (enforced)

One rule decides the text colour on every accent-painted surface, and all three
frontends share it (`XDM.Core/UI/ThemeContrast.cs`):

| Constraint | Floor | Where |
|---|---|---|
| Label on a scheme accent (semibold 11–14px control text) | **3.0:1** (WCAG AA large text) | buttons, selected rows, menu highlight, checkboxes, list header band |
| Text typed into a field | **4.5:1** (WCAG AA body text) | toolbar search entry, plain entries |
| Disabled / backdrop text | exempt | deliberately quiet |

- `ThemeContrast.InkFor(accent)` returns white while white still clears 3.0:1,
  and `#1a1a1a` once the fill is too bright for it (Espresso Mocha, Cyberpunk
  Matrix, Nord Emerald are dark-ink themes).
- `ThemeContrast.HoverFor(accent)` returns a **deeper** accent (`0.88×`), not a
  lighter one: lightening a hover washed the label out (white on `#fbbf24` was
  1.67:1). The same hover is used for `button.suggested-action:hover` and the
  selected sidebar row.
- A bevel gradient counts by its **lightest stop**, which is what the eye reads.
- GTK 3 has no CSS variables, so `scripts/theme-contrast.py` writes these literal
  hex values into the 14 theme files. It is idempotent — re-run it after changing
  a scheme accent or adding a theme. GTK 4 folds the same two functions into its
  `accent_foreground` / `accent_hover` tokens at load time; WPF maps `InkFor` onto
  `AccentForegroundBrush`.
- `ThemeContrastTests` fails the build if any theme, token or scheme breaks a
  floor, and `EverySchemeSurfaceTokenMatchesItsStylesheet` keeps the surface
  columns below honest against the shipped CSS.
- The browser extensions carry the same rule as CSS custom properties:
  `--accent` is the **fill** and `--accent-text` the **text** colour (one token
  cannot serve both — deepening the fill for white text makes it unreadable as
  text). A bright fill takes dark ink, so the brand-orange CTA and the
  success-green "queued" button use `#1a1a1a` labels.
  `ExtensionThemeContrastTests` pins both modes and keeps the Chrome/Firefox
  stylesheets in sync.

### Dark Theme Palettes (7 Schemes)

| Scheme ID | Scheme Name | Base Surface | Sidebar | Card Surface | Accent Hex & Hover | TreeView Hover | Active Row Hex |
|:---|:---|:---|:---|:---|:---|:---|:---|
| `0` | **Charcoal Blue (Default)** | `#1d1d1d` | `#161616` | `#262626` | `#3584e4` / `#5a9bea` | `#262c36` | `#323b4a` |
| `1` | **Midnight Violet** | `#161420` | `#110f1a` | `#211e30` | `#8b5cf6` / `#a78bfa` | `#282038` | `#382d4e` |
| `2` | **Nord Emerald** | `#131c1a` | `#0d1513` | `#1d2c29` | `#10b981` / `#34d399` | `#1b302a` | `#27443c` |
| `3` | **Sunset Amber** | `#1a1618` | `#141012` | `#2b2328` | `#f43f5e` / `#fb7185` | `#332128` | `#462e37` |
| `4` | **Dracula Orchid** | `#191622` | `#13101c` | `#221d2e` | `#ec4899` / `#f472b6` | `#301e38` | `#422b4d` |
| `5` | **Cyberpunk Matrix** | `#0f172a` | `#0a0f1d` | `#182236` | `#06b6d4` / `#22d3ee` | `#162a3d` | `#223b55` |
| `6` | **Espresso Mocha** | `#1c1816` | `#14110f` | `#26211e` | `#f59e0b` / `#fbbf24` | `#30241b` | `#443327` |

### Light Theme Palettes (7 Schemes)

| Scheme ID | Scheme Name | Base Surface | Sidebar | Card Surface | Accent Hex & Hover | TreeView Hover | Active Row Hex |
|:---|:---|:---|:---|:---|:---|:---|:---|
| `0` | **Classic Blue (Default)** | `#fcfcfc` | `#ececec` | `#ffffff` | `#3584e4` / `#5a9bea` | `#f0f4f9` | `#dbe7f7` |
| `1` | **Nordic Frost** | `#f4f8fa` | `#e3edf1` | `#f8fafb` | `#0891b2` / `#06b6d4` | `#e6f4f8` | `#cfe2ea` |
| `2` | **Solarized Sand** | `#faf6ee` | `#ede6d8` | `#fdfbf6` | `#d97706` / `#f59e0b` | `#f7eee0` | `#ecddc5` |
| `3` | **Rose Garden** | `#fbf5f7` | `#f2e4ea` | `#fdf8fa` | `#e11d48` / `#f43f5e` | `#fbe8ee` | `#f4d1dc` |
| `4` | **Matcha Forest** | `#f3f8f5` | `#e2ede6` | `#ffffff` | `#059669` / `#10b981` | `#e3f3eb` | `#cbe7d7` |
| `5` | **Lavender Bloom** | `#f7f5fb` | `#eae5f4` | `#ffffff` | `#7c3aed` / `#8b5cf6` | `#ede7fa` | `#ded2f5` |
| `6` | **Citrus Peach** | `#fdf6f0` | `#f6e7db` | `#ffffff` | `#ea580c` / `#f97316` | `#fdece0` | `#f7d8c0` |

## Typography

| Level | Size | Weight | Use |
|-------|------|--------|-----|
| Caption | 11px | 400/600 | Badges, sidebar headings, update dot |
| Secondary | 12px | 400/500 | Bottombar, paths, version badge, status |
| Body | 13px | 400 | Search entry, taglines |
| Body-lg | 14px | 400 | Default (GTK inherited), extension text |
| Title | 15px | 700 | Dialog titles (download-complete) |
| Display | 17px | 700 | About dialog app name |

Font: system font only (Cantarell/Adwaita on GTK, `system-ui, sans-serif` in extensions).

## Rules

1. Primary interactive accent: selection, focus, primary buttons, progress (driven dynamically by active Color Scheme).
2. Orange (brand) never appears on interactive controls — logo tile, brand moments only.
3. Destructive stays red.
4. Every corner uses one radius from the scale; every gap from the spacing scale.
5. System font only; hierarchy via weight + dim color.

## Icons

Remix Icon outline set in `app/XDM/XDM.Gtk.UI/svg-icons/` + `fetchflow-mark.svg` (brand).

## Status Indicator Colors (Pango markup)

These are used inline in C# for dynamic status text, not in CSS:

| State | Color | Used in |
|-------|-------|---------|
| Active (connected) | #22c55e | MainWindow, SettingsDialog |
| Ready | #38bdf8 | MainWindow, SettingsDialog |
| Listening | #94a3b8 | MainWindow, SettingsDialog |

## WPF Theme Token Mapping (Windows parity)

Scheme hex tokens are single-sourced in `app/XDM/XDM.Core/UI/ColorSchemeTable.cs` (shared project — consumed by both GTK `ThemeManager` and WPF `Utils/WpfThemeManager.cs`). Never fork these values into CSS/XAML copies; the runtime layers them live.

| ColorSchemeDefinition token | GTK effect | WPF runtime brush override |
|---|---|---|
| `AccentHex` | accent CSS variables | `AccentBrush`, `TabSelectionColor`, `ProgressBarForecolor`, `TextFocusedBorder`, `TextMouseOverBorder`, `ButtonFocusedBorder`, `HyperlinkForecolor` |
| accent luminance | — | `AccentForegroundBrush` (white / `#1A1A1A`, Rec.601 threshold 0.55) |
| `HoverBackgroundHex` | row hover | `RowHoverBrush`, `ListViewMouseOverBackcolor` |
| `ActiveBackgroundHex` | row selected | `RowActiveBrush`, `ListViewSelectedBackcolor`, `CategoryHighlight`, `SystemColors.HighlightBrushKey` |
| `AlternateBackgroundHex` | alternating rows | `RowAlternateBrush` |
| `CardBackgroundHex` | card / rail surfaces | `CardBackgroundBrush` |

WPF base neutral skins live in `Themes/DarkTheme.xaml` + `Themes/LightTheme.xaml` (full brush-key parity); control chrome in `Resources/*Styles.xaml` references brushes via `DynamicResource` only, so scheme swaps recolor open windows without restart.

## GTK 4 Native Theming System

Native GTK 4 stylesheets live in `app/XDM/XDM.Gtk4.UI/theme/` (`gtk4-dark.css` and `gtk4-light.css`) and are deployed side-by-side with GTK 3 stylesheets in the `/opt/fetchflow/theme/` directory.

- **Shell:** `AdwApplication` + `AdwApplicationWindow` with an `AdwToolbarView` (top bar `AdwHeaderBar`, content below) and `AdwWindowTitle` carrying the app name with a static `Downloads` subtitle (the active view is named in the content headline, so it is not repeated twice). `AdwApplicationWindow` forbids `SetTitlebar()`, so the toolbar view is mandatory. Window controls come from libadwaita, not hand-built buttons. Requires libadwaita >= 1.4.
- **Color scheme:** `AdwStyleManager` owns light/dark. An explicit Light/Dark choice forces `ForceLight`/`ForceDark`; Follow System leaves `ColorScheme.Default` and the app re-applies its CSS when the manager's `dark` property changes.
- **Empty state and About:** `AdwStatusPage` for the empty list and `AdwAboutWindow` for About, both fed from `AppInfo`/`TextResource`. The Legal page receives the repository `LICENSE` as an embedded resource (`SetLicense`) because libadwaita treats the license type and a license body as exclusive and rendered an empty Legal page with the type alone; the type is only used as a fallback when the text is unavailable. Developer names carry no bracketed URL (the Website row already links it), so no stray bracket renders on the Credits page. The same embedded text opens in `LicenseDialog` (Help → License): a monospace, word-wrapped `Gtk.TextView` with Copy and Close in the header.
- **Display-level CSS Provider:** Applied via `Gtk.CssProvider` per `Gdk.Display` (`Gtk.StyleContext.AddProviderForDisplay`) at priority `GTK_STYLE_PROVIDER_PRIORITY_APPLICATION` (600).
- **Interface Color Scheme:** `Gtk4Theme.cs` sets the GTK 4 modern property `gtk-interface-color-scheme` to `prefer-dark` or `prefer-light`, avoiding the deprecated `gtk-application-prefer-dark-theme` warning (FF-UI-010) while maintaining a graceful fallback for earlier GTK 4 versions.
- **System Theme Integrity:** `gtk-theme-name` is intentionally left untouched, preserving system-wide styling on desktop environments such as Breeze (KDE) and Mint-Y (Cinnamon).
- **List chrome:** the list is a header-less `Gtk.ListView` — one card per row (4x10px gaps,
  12px radius, subtle border, 6x8px inner padding) with no column header and no column
  resizing. Each row lays out icon | name+source+destination | size | progress itself, so the
  columns never need dragging. The view keeps a small end inset so the overlay scrollbar never
  sits on a card, and the row body keeps native Ctrl/Shift multi-selection.
- **Row hierarchy:** two lines per row — the file name (14px, 600) and one middle-ellipsized
  meta line joining source site, file type and destination folder (`◉ domain · RAR · ~/path`,
  11px `@meta_text`). Size is a 12px `@meta_text` column and progress bars are a 4px
  accent-filled track, so the eye lands on the name first and rows stay compact. A
  `.status-pill` between the text column and Size names the live state — Downloading
  (accent), Paused (amber), Queued (neutral), Failed (red) — and hides itself once the
  row is complete, where the check plus finish date already say it. Hovering a row reveals
  one accent-tinted shortcut at its end: pause, resume (paused rows) or open (finished
  rows). Finished rows show a second shortcut that opens the destination folder and a
  third, red shortcut that removes the download through the same confirm dialog as the
  context menu. All three select their own row first, so the shared core actions apply
  to it. Pause, resume and
  failure arrive with no progress tick, so the core row lookup re-syncs the affected row
  on idle: the pill flips (Downloading to Paused/Failed), the frozen speed clears and
  the hover shortcut swaps pause for resume.
- **Native-first styling:** the GTK 4 stylesheets deliberately stay minimal. Buttons, entries, header bars, sidebar rows, progress bars, hover/active/selection states and focus rings are drawn by libadwaita/Adwaita, so the app looks and behaves like the user's desktop and follows its accessibility settings. The stylesheets only carry what native theming cannot express: the Active vs Complete list surfaces, a thin column divider, the accent token rewritten from `ColorSchemeTable`, the bandwidth/extension state colours and the dialog section-heading weight. Sidebar lists use libadwaita's `navigation-sidebar` class and the Active/Complete block uses `boxed-list`, instead of hand-written row styling.
- **Theme mode:** the shared `Config.ThemeMode` codes are `0 = Light`, `1 = Dark`, `2 = Follow
  System` (default `2`) and are identical for GTK 3, GTK 4 and WPF. GTK 4 applies the saved
  mode at startup, follows the session preference live while in Follow System, and persists
  the Ctrl+T toggle. It never forces `gtk-theme-name`.
- **Per-view surfaces:** the Active list uses `view` and the Complete list uses
  `view-finished` (`#2a2a2a` dark / `#f8f8f8` light). The list widget carries an
  `unfinished`/`finished` style class so the two views read apart, matching GTK 3.
- **Sidebar sections:** small `.sidebar-heading` captions ("Downloads", "Categories") group
  plain `navigation-sidebar` rows; there is no raised status card, so the sidebar reads as one
  calm column. Selected rows carry the scheme accent.
- **Header actions:** the header carries the app mark (28px) beside an `AdwWindowTitle`
  whose subtitle is live while transfers run (`4 active · 5.1 MB/s`, otherwise `Downloads`)
  with a small accent pulse dot while at least one transfer is actually running
  (queued-only work leaves it still) and exactly one labelled primary action
  (`.suggested-action`, "Add download"). The contextual transfer actions (Pause, Resume,
  Open File, Open Folder, Delete) form one icon group that is reconciled on an idle tick after
  each selection change, so it appears only when a selected row can actually run one of them —
  a view with nothing selected shows no ghosted buttons. When the user's
  `gtk-decoration-layout` puts an `icon` element on the start side (e.g.
  `icon:minimize,maximize,close`), start-side title buttons are hidden so the app mark is not
  drawn twice. Search is a GNOME search toggle that opens a `Gtk.SearchBar` scoped to the
  content column (Ctrl+F opens and focuses it).
- **Content headline:** the active view name (16px, 700) plus a live count — `N items`, or
  `N selected` while rows are selected — with Select All and the Sort menu button on the
  trailing edge. It replaces the old column header row.
- **List updates:** progress ticks refresh the bound cells of that row only. The row model and
  the `MultiSelection` are created once, so a running download cannot clear the selection,
  and switching view or search keeps the selection by transfer id.
- **Row meta line:** the second line shows `◉ domain · ◫ ~/folder` (plus the extension for
  finished rows) and is middle-ellipsized; without that the column's natural width pushes
  Size/Progress out of a 900 px window. Falls back to the transfer status when no meta exists.
- **Sidebar counts:** Active, Complete and every category show a right-aligned, muted count
  (`.sidebar-count`), blank when the view is empty, so captions stay aligned.
- **UI error containment:** commit paths that touch configuration, the database or files run
  through `UiGuard.Run`. An exception escaping a GObject signal handler terminates the whole
  process (that is how the proxy-save bug killed the app), so the guard logs it and shows the
  reason instead, leaving the dialog open.
- **Rows own their selection natively:** the rows use the `MultiSelection` plus GTK's native
  Ctrl/Shift click semantics; Select All and the "N selected" count live in the content
  headline.
- **Status footer:** one slim bar with no divider (the list surface already separates it). It opens with the activity summary —
  `N active · X/s` while transfers run (`Idle` — or `Idle — Monitoring Off` when capture
  is off — otherwise), computed from the visible rows'
  parsed speeds, a `· N queued` count for waiting transfers — and doubles as a flat button
  that opens the progress window. The
  connectivity badge follows it — while monitoring is switched off the badge reads
  "Monitoring Off" (dimmed) instead of claiming the extension is ready, since
  nothing would be captured — the GTK 3 badge behaves the same way; Browser Monitoring is a `Gtk.Switch`
  with a caption, then the bandwidth limiter and the queue window on the right. All
  footer controls render at 11px so the bar stays quieter than the content in both
  themes, and `.bottom-bar` padding is defined once per stylesheet (a leftover
  light-only padding rule used to make the light bar 15px taller), and the monitoring-off tooltip is translated like the badge label. The
  activity summary takes the dimmed tone while monitoring is off (its numbers stay
  truthful — the engine runs regardless). Its buttons
  draw their icons from the active scheme accent and are re-tinted whenever the theme changes.
- **Seamless surfaces:** the paned grab handle paints nothing (`opacity: 0`, still
  draggable), download cards carry no outline, and header bars cast no shadow, so
  sidebar, list and header meet with no hairlines in either theme.
- **Sidebar rows:** plain `navigation-sidebar` rows with an 8px inset. Icons keep their
  category colour at rest and turn white on the accent selection (counts fade to 85% white),
  so nothing disappears into the accent fill. A Settings entry (accent gear icon, Ctrl+,)
  is pinned under the category list with breathing room instead of a divider.
- **Empty state:** `AdwStatusPage` with a pill-shaped `.suggested-action` "Add download"
  button, wired to the same primary action as the header so an empty view is actionable.
  When a search filters everything out the page switches to a search icon, "No results for
  “term”" and a "Clear search" action, so a zero-result filter never looks like an empty
  library. Settings lives only in the sidebar (header menu keeps the remaining entries).
- **Row checkboxes:** a press on the gutter checkbox is claimed in the capture phase, because
  the view's own click handler runs afterwards and would replace the accumulated
  multi-selection with that single row. `ToggleRowAt` owns the selection change and the
  checkbox state so the two cannot drift.
- **Settings dialog:** icon tab strip over grouped boxed-list cards; every control applies
  immediately, so the header carries only the title and the native window control closes it
  (closing persists any typed field). The window measures the visible page
  (`FitToPage`) and sizes itself between 380 and 700 px, so a short page such as Advanced
  opens without empty space below its last card while long pages scroll.
- **Media downloader:** `VideoDownloaderWindow` is a four-page `Gtk.Stack` view over the shared
  `VideoDownloaderUIController` (URL + optional browser cookies + credentials → resolving
  spinner → ticked result list with quality and download actions → error page).
- **Colour schemes:** on load the stylesheet's `@accent`/`@accent_hover` tokens are rewritten
  from the active `ColorSchemeTable` entry, so the 7 dark and 7 light palettes drive GTK 4
  exactly as they drive GTK 3 and WPF.

