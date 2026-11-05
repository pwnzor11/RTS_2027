"""Left Behind Garage Rig (3x3): vehicle production, an open workshop on a big flatbed.  Original design.

A wide flatbed with a teal bus-body tool wall and a rust container stacked on top at the west end, a patched canvas roof sagging
over the north half of the bay, and the south half open: a half-built buggy on blocks (rust frame, mismatched wheels, a teal
hood still in primer), a gantry hoist (hazard-yellow beam, chain and hook that sway), tyre stacks, a drum rack, two stovepipes, a
loudspeaker horn, a boom crane, a mast with the house flag and a house panel on the tool wall.
Packs: the roof and gantry fold flat, the chassis on blocks goes onto the deck.
Idle loop: welding sparks flash, the hook swings, smoke, the flag flaps, a work lamp blinks.  Damage (structural only, D-069): roof
fallen in on one side, gantry beam bent, tool wall holed, a stovepipe snapped; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_garage_rig"
TITLE = "Left Behind Garage Rig"
FOOTPRINT = (3, 3)
CANVAS = (192, 264)
ORIGIN = (0, 72)
DOCK = (1, 3)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 2.2, 1.6, 3.6, 0),      # fallen roof
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 2.2, 1.6, 3.9, 2),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", -3.2, -2.5, 2.4, 5),    # tool wall breach
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -0.6, 1.4, 5.0, 6),
]


def g_chassis(S, dmg, i):
    K.pad(S, 4.9, 4.7)
    S.bb("railS", -4.9, 4.8, -3.55, -3.2, 0.62, 1.25, "ash2", 0.04)
    S.bb("railN", -4.9, 4.8, 3.2, 3.55, 0.62, 1.25, "ash2", 0.04)
    S.bb("deck", -4.9, 4.8, -3.6, 3.6, 1.25, 1.45, "wood", 0.03)
    S.bb("bump", 4.7, 4.9, -3.0, 3.0, 0.7, 1.1, "hazard", 0.02)
    for k, (x, r) in enumerate(((-3.7, 0.66), (-2.4, 0.58), (1.5, 0.66), (3.4, 0.7))):
        K.wheel(S, f"w{k}", x, -3.05, r + 0.1, r * (0.8 if dmg and k == 2 else 1), 0.55, "steel" if k % 2 == 0 else "rust")
        K.wheel(S, f"wn{k}", x, 3.0, r + 0.1, r, 0.55)


def g_walls(S, dmg, i):
    # tool wall: teal bus body on the west with a rust container stacked on it, low ash back wall on the north
    S.bb("bus", -4.8, -1.6, -2.6, 2.7, 1.45, 3.5, "teal", 0.07)
    S.bb("busroof", -4.9, -1.5, -2.7, 2.8, 3.5, 3.64, "canvas2" if not dmg else "canvas2_s", 0.04)
    S.bb("busstripe", -4.8, -1.6, -2.65, -2.55, 2.25, 2.4, "bone", 0.0)
    for k, (x0, x1, m) in enumerate(((-4.5, -3.6, "glass"), (-3.3, -2.5, "wood"), (-2.2, -1.8, "glass"))):
        S.bb(f"swin{k}", x0, x1, -2.64, -2.6, 2.6, 3.2, m, 0.0)
    S.bb("sdoor", -4.5, -3.9, -2.64, -2.6, 1.5, 2.3, "ash2", 0.0)
    S.bb("spatch1", -3.4, -2.4, -2.67, -2.6, 1.6, 2.15, "rust", 0.0)
    S.bb("spatch2", -2.3, -1.7, -2.67, -2.6, 1.55, 2.0, "ash", 0.0)
    S.bb("cont", -4.6, -2.6, -2.0, 1.9, 3.64, 4.7, "rust", 0.05)
    S.bb("contdoor", -4.4, -2.8, -2.04, -2.0, 3.8, 4.5, "rust2", 0.0)
    S.bb("contpanel", -4.3, -2.9, -2.08, -2.04, 3.85, 4.4, "house", 0.0)
    S.bb("contstripe", -4.6, -2.6, -2.04, -2.0, 3.64, 3.74, "hazard", 0.0, nocast=True)
    S.bb("back", -1.6, 4.8, 2.6, 3.0, 1.45, 3.0, "ash2", 0.04)
    for k, x in enumerate((-0.8, 0.8, 2.4, 4.0)):
        S.bb(f"backp{k}", x - 0.5, x + 0.5, 2.56, 2.6, 1.6, 2.7, ("rust", "teal", "ash", "ochre")[k], 0.0)
    if dmg:
        S.bb("wallhole", -4.0, -3.0, -2.68, -2.64, 1.8, 3.1, "char", 0.0, nocast=True)


def g_roof(S, dmg, i):
    # patched canvas roof over the north half of the bay, east end held by posts
    rise = 0.9 if not dmg else 1.8
    run = 3.4
    ang = math.degrees(math.atan2(rise, run * S.YS))
    ln = math.hypot(run * S.YS, rise) / S.YS
    zm = 4.0 - rise / 2
    for k, (x0, x1, m) in enumerate(((-1.5, 0.1, "canvas2"), (0.1, 1.7, "teal"), (1.7, 3.3, "canvas2"), (3.3, 4.9, "rust"))):
        if dmg and k == 3:
            S.box(f"roof{k}", (x1 - x0, ln, 0.07), ((x0 + x1) / 2, 1.3, 1.6), "rust_s", 0.0, rot=(-40, 0, 0))
            continue
        S.box(f"roof{k}", (x1 - x0, ln, 0.07), ((x0 + x1) / 2, 1.3, zm), m if not dmg else m + "_s" if m != "rust" else "rust_s", 0.0, rot=(ang, 0, 0))
    zf = 4.0 - rise
    for px in (-1.2, 1.7, 4.6):
        if dmg and px > 4:
            continue
        K.post(S, "rpost", px, -0.3, 1.45, zf + 0.05, 0.12, "wood")
    for k in range(6):
        x = -1.3 + k * 1.05
        S.bb(f"fringe{k}", x, x + 0.5, -0.46 + 0.04 * math.sin(i + k), -0.4 + 0.04 * math.sin(i + k), zf - 0.3, zf, "house" if k == 2 else ("canvas2" if k % 2 else "rust"), 0.0, nocast=True)


def g_gear(S, dmg, i):
    # half-built buggy on blocks in the open south half of the bay
    S.bb("block1", 0.2, 0.7, -2.7, -1.4, 1.45, 1.75, "wood", 0.02)
    S.bb("block2", 3.2, 3.7, -2.7, -1.4, 1.45, 1.75, "wood", 0.02)
    S.bb("frameS", 0.0, 4.0, -2.65, -2.4, 1.75, 2.0, "rust2", 0.03)
    S.bb("frameN", 0.0, 4.0, -1.5, -1.25, 1.75, 2.0, "rust2", 0.03)
    for k, x in enumerate((0.6, 3.4)):
        S.bb(f"axle{k}", x - 0.07, x + 0.07, -2.7, -1.2, 1.75, 1.9, "dark", 0.0)
    S.bb("hood", 2.2, 3.9, -2.55, -1.35, 2.0, 2.55, "teal" if i != 7 else "primer", 0.06)
    S.bb("seat", 1.2, 1.9, -2.3, -1.6, 2.0, 2.45, "canvas2", 0.1)
    S.bb("rollbar", 0.4, 0.5, -2.5, -1.4, 2.0, 3.0, "steel", 0.0)
    S.bb("rollbar2", 0.4, 1.3, -2.5, -2.4, 2.9, 3.0, "steel", 0.0)
    S.bb("engine", 1.9, 2.5, -2.0, -1.5, 2.55, 2.9, "ash2", 0.03)
    # gantry hoist: two posts and a hazard beam, chain and hook sway
    for x in (-0.3, 4.4):
        K.post(S, "gp", x, -3.0, 1.45, 4.2 if not dmg else 3.4, 0.16, "steel")
    if dmg:
        K.beam(S, "gbeam", (-0.3, -3.0, 4.2), (4.4, -3.0, 3.4), 0.28, "hazard")
    else:
        K.beam(S, "gbeam", (-0.3, -3.0, 4.2), (4.4, -3.0, 4.2), 0.28, "hazard")
        sw = 0.25 * math.sin(i * math.pi / 2)
        K.beam(S, "chain", (2.1, -3.0, 4.1), (2.1 + sw, -2.7, 2.9), 0.05, "dark", nocast=True)
        S.bb("hook", 2.0 + sw, 2.3 + sw, -2.8, -2.6, 2.6, 2.9, "hazard", 0.0)
    for k in range(3):
        K.tyre(S, f"ty{k}", 4.3, -0.6 + k * 0.1, 1.45 + k * 0.35, 0.55, 0.35)
    K.drum(S, "d0", -1.2, -2.4, 1.45, m="rust", band="hazard")
    K.drum(S, "d1", -0.6, -2.8, 1.45, m="teal")
    S.bb("crate", 1.6, 2.6, 1.0, 2.0, 1.45, 2.2, "wood", 0.03)
    S.bb("crateb", 1.7, 2.5, 1.1, 1.9, 2.2, 2.5, "canvas2", 0.1)
    if not dmg and i % 2 == 0:
        K.spark(S, 3.0, -2.0, 2.65, i)


def g_extras(S, dmg, i):
    K.stovepipe(S, "p1", -0.8, 1.8, 3.9, 5.5 if not dmg else 4.5, i, smoke=True, broken=dmg)
    K.stovepipe(S, "p2", 0.3, 2.2, 3.9, 5.0, i + 2, smoke=False)
    S.cyl("hornpole", 0.05, 0.8, (-3.6, 0.6, 4.1), "ash2", verts=6)
    S.sphere("worklamp", 0.12, (-1.4, -3.0, 3.0), "lamp" if (i % 2 == 0 and not dmg) else "lamp_off", nocast=True)
    K.flag(S, "flag", -4.6, -3.0, 6.4 if not dmg else 5.0, i, w=1.5, h=1.0, dmg=dmg)
    # boom crane on the north-east corner
    K.post(S, "crane", 4.4, 3.0, 1.45, 4.6, 0.2, "rust2")
    if dmg:
        K.beam(S, "boom", (4.4, 3.0, 4.6), (3.2, 2.4, 3.6), 0.2, "rust")
    else:
        K.beam(S, "boom", (4.4, 3.0, 4.6), (2.6, 3.0, 5.3), 0.2, "rust")
        K.beam(S, "boomc", (2.6, 3.0, 5.2), (2.6, 3.0, 4.0), 0.04, "dark", nocast=True)


GROUPS = [g_chassis, g_walls, g_roof, g_gear, g_extras]


def site(S):
    K.stakes_site(S, 4.3, 4.0)


def dmg_extra(S, i):
    S.bb("rubble", 3.6, 4.6, -4.4, -3.8, 0.1, 0.3, "ash2", 0.04)
    S.box("fallen", (1.4, 0.6, 0.1), (-1.0, -4.0 * S.YS, 0.2), "teal", 0.0, rot=(0, 0, 20), stretch=False)
    S.bb("scorch", 1.0, 3.0, 0.0, 1.8, 3.2, 3.25, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
