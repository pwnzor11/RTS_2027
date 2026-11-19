"""Field Manual full build (M14-05): shared pieces for every part module. Owned by the lead; part modules import from here only.

Contract (docs in tools/booklet/full/COMMON.md):
  Page(body, runhead='', toc=None, draft=False, extra='', left=False, faction=None)
    body     inner HTML of one half-letter page (no <section>; book.py wraps it, adds runhead, folio and the draft stamp)
    runhead  running head text (small caps line at the top)
    toc      a line for the contents page (part and chapter openers only), or None
    draft    True when the page carries new text the owner has not approved yet (prints the red DRAFT stamp)
    extra    extra CSS classes on the page section (e.g. 'cover', 'nofolio')
    left     True: this page must be a LEFT page (even folio), e.g. the first page of a two-page spread; book.py inserts a
             notes page before it when needed
    faction  'authority' | 'directorate' | 'left_behind' | 'expedition' | None: sets the page's accent colours
"""
import base64, html, hashlib, io, json, pathlib, re, tomllib
from dataclasses import dataclass
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[3]          # repo root
FULL = pathlib.Path(__file__).resolve().parent              # tools/booklet/full
CACHE = FULL / 'cache'                                      # git-ignored, resized images
E = html.escape


@dataclass
class Page:
    body: str
    runhead: str = ''
    toc: str | None = None
    draft: bool = False
    extra: str = ''
    left: bool = False
    faction: str | None = None


# ---- faction table (accents chosen by the owner, session 32) --------------------------------------------------------------
FACTIONS = {
    'authority':   {'name': 'The Continental Reconstruction Authority', 'short': 'Authority', 'prefix': 'auth_',
                    'accent': '#55633a', 'accent_d': '#3b4628', 'second': '#d99a1e'},
    'directorate': {'name': 'The Directorate of the Interior', 'short': 'Directorate', 'prefix': 'dir_',
                    'accent': '#6e1f1f', 'accent_d': '#4a1414', 'second': '#8e2a2a'},
    'left_behind': {'name': 'The Left Behind', 'short': 'Left Behind', 'prefix': 'lb_',
                    'accent': '#a04a1c', 'accent_d': '#6e3212', 'second': '#3f7c7a'},
    'expedition':  {'name': 'The Overseas Expedition', 'short': 'Expedition', 'prefix': 'exp_',
                    'accent': '#22344f', 'accent_d': '#152238', 'second': '#b08d3c',
                    'house': '#d0702a'},
}
FACTION_ORDER = ['authority', 'directorate', 'left_behind', 'expedition']


# ---- images --------------------------------------------------------------------------------------------------------------
GRADE_VERSION = 1


def img(path, max_px=900, jpeg=False):
    """Data URI for a repo-relative (or absolute) image path, downscaled so its longer side is at most max_px (cached).
    SVG files are embedded as they are. jpeg=True for full-bleed paintings (smaller PDF); sprites and renders stay PNG."""
    p = pathlib.Path(path)
    if not p.is_absolute(): p = ROOT / p
    if p.suffix == '.svg':
        return 'data:image/svg+xml;base64,' + base64.b64encode(p.read_bytes()).decode()
    grade = _grade_for(p)
    key = hashlib.sha1(f'{p}:{p.stat().st_mtime_ns}:{max_px}:{jpeg}:{grade}:{GRADE_VERSION}'.encode()).hexdigest()[:16]
    CACHE.mkdir(exist_ok=True)
    out = CACHE / (key + ('.jpg' if jpeg else '.png'))
    if not out.exists():
        im = Image.open(p)
        if max(im.size) > max_px:
            s = max_px / max(im.size)
            im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
        if grade == 'paint':
            import grade as G; im = G.paint(im)
        elif grade:
            import grade as G; im = G.house(im, grade)
        if jpeg: im.convert('RGB').save(out, 'JPEG', quality=86)
        else: im.save(out, 'PNG', optimize=True)
    mime = 'image/jpeg' if jpeg else 'image/png'
    return f'data:{mime};base64,' + base64.b64encode(out.read_bytes()).decode()


def _grade_for(p):
    """Booklet colour grade for an image: 'paint' for the painted stills, a hex colour for hero renders whose house panels are
    magenta (the faction's display colour), None otherwise."""
    parts = p.parts
    if 'stills' in parts: return 'paint'
    if 'booklet' in parts and p.parent.name in FACTIONS: return FACTIONS[p.parent.name].get('house')
    return None


def hero(uid):
    """Path of the hero render for a rules id (art/booklet/<faction>/<id>.png), or None."""
    for d in (ROOT / 'art' / 'booklet').iterdir():
        p = d / f'{uid}.png'
        if d.is_dir() and p.exists(): return p
    return None


# ---- data ----------------------------------------------------------------------------------------------------------------
_rules = None
def rules():
    """tools/booklet/data/rules_resolved.json (run tools/booklet/data/resolve_rules.py first): units, buildings, upgrades,
    weapons, warheads with inheritance applied as the loaders do."""
    global _rules
    if _rules is None: _rules = json.load(open(ROOT / 'tools/booklet/data/rules_resolved.json'))
    return _rules


def techtree(faction):
    """tools/booklet/data/techtree_<faction>.json (run tools/booklet/data/techtree.py first)."""
    return json.load(open(ROOT / f'tools/booklet/data/techtree_{faction}.json'))


_archive = None
def archive():
    """Every data/archive/*.toml [[entry]] by id (title, subtitle, body, field_note, ...). Display names always come from here."""
    global _archive
    if _archive is None:
        _archive = {}
        for f in sorted((ROOT / 'data/archive').glob('*.toml')):
            for e in tomllib.load(open(f, 'rb')).get('entry', []): _archive[e['id']] = e
    return _archive


def title(uid):
    """Display name of a rules id (the Archive title), else the id."""
    return archive().get(uid, {}).get('title', uid)


def paras(text):
    """Plain text with blank-line paragraphs -> <p> HTML (escaped)."""
    return ''.join(f'<p>{E(" ".join(p.split()))}</p>' for p in re.split(r'\n\s*\n', text.strip()) if p.strip())
