# Field Manual (game booklet) tools

Design and page plan: `docs/design/19_field_manual.md`. Everything on a stat or tech-tree page is generated from `data/`.

Run from the repo root (Python 3.12+, Pillow, Graphviz `dot`, Playwright with Chromium; on Windows `pip install pillow playwright`,
`playwright install chromium`, Graphviz from graphviz.org):

1. `python tools/booklet/data/resolve_rules.py` and `python tools/booklet/data/techtree.py` (rules with inheritance applied, tech trees).
2. Map previews: `dotnet run --project src/Tools -c Release -- map render --biome <b> --size m --players 2 --seed 3 --layout 1 --png tools/booklet/pilot/img/map_<b>.png --scale 4`
   for rust_belt, glass_coast, mountains, ash_plains.
3. `python tools/booklet/pilot/prep.py`, `python tools/booklet/pilot/tree.py`, `python tools/booklet/pilot/build.py`,
   `python tools/booklet/pilot/render.py` -> `tools/booklet/pilot/out/RTS_2027_Field_Manual_pilot.pdf`.

Hero renders of every unit and building (owner decision, session 18): `tools/booklet/render_heroes.ps1 -Pilot` first (12 renders),
then `-All`; outputs go to `art/booklet/<faction>/<id>.png`. See the header of the script.
