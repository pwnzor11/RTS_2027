"""Directorate Command Shaft: the conyard (3x3 cells, the biggest Directorate structure).  Original design.  Revision 2.

The read is a PITHEAD: a tall A-frame winding tower dead centre over a deep shaft collar, two big sheave wheels at the top and a
vertical magenta banner hanging down its front.  Two low wings flank it: a west hall that holds the size-M tunnel entrance (ramp
mouth under a small hood with a magenta band, blast doors folded back, ember glow at the bottom) and a small winding house to the
north.  An ash stack on the east side, a loudspeaker pair on the hall roof, bone / oxide hazard bar along the south edge.
House colour (magenta, remapped in game): the hanging banner and the hood band.
Idle loop: sheaves turn, the cage bobs, ash puffs, furnace slits flicker.  Damage is structural only (D-069): the tower top has
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
CANVAS = (192, 312)
ORIGIN = (0, 96)        # headroom for the tall winding tower, big sheave wheels and ash puffs
DOCK = (1, 3)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-5.0, 5.0, -5.0, 5.0)
TR = (-4.1, -1.0)                       # size-M ramp mouth, 3.1 wide
TR_YTOP, TR_BOT = -4.3, -1.15
FRONT = (-5.0, -0.4, -2.2, -0.9)        # low front block holding the mouth (x0, x1, y0, y1)
WING = (-5.0, -2.2, -0.9, 2.8)          # west wing behind it
HALL_H = 1.9
CX, CY = 0.3, 1.2                       # tower centre
COL = (-1.3, 1.9, -0.3, 2.7)            # shaft collar
COL_H = 1.0
LEG_BASE = ((-1.6, -0.6), (2.2, -0.6), (-1.6, 2.4), (2.2, 2.4))
LEG_TOP = ((CX - 0.65, CY - 0.5), (CX + 0.65, CY - 0.5), (CX - 0.65, CY + 0.5), (CX + 0.65, CY + 0.5))
TOP_Z = 7.4
LEVELS = (COL_H, 3.4, 5.6, TOP_Z)
SHEAVES = ((CX - 0.75, CY, 8.15), (CX + 0.75, CY, 8.15))
SHEAVE_R = 0.7
STACK = (3.7, 2.6)
WIND = (-1.8, 2.4, 3.3, 4.8)            # winding house
WIND_H = 1.8


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def leg_pt(k, z):
    """(x, y) on leg k at height z; legs run from the collar corners to the sheave platform."""
    (bx, by), (tx, ty) = LEG_BASE[k], LEG_TOP[k]
    t = (z - COL_H) / (TOP_Z - COL_H)
    return bx + (tx - bx) * t, by + (ty - by) * t


def headframe(S, i, dmg):
    zt = 5.2 if dmg else TOP_Z
    lv = [z for z in LEVELS if z <= zt + 0.01]
    # four strong legs leaning in to the sheave platform (the SE one is broken off in the damaged state)
    for k in range(4):
        if dmg and k == 1:
            continue
        x0, y0 = LEG_BASE[k]
        x1, y1 = leg_pt(k, zt)
        L.beam(S, f"leg{k}", (x0, y0, COL_H), (x1, y1, zt), 0.32, "hull2")
    # horizontal ties at each level, X braces in every panel on the front and both sides
    for z in lv[1:]:
        pts = [leg_pt(k, z) for k in range(4)]
        for a, b in ((0, 1), (2, 3), (0, 2), (1, 3)):
            if dmg and 1 in (a, b):
                continue
            L.beam(S, "tie", (pts[a][0], pts[a][1], z), (pts[b][0], pts[b][1], z), 0.14, "steel")
    for za, zb in zip(lv, lv[1:]):
        for a, b in ((0, 1), (0, 2), (1, 3)):
            if dmg and 1 in (a, b):
                continue
            for (p, q) in ((a, b), (b, a)):
                pa, pb = leg_pt(p, za), leg_pt(q, zb)
                L.beam(S, "brace", (pa[0], pa[1], za), (pb[0], pb[1], zb), 0.08, "steel")
    # bone / oxide hazard bands on the legs (sparse, so the lattice still reads)
    if not dmg:
        for k in (0, 1):
            for zz, m in ((2.2, "bone"), (4.5, "oxide"), (6.6, "bone")):
                x, y = leg_pt(k, zz)
                S.bb("legband", x - 0.19, x + 0.19, y - 0.19, y + 0.19, zz, zz + 0.22, m, 0.0)
    if dmg:
        # snapped SE leg lies on the pad, a stub stands, the platform edge hangs, one sheave lies flat
        L.beam(S, "legbroke", (2.3, -0.9, 0.3), (1.5, -3.9, 0.3), 0.32, "hull2")
        L.beam(S, "legstub", (2.2, -0.6, COL_H), (2.0, -0.2, 2.6), 0.32, "hull2")
        S.bb("platd", CX - 0.8, CX + 0.2, CY - 0.5, CY + 0.5, zt, zt + 0.18, "hull2", 0.0)
        S.cyl("wheelD", SHEAVE_R, 0.2, (2.9, -1.4, 0.3), "bone", verts=16)
        S.cyl("wheelDd", SHEAVE_R - 0.08, 0.22, (2.9, -1.4, 0.3), "dark", verts=14)
        for k in range(4):
            S.bb(f"rubc{k}", 2.4 + 0.7 * k, 2.8 + 0.7 * k, -2.2 - 0.2 * k, -1.8 - 0.2 * k, 0.14, 0.4 + 0.1 * k, "concrete_s", 0.0)
        S.bb("bannerd", CX - 0.5, CX + 0.1, CY - 0.55, CY - 0.5, zt - 1.2, zt - 0.1, "house", 0.0)
        L.beam(S, "cabled", (CX - 0.4, CY, zt), (CX - 0.1, CY - 0.3, 2.4), 0.05, "dark", nocast=True)
        return
    # platform, hanging magenta banner, sheave pillars and big wheels
    S.bb("plat", CX - 0.95, CX + 0.95, CY - 0.7, CY + 0.7, TOP_Z, TOP_Z + 0.2, "hull2", 0.02)
    S.bb("platband", CX - 0.95, CX + 0.95, CY - 0.75, CY - 0.7, TOP_Z - 0.06, TOP_Z + 0.22, "oxide", 0.0)
    bx0, bx1 = CX - 0.55, CX + 0.55
    by = CY - 0.78
    S.bb("bannerbar", bx0 - 0.1, bx1 + 0.1, by - 0.03, by + 0.03, TOP_Z - 0.2, TOP_Z - 0.1, "steel", 0.0)
    S.bb("banner", bx0, bx1, by - 0.05, by, TOP_Z - 2.7, TOP_Z - 0.2, "house", 0.0)
    S.bb("bannertrimT", bx0, bx1, by - 0.07, by - 0.05, TOP_Z - 0.55, TOP_Z - 0.2, "oxide", 0.0)
    S.bb("bannertrimB", bx0, bx1, by - 0.07, by - 0.05, TOP_Z - 2.7, TOP_Z - 2.45, "bone", 0.0)
    S.bb("pillar", CX - 0.95, CX - 0.78, CY - 0.4, CY + 0.4, TOP_Z + 0.2, SHEAVES[0][2] + 0.3, "hull2", 0.0)
    S.bb("pillar", CX + 0.78, CX + 0.95, CY - 0.4, CY + 0.4, TOP_Z + 0.2, SHEAVES[0][2] + 0.3, "hull2", 0.0)
    S.bb("pillar", CX - 0.08, CX + 0.08, CY - 0.4, CY + 0.4, TOP_Z + 0.2, SHEAVES[0][2] - 0.2, "hull2", 0.0)
    YS = S.YS
    for n, (wx, wy, wz) in enumerate(SHEAVES):
        S.cyl(f"wheel{n}", SHEAVE_R, 0.22, (wx, wy, wz), "bone", axis="Y", verts=20)
        S.cyl(f"wheeld{n}", SHEAVE_R - 0.1, 0.26, (wx, wy, wz), "dark", axis="Y", verts=18)
        for q in range(2):
            ang = i * 22.5 + q * 90 + n * 11
            S.box(f"spoke{n}{q}", (2 * SHEAVE_R - 0.12, 0.3, 0.12), (wx, (wy - 0.08) * YS, wz), "bone", 0.0, rot=(0, ang, 0), stretch=False)
        S.cyl(f"hub{n}", 0.18, 0.34, (wx, wy - 0.04, wz), "oxide", axis="Y", verts=12)
    # cables: from the wheels down to the cage, and one long run off to the winding house
    bob = (0.0, 0.3, 0.0, -0.3)[i % 4]
    cz = 2.4 + bob
    for (wx, wy, wz) in SHEAVES:
        L.beam(S, "cable", (wx, wy, wz - 0.7), (wx, wy, cz + 0.55), 0.06, "dark", nocast=True)
    S.bb("cage", CX - 0.6, CX + 0.6, CY - 0.35, CY + 0.35, cz - 0.5, cz + 0.5, "oxide", 0.02)
    S.bb("cageroof", CX - 0.7, CX + 0.7, CY - 0.45, CY + 0.45, cz + 0.5, cz + 0.6, "bone", 0.0)
    S.bb("cagedark", CX - 0.45, CX + 0.45, CY - 0.4, CY - 0.35, cz - 0.4, cz + 0.2, "dark", 0.0, nocast=True)
    L.beam(S, "windcable", (CX - 0.75 - SHEAVE_R, CY, SHEAVES[0][2] - 0.1), (-0.4, 3.7, WIND_H + 0.6), 0.06, "dark", nocast=True)


def collar(S, i, dmg, mode):
    x0, x1, y0, y1 = COL
    S.bb("collar", x0, x1, y0, y1, 0.14, COL_H, "concrete", 0.05)
    S.bb("collarlip", x0 - 0.15, x1 + 0.15, y0 - 0.15, y1 + 0.15, 0.14, 0.45, "concrete2", 0.04)
    S.bb("collartop", x0, x1, y0, y1, COL_H, COL_H + 0.1, "concrete2", 0.0)
    S.bb("shaftmouth", x0 + 0.35, x1 - 0.35, y0 + 0.35, y1 - 0.35, COL_H + 0.05, COL_H + 0.14, "dark", 0.0)
    lit = (mode != "build") and not dmg
    g = "redglow" if lit and (i % 2 == 0 or mode == "build") else ("oxide" if lit else "oxide2")
    S.bb("glowc", CX - 0.5, CX + 0.5, y0 + 0.9, y0 + 1.5, COL_H + 0.14, COL_H + 0.17, g, 0.0, nocast=True)
    for k in range(7):
        xx = x0 + 0.5 + k * ((x1 - x0 - 1.0) / 6)
        S.bb(f"grate{k}", xx - 0.03, xx + 0.03, y0 + 0.3, y1 - 0.3, COL_H + 0.17, COL_H + 0.22, "steel", 0.0, nocast=True)
    n = 11
    sw = (x1 - x0 + 0.3) / n
    for k in range(n):
        S.bb(f"chaz{k}", x0 - 0.15 + k * sw, x0 - 0.15 + (k + 1) * sw, y0 - 0.2, y0 - 0.15, 0.14, 0.45, "oxide" if k % 2 == 0 else "bone", 0.0)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    fx0, fx1, fy0, fy1 = FRONT
    wx0, wx1, wy0, wy1 = WING
    ydoor = fy0 + 0.02
    # ------------------------------------------------------------------ pad, ramp entrance, hazard bar
    D.pad_with_trench(S, PAD, TR, TR_YTOP, ydoor)
    D.hazard_bar(S, -5.0, 5.0, -4.9, -4.55, 0.6)
    D.ramp_entrance(S, TR, TR_YTOP, ydoor, TR_BOT, dmg, mouth_top=1.65)
    # ------------------------------------------------------------------ low hall: front block + west wing
    S.bb("hall", fx0, fx1, fy0, fy1, 0.14, HALL_H, "concrete", 0.05)
    S.bb("hplinth", fx0 - 0.1, fx1 + 0.1, fy0 - 0.1, fy1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("hroof", fx0 - 0.1, fx1 + 0.15, fy0 - 0.2, fy1 + 0.1, HALL_H, HALL_H + 0.22, "roof", 0.05)
    S.bb("wing", wx0, wx1, wy0, wy1, 0.14, HALL_H, "concrete", 0.05)
    S.bb("wplinth", wx0 - 0.1, wx1 + 0.1, wy0, wy1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("wroof", wx0 - 0.1, wx1 + 0.15, wy0 - 0.1, wy1 + 0.2, HALL_H, HALL_H + 0.22, "roof", 0.05)
    zr = HALL_H + 0.22
    S.bb("hparW", wx0 - 0.1, wx0 + 0.06, fy0 - 0.2, wy1 + 0.2, zr, zr + 0.18, "concrete2", 0.02)
    S.bb("hparN", wx0 - 0.1, wx1 + 0.15, wy1 + 0.06, wy1 + 0.2, zr, zr + 0.18, "concrete2", 0.02)
    S.bb("hparS", fx0 - 0.1, fx1 + 0.15, fy0 - 0.2, fy0 - 0.06, zr, zr + 0.18, "concrete2", 0.02)
    D.flank(S, "L", fx0 + 0.1, TR[0] - 0.3, fy0, z1=1.65)
    # air-intake block with louvres on the west wing roof, roof vents
    S.bb("intake", -4.7, -2.9, 0.5, 1.9, zr, zr + 0.45, "concrete2", 0.04)
    for k in range(5):
        S.bb(f"grille{k}", -4.6 + k * 0.33, -4.46 + k * 0.33, 0.45, 1.9, zr + 0.45, zr + 0.52, "dark", 0.0)
    D.roof_vents(S, zr, ((-4.4, 2.3, 0.8), (-3.5, 2.3, 1.1)))
    D.hood(S, TR[0] - 0.3, TR[1] + 0.3, ydoor - 1.1, ydoor - 0.1, dmg, z0=1.65, z1=2.0, band_z=(1.68, 1.97), collapse=1.9)
    D.loudspeaker(S, -2.9, -1.5, zr, dmg)
    # ------------------------------------------------------------------ shaft collar + winding tower (the centrepiece)
    collar(S, i, dmg, mode)
    headframe(S, i, dmg)
    # ------------------------------------------------------------------ small winding house (north) and ash stack (east)
    x0, x1, y0, y1 = WIND
    S.bb("wind", x0, x1, y0, y1, 0.14, WIND_H, "concrete2", 0.04)
    S.bb("windroof", x0 - 0.15, x1 + 0.15, y0 - 0.15, y1 + 0.15, WIND_H, WIND_H + 0.2, "roof", 0.04)
    S.bb("windband", x0, x1, y0 - 0.04, y0, 1.0, 1.3, "oxide", 0.0)
    S.bb("windplate", x0 + 0.3, x0 + 1.9, y0 - 0.08, y0 - 0.04, 0.35, 0.9, "bone", 0.0)
    for k in range(2):
        S.bb(f"sten_w{k}", x0 + 0.4, x0 + 1.8, y0 - 0.1, y0 - 0.08, 0.45 + k * 0.22, 0.56 + k * 0.22, "dark", 0.0, nocast=True)
    D.furnace_slits(S, x0 + 2.4, y0 - 0.1, 0.35, 0.8, 3, i, (idle or mode == "build") and not dmg, step=0.55, w=0.28)
    sx, sy = STACK
    S.bb("furn", sx - 0.9, sx + 0.9, sy - 0.8, sy + 0.8, 0.14, 1.3, "concrete2", 0.04)
    S.bb("furnroof", sx - 1.0, sx + 1.0, sy - 0.9, sy + 0.9, 1.3, 1.45, "roof", 0.04)
    S.bb("furnband", sx - 0.9, sx + 0.9, sy - 0.84, sy - 0.8, 0.9, 1.1, "oxide", 0.0)
    D.furnace_slits(S, sx - 0.6, sy - 0.9, 0.35, 0.75, 3, i + 1, (idle or mode == "build") and not dmg, step=0.45, w=0.25)
    D.chimney(S, sx, sy, 1.45, 3.2 if dmg else 4.0, dmg, idle or mode == "build", i, rise=1.5, lean_len=1.3)
    D.ash_drift(S, ((3.8, -2.6, 1.3), (-4.4, 3.9, 0.8), (3.9, 0.0, 0.8)))
    if dmg:
        L.R.scorch(S, "scH", -2.4, -2.3, 2.1, 1.6, 0.5)
        L.R.scorch(S, "scC", CX, CY - 1.6, 0.9, 1.6, 0.6)


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
    dict(state="damaged", kind="fire_large", x=anchor(CX, CY, 5.2)[0], y=anchor(CX, CY, 5.2)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(-1.2, -3.0, 0.5)[0], y=anchor(-1.2, -3.0, 0.5)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(2.9, -1.4, 0.4)[0], y=anchor(2.9, -1.4, 0.4)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=anchor(STACK[0], STACK[1], 3.3)[0], y=anchor(STACK[0], STACK[1], 3.3)[1], phase=6),
    dict(state="damaged", kind="smoke_plume", x=anchor(CX, CY, 5.4)[0], y=anchor(CX, CY, 5.4)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(STACK[0], STACK[1], 3.4)[0], y=anchor(STACK[0], STACK[1], 3.4)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-4.8, 4.8, -4.2, 4.6), H=TOP_Z,
    boxes=[(-1.6, 2.2, -0.6, 2.4)], rings=[(STACK[0], STACK[1], 0.7)],
    clips=(3.0, 5.4), scaf=[(-5.0, -4.3, -4.7, -4.1), (0.6, 1.6, -4.7, -4.1)],
    derrick=(4.6, -3.2, 5.0, (3.6, -2.4, 5.2)), sparks=[(-2.0, -1.0, 2.3), (CX, CY, 3.0), (-1.0, 3.8, 2.0)],
    hide=D.HIDE_ANIM + ("wheel", "spoke", "hub", "cage", "cable", "windcable", "banner"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
