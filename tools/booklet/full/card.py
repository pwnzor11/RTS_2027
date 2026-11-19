"""Field Manual quick-reference card (M14-05): two pages, printed separately from the manual.

Page 1: hotkeys (every default binding in GameKeys.cs) and the counter chart in miniature.
Page 2: one column per faction with its tier buildings and main units. Everything is read at build time.
"""
from common import Page, E, FACTIONS, FACTION_ORDER, techtree, title
from appendix import ARMOUR, keys_table, shade, weapon_rows, versus_of

CSS = """
.cd-keys { display:grid; grid-template-columns: repeat(3, 1fr); column-gap:.12in; font-size:5.9pt; line-height:1.1; }
.cd-key { display:flex; gap:2.5pt; break-inside:avoid; margin:0 0 .9pt; }
.cd-key b { flex:0 0 .48in; font-family:'Courier 10 Pitch','Nimbus Mono PS',monospace; color:var(--olive-d); }
.cd-mini { margin-top:3pt; }
.cd-mini .cd-row { display:flex; align-items:center; gap:2pt; margin:0 0 1.2pt; }
.cd-mini .cd-lab { flex:0 0 .6in; font-size:5.4pt; text-transform:uppercase; letter-spacing:.04em; font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; }
.cd-mini .cd-tiles { display:flex; gap:0; }
.cd-mini .cd-tile { display:inline-block; width:.062in; height:.11in; border:.2pt solid #cfc3a0; }
.cd-mini .cd-gap { display:inline-block; width:.07in; }
.ap-s0 { background:#f4efe0; } .ap-s1 { background:#e2d6b6; } .ap-s2 { background:#c4b27a; }
.ap-s3 { background:#8b9a5c; } .ap-s4 { background:#d99a1e; } .ap-na { background:#fff; }
.cd-mini .cd-fac { display:flex; align-items:center; gap:2pt; margin:2pt 0 1pt; }
.cd-mini .cd-fac span { font-family:'TeX Gyre Heros Cn',sans-serif; font-size:5.2pt; text-transform:uppercase; letter-spacing:.05em; color:var(--olive-d); font-weight:700; }
.cd-tech { display:grid; grid-template-columns: repeat(4, 1fr); column-gap:.09in; }
.cd-col h3 { font-size:6.6pt; margin:0 0 2pt; border-bottom:1.2pt solid var(--rule); padding-bottom:1pt; }
.cd-col h4 { font-family:'TeX Gyre Heros Cn',sans-serif; font-size:5.6pt; letter-spacing:.1em; text-transform:uppercase; margin:4pt 0 1pt; color:var(--muted); }
.cd-col ul { margin:0; padding:0; list-style:none; font-size:5.8pt; line-height:1.14; }
.cd-col li { margin:0 0 .6pt; }
.cd-col li.cd-u { font-style:italic; }
"""

LABEL_RE_SKIP = ('Build & System',)


def _label(aid):
    """Short on-card label derived from the action id: scroll_left -> Scroll left, group_1 -> Group 1."""
    return aid.replace('_', ' ').capitalize()


def hotkeys_html():
    rows, _ = keys_table()
    items = [(key, _label(aid)) for grp, key, desc, aid in rows if aid]
    return '<div class="cd-keys">' + ''.join(
        f'<div class="cd-key"><b>{E(k)}</b><span>{E(l)}</span></div>' for k, l in items) + '</div>'


def miniature_html():
    rows = weapon_rows()
    tiles = {c: [] for c in ARMOUR}
    cur = None
    fac_html = ''
    groups = []
    for f, uid, pw in rows:
        if f != cur:
            if groups: groups.append(('gap', None))
            groups.append(('fac', FACTIONS[f]['short']))
            cur = f
        groups.append(('w', (uid, pw)))
    out = ['<div class="cd-mini">']
    for c in ARMOUR:
        cells = []
        for kind, val in groups:
            if kind == 'gap':
                cells.append('<span class="cd-gap"></span>')
            elif kind == 'w':
                uid, pw = val
                v = versus_of(pw).get(c)
                cells.append(f'<span class="cd-tile {shade(v)}" title="{E(title(uid))}: {v}"></span>')
        out.append(f'<div class="cd-row"><span class="cd-lab">{E(c)}</span><span class="cd-tiles">{"".join(cells)}</span></div>')
    out.append('</div>')
    # faction names under the strip, using the same spacing as the tiles
    names = []
    cur = None
    for f, uid, pw in rows:
        if f != cur:
            cur = f
            names.append(f'<span>{E(FACTIONS[f]["short"])}</span>')
    out.append('<div class="cd-fac">' + ' &middot; '.join(names) + '</div>')
    return ''.join(out)


def page_hotkeys():
    body = (f'<style>{CSS}</style><h2>Hotkeys</h2>{hotkeys_html()}'
            f'<h2 style="margin-top:8pt">Counter chart</h2>'
            f'<p style="font-size:6.8pt;margin:0 0 2pt">Warhead percent against each armour class, one tile per weapon, '
            f'grouped by faction.</p>{miniature_html()}')
    return Page(body, runhead='Quick reference', draft=False)


def _tier_list(faction):
    """Per tier: buildings and main units (infantry and vehicles), names via title()."""
    nodes = techtree(faction)['nodes']
    out = []
    for tier in (1, 2, 3):
        b = [title(k) for k, v in nodes.items() if v['kind'] == 'building' and v['tier'] == tier]
        u = [(title(k), v['kind']) for k, v in nodes.items() if v['kind'] in ('infantry', 'vehicle') and v['tier'] == tier]
        out.append((tier, b, u))
    return out


def page_factions():
    cols = []
    for f in FACTION_ORDER:
        info = FACTIONS[f]
        tiers = []
        for tier, b, u in _tier_list(f):
            bl = ''.join(f'<li>{E(n)}</li>' for n in b)
            ul = ''.join(f'<li class="cd-u">{E(n)}</li>' for n, _ in u)
            tiers.append(f'<h4>Tier {tier}</h4><ul>{bl}{ul}</ul>')
        cols.append(f'<div class="cd-col"><h3 style="color:{info["accent_d"]}">{E(info["short"])}</h3>{"".join(tiers)}</div>')
    body = (f'<style>{CSS}</style><h2>Tech trees</h2>'
            f'<p style="font-size:6.8pt;margin:0 0 4pt">Buildings in roman type, main units in italics, by tier.</p>'
            f'<div class="cd-tech">{"".join(cols)}</div>')
    return Page(body, runhead='Quick reference', draft=False)


def pages():
    return [page_hotkeys(), page_factions()]
