"""Directorate burrowing scraper (tunnel-borne harvester) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Squat red-oxide armoured body on low tracks, big banded conical auger/drill nose, left-hand slit cab, and an open scoop
hopper full of spoil at the rear, fed by an inclined chute.  Red-and-bone hazard bands on the nose block, flanks and drill.
House paint: big flank panels and the hopper sides.  States: idle (16 x 1) and dive (16 x 4: nose-down burrow into a crater).
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_dig_common as D

ID = "dir_scraper"
TITLE = "Directorate burrowing scraper (tunnel-borne harvester)"
CANVAS = 144
TARGET = (0.0, 0.3, 0.9)
STATES = [("idle", 16, 1, 1), ("dive", 16, 4, 3)]
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    dive = state == "dive"
    if dive:
        D.dive_pre(S, k, (0, -1.3, 0.6), 1.0)
    D.tracks(S, 1.0, 2.9, 0.62, 0.72, y0=-0.05, nwheel=5)
    # hull + deck
    box("hull", (1.95, 2.7, 0.62), (0, -0.1, 0.98), "red", 0.1)
    box("deck", (1.95, 2.7, 0.08), (0, -0.1, 1.32), "conc2", 0.04)
    box("rearplate", (1.95, 0.1, 0.55), (0, -1.48, 0.98), "conc2", 0.03)
    # nose block housing the drill drive, hazard-banded front face
    box("nose", (1.95, 0.55, 0.62), (0, 1.5, 1.0), "conc2", 0.08)
    D.vbands(S, "nbnd", 1.8, -0.9, 0.9, 1.0, 0.5, 6)
    cyl("collar", 0.86, 0.22, (0, 1.9, 1.05), "steel2", "Y", 20)
    # drill: banded cone
    D.drill(S, 2.0, 1.55, 0.82, 1.05, n=5)
    # flank house panels + hazard bands at the front end of the flank
    for s in (-1, 1):
        box(f"flank{s}", (0.06, 1.35, 0.46), (s * 0.99, -0.35, 1.02), "house", 0.0)
        D.bands(S, f"fb{s}_", s * 0.99, 0.5, 1.2, 1.02, 0.46, 4)
    # cab (left): slit window, roof, beacon
    box("cab", (0.78, 0.9, 0.5), (-0.5, 0.65, 1.6), "red2", 0.07)
    box("cabroof", (0.84, 0.96, 0.07), (-0.5, 0.65, 1.88), "conc2", 0.03)
    box("cabslit", (0.62, 0.04, 0.12), (-0.5, 1.11, 1.68), "glass", 0.0)
    box("cabslitS", (0.04, 0.5, 0.12), (-0.91, 0.7, 1.68), "glass", 0.0)
    cyl("beacon", 0.09, 0.14, (-0.5, 0.45, 1.98), "lens" if (not dive or k % 2 == 0) else "rust", "Z", 10)
    # inclined chute from drill house back up to the hopper
    box("chute", (0.7, 1.5, 0.1), (0.4, 0.5, 1.5), "steel", 0.02, rot=(-18, 0, 0))
    for s in (0, 1):
        box(f"chrail{s}", (0.07, 1.5, 0.16), (0.4 + (s - 0.5) * 0.74, 0.5, 1.58), "conc3", 0.02, rot=(-18, 0, 0))
    # rear scoop hopper: walls, banded lip, spoil heap
    for s in (-1, 1):
        box(f"hwall{s}", (0.1, 1.2, 0.75), (s * 0.92, -0.85, 1.72), "conc2", 0.03)
        box(f"hside{s}", (0.04, 0.9, 0.45), (s * 0.98, -0.85, 1.7), "house", 0.0)
    box("hrear", (1.95, 0.1, 0.75), (0, -1.45, 1.72), "conc2", 0.03)
    box("hfront", (1.75, 0.1, 0.55), (0, -0.25, 1.62), "conc2", 0.03)
    D.vbands(S, "hlip", -1.51, -0.9, 0.9, 1.98, 0.14, 6)
    sph("spoil", 1.0, (0, -0.85, 1.75), "earth2", scale=(0.88, 0.58, 0.34))
    for i, (x, y, r) in enumerate(((-0.35, -1.0, 0.28), (0.3, -0.65, 0.26), (0.05, -1.2, 0.22))):
        sph(f"lump{i}", r, (x, y, 2.0 + 0.02 * i), "earth", scale=(1, 1, 0.7))
    # stack
    cyl("stk", 0.12, 0.9, (0.75, 0.15, 1.8), "conc3", "Z", 12)
    cyl("stkcap", 0.16, 0.07, (0.75, 0.15, 2.28), "brass", "Z", 12)
    if dive:
        D.dive_post(S, k, 1.9, 2.8, 0.5)
