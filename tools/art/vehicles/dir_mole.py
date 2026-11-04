"""Directorate burrowing troop carrier (mole) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Low wedge-shaped armoured hull on short tracks, a smaller banded drill nose, a rear ramp door (hazard-edged, folded down and
back), and five small vision slits across the sloped front.  House paint: big roof panel and flank panels.
States: idle (16 x 1) and dive (16 x 4).
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_dig_common as D

ID = "dir_mole"
TITLE = "Directorate burrowing troop carrier (mole)"
CANVAS = 128
TARGET = (0.0, 0.3, 0.7)
STATES = [("idle", 16, 1, 1), ("dive", 16, 4, 3)]
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS


def build(S, state, k, n):
    box, cyl, cone = S.box, S.cyl, S.cone
    dive = state == "dive"
    if dive:
        D.dive_pre(S, k, (0, -1.2, 0.5), 0.8)
    D.tracks(S, 0.98, 2.7, 0.56, 0.6, y0=-0.1, nwheel=4)
    # wedge hull: flat rear body + sloped glacis
    box("body", (1.78, 2.2, 0.55), (0, -0.4, 0.82), "conc2", 0.09)
    box("redlow", (1.8, 2.2, 0.2), (0, -0.4, 0.62), "red", 0.05)
    box("glacis", (1.78, 1.35, 0.5), (0, 0.95, 0.82), "red", 0.08, rot=(-22, 0, 0))
    box("roof", (1.5, 1.5, 0.09), (0, -0.55, 1.14), "conc", 0.03)
    box("roofpanel", (1.1, 1.1, 0.04), (0, -0.55, 1.2), "house", 0.0)
    box("rearplate", (1.78, 0.1, 0.5), (0, -1.5, 0.82), "conc3", 0.03)
    # five small vision slits across the slope
    for i in range(5):
        sx = (i - 2) * 0.33
        box(f"slitf{i}", (0.29, 0.12, 0.17), (sx, 1.28, 1.0), "bone", 0.0, rot=(-22, 0, 0))
        box(f"slit{i}", (0.2, 0.14, 0.07), (sx, 1.31, 1.03), "dark", 0.0, rot=(-22, 0, 0))
    # flank panels + bone bands
    for s in (-1, 1):
        box(f"flank{s}", (0.06, 1.2, 0.34), (s * 0.9, -0.5, 0.86), "house", 0.0)
        D.bands(S, f"fb{s}_", s * 0.9, 0.3, 0.95, 0.84, 0.3, 3)
    # rear ramp door: folded down and back, hazard edge
    box("ramp", (1.15, 0.85, 0.07), (0, -1.9, 0.5), "conc3", 0.02, rot=(26, 0, 0))
    box("rampedge", (1.15, 0.12, 0.09), (0, -2.25, 0.34), "bone", 0.0, rot=(26, 0, 0))
    for s in (-1, 1):
        box(f"ramprail{s}", (0.06, 0.85, 0.13), (s * 0.6, -1.9, 0.53), "redhi", 0.0, rot=(26, 0, 0))
        box(f"hinge{s}", (0.14, 0.1, 0.14), (s * 0.55, -1.5, 0.76), "steel2", 0.02)
    # smaller drill nose
    cyl("collar", 0.52, 0.2, (0, 1.72, 0.78), "steel2", "Y", 18)
    D.drill(S, 1.82, 1.0, 0.5, 0.78, n=4)
    if dive:
        D.dive_post(S, k, 1.6, 2.5, 0.35)
