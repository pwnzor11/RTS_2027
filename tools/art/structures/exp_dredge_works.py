"""Expedition Dredge Works (3x2, ash refinery).  Original design.

A low clanking works on the ash tidemark: a navy-roofed white process shed with a team-colour band, a skimmer bay door in orange on the south
face with a hopper and chute, two chrome settling tanks with copper bands, a tall chrome flue with orange cap and steam, a conveyor from the
hopper into the shed with moving cleats.  Idle: flue steam, conveyor cleats, bay lamp.  Damage (structural, D-069): roof holed, one tank
split, flue snapped, conveyor broken; fire / smoke are FX anchors.  Build: site, apron, shed, tanks, flue and conveyor.
Units: 3.4 per cell, footprint centred on (0, 0): x in [-5.1, 5.1], y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_dredge_works"
TITLE = "Expedition Dredge Works"
FOOTPRINT = (3, 2)
CANVAS = (192, 192)
ORIGIN = (0, 64)
DOCK = (1, 2)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -1.6, 1.0, 1.9, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -1.6, 1.0, 2.2, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 3.4, 0.6, 1.6, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 3.4, 0.6, 1.9, 5),
]


def g_ground(S, dmg, i):
    K.pad(S, 5.0, 3.3)
    S.bb("tide", -5.05, 5.05, -3.35, 3.35, 0.0, 0.2, "ash", 0.04)


def g_shed(S, dmg, i):
    S.bb("shed", -4.6, 0.6, -0.4, 3.0, 0.12, 1.7, "white", 0.1)
    S.bb("sbase", -4.64, 0.64, -0.44, 3.04, 0.12, 0.45, "ash", 0.04)
    S.bb("sstripe", -4.62, 0.62, -0.42, -0.4, 1.0, 1.28, "house", 0.0, nocast=True)
    S.bb("swin", -4.2, -1.4, -0.42, -0.4, 1.35, 1.6, "glass", 0.0, nocast=True)
    S.bb("bay", -1.0, 0.2, -0.42, -0.4, 0.12, 1.25, "orange", 0.0, nocast=True)
    P.hazard(S, -4.4, -1.6, -0.42, 0.14, 0.4, 8)
    if not dmg:
        S.bb("roof", -4.75, 0.75, -0.55, 3.15, 1.7, 1.95, "navy", 0.06)
        S.bb("rib", -4.75, 0.75, 1.1, 1.3, 1.95, 2.03, "chrome2", 0.0)
    else:
        S.bb("roof", -4.75, -1.5, -0.55, 3.15, 1.7, 1.95, "navy", 0.06)
        S.bb("roof2", -1.5, 0.75, 1.4, 3.15, 1.7, 1.95, "navy", 0.06)
        S.box("flap", (1.8, 1.2, 0.1), (-0.8, 0.5 * S.YS, 2.0), "navy", 0.0, rot=(0, -22, 10), stretch=False)
    P.bubble_lamp(S, "bl", -1.3, -0.5, 1.5, (i % 2 == 0) and not dmg)


def g_tanks(S, dmg, i):
    for k, x in enumerate((1.7, 3.6)):
        S.cyl(f"tank{k}", 0.85, 1.9, (x, 1.7, 1.0), "chrome", verts=20)
        S.cyl(f"tband{k}", 0.88, 0.16, (x, 1.7, 0.6), "copper", verts=20)
        S.cyl(f"thouse{k}", 0.88, 0.22, (x, 1.7, 1.55), "house", verts=20)
        S.cyl(f"tcap{k}", 0.7, 0.12, (x, 1.7, 2.0), "navy", verts=20)
    if dmg:
        S.box("split", (0.18, 0.1, 1.6), (3.9, 0.85 * S.YS, 1.0), "char", 0.0, stretch=False, nocast=True)


def g_flue(S, dmg, i):
    top = 4.2 if not dmg else 3.0
    S.cyl("flue", 0.42, top - 1.9, (-3.4, 2.3, 1.9 + (top - 1.9) / 2), "chrome", verts=18)
    S.cyl("flueband", 0.45, 0.14, (-3.4, 2.3, 2.5), "copper", verts=18)
    S.cyl("fluehouse", 0.45, 0.3, (-3.4, 2.3, top - 0.4), "house", verts=18)
    S.cyl("fluecap", 0.46, 0.07, (-3.4, 2.3, top), "orange", verts=18)
    if not dmg:
        K.puffs(S, "fl", -3.4, 2.3, top + 0.2, i, drift=0.7)


def g_conv(S, dmg, i):
    # hopper out in front, belt rising into the shed east of the bay
    S.bb("hopper", 1.0, 3.2, -2.9, -1.2, 0.12, 0.9, "navy", 0.06)
    S.bb("hopperin", 1.2, 3.0, -2.7, -1.4, 0.9, 0.95, "ash", 0.0, nocast=True)
    S.bb("hopstripe", 1.0, 3.2, -2.92, -2.9, 0.4, 0.6, "orange", 0.0, nocast=True)
    run, rise = 1.6, 0.9
    ang = math.degrees(math.atan2(rise, run * S.YS))
    ln = math.hypot(run * S.YS, rise) / S.YS
    if not dmg:
        S.box("belt", (0.6, ln, 0.12), (0.6, -1.2 + run / 2 - 0.0, 0.5 + rise / 2), "chrome2", 0.02, rot=(-ang, 0, 0))
        for k in range(4):
            ph = ((i / 4) + k / 4) % 1
            S.bb("cleat", 0.35, 0.85, -1.2 + ph * run - 0.05, -1.2 + ph * run + 0.05, 0.55 + ph * rise + 0.08, 0.55 + ph * rise + 0.16, "orange", 0.0, nocast=True)
    else:
        S.box("belt", (0.6, ln * 0.5, 0.12), (0.6, -1.0, 0.8), "chrome2", 0.02, rot=(-ang, 0, 0))
    for k in range(3):
        S.cyl(f"drum{k}", 0.26, 0.7, (4.4, -2.0 + k * 0.6, 0.47), "orange2" if k != 1 else "white2", verts=10)


def g_extras(S, dmg, i):
    K.flag(S, "flag", 4.7, -2.9, 3.0 if not dmg else 2.2, i, w=1.1, h=0.7, dmg=dmg)
    for k, (x, y) in enumerate(((-4.7, -3.2), (4.7, -3.2))):
        K.lamp_post(S, str(k), x, y, (i + k) % 2 == 0 and not dmg)


GROUPS = [g_ground, g_shed, g_tanks, g_flue, g_conv, g_extras]


def site(S):
    K.stakes_site(S, 4.7, 3.0)


def dmg_extra(S, i):
    S.bb("scorch1", -2.4, -0.8, 0.2, 1.4, 1.95, 1.97, "char", 0.0, nocast=True)
    S.bb("scorch2", 2.0, 4.2, -1.0, 0.2, 0.12, 0.14, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
