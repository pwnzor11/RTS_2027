"""Directorate boring machine (the tunnelling vehicle that deploys into a Hatch) for tools/art/build_vehicle.py.

Long tracked red-oxide body dominated by a huge rotary boring head at the front: banded rim, ringed cutter teeth, red face,
bone nose cone.  Two exhaust stacks and two flared warning horns on the roof, big house flank panels, hazard bands.
States: idle (16 x 1) and dive (16 x 4).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_common as D

ID = "dir_bore"
TITLE = "Directorate boring machine (Hatch tunneller)"
CANVAS = 144
TARGET = (0.0, 0.3, 1.0)
STATES = [("idle", 16, 1, 1), ("dive", 16, 4, 3)]
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS


def build(S, state, k, n):
    box, cyl, cone, sph = S.box, S.cyl, S.cone, S.sphere
    dive = state == "dive"
    if dive:
        D.dive_pre(S, k, (0, -2.0, 0.6), 1.25)
    D.tracks(S, 1.12, 4.1, 0.7, 0.8, y0=-0.55, nwheel=7)
    box("hull", (2.1, 3.5, 0.72), (0, -0.55, 1.08), "red", 0.1)
    box("deck", (2.1, 3.5, 0.08), (0, -0.55, 1.46), "conc2", 0.04)
    box("rearplate", (2.1, 0.1, 0.7), (0, -2.33, 1.08), "conc2", 0.03)
    D.vbands(S, "rbnd", -2.4, -1.0, 1.0, 1.0, 0.4, 6)
    box("engine", (1.5, 1.7, 0.55), (0, -0.9, 1.78), "red2", 0.08)
    box("engroof", (1.56, 1.76, 0.07), (0, -0.9, 2.08), "conc2", 0.03)
    for s in (-1, 1):
        box(f"flank{s}", (0.06, 2.3, 0.5), (s * 1.07, -0.7, 1.1), "house", 0.0)
        D.bands(S, f"fb{s}_", s * 1.07, 0.55, 1.3, 1.1, 0.5, 4)
        box(f"engside{s}", (0.05, 1.2, 0.34), (s * 0.77, -0.9, 1.8), "house", 0.0)
    # cab slit block behind the head
    box("cab", (1.1, 0.7, 0.45), (0, 0.55, 1.75), "red2", 0.07)
    box("cabroof", (1.16, 0.76, 0.07), (0, 0.55, 2.0), "conc2", 0.03)
    box("cabslit", (0.9, 0.04, 0.1), (0, 0.91, 1.82), "glass", 0.0)
    # exhaust stacks (rear) with ribbed collars and caps
    for s in (-1, 1):
        x = s * 0.55
        cyl(f"stk{s}", 0.17, 1.2, (x, -1.75, 2.55), "conc", "Z", 14)
        cyl(f"stkr{s}", 0.22, 0.07, (x, -1.75, 2.1), "steel2", "Z", 14)
        cyl(f"stkb{s}", 0.19, 0.08, (x, -1.75, 2.75), "redhi", "Z", 14)
        cyl(f"stkc{s}", 0.22, 0.07, (x, -1.75, 3.17), "dark", "Z", 14)
    # warning horns on the cab roof, flared forward
    for s in (-1, 1):
        cone(f"horn{s}", 0.32, 0.08, 0.75, (s * 0.4, 0.6, 2.32), "brass", 14, rot=(98, 0, 0))
        cyl(f"hornm{s}", 0.27, 0.03, (s * 0.4, 0.98, 2.34), "dark", "Y", 12)
    # the boring head
    hz, hy = 1.4, 1.85
    cyl("headback", 1.0, 0.45, (0, 1.2, hz), "conc3", "Y", 24)
    cyl("headdisc", 1.32, 0.5, (0, hy, hz), "conc2", "Y", 28)
    cyl("headrim", 1.34, 0.12, (0, hy + 0.12, hz), "bone", "Y", 28)
    cyl("headrim2", 1.34, 0.12, (0, hy - 0.12, hz), "redhi", "Y", 28)
    cyl("headface", 1.05, 0.12, (0, hy + 0.3, hz), "red", "Y", 24)
    cyl("headring", 0.62, 0.14, (0, hy + 0.36, hz), "bone", "Y", 20)
    for i in range(14):
        a = 2 * math.pi * i / 14
        cone(f"tooth{i}", 0.17, 0.02, 0.5, (1.18 * math.cos(a), hy + 0.5, hz + 1.18 * math.sin(a)), "steel2", 8, rot=(-90, 0, 0))
    for i in range(8):
        a = 2 * math.pi * (i + 0.5) / 8
        cone(f"toothi{i}", 0.14, 0.02, 0.42, (0.78 * math.cos(a), hy + 0.5, hz + 0.78 * math.sin(a)), "steel", 8, rot=(-90, 0, 0))
    cone("headnose", 0.42, 0.04, 0.9, (0, hy + 0.7, hz), "bone", 18, rot=(-90, 0, 0))
    if dive:
        D.dive_post(S, k, 2.1, 3.0, 0.2)
