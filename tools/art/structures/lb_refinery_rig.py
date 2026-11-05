"""Left Behind Refinery Rig (2x2): a mobile refinery on a flatbed.  Original design.

A tall ochre still column with rust bands and a ladder at the east end, a long teal settling tank lashed on the deck, a coil of
mismatched pipes between them, a flare stack with a flickering flame, an ore hopper with a sloped chute over the south edge where
the harvester docks, a small rust-red operator cab with a canvas awning and a house panel, drums and a mast flag.
Packs: the column lowers onto the deck, the hopper folds up, so it can sit on a crawler.
Idle loop: flare flickers between two flame tones, smoke from the cab pipe, flag flaps, a gauge lamp blinks.  Damage (structural
only, D-069): column cracked and leaning, flare stack snapped, hopper bent, awning down; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_refinery_rig"
TITLE = "Left Behind Refinery Rig"
FOOTPRINT = (2, 2)
CANVAS = (128, 192)
ORIGIN = (0, 64)
DOCK = (0, 2)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
COL = (1.7, 0.8)
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 1.7, 0.8, 3.2, 0),      # column crack
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 1.7, 0.8, 3.5, 3),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", -2.6, 1.2, 3.0, 5),     # flare stump
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -2.6, 1.2, 3.3, 7),
]


def g_chassis(S, dmg, i):
    K.pad(S, 3.25, 3.2)
    S.bb("railS", -3.1, 3.0, -2.5, -2.2, 0.55, 1.1, "ash2", 0.04)
    S.bb("railN", -3.1, 3.0, 2.2, 2.5, 0.55, 1.1, "ash2", 0.04)
    S.bb("deck", -3.1, 3.0, -2.6, 2.6, 1.1, 1.28, "wood", 0.03)
    S.bb("bump", 2.95, 3.1, -2.2, 2.2, 0.6, 1.0, "hazard", 0.02)
    for k, (x, r) in enumerate(((-2.3, 0.6), (-1.0, 0.6), (1.0, 0.5), (2.3, 0.65))):
        K.wheel(S, f"w{k}", x, -2.3, r + 0.1, r, 0.5, "rust" if k == 1 else "steel")
        K.wheel(S, f"wn{k}", x, 2.3, r + 0.1, r, 0.5)


def g_vessels(S, dmg, i):
    cx, cy = COL
    h = 3.0
    S.cyl("colbase", 0.95, 0.3, (cx, cy, 1.43), "ash2", verts=20)
    S.cyl("col", 0.8, h, (cx, cy, 1.28 + h / 2), "ochre" if not dmg else "ochre_s", verts=20)
    for k, z in enumerate((1.9, 2.7, 3.5)):
        S.cyl(f"colband{k}", 0.85, 0.14, (cx, cy, z), "rust", verts=20)
    if dmg:
        S.bb("crack", cx - 0.1, cx + 0.15, cy - 0.85, cy - 0.78, 2.0, 3.7, "char", 0.0, nocast=True)
        S.cyl("coltop", 0.8, 0.1, (cx, cy, 1.28 + h + 0.05), "dark", verts=16)
    else:
        S.cone("colcap", 0.82, 0.18, 0.6, (cx, cy, 1.28 + h + 0.3), "rust2", verts=20, nocast=False)
        S.cyl("colvent", 0.07, 0.4, (cx, cy, 1.28 + h + 0.8), "ash2", verts=8)
    for k in range(6):
        S.bb(f"rung{k}", cx + 0.8, cx + 1.05, cy - 0.1, cy - 0.06, 1.5 + k * 0.45, 1.56 + k * 0.45, "wood", 0.0, nocast=True)
    # long settling tank
    S.cyl("tank", 0.75, 3.0, (-0.9, -0.5, 1.28 + 0.75), "teal", axis="X", verts=20)
    for k, x in enumerate((-2.0, -0.9, 0.2)):
        S.cyl(f"tankband{k}", 0.79, 0.12, (x, -0.5, 2.03), "rust" if k != 1 else "hazard", axis="X", verts=20)
    S.bb("tankpanel", -2.5, -0.9, -1.3, -1.25, 1.75, 2.3, "house", 0.0)
    for x in (-1.8, 0.0):
        S.bb(f"saddle{x}", x - 0.2, x + 0.2, -1.1, 0.1, 1.28, 1.5, "ash2", 0.02)


def g_pipes(S, dmg, i):
    cx, cy = COL
    S.cyl("pipeA", 0.13, 2.2, (0.4, 0.4, 3.0), "ash", axis="X", verts=10)
    S.cyl("pipeB", 0.13, 1.6, (0.8, 0.4, 2.2), "ash2", axis="X", verts=10)
    S.cyl("pipeV", 0.13, 1.8, (-0.6, 0.4, 2.1), "ash", verts=10)
    for k, x in enumerate((-0.2, 0.9)):
        S.cyl(f"fl{k}", 0.2, 0.1, (x, 0.4, 3.0), "rust", axis="X", verts=10)
    # flare stack at the west end with a flickering flame
    fx_, fy_ = -2.6, 1.2
    top = 3.0 if dmg else 4.4
    S.cyl("flare", 0.16, top - 1.28, (fx_, fy_, (top + 1.28) / 2), "ash2", verts=10)
    S.cyl("flareband", 0.2, 0.12, (fx_, fy_, 2.4), "rust", verts=10)
    if not dmg:
        a = i % 2
        S.cone("flame", 0.2, 0.02, 0.9 + 0.3 * a, (fx_, fy_, top + 0.45 + 0.15 * a), "flame" if a == 0 else "flame2", verts=8, nocast=True)
        S.sphere("flamecore", 0.12, (fx_, fy_, top + 0.1), "lamp", nocast=True)
    else:
        S.cyl("flarex", 0.16, 0.1, (fx_, fy_, top + 0.04), "dark", verts=8)
        K.beam(S, "flarefall", (fx_ + 0.1, fy_, top), (fx_ + 1.1, fy_ - 0.5, 1.4), 0.3, "ash2")


def g_cab(S, dmg, i):
    S.bb("cab", -3.0, -1.6, 0.9, 2.4, 1.28, 2.9, "rust", 0.07)
    S.bb("cabroof", -3.1, -1.5, 0.8, 2.5, 2.9, 3.05, "canvas2", 0.04)
    S.bb("cabwin", -2.8, -1.8, 0.86, 0.9, 1.9, 2.5, "glass", 0.0)
    S.bb("cabpanel", -2.85, -1.75, 0.82, 0.86, 1.4, 1.8, "house", 0.0)
    K.stovepipe(S, "cpipe", -2.0, 1.7, 3.0, 4.1 if not dmg else 3.4, i, smoke=True, broken=dmg)
    # hopper and sloped chute over the south edge (harvester dock)
    S.bb("hopper", 0.0, 1.5, -2.5, -1.4, 1.28, 2.1, "ash2", 0.05)
    S.bb("hopperrim", -0.1, 1.6, -2.6, -2.45, 2.05, 2.3, "rust", 0.04)
    ang = -math.degrees(math.atan2(0.9, 1.0 * S.YS))
    S.box("chute", (1.3, 1.2, 0.12), (0.75, -3.0, 1.0), "steel", 0.03, rot=(ang, 0, 0))
    for sx in (-1, 1):
        S.box("cheek", (0.14, 1.2, 0.4), (0.75 + sx * 0.65, -3.0, 1.1), "rust", 0.03, rot=(ang, 0, 0))


def g_extras(S, dmg, i):
    K.drum(S, "d0", 2.5, -1.9, 1.28, m="rust", band="hazard")
    K.drum(S, "d1", 2.5, -1.2, 1.28, m="ochre")
    S.sphere("gaugelamp", 0.1, (cx_gauge(), -0.2, 2.9), "lamp" if (i % 2 == 0 and not dmg) else "lamp_off")
    K.flag(S, "flag", 1.7, 2.4, 5.0 if not dmg else 3.6, i, w=1.2, h=0.8, dmg=dmg)
    S.bb("sack", -0.6, 0.4, 1.8, 2.5, 1.28, 1.7, "canvas2", 0.12)


def cx_gauge():
    return 0.9


GROUPS = [g_chassis, g_vessels, g_pipes, g_cab, g_extras]


def site(S):
    K.stakes_site(S, 2.7, 2.7)


def dmg_extra(S, i):
    S.bb("rubble", -3.0, -2.2, -3.1, -2.6, 0.1, 0.3, "ash2", 0.04)
    S.bb("spill", 0.2, 1.4, -3.3, -2.9, 0.1, 0.13, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
