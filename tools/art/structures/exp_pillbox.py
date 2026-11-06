"""Expedition Seawall Emplacement (1x1 turret).  Original design.

A low navy-faced concrete seawall bunker with a white capped parapet, a copper-ringed chrome gun turret on top with a long barrel and a bubble
sight, orange firing-slit lamp, team-colour band and an ash tidemark footing.  Idle: sight lamp blink.  Damage (structural, D-069): parapet
broken, barrel drooping, turret skewed; fire / smoke are FX anchors.  Build: site, footing, bunker, turret.  The gun is a turret layer in the
game; the sprite shows it at rest facing south.
Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-1.7, 1.7].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_pillbox"
TITLE = "Expedition Seawall Emplacement"
FOOTPRINT = (1, 1)
CANVAS = (64, 96)
ORIGIN = (0, 32)
DOCK = (0, 0)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 0.0, 0.0, 1.5, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.0, 0.0, 1.8, 0),
]


def g_ground(S, dmg, i):
    S.bb("pad", -1.6, 1.6, -1.6, 1.6, 0.0, 0.1, "pad", 0.03)
    S.bb("tide", -1.62, 1.62, -1.62, 1.62, 0.0, 0.2, "ash", 0.04)


def g_bunker(S, dmg, i):
    S.bb("bunk", -1.35, 1.35, -1.2, 1.2, 0.1, 1.1, "navy", 0.1)
    S.bb("btide", -1.38, 1.38, -1.23, 1.23, 0.1, 0.3, "ash", 0.05)
    S.bb("bband", -1.37, 1.37, -1.22, -1.2, 0.65, 0.85, "house", 0.0, nocast=True)
    S.bb("slit", -0.6, 0.6, -1.22, -1.2, 0.35, 0.55, "dark", 0.0, nocast=True)
    S.bb("slitlamp", 0.8, 1.1, -1.22, -1.2, 0.3, 0.5, "orange", 0.0, nocast=True)
    S.bb("deck", -1.3, 1.3, -1.15, 1.15, 1.1, 1.18, "navy2", 0.03)
    rim = [(-1.45, 1.45, -1.3, -1.1), (-1.45, 1.45, 1.1, 1.3), (-1.45, -1.25, -1.3, 1.3), (1.25, 1.45, -1.3, 1.3)]
    for k, (x0, x1, y0, y1) in enumerate(rim):
        if dmg and k in (1, 3):
            continue
        S.bb(f"par{k}", x0, x1, y0, y1, 1.1, 1.3, "white", 0.05)
    if dmg:
        S.bb("rubble", 0.5, 1.3, -0.6, 0.4, 1.1, 1.2, "ash", 0.1)


def g_turret(S, dmg, i):
    S.cyl("ring", 0.7, 0.14, (0.0, 0.1, 1.38), "copper", verts=20)
    S.cyl("tur", 0.6, 0.5, (0.0, 0.1, 1.7), "chrome", verts=20)
    S.cyl("turband", 0.62, 0.1, (0.0, 0.1, 1.62), "house", verts=20)
    S.sphere("sight", 0.2, (0.3, 0.1, 2.05), "glass", nocast=False, squash=0.8)
    if dmg:
        K.beam(S, "gun", (0.0, -0.4, 1.75), (0.0, -1.5, 1.35), 0.2, "chrome2")
    else:
        K.beam(S, "gun", (0.0, -0.4, 1.8), (0.0, -1.8, 1.8), 0.18, "chrome2")
        S.cyl("muz", 0.14, 0.2, (0.0, -1.85, 1.8), "dark", axis="Y", verts=10)
        P.bubble_lamp(S, "sl", -0.3, 0.1, 2.05, i % 2 == 0)


GROUPS = [g_ground, g_bunker, g_turret]


def site(S):
    K.stakes_site(S, 1.4, 1.4)


def dmg_extra(S, i):
    S.bb("scorch", -0.8, 0.8, 0.2, 1.0, 1.3, 1.32, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
