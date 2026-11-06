"""Expedition Tank Farm (1x2, fuel silo).  Original design.

Two painted chrome cylinders (navy cap, copper band, team-colour ring, orange number plate) behind a sandbag berm on an ash-tide apron, with a
pipe manifold and valve wheel, a gauge post with blinking lamp.  Idle: lamp blink.  Damage (structural, D-069): north tank split and
slumped, pipe broken, berm scattered; fire / smoke are FX anchors.  Build: site, apron, berm, tank 1, tank 2.
Units: 3.4 per cell, footprint centred on (0, 0): x in [-1.7, 1.7], y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_tank_farm"
TITLE = "Expedition Tank Farm"
FOOTPRINT = (1, 2)
CANVAS = (64, 160)
ORIGIN = (0, 32)
DOCK = (0, 0)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 0.0, 1.6, 1.6, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.0, 1.6, 1.9, 0),
]


def g_ground(S, dmg, i):
    S.bb("apron", -1.6, 1.6, -3.3, 3.3, 0.0, 0.1, "pad", 0.03)
    S.bb("tide", -1.62, 1.62, -3.32, 3.32, 0.0, 0.2, "ash", 0.04)
    S.bb("edge", -1.3, 1.3, -3.1, -3.0, 0.1, 0.12, "orange", 0.0, nocast=True)


def g_berm(S, dmg, i):
    for k in range(5):
        if dmg and k in (1, 3):
            S.bb(f"bag{k}", -1.3 + k * 0.55, -0.9 + k * 0.55, -3.0, -2.5, 0.1, 0.3, "rope", 0.1)
            continue
        S.bb(f"bag{k}", -1.4 + k * 0.6, -0.85 + k * 0.6, -3.0, -2.5, 0.1, 0.55, "rope", 0.12)
    S.bb("bermw", -1.5, -1.2, -2.5, 3.0, 0.1, 0.5, "rope", 0.1)
    S.bb("bermr", 1.2, 1.5, -2.5, 3.0, 0.1, 0.5, "rope", 0.1)


def tank(S, tag, y, dmg, num):
    h = 2.0 if not dmg else 1.2
    S.cyl("tk" + tag, 0.95, h, (0.0, y, 0.1 + h / 2), "chrome", verts=24)
    S.cyl("tkband" + tag, 0.98, 0.16, (0.0, y, 0.55), "copper", verts=24)
    S.cyl("tkring" + tag, 0.98, 0.22, (0.0, y, 0.1 + h * 0.7), "house", verts=24)
    S.cyl("tkcap" + tag, 0.8, 0.14, (0.0, y, 0.1 + h), "navy", verts=24)
    S.cyl("tkhat" + tag, 0.3, 0.15, (0.0, y, 0.2 + h), "orange", verts=12)
    if dmg:
        S.box("tksplit" + tag, (0.2, 0.1, 1.0), (0.5, (y - 0.95) * S.YS, 0.8), "char", 0.0, stretch=False, nocast=True)


def g_tank1(S, dmg, i):
    tank(S, "a", -0.9, False, "1")
    S.bb("plate", -0.35, 0.35, -1.9, -1.86, 0.5, 0.85, "orange", 0.0, nocast=True)


def g_tank2(S, dmg, i):
    tank(S, "b", 1.6, dmg, "2")


def g_pipes(S, dmg, i):
    K.beam(S, "man", (1.1, -0.9, 0.5), (1.1, 1.6 if not dmg else 0.5, 0.5), 0.14, "copper")
    S.cyl("valve", 0.2, 0.08, (1.1, -0.2, 0.75), "orange", verts=10)
    K.post(S, "gp", 1.3, -2.2, 0.1, 1.2, 0.08, "chrome2")
    P.bubble_lamp(S, "gl", 1.3, -2.2, 1.3, (i % 2 == 0) and not dmg)


GROUPS = [g_ground, g_berm, g_tank1, g_tank2, g_pipes]


def site(S):
    K.stakes_site(S, 1.4, 3.1)


def dmg_extra(S, i):
    S.bb("spill", -0.8, 0.8, 1.9, 2.8, 0.1, 0.12, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
