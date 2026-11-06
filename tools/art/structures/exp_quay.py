"""Expedition Quay (3x2, small naval yard; water mask [0,0,0,1,1,1]: north row land, south row water).  Original design.

Land half (north): a white workshop with navy roof and team-colour band, orange roll door, stacked ship's stores.  Sea half (south): a concrete
wharf deck on piles with navy fender edge and ash tidemark, a chrome slipway ramp with orange rails running down to the water edge, a chrome
crane with copper jib and swinging hook, bollards, flag.  Idle: hook, flag, lamps.  Damage (structural, D-069): wharf holed, crane bent, workshop
roof caved; fire / smoke are FX anchors.  Build: site, deck, workshop, slip, crane.
Units: 3.4 per cell, footprint centred on (0, 0): x in [-5.1, 5.1], y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_quay"
TITLE = "Expedition Quay"
FOOTPRINT = (3, 2)
CANVAS = (192, 192)
ORIGIN = (0, 64)
DOCK = (1, 2)
# Layout parameter (Q-55): SHORE_Y is the shore line in ground units; land lies north of it, the water-side pier/wharf south of it.
# Redo the layout by changing SHORE_Y (and the water-side groups) if docks become wholly on water.
SHORE_Y = 0.0
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -2.4, 1.6, 1.7, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -2.4, 1.6, 2.0, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 2.8, -1.8, 0.9, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 2.8, -1.8, 1.2, 5),
]


def g_ground(S, dmg, i):
    S.bb("land", -5.0, 5.0, SHORE_Y, 3.3, 0.0, 0.1, "pad", 0.03)
    S.bb("landtide", -5.05, 5.05, SHORE_Y, 3.35, 0.0, 0.18, "ash", 0.04)
    P.piles(S, -4.8, 4.8, -3.2, SHORE_Y, 8, 3, z1=0.3, r=0.12)
    S.bb("deck", -5.0, 5.0, -3.2, SHORE_Y, 0.3, 0.5, "pad2", 0.04)
    S.bb("tidewash", -5.05, 5.05, -3.25, SHORE_Y, 0.2, 0.36, "ash", 0.03)
    S.bb("fender", -5.1, 5.1, -3.35, -3.15, 0.3, 0.62, "navy2", 0.05)
    for k in range(8):
        S.bb("fendstripe", -4.8 + k * 1.25, -4.5 + k * 1.25, -3.37, -3.35, 0.35, 0.55, "orange", 0.0, nocast=True)
    for k, x in enumerate((-4.4, -1.5, 4.4)):
        S.cyl(f"bol{k}", 0.15, 0.3, (x, -2.9, 0.65), "chrome2", verts=10)


def g_shop(S, dmg, i):
    S.bb("shop", -4.6, -0.6, 0.6, 3.0, 0.1, 1.7, "white", 0.08)
    S.bb("sstripe", -4.6, -0.6, 0.58, 0.6, 1.0, 1.28, "house", 0.0, nocast=True)
    S.bb("swin", -4.2, -2.8, 0.58, 0.6, 1.35, 1.6, "glass", 0.0, nocast=True)
    S.bb("sdoor", -2.2, -0.9, 0.58, 0.6, 0.1, 1.3, "orange", 0.0, nocast=True)
    if not dmg:
        S.bb("sroof", -4.75, -0.45, 0.45, 3.15, 1.7, 1.95, "navy", 0.05)
        S.bb("srib", -4.75, -0.45, 1.7, 1.9, 1.95, 2.02, "chrome2", 0.0)
    else:
        S.bb("sroof", -4.75, -2.6, 0.45, 3.15, 1.7, 1.95, "navy", 0.05)
        S.box("sflap", (1.8, 1.4, 0.1), (-1.6, 1.8 * S.YS, 2.0), "navy", 0.0, rot=(0, -22, 8), stretch=False)
    for k in range(3):
        S.bb(f"st{k}", 0.4 + k * 0.8, 1.1 + k * 0.8, 1.0, 1.7, 0.1, 0.7, "navy" if k != 1 else "orange2", 0.05)


def g_slip(S, dmg, i):
    run, rise = 2.6, 0.5
    ang = math.degrees(math.atan2(rise, run * S.YS))
    ln = math.hypot(run * S.YS, rise) / S.YS
    S.box("slip", (2.4, ln, 0.1), (-1.0, -0.2 - run / 2, 0.5 - rise / 2 + 0.1), "chrome2", 0.02, rot=(-ang, 0, 0))
    for s in (-1, 1):
        S.box(f"srail{s}", (0.1, ln, 0.14), (-1.0 + s * 1.2, -0.2 - run / 2, 0.5 - rise / 2 + 0.2), "orange", 0.0, rot=(-ang, 0, 0))
    if dmg:
        S.bb("hole", 1.4, 3.0, -2.6, -1.2, 0.5, 0.52, "char", 0.0, nocast=True)


def g_crane(S, dmg, i):
    cx, cy = 3.6, -1.0
    for ax in (-0.25, 0.25):
        for ay in (-0.25, 0.25):
            K.post(S, "cw", cx + ax, cy + ay, 0.5, 3.4, 0.09, "chrome2")
    for z in (1.2, 2.2):
        S.bb("brace", cx - 0.25, cx + 0.25, cy - 0.27, cy - 0.23, z, z + 0.06, "chrome2", 0.0, nocast=True)
    S.bb("cab", cx - 0.35, cx + 0.35, cy - 0.35, cy + 0.35, 3.4, 3.7, "house", 0.04)
    if dmg:
        K.beam(S, "jib", (cx, cy, 3.6), (cx - 0.9, cy - 1.0, 2.5), 0.2, "copper")
    else:
        K.beam(S, "jib", (cx, cy, 3.6), (cx - 0.3, cy - 2.2, 3.6), 0.2, "copper")
        sw = 0.18 * math.sin(i * math.pi / 2)
        K.beam(S, "cable", (cx - 0.3, cy - 2.0, 3.55), (cx - 0.3 + sw, cy - 2.0, 1.6), 0.04, "dark", nocast=True)
        S.bb("hook", cx - 0.4 + sw, cx - 0.2 + sw, cy - 2.1, cy - 1.9, 1.4, 1.6, "orange", 0.0)
    K.flag(S, "flag", 4.8, 2.6, 3.0 if not dmg else 2.2, i, w=1.1, h=0.7, dmg=dmg)


GROUPS = [g_ground, g_shop, g_slip, g_crane]


def site(S):
    S.bb("pad", -5.0, 5.0, -3.4, 3.4, 0.0, 0.1, "pad2", 0.03)
    for (sx, sy) in ((-4.8, -3.2), (4.8, -3.2), (-4.8, 3.2), (4.8, 3.2)):
        K.post(S, "stake", sx, sy, 0.1, 1.0, 0.12, "timber")
        S.bb("stakeflag", sx, sx + 0.35, sy - 0.02, sy + 0.02, 0.7, 1.0, "orange", 0.0, nocast=True)
    S.bb("line", -4.8, 4.8, -0.04, 0.04, 0.88, 0.92, "rope", 0.0, nocast=True)
    P.piles(S, -4.5, 4.5, -2.6, -0.4, 6, 2, z1=0.5)


def dmg_extra(S, i):
    S.bb("scorch", -3.8, -1.4, 1.2, 2.4, 1.95, 1.97, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
