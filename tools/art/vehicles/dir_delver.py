"""Directorate Delver (armed ambush burrower) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Same family as the mole and the bore, built heavier and angrier: a squat, thickly armoured wedge hull on short tracks, a
fat banded drill head, and a short-barrelled gun mounted on the boring head itself (a steel saddle over the collar, stubby
barrel with a muzzle brake and an amber muzzle lamp).  The gun is fixed to the nose, so the vehicle turns to aim: no turret layer.
Dark oxide plating with bone bands.  House paint: roof plate and the big flank armour plates.
States: idle (16 x 1) and dive (16 x 4), like dir_mole / dir_bore (dir_dig_common dive_pre / dive_post: nose-down burrow into a crater).
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_dig_common as D

ID = "dir_delver"
TITLE = "Directorate Delver (armed ambush burrower)"
CANVAS = 144
TARGET = (0.0, 0.3, 0.8)
STATES = [("idle", 16, 1, 1), ("dive", 16, 4, 3)]
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS


def build(S, state, k, n):
    box, cyl, cone = S.box, S.cyl, S.cone
    dive = state == "dive"
    if dive:
        D.dive_pre(S, k, (0, -1.2, 0.55), 0.9)
    D.tracks(S, 1.05, 2.9, 0.62, 0.66, y0=-0.2, nwheel=5)
    # armoured wedge hull
    box("body", (1.98, 2.4, 0.6), (0, -0.5, 0.88), "red2", 0.09)
    box("redlow", (2.0, 2.4, 0.2), (0, -0.5, 0.66), "red", 0.05)
    box("glacis", (1.98, 1.1, 0.55), (0, 0.85, 0.88), "red", 0.08, rot=(-26, 0, 0))
    box("roof", (1.6, 1.6, 0.1), (0, -0.65, 1.22), "conc2", 0.03)
    box("roofpanel", (1.2, 1.2, 0.04), (0, -0.65, 1.29), "house", 0.0)
    box("rearplate", (1.98, 0.1, 0.55), (0, -1.7, 0.88), "conc3", 0.03)
    box("hump", (0.8, 0.8, 0.28), (0, -1.0, 1.4), "conc2", 0.06)      # engine hump behind the roof plate
    for s in (-1, 1):
        cyl(f"exh{s}", 0.1, 0.5, (s * 0.55, -1.55, 1.5), "steel2", "Z", 8)
        box(f"flank{s}", (0.07, 1.5, 0.42), (s * 1.0, -0.65, 0.9), "house", 0.0)
        D.bands(S, f"fb{s}_", s * 1.0, 0.1, 0.95, 0.9, 0.42, 3)
        box(f"slit{s}", (0.3, 0.1, 0.1), (s * 0.5, 1.15, 1.08), "dark", 0.0, rot=(-26, 0, 0))
    # head: collar, fat banded drill
    cyl("collar", 0.62, 0.24, (0, 1.65, 0.8), "steel2", "Y", 20)
    D.drill(S, 1.77, 1.15, 0.6, 0.8, n=4)
    # gun on the boring head: saddle over the collar, short barrel pointing forward above the drill base
    box("saddle", (0.6, 0.7, 0.2), (0, 1.5, 1.28), "steel2", 0.05)
    box("saddlecap", (0.46, 0.5, 0.1), (0, 1.5, 1.4), "conc3", 0.02)
    cyl("breech", 0.22, 0.5, (0, 1.4, 1.36), "steel", "Y", 12)
    cyl("barrel", 0.12, 1.0, (0, 2.15, 1.36), "steel2", "Y", 10)
    cyl("brake", 0.17, 0.16, (0, 2.7, 1.36), "dark", "Y", 10)
    cyl("brakering", 0.18, 0.05, (0, 2.58, 1.36), "bone", "Y", 10)
    cyl("muzzle", 0.07, 0.04, (0, 2.8, 1.36), "lens", "Y", 8)
    if dive:
        D.dive_post(S, k, 1.7, 2.6, 0.3)
