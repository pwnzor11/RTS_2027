"""Field Manual full build (M14-05). Run from the repo root after tools/booklet/data/resolve_rules.py and techtree.py:
    python tools/booklet/full/book.py [--html-only]
Writes tools/booklet/full/out/RTS_2027_Field_Manual.pdf (and the quick-reference card PDF when card.py exists).
Owned by the lead. Part modules each expose pages() -> list[Page] (see common.py)."""
import importlib, pathlib, sys, traceback
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import FULL, Page, E

PARTS = [('front', 'Front matter'), ('part1_world', 'Part I · The World'), ('part2_basics', 'Part II · Getting Started'),
         ('part3_factions', 'Part III · The Factions'), ('part4_theatres', 'Part IV · Theatres of War'),
         ('part5_campaigns', 'Part V · Campaigns'), ('appendix', 'Appendices')]
TOC_MARK = '<!--TOC-->'


def notes_page():
    return Page('<h2>Notes</h2><div class="lines"></div>', extra='notes')


def collect(mods):
    pages = []
    for mod, label in mods:
        try:
            got = importlib.import_module(mod).pages()
        except ModuleNotFoundError as e:
            if e.name != mod: raise
            got = [Page(f'<div class="kicker">{E(label)}</div><div class="placeholder">This part is not built yet ({mod}.py).</div>', toc=label)]
        pages.extend(got)
    return pages


def number(pages, pad=True):
    out = []   # (page, folio)
    n = 1
    for p in pages:
        if p.left and n % 2 == 1:
            out.append((notes_page(), n)); n += 1
        out.append((p, n)); n += 1
    while pad and len(out) % 4: out.append((notes_page(), n)); n += 1   # printed booklets come in sheets of four pages
    return out


def section(p, n):
    side = 'left' if n % 2 == 0 else 'right'
    cls = f'page {side} {p.extra}' + (f' f-{p.faction}' if p.faction else '')
    rh = f'<div class="runhead">{E(p.runhead)}</div>' if p.runhead else ''
    dr = '<div class="draft">DRAFT TEXT · NOT YET APPROVED</div>' if p.draft else ''
    fo = '' if ('cover' in p.extra or 'nofolio' in p.extra) else f'<div class="folio">{n}</div>'
    return f'<section class="{cls}">{dr}{rh}{p.body}{fo}</section>'


def toc_html(numbered):
    rows = []
    for p, n in numbered:
        if not p.toc: continue
        part = p.toc.startswith('Part ') or p.toc in ('Appendices', 'Front matter')
        rows.append(f'<tr class="{"part" if part else ""}"><td>{E(p.toc)}</td><td class="p">{n}</td></tr>')
    return '<table class="toc">' + ''.join(rows) + '</table>'


def render(html_path, pdf_path):
    from playwright.sync_api import sync_playwright
    with sync_playwright() as pw:
        b = pw.chromium.launch()
        pg = b.new_page()
        pg.goto(html_path.as_uri(), wait_until='networkidle')
        pg.pdf(path=str(pdf_path), width='5.5in', height='8.5in', print_background=True, prefer_css_page_size=True)
        b.close()


def build(mods, name, html_only):
    numbered = number(collect(mods), pad=name.startswith("RTS_2027_Field"))
    toc = toc_html(numbered)
    body = ''.join(section(p, n) for p, n in numbered).replace(TOC_MARK, toc)
    css = (FULL / 'style.css').read_text()
    out = FULL / 'out'; out.mkdir(exist_ok=True)
    h = out / f'{name}.html'
    h.write_text(f"<!doctype html><html lang='en'><head><meta charset='utf-8'><title>{E(name)}</title><style>{css}</style></head><body>{body}</body></html>")
    if not html_only: render(h, out / f'{name}.pdf')
    drafts = sum(1 for p, _ in numbered if p.draft)
    print(f'{name}: {len(numbered)} pages ({drafts} with draft text) -> {out / (name + (".html" if html_only else ".pdf"))}')


if __name__ == '__main__':
    html_only = '--html-only' in sys.argv
    only = [a for a in sys.argv[1:] if not a.startswith('--')]   # e.g. book.py part2_basics: build one part alone for review
    mods = [m for m in PARTS if m[0] in only] + [(o, o) for o in only if o not in dict(PARTS)] if only else PARTS   # any module can be previewed alone
    build(mods, 'RTS_2027_Field_Manual' if not only else 'part_' + '_'.join(only), html_only)
    if not only and (FULL / 'card.py').exists(): build([('card', 'Quick-reference card')], 'RTS_2027_Quick_Reference', html_only)
