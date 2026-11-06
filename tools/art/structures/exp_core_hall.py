"""Expedition Core Hall (3x3): valve-computer research hall, the tier-3 gate.  Original design.

A long low navy hall with a glass-fronted south face: a row of lit rack windows (valve banks) behind chrome mullions, a lagoon-white
roof with a copper coping and a team-colour band, four turning cooling fans in chrome cowls on the roof, a tall chrome cooling stack
with a copper band steaming at the north-west, a cable trench and heavy copper bus pipes along the east wall, an orange service door and
a wing for the cooling plant.  Idle loop: rack lamps chase along the glass, fan blades turn, steam rises.
Damage (structural, D-069): glass front smashed, one fan cowl torn off, stack snapped; fire and smoke are FX anchors.
Build: surveyed plot, apron, hall shell, glass front and racks, roof fans, stack.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_e as P

ID = "exp_core_hall"
TITLE = "Expedition Core Hall"
FOOTPRINT = (3, 3)
CANVAS = (192, 256)
ORIGIN = (0, 64)
DOCK = (1, 3)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -1.0, -1.2, 1.6, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 2.0, 2.0, 2.4, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -1.0, -1.2, 1.9, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 2.0, 2.0, 2.7, 5),
]


def g_ground(S, dmg, i):
    K.pad(S, 5.0, 5.0)
    for k, (x, y) in enumerate(((-4.7, -4.7), (4.7, -4.7), (4.7, 4.7))):
        K.lamp_post(S, str(k), x, y, (i + k) % 2 == 0 and not dmg)


def g_shell(S, dmg, i):
    P.block(S, "hall", -4.4, 3.4, -2.4, 3.8, 0.12, 2.3, "navy", 0.1, tidemark=True)
    S.bb("roof", -4.5, 3.5, -2.5, 3.9, 2.3, 2.5, "white", 0.06)
    P.rail_ring(S, -4.5, 3.5, -2.5, 3.9, 2.5, t=0.12)
    P.stripe(S, "hstripe", -4.4, 3.4, -2.4, 1.9, 2.2)
    S.bb("door", -4.0, -3.3, -2.43, -2.4, 0.12, 1.3, "orange", 0.0, nocast=True)
    K.EV.digits(S, "hn", "05", 2.5, -2.4, 1.55, 0.12, "white", flat=False)


def g_glass(S, dmg, i):
    # glass front with a row of racks behind: lit lamps chase along
    S.bb("glass", -3.1, 2.0, -2.43, -2.4, 0.7, 1.75, "glass_s" if dmg else "glass", 0.0, nocast=True)
    for k in range(7):
        x = -3.0 + k * 0.78
        S.bb(f"mull{k}", x - 0.04, x + 0.04, -2.47, -2.4, 0.65, 1.8, "chrome2", 0.0, nocast=True)
        lit = ((i + k) % 4 == 0) and not dmg
        S.bb(f"lamp{k}", x + 0.18, x + 0.55, -2.46, -2.43, 1.0 + 0.0, 1.14, "lamp" if lit else "lamp_off", 0.0, nocast=True)
        S.bb(f"lampb{k}", x + 0.18, x + 0.55, -2.46, -2.43, 1.35, 1.49, "lamp" if (i + k) % 3 == 1 and not dmg else "lamp_off", 0.0, nocast=True)
    S.bb("sill", -3.2, 2.1, -2.55, -2.4, 0.55, 0.7, "copper", 0.0, nocast=True)
    if dmg:
        S.bb("smash", -1.5, -0.4, -2.46, -2.42, 0.7, 1.75, "char", 0.0, nocast=True)


def g_roof(S, dmg, i):
    for k, x in enumerate((-3.2, -1.4, 0.4, 2.2)):
        if dmg and k == 2:
            S.cyl("ftorn", 0.5, 0.1, (x, 0.4, 2.55), "char", verts=12, nocast=True)
            continue
        S.cyl(f"cowl{k}", 0.62, 0.3, (x, 0.9, 2.65), "chrome", verts=18)
        S.cyl(f"cowlr{k}", 0.65, 0.07, (x, 0.9, 2.78), "copper", verts=18, nocast=True)
        S.cyl(f"hub{k}", 0.1, 0.1, (x, 0.9, 2.82), "dark", verts=8)
        a = (i * 30 + k * 20) % 180
        S.box(f"blade{k}", (1.0, 0.14, 0.05), (x, 0.9 * S.YS, 2.84), "chrome2", 0.0, rot=(0, 0, a), stretch=False, nocast=True)
        S.box(f"bladeb{k}", (1.0, 0.14, 0.05), (x, 0.9 * S.YS, 2.84), "chrome2", 0.0, rot=(0, 0, a + 90), stretch=False, nocast=True)


def g_stack(S, dmg, i):
    top = 4.4 if not dmg else 3.2
    S.cyl("stack", 0.5, top - 0.12, (-4.1, 3.9, 0.12 + (top - 0.12) / 2), "chrome", verts=20)
    S.cyl("stkband", 0.53, 0.16, (-4.1, 3.9, 2.8), "copper", verts=20)
    S.cyl("stkhouse", 0.53, 0.3, (-4.1, 3.9, top - 0.4), "house", verts=20)
    S.cyl("stkcap", 0.48, 0.06, (-4.1, 3.9, top), "dark", verts=16)
    S.cyl("stktide", 0.54, 0.3, (-4.1, 3.9, 0.3), "ash", verts=20)
    if not dmg:
        K.puffs(S, "stk", -4.1, 3.9, top + 0.2, i, drift=0.9)
    # copper bus pipes on the east wall, cooling plant wing
    for k in range(3):
        S.bb(f"bus{k}", 3.4, 4.5, -1.6 + k * 1.8 - 0.1, -1.6 + k * 1.8 + 0.1, 0.9 + 0.0, 1.05, "copper", 0.0, nocast=True)
    S.bb("plant", 3.5, 4.8, -2.2, -0.2, 0.12, 1.3, "white", 0.08)
    S.bb("proof", 3.45, 4.85, -2.25, -0.15, 1.3, 1.4, "navy", 0.03)
    S.bb("pstripe", 3.5, 4.8, -2.23, -2.2, 0.7, 0.9, "orange", 0.0, nocast=True)
    K.flag(S, "flag", 4.6, 4.4, 3.4 if not dmg else 2.4, i, w=1.2, h=0.75, dmg=dmg)


GROUPS = [g_ground, g_shell, g_glass, g_roof, g_stack]


def site(S):
    K.stakes_site(S, 4.4, 4.2)


def dmg_extra(S, i):
    P.scorch(S, "sc1", -2.5, 0.5, -3.2, -2.6, 0.12)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
