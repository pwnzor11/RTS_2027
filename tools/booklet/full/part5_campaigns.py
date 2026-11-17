"""Part V, Campaigns (M14-05e). Mission names, acts, maps and tech lists are read from data/missions/<id>/mission.toml; text from part45_text.toml."""
import tomllib
from common import Page, ROOT, FULL, FACTIONS, FACTION_ORDER, E, title

T = tomllib.load(open(FULL / 'part45_text.toml', 'rb'))
RUN = 'Part V · Campaigns'
THEATRE = {'rustbelt_m2.map.toml': 'Rust Belt', 'glass_coast_e1.map.toml': 'Glass Coast', 'ash_reckoning.map.toml': 'Ash Plains'}

CSS = """<style>
.p5 p { font-size:8.8pt; }
.p5 ul, .p5 ol { margin:0 0 4pt 14pt; padding:0; font-size:8.6pt; } .p5 li { margin-bottom:2pt; }
.p5-lead { font-style:italic; font-size:9pt; }
.p5-box { border:1.2pt solid var(--rule); background:var(--tint); padding:4pt 7pt; margin-top:6pt; font-size:8.4pt; }
.p5-box p { margin:1pt 0 3pt; text-align:left; }
.p5 .ops td { font-size:8.4pt; }
.p5 .ops .meta { font-family:'TeX Gyre Heros Cn',sans-serif; font-size:6.8pt; letter-spacing:.06em; text-transform:uppercase; color:var(--muted); }
.p5-tab { width:100%; border-collapse:collapse; font-size:7.8pt; margin:2pt 0 4pt; }
.p5-tab td { border-bottom:.6pt solid #a99d7f; padding:2pt 3pt 2pt 0; vertical-align:top; }
.p5-tab td:first-child { font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; width:1.15in; }
</style>"""


def mission(mid):
    return tomllib.load(open(ROOT / f'data/missions/{mid}/mission.toml', 'rb'))


def ids(fac):
    return [f'{FACTIONS[fac]["prefix"]}0{i}' for i in range(1, 5)]


def names(tech):
    return ', '.join(title(t) for t in tech)


def tutorial_page():
    m = mission('tut_01'); t = T['tutorial']
    import re
    mp = (ROOT / 'data/maps' / m['mission']['map']).read_text()
    w = int(re.search(r'^width\s*=\s*(\d+)', mp, re.M).group(1)); h = int(re.search(r'^height\s*=\s*(\d+)', mp, re.M).group(1))
    me = m['players'][0]
    setup = t['setup'].format(w=w, h=h, cash=me['cash'], tech=names(me['tech']))
    lessons = ''.join(f'<li>{E(x)}</li>' for x in t['lessons'])
    body = CSS + f"""<div class="p5"><div class="kicker">Tutorial</div><h2>{E(m['mission']['title'])}</h2>
<p class="p5-lead">{E(t['title_note'])} {E(t['lead'])}</p>
<h3>You will learn to</h3><ol>{lessons}</ol>
<div class="p5-box"><p>{E(setup)}</p></div>
<p style="margin-top:6pt">{E(t['shared'])}</p></div>"""
    return Page(body, runhead=RUN, toc=RUN, draft=False)


def campaign_page(fac):
    f = FACTIONS[fac]
    rows = ''
    for i, mid in enumerate(ids(fac), 1):
        m = mission(mid)['mission']
        rows += (f'<tr><td class="n">{i}</td><td><b>{E(m["title"])}</b><br><span class="meta">{E(THEATRE.get(m["map"], ""))} · Act {m["act"]}</span><br>'
                 f'{E(T["teaser"][mid])}</td></tr>')
    body = CSS + f"""<div class="p5"><div class="kicker">Campaign</div><h2>{E(f['name'])}</h2>
<p class="p5-lead">{E(T['campaign'][fac]['premise'])}</p>
<h3>Missions</h3><table class="ops">{rows}</table></div>"""
    return Page(body, runhead=RUN, faction=fac, draft=False)


def tech_page():
    t = T['tech']
    how = ''.join(f'<li>{E(x)}</li>' for x in t['how'])
    rows = ''
    for fac in FACTION_ORDER:
        cum_ok = True
        ms = [mission(mid) for mid in ids(fac)]
        for a, b in zip(ms, ms[1:]):   # verify the claim "winning adds to the next mission's list"
            cum_ok &= set(a.get('unlocks_on_win', {}).get('tech', [])) <= set(b['starts_with']['tech'])
        assert cum_ok, fac
        for i, m in enumerate(ms[:3], 1):
            rows += (f'<tr><td>{E(FACTIONS[fac]["short"])} {i}<br><span style="font-weight:400">{E(m["mission"]["title"])}</span></td>'
                     f'<td>Starts with {len(m["starts_with"]["tech"])} items. Win to unlock: {E(names(m["unlocks_on_win"]["tech"]))}.</td></tr>')
    body = CSS + f"""<div class="p5"><div class="kicker">Campaign tech</div><h2>How Tech Is Issued</h2>
<p>{E(t['lead'])}</p><ul>{how}</ul>
<h3>Rewards, mission by mission</h3><table class="p5-tab">{rows}</table>
<p style="font-size:8pt"><i>{E(t['spoiler_note'])}</i></p></div>"""
    return Page(body, runhead=RUN, draft=False)


def pages():
    return [tutorial_page()] + [campaign_page(f) for f in FACTION_ORDER] + [tech_page()]
