"""Expedition Landing Beacon (1x1).  Original design.

A tripod of chrome legs carrying a signal-orange lamp with a copper hood and a turning lens beam, an aerial mast with team-colour pennant, and
a small tent of sealed navy crates (white lid, orange straps) on a round ash-tide pad.  Idle: lamp pulses, pennant flaps.  Damage (structural,
D-069): tripod tipped, lamp dark and smashed, crates scattered; fire / smoke are FX anchors.  Build: site, pad, crates, tripod, lamp.
Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-1.7, 1.7].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_beacon"
TITLE = "Expedition Landing Beacon"
FOOTPRINT = (1, 1)
CANVAS = (64, 112)
ORIGIN = (0, 48)
DOCK = (0, 0)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 0.2, 0.2, 1.2, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.2, 0.2, 1.5, 0),
]


def g_ground(S, dmg, i):
    S.bb("pad", -1.6, 1.6, -1.6, 1.6, 0.0, 0.1, "pad", 0.03)
    S.bb("tide", -1.62, 1.62, -1.62, 1.62, 0.0, 0.18, "ash", 0.04)
    S.cyl("ring", 1.15, 0.03, (0.0, 0.1, 0.12), "orange", verts=24, nocast=True)
    S.cyl("ringin", 1.0, 0.04, (0.0, 0.1, 0.12), "pad2", verts=24, nocast=True)


def g_crates(S, dmg, i):
    if not dmg:
        S.bb("c1", -1.4, -0.6, -1.4, -0.5, 0.1, 0.7, "navy", 0.05)
        S.bb("c2", -0.5, 0.2, -1.4, -0.7, 0.1, 0.6, "navy2", 0.05)
        S.bb("c3", -1.3, -0.7, -1.3, -0.6, 0.7, 1.0, "white2", 0.05)
        S.bb("strap", -1.4, -0.6, -1.42, -1.4, 0.4, 0.5, "orange", 0.0, nocast=True)
    else:
        S.bb("c1", -1.4, -0.6, -1.4, -0.5, 0.1, 0.7, "navy", 0.05)
        S.bb("c2", 0.2, 0.9, -1.2, -0.5, 0.1, 0.5, "navy2", 0.05, rot=(0, 0, 20)) if False else S.bb("c2", 0.2, 0.9, -1.2, -0.5, 0.1, 0.5, "navy2", 0.05)


def g_tripod(S, dmg, i):
    if dmg:
        K.beam(S, "l1", (0.0, 0.2, 0.2), (0.9, 0.9, 0.5), 0.1, "chrome2")
        K.beam(S, "l2", (0.0, 0.2, 0.2), (-0.6, 0.4, 0.9), 0.1, "chrome2")
        S.bb("lampd", 0.7, 1.1, 0.7, 1.1, 0.1, 0.4, "dark", 0.05)
        return
    for (dx, dy) in ((-0.7, -0.5), (0.7, -0.5), (0.0, 0.8)):
        K.beam(S, "leg", (0.0 + dx, 0.2 + dy, 0.1), (0.0, 0.2, 1.7), 0.1, "chrome2")
    S.cyl("plat", 0.3, 0.1, (0.0, 0.2, 1.7), "copper", verts=12)


def g_lamp(S, dmg, i):
    if dmg:
        return
    pulse = i % 2 == 0
    S.sphere("lamp", 0.3 if pulse else 0.26, (0.0, 0.2, 2.0), "lamp", nocast=False)
    S.cyl("hood", 0.36, 0.1, (0.0, 0.2, 2.35), "copper", verts=14)
    S.cyl("band", 0.3, 0.12, (0.0, 0.2, 1.8), "orange2", verts=12)
    K.post(S, "aer", 0.0, 0.2, 2.4, 3.4, 0.05, "chrome")
    K.flag(S, "pen", 0.0, 0.2, 3.4, i, w=0.8, h=0.45)


GROUPS = [g_ground, g_crates, g_tripod, g_lamp]


def site(S):
    K.stakes_site(S, 1.4, 1.4)


def dmg_extra(S, i):
    S.bb("scorch", -0.4, 1.0, -0.2, 0.9, 0.1, 0.12, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
