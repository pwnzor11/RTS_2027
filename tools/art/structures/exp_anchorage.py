"""Expedition Anchorage (2x2, shipment dock; ALL FOUR CELLS WATER, D-153/D-156 floating dock, touches the shore on any side).  Original design.

A floating pontoon platform on the water: no land and no earth in the sprite.  The hull floats a little above the lagoon with a navy fender
skirt, a lagoon-white foam ring where it meets the water, an orange-lined deck, chrome bollards, a team-colour cabin, a dock crane (variant A),
a fuel gantry with hose (B) or a davit crane with telex mast (C), tanks, a short ramp stub to the north edge and a team flag.
Variants (env ANCH_VARIANT=A|B|C, default = the recommended one): A square hull + lattice crane, B stepped-octagon hull + fuel gantry,
C twin cylindrical float tubes + davit crane and mast.
Idle: hook / hose reel lamp, flag, lamps.  Damage (structural, D-069): deck corner collapsed into the water, jib bent, cabin roof peeled;
fire/smoke are FX anchors.  Build: site buoys, hull, cabin, lifting gear, ramp, flag.
Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_anchorage"
TITLE = "Expedition Anchorage"
FOOTPRINT = (2, 2)
CANVAS = (128, 192)
ORIGIN = (0, 64)
DOCK = (0, 0)
VAR = os.environ.get("ANCH_VARIANT", "C").upper()
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 1.6, -1.6, 0.8, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 1.6, -1.6, 1.2, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", -1.6, 1.2, 1.8, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -1.6, 1.2, 2.1, 5),
]
DECK = 0.62     # deck surface height above the water


def hull_rects():
    """(hx, hy) half extents of the overlapping boxes that make the hull outline."""
    if VAR == "B":
        return [(2.95, 2.1), (2.1, 2.95), (2.6, 2.6)]
    if VAR == "C":
        return [(2.95, 2.5)]
    return [(2.9, 2.9)]


def g_ground(S, dmg, i):
    rects = hull_rects()
    for k, (hx, hy) in enumerate(rects):
        S.bb(f"foam{k}", -hx - 0.22, hx + 0.22, -hy - 0.22, hy + 0.22, 0.0, 0.04, "white2", 0.0, nocast=True)
    for k, (hx, hy) in enumerate(rects):
        if VAR == "C":
            for sx in (-1, 1):
                S.cyl(f"tube{sx}", 0.62, 2 * hy + 0.2, (sx * 2.35, 0.0, 0.25), "white2", axis="Y", verts=16)
                S.cyl(f"band{sx}a", 0.66, 0.35, (sx * 2.35, -1.7, 0.25), "orange", axis="Y", verts=16, nocast=True)
                S.cyl(f"band{sx}b", 0.66, 0.35, (sx * 2.35, 1.7, 0.25), "orange", axis="Y", verts=16, nocast=True)
                S.sphere(f"cap{sx}s", 0.62, (sx * 2.35, -hy - 0.1, 0.25), "white2", nocast=False, squash=0.9)
            for yy in (-1.9, 0.0, 1.9):
                S.bb("strut", -2.4, 2.4, yy - 0.2, yy + 0.2, 0.4, 0.58, "navy2", 0.03)
            S.bb("deckC", -2.0, 2.0, -hy, hy, 0.46, DECK, "pad2", 0.04)
            S.bb("deckrimC", -2.0, 2.0, -hy - 0.05, hy + 0.05, 0.46, 0.56, "navy2", 0.03)
            if k == 0:
                for sx in (-1, 1):
                    S.bb(f"railC{sx}", sx * 2.35 - 0.04, sx * 2.35 + 0.04, -hy + 0.2, hy - 0.2, 0.7, 0.74, "orange", 0.0, nocast=True)
            continue
        S.bb(f"hull{k}", -hx, hx, -hy, hy, 0.0, 0.5 - 0.01 * k, "navy2", 0.05)
        S.bb(f"fender{k}", -hx - 0.05, hx + 0.05, -hy - 0.05, hy + 0.05, 0.22 + 0.01 * k, 0.46 - 0.01 * k, "navy", 0.04)
        S.bb(f"deck{k}", -hx + 0.05, hx - 0.05, -hy + 0.05, hy - 0.05, 0.4, DECK - 0.012 * k, "pad2", 0.0)
    if VAR != "C":
        hx, hy = rects[0][0], rects[0][1]
        if VAR == "B":
            hx, hy = 2.5, 2.5
        for yy in (-hy + 0.12, hy - 0.12):
            S.bb("edgeEW", -hx + 0.1, hx - 0.1, yy - 0.06, yy + 0.06, DECK, DECK + 0.02, "orange", 0.0, nocast=True)
        for xx in (-hx + 0.12, hx - 0.12):
            S.bb("edgeNS", xx - 0.06, xx + 0.06, -hy + 0.1, hy - 0.1, DECK, DECK + 0.02, "orange", 0.0, nocast=True)
        for k in range(5):
            S.bb("fendstripe", -2.5 + k * 1.25, -2.1 + k * 1.25, -rects[0][1] - 0.07, -rects[0][1] - 0.05, 0.25, 0.43, "orange", 0.0, nocast=True)
    # bollards (chrome) around the rim
    bx = 2.6 if VAR != "C" else 2.45
    by = 2.6 if VAR != "C" else 2.3
    for k, (x, y) in enumerate(((-bx, -by), (bx, -by), (-bx, by), (bx, by), (0.0, -by), (0.0, by))):
        S.cyl(f"bol{k}", 0.15, 0.3, (x, y, DECK + 0.15), "chrome2", verts=10)
        S.cyl(f"bolcap{k}", 0.2, 0.06, (x, y, DECK + 0.32), "chrome", verts=10, nocast=True)


def g_cabin(S, dmg, i):
    x0, x1, y0, y1 = -2.5, -0.7, 0.9, 2.4
    S.bb("cab", x0, x1, y0, y1, DECK, DECK + 1.2, "white", 0.07)
    S.bb("cstripe", x0, x1, y0 - 0.02, y0, DECK + 0.55, DECK + 0.85, "house", 0.0, nocast=True)
    S.bb("cwin", x0 + 0.25, x1 - 0.25, y0 - 0.02, y0, DECK + 0.9, DECK + 1.1, "glass", 0.0, nocast=True)
    S.bb("cdoor", x1 - 0.55, x1 - 0.2, y0 - 0.02, y0, DECK, DECK + 0.7, "orange", 0.0, nocast=True)
    if not dmg:
        S.bb("croof", x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, DECK + 1.2, DECK + 1.4, "navy", 0.05)
    else:
        S.bb("croof", x0 - 0.1, (x0 + x1) / 2, y0 - 0.1, y1 + 0.1, DECK + 1.2, DECK + 1.4, "navy", 0.05)
        S.box("cflap", (1.0, 1.0, 0.1), ((x0 + x1) / 2 + 0.3, 1.9 * S.YS, DECK + 1.65), "navy", 0.0, rot=(0, -22, 8), stretch=False)
    # stowed cargo drums / crates beside the cabin
    for k in range(2):
        S.cyl(f"drum{k}", 0.28, 0.6, (-2.3 + k * 0.7, 0.2, DECK + 0.3), "orange" if k == 0 else "navy", verts=12)


def g_tanks(S, dmg, i):
    """Two horizontal fuel tanks on cradles (dock stores)."""
    for k, yy in enumerate((0.4, 1.4)):
        S.cyl(f"tank{k}", 0.38, 1.7, (0.5, yy, DECK + 0.5), "white2", axis="X", verts=14)
        S.bb(f"tband{k}", 0.2, 0.4, yy - 0.4, yy + 0.4, DECK + 0.5, DECK + 0.9, "house", 0.0, nocast=True)
        S.bb(f"cradle{k}", -0.2, 1.2, yy - 0.1, yy + 0.1, DECK, DECK + 0.2, "chrome2", 0.0, nocast=True)


def g_lift(S, dmg, i):
    if VAR == "B":
        return _gantry(S, dmg, i)
    if VAR == "C":
        return _davit(S, dmg, i)
    return _crane(S, dmg, i)


def _crane(S, dmg, i):
    cx, cy = 1.9, -0.6
    for ax in (-0.25, 0.25):
        for ay in (-0.25, 0.25):
            K.post(S, "cw", cx + ax, cy + ay, DECK, 3.6, 0.09, "chrome2")
    for z in (1.4, 2.4):
        S.bb("brace", cx - 0.25, cx + 0.25, cy - 0.27, cy - 0.23, z, z + 0.06, "chrome2", 0.0, nocast=True)
    S.bb("cab2", cx - 0.35, cx + 0.35, cy - 0.35, cy + 0.35, 3.6, 3.9, "house", 0.04)
    if dmg:
        K.beam(S, "jib", (cx, cy, 3.8), (cx - 0.8, cy - 1.0, 2.7), 0.2, "copper")
    else:
        K.beam(S, "jib", (cx, cy, 3.8), (cx - 0.2, cy - 2.3, 3.8), 0.2, "copper")
        sw = 0.18 * math.sin(i * math.pi / 2)
        K.beam(S, "cable", (cx - 0.2, cy - 2.1, 3.75), (cx - 0.2 + sw, cy - 2.1, 1.7), 0.04, "dark", nocast=True)
        S.bb("hook", cx - 0.3 + sw, cx - 0.1 + sw, cy - 2.2, cy - 2.0, 1.5, 1.7, "orange", 0.0)


def _gantry(S, dmg, i):
    """Fuel gantry: portal frame over the berth with a hose reel, hanging hose and a house-colour pump housing."""
    gx = 1.7
    for sy in (-1.6, 0.2):
        K.post(S, "gp", gx - 1.0, sy, DECK, 2.9, 0.14, "chrome2")
        K.post(S, "gp", gx + 1.0, sy, DECK, 2.9, 0.14, "chrome2")
    if dmg:
        S.bb("gtopx", gx - 1.1, gx + 0.1, -1.68, -1.52, 2.9, 3.06, "chrome2", 0.02)
        K.beam(S, "gtopb", (gx + 0.2, -0.7, 3.0), (gx + 1.0, -0.7, 2.2), 0.14, "chrome2")
        return
    for yy in (-1.6, 0.2):
        S.bb("gtopx", gx - 1.1, gx + 1.1, yy - 0.08, yy + 0.08, 2.9, 3.06, "chrome2", 0.02)
    S.bb("gtopy", gx + 0.2, gx + 0.4, -1.65, 0.25, 2.95, 3.1, "orange", 0.02)
    S.bb("pump", gx - 0.9, gx - 0.3, -0.5, 0.0, DECK, DECK + 1.0, "house", 0.05)
    S.bb("pwin", gx - 0.8, gx - 0.4, -0.52, -0.5, DECK + 0.55, DECK + 0.85, "glass", 0.0, nocast=True)
    S.cyl("reel", 0.38, 0.5, (gx + 0.3, -0.7, 3.4), "orange", axis="X", verts=14)
    sw = 0.12 * math.sin(i * math.pi / 2)
    K.beam(S, "hose", (gx + 0.3, -0.7, 3.25), (gx + 0.3 + sw, -1.1, 1.5), 0.07, "dark", nocast=True)
    S.bb("nozzle", gx + 0.2 + sw, gx + 0.4 + sw, -1.2, -1.0, 1.2, 1.55, "chrome", 0.0)
    P.bubble_lamp(S, "gl", gx + 1.0, -1.6, 3.15, i % 2 == 0)


def _davit(S, dmg, i):
    """Davit crane on a pedestal with a swinging boat hook, plus a telex mast with a turning dish."""
    cx, cy = 1.9, -0.4
    S.cyl("ped", 0.4, DECK + 1.2 - DECK, (cx, cy, DECK + 0.6), "chrome2", verts=12)
    S.cyl("pedb", 0.45, 0.2, (cx, cy, DECK + 1.2), "house", verts=12)
    if dmg:
        K.beam(S, "boom", (cx, cy, DECK + 1.3), (cx - 0.9, cy - 0.8, 2.3), 0.2, "copper")
    else:
        K.beam(S, "boom", (cx, cy, DECK + 1.3), (cx, cy, 3.2), 0.2, "chrome2")
        K.beam(S, "arm", (cx, cy, 3.2), (cx - 0.2, cy - 2.0, 3.5), 0.18, "copper")
        sw = 0.18 * math.sin(i * math.pi / 2)
        K.beam(S, "cable", (cx - 0.2, cy - 1.9, 3.45), (cx - 0.2 + sw, cy - 1.9, 1.8), 0.04, "dark", nocast=True)
        S.bb("hook", cx - 0.3 + sw, cx - 0.1 + sw, cy - 2.0, cy - 1.8, 1.6, 1.8, "orange", 0.0)
    mx, my, top = -1.0, -1.6, 3.4
    K.post(S, "mast", mx, my, DECK, top, 0.1, "chrome2")
    if dmg:
        K.beam(S, "mastbent", (mx, my, 2.0), (mx + 0.8, my, 2.9), 0.1, "chrome2")
        return
    K.post(S, "mast2", mx, my, top, top + 0.4, 0.06, "chrome")
    S.box("dish", (0.9, 0.1, 0.4), (mx, my * S.YS, top - 0.3), "white2", 0.0, rot=(0, 0, i * 45), stretch=False)
    P.bubble_lamp(S, "ml", mx, my, top + 0.55, i % 2 == 0)


def g_ramp(S, dmg, i):
    """Short ramp stub to the north edge (generic gangway end, reads on any coast)."""
    y0 = 2.7 if VAR != "C" else 2.6
    run, rise = 0.7, 0.3
    ang = math.degrees(math.atan2(rise, run * S.YS))
    ln = math.hypot(run * S.YS, rise) / S.YS
    S.box("ramp", (1.3, ln, 0.08), (0.9, y0 + run / 2, DECK - rise / 2 + 0.04), "chrome2", 0.02, rot=(ang, 0, 0))
    for s in (-1, 1):
        S.box(f"rrail{s}", (0.07, ln, 0.1), (0.9 + s * 0.65, y0 + run / 2, DECK - rise / 2 + 0.12), "orange", 0.0, rot=(ang, 0, 0))


def g_flag(S, dmg, i):
    K.flag(S, "flag", -2.5 if VAR != "C" else -1.6, -2.3, 3.0 if not dmg else 2.2, i, w=1.1, h=0.7, dmg=dmg)
    for k, x in enumerate((-0.4, 0.4)):
        K.lamp_post(S, str(k), 0.2 + x * 2, 2.45, (i + k) % 2 == 0)


GROUPS = [g_ground, g_cabin, g_lift, g_tanks, g_ramp, g_flag]


def site(S):
    """Build frame 0: a staked-out berth on the water: orange marker buoys joined by a rope, a few empty float drums."""
    for (sx, sy) in ((-3.1, -3.1), (3.1, -3.1), (-3.1, 3.1), (3.1, 3.1)):
        S.sphere("buoy", 0.28, (sx, sy, 0.12), "orange", nocast=False, squash=0.9)
        K.post(S, "bpost", sx, sy, 0.12, 0.9, 0.06, "chrome2")
        S.bb("bflag", sx, sx + 0.35, sy - 0.02, sy + 0.02, 0.6, 0.9, "orange", 0.0, nocast=True)
    for yy in (-3.1, 3.1):
        S.bb("line", -3.1, 3.1, yy - 0.03, yy + 0.03, 0.05, 0.1, "rope", 0.0, nocast=True)
    for xx in (-3.1, 3.1):
        S.bb("line", xx - 0.03, xx + 0.03, -3.1, 3.1, 0.05, 0.1, "rope", 0.0, nocast=True)
    for k, (x, y) in enumerate(((-1.2, -0.8), (0.4, 0.9), (1.4, -0.6))):
        S.cyl(f"fdrum{k}", 0.42, 0.7, (x, y, 0.2), "white2", axis="X", verts=12)


def dmg_extra(S, i):
    # deck breach with charring where the corner collapsed
    S.bb("hole", 0.9, 2.6, -2.6, -1.0, DECK, DECK + 0.02, "char", 0.0, nocast=True)
    S.bb("scorch", -2.4, -0.8, 1.0, 2.3, DECK + 1.41, DECK + 1.43, "char", 0.0, nocast=True)
    S.box("slab", (1.5, 1.4, 0.12), (2.0, -2.0 * S.YS, 0.35), "pad_s", 0.0, rot=(0, 18, 0), stretch=False)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
