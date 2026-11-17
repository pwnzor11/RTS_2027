"""Field Manual Part III (M14-05d): faction openers, society and leaders, commander's notes, tech trees.
Exposes pages() -> list[Page]. Entries pages come from part3_entries.faction_entries(faction) (another helper's module).
Generated SVGs (emblems, tree halves, research hexagons) are written to tools/booklet/full/img/part3_<faction>_*.svg."""
import html, re, subprocess, sys, tomllib
from common import ROOT, FULL, FACTIONS, FACTION_ORDER, Page, E, img, rules, techtree, archive, title


def rich(t):
    """Escape plain text and turn the Archive's **bold** and *italic* marks into HTML."""
    t = E(' '.join(t.split()), quote=False)
    t = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', t)
    return re.sub(r'\*(.+?)\*', r'<i>\1</i>', t)


def paras(text):
    return ''.join(f'<p>{rich(p)}</p>' for p in re.split(r'\n\s*\n', text.strip()) if p.strip())

try:
    from part3_entries import faction_entries
except ImportError:
    def faction_entries(faction):
        return [Page('<div class="placeholder">Entries for %s are not built yet.</div>' % E(FACTIONS[faction]['short']),
                     runhead=FACTIONS[faction]['short'] + ' · Field Guide', faction=faction)]

IMG = FULL / 'img'
INK, PAPER = '#1e1b16', '#efe7d3'
TIER_COL = {1: '#dfe0c8', 2: '#efd9a0', 3: '#e9bba4'}
TREE_FONT = 'TeX Gyre Heros Cn'


# ======================================================================================================================
# text sources
# ======================================================================================================================
def lore_entries(faction):
    """The four approved lore entries of design 19 (section 6 / 6b / 6c / 6d), verbatim: [(title, aside, text)]."""
    md = (ROOT / 'docs/design/19_field_manual.md').read_text()
    marker = {'authority': '## 6. ', 'directorate': '## 6b. ', 'left_behind': '## 6c. ', 'expedition': '## 6d. '}[faction]
    sec = md.split(marker, 1)[1]
    sec = re.split(r'\n## ', sec, 1)[0]
    out = []
    for block in re.split(r'\n\s*\n', sec):
        block = ' '.join(block.split())
        m = re.match(r'\*\*(.+?)\*\*\s*(.*)$', block)
        if not m: continue
        ttl, rest = m.group(1).rstrip('.'), m.group(2)
        aside = ''
        mt = re.match(r'(.*?)\s*(\(.*\))$', ttl)
        if mt: ttl, aside = mt.group(1), mt.group(2)
        mm = re.match(r'(\([^)]*\))\s*(.*)$', rest)
        if mm: aside, rest = mm.group(1), mm.group(2).lstrip('. ')
        out.append((ttl, aside, rest))
    assert len(out) == 4, (faction, [o[0] for o in out])
    return out


def arch(uid):
    return archive()[uid]


def arch_para(uid, n=None):
    """Body of an Archive entry (verbatim); n = keep only the first n paragraphs."""
    ps = [p for p in re.split(r'\n\s*\n', arch(uid)['body'].strip()) if p.strip()]
    return ps if n is None else ps[:n]


# per faction: Archive entries on the society pages, and leaders (Archive leader ids with their motivation line)
SOCIETY = {
    'authority':   ['faction_auth_society', 'faction_auth_army', 'faction_auth_daily_life'],
    'directorate': ['faction_dir_voice'],
    'left_behind': ['faction_lb_culture'],
    'expedition':  ['faction_exp_reason'],
}
# Archive entries printed under the emblem on the second page of the opener spread
OPENER_ARCH = {
    'authority':   ['faction_auth_symbols', 'faction_auth_values'],
    'directorate': ['faction_dir_bunker'],
    'left_behind': ['faction_lb_abandonment', 'faction_lb_first_rigs'],
    'expedition':  ['faction_exp_symbols', 'faction_exp_origin'],
}
LEADERS = {
    'authority':   ['leader_auth_wrenfield', 'leader_auth_pell'],
    'directorate': ['leader_dir_harrowgate', 'leader_dir_larkin', 'leader_dir_thrall', 'leader_dir_lund'],
    'left_behind': ['leader_lb_calloway_ross'],
    'expedition':  ['leader_exp_aldersey', 'leader_exp_strand', 'leader_exp_roque'],
}
# how many society blocks (Archive, then lore) go on page 1 of the society spread (the rest go on page 2)
SPLIT = {'authority': 4, 'directorate': 3, 'left_behind': 3, 'expedition': 3}

FACT = {
    'authority': dict(
        kicker='Faction one · The protagonists', quote='Count the living, mend the line.', quote_src='teletype header, every message since 1963',
        rows=[('Founded', 'February 1963, the Lakeshore Conference'), ('Seat', 'The Administration, answering to the Charter'),
              ('Field commander', 'Brigadier Hester Wrenfield'), ('Colours', 'Working grey, olive green, safety yellow'),
              ('Plays like', 'Slow, armoured and very hard to dislodge. The best artillery on the continent.')],
        symbols='faction_auth_symbols'),
    'directorate': dict(
        kicker='Faction two · The Interior Annex', quote='The surface is not an enemy but unfinished business.', quote_src='the Doctrine of Reclamation',
        rows=[('Founded', 'November 1962, when the Interior Annex closed its blast doors'), ('Seat', 'The Interior Annex'),
              ('Leader', 'Director Lucan Harrowgate'), ('Colours', 'Bone-white concrete, red signal paint, brass speaker grilles'),
              ('Plays like', 'Conscripts in numbers. Open every operation from below, with Hatches and Bores.')],
        symbols=None),
    'left_behind': dict(
        kicker='Faction three · The caravans of the Drift', quote='A rig which has been counted has not been left.', quote_src='the caravans, on the evening count',
        rows=[('Founded', '1990 at Tern Point; the first caravan left in 1994'), ('Seat', 'None. The Gathering is held wherever the wind is kindest.'),
              ('Field commander', 'War-captain Jessup Calloway-Ross'), ('Dress', 'Layered cloth over sheet metal; goggles are family heirlooms'),
              ('Plays like', 'Fold what you can, build the rest cheap enough to leave. Fight where the wind and the wrecks are yours.')],
        symbols=None),
    'expedition': dict(
        kicker='Faction four · The Second Relief Fleet', quote='Return with the tide.', quote_src='the opening line of every fleet telex',
        rows=[('Founded', 'The Second Relief Fleet sailed in the spring of 2000'), ('Seat', 'The Tidewater Concordat of surviving ministries'),
              ('Field commander', 'Fleet Admiral Corvin Aldersey'), ('Colours', 'Deep navy, lagoon white, polished copper, signal orange'),
              ('Plays like', 'Carefully, expensively and well. Fight at range, break off early, mend what you can recover.')],
        symbols='faction_exp_symbols'),
}


# ======================================================================================================================
# emblems (original SVG, saved to img/part3_<faction>_emblem.svg)
# ======================================================================================================================
def emblem_svg(f):
    a, s = FACTIONS[f]['accent'], FACTIONS[f]['second']
    head = '<svg class="p3-emblem" viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">'
    if f == 'authority':
        teeth = ''.join(f'<rect x="45" y="6" width="10" height="13" transform="rotate({k} 50 50)"/>' for k in range(0, 360, 30))
        body = (f'<g fill="none" stroke="{INK}" stroke-width="3"><circle cx="50" cy="50" r="31" fill="{a}"/>'
                f'<g fill="{a}" stroke="{INK}" stroke-width="2.5">{teeth}</g>'
                f'<circle cx="50" cy="50" r="31" fill="{a}" stroke="{INK}"/><circle cx="50" cy="50" r="21" fill="{PAPER}"/>'
                f'<polygon points="50,31 54,50 50,69 46,50" fill="{s}" stroke="{INK}" stroke-width="1.6"/>'
                f'<polygon points="31,50 50,46 69,50 50,54" fill="{INK}" stroke="{INK}" stroke-width="1.2"/>'
                f'<circle cx="50" cy="50" r="2.6" fill="{PAPER}" stroke="{INK}" stroke-width="1.2"/></g>')
    elif f == 'directorate':
        # a shaft sunk through concentric levels: rings, descending bars, a lit hatch at the bottom
        notch = ''.join(f'<rect x="46" y="4" width="8" height="9" transform="rotate({k} 50 50)"/>' for k in range(0, 360, 45))
        body = (f'<g stroke="{INK}" stroke-width="2.5"><g fill="{a}">{notch}</g>'
                f'<circle cx="50" cy="50" r="38" fill="{a}"/><circle cx="50" cy="50" r="30" fill="{PAPER}"/>'
                f'<rect x="30" y="29" width="40" height="7" fill="{INK}"/><rect x="35" y="41" width="30" height="7" fill="{INK}"/>'
                f'<rect x="40" y="53" width="20" height="7" fill="{INK}"/>'
                f'<polygon points="44,64 56,64 50,74" fill="{a}"/></g>')
    elif f == 'left_behind':
        # a wheel (the rig) with a flag of names on a mast
        spokes = ''.join(f'<line x1="50" y1="64" x2="50" y2="86" transform="rotate({k} 50 75)"/>' for k in range(0, 360, 45))
        body = (f'<g stroke="{INK}" stroke-width="2.5"><circle cx="50" cy="75" r="21" fill="{PAPER}"/>'
                f'<g stroke="{s}" stroke-width="3">{spokes}</g><circle cx="50" cy="75" r="21" fill="none"/>'
                f'<circle cx="50" cy="75" r="5" fill="{a}"/>'
                f'<line x1="50" y1="54" x2="50" y2="8" stroke-width="3"/>'
                f'<polygon points="50,10 88,22 50,36" fill="{a}"/>'
                f'<line x1="56" y1="19" x2="76" y2="23" stroke="{PAPER}" stroke-width="1.6"/><line x1="56" y1="25" x2="70" y2="27.5" stroke="{PAPER}" stroke-width="1.6"/></g>')
    else:  # expedition: ring of eight stars, anchor crossed by a compass needle
        import math
        stars = ''
        for k in range(8):
            ang = math.radians(k * 45 - 90); cx, cy = 50 + 41 * math.cos(ang), 50 + 41 * math.sin(ang)
            pts = ' '.join(f'{cx + (5 if i % 2 == 0 else 2) * math.cos(math.radians(i * 36 - 90)):.1f},{cy + (5 if i % 2 == 0 else 2) * math.sin(math.radians(i * 36 - 90)):.1f}' for i in range(10))
            stars += f'<polygon points="{pts}" fill="{s}" stroke="{INK}" stroke-width="0.8"/>'
        body = (f'<circle cx="50" cy="50" r="33" fill="{a}" stroke="{INK}" stroke-width="3"/>{stars}'
                f'<g stroke="{PAPER}" stroke-width="3.4" fill="none" stroke-linecap="round"><line x1="50" y1="26" x2="50" y2="70"/>'
                f'<line x1="41" y1="35" x2="59" y2="35"/><path d="M33,56 Q36,72 50,72 Q64,72 67,56"/><circle cx="50" cy="24" r="3"/></g>'
                f'<polygon points="50,27 54,50 50,73 46,50" fill="{s}" stroke="{INK}" stroke-width="1.4" transform="rotate(45 50 50)"/>'
                f'<circle cx="50" cy="50" r="2.4" fill="{PAPER}" stroke="{INK}" stroke-width="1"/>')
    return head + body + '</svg>'


def write_emblems():
    for f in FACTION_ORDER:
        (IMG / f'part3_{f}_emblem.svg').write_text(emblem_svg(f))


# ======================================================================================================================
# tech trees (graphviz)
# ======================================================================================================================
def tree_model(f):
    tt = techtree(f); n = tt['nodes']
    prov = {}                                   # pseudo id (tier2 / tier3 / conyard) -> providing building
    for k, v in rules()['buildings'].items():
        for p in v.get('provides', []):
            if p != k: prov[p] = k
    blds = [k for k, v in n.items() if v['kind'] == 'building' and v.get('obtainable', True)]
    ups = [k for k, v in n.items() if v['kind'] == 'upgrade' and v.get('obtainable', True)]
    units = [k for k, v in n.items() if v['kind'] in ('infantry', 'vehicle') and v.get('obtainable', True)]
    kids = {b: [] for b in blds}
    load = {b: 0 for b in blds}                 # how many units each building can build
    for u in units:
        for p in n[u]['producer']:
            if p in load: load[p] += 1
    for u in units:
        pr = [p for p in n[u]['producer'] if p in kids]
        if not pr:
            continue
        pr.sort(key=lambda b: (load[b], n[b]['tier'], n[b]['cost']))
        kids[pr[0]].append(u)                   # listed under the most specialised building that can build it
    req = {b: sorted({prov.get(r, r) for r in n[b]['requires'] if prov.get(r, r) in n and prov.get(r, r) != b}) for b in blds}
    return tt, n, blds, ups, kids, req, prov


LEFT = {
    'authority': ['auth_conyard', 'auth_power', 'auth_refinery', 'auth_silo', 'auth_factory', 'auth_engineering', 'auth_airfield',
                  'auth_signals_hq', 'auth_atomic_battery', 'auth_gate', 'auth_wall'],
    'directorate': ['dir_shaft', 'dir_reactor_vent', 'dir_smelter_vent', 'dir_cistern', 'dir_assembly_bore', 'dir_isotope_works', 'dir_mooring',
                    'dir_committee_hall', 'dir_sunrise', 'dir_bunk_gallery', 'dir_bunker'],
    'left_behind': ['lb_mother_rig', 'lb_dynamo_rig', 'lb_garage_rig', 'lb_refinery_rig', 'lb_yard', 'lb_still_rig', 'lb_lookout',
                    'lb_bench', 'lb_rotor_pad', 'lb_second_drift'],
    'expedition': ['exp_landing_depot', 'exp_pile_house', 'exp_provision_store', 'exp_beacon', 'exp_dredge_works', 'exp_tank_farm', 'exp_hover_yard',
                   'exp_plotting_room', 'exp_core_hall', 'exp_salvo_array', 'exp_flight_deck'],
}


def box(f, b, n, kids, ghost=False):
    t = lambda i: html.escape(title(i), quote=False)
    v = n[b]; c = '#f3eedf' if ghost else TIER_COL[v.get('tier', 1)]
    rows = ''
    if not ghost:
        for u in sorted(kids[b], key=lambda u: (n[u]['tier'], n[u]['cost'])):
            bg = '#f7f2e4' if n[u]['tier'] == 1 else TIER_COL[n[u]['tier']]
            rows += (f'<TR><TD ALIGN="LEFT" BGCOLOR="{bg}"><FONT POINT-SIZE="9">{t(u)}</FONT></TD>'
                     f'<TD ALIGN="RIGHT" BGCOLOR="{bg}"><FONT POINT-SIZE="8">{n[u]["cost"]}</FONT></TD></TR>')
    hdr = f'<I>{t(b)} (see opposite)</I>' if ghost else f'<B>{t(b).upper()}</B>'
    cost = ' ' if ghost else str(v['cost'])
    return (f'"{b}" [label=<<TABLE BORDER="1" CELLBORDER="0" CELLSPACING="0" CELLPADDING="3" COLOR="{"#8a8270" if ghost else INK}" '
            f'BGCOLOR="{c}" STYLE="{"dashed" if ghost else ""}"><TR><TD ALIGN="LEFT"><FONT POINT-SIZE="10">{hdr}</FONT></TD>'
            f'<TD ALIGN="RIGHT"><FONT POINT-SIZE="8">{cost}</FONT></TD></TR>{rows}</TABLE>>];')


def svg_size(path):
    m = re.search(r'viewBox="[\d.\-]+ [\d.\-]+ ([\d.]+) ([\d.]+)"', path.read_text())
    return float(m.group(1)), float(m.group(2))


def graph(f, members, ghosts, name, n, kids, req, box_w, box_h):
    """Render the better of TB / LR for the target box; returns the svg path."""
    best = None
    for rd in ('TB', 'LR'):
        L = ['digraph T {', f'rankdir={rd}; nodesep=0.22; ranksep=0.34; bgcolor="transparent";',
             f'node [shape=plain, fontname="{TREE_FONT}"]; edge [color="#4d4a40", arrowsize=0.55, penwidth=1.1];']
        L += [box(f, b, n, kids) for b in members] + [box(f, g, n, kids, True) for g in ghosts]
        allm = set(members) | set(ghosts)
        for b in members:
            for s in req[b]:
                if s in allm: L.append(f'"{s}" -> "{b}";')
        L.append('}')
        dot = IMG / f'part3_{f}_{name}_{rd}.dot'
        dot.write_text('\n'.join(L))
        out = IMG / f'part3_{f}_{name}_{rd}.svg'
        subprocess.run(['dot', '-Tsvg', '-o', str(out), str(dot)], check=True)
        w, h = svg_size(out)
        sc = min(box_w / w, box_h / h)
        if best is None or sc > best[0]: best = (sc, rd, out, dot)
    sc, rd, out, dot = best
    final = IMG / f'part3_{f}_{name}.svg'
    final.write_text(out.read_text())
    for rd2 in ('TB', 'LR'):
        for ext in ('svg', 'dot'): (IMG / f'part3_{f}_{name}_{rd2}.{ext}').unlink()
    return final, sc


def hex_svg(f, n, ups, prov):
    """Research hexagons by tier: one column per tier (graphviz)."""
    cols = {1: [], 2: [], 3: []}
    for u in sorted(ups, key=lambda u: (n[u]['tier'], n[u]['cost'], title(u))):
        cols[n[u]['tier']].append(u)
    L = ['digraph H {', 'rankdir=TB; nodesep=0.07; ranksep=0.12; bgcolor="transparent";',
         f'node [shape=hexagon, style=filled, fontname="{TREE_FONT}", color="{INK}", penwidth=1.1, margin="0.02,0.02", width=1.0, height=0.46];',
         'edge [style=invis];']
    for t, us in cols.items():
        for u in us:
            nm = 'Classified upgrade' if u.endswith('order_seven') else title(u)
            L.append(f'"{u}" [label=<{html.escape(nm, quote=False)}<BR/><FONT POINT-SIZE="7">{n[u]["cost"]} cr</FONT>>, fillcolor="{TIER_COL[t]}", fontsize=8.5];')
        if len(us) > 1: L.append('{rank=same; ' + '; '.join(f'"{u}"' for u in us) + '}')
        for x, y in zip(us, us[1:]): L.append(f'"{x}" -> "{y}";')
    firsts = [us[0] for us in cols.values() if us]
    for a_, b_ in zip(firsts, firsts[1:]): L.append(f'"{a_}" -> "{b_}";')
    L.append('}')
    dot = IMG / f'part3_{f}_research.dot'; dot.write_text('\n'.join(L))
    out = IMG / f'part3_{f}_research.svg'
    subprocess.run(['dot', '-Tsvg', '-o', str(out), str(dot)], check=True)
    dot.unlink()
    return out


def build_tree_assets(f):
    tt, n, blds, ups, kids, req, prov = tree_model(f)
    left = [b for b in LEFT[f] if b in blds]
    right = [b for b in blds if b not in left]
    for b in left:
        assert not any(r in right for r in req[b]), (f, b, 'left needs a right building', req[b])
    ghosts = sorted({r for b in right for r in req[b] if r in left})
    lsvg, lsc = graph(f, left, [], 'tree_left', n, kids, req, 4.4 * 96, 5.5 * 96)
    rsvg, rsc = graph(f, right, ghosts, 'tree_right', n, kids, req, 4.4 * 96, 3.0 * 96)
    hsvg = hex_svg(f, n, ups, prov)
    return dict(tt=tt, n=n, ups=ups, kids=kids, req=req, prov=prov, left=left, right=right, lsvg=lsvg, rsvg=rsvg, hsvg=hsvg, ghosts=ghosts)


# ======================================================================================================================
# page builders
# ======================================================================================================================
CSS = """<style>
.p3-emblem { width:1.5in; height:1.5in; display:block; margin:.1in auto .12in; }
.p3-emblem-sm { width:.85in; height:.85in; float:right; margin:-.05in 0 .05in .12in; }
.p3-quote { font-style:italic; font-size:8.4pt; margin:0 0 4pt; }
.p3-glance { width:100%; border-collapse:collapse; font-family:'TeX Gyre Heros Cn',sans-serif; font-size:8pt; margin:4pt 0 6pt; }
.p3-glance td { border-bottom:.6pt solid #a99d7f; padding:1.6pt 2pt; vertical-align:top; }
.p3-glance td:first-child { text-transform:uppercase; letter-spacing:.06em; color:var(--muted); width:1.15in; }
.p3-glance td:last-child { font-weight:700; }
.p3-soc p { font-size:8.2pt; line-height:1.28; margin-bottom:4pt; }
.p3-soc h3 { margin:6pt 0 1pt; }
.p3-soc .sub { font-style:italic; color:var(--muted); font-size:7.6pt; margin:0 0 2pt; }
.p3-leaders { border:1.2pt solid var(--rule); background:var(--tint); padding:4pt 7pt 2pt; margin-top:6pt; }
.p3-leaders h3 { margin:0 0 2pt; }
.p3-leaders .l { font-size:7.8pt; line-height:1.25; margin:0 0 3.5pt; text-align:left; }
.p3-leaders .l b { font-family:'TeX Gyre Heros Cn',sans-serif; text-transform:uppercase; letter-spacing:.04em; }
.p3-notes ul { margin:2pt 0 4pt 12pt; padding:0; } .p3-notes li { font-size:8.2pt; line-height:1.28; margin-bottom:3pt; }
.p3-notes .stat { font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; }
.p3-treebox { display:flex; justify-content:center; align-items:flex-start; }
.p3-treebox img { max-width:100%; max-height:100%; }
.p3-legend { display:flex; gap:9pt; align-items:center; font-family:'TeX Gyre Heros Cn',sans-serif; font-size:7pt; margin-top:4pt; }
.p3-legend span { display:inline-flex; align-items:center; gap:3pt; }
.p3-legend i { width:12pt; height:8pt; border:1pt solid var(--rule); display:inline-block; }
.p3-res { width:100%; border-collapse:collapse; font-family:'TeX Gyre Heros Cn',sans-serif; font-size:7.4pt; }
.p3-res th { text-align:left; border-bottom:1pt solid var(--rule); font-size:6.8pt; letter-spacing:.06em; text-transform:uppercase; padding:1pt 2pt; }
.p3-res th.r { text-align:right; }
.p3-res td { padding:.8pt 2pt; border-bottom:.5pt solid #b9ad8f; } .p3-res td.r { text-align:right; }
.p3-parts { display:grid; grid-template-columns:1fr; gap:7pt; margin-top:8pt; }
.p3-part { display:grid; grid-template-columns:1.55in 1fr; gap:8pt; border-top:2pt solid var(--rule); padding-top:4pt; }
.p3-part img.s { width:1.55in; height:.95in; object-fit:cover; border:1pt solid var(--rule); display:block; }
.p3-part h3 { margin:0 0 2pt; font-size:9.5pt; } .p3-part p { font-size:7.8pt; margin:0 0 2pt; text-align:left; line-height:1.25; }
</style>"""


def blocks_html(items):
    out = ''
    for kind, ttl, sub, text in items:
        if kind == 'arch':
            out += f'<h3>{E(ttl)}</h3>' + (f'<div class="sub">{E(sub)}</div>' if sub else '') + text
        else:
            out += f'<h3>{E(ttl)}</h3>' + (f'<div class="sub">{E(sub)}</div>' if sub else '') + text
    return out


def society_pages(f):
    items = []
    for uid in SOCIETY[f]:
        e = arch(uid)
        items.append(('arch', e['title'], e['subtitle'], paras(e['body'])))
    for ttl, aside, text in lore_entries(f):
        items.append(('lore', ttl, (aside.strip('()')[:1].upper() + aside.strip('()')[1:]) if aside else '', paras(text)))
    k = SPLIT[f]
    lead = ''
    for uid in LEADERS[f]:
        e = arch(uid)
        mot = re.search(r'Motivation[^:]*:\s*(.*)', e['body'])
        if mot:
            txt = ' '.join(mot.group(0).split())
        else:                                   # Left Behind: the Archive text, first two sentences only
            txt = ' '.join(arch_para(uid, 1)[0].split()); txt = ' '.join(re.split(r'(?<=[.!?])\s+', txt)[:2])
        lead += f'<p class="l"><b>{E(e["title"])}</b> · {E(e["subtitle"])}. {rich(txt)}</p>'
    if f == 'left_behind':                      # the Speaker has no leader entry: first paragraph of "The Speakers", verbatim
        t = ' '.join(arch_para('faction_lb_leaders', 1)[0].split())
        lead = f'<p class="l">{rich(t)}</p>' + lead
    sp = FACTIONS[f]['short']
    p1 = (f'<div class="kicker">{E(sp)} · Society</div><h2>{E(FACTIONS[f]["name"])}</h2>' + '<div class="p3-soc">' + blocks_html(items[:k]) + '</div>')
    p2 = ('<div class="p3-soc">' + blocks_html(items[k:]) + f'<div class="p3-leaders"><h3>Leaders</h3>{lead}</div></div>')
    return [Page(CSS + p1, runhead=f'{sp} · Society and Leaders', draft=False, faction=f),
            Page(p2, runhead=f'{sp} · Society and Leaders', faction=f)]


def opener_pages(f, idx):
    fa = FACTIONS[f]; d = FACT[f]
    dl = ''.join(f'<dt>{E(a)}</dt><dd>{E(b)}</dd>' for a, b in d['rows'])
    name = fa['name']
    words = name.split(' ')
    mid = len(words) // 2 if f != 'authority' else 2
    h1 = E(' '.join(words[:mid])) + '<br>' + E(' '.join(words[mid:]))
    p1 = (f'<div class="bleedart" style="background-image:url({img("data/images/stills/still_%s_act2.png" % f, max_px=1400, jpeg=True)})"></div>'
          f'<div class="opener-title"><div class="kicker">{E(d["kicker"])}</div><h1>{h1}</h1><div class="bar"></div>'
          f'<p class="p3-quote">&ldquo;{E(d["quote"])}&rdquo; <span style="font-style:normal">&mdash; {E(d["quote_src"])}.</span></p>'
          f'<div class="factbox"><dl>{dl}</dl></div></div>')
    # right page: emblem, marks (Archive, where it exists), and the start-of-game facts from the data
    tt, n, blds, ups, kids, req, prov = tree_model(f)
    r = rules()
    t2, t3 = prov[tt['tier2']], prov[tt['tier3']]
    start = ', '.join(title(b) for b in tt['start_buildings'])
    su = {}
    for u in tt['start_units']: su[u] = su.get(u, 0) + 1
    units = ', '.join((f'{c} × ' if c > 1 else '') + title(u) for u, c in su.items()) or 'None'
    glance = [('Starts with', start + (' and ' + units if su else '')), ('Tier 2 opens with', f'{title(t2)} ({n[t2]["cost"]} cr)'),
              ('Tier 3 opens with', f'{title(t3)} ({n[t3]["cost"]} cr)'),
              ('Buildings, units', f'{len(blds)} buildings, {sum(len(v) for v in kids.values())} units, {len(ups)} upgrades')]
    gl = ''.join(f'<tr><td>{E(a)}</td><td>{E(b)}</td></tr>' for a, b in glance)
    sym = ''
    for uid in OPENER_ARCH[f]:
        e = arch(uid); sym += f'<h3>{E(e["title"])}</h3><div class="sub">{E(e["subtitle"])}</div>' + paras(e['body'])
    p2 = (f'<img class="p3-emblem" style="width:1.25in;height:1.25in" src="{img(IMG / ("part3_%s_emblem.svg" % f))}">'
          f'<div class="kicker" style="text-align:center">{E(fa["short"])} · At a glance</div>'
          f'<div class="bar" style="margin-top:3pt"></div><table class="p3-glance">{gl}</table>'
          f'<div class="p3-soc">{sym}</div>')
    return [Page(CSS + p1, runhead='Part III · The Factions', toc=name, left=True, faction=f),
            Page(p2, runhead='Part III · The Factions', faction=f)]


def notes_page(f, A):
    """Commander's notes: new text (draft), every number read from the data."""
    n, tt, req, prov = A['n'], A['tt'], A['req'], A['prov']
    r = rules()['buildings']
    T = title
    def cr(b): return f'{n[b]["cost"]} credits'
    def names(bs): return ' and '.join(T(b) for b in bs)
    t2, t3 = prov[tt['tier2']], prov[tt['tier3']]
    bl = [k for k, v in n.items() if v['kind'] == 'building']
    power = [b for b in bl if r[b]['power'] > 0]
    pw = power[0]
    heavy = min(bl, key=lambda b: r[b]['power'])
    fact = [b for b in bl if r[b]['produces'] == 'vehicle' and n[b]['tier'] == 1]
    fact.sort(key=lambda b: n[b]['cost'])
    inf = [b for b in bl if r[b]['produces'] == 'infantry'][0]
    free = [b for b in bl if r[b]['free_unit']]
    stor = sorted((b for b in bl if r[b]['storage']), key=lambda b: -r[b]['storage'])
    li = []
    li.append(f'<b>Power.</b> The <b>{T(pw)}</b> costs <span class="stat">{n[pw]["cost"]}</span> and gives <span class="stat">{r[pw]["power"]}</span> power. '
              f'Heavy buildings draw a lot: the {T(heavy)} alone uses <span class="stat">{abs(r[heavy]["power"])}</span>. Add generators before you add them.')
    if free:
        fb = free[0]
        li.append(f'<b>Economy.</b> The <b>{T(fb)}</b> costs <span class="stat">{n[fb]["cost"]}</span> and comes with a free <b>{T(r[fb]["free_unit"])}</b>. '
                  f'It stores {r[fb]["storage"]} credits. The largest store you can build is the {T(stor[0])} ({r[stor[0]]["storage"]}).')
    li.append(f'<b>First factory.</b> The <b>{T(fact[0])}</b> ({cr(fact[0])}) needs {names(req[fact[0]]) or "nothing else"}. '
              f'Your infantry building is the <b>{T(inf)}</b> ({cr(inf)}).')
    li.append(f'<b>Tier 2</b> opens when the <b>{T(t2)}</b> ({cr(t2)}) stands; it needs {names(req[t2])}. '
              f'<b>Tier 3</b> opens with the <b>{T(t3)}</b> ({cr(t3)}); it needs {names(req[t3])}. Guard both.')
    li.append(f'<b>Research.</b> Upgrades are listed with their cost on the technology tree pages. Buy the tier 1 upgrades early; they are the cheapest.')
    notes = {
        'authority': lambda: '<b>Style.</b> Build a perimeter and keep the Vehicle Factory busy. Repair your tanks rather than replace them.',
        'directorate': lambda: '<b>Style.</b> Spend conscripts and protect the Command Shaft. A Hatch gives you a position the enemy cannot see coming.',
        'left_behind': lambda: f'<b>Style.</b> You start with a Packed Rig, not a building. Unpack it, then keep your structures cheap: the Scrap Wall costs {n["lb_scrap_wall"]["cost"]} and the Spike Nest {n["lb_spike_nest"]["cost"]}.',
        'expedition': lambda: f'<b>Style.</b> You start with a Command Ship, a Landing Craft and a Hover Skimmer. A Landing Beacon costs {n["exp_beacon"]["cost"]}; light it before you land the guns.',
    }[f]()
    li.append(notes)
    # a tidy opening: the cheapest road to a power grid, infantry, income, a factory and both tier buildings
    order = []
    def need(b):
        for r_ in req[b]: need(r_)
        if b not in order and n[b]['cost'] > 0: order.append(b)
    for b in [pw, inf] + free[:1] + [fact[0], t2, t3]: need(b)
    run = 0; trs = ''
    for i, b in enumerate(order, 1):
        run += n[b]['cost']
        trs += f'<tr><td class="r">{i}</td><td>{E(T(b))}</td><td class="r">{n[b]["cost"]}</td><td class="r">{run}</td></tr>'
    table = ('<h3>A tidy build order</h3><table class="p3-res"><tr><th class="r">Step</th><th>Building</th><th class="r">Credits</th>'
             f'<th class="r">Running total</th></tr>{trs}</table>'
             '<p style="font-size:7.4pt;margin-top:3pt">This is the shortest chain of buildings that reaches both tier buildings. '
             'Defences, extra generators and a second income come on top of it.</p>')
    sp = FACTIONS[f]['short']
    body = (f'<div class="kicker">{E(sp)} · Commander&rsquo;s notes</div><h2>First Hour, {E(sp)}</h2><div class="p3-notes">'
            f'<p style="font-size:8.2pt">Practical advice for a new commander. The costs are the game&rsquo;s own.</p><ul>'
            + ''.join(f'<li>{x}</li>' for x in li) + '</ul>' + table + '</div>')
    return Page(body, runhead=f'{sp} · Notes', draft=True, faction=f)


def tree_pages(f, A):
    n, ups, prov, tt = A['n'], A['ups'], A['prov'], A['tt']
    sp = FACTIONS[f]['short']
    t2, t3 = prov[tt['tier2']], prov[tt['tier3']]
    legend = (f'<div class="p3-legend"><span><i style="background:{TIER_COL[1]}"></i>Tier 1</span>'
              f'<span><i style="background:{TIER_COL[2]}"></i>Tier 2 ({E(title(t2))})</span>'
              f'<span><i style="background:{TIER_COL[3]}"></i>Tier 3 ({E(title(t3))})</span></div>')
    prods = sorted({p for u in ups for p in n[u]['producer']}, key=lambda b: (n[b]['tier'], n[b]['cost']))
    rows = ''
    for u in sorted(ups, key=lambda u: (n[u]['tier'], n[u]['cost'], title(u))):
        nm = 'Classified upgrade' if u.endswith('order_seven') else title(u)
        need = [prov.get(x, x) for x in n[u]['requires']]
        rows += f'<tr><td>{E(nm)}</td><td class="r">T{n[u]["tier"]}</td><td>{E(", ".join(title(x) for x in need) or "none")}</td><td class="r">{n[u]["cost"]}</td></tr>'
    left = (f'<div class="kicker">Part III · Technology</div><h1 style="font-size:19pt">{E(sp)} Technology Tree</h1>'
            f'<p style="font-size:7.8pt;margin-top:4pt">Arrows point from a building to what it opens. Units are listed under the building that '
            f'trains them, with their cost in credits. This page: the economy, production and the tier buildings. Overleaf: the rest, and research.</p>'
            f'<div class="p3-treebox" style="height:5.45in;margin-top:3pt"><img src="{img(A["lsvg"])}"></div>{legend}')
    right = (f'<h2>The Rest of the Tree</h2><p style="font-size:7.6pt;margin-bottom:3pt">Dashed boxes are on the opposite page. '
             f'Some units can be built at more than one building. Each is listed once, at the most specialised.</p>'
             f'<div class="p3-treebox"><img src="{img(A["rsvg"])}" style="max-height:3.1in"></div>'
             f'<h3 style="margin-top:3pt">Research</h3><div class="p3-treebox"><img src="{img(A["hsvg"])}" style="max-width:100%;max-height:1.5in"></div>'
             f'<p style="font-size:7.2pt;margin:2pt 0 2pt">Researched at: {E(", ".join(title(p) for p in prods))}.</p>'
             f'<table class="p3-res"><tr><th>Upgrade</th><th>Tier</th><th>Needs</th><th>Credits</th></tr>{rows}</table>')
    return [Page(CSS + left, runhead='Part III · Technology', left=True, faction=f),
            Page(right, runhead=f'{sp} · Technology Tree', faction=f)]


def part_opener():
    rows = ''
    for i, f in enumerate(FACTION_ORDER):
        fa, d = FACTIONS[f], FACT[f]
        rows += (f'<div class="p3-part"><img class="s" src="{img("data/images/stills/still_%s_act2.png" % f, max_px=500, jpeg=True)}">'
                 f'<div><h3 style="color:{fa["accent_d"]}">{E(fa["name"])}</h3><p><i>{E(d["kicker"].split(" · ")[1])}</i></p>'
                 f'<p>{E(dict(d["rows"])["Plays like"])}</p></div></div>')
    body = (CSS + '<div class="kicker">Part III</div><h1 style="font-size:30pt;margin-top:4pt">The Factions</h1><div class="bar" style="margin-top:6pt"></div>'
            '<p>Four powers fight for the surface. Each has its own people, its own way of building and its own way of fighting. '
            'This part gives each faction the same chapter: an opening spread, its society and leaders, a page of commander&rsquo;s notes, '
            'the field guide to its units and structures, and its technology tree.</p>' + f'<div class="p3-parts">{rows}</div>')
    return Page(body, runhead='Part III', toc='Part III · The Factions', draft=True)


def pages():
    IMG.mkdir(exist_ok=True)
    write_emblems()
    out = [part_opener()]
    for i, f in enumerate(FACTION_ORDER):
        A = build_tree_assets(f)
        out += opener_pages(f, i)
        out += society_pages(f)
        out.append(notes_page(f, A))
        out += faction_entries(f)
        out += tree_pages(f, A)
    return out


if __name__ == '__main__':
    print(len(pages()), 'pages')
