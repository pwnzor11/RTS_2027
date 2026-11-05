"""Left Behind Gleaner (ash harvester, light hopper) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A low tracked crawler: a faded-teal bus-panel cab hunched at front left, a wide rake-and-suction hood across the nose (hazard
yellow lip), and a big open hopper of mismatched patch plate behind the cab, heaped with grey ash, with a canvas shade rolled
over its front edge, a lashed barrel on the right and a stovepipe.  Tracks are mismatched (the right one shorter).
Footprint about 3.1 x 5.2 world units (M).  House paint: both hopper flank panels, the cab roof stripe, the mast flag.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as L

ID = "lb_gleaner"
TITLE = "Left Behind Gleaner (ash harvester)"
CANVAS = 192
TARGET = (0.0, 0.0, 1.1)
STATES = [("idle", 16, 1, 1)]
PALETTE = L.PALETTE
MATERIALS = L.MATERIALS


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    L.tracks(S, -1.3, -2.3, 2.1, "L", "rust", n=5)
    L.tracks(S, 1.3, -2.0, 1.7, "R", "hull2", n=4)
    box("frame", (2.9, 4.8, 0.2), (0, -0.1, 1.15), "ash2", 0.03)
    box("rearplate", (2.7, 0.1, 0.5), (0, -2.55, 1.35), "rust2", 0.02)
    # cab front left
    L.cab(S, -0.75, 1.35, 1.25, w=1.3, d=1.4, h=1.1, body="hull")
    box("cabstripe", (1.0, 1.0, 0.04), (-0.75, 1.3, 2.4), "house", 0.0)
    box("cabpatch", (0.07, 0.6, 0.4), (-1.42, 1.1, 1.8), "rust", 0.0)
    L.lamp(S, "L", -1.15, 2.1, 1.7)
    # suction hood / rake across the nose
    box("hood", (2.7, 0.9, 0.7), (0, 2.25, 1.0), "rust", 0.08)
    box("hoodlip", (2.8, 0.15, 0.2), (0, 2.75, 0.8), "hazard", 0.02)
    box("hoodtop", (2.2, 0.6, 0.1), (0.3, 2.2, 1.4), "ash", 0.02)
    cyl("duct", 0.22, 2.2, (0.9, 1.0, 1.8), "ash2", "Y", 10, rot=None)
    for i in range(5):
        box(f"tine{i}", (0.1, 0.4, 0.1), (-1.1 + i * 0.55, 2.95, 0.6), "dark", 0.0)
    # hopper behind
    box("hopper", (2.8, 2.7, 1.1), (0, -1.2, 1.9), "hull2", 0.06)
    box("hopin", (2.3, 2.3, 0.1), (0, -1.2, 2.5), "heap", 0.0)
    cyl("heap", 0.7, 0.5, (0.2, -1.5, 2.6), "heap", "Z", 10)
    cyl("heap2", 0.4, 0.4, (-0.6, -0.6, 2.55), "ash", "Z", 10)
    for s_, nm in ((-1, "L"), (1, "R")):
        box(f"hpanel{nm}", (0.06, 1.7, 0.7), (s_ * 1.42, -1.2, 1.95), "house", 0.0)
    box("hpatch1", (0.07, 0.8, 0.4), (-1.43, 0.0, 2.2), "ochre", 0.0)
    box("hpatch2", (0.07, 0.8, 0.45), (1.43, -2.2, 2.2), "rust", 0.0)
    box("hpatch3", (0.07, 0.6, 0.35), (1.43, 0.0, 1.75), "ash", 0.0)
    box("hlipF", (2.9, 0.12, 0.14), (0, 0.17, 2.52), "ochre", 0.0)
    cyl("shade", 0.22, 2.4, (0, 0.15, 2.7), "canvas", "X", 12)
    cyl("shadeB", 0.24, 0.3, (-1.1, 0.15, 2.7), "rust", "X", 12)
    # barrel + stovepipe + flag
    L.barrel(S, "a", 1.0, -2.0, 2.0, "rust", band="hazard")
    L.barrel(S, "b", 0.3, -2.1, 1.9, "hull", h=0.7)
    L.stack(S, "s", -1.0, 0.7, 2.7, 1.3)
    L.flag(S, "m", 1.35, -2.35, 1.4, h=1.4)
