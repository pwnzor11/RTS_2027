"""Expedition Plotting Room (2x2): radar and designation centre, the tier-2 gate.  Original design.

A windowless navy blockhouse on its grey ash tidemark; a lagoon-white chart-room deck above it with copper trim and a team-colour stripe
(no windows: only a glass slit), a lattice radar tower at the north-east turning a chrome slab antenna, a white radome beside it, a
teleprinter annex with an orange door under a canopy and a telex aerial.  Idle loop: antenna turns, lamps blink, flag, vent steam.
Damage (structural, D-069): roof hatch blown in, antenna bent, radome cracked open, flag in rags; fire and smoke are FX anchors.
Build: surveyed site, apron, blockhouse, chart deck, radar, annex.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K
import exp_parts_art_exp_e as P

ID = "exp_plotting_room"
TITLE = "Expedition Plotting Room"
FOOTPRINT = (2, 2)
CANVAS = (128, 200)
ORIGIN = (0, 72)
DOCK = (0, 0)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 0.8, 1.2, 2.8, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", -1.6, 0.6, 2.6, 4),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.8, 1.2, 3.1, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -1.6, 0.6, 2.9, 5),
]


def g_ground(S, dmg, i):
    P.apron(S, -3.3, 3.3, -3.3, 3.3)
    for k, (x, y) in enumerate(((-3.0, -3.0), (3.0, -3.0))):
        K.lamp_post(S, str(k), x, y, (i + k) % 2 == 0 and not dmg)


def g_block(S, dmg, i):
    P.block(S, "bunker", -2.9, 2.0, -1.6, 2.8, 0.12, 1.5, "navy", 0.1, tidemark=True)
    P.rail_ring(S, -2.9, 2.0, -1.6, 2.8, 1.5)
    P.stripe(S, "hstripe", -2.8, 1.9, -1.6, 0.7, 0.95)
    K.EV.digits(S, "hn", "04", -1.9, -1.6, 0.9, 0.12, "white", flat=False)
    S.bb("slit", -0.4, 1.4, -1.63, -1.6, 1.0, 1.2, "glass", 0.0, nocast=True)
    # chart-room deck, windowless
    S.bb("deck", -2.5, 1.2, -1.2, 2.2, 1.58, 2.4, "white", 0.1)
    P.trim(S, "dtrim", -2.5, 1.2, -1.2, 2.3, 0.1)
    P.stripe(S, "dstripe", -2.5, 1.2, -1.2, 1.7, 1.85)
    if dmg:
        S.box("roofhole", (1.4, 1.0, 0.1), (0.2, 1.3 * S.YS, 2.45), "char", 0.0, stretch=False, nocast=True)
        S.box("roofflap", (1.0, 0.9, 0.1), (-0.8, 0.8 * S.YS, 2.6), "navy2", 0.0, rot=(0, 25, 12), stretch=False)
    else:
        S.bb("hatch", -0.6, 0.6, 0.2, 1.2, 2.4, 2.5, "chrome2", 0.04)
        for k in range(3):
            S.bb(f"vent{k}", -2.2 + k * 0.5, -1.9 + k * 0.5, 1.4, 2.0, 2.4, 2.55, "chrome2", 0.02)
        K.puffs(S, "vap", -1.0, 1.7, 2.7, i, n=2, drift=0.6)


def g_radar(S, dmg, i):
    cx, cy = 2.2, 1.9
    for ax in (-0.3, 0.3):
        for ay in (-0.3, 0.3):
            K.post(S, "tw", cx + ax, cy + ay, 0.12, 2.8, 0.09, "chrome2")
    for z in (0.9, 1.7, 2.5):
        S.bb("brace", cx - 0.3, cx + 0.3, cy - 0.32, cy - 0.28, z, z + 0.06, "chrome2", 0.0, nocast=True)
        S.bb("braceb", cx - 0.3, cx + 0.3, cy + 0.28, cy + 0.32, z, z + 0.06, "chrome2", 0.0, nocast=True)
    S.bb("tcap", cx - 0.42, cx + 0.42, cy - 0.42, cy + 0.42, 2.8, 2.9, "copper", 0.02)
    S.bb("tband", cx - 0.32, cx + 0.32, cy - 0.34, cy - 0.3, 1.2, 1.4, "house", 0.0, nocast=True)
    P.radar_bar(S, "rad", cx, cy, 2.9, i, 8, 2.2, dmg)
    # radome
    if dmg:
        S.cyl("domeb", 0.75, 0.3, (-2.1, -2.1, 0.27), "chrome2", verts=20)
        S.sphere("dome", 0.7, (-2.1, -2.1, 0.5), "white2", nocast=False, squash=0.5)
    else:
        P.dome(S, "dome", -2.1, -2.1, 0.12, 0.8)
        S.sphere("domelamp", 0.1, (-2.1, -2.1, 1.1), "lamp" if i % 2 == 0 else "lamp_off")


def g_annex(S, dmg, i):
    S.bb("annex", 0.6, 2.8, -2.8, -1.7, 0.12, 1.2, "white", 0.08)
    S.bb("aroof", 0.5, 2.9, -2.9, -1.6, 1.2, 1.3, "navy", 0.03)
    P.stripe(S, "astripe", 0.6, 2.8, -2.8, 0.95, 1.1)
    S.bb("awin", 0.8, 1.5, -2.83, -2.8, 0.5, 0.85, "glass", 0.0, nocast=True)
    S.bb("adoor", 1.9, 2.5, -2.83, -2.8, 0.12, 1.0, "orange", 0.0, nocast=True)
    S.bb("canopy", 1.8, 2.6, -3.2, -2.8, 1.05, 1.12, "navy2", 0.02)
    K.post(S, "ant", 0.9, -2.2, 1.3, 3.0 if not dmg else 2.2, 0.06, "chrome2")
    if not dmg:
        S.bb("dip", 0.4, 1.4, -2.25, -2.15, 2.8, 2.86, "chrome", 0.0, nocast=True)
    for k in range(2):
        S.bb(f"crate{k}", -0.1 + k * 0.1, 0.5 + k * 0.1, -2.9 + k * 0.1, -2.3 + k * 0.1, 0.12 + 0.6 * k, 0.72 + 0.6 * k, "navy" if k == 0 else "orange2", 0.05)


def g_extras(S, dmg, i):
    K.flag(S, "flag", -2.9, 2.9, 3.0 if not dmg else 2.2, i, w=1.1, h=0.7, dmg=dmg)


GROUPS = [g_ground, g_block, g_radar, g_annex, g_extras]


def site(S):
    K.stakes_site(S, 3.0, 3.0)


def dmg_extra(S, i):
    P.scorch(S, "sc1", -0.2, 1.4, -1.8, -1.2, 0.12)
    S.bb("sc2", 1.6, 2.0, -1.62, -1.6, 0.8, 1.4, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
