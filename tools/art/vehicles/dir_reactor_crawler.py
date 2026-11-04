"""Directorate Reactor Crawler (heavy) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A big slow crawler: four track pods (two per side, a hull slung between them), a slab concrete hull with oxide-red glacis and
bone stencil panels, an exposed reactor-core housing on the rear deck (drum with a sickly glowing window ring and glowing top
cap, radiator fin banks either side and across the tail) and a squat radiation cannon turret forward of it.
Layers: body (hull, whole-vehicle shadow) + turret (cannon turret on its own sheet, pivot on the forward deck).
House paint: hull flank panels, glacis stripe, turret top plate and side bands.  ~1.15 x the Authority tank in length, ~1.9 x in width.
"""
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_common as D

ID = "dir_reactor_crawler"
TITLE = "Directorate Reactor Crawler"
CANVAS = 160
TARGET = (0.0, 0.1, 1.2)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT = (0.0, 0.95, 2.05)
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    # ---- four track pods
    for s, side in ((-1, "L"), (1, "R")):
        x = s * 1.6
        for y, tag in ((1.35, "F"), (-1.35, "R")):
            box(f"pod{side}{tag}", (0.85, 2.0, 1.0), (x, y, 0.55), "track", 0.14)
            cyl(f"idlA{side}{tag}", 0.5, 0.85, (x, y + 1.0, 0.55), "track", "X")
            cyl(f"idlB{side}{tag}", 0.5, 0.85, (x, y - 1.0, 0.55), "track", "X")
            for i in range(3):
                yy = y - 0.6 + i * 0.6
                cyl(f"wh{side}{tag}{i}", 0.33, 0.1, (x + s * 0.45, yy, 0.5), "steel", "X", 14)
                cyl(f"hb{side}{tag}{i}", 0.12, 0.08, (x + s * 0.51, yy, 0.5), "rust", "X", 8)
            box(f"guard{side}{tag}", (0.9, 2.2, 0.08), (x, y, 1.13), "hull2", 0.03)
    # ---- hull slung between the pods, slab deck, glacis, tail
    box("hull", (2.6, 4.6, 1.1), (0, 0, 1.35), "hull", 0.12)
    box("deck", (2.5, 4.4, 0.14), (0, 0, 1.95), "hull2", 0.05)
    box("glacis", (2.5, 1.2, 0.16), (0, 2.4, 1.78), "oxide", 0.05, rot=(-28, 0, 0))
    box("nose", (2.6, 0.5, 0.8), (0, 2.45, 1.2), "hull2", 0.08)
    box("tail", (2.6, 0.12, 0.9), (0, -2.35, 1.4), "hull2", 0.04)
    box("glacisstripe", (1.7, 0.7, 0.04), (0, 2.45, 1.97), "house", 0.0, rot=(-28, 0, 0))
    for sx in (-1, 1):
        box(f"flank{sx}", (0.07, 3.2, 0.55), (sx * 1.3, 0.0, 1.55), "house", 0.0)
        box(f"flankbone{sx}", (0.08, 1.2, 0.26), (sx * 1.3, -1.7, 1.55), "bone", 0.0)
        box(f"lamp{sx}", (0.28, 0.08, 0.2), (sx * 0.8, 2.72, 1.3), "lens", 0.0)
        box(f"nosebone{sx}", (0.5, 0.06, 0.3), (sx * 0.8, 2.72, 1.0), "bone", 0.0)
        box(f"exh{sx}", (0.2, 0.2, 0.5), (sx * 1.05, 0.35, 2.25), "dark", 0.02)
    # ---- reactor housing: drum with glow window ring and cap, on a plinth
    box("plinth", (2.0, 1.9, 0.18), (0, -1.15, 2.1), "hull2", 0.05)
    cyl("drum", 0.95, 1.0, (0, -1.15, 2.7), "hull2", "Z", 10)
    cyl("drumband", 1.0, 0.16, (0, -1.15, 2.4), "oxide", "Z", 10)
    cyl("drumband2", 1.0, 0.12, (0, -1.15, 3.1), "bone", "Z", 10)
    for ang in range(0, 360, 90):
        a = math.radians(ang)
        cx, cy = 0.9 * math.sin(a), -1.15 + 0.9 * math.cos(a)
        box(f"winframe{ang}", (0.62, 0.1, 0.58), (cx, cy, 2.76), "dark", 0.0, rot=(0, 0, -ang))
        box(f"win{ang}", (0.5, 0.14, 0.44), (0.98 * math.sin(a), -1.15 + 0.98 * math.cos(a), 2.76), "glow", 0.0, rot=(0, 0, -ang))
    cyl("cap", 0.62, 0.1, (0, -1.15, 3.24), "glow", "Z", 12)
    cyl("capcore", 0.3, 0.14, (0, -1.15, 3.28), "dark", "Z", 8)
    cyl("capring", 0.78, 0.08, (0, -1.15, 3.2), "hull2", "Z", 12)
    # ---- radiator fins: banks either side of the drum and across the tail
    for sx in (-1, 1):
        for i in range(9):
            box(f"finS{sx}{i}", (0.42, 0.06, 0.8), (sx * 0.98 + sx * 0.0, -2.0 + i * 0.22, 2.55), "steel", 0.0)
        box(f"finrail{sx}", (0.46, 2.0, 0.07), (sx * 0.98, -1.1, 2.18), "dark", 0.0)
    for i in range(7):
        box(f"finT{i}", (0.06, 0.4, 0.7), (-0.9 + i * 0.3, -2.1, 2.5), "steel", 0.0)
    # ---- turret (own layer): squat radiation cannon
    S.begin("turret", PIVOT)
    px, py, pz = PIVOT
    cyl("tbase", 0.85, 0.4, (px, py, pz + 0.2), "hull2", "Z", 10)
    cyl("tdome", 0.68, 0.22, (px, py, pz + 0.5), "hull", "Z", 10)
    box("ttop", (0.62, 0.62, 0.05), (px, py - 0.05, pz + 0.64), "house", 0.0)
    for sx in (-1, 1):
        box(f"tband{sx}", (0.06, 0.8, 0.28), (px + sx * 0.76, py, pz + 0.3), "house", 0.0, rot=(0, 0, 0))
        cyl(f"tcoil{sx}", 0.17, 0.5, (px + sx * 0.5, py + 0.75, pz + 0.38), "oxide2", "Y", 10)
    box("mantlet", (0.75, 0.3, 0.5), (px, py + 0.62, pz + 0.38), "oxide", 0.06)
    cyl("barrel", 0.24, 1.1, (px, py + 1.3, pz + 0.38), "steel", "Y", 12)
    cyl("emitr1", 0.32, 0.14, (px, py + 1.0, pz + 0.38), "oxide", "Y", 12)
    cyl("emitr2", 0.3, 0.14, (px, py + 1.5, pz + 0.38), "bone", "Y", 12)
    cyl("muzzle", 0.2, 0.16, (px, py + 1.9, pz + 0.38), "glow", "Y", 12)
    cyl("hatch", 0.16, 0.05, (px - 0.25, py - 0.3, pz + 0.68), "dark", "Z", 8)
