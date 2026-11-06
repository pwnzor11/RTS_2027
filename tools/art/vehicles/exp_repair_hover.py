"""Expedition Mender (field repair hover) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Glide Tank hover style: a lagoon-white workshop box (navy skirt deck) with a welding bay open on one side (dark interior, orange spark lamp), a
slewing crane arm with a copper hook and a spare-plate cabinet on the tail.  Signal-orange cross plates on the roof read as "repair" at 1x.
Team colour: hull stripes, cabinet flash.  Hull number "01".  Footprint about 2.4 x 3.8 units.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_repair_hover"
TITLE = "Expedition Mender"
K = 0.82
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
    E.skirt(S, 2.4, 3.8, 0.0, 0.42, LIFT)
    S.lift(LIFT)
    box("hullL", (2.1, 3.4, 0.46), (0, 0, 0.66), "navy2", 0.16)
    box("deck", (1.95, 3.0, 0.1), (0, -0.05, 0.96), "navy", 0.04)
    box("prow", (1.9, 0.9, 0.4), (0, 1.6, 0.84), "navy", 0.14, rot=(-24, 0, 0))
    box("bumper", (1.95, 0.1, 0.1), (0, 2.0, 0.52), "chrome", 0.03)
    for s in (-1, 1):
        box(f"stripe{s}", (0.05, 2.9, 0.14), (s * 1.07, 0, 0.7), "house", 0.0)
        box(f"sill{s}", (0.07, 3.0, 0.05), (s * 1.07, 0, 0.9), "copper", 0.0)
        E.lamp(S, f"L{s}", s * 0.6, 2.05, 0.7)
    # workshop box (cab forward, bay aft)
    box("cab", (1.7, 1.2, 0.75), (0, 0.65, 1.38), "white", 0.12)
    box("cabglass", (1.4, 0.06, 0.26), (0, 1.26, 1.5), "glass", 0.0)
    box("cabroof", (1.5, 0.9, 0.05), (0, 0.65, 1.77), "white2", 0.01)
    box("crossH", (0.9, 0.3, 0.02), (0, 0.65, 1.8), "orange", 0.0)
    box("crossV", (0.3, 0.9, 0.02), (0, 0.65, 1.8), "orange", 0.0)
    for s in (-1, 1):
        box(f"cabflash{s}", (0.05, 0.9, 0.16), (s * 0.87, 0.65, 1.32), "house", 0.0)
        box(f"cabtrim{s}", (0.05, 1.1, 0.05), (s * 0.87, 0.65, 1.62), "copper", 0.0)
    # welding bay: open back wall, dark interior, spark lamp, bench
    box("bayfloor", (1.7, 1.4, 0.05), (0, -0.85, 1.0), "dark", 0.0)
    box("bayback", (1.7, 0.1, 0.7), (0, -0.2, 1.38), "white2", 0.03)
    box("bayroof", (1.8, 1.5, 0.07), (0, -0.9, 1.78), "white", 0.02)
    box("bayroofst", (1.8, 0.06, 0.08), (0, -0.2, 1.78), "orange", 0.0)
    for s in (-1, 1):
        box(f"baypost{s}", (0.08, 0.08, 0.75), (s * 0.85, -1.55, 1.4), "chrome2", 0.0)
        box(f"baywall{s}", (0.07, 1.4, 0.7), (s * 0.85, -0.85, 1.38), "white2", 0.03)
    box("bench", (0.9, 0.35, 0.3), (0.0, -0.7, 1.2), "chrome2", 0.04)
    sph("spark", 0.1, (0.0, -0.7, 1.45), "orange")
    box("cabinet", (0.8, 0.35, 0.55), (0.0, -1.7, 1.25), "navy", 0.05)
    box("cabflash2", (0.4, 0.05, 0.14), (0.0, -1.9, 1.25), "house", 0.0)
    # crane arm (slews from a post on the bay roof), hook, and a hull number plate on the prow deck
    cyl("cranepost", 0.14, 0.5, (-0.6, -0.9, 2.05), "chrome", "Z", 10)
    box("craneboom", (0.12, 1.6, 0.12), (-0.6, -0.1, 2.5), "copper", 0.0, rot=(-14, 0, 0))
    box("craneboom2", (0.07, 1.1, 0.07), (-0.6, 0.15, 2.7), "chrome", 0.0, rot=(-14, 0, 0))
    cyl("cable", 0.02, 0.4, (-0.6, 0.65, 2.45), "chrome2", "Z", 6)
    sph("hook", 0.08, (-0.6, 0.65, 2.22), "orange")
    cyl("craneb", 0.2, 0.1, (-0.6, -0.9, 1.84), "chrome2", "Z", 12)
    E.digits(S, "hn", "01", 0, 1.6, 1.0, 0.07, "white")
    E.nacelle(S, "L", -0.7, -1.83, 1.1, 0.32, 0.8)
    E.nacelle(S, "R", 0.7, -1.83, 1.1, 0.32, 0.8)
    cyl("beacon", 0.09, 0.14, (0.7, 0.05, 1.86), "lens", "Z", 10)
    S.lift(0)
