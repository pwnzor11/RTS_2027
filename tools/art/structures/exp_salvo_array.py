"""Expedition Salvo Array (3x3): the superweapon, a pier of launch rails linking the fleet's missile ships into one barrage.  Original design.

A raised navy launch deck on its grey ash tidemark, a copper-coped edge and orange hazard kerb; on it two ranks of six launch rails
(chrome cradles, twelve missiles standing at a steep angle towards the north), the missiles white with navy fin collars and signal-orange
noses.  A control gantry with a lagoon-white bunker and team-colour band at the south-east, a turning radar bar on a chrome mast, a
cable run, and the team flag.  Charge state: the engine only knows idle / damaged / build, so idle is an 8-frame SLOW CHARGE like the
Directorate Sunrise Device: the missile noses light one after another (lamp-yellow) until all twelve glow, then drop dark; the control
lamps and radar follow.  Damage (structural, D-069): four missiles gone, two leaning, a rail snapped, gantry torn; fire and smoke are FX
anchors.  Build: surveyed plot, apron and deck, rails, missiles, gantry.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_e as P

ID = "exp_salvo_array"
TITLE = "Expedition Salvo Array"
FOOTPRINT = (3, 3)
CANVAS = (192, 304)
ORIGIN = (0, 112)
DOCK = (1, 3)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 8, 6), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -1.0, 1.2, 2.2, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 3.4, -3.0, 1.8, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 2.2, 2.2, 3.0, 2),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -1.0, 1.2, 2.5, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 3.4, -3.0, 2.1, 5),
]
ROWS = (1.0, 2.9)
COLS = [-3.0 + k * 1.2 for k in range(6)]


def g_ground(S, dmg, i):
    K.pad(S, 5.0, 5.0)
    for k, (x, y) in enumerate(((-4.7, -4.7), (4.7, -4.7), (-4.7, 4.7), (4.7, 4.7))):
        K.lamp_post(S, str(k), x, y, (i + k) % 2 == 0 and not dmg)


def g_deck(S, dmg, i):
    S.bb("deck", -4.2, 4.2, 0.0, 4.4, 0.12, 0.7, "navy", 0.12)
    S.bb("dtide", -4.23, 4.23, -0.03, 4.43, 0.12, 0.34, "ash", 0.04)
    P.rail_ring(S, -4.2, 4.2, 0.0, 4.4, 0.7, t=0.14)
    S.bb("kerb", -4.0, 4.0, -0.03, 0.0, 0.4, 0.58, "orange", 0.0, nocast=True)
    S.bb("hstripe", -4.0, 4.0, -0.06, -0.03, 0.2, 0.34, "house", 0.0, nocast=True)
    K.EV.digits(S, "hn", "09", -3.0, -0.03, 0.46, 0.08, "white", flat=False)
    # stair / ramp to the deck
    S.bb("steps", 0.3, 1.5, -0.6, 0.0, 0.12, 0.6, "chrome2", 0.02)


def g_rails(S, dmg, i):
    for r, y in enumerate(ROWS):
        S.bb(f"sleeper{r}", -3.7, 3.7, y - 0.25, y + 0.25, 0.7, 0.85, "chrome2", 0.03)
        for c, x in enumerate(COLS):
            k = r * 6 + c
            K.beam(S, f"rail{k}", (x + 0.0, y - 0.05, 0.85), (x, y + 0.7, 3.0), 0.12, "chrome2", nocast=True)
    S.bb("sleepc", -3.8, 3.8, 0.5, 0.58, 0.85, 1.05, "copper", 0.0, nocast=True)




def g_miss(S, dmg, i):
    lit_n = int((i + 1) * 12 / 8) if not dmg else 0
    for r, y in enumerate(ROWS):
        for c, x in enumerate(COLS):
            k = r * 6 + c
            if dmg and k % 3 == 1:
                continue
            tip = "lamp" if k < lit_n else "orange"
            lean = 0.5 if (dmg and k % 3 == 2) else 0.0
            P.missile(S, f"m{k}", x, y + 0.1, 0.95, 0.75, 3.1, w=0.3, tip=tip, dx=lean)
    if dmg:
        K.beam(S, "railbrk", (COLS[1], ROWS[0] - 0.05, 0.85), (COLS[1] + 0.8, ROWS[0] + 0.3, 1.8), 0.12, "chrome2", nocast=True)


def g_gantry(S, dmg, i):
    S.bb("bunk", 1.6, 4.4, -3.8, -1.2, 0.12, 1.6, "white", 0.1)
    S.bb("bunktide", 1.57, 4.43, -3.83, -1.17, 0.12, 0.4, "ash", 0.0)
    S.bb("broof", 1.5, 4.5, -3.9, -1.1, 1.6, 1.74, "navy", 0.04)
    P.stripe(S, "bstripe", 1.6, 4.4, -3.8, 1.1, 1.3)
    S.bb("bwin", 1.9, 3.1, -3.83, -3.8, 0.75, 1.15, "glass_s" if dmg else "glass", 0.0, nocast=True)
    S.bb("bdoor", 3.5, 4.1, -3.83, -3.8, 0.12, 1.15, "orange", 0.0, nocast=True)
    # gantry tower with radar
    for ax in (-0.25, 0.25):
        for ay in (-0.25, 0.25):
            K.post(S, "tw", -3.9 + ax, -2.4 + ay, 0.12, 3.4 if not dmg else 2.2, 0.09, "chrome2")
    for z in (1.0, 1.9, 2.7):
        S.bb("br", -4.15, -3.65, -2.67, -2.63, z, z + 0.06, "chrome2", 0.0, nocast=True)
    if not dmg:
        P.radar_bar(S, "rad", -3.9, -2.4, 3.4, i, 8, 1.9)
        K.post(S, "mast", 4.4, -1.4, 1.74, 3.2, 0.07, "chrome2")
        S.sphere("lamp", 0.11, (4.4, -1.4, 3.3), "lamp" if i % 2 == 0 else "lamp_off")
    else:
        K.beam(S, "twbent", (-3.9, -2.4, 2.2), (-3.2, -2.1, 2.8), 0.1, "chrome2")
    # cables
    S.bb("cable", -3.0, 1.6, -2.55, -2.45, 0.12, 0.2, "dark", 0.0, nocast=True)
    K.flag(S, "flag", -4.7, -4.2, 3.4 if not dmg else 2.4, i, w=1.3, h=0.8, dmg=dmg)
    for k in range(3):
        S.cyl(f"drum{k}", 0.3, 0.8, (-1.6 + k * 0.62, -4.1, 0.52), "orange2" if k != 1 else "white2", verts=12)


GROUPS = [g_ground, g_deck, g_rails, g_miss, g_gantry]


def site(S):
    K.stakes_site(S, 4.4, 4.2)


def dmg_extra(S, i):
    P.scorch(S, "sc1", -1.5, 1.5, 0.2, 1.0, 0.7)
    P.scorch(S, "sc2", 2.0, 3.6, -3.2, -2.0, 0.12)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
