"""Part II: Getting Started (M14-05b). 12 pages.

Sources: keys are parsed from src/Game/Controls/GameKeys.cs + KeyNames.cs at build time (never typed by hand); menu names from
src/Game/Menu/MainMenuModel.cs and the skirmish / LAN screens; numbers from data/rules/game.toml, buildings.toml, data/ai/difficulty.toml
and rules_resolved.json; guide wording from src/Game/Menu/HowToPlayContent.cs (re-read at build time). Own wording sits on draft pages.
"""
import pathlib, re, sys, tomllib
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import Page, ROOT, E, rules, title

RH = 'Part II · Getting Started'
TICKS = 20  # game.toml [game] tick_rate

# ---- data readers --------------------------------------------------------------------------------------------------------------
GAME = tomllib.load(open(ROOT / 'data/rules/game.toml', 'rb'))
BUILD = tomllib.load(open(ROOT / 'data/rules/buildings.toml', 'rb'))
DIFF = tomllib.load(open(ROOT / 'data/ai/difficulty.toml', 'rb'))
ECO, VET, SUP = GAME['economy'], GAME['veterancy'], GAME['supply']
assert GAME['game']['tick_rate'] == TICKS


def key_name(k):
    """Port of KeyNames.Of for the keys that GameKeys uses."""
    fixed = {'Decimal': 'Num .', 'Add': 'Num +', 'OemPeriod': '.', 'OemMinus': '-', 'OemPlus': '=', 'Space': 'Space'}
    if k in fixed: return fixed[k]
    m = re.fullmatch(r'D(\d)', k)
    if m: return m.group(1)
    m = re.fullmatch(r'NumPad(\d)', k)
    if m: return 'Num ' + m.group(1)
    return k


def load_keys():
    src = (ROOT / 'src/Game/Controls/GameKeys.cs').read_text()
    out = {}
    for m in re.finditer(r'new\(GameAction\.(\w+), "(\w+)", (\w+), "([^"]*)", Keys\.(\w+)', src):
        out[m.group(1)] = key_name(m.group(5))
    return out


KEYS = load_keys()
for need in ('AttackMove', 'Stop', 'Guard', 'Scatter', 'Deploy', 'Pack', 'TabStructures', 'TabAircraft', 'CentreBase', 'Pause', 'QuickSave',
             'QuickLoad', 'SelectAll', 'Group1', 'StanceAggressive', 'IdleHarvester', 'CameoSlot12', 'Bookmark1', 'LastAlert'):
    assert need in KEYS, need


def K(a): return f'<b class="p2-key">{E(KEYS[a])}</b>'


def guide_pages():
    """HowToPlayContent.cs pages: title -> list of lines (approved wording)."""
    src = (ROOT / 'src/Game/Menu/HowToPlayContent.cs').read_text()
    out = {}
    for m in re.finditer(r'new GuidePage\("([^"]+)", GuideArt\.\w+, new\[\]\s*\{(.*?)\n        \}\)', src, re.S):
        out[m.group(1)] = re.findall(r'^\s*"(.*)",\s*$', m.group(2), re.M)
    return out


GUIDE = guide_pages()
assert 'Selecting' in GUIDE and len(GUIDE) == 8, list(GUIDE)


def G(page, i=None):
    """Approved guide lines, escaped, as <li> (all lines, or line i as a plain string)."""
    lines = GUIDE[page]
    return E(lines[i]) if i is not None else ''.join(f'<li>{E(l)}</li>' for l in lines)


def btitle(b): return title(b)


CSS = """<style>
.p2-key{font-family:'TeX Gyre Heros Cn',sans-serif;font-size:7.4pt;border:.8pt solid #2a271f;border-radius:2pt;padding:0 3pt;background:#f6f1e2;white-space:nowrap}
.p2 ul{margin:0 0 5pt 0;padding-left:11pt}.p2 li{margin-bottom:2.5pt;text-align:left}
.p2 table.p2-t{width:100%;border-collapse:collapse;font-size:7.8pt;margin:3pt 0 6pt}
.p2 .p2-t th{font-family:'TeX Gyre Heros Cn',sans-serif;text-transform:uppercase;letter-spacing:.05em;font-size:6.8pt;text-align:left;border-bottom:1.4pt solid #2a271f;padding:1.5pt 3pt;color:#5d5543}
.p2 .p2-t td{border-bottom:.6pt solid #a99d7f;padding:1.8pt 3pt;vertical-align:top}
.p2 .p2-t td.r,.p2 .p2-t th.r{text-align:right}
.p2 .p2-t td.n{font-family:'TeX Gyre Heros Cn',sans-serif;font-weight:700;white-space:nowrap}
.p2 .lede{font-size:9.2pt;font-style:italic;margin-bottom:6pt}
.p2 .rule{border:1.2pt solid var(--rule);background:var(--tint);padding:4pt 7pt;margin:5pt 0;font-size:8.2pt}
.p2 .rule b{font-family:'TeX Gyre Heros Cn',sans-serif;text-transform:uppercase;letter-spacing:.05em}
.p2-scr{position:relative;width:4.45in;height:4.3in;border:1.5pt solid #2a271f;background:#cdbf98;margin:4pt 0 6pt;font-family:'TeX Gyre Heros Cn',sans-serif;font-size:6.6pt;text-transform:uppercase;letter-spacing:.04em;color:#2a271f}
.p2-scr div{position:absolute;box-sizing:border-box}
.p2-scr .map{left:0;top:0;width:3.2in;height:4.3in;background:repeating-linear-gradient(0deg,rgba(0,0,0,.05) 0 .25in,transparent .25in .5in),#bfb085}
.p2-scr .side{left:3.2in;top:0;width:1.25in;height:4.3in;background:#3a362c;border-left:1.5pt solid #2a271f}
.p2-scr .mini{left:3.28in;top:.08in;width:1.09in;height:.95in;background:#6b7a52;border:1pt solid #f0e6cc}
.p2-scr .cred{left:3.28in;top:1.1in;width:1.09in;height:.2in;background:#d99a1e;border:1pt solid #1e1b16}
.p2-scr .pow{left:3.28in;top:1.36in;width:1.09in;height:.2in;background:repeating-linear-gradient(45deg,#55633a 0 4pt,#3b4628 4pt 8pt);border:1pt solid #1e1b16}
.p2-scr .tabs{left:3.28in;top:1.64in;width:1.09in;height:.22in;background:#8a8068;border:1pt solid #f0e6cc}
.p2-scr .cam{left:3.28in;top:1.94in;width:1.09in;height:2.2in;background:repeating-linear-gradient(0deg,#6e6650 0 .46in,#3a362c .46in .5in),#6e6650;border:1pt solid #f0e6cc}
.p2-scr .card{left:.15in;bottom:.12in;width:2.9in;height:.8in;background:#f6f1e2;border:1.2pt solid #2a271f}
.p2-scr .unit{width:.22in;height:.22in;background:#55633a;border:1pt solid #1e1b16}
.p2-scr .hp{height:.05in;background:#2e8b3a;border:.6pt solid #1e1b16;width:.3in}
.p2-scr .ghost{width:.5in;height:.5in;border:1.2pt dashed #2e8b3a;background:rgba(46,139,58,.25)}
.p2-scr .nb{width:.2in;height:.2in;border-radius:50%;background:#a03020;color:#fff;font-weight:700;font-size:7.5pt;text-align:center;line-height:.2in;border:1pt solid #fff;letter-spacing:0;z-index:4}
.p2-scr .lab{color:#2a271f;font-size:6pt}
</style>"""


def table(head, rows, cls='p2-t'):
    h = ''.join(f'<th class="{"r" if i and head[i].endswith("|") else ""}">{E(c.rstrip("|"))}</th>' for i, c in enumerate(head))
    b = ''.join('<tr>' + ''.join(f'<td class="{"n" if i == 0 else ""}">{c}</td>' for i, c in enumerate(r)) + '</tr>' for r in rows)
    return f'<table class="{cls}"><tr>{h}</tr>{b}</table>'


def pg(body, toc, first=False, draft=False):
    css = CSS if first else ''
    return Page(f'{css}<div class="p2">{body}</div>', runhead=RH, toc=toc, draft=draft)


# ============================================================================================================================
def p01():
    return pg(f"""<div class="kicker">Part II · Getting Started</div><h1 style="font-size:20pt">Running the Game</h1><div class="bar"></div>
<p class="lede">{G('The basics', 0)}</p>
<h3>What you need to know first</h3>
<ul>
<li>RTS_2027 is a two-dimensional real-time strategy game for Windows. It is open source under the GNU General Public License, version 3.</li>
<li>The game runs at {TICKS} simulation ticks per second. Times in this manual are in seconds at normal speed. In a single-player game or skirmish you can run the game faster or slower with {K('GameSpeedUp')} and {K('GameSpeedDown')}.</li>
<li>The first thing you see is the main menu (next page). If you are new, choose How to Play: it is the same guide this Part follows.</li>
<li>Press {K('Pause')} to pause the game. Press Esc to back out of a mode, then to open the pause menu. Press F1 or ? for a list of every key.</li>
</ul>
<h3>Your first game in four lines</h3>
<ul>{G('The basics')}</ul>
<div class="fieldnote">Skirmish is the quickest way in: one map, one AI opponent. New AI slots start at tier 2, Regular.</div>
<h3>How this Part is laid out</h3>
<p>The main menu and the three ways to start a game come first. Then the screen, the sidebar and the orders you give. The last five pages are
the rules that run under every game: economy, power, supply cap, veterancy and the AI difficulty tiers. Where a number appears, it is read
from the game data, so it matches the game you are running.</p>""", 'Part II · Getting Started', first=True)


def p02():
    rows = [('Continue', 'Resumes the most recent save. Greyed out when there is none ("No saved game to continue").'),
            ('Campaign', 'New campaign or load a saved one.'),
            ('Skirmish', 'A single game against AI opponents on a map you choose. New or load.'),
            ('LAN Game', 'The LAN browser: host a game or join one.'),
            ('Map Editor', 'Build your own maps.'),
            ('Replays', 'Lists saved replays.'),
            ('Archive', 'The lore screen. Its entries are the source of the names in this manual.'),
            ('Profile', 'Medals and records.'),
            ('How to Play', 'The paged controls and basics guide, eight pages.'),
            ('Settings', 'Settings and About.'),
            ('Quit', 'Leave the game.')]
    return pg(f"""<div class="kicker">Getting started</div><h1 style="font-size:20pt">The Main Menu</h1><div class="bar"></div>
<p>The main menu lists eleven entries, in this order. Move with the arrow keys or the mouse; Enter or a click chooses.</p>
{table(['Entry', 'What it does'], [(E(a), E(b)) for a, b in rows])}
<h3>How to Play, in short</h3>
<p>How to Play has eight pages: {', '.join(E(t) for t in GUIDE)}. Every control it names exists in the game; this Part repeats its wording where it can.</p>
<div class="fieldnote">Continue is selected first when you have a save. Otherwise the first usable entry is.</div>""",
              'The Main Menu')


def p03():
    return pg(f"""<div class="kicker">Getting started</div><h1 style="font-size:20pt">Ways to Play</h1><div class="bar"></div>
<h3>Campaign</h3>
<p>Choose Campaign for a new campaign or to load a saved one. Campaigns are described in Part V.</p>
<h3>Skirmish</h3>
<p>A skirmish is one game on one map. The Skirmish screen has these rows:</p>
<table class="p2-t"><tr><th>Row</th><th>Choice</th></tr>
<tr><td class="n">Map</td><td>Step with &lt; and &gt;. A generated map adds Biome, Size, Ocean and Stamps rows, and a Seed row with - , + and Reroll.</td></tr>
<tr><td class="n">Slots</td><td>Two to four slots; - and + change the count. Each slot has Kind, Fac, Tier, Cmdr, Team and Col buttons that cycle the choice.</td></tr>
<tr><td class="n">Speed</td><td>Game speed.</td></tr>
<tr><td class="n">Start</td><td>Starts the game. With no Human slot the button reads Spectate and you watch an AI game.</td></tr></table>
<p>A slot can be Closed, Human or AI. For an AI slot, Tier picks the difficulty (see the last page of this Part). New AI slots start at tier 2, <b>Regular</b>. Cmdr marks an AI slot as led by an LLM commander. </p>
<h3>Replays</h3>
<p>Choose Replays on the main menu to list your saved replays.</p>
<h3>LAN game</h3>
<p>LAN Game opens the browser. Choose <b>Host game</b> to open a lobby, or join a game from the list. You can also type an address in the row <b>Join by IP</b> and press Enter, and switch between <b>Join as: player</b> and <b>Join as: spectator</b>.</p>
<p>In the lobby the host sets the map and each slot. Other players change their own faction, team and colour, or Move to an open slot. The host can Kick a player.</p>""",
              'Campaign, Skirmish, Replays, LAN')


def p04():
    nb = lambda n, x, y: f'<div class="nb" style="left:{x}in;top:{y}in">{n}</div>'
    scr = f"""<div class="p2-scr">
<div class="map"></div>
<div class="ghost" style="left:1.1in;top:.9in"></div><div class="unit" style="left:.5in;top:2.3in"></div><div class="unit" style="left:.8in;top:2.45in"></div>
<div class="unit" style="left:.65in;top:2.7in"></div><div class="hp" style="left:.46in;top:2.2in"></div>
<div class="side"></div><div class="mini"></div><div class="cred"></div><div class="pow"></div><div class="tabs"></div><div class="cam"></div>
<div class="card"></div>
<div class="lab" style="left:3.3in;top:.45in;color:#f0e6cc">minimap</div>
<div class="lab" style="left:3.3in;top:1.16in">credits</div><div class="lab" style="left:3.3in;top:1.42in;color:#f0e6cc">power</div>
<div class="lab" style="left:.2in;bottom:.95in;">selection card</div>
{nb(1, .05, .05)}{nb(2, 4.15, 1.62)}{nb(3, 4.2, 2.8)}{nb(4, 4.15, .1)}{nb(5, 4.2, 1.08)}{nb(6, 4.2, 1.34)}{nb(7, 2.75, 3.4)}{nb(8, .3, 2.12)}{nb(9, 1.3, 1.5)}
</div>"""
    items = [('1', 'The map view', 'Where you give orders. Scroll with the arrow keys or the mouse at the screen edge; the wheel zooms; middle-drag drags the view.'),
             ('2', 'Sidebar tabs', f'Five tabs: Structures {K("TabStructures")}, Defences {K("TabDefences")}, Infantry {K("TabInfantry")}, Vehicles {K("TabVehicles")}, Upgrades {K("TabUpgrades")}. An Aircraft tab {K("TabAircraft")} appears when you have aircraft.'),
             ('3', 'Cameos', 'The build icons. Click one to start building it; right-click to cancel.'),
             ('4', 'Minimap', 'Click or drag it to move the view.'),
             ('5', 'Credits', 'Your money. It rises as Scrapers unload.'),
             ('6', 'Power', 'Keep your Power Plants ahead of what you build.'),
             ('7', 'Selection card', 'At the bottom of the map. Health, power, production and rally point for a building, with Sell and Repair; stats and order buttons for units.'),
             ('8', 'Health bars', 'Shown over selected units and damaged buildings.'),
             ('9', 'Building ghost', 'After a structure finishes, click it, then click a green spot near your base. Red means blocked.')]
    lst = ''.join(f'<tr><td class="n">{a}</td><td><b>{E(b)}</b>. {c}</td></tr>' for a, b, c in items)
    return pg(f"""<div class="kicker">Getting started</div><h1 style="font-size:20pt">The Screen</h1><div class="bar"></div>
{scr}
<div style="font-size:6.6pt;margin:-3pt 0 3pt;color:#5d5543">Schematic, not a screenshot. Positions are indicative.</div>
<table class="p2-t" style="font-size:7.2pt">{lst}</table>""", 'The Screen')


def p05():
    cam = [('Q', 'TabStructures'), ('W', 'TabDefences'), ('E', 'TabInfantry'), ('R', 'TabVehicles'), ('T', 'TabUpgrades'), ('Y', 'TabAircraft')]
    names = {'TabStructures': 'Structures', 'TabDefences': 'Defences', 'TabInfantry': 'Infantry', 'TabVehicles': 'Vehicles', 'TabUpgrades': 'Upgrades',
             'TabAircraft': 'Aircraft (when you have any)'}
    rows = [(K(a), names[a]) for _, a in cam]
    ex = rules()['buildings']
    exu = rules()['units']
    pl = ex['auth_power']
    tk = exu['auth_tank']
    return pg(f"""<div class="kicker">Getting started</div><h1 style="font-size:20pt">Sidebar and Production</h1><div class="bar"></div>
<ul>{G('Building')}</ul>
<div class="cols2">
{table(['Key', 'Sidebar tab'], rows)}
<h3>Cameo keys</h3>
<p>The cameos in the open tab have keys too: {K('CameoSlot1')} {K('CameoSlot2')} {K('CameoSlot3')} are row 1, {K('CameoSlot4')} {K('CameoSlot5')} {K('CameoSlot6')} row 2,
{K('CameoSlot7')} {K('CameoSlot8')} {K('CameoSlot9')} row 3, {K('CameoSlot10')} {K('CameoSlot11')} {K('CameoSlot12')} row 4. Hold Shift to queue five.</p>
</div>
<h3>How long things take</h3>
<div class="rule"><b>Rule.</b> Building takes {ECO['build_ticks_per_100_credits'] // TICKS} seconds for every 100 credits of cost, at normal speed.
A {E(btitle('auth_power'))} costs {pl['cost']} and takes {pl['build_seconds']:g} seconds. A {E(title('auth_tank'))} costs {tk['cost']} and takes {tk['build_seconds']:g}.</div>
<p>Each lane holds a queue of at most {ECO['max_queue']} items. 
</p>
<h3>Sell and repair</h3>
<p>Selling a building returns {ECO['sell_refund_permille'] // 10} percent of its cost. Repair mends {ECO['repair_hp_per_tick']} health per tick ({ECO['repair_hp_per_tick'] * TICKS} per second).</p>
<div class="fieldnote">Two keys help with production: {K('IdleFactory')} jumps to the next idle factory, {K('IdleHarvester')} to the next idle harvester.</div>""",
              'Sidebar and Production')


def p06():
    def r(a, b): return (a, b)
    keys = [(K('AttackMove'), 'Attack-move: press, then click the ground. Press again to cancel.'), (K('Stop'), 'Stop'), (K('Guard'), 'Guard the spot'),
            (K('Scatter'), 'Scatter the group'), (K('Deploy'), 'Deploy or unload'), (K('Pack'), 'Pack or unpack a rig'),
            (K('StanceAggressive'), 'Stance: aggressive (chase what is in sight)'), (K('StanceDefensive'), 'Stance: defensive (fire in range, chase a few cells, walk back)'),
            (K('StanceHoldFire'), 'Stance: hold fire (fire only at what you order)')]
    return pg(f"""<div class="kicker">Getting started</div><h1 style="font-size:20pt">Selecting and Giving Orders</h1><div class="bar"></div>
<h3>Selecting</h3><ul>{G('Selecting')}</ul>
<p>Shift with a click or box adds to the selection. A double-click, or Ctrl with a click, selects every unit of that type on screen. {K('SelectAll')} selects all combat units, not harvesters or builders.
Control groups: Ctrl with {K('Group1')} to {K('Group9')} sets one, Shift adds, the key recalls it, twice centres the view.</p>
<h3>Orders</h3><ul>{G('Moving and attacking')}</ul>
{table(['Key', 'Order'], [(a, E(b) if not b.startswith('<') else b) for a, b in keys])}
<p>Shift with right-click queues waypoints (up to 8). Ctrl with right-click attacks the ground.</p>""", 'Selection and Orders')


def p07():
    sc = rules()['units']['auth_scraper']
    cap = sc['harvest_capacity']
    return pg(f"""<div class="kicker">Rules</div><h1 style="font-size:20pt">Economy</h1><div class="bar"></div>
<p class="lede">{G('The basics', 2)}</p>
<ul>{G('Harvesting ashfall')}</ul>
<h3>The numbers</h3>
{table(['Rule', 'Value|'], [
    ('Starting cash', f"{ECO['starting_cash']:,}"),
    ('Ashfall, credits per unit carried', f"{ECO['credits_ashfall']}"),
    ('Pitchblende, credits per unit carried', f"{ECO['credits_pitchblende']}"),
    (f'{E(title("auth_scraper"))} load (units of ash)', f'{cap}'),
    ('Scooping, ticks per unit', f"{ECO['harvest_ticks_per_unit']}"),
    ('Unloading, ticks per unit', f"{ECO['unload_ticks_per_unit']}"),
    ('Field regrowth interval (seconds)', f"{ECO['regrowth_interval_ticks'] // TICKS}"),
    ('Sell refund', f"{ECO['sell_refund_permille'] // 10} percent"),
])}
<div class="rule"><b>Rule.</b> One full {E(title('auth_scraper'))} load of ashfall is {cap} x {ECO['credits_ashfall']} = {cap * ECO['credits_ashfall']:,} credits.
Pitchblende pays double per unit: {ECO['credits_pitchblende']}.</div>
<p>A {E(title('auth_refinery'))} costs {BUILD['auth_refinery']['cost']:,} and stores {BUILD['auth_refinery']['storage']:,}; a {E(title('auth_silo'))} costs {BUILD['auth_silo']['cost']} and stores {BUILD['auth_silo']['storage']:,}. Other factions have their own versions;
Part III lists them.</p>
<div class="fieldnote">Ash harms unprotected soldiers standing on a field: {GAME['radiation']['ashfall_damage']} damage every {GAME['radiation']['ashfall_interval_ticks'] / TICKS:g} seconds, before their radiation resistance. Vehicles and buildings are unaffected.</div>""",
              'Economy')


def p08():
    pw = [(f, i) for f, i in (('Authority', 'auth_power'), ('Directorate', 'dir_reactor_vent'), ('Left Behind', 'lb_dynamo_rig'), ('Expedition', 'exp_pile_house'))]
    rows = [(f, E(title(i)), f"+{BUILD[i]['power']}", f"{BUILD[i]['cost']}") for f, i in pw]
    drains = [('auth_barracks', 'Authority'), ('auth_refinery', 'Authority'), ('auth_factory', 'Authority'), ('auth_turret', 'Authority'),
              ('dir_smelter_vent', 'Directorate'), ('dir_assembly_bore', 'Directorate'), ('lb_refinery_rig', 'Left Behind'), ('lb_garage_rig', 'Left Behind')]
    dr = [(E(title(i)), f, f"{BUILD[i]['power']}", f"{BUILD[i]['cost']:,}") for i, f in drains]
    return pg(f"""<div class="kicker">Rules</div><h1 style="font-size:20pt">Power</h1><div class="bar"></div>
<p class="lede">{G('The basics', 1)}</p>
<div class="rule"><b>Rule.</b> Power plants produce power. Most other buildings use it. Your total produced must stay at or above your total used. Below that you are in low power, and production slows.</div>
<h3>Where power comes from</h3>
{table(['Faction', 'Building', 'Power|', 'Cost|'], rows)}
<h3>Some buildings that use it</h3>
{table(['Building', 'Faction', 'Power|', 'Cost|'], dr)}
<p>The main base building, walls and some defences use none.</p>
<div class="fieldnote">Build a plant before you need it: keep your Power Plants ahead of what you build.</div>""", 'Power')


def p09():
    ids = ['auth_rifleman', 'auth_scraper', 'auth_tank', 'auth_bulwark']
    U = rules()['units']
    rows = [(E(title(i)), f"{U[i]['cost']}", f"{U[i]['supply_resolved']}") for i in ids]
    sb = [('Authority', 'auth_ration_hall'), ('Directorate', 'dir_bunk_gallery'), ('Left Behind', 'lb_still_rig'), ('Expedition', 'exp_provision_store')]
    base = [('Authority', 'auth_conyard'), ('Directorate', 'dir_shaft'), ('Left Behind', 'lb_mother_rig'), ('Expedition', 'exp_landing_depot')]
    brows = [(f, E(title(b)), f"+{BUILD[b]['supply_provided']}", E(title(s)), f"+{BUILD[s]['supply_provided']}") for (f, b), (_, s) in zip(base, sb)]
    b1, b2 = SUP['band1_max'], SUP['band2_max']
    return pg(f"""<div class="kicker">Rules</div><h1 style="font-size:20pt">Supply Cap</h1><div class="bar"></div>
<div class="rule"><b>Rule.</b> Every unit uses supply. Your cap is the supply your main base and your supply buildings provide, up to a ceiling of {SUP['ceiling']}.
At the cap, you cannot queue more units until you build more supply or lose units.</div>
<h3>What a unit costs in supply</h3>
<p>Supply follows the unit's credit cost: {SUP['band_values'][0]} up to {b1} credits, {SUP['band_values'][1]} up to {b2}, {SUP['band_values'][2]} above {b2}. Harvesters count 1 whatever they cost.</p>
{table(['Unit', 'Cost|', 'Supply|'], rows)}
<h3>What provides supply</h3>
{table(['Faction', 'Main base', 'Cap|', 'Supply building', 'Adds|'], brows)}
<p>Buildings and walls use no supply. A sold or destroyed building stops counting at once. No unit ever dies because of the cap; the cap only stops production.</p>""", 'Supply Cap')


def p10():
    names = ['Veteran', 'Elite', 'Hero']
    th = VET['thresholds']
    rows = []
    for i, n in enumerate(names):
        dmg = VET['damage_permille'][i] - 1000
        tk = 1000 - VET['taken_permille'][i]
        rl = 1000 - VET['reload_permille'][i]
        sp = VET['speed_permille'][i] - 1000
        hl = VET['heal_permille'][i]
        rows.append((n, f"{th[i]:,}", f"+{dmg // 10}%", f"-{tk // 10}%", f"-{rl // 10}%", f"+{sp // 10}%" if sp else '-', f"{hl // 10}% / s" if hl else '-'))
    return pg(f"""<div class="kicker">Rules</div><h1 style="font-size:20pt">Veterancy</h1><div class="bar"></div>
<div class="rule"><b>Rule.</b> A unit that lands the killing blow on an enemy earns kill value equal to the victim's cost. At {th[0]}, {th[1]:,} and {th[2]:,} it ranks up to
Veteran, Elite and Hero. The same three ranks apply to every faction.</div>
{table(['Rank', 'Kill value|', 'Damage|', 'Damage taken|', 'Reload time|', 'Speed|', 'Self-heal|'], rows)}
<ul>
<li>Kills of buildings count for {VET['kill_permille_building'] // 10} percent of their cost; kills of units count in full.</li>
<li>Only armed units rank. Harvesters, engineers and unarmed transports stay at rank zero. Buildings never rank.</li>
<li>Hero units heal {VET['heal_permille'][2] // 10} percent of their health each {VET['heal_interval_ticks'] // TICKS} second, after {VET['heal_quiet_ticks'] // TICKS} seconds without firing or being hit.</li>
<li>A kill is credited to the unit that fires the lethal shot, not to its neighbours. Superweapons, radiation and death blasts credit no one.</li>
<li>A unit keeps its rank when it is loaded, goes underground or packs. It loses it if it becomes a building.</li>
</ul>
<div class="fieldnote">A Hero is worth about two ordinary units of its type. Protect a ranked unit: pull it back when it is hurt.</div>""", 'Veterancy')


def p11():
    return pg(f"""<div class="kicker">Getting started</div><h1 style="font-size:20pt">Saving and Loading</h1><div class="bar"></div>
<div class="rule"><b>Quicksave</b> {K('QuickSave')} (skirmish and missions). <b>Quickload</b> {K('QuickLoad')}: the game asks before it loads.</div>
<h3>From the menus</h3>
<ul>
<li><b>Continue</b> on the main menu resumes the most recent save. It is greyed out when there is none.</li>
<li><b>Campaign</b> and <b>Skirmish</b> each offer New and Load.</li>
<li><b>Replays</b> lists saved replays.</li>
</ul>
<h3>Things to know</h3>
<ul>
<li>Pause with {K('Pause')} before you think. Esc backs out of any mode first, and then opens the pause menu.</li>
<li>Quicksave and quickload apply to skirmish and missions.</li>
<li>Camera bookmarks are separate: Ctrl with {K('Bookmark1')}, {K('Bookmark2')}, {K('Bookmark3')} or {K('Bookmark4')} stores a view; the key alone jumps to it. {K('CentreBase')} centres on your base and {K('LastAlert')} jumps to the last alert.</li>
</ul>
<div class="fieldnote">Save before you attack a base you have not scouted.</div>""", 'Saving and Loading')


FACTION_NAMES = {'authority': 'Authority', 'directorate': 'Directorate', 'left_behind': 'Left Behind', 'expedition': 'Expedition'}


def _override_change(k, v):
    """One per-faction override key as a short phrase for the Difficulty Tiers table (None = not shown)."""
    if k == 'respond_to_attacks': return 'respond_to_attacks' if v else 'no respond_to_attacks'
    if k == 'attack_threshold': return f'attacks at army {v}'
    if k == 'army_cap': return f'army cap {v}'
    if k == 'army_cap_until_tick': return f'cap until minute {v / TICKS / 60:g}'
    if k == 'harvester_target': return f'{v} harvesters'
    if k == 'attack_ratio_pct': return f'attack ratio {v} %'
    if k == 'expansion_tick': return f'expands from minute {v / TICKS / 60:g}' if v else 'never expands'
    if k == 'retreat_health_pct': return f'retreats below {v} % health'
    if k == 'unit_reserve': return f'keeps {v} credits in hand'
    return None


def override_rows():
    """Rows (faction, tier, changes) for the per-faction tier tables in data/ai/difficulty.toml [faction.<id>.tierN]."""
    rows = []
    for fid, name in FACTION_NAMES.items():
        for n in range(1, 6):
            ov = DIFF.get('faction', {}).get(fid, {}).get(f'tier{n}')
            if not ov: continue
            parts = [c for k, v in ov.items() if (c := _override_change(k, v))]
            if parts: rows.append((name, str(n), E('; '.join(parts))))
    return rows


def p12():
    order = ['recruit', 'regular', 'veteran', 'elite', 'commissar']
    gloss = {'repair': 'repairs damaged structures', 'replace_harvesters': 'rebuilds lost harvesters', 'respond_to_attacks': 'sends idle units to a structure under attack',
             'scatter': 'moves units out of splash fire', 'focus_fire': 'focuses fire on the weakest enemy in range', 'enemy_aware_mix': 'picks units against what it has seen of yours',
             'two_pronged': 'splits a large wave in two', 'counter_defences': 'favours long-range units against defences', 'use_upgrades': 'researches upgrades',
             'predicted_retreat': 'calls off a wave it expects to lose', 'superweapons': 'builds and fires a superweapon', 'air': 'builds air tech and aircraft',
             'scouting': 'scouts with a cheap unit', 'harass': 'raids harvesters and power', 'multi_front': 'attacks from two flanks at once', 'adapt_build': 'adds production and defences when cash piles up'}
    seen, rows = set(), []
    for n in order:
        t = DIFF[n]
        new = [gloss[f] for f in gloss if t.get(f) and f not in seen]
        seen |= {f for f in gloss if t.get(f)}
        if t['build_repertoire']: new.insert(0, f"builds a fixed list of {len(t['build_repertoire'])} structures")
        if t['expansion_tick']: new.append(f"expands from minute {t['expansion_tick'] // TICKS // 60:g}")
        rows.append((n.title(), f"{t['tier']}", f"{t['decision_interval_ticks'] / TICKS:g} s", f"{t['attack_threshold']}", E('; '.join(new) or 'basic play only')))
    over = override_rows()
    over_html = ('<p>Some factions set their own numbers for single tiers. The table below lists them. Every other tier uses the table above.</p>'
                 + table(['Faction', 'Tier', 'Changes'], over)) if over else ''
    return pg(f"""<div class="kicker">Rules</div><h1 style="font-size:20pt">Difficulty Tiers</h1><div class="bar"></div>
<p>Each AI slot has one of five tiers. Higher tiers think faster and use more of the game. Tier 2, Regular, is the default.</p>
{table(['Tier', 'No.|', 'Thinks every|', 'Attacks at army|', 'Adds'], rows)}
<p>Each tier also keeps everything the tier below it has. The Adds column lists what is new. "Attacks at army" is the army size at which the AI first sends a wave.</p>
{over_html}<div class="fieldnote">A slot marked Cmdr is led by an LLM commander; press {K('Talk')} to talk to one when it is in the game.</div>""",
              'Difficulty Tiers')


def pages():
    return [p01(), p02(), p03(), p04(), p05(), p06(), p07(), p08(), p09(), p10(), p11(), p12()]
