"""Directorate Bunk Gallery: supply building, tiers of cots behind blast doors (2x2 cells).  Original design.

The read is a STEPPED, EARTH-BANKED CONCRETE TERRACE rising to the north: three tiers, each with its own row of blast doors (dark
recess, oxide door band) and a row of small lamp slits above them that are never fully out, a magenta house band across the front of
every tier's roof slab, earth banked against both flanks.  On the top tier a loudspeaker pair, two roof vents and a short ash stack.
Bone / oxide hazard bar along the south edge of the pad, a bone stencil board at the foot of the lowest tier.
Idle loop: lamp slits flicker in turn, the stack breathes ash.  Damage is structural only (D-069): the top slab's east end has
fallen, one door is buckled, the stack is snapped; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint x, y in [-3.4, 3.4].
"""
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_bunk_gallery"
TITLE = "Directorate Bunk Gallery"
FOOTPRINT = (2, 2)
CANVAS = (128, 208)
ORIGIN = (0, 64)
DOCK = (1, 2)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-3.3, 3.3, -3.3, 3.3)
TIERS = ((-2.4, -1.2, 1.1), (-1.2, 0.4, 2.0), (0.4, 3.0, 2.9))     # y0, y1, roof height
DOORS = (-1.9, 0.0, 1.9)
STK = (-2.3, 2.3)
STK_TOP = 3.7
TOPZ = TIERS[2][2] + 0.18


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    lit = (idle or mode == "build") and not dmg
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    S.bb("pad", PAD[0], PAD[1], PAD[2], PAD[3], 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", PAD[0], PAD[1], PAD[2], PAD[2] + 0.1, 0.14, 0.2, "concrete2", 0.0)
    D.hazard_bar(S, -3.3, 3.3, -3.25, -2.95, 0.6)
    # earth banked against the flanks
    for sgn in (-1, 1):
        xa, xb = (2.9, 3.3) if sgn > 0 else (-3.3, -2.9)
        S.bb("berm", xa, xb, -1.2, 3.0, 0.14, 1.0, "earth", 0.08)
        S.bb("berm", xa + 0.1 * (-sgn), xb + 0.1 * (-sgn), 0.4, 3.0, 1.0, 1.6, "earth2", 0.08)
    for t, (ya, yb, h) in enumerate(TIERS):
        ztop = h
        S.bb(f"tier{t}", -2.9, 2.9, ya, yb, 0.14, ztop, "concrete", 0.04)
        broken = dmg and t == 2
        xe = 0.9 if broken else 3.0
        S.bb(f"tcap{t}", -3.0, xe, ya - 0.15, yb + 0.05, ztop, ztop + 0.18, "roof", 0.04)
        S.bb(f"tband{t}", -2.8, min(xe - 0.1, 2.8), ya - 0.2, ya - 0.15, ztop + 0.02, ztop + 0.16, "house", 0.0)
        S.bb(f"tsill{t}", -2.9, 2.9, ya - 0.08, ya, 0.14, 0.34, "concrete2", 0.0)
        for k, x in enumerate(DOORS):
            if t == 0 and k == 1:
                continue            # the stencil board stands here
            dh = 0.62 if t == 0 else 0.7
            S.bb(f"mouth{t}{k}", x - 0.5, x + 0.5, ya - 0.06, ya, 0.34, 0.34 + dh, "dark", 0.0)
            S.bb(f"dband{t}{k}", x - 0.55, x + 0.55, ya - 0.1, ya - 0.06, 0.34 + dh, 0.34 + dh + 0.12, "oxide", 0.0)
            if dmg and t == 1 and k == 2:
                S.box("doorbuck", (0.5, 0.1, 0.55), (x + 0.1, (ya - 0.2) * S.YS, 0.65), "door", 0.0, rot=(0, 0, 24), stretch=False)
            else:
                S.bb(f"door{t}{k}", x - 0.42, x + 0.42, ya - 0.1, ya - 0.06, 0.38, 0.34 + dh - 0.04, "door", 0.0)
            D.furnace_slits(S, x - 0.35, ya - 0.1, 0.34 + dh + 0.2, 0.34 + dh + 0.4, 3, i + t + k, lit, step=0.28, w=0.18)
    # stencil board at the foot of the lowest tier
    ya0 = TIERS[0][0]
    S.bb("sboard", -0.55, 0.55, ya0 - 0.1, ya0 - 0.04, 0.3, 0.95, "bone", 0.0)
    for k in range(3):
        S.bb(f"sten{k}", -0.4, 0.4, ya0 - 0.12, ya0 - 0.1, 0.4 + 0.18 * k, 0.5 + 0.18 * k, "dark", 0.0, nocast=True)
    D.loudspeaker(S, 1.8, 1.4, TOPZ, dmg, height=1.5)
    D.roof_vents(S, TOPZ, ((-0.6, 2.3, 0.5), (0.4, 1.0, 0.45)))
    D.chimney(S, STK[0], STK[1], TOPZ - 0.1, STK_TOP - (0.5 if dmg else 0.0), dmg, lit, i, rise=1.0, lean_len=1.0)
    D.ash_drift(S, ((2.6, -2.2, 0.8), (-2.9, -2.0, 0.6)))
    if dmg:
        S.box("slabfall", (1.6, 0.9, 0.3), (2.2, 0.0, 0.3), "roof_s", 0.02, rot=(0, 0, 18), stretch=False)
        for k, (x, y, w, h) in enumerate(((1.4, -0.7, 0.4, 0.3), (2.7, -0.4, 0.35, 0.26), (1.9, 0.5, 0.3, 0.22))):
            S.box(f"rub{k}", (w, w * 0.8, h), (x, y * S.YS, 0.14 + h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k), stretch=False)
        for k, x in enumerate((1.0, 1.3)):
            L.beam(S, f"rbar{k}", (x, 1.4, TOPZ), (x + 0.2, 1.2, TOPZ + 0.6 + 0.2 * k), 0.05, "rebar")
        L.R.scorch(S, "scG", 1.5, TIERS[1][0] - 0.08, 1.5, 1.2, 0.8)


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.2, 3.0, -3.0, -2.3, 0.14, 0.7, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 1.8, (-1.8, -2.8, z), "steel", axis="X", verts=10)
    S.bb("planks", -3.0, -1.8, 2.5, 3.0, 0.14, 0.4, "timber", 0.0)


def _left(S):
    S.bb("planks", -2.9, -1.9, -2.8, -2.5, 0.14, 0.3, "timber", 0.0)
    L.spark(S, 0.4, -2.0, 1.4, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(1.5, 1.2, TOPZ + 0.3)[0], y=anchor(1.5, 1.2, TOPZ + 0.3)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(1.9, -1.4, 0.9)[0], y=anchor(1.9, -1.4, 0.9)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(STK[0], STK[1], STK_TOP - 0.4)[0], y=anchor(STK[0], STK[1], STK_TOP - 0.4)[1], phase=2),
    dict(state="damaged", kind="smoke_plume", x=anchor(1.5, 1.0, TOPZ + 0.5)[0], y=anchor(1.5, 1.0, TOPZ + 0.5)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(STK[0], STK[1], STK_TOP - 0.3)[0], y=anchor(STK[0], STK[1], STK_TOP - 0.3)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-3.0, 3.0, -2.4, 3.0), H=2.0, boxes=[(-3.0, 3.0, -2.4, 3.0)], rings=[(STK[0], STK[1], 0.6)],
    clips=(1.0, 1.9), scaf=[(-3.2, -2.5, -3.1, -2.5), (1.5, 2.5, -3.1, -2.5)],
    derrick=(3.0, -2.8, 4.0, (2.3, -2.0, 4.3)), sparks=[(-2.0, -0.9, 1.9), (1.0, 1.0, 1.9), (-2.0, 2.5, 2.0)],
    hide=D.HIDE_ANIM + ("tband",), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
