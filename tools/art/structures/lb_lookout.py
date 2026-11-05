"""Left Behind Lookout Mast (1x1): radar and minimap jam.  Original design.

A tall four-legged lattice mast bolted to a small flatbed trailer: a teal instrument cabin with a house panel and a bone stripe, a
lashed canvas sunshade, a railed platform near the top carrying a drum lamp, a spinning radar paddle of mismatched sheet metal on
the very top, whip antennas and a jammer coil in an ochre can, guy ropes to the pad, a mast flag.
Packs (T2 upgrade): the mast tilts down along the trailer bed.
Idle loop: the paddle turns 45 deg per frame (4 frames = half turn, the paddle is symmetric), the lamp blinks, the flag flaps.
Damage (structural only, D-069): mast snapped and leaning, paddle gone, cabin holed; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-1.7, 1.7].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_lookout"
TITLE = "Left Behind Lookout Mast"
FOOTPRINT = (1, 1)
CANVAS = (96, 192)
ORIGIN = (16, 112)
DOCK = (0, 1)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
TOP = 6.6
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 0.0, 0.0, 3.2, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.0, 0.0, 3.5, 3),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", -0.9, -0.7, 1.4, 5),
]


def g_base(S, dmg, i):
    K.pad(S, 1.62, 1.6)
    S.bb("bed", -1.5, 1.4, -1.0, 1.0, 0.55, 0.72, "wood", 0.03)
    S.bb("rail", -1.5, 1.4, -1.0, -0.85, 0.35, 0.55, "ash2", 0.02)
    for k, x in enumerate((-0.9, 0.7)):
        K.wheel(S, f"w{k}", x, -1.0, 0.4, 0.4 * (0.8 if dmg and k == 1 else 1.0), 0.3, "rust" if k else "steel")
    S.bb("jack", 1.35, 1.6, -0.15, 0.15, 0.1, 0.6, "ash2", 0.0)


def g_cabin(S, dmg, i):
    S.bb("cabin", -1.4, -0.1, -0.9, 0.9, 0.72, 1.9, "teal", 0.05)
    S.bb("cabroof", -1.5, 0.0, -1.0, 1.0, 1.9, 2.0, "canvas2" if not dmg else "canvas2_s", 0.03)
    S.bb("cabstripe", -1.4, -0.1, -0.94, -0.88, 1.3, 1.4, "bone", 0.0)
    S.bb("cabwin", -1.2, -0.7, -0.94, -0.9, 1.1, 1.7, "glass", 0.0)
    S.bb("cabpanel", -0.6, -0.15, -0.95, -0.9, 0.85, 1.3, "house", 0.0)
    if dmg:
        S.bb("hole", -1.1, -0.5, -0.96, -0.92, 0.9, 1.6, "char", 0.0, nocast=True)
    S.cyl("jam", 0.28, 0.5, (-1.0, 0.3, 2.25), "ochre", verts=12)
    S.cyl("jamband", 0.3, 0.07, (-1.0, 0.3, 2.3), "rust", verts=12)


def g_mast(S, dmg, i):
    top = TOP if not dmg else 3.4
    for ax in (-0.28, 0.28):
        for ay in (-0.28, 0.28):
            K.post(S, "leg", 0.5 + ax, 0.0 + ay, 0.72, top, 0.09, "rust2")
    z = 0.9
    while z < top - 0.6:
        K.beam(S, "br", (0.22, -0.28, z), (0.78, -0.28, z + 0.7), 0.05, "steel", nocast=True)
        K.beam(S, "br", (0.78, -0.28, z), (0.22, -0.28, z + 0.7), 0.05, "steel", nocast=True)
        S.bb("ring", 0.2, 0.8, -0.3, 0.3, z, z + 0.05, "steel", 0.0, nocast=True)
        z += 0.7
    if dmg:
        K.beam(S, "lean", (0.5, 0.0, top), (1.3, -0.3, 2.0), 0.2, "rust2")
        return
    zp = 4.4
    S.bb("deck", 0.1, 0.9, -0.4, 0.4, zp, zp + 0.08, "wood", 0.0)
    S.bb("deckrail", 0.1, 0.9, -0.42, -0.38, zp + 0.08, zp + 0.5, "ash2", 0.0, nocast=True)
    S.cyl("lamp", 0.16, 0.3, (0.7, -0.1, zp + 0.25), "ash2", verts=10)
    S.sphere("lamplens", 0.12, (0.7, -0.35, zp + 0.28), "lamp" if i % 2 == 0 else "lamp_off")
    S.cyl("hubpost", 0.05, 0.4, (0.5, 0.0, top + 0.2), "dark", verts=6)
    a = i * 45.0
    S.box("paddle", (1.7, 0.1, 0.7), (0.5, 0.0, top + 0.5), "ash", 0.02, rot=(0, 0, a), stretch=False)
    S.box("paddle2", (1.7, 0.12, 0.2), (0.5, 0.0, top + 0.3), "rust", 0.0, rot=(0, 0, a), stretch=False, nocast=True)
    S.box("paddlefeed", (0.1, 0.5, 0.1), (0.5, 0.0, top + 0.5), "dark", 0.0, rot=(0, 0, a), stretch=False, nocast=True)
    for k, (dx, h) in enumerate(((-0.4, 1.8), (0.9, 1.4))):
        S.cyl(f"whip{k}", 0.025, h, (0.5 + dx, 0.2, top + h / 2 - 0.1), "dark", verts=5)
    K.beam(S, "guy1", (0.5, 0.0, 3.6), (1.5, -1.2, 0.1), 0.03, "rope", nocast=True)
    K.beam(S, "guy2", (0.5, 0.0, 3.6), (-0.3, 1.3, 0.1), 0.03, "rope", nocast=True)


def g_extras(S, dmg, i):
    # lashed canvas sunshade on two poles in front of the cabin
    K.post(S, "sp1", -1.4, -1.5, 0.1, 1.5, 0.08, "wood")
    K.post(S, "sp2", 0.0, -1.5, 0.1, 1.5, 0.08, "wood")
    S.box("shade", (1.6, 0.9, 0.06), (-0.7, -1.2, 1.55), "canvas" if not dmg else "canvas_s", 0.0, rot=(-8, 0, 0))
    S.bb("shadefr", -1.4, 0.0, -1.66, -1.6, 1.3, 1.5, "house", 0.0, nocast=True)
    K.flag(S, "flag", -1.35, 0.75, 3.7 if not dmg else 2.8, i, w=0.9, h=0.6, dmg=dmg)
    K.drum(S, "d0", 1.2, 1.2, 0.1, m="rust", band="hazard", h=0.6, r=0.3)


GROUPS = [g_base, g_cabin, g_mast, g_extras]


def site(S):
    K.stakes_site(S, 1.4, 1.4, sign=False)


def dmg_extra(S, i):
    S.bb("rubble", 0.6, 1.4, -1.5, -1.0, 0.1, 0.25, "ash2", 0.04)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
