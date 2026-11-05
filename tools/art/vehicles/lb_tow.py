"""Left Behind Tow Truck (transport 6, tows packed rigs) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

A big six-wheeled truck: long faded-teal bus cab with a rust hood and a front winch bumper, a canvas-hooped passenger bed
(sail-cloth over bent pipe bows, flap rolled up on one side), and at the tail a heavy hazard-yellow A-frame tow hitch with a
chain.  Spare tyre on the cab roof, stovepipe, mast flag.  Footprint about 2.7 x 5.6 units (M).
House paint: both cab doors, the canvas hood's side flaps, the mast flag.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as L

ID = "lb_tow"
TITLE = "Left Behind Tow Truck"
CANVAS = 208
TARGET = (0.0, 0.0, 1.2)
STATES = [("idle", 16, 1, 1)]
PALETTE = L.PALETTE
MATERIALS = L.MATERIALS


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    for s, side in ((-1, "L"), (1, "R")):
        for i, y in enumerate((2.0, -0.7, -1.9)):
            L.wheel(S, f"{side}{i}", s * 1.2, y, 0.56 if i != 2 or s > 0 else 0.5, 0.44, "rust" if i != 1 else "ash")
        box(f"guardF{side}", (0.55, 1.3, 0.07), (s * 1.2, 2.0, 1.2), "hull2", 0.02)
    for i, y in enumerate((2.0, -0.7, -1.9)):
        box(f"axle{i}", (2.4, 0.12, 0.12), (0, y, 0.56), "dark", 0)
    box("frame", (1.9, 5.6, 0.26), (0, -0.2, 0.92), "ash2", 0.03)
    # cab
    L.cab(S, 0, 2.0, 1.05, w=2.0, d=1.7, h=1.4, body="hull", roof="rust2")
    box("hood", (1.8, 0.9, 0.85), (0, 3.15, 1.45), "rust", 0.07)
    box("hoodpatch", (1.0, 0.6, 0.04), (0.3, 3.15, 1.89), "bone", 0.0)
    box("grille", (1.3, 0.05, 0.4), (0, 3.62, 1.4), "dark", 0.0)
    box("winch", (2.1, 0.4, 0.35), (0, 3.7, 1.05), "hazard", 0.04)
    cyl("winchdrum", 0.2, 0.9, (0, 3.75, 1.35), "steel", "X", 12)
    for s in (-1, 1):
        box(f"door{s}", (0.05, 1.0, 0.65), (s * 1.02, 1.9, 1.7), "house", 0.0)
        L.lamp(S, f"hl{s}", s * 0.65, 3.64, 1.5)
    box("cabpatch", (0.07, 0.6, 0.35), (-1.02, 2.4, 2.0), "ochre", 0.0)
    cyl("spare", 0.5, 0.3, (0, 1.8, 2.7), "track", "Z", 14, rot=(8, 0, 0))
    cyl("sparehub", 0.22, 0.34, (0, 1.8, 2.7), "rust", "Z", 10, rot=(8, 0, 0))
    L.stack(S, "c", 0.9, 1.2, 2.8, 1.0)
    # passenger bed with canvas hoops
    box("bedfloor", (2.2, 3.0, 0.14), (0, -1.2, 1.1), "wood", 0.03)
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"side{nm}", (0.08, 3.0, 0.5), (s * 1.08, -1.2, 1.4), "rust" if s > 0 else "hull2", 0.03)
    for i, y in enumerate((-2.4, -1.5, -0.6, 0.2)):
        cyl(f"bow{i}", 0.06, 2.1, (0, y, 2.1), "steel", "X", 6)
        for s in (-1, 1):
            cyl(f"bowp{i}{s}", 0.05, 0.8, (s * 1.0, y, 1.75), "steel", "Z", 6)
    box("hood_c", (1.9, 3.2, 0.1), (0, -1.1, 2.3), "canvas", 0.05)
    box("hood_c2", (0.7, 3.2, 0.12), (0.5, -1.1, 2.32), "canvas2", 0.03)
    box("flapL", (0.06, 2.0, 0.65), (-1.02, -1.0, 1.95), "house", 0.0, rot=(0, 0, 0))
    box("flapR", (0.1, 1.4, 0.9), (1.05, -1.0, 1.85), "canvas2", 0.0)
    box("hoodpatch2", (0.8, 0.9, 0.04), (-0.4, -2.0, 2.37), "rust", 0.0)
    box("tailgate", (2.2, 0.1, 0.5), (0, -2.75, 1.4), "rust2", 0.03)
    # A-frame tow hitch + chain at the tail
    for s in (-1, 1):
        box(f"arm{s}", (0.14, 1.4, 0.14), (s * 0.35, -3.4, 1.0), "hazard", 0.02, rot=(0, 0, s * -12))
    box("hitch", (0.5, 0.3, 0.3), (0, -4.1, 0.95), "dark", 0.03)
    cyl("hook", 0.14, 0.2, (0, -4.25, 0.95), "steel", "Z", 8)
    for i in range(4):
        cyl(f"chain{i}", 0.05, 0.18, (-0.15 + i * 0.1, -3.4 - i * 0.15, 1.2 - i * 0.05), "dark", "X", 6)
    L.flag(S, "m", 1.05, -2.6, 1.4, h=1.7)
