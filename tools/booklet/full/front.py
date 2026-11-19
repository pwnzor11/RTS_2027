"""Front matter (M14-05a): cover, inside cover, contents, how to use this manual."""
import pathlib, re
from common import Page, ROOT, E, img

STYLE = """<style>
.fr-ic h3 { margin-top:9pt; }
.fr-ic p, .fr-ic li { font-size:8.2pt; }
.fr-ic ul { margin:2pt 0 4pt; padding-left:12pt; }
.fr-fw { font-size:8.6pt; }
.fr-fw p { font-size:8.4pt; }
.fr-parts { width:100%; border-collapse:collapse; margin-top:3pt; }
.fr-parts td { vertical-align:top; padding:2.5pt 4pt 3pt 0; border-bottom:.6pt solid #a99d7f; font-size:7.8pt; }
.fr-parts td.n { font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; font-size:7.8pt; width:.95in; text-transform:uppercase; letter-spacing:.04em; color:var(--olive-d); }
</style>"""


def foreword():
    """The approved foreword 'Read Before Operating', verbatim from docs/design/19_field_manual.md section 6."""
    txt = (ROOT / 'docs/design/19_field_manual.md').read_text()
    m = re.search(r'\*\*Read Before Operating[^*]*\*\*\s*(.+?)\n\s*\n', txt, re.S)
    return ' '.join(m.group(1).split())


def pages():
    cover = Page(f"""<div class="art" style="background-image:url({img('data/images/stills/still_authority_act4.png', 1600, True)});background-position:50% 50%"></div><div class="shade"></div>
<div class="top"><div class="logo">RTS<span>·</span>2027</div><div class="tag">Ashfall · The War for the Surface</div></div>
<div class="bottom"><div class="manual">Field Manual</div><div class="sub">Operations handbook for commanders of the Authority, the Directorate, the Left Behind and the Overseas Expedition</div>
<div class="stripe"></div><div class="fine"><span>Open source · GPL v3</span><span>Windows</span></div></div>""",
                 extra='cover', toc='Front matter')

    inside = Page(STYLE + """<div class="fr-ic"><div class="kicker">Front matter</div><h2>About This Edition</h2>
<h3>Licence</h3>
<p>RTS_2027 is free software under the GNU General Public License, version 3. You may play it, copy it, study it and change
it, and you may pass it on under the same licence. The full text is in the file <b>LICENSE</b> that comes with the game.</p>
<h3>Open source forever</h3>
<p>The game and its source code are open and will stay open. This manual is part of the project.</p>
<h3>Platform</h3>
<p>Windows.</p>
<h3>Credits</h3>
<p>Game design, direction and approval of all lore: the project owner. Code, art, music, sound, voices and text were made
with AI assistance (Claude and the tools listed in the credits file). The cover and chapter paintings are rendered from
scenes written in code with Blender. Music and voices are generated and released without rights claimed.</p>
<p>The game is built on MonoGame, SDL, OpenAL Soft, NVorbis, Tomlyn and MoonSharp. Some effects and props come from
Skorpio, Sharm, Kenney, the Liberated Pixel Cup artists and the 0 A.D. project, each under its own free licence.
Every asset, author and licence is listed in <b>docs/CREDITS.md</b>.</p>
<h3>Names</h3>
<p>All factions, places and people in this book are invented. The game claims no affiliation with any other game or company.</p>
</div>""", runhead='Front matter', draft=False)

    contents = Page('<h2>Contents</h2><!--TOC-->', runhead='Front matter')

    parts = [('I · The World', 'How the war came about: the Exchange, the Charter, the Annex, ashfall, a timeline and a glossary.'),
             ('II · Getting Started', 'Running the game, the menus, the screen, production, orders and the rules of the economy.'),
             ('III · The Factions', 'One chapter for each of the four powers: society, leaders, units, structures, upgrades and tech tree.'),
             ('IV · Theatres of War', 'The four biomes, how maps are built, and how to make your own.'),
             ('V · Campaigns', 'The tutorial and one page per campaign: premise and mission list.'),
             ('Appendices', 'The counter chart, keyboard reference, full glossary, credits and licences.')]
    rows = ''.join(f'<tr><td class="n">{E(a)}</td><td>{E(b)}</td></tr>' for a, b in parts)
    how = Page(STYLE + f"""<div class="kicker">Front matter</div><h2>How to Use This Manual</h2>
<h3>Read Before Operating</h3>
<div class="fr-fw"><p>{E(foreword())}</p></div>
<h3>Guide to the parts</h3>
<p>Plain rules and numbers are set in the manual's plain type. Lore, briefings and field notes are set in the voice of the
faction that wrote them. Every number comes from the game data of this build.</p>
<table class="fr-parts">{rows}</table>""", runhead='Front matter', toc='How to use this manual', draft=False)
    return [cover, inside, contents, how]
