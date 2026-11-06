"""Expedition SAM Mount (1x1): surface-to-air battery.  Original design.

A round poured pad with an orange ring, a navy turntable drum on its grey ash tidemark with a team-colour band, and on top a white
launcher cradle carrying two guided missiles (white bodies, orange noses, navy fins) angled to the sky, a small copper-trimmed
control box with a chrome whip aerial.  Idle loop: the mount sweeps left and right a few degrees, the lamp blinks.
Damage (structural, D-069): one missile hanging, cradle skewed, aerial bent; fire and smoke are FX anchors.
Build: stakes, pad, drum, cradle, missiles.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-1.7, 1.7].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_e as P

ID = "exp_sam"
TITLE = "Expedition SAM Mount"
FOOTPRINT = (1, 1)
CANVAS = (80, 136)
ORIGIN = (8, 64)
DOCK = (0, 0)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 6, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 0.0, 0.0, 2.0, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.0, 0.0, 2.3, 0),
]
SWEEP = (-14, -5, 5, 14)


def g_ground(S, dmg, i):
    P.apron(S, -1.6, 1.6, -1.6, 1.6, lines=False)
    S.cyl("ring", 1.35, 0.02, (0, 0, 0.14), "orange", verts=28, nocast=True)
    S.cyl("ringin", 1.2, 0.03, (0, 0, 0.15), "pad", verts=28, nocast=True)


def g_drum(S, dmg, i):
    S.cyl("drum", 0.75, 0.7, (0, 0, 0.47), "navy", verts=20)
    S.cyl("dtide", 0.78, 0.2, (0, 0, 0.22), "ash", verts=20)
    S.cyl("dband", 0.78, 0.12, (0, 0, 0.6), "house", verts=20, nocast=True)
    S.cyl("race", 0.8, 0.07, (0, 0, 0.84), "chrome2", verts=20)


def g_cradle(S, dmg, i):
    yaw = SWEEP[i % 4] if not dmg else 40
    S.box("cradle", (1.0, 0.8, 0.28), (0, 0, 1.0), "white2", 0.06, rot=(0, 0, yaw), stretch=False)
    S.box("cradlec", (1.05, 0.2, 0.1), (0, 0, 1.17), "copper", 0.0, rot=(0, 0, yaw), stretch=False, nocast=True)
    K.post(S, "tower", 0, 0, 1.0, 1.3, 0.3, "navy2")


def g_missiles(S, dmg, i):
    yaw = math.radians(SWEEP[i % 4] if not dmg else 40)
    for k, s in enumerate((-1, 1)):
        px, py = -math.sin(yaw) * 0.28 * s, math.cos(yaw) * 0.28 * s
        # missiles point north-ish and up (rotate the launch direction by the mount yaw)
        ux, uy = -math.sin(yaw) * 0.9, math.cos(yaw) * 0.9
        z0 = 1.25
        if dmg and k == 1:
            P.missile(S, f"ms{k}", px, py, z0, uy * 0.9, 0.2, dx=ux * 0.9)
        else:
            P.missile(S, f"ms{k}", px, py, z0, uy * 1.2, 1.7, dx=ux * 1.2)
    if not dmg:
        S.bb("box", 0.9, 1.4, -1.2, -0.7, 0.12, 0.65, "white", 0.05)
        S.bb("boxs", 0.9, 1.4, -1.23, -1.2, 0.35, 0.5, "orange", 0.0, nocast=True)
        K.post(S, "whip", 1.15, -0.95, 0.65, 2.1, 0.04, "chrome2")
        S.sphere("lamp", 0.09, (1.15, -0.95, 2.18), "lamp" if i % 2 == 0 else "lamp_off")
    else:
        S.bb("box", 0.9, 1.4, -1.2, -0.7, 0.12, 0.65, "white", 0.05)
        K.beam(S, "whip", (1.15, -0.95, 0.65), (1.5, -0.95, 1.4), 0.04, "chrome2")


GROUPS = [g_ground, g_drum, g_cradle, g_missiles]


def site(S):
    K.stakes_site(S, 1.3, 1.3)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site)
