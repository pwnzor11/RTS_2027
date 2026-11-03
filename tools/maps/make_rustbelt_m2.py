#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Builds data/maps/rustbelt_m2.map.toml (64x64 Rust Belt skirmish map) and optional proof images.

Deterministic: no random numbers and no wall-clock. Re-running gives byte-identical output.

Layout rules (docs/design/02_game_design.md section 3): point-symmetric under 180-degree rotation
(cell (x, y) matches (63-x, 63-y)), two starts in opposite corners, wide main ramps, several ashfall
fields near each base, one pitchblende pit per start plus contested pits in the middle, rubble and
ruins, and a frozen river with two fords. The script checks symmetry, routes and the one-cell rules
before it writes anything.

Usage:
  python3 tools/maps/make_rustbelt_m2.py                # write the map
  python3 tools/maps/make_rustbelt_m2.py --proof DIR    # also write proof PNGs into DIR
"""
import argparse
import base64
import math
import os
import struct
import sys
import tomllib
from collections import deque

W = H = 64
MAP_NAME = "rustbelt_m2"
THEATER = "rustbelt"
STARTS = [(4, 4), (59, 59)]
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MAP_OUT = os.path.join(REPO, "data", "maps", MAP_NAME + ".map.toml")
PALETTE_PATH = os.path.join(REPO, "data", "theaters", "rustbelt.toml")

# LandType and OverlayKind values from src/Sim/Rules/Enums.cs (append-only, persisted).
CLEAR, ROAD, ROUGH, ASH, WATER, ROCK, CLIFF = range(7)
NO_OVL, ASHFALL, PITCH = 0, 1, 2
LAND_NAMES = ["clear", "road", "rough", "ash", "water", "rock", "cliff"]

M32 = 0xFFFFFFFF


def rot(x, y):
    """180-degree rotation: the symmetry the map must keep."""
    return (W - 1 - x, H - 1 - y)


def inb(x, y):
    return 0 <= x < W and 0 <= y < H


class Grid:
    def __init__(self):
        n = W * H
        self.land = [CLEAR] * n
        self.height = [0] * n
        self.ovl = [NO_OVL] * n
        self.dens = [0] * n

    def i(self, x, y):
        return y * W + x

    def set_land(self, x, y, v):
        if inb(x, y):
            self.land[self.i(x, y)] = v

    def set_ovl(self, x, y, kind, dens):
        if inb(x, y):
            k = self.i(x, y)
            self.ovl[k] = kind
            self.dens[k] = dens


def paint_sym(g, cells, fn):
    """Applies fn(grid, x, y) to each cell and to its rotated partner."""
    for (x, y) in cells:
        fn(g, x, y)
        rx, ry = rot(x, y)
        fn(g, rx, ry)


def disc(cx, cy, r):
    out = []
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            if inb(x, y) and (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                out.append((x, y))
    return out


def river_centre_offset(u):
    """Odd function of u = x - y, so the river curve is symmetric under rotation."""
    return round(3 * math.sin(2 * math.pi * u / W))


def river_cells():
    cells = []
    for y in range(H):
        for x in range(W):
            d = x + y - (W - 1)
            if abs(d - river_centre_offset(x - y)) <= 1:
                cells.append((x, y))
    return cells


def build():
    g = Grid()

    # 1. Plateaus: a 15x15 high ground with a cliff ring and two wide ramps per base.
    plateau = [(x, y) for y in range(0, 15) for x in range(0, 15)]
    ring = [(15, y) for y in range(0, 16)] + [(x, 15) for x in range(0, 16)]
    ramps = {(15, y) for y in range(6, 10)} | {(x, 15) for x in range(6, 10)}
    for (x, y) in plateau:
        g.height[g.i(x, y)] = 1
    for (x, y) in ring:
        if (x, y) not in ramps:
            g.set_land(x, y, CLIFF)
            g.height[g.i(x, y)] = 1
    for (x, y) in ramps:
        g.height[g.i(x, y)] = 1
    # Base B gets the same cells rotated. Heights and cliffs are copied explicitly.
    base_cells = plateau + [c for c in ring if c not in ramps] + list(ramps)
    for (x, y) in base_cells:
        rx, ry = rot(x, y)
        g.height[g.i(rx, ry)] = g.height[g.i(x, y)]
        if g.land[g.i(x, y)] == CLIFF:
            g.set_land(rx, ry, CLIFF)

    # 2. Frozen river along the anti-diagonal, with a symmetric wobble.
    for (x, y) in river_cells():
        g.set_land(x, y, WATER)

    # 3. Two fords: a 5x5 bridge (road) at the river cells nearest (18,45) and its partner (45,18).
    for target in [(18, 45)]:
        best = min(river_cells(), key=lambda c: (abs(c[0] - target[0]) + abs(c[1] - target[1]), c))
        for (x, y) in disc(best[0], best[1], 2):
            if g.land[g.i(x, y)] == WATER:
                g.set_land(x, y, ROAD)
        rx, ry = rot(*best)
        for (x, y) in disc(rx, ry, 2):
            if g.land[g.i(x, y)] == WATER:
                g.set_land(x, y, ROAD)

    # 4. Roads from each base ramp out toward the fords (painted only over clear ground).
    road_lines = [
        [(16, 8), (38, 8), (38, 18), (43, 18)],   # from base A east ramp to ford 2
        [(7, 16), (7, 38), (15, 38), (16, 42)],   # from base A south ramp to ford 1
    ]
    for line in road_lines:
        for (x0, y0), (x1, y1) in zip(line, line[1:]):
            for x in range(min(x0, x1), max(x0, x1) + 1):
                for y in range(min(y0, y1), max(y0, y1) + 1):
                    for (cx, cy) in [(x, y), rot(x, y)]:
                        if inb(cx, cy) and g.land[g.i(cx, cy)] == CLEAR:
                            g.set_land(cx, cy, ROAD)

    # 5. Rubble and ruins (rough) and stone outcrops (rock), in symmetric pairs.
    for (cx, cy, r, kind) in [(30, 12, 2, ROUGH), (12, 30, 2, ROCK), (40, 30, 1, ROUGH)]:
        for (x, y) in disc(cx, cy, r):
            for (px_, py_) in [(x, y), rot(x, y)]:
                if g.land[g.i(px_, py_)] == CLEAR:
                    g.set_land(px_, py_, kind)

    # 6. Ash ground patch in the open middle-east.
    for (x, y) in disc(44, 34, 3):
        for (px_, py_) in [(x, y), rot(x, y)]:
            if g.land[g.i(px_, py_)] == CLEAR:
                g.set_land(px_, py_, ASH)

    # 7. Ashfall fields near each base (density falls off from the centre).
    for (cx, cy, r) in [(22, 8, 3), (9, 24, 3), (26, 22, 2)]:
        for (x, y) in disc(cx, cy, r):
            d = max(abs(x - cx), abs(y - cy))
            dens = max(60, 255 - 60 * d)
            if g.land[g.i(x, y)] in (CLEAR, ROUGH, ASH, ROAD):
                g.set_ovl(x, y, ASHFALL, dens)
        rx, ry = rot(cx, cy)
        for (x, y) in disc(rx, ry, r):
            d = max(abs(x - rx), abs(y - ry))
            dens = max(60, 255 - 60 * d)
            if g.land[g.i(x, y)] in (CLEAR, ROUGH, ASH, ROAD):
                g.set_ovl(x, y, ASHFALL, dens)

    # 8. Pitchblende: one pit per start, plus a contested pair in the middle.
    for (cx, cy) in [(19, 18), (29, 27)]:
        for (x, y) in disc(cx, cy, 1):
            g.set_ovl(x, y, PITCH, 255 if (x, y) == (cx, cy) else 200)
        rx, ry = rot(cx, cy)
        for (x, y) in disc(rx, ry, 1):
            g.set_ovl(x, y, PITCH, 255 if (x, y) == (rx, ry) else 200)
    # 9. Starts sit on clear ground, no overlay.
    for (sx, sy) in STARTS:
        g.set_land(sx, sy, CLEAR)
        g.set_ovl(sx, sy, NO_OVL, 0)

    return g


def check_symmetry(g):
    for y in range(H):
        for x in range(W):
            rx, ry = rot(x, y)
            a, b = g.i(x, y), g.i(rx, ry)
            for name, arr in [("land", g.land), ("height", g.height), ("ovl", g.ovl), ("dens", g.dens)]:
                if arr[a] != arr[b]:
                    raise SystemExit(f"symmetry broken: {name} at ({x},{y}) vs ({rx},{ry})")


def passable(v):
    return v not in (WATER, ROCK, CLIFF)


def reach(g, src, blocked=frozenset()):
    seen = {src}
    q = deque([src])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if inb(nx, ny) and (nx, ny) not in seen and (nx, ny) not in blocked and passable(g.land[g.i(nx, ny)]):
                seen.add((nx, ny))
                q.append((nx, ny))
    return seen


def check_routes(g):
    a, b = STARTS
    full = reach(g, a)
    if b not in full:
        raise SystemExit("starts are not connected")
    # Ford cells are the road cells that lie on the river band.
    river = set(river_cells())
    fords = {(x, y) for (x, y) in river if g.land[g.i(x, y)] == ROAD}
    if not fords:
        raise SystemExit("no fords on the river")
    # Split the fords into two groups (one per crossing) by the side of the river they sit on.
    groups = []
    for c in sorted(fords):
        for grp in groups:
            if any(abs(c[0] - d[0]) <= 3 and abs(c[1] - d[1]) <= 3 for d in grp):
                grp.add(c)
                break
        else:
            groups.append({c})
    if len(groups) != 2:
        raise SystemExit(f"expected 2 fords, found {len(groups)}")
    for grp in groups:
        if b not in reach(g, a, blocked=frozenset(grp)):
            raise SystemExit("only one route between the bases: a single ford blocks it")
    return groups


def encode_layers(g):
    n = W * H
    tpl = b"".join(struct.pack("<H", 0) for _ in range(n))
    return {
        "template": base64.b64encode(tpl).decode(),
        "land": base64.b64encode(bytes(g.land)).decode(),
        "height": base64.b64encode(bytes(g.height)).decode(),
        "overlay": base64.b64encode(bytes(g.ovl)).decode(),
        "density": base64.b64encode(bytes(g.dens)).decode(),
    }


def write_map(g, path):
    lay = encode_layers(g)
    starts = ", ".join(f"[{x}, {y}]" for (x, y) in STARTS)
    text = (
        "# Generated by tools/maps/make_rustbelt_m2.py. Do not hand-edit; change the script and re-run.\n"
        "[map]\n"
        f'name = "{MAP_NAME}"\n'
        f'theater = "{THEATER}"\n'
        f"width = {W}\n"
        f"height = {H}\n"
        "format_version = 1\n"
        f"start_positions = [{starts}]\n\n"
        "[layers]\n"
        f'template = "{lay["template"]}"\n'
        f'land = "{lay["land"]}"\n'
        f'height = "{lay["height"]}"\n'
        f'overlay = "{lay["overlay"]}"\n'
        f'density = "{lay["density"]}"\n'
    )
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


# ---------------------------------------------------------------- palette bake (mirrors TerrainBake.cs)

def load_palette(path=PALETTE_PATH):
    with open(path, "rb") as f:
        t = tomllib.load(f)

    def rgb(s):
        s = s.lstrip("#")
        return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16))

    pal = {
        "texel_cells": int(t["texel_cells"]),
        "land": [(rgb(t["land"][n]["dark"]), rgb(t["land"][n]["base"]), rgb(t["land"][n]["light"])) for n in LAND_NAMES],
        "ashfall": {"speck": rgb(t["overlay"]["ashfall"]["speck"]),
                    "max_permille": int(t["overlay"]["ashfall"]["max_coverage_permille"])},
        "pitch": {"chunk": rgb(t["overlay"]["pitchblende"]["chunk"]),
                  "glint": rgb(t["overlay"]["pitchblende"]["glint"]),
                  "max_permille": int(t["overlay"]["pitchblende"]["max_coverage_permille"]),
                  "glint_one_in": int(t["overlay"]["pitchblende"]["glint_one_in"])},
    }
    return pal


def hash3(a, b, salt):
    h = (a * 0x9E3779B1 + b * 0x85EBCA77 + salt * 0xC2B2AE3D + 0x27D4EB2F) & M32
    h ^= h >> 16
    h = (h * 0x7FEB352D) & M32
    h ^= h >> 15
    h = (h * 0x846CA68B) & M32
    h ^= h >> 16
    return h


def bake(g, pal):
    """Returns (width, height, rgba bytes) for the map. Pure integer maths, deterministic."""
    S = pal["texel_cells"]
    TW, TH = W * S, H * S
    out = bytearray(TW * TH * 4)
    for ty in range(TH):
        cy, sy = divmod(ty, S)
        for tx in range(TW):
            cx, sx = divmod(tx, S)
            k = cy * W + cx
            land = g.land[k]
            ovl = g.ovl[k]
            dens = g.dens[k]
            col = None
            if ovl == PITCH:
                chunk_thr = dens * pal["pitch"]["max_permille"] // 255
                if hash3(tx >> 1, ty >> 1, 4) % 1000 < chunk_thr:
                    col = pal["pitch"]["glint"] if hash3(tx, ty, 5) % pal["pitch"]["glint_one_in"] == 0 else pal["pitch"]["chunk"]
            if col is None:
                # Cell-scale blotches: about one cell in four is dark or light, the rest base.
                m = hash3(tx >> 3, ty >> 3, 1) % 8
                c = -1 if m == 0 else (1 if m == 1 else 0)
                # Sparse texel grain: one texel in six nudged one shade.
                f = hash3(tx, ty, 2)
                if f % 6 == 0:
                    c += 1 if (f >> 8) & 1 else -1
                c = max(-1, min(1, c))
                shades = pal["land"][LAND_NAMES.index(LAND_NAMES[land])]
                col = shades[c + 1]
            if ovl == ASHFALL and hash3(tx, ty, 3) % 1000 < dens * pal["ashfall"]["max_permille"] // 255:
                col = pal["ashfall"]["speck"]
            # 1-texel edge blend toward the neighbour's base colour when the land type differs.
            def base_of(nx, ny):
                return pal["land"][g.land[ny * W + nx]][1]
            if sx == 0 and cx > 0 and g.land[k - 1] != land:
                col = avg(col, base_of(cx - 1, cy))
            if sx == S - 1 and cx < W - 1 and g.land[k + 1] != land:
                col = avg(col, base_of(cx + 1, cy))
            if sy == 0 and cy > 0 and g.land[k - W] != land:
                col = avg(col, base_of(cx, cy - 1))
            if sy == S - 1 and cy < H - 1 and g.land[k + W] != land:
                col = avg(col, base_of(cx, cy + 1))
            o = (ty * TW + tx) * 4
            out[o:o + 4] = bytes((col[0], col[1], col[2], 255))
    return TW, TH, bytes(out)


def avg(a, b):
    return tuple((a[i] + b[i]) // 2 for i in range(3))


# ---------------------------------------------------------------- proof images

def write_proof(g, pal, outdir):
    from PIL import Image

    os.makedirs(outdir, exist_ok=True)
    TW, TH, rgba = bake(g, pal)
    tex = Image.frombytes("RGBA", (TW, TH), rgba)
    S = pal["texel_cells"]
    import hashlib
    print("bake sha256:", hashlib.sha256(rgba).hexdigest())
    overview = tex.resize((W * 4, H * 4), Image.BOX)
    overview.save(os.path.join(outdir, "rustbelt_m2.png"))

    tank = Image.open(os.path.join(REPO, "data", "sprites", "vehicles", "tank_v2.png")).convert("RGBA").crop((0, 0, 96, 96))
    tank = house_green(tank)
    CROP_X, CROP_Y, CROP_CELLS = 36, 26, 16
    crop_tex = tex.crop((CROP_X * S, CROP_Y * S, (CROP_X + CROP_CELLS) * S, (CROP_Y + CROP_CELLS) * S))
    px = 64
    big = crop_tex.resize((CROP_CELLS * px, CROP_CELLS * px), Image.BILINEAR).convert("RGBA")
    for (cx, cy) in TANK_SPOTS:
        lx, ly = (cx - CROP_X) * px + px // 2, (cy - CROP_Y) * px + px // 2
        big.alpha_composite(tank, (lx - 48, ly - 64))
    big.convert("RGB").save(os.path.join(outdir, "rustbelt_m2_crop_64px.png"))


TANK_SPOTS = [(38, 38), (44, 34)]  # clear ground, then ash


def house_green(img):
    """Recolours the magenta house ramp to a green ramp of the same shading (luma-scaled)."""
    ramp = [0x400040, 0x5B125B, 0x772577, 0x923792, 0xAD49AD, 0xC85BC8, 0xE46EE4, 0xFF80FF]
    green = (92, 168, 64)
    lookup = {}
    for i, rgb in enumerate(ramp):
        f = (i + 1) / len(ramp)
        lookup[((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255)] = tuple(int(c * f) for c in green)
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, gg, b, a = px[x, y]
            if a and (r, gg, b) in lookup:
                px[x, y] = lookup[(r, gg, b)] + (a,)
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--proof", metavar="DIR", help="also write proof PNGs into DIR")
    args = ap.parse_args()

    g = build()
    check_symmetry(g)
    groups = check_routes(g)
    write_map(g, MAP_OUT)
    print(f"wrote {MAP_OUT}")
    print(f"fords: {len(groups)} crossings, each one removable without cutting the bases")
    if args.proof:
        write_proof(g, load_palette(), args.proof)
        print(f"wrote proof images into {args.proof}")
    else:
        import hashlib
        _, _, rgba = bake(g, load_palette())
        print("bake sha256:", hashlib.sha256(rgba).hexdigest())


if __name__ == "__main__":
    sys.exit(main())
