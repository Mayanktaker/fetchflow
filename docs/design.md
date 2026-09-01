# FetchFlow Design Language

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Single source of truth for FetchFlow's UI tokens. GTK theme files inline these values
(GTK3 CSS has no variables); browser extensions mirror them as CSS custom properties.
Spec: docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md

## Radius

| Token | Value | Used for |
|---|---|---|
| sm | 6px | checkboxes, small chips |
| md | 8px | buttons, sidebar pills, icon tiles, linked outer corners |
| lg | 10px | menus, popovers, extension cards |
| badge | 24px | about-dialog logo badge |
| pill | 999px | search entry, progress bars, switches, badges |

## Spacing

4 / 8 / 12 / 16 / 24 px — all margins, paddings and gaps derive from this scale.

## Color

| Role | Dark | Light |
|---|---|---|
| window | #1d1d1d | #fcfcfc |
| sidebar | #161616 | #ececec |
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

1. Blue (#3584e4 family) is the ONLY interactive accent: selection, focus,
   primary buttons, progress (gradient #3584e4 → #5a9bea).
2. Orange (brand) never appears on interactive controls — logo tile, brand
   moments only.
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
