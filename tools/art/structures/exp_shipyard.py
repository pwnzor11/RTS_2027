"""Expedition Shipyard (3x3): warship yard, a water building (south row of the footprint is sea).  Original design.

A tall covered slipway on the land half: navy gantry legs and a white roof with a team-colour band, a travelling crane beam with a
swinging hook, and on the ways a half-built destroyer hull (navy, ash tidemark, copper rail, white bridge block) pointing south down two
chrome slip rails into the sea row.  The sea side is a concrete quay edge with fender posts and bollards, a floating pontoon and
marker buoys.  Idle loop: crane hook swings, lamps blink, welding spark, flag.  Damage (structural, D-069): roof half fallen in,
crane jib bent, bridge torn; fire and smoke are FX anchors.  Build: surveyed berth, quay, gantry, hull, crane.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-5.1, 5.1]; water row y < -1.7.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_e as P

ID = "exp_shipyard"
TITLE = "Expedition Shipyard"
FOOTPRINT = (3, 3)
CANVAS = (192, 288)
ORIGIN = (0, 96)
DOCK = (1, 3)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
WY = -1.7
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -0.5, 1.0, 3.2, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 1.0, -2.8, 2.0, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -0.5, 1.0, 3.5, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 1.0, -2.8, 2.3, 5),
]


def g_ground(S, dmg, i):
    P.apron(S, -5.0, 5.0, WY, 5.0)
    P.quay_edge(S, -5.0, 5.0, WY)
    for k, (x, y) in enumerate(((-4.7, 4.7), (4.7, 4.7), (4.7, WY + 0.4))):
        K.lamp_post(S, str(k), x, y, (i + k) % 2 == 0 and not dmg)
    P.pontoon(S, "pont", 3.0, 4.6, -4.9, WY - 0.7)
    for k, bx in enumerate((-4.5, 2.2)):
        P.buoy(S, f"buoy{k}", bx, -4.6, i, lit=(i + k) % 2 == 0)


def g_gantry(S, dmg, i):
    # four gantry legs + roof frame over the slip; open to the south
    for sx in (-2.7, 2.7):
        for sy in (-0.4, 4.2):
            S.bb("leg", sx - 0.25, sx + 0.25, sy - 0.25, sy + 0.25, 0.12, 4.2, "navy", 0.04)
            S.bb("legt", sx - 0.28, sx + 0.28, sy - 0.28, sy + 0.28, 0.12, 0.34, "ash", 0.0)
    for sx in (-2.7, 2.7):
        S.bb("side", sx - 0.14, sx + 0.14, -0.4, 4.2, 3.6, 4.3, "navy2", 0.04)
    S.bb("northwall", -2.7, 2.7, 4.0, 4.4, 0.12, 4.0, "navy", 0.06)
    S.bb("nwstripe", -2.6, 2.6, 3.96, 4.0, 2.4, 2.6, "house", 0.0, nocast=True)
    S.bb("nwwin", -1.9, 1.9, 3.96, 4.0, 3.0, 3.4, "glass", 0.0, nocast=True)
    # roof over the north half only: the south half of the slip is open rafters so the hull on the ways reads from the camera
    for sx in (-2.7, 2.7):
        S.bb("sidewall", sx - 0.12, sx + 0.12, 1.6, 4.2, 0.12, 2.6, "navy", 0.03)
    if dmg:
        S.bb("roofW", -2.9, 0.1, 1.9, 4.4, 4.3, 4.45, "white", 0.04)
        S.box("roofE", (2.6, 2.2, 0.14), (1.6, 3.0 * S.YS, 3.4), "white2", 0.0, rot=(0, 24, 8), stretch=False)
        for k in range(2):
            S.bb(f"rafter{k}", -2.9, 2.9, -0.3 + k * 1.1, -0.1 + k * 1.1, 4.3, 4.45, "navy2", 0.02)
    else:
        S.bb("roof", -2.9, 2.9, 1.9, 4.5, 4.3, 4.5, "white", 0.06)
        S.bb("roofband", -2.9, 2.9, 1.86, 1.9, 4.3, 4.5, "house", 0.0, nocast=True)
        S.bb("rooftrim", -2.9, 2.9, 1.82, 1.86, 4.45, 4.55, "copper", 0.0, nocast=True)
        for k in range(3):
            S.bb(f"rafter{k}", -2.9, 2.9, -0.3 + k * 1.1, -0.1 + k * 1.1, 4.3, 4.45, "navy2", 0.02)
        for k in range(3):
            S.bb(f"skylight{k}", -2.0 + k * 1.5, -1.2 + k * 1.5, 2.6, 3.8, 4.5, 4.6, "glass", 0.02, nocast=True)


def g_hull(S, dmg, i):
    for sx in (-0.9, 0.9):
        S.bb("slip", sx - 0.12, sx + 0.12, -4.6, 3.8, 0.12, 0.2, "chrome2", 0.0, nocast=True)
    # destroyer hull on the ways, bow to the south
    S.bb("hull", -1.3, 1.3, -3.8, 3.0, 0.5, 1.9, "navy", 0.35)
    S.bb("htide", -1.33, 1.33, -3.8, 3.0, 0.5, 0.78, "ash", 0.12)
    S.bb("hstripe", -1.32, 1.32, -3.7, 2.9, 1.2, 1.4, "house", 0.0, nocast=True)
    S.bb("deckx", -1.15, 1.15, -3.5, 2.8, 1.9, 1.98, "white2", 0.02)
    S.bb("rail", -1.3, 1.3, -3.8, 3.0, 1.9, 1.95, "copper", 0.0, nocast=True)
    S.bb("bridge", -0.8, 0.8, 0.0, 1.6, 1.98, 2.7, "white", 0.1)
    S.bb("bwin", -0.7, 0.7, -0.03, 0.0, 2.2, 2.5, "glass", 0.0, nocast=True)
    if dmg:
        S.box("btorn", (1.2, 0.8, 0.1), (0.2, 1.2 * S.YS, 2.9), "navy2", 0.0, rot=(0, 20, 18), stretch=False)
    else:
        S.cyl("fun", 0.3, 0.9, (0, 1.8, 2.45), "chrome", verts=14)
        S.cyl("funb", 0.32, 0.12, (0, 1.8, 2.7), "house", verts=14)
        K.puffs(S, "fun", 0, 1.8, 3.0, i, n=2, drift=0.5)
    S.cyl("tur", 0.5, 0.35, (0, -1.8, 2.1), "navy", verts=16)
    K.beam(S, "gun", (0, -1.9, 2.3), (0, -3.0, 2.5), 0.16, "chrome")
    # hull cradle blocks
    for k in range(4):
        S.bb("cradle", -1.55, 1.55, -3.2 + k * 1.9, -3.0 + k * 1.9, 0.12, 0.6, "timber", 0.03)
    K.EV.digits(S, "hn", "02", 0.0, -3.82, 1.2, 0.1, "white", flat=False)


def g_crane(S, dmg, i):
    # crane runs along the roof beam at the south edge of the gantry
    top = 4.55
    S.bb("beam", -3.0, 3.0, -0.5, -0.1, top, top + 0.25, "copper", 0.03)
    S.bb("trolley", -0.6, 0.6, -0.55, 0.0, top + 0.25, top + 0.7, "orange2", 0.03)
    if dmg:
        K.beam(S, "jib", (0, -0.3, top + 0.7), (0.9, -0.8, 3.3), 0.14, "chrome2")
    else:
        sw = 0.22 * math.sin(i * math.pi / 2)
        K.beam(S, "cable", (sw * 0.3, -0.3, top + 0.2), (sw, -0.3, 3.3), 0.05, "dark", nocast=True)
        S.bb("hook", sw - 0.18, sw + 0.18, -0.5, -0.1, 3.0, 3.35, "orange", 0.0)
    # welding spark
    if not dmg and i % 2 == 0:
        S.sphere("weld", 0.14, (1.3, -2.0, 2.2), "spark")
    K.flag(S, "flag", -4.7, -0.3, 4.9 if not dmg else 3.4, i, w=1.3, h=0.8, dmg=dmg)
    S.bb("crate", 3.3, 4.3, 2.3, 3.4, 0.12, 0.8, "navy", 0.05)
    for k in range(3):
        S.cyl(f"drum{k}", 0.3, 0.8, (3.5 + k * 0.5, 0.8, 0.52), "orange2" if k != 1 else "white2", verts=12)


GROUPS = [g_ground, g_gantry, g_hull, g_crane]


def site(S):
    P.site_water(S, 4.4, 4.4, WY)


def dmg_extra(S, i):
    P.scorch(S, "sc1", -2.4, -0.6, 0.5, 2.5, 0.2)
    S.bb("sc2", 1.0, 2.2, WY - 0.02, WY, 0.1, 0.4, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
