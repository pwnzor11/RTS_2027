"""Left Behind Flak Net (1x1): anti-air defence, a quad flak gun under a rope net.  Original design.

Four wooden poles at the corners of the cell hold a net of rope with a few canvas patches, one of them in the house colour, strung
at head height over a ring of lying tyres.  In the middle a pedestal of lashed drums carries a four-barrelled flak cluster (steel
barrels, gunmetal breech block, a canvas seat) tilted steeply up, with a house panel on the pedestal, an ammunition crate, a lamp.
Static fixed mount (no turret sheet, like dir_rocket_nest).  Idle loop: the net sways, the lamp blinks, a patch flaps.
Damage (structural only, D-069): one pole down and the net torn, barrels bent, pedestal drum tipped; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-1.7, 1.7].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_flak_net"
TITLE = "Left Behind Flak Net"
FOOTPRINT = (1, 1)
CANVAS = (64, 112)
ORIGIN = (0, 48)
DOCK = (0, 1)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 0.0, 0.2, 2.0, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.0, 0.2, 2.3, 3),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 1.0, -0.8, 0.8, 5),
]
NZ = 3.3     # net height


def g_ground(S, dmg, i):
    S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.1, "dust", 0.03)
    for k in range(9):
        if k in (3, 6):
            continue
        a = math.radians(k * 40 + 15)
        S.cyl(f"tyre{k}", 0.32, 0.28, (1.3 * math.sin(a), 1.25 * math.cos(a), 0.24), "rubber", verts=12, stretch=False)
    S.bb("crate", 0.9, 1.4, 0.5, 1.2, 0.1, 0.6, "wood", 0.03)
    S.bb("crateb", 0.95, 1.35, 0.55, 1.15, 0.6, 0.75, "hazard", 0.04)


def g_gun(S, dmg, i):
    for k, (dx, dy) in enumerate(((-0.35, -0.2), (0.35, -0.2), (0.0, 0.35))):
        S.cyl(f"ped{k}", 0.34, 0.9, (dx, dy, 0.1 + 0.45), ("rust", "teal", "ochre")[k], verts=12)
    S.cyl("pedband", 0.37, 0.08, (-0.35, -0.2, 0.55), "hazard", verts=12)
    S.bb("pedpanel", -0.5, 0.2, -0.56, -0.52, 0.45, 0.8, "house", 0.0)
    S.cyl("mount", 0.45, 0.1, (0.0, 0.0, 1.05), "ash2", verts=14)
    S.bb("breech", -0.3, 0.3, -0.25, 0.25, 1.1, 1.5, "gunmetal", 0.04)
    S.bb("seat", -0.2, 0.2, -0.65, -0.3, 1.15, 1.35, "canvas2", 0.08)
    for k, (dx, dz) in enumerate(((-0.14, 0.0), (0.14, 0.0), (-0.14, 0.18), (0.14, 0.18))):
        if dmg and k == 1:
            K.beam(S, f"barrel{k}", (dx, 0.1, 1.45 + dz), (dx + 0.5, 0.5, 1.9), 0.11, "steel")
            continue
        tip = (dx, 0.9, 2.7 + dz) if not dmg else (dx, 0.6, 2.2 + dz)
        K.beam(S, f"barrel{k}", (dx, 0.1, 1.45 + dz), tip, 0.11, "steel")
    if not dmg:
        S.bb("shroud", -0.28, 0.28, 0.2, 0.5, 1.7, 2.0, "ash2", 0.03)
        S.bb("brake", -0.3, 0.3, 0.85, 1.0, 2.62, 2.95, "dark", 0.02, nocast=True)


def g_net(S, dmg, i):
    poles = ((-1.45, -1.3), (1.45, -1.3), (-1.45, 1.3), (1.45, 1.3))
    for k, (px_, py_) in enumerate(poles):
        if dmg and k == 3:
            K.beam(S, "polefall", (px_, py_, 0.1), (px_ + 0.2, py_ - 0.2, 0.4), 0.12, "wood")
            K.beam(S, "polefall2", (px_ - 0.8, py_ + 0.1, 0.4), (px_ - 2.0, py_ - 0.4, 0.2), 0.12, "wood")
            continue
        K.post(S, "pole", px_, py_, 0.1, NZ + 0.1, 0.12, "wood")
        S.bb(f"polecap{k}", px_ - 0.1, px_ + 0.1, py_ - 0.1, py_ + 0.1, NZ + 0.1, NZ + 0.2, "hazard" if k == 0 else "rust2", 0.0)
    sag = 0.12 * math.sin(i * math.pi / 2)
    n = 5
    for k in range(n):
        t = k / (n - 1)
        y = -1.3 + 2.6 * t
        x = -1.45 + 2.9 * t
        z = NZ + sag * math.sin(math.pi * t) - 0.18 * math.sin(math.pi * t)
        if dmg and k >= 3:
            continue
        K.beam(S, f"nety{k}", (-1.45, y, NZ - 0.05), (1.45 if not (dmg and k >= 2) else 0.2, y, z), 0.04, "rope", nocast=True)
        K.beam(S, f"netx{k}", (x, -1.3, NZ - 0.05), (x, 1.3 if not (dmg and k >= 3) else 0.1, z), 0.04, "rope", nocast=True)
    # canvas patches on the net, one in the house colour
    for k, (x, y, m) in enumerate(((-0.6, -0.4, "canvas2"), (0.5, 0.6, "house"), (0.7, -0.7, "rust"), (-0.8, 0.8, "teal"))):
        if dmg and k >= 2:
            continue
        S.bb(f"patch{k}", x - 0.3, x + 0.3, y - 0.3, y + 0.3, NZ - 0.12 - 0.05 * math.sin(i + k), NZ - 0.07 - 0.05 * math.sin(i + k), m if not dmg else m + "_s" if m != "house" and m != "rust" else m, 0.0, nocast=True)
    S.sphere("lamp", 0.1, (-1.2, -1.1, 1.3), "lamp" if (i % 2 == 0 and not dmg) else "lamp_off", nocast=True)
    K.post(S, "lampp", -1.2, -1.1, 0.1, 1.2, 0.07, "wood")


GROUPS = [g_ground, g_gun, g_net]


def site(S):
    S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.1, "dust", 0.03)
    for sx, sy in ((-1.45, -1.3), (1.45, -1.3), (-1.45, 1.3), (1.45, 1.3)):
        K.post(S, "stake", sx, sy, 0.1, 0.9, 0.1, "wood")
    S.cyl("roll", 0.28, 1.2, (0.0, 0.8, 0.4), "rope", axis="X", verts=10)
    S.bb("planks", -1.0, 0.0, -0.8, -0.2, 0.1, 0.3, "wood", 0.0)


def dmg_extra(S, i):
    S.bb("scorch", -0.8, 0.8, -0.8, 0.8, 0.1, 0.12, "char", 0.0, nocast=True)
    S.box("fallendrum", (0.6, 0.6, 0.6), (1.0, -0.7 * S.YS, 0.4), "rust_s", 0.0, rot=(0, 20, 30), stretch=False)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
