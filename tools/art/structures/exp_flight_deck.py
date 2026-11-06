"""Expedition Flight Deck (3x3): airfield and pads.  Original design.

A level poured apron with three painted landing pads (navy discs with a white ring and orange H) along the north, a short runway strip
with white dashes across the south edge, a lighthouse-height control tower (white shaft, copper gallery, glass cab, team-colour band,
turning beacon), a hangar-lean-to with an orange door, a fuel tank with a pipe line and a windsock.  Idle loop: beacon turns, pad lamps
chase, windsock swings, flag.  Damage (structural, D-069): cab shattered, one fuel tank cracked, hangar roof holed; fire and smoke are
FX anchors.  Build: surveyed plot, apron, pads, tower, hangar, fuel.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_e as P

ID = "exp_flight_deck"
TITLE = "Expedition Flight Deck"
FOOTPRINT = (3, 3)
CANVAS = (192, 288)
ORIGIN = (0, 96)
DOCK = (1, 3)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 3.9, 3.3, 5.3, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 3.2, -1.2, 1.3, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 3.9, 3.3, 5.6, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 3.2, -1.2, 1.6, 5),
]


def g_ground(S, dmg, i):
    K.pad(S, 5.0, 5.0)
    # runway strip along the south edge
    S.bb("runway", -4.8, 4.8, -4.7, -3.2, 0.12, 0.15, "pad2", 0.0, nocast=True)
    for k in range(6):
        S.bb("dash", -4.2 + k * 1.6, -3.4 + k * 1.6, -4.05, -3.85, 0.15, 0.17, "white", 0.0, nocast=True)
    for k, (x, y) in enumerate(((-4.7, 4.7), (4.7, 4.7), (-4.7, -4.7), (4.7, -4.7))):
        K.lamp_post(S, str(k), x, y, (i + k) % 2 == 0 and not dmg)


def g_pads(S, dmg, i):
    for k, x in enumerate((-3.0, -0.8, 1.4)):
        y = 2.5
        S.cyl(f"pad{k}", 1.0, 0.04, (x, y, 0.14), "navy", verts=28, nocast=True)
        S.cyl(f"ring{k}", 0.92, 0.03, (x, y, 0.17), "white", verts=28, nocast=True)
        S.cyl(f"in{k}", 0.78, 0.03, (x, y, 0.2), "navy", verts=28, nocast=True)
        S.bb(f"hl{k}", x - 0.35, x - 0.2, y - 0.4, y + 0.4, 0.2, 0.23, "orange", 0.0, nocast=True)
        S.bb(f"hr{k}", x + 0.2, x + 0.35, y - 0.4, y + 0.4, 0.2, 0.23, "orange", 0.0, nocast=True)
        S.bb(f"hm{k}", x - 0.3, x + 0.3, y - 0.07, y + 0.07, 0.2, 0.23, "orange", 0.0, nocast=True)
        lit = ((i + k) % 4 == 0) and not dmg
        S.sphere(f"padlamp{k}", 0.1, (x + 0.95, y - 0.95, 0.3), "lamp" if lit else "lamp_off")
    if dmg:
        S.cyl("crater", 0.5, 0.03, (-0.8, 2.5, 0.22), "char", verts=14, nocast=True)


def g_tower(S, dmg, i):
    cx, cy = 3.9, 3.3
    S.cyl("tbase", 0.65, 0.5, (cx, cy, 0.37), "navy", verts=16)
    S.cyl("tshaft", 0.45, 3.4, (cx, cy, 2.3), "white", verts=16)
    S.cyl("tband", 0.47, 0.25, (cx, cy, 1.5), "house", verts=16, nocast=True)
    S.cyl("tband2", 0.47, 0.18, (cx, cy, 2.9), "orange", verts=16, nocast=True)
    S.cyl("tide", 0.68, 0.25, (cx, cy, 0.3), "ash", verts=16)
    S.cyl("gallery", 0.85, 0.14, (cx, cy, 4.05), "copper", verts=20)
    S.cyl("cab", 0.65, 0.7, (cx, cy, 4.5), "glass_s" if dmg else "glass", verts=16)
    S.cyl("cabroof", 0.78, 0.14, (cx, cy, 4.92), "white2", verts=18)
    K.post(S, "mast", cx, cy, 4.98, 5.4, 0.07, "chrome2")
    if dmg:
        S.box("cabx", (0.9, 0.5, 0.1), (cx - 0.1, (cy - 0.6) * S.YS, 4.55), "navy2", 0.0, rot=(0, 25, 10), stretch=False, nocast=True)
    else:
        a = i * 90
        S.box("beacon", (0.7, 0.14, 0.14), (cx, cy * S.YS, 5.55), "orange", 0.0, rot=(0, 0, a), stretch=False)
        S.sphere("blamp", 0.11, (cx + 0.3 * math.cos(math.radians(a)), cy + 0.3 * math.sin(math.radians(a)), 5.55), "lamp")
    K.flag(S, "flag", 2.4, 4.7, 3.8 if not dmg else 2.6, i, w=1.2, h=0.75, dmg=dmg)


def g_hangar(S, dmg, i):
    S.bb("hang", -4.7, -2.0, -2.5, -0.5, 0.12, 1.6, "white", 0.1)
    S.bb("hroof", -4.8, -1.9, -2.6, -0.4, 1.6, 1.78, "navy", 0.05)
    S.bb("hband", -4.7, -2.0, -2.53, -2.5, 1.1, 1.35, "house", 0.0, nocast=True)
    S.bb("hdoor", -4.2, -2.5, -2.53, -2.5, 0.12, 1.2, "orange", 0.0, nocast=True)
    S.bb("hwin", -2.4, -2.1, -2.53, -2.5, 0.6, 1.1, "glass", 0.0, nocast=True)
    if dmg:
        S.box("hhole", (1.2, 0.9, 0.06), (-3.4, -1.5 * S.YS, 1.82), "char", 0.0, stretch=False, nocast=True)
    else:
        S.bb("hvent", -4.2, -3.4, -1.9, -1.1, 1.78, 1.9, "chrome2", 0.03)
    # windsock
    K.post(S, "wmast", -1.4, -0.2, 0.12, 2.3, 0.07, "chrome2")
    sw = 0.3 * math.sin(i * math.pi / 2)
    K.beam(S, "sock", (-1.4, -0.2, 2.2), (-0.5 + sw * 0.4, -0.2 + sw, 2.1), 0.28, "orange" if i % 2 == 0 else "white")


def g_fuel(S, dmg, i):
    S.cyl("tank", 0.8, 1.1, (1.2, -1.2, 0.7), "chrome", axis="Z", verts=20)
    S.cyl("tankb", 0.82, 0.14, (1.2, -1.2, 0.9), "house", verts=20, nocast=True)
    S.cyl("tank2", 0.8, 1.1, (3.2, -1.2, 0.7), "chrome", verts=20)
    S.cyl("tank2b", 0.82, 0.14, (3.2, -1.2, 0.9), "orange", verts=20, nocast=True)
    S.bb("pipe", 1.2, 3.2, -0.55, -0.45, 0.5, 0.6, "copper", 0.0, nocast=True)
    S.bb("hose", 3.9, 4.0, -4.0, -1.0, 0.12, 0.2, "orange2", 0.0, nocast=True)
    if dmg:
        S.box("crack", (0.2, 0.05, 0.9), (3.2, (-1.2 - 0.8) * S.YS, 0.8), "char", 0.0, stretch=False, nocast=True)


GROUPS = [g_ground, g_pads, g_tower, g_hangar, g_fuel]


def site(S):
    K.stakes_site(S, 4.4, 4.2)


def dmg_extra(S, i):
    P.scorch(S, "sc1", 1.0, 3.8, -1.0, -0.2, 0.12)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
