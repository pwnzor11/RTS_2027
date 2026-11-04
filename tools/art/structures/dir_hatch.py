"""Directorate Hatch: small tunnel entrance (1x1 cell, size-S entrance on the south face).  Original design.

A squat reinforced umber concrete collar block with a narrow stair mouth under a small magenta-banded hood, a bone stencil flank and a
magenta house panel, and on the roof a four-post LIFT CAGE with a hanging oxide car and a short vent pipe.  Hazard bar on the south edge.
This module also holds the shared BASE (pad, collar block, hazard bar, hood, flanks, house panel) that dir_bore_head reuses with a
taller collar, a wider mouth and a bigger head, so the Bore Head reads as the upgrade of the Hatch.
Idle loop: the cage car bobs, ember glow flickers, vent puffs.  Damage is structural only (D-069): hood end fallen, cage bent.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint x, y in [-1.7, 1.7].
"""
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_hatch"
TITLE = "Directorate Hatch"
FOOTPRINT = (1, 1)
CANVAS = (64, 112)
ORIGIN = (0, 40)
DOCK = (0, 0)
CAP_MATERIAL = D.CAP_MATERIAL
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-1.6, 1.6, -1.6, 1.6)
BLOCK = (-1.3, 1.3, -0.25, 1.3)
YD = -0.25


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def base(S, dmg, H, tr, ytop, bot, nst, mouth_top, hood_z, flank_w):
    """Shared pad + collar block + entrance of Hatch and Bore Head.  Returns the roof height."""
    x0, x1, y0, y1 = BLOCK
    zr = H + 0.15
    D.pad_with_trench(S, PAD, tr, ytop, YD + 0.02)
    D.hazard_bar(S, -1.6, 1.6, -1.55, -1.3, 0.4)
    D.ramp_entrance(S, tr, ytop, YD + 0.02, bot, dmg, mouth_top=mouth_top, nst=nst, doors=False)
    S.bb("block", x0, x1, y0, y1, 0.14, H, "concrete", 0.04)
    S.bb("plinth", x0 - 0.08, x1 + 0.08, y0 - 0.08, y1 + 0.08, 0.14, 0.45, "concrete2", 0.03)
    S.bb("roof", x0 - 0.08, x1 + 0.1, y0 - 0.15, y1 + 0.08, H, zr, "roof", 0.04)
    S.bb("parS", x0 - 0.08, x1 + 0.1, y0 - 0.15, y0 - 0.04, zr, zr + 0.14, "concrete2", 0.02)
    S.bb("parN", x0 - 0.08, x1 + 0.1, y1, y1 + 0.08, zr, zr + 0.14, "concrete2", 0.02)
    # flanks: bone stencil west, magenta house panel east (when there is room beside the cheeks)
    if flank_w > 0.3:
        fa, fb = tr[0] - 0.3 - flank_w, tr[0] - 0.3
        D.flank(S, "L", fa, fb, y0, z0=0.45, z1=hood_z - 0.05, stripes=2)
        ga, gb = tr[1] + 0.3, tr[1] + 0.3 + flank_w
        S.bb("flankR", ga, gb, y0 - 0.06, y0, 0.45, hood_z - 0.05, "concrete2", 0.0)
        S.bb("hpanel", ga + 0.05, gb - 0.05, y0 - 0.1, y0 - 0.06, 0.55, hood_z - 0.15, "house", 0.0)
        S.bb("hpaneltrim", ga + 0.05, gb - 0.05, y0 - 0.12, y0 - 0.1, 0.55, 0.68, "bone", 0.0)
    D.hood(S, tr[0] - 0.2, tr[1] + 0.2, y0 - 0.55, y0 - 0.08, dmg, z0=hood_z, z1=hood_z + 0.25, band_z=(hood_z + 0.02, hood_z + 0.23), collapse=0.5)
    return zr


def cage(S, i, dmg, mode, zr):
    pts = ((-0.5, 0.3), (0.5, 0.3), (-0.5, 1.1), (0.5, 1.1))
    top = zr + (0.55 if dmg else 1.15)
    for k, (px, py) in enumerate(pts):
        if dmg and k == 1:
            L.beam(S, "postb", (px, py, zr), (px + 0.5, py - 0.4, zr + 0.3), 0.07, "steel")
            continue
        S.bb(f"post{k}", px - 0.04, px + 0.04, py - 0.04, py + 0.04, zr, top, "steel", 0.0)
    for nm, (ax, bx, ay, by) in (("S", (-0.55, 0.55, 0.22, 0.3)), ("N", (-0.55, 0.55, 1.1, 1.18)), ("W", (-0.55, -0.47, 0.22, 1.18)), ("E", (0.47, 0.55, 0.22, 1.18))):
        S.bb("cagebar" + nm, ax, bx, ay, by, top, top + 0.07, "oxide" if nm == "S" else "hull2", 0.0)
    if not dmg:
        bob = (0.0, 0.1, 0.0, -0.1)[i % 4] if mode != "build" else 0.0
        S.bb("car", -0.38, 0.38, 0.45, 0.95, zr + 0.2 + bob, zr + 0.75 + bob, "oxide", 0.02)
        S.bb("cardark", -0.3, 0.3, 0.42, 0.45, zr + 0.3 + bob, zr + 0.65 + bob, "dark", 0.0, nocast=True)
    S.cyl("vent", 0.13, 0.55, (0.95, 0.9, zr + 0.28), "hull2", verts=10)
    S.cyl("venth", 0.2, 0.08, (0.95, 0.9, zr + 0.58), "steel", verts=10)


def finished(S, i, dmg, mode):
    lit = mode in ("idle", "build") and not dmg
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    zr = base(S, dmg, 1.0, (-0.5, 0.5), -1.1, -0.2, 3, 0.85, 0.85, 0.4)
    cage(S, i, dmg, mode, zr)
    S.bb("glowfloor2", -0.3, 0.3, YD - 0.16, YD - 0.1, -0.12, -0.05, "redglow" if lit and i % 2 == 0 else "oxide2", 0.0, nocast=True)
    if dmg:
        L.R.scorch(S, "scH", 0.0, -1.0, 1.0, 0.7, 0.5)


def _clutter(S, i):
    S.bb("crate", 0.9, 1.4, -1.4, -0.9, 0.14, 0.5, "crate", 0.03)
    if i <= 2:
        S.bb("rebarpile", -1.4, -0.7, -1.2, -1.1, 0.14, 0.25, "rebar", 0.0)


def _left(S):
    S.bb("planks", -1.4, -0.8, -1.2, -0.9, 0.14, 0.25, "timber", 0.0)
    L.spark(S, 0.0, -0.8, 1.0, 3, 6)


FX = [
    dict(state="damaged", kind="fire_small", x=anchor(0.0, 0.7, 1.4)[0], y=anchor(0.0, 0.7, 1.4)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(0.0, 0.7, 1.5)[0], y=anchor(0.0, 0.7, 1.5)[1], phase=3),
]

SPEC = dict(
    pad=PAD, env=(-1.3, 1.3, -0.25, 1.3), H=1.2, boxes=[(-1.3, 1.3, -0.25, 1.3)], rings=[],
    clips=(0.6, 1.1), scaf=[(-1.5, -1.0, -1.5, -1.0), (0.9, 1.4, -1.5, -1.0)],
    derrick=(1.4, -1.2, 2.0, (1.0, -0.9, 2.2)), sparks=[(-0.8, 0.0, 1.0), (0.5, 0.8, 1.0), (0.0, 1.0, 1.0)],
    hide=D.HIDE_ANIM + ("hpanel", "car"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
