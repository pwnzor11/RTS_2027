"""Left Behind Dune Buggy (fast raider scout, light gun) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Four fat mismatched tyres under a lopsided tub of bus-panel sheet: faded-teal left side, rust-red right side with a bone patch,
an open cockpit with a pipe roll-cage, a spare tyre on the tail, a light gun on a pintle at the rear, and a tall whip aerial
carrying a house-coloured pennant.  A rolled canvas bedroll rides across the bonnet.  Footprint about 2.0 x 3.0 units (S).
House paint: both door panels, the bonnet patch, the pennant.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as L

ID = "lb_buggy"
TITLE = "Left Behind Dune Buggy"
CANVAS = 128
TARGET = (0.0, 0.0, 0.8)
STATES = [("idle", 16, 1, 1)]
PALETTE = L.PALETTE
MATERIALS = L.MATERIALS


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    for s, side in ((-1, "L"), (1, "R")):
        L.wheel(S, side + "F", s * 0.95, 1.05, 0.42, 0.36, "rust" if s < 0 else "ash")
        L.wheel(S, side + "R", s * 0.95, -0.95, 0.5 if s > 0 else 0.42, 0.42, "hazard" if s > 0 else "rust")
    box("axleF", (1.8, 0.1, 0.1), (0, 1.05, 0.42), "dark", 0)
    box("axleR", (1.8, 0.1, 0.1), (0, -0.95, 0.46), "dark", 0)
    box("chassis", (1.3, 3.0, 0.2), (0, 0.0, 0.62), "ash2", 0.02)
    # lopsided tub: teal left half, rust right half
    box("tubL", (0.7, 1.9, 0.6), (-0.3, -0.35, 0.95), "hull", 0.06)
    box("tubR", (0.65, 1.9, 0.5), (0.35, -0.35, 0.9), "rust", 0.06)
    box("hood", (1.25, 1.0, 0.45), (0, 1.0, 0.9), "hull2", 0.07)
    box("hoodpatch", (0.8, 0.6, 0.04), (0.15, 1.05, 1.14), "house", 0.0)
    box("hoodrust", (0.4, 0.5, 0.04), (-0.4, 1.2, 1.14), "rust", 0.0)
    box("grille", (0.9, 0.05, 0.25), (0, 1.52, 0.88), "dark", 0.0)
    L.lamp(S, "L", -0.4, 1.55, 1.0)
    L.lamp(S, "R", 0.35, 1.55, 0.95)
    box("doorL", (0.05, 0.9, 0.38), (-0.67, -0.2, 1.0), "house", 0.0)
    box("doorR", (0.05, 0.8, 0.34), (0.69, -0.2, 0.95), "house", 0.0)
    box("patchL", (0.06, 0.6, 0.3), (-0.67, -1.0, 0.9), "ochre", 0.0)
    box("patchR", (0.06, 0.5, 0.3), (0.69, 0.4, 0.85), "bone", 0.0)
    box("rearplate", (1.3, 0.06, 0.5), (0, -1.32, 0.92), "rust2", 0.0)
    # open cockpit: seats, dash, roll-cage of pipe
    box("dash", (1.2, 0.14, 0.2), (0, 0.5, 1.2), "dark", 0.02)
    box("seatL", (0.4, 0.4, 0.14), (-0.3, -0.05, 1.18), "canvas2", 0.04)
    box("seatR", (0.4, 0.4, 0.14), (0.3, -0.05, 1.18), "rust2", 0.04)
    box("seatbL", (0.4, 0.08, 0.35), (-0.3, -0.3, 1.38), "canvas2", 0.03)
    box("seatbR", (0.4, 0.08, 0.35), (0.3, -0.3, 1.38), "rust2", 0.03)
    for s in (-1, 1):
        cyl(f"cageF{s}", 0.04, 0.9, (s * 0.62, 0.42, 1.5), "steel", "Z", 6)
        cyl(f"cageR{s}", 0.04, 0.9, (s * 0.62, -0.55, 1.5), "steel", "Z", 6)
        cyl(f"cageT{s}", 0.04, 0.97, (s * 0.62, -0.06, 1.95), "steel", "Y", 6)
    cyl("cageX", 0.04, 1.24, (0, 0.42, 1.95), "steel", "X", 6)
    # bedroll over the bonnet, spare tyre at the tail, light gun on a pintle at the rear
    cyl("bedroll", 0.2, 1.2, (0, 1.35, 1.25), "canvas", "X", 10)
    cyl("bedrollstrap", 0.22, 0.08, (-0.4, 1.35, 1.25), "rust", "X", 10)
    cyl("spare", 0.42, 0.22, (0, -1.5, 1.05), "track", "Y", 16)
    cyl("sparehub", 0.2, 0.25, (0, -1.5, 1.05), "bone", "Y", 10)
    cyl("post", 0.05, 0.6, (0.0, -0.95, 1.55), "steel", "Z", 8)
    box("gun", (0.14, 0.6, 0.14), (0, -0.75, 1.9), "dark", 0.02)
    cyl("gunbarrel", 0.04, 0.8, (0, -0.2, 1.9), "steel", "Y", 6)
    box("ammo", (0.18, 0.28, 0.16), (0.16, -0.95, 1.8), "hazard", 0.02)
    # whip aerial with a house pennant
    cyl("whip", 0.025, 2.0, (-0.62, -1.3, 2.1), "dark", "Z", 5)
    box("pennant", (0.03, 0.7, 0.3), (-0.62, -1.65, 3.0), "house", 0.0)
