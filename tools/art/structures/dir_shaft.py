"""Directorate Command Shaft: the conyard (3x3 cells, the biggest Directorate structure).  Original design.

A winding tower (headframe) rises over a deep shaft collar; sheave wheels turn at the top, a cage rides the cable, red embers
glow at the bottom of the grated shaft mouth.  A low hall in front holds the size-M tunnel entrance: a ramp mouth under a small
cantilevered hood (magenta banding), blast doors folded back.  Behind: a winding house with a magenta roof panel, a banded ash
stack, a loudspeaker pair on the hall roof.  Bone / oxide hazard bar along the south edge.
House colour (magenta, remapped in game): hood banding, winding-house roof panel, headframe name-board.
Idle loop: sheaves turn, the cage bobs, ash puffs, furnace slits flicker.  Damage is structural only (D-069): the headframe top has
snapped off (a leg lies on the pad, a sheave wheel lies flat), the hood end has fallen, the stack is snapped, a blast door buckled;
fire / smoke are FX anchors.  Build stages: the shared L.stages() driver (auth_conyard).
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_shaft"
TITLE = "Directorate Command Shaft"
FOOTPRINT = (3, 3)
CANVAS = (192, 288)
ORIGIN = (0, 68)        # headroom for the winding tower, sheave wheels and ash puffs
DOCK = (1, 3)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-5.0, 5.0, -5.0, 5.0)
TR = (-3.8, -0.4)                       # size-M ramp: 3.4 wide = one cell
TR_YTOP, TR_BOT = -4.3, -1.15
HALL = (-5.0, 1.2, -2.2, 0.9)           # x0, x1, y0, y1; the mouth is its south face
HALL_H = 2.3
COL = (1.7, 4.3, -0.6, 2.0)             # shaft collar
COL_H = 1.0
LEG_BASE = ((1.9, -0.4), (4.1, -0.4), (1.9, 1.8), (4.1, 1.8))
LEG_TOP = ((2.5, 0.2), (3.5, 0.2), (2.5, 1.2), (3.5, 1.2))
TOP_Z = 6.0
STACK = (4.2, 3.6)
WIND = (-4.2, 0.6, 2.4, 4.8)            # winding house
WIND_H = 2.0
SHEAVES = ((2.6, 0.7, 6.65), (3.4, 0.7, 6.65))


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def leg_pt(k, z, top=TOP_Z):
    """Point on leg k at height z (legs run from the collar to the sheave platform)."""
    (bx, by), (tx, ty) = LEG_BASE[k], LEG_TOP[k]
    t = (z - COL_H) / (TOP_Z - COL_H)
    return bx + (tx - bx) * t, by + (ty - by) * t


def headframe(S, i, dmg):
    zt = 4.2 if dmg else TOP_Z
    # legs: four members leaning in to the sheave platform; the SE one is broken off in the damaged state
    for k in range(4):
        if dmg and k == 1:
            continue
        x0, y0 = LEG_BASE[k]
        x1, y1 = leg_pt(k, zt)
        L.beam(S, f"leg{k}", (x0, y0, COL_H), (x1, y1, zt), 0.2, "hull2")
    # ties at two heights (front, back, sides), then diagonal braces on the front face and the sides
    ties = (2.5, 4.0) if not dmg else (2.5,)
    for z in ties:
        pts = [leg_pt(k, z) for k in range(4)]
        for a, b in ((0, 1), (2, 3), (0, 2), (1, 3)):
            if dmg and 1 in (a, b):
                continue
            L.beam(S, "tie", (pts[a][0], pts[a][1], z), (pts[b][0], pts[b][1], z), 0.1, "steel")
    za, zb = COL_H, ties[0]
    for (a, b) in ((0, 1), (2, 3)):
        if dmg and 1 in (a, b):
            continue
        pa, pb = leg_pt(a, za), leg_pt(b, zb)
        L.beam(S, "brace", (pa[0], pa[1], za), (pb[0], pb[1], zb), 0.07, "steel")
    if len(ties) > 1:
        pa, pb = leg_pt(0, ties[0]), leg_pt(1, ties[1])
        L.beam(S, "brace", (pa[0], pa[1], ties[0]), (pb[0], pb[1], ties[1]), 0.07, "steel")
        pa, pb = leg_pt(1, ties[0]), leg_pt(0, ties[1])
        L.beam(S, "brace", (pa[0], pa[1], ties[0]), (pb[0], pb[1], ties[1]), 0.07, "steel")
    if dmg:
        # snapped SE leg lies on the pad, a stub stands, the broken platform edge hangs
        L.beam(S, "legbroke", (3.9, -1.4, 0.25), (3.0, -4.0, 0.25), 0.2, "hull2")
        L.beam(S, "legstub", (4.1, -0.4, COL_H), (4.0, -0.2, 2.2), 0.2, "hull2")
        S.bb("platd", 2.4, 3.3, 0.1, 1.3, zt, zt + 0.15, "hull2", 0.0)
        S.cyl("wheelD", 0.42, 0.14, (3.4, -1.3, 0.3), "bone", verts=14)
        S.cyl("wheelDd", 0.35, 0.16, (3.4, -1.3, 0.3), "dark", verts=12)
        for k in range(3):
            S.bb(f"rubc{k}", 1.9 + 0.8 * k, 2.3 + 0.8 * k, -1.8 - 0.2 * k, -1.4 - 0.2 * k, 0.14, 0.4 + 0.1 * k, "concrete_s", 0.0)
        L.beam(S, "cabled", (2.6, 0.7, zt), (2.9, 0.5, 2.2), 0.04, "dark", nocast=True)
        return
    # platform, name-board in house colour, sheave housings and wheels
    S.bb("plat", 2.35, 3.65, 0.05, 1.35, TOP_Z, TOP_Z + 0.18, "hull2", 0.02)
    S.bb("platband", 2.35, 3.65, 0.0, 0.05, TOP_Z - 0.05, TOP_Z + 0.2, "oxide", 0.0)
    S.bb("nameboard", 2.45, 3.55, -0.02, 0.05, TOP_Z - 0.75, TOP_Z - 0.2, "house", 0.0)
    S.bb("pillar", 2.35, 2.5, 0.4, 1.0, TOP_Z + 0.18, TOP_Z + 0.6, "hull2", 0.0)
    S.bb("pillar", 3.5, 3.65, 0.4, 1.0, TOP_Z + 0.18, TOP_Z + 0.6, "hull2", 0.0)
    YS = S.YS
    for n, (wx, wy, wz) in enumerate(SHEAVES):
        S.cyl(f"wheel{n}", 0.42, 0.14, (wx, wy, wz), "bone", axis="Y", verts=16)
        S.cyl(f"wheeld{n}", 0.35, 0.16, (wx, wy, wz), "dark", axis="Y", verts=14)
        for q in range(2):
            ang = i * 22.5 + q * 90 + n * 11
            S.box(f"spoke{n}{q}", (0.74, 0.18, 0.07), (wx, (wy - 0.05) * YS, wz), "steel", 0.0, rot=(0, ang, 0), stretch=False)
        S.cyl(f"hub{n}", 0.1, 0.2, (wx, wy - 0.02, wz), "oxide", axis="Y", verts=10)
    # cables: down from the wheels to the cage, and a long cable running off to the winding house
    bob = (0.0, 0.3, 0.0, -0.3)[i % 4]
    cz = 3.0 + bob
    for (wx, wy, wz) in SHEAVES:
        L.beam(S, "cable", (wx, wy, wz - 0.4), (wx, wy, cz + 0.55), 0.045, "dark", nocast=True)
    S.bb("cage", 2.6, 3.4, 0.4, 1.0, cz - 0.5, cz + 0.5, "oxide", 0.02)
    S.bb("cageroof", 2.55, 3.45, 0.35, 1.05, cz + 0.5, cz + 0.6, "bone", 0.0)
    S.bb("cagedark", 2.7, 3.3, 0.36, 0.4, cz - 0.4, cz + 0.2, "dark", 0.0, nocast=True)
    L.beam(S, "windcable", (2.15, 0.7, 6.55), (0.2, 3.0, WIND_H + 0.5), 0.05, "dark", nocast=True)


def collar(S, i, dmg, mode):
    x0, x1, y0, y1 = COL
    S.bb("collar", x0, x1, y0, y1, 0.14, COL_H, "concrete", 0.05)
    S.bb("collarlip", x0 - 0.15, x1 + 0.15, y0 - 0.15, y1 + 0.15, 0.14, 0.45, "concrete2", 0.04)
    S.bb("collartop", x0, x1, y0, y1, COL_H, COL_H + 0.1, "concrete2", 0.0)
    # dark grated opening with an ember glow far below
    S.bb("shaftmouth", x0 + 0.35, x1 - 0.35, y0 + 0.35, y1 - 0.35, COL_H + 0.05, COL_H + 0.14, "dark", 0.0)
    lit = (mode != "build") and not dmg
    g = "redglow" if lit and (i % 2 == 0 or mode == "build") else ("oxide" if lit else "oxide2")
    S.bb("glowc", x0 + 1.0, x1 - 1.0, y0 + 1.1, y1 - 0.75, COL_H + 0.14, COL_H + 0.17, g, 0.0, nocast=True)
    for k in range(6):
        xx = x0 + 0.5 + k * ((x1 - x0 - 1.0) / 5)
        S.bb(f"grate{k}", xx - 0.03, xx + 0.03, y0 + 0.3, y1 - 0.3, COL_H + 0.17, COL_H + 0.22, "steel", 0.0, nocast=True)
    # bone / oxide hazard stripes down the collar's south face
    n = 9
    sw = (x1 - x0 + 0.3) / n
    for k in range(n):
        S.bb(f"chaz{k}", x0 - 0.15 + k * sw, x0 - 0.15 + (k + 1) * sw, y0 - 0.2, y0 - 0.15, 0.14, 0.45, "oxide" if k % 2 == 0 else "bone", 0.0)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    hx0, hx1, hy0, hy1 = HALL
    ydoor = hy0 + 0.02
    # ------------------------------------------------------------------ pad, ramp entrance, hazard bar
    D.pad_with_trench(S, PAD, TR, TR_YTOP, ydoor)
    D.hazard_bar(S, -5.0, 5.0, -4.9, -4.55, 0.6)
    D.ramp_entrance(S, TR, TR_YTOP, ydoor, TR_BOT, dmg)
    # ------------------------------------------------------------------ hall
    S.bb("hall", hx0, hx1, hy0, hy1, 0.14, HALL_H, "concrete", 0.05)
    S.bb("hplinth", hx0 - 0.1, hx1 + 0.1, hy0 - 0.1, hy1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("hroof", hx0 - 0.1, hx1 + 0.2, hy0 - 0.2, hy1 + 0.2, HALL_H, HALL_H + 0.22, "roof", 0.05)
    zr = HALL_H + 0.22
    S.bb("hparW", hx0 - 0.1, hx0 + 0.06, hy0 - 0.2, hy1 + 0.2, zr, zr + 0.18, "concrete2", 0.02)
    S.bb("hparE", hx1 + 0.06, hx1 + 0.2, hy0 - 0.2, hy1 + 0.2, zr, zr + 0.18, "concrete2", 0.02)
    S.bb("hparN", hx0 - 0.1, hx1 + 0.2, hy1 + 0.06, hy1 + 0.2, zr, zr + 0.18, "concrete2", 0.02)
    xhi = -1.0 if dmg else 0.2
    S.bb("roofhouse", -3.4 if not dmg else -3.0, xhi, -1.5, 0.3, zr, zr + 0.04, "house2", 0.0)
    S.bb("roofframe", -3.5 if not dmg else -3.1, xhi + 0.1, -1.6, 0.4, zr, zr + 0.02, "dark", 0.0)
    D.flank(S, "L", hx0 + 0.1, TR[0] - 0.3, hy0)
    D.flank(S, "R", TR[1] + 0.3, hx1 - 0.05, hy0)
    D.roof_vents(S, zr, ((-4.4, 0.3, 0.9), (-4.4, -1.0, 0.7)))
    D.hood(S, TR[0] - 0.3, TR[1] + 0.3, ydoor - 1.1, ydoor - 0.1, dmg, collapse=1.9)
    D.loudspeaker(S, -2.4 if not dmg else -2.4, -1.9, zr, dmg)
    # ------------------------------------------------------------------ shaft collar + winding tower
    collar(S, i, dmg, mode)
    headframe(S, i, dmg)
    # ------------------------------------------------------------------ winding house (north), ash stack (north-east)
    wx0, wx1, wy0, wy1 = WIND
    S.bb("wind", wx0, wx1, wy0, wy1, 0.14, WIND_H, "concrete2", 0.04)
    S.bb("windroof", wx0 - 0.15, wx1 + 0.15, wy0 - 0.15, wy1 + 0.15, WIND_H, WIND_H + 0.2, "roof", 0.04)
    S.bb("windband", wx0, wx1, wy0 - 0.04, wy0, 1.1, 1.4, "oxide", 0.0)
    S.bb("windplate", wx0 + 0.3, wx0 + 2.0, wy0 - 0.08, wy0 - 0.04, 0.4, 1.0, "bone", 0.0)
    for k in range(2):
        S.bb(f"sten_w{k}", wx0 + 0.4, wx0 + 1.9, wy0 - 0.1, wy0 - 0.08, 0.5 + k * 0.25, 0.62 + k * 0.25, "dark", 0.0, nocast=True)
    S.bb("windhouse", wx0 + 1.3, wx1 - 1.3, wy0 + 0.7, wy1 - 0.7, WIND_H + 0.2, WIND_H + 0.24, "house2", 0.0)
    D.furnace_slits(S, wx0 + 2.6, wy0 - 0.1, 0.35, 0.8, 3, i, (idle or mode == "build") and not dmg, step=0.6, w=0.3)
    sx, sy = STACK
    S.bb("furn", sx - 0.9, sx + 0.8, sy - 0.8, sy + 0.9, 0.14, 1.3, "concrete2", 0.04)
    S.bb("furnroof", sx - 1.0, sx + 0.9, sy - 0.9, sy + 1.0, 1.3, 1.45, "roof", 0.04)
    S.bb("furnband", sx - 0.9, sx + 0.8, sy - 0.84, sy - 0.8, 0.9, 1.1, "oxide", 0.0)
    D.chimney(S, sx, sy, 1.45, 3.1 if dmg else 4.2, dmg, idle or mode == "build", i, rise=1.5, lean_len=1.4)
    D.ash_drift(S, ((3.8, -2.6, 1.3), (-4.4, 1.7, 0.8), (4.2, 0.6, 0.8)))
    if dmg:
        L.R.scorch(S, "scH", -2.0, -2.3, 2.1, 1.6, 0.5)
        L.R.scorch(S, "scC", 3.0, -0.65, 0.9, 1.6, 0.6)


# ---------------------------------------------------------------------------------------------- build stages / damage FX
def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 3.4, 4.2, -4.4, -3.6, 0.14, 0.8, "crate", 0.03)
        S.bb("crate", 3.55, 4.1, -4.3, -3.7, 0.8, 1.3, "crate", 0.03, rot=(0, 0, 12))
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (1.0, -3.9, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", 0.6, 2.2, -3.2, -3.0, 0.14, 0.3, "rebar", 0.0)
        S.cyl("barrel", 0.3, 0.7, (4.3, -2.6, 0.5), "rust", verts=12)
    S.bb("planks", -4.6, -3.2, 3.8, 4.4, 0.14, 0.5, "timber", 0.0)


def _left(S):
    S.bb("planks", -4.5, -4.2, -4.4, -3.4, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 2.0, (2.6, -4.0, 0.3), "steel", axis="X", verts=10)
    S.bb("crate", 3.5, 4.3, -4.5, -3.8, 0.14, 0.8, "crate", 0.03)
    L.spark(S, 0.4, -3.4, 1.4, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(3.0, 0.7, 4.0)[0], y=anchor(3.0, 0.7, 4.0)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(-1.2, -3.0, 0.5)[0], y=anchor(-1.2, -3.0, 0.5)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(-1.9, -1.8, 0.3)[0], y=anchor(-1.9, -1.8, 0.3)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=anchor(4.2, 3.6, 3.3)[0], y=anchor(4.2, 3.6, 3.3)[1], phase=6),
    dict(state="damaged", kind="smoke_plume", x=anchor(3.0, 0.7, 4.3)[0], y=anchor(3.0, 0.7, 4.3)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(4.2, 3.6, 3.4)[0], y=anchor(4.2, 3.6, 3.4)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-4.8, 4.8, -4.2, 4.6), H=TOP_Z,
    boxes=[(1.9, 4.1, -0.4, 1.8)], rings=[(STACK[0], STACK[1], 0.7)],
    clips=(2.5, 4.4), scaf=[(-5.0, -4.2, -4.7, -4.1), (0.6, 1.6, -4.7, -4.1)],
    derrick=(4.6, -3.2, 5.0, (3.6, -2.4, 5.2)), sparks=[(-2.0, -1.0, 2.3), (2.6, 0.6, 3.0), (-1.0, 3.5, 2.0)],
    hide=D.HIDE_ANIM + ("wheel", "spoke", "hub", "cage", "cable", "windcable", "nameboard"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
