"""Expedition Billet (2x2, crew barracks).  Original design.

Two low prefab huts side by side (white, navy curved-look roofs) joined by a covered porch with an orange door, team-colour stripe, a smoking
chrome stovepipe with copper cap, a noticeboard, a row of kit lockers and sandbag edge in front, a flagpole.  Idle: stovepipe smoke, flag,
porch lamp.  Damage (structural, D-069): one roof caved, stovepipe snapped, board down; fire / smoke are FX anchors.  Build: site, apron,
west hut, east hut, porch and extras.
Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_billet"
TITLE = "Expedition Billet"
FOOTPRINT = (2, 2)
CANVAS = (128, 160)
ORIGIN = (0, 32)
DOCK = (0, 0)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 1.6, 0.8, 1.6, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 1.6, 0.8, 1.9, 0),
]


def g_ground(S, dmg, i):
    K.pad(S, 3.2, 3.2)
    S.bb("tide", -3.25, 3.25, -3.25, 3.25, 0.0, 0.2, "ash", 0.04)
    for k in range(5):
        S.bb(f"sand{k}", -3.0 + k * 1.2, -2.1 + k * 1.2, -3.3, -2.85, 0.1, 0.5, "rope", 0.12)


def g_west(S, dmg, i):
    S.bb("hutw", -3.0, -0.3, -1.6, 2.6, 0.12, 1.4, "white", 0.1)
    S.bb("wstripe", -3.0, -0.3, -1.62, -1.6, 0.8, 1.05, "house", 0.0, nocast=True)
    S.bb("wwin", -2.6, -1.8, -1.62, -1.6, 0.9, 1.25, "glass", 0.0, nocast=True)
    S.bb("wwin2", -1.4, -0.6, -1.62, -1.6, 0.9, 1.25, "glass", 0.0, nocast=True)
    S.bb("wbase", -3.04, -0.26, -1.64, 2.64, 0.12, 0.4, "ash", 0.04)
    if not dmg:
        S.bb("wroof", -3.15, -0.15, -1.75, 2.75, 1.4, 1.65, "navy", 0.05)
        S.bb("wrib", -3.15, -0.15, 0.5, 0.7, 1.65, 1.72, "chrome2", 0.0)
    else:
        S.bb("wroof", -3.15, -0.15, -1.75, -0.2, 1.4, 1.65, "navy", 0.05)
        S.box("wflap", (2.2, 1.4, 0.1), (-1.6, 1.0 * S.YS, 1.9), "navy", 0.0, rot=(0, 24, 8), stretch=False)


def g_east(S, dmg, i):
    S.bb("hute", 1.0, 3.0, -1.6, 2.6, 0.12, 1.4, "white", 0.1)
    S.bb("estripe", 1.0, 3.0, -1.62, -1.6, 0.8, 1.05, "house", 0.0, nocast=True)
    S.bb("ewin", 1.5, 2.5, -1.62, -1.6, 0.9, 1.25, "glass", 0.0, nocast=True)
    S.bb("ebase", 0.96, 3.04, -1.64, 2.64, 0.12, 0.4, "ash", 0.04)
    S.bb("eroof", 0.85, 3.15, -1.75, 2.75, 1.4, 1.65, "navy", 0.05)
    if not dmg:
        S.cyl("pipe", 0.12, 1.2, (2.4, 1.8, 2.25), "chrome", verts=10)
        S.cyl("pipecap", 0.2, 0.08, (2.4, 1.8, 2.9), "copper", verts=12)
        K.puffs(S, "pp", 2.4, 1.8, 3.0, i, drift=0.5)
    else:
        S.cyl("pipe", 0.12, 0.5, (2.4, 1.8, 1.9), "chrome", verts=10)


def g_porch(S, dmg, i):
    S.bb("porch", -0.3, 1.0, -1.4, 1.4, 0.12, 1.1, "white2", 0.06)
    S.bb("pdoor", -0.1, 0.8, -1.42, -1.4, 0.12, 0.95, "orange", 0.0, nocast=True)
    S.bb("proof", -0.4, 1.1, -1.55, 1.5, 1.1, 1.3, "navy2", 0.04)
    P.bubble_lamp(S, "pl", 0.35, -1.55, 1.15, (i % 2 == 0) and not dmg)


def g_extras(S, dmg, i):
    # noticeboard and lockers
    if not dmg:
        S.bb("board", -2.6, -1.4, -2.55, -2.5, 0.7, 1.4, "timber", 0.0)
        S.bb("paper", -2.4, -1.9, -2.58, -2.55, 0.9, 1.3, "rope", 0.0, nocast=True)
    else:
        S.bb("board", -2.6, -1.4, -2.7, -2.4, 0.12, 0.2, "timber", 0.0)
    K.post(S, "bp1", -2.6, -2.5, 0.1, 0.7, 0.08, "timber")
    K.post(S, "bp2", -1.4, -2.5, 0.1, 0.7, 0.08, "timber")
    for k in range(4):
        S.bb(f"locker{k}", 0.4 + k * 0.5, 0.8 + k * 0.5, -2.7, -2.3, 0.12, 0.9, "navy" if k % 2 == 0 else "chrome2", 0.03)
    K.flag(S, "flag", 3.0, -2.9, 2.8 if not dmg else 2.0, i, w=1.1, h=0.7, dmg=dmg)


GROUPS = [g_ground, g_west, g_east, g_porch, g_extras]


def site(S):
    K.stakes_site(S, 3.0, 3.0)


def dmg_extra(S, i):
    S.bb("scorch", 1.2, 2.8, 0.4, 1.8, 1.65, 1.67, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
