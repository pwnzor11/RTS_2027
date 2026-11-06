"""Expedition Anchorage (2x2, shipment quay; water mask [0,0,1,1]: north row land, south row water).  Original design.

A concrete pier with a tide gauge, a crane and a telex mast.  Land half (north): a white telex office with navy roof and team-colour band,
the chrome telex mast with a turning dish, manifest crates.  Sea half (south): the pier deck runs out over timber piles with a navy fender
edge and grey ash tidemark, orange bollards, a copper-jibbed quay crane with a swinging hook, a striped tide gauge post and a team flag.
Idle: hook, flag, dish, lamps.  Damage (structural, D-069): pier deck broken open at the seaward end, crane bent, mast leaning; fire/smoke
are FX anchors.  Build: site, deck, office, crane, mast.
Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_anchorage"
TITLE = "Expedition Anchorage"
FOOTPRINT = (2, 2)
CANVAS = (128, 192)
ORIGIN = (0, 64)
DOCK = (0, 0)
# Layout parameter (Q-55): SHORE_Y is the shore line in ground units; land lies north of it, the water-side pier/wharf south of it.
# Redo the layout by changing SHORE_Y (and the water-side groups) if docks become wholly on water.
SHORE_Y = 0.0
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 1.0, -2.0, 0.8, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 1.0, -2.0, 1.2, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", -1.8, 1.6, 1.8, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -1.8, 1.6, 2.1, 5),
]


def g_ground(S, dmg, i):
    # land half
    S.bb("land", -3.3, 3.3, SHORE_Y, 3.3, 0.0, 0.1, "pad", 0.03)
    S.bb("landtide", -3.32, 3.32, SHORE_Y, 3.32, 0.0, 0.18, "ash", 0.04)
    # sea half: pier on piles
    P.piles(S, -3.0, 3.0, -3.2, SHORE_Y, 6, 3, z1=0.3, r=0.12)
    S.bb("deck", -3.2, 3.2, -3.2, SHORE_Y, 0.3, 0.5, "pad2", 0.04)
    S.bb("tidewash", -3.25, 3.25, -3.25, SHORE_Y, 0.2, 0.36, "ash", 0.03)
    S.bb("fender", -3.3, 3.3, -3.35, -3.15, 0.3, 0.62, "navy2", 0.05)
    for k in range(5):
        S.bb("fendstripe", -3.0 + k * 1.5, -2.7 + k * 1.5, -3.37, -3.35, 0.35, 0.55, "orange", 0.0, nocast=True)
    S.bb("edgeL", -3.1, -2.95, -3.0, 0.0, 0.5, 0.52, "orange", 0.0, nocast=True)
    S.bb("edgeR", 2.95, 3.1, -3.0, 0.0, 0.5, 0.52, "orange", 0.0, nocast=True)
    for k, x in enumerate((-2.6, 0.0, 2.6)):
        S.cyl(f"bol{k}", 0.15, 0.3, (x, -2.9, 0.65), "chrome2", verts=10)


def g_office(S, dmg, i):
    S.bb("office", -3.0, -0.4, 0.8, 3.0, 0.1, 1.7, "white", 0.08)
    S.bb("ostripe", -3.0, -0.4, 0.78, 0.8, 1.0, 1.28, "house", 0.0, nocast=True)
    S.bb("owin", -2.6, -1.4, 0.78, 0.8, 1.35, 1.6, "glass", 0.0, nocast=True)
    S.bb("odoor", -1.0, -0.6, 0.78, 0.8, 0.1, 1.2, "orange", 0.0, nocast=True)
    if not dmg:
        S.bb("oroof", -3.1, -0.3, 0.7, 3.1, 1.7, 1.9, "navy", 0.05)
    else:
        S.bb("oroof", -3.1, -1.6, 0.7, 3.1, 1.7, 1.9, "navy", 0.05)
        S.box("oflap", (1.2, 1.0, 0.1), (-0.9, 1.8 * S.YS, 2.0), "navy", 0.0, rot=(0, -20, 8), stretch=False)
    for k in range(3):
        S.bb(f"crate{k}", 0.6 + k * 0.7, 1.2 + k * 0.7, 1.0, 1.6, 0.1, 0.65, "navy" if k != 1 else "orange2", 0.05)


def g_crane(S, dmg, i):
    cx, cy = 2.2, -0.9
    for ax in (-0.25, 0.25):
        for ay in (-0.25, 0.25):
            K.post(S, "cw", cx + ax, cy + ay, 0.5, 3.4, 0.09, "chrome2")
    for z in (1.2, 2.2):
        S.bb("brace", cx - 0.25, cx + 0.25, cy - 0.27, cy - 0.23, z, z + 0.06, "chrome2", 0.0, nocast=True)
    S.bb("cab", cx - 0.35, cx + 0.35, cy - 0.35, cy + 0.35, 3.4, 3.7, "house", 0.04)
    if dmg:
        K.beam(S, "jib", (cx, cy, 3.6), (cx - 0.8, cy - 1.0, 2.5), 0.2, "copper")
    else:
        K.beam(S, "jib", (cx, cy, 3.6), (cx - 0.2, cy - 2.2, 3.6), 0.2, "copper")
        sw = 0.18 * math.sin(i * math.pi / 2)
        K.beam(S, "cable", (cx - 0.2, cy - 2.0, 3.55), (cx - 0.2 + sw, cy - 2.0, 1.6), 0.04, "dark", nocast=True)
        S.bb("hook", cx - 0.3 + sw, cx - 0.1 + sw, cy - 2.1, cy - 1.9, 1.4, 1.6, "orange", 0.0)


def g_mast(S, dmg, i):
    x, y = -2.3, 0.4
    top = 4.0
    K.post(S, "mast", x, y, 0.1, top, 0.1, "chrome2")
    if dmg:
        K.beam(S, "mastbent", (x, y, 2.4), (x + 0.8, y, 3.5), 0.1, "chrome2")
        return
    K.post(S, "mast2", x, y, top, top + 0.4, 0.06, "chrome")
    a = math.radians(i * 45)
    S.box("dish", (0.9, 0.1, 0.4), (x, y * S.YS, top - 0.3), "white2", 0.0, rot=(0, 0, math.degrees(a)), stretch=False)
    P.bubble_lamp(S, "ml", x, y, top + 0.55, i % 2 == 0)


def g_gauge(S, dmg, i):
    x, y = -2.9, -2.2
    K.post(S, "gp", x, y, 0.5, 2.4, 0.16, "white")
    for k in range(4):
        S.bb("gband", x - 0.1, x + 0.1, y - 0.12, y - 0.08, 0.7 + k * 0.45, 0.9 + k * 0.45, "orange", 0.0, nocast=True)
    K.flag(S, "flag", 2.9, 2.6, 3.2 if not dmg else 2.4, i, w=1.1, h=0.7, dmg=dmg)


GROUPS = [g_ground, g_office, g_crane, g_mast, g_gauge]


def site(S):
    S.bb("pad", -3.4, 3.4, -3.4, 3.4, 0.0, 0.1, "pad2", 0.03)
    for (sx, sy) in ((-3.2, -3.2), (3.2, -3.2), (-3.2, 3.2), (3.2, 3.2)):
        K.post(S, "stake", sx, sy, 0.1, 1.0, 0.12, "timber")
        S.bb("stakeflag", sx, sx + 0.35, sy - 0.02, sy + 0.02, 0.7, 1.0, "orange", 0.0, nocast=True)
    S.bb("line", -3.2, 3.2, -0.04, 0.04, 0.88, 0.92, "rope", 0.0, nocast=True)
    P.piles(S, -2.5, 2.5, -2.6, -0.4, 4, 2, z1=0.5)


def dmg_extra(S, i):
    S.bb("hole", 0.4, 2.4, -3.0, -1.8, 0.5, 0.52, "char", 0.0, nocast=True)
    S.bb("scorch", -2.4, -1.0, 1.0, 2.2, 1.9, 1.92, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
