"""Expedition Hoverlander (amphibious APC) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Glide Tank hover style (rubber skirt with ash tidemark, lifted hull).  A boxy navy troop box with a lagoon-white roof, glass slits, a rear door
with an orange hazard bar, a roof hatch with a light-gun mount (fixed, no turret layer), a big caged lift fan on the roof tail and two rear
nacelles.  Team colour: hull stripes and fan-cage flash.  Hull number "05" on the roof.  Footprint about 2.5 x 3.8 units.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_hoverlander"
TITLE = "Expedition Hoverlander"
K = 0.84
LIFT = 0.14
CANVAS = 128
TARGET = (0.0, 0.0, 0.9 * K)
STATES = [("idle", 16, 1, 1)]
PALETTE = E.PALETTE
MATERIALS = dict(E.MATERIALS)


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    E.skirt(S, 2.5, 3.8, 0.0, 0.42, LIFT)
    S.lift(LIFT)
    box("hullL", (2.2, 3.4, 0.46), (0, 0, 0.66), "navy2", 0.16)
    box("box", (2.1, 2.5, 0.62), (0, -0.2, 1.2), "navy", 0.15)
    box("prow", (2.0, 0.9, 0.5), (0, 1.55, 0.86), "navy", 0.14, rot=(-26, 0, 0))
    box("bumper", (2.0, 0.1, 0.1), (0, 2.0, 0.52), "chrome", 0.03)
    box("roof", (1.9, 2.3, 0.05), (0, -0.2, 1.53), "white", 0.01)
    box("roofstripe", (1.9, 0.07, 0.055), (0, 0.55, 1.53), "orange", 0.0)
    box("glass", (1.5, 0.06, 0.2), (0, 1.0, 1.28), "glass", 0.0, rot=(-26, 0, 0))
    for s in (-1, 1):
        box(f"stripe{s}", (0.05, 2.9, 0.14), (s * 1.12, 0.0, 0.7), "house", 0.0)
        box(f"sill{s}", (0.07, 3.0, 0.05), (s * 1.12, 0.0, 0.9), "copper", 0.0)
        box(f"slit{s}", (0.05, 1.8, 0.16), (s * 1.06, 0.0, 1.32), "glass", 0.0)
        box(f"troof{s}", (0.06, 2.3, 0.06), (s * 0.97, -0.2, 1.52), "copper", 0.0)
        E.lamp(S, f"L{s}", s * 0.6, 2.05, 0.72)
    # rear door
    box("door", (1.1, 0.07, 0.5), (0, -1.47, 1.2), "navy2", 0.02)
    box("doorbar", (1.1, 0.08, 0.1), (0, -1.5, 1.2), "orange", 0.0)
    # roof hatch + light gun
    cyl("hatchring", 0.38, 0.08, (-0.5, 0.15, 1.58), "chrome", "Z", 18)
    cyl("hatch", 0.3, 0.06, (-0.5, 0.15, 1.64), "navy2", "Z", 16)
    box("gunmt", (0.26, 0.3, 0.2), (-0.5, 0.2, 1.8), "chrome2", 0.05)
    cyl("gun", 0.05, 1.0, (-0.5, 0.8, 1.82), "chrome", "Y", 8)
    cyl("guncol", 0.08, 0.12, (-0.5, 0.45, 1.82), "copper", "Y", 8)
    E.digits(S, "hn", "05", 0.15, 0.55, 1.57, 0.07, "white")
    # caged lift fan aft
    cyl("fanb", 0.62, 0.1, (0.0, -0.95, 1.6), "chrome2", "Z", 22)
    cyl("fanhole", 0.5, 0.06, (0.0, -0.95, 1.66), "dark", "Z", 22)
    cyl("fanhub", 0.12, 0.1, (0.0, -0.95, 1.7), "copper", "Z", 10)
    for q in range(3):
        box(f"blade{q}", (0.95, 0.12, 0.03), (0, -0.95, 1.7), "chrome", 0.0, rot=(0, 0, q * 60))
    for s in (-1, 1):
        cyl(f"cage{s}", 0.03, 0.4, (s * 0.58, -0.95, 1.82), "chrome", "Z", 6)
        cyl(f"cagey{s}", 0.03, 0.4, (0, -0.95 + s * 0.58, 1.82), "chrome", "Z", 6)
    box("fanflash", (0.5, 0.06, 0.12), (0, -1.6, 1.4), "house", 0.0)
    for s in (-1, 1):
        E.nacelle(S, f"{s}", s * 0.8, -1.82, 1.0, 0.3, 0.8)
    S.lift(0)
