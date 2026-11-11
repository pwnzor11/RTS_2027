"""Build the RTS_2027 field manual pilot: half-letter pages -> HTML -> PDF (Playwright/Chromium)."""
import json, html, base64, pathlib, re

B = pathlib.Path('tools/booklet/pilot')
cards = json.load(open(B / 'cards.json'))
ups = json.load(open(B / 'ups.json'))['ups']
E = html.escape

def img(name):
    p = B / 'img' / name
    mime = 'image/svg+xml' if name.endswith('.svg') else 'image/png'
    return f'data:{mime};base64,' + base64.b64encode(p.read_bytes()).decode()

CSS = r"""
@page { size: 5.5in 8.5in; margin: 0; }
:root { --paper:#efe7d3; --ink:#1e1b16; --muted:#5c564a; --olive:#55633a; --olive-d:#3b4628; --amber:#d99a1e;
        --rule:#2a271f; --tint:#e3d9bf; }
* { box-sizing: border-box; }
html, body { margin:0; padding:0; background:#888; }
body { font-family: 'TeX Gyre Schola', 'Century Schoolbook', serif; color: var(--ink); font-size: 8.6pt; line-height: 1.32; }
.page { width:5.5in; height:8.5in; background: var(--paper); position:relative; overflow:hidden; page-break-after: always;
        padding: .5in .45in .55in .45in; }
.page.left  { padding-left:.45in; padding-right:.6in; }
.page.right { padding-left:.6in;  padding-right:.45in; }
.page::after { content:''; position:absolute; inset:0; pointer-events:none;
  background: radial-gradient(ellipse at 30% 20%, rgba(255,255,255,.18), transparent 60%),
              radial-gradient(ellipse at 80% 90%, rgba(120,90,40,.10), transparent 55%); }
.folio { position:absolute; bottom:.28in; font-family:'TeX Gyre Heros Cn', sans-serif; font-weight:700; font-size:7.5pt;
         letter-spacing:.12em; color:var(--muted); }
.left .folio { left:.45in; } .right .folio { right:.45in; }
.runhead { position:absolute; top:.24in; font-family:'TeX Gyre Heros Cn', sans-serif; font-size:7pt; letter-spacing:.2em;
           text-transform:uppercase; color:var(--muted); }
.left .runhead { left:.45in; } .right .runhead { right:.45in; }
h1,h2,h3,h4 { font-family:'TeX Gyre Heros Cn', 'Nimbus Sans Narrow', sans-serif; font-weight:700; margin:0; text-transform:uppercase; }
h1 { font-size: 22pt; line-height:.95; letter-spacing:.01em; }
h2 { font-size: 13pt; letter-spacing:.03em; border-bottom: 2.5pt solid var(--rule); padding-bottom:2pt; margin: 0 0 6pt; }
h3 { font-size: 9.5pt; letter-spacing:.06em; margin: 8pt 0 2pt; color: var(--olive-d); }
p { margin: 0 0 5pt; text-align: justify; hyphens: auto; }
.cols2 { column-count:2; column-gap:.2in; column-rule: .5pt solid #b9ad8f; }
.cols2 h3:first-child { margin-top:0; }
.cols2 p { text-align:left; }
.mono, .tele { font-family:'Courier 10 Pitch','Nimbus Mono PS', monospace; }
.tele { font-size:7.4pt; line-height:1.25; background:#f6f1e2; border:1pt solid #2a271f; padding:5pt 6pt; margin:6pt 0;
        box-shadow: 2pt 2pt 0 rgba(0,0,0,.18); white-space:pre-wrap; }
.kicker { font-family:'TeX Gyre Heros Cn', sans-serif; font-size:7.5pt; letter-spacing:.22em; text-transform:uppercase; color:var(--olive); font-weight:700; }
.bar { height:6pt; background: repeating-linear-gradient(45deg, var(--amber) 0 6pt, var(--ink) 6pt 12pt); margin: 4pt 0 8pt; }
.draft { position:absolute; top:.22in; left:50%; transform:translateX(-50%); font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700;
         font-size:6.5pt; letter-spacing:.2em; color:#a03020; border:1pt solid #a03020; padding:1pt 5pt; background:rgba(255,255,255,.4); z-index:5; }
/* cover */
.cover { padding:0; background:#1d1b17; color:#f0e6cc; }
.cover .art { position:absolute; inset:0; background-size:cover; background-position: 55% 50%; filter: saturate(.9) contrast(1.05); }
.cover .shade { position:absolute; inset:0; background: linear-gradient(180deg, rgba(20,18,14,.85) 0%, rgba(20,18,14,.15) 34%, rgba(20,18,14,.05) 55%, rgba(20,18,14,.92) 88%); }
.cover .top { position:absolute; top:.45in; left:.45in; right:.45in; }
.cover .logo { font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; font-size:54pt; line-height:.85; letter-spacing:-.01em;
               color:#f3e9cf; text-shadow: 0 2pt 0 #000, 0 0 12pt rgba(0,0,0,.6); }
.cover .logo span { color: var(--amber); }
.cover .tag { font-family:'TeX Gyre Heros Cn',sans-serif; letter-spacing:.32em; font-size:8.5pt; text-transform:uppercase; margin-top:6pt; color:#e8dcbc; }
.cover .bottom { position:absolute; bottom:.42in; left:.45in; right:.45in; }
.cover .manual { font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; font-size:15pt; letter-spacing:.14em; text-transform:uppercase; }
.cover .sub { font-size:8pt; color:#d8ccaa; margin-top:3pt; font-style:italic; }
.cover .stripe { height:9pt; margin:8pt 0; background: repeating-linear-gradient(45deg, var(--amber) 0 9pt, #111 9pt 18pt); }
.cover .fine { font-family:'TeX Gyre Heros Cn',sans-serif; font-size:6.5pt; letter-spacing:.12em; color:#b9ad8f; text-transform:uppercase; display:flex; justify-content:space-between; }
/* faction opener */
.bleedart { position:absolute; left:0; right:0; top:0; height:5.1in; background-size:cover; background-position:50% 50%; }
.bleedart::after { content:''; position:absolute; inset:0; background: linear-gradient(180deg, transparent 55%, var(--paper) 100%); }
.opener-title { position:absolute; left:.45in; right:.6in; top:4.55in; }
.opener-title h1 { font-size:25pt; color: var(--ink); }
.opener-title .kicker { margin-bottom:3pt; }
.emblem { width:.95in; height:.95in; float:right; margin: -.1in 0 .05in .12in; }
.slogan { font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; letter-spacing:.18em; text-align:center; font-size:8.5pt;
          border-top:1.5pt solid var(--rule); border-bottom:1.5pt solid var(--rule); padding:3pt 0; margin: 8pt 0; }
.factbox { border:1.2pt solid var(--rule); background: var(--tint); padding:5pt 7pt; margin-top:6pt; font-size:7.8pt; }
.factbox dl { display:grid; grid-template-columns: 1.1in 1fr; gap:1.5pt 6pt; margin:0; }
.factbox dt { font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; text-transform:uppercase; letter-spacing:.05em; }
.factbox dd { margin:0; }
/* unit entries */
.unit { border-top: 2.5pt solid var(--rule); padding-top:4pt; margin-bottom: 7pt; }
.unit .head { display:flex; justify-content:space-between; align-items:baseline; }
.unit .head h2 { border:0; margin:0; padding:0; font-size:15pt; }
.unit .head .cost { font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; font-size:12pt; }
.unit .sub { font-style:italic; color: var(--muted); margin-bottom:4pt; }
.unit .art { background: radial-gradient(ellipse at 50% 70%, #e9dfc4 0%, #d8cba8 70%, #cbbd96 100%);
             border:1pt solid var(--rule); display:flex; align-items:center; justify-content:center; position:relative; }
.unit .art img { image-rendering: pixelated; }
.unit .art .plate { position:absolute; left:4pt; top:3pt; font-family:'TeX Gyre Heros Cn',sans-serif; font-size:6.5pt; letter-spacing:.18em; color:var(--muted); }
.stats { width:100%; border-collapse:collapse; font-family:'TeX Gyre Heros Cn',sans-serif; font-size:8pt; margin: 5pt 0; }
.stats td { border-bottom:.6pt solid #a99d7f; padding:1.4pt 2pt; }
.stats td:first-child { text-transform:uppercase; letter-spacing:.06em; color:var(--muted); width:40%; }
.stats td:last-child { font-weight:700; text-align:right; }
.versus { display:grid; grid-template-columns: repeat(5, 1fr); gap:2pt; margin:4pt 0 2pt; }
.versus div { text-align:center; font-family:'TeX Gyre Heros Cn',sans-serif; font-size:6.4pt; text-transform:uppercase; letter-spacing:.04em; }
.versus .m { height:26pt; border:1pt solid var(--rule); background:#f6f1e2; position:relative; margin-bottom:1pt; }
.versus .m i { position:absolute; left:0; right:0; bottom:0; background: var(--olive); }
.versus .m b { position:absolute; top:1pt; left:0; right:0; font-size:7pt; }
.fieldnote { font-family:'Courier 10 Pitch',monospace; font-size:7pt; line-height:1.25; border-left:3pt solid var(--amber);
             padding:3pt 0 3pt 6pt; margin:5pt 0; background:rgba(255,255,255,.35); }
.mini { display:grid; grid-template-columns: 1.35in 1fr; gap: 8pt; }
/* tech tree spread */
.tree-wrap { position:absolute; top:.85in; height:6.55in; width:10.2in; }
.left .tree-wrap  { left:.4in; }
.right .tree-wrap { left:calc(-5.5in + .4in); }
.tree-wrap img { width:100%; height:100%; object-fit:contain; object-position: center top; }
.legend { position:absolute; bottom:.55in; font-family:'TeX Gyre Heros Cn',sans-serif; font-size:7pt; display:flex; gap:10pt; align-items:center; }
.left .legend { left:.45in; }
.legend span { display:inline-flex; align-items:center; gap:3pt; }
.legend i { width:12pt; height:8pt; border:1pt solid var(--rule); display:inline-block; }
.research { position:absolute; right:.45in; bottom:.55in; width:2.15in; border:1.2pt solid var(--rule); background:var(--tint); padding:4pt 6pt; font-size:7pt; }
.research h4 { font-size:8pt; letter-spacing:.08em; border-bottom:1pt solid var(--rule); margin-bottom:2pt; }
.research table { width:100%; border-collapse:collapse; font-family:'TeX Gyre Heros Cn',sans-serif; }
.research td { padding:.6pt 0; } .research td:last-child { text-align:right; }
/* timeline / maps */
.timeline { border-left:2pt solid var(--rule); margin-left:.62in; padding-left:9pt; }
.timeline .ev { position:relative; margin-bottom:4.5pt; font-size:7.9pt; }
.timeline .ev b { position:absolute; left:-.84in; width:.62in; text-align:right; font-family:'TeX Gyre Heros Cn',sans-serif; font-size:8pt; }
.timeline .ev::before { content:''; position:absolute; left:-12.5pt; top:3pt; width:7pt; height:7pt; background:var(--amber); border:1pt solid var(--rule); }
.maps { display:grid; grid-template-columns: 1fr 1fr; gap: 8pt 10pt; margin-top:4pt; }
.maps figure { margin:0; }
.maps img { width:100%; image-rendering: pixelated; border:1.2pt solid var(--rule); display:block; }
.maps figcaption { font-family:'TeX Gyre Heros Cn',sans-serif; font-size:7.3pt; line-height:1.2; margin-top:2pt; }
.maps figcaption b { text-transform:uppercase; letter-spacing:.06em; }
.ops { width:100%; border-collapse:collapse; margin-top:4pt; }
.ops td { vertical-align:top; padding:3pt 4pt 4pt 0; border-bottom:.6pt solid #a99d7f; font-size:7.8pt; }
.ops td.n { font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; font-size:14pt; width:.32in; color:var(--olive); }
.ops b { font-family:'TeX Gyre Heros Cn',sans-serif; text-transform:uppercase; letter-spacing:.05em; }
"""

def page(side, n, body, runhead='', draft=False, extra=''):
    rh = f'<div class="runhead">{E(runhead)}</div>' if runhead else ''
    dr = '<div class="draft">DRAFT TEXT · NOT YET APPROVED</div>' if draft else ''
    fo = f'<div class="folio">{n}</div>' if n else ''
    return f'<section class="page {side} {extra}">{dr}{rh}{body}{fo}</section>'

EMBLEM = """<svg class="emblem" viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
<g fill="none" stroke="#1e1b16" stroke-width="3">
<circle cx="50" cy="50" r="31" fill="#55633a"/>
<g fill="#55633a" stroke="#1e1b16" stroke-width="2.5">""" + ''.join(
    f'<rect x="45" y="6" width="10" height="13" transform="rotate({a} 50 50)"/>' for a in range(0, 360, 30)) + """</g>
<circle cx="50" cy="50" r="31" fill="#55633a" stroke="#1e1b16"/>
<circle cx="50" cy="50" r="21" fill="#efe7d3"/>
<polygon points="50,31 54,50 50,69 46,50" fill="#d99a1e" stroke="#1e1b16" stroke-width="1.6"/>
<polygon points="31,50 50,46 69,50 50,54" fill="#1e1b16" stroke="#1e1b16" stroke-width="1.2"/>
<circle cx="50" cy="50" r="2.6" fill="#efe7d3" stroke="#1e1b16" stroke-width="1.2"/>
</g></svg>"""

def stat_rows(c):
    w = c.get('weapon')
    rows = [('Cost', f"{c['cost']} credits"), ('Build time', f"{c['build']:g} s"), ('Armour', f"{c['hp']} HP · {c['armor']}"),
            ('Speed', f"{c['speed']:g} cells/s"), ('Sight', f"{c['sight']} cells")]
    if w:
        rows += [('Weapon range', f"{w['range']:g} cells"), ('Damage / reload', f"{w['damage']} / {w['reload']:g} s")]
    rows.append(('Requires', ', '.join(c['prereq']) or '—'))
    return '<table class="stats">' + ''.join(f'<tr><td>{E(a)}</td><td>{E(b)}</td></tr>' for a, b in rows) + '</table>'

def versus(c):
    w = c.get('weapon')
    if not w: return ''
    lab = {'unarmored': 'Infantry', 'light': 'Light', 'heavy': 'Armour', 'structure': 'Bldg', 'fortified': 'Defence'}
    cells = ''.join(f'<div><div class="m"><i style="height:{min(v,100)}%"></i><b>{v}%</b></div>{lab[k]}</div>' for k, v in w['vs'].items())
    return f'<div class="kicker" style="font-size:6.5pt;margin-top:4pt">Damage against</div><div class="versus">{cells}</div>'

def unit_full(c, imgfile, h=1.75):
    return f"""<div class="unit">
<div class="head"><h2>{E(c['title'])}</h2><div class="cost">{c['cost']}</div></div>
<div class="sub">{E(c['subtitle'])} · Tier {c['tier']} · {E(c['loco'])}</div>
<div class="art" style="height:{h}in"><span class="plate">PLATE A-{c['id'][-4:].upper()}</span><img src="{img(imgfile)}" style="height:{h*0.82:.2f}in"></div>
<div class="mini" style="margin-top:6pt"><div>{stat_rows(c)}{versus(c)}</div>
<div><p>{E(c['body'])}</p><div class="fieldnote">{E(c['note'])}</div>
<p style="font-size:7.6pt"><b>Use it for:</b> {E(', '.join(c.get('strong', [])) or 'support')}. <b>Keep it away from:</b> {E(', '.join(c.get('weak', [])) or '—')}.</p></div></div></div>"""

def unit_half(c, imgfile):
    return f"""<div class="unit">
<div class="head"><h2 style="font-size:12.5pt">{E(c['title'])}</h2><div class="cost">{c['cost']}</div></div>
<div class="sub">{E(c['subtitle'])} · Tier {c['tier']}</div>
<div class="mini"><div><div class="art" style="height:.9in"><img src="{img(imgfile)}" style="height:.74in"></div>{stat_rows(c)}</div>
<div><p>{E(c['body'])}</p><div class="fieldnote">{E(c['note'])}</div>{versus(c)}</div></div></div>"""

pages = []
# 1 cover
pages.append(f"""<section class="page cover"><div class="art" style="background-image:url({img('still_authority_act2.png')})"></div><div class="shade"></div>
<div class="top"><div class="logo">RTS<span>·</span>2027</div><div class="tag">Ashfall · The War for the Surface</div></div>
<div class="bottom"><div class="manual">Field Manual</div><div class="sub">Operations handbook for commanders of the Authority, the Directorate, the Left Behind and the Overseas Expedition</div>
<div class="stripe"></div><div class="fine"><span>Pilot edition · data of build e059f0f</span><span>Open source · GPL v3</span><span>Windows</span></div></div></section>""")

# 2-3 faction opener
pages.append(page('left', 2, f"""<div class="bleedart" style="background-image:url({img('still_authority_act2.png')})"></div>
<div class="opener-title"><div class="kicker">Faction one · The protagonists</div><h1>The Continental<br>Reconstruction Authority</h1>
<div class="bar"></div>
<p style="font-size:8.2pt"><i>"Count the living, mend the line."</i> — teletype header, every message since 1963.</p>
<div class="factbox"><dl><dt>Founded</dt><dd>February 1963, the Lakeshore Conference</dd><dt>Seat</dt><dd>The Administration, answering to the Charter</dd>
<dt>Field commander</dt><dd>Brigadier Hester Wrenfield</dd><dt>Colours</dt><dd>Working grey, olive, safety yellow</dd>
<dt>Plays like</dt><dd>Slow, armoured, very hard to dislodge. The best artillery on the continent.</dd></dl></div></div>""",
    runhead='Part II · The Factions'))

pages.append(page('right', 3, f"""{EMBLEM}
<h2>A Government of Clerks and Engineers</h2>
<div class="cols2">
<p>The Authority is governed by councils of the people who keep things running: grid engineers, rail dispatchers, agricultural
surveyors, county clerks. Each district elects a Reconstruction Board, the boards send delegates to the Administration, and the
Administration answers to a Charter that nobody is allowed to rewrite on a Tuesday. Decisions are slow, minuted and filed in
triplicate, and the Authority is proud of it. Slow is how it survived the First Winter without a single coup.</p>
<p>Its people are not united by a leader or a flag but by a ledger. Everyone is counted; everyone is owed a ration, a roof and a
hearing. The price is a labour year, served by every citizen in work crews, convoy service or the militia.</p>
<h3>The reluctant army</h3>
<p>The Authority does not want a war. It wants a working grid, a census that adds up, and fields it can plant. The Charter still
contains the clause that will dissolve the Authority when its work is done, and the soldiers know it.</p>
<p>That reluctance is also a weakness. The Authority will fortify, repair and wait long past the point where an enemy would have
attacked, and it will count the cost of every shell before it fires one.</p>
<h3>How the Authority fights</h3>
<p>The army is the labour year with rifles. Its doctrine is the surveyor's: find the ground, hold the ground, then improve the
ground. A base has a perimeter, a power grid and a road out. Armour goes forward behind sealed transports because ash kills
unprotected infantry, and every column carries clean water.</p>
</div>
<div class="slogan">COUNT THE LIVING · MEND THE LINE</div>
<h3 style="margin-top:2pt">Commander's notes <span style="color:#a03020;font-size:6.5pt;letter-spacing:.15em">· DRAFT TEXT</span></h3>
<p style="font-size:8pt">Build power before anything else and keep a surplus: every Authority structure draws on it. Your Refinery
is expensive (2000) but delivers a free Scraper. Defend with turrets and infantry while the Vehicle Factory comes up; the Heavy
Tank is the backbone of every column. Engineering Works opens tier 2, the Signals Headquarters tier 3.</p>""",
    runhead='The Continental Reconstruction Authority'))

# 4-5 unit pages
pages.append(page('left', 4, unit_full(cards['auth_tank'], 'auth_tank.png', 1.1) + unit_half(cards['auth_scraper'], 'auth_scraper.png'),
                  runhead='Authority · Field Guide: Vehicles'))
pages.append(page('right', 5, unit_half(cards['auth_rifleman'], 'auth_rifleman.png') + unit_half(cards['auth_rocketeer'], 'auth_rocketeer.png'), runhead='Authority · Field Guide: Infantry'))

# 6-7 tech tree spread (split so no box crosses the gutter)
uptable = ''.join(f'<tr><td>{n}</td><td>T{t} · {c}</td></tr>' for n, c, t in ups)
pages.append(page('left', 6, f"""<div class="kicker">Part III · Technology</div><h1 style="font-size:20pt">Authority Technology Tree</h1>
<p style="font-size:7.8pt;margin-top:4pt">Everything starts at the Construction Yard. Arrows point from a building to what it opens.
Units are listed under the building that trains them; numbers are credits. This page: the economy and the heavy branch.</p>
<div style="height:5.6in;margin-top:4pt;display:flex;justify-content:center"><img src="{img('tree_left.svg')}" style="max-width:100%;max-height:100%"></div>
<div class="legend"><span><i style="background:#dfe0c8"></i>Tier 1</span><span><i style="background:#efd9a0"></i>Tier 2 (Engineering Works)</span><span><i style="background:#e9bba4"></i>Tier 3 (Signals HQ)</span></div>""",
    runhead='Part III · Technology'))
pages.append(page('right', 7, f"""<h2>Infantry and Defences</h2>
<p style="font-size:7.8pt">The infantry branch hangs off the Power Plant. Dashed boxes are on the opposite page.</p>
<div style="margin:6pt 0 10pt;display:flex;justify-content:center"><img src="{img('tree_right.svg')}" style="width:100%"></div>
<div class="research" style="position:static;width:auto"><h4>Research · at any research building</h4><table>{uptable}</table></div>
<h3>Reading the tiers</h3>
<p style="font-size:7.8pt">Tier 2 opens when an Engineering Works stands; tier 3 when a Signals Headquarters stands. Lose the building
and the tier closes again until you rebuild it, so guard them. In the campaign, new tech is issued mission by mission as the
Administration releases it.</p>""", runhead='Authority · Technology Tree'))

# 8-9 world + maps
pages.append(page('left', 8, """<div class="kicker">Part I · The World</div><h2>Thirty-Nine Years After the Exchange</h2>
<p>For nine days in October 1962 a standoff that everyone expected to end at the brink did not. The war never had a surrender; it
simply ran out of things to throw. What was left was a continent of fires, broken power lines and a sky the colour of a bruise —
and, under it all, the grey drift the survivors learned to mine: <b>ashfall</b>.</p>
<div class="timeline">
<div class="ev"><b>Oct 1962</b>The Exchange. In nine days the major cities of the continent are struck.</div>
<div class="ev"><b>Nov 1962</b>A sealed government complex closes its blast doors from the inside. It calls itself the Interior Annex.</div>
<div class="ev"><b>1962–63</b>The First Winter. Engineers, utility crews and rail dispatchers keep towns alive by hand.</div>
<div class="ev"><b>Feb 1963</b>The Lakeshore Conference signs the Charter of Reconstruction. The Authority is born.</div>
<div class="ev"><b>1968</b>The first armoured scraper takes ash from a field; a centrifuge turns it into reactor fuel.</div>
<div class="ev"><b>1979</b>The Convoy Roads open across the Rust Belt.</div>
<div class="ev"><b>1985</b>The Voice begins the Reclamation broadcasts: the Directorate will take the surface back.</div>
<div class="ev"><b>1990</b>The Great Drift buries the Glass Coast.</div>
<div class="ev"><b>Spring 2000</b>A survey convoy on the Rust Belt road goes silent. The search party finds a hatch and a loudspeaker.</div>
<div class="ev"><b>2001</b>The Directorate surfaces openly. The war begins.</div>
</div>
<h3>The Authority campaign</h3>
<table class="ops">
<tr><td class="n">1</td><td><b>Survey Convoy</b><br>Two scrapers, one jeep, four riflemen and a pitchblende claim in the north-east. The scrapers are the mission.</td></tr>
<tr><td class="n">2</td><td><b>The Junction</b><br>All the grain and all the fuel cross one road junction. The relief column will not enter it open.</td></tr>
<tr><td class="n">3</td><td><b>The Coast Road</b><br>Get the survey parties to the Glass Coast before the window closes. The dunes are not empty.</td></tr>
<tr><td class="n">4</td><td><b>The Sunrise Device</b><br>An old weapon of the Exchange is awake on the south bank, and the countdown has started.</td></tr>
</table>""", runhead='Part I · The World'))

pages.append(page('right', 9, f"""<h2>The Four Theatres</h2>
<p>Every skirmish map is drawn from one of four theatres. Each has its own ground, its own resources and its own way of hurting
the careless. Read the ground before you build on it.</p>
<div class="maps">
<figure><img src="{img('map_rust_belt.png')}"><figcaption><b>Rust Belt</b> — a grid of dead towns and convoy roads, cut by one river. Fast lanes, many angles of attack.</figcaption></figure>
<figure><img src="{img('map_glass_coast.png')}"><figcaption><b>Glass Coast</b> — fused sand and open sea. The Expedition's home ground; watch the water.</figcaption></figure>
<figure><img src="{img('map_mountains.png')}"><figcaption><b>Mountains</b> — ridges and passes. Few routes, so chokepoints decide games.</figcaption></figure>
<figure><img src="{img('map_ash_plains.png')}"><figcaption><b>Ash Plains</b> — wide open drift. Rich fields, nowhere to hide.</figcaption></figure>
</div>
<h3>Reading a map</h3>
<p style="font-size:8pt">Red circles mark starting positions. Yellow is an ashfall field, green a rich pitchblende pit, magenta a
neutral ruin you can garrison. Small discs are rubble and wrecks that block movement; blue is water. Your first field is close to home; the richer ones are always where your enemy can reach
them too.</p>""", runhead='Part IV · Theatres of War'))

doc = f"<!doctype html><html lang='en'><head><meta charset='utf-8'><title>RTS_2027 Field Manual pilot</title><style>{CSS}</style></head><body>{''.join(pages)}</body></html>"
(B / 'booklet_pilot.html').write_text(doc)
print('pages', len(pages))
