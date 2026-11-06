"""Expedition Hover Yard (3x2, hovercraft factory).  Original design.

A wide white shed with a navy barrel-look roof and a team-colour band, a sloping chrome slipway ramp with orange rails out of the south face
(the bay), a gantry frame with a copper beam that lifts a rubber skirt (swinging), a skirt stack and fan nacelle by the wall, hull number plate,
radar-less lamp posts.  Idle: gantry hook, bay lamps, flag.  Damage (structural, D-069): roof holed, gantry fallen over, bay door torn; fire /
smoke are FX anchors.  Build: site, apron, shed, slipway, gantry.
Units: 3.4 per cell, footprint centred on (0, 0): x in [-5.1, 5.1], y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_hover_yard"
TITLE = "Expedition Hover Yard"
FOOTPRINT = (3, 2)
CANVAS = (192, 192)
ORIGIN = (0, 64)
DOCK = (1, 2)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -1.2, 0.8, 1.9, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -1.2, 0.8, 2.2, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 3.6, -0.4, 1.4, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 3.6, -0.4, 1.7, 5),
]


def g_ground(S, dmg, i):
    K.pad(S, 5.0, 3.3)
    S.bb("tide", -5.05, 5.05, -3.35, 3.35, 0.0, 0.2, "ash", 0.04)


def g_shed(S, dmg, i):
    S.bb("shed", -4.8, 1.2, -0.6, 3.0, 0.12, 1.9, "white", 0.1)
    S.bb("sbase", -4.84, 1.24, -0.64, 3.04, 0.12, 0.45, "ash", 0.04)
    S.bb("sstripe", -4.82, 1.22, -0.62, -0.6, 1.1, 1.4, "house", 0.0, nocast=True)
    S.bb("swin", -4.4, -2.2, -0.62, -0.6, 1.5, 1.8, "glass", 0.0, nocast=True)
    S.bb("bay", -1.8, 0.8, -0.62, -0.6, 0.12, 1.45, "dark", 0.0, nocast=True)
    S.bb("bayfr", -1.9, 0.9, -0.64, -0.6, 1.4, 1.55, "orange", 0.0, nocast=True)
    K.EV.digits(S, "hn", "02", -3.4, -0.62, 0.55, 0.09, "white", flat=False)
    if not dmg:
        S.bb("roof", -4.95, 1.35, -0.75, 3.15, 1.9, 2.15, "navy", 0.06)
        for k in range(3):
            S.bb(f"rib{k}", -4.95, 1.35, 0.2 + k * 0.9, 0.35 + k * 0.9, 2.15, 2.22, "chrome2", 0.0)
    else:
        S.bb("roof", -4.95, -2.0, -0.75, 3.15, 1.9, 2.15, "navy", 0.06)
        S.bb("roof2", -2.0, 1.35, 1.6, 3.15, 1.9, 2.15, "navy", 0.06)
        S.box("flap", (2.0, 1.4, 0.1), (-0.6, 0.5 * S.YS, 2.2), "navy", 0.0, rot=(0, -22, 10), stretch=False)
    for k, x in enumerate((-1.3, 0.3)):
        P.bubble_lamp(S, f"bl{k}", x - 0.0, -0.7, 1.7, ((i + k) % 2 == 0) and not dmg)


def g_slip(S, dmg, i):
    run, rise = 2.2, 0.8
    ang = math.degrees(math.atan2(rise, run * S.YS))
    ln = math.hypot(run * S.YS, rise) / S.YS
    S.box("slip", (2.6, ln, 0.1), (-0.5, -0.6 - run / 2, 0.12 + rise / 2), "chrome2", 0.02, rot=(ang, 0, 0))
    for s in (-1, 1):
        S.box(f"srail{s}", (0.1, ln, 0.14), (-0.5 + s * 1.3, -0.6 - run / 2, 0.12 + rise / 2 + 0.1), "orange", 0.0, rot=(ang, 0, 0))


def g_gantry(S, dmg, i):
    cx = 3.3
    for ax in (-1.0, 1.0):
        for ay in (-0.2, 0.2):
            if dmg and ax > 0:
                continue
            K.post(S, "gp", cx + ax, 1.2 + ay, 0.12, 3.0, 0.1, "chrome2")
    if dmg:
        K.beam(S, "gb", (cx - 1.0, 1.4, 3.0), (cx + 1.6, 0.2, 0.6), 0.16, "copper")
        S.bb("skirtfall", cx + 0.8, cx + 1.8, -0.4, 0.3, 0.12, 0.5, "rubber", 0.1) if False else None
    else:
        K.beam(S, "gb", (cx - 1.0, 1.2, 3.0), (cx + 1.0, 1.2, 3.0), 0.16, "copper")
        sw = 0.2 * math.sin(i * math.pi / 2)
        K.beam(S, "cab", (cx + sw * 0.2, 1.2, 2.95), (cx + sw, 1.2, 1.8), 0.04, "dark", nocast=True)
        S.cyl("skirtring", 0.45, 0.3, (cx + sw, 1.2, 1.55), "dark", verts=16)
        S.cyl("skirtband", 0.47, 0.06, (cx + sw, 1.2, 1.45), "ash", verts=16)
    S.cyl("fan", 0.5, 0.7, (4.5, -1.8, 0.47), "chrome2", verts=14)
    S.cyl("fancap", 0.38, 0.06, (4.5, -1.8, 0.85), "orange", verts=14)
    K.flag(S, "flag", -4.7, -2.9, 3.0 if not dmg else 2.2, i, w=1.1, h=0.7, dmg=dmg)


GROUPS = [g_ground, g_shed, g_slip, g_gantry]


def site(S):
    K.stakes_site(S, 4.7, 3.0)


def dmg_extra(S, i):
    S.bb("scorch1", -3.0, -1.0, 0.4, 1.8, 2.15, 2.17, "char", 0.0, nocast=True)
    S.bb("scorch2", 2.6, 4.4, -1.4, 0.0, 0.12, 0.14, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
