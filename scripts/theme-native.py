#!/usr/bin/env python3
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Generates the 14 XDM.Gtk.UI theme stylesheets from XDM.Core/UI/ColorSchemeTable.cs.
#
# GTK 3 CSS has no variables, so every theme is normally hand-copied. Under the
# native-Adwaita policy (docs/design.md "Native controls") a theme file only
# paints surfaces, so it can be generated instead of duplicated: one template,
# one token source, 14 files that cannot drift.
#
# The spacing and type scales are read from XDM.Core/UI/DesignTokens.cs, so a
# value that is not on the scale fails this script instead of quietly shipping.
#
# Usage:
#   scripts/theme-native.py            # rewrite the theme files
#   scripts/theme-native.py --check    # exit 1 if a file differs (CI / pre-commit)
import re
import sys

TABLE = 'app/XDM/XDM.Core/UI/ColorSchemeTable.cs'
TOKENS = 'app/XDM/XDM.Core/UI/DesignTokens.cs'
THEME_DIR = 'app/XDM/XDM.Gtk.UI/theme/'

DARK_TEXT = {'fg': '#f4f4f4', 'view_fg': '#e4e4e4', 'dim': '#b4b4b4', 'caption': '#b4b4b4',
             'band_fg': '#b4b4b4', 'ink': '#ffffff', 'backdrop_ink': '#dbe6f5',
             'hair': 'rgba(255, 255, 255, 0.06)', 'hair_strong': 'rgba(255, 255, 255, 0.10)',
             'hair_edge': 'rgba(255, 255, 255, 0.04)', 'rule': '#0e0e0e'}
LIGHT_TEXT = {'fg': '#222222', 'view_fg': '#242424', 'dim': '#5f5f5f', 'caption': '#5f5f5f',
              'band_fg': '#4a4a4a', 'ink': '#0f172a', 'backdrop_ink': '#1b2739',
              'hair': 'rgba(0, 0, 0, 0.08)', 'hair_strong': 'rgba(0, 0, 0, 0.14)',
              'hair_edge': 'rgba(0, 0, 0, 0.05)', 'rule': '#d0d0d0'}


def rgb(h):
    h = h.lstrip('#')
    return [int(h[i:i + 2], 16) for i in (0, 2, 4)]


def hx(v):
    return '#%02x%02x%02x' % tuple(max(0, min(255, int(round(c)))) for c in v)


def mix(fg, bg, amount):
    """Paints fg at `amount` over bg and returns the flattened hex."""
    f, b = rgb(fg), rgb(bg)
    return hx([f[i] * amount + b[i] * (1 - amount) for i in range(3)])


def lum(h):
    v = [c / 255 for c in rgb(h)]
    v = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in v]
    return .2126 * v[0] + .7152 * v[1] + .0722 * v[2]


def cr(a, b):
    x, y = sorted([lum(a), lum(b)])
    return (y + .05) / (x + .05)


ROW = re.compile(
    r'new ColorSchemeDefinition\("(?P<id>[\w]+)",\s*"(?P<name>[^"]+)",\s*"(?P<css>[\w.-]+\.css)",'
    r'\s*\d+,\s*\d+,\s*\d+,\s*"(?P<accent>#[0-9a-fA-F]{6})",\s*"(?P<hover>#[0-9a-fA-F]{6})",'
    r'\s*"(?P<active>#[0-9a-fA-F]{6})",\s*"(?P<alternate>#[0-9a-fA-F]{6})",'
    r'\s*"(?P<card>#[0-9a-fA-F]{6})",\s*"(?P<base>#[0-9a-fA-F]{6})",'
    r'\s*"(?P<sidebar>#[0-9a-fA-F]{6})",\s*"(?P<header>#[0-9a-fA-F]{6})"\)')

# Spacing and type declarations the scale guards check
SPACE_DECL = re.compile(
    r'(?<![\w-])(?:padding|margin)(?:-(?:top|right|bottom|left))?\s*:\s*([^;]+);')
SIZE_DECL = re.compile(r'(?<![\w-])font-size\s*:\s*([^;]+);')


def schemes():
    """Every scheme row, tagged with the mode it belongs to."""
    src = open(TABLE).read()
    dark_part, light_part = src.split('LightSchemes', 1)
    out = []
    for mode, block in (('dark', dark_part.split('DarkSchemes', 1)[1]), ('light', light_part)):
        for m in ROW.finditer(block):
            out.append(dict(m.groupdict(), dark=(mode == 'dark')))
    return out


def token_scale(name):
    """One int[] token array from DesignTokens.cs (the single scale source)."""
    src = open(TOKENS).read()
    m = re.search(name + r'\s*=\s*\{([^}]*)\}', src)
    if not m:
        raise SystemExit(f"DesignTokens.cs has no {name} array")
    return sorted(int(v) for v in re.findall(r'\d+', m.group(1)))


def ink_for(fill):
    """Label ink that clears the 3.0:1 floor on this fill (ThemeContrast.InkFor)."""
    return '#ffffff' if cr('#ffffff', fill) >= 3.0 else '#1a1a1a'


def deepen(accent, factor):
    """A deeper accent, the form ThemeContrast.HoverFor/active use."""
    return hx([c * factor for c in rgb(accent)])


def theme(s, t):
    """One stylesheet: tokens from the table, structure identical in every theme."""
    accent = s['accent']
    card = s['card']
    # A selected row is an accent tint over the card, never a solid accent block
    selected = mix(accent, card, 0.20)
    backdrop = mix(accent, card, 0.12)
    edge = mix(accent, card, 0.55)
    edge_soft = mix(accent, card, 0.30)
    focus = mix(accent, card, 0.45)
    # Primary action: scheme accent fill, ink derived so the label always reads
    ink = ink_for(accent)
    action_hover = deepen(accent, 0.88)
    action_active = deepen(accent, 0.76)
    # The band is the theme's own header tone, not the accent
    band = s['header']
    band_hover = mix(t['fg'], band, 0.06 if not s['dark'] else 0.08)
    band_active = mix('#000000', band, 0.10) if s['dark'] else mix('#000000', band, 0.04)

    name = 'dark' if s['dark'] else 'light'
    return f"""/*
 * XDM {name} theme layer — {s['name']} — native Adwaita + FetchFlow surfaces
 * © Mayanktaker Computers & Web Development | https://mayanktaker.com
 *
 * GENERATED by scripts/theme-native.py from XDM.Core/UI/ColorSchemeTable.cs.
 * Do not hand-edit; change the template or the scheme row and regenerate.
 *
 * POLICY: Adwaita draws every control (buttons, entries, menus, tooltips,
 * popovers, switches, checks, scrollbars). This file paints only what the
 * toolkit cannot express: window/header surfaces, list surfaces, download rows
 * as cards, the sidebar, the column-header band with its caption insets, the
 * progress fill, and the row selection tint.
 *
 * Tokens: window {s['base']} · sidebar {s['sidebar']} · headerbar {s['header']}
 *   view {s['alternate']} · card {card} · accent {accent}
 *   selected {selected} · backdrop {backdrop}
 */

/* Base surfaces */
window,
dialog {{
  background-color: {s['base']};
  color: {t['fg']};
}}

headerbar {{
  background-color: {s['header']};
  border-bottom: 1px solid {t['rule']};
  color: {t['fg']};
  border-top-left-radius: 7px;
  border-top-right-radius: 7px;
}}

/* Compact padding scoped to the main window headerbar so dialog headerbars keep stock metrics */
headerbar.main-headerbar {{
  padding: 2px 6px;
}}

/* Left-aligned headerbar custom title: bold app name + dim view subtitle */
label.header-title-app {{
  font-weight: bold;
  color: {t['fg']};
}}

label.header-title-view {{
  color: {t['dim']};
}}

/* Update-available dot after the title group */
label.update-dot {{
  color: {'#33d17a' if s['dark'] else '#26a269'};
  font-size: 11px;
  margin-left: 6px;
}}

/* Views: Active and Complete keep distinct surfaces so the two read apart */
treeview {{
  background-color: {s['alternate']};
  color: {t['view_fg']};
}}

treeview.unfinished,
treeview.finished {{
  background-color: transparent;
}}

treeview.finished {{
  background-color: {s['base']};
}}

/* Download rows read as cards: separated, rounded, no bevel and no shadow */
treeview.unfinished row,
treeview.finished row {{
  margin: 4px 10px;
  padding: 6px 8px;
  border-radius: 12px;
  background-color: {card};
  border: 1px solid {t['hair']};
}}

treeview.unfinished row:nth-child(even),
treeview.finished row:nth-child(even) {{
  background-color: {s['alternate']};
}}

treeview.unfinished row:hover,
treeview.finished row:hover {{
  background-color: {s['hover']};
  border-color: {t['hair_strong']};
}}

/* Selection is an accent tint, not a solid accent block */
treeview.unfinished row:selected,
treeview.finished row:selected,
treeview.unfinished:selected,
treeview.finished:selected {{
  background-color: {selected};
  border-color: {edge};
  color: {t['ink']};
}}

treeview.unfinished row:selected:backdrop,
treeview.finished row:selected:backdrop,
treeview.unfinished:selected:backdrop,
treeview.finished:selected:backdrop {{
  background-color: {backdrop};
  border-color: {edge_soft};
  color: {t['backdrop_ink']};
}}

/* Row focus ring stays inside the card */
treeview.unfinished row:focus,
treeview.finished row:focus {{
  outline: 2px solid {focus};
  outline-offset: -2px;
}}

/* Sidebar surfaces */
list,
listbox,
list.sidebar {{
  background-color: {s['sidebar']};
}}

list.sidebar row {{
  border-radius: 8px;
  margin: 2px 6px;
  padding: 6px 10px;
  background-color: transparent;
}}

list.sidebar row:selected {{
  background-color: {selected};
  color: {t['ink']};
}}

treeview.sidebar-list,
treeview.sidebar-status-list {{
  background-color: transparent;
  color: {t['dim']};
  border-radius: 8px;
}}

treeview.sidebar-list:hover,
treeview.sidebar-status-list:hover {{
  color: {t['fg']};
}}

treeview.sidebar-list:selected,
treeview.sidebar-status-list:selected {{
  background-color: {selected};
  color: {t['ink']};
  border-radius: 8px;
}}

scrolledwindow.sidebar-scroll {{
  background-color: {s['sidebar']};
  border-radius: 8px;
}}

label.sidebar-heading {{
  font-size: 11px;
  font-weight: 700;
  color: {t['caption']};
  margin: 14px 20px 4px;
}}

/* Headline above the list: view name, count, select-all and sort (GTK 4 layout) */
.content-toolbar {{
  padding: 14px 18px 8px;
}}

.view-title {{
  font-size: 16px;
  font-weight: 700;
}}

.view-count {{
  font-size: 12px;
  color: {t['dim']};
}}

/* No column header, like GTK 4: select-all and sort live in the bottom bar */

/* Scheme accent reaches the one widget with no accent API in GTK 3 */
progressbar progress {{
  background-color: {accent};
  background-image: none;
  border-radius: 999px;
}}

/* The one control we brand: the primary action carries the scheme accent, and
   its ink is derived from that fill (docs/design.md rule 1). Adwaita owns every
   other control, so this rule is the documented exception, not a drift. */
button.suggested-action {{
  background-color: {accent};
  background-image: none;
  border-color: {action_active};
  color: {ink};
}}

button.suggested-action:hover {{
  background-color: {action_hover};
  border-color: {action_active};
  color: {ink};
}}

button.suggested-action:active,
button.suggested-action:checked {{
  background-color: {action_active};
  color: {ink};
}}

/* Search entry: Adwaita's colours, our compact metrics so the toolbar height
   stays stable between themes */
entry.toolbar-search-entry {{
  min-height: 26px;
  padding: 4px 12px;
  margin: 0;
  border-radius: 999px;
  transition: all 150ms ease;
}}
"""


def off_scale(css, spacing, type_scale):
    """Declarations whose px value is not on the shared spacing/type scale."""
    bad = []
    for value in SPACE_DECL.findall(css):
        bad += [f"{value.strip()}" for px in re.findall(r'(\d+)px', value)
                if int(px) not in spacing]
    for value in SIZE_DECL.findall(css):
        bad += [f"font-size {value.strip()}" for px in re.findall(r'(\d+)px', value)
                if int(px) not in type_scale]
    return bad


def main():
    check = '--check' in sys.argv
    spacing = token_scale('Spacing')
    type_scale = token_scale('TypeScale')
    drift = []
    for s in schemes():
        t = DARK_TEXT if s['dark'] else LIGHT_TEXT
        css = theme(s, t)
        path = THEME_DIR + s['css']
        # Guard: every gap and type size must come from the shared scales
        off = off_scale(css, spacing, type_scale)
        if off:
            raise SystemExit(f"{s['css']}: off-scale values: " + ', '.join(sorted(set(off))))
        # Guard: the generated selection must stay readable with its own ink
        sel = mix(s['accent'], s['card'], 0.20)
        if cr(t['ink'], sel) < 3.2:
            raise SystemExit(f"selection tint unreadable in {s['css']}: "
                             f"{t['ink']} on {sel} = {cr(t['ink'], sel):.2f}:1")
        # Guard: the primary action's label must read on accent, hover and active
        action_ink = ink_for(s['accent'])
        for what, fill in (('action', s['accent']),
                           ('action:hover', deepen(s['accent'], 0.88)),
                           ('action:active', deepen(s['accent'], 0.76))):
            if cr(action_ink, fill) < 3.0:
                raise SystemExit(f"{s['css']}: {what} ink {action_ink} on {fill} "
                                 f"= {cr(action_ink, fill):.2f}:1")
        if check:
            if not __import__('os').path.exists(path) or open(path).read() != css:
                drift.append(s['css'])
        else:
            open(path, 'w').write(css)
        print(f"{s['css']:26s} {s['id']:18s} accent {s['accent']}  selected {sel}")
    if check and drift:
        raise SystemExit('drifted from the generator: ' + ', '.join(drift))
    if check:
        print('all 14 theme files match the generator')


if __name__ == '__main__':
    main()
