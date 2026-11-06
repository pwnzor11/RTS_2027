"""Expedition Sonar Picket (1x1): submarine detection post.  Original design.

A small concrete quay-head: a navy float drum with a grey ash tidemark and a signal-orange band stands on the pad, carrying a chrome
mast with a ring of copper hydrophone cans and a white listening dome; a team-colour pennant flies from the top and a cable runs out of the
pad edge towards the sea.  Idle loop: the lamp on the mast blinks and a pulse ring of cans lights up one after another.
Damage (structural, D-069): dome cracked, mast leaning, cable cut; fire and smoke are FX anchors.  Build: stakes, pad, drum, mast, dome.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-1.7, 1.7].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_e as P

ID = "exp_picket"
TITLE = "Expedition Sonar Picket"
FOOTPRINT = (1, 1)
CANVAS = (80, 152)
ORIGIN = (8, 80)
DOCK = (0, 0)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 6, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 0.0, 0.0, 3.2, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.0, 0.0, 3.5, 0),
]


def g_ground(S, dmg, i):
    P.apron(S, -1.6, 1.6, -1.6, 1.6, lines=False)
    S.bb("edge", -1.45, 1.45, -1.45, -1.35, 0.12, 0.14, "orange", 0.0, nocast=True)
    if dmg:
        S.bb("cable", 1.0, 1.6, -0.05, 0.05, 0.12, 0.17, "dark", 0.0, nocast=True)
    else:
        S.bb("cable", 1.0, 1.7, -0.05, 0.05, 0.12, 0.19, "chrome2", 0.0, nocast=True)


def g_drum(S, dmg, i):
    S.cyl("drum", 0.85, 1.0, (0, 0, 0.62), "navy", verts=22)
    S.cyl("dtide", 0.88, 0.24, (0, 0, 0.24), "ash", verts=22)
    S.cyl("dband", 0.88, 0.16, (0, 0, 0.8), "orange", verts=22, nocast=True)
    S.cyl("dhouse", 0.88, 0.08, (0, 0, 0.62), "house", verts=22, nocast=True)
    S.cyl("dtop", 0.7, 0.1, (0, 0, 1.17), "chrome2", verts=20)


def g_mast(S, dmg, i):
    if dmg:
        K.beam(S, "mast", (0, 0, 1.2), (0.5, 0.0, 3.0), 0.13, "chrome2")
    else:
        K.post(S, "mast", 0, 0, 1.2, 3.0, 0.13, "chrome")
    for k in range(4):
        a = math.radians(k * 90 + 45)
        lit = (i == k) and not dmg
        S.cyl(f"can{k}", 0.15, 0.4, (0.5 * math.cos(a), 0.5 * math.sin(a), 1.55), "orange" if lit else "copper", verts=10)
    S.cyl("ring", 0.55, 0.06, (0, 0, 1.4), "chrome2", verts=16, nocast=True)


def g_dome(S, dmg, i):
    S.cyl("domeb", 0.34, 0.14, (0.0, 0.0, 2.1), "copper", verts=14)
    S.sphere("dome", 0.42, (0.0, 0.0, 2.35), "white2" if dmg else "white", nocast=False, squash=0.85)
    if not dmg:
        S.sphere("lamp", 0.1, (0, 0, 3.1), "lamp" if i % 2 == 0 else "lamp_off")
    S.bb("pen", 0.0, 0.7 if not dmg else 0.4, -0.03, 0.03, 2.75, 2.95, "house", 0.0, nocast=True)


GROUPS = [g_ground, g_drum, g_mast, g_dome]


def site(S):
    K.stakes_site(S, 1.3, 1.3)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site)
