"""Expedition Gun Battery (2x2): coastal artillery.  Original design.

Two heavy twin guns, each in a round navy shield on a chrome race, sunk into low concrete emplacements with sandbag-grey ash tidemark
and copper coping, a team-colour band on every shield; a small white ammunition bunker with an orange door between them, a chrome
range-finder mast and a pennant.  The guns face south-east.  Idle loop: the muzzles glow orange in turn, lamps blink, flag.
Damage (structural, D-069): one barrel drooping, one emplacement cracked, the mast bent; fire and smoke are FX anchors.
Build: surveyed plot, apron, emplacements, guns, bunker.  No firing state: the engine draws muzzle fx itself.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_e as P

ID = "exp_gun_battery"
TITLE = "Expedition Gun Battery"
FOOTPRINT = (2, 2)
CANVAS = (144, 184)
ORIGIN = (8, 56)
DOCK = (0, 0)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -1.6, 0.8, 1.7, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 1.4, -1.0, 1.6, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -1.6, 0.8, 2.0, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 1.4, -1.0, 1.9, 5),
]
GUNS = ((-1.6, 1.5), (1.6, -0.7))


def g_ground(S, dmg, i):
    P.apron(S, -3.3, 3.3, -3.3, 3.3)
    for k, (x, y) in enumerate(((-3.0, -3.0), (3.0, 3.0))):
        K.lamp_post(S, str(k), x, y, (i + k) % 2 == 0 and not dmg)


def g_pits(S, dmg, i):
    for k, (x, y) in enumerate(GUNS):
        S.cyl(f"pit{k}", 1.45, 0.6, (x, y, 0.42), "pad2", verts=24)
        S.cyl(f"pittide{k}", 1.48, 0.22, (x, y, 0.23), "ash", verts=24)
        S.cyl(f"coping{k}", 1.47, 0.07, (x, y, 0.74), "copper", verts=24, nocast=True)
        if dmg and k == 0:
            S.box("pitcrack", (0.1, 1.4, 0.05), (x - 0.5, y * S.YS, 0.78), "char", 0.0, stretch=False, nocast=True)


def g_guns(S, dmg, i):
    for k, (x, y) in enumerate(GUNS):
        hot = (i + 2 * k) % 4 == 0 and not dmg
        P.gun_turret(S, f"gun{k}", x, y, 0.72, 1.0, -0.8, dmg=(dmg and k == 1), size=0.85, barrel=2.2)
        if hot:
            S.sphere(f"glow{k}", 0.2, (x + 1.55, y - 1.2, 1.55), "flame")


def g_bunker(S, dmg, i):
    S.bb("bunk", -0.9, 0.9, -3.0, -1.5, 0.12, 1.1, "white", 0.08)
    S.bb("broof", -1.0, 1.0, -3.1, -1.4, 1.1, 1.22, "navy", 0.03)
    P.stripe(S, "bstripe", -0.9, 0.9, -3.0, 0.75, 0.9)
    S.bb("bdoor", -0.3, 0.3, -3.03, -3.0, 0.12, 0.85, "orange", 0.0, nocast=True)
    S.bb("bslit", 0.4, 0.8, -3.03, -3.0, 0.7, 0.85, "glass", 0.0, nocast=True)
    for k in range(3):
        S.cyl(f"shell{k}", 0.2, 0.7, (-2.7 + k * 0.5, -2.6, 0.5), "chrome2" if k != 1 else "copper", verts=10)


def g_mast(S, dmg, i):
    mx, my = 3.0, 0.6
    K.post(S, "rfm", mx, my, 0.12, 3.1 if not dmg else 2.0, 0.1, "chrome2")
    if dmg:
        K.beam(S, "rfbent", (mx, my, 2.0), (mx - 0.6, my, 2.5), 0.1, "chrome2")
    else:
        S.bb("rfbar", mx - 0.6, mx + 0.6, my - 0.07, my + 0.07, 2.9, 3.0, "chrome", 0.0, nocast=True)
        S.sphere("rflamp", 0.11, (mx, my, 3.2), "lamp" if i % 2 == 0 else "lamp_off")
    K.flag(S, "flag", -3.0, -0.3, 2.6 if not dmg else 1.8, i, w=1.0, h=0.6, dmg=dmg)


GROUPS = [g_ground, g_pits, g_guns, g_bunker, g_mast]


def site(S):
    K.stakes_site(S, 3.0, 3.0)


def dmg_extra(S, i):
    P.scorch(S, "sc1", 0.0, 2.0, -2.2, -1.2, 0.12)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
