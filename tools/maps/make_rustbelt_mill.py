#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Builds data/maps/rustbelt_mill.map.toml: the 64x64 Rust Belt story map "rustbelt_mill" (D-341 Q6, design 22 section 1 Q6).

A hand-finished story map (design 16 section 3.6: no symmetry or fairness checks; connectivity and prop safety only). It is built from
scratch, not from a generated base: a rail line crosses the map west to east (2-cell road bed, rows 31-32), a ruined mill town of rubble
blocks sits north of the rail in the middle, a wrecked supply train lies on the rail just east of the town, the Authority survey camp and
the open scrap field for the Directorate assembly bore face the rail inside the town, Shaft Four spoil ground fills the north-west, and
the two homes (south-west and north-east corners) are half-turn images of each other (plateau, cliff ring, two exits, pits and ashfall).

Deterministic: no random module, no wall-clock. Re-running gives byte-identical output.
Used by auth_06, dir_06, lb_06 and (later) auth_08. Zone names are listed in the map file header ("# zone <name> x0 y0 x1 y1", inclusive
cell rects); missions copy them into their own [zones.*] tables, and tests/Scripting.Tests/S48/S48MapMillTests.cs reads the header.

Usage:
  python3 tools/maps/make_rustbelt_mill.py            # write the map
"""
import base64
import os
import struct
import sys
from collections import deque

W = H = 64
MAP_NAME = "rustbelt_mill"
THEATER = "rustbelt"
STARTS = [(7, 56), (56, 7)]          # home_sw, home_ne (half-turn images of each other)
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MAP_OUT = os.path.join(REPO, "data", "maps", MAP_NAME + ".map.toml")

# LandType and OverlayKind values from src/Sim/Rules/Enums.cs (append-only, persisted).
CLEAR, ROAD, ROUGH, ASH, WATER, ROCK, CLIFF = range(7)
NO_OVL, ASHFALL, PITCH, WALL, TREE, WRECK, BLOCK, PYLON, SNAG = range(9)
M32 = 0xFFFFFFFF

# Zones: name -> (x0, y0, x1, y1) inclusive. Order is the order of the header.
ZONES = {
    "home_sw": (1, 48, 15, 62),
    "home_ne": (48, 1, 62, 15),
    "mill_town": (17, 14, 46, 30),
    "rail_w": (0, 30, 17, 33),
    "rail_e": (57, 30, 63, 33),
    "train_wreck": (46, 29, 56, 34),
    "bore_site": (19, 24, 28, 30),
    "survey_camp": (38, 25, 45, 30),
    "shaft_ground": (2, 4, 18, 22),
    "shaft_head_a": (5, 7, 8, 10),
    "shaft_head_b": (13, 12, 16, 15),
    "shaft_head_c": (6, 17, 9, 20),
    "grain_elevator": (43, 17, 46, 20),
    "pitch_spur": (9, 24, 16, 29),
    "spawn_n": (27, 0, 35, 2),
    "spawn_e": (61, 29, 63, 34),
    "spawn_s": (27, 61, 35, 63),
    "spawn_w": (0, 29, 2, 34),
}
# Zones that stay free of scatter props, rubble and rock (the missions build or place actors there).
CLEAN = ["bore_site", "survey_camp", "grain_elevator", "shaft_head_a", "shaft_head_b", "shaft_head_c",
         "spawn_n", "spawn_e", "spawn_s", "spawn_w"]

NOTES = [
    "home_sw / home_ne: the two player corners. Start cells (7,56) and (56,7); plateau, cliff ring, exits east+north (sw) and west+south (ne).",
    "mill_town: ruined blocks (Block props with rubble breaches), main street x30-31, avenues y15-16 and y21-22.",
    "rail_w / rail_e: the rail bed (road rows y31-32) west of x17 and east of x57; the rail also runs through the town and train_wreck.",
    "train_wreck: the wrecked supply train (Wreck props on y31, y33 and the engine at x55-56); lane y32 stays open for haulers (lb_06).",
    "bore_site: open buildable scrap field, scrap Wrecks on its north edge y23 (dir_06 bore, smelter vent, cistern, bunk gallery).",
    "survey_camp: open lot east of the old mill, beside the rail (Authority survey camp).",
    "shaft_ground: north-west spoil heaps; shaft_head_a/b/c are the tunnel-head pads (auth_08 Shaft Four).",
    "grain_elevator: 4x4 pad in the north-east of the town (auth_06 hidden bonus: the elevator stays standing).",
    "pitch_spur: pitchblende pit and ashfall beside the rail west of the town (auth_06 scrapers).",
    "spawn_n/e/s/w: 3-cell-deep strips on each map edge for reinforcements (spawn_e and spawn_w cross the rail).",
]


def inb(x, y):
    return 0 <= x < W and 0 <= y < H


def rot(x, y):
    return (W - 1 - x, H - 1 - y)


def hash3(a, b, salt):
    h = (a * 0x9E3779B1 + b * 0x85EBCA77 + salt * 0xC2B2AE3D + 0x27D4EB2F) & M32
    h ^= h >> 16
    h = (h * 0x7FEB352D) & M32
    h ^= h >> 15
    h = (h * 0x846CA68B) & M32
    h ^= h >> 16
    return h


def rect_cells(r):
    x0, y0, x1, y1 = r
    return [(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)]


def in_rect(x, y, r):
    return r[0] <= x <= r[2] and r[1] <= y <= r[3]


class Grid:
    def __init__(self):
        n = W * H
        self.land = [CLEAR] * n
        self.height = [0] * n
        self.ovl = [NO_OVL] * n
        self.dens = [0] * n
        self.fixed = [False] * n   # cells later passes must not touch (roads, resources, props placed on purpose)

    def i(self, x, y):
        return y * W + x

    def land_at(self, x, y):
        return self.land[self.i(x, y)]

    def put_land(self, x, y, v, fix=False):
        if inb(x, y):
            k = self.i(x, y)
            self.land[k] = v
            if fix:
                self.fixed[k] = True

    def put_ovl(self, x, y, kind, dens=0, fix=True):
        if inb(x, y):
            k = self.i(x, y)
            self.ovl[k] = kind
            self.dens[k] = dens
            if fix:
                self.fixed[k] = True


def road(g, x0, y0, x1, y1):
    """A road rectangle (inclusive), painted over everything that is not cliff or rock."""
    for (x, y) in rect_cells((x0, y0, x1, y1)):
        if inb(x, y) and g.land_at(x, y) not in (CLIFF, ROCK):
            g.put_land(x, y, ROAD, fix=True)
            k = g.i(x, y)
            if g.ovl[k] != NO_OVL:
                g.ovl[k] = NO_OVL
                g.dens[k] = 0


def disc(cx, cy, r):
    return [(x, y) for y in range(cy - r, cy + r + 1) for x in range(cx - r, cx + r + 1)
            if inb(x, y) and (x - cx) ** 2 + (y - cy) ** 2 <= r * r + r // 2]


def ashfall(g, cx, cy, r):
    for (x, y) in disc(cx, cy, r):
        d = max(abs(x - cx), abs(y - cy))
        if g.land_at(x, y) in (CLEAR, ROUGH, ASH, ROAD) and g.ovl[g.i(x, y)] in (NO_OVL, ASHFALL):
            g.put_ovl(x, y, ASHFALL, max(60, 255 - 60 * d))


def pit(g, cx, cy):
    for (x, y) in disc(cx, cy, 1):
        if g.land_at(x, y) in (CLEAR, ROUGH, ASH, ROAD):
            g.put_ovl(x, y, PITCH, 255 if (x, y) == (cx, cy) else 200)


def build():
    g = Grid()

    # 1. Base ground: sparse rough and ash patches (hash noise, deterministic).
    for y in range(H):
        for x in range(W):
            m = hash3(x >> 2, y >> 2, 11) % 16
            if m == 0:
                g.put_land(x, y, ROUGH)
            elif m == 1 and hash3(x, y, 12) % 3:
                g.put_land(x, y, ASH)
    for (cx, cy, r) in [(50, 46, 4), (40, 54, 3), (22, 44, 3), (56, 24, 3)]:   # ash ground patches
        for (x, y) in disc(cx, cy, r):
            g.put_land(x, y, ASH)

    # 2. Homes: a 17x18 plateau per corner with a cliff ring and two exits. Home B is the half-turn image of home A.
    def home_a():
        plate = [(x, y) for y in range(46, 64) for x in range(0, 17)]
        cliffs = [(16, y) for y in range(46, 64) if not 54 <= y <= 59] + [(x, 46) for x in range(0, 17) if not 7 <= x <= 11]
        return plate, cliffs
    plate, cliffs = home_a()
    for (x, y) in plate:
        for (px, py) in (rot_pair(x, y)):
            g.height[g.i(px, py)] = 1
            g.put_land(px, py, CLEAR, fix=True)
    for (x, y) in cliffs:
        for (px, py) in rot_pair(x, y):
            g.put_land(px, py, CLIFF, fix=True)
    # Rock outcrops just outside the cliff ring for texture; the exits stay open.
    for (cx, cy, r) in [(20, 49, 2), (20, 62, 1), (3, 42, 2), (13, 42, 1)]:
        for (x, y) in disc(cx, cy, r):
            for (px, py) in rot_pair(x, y):
                if g.land_at(px, py) != CLIFF and not g.fixed[g.i(px, py)]:
                    g.put_land(px, py, ROCK)
    # Resources per home: a pitchblende pit and two ashfall fields.
    for (kind, cx, cy, r) in [("pit", 12, 51, 0), ("ash", 4, 50, 2), ("ash", 13, 60, 2), ("ash", 3, 61, 1)]:
        for (px, py) in rot_pair(cx, cy):
            if kind == "pit":
                pit(g, px, py)
            else:
                ashfall(g, px, py, r)
    # Starts: clear, no overlay.
    for (sx, sy) in STARTS:
        for (x, y) in rect_cells((sx - 3, sy - 3, sx + 3, sy + 3)):
            g.put_land(x, y, CLEAR, fix=True)
            g.ovl[g.i(x, y)] = NO_OVL
            g.dens[g.i(x, y)] = 0

    # 3. The rail bed: two road rows (y 31-32) across the whole map, with a rough shoulder and a pylon every 8 cells.
    road(g, 0, 31, 63, 32)
    for x in range(W):
        for y in (30, 33):
            if g.land_at(x, y) not in (CLIFF, ROCK) and not g.fixed[g.i(x, y)] and hash3(x, y, 21) % 3 == 0:
                g.put_land(x, y, ROUGH)
    for x in range(4, 60, 8):
        if x in range(28, 36):
            continue
        g.put_ovl(x, 29 if (x // 8) % 2 else 34, PYLON, hash3(x, 7, 22) % 3)

    # 4. The mill town (x 17..46, y 14..30).
    road(g, 17, 15, 45, 16)          # north avenue H0
    road(g, 30, 6, 31, 30)           # main street V1, from the NE road down to the rail
    road(g, 19, 21, 43, 22)          # middle avenue H1
    road(g, 29, 23, 29, 30)          # west shoulder of the main street (keeps the yard open to it)
    # Rubble ring around the town (outside the avenues).
    for (x, y) in rect_cells((16, 13, 47, 31)):
        if inb(x, y) and g.land_at(x, y) in (CLEAR, ASH) and not g.fixed[g.i(x, y)] and hash3(x, y, 31) % 4 == 0:
            g.put_land(x, y, ROUGH)
    # Ruined blocks: filled Block props, with rubble breaches and ragged corners; alleys between the halves.
    blocks = [(19, 17, 23, 20), (25, 17, 28, 20), (33, 17, 37, 20), (39, 17, 42, 20),   # north row, alleys at x24, x38
              (33, 23, 37, 28)]                                                          # the old mill, east of the main street
    for (x0, y0, x1, y1) in blocks:
        for (x, y) in rect_cells((x0, y0, x1, y1)):
            edge = x in (x0, x1) or y in (y0, y1)
            corner = x in (x0, x1) and y in (y0, y1)
            h = hash3(x, y, 41)
            if corner and h % 2:
                g.put_land(x, y, ROUGH, fix=True)       # ragged corner: rubble, open
                continue
            if edge and h % 5 == 0:
                g.put_land(x, y, ROUGH, fix=True)       # breach
                continue
            g.put_land(x, y, CLEAR if g.land_at(x, y) in (ROUGH, ASH) else g.land_at(x, y), fix=True)
            g.put_ovl(x, y, BLOCK, h % 3)
    # Rubble heaps in the avenues' mouths and a few car wrecks in the streets (all leave a free lane).
    for (x, y, v) in [(26, 22, 0), (35, 15, 1), (21, 16, 2), (41, 16, 0)]:
        g.put_ovl(x, y, WRECK, v)
    # Scrap wrecks along the north edge of the bore yard (outside the zone: y 23 is its border row, so sparse and every other cell).
    for x in range(19, 29, 2):
        g.put_ovl(x, 23, WRECK, hash3(x, 23, 42) % 3)
    # Yards: the bore yard and the survey lot are cleared to buildable ground.
    for name in ("bore_site", "survey_camp", "grain_elevator"):
        for (x, y) in rect_cells(ZONES[name]):
            g.put_land(x, y, CLEAR if hash3(x, y, 43) % 5 else ASH, fix=True)
            g.ovl[g.i(x, y)] = NO_OVL
            g.dens[g.i(x, y)] = 0
    # Re-lay the scrap row after the yard clear (it sits on the bore_site border row y 24? no: y 23 is outside the rect).

    # 5. Roads out of the homes. Home A east exit (17,54..59) to the rail; north exit (7..11, 45) to the rail.
    road(g, 17, 56, 26, 57)
    road(g, 25, 33, 26, 57)
    road(g, 9, 33, 10, 45)
    # Home B (half-turn images): west exit (46,4..9) -> main street; south exit (52..56,18) -> rail.
    road(g, 31, 5, 46, 6)
    road(g, 53, 18, 54, 30)
    # East-west road along the south of the map, tying the two long roads together and passing the contested pit.
    road(g, 26, 47, 54, 48)
    road(g, 53, 33, 54, 48)

    # 6. The train wreck on the rail, east of the town: car chunks on y 31, derailed cars on y 33, the engine at the east end.
    #    y 32 stays open the whole way (haulers and escorts).
    for x in list(range(47, 50)) + list(range(51, 54)):
        g.put_ovl(x, 31, WRECK, hash3(x, 31, 51) % 3)
    for x in (50, 51, 52):
        g.put_ovl(x, 33, WRECK, hash3(x, 33, 52) % 3)
    for (x, y) in [(55, 30), (56, 30), (55, 31), (56, 31)]:
        g.put_ovl(x, y, WRECK, hash3(x, y, 53) % 3)
    g.put_ovl(48, 30, WRECK, 1)

    # 7. Resources outside the homes.
    pit(g, 12, 26)                  # pitch_spur, west of the town beside the rail
    ashfall(g, 11, 28, 2)
    ashfall(g, 15, 27, 1)
    pit(g, 51, 38)                  # south of the train wreck (lb_06 lure)
    ashfall(g, 56, 40, 2)
    pit(g, 32, 45)                  # contested middle pit, south of the town
    ashfall(g, 38, 44, 2)
    ashfall(g, 24, 42, 2)
    ashfall(g, 46, 12, 2)           # north-east, near home B
    ashfall(g, 38, 9, 1)

    # 8. Shaft ground (north-west): spoil heaps of rock around three cleared tunnel-head pads, open toward the town.
    for (x, y) in rect_cells(ZONES["shaft_ground"]):
        if hash3(x, y, 61) % 3 == 0:
            g.put_land(x, y, ASH)
        elif hash3(x, y, 62) % 4 == 0:
            g.put_land(x, y, ROUGH)
    heaps = [(3, 6, 2), (10, 5, 2), (4, 13, 2), (11, 9, 1), (17, 9, 2), (10, 15, 1), (3, 21, 1), (12, 21, 2), (17, 17, 1), (16, 4, 1)]
    for (cx, cy, r) in heaps:
        for (x, y) in disc(cx, cy, r):
            if not g.fixed[g.i(x, y)]:
                g.put_land(x, y, ROCK)
    for name in ("shaft_head_a", "shaft_head_b", "shaft_head_c"):
        x0, y0, x1, y1 = ZONES[name]
        for (x, y) in rect_cells((x0 - 1, y0 - 1, x1 + 1, y1 + 1)):
            if in_rect(x, y, ZONES[name]):
                g.put_land(x, y, ASH, fix=True)
                g.ovl[g.i(x, y)] = NO_OVL
            elif g.land_at(x, y) == ROCK:
                g.put_land(x, y, ROUGH)       # open the heaps back from each pad by one ring
    # A track from the town's north-west corner into the shaft ground (rubble road, clear lane).
    road(g, 17, 15, 18, 16)
    road(g, 10, 15, 18, 16)
    road(g, 10, 7, 11, 16)
    road(g, 6, 11, 11, 12)
    road(g, 7, 17, 8, 21)
    road(g, 7, 12, 8, 17)

    # 9. Scatter props (dead trees and a few snags) in open ground, away from roads, resources, yards, homes and the rail.
    protected = list(ZONES[n] for n in CLEAN)
    for y in range(H):
        for x in range(W):
            k = g.i(x, y)
            if g.fixed[k] or g.land[k] not in (CLEAR, ROUGH, ASH) or g.ovl[k] != NO_OVL:
                continue
            if any(in_rect(x, y, r) for r in protected):
                continue
            if in_rect(x, y, ZONES["home_sw"]) or in_rect(x, y, ZONES["home_ne"]) or 28 <= y <= 35 or 14 <= y <= 31 and 16 <= x <= 47:
                continue
            if hash3(x, y, 71) % 47 == 0:
                g.put_ovl(x, y, SNAG, hash3(x, y, 72) % 3, fix=False)
            elif hash3(x, y, 73) % 83 == 0:
                g.put_ovl(x, y, TREE, hash3(x, y, 74) % 3, fix=False)
    # Home interiors: a few rubble Snags well away from the start squares and fields.
    for (x, y) in [(3, 54), (14, 48), (2, 58), (14, 62)]:
        for (px, py) in rot_pair(x, y):
            if g.ovl[g.i(px, py)] == NO_OVL and g.land_at(px, py) == CLEAR:
                g.put_ovl(px, py, SNAG, 1)

    fix_up(g)
    return g


def rot_pair(x, y):
    return [(x, y), rot(x, y)]


def passable(g, x, y):
    k = g.i(x, y)
    return g.land[k] not in (WATER, ROCK, CLIFF) and g.ovl[k] not in (WALL, TREE, WRECK, BLOCK, PYLON, SNAG)


def components(g):
    comp = [-1] * (W * H)
    n = 0
    for y in range(H):
        for x in range(W):
            if comp[g.i(x, y)] != -1 or not passable(g, x, y):
                continue
            comp[g.i(x, y)] = n
            q = deque([(x, y)])
            while q:
                cx, cy = q.popleft()
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = cx + dx, cy + dy
                    if inb(nx, ny) and comp[g.i(nx, ny)] == -1 and passable(g, nx, ny):
                        comp[g.i(nx, ny)] = n
                        q.append((nx, ny))
            n += 1
    return comp, n


def fix_up(g):
    """Resources never touch a prop (prop_on_resource); sealed pockets are filled with rock; props never sit in a start square."""
    for y in range(H):
        for x in range(W):
            k = g.i(x, y)
            if g.ovl[k] in (TREE, WRECK, BLOCK, PYLON, SNAG, WALL):
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if inb(nx, ny) and g.ovl[g.i(nx, ny)] in (ASHFALL, PITCH):
                            g.ovl[g.i(nx, ny)] = NO_OVL
                            g.dens[g.i(nx, ny)] = 0
    # Pockets: any passable component other than the one holding start 1 is sealed off; fill it so no unit can be stranded.
    comp, n = components(g)
    main = comp[g.i(*STARTS[0])]
    for y in range(H):
        for x in range(W):
            c = comp[g.i(x, y)]
            if c != -1 and c != main:
                g.put_land(x, y, ROCK)
                g.ovl[g.i(x, y)] = NO_OVL
                g.dens[g.i(x, y)] = 0


def check(g):
    comp, n = components(g)
    if n != 1:
        raise SystemExit(f"map has {n} separate ground areas")
    for name, r in ZONES.items():
        cells = [(x, y) for (x, y) in rect_cells(r) if passable(g, x, y)]
        if not cells:
            raise SystemExit(f"zone {name} has no passable cell")
    for name in CLEAN:
        for (x, y) in rect_cells(ZONES[name]):
            if g.land_at(x, y) in (ROCK, CLIFF, WATER) or g.ovl[g.i(x, y)] not in (NO_OVL,):
                raise SystemExit(f"zone {name} is not clean at ({x},{y})")
    for (sx, sy) in STARTS:
        for (x, y) in rect_cells((sx - 2, sy - 2, sx + 2, sy + 2)):
            if g.land_at(x, y) not in (CLEAR, ROAD, ASH) or g.ovl[g.i(x, y)] != NO_OVL or g.height[g.i(x, y)] != g.height[g.i(sx, sy)]:
                raise SystemExit(f"start square at ({sx},{sy}) is not clear and flat")
    for (x, y) in rect_cells(ZONES["train_wreck"]):
        pass
    for x in range(46, 57):
        if not passable(g, x, 32):
            raise SystemExit(f"rail lane blocked at ({x},32)")


def header():
    lines = ["# Generated by tools/maps/make_rustbelt_mill.py. Do not hand-edit; change the script and re-run.",
             "# rustbelt_mill: story map (D-341 Q6, M16-C-12). Used by auth_06, dir_06, lb_06 and later auth_08. Not symmetric; not fairness-checked.",
             "# Starts: home_sw (7,56) and home_ne (56,7). The rail bed runs west to east along rows y31-32.",
             "#"]
    lines += ["# " + n for n in NOTES]
    lines += ["#", "# Zones (inclusive cell rects). The format is exact: S48MapMillTests reads these lines.",
              "# zone <name> <x0> <y0> <x1> <y1>"]
    for name, (x0, y0, x1, y1) in ZONES.items():
        lines.append(f"# zone {name} {x0} {y0} {x1} {y1}")
    return "\n".join(lines) + "\n"


def write_map(g, path):
    n = W * H
    tpl = b"".join(struct.pack("<H", 0) for _ in range(n))
    b64 = lambda b: base64.b64encode(bytes(b)).decode()
    starts = ", ".join(f"[{x}, {y}]" for (x, y) in STARTS)
    text = (
        header() +
        "[map]\n"
        f'name = "{MAP_NAME}"\n'
        f'theater = "{THEATER}"\n'
        f"width = {W}\n"
        f"height = {H}\n"
        "format_version = 4\n"
        f"start_positions = [{starts}]\n\n"
        "[layers]\n"
        f'template = "{base64.b64encode(tpl).decode()}"\n'
        f'land = "{b64(g.land)}"\n'
        f'height = "{b64(g.height)}"\n'
        f'overlay = "{b64(g.ovl)}"\n'
        f'density = "{b64(g.dens)}"\n'
    )
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def main():
    g = build()
    check(g)
    write_map(g, MAP_OUT)
    print(f"wrote {MAP_OUT}")


if __name__ == "__main__":
    sys.exit(main())
