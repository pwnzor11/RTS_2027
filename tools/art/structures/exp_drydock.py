"""Expedition Drydock (4x3): heavy ship repair, a water building (south row of the footprint is sea).  Original design.

A deep concrete basin running north-south down the middle of the pad: two thick quay walls with copper coping, a dark basin floor
and, sitting on keel blocks in it, a cruiser hull under repair (navy with a grey ash tidemark, white deckhouse, copper rail,
team-colour stripe, chrome turret) its bow in the water row.  A floating caisson gate (orange, chrome) closes the south end of the basin.
Two travelling gantry cranes run on the walls, a pump house (white with a copper band) at the north-west, bollards and fenders along
the quay edge.  Idle loop: crane hooks swing, pump steam, lamps, flag.  Damage (structural, D-069): one crane collapsed across the wall,
pump house roof torn, gate leaning; fire and smoke are FX anchors.  Build: surveyed basin site, walls, basin and hull, cranes, pump.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x in [-6.8, 6.8], y in [-5.1, 5.1]; water row y < -1.7.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_e as P

ID = "exp_drydock"
TITLE = "Expedition Drydock"
FOOTPRINT = (4, 3)
CANVAS = (256, 288)
ORIGIN = (0, 96)
DOCK = (1, 3)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
WY = -1.7
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 0.0, 1.0, 2.6, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 4.9, 0.0, 3.0, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.0, 1.0, 2.9, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -5.2, 3.4, 2.4, 5),
]
BX = 2.0     # basin half width


def g_ground(S, dmg, i):
    P.apron(S, -6.6, 6.6, WY, 5.0)
    for k, (x, y) in enumerate(((-6.3, 4.7), (6.3, 4.7), (-6.3, WY + 0.5), (6.3, WY + 0.5))):
        K.lamp_post(S, str(k), x, y, (i + k) % 2 == 0 and not dmg)
    for sx in (-1, 1):
        P.quay_edge(S, -6.6 if sx < 0 else BX + 0.7, -BX - 0.7 if sx < 0 else 6.6, WY)
    for k, bx in enumerate((-5.3, 5.3)):
        P.buoy(S, f"buoy{k}", bx, -4.6, i, lit=(i + k) % 2 == 0)


def g_basin(S, dmg, i):
    # basin: two walls, dark floor
    S.bb("floor", -BX, BX, -5.0, 4.6, 0.0, 0.1, "dark", 0.0)
    for sx in (-1, 1):
        x0, x1 = (sx * BX, sx * (BX + 0.7))
        a, b = min(x0, x1), max(x0, x1)
        S.bb("wall", a, b, -5.0, 4.6, 0.0, 1.5, "pad2", 0.05)
        S.bb("walltop", a, b, -5.0, 4.6, 1.5, 1.56, "pad", 0.0, nocast=True)
        S.bb("walltide", a - (0.03 if sx < 0 else 0.0), b + (0.03 if sx > 0 else 0.0), -5.0, 4.6, 0.0, 0.32, "ash", 0.0)
        ci = (a, a + 0.12) if sx > 0 else (b - 0.12, b)
        S.bb("coping", ci[0], ci[1], -5.0, 4.6, 1.5, 1.58, "copper", 0.0, nocast=True)
        S.bb("wstripe", a if sx > 0 else b - 0.02, a + 0.02 if sx > 0 else b, -4.6, 4.2, 0.9, 1.1, "orange", 0.0, nocast=True)
    S.bb("headwall", -BX, BX, 4.4, 4.9, 0.0, 1.5, "pad2", 0.05)
    S.bb("headcop", -BX, BX, 4.4, 4.52, 1.5, 1.58, "copper", 0.0, nocast=True)
    # caisson gate closing the south end
    gl = -4.6
    S.bb("gate", -BX - 0.1, BX + 0.1, gl - 0.4, gl + 0.4, 0.0, 1.0, "chrome2", 0.05)
    S.bb("gatetide", -BX - 0.12, BX + 0.12, gl - 0.42, gl + 0.42, 0.0, 0.25, "ash", 0.0)
    S.bb("gatestripe", -BX, BX, gl - 0.44, gl - 0.4, 0.5, 0.75, "orange", 0.0, nocast=True)
    if dmg:
        S.box("gatebrk", (1.6, 0.6, 0.12), (1.0, gl * S.YS, 1.1), "navy2", 0.0, rot=(0, 20, 0), stretch=False, nocast=True)


def g_hull(S, dmg, i):
    for k in range(5):
        S.bb("keel", -0.7, 0.7, -4.0 + k * 1.9, -3.6 + k * 1.9, 0.1, 0.45, "timber", 0.03)
    S.bb("hull", -1.45, 1.45, -4.2, 4.1, 0.45, 2.0, "navy", 0.4)
    S.bb("htide", -1.48, 1.48, -4.2, 4.1, 0.45, 0.75, "ash", 0.14)
    S.bb("hstripe", -1.47, 1.47, -4.1, 4.0, 1.3, 1.5, "house", 0.0, nocast=True)
    S.bb("deck", -1.3, 1.3, -3.9, 3.8, 2.0, 2.06, "white2", 0.02)
    S.bb("rail", -1.45, 1.45, -4.2, 4.1, 2.0, 2.06, "copper", 0.0, nocast=True)
    S.bb("house", -0.9, 0.9, 0.0, 2.0, 2.06, 2.9, "white", 0.1)
    S.bb("hhwin", -0.8, 0.8, -0.03, 0.0, 2.35, 2.65, "glass", 0.0, nocast=True)
    S.bb("hdstripe", -0.9, 0.9, -0.03, 0.0, 2.1, 2.25, "orange", 0.0, nocast=True)
    S.cyl("tur", 0.6, 0.3, (0, -2.2, 2.2), "navy", verts=18)
    if dmg:
        K.beam(S, "gun", (0, -2.3, 2.45), (0.3, -3.0, 2.2), 0.18, "chrome2")
        S.box("htorn", (1.3, 0.9, 0.1), (0.2, 1.3 * S.YS, 3.0), "navy2", 0.0, rot=(0, 20, 18), stretch=False)
    else:
        K.beam(S, "gun", (0, -2.3, 2.45), (0, -3.5, 2.7), 0.18, "chrome")
        S.cyl("fun", 0.32, 1.0, (0, 2.9, 2.6), "chrome", verts=14)
        S.cyl("funb", 0.34, 0.14, (0, 2.9, 3.0), "house", verts=14)
        K.puffs(S, "fun", 0, 2.9, 3.3, i, n=2, drift=0.5)
    K.EV.digits(S, "hn", "07", 0.0, -4.22, 1.35, 0.11, "white", flat=False)


def crane(S, name, side, y, i, dmg):
    x = side * (BX + 0.35)
    for dy in (-0.5, 0.5):
        S.bb(name + "leg", x - 0.2, x + 0.2, y + dy - 0.15, y + dy + 0.15, 1.58, 4.1, "navy", 0.03)
    S.bb(name + "beam", -BX - 0.8, BX + 0.8, y - 0.25, y + 0.25, 4.1, 4.4, "copper", 0.03)
    S.bb(name + "trol", -0.4, 0.4, y - 0.3, y + 0.3, 4.4, 4.7, "orange2", 0.03)
    S.bb(name + "band", -BX - 0.8, BX + 0.8, y - 0.27, y - 0.25, 4.15, 4.35, "house", 0.0, nocast=True)
    sw = 0.2 * math.sin((i + (0 if side < 0 else 2)) * math.pi / 2)
    K.beam(S, name + "cab", (sw * 0.3, y, 4.4), (sw, y, 3.2), 0.05, "dark", nocast=True)
    S.bb(name + "hook", sw - 0.15, sw + 0.15, y - 0.12, y + 0.12, 2.9, 3.25, "orange", 0.0)


def g_cranes(S, dmg, i):
    crane(S, "cA", -1, 2.8, i, False)
    if dmg:
        K.beam(S, "cBfall", (BX + 0.35, -0.8, 1.58), (-1.4, -2.0, 3.1), 0.28, "navy")
        K.beam(S, "cBfall2", (BX + 0.35, -1.4, 1.58), (-1.4, -2.6, 2.7), 0.2, "copper")
    else:
        crane(S, "cB", 1, -0.4, i, False)


def g_pump(S, dmg, i):
    S.bb("pump", -6.2, -3.4, 1.6, 4.4, 0.12, 1.7, "white", 0.1)
    S.bb("pumptide", -6.24, -3.36, 1.56, 4.44, 0.12, 0.4, "ash", 0.0)
    S.bb("pstripe", -6.2, -3.4, 1.56, 1.6, 1.1, 1.3, "house", 0.0, nocast=True)
    S.bb("pwin", -5.8, -4.6, 1.56, 1.6, 0.8, 1.4, "glass", 0.0, nocast=True)
    S.bb("pdoor", -4.4, -3.8, 1.56, 1.6, 0.12, 1.15, "orange", 0.0, nocast=True)
    if dmg:
        S.box("proof", (2.8, 1.5, 0.12), (-4.8, 3.0 * S.YS, 1.9), "navy2", 0.0, rot=(0, 18, 8), stretch=False)
    else:
        S.bb("proof", -6.3, -3.3, 1.5, 4.5, 1.7, 1.85, "navy", 0.04)
        S.cyl("pstack", 0.28, 1.2, (-4.0, 3.8, 2.4), "chrome", verts=12)
        S.cyl("pstkc", 0.3, 0.1, (-4.0, 3.8, 3.0), "copper", verts=12)
        K.puffs(S, "pmp", -4.0, 3.8, 3.1, i, n=2, drift=0.7)
    for k in range(3):
        S.cyl(f"pdrum{k}", 0.3, 0.8, (-5.9 + k * 0.7, 0.6, 0.52), "orange2" if k != 1 else "white2", verts=12)
    K.flag(S, "flag", 5.9, 4.3, 3.6 if not dmg else 2.4, i, w=1.3, h=0.8, dmg=dmg)


GROUPS = [g_ground, g_basin, g_hull, g_cranes, g_pump]


def site(S):
    P.site_water(S, 6.2, 4.4, WY)
    S.bb("outline", -BX, BX, -1.0, 4.0, 0.1, 0.13, "rope", 0.0, nocast=True)


def dmg_extra(S, i):
    P.scorch(S, "sc1", BX + 0.5, 6.0, -1.0, 1.0, 0.12)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
