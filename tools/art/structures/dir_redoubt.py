"""Directorate Redoubt: half-buried garrison strongpoint, no gun of its own (2x2 cells).  Original design.

The read is a LOW EARTH MOUND with a concrete-and-shuttering front: a timber-shuttered south face with a row of FIVE narrow firing
slits at ground level (one per citizen, ember-dark inside), a magenta house band across the top of the face, a hooded stair mouth
at the west end, an earth lip piled in front of the slits and the mound heaped over the back of the roof with a small hatch, a
periscope vent and a leaning flag pole.  Bone / oxide hazard bar on the pad edge.
Idle loop: slit embers flicker in turn, the periscope vent breathes, the flag flaps.  Damage is structural only (D-069): the east
end of the roof slab has slumped, shuttering is split, the earth cap has slid, the hood end has fallen; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint x, y in [-3.4, 3.4].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_redoubt"
TITLE = "Directorate Redoubt"
FOOTPRINT = (2, 2)
CANVAS = (128, 192)
ORIGIN = (0, 48)
DOCK = (0, 2)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-3.3, 3.3, -3.3, 3.3)
TR = (-2.7, -1.5)
TR_YTOP, TR_BOT = -3.0, -0.4
YF = -1.7               # south face
FACE = (-1.2, 3.0)      # x range of the shuttered face
FACE_H = 1.4
ROOF_Z = FACE_H + 0.22
SLITS = (-0.6, 0.2, 1.0, 1.8, 2.6)


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    lit = (idle or mode == "build") and not dmg
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    D.pad_with_trench(S, PAD, TR, TR_YTOP, YF + 0.02)
    D.hazard_bar(S, -3.3, 3.3, -3.25, -2.95, 0.6)
    D.ramp_entrance(S, TR, TR_YTOP, YF + 0.02, TR_BOT, dmg, mouth_top=1.2, nst=5)
    x0, x1 = FACE
    # shuttered concrete face: timber planking bands over a concrete core
    S.bb("core", -3.0, 3.1, YF, 2.9, 0.14, FACE_H, "concrete", 0.05)
    S.bb("plinth", x0, x1 + 0.1, YF - 0.1, YF, 0.14, 0.45, "concrete2", 0.02)
    for k in range(3):
        zz = 0.5 + 0.3 * k
        S.bb(f"plank{k}", x0, x1, YF - 0.08, YF - 0.04, zz, zz + 0.2, "timber", 0.0, nocast=True)
    for k, sx in enumerate(SLITS):
        S.bb(f"slit{k}", sx - 0.28, sx + 0.28, YF - 0.12, YF - 0.04, 0.7, 1.05, "dark", 0.0, nocast=True)
        g = "redglow" if (lit and (k + i) % 3 != 0) else "oxide2"
        S.bb(f"glow{k}", sx - 0.2, sx + 0.2, YF - 0.13, YF - 0.12, 0.74, 0.92, g, 0.0, nocast=True)
    # west flank panel beside the hood: bone number board
    D.flank(S, "L", -3.0, TR[0] - 0.4, YF, z1=1.2, stripes=3)
    # roof slab, house band over the face, hood over the stair mouth
    xe = 1.4 if dmg else 3.1
    S.bb("roof", -3.0, xe, YF - 0.25, 2.9, FACE_H, ROOF_Z, "roof", 0.05)
    S.bb("band", x0 + 0.1, min(x1 - 0.1, xe - 0.1), YF - 0.3, YF - 0.25, FACE_H + 0.02, ROOF_Z - 0.02, "house", 0.0)
    D.hood(S, TR[0] - 0.3, TR[1] + 0.3, YF - 0.9, YF - 0.1, dmg, z0=1.2, z1=1.5, band_z=(1.23, 1.47), collapse=0.9)
    # earth: lip in front of the slits (gaps), mound heaped over the back of the roof
    for k, (a, b) in enumerate(((-1.0, -0.2), (0.4, 1.4), (2.0, 3.0))):
        S.bb(f"lip{k}", a, b, YF - 0.65, YF - 0.3, 0.14, 0.4, "earth", 0.1)
    S.bb("mound0", -3.1, 3.2, 0.4, 3.1, ROOF_Z, ROOF_Z + 0.5, "earth", 0.12)
    S.bb("mound1", -2.5, 2.6, 1.2, 3.1, ROOF_Z + 0.5, ROOF_Z + 0.95, "earth2", 0.12)
    for k, yy in enumerate((0.9, 1.7, 2.5)):
        S.bb(f"furrow{k}", -2.8, 2.9, yy, yy + 0.18, ROOF_Z + 0.5, ROOF_Z + 0.53, "earth2", 0.0, nocast=True)
    for k, (rx, ry) in enumerate(((-2.4, 0.7), (2.2, 1.0), (1.6, 2.6))):
        S.box(f"stone{k}", (0.4, 0.3, 0.25), (rx, ry * S.YS, ROOF_Z + 0.62), "concrete2", 0.0, rot=(0, 0, 20 * k), stretch=False)
    for sgn in (-1, 1):
        xa, xb = (3.0, 3.35) if sgn > 0 else (-3.35, -3.0)
        S.bb("flankearth", xa, xb, YF + 0.2, 3.0, 0.14, ROOF_Z - 0.2, "earth", 0.1)
    # roof hardware: hatch, periscope vent, flag
    S.bb("hatchf", -2.0, -0.9, 0.9, 1.9, ROOF_Z + 0.5, ROOF_Z + 0.62, "concrete2", 0.02)
    S.bb("hatch", -1.9, -1.0, 1.0, 1.8, ROOF_Z + 0.62, ROOF_Z + 0.7, "hull2", 0.0)
    S.cyl("peri", 0.14, 0.9, (0.6, 1.8, ROOF_Z + 0.95 + 0.45), "steel", verts=8)
    S.bb("perihd", 0.4, 0.8, 1.6, 2.0, ROOF_Z + 1.9, ROOF_Z + 2.15, "hull2", 0.02)
    S.bb("perilens", 0.45, 0.75, 1.55, 1.6, ROOF_Z + 1.95, ROOF_Z + 2.1, "redglow" if lit else "oxide2", 0.0, nocast=True)
    if lit:
        ph = (i / 4) % 1
        S.sphere("vpuff", 0.1 + 0.2 * math.sin(math.pi * ph), (0.6 + ph * 0.6, 1.8, ROOF_Z + 2.4 + ph * 0.7), "smoke2")
    px_, py_ = 2.7, 2.2
    S.cyl("pole", 0.06, 2.3, (px_, py_, ROOF_Z + 0.95 + 1.15), "steel", verts=6)
    if dmg:
        S.box("flagd", (0.8, 0.05, 0.5), (px_ + 0.35, (py_ - 0.1) * S.YS, ROOF_Z + 2.6), "house", 0.0, rot=(0, 0, 60), stretch=False)
    else:
        sw = 0.15 * math.sin(i * math.pi / 2)
        S.box("flag", (1.0 + sw, 0.05, 0.7), (px_ + 0.55 + sw / 2, (py_ - 0.1) * S.YS, ROOF_Z + 2.85), "house", 0.0, rot=(0, 0, 4 * math.sin(i * math.pi / 2)), stretch=False)
    D.ash_drift(S, ((2.7, -2.4, 0.8), (-0.4, -2.6, 0.6)))
    if dmg:
        S.box("slab", (1.7, 1.0, 0.3), (2.3, 0.2 * S.YS, ROOF_Z - 0.1), "roof_s", 0.02, rot=(0, 14, 16), stretch=False)
        S.bb("split", 1.4, 1.9, YF - 0.09, YF - 0.05, 0.3, 1.3, "char", 0.0, nocast=True)
        for k, (x, y, w, h) in enumerate(((1.8, -1.4, 0.4, 0.3), (2.9, -1.1, 0.35, 0.26), (1.4, -1.0, 0.3, 0.25))):
            S.box(f"rub{k}", (w, w * 0.8, h), (x, y * S.YS, 0.14 + h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k), stretch=False)
        for k, x in enumerate((1.3, 1.6)):
            L.beam(S, f"rbar{k}", (x, 0.6, ROOF_Z), (x + 0.2, 0.4, ROOF_Z + 0.6 + 0.2 * k), 0.05, "rebar")
        L.R.scorch(S, "scR", 2.0, YF - 0.1, 0.6, 1.6, 0.6)


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.2, 3.0, -3.0, -2.3, 0.14, 0.7, "crate", 0.03)
    if i <= 2:
        S.bb("rebarpile", 0.6, 2.4, -2.7, -2.5, 0.14, 0.3, "rebar", 0.0)
    S.bb("planks", -3.0, -1.8, 2.5, 3.0, 0.14, 0.4, "timber", 0.0)


def _left(S):
    S.bb("planks", 0.6, 1.8, -2.7, -2.4, 0.14, 0.3, "timber", 0.0)
    L.spark(S, 0.4, -2.0, 1.2, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(2.2, 0.4, ROOF_Z + 0.2)[0], y=anchor(2.2, 0.4, ROOF_Z + 0.2)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(1.6, YF - 0.3, 0.7)[0], y=anchor(1.6, YF - 0.3, 0.7)[1], phase=4),
    dict(state="damaged", kind="smoke_plume", x=anchor(2.0, 0.4, ROOF_Z + 0.4)[0], y=anchor(2.0, 0.4, ROOF_Z + 0.4)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(0.6, 1.8, ROOF_Z + 2.0)[0], y=anchor(0.6, 1.8, ROOF_Z + 2.0)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-3.0, 3.0, -1.7, 2.9), H=1.8, boxes=[(-3.0, 3.0, YF, 2.9)], rings=[(0.6, 1.8, 0.4)],
    clips=(0.9, 1.7), scaf=[(-3.2, -2.5, -3.1, -2.5), (1.5, 2.5, -3.1, -2.5)],
    derrick=(3.0, -2.8, 3.6, (2.3, -2.0, 3.9)), sparks=[(-2.0, -0.9, 1.7), (1.0, 1.0, 1.7), (-2.0, 2.5, 1.8)],
    hide=D.HIDE_ANIM + ("band", "flag", "perilens"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
