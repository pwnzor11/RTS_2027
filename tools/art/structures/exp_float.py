"""Floating-dock kit (variant C language, D-153/D-156): white float tubes with orange bands, grey deck on navy rim, foam ring, bollards,
team-colour cabin, davit crane, short ramp stub.  Everything stands on water; no land.  Used by exp_quay / exp_shipyard / exp_drydock
(exp_anchorage keeps its own copy of the same shapes).  Units: ground units, 3.4 per cell, y north, centred on the footprint."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

DECK = 0.62
TR = 0.62       # tube radius


def foam(S, x0, x1, y0, y1):
    S.bb("foam", x0 - 0.22, x1 + 0.22, y0 - 0.22, y1 + 0.22, 0.0, 0.04, "white2", 0.0, nocast=True)


def tube_y(S, x, y0, y1, caps=(True, True)):
    foam(S, x - TR, x + TR, y0 - TR * 0.9, y1 + TR * 0.9)
    S.cyl("tube", TR, y1 - y0, (x, (y0 + y1) / 2, 0.25), "white2", axis="Y", verts=16)
    for yy in (y0 + 0.5, y1 - 0.5):
        S.cyl("band", TR + 0.04, 0.35, (x, yy, 0.25), "orange", axis="Y", verts=16, nocast=True)
    for yy, c in zip((y0, y1), caps):
        if c:
            S.sphere("cap", TR, (x, yy, 0.25), "white2", nocast=False, squash=0.9)


def tube_x(S, y, x0, x1, caps=(True, True)):
    foam(S, x0 - TR * 0.9, x1 + TR * 0.9, y - TR, y + TR)
    S.cyl("tube", TR, x1 - x0, ((x0 + x1) / 2, y, 0.25), "white2", axis="X", verts=16)
    for xx in (x0 + 0.5, x1 - 0.5):
        S.cyl("band", TR + 0.04, 0.35, (xx, y, 0.25), "orange", axis="X", verts=16, nocast=True)
    for xx, c in zip((x0, x1), caps):
        if c:
            S.sphere("cap", TR, (xx, y, 0.25), "white2", nocast=False, squash=0.9)


def deck(S, x0, x1, y0, y1, edge=True):
    foam(S, x0, x1, y0, y1)
    S.bb("drim", x0 - 0.05, x1 + 0.05, y0 - 0.05, y1 + 0.05, 0.32, 0.5, "navy2", 0.03)
    S.bb("deck", x0, x1, y0, y1, 0.46, DECK, "pad2", 0.03)
    if edge:
        S.bb("edgeA", x0 + 0.1, x1 - 0.1, y0 + 0.1, y0 + 0.2, DECK, DECK + 0.02, "orange", 0.0, nocast=True)
        S.bb("edgeB", x0 + 0.1, x1 - 0.1, y1 - 0.2, y1 - 0.1, DECK, DECK + 0.02, "orange", 0.0, nocast=True)


def bollards(S, pts):
    for x, y in pts:
        S.cyl("bol", 0.15, 0.3, (x, y, DECK + 0.15), "chrome2", verts=10)
        S.cyl("bolcap", 0.2, 0.06, (x, y, DECK + 0.32), "chrome", verts=10, nocast=True)


def ramp(S, x, y, side):
    """Short ramp stub down to the water on edge 'N' or 'E' (generic gangway end)."""
    run, rise = 0.7, 0.3
    if side == "N":
        ang = math.degrees(math.atan2(rise, run * S.YS))
        ln = math.hypot(run * S.YS, rise) / S.YS
        S.box("ramp", (1.3, ln, 0.08), (x, y + run / 2, DECK - rise / 2 + 0.04), "chrome2", 0.02, rot=(-ang, 0, 0))
        for s in (-1, 1):
            S.box("rrail", (0.07, ln, 0.1), (x + s * 0.65, y + run / 2, DECK - rise / 2 + 0.12), "orange", 0.0, rot=(-ang, 0, 0))
    else:
        ang = math.degrees(math.atan2(rise, run))
        ln = math.hypot(run, rise)
        S.box("ramp", (ln, 1.3, 0.08), (x + run / 2, y, DECK - rise / 2 + 0.04), "chrome2", 0.02, rot=(0, ang, 0))
        for s in (-1, 1):
            S.box("rrail", (ln, 0.07, 0.1), (x + run / 2, y + s * 0.65, DECK - rise / 2 + 0.12), "orange", 0.0, rot=(0, ang, 0))


def cabin(S, x0, x1, y0, y1, dmg, h=1.2):
    S.bb("cab", x0, x1, y0, y1, DECK, DECK + h, "white", 0.07)
    S.bb("cstripe", x0, x1, y0 - 0.02, y0, DECK + 0.55, DECK + 0.85, "house", 0.0, nocast=True)
    S.bb("cwin", x0 + 0.25, x1 - 0.25, y0 - 0.02, y0, DECK + 0.9, DECK + 1.1, "glass", 0.0, nocast=True)
    S.bb("cdoor", x1 - 0.55, x1 - 0.2, y0 - 0.02, y0, DECK, DECK + 0.7, "orange", 0.0, nocast=True)
    if not dmg:
        S.bb("croof", x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, DECK + h, DECK + h + 0.2, "navy", 0.05)
    else:
        S.bb("croof", x0 - 0.1, (x0 + x1) / 2, y0 - 0.1, y1 + 0.1, DECK + h, DECK + h + 0.2, "navy", 0.05)
        S.box("cflap", (1.0, 1.0, 0.1), ((x0 + x1) / 2 + 0.3, (y0 + y1) / 2 * S.YS, DECK + h + 0.45), "navy", 0.0, rot=(0, -22, 8), stretch=False)


def davit(S, cx, cy, dmg, i, reach=-2.0, top=3.2, ph=0):
    S.cyl("ped", 0.4, 1.2, (cx, cy, DECK + 0.6), "chrome2", verts=12)
    S.cyl("pedb", 0.45, 0.2, (cx, cy, DECK + 1.2), "house", verts=12)
    if dmg:
        K.beam(S, "boom", (cx, cy, DECK + 1.3), (cx - 0.9, cy - 0.8, top - 0.9), 0.2, "copper")
        return
    K.beam(S, "boom", (cx, cy, DECK + 1.3), (cx, cy, top), 0.2, "chrome2")
    K.beam(S, "arm", (cx, cy, top), (cx - 0.2, cy + reach, top + 0.3), 0.18, "copper")
    sw = 0.18 * math.sin((i + ph) * math.pi / 2)
    K.beam(S, "cable", (cx - 0.2, cy + reach + 0.1, top + 0.25), (cx - 0.2 + sw, cy + reach + 0.1, 1.8), 0.04, "dark", nocast=True)
    S.bb("hook", cx - 0.3 + sw, cx - 0.1 + sw, cy + reach, cy + reach + 0.2, 1.6, 1.8, "orange", 0.0)


def mast(S, mx, my, dmg, i, top=3.4):
    K.post(S, "mast", mx, my, DECK, top, 0.1, "chrome2")
    if dmg:
        K.beam(S, "mastbent", (mx, my, 2.0), (mx + 0.8, my, 2.9), 0.1, "chrome2")
        return
    K.post(S, "mast2", mx, my, top, top + 0.4, 0.06, "chrome")
    S.box("dish", (0.9, 0.1, 0.4), (mx, my * S.YS, top - 0.3), "white2", 0.0, rot=(0, 0, i * 45), stretch=False)
    P.bubble_lamp(S, "ml", mx, my, top + 0.55, i % 2 == 0)


def site(S, hx, hy):
    """Build frame 0: staked-out berth of orange marker buoys joined by rope, with empty float drums."""
    for (sx, sy) in ((-hx, -hy), (hx, -hy), (-hx, hy), (hx, hy)):
        S.sphere("buoy", 0.28, (sx, sy, 0.12), "orange", nocast=False, squash=0.9)
        K.post(S, "bpost", sx, sy, 0.12, 0.9, 0.06, "chrome2")
        S.bb("bflag", sx, sx + 0.35, sy - 0.02, sy + 0.02, 0.6, 0.9, "orange", 0.0, nocast=True)
    for yy in (-hy, hy):
        S.bb("line", -hx, hx, yy - 0.03, yy + 0.03, 0.05, 0.1, "rope", 0.0, nocast=True)
    for xx in (-hx, hx):
        S.bb("line", xx - 0.03, xx + 0.03, -hy, hy, 0.05, 0.1, "rope", 0.0, nocast=True)
    for k in range(3):
        S.cyl(f"fdrum{k}", 0.42, 0.7, (-hx * 0.4 + k * hx * 0.4, (k - 1) * hy * 0.3, 0.2), "white2", axis="X", verts=12)


def breach(S, x0, x1, y0, y1):
    S.bb("hole", x0, x1, y0, y1, DECK, DECK + 0.02, "char", 0.0, nocast=True)
    S.box("slab", (1.4, 1.3, 0.12), ((x0 + x1) / 2 + 0.4, (y0 - 0.5) * S.YS, 0.35), "pad_s", 0.0, rot=(0, 18, 0), stretch=False)
