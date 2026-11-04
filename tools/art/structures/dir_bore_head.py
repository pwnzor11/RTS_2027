"""Directorate Bore Head: large tunnel entrance, the upgrade of dir_hatch (1x1 cell, size-M entrance on the south face).  Original design.

Same pad, collar footprint, plinth, hazard bar and house colours as dir_hatch (the shared base lives in dir_hatch.base), but the head
is bigger: a taller collar, a wide ramp mouth under a heavy magenta-banded hood, and on the roof an A-frame carrying a big toothed
CUTTER WHEEL (bone disc, oxide teeth, spokes) with a magenta banner hanging below it, plus a vent stack.  Ember glow in the mouth.
Idle loop: the cutter wheel turns, vent puffs, glow flickers.  Damage is structural only (D-069): hood end fallen, wheel hanging off one
post.  Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint x, y in [-1.7, 1.7].
"""
import importlib.util
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D
_spec = importlib.util.spec_from_file_location("structlib_dir_hatch", os.path.join(_HERE, "dir_hatch.py"))
H = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(H)

L = D.L
ID = "dir_bore_head"
TITLE = "Directorate Bore Head"
FOOTPRINT = (1, 1)
CANVAS = (64, 140)
ORIGIN = (0, 68)
DOCK = (0, 0)
CAP_MATERIAL = D.CAP_MATERIAL
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = H.PAD
COLLAR_H = 1.6
WZ = 1.0          # wheel centre height above the roof
WR = 0.62


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def head(S, i, dmg, mode, zr):
    WY = 0.55
    zc = zr + WZ
    for sgn in (-1, 1):
        if dmg and sgn == 1:
            S.bb("postd", 0.8, 0.98, WY - 0.2, WY + 0.2, zr, zr + 0.5, "hull2", 0.01)
            continue
        S.bb(f"post{sgn}", sgn * 0.89 - 0.09, sgn * 0.89 + 0.09, WY - 0.2, WY + 0.2, zr, zc + 0.3, "hull2", 0.01)
        S.bb(f"postband{sgn}", sgn * 0.89 - 0.11, sgn * 0.89 + 0.11, WY - 0.22, WY + 0.22, zr + 0.2, zr + 0.4, "bone", 0.0)
    cx = -0.1 if dmg else 0.0
    cz = zr + 0.25 if dmg else zc
    S.cyl("wheel", WR, 0.2, (cx, WY, cz), "bone", axis="Y", verts=18)
    S.cyl("wheeld", WR - 0.1, 0.24, (cx, WY, cz), "dark", axis="Y", verts=16)
    for q in range(2):
        ang = i * 22.5 + q * 90
        S.box(f"spoke{q}", (2 * WR - 0.1, 0.26, 0.1), (cx, (WY - 0.06) * S.YS, cz), "bone", 0.0, rot=(0, ang, 0), stretch=False)
    for k in range(8):
        a = math.radians(k * 45 + i * 22.5)
        S.bb(f"tooth{k}", cx + (WR + 0.02) * math.cos(a) - 0.07, cx + (WR + 0.02) * math.cos(a) + 0.07, WY - 0.13, WY + 0.0,
             cz + (WR + 0.02) * math.sin(a) - 0.07, cz + (WR + 0.02) * math.sin(a) + 0.07, "oxide", 0.0)
    S.cyl("hub", 0.14, 0.3, (cx, WY - 0.04, cz), "oxide", axis="Y", verts=10)
    if not dmg:
        S.bb("banner", -0.3, 0.3, WY - 0.26, WY - 0.22, zr + 0.05, zc - 0.45, "house", 0.0)
        S.bb("bannertrim", -0.3, 0.3, WY - 0.28, WY - 0.26, zr + 0.05, zr + 0.17, "bone", 0.0)
    # vent stack at the back
    S.cyl("vent", 0.22, 0.9, (0.9, 1.0, zr + 0.45), "hull2", verts=10)
    S.cyl("venth", 0.3, 0.1, (0.9, 1.0, zr + 0.95), "steel", verts=10)
    if mode != "build" and not dmg:
        for q in range(2):
            ph = ((i / 4) + q / 2) % 1
            r = 0.1 + 0.25 * math.sin(math.pi * ph) ** 0.7
            S.sphere(f"puff{q}", r, (0.9 + ph * 0.6, 1.0, zr + 1.2 + ph * 0.9), "smoke2", squash=0.85)


def finished(S, i, dmg, mode):
    lit = mode in ("idle", "build") and not dmg
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    zr = H.base(S, dmg, COLLAR_H, (-0.75, 0.75), -1.35, -0.7, 4, 1.35, 1.35, 0.0)
    # house panel on the collar face beside the mouth and ember glow strip
    S.bb("hpanel", 0.82, 1.2, H.YD - 0.1, H.YD - 0.06, 0.55, 1.2, "house", 0.0)
    S.bb("hpaneltrim", 0.82, 1.2, H.YD - 0.12, H.YD - 0.1, 0.55, 0.68, "bone", 0.0)
    S.bb("stenW", -1.2, -0.82, H.YD - 0.06, H.YD, 0.55, 1.2, "bone", 0.0)
    for k in range(2):
        S.bb(f"sten_w{k}", -1.15, -0.87, H.YD - 0.08, H.YD - 0.06, 0.7 + k * 0.25, 0.82 + k * 0.25, "dark", 0.0, nocast=True)
    head(S, i, dmg, mode, zr)
    if dmg:
        L.R.scorch(S, "scH", 0.0, -1.0, 1.1, 0.7, 0.5)


def _clutter(S, i):
    S.bb("crate", 0.9, 1.4, -1.4, -0.9, 0.14, 0.5, "crate", 0.03)


def _left(S):
    S.bb("planks", -1.4, -0.8, -1.2, -0.9, 0.14, 0.25, "timber", 0.0)
    L.spark(S, 0.0, -0.8, 1.2, 3, 6)


FX = [
    dict(state="damaged", kind="fire_small", x=anchor(0.0, 0.6, 2.0)[0], y=anchor(0.0, 0.6, 2.0)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(0.0, 0.6, 2.1)[0], y=anchor(0.0, 0.6, 2.1)[1], phase=3),
]

SPEC = dict(
    pad=PAD, env=(-1.3, 1.3, -0.25, 1.3), H=1.8, boxes=[(-1.3, 1.3, -0.25, 1.3)], rings=[],
    clips=(0.9, 1.7), scaf=[(-1.5, -1.0, -1.5, -1.0), (0.9, 1.4, -1.5, -1.0)],
    derrick=(1.4, -1.2, 2.4, (1.0, -0.9, 2.6)), sparks=[(-0.8, 0.0, 1.5), (0.5, 0.8, 1.6), (0.0, 1.0, 1.6)],
    hide=D.HIDE_ANIM + ("hpanel", "wheel", "spoke", "tooth", "hub", "banner"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
