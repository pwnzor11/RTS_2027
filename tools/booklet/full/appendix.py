"""Field Manual appendices (M14-05): counter chart, keyboard reference, glossary, credits and licences.

Every number and key here is read at build time from rules_resolved.json, src/Game/Controls/GameKeys.cs, KeyNames.cs,
docs/CREDITS.md and docs/LICENSING.md. Nothing is typed by hand. The helpers at the top are shared with card.py.
"""
import re
from common import Page, E, ROOT, FACTIONS, FACTION_ORDER, rules, title

ARMOUR = ['unarmored', 'light', 'heavy', 'structure', 'fortified']
GK_SRC = ROOT / 'src/Game/Controls/GameKeys.cs'
KN_SRC = ROOT / 'src/Game/Controls/KeyNames.cs'
CREDITS_MD = ROOT / 'docs/CREDITS.md'
LICENSING_MD = ROOT / 'docs/LICENSING.md'

INFO_RE = re.compile(r'new\(GameAction\.(\w+),\s*"(\w+)",\s*(\w+),\s*"((?:[^"\\]|\\.)*)",\s*Keys\.(\w+)(?:,\s*KeyContext\.\w+)?\)')
ROW_RE = re.compile(r'A\(GameAction\.(\w+)\)|F\((\w+), "([^"]+)", "([^"]+)"\)')
KEYNAME_RE = re.compile(r'Keys\.(\w+)\s*=>\s*"((?:[^"\\]|\\.)*)"')


# ---- shared helpers (card.py imports these) ------------------------------------------------------------------------------
def shade(v):
    """CSS class for a warhead percent: five bins, darker as the damage grows."""
    if v is None: return 'ap-na'
    if v <= 0: return 'ap-s0'
    if v < 50: return 'ap-s1'
    if v < 100: return 'ap-s2'
    if v == 100: return 'ap-s3'
    return 'ap-s4'


def weapon_rows():
    """One row per distinct main weapon (a unit's primary_weapon), factions in FACTION_ORDER.
    Returns [(faction, unit_id, weapon_id)], unit_id being the first unit that carries the weapon."""
    out = []
    units = rules()['units']
    for f in FACTION_ORDER:
        seen = set()
        for uid, v in units.items():
            pw = v.get('primary_weapon')
            if v.get('faction') != f or not pw or pw in seen: continue
            seen.add(pw)
            out.append((f, uid, pw))
    return out


def versus_of(weapon_id):
    """The armour-class percent table of a weapon's warhead (dict: armour class -> percent)."""
    r = rules()
    return r['warheads'][r['weapons'][weapon_id]['warhead']]['versus']


def key_label(name):
    """On-screen name of a Keys member, read by the rules KeyNames.cs applies (D1 -> 1, NumPad7 -> Num 7, OemPeriod -> .)."""
    m = re.fullmatch(r'D(\d)', name)
    if m: return m.group(1)
    m = re.fullmatch(r'NumPad(\d)', name)
    if m: return 'Num ' + m.group(1)
    return KEYNAME_MAP.get(name, name)


def _keyname_map():
    return {k: v for k, v in KEYNAME_RE.findall(KN_SRC.read_text(encoding='utf-8'))}


KEYNAME_MAP = _keyname_map()


def keys_table():
    """(rows, info): rows = [(group, key, description)] in the Controls page order, with the System group split into
    'Production' (sidebar tabs and cameos) and 'Other'. info = {action name: {id, group, key, desc}}."""
    src = GK_SRC.read_text(encoding='utf-8')
    names = dict(re.findall(r'public const string (\w+) = "([^"]+)";', src))
    info = {}
    for act, aid, grp, desc, key in INFO_RE.findall(src):
        info[act] = dict(id=aid, group=names[grp], desc=desc, key=key_label(key))
    body = src[src.index('private static List<ControlRow> BuildRows()'):]
    rows = []
    for m in ROW_RE.finditer(body):
        if m.group(1):
            i = info[m.group(1)]
            rows.append((i['group'], i['key'], i['desc'], i['id']))
        else:
            rows.append((names[m.group(2)], m.group(3), m.group(4), None))
    split = []
    for grp, key, desc, aid in rows:
        if grp == 'Build & System':
            grp = 'Production' if aid and aid.startswith(('tab_', 'cameo_')) else 'Other'
        split.append((grp, key, desc, aid))
    return split, info


def _credits():
    """Library rows (name, short licence) and the counts of the other tables in docs/CREDITS.md."""
    text = CREDITS_MD.read_text(encoding='utf-8')
    sections = re.split(r'^## ', text, flags=re.M)
    libs, counts = [], {}
    for sec in sections:
        head, _, body = sec.partition('\n')
        rows = [l for l in body.splitlines() if l.startswith('|') and not re.match(r'^\|\s*-', l)]
        rows = rows[1:] if rows else rows     # first row is the table header
        if head.strip() == 'Libraries':
            for l in rows:
                cells = [c.strip() for c in l.strip('|').split('|')]
                name = cells[0].replace('`', '').split(' (')[0].strip()
                lic = cells[1].split(' (')[0].split(';')[0].strip()
                libs.append((name, lic))
        elif head.strip():
            counts[head.strip()] = len(rows)
    return libs, counts


def _licence_lines():
    """The GPL statement and the MonoGame linking permission, cleaned of markdown, from docs/LICENSING.md."""
    text = LICENSING_MD.read_text(encoding='utf-8')
    def clean(s): return re.sub(r'\s+', ' ', s.replace('**', '').replace('`', '')).strip()
    our = clean(text.split('## Our license', 1)[1].split('## ', 1)[0])
    # Only the licence sentence and the owner's intent: never the reference-source section further down.
    licence = next(s for s in our.split('. ') if s.startswith('RTS_2027 is licensed under'))
    intent = next(s for s in our.split('. ') if 'open source forever' in s)
    link_sec = clean(text.split('## Linking exception for MonoGame (D-055)', 1)[1].split('## ', 1)[0])
    link = next(s for s in link_sec.split('. ') if 'additional permission' in s)
    return licence.rstrip('.') + '. ' + intent.rstrip('.') + '.', link.rstrip('.') + '.'


# ---- page 1: counter chart -----------------------------------------------------------------------------------------------
CSS = """
.ap-ct { width:100%; border-collapse:collapse; font-size:6.2pt; line-height:1.1; margin-top:4pt; }
.ap-ct th { font-family:'TeX Gyre Heros Cn',sans-serif; font-size:5.8pt; letter-spacing:.06em; text-transform:uppercase;
            text-align:center; border-bottom:1pt solid var(--rule); padding:1pt 1pt; }
.ap-ct th:first-child { text-align:left; }
.ap-ct td { padding:.5pt 2pt; border-bottom:.3pt solid #c9bd9c; text-align:center; font-variant-numeric: tabular-nums; }
.ap-ct td.ap-lbl { text-align:left; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; max-width:1.9in; }
.ap-ct tr.ap-fac td { text-align:left; font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; text-transform:uppercase;
                      letter-spacing:.08em; font-size:5.8pt; color:var(--olive-d); padding-top:2.5pt; border-bottom:.6pt solid var(--olive-d); }
.ap-s0 { background:#f4efe0; } .ap-s1 { background:#e2d6b6; } .ap-s2 { background:#c4b27a; }
.ap-s3 { background:#8b9a5c; color:#fff; } .ap-s4 { background:#d99a1e; color:#1e1b16; } .ap-na { background:#fff; color:#888; }
.ap-legend { display:flex; gap:3pt; align-items:center; font-size:6pt; margin:4pt 0 0; }
.ap-legend span { display:inline-block; width:.42in; text-align:center; padding:.5pt 0; border:.4pt solid #6b644f; }
.ap-kb { column-count:2; column-gap:.16in; column-rule:.5pt solid #b9ad8f; }
.ap-kb { font-size:5.7pt; }
.ap-kb h3 { break-after:avoid; margin:3pt 0 .5pt; font-size:6.8pt; }
.ap-kb h3:first-child { margin-top:0; }
.ap-kr { display:flex; gap:2pt; break-inside:avoid; font-size:5.7pt; line-height:1.08; margin:0 0 .4pt; }
.ap-kk { flex:0 0 .6in; font-family:'Courier 10 Pitch','Nimbus Mono PS',monospace; font-weight:700; color:var(--olive-d); }
.ap-kd { flex:1 1 auto; }
.ap-gl p { font-size:7.2pt; line-height:1.22; margin:0 0 3pt; break-inside:avoid; text-align:left; }
.ap-gl b { font-family:'TeX Gyre Heros Cn',sans-serif; text-transform:uppercase; letter-spacing:.04em; color:var(--olive-d); }
.ap-cr h3 { margin-top:6pt; }
.ap-cr ul { margin:0 0 4pt 0; padding-left:10pt; font-size:7pt; line-height:1.2; }
.ap-cr li { margin:0 0 .5pt; }
"""


def counter_html():
    rows = weapon_rows()
    head = ''.join(f'<th>{c.capitalize()}</th>' for c in ARMOUR)
    out = [f'<table class="ap-ct"><thead><tr><th>Weapon carried by</th>{head}</tr></thead><tbody>']
    cur = None
    for f, uid, pw in rows:
        if f != cur:
            cur = f
            out.append(f'<tr class="ap-fac"><td colspan="{len(ARMOUR) + 1}">{E(FACTIONS[f]["name"])}</td></tr>')
        vs = versus_of(pw)
        cells = ''.join(f'<td class="{shade(vs.get(c))}">{vs.get(c, "")}</td>' for c in ARMOUR)
        out.append(f'<tr><td class="ap-lbl">{E(title(uid))}</td>{cells}</tr>')
    out.append('</tbody></table>')
    legend = ('<div class="ap-legend"><span class="ap-s0">0</span><span class="ap-s1">1 to 49</span>'
              '<span class="ap-s2">50 to 99</span><span class="ap-s3">100</span><span class="ap-s4">over 100</span></div>')
    return ''.join(out) + legend


def counter_page():
    body = (f'<style>{CSS}</style><h2>Counter chart</h2>'
            f'<p>Percent of a warhead&rsquo;s damage that reaches each armour class.</p>{counter_html()}')
    return Page(body, runhead='Appendices', toc='Appendices', draft=True)


# ---- page 2: keyboard reference -------------------------------------------------------------------------------------------
def keyboard_page():
    rows, _ = keys_table()
    order = ['Camera', 'Selection', 'Orders', 'Production', 'Other']
    groups = {g: [] for g in order}
    for grp, key, desc, aid in rows:
        groups[grp].append((key, desc))
    parts = []
    for g in order:
        parts.append(f'<h3>{E(g)}</h3>')
        for key, desc in groups[g]:
            parts.append(f'<div class="ap-kr"><span class="ap-kk">{E(key)}</span><span class="ap-kd">{E(desc)}</span></div>')
    body = (f'<h2>Keyboard reference</h2><p>Default keys. Most of them can be rebound on the Controls page.</p>'
            f'<div class="ap-kb">{"".join(parts)}</div>')
    return Page(body, runhead='Appendices', draft=True)


# ---- page 3: glossary -----------------------------------------------------------------------------------------------------
GLOSSARY = [
    ('Ashfall', 'Radioactive ash that still falls from the dust belt. It is scraped up and refined, and it harms unprotected infantry.'),
    ('Armour class', 'The kind of armour a unit or structure carries. A warhead hits each class by a different percent.'),
    ('Attack-move', 'An order to march towards a spot and fire at anything met on the way.'),
    ('Construction Yard', 'The main base building. Your first structure, and the one every base grows from.'),
    ('Control group', 'Units stored under a number key from 1 to 9, so you can select the same units again at once.'),
    ('Cameo', 'One build icon in the sidebar. Shift and the cameo key queues five of that item.'),
    ('Guard', 'An order to hold a spot and defend it.'),
    ('Harvester', 'A vehicle that gathers ashfall and carries it back to a refinery.'),
    ('Mission', 'A single battle in a campaign, with its own map and objectives.'),
    ('Objective', 'A goal in a mission. The objective list shows what is still left to do.'),
    ('Pitchblende', 'The rare, rich tier of ashfall. Pitchblende pits yield far more than ordinary ground.'),
    ('Power Plant', 'A building that supplies the power your base runs on.'),
    ('Prerequisite', 'A building or upgrade that must already exist before another item can be built.'),
    ('Quicksave', 'A save made with one key. Available in skirmish and in missions.'),
    ('Rally point', 'The place where new units from a factory gather before you give them orders.'),
    ('Refinery', 'A building that processes harvested ashfall into fissile concentrate.'),
    ('Sidebar', 'The panel where you pick what to build, in tabs for structures, defences, infantry, vehicles, upgrades and aircraft.'),
    ('Skirmish', 'A game against computer opponents on a map you choose.'),
    ('Spectator', 'Watching a game between computer players without taking part.'),
    ('Stance', 'How a unit acts when left to itself: aggressive, defensive or hold fire.'),
    ('Supply cap', 'The limit on how many units a side can field. Each unit uses up part of it.'),
    ('Tier', 'A stage of technology. Higher tiers unlock stronger buildings and units.'),
    ('Veterancy', 'Units that win kills gain rank, in three ranks, and fight better as they rise.'),
    ('Warhead', 'The part of a weapon that does the damage, and how much of that damage reaches each armour class.'),
    ('Waypoint', 'A queued stop for a selection. Shift and right click add one, and any plain order clears the queue.'),
]


def glossary_page():
    items = ''.join(f'<p><b>{E(t)}.</b> {E(d)}</p>' for t, d in sorted(GLOSSARY, key=lambda x: x[0].lower()))
    body = f'<style>{CSS}</style><h2>Glossary</h2><div class="cols2 ap-gl">{items}</div>'
    return Page(body, runhead='Appendices', draft=True)


# ---- page 4: credits and licences ----------------------------------------------------------------------------------------
def credits_page():
    libs, counts = _credits()
    licence, link = _licence_lines()
    lib_items = ''.join(f'<li>{E(n)}: {E(l)}</li>' for n, l in libs)
    imported = counts.get('Imported assets', 0)
    audio = counts.get('Audio', 0)
    art = counts.get('AI-assisted artwork', 0)
    body = (f'<style>{CSS}</style><h2>Credits and licences</h2>'
            f'<div class="ap-cr"><h3>Licence</h3><p>{E(licence)}</p>'
            f'<p>{E(link)}</p>'
            f'<h3>Libraries</h3><ul>{lib_items}</ul>'
            f'<h3>Assets and artwork</h3>'
            f'<p>The credits file lists {imported} imported asset groups, {audio} audio sources and {art} AI-assisted artwork '
            f'entries, each with its source and licence. Full notices and licence texts ship with every copy of the game.</p></div>')
    return Page(body, runhead='Appendices', draft=True)


def pages():
    return [counter_page(), keyboard_page(), glossary_page(), credits_page()]
