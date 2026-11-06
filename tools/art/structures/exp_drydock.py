"""Expedition Drydock (4x3, heavy ship repair; ALL TWELVE CELLS WATER, D-153/D-156 floating dock, touches the shore on any side).  Original design.

A floating basin dock: white float tubes with orange bands down both sides and across the north, two grey decks (the basin walls) either
side of an open water basin, a head deck with a ramp stub through the north tube, and in the basin a cruiser under repair (navy, ash
tidemark, white deckhouse, copper rail, team-colour stripe, chrome turret) floating bow to the SOUTH.  The caisson gate at the south end is
swung open (two orange/chrome leaves) so the open water exit reads.  Two travelling gantry cranes span the basin, a pump house (white,
copper band, team stripe) on the west deck, bollards, flag.  Idle: hooks, pump steam, lamps, flag.  Damage (structural, D-069): one crane
collapsed across the basin, pump roof torn, gate leaf broken; fire and smoke are FX anchors.  Build: berth buoys, decks, basin hull,
cranes, pump.  Units: 3.4 per cell, footprint centred on (0, 0): x in [-6.8, 6.8], y in [-5.1, 5.1].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_float as F

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
D = F.DECK
BX = 2.0     # basin half width
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 0.0, 0.8, 2.6, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 4.2, -1.0, 1.4, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.0, 0.8, 2.9, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -4.2, 2.4, 2.4, 5),
]


def g_ground(S, dmg, i):
    for sx in (-1, 1):
        F.tube_y(S, sx * 6.1, -4.3, 4.3)
    F.tube_x(S, 4.3, -6.1, -1.3, caps=(False, True))
    F.tube_x(S, 4.3, 1.3, 6.1, caps=(True, False))
    for sx in (-1, 1):
        a, b = (BX, 5.6) if sx > 0 else (-5.6, -BX)
        F.deck(S, a, b, -3.6, 3.9, edge=False)
        S.bb("coping", a if sx > 0 else b - 0.12, (a + 0.12) if sx > 0 else b, -3.6, 3.9, D, D + 0.05, "copper", 0.0, nocast=True)
        S.bb("wstripe", (a + 0.3) if sx > 0 else b - 0.5, (a + 0.5) if sx > 0 else b - 0.3, -3.4, 3.7, D, D + 0.02, "orange", 0.0, nocast=True)
    F.deck(S, -BX, BX, 3.1, 3.9, edge=False)
    S.bb("headcop", -BX, BX, 3.1, 3.22, D, D + 0.05, "copper", 0.0, nocast=True)
    F.bollards(S, [(-5.2, -3.2), (5.2, -3.2), (-5.2, 3.5), (5.2, 3.5), (-3.0, -3.2), (3.0, -3.2)])
    # caisson gate leaves swung open to the sides at the south end of the basin
    gl = -3.9
    for sx in (-1, 1):
        K.beam(S, "leafa", (sx * (BX + 0.1), gl, 0.45), (sx * (BX + 1.5), gl - 0.9, 0.45), 0.4, "chrome2")
        K.beam(S, "leafb", (sx * (BX + 0.1), gl, 0.85), (sx * (BX + 1.5), gl - 0.9, 0.85), 0.4, "orange")
    if dmg:
        S.box("gatebrk", (1.6, 0.6, 0.12), (0.9, (gl - 0.6) * S.YS, 0.5), "navy2", 0.0, rot=(0, 20, 0), stretch=False, nocast=True)


def g_hull(S, dmg, i):
    z = 0.12
    S.bb("hull", -1.45, 1.45, -4.6, 3.0, z + 0.3, z + 1.66, "navy", 0.3)
    S.bb("htide", -1.48, 1.48, -4.6, 3.0, z + 0.3, z + 0.6, "ash", 0.14)
    S.bb("hstripe", -1.47, 1.47, -4.5, 2.9, z + 1.15, z + 1.35, "house", 0.0, nocast=True)
    S.bb("deck", -1.3, 1.3, -4.3, 2.8, z + 1.66, z + 1.78, "white2", 0.0)
    for sx in (-1.4, 1.4):
        S.bb("rail", sx - 0.05, sx + 0.05, -4.5, 2.9, z + 1.7, z + 1.84, "copper", 0.0, nocast=True)
    S.bb("house", -0.9, 0.9, -0.2, 1.6, z + 1.76, z + 2.6, "white", 0.1)
    S.bb("hhwin", -0.8, 0.8, -0.23, -0.2, z + 2.05, z + 2.35, "glass", 0.0, nocast=True)
    S.bb("hdstripe", -0.9, 0.9, -0.23, -0.2, z + 1.8, z + 1.95, "orange", 0.0, nocast=True)
    S.cyl("tur", 0.6, 0.3, (0, -2.4, z + 1.9), "navy", verts=18)
    if dmg:
        K.beam(S, "gun", (0, -2.5, z + 2.15), (0.3, -3.2, z + 1.9), 0.18, "chrome2")
        S.box("htorn", (1.3, 0.9, 0.1), (0.2, 1.0 * S.YS, z + 2.9), "navy2", 0.0, rot=(0, 20, 18), stretch=False)
    else:
        K.beam(S, "gun", (0, -2.5, z + 2.15), (0, -3.7, z + 2.4), 0.18, "chrome")
        S.cyl("fun", 0.32, 1.0, (0, 2.2, z + 2.3), "chrome", verts=14)
        S.cyl("funb", 0.34, 0.14, (0, 2.2, z + 2.7), "house", verts=14)
        K.puffs(S, "fun", 0, 2.2, z + 3.0, i, n=2, drift=0.5)
    K.EV.digits(S, "hn", "07", 0.0, -4.62, z + 1.1, 0.11, "white", flat=False)


def crane(S, name, side, y, i):
    x = side * (BX + 0.4)
    for dy in (-0.5, 0.5):
        S.bb(name + "leg", x - 0.2, x + 0.2, y + dy - 0.15, y + dy + 0.15, D, D + 3.4, "navy", 0.03)
    top = D + 3.4
    S.bb(name + "beam", -BX - 0.8, BX + 0.8, y - 0.25, y + 0.25, top, top + 0.3, "copper", 0.03)
    S.bb(name + "trol", -0.4, 0.4, y - 0.3, y + 0.3, top + 0.3, top + 0.6, "orange2", 0.03)
    S.bb(name + "band", -BX - 0.8, BX + 0.8, y - 0.27, y - 0.25, top + 0.02, top + 0.28, "house", 0.0, nocast=True)
    sw = 0.2 * math.sin((i + (0 if side < 0 else 2)) * math.pi / 2)
    K.beam(S, name + "cab", (sw * 0.3, y, top + 0.3), (sw, y, D + 2.2), 0.05, "dark", nocast=True)
    S.bb(name + "hook", sw - 0.15, sw + 0.15, y - 0.12, y + 0.12, D + 1.9, D + 2.25, "orange", 0.0)


def g_cranes(S, dmg, i):
    crane(S, "cA", -1, 2.4, i)
    if dmg:
        K.beam(S, "cBfall", (BX + 0.4, -0.8, D), (-1.4, -1.8, D + 2.6), 0.28, "navy")
        K.beam(S, "cBfall2", (BX + 0.4, -1.4, D), (-1.4, -2.4, D + 2.2), 0.2, "copper")
    else:
        crane(S, "cB", 1, -1.2, i)


def g_pump(S, dmg, i):
    x0, x1, y0, y1 = -5.4, -3.0, 0.4, 2.8
    F.cabin(S, x0, x1, y0, y1, dmg, h=1.3)
    if not dmg:
        S.cyl("pstack", 0.28, 1.2, (-3.6, 2.3, D + 1.7), "chrome", verts=12)
        S.cyl("pstkc", 0.3, 0.1, (-3.6, 2.3, D + 2.3), "copper", verts=12)
        K.puffs(S, "pmp", -3.6, 2.3, D + 2.4, i, n=2, drift=0.7)
    for k in range(3):
        S.cyl(f"pdrum{k}", 0.3, 0.8, (-5.0 + k * 0.7, -1.0, D + 0.4), "orange2" if k != 1 else "white2", verts=12)
    K.flag(S, "flag", 4.6, 3.0, 3.4 if not dmg else 2.4, i, w=1.3, h=0.8, dmg=dmg)


def g_ramp(S, dmg, i):
    F.ramp(S, 0.0, 3.9, "N")


GROUPS = [g_ground, g_hull, g_cranes, g_pump, g_ramp]


def site(S):
    F.site(S, 5.6, 3.8)
    S.bb("outline", -BX, BX, -1.0, 3.0, 0.1, 0.13, "rope", 0.0, nocast=True)


def dmg_extra(S, i):
    F.breach(S, 3.4, 5.0, -2.8, -1.6)
    S.bb("sc1", -5.2, -3.4, -2.2, -1.0, D + 0.01, D + 0.03, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
