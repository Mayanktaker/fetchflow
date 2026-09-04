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

Exception: download list rows (Active/Complete TreeViews) use a dedicated 5px
vertical margin (`margin: 5px 6px;` primary, `margin: 5px 8px;` cascade block)
in every theme, giving rows consistent breathing room regardless of palette.

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
| dim text | #b4b4b4 | #6b6b6b |
| accent / hover / active | #3584e4 / #5a9bea / #2767b8 | same |
| destructive / hover / active | #e01b24 / #ef4a52 / #a5121a | same |
| brand / hover | #f97316 / #fb923c | #ea580c / #f97316 |
| success | #4ade80 | #16a34a |

## Color Schemes & Palettes

FetchFlow provides 7 curated color schemes for Dark mode and 7 curated color schemes for Light mode, selectable from Settings -> General -> Color Scheme.

### Dark Theme Palettes (7 Schemes)

| Scheme ID | Scheme Name | Base Surface | Sidebar | Card Surface | Accent Hex & Hover | TreeView Hover |
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
