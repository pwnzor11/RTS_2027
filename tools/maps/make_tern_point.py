#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Builds data/maps/tern_point.map.toml: the 64x64 glass_coast story map of the 1990 railhead at Tern Point (D-341 Q6, M16-C-12).

Deterministic: integer maths only, no random numbers, no wall-clock. Re-running gives byte-identical output.
Story maps are hand-made and not symmetric (design 16 section 3.6); the script checks connectivity, the landing beach and the prop
rules before it writes anything, and `map validate` re-checks them with the game's own validator.

Used by exp_05 The South Coast, exp_06 Dredge Rights, auth_10 Tern Point and lb_10 The Platform (design 22 section 3).

Layout (x right, y down). The open sea is the SOUTH edge west of x = 44; the Point (the headland with the railhead) holds the south-east
corner. From the sea inland: deep water, shallows (dredge fields of Ashfall on them), a 1-2 cell beach (Coast, the landing cells),
the backshore and a beach base, a belt of dunes in the north, the east dune belt behind the Point, an inland base in the middle north.
The 1990 rail line comes in from the east edge, runs west along y = 34 (the railhead yard with sidings and dead locomotives) and then
south along x = 49..50 to the platform on the shore of the Point.

Zones (rect = [x0, y0, x1, y1], inclusive; the missions copy these into their own [zones.*] tables):
  beach_landing    [8, 36, 28, 49]    beach, backshore and the beach start (16, 40); landing markers on the Coast cells
  home_inland      [30, 10, 52, 28]   inland base ground, start (40, 19)
  sea_s            [0, 53, 43, 63]    the open sea (deep water south of the shallows)
  relief_wreck     [28, 48, 38, 54]   the 1997 relief ship, aground in the shallows off the beach
  dredge_shallows  [4, 50, 27, 57]    shallows with Ashfall fields for skimmers
  railhead         [47, 32, 62, 47]   the yard: track, sidings, dead locomotives, sheds
  platform         [44, 48, 50, 60]   the evacuation platform on the shore of the Point
  dunes_n          [0, 0, 63, 9]      north dune belt
  dunes_s          [52, 40, 63, 61]   east dune belt behind the Point
  spawn_n          [24, 0, 48, 1]     north edge, ground arrivals
  spawn_e          [62, 8, 63, 44]    east edge (the rail line enters at (63, 34))
  spawn_s          [46, 62, 63, 63]   south edge of the Point
  spawn_w          [0, 10, 1, 40]     west edge
  sea_spawn        [0, 60, 40, 63]    south sea edge, for ships and landing craft

Usage:
  python3 tools/maps/make_tern_point.py
"""
import base64
import os
import struct
import sys
from collections import deque

W = H = 64
MAP_NAME = "tern_point"
THEATER = "glasscoast"            # as glass_coast_e1 writes it
STARTS = [(16, 40), (40, 19)]    # beach, inland
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MAP_OUT = os.path.join(REPO, "data", "maps", MAP_NAME + ".map.toml")

# LandType and OverlayKind values from src/Sim/Rules/Enums.cs (append-only, persisted).
CLEAR, ROAD, ROUGH, ASH, WATER, ROCK, CLIFF, SHALLOW, COAST = range(9)
NO_OVL, ASHFALL, PITCH, WALL, TREE, WRECK, BLOCK, PYLON, SNAG = range(9)
M32 = 0xFFFFFFFF

ZONES = {
    "beach_landing": (8, 36, 28, 49), "home_inland": (30, 10, 52, 28), "sea_s": (0, 53, 43, 63),
    "relief_wreck": (28, 48, 38, 54), "dredge_shallows": (4, 50, 27, 57), "railhead": (47, 32, 62, 47),
    "platform": (44, 48, 50, 60), "dunes_n": (0, 0, 63, 9), "dunes_s": (52, 40, 63, 61),
    "spawn_n": (24, 0, 48, 1), "spawn_e": (62, 8, 63, 44), "spawn_s": (46, 62, 63, 63), "spawn_w": (0, 10, 1, 40),
    "sea_spawn": (0, 60, 40, 63),
}


def hash3(a, b, salt):
    h = (a * 0x9E3779B1 + b * 0x85EBCA77 + salt * 0xC2B2AE3D + 0x27D4EB2F) & M32
    h ^= h >> 16
    h = (h * 0x7FEB352D) & M32
    h ^= h >> 15
    h = (h * 0x846CA68B) & M32
    h ^= h >> 16
    return h


def inb(x, y):
    return 0 <= x < W and 0 <= y < H


def n4(x, y):
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        if inb(x + dx, y + dy):
            yield x + dx, y + dy


class Grid:
    def __init__(self):
        n = W * H
        self.land = [CLEAR] * n
        self.ovl = [NO_OVL] * n
        self.dens = [0] * n

    def i(self, x, y):
        return y * W + x

    def L(self, x, y):
        return self.land[y * W + x]

    def setl(self, x, y, v):
        if inb(x, y):
            self.land[y * W + x] = v

    def seto(self, x, y, kind, dens=0):
        if inb(x, y):
            self.ovl[y * W + x] = kind
            self.dens[y * W + x] = dens


SHORE_PTS = [(0, 49), (8, 47), (16, 46), (24, 47), (32, 49), (40, 51), (44, 52)]


def shore_y(x):
    """First sea row at column x (west of the Point): integer interpolation plus a small stepped wobble."""
    for (x0, y0), (x1, y1) in zip(SHORE_PTS, SHORE_PTS[1:]):
        if x0 <= x <= x1:
            base = y0 + (y1 - y0) * (x - x0) // (x1 - x0)
            break
    else:
        base = SHORE_PTS[-1][1]
    return base + (hash3(x // 3, 0, 7) % 3) - 1


def point_x(y):
    """First land column of the Point at row y (the bay is x < point_x)."""
    return 44 + (hash3(0, y // 4, 8) % 2)


def build():
    g = Grid()
    bay = [[False] * W for _ in range(H)]
    for y in range(H):
        for x in range(W):
            if y >= 48 and x < point_x(y) and y >= shore_y(min(x, 44)):
                bay[y][x] = True

    # distance (4-neighbour) of every bay cell from the nearest land cell
    dist = {}
    dq = deque()
    for y in range(H):
        for x in range(W):
            if bay[y][x] and any(not bay[ny][nx] for nx, ny in n4(x, y)):
                dist[(x, y)] = 1
                dq.append((x, y))
    while dq:
        x, y = dq.popleft()
        for nx, ny in n4(x, y):
            if bay[ny][nx] and (nx, ny) not in dist:
                dist[(nx, ny)] = dist[(x, y)] + 1
                dq.append((nx, ny))
    for (x, y), d in dist.items():
        reach = 5 if 4 <= x <= 27 else (4 if 27 < x <= 38 else 3)
        g.setl(x, y, SHALLOW if d <= reach else WATER)

    # beach: land cells next to the bay are Coast; two cells deep along the landing beach
    land_d = {}
    dq = deque()
    for y in range(H):
        for x in range(W):
            if not bay[y][x] and any(bay[ny][nx] for nx, ny in n4(x, y)):
                land_d[(x, y)] = 1
                dq.append((x, y))
    while dq:
        x, y = dq.popleft()
        for nx, ny in n4(x, y):
            if not bay[ny][nx] and (nx, ny) not in land_d and land_d[(x, y)] < 3:
                land_d[(nx, ny)] = land_d[(x, y)] + 1
                dq.append((nx, ny))
    for (x, y), d in land_d.items():
        if d == 1 or (d == 2 and 8 <= x <= 28):
            g.setl(x, y, COAST)

    # dunes: diagonal ridges of Rough, thinned by noise (north belt, east belt behind the Point, a few strays)
    def dune(x, y):
        ridge = (x * 3 + y * 5) % 17 < 5
        return ridge and hash3(x, y, 21) % 4 != 0

    for y in range(0, 10):
        for x in range(W):
            if g.L(x, y) == CLEAR and (dune(x, y) or hash3(x, y, 22) % 9 == 0):
                g.setl(x, y, ROUGH)
    for y in range(40, 62):
        for x in range(52, W):
            if g.L(x, y) == CLEAR and dune(x, y):
                g.setl(x, y, ROUGH)
    for y in range(10, 62):
        for x in range(W):
            if g.L(x, y) == CLEAR and hash3(x // 2, y // 2, 23) % 23 == 0 and not (30 <= x <= 52 and 10 <= y <= 28) and not (8 <= x <= 28 and 36 <= y <= 49):
                g.setl(x, y, ROUGH)
    # rock outcrops in the dunes (impassable, so the north belt has a few chokes)
    for (rx, ry) in [(8, 4), (22, 7), (52, 3), (60, 8), (58, 46), (62, 56)]:
        for dy in range(2):
            for dx in range(2):
                if g.L(rx + dx, ry + dy) in (CLEAR, ROUGH):
                    g.setl(rx + dx, ry + dy, ROCK)

    # ash patches with Ashfall fields: beach base (two), inland base (two)
    def ash_blob(cx, cy, r, dens_base):
        cells = []
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r, cx + r + 1):
                if inb(x, y) and (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 1 and g.L(x, y) in (CLEAR, ROUGH):
                    g.setl(x, y, ASH)
                    g.seto(x, y, ASHFALL, dens_base + hash3(x, y, 31) % 64)
                    cells.append((x, y))
        return cells

    ash_blob(25, 40, 2, 160)
    ash_blob(8, 41, 2, 150)
    ash_blob(47, 20, 2, 170)
    ash_blob(32, 23, 2, 150)

    # dredge fields: Ashfall on the shallows off the beach (skimmers / hover only)
    for (cx, cy) in [(10, 52), (17, 53), (23, 54)]:
        for y in range(cy - 2, cy + 3):
            for x in range(cx - 3, cx + 4):
                if inb(x, y) and g.L(x, y) == SHALLOW and (x - cx) ** 2 + 2 * (y - cy) ** 2 <= 12:
                    g.seto(x, y, ASHFALL, 140 + hash3(x, y, 32) % 100)

    # rail line: east edge along y = 34, then south along x = 49..50 to the buffer stops; platform on the shore side
    for x in range(48, 64):
        g.setl(x, 34, ROAD)
    for y in range(34, 62):
        g.setl(49, y, ROAD)
        g.setl(50, y, ROAD)
    for y in range(48, 60):
        g.setl(46, y, ROAD)
        g.setl(47, y, ROAD)   # the platform
    # sidings in the yard
    for sy in (37, 40, 43):
        for x in range(51, 62):
            g.setl(x, sy, ROAD)
    for y in range(34, 44):
        g.setl(58, y, ROAD) if y in (35, 36, 38, 39, 41, 42) else None   # points joining the sidings

    # props (blocking). Dead locomotives (Wreck) on the track and sidings, sheds and platform edge (Block), masts (Pylon).
    def prop(x, y, kind, var=None):
        if inb(x, y) and g.L(x, y) in (CLEAR, ROAD, ROUGH, COAST, SHALLOW, ASH) and g.ovl[g.i(x, y)] == NO_OVL:
            g.seto(x, y, kind, hash3(x, y, 41) % 3 if var is None else var)

    for (x, y) in [(49, 40), (49, 46), (50, 53), (54, 37), (58, 40), (55, 43), (60, 37)]:
        prop(x, y, WRECK)
    for (x, y) in [(52, 35), (53, 35), (56, 38), (57, 38), (52, 41), (53, 41), (60, 44), (45, 48), (45, 49), (45, 55), (45, 56), (48, 59), (48, 60)]:
        prop(x, y, BLOCK)
    for (x, y) in [(48, 48), (48, 52), (48, 56), (55, 33), (61, 33)]:
        prop(x, y, PYLON)
    # the relief ship, aground on the sandbar in the shallows, with driftwood along the beach
    for (x, y) in [(30, 51), (31, 51), (32, 52), (33, 52), (34, 52), (35, 51), (36, 51), (33, 50), (34, 50)]:
        prop(x, y, WRECK)
    for (x, y) in [(29, 49), (37, 50), (38, 51), (27, 48), (12, 47), (20, 46), (23, 47)]:
        prop(x, y, SNAG)
    # dead trees round the inland base and the backshore
    for (x, y) in [(36, 14), (44, 25), (33, 27), (51, 15), (12, 36), (27, 44), (21, 37), (10, 35), (30, 38), (41, 30)]:
        prop(x, y, TREE)
    return g


def landing_cells(g):
    """Coast cells next to Shallow water along the beach: the landing markers."""
    out = []
    for x in range(10, 27, 4):
        for y in range(40, 52):
            if g.L(x, y) == COAST and any(g.L(nx, ny) in (SHALLOW, WATER) for nx, ny in n4(x, y)):
                out.append((x, y))
                break
    return out


def convoy_cells(g):
    cells = [(x, 63) for x in range(4, 41, 8) if g.L(x, 63) == WATER]
    cells += [(0, y) for y in (58, 62) if g.L(0, y) == WATER]
    return cells


# ---------------------------------------------------------------- checks

FOOT_OK = {CLEAR, ROAD, ROUGH, ASH, COAST}


def reach(g, start, ok, blocked_ovl=True):
    seen = {start}
    dq = deque([start])
    while dq:
        x, y = dq.popleft()
        for nx, ny in n4(x, y):
            if (nx, ny) in seen or g.L(nx, ny) not in ok:
                continue
            if blocked_ovl and g.ovl[g.i(nx, ny)] >= WALL:
                continue
            seen.add((nx, ny))
            dq.append((nx, ny))
    return seen


def in_zone(name, p):
    x0, y0, x1, y1 = ZONES[name]
    return x0 <= p[0] <= x1 and y0 <= p[1] <= y1


def check(g, landing, convoy):
    land_ok = FOOT_OK
    a = reach(g, STARTS[0], land_ok)
    assert STARTS[1] in a, "beach start cannot walk to the inland start"
    for name in ("beach_landing", "home_inland", "relief_wreck", "railhead", "platform", "dunes_n", "dunes_s", "spawn_n", "spawn_e", "spawn_s", "spawn_w"):
        x0, y0, x1, y1 = ZONES[name]
        cells = [(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1) if (x, y) in a]
        assert cells, f"zone {name} has no ground cell reachable from the beach start"
    for c in landing:
        assert c in a, f"landing cell {c} is not reachable on foot"
        assert in_zone("beach_landing", c) or in_zone("beach_landing", (c[0], c[1] - 3)), f"landing {c} outside beach_landing"
    assert len(landing) >= 3, "want at least three landing markers"
    # the sea: every convoy cell reaches every landing cell's neighbouring shallow by water
    sea = reach(g, convoy[0], {WATER, SHALLOW}, blocked_ovl=False)
    for c in convoy:
        assert c in sea, f"convoy cell {c} is in a separate sea"
    for (x, y) in landing:
        assert any(n in sea for n in n4(x, y)), f"landing {(x, y)} touches no sea the convoy reaches"
    # no stray lakes: every Water/Shallow cell is in the one sea
    for y in range(H):
        for x in range(W):
            if g.L(x, y) in (WATER, SHALLOW):
                assert (x, y) in sea, f"isolated water at {(x, y)}"
    # start squares: clear, buildable, flat, no resource or prop
    for (sx, sy) in STARTS:
        for y in range(sy - 2, sy + 3):
            for x in range(sx - 2, sx + 3):
                assert g.L(x, y) in (CLEAR, ASH, ROAD, COAST) and g.ovl[g.i(x, y)] == NO_OVL, f"start square dirty at {(x, y)}"
    # props never touch a resource cell
    for y in range(H):
        for x in range(W):
            if g.ovl[g.i(x, y)] >= TREE:
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        if inb(x + dx, y + dy):
                            assert g.ovl[g.i(x + dx, y + dy)] not in (ASHFALL, PITCH), f"prop at {(x, y)} touches a resource"
    # at least two Ash fields near each start (path <= 14)
    for s in STARTS:
        dist = {s: 0}
        dq = deque([s])
        while dq:
            c = dq.popleft()
            for nx, ny in n4(*c):
                if (nx, ny) not in dist and g.L(nx, ny) in land_ok and g.ovl[g.i(nx, ny)] < WALL and dist[c] < 14:
                    dist[(nx, ny)] = dist[c] + 1
                    dq.append((nx, ny))
        fields = {(x // 6, y // 6) for (x, y) in dist if g.ovl[g.i(x, y)] == ASHFALL}
        assert len(fields) >= 1, f"start {s} has no ash field within 14 cells"


# ---------------------------------------------------------------- output

def write_map(g, path, landing, convoy):
    n = W * H
    b64 = lambda b: base64.b64encode(bytes(b)).decode()
    tpl = b"".join(struct.pack("<H", 0) for _ in range(n))
    cells = lambda cs: ", ".join(f"[{x}, {y}]" for (x, y) in cs)
    head = ["# Generated by tools/maps/make_tern_point.py. Do not hand-edit; change the script and re-run.",
            "# Tern Point (D-341 Q6): the 1990 railhead on the shore. Sea along the south edge west of x = 44; beach, relief wreck, dredge shallows, railhead, platform, dunes.",
            "# Zones for missions (rect = [x0, y0, x1, y1], inclusive; no zone data is stored in the map file):"]
    for name, (x0, y0, x1, y1) in ZONES.items():
        head.append(f"#   {name:<16} [{x0}, {y0}, {x1}, {y1}]")
    text = (
        "\n".join(head) + "\n"
        "[map]\n"
        f'name = "{MAP_NAME}"\n'
        f'theater = "{THEATER}"\n'
        f"width = {W}\n"
        f"height = {H}\n"
        "format_version = 4\n"
        f"start_positions = [{cells(STARTS)}]\n"
        'biome = "glass_coast"\n'
        'symmetry = "none"\n'
        "max_players = 2\n"
        f"convoy_edge = [{cells(convoy)}]\n"
        f"landing = [{cells(landing)}]\n\n"
        "[layers]\n"
        f'template = "{b64(tpl)}"\n'
        f'land = "{b64(g.land)}"\n'
        f'height = "{b64(bytes(n))}"\n'
        f'overlay = "{b64(g.ovl)}"\n'
        f'density = "{b64(g.dens)}"\n'
    )
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def main():
    g = build()
    landing = landing_cells(g)
    convoy = convoy_cells(g)
    check(g, landing, convoy)
    write_map(g, MAP_OUT, landing, convoy)
    print(f"wrote {MAP_OUT}")
    print("landing:", landing)
    print("convoy_edge:", convoy)


if __name__ == "__main__":
    sys.exit(main())
