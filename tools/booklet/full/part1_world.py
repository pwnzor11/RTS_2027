"""Part I · The World (M14-05a): 8 pages. Approved lore is read from data/archive at build time; only the glossary page
and its captions are new text (draft=False)."""
import re, tomllib
from common import Page, FACTIONS, FACTION_ORDER, FULL, E, archive, img, paras

CSS = """<style>
.p1-strip { position:relative; height:3.0in; margin:-.5in -.45in 8pt -.45in; background-size:cover; background-position:50% 55%; }
.p1-strip::after { content:''; position:absolute; inset:0; background:linear-gradient(180deg, transparent 55%, var(--paper) 100%); }
.p1-big p { font-size:9.8pt; line-height:1.42; margin-bottom:7pt; }
.p1-big h2 { font-size:16pt; }
.p1-sub { font-family:'TeX Gyre Heros Cn',sans-serif; font-size:8pt; letter-spacing:.12em; text-transform:uppercase; color:var(--muted); margin:-3pt 0 8pt; }
.p1-dates { margin-top:10pt; }
.p1-dates .timeline { margin-left:.86in; }
.p1-dates .timeline .ev { font-size:8.4pt; margin-bottom:6pt; }
.p1-dates .timeline .ev b { left:-1.08in; width:.84in; }
.p1-tl .timeline .ev { font-size:7.7pt; margin-bottom:3.6pt; line-height:1.28; }
.p1-fc { border:1.2pt solid var(--rule); background:var(--tint); margin:0 0 9pt; display:flex; }
.p1-fc .bar2 { width:.2in; flex:none; }
.p1-fc .in { padding:4pt 7pt 3pt; }
.p1-fc h3 { margin:0; font-size:10.5pt; }
.p1-fc .st { font-family:'TeX Gyre Heros Cn',sans-serif; font-size:7pt; letter-spacing:.1em; text-transform:uppercase; color:var(--muted); margin-bottom:2pt; }
.p1-fc p { font-size:8.7pt; line-height:1.36; margin:0 0 3pt; }
.p1-gl { column-count:2; column-gap:.2in; column-rule:.5pt solid #b9ad8f; margin-top:3pt; }
.p1-gl div { break-inside:avoid; margin-bottom:2.6pt; font-size:7.1pt; line-height:1.2; }
.p1-gl b { font-family:'TeX Gyre Heros Cn',sans-serif; text-transform:uppercase; letter-spacing:.05em; font-size:7.6pt; display:block; color:var(--olive-d); }
</style>"""


def timeline_events():
    """[(date, text)] from the Archive history_timeline entry, verbatim."""
    body = archive()['history_timeline']['body']
    return re.findall(r'^\*([^*]+?)\.\*\s+(.+)$', body, re.M)


def tl(dates, cls='p1-dates'):
    ev = {d: t for d, t in timeline_events()}
    rows = ''.join(f'<div class="ev"><b>{E(d)}</b>{E(ev[d])}</div>' for d in dates)
    return f'<div class="{cls}"><div class="timeline">{rows}</div></div>'


def chapter(uid, still, dates, toc, runhead='Part I · The World', first=False, kicker='Part I · The World', pos='50% 55%', size='cover'):
    e = archive()[uid]
    strip = f'<div class="p1-strip" style="background-image:url({img(f"data/images/stills/{still}", 1100, True)});background-position:{pos};background-size:{size}"></div>'
    return Page((CSS if first else '') + strip +
                f'<div class="p1-big"><div class="kicker">{E(kicker)}</div><h2>{E(e["title"])}</h2>'
                f'<div class="p1-sub">{E(e["subtitle"])}</div>{paras(e["body"])}</div>' + tl(dates),
                runhead=runhead, toc=toc)


def first_sentences(text, max_words=85):
    para = re.split(r'\n\s*\n', text.strip())[0]
    para = ' '.join(para.split())
    out, n = [], 0
    for s in re.split(r'(?<=[.!?])\s+', para):
        w = len(s.split())
        if out and n + w > max_words: break
        out.append(s); n += w
    return ' '.join(out)


def pages():
    A = archive()
    out = []
    out.append(chapter('history_the_exchange', 'still_authority_act2.png', ['Oct 1962'], 'Part I · The World', first=True))
    out.append(chapter('history_first_winter', 'still_authority_act3.png', ['Winter 1962-63'], 'The First Winter', pos='50% 70%'))
    out.append(chapter('history_the_charter', 'still_authority_act2.png', ['Feb 1963', '1979'], 'The Charter of Reconstruction', pos='8% 80%', size='230%'))
    out.append(chapter('history_the_annex', 'still_directorate_act2.png', ['Nov 1962', '1971', '1983', '1994'], 'The Interior Annex'))
    out.append(chapter('history_ashfall_decades', 'still_authority_act3.png', ['1968', '1990'], 'The Ashfall Decades', pos='90% 30%', size='200%'))

    # 6: timeline (all 15 Archive entries, verbatim) + First Contact
    allev = [d for d, _ in timeline_events()]
    fc = A['history_first_contact']
    out.append(Page(f'<div class="kicker">Part I · The World</div><h2>Timeline · 1962 to 2001</h2>'
                    + '<div class="p1-tl">' + tl(allev, 'p1-t') + '</div>'
                    + f'<h3>{E(fc["title"])}</h3><p style="font-size:7.8pt">{E(" ".join(fc["body"].split()))}</p>',
                    runhead='Part I · The World', toc='Timeline 1962 to 2001'))

    # 7: four powers
    srcs = {'authority': 'faction_auth_society', 'directorate': 'faction_dir_bunker',
            'left_behind': 'faction_lb_abandonment', 'expedition': 'faction_exp_origin'}
    cards = ''
    for f in FACTION_ORDER:
        F, e = FACTIONS[f], A[srcs[f]]
        cards += (f'<div class="p1-fc" style="--olive-d:{F["accent_d"]}"><div class="bar2" style="background:repeating-linear-gradient(45deg,{F["accent"]} 0 5pt,{F["second"]} 5pt 10pt)"></div>'
                  f'<div class="in"><h3 style="color:{F["accent"]}">{E(F["name"])}</h3><div class="st">{E(e["title"])} · {E(e["subtitle"])}</div>'
                  f'<p>{E(first_sentences(e["body"]))}</p></div></div>')
    out.append(Page(f'<div class="kicker">Part I · The World</div><h2>The Four Powers at a Glance</h2>{cards}',
                    runhead='Part I · The World', toc='The four powers at a glance'))

    # 8: glossary (new text)
    T = tomllib.load(open(FULL / 'part1_world_text.toml', 'rb'))
    terms = ''.join(f'<div><b>{E(t["t"])}</b>{E(t["d"])}</div>' for t in T['term'])
    out.append(Page(f'<div class="kicker">Part I · The World</div><h2>Glossary of the Setting</h2>'
                    f'<p style="font-size:7.8pt">{E(T["glossary_intro"]["text"])}</p><div class="p1-gl">{terms}</div>',
                    runhead='Part I · The World', toc='Glossary of the setting', draft=False))
    return out
