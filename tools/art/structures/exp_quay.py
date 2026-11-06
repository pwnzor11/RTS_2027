"""Expedition Quay (3x2, small naval yard; ALL SIX CELLS WATER, D-153/D-156 floating dock, touches the shore on any side).  Original design.

A berth and cargo dock floating on two white float tubes with orange bands (the Anchorage hull language): grey deck on a navy rim with a
foam ring, a team-colour cabin, stacked cargo containers and drums, two davit cranes with swinging hooks, chrome bollards, a flag and a
short ramp stub at the east end.  No land.  Idle: hooks, flag, lamps.  Damage (structural, D-069): deck holed with a collapsed slab, crane
bent, cabin roof peeled; fire / smoke are FX anchors.  Build: berth buoys, hull, cabin, cargo, cranes, ramp.
Units: 3.4 per cell, footprint centred on (0, 0): x in [-5.1, 5.1], y in [-3.4, 3.4].
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_float as F

ID = "exp_quay"
TITLE = "Expedition Quay"
FOOTPRINT = (3, 2)
CANVAS = (192, 192)
ORIGIN = (0, 64)
DOCK = (1, 2)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -3.4, 1.2, 1.8, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -3.4, 1.2, 2.1, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 1.2, -1.4, 0.9, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 1.2, -1.4, 1.2, 5),
]
D = F.DECK


def g_hull(S, dmg, i):
    for sy in (-1, 1):
        F.tube_x(S, sy * 2.7, -4.25, 4.25)
    F.deck(S, -4.6, 4.6, -2.2, 2.2)
    F.bollards(S, [(-4.0, -1.8), (-1.4, -1.8), (1.4, -1.8), (4.0, -1.8), (-4.0, 1.8), (4.0, 1.8)])


def g_cabin(S, dmg, i):
    F.cabin(S, -4.4, -2.7, 0.5, 1.9, dmg)
    for k in range(2):
        S.cyl(f"drum{k}", 0.28, 0.6, (-4.0 + k * 0.7, -0.5, D + 0.3), "orange" if k == 0 else "navy", verts=12)


def g_cargo(S, dmg, i):
    for k in range(3):
        S.bb(f"cont{k}", -1.4 + k * 1.1, -0.4 + k * 1.1, 0.4, 1.8, D, D + 0.65, "navy" if k != 1 else "orange2", 0.05)
    S.bb("cont3", -0.3, 0.7, 0.4, 1.8, D + 0.65, D + 1.3, "orange2", 0.05)
    S.bb("cstrp", -0.3, 0.7, 0.38, 0.4, D + 0.8, D + 1.1, "house", 0.0, nocast=True)


def g_cranes(S, dmg, i):
    F.davit(S, 3.2, -0.2, dmg, i, reach=-1.7, top=3.2)
    F.davit(S, -1.6, -1.0, dmg, i, reach=-1.0, top=2.7, ph=2)


def g_ramp(S, dmg, i):
    F.ramp(S, 4.6, 0.0, "E")


def g_flag(S, dmg, i):
    K.flag(S, "flag", 2.2, 1.5, 3.0 if not dmg else 2.2, i, w=1.1, h=0.7, dmg=dmg)
    K.lamp_post(S, "a", 1.0, 1.9, i % 2 == 0)


GROUPS = [g_hull, g_cabin, g_cargo, g_cranes, g_ramp, g_flag]


def site(S):
    F.site(S, 4.6, 2.9)


def dmg_extra(S, i):
    F.breach(S, 0.4, 2.0, -2.0, -0.9)
    S.bb("scorch", -4.3, -2.8, 0.6, 1.8, D + 1.41, D + 1.43, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
