# XDM Design Language

<!-- © Mayanktaker Computers & Web Development | https://mayanktaker.com -->

Single source of truth for XDM's UI tokens. GTK theme files inline these values
(GTK3 CSS has no variables); browser extensions mirror them as CSS custom
properties. Spec: docs/superpowers/specs/2026-08-30-xdm-design-system-modernization-design.md

## Radius
| Token | Value | Used for |
|---|---|---|
| sm | 6px | checkboxes, small chips |
| md | 8px | buttons, sidebar pills, icon tiles, linked outer corners |
| lg | 10px | menus, popovers, extension cards |
| pill | 999px | search entry, progress bars, switches, badges |

## Spacing
4 / 8 / 12 / 16 / 24 px — all margins, paddings and gaps derive from this scale.

## Color
| Role | Dark | Light |
|---|---|---|
| window | #1d1d1d | #fcfcfc |
| sidebar | #161616 | #ececec |
| view | #232323 | #ffffff |
| entry | #282828 | #ffffff |
| elevated | #262626 | #ffffff |
| button surface | #2e2e2e | #ffffff |
| border | #404040 | #cacaca |
| text | #f4f4f4 | #222222 |
| dim text | #b4b4b4 | #6b6b6b |
| accent / hover / active | #3584e4 / #5a9bea / #2767b8 | same |
| destructive / hover / active | #e01b24 / #ef4a52 / #a5121a | same |
| brand / hover | #f97316 / #fb923c | #ea580c / #f97316 |
| success | #4ade80 | #16a34a |

## Rules
1. Blue (#3584e4 family) is the ONLY interactive accent: selection, focus,
   primary buttons, progress (gradient #3584e4 → #5a9bea).
2. Orange (brand) never appears on interactive controls — logo tile, brand
   moments only.
3. Destructive stays red.
4. Every corner uses one radius from the scale; every gap from the spacing scale.
5. System font only; hierarchy via weight + dim color.

## Icons
Remix Icon outline set in app/XDM/XDM.Gtk.UI/svg-icons/ + xdm-mark.svg (brand).
