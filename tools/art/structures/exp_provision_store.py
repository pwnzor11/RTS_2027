"""Expedition Provision Store (1x2, supply building).  Original design.

A narrow navy-roofed white warehouse shed on the ash tidemark: orange roll door with a team-colour band, copper roof vent, stacked ration
crates and orange drums at the south end, a chrome hoist beam with a swinging hook, a ration-flag pole.  Idle: hook swing, flag, door lamp.
Damage (structural, D-069): roof caved in, crates spilled, hoist bent; fire / smoke are FX anchors.  Build: site, apron, shed, roof, hoist.
Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x in [-1.7, 1.7], y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_d as P

ID = "exp_provision_store"
TITLE = "Expedition Provision Store"
FOOTPRINT = (1, 2)
CANVAS = (64, 176)
ORIGIN = (0, 48)
DOCK = (0, 0)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 0.0, 1.0, 1.7, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.0, 1.0, 2.0, 0),
]


def g_ground(S, dmg, i):
    S.bb("apron", -1.6, 1.6, -3.3, 3.3, 0.0, 0.1, "pad", 0.03)
    S.bb("tide", -1.62, 1.62, -3.32, 3.32, 0.0, 0.2, "ash", 0.04)
    S.bb("edge", -1.3, 1.3, -3.1, -3.0, 0.1, 0.12, "orange", 0.0, nocast=True)


def g_shed(S, dmg, i):
    S.bb("shed", -1.3, 1.0, -0.8, 3.0, 0.1, 1.7, "white", 0.08)
    S.bb("sstripe", -1.32, 1.02, -0.82, -0.8, 1.0, 1.25, "house", 0.0, nocast=True)
    S.bb("sdoor", -0.6, 0.4, -0.82, -0.8, 0.1, 1.0, "orange", 0.0, nocast=True)
    S.bb("swin", 0.5, 0.95, -0.82, -0.8, 1.2, 1.55, "glass", 0.0, nocast=True)
    S.bb("sbase", -1.34, 1.04, -0.84, 3.04, 0.1, 0.4, "ash", 0.04)
    P.bubble_lamp(S, "dl", -0.8, -0.9, 1.45, (i % 2 == 0) and not dmg)
    K.EV.digits(S, "sn", "03", 0.0, -0.83, 1.35, 0.06, "white", flat=False) if False else None


def g_roof(S, dmg, i):
    if not dmg:
        S.bb("roof", -1.4, 1.1, -0.9, 3.1, 1.7, 1.95, "navy", 0.06)
        S.bb("ridge", -1.4, 1.1, 1.0, 1.2, 1.95, 2.05, "chrome2", 0.0)
        S.cyl("vent", 0.22, 0.35, (-0.5, 2.3, 2.1), "copper", verts=12)
    else:
        S.bb("roof", -1.4, 1.1, -0.9, 0.6, 1.7, 1.95, "navy", 0.06)
        S.box("flap", (1.6, 1.3, 0.1), (0.0, 1.6 * S.YS, 2.0), "navy", 0.0, rot=(0, 22, 10), stretch=False)


def g_stock(S, dmg, i):
    for k, (x, y, z) in enumerate(((0.9, -1.6, 0.1), (0.9, -1.6, 0.7), (0.0, -1.7, 0.1), (-1.0, -1.6, 0.1))):
        if dmg and k == 1:
            S.bb("crate", 1.0, 1.5, -2.6, -2.1, 0.1, 0.6, "orange2", 0.05)
            continue
        S.bb(f"crate{k}", x - 0.35, x + 0.35, y - 0.35, y + 0.35, z, z + 0.58, "navy" if k % 2 == 0 else "orange2", 0.05)
    for k in range(2):
        S.cyl(f"drum{k}", 0.26, 0.7, (-1.0 + k * 0.55, -2.6, 0.45), "orange2" if k else "white2", verts=10)


def g_hoist(S, dmg, i):
    x = 1.3
    K.post(S, "hp", x, 2.4, 0.1, 3.0, 0.1, "chrome2")
    if dmg:
        K.beam(S, "hb", (x, 2.4, 3.0), (x - 1.0, 2.4, 2.4), 0.12, "copper")
    else:
        K.beam(S, "hb", (x, 2.4, 3.0), (x - 1.4, 2.4, 3.0), 0.12, "copper")
        sw = 0.15 * math.sin(i * math.pi / 2)
        K.beam(S, "cab", (x - 1.2, 2.4, 2.95), (x - 1.2 + sw, 2.4, 2.1), 0.04, "dark", nocast=True)
        S.bb("hook", x - 1.3 + sw, x - 1.1 + sw, 2.3, 2.5, 1.9, 2.1, "orange", 0.0)
    K.flag(S, "flag", 1.3, -2.2, 3.4 if not dmg else 2.6, i, w=1.0, h=0.6, dmg=dmg)


GROUPS = [g_ground, g_shed, g_roof, g_stock, g_hoist]


def site(S):
    K.stakes_site(S, 1.5, 3.1)


def dmg_extra(S, i):
    S.bb("scorch", -0.8, 0.6, 0.6, 1.6, 0.1, 0.12, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
