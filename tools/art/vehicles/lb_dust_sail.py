"""Left Behind Dust Sail (wind-rigged Blackout layer) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

A land-yacht: a long narrow plank-and-bus-panel hull on three fat wheels (two forward, one steering at the tail, all mismatched),
a tall raked mast carrying a big patched sailcloth main sail set along the keel (house-coloured emblem panel and a rust patch),
a small jib, a boom, and amidships a hopper of fine dust with a sack chute that the crew shake out over the enemy.
Tall silhouette on purpose: the sail dwarfs the hull.  Footprint about 2.2 x 4.8 units (M).
House paint: the sail emblem, the pennant at the masthead, the hull flank panels.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as L

ID = "lb_dust_sail"
TITLE = "Left Behind Dust Sail"
CANVAS = 224
TARGET = (0.0, 0.0, 2.0)
STATES = [("idle", 16, 1, 1)]
PALETTE = L.PALETTE
MATERIALS = L.MATERIALS


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    L.wheel(S, "FL", -1.15, 1.2, 0.55, 0.42, "rust")
    L.wheel(S, "FR", 1.15, 1.2, 0.5, 0.42, "ash")
    L.wheel(S, "T", 0.0, -1.9, 0.4, 0.3, "hazard")
    box("axleF", (2.3, 0.12, 0.12), (0, 1.2, 0.55), "dark", 0)
    box("hullplank", (1.5, 4.4, 0.25), (0, 0.0, 0.85), "wood", 0.04)
    box("hull", (1.3, 3.4, 0.55), (0, 0.4, 1.2), "hull", 0.07)
    box("bow", (1.1, 0.9, 0.5), (0, 2.4, 1.1), "rust", 0.1)
    box("bowlip", (1.2, 0.12, 0.2), (0, 2.88, 0.95), "hazard", 0.02)
    box("tail", (1.0, 0.9, 0.45), (0, -2.1, 1.1), "hull2", 0.07)
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"flank{nm}", (0.06, 1.8, 0.35), (s * 0.67, 0.4, 1.25), "house", 0.0)
        box(f"flpatch{nm}", (0.07, 0.8, 0.3), (s * 0.67, -0.9, 1.2), "ochre" if s < 0 else "rust", 0.0)
    box("deckpatch", (0.9, 1.0, 0.04), (0, 1.3, 1.5), "canvas2", 0.0)
    # dust hopper amidships with chute
    box("hopper", (1.0, 1.1, 0.7), (0, -0.7, 1.7), "rust2", 0.05)
    box("hopperdust", (0.8, 0.9, 0.06), (0, -0.7, 2.07), "dust", 0.0)
    cyl("sack1", 0.3, 0.6, (-0.3, -0.7, 2.2), "canvas", "X", 10)
    cyl("sack2", 0.26, 0.5, (0.35, -0.8, 2.2), "canvas2", "X", 10)
    box("chute", (0.3, 0.9, 0.12), (0.7, -1.1, 1.35), "steel", 0.02, rot=(-20, 0, 25))
    # cockpit hump + helm
    box("cockpit", (0.9, 0.9, 0.12), (0, 1.0, 1.52), "dark", 0.02)
    cyl("tiller", 0.04, 1.0, (0, 1.0, 1.7), "wood", "Y", 6)
    # mast, boom, sails
    cyl("mast", 0.1, 5.4, (0, 0.9, 4.0), "wood", "Z", 8, rot=(-4, 0, 0))
    cyl("masthead", 0.14, 0.12, (0, 0.7, 6.7), "rust2", "Z", 8)
    cyl("boom", 0.07, 3.2, (0, -0.6, 2.4), "wood", "Y", 6)
    # main sail: big sheet in the keel plane, raked
    box("sail", (0.05, 3.0, 3.8), (0, -0.7, 4.5), "canvas", 0.0, rot=(-2, 0, 0))
    box("sail2", (0.06, 1.2, 1.4), (0, -1.5, 3.4), "canvas2", 0.0)
    box("sailemblem", (0.08, 1.3, 1.1), (0, -0.4, 4.8), "house", 0.0)
    box("sailpatch", (0.07, 0.8, 0.8), (0, -1.5, 5.6), "rust", 0.0)
    box("sailseam", (0.07, 3.0, 0.07), (0, -0.7, 4.0), "rope", 0.0)
    box("sailseam2", (0.07, 3.0, 0.07), (0, -0.7, 5.2), "rope", 0.0)
    box("jib", (0.04, 1.6, 2.4), (0, 1.9, 3.3), "canvas2", 0.0, rot=(0, 0, 0))
    cyl("jibstay", 0.03, 3.6, (0, 1.9, 2.9), "rope", "Z", 5, rot=(-25, 0, 0))
    box("pennant", (0.04, 0.9, 0.35), (0, -0.2, 6.55), "house", 0.0)
    for y, z in ((2.3, 1.2), (-2.0, 1.2)):
        dy, dz = y - 0.9, 6.6 - z
        cyl(f"stay{y}", 0.025, (dy * dy + dz * dz) ** 0.5, (0, (0.9 + y) / 2, (z + 6.6) / 2), "rope", "Z", 4, rot=(-math_atan(dy, dz), 0, 0))
    L.barrel(S, "a", 0.0, -2.0, 1.75, "hull", r=0.28, h=0.6)


def math_atan(dy, dz):
    import math
    return math.degrees(math.atan2(dy, dz))
