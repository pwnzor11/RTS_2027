"""Left Behind Salvage Hauler (wreck collector, wreck-to-hopper) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Six-wheeled flatbed truck: faded-teal bus-panel cab at the front, a rust-red open scrap bin on the back heaped with wreck
plate, pipes and a tyre, and a hinged grapple boom folded over the bin with a hazard-yellow claw hanging at the rear.  A
lashed barrel and a canvas tarp ride on the deck, a mast flag flies at the tail.  Footprint about 2.6 x 5.0 units (M).
House paint: cab door panels, the bin sides, the mast flag.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as L

ID = "lb_hauler"
TITLE = "Left Behind Salvage Hauler"
CANVAS = 208
TARGET = (0.0, 0.0, 1.1)
STATES = [("idle", 16, 1, 1)]
PALETTE = L.PALETTE
MATERIALS = L.MATERIALS


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    for s, side in ((-1, "L"), (1, "R")):
        for i, (y, r) in enumerate(((1.6, 0.5), (-0.9, 0.5), (-2.0, 0.44 if s < 0 else 0.5))):
            L.wheel(S, f"{side}{i}", s * 1.1, y, r, 0.4, "rust" if i != 1 else "ash")
    for i, y in enumerate((1.6, -0.9, -2.0)):
        box(f"axle{i}", (2.2, 0.12, 0.12), (0, y, 0.5), "dark", 0)
    box("frame", (1.8, 5.2, 0.24), (0, -0.2, 0.85), "ash2", 0.03)
    # cab (teal) front, with a rust hood and a patched door
    L.cab(S, 0, 1.75, 1.0, w=1.9, d=1.5, h=1.3, body="hull", roof="rust2")
    box("hood", (1.7, 0.9, 0.8), (0, 2.8, 1.35), "rust", 0.07)
    box("hoodtop", (1.2, 0.6, 0.04), (0.2, 2.8, 1.77), "ochre", 0.0)
    box("grille", (1.2, 0.05, 0.4), (0, 3.27, 1.3), "dark", 0.0)
    for s in (-1, 1):
        box(f"door{s}", (0.05, 0.9, 0.6), (s * 0.97, 1.6, 1.55), "house", 0.0)
        L.lamp(S, f"hl{s}", s * 0.6, 3.3, 1.2)
        box(f"guard{s}", (0.5, 3.0, 0.07), (s * 1.1, 1.6, 0.98), "hull2", 0.02)
    box("cabpatch", (0.07, 0.6, 0.35), (-0.97, 2.1, 1.8), "bone", 0.0)
    L.stack(S, "c", 0.85, 1.1, 2.6, 1.0)
    # scrap bin on the back: walls, heap of salvage, tyre
    box("binfloor", (2.1, 3.0, 0.14), (0, -1.45, 1.0), "wood", 0.03)
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"bin{nm}", (0.1, 3.0, 0.7), (s * 1.0, -1.45, 1.4), "rust" if s > 0 else "hull2", 0.04)
        box(f"binpanel{nm}", (0.06, 1.8, 0.45), (s * 1.06, -1.4, 1.45), "house", 0.0)
    box("binB", (2.1, 0.1, 0.7), (0, -2.95, 1.4), "rust2", 0.04)
    box("binF", (2.1, 0.1, 0.9), (0, 0.05, 1.5), "ash2", 0.04)
    box("plate1", (1.3, 1.1, 0.12), (-0.2, -1.2, 1.75), "ash", 0.0, rot=(0, 8, 10))
    box("plate2", (1.0, 0.9, 0.12), (0.3, -2.1, 1.85), "ochre", 0.0, rot=(5, -10, -12))
    box("plate3", (0.8, 1.0, 0.1), (-0.4, -2.3, 1.7), "hull", 0.0, rot=(0, 0, 25))
    cyl("tyre", 0.5, 0.3, (0.4, -0.5, 1.9), "track", "Z", 14, rot=(20, 15, 0))
    cyl("pipeA", 0.1, 2.2, (-0.5, -1.6, 2.0), "steel", "Y", 8, rot=(8, 0, 6))
    cyl("pipeB", 0.1, 1.8, (0.7, -1.8, 1.95), "rust2", "Y", 8, rot=(-6, 0, -4))
    # grapple boom: pivot post, arm laid back over the bin, cable and a hanging claw
    box("boompost", (0.4, 0.4, 1.0), (-0.75, -0.2, 2.0), "ash2", 0.03)
    box("boom", (0.25, 3.6, 0.25), (-0.75, -1.9, 2.55), "hazard", 0.03, rot=(-6, 0, 0))
    cyl("cable", 0.03, 0.9, (-0.75, -3.65, 2.2), "dark", "Z", 5)
    box("clawbody", (0.5, 0.4, 0.3), (-0.75, -3.65, 1.7), "ash2", 0.03)
    for i, dx in enumerate((-0.25, 0.0, 0.25)):
        box(f"claw{i}", (0.1, 0.12, 0.55), (-0.75 + dx, -3.65, 1.4), "hazard" if i == 1 else "steel", 0.0)
    # lashed barrel, canvas tarp, flag
    L.barrel(S, "a", 0.0, 0.5, 1.7, "hull", r=0.32, h=0.7, band="rust")
    box("tarp", (1.0, 0.8, 0.3), (0.55, 0.25, 1.35), "canvas2", 0.12)
    L.flag(S, "m", 1.0, -3.05, 1.7, h=1.7)
