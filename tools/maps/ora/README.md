# tools/maps/ora: OpenRA .oramap reader (analysis only)

`ora_read.py` reads an OpenRA Red Alert community map (`.oramap` = zip with `map.yaml` + `map.bin`) and prints JSON: size, bounds,
title and author, a per-cell grid of **our** terrain classes, resource cells (ore or gems plus density), and every actor
(spawns, ore and gem wells, trees, rocks, civilian and tech buildings, walls and fences, small props) with its footprint cells.

It exists to measure well-liked community maps for `docs/design/17_oramap_study.md`. **It never converts a map into our format,
and nothing it reads is committed**: the maps, their previews and the OpenRA tileset files stay in a scratch folder (community
maps have their own authors and licences; Red Alert tiles and names are EA's, D-050).

```
python3 -I tools/maps/ora/ora_read.py MAP.oramap --tilesets DIR --rules DIR -o out.json
python3 -I tools/maps/ora/ora_read.py --selfcheck MAP.oramap --tilesets DIR
```

* `--tilesets DIR`: folder with `temperat.yaml`, `snow.yaml`, `desert.yaml`, `interior.yaml` from the OpenRA repository
  (`mods/ra/tilesets/`, GPL v3). Download them yourself; do not commit them.
* `--rules DIR` (optional): folder with OpenRA `rules/*.yaml` (decoration, civilian, misc, structures), for actor footprints.
  Without it each actor is one cell.
* Grid letters: `c` clear (ore and gem cells count as clear), `r` road, `g` rough, `w` water and river, `x` cliff, rock and tree
  terrain, `s` beach, `b` bridge.
* `map.bin` layout: format byte (1 or 2), width and height as u16, then (format 2) three u32 offsets for tiles, heights and
  resources; tiles are u16 template id plus u8 index per cell, stored x-major; resources are u8 type plus u8 density.
* Python 3 standard library only. The study's metric and render scripts used numpy, scipy and PIL in scratch and are not part of
  the repo; the method is described in the design doc.
