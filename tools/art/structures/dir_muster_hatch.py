"""Directorate Muster Hatch: conscript barracks (2x2 cells, size-S tunnel entrance on the south face).  Original design.

The read is a LOW, WIDE DRILL HALL: a long umber bunker with a narrow stair mouth under a small magenta-banded hood, flanked by
stencilled wall panels (a bone number board and a magenta house panel).  On the roof a big double steel HATCH stands folded open over
a glowing pit (hazard-striped leaves), a loudspeaker pair on a pole and a short ash stack at the back.  Bone drill lines on the pad,
bone / oxide hazard bar along the south edge.
Idle loop: pit glow flickers, ash puffs.  Damage is structural only (D-069): hood end fallen, one hatch leaf buckled, stack snapped.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint x, y in [-3.4, 3.4].
"""
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_muster_hatch"
TITLE = "Directorate Muster Hatch"
FOOTPRINT = (2, 2)
CANVAS = (128, 220)
ORIGIN = (0, 76)
DOCK = (1, 2)           # conscripts exit just below the mouth
CAP_MATERIAL = D.CAP_MATERIAL
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-3.3, 3.3, -3.3, 3.3)
TR = (-0.85, 0.85)
TR_YTOP, TR_BOT = -3.15, -0.8
YD = -1.4
HALL = (-3.0, 3.0, YD, 3.0)
HALL_H = 1.8
ZR = HALL_H + 0.22
STK = (-2.4, 2.4)
STK_TOP = 3.0


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def hatch_leaves(S, i, dmg, lit):
    x0, x1 = 0.3, 2.0
    S.bb("pitrim", x0 - 0.1, x1 + 0.1, 0.1, 2.4, ZR, ZR + 0.12, "concrete2", 0.03)
    S.bb("pit", x0, x1, 0.2, 2.3, ZR + 0.1, ZR + 0.13, "dark", 0.0)
    g = "redglow" if lit and i % 2 == 0 else ("oxide" if lit else "oxide2")
    S.bb("glowpit", x0 + 0.2, x1 - 0.2, 0.6, 1.9, ZR + 0.13, ZR + 0.16, g, 0.0, nocast=True)
    # south leaf folded up and back toward us, north leaf folded up and back
    D.tilted(S, "leafS", x0, x1, 0.1, ZR + 0.12, -0.5, ZR + 1.0, 0.12, "hull2", 0.02)
    D.tilted(S, "leafSb", x0, x1, -0.08, ZR + 0.4, -0.5, ZR + 0.85, 0.14, "oxide", 0.0, nocast=True)
    if dmg:
        S.box("leafNd", (1.7, 1.0, 0.1), (1.2, 2.9 * S.YS, ZR + 0.25), "hull2", 0.02, rot=(0, 10, 12), stretch=False)
    else:
        D.tilted(S, "leafN", x0, x1, 2.4, ZR + 0.12, 3.0, ZR + 1.0, 0.12, "hull2", 0.02)
        D.tilted(S, "leafNb", x0, x1, 2.42, ZR + 0.4, 2.9, ZR + 0.82, 0.14, "bone", 0.0, nocast=True)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    lit = (idle or mode == "build") and not dmg
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    x0, x1, y0, y1 = HALL
    D.pad_with_trench(S, PAD, TR, TR_YTOP, YD + 0.02)
    D.hazard_bar(S, -3.3, 3.3, -3.25, -2.95, 0.6)
    for k in range(3):
        for sgn in (-1, 1):
            xa = 1.3 if sgn > 0 else -3.0
            S.bb(f"drill{k}{sgn}", xa, xa + 1.7, -2.65 + k * 0.28, -2.58 + k * 0.28, 0.14, 0.16, "bone", 0.0, nocast=True)
    D.ramp_entrance(S, TR, TR_YTOP, YD + 0.02, TR_BOT, dmg, mouth_top=1.25, nst=5)
    S.bb("hall", x0, x1, y0, y1, 0.14, HALL_H, "concrete", 0.05)
    S.bb("hplinth", x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("hroof", x0 - 0.1, x1 + 0.15, y0 - 0.2, y1 + 0.1, HALL_H, ZR, "roof", 0.05)
    S.bb("hparS", x0 - 0.1, x1 + 0.15, y0 - 0.2, y0 - 0.06, ZR, ZR + 0.16, "concrete2", 0.02)
    S.bb("hparW", x0 - 0.1, x0 + 0.06, y0 - 0.2, y1 + 0.1, ZR, ZR + 0.16, "concrete2", 0.02)
    S.bb("hparE", x1 + 0.0, x1 + 0.15, y0 - 0.2, y1 + 0.1, ZR, ZR + 0.16, "concrete2", 0.02)
    # flank panels: bone number board west, magenta house panel east
    D.flank(S, "L", -2.8, TR[0] - 0.4, y0, z1=1.6, stripes=4)
    fx0, fx1 = TR[1] + 0.4, 2.8
    S.bb("flankR", fx0, fx1, y0 - 0.06, y0, 0.5, 1.6, "concrete2", 0.0)
    S.bb("flankbandR", fx0, fx1, y0 - 0.1, y0 - 0.06, 0.55, 0.8, "oxide", 0.0)
    S.bb("hpanel", fx0 + 0.1, fx1 - 0.1, y0 - 0.1, y0 - 0.06, 0.95, 1.5, "house", 0.0)
    D.hood(S, TR[0] - 0.3, TR[1] + 0.3, y0 - 0.9, y0 - 0.1, dmg, z0=1.25, z1=1.6, band_z=(1.28, 1.57), collapse=1.2)
    hatch_leaves(S, i, dmg, lit)
    D.loudspeaker(S, -1.7, 0.3, ZR, dmg, height=1.7)
    D.roof_vents(S, ZR, ((-0.5, 2.3, 0.6),))
    D.chimney(S, STK[0], STK[1], ZR, STK_TOP - (0.5 if dmg else 0.0), dmg, lit, i, rise=1.0, lean_len=1.0)
    D.ash_drift(S, ((2.7, -2.3, 0.8), (-2.9, -2.0, 0.6)))
    if dmg:
        L.R.scorch(S, "scH", 0.0, -2.2, 1.6, 1.0, 0.5)


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.2, 3.0, -3.0, -2.3, 0.14, 0.7, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 1.8, (-1.8, -2.6, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", 1.2, 2.8, -2.5, -2.3, 0.14, 0.3, "rebar", 0.0)
    S.bb("planks", -3.0, -1.8, 2.5, 3.0, 0.14, 0.4, "timber", 0.0)


def _left(S):
    S.bb("planks", -2.9, -1.9, -2.6, -2.2, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 1.8, (1.8, -2.6, 0.3), "steel", axis="X", verts=10)
    L.spark(S, 0.4, -2.0, 1.4, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(1.2, 2.9, ZR + 0.3)[0], y=anchor(1.2, 2.9, ZR + 0.3)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(1.6, YD - 0.6, 0.4)[0], y=anchor(1.6, YD - 0.6, 0.4)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(STK[0], STK[1], STK_TOP - 0.4)[0], y=anchor(STK[0], STK[1], STK_TOP - 0.4)[1], phase=2),
    dict(state="damaged", kind="smoke_plume", x=anchor(1.2, 2.6, ZR + 0.5)[0], y=anchor(1.2, 2.6, ZR + 0.5)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(STK[0], STK[1], STK_TOP - 0.3)[0], y=anchor(STK[0], STK[1], STK_TOP - 0.3)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-3.0, 3.0, -1.4, 3.0), H=2.0, boxes=[(-3.0, 3.0, YD, 3.0)], rings=[(STK[0], STK[1], 0.6)],
    clips=(1.0, 1.9), scaf=[(-3.2, -2.5, -3.1, -2.5), (1.5, 2.5, -3.1, -2.5)],
    derrick=(3.0, -2.8, 4.0, (2.3, -2.0, 4.3)), sparks=[(-2.0, -0.9, 1.9), (1.0, 1.0, 1.9), (-2.0, 2.5, 2.0)],
    hide=D.HIDE_ANIM + ("hpanel",), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
