"""Field Manual Part III field-guide entries (M14-05c): units, structures and upgrades of all four factions.

    faction_entries(faction) -> list[Page]   infantry, vehicles (incl. aircraft and ships), structures and defences, upgrades
    pages()                                  all four factions (preview only)

Every number comes from rules_resolved.json, every name and text from the Archive. Nothing here is new prose, so no page is a draft.
Entries are packed onto pages by measuring their real rendered height (Chromium, same stylesheet as the book), so nothing overflows.
"""
import pathlib, sys
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import (Page, FACTIONS, FACTION_ORDER, ROOT, FULL, E, img, hero, rules, techtree, archive, title)

PAGE_H = 7.28 * 96          # usable body height in CSS px (page 8.5in, 0.5in top padding, 0.55in bottom, small safety margin)
BODY_W = '4.45in'           # page width 5.5in minus the two paddings
BASE = ['unarmored', 'light', 'heavy', 'structure', 'fortified']
SHIPS = BASE + ['ship_light', 'ship_heavy']
LAB = {'unarmored': 'Infantry', 'light': 'Light', 'heavy': 'Armour', 'structure': 'Bldg', 'fortified': 'Defence',
       'ship_light': 'Ship L', 'ship_heavy': 'Ship H'}
ARMN = {'unarmored': 'infantry', 'light': 'light vehicles', 'heavy': 'armour', 'structure': 'structures', 'fortified': 'defences',
        'ship_light': 'light ships', 'ship_heavy': 'heavy ships'}

CSS = """<style>
.p3e-h p, .p3e-f p, .p3e-c p { margin:0 0 3pt; }
.p3e-b { display:grid; grid-template-columns:1.6in minmax(0,1fr); gap:8pt; }
.p3e-b p { font-size:8pt; line-height:1.27; } .p3e-b .fieldnote { font-size:6.6pt; margin:2pt 0; padding:1.5pt 4pt; }
.p3e-b .stats { font-size:7pt; margin:2pt 0 0; } .p3e-b .stats td { padding:.7pt 2pt; white-space:nowrap; }
.p3e-b .stats td:first-child { width:auto; font-size:6.4pt; } .p3e-b .art { margin-bottom:0; }
.p3e-h .unit, .p3e-c .unit { margin-bottom:6pt; }
.p3e-top { display:grid; grid-template-columns:1.3in 1fr; gap:8pt; }
.p3e-c .p3e-top { grid-template-columns:1.15in 1fr; } .p3e-top .art { height:auto; padding:3pt 0; display:flex; align-items:center; justify-content:center; }
.p3e-row2 { display:grid; grid-template-columns:1fr 2.05in; gap:8pt; align-items:end; margin-top:3pt; }
.p3e-row2.solo { grid-template-columns:1fr; }
.p3e-s2 { width:100%; border-collapse:collapse; font-family:'TeX Gyre Heros Cn',sans-serif; font-size:7.4pt; }
.p3e-s2 td { border-bottom:.6pt solid #a99d7f; padding:1pt 3pt 1pt 2pt; white-space:nowrap; }
.p3e-s2 td.l { text-transform:uppercase; letter-spacing:.05em; color:var(--muted); font-size:6.6pt; }
.p3e-s2 td.v { font-weight:700; } .p3e-s2 td.w { white-space:normal; font-weight:700; }
.p3e-vs .versus .m { height:19pt; } .p3e-vs .versus div, .p3e-f .versus div { white-space:nowrap; } .p3e-vs .versus[style] div, .p3e-f .versus[style] div { font-size:5.4pt; letter-spacing:0; } .p3e-vs .kicker { margin-top:0 !important; }
.p3e-f .stats td { white-space:nowrap; } .p3e-f .mini { grid-template-columns:1.85in 1fr; }
.p3e-up .u { border-top:1.2pt solid var(--rule); padding:4pt 0 4pt; }
.p3e-up .u .t { display:flex; justify-content:space-between; align-items:baseline; font-family:'TeX Gyre Heros Cn',sans-serif; }
.p3e-up .u .t b { font-size:11pt; text-transform:uppercase; letter-spacing:.03em; } .p3e-up .u .t span { font-weight:700; font-size:10pt; }
.p3e-up .u .e { font-style:italic; margin:1pt 0; } .p3e-up .u .m { font-family:'TeX Gyre Heros Cn',sans-serif; font-size:7.2pt; color:var(--muted); text-transform:uppercase; letter-spacing:.06em; }
.p3e-wall { width:100%; border-collapse:collapse; font-family:'TeX Gyre Heros Cn',sans-serif; font-size:7.6pt; margin-top:2pt; }
.p3e-wall th { text-align:left; font-size:6.6pt; letter-spacing:.1em; text-transform:uppercase; color:var(--muted); border-bottom:1pt solid var(--rule); padding:1pt 2pt; }
.p3e-wall td { border-bottom:.6pt solid #a99d7f; padding:1.6pt 2pt; } .p3e-wall td.n, .p3e-wall th.n { text-align:right; padding-right:10pt; }
.p3e-wall i { font-family:'TeX Gyre Schola',serif; color:var(--muted); }
</style>"""


# ---- data ----------------------------------------------------------------------------------------------------------------
def _prereq(r):
    return ', '.join(title(p) for p in r['prerequisites'] if 'tier' not in p) or '—'


def _weapon(r):
    R = rules()
    wid = r.get('primary_weapon')
    w = R['weapons'].get(wid) if wid else None
    if not w: return None
    return {'range': w['range_cells'], 'damage': w['damage'], 'reload': w['reload_seconds'], 'targets': w['targets'],
            'vs': R['warheads'][w['warhead']]['versus']}


def card(uid):
    R = rules(); A = archive()
    r = R['units'].get(uid) or R['buildings'][uid]
    is_unit = uid in R['units']
    c = {'id': uid, 'title': title(uid), 'subtitle': A[uid]['subtitle'], 'note': A[uid]['field_note'], 'body': A[uid]['body'].strip(),
         'cost': r['cost'], 'hp': r['max_health'], 'armor': r['armor'].replace('_', ' '), 'build': r['build_seconds'],
         'sight': r['sight'], 'tier': r.get('tier', 1), 'prereq': _prereq(r), 'unit': is_unit, 'weapon': _weapon(r), 'raw': r}
    if is_unit:
        c['speed'] = round(r['speed'] * 20 / 256, 2); c['loco'] = r['locomotion']
    w = c['weapon']
    if w:
        classes = SHIPS if r.get('domain') == 'naval' else BASE
        c['classes'] = classes
        c['strong'] = [ARMN[a] for a in classes if w['vs'][a] >= 75]
        c['weak'] = [ARMN[a] for a in classes if w['vs'][a] <= 40]
    return c


def stat_list(c):
    r = c['raw']; w = c['weapon']
    rows = [('Cost', f"{c['cost']} credits"), ('Build time', f"{c['build']:g} s"), ('Armour', f"{c['hp']} HP · {c['armor']}")]
    if c['unit']:
        rows.append(('Speed', f"{c['speed']:g} cells/s"))
    rows.append(('Sight', f"{c['sight']} cells"))
    if c['unit']:
        if r['harvest_capacity']: rows.append(('Ash capacity', f"{r['harvest_capacity']}"))
        if r['passengers']: rows.append(('Passengers', f"{r['passengers']}"))
    else:
        if r['power'] > 0: rows.append(('Power', f"supplies {r['power']}"))
        elif r['power'] < 0: rows.append(('Power', f"uses {-r['power']}"))
        if r['produces']: rows.append(('Produces', r['produces']))
        if r['storage']: rows.append(('Storage', f"{r['storage']}"))
    if w:
        rows.append(('Weapon range', f"{w['range']:g} cells"))
        rows.append(('Damage / reload', f"{w['damage']} / {w['reload']:g} s"))
        if w['targets'] != 'ground': rows.append(('Fires at', w['targets']))
    elif c['unit']:
        rows.append(('Weapon', 'none'))
    rows.append(('Requires', c['prereq']))
    return rows


def stat_rows(c):
    rows = stat_list(c)
    return '<table class="stats">' + ''.join(f'<tr><td>{E(a)}</td><td>{E(b)}</td></tr>' for a, b in rows) + '</table>'


def stat_pairs(c):
    rows = stat_list(c); req = rows.pop()
    trs = ''
    for k in range(0, len(rows), 2):
        pr = rows[k:k + 2]
        tr = ''.join(f'<td class="l">{E(a)}</td><td class="v">{E(b)}</td>' for a, b in pr)
        if len(pr) == 1: tr += '<td></td><td></td>'
        trs += f'<tr>{tr}</tr>'
    trs += f'<tr><td class="l">{E(req[0])}</td><td class="w" colspan="3">{E(req[1])}</td></tr>'
    return f'<table class="p3e-s2">{trs}</table>'


def versus(c):
    w = c['weapon']
    if not w: return ''
    cls = c['classes']
    cells = ''.join(f'<div><div class="m"><i style="height:{min(w["vs"][k], 100)}%"></i><b>{w["vs"][k]}%</b></div>{LAB[k]}</div>' for k in cls)
    grid = f' style="grid-template-columns:repeat({len(cls)},1fr)"' if len(cls) != 5 else ''
    return f'<div class="kicker" style="font-size:6.5pt;margin-top:4pt">Damage against</div><div class="versus"{grid}>{cells}</div>'


def use_line(c):
    if not c['weapon']: return ''
    return (f"<p style=\"font-size:7.6pt\"><b>Use it for:</b> {E(', '.join(c['strong']) or '—')}. "
            f"<b>Keep it away from:</b> {E(', '.join(c['weak']) or '—')}.</p>")


def art(uid, h, px):
    p = hero(uid)
    if p is None:   # fall back to a sprite frame (every id has a hero today, so this is only a guard)
        p = _sprite_frame(uid)
    return f'<img src="{img(p, max_px=px)}" style="height:{h * .82:.2f}in;max-width:100%;object-fit:contain">'


def _sprite_frame(uid):
    from PIL import Image
    for sub in ('vehicles', 'infantry', 'buildings'):
        f = ROOT / 'data/sprites' / sub / f'{uid}.png'
        if f.exists():
            out = FULL / 'cache' / f'sprite_{uid}.png'; out.parent.mkdir(exist_ok=True)
            im = Image.open(f); n = max(1, round(im.width / max(1, im.height)))
            fw = im.width // n if n > 1 else im.width
            im.crop((0, 0, fw, im.height)).save(out)
            return out
    raise FileNotFoundError(uid)


# ---- entry html ----------------------------------------------------------------------------------------------------------
def full(c):
    h = 1.35
    sub = f"{E(c['subtitle'])} · Tier {c['tier']}" + (f" · {E(c['loco'])}" if c['unit'] else '')
    return f"""<div class="unit p3e-f">
<div class="head"><h2>{E(c['title'])}</h2><div class="cost">{c['cost']}</div></div>
<div class="sub">{sub}</div>
<div class="art" style="height:{h}in"><span class="plate">PLATE {E(c['id'].split('_')[0].upper())}-{E(c['id'][-4:].upper())}</span>{art(c['id'], h, 1100)}</div>
<div class="mini" style="margin-top:6pt"><div>{stat_rows(c)}{versus(c)}</div>
<div><p>{E(c['body'])}</p><div class="fieldnote">{E(c['note'])}</div>{use_line(c)}</div></div></div>"""


def half(c, compact=False):
    h = .8
    sub = f"{E(c['subtitle'])} \u00b7 Tier {c['tier']}"
    return f"""<div class="unit {'p3e-c' if compact else 'p3e-h'} p3e-vs">
<div class="head"><h2 style="font-size:12pt">{E(c['title'])}</h2><div class="cost" style="font-size:11pt">{c['cost']}</div></div>
<div class="sub" style="margin-bottom:2pt">{sub}</div>
<div class="p3e-b"><div><div class="art" style="height:{h}in">{art(c['id'], h, 700)}</div>{stat_rows(c)}</div>
<div><p>{E(c['body'])}</p><div class="fieldnote">{E(c['note'])}</div>{versus(c)}{use_line(c) if not compact else ''}</div></div></div>"""


def walls_table(ids):
    R = rules()
    rows = ''
    for i in ids:
        b = R['buildings'][i]
        rows += (f"<tr><td><b>{E(title(i))}</b><br><i>{E(archive()[i]['subtitle'])}</i></td><td class=\"n\">{b['cost']}</td>"
                 f"<td class=\"n\">{b['max_health']}</td><td>{E(b['armor'].replace('_', ' '))}</td></tr>")
    return (f'<div class="kicker" style="margin-top:4pt">Walls and gates</div>'
            f'<table class="p3e-wall"><tr><th>Name</th><th class="n">Cost</th><th class="n">HP</th><th>Armour</th></tr>{rows}</table>')


def upgrades_html(f, ids):
    R = rules(); T = techtree(f)['nodes']; A = archive()
    out = ''
    for i in ids:
        u = R['upgrades'][i]
        where = ', '.join(title(b) for b in T[i]['producer'] if R['buildings'].get(b, {}).get('researches')) or '—'
        out += (f'<div class="u"><div class="t"><b>{E(title(i))}</b><span>{u["cost"]}</span></div>'
                f'<div class="e">{E(A[i]["subtitle"])}</div>'
                f'<div class="m">Researched at: {E(where)} · Tier {u["tier"]} · {u["build_seconds"]:g} s</div></div>')
    return f'<h2>Upgrades</h2><div class="p3e-up">{out}</div>'


# ---- measuring and packing -----------------------------------------------------------------------------------------------
_heights = {}


def measure(items, faction):
    """items: list of (key, html). Fills _heights[key] with the rendered height in CSS px (including margins)."""
    todo = [(k, h) for k, h in items if k not in _heights]
    if not todo: return
    from playwright.sync_api import sync_playwright
    css = (FULL / 'style.css').read_text()
    body = ''.join(f'<div class="k" data-k="{E(k)}">{h}</div>' for k, h in todo)
    doc = (f"<!doctype html><html><head><meta charset='utf-8'><style>{css}</style>{CSS}</head><body>"
           f"<div class='page right f-{faction}' style='height:auto;overflow:visible;padding-top:0;padding-bottom:0'>{body}</div></body></html>")
    tmp = FULL / 'cache' / 'p3e_measure.html'; tmp.parent.mkdir(exist_ok=True); tmp.write_text(doc)
    with sync_playwright() as pw:
        b = pw.chromium.launch(); pg = b.new_page(); pg.goto(tmp.as_uri(), wait_until='networkidle')
        res = pg.evaluate("[...document.querySelectorAll('.k')].map(e=>{const r=e.getBoundingClientRect();"
                          "const m=parseFloat(getComputedStyle(e.firstElementChild||e).marginBottom)||0;return [e.dataset.k,r.height+m]})")
        b.close()
    for k, h in res: _heights[k] = h


def pack(items, faction, first_extra=0):
    """Packs [(key, html)] into pages of PAGE_H in order, taking one of the next few entries early when the next one does not fit."""
    measure(items, faction)
    left = list(items); pagesl = []
    while left:
        used = 0; cur = []
        while left:
            pick = None
            for n, (k, h) in enumerate(left):
                if used + _heights[k] <= PAGE_H: pick = n; break
            if pick is None: break
            k, h = left.pop(pick); used += _heights[k]; cur.append(h)
        if not cur:   # entry taller than a page: cannot happen with the current layouts
            raise ValueError('entry too tall: ' + left[0][0])
        pagesl.append(cur)
    return pagesl


# ---- selection -------------------------------------------------------------------------------------------------------------
def obtainable(f):
    R = rules(); T = techtree(f)['nodes']; pre = FACTIONS[f]['prefix']
    return [i for i, n in T.items() if i.startswith(pre) and n.get('obtainable')], T


def mains(vehicles):
    """Harvester (largest ash capacity), main battle tank (armed turreted land vehicle: lowest tier, then dearest), top-tier unit."""
    R = rules()['units']
    v = lambda i: R[i]
    harv = max((i for i in vehicles if v(i)['harvest_capacity']), key=lambda i: v(i)['harvest_capacity'], default=None)
    tanks = [i for i in vehicles if v(i)['primary_weapon'] and v(i)['has_turret'] and v(i)['domain'] == 'ground']
    mbt = min(tanks, key=lambda i: (v(i)['tier'], -v(i)['cost'], -v(i)['max_health']), default=None)
    top = max(vehicles, key=lambda i: (v(i)['tier'], v(i)['cost']))
    out = []
    for i in (harv, mbt, top):
        if i and i not in out: out.append(i)
    return out


def faction_entries(faction):
    R = rules()
    f = faction; F = FACTIONS[f]
    ids, T = obtainable(f)
    key = lambda i: (T[i].get('tier', 1), T[i]['cost'], i)
    inf = sorted((i for i in ids if T[i]['kind'] == 'infantry'), key=key)
    veh = sorted((i for i in ids if T[i]['kind'] == 'vehicle'), key=key)
    bld = [i for i in ids if T[i]['kind'] == 'building']
    walls = sorted((i for i in bld if R['buildings'][i]['wall']), key=key)
    structs = sorted((i for i in bld if not R['buildings'][i]['wall']), key=lambda i: (T[i]['category'] == 'defence', ) + key(i))
    upg = sorted((i for i in ids if T[i]['kind'] == 'upgrade' and i != 'dir_up_order_seven'), key=key)   # see report QUESTIONS

    out = []

    def emit(label, chunks, extra=''):
        for n, ch in enumerate(chunks):
            body = (CSS if (not out) else '') + ''.join(ch) + (extra if (n == len(chunks) - 1) else '')
            out.append(Page(body, runhead=f"{F['short']} · Field Guide: {label}", faction=f))

    emit('Infantry', (pack([(i, half(card(i))) for i in inf], f)))
    main = mains(veh)
    rest = [i for i in veh if i not in main]
    items = [(i, full(card(i))) for i in main] + [(i, half(card(i))) for i in rest]
    emit('Vehicles', pack(items, f))
    # structures: three per page where they fit; the wall table goes in the free space at the end
    items = [(i, half(card(i), compact=True)) for i in structs]
    wt = walls_table(walls) if walls else ''
    if wt: items.append(('__walls__' + f, wt))
    emit('Structures and Defences', pack(items, f))
    out.append(Page(upgrades_html(f, upg), runhead=f"{F['short']} · Field Guide: Upgrades", faction=f))
    return out


def pages():
    out = []
    for f in FACTION_ORDER: out += faction_entries(f)
    return out


if __name__ == '__main__':
    for f in FACTION_ORDER:
        print(f, len(faction_entries(f)))
