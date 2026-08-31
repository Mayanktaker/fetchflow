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
4 / 8 / 12 / 16 / 24 px — all margins, paddings and gaps derive from this scale. Deliberate optical exceptions exist (e.g. 9px row gaps, 1–3px hairline paddings) where optical balance beats strict scale adherence.

## Color
| Role | Dark | Light |
|---|---|---|
| window | #282c34 | #fcfcfc |
| sidebar | #21252b | #ececec |
| view | #2c313a | #ffffff |
| entry | #21252b | #ffffff |
| elevated | #21252b | #ffffff |
| button surface | #3e4451 | #ffffff |
| border | #3e4451 | #cacaca |
| text | #abb2bf | #222222 |
| dim text | #9da5b4 | #6b6b6b |
| accent / hover / active | #61afef / #7cb8f2 / #4d78cc | same |
| destructive / hover / active | #e06c75 / #ee8189 / #c25a63 | same |
| brand / hover | #d19a66 / #e0af89 | #ea580c / #f97316 |
| success | #98c379 | #16a34a |

## Rules
1. Blue is the ONLY interactive accent: selection, focus, primary buttons, and
   progress (dark gradient #61afef → #7cb8f2; light theme keeps its blue family).
2. Orange (brand) never appears on interactive controls — logo tile, brand
   moments only.
3. Destructive stays red.
4. Every corner uses one radius from the scale; every gap from the spacing scale.
5. Typography uses bundled Inter, then Cantarell, then sans-serif; hierarchy uses weight + dim color.
6. Settings provides an explicit persisted Dark/Light selector with live switching.

## Icons
Remix Icon outline set in app/XDM/XDM.Gtk.UI/svg-icons/ + xdm-mark.svg (brand).
