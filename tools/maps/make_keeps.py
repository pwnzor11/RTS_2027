#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Prints the [[keep]] blocks of a stamp plan for one map: every mission actor cell (radius 1) and every mission zone that is a spawn, landing,
base, cache or objective area, for each mission that uses the map. Usage: make_keeps.py <map id> (run from the repo root)."""
import glob, re, sys, tomllib

BASE_RE = re.compile(r"spawn|landing|stash|cache|survey|north_edge|home|enemy_base|base|redoubt|keep|device|beach|coast|shore|cove|wreck|field|mouth|gate|ford")
mid = sys.argv[1]
rects = set()
for f in sorted(glob.glob("data/missions/*/mission.toml")):
    t = tomllib.load(open(f, "rb"))
    if t["mission"]["map"] != mid + ".map.toml": continue
    for a in t.get("actors", []):
        if "cell" in a:
            x, y = a["cell"]; rects.add((x - 1, y - 1, x + 1, y + 1))
    for name, z in t.get("zones", {}).items():
        if BASE_RE.search(name): rects.add(tuple(z["rect"]))
for r in sorted(rects):
    print(f"[[keep]]\nrect = [{max(r[0],0)}, {max(r[1],0)}, {r[2]}, {r[3]}]")
