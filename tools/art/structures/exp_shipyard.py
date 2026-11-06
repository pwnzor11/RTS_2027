"""Expedition Shipyard (3x3, warship yard; ALL NINE CELLS WATER, D-153/D-156 floating dock, touches the shore on any side).  Original design.

A U-shaped pontoon: white float tubes with orange bands down both sides and across the north, a grey deck carrying a covered slipway
(navy gantry legs, white roof with a team-colour band, travelling crane with swinging hook) and a half-built destroyer on chrome ways
pointing SOUTH over the open water bay where the finished ship leaves.  Short ramp stub through the north tube, bollards, flag.
Idle: crane hook, lamps, welding spark, funnel smoke, flag.  Damage (structural, D-069): roof half fallen in, crane bent, bridge torn.
Build: berth buoys, hull, gantry, destroyer, crane.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_float as F

ID = "exp_shipyard"
TITLE = "Expedition Shipyard"
FOOTPRINT = (3, 3)
CANVAS = (192, 288)
ORIGIN = (0, 96)
DOCK = (1, 3)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
D = F.DECK
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -0.5, 1.0, 3.6, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 2.2, -1.0, 1.2, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -0.5, 1.0, 3.9, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 2.2, -1.0, 1.5, 5),
]


def g_ground(S, dmg, i):
    for sx in (-1, 1):
        F.tube_y(S, sx * 4.4, -4.3, 4.3)
    F.tube_x(S, 4.3, -4.4, -1.2, caps=(False, True))
    F.tube_x(S, 4.3, 1.2, 4.4, caps=(True, False))
    F.deck(S, -3.9, 3.9, -1.2, 3.8)
    F.bollards(S, [(-3.5, -0.8), (3.5, -0.8), (-3.5, 3.4), (3.5, 3.4)])
    for sx in (-1, 1):   # open bay: marker buoys beside the exit lane
        S.sphere("bb", 0.25, (sx * 3.0, -4.3, 0.12), "orange", nocast=False, squash=0.9)
        S.bb("bbp", sx * 3.0 - 0.03, sx * 3.0 + 0.03, -4.33, -4.27, 0.1, 0.8, "chrome2", 0.0, nocast=True)


def g_gantry(S, dmg, i):
    zt = D + 3.7
    for sx in (-2.7, 2.7):
        for sy in (-0.4, 3.4):
            S.bb("leg", sx - 0.25, sx + 0.25, sy - 0.25, sy + 0.25, D, zt, "navy", 0.04)
    for sx in (-2.7, 2.7):
        S.bb("side", sx - 0.14, sx + 0.14, -0.4, 3.4, zt - 0.6, zt + 0.1, "navy2", 0.04)
        S.bb("sidewall", sx - 0.12, sx + 0.12, 1.2, 3.4, D, D + 2.0, "navy", 0.03)
    S.bb("northwall", -2.7, 2.7, 3.2, 3.6, D, D + 3.4, "navy", 0.06)
    S.bb("nwstripe", -2.6, 2.6, 3.16, 3.2, D + 1.9, D + 2.1, "house", 0.0, nocast=True)
    S.bb("nwwin", -1.9, 1.9, 3.16, 3.2, D + 2.5, D + 2.9, "glass", 0.0, nocast=True)
    if dmg:
        S.bb("roofW", -2.9, 0.1, 1.5, 3.6, zt, zt + 0.15, "white", 0.04)
        S.box("roofE", (2.6, 2.0, 0.14), (1.6, 2.5 * S.YS, D + 2.8), "white2", 0.0, rot=(0, 24, 8), stretch=False)
        for k in range(2):
            S.bb(f"rafter{k}", -2.9, 2.9, -0.3 + k * 1.1, -0.1 + k * 1.1, zt, zt + 0.15, "navy2", 0.02)
    else:
        S.bb("roof", -2.9, 2.9, 1.5, 3.6, zt, zt + 0.2, "white", 0.06)
        S.bb("roofband", -2.9, 2.9, 1.46, 1.5, zt, zt + 0.2, "house", 0.0, nocast=True)
        S.bb("rooftrim", -2.9, 2.9, 1.42, 1.46, zt + 0.15, zt + 0.25, "copper", 0.0, nocast=True)
        for k in range(3):
            S.bb(f"rafter{k}", -2.9, 2.9, -0.3 + k * 0.9, -0.1 + k * 0.9, zt, zt + 0.15, "navy2", 0.02)
        for k in range(3):
            S.bb(f"skylight{k}", -2.0 + k * 1.5, -1.2 + k * 1.5, 2.0, 3.2, zt + 0.2, zt + 0.28, "glass", 0.02, nocast=True)


def g_hull(S, dmg, i):
    h = D - 0.12   # everything on the ways is lifted onto the deck
    for sx in (-0.9, 0.9):
        S.bb("slip", sx - 0.12, sx + 0.12, -4.4, 3.4, D, D + 0.08, "chrome2", 0.0, nocast=True)
    S.bb("hull", -1.3, 1.3, -3.8, 2.7, D + 0.4, D + 1.8, "navy", 0.35)
    S.bb("htide", -1.33, 1.33, -3.8, 2.7, D + 0.4, D + 0.68, "ash", 0.12)
    S.bb("hstripe", -1.32, 1.32, -3.7, 2.6, D + 1.1, D + 1.3, "house", 0.0, nocast=True)
    S.bb("deckx", -1.15, 1.15, -3.5, 2.5, D + 1.8, D + 1.88, "white2", 0.02)
    S.bb("rail", -1.3, 1.3, -3.8, 2.7, D + 1.8, D + 1.85, "copper", 0.0, nocast=True)
    S.bb("bridge", -0.8, 0.8, 0.0, 1.6, D + 1.88, D + 2.6, "white", 0.1)
    S.bb("bwin", -0.7, 0.7, -0.03, 0.0, D + 2.1, D + 2.4, "glass", 0.0, nocast=True)
    if dmg:
        S.box("btorn", (1.2, 0.8, 0.1), (0.2, 1.2 * S.YS, D + 2.8), "navy2", 0.0, rot=(0, 20, 18), stretch=False)
    else:
        S.cyl("fun", 0.3, 0.9, (0, 1.9, D + 2.35), "chrome", verts=14)
        S.cyl("funb", 0.32, 0.12, (0, 1.9, D + 2.6), "house", verts=14)
        K.puffs(S, "fun", 0, 1.9, D + 2.9, i, n=2, drift=0.5)
    S.cyl("tur", 0.5, 0.35, (0, -1.8, D + 2.0), "navy", verts=16)
    K.beam(S, "gun", (0, -1.9, D + 2.2), (0, -3.0, D + 2.4), 0.16, "chrome")
    for k in range(4):
        S.bb("cradle", -1.55, 1.55, -3.2 + k * 1.9, -3.0 + k * 1.9, D, D + 0.45, "timber", 0.03)
    K.EV.digits(S, "hn", "02", 0.0, -3.82, D + 1.1, 0.1, "white", flat=False)


def g_crane(S, dmg, i):
    top = D + 3.95
    S.bb("beam", -3.0, 3.0, -0.5, -0.1, top, top + 0.25, "copper", 0.03)
    S.bb("trolley", -0.6, 0.6, -0.55, 0.0, top + 0.25, top + 0.7, "orange2", 0.03)
    if dmg:
        K.beam(S, "jib", (0, -0.3, top + 0.7), (0.9, -0.8, D + 2.8), 0.14, "chrome2")
    else:
        sw = 0.22 * math.sin(i * math.pi / 2)
        K.beam(S, "cable", (sw * 0.3, -0.3, top + 0.2), (sw, -0.3, D + 2.8), 0.05, "dark", nocast=True)
        S.bb("hook", sw - 0.18, sw + 0.18, -0.5, -0.1, D + 2.5, D + 2.85, "orange", 0.0)
    if not dmg and i % 2 == 0:
        S.sphere("weld", 0.14, (1.3, -2.0, D + 1.7), "spark")
    K.flag(S, "flag", -3.5, -0.8, 4.9 if not dmg else 3.4, i, w=1.3, h=0.8, dmg=dmg)
    for k in range(3):
        S.cyl(f"drum{k}", 0.3, 0.8, (3.3, 0.2 + k * 0.7, D + 0.4), "orange2" if k != 1 else "white2", verts=12)


def g_ramp(S, dmg, i):
    F.ramp(S, 0.0, 3.8, "N")


GROUPS = [g_ground, g_gantry, g_hull, g_crane, g_ramp]


def site(S):
    F.site(S, 4.0, 3.8)


def dmg_extra(S, i):
    F.breach(S, 1.9, 3.4, 0.4, 1.8)
    S.bb("sc1", -3.6, -2.0, 0.2, 1.4, D + 0.01, D + 0.03, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
