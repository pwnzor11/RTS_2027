"""Directorate Land-Train (mobile HQ) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Two articulated armoured cars on tracks, joined by a dark bellows: the lead car carries a long rail-gun barrel on twin rails along
its spine (breech block at the back, barrel overhanging the nose), the rear car is the command car (tall roof, slit windows, a
lattice mast, whip antennas, a stepped loudspeaker horn pair).  Fixed body, one layer.  The two cars are rigid (no bend in 16 facings).
Footprint: 6.6 world units long overall (barrel tip included) and 2.6 wide = 1.94 x 0.76 cells, so at any of the 16 facings it
fits a 2x2-cell box (128 px); frame is 176 px so nothing clips.  The Authority Construction Yard is 3x3 cells (192 px).
House paint: big flank panels on both cars, rail-gun housing top plates, command-car roof panel and loudspeaker stripe.
"""
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_common as D

ID = "dir_landtrain"
TITLE = "Directorate Land-Train (mobile HQ)"
CANVAS = 176
TARGET = (0.0, 0.0, 1.2)
STATES = [("idle", 16, 1, 1)]
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS


def tracks(S, y0, y1, tag):
    box, cyl = S.box, S.cyl
    yc, ln = (y0 + y1) / 2, y1 - y0
    for s, side in ((-1, "L"), (1, "R")):
        x = s * 1.12
        box(f"trk{tag}{side}", (0.7, ln - 0.6, 0.8), (x, yc, 0.45), "track", 0.12)
        cyl(f"idA{tag}{side}", 0.45, 0.7, (x, y1 - 0.35, 0.45), "track", "X")
        cyl(f"idB{tag}{side}", 0.45, 0.7, (x, y0 + 0.35, 0.45), "track", "X")
        for i in range(5):
            y = y0 + 0.5 + i * (ln - 1.0) / 4
            cyl(f"wh{tag}{side}{i}", 0.3, 0.1, (x + s * 0.38, y, 0.42), "steel", "X", 14)
            cyl(f"hb{tag}{side}{i}", 0.11, 0.08, (x + s * 0.44, y, 0.42), "rust", "X", 8)
        box(f"skirt{tag}{side}", (0.07, ln - 0.2, 0.32), (x + s * 0.37, yc, 0.95), "hull2", 0.02)


def build(S, state, k, n):
    box, cyl, cone = S.box, S.cyl, S.cone
    S.begin("body")
    # ---- lead car: y 0.2 .. 3.1
    tracks(S, 0.2, 3.1, "A")
    box("hullA", (2.1, 2.9, 0.9), (0, 1.65, 1.1), "hull", 0.1)
    box("deckA", (2.1, 2.9, 0.1), (0, 1.65, 1.6), "hull2", 0.04)
    box("noseA", (2.1, 0.5, 0.5), (0, 3.0, 1.0), "oxide", 0.08, rot=(0, 0, 0))
    box("glacisA", (2.0, 0.8, 0.12), (0, 2.85, 1.55), "oxide", 0.04, rot=(-22, 0, 0))
    box("bonenoseA", (1.2, 0.06, 0.26), (0, 3.28, 1.0), "bone", 0.0)
    for sx in (-1, 1):
        box(f"flankA{sx}", (0.07, 2.2, 0.5), (sx * 1.07, 1.55, 1.2), "house", 0.0)
        box(f"plateA{sx}", (0.3, 1.2, 0.05), (sx * 0.7, 1.9, 1.67), "house", 0.0)
        box(f"railA{sx}", (0.1, 2.5, 0.18), (sx * 0.2, 1.75, 1.74), "dark", 0.02)
        box(f"lampA{sx}", (0.24, 0.07, 0.16), (sx * 0.7, 3.3, 1.2), "lens", 0.0)
    for y in (0.7, 1.5, 2.3):
        box(f"bracket{y}", (0.62, 0.14, 0.14), (0, y, 1.86), "hull2", 0.02)
    box("breech", (0.55, 0.9, 0.5), (0, 0.75, 2.05), "hull2", 0.07)
    box("breechtop", (0.4, 0.6, 0.04), (0, 0.75, 2.32), "house", 0.0)
    cyl("barrel", 0.17, 3.3, (0, 1.85, 2.0), "steel", "Y", 14)
    cyl("barrelsleeve", 0.24, 1.1, (0, 1.0, 2.0), "hull2", "Y", 14)
    cyl("coil1", 0.26, 0.12, (0, 1.9, 2.0), "oxide", "Y", 14)
    cyl("coil2", 0.26, 0.12, (0, 2.4, 2.0), "oxide", "Y", 14)
    cyl("coil3", 0.26, 0.12, (0, 2.9, 2.0), "bone", "Y", 14)
    cyl("muzzle", 0.2, 0.2, (0, 3.45, 2.0), "dark", "Y", 14)
    # ---- joint / bellows
    box("bellows", (1.5, 0.5, 0.8), (0, 0.0, 1.15), "dark", 0.1)
    for i in range(3):
        box(f"fold{i}", (1.6, 0.06, 0.9), (0, -0.15 + i * 0.15, 1.15), "hull2", 0.0)
    box("coupling", (0.6, 0.6, 0.12), (0, 0.0, 0.7), "steel", 0.03)
    # ---- rear command car: y -3.1 .. -0.2
    tracks(S, -3.1, -0.2, "B")
    box("hullB", (2.1, 2.9, 1.0), (0, -1.65, 1.15, ), "hull", 0.1)
    box("cabinB", (1.9, 2.3, 0.7), (0, -1.6, 1.95), "hull2", 0.1)
    box("roofB", (1.95, 2.4, 0.08), (0, -1.6, 2.33), "hull", 0.03)
    box("roofhouse", (0.9, 0.8, 0.04), (-0.3, -2.0, 2.39), "house", 0.0)
    box("tailB", (2.1, 0.1, 0.9), (0, -3.1, 1.2), "hull2", 0.03)
    box("tailstripe", (1.4, 0.06, 0.2), (0, -3.16, 1.2), "oxide", 0.0)
    for sx in (-1, 1):
        box(f"flankB{sx}", (0.07, 2.2, 0.46), (sx * 1.07, -1.65, 1.1), "house", 0.0)
        for i in range(4):
            box(f"slit{sx}{i}", (0.07, 0.34, 0.12), (sx * 0.96, -2.5 + i * 0.58, 2.02), "glass", 0.0)
        box(f"bonebandB{sx}", (0.08, 2.0, 0.1), (sx * 0.96, -1.6, 1.74), "bone", 0.0)
    box("frontslitB", (1.2, 0.05, 0.14), (0, -0.46, 2.0), "glass", 0.0)
    # lattice mast with a dish, whip antennas, loudspeaker horns on the roof
    box("mastbase", (0.4, 0.4, 0.14), (0.55, -2.55, 2.46), "dark", 0.02)
    for dx, dy in ((-0.1, -0.1), (0.1, -0.1), (-0.1, 0.1), (0.1, 0.1)):
        cyl(f"mast{dx}{dy}", 0.025, 1.5, (0.55 + dx, -2.55 + dy, 3.2), "steel", "Z", 6)
    for i in range(4):
        box(f"mastx{i}", (0.24, 0.24, 0.03), (0.55, -2.55, 2.7 + i * 0.35), "steel", 0.0)
    cyl("dish", 0.38, 0.06, (0.55, -2.4, 4.0), "bone", rot=(-70, 0, 0), verts=14)
    cyl("dishfeed", 0.03, 0.4, (0.55, -2.33, 4.0), "dark", rot=(-70, 0, 0), verts=6)
    for sx, h in ((-1, 1.7), (1, 1.2)):
        cyl(f"whip{sx}", 0.02, h, (sx * 0.75, -2.9, 2.4 + h / 2), "dark", "Z", 5)
        S.sphere(f"whiptip{sx}", 0.05, (sx * 0.75, -2.9, 2.4 + h), "oxide")
    cyl("hornpole", 0.05, 0.5, (-0.45, -1.0, 2.6), "steel", "Z", 6)
    for hx in (-0.7, -0.2):
        cyl(f"hornA{hx}", 0.09, 0.3, (hx, -0.95, 2.95), "dark", rot=(-90, 0, 0), verts=8)
        cone(f"horn{hx}", 0.3, 0.08, 0.55, (hx, -0.7, 2.95), "bone", rot=(-90, 0, 0))
        cyl(f"hornring{hx}", 0.3, 0.05, (hx, -0.43, 2.95), "oxide", rot=(-90, 0, 0), verts=12)
    box("hornbar", (0.9, 0.08, 0.08), (-0.45, -0.95, 2.82), "steel", 0.0)
