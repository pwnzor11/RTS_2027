"""Part IV, Theatres of War (M14-05e). Text in part45_text.toml; map facts from data/mapgen/biomes.toml and the editor source."""
import re, tomllib
from common import Page, ROOT, FULL, E, img

T = tomllib.load(open(FULL / 'part45_text.toml', 'rb'))
BIOMES = tomllib.load(open(ROOT / 'data/mapgen/biomes.toml', 'rb'))['biome']
RUN = 'Part IV · Theatres of War'

CSS = """<style>
.p4-fig { width:2.7in; margin:0 auto 6pt; }
.p4-fig img { width:100%; image-rendering:pixelated; border:1.2pt solid var(--rule); display:block; }
.p4-fig figcaption { font-family:'TeX Gyre Heros Cn',sans-serif; font-size:6.8pt; line-height:1.2; margin-top:2pt; }
.p4-lead { font-style:italic; font-size:9pt; }
.p4-box { clear:both; border:1.2pt solid var(--rule); background:var(--tint); padding:4pt 7pt; margin-top:5pt; font-size:8pt; }
.p4-box b { font-family:'TeX Gyre Heros Cn',sans-serif; text-transform:uppercase; letter-spacing:.08em; font-size:7.5pt; }
.p4-box p { margin:1pt 0 3pt; text-align:left; }
.p4 p { font-size:8.8pt; }
.p4 ul { margin:0 0 4pt 12pt; padding:0; font-size:8.2pt; } .p4 li { margin-bottom:2pt; }
.p4-tab { width:100%; border-collapse:collapse; font-size:7.8pt; margin:2pt 0 4pt; }
.p4-tab td { border-bottom:.6pt solid #a99d7f; padding:1.6pt 3pt 1.6pt 0; vertical-align:top; }
.p4-tab td:first-child { font-family:'TeX Gyre Heros Cn',sans-serif; font-weight:700; width:.75in; white-space:nowrap; }
.p4-tab td.k { width:.35in; text-align:center; }
</style>"""


def _src(path, pat):
    return int(re.search(pat, (ROOT / path).read_text()).group(1))


def biome_page(key, first=False):
    b = T['biome'][key]
    vals = dict(BIOMES[key])
    ground = ''.join(f'<p>{E(s.format(**vals))}</p>' for s in b['ground'])
    body = (CSS if first else '') + f"""<div class="p4"><div class="kicker">Theatre</div><h2>{E(b['name'])}</h2>
<p class="p4-lead">{E(b['lead'])}</p>
<figure class="p4-fig"><img src="{img('tools/booklet/full/img/' + b['map'])}"><figcaption>{E(b['caption'])}</figcaption></figure>
<h3>What the ground does</h3>{ground}
<div class="p4-box"><b>Resources</b><p>{E(b['resources'].format(**vals))}</p></div>
<div class="p4-box"><b>What the theatre rewards</b><p>{E(b['rewards'])}</p></div></div>"""
    return Page(body, runhead=RUN, toc=RUN if first else None, draft=False)


def design_page():
    d = T['design']
    fv = dict(small=_src('src/Data/MapGenerator.cs', r'MapSize\.Small => (\d+)'), medium=_src('src/Data/MapGenerator.cs', r'MapSize\.Medium => (\d+)'),
              large=_src('src/Sim/Core/Constants.cs', r'MaxMapCells = (\d+)'),
              min_fields=_src('src/Data/MapValidator.cs', r'MinFieldsPerStart = (\d+)'), field_dist=_src('src/Data/MapValidator.cs', r'FieldPathDistance = (\d+)'))
    checks = ''.join(f'<li>{E(c.format(**fv))}</li>' for c in d['checks'])
    tips = ''.join(f'<li>{E(c)}</li>' for c in d['tips'])
    body = f"""<div class="p4"><div class="kicker">Map design</div><h2>How a Map Is Built</h2>
<p>{E(d['intro'])} {E(d['sizes'].format(**fv))}</p>
<h3>Fair by construction</h3><p>{E(d['fair'])} A new map must pass these checks:</p><ul>{checks}</ul>
<h3>Lanes, starts and expansions</h3>
<p><b>Lanes.</b> {E(d['lanes'])}</p><p><b>Starts.</b> {E(d['starts'])}</p><p><b>Expansions.</b> {E(d['expansions'])}</p>
<div class="p4-box"><b>Where the generator is going</b><p>{E(d['note'])}</p></div>
<h3>Reading a map</h3><p>{E(d['legend'])}</p><ul>{tips}</ul></div>"""
    return Page(body, runhead=RUN, draft=False)


def editor_page():
    e = T['editor']
    fv = dict(max_starts=_src('src/Game/Screens/Editor/EditorDocument.cs', r'MaxStarts = (\d+)'),
              brushes=', '.join(re.search(r'Sizes = \[([^\]]*)\]', (ROOT / 'src/Game/Screens/Editor/BrushShape.cs').read_text()).group(1).replace(' ', '').split(',')))
    tr = ''.join(f'<tr><td class="k">{E(k)}</td><td><b>{E(n)}</b> · {E(w.format(**fv))}</td></tr>' for k, n, w in e['tool_rows'])
    kr = ''.join(f'<tr><td>{E(k)}</td><td>{E(w.format(**fv))}</td></tr>' for k, w in e['keys'])
    body = f"""<div class="p4"><div class="kicker">Map editor</div><h2>The Map Editor</h2>
<p>{E(e['intro'])}</p><p>{E(e['view'])}</p>
<h3>Tools</h3><p style="margin-bottom:1pt">{E(e['tools_note'])}</p><table class="p4-tab">{tr}</table>
<h3>Keys</h3><table class="p4-tab">{kr}</table>
<h3>Symmetry</h3><p>{E(e['symmetry'])}</p><p>{E(e['save'])}</p></div>"""
    return Page(body, runhead=RUN, draft=False)


def pages():
    return [biome_page('rust_belt', True), biome_page('glass_coast'), biome_page('mountains'), biome_page('ash_plains'),
            design_page(), editor_page()]
