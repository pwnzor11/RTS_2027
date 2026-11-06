"""Expedition Pile House (2x2, power): a squat concrete reactor house on timber piles.  Original design.

Lagoon-white slab house with a navy roof and a team-colour band, set on a navy plinth over visible timber piles with a grey ash tidemark; a
chrome cooling stack with a copper band and orange cap, a chrome generator dome annex with a hazard-striped door, a pylon with insulators
and a signal-orange warning lamp.  Idle loop: stack steam, warning lamp, pylon arcs.  Damage (structural, D-069): roof torn open, stack
snapped short and leaning, annex dome cracked, pylon bent; fire and smoke are FX anchors.  Build: surveyed site, plinth, house, stack, annex.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_pile_house"
TITLE = "Expedition Pile House"
FOOTPRINT = (2, 2)
CANVAS = (128, 192)
ORIGIN = (0, 64)
DOCK = (0, 0)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -0.8, 0.6, 2.4, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -0.8, 0.6, 2.8, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 1.6, 1.6, 2.2, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 1.6, 1.6, 2.6, 5),
]


def g_ground(S, dmg, i):
    S.bb("apron", -3.2, 3.2, -3.2, 3.2, 0.0, 0.1, "pad", 0.03)
    S.bb("tide", -3.25, 3.25, -3.25, 3.25, 0.0, 0.2, "ash", 0.05)
    S.bb("edge", -3.0, 3.0, -3.12, -3.0, 0.1, 0.12, "orange", 0.0, nocast=True)
    P.piles(S, -2.7, 0.9, -2.6, 0.6, 4, 3, z1=0.5)


def g_house(S, dmg, i):
    S.bb("plinth", -2.9, 1.1, -2.7, 0.7, 0.1, 0.7, "navy2", 0.05)
    S.bb("plinthtide", -2.93, 1.13, -2.73, 0.73, 0.1, 0.3, "ash", 0.05)
    S.bb("house", -2.7, 0.9, -2.5, 0.5, 0.7, 2.1, "white", 0.1)
    S.bb("hstripe", -2.72, 0.92, -2.52, -2.5, 1.15, 1.4, "house", 0.0, nocast=True)
    S.bb("hwin", -2.3, -0.3, -2.53, -2.5, 1.5, 1.85, "glass", 0.0, nocast=True)
    S.bb("door", 0.0, 0.6, -2.53, -2.5, 0.7, 1.4, "orange", 0.0, nocast=True)
    P.hazard(S, -2.6, -0.4, -2.52, 0.72, 0.95, 6)
    if not dmg:
        S.bb("roof", -2.85, 1.05, -2.65, 0.65, 2.1, 2.3, "navy", 0.06)
        S.bb("roofrib", -2.85, 1.05, -1.3, -1.1, 2.3, 2.38, "chrome2", 0.0)
        S.bb("vent", -2.2, -1.2, -1.2, -0.2, 2.3, 2.6, "white2", 0.05)
    else:
        S.bb("roof", -2.85, -0.9, -2.65, 0.65, 2.1, 2.3, "navy", 0.06)
        S.box("roofflap", (1.8, 1.2, 0.1), (0.0, 0.2 * S.YS, 2.4), "navy", 0.0, rot=(0, 25, 12), stretch=False)
    K.EV.digits(S, "pn", "01", -1.0, -2.53, 0.78, 0.07, "white", flat=False)


def g_stack(S, dmg, i):
    top = 4.0 if not dmg else 3.0
    S.cyl("stack", 0.5, top - 2.3, (-1.9, 0.0, 2.3 + (top - 2.3) / 2), "chrome", verts=20)
    S.cyl("stackband", 0.53, 0.16, (-1.9, 0.0, 2.7), "copper", verts=20)
    S.cyl("stackhouse", 0.53, 0.28, (-1.9, 0.0, top - 0.4), "house", verts=20)
    S.cyl("stackcap", 0.55, 0.08, (-1.9, 0.0, top), "orange", verts=20)
    if not dmg:
        K.puffs(S, "st", -1.9, 0.0, top + 0.2, i, drift=0.6)
    P.ladder(S, -1.9, -0.55, 0.7, top - 0.4)


def g_annex(S, dmg, i):
    S.bb("annex", 1.2, 3.0, -2.6, -0.2, 0.1, 1.2, "white2", 0.08)
    S.bb("annexstripe", 1.2, 3.0, -2.62, -2.6, 0.7, 0.9, "navy", 0.0, nocast=True)
    S.bb("annexdoor", 1.7, 2.5, -2.62, -2.6, 0.1, 1.0, "orange", 0.0, nocast=True)
    S.sphere("dome", 0.85, (2.1, -1.4, 1.25), "chrome", nocast=False, squash=0.7)
    S.cyl("domeband", 0.88, 0.1, (2.1, -1.4, 1.2), "copper", verts=20)
    if dmg:
        S.box("domecrack", (0.1, 1.0, 0.6), (2.3, -1.4 * S.YS, 1.8), "char", 0.0, rot=(0, 0, 25), stretch=False, nocast=True)


def g_pylon(S, dmg, i):
    cx, cy = 2.3, 1.5
    for ax in (-0.2, 0.2):
        for ay in (-0.2, 0.2):
            K.post(S, "py", cx + ax, cy + ay, 0.1, 3.2, 0.08, "chrome2")
    for z in (1.0, 2.0):
        S.bb("pybrace", cx - 0.2, cx + 0.2, cy - 0.22, cy - 0.18, z, z + 0.05, "chrome2", 0.0, nocast=True)
    if dmg:
        K.beam(S, "arm", (cx - 0.9, cy, 3.2), (cx + 0.9, cy - 0.2, 2.7), 0.1, "chrome2")
    else:
        K.beam(S, "arm", (cx - 0.9, cy, 3.1), (cx + 0.9, cy, 3.1), 0.1, "chrome2")
        for s in (-0.8, 0.0, 0.8):
            S.cyl("ins", 0.07, 0.3, (cx + s, cy, 2.9), "white2", verts=8)
        if i % 2 == 0:
            S.sphere("arc", 0.1, (cx + 0.4, cy, 2.95), "spark")
    P.bubble_lamp(S, "warn", cx, cy, 3.45, (i % 2 == 0) and not dmg)


GROUPS = [g_ground, g_house, g_stack, g_annex, g_pylon]


def site(S):
    K.stakes_site(S, 2.9, 2.8)


def dmg_extra(S, i):
    S.bb("scorch1", -2.0, -0.4, -0.4, 0.4, 2.3, 2.32, "char", 0.0, nocast=True)
    S.bb("scorch2", 0.6, 2.6, 0.4, 1.4, 0.1, 0.12, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
