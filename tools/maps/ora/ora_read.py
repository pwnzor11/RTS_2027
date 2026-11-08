#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Read an OpenRA Red Alert .oramap (analysis only) and dump a per-cell grid of OUR terrain classes as JSON.

Study tool for docs/design/17_oramap_study.md. It never converts a map into our format and writes nothing next to the
input. Written from the public file-format description (map.yaml = MiniYaml, map.bin = BinaryDataHeader + tiles +
heights + resources); no OpenRA code is copied.

Usage:
    python3 ora_read.py MAP.oramap --tilesets DIR [--rules DIR] [-o out.json] [--no-grid]
    python3 ora_read.py --selfcheck MAP.oramap --tilesets DIR

--tilesets DIR  folder holding temperat.yaml / snow.yaml / desert.yaml / interior.yaml (fetch them from the OpenRA
                repository, mods/ra/tilesets/; do not commit them).
--rules DIR     optional folder with OpenRA rules yaml (decoration/civilian/misc/structures) used to read actor
                footprints; without it every actor is a 1x1 blocker.

Class letters in the grid (one char per cell, rows top to bottom):
    c clear   r road   g rough   w water (incl. river)   x cliff/rock/tree-terrain   s beach/shore   b bridge
"""
import argparse
import glob
import json
import os
import struct
import sys
import zipfile

CLASS_OF = {
    "clear": "c", "ore": "c", "gems": "c", "road": "r", "rough": "g", "water": "w", "river": "w",
    "rock": "x", "tree": "x", "wall": "x", "beach": "s", "bridge": "b",
}
PASSABLE = set("crgsb")  # ground-passable classes (foot and tracked alike for this study)


def parse_miniyaml(text):
    """Parse MiniYaml into a list of nodes (key, value, children). Tabs (or 4 spaces) indent; '#' starts a comment."""
    root = []
    stack = [(-1, root)]
    for raw in text.replace("\r\n", "\n").split("\n"):
        line = raw.rstrip()
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        depth = 0
        i = 0
        while i < len(line):
            if line[i] == "\t":
                depth += 1
                i += 1
            elif line[i:i + 4] == "    ":
                depth += 1
                i += 4
            else:
                break
        body = line[i:]
        if ":" in body:
            key, val = body.split(":", 1)
        else:
            key, val = body, ""
        node = (key.strip(), val.strip(), [])
        while stack and stack[-1][0] >= depth:
            stack.pop()
        stack[-1][1].append(node)
        stack.append((depth, node[2]))
    return root


def child(node, key):
    for k, v, c in node[2]:
        if k == key:
            return v
    return None


def load_tileset(path):
    """Return {template id: [terrain type lower-case per tile index (None where undefined)]}, size map."""
    with open(path, encoding="utf-8") as f:
        nodes = parse_miniyaml(f.read())
    templates = {}
    for k, v, c in nodes:
        if k != "Templates":
            continue
        for tk, tv, tc in c:
            tid = None
            size = (1, 1)
            tiles = {}
            for ck, cv, cc in tc:
                if ck == "Id":
                    tid = int(cv)
                elif ck == "Size":
                    a, b = cv.split(",")
                    size = (int(a), int(b))
                elif ck == "Tiles":
                    for ik, iv, _ in cc:
                        tiles[int(ik)] = iv.split()[0].lower() if iv else "clear"
            if tid is None:
                continue
            n = max(size[0] * size[1], (max(tiles) + 1) if tiles else 0)
            templates[tid] = [tiles.get(i) for i in range(n)]
    return templates


def load_footprints(rules_dir):
    """Return {actor id lower-case: (w, h, [(dx, dy) occupied cells])} from OpenRA rules yaml files."""
    out = {}
    if not rules_dir:
        return out
    for path in glob.glob(os.path.join(rules_dir, "rules_*.yaml")) + glob.glob(os.path.join(rules_dir, "*.yaml")):
        try:
            with open(path, encoding="utf-8") as f:
                nodes = parse_miniyaml(f.read())
        except OSError:
            continue
        for k, v, c in nodes:
            if k.startswith("^") or k.startswith("-"):
                continue
            for ck, cv, cc in c:
                if ck == "Building":
                    fp = child((ck, cv, cc), "Footprint")
                    dim = child((ck, cv, cc), "Dimensions")
                    if fp is None and dim is None:
                        continue
                    if dim:
                        w, h = (int(t) for t in dim.split(","))
                    else:
                        w, h = 1, 1
                    cells = []
                    if fp:
                        chars = fp.replace(" ", "")
                        for i, ch in enumerate(chars):
                            if ch not in "_":
                                cells.append((i % w, i // w))
                    else:
                        cells = [(0, 0)]
                    out[k.lower()] = (w, h, cells)
    for name in list(out):  # husks use the base tree/rock footprint
        pass
    return out


def category(t):
    t = t.lower()
    base = t.split(".")[0]
    if t == "mpspawn":
        return "spawn"
    if t == "mine":
        return "ore_well"
    if t == "gmine":
        return "gem_well"
    if base.startswith(("t0", "t1")) and base[1:].isdigit() or (base.startswith("tc") and base[2:].isdigit()):
        return "tree"
    if base.startswith("rock") or base in ("tanktrap1", "tanktrap2"):
        return "rock"
    if base.startswith("v") and base[1:].isdigit():
        return "civ_building"
    if base in ("oilb", "fcom", "hosp", "miss", "bio", "barr", "weap", "powr", "proc", "silo", "atek", "stek", "dome", "fix", "hpad", "afld", "pbox", "gun", "ftur", "tsla", "sam", "gap", "iron", "mslo", "pdox", "kenn", "syrd", "spen", "fact", "apwr", "tent", "agun", "fapw", "mcv"):
        return "tech_building"
    if base in ("fenc", "brik", "wood", "barb", "sbag", "cycl"):
        return "wall_fence"
    if base in ("brl3", "barl", "boxes01", "boxes02", "boxes03", "boxes04", "boxes05", "boxes06", "boxes07", "boxes08", "boxes09", "utilpol1", "utilpol2", "windmill", "lhus", "rice", "hay", "dtrk", "flare"):
        return "small_prop"
    if base.startswith(("v", "ice", "mnl", "mnr")) :
        return "civ_building"
    if base == "waypoint":
        return "waypoint"
    if base in ("c1", "c2", "c3", "c4", "c5", "c6", "c7", "c8", "c9", "c10", "c11", "4tnk", "1tnk", "2tnk", "3tnk", "jeep", "truk", "apc", "arty", "e1", "e2", "e3", "e4", "e6", "e7", "harv", "mcv", "mnly", "mgg", "mrj", "ftrk", "v2rl", "ttnk", "stnk", "ca", "dd", "ss", "pt", "lst", "mig", "yak", "heli", "hind", "tran", "badr", "u2", "mh60", "shok", "dog", "medi", "spy", "thf", "ant"):
        return "unit"
    if base in ("crate", "wcrate", "scrate"):
        return "crate"
    return "other"


def read_map(path, tilesets_dir, rules_dir=None, with_grid=True):
    with zipfile.ZipFile(path) as z:
        yaml_text = z.read("map.yaml").decode("utf-8", "replace")
        bin_data = z.read("map.bin")
    nodes = parse_miniyaml(yaml_text)
    top = {k: v for k, v, _ in nodes}
    w, h = (int(t) for t in top["MapSize"].split(","))
    bx, by, bw, bh = (int(t) for t in top.get("Bounds", f"0,0,{w},{h}").split(","))
    tileset = top.get("Tileset", "TEMPERAT").lower()
    tpath = os.path.join(tilesets_dir, tileset + ".yaml")
    templates = load_tileset(tpath)

    fmt = bin_data[0]
    bw_, bh_ = struct.unpack_from("<HH", bin_data, 1)
    if (bw_, bh_) != (w, h):
        raise ValueError("map.bin size does not match MapSize")
    if fmt == 1:
        tiles_off, heights_off, res_off = 5, 0, 3 * w * h + 5
    elif fmt == 2:
        tiles_off, heights_off, res_off = struct.unpack_from("<III", bin_data, 5)
    else:
        raise ValueError(f"unknown map.bin format {fmt}")

    cls = [["c"] * w for _ in range(h)]
    ttype = [[""] * w for _ in range(h)]
    if tiles_off:
        pos = tiles_off
        for x in range(w):          # file order: x outer, y inner
            for y in range(h):
                tid, idx = struct.unpack_from("<HB", bin_data, pos)
                pos += 3
                tmpl = templates.get(tid)
                t = None
                if tmpl is not None:
                    if idx == 255:
                        idx = x % 4 + (y % 4) * 4
                    t = tmpl[idx] if idx < len(tmpl) else None
                t = t or "clear"
                ttype[y][x] = t
                cls[y][x] = CLASS_OF.get(t, "c")
    res = []
    resgrid = {}
    if res_off:
        pos = res_off
        for x in range(w):
            for y in range(h):
                rt, rd = bin_data[pos], bin_data[pos + 1]
                pos += 2
                if rt:
                    res.append({"x": x, "y": y, "type": "ore" if rt == 1 else "gems" if rt == 2 else f"t{rt}", "density": rd})
                    resgrid[(x, y)] = rt
    heights = None
    if heights_off:
        heights = [[0] * w for _ in range(h)]
        pos = heights_off
        for x in range(w):
            for y in range(h):
                heights[y][x] = bin_data[pos]
                pos += 1

    foot = load_footprints(rules_dir)
    actors = []
    for k, v, c in nodes:
        if k != "Actors":
            continue
        for ak, av, ac in c:
            loc = child((ak, av, ac), "Location")
            if not loc:
                continue
            ax, ay = (int(t) for t in loc.split(","))
            typ = av.strip().lower()
            fp = foot.get(typ) or foot.get(typ.split(".")[0])
            cells = [[ax + dx, ay + dy] for dx, dy in (fp[2] if fp else [(0, 0)])]
            actors.append({"type": typ, "cat": category(typ), "x": ax, "y": ay,
                           "owner": child((ak, av, ac), "Owner") or "", "cells": cells})
    out = {
        "title": top.get("Title", top.get("Title ", "")), "author": top.get("Author", ""), "tileset": tileset.upper(),
        "size": [w, h], "bounds": [bx, by, bw, bh], "map_format": top.get("MapFormat", ""), "bin_format": fmt,
        "resources": res, "actors": actors,
        "class_counts": {},
    }
    counts = {}
    for row in cls:
        for ch in row:
            counts[ch] = counts.get(ch, 0) + 1
    out["class_counts"] = counts
    if with_grid:
        out["grid"] = ["".join(r) for r in cls]
        if heights is not None:
            out["heights"] = heights
    return out


def selfcheck(path, tilesets_dir):
    d = read_map(path, tilesets_dir)
    w, h = d["size"]
    assert len(d["grid"]) == h and all(len(r) == w for r in d["grid"]), "grid shape"
    assert sum(d["class_counts"].values()) == w * h, "class counts"
    spawns = [a for a in d["actors"] if a["cat"] == "spawn"]
    assert spawns, "no mpspawn found"
    bx, by, bw, bh = d["bounds"]
    for a in spawns:
        assert 0 <= a["x"] < w and 0 <= a["y"] < h, "spawn off map"
        assert d["grid"][a["y"]][a["x"]] in PASSABLE, f"spawn on impassable cell {a['x']},{a['y']}"
    print(f"selfcheck ok: {d['title']!r} {w}x{h} spawns={len(spawns)} classes={d['class_counts']} resources={len(d['resources'])}")


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("map")
    ap.add_argument("--tilesets", required=True)
    ap.add_argument("--rules")
    ap.add_argument("-o", "--out")
    ap.add_argument("--no-grid", action="store_true")
    ap.add_argument("--selfcheck", action="store_true")
    a = ap.parse_args(argv)
    if a.selfcheck:
        selfcheck(a.map, a.tilesets)
        return 0
    d = read_map(a.map, a.tilesets, a.rules, not a.no_grid)
    text = json.dumps(d, separators=(",", ":"))
    if a.out:
        with open(a.out, "w", encoding="utf-8") as f:
            f.write(text)
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
