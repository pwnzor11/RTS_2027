"""Left Behind Harpoon Wagon (anti-armour, tethers targets) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Four-wheeled flatbed: faded-teal bus-panel cab with a rust hood, a rust-red bed with a lashed cable reel and barrels, and on the
bed a swivelling harpoon gun on its own turret layer (pivot on the middle of the bed): a long banded tube on a pedestal with a
barbed head poking out, a house-coloured gun shield, a coiled tether drum and a canvas-wrapped breech.
Layers: body (truck + whole-vehicle shadow), turret (the gun).  Footprint about 2.5 x 4.6 units (M).
House paint: cab doors, bed side panels, the gun shield.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as L

ID = "lb_harpoon_wagon"
TITLE = "Left Behind Harpoon Wagon"
CANVAS = 192
TARGET = (0.0, 0.0, 1.0)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT = (0.0, -0.7, 1.12)
MOUNT = dict(deck_z=1.0, r_lip=0.75, r_well=0.0, r_shadow=1.0, soft_r=0.95, lip="rust2", well="dark")
PALETTE = L.PALETTE
MATERIALS = L.MATERIALS


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    for s, side in ((-1, "L"), (1, "R")):
        L.wheel(S, side + "F", s * 1.1, 1.4, 0.52, 0.4, "rust")
        L.wheel(S, side + "R", s * 1.1, -1.6, 0.52 if s < 0 else 0.46, 0.4, "ash" if s < 0 else "hazard")
        box(f"guard{side}", (0.5, 1.3, 0.07), (s * 1.1, 1.4, 1.08), "hull2", 0.02)
        box(f"guardR{side}", (0.5, 1.3, 0.07), (s * 1.1, -1.6, 1.08), "rust2", 0.02)
    box("axleF", (2.2, 0.12, 0.12), (0, 1.4, 0.52), "dark", 0)
    box("axleR", (2.2, 0.12, 0.12), (0, -1.6, 0.52), "dark", 0)
    box("frame", (1.7, 4.6, 0.24), (0, -0.1, 0.8), "ash2", 0.03)
    L.cab(S, 0, 1.55, 0.95, w=1.8, d=1.4, h=1.2, body="hull", roof="rust2")
    box("hood", (1.6, 0.9, 0.75), (0, 2.55, 1.25), "rust", 0.07)
    box("hoodpatch", (0.9, 0.6, 0.04), (-0.3, 2.55, 1.64), "ochre", 0.0)
    box("grille", (1.1, 0.05, 0.38), (0, 3.02, 1.2), "dark", 0.0)
    for s in (-1, 1):
        box(f"door{s}", (0.05, 0.8, 0.55), (s * 0.92, 1.5, 1.45), "house", 0.0)
        L.lamp(S, f"hl{s}", s * 0.55, 3.05, 1.15)
    box("cabpatch", (0.07, 0.5, 0.3), (0.92, 1.95, 1.75), "bone", 0.0)
    L.stack(S, "c", -0.8, 1.0, 2.5, 0.9)
    # bed
    box("deck", (1.9, 2.9, 0.14), (0, -1.1, 0.95), "wood", 0.03)
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"rail{nm}", (0.08, 2.9, 0.3), (s * 0.95, -1.1, 1.15), "rust" if s < 0 else "hull2", 0.03)
        box(f"railpanel{nm}", (0.06, 1.3, 0.25), (s * 0.99, -1.4, 1.17), "house", 0.0)
    box("tail", (1.9, 0.1, 0.35), (0, -2.55, 1.15), "hazard", 0.03)
    L.barrel(S, "a", 0.65, -2.1, 1.35, "rust", r=0.3, h=0.7)
    L.barrel(S, "b", -0.65, -2.2, 1.35, "hull", r=0.3, h=0.7, band="hazard")
    cyl("reel", 0.38, 0.6, (-0.65, -0.1, 1.45), "ash2", "X", 14)
    cyl("reelrope", 0.4, 0.4, (-0.65, -0.1, 1.45), "rope", "X", 14)
    L.flag(S, "m", 0.9, -2.5, 1.2, h=1.5)
    # ---- turret: harpoon gun
    S.mount(PIVOT, **MOUNT)
    S.begin("turret", PIVOT)
    px, py, pz = PIVOT
    cyl("tt", 0.62, 0.12, (px, py, pz + 0.06), "ash2", "Z", 20)
    box("ped", (0.5, 0.5, 0.4), (px, py, pz + 0.3), "rust", 0.04)
    box("breech", (0.6, 1.0, 0.55), (px, py - 0.35, pz + 0.75), "rust2", 0.06)
    cyl("breechcanvas", 0.3, 0.8, (px, py - 0.55, pz + 1.05), "canvas", "Y", 12)
    cyl("tube", 0.2, 2.4, (px, py + 0.95, pz + 0.85), "steel", "Y", 12)
    cyl("tubeband1", 0.23, 0.2, (px, py + 0.5, pz + 0.85), "hazard", "Y", 12)
    cyl("tubeband2", 0.23, 0.3, (px, py + 1.4, pz + 0.85), "house", "Y", 12)         # S16 team colour: tube band reads on every facing
    cyl("mouth", 0.25, 0.14, (px, py + 2.18, pz + 0.85), "dark", "Y", 12)
    S.cone("harp", 0.14, 0.0, 0.55, (px, py + 2.3, pz + 0.85), "steel", rot=(-90, 0, 0), verts=8)
    for s in (-1, 1):
        box(f"barb{s}", (0.2, 0.2, 0.05), (px + s * 0.1, py + 2.12, pz + 0.85), "steel", 0.0, rot=(0, 0, s * 25))
    box("shield", (1.3, 0.07, 0.8), (px, py + 0.1, pz + 0.75), "ash2", 0.02)
    box("shieldpanel", (1.0, 0.08, 0.5), (px, py + 0.15, pz + 0.75), "house", 0.0)
    cyl("drum", 0.34, 0.5, (px + 0.6, py - 0.3, pz + 0.55), "ash", "X", 14)
    cyl("drumrope", 0.36, 0.3, (px + 0.6, py - 0.3, pz + 0.55), "rope", "X", 14)
    box("sight", (0.14, 0.3, 0.14), (px - 0.4, py - 0.1, pz + 1.15), "dark", 0.02)
