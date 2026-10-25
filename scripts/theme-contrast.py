#!/usr/bin/env python3
# © Mayanktaker Computers & Web Development | https://mayanktaker.com
# Theme contrast generator for XDM.Gtk.UI/theme/*.css.
#
# GTK 3 CSS has no variables, so every accent hex is written out per theme file.
# This script derives them from one rule instead of by hand, and is idempotent:
# run it after changing a scheme accent, adding a theme, or adding a rule that
# paints text on the accent. Its math mirrors XDM.Core/UI/ThemeContrast.cs, so
# GTK 3, GTK 4 and WPF always resolve the same ink and the same hover.
#
#   A) dark search pill  -> the normal entry surface (the authored light ink reads)
#   B) one ink per theme -> white stays while it clears the floor, else dark ink
#   C) one hover per theme -> a deeper accent (0.88x), reused at every hover site
#   E) bevel gradients   -> the light end must clear the floor with that ink
#
# Usage: scripts/theme-contrast.py [--dry-run]
#  A) dark search pill -> normal entry surface (authored light ink reads again)
#  B) one ink per theme for accent-painted surfaces (white below 3.0 -> dark ink)
#  C) one accent-hover per theme (deeper accent), reused at every hover site
#  E) bevel gradients: the light end must clear the floor with that same ink
# Mirrors ThemeContrast (XDM.Core/UI/ThemeContrast.cs) so GTK3 == GTK4 == WPF.
import re, glob

ROOT = 'app/XDM/XDM.Gtk.UI/theme/'
DRY = '--dry-run' in __import__('sys').argv
FLOOR = 3.2          # AA large-text floor (3.0) + headroom
HOVER_SCALE = 0.88   # deeper accent = hover
INK_DARK = '#1a1a1a'

COMMENT = re.compile(r'/\*.*?\*/', re.S)
STATE = re.compile(r':(hover|active|focus|focus-within|checked)$')
RULE = re.compile(r'([^{}]+)\{([^{}]*)\}')
BG = re.compile(r'(?<![\w-])background-color\s*:\s*(#[0-9a-fA-F]{6})\s*;')
FG = re.compile(r'(?<![\w-])color\s*:\s*(#[0-9a-fA-F]{6})\s*;')
GRAD = re.compile(r'linear-gradient\([^)]*\)')
HEX = re.compile(r'#[0-9a-fA-F]{6}')

ACCENT_SELECTORS = {
    'treeview.sidebar-status-list:selected', 'treeview.sidebar-list:selected',
    'row:selected', 'list > row:selected', 'list.sidebar > row:selected',
    'list row:selected', 'list.sidebar row:selected',
    'entry selection', 'button.suggested-action', 'menuitem:hover',
    'checkbutton check:checked', 'radiobutton radio:checked',
    'treeview.sidebar-list row:selected',
    'treeview.unfinished header button', 'treeview.finished header button',
}
HOVER_SELECTORS = {'treeview.sidebar-list row:selected:hover',
                   'treeview.sidebar-list:selected:hover',
                   'button.suggested-action:hover'}

def rgb(h):
    h = h.lstrip('#')
    return [int(h[i:i+2], 16) for i in (0, 2, 4)]

def hx(v):
    return '#%02x%02x%02x' % tuple(max(0, min(255, int(round(c)))) for c in v)

def lum(h):
    v = [c / 255 for c in rgb(h)]
    v = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in v]
    return .2126 * v[0] + .7152 * v[1] + .0722 * v[2]

def cr(a, b):
    x, y = sorted([lum(a), lum(b)])
    return (y + .05) / (x + .05)

def ink_for(fill):
    return '#ffffff' if cr('#ffffff', fill) >= 3.0 else INK_DARK

def away_from(fill, ink):
    dark_ink = lum(ink) < 0.5
    v, out = rgb(fill), fill
    for _ in range(80):
        if cr(out, ink) >= FLOOR:
            return out
        v = [max(0, min(255, c * (1.08 if dark_ink else 0.95))) for c in v]
        out = hx(v)
    return out

def hover_for(accent, ink):
    deeper = hx([c * HOVER_SCALE for c in rgb(accent)])
    return deeper if cr(deeper, ink) >= FLOOR else away_from(accent, ink)

def scale(hexes, f):
    return [hx([c * f for c in rgb(h)]) for h in hexes]

def swap_all(text, old, new):
    return text.replace(old, new)

def selectors(raw):
    return [s.strip() for s in COMMENT.sub('', raw).split(',') if s.strip()]

def is_accent(sel):
    return sel in ACCENT_SELECTORS or STATE.sub('', sel) in ACCENT_SELECTORS

edits = []
for path in sorted(glob.glob(ROOT + '*.css')):
    name = path.split('/')[-1]
    src = open(path).read()

    # (A) Dark search pill
    if '-dark' in name:
        eb = BG.findall(re.search(r'^entry \{([^{}]*)\}', src, re.M).group(1))[0]
        before = src
        src = re.sub(r'(entry\.toolbar-search-entry \{[^{}]*background-color: )#[0-9a-fA-F]{6};',
                     lambda m: m.group(1) + eb + ';', src, count=1)
        src = re.sub(r'(entry\.toolbar-search-entry:focus \{[^{}]*?)background-color: #[0-9a-fA-F]{6};\s*',
                     lambda m: m.group(1), src, count=1)
        if src != before:
            edits.append((name, 'search pill -> %s' % eb))

    # Family fills, plus this theme's one ink and one hover
    family = set()
    for m in RULE.finditer(src):
        if any(is_accent(x) for x in selectors(m.group(1))):
            family.update(BG.findall(m.group(2)))
    accent = re.search(r'treeview\.sidebar-list:selected \{[^{}]*?background-color: (#[0-9a-fA-F]{6})',
                       src).group(1)
    ink = ink_for(accent)
    hover = hover_for(accent, ink)
    edits.append((name, 'accent %s  ink %s  hover %s  (white %.2f)'
                  % (accent, ink, hover, cr('#ffffff', accent))))

    # (B) Ink on accent-painted rules
    def fix_ink(m):
        sel, body = selectors(m.group(1)), m.group(2)
        if not any(is_accent(x) for x in sel) or any(x.endswith(':active') for x in sel):
            return m.group(0)
        bgs = BG.findall(body)
        if not bgs or bgs[-1] not in family:
            return m.group(0)
        bg = bgs[-1]

        def one(mm):
            cur = mm.group(1)
            if cur == ink or cr(cur, bg) >= 3.0:
                return mm.group(0)
            edits.append((name, '  ink %s -> %s on %s  %s' % (cur, ink, bg, sel[0][:32])))
            return mm.group(0).replace(cur, ink)

        return FG.sub(one, m.group(0), count=1)

    src = RULE.sub(fix_ink, src)

    # (C) Every accent hover site carries the one accent-hover fill
    def fix_hover(m):
        sel = selectors(m.group(1))
        if not any(x in HOVER_SELECTORS for x in sel):
            return m.group(0)
        body = m.group(2)
        bgs = BG.findall(body)
        if not bgs:
            return m.group(0)
        old = bgs[-1]
        if old == hover:
            return m.group(0)
        body = body.replace(old, hover)

        def rebuild(g):
            hi = hx([c + (255 - c) * 0.12 for c in rgb(hover)])
            lo = hx([c * 0.88 for c in rgb(hover)])
            return 'linear-gradient(to bottom, %s, %s)' % (hi, lo)

        if GRAD.search(body):
            body = GRAD.sub(rebuild, body)
        edits.append((name, '  hover %s -> %s  %s' % (old, hover, sel[0][:32])))
        return m.group(0).replace(m.group(2), body)

    src = RULE.sub(fix_hover, src)

    # (E) Bevel gradients: the light end is what the eye reads, so it must
    #     clear the floor with the theme ink as well
    def fix_bevel(m):
        sel = selectors(m.group(1))
        if not any(is_accent(x) for x in sel):
            return m.group(0)
        if any(x.endswith(':active') or x in HOVER_SELECTORS for x in sel):
            return m.group(0)
        body = m.group(2)
        if not GRAD.search(body):
            return m.group(0)

        def one(g):
            stops = HEX.findall(g.group(0))
            f = 1.0
            while f > 0.3 and not all(cr(s, ink) >= FLOOR for s in scale(stops, f)):
                f -= 0.02
            if f >= 1.0:
                return g.group(0)
            out = g.group(0)
            for old, new in zip(stops, scale(stops, f)):
                out = out.replace(old, new)
            edits.append((name, '  bevel x%.2f %s  %s' % (f, '/'.join(stops), sel[0][:30])))
            return out

        return m.group(0).replace(m.group(2), GRAD.sub(one, body))

    src = RULE.sub(fix_bevel, src)
    if not DRY:
        open(path, 'w').write(src)

for p, e in edits:
    print('%-24s %s' % (p, e))
print('--- %d edits' % len(edits))
