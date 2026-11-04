"""Directorate Broadcast Mast: radar + jammer (2x2 cells).  Original design.

The read is a tall, slim LATTICE MAST standing on a concrete collar at the north-east of the pad: four tapering legs with X braces,
a rotating radar bar antenna on a pole at the very top (spins in the idle loop), a ring of loudspeaker horns on a platform two thirds
up, a hanging magenta banner on the south face and a blinking ember lamp.  A low umber bunker with a ramp mouth under a small hood
(magenta band, blast doors folded back, ember glow) sits south-west, with an ash stack behind it, bone / oxide hazard bar on the edge.
Idle loop: the antenna turns, lamp blinks, furnace slits flicker, ash puffs.  Damage is structural only (D-069): the mast has snapped
at the platform (the top section lies on the pad with the antenna), the hood end has fallen, the stack is snapped.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_mast"
TITLE = "Directorate Broadcast Mast"
FOOTPRINT = (2, 2)
CANVAS = (128, 320)
ORIGIN = (0, 172)       # headroom for the tall lattice, antenna and lamp
DOCK = (0, 2)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-3.3, 3.3, -3.3, 3.3)
TR = (-2.7, -1.0)
TR_YTOP, TR_BOT = -2.85, -0.6
FRONT = (-3.3, 0.5, -1.9, 1.6)
HALL_H = 1.5
MX, MY = 1.9, 1.5                       # mast centre
COL = (MX - 0.95, MX + 0.95, MY - 0.95, MY + 0.95)
COL_H = 0.9
HB, HT = 0.8, 0.22                      # leg half-width at base / top
TOP_Z = 10.0
PLAT_Z = 6.6
LEVELS = (COL_H, 3.0, 4.9, PLAT_Z, 8.4, TOP_Z)
STACK = (-2.2, 2.4)


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def leg_xy(k, z, zt=TOP_Z):
    sx, sy = (-1, -1, 1, 1)[k], (-1, 1, -1, 1)[k]
    t = (z - COL_H) / (zt - COL_H)
    h = HB + (HT - HB) * t
    return MX + sx * h, MY + sy * h


def mast(S, i, dmg):
    zt = PLAT_Z if dmg else TOP_Z
    lv = [z for z in LEVELS if z <= zt + 0.01]
    for k in range(4):
        x0, y0 = leg_xy(k, COL_H)
        x1, y1 = leg_xy(k, zt)
        L.beam(S, f"leg{k}", (x0, y0, COL_H), (x1, y1, zt), 0.24, "hull2")
    for z in lv[1:]:
        pts = [leg_xy(k, z) for k in range(4)]
        for a, b in ((0, 1), (2, 3), (0, 2), (1, 3)):
            L.beam(S, "tie", (pts[a][0], pts[a][1], z), (pts[b][0], pts[b][1], z), 0.1, "steel")
    for za, zb in zip(lv, lv[1:]):
        for a, b in ((0, 2), (1, 3), (0, 1)):          # south face, both sides
            for (p, q) in ((a, b), (b, a)):
                pa, pb = leg_xy(p, za), leg_xy(q, zb)
                L.beam(S, "brace", (pa[0], pa[1], za), (pb[0], pb[1], zb), 0.06, "steel")
    if not dmg:
        for k in (0, 2):
            for zz, m in ((2.0, "bone"), (4.0, "oxide"), (8.7, "bone")):
                x, y = leg_xy(k, zz)
                S.bb("legband", x - 0.15, x + 0.15, y - 0.15, y + 0.15, zz, zz + 0.2, m, 0.0)
    # horn ring platform
    pz = PLAT_Z
    S.bb("plat", MX - 0.62, MX + 0.62, MY - 0.62, MY + 0.62, pz, pz + 0.16, "hull2", 0.02)
    S.bb("platband", MX - 0.62, MX + 0.62, MY - 0.66, MY - 0.62, pz - 0.05, pz + 0.18, "oxide", 0.0)
    hz = pz + 0.5
    S.bb("harmx", MX - 0.55, MX + 0.55, MY - 0.05, MY + 0.05, hz - 0.05, hz + 0.05, "steel", 0.0)
    S.bb("harmy", MX - 0.05, MX + 0.05, MY - 0.55, MY + 0.55, hz - 0.05, hz + 0.05, "steel", 0.0)
    for sg in (-1, 1):
        S.cyl(f"hdrvx{sg}", 0.11, 0.22, (MX + sg * 0.55, MY, hz), "oxide", axis="X", verts=10)
        S.cone(f"hornx{sg}", 0.06, 0.34, 0.8, (MX + sg * 1.0, MY, hz), "door", verts=14, rot=(0, 90 * sg, 0), nocast=False)
        S.cyl(f"hrimx{sg}", 0.36, 0.05, (MX + sg * 1.42, MY, hz), "bone", axis="X", verts=14)
        S.cyl(f"hthrx{sg}", 0.27, 0.06, (MX + sg * 1.43, MY, hz), "dark", axis="X", verts=12)
        S.cone(f"horny{sg}", 0.06, 0.32, 0.7, (MX, MY + sg * 0.9, hz + 0.05), "door", verts=14, rot=(-90 * sg, 0, 0), nocast=False)
    if dmg:
        # snapped: stub of the leg tops, the upper section lies on the pad, antenna beside it
        L.beam(S, "legbroke", (3.0, -0.2, 0.3), (1.3, -2.4, 0.3), 0.24, "hull2")
        L.beam(S, "legbroke2", (3.2, -0.3, 0.3), (1.6, -2.6, 0.3), 0.24, "hull2")
        for q in range(4):
            L.beam(S, "brokb", (3.0 - 0.45 * q, -0.4 - 0.5 * q, 0.3), (3.4 - 0.45 * q, -0.5 - 0.5 * q, 0.7), 0.06, "steel")
        S.bb("antD", 0.4, 2.2, -2.9, -2.7, 0.14, 0.7, "bone", 0.02, rot=(0, 0, 14))
        S.bb("antDd", 0.5, 2.1, -2.72, -2.7, 0.2, 0.62, "dark", 0.0, nocast=True, rot=(0, 0, 14))
        S.cyl("hornD", 0.34, 0.8, (3.0, -1.7, 0.4), "door", axis="X", verts=12)
        S.bb("bannerd", 2.7, 3.2, -1.5, -1.45, 0.14, 0.2, "house", 0.0)
        return
    # hanging banner on the south face + hanging bar
    by = MY - 0.82
    S.bb("bannerbar", MX - 0.5, MX + 0.5, by - 0.03, by + 0.03, 6.3, 6.4, "steel", 0.0)
    S.bb("banner", MX - 0.4, MX + 0.4, by - 0.05, by, 3.8, 6.3, "house", 0.0)
    S.bb("bannertrimT", MX - 0.4, MX + 0.4, by - 0.07, by - 0.05, 5.8, 6.3, "oxide", 0.0)
    S.bb("bannertrimB", MX - 0.4, MX + 0.4, by - 0.07, by - 0.05, 3.8, 4.1, "bone", 0.0)
    # radar bar antenna: pole, hub, spinning reflector (45 deg per frame; looks the same every 180)
    S.cyl("apole", 0.07, 0.9, (MX, MY, TOP_Z + 0.45), "steel", verts=8)
    S.bb("aplat", MX - 0.3, MX + 0.3, MY - 0.3, MY + 0.3, TOP_Z, TOP_Z + 0.14, "hull2", 0.02)
    ang = i * 45.0 + 20
    YS = S.YS
    az = TOP_Z + 1.15
    S.box("dishA", (2.6, 0.2, 1.0), (MX, MY * YS, az), "bone", 0.02, rot=(0, 0, ang), stretch=False)
    S.box("dishB", (2.3, 0.1, 0.7), (MX + 0.0, MY * YS, az), "dark", 0.0, rot=(0, 0, ang + 90), stretch=False, nocast=True)
    S.box("dishC", (2.64, 0.24, 0.12), (MX, MY * YS, az + 0.5), "oxide", 0.0, rot=(0, 0, ang), stretch=False)
    S.cyl("dishhub", 0.16, 0.5, (MX, MY, az), "steel", verts=10)
    S.cyl("lampm", 0.1, 0.3, (MX, MY, az + 0.75), "steel", verts=8)
    S.sphere("blink", 0.2, (MX, MY, az + 1.0), "redglow" if i % 2 == 0 else "oxide2")


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    fx0, fx1, fy0, fy1 = FRONT
    ydoor = fy0 + 0.02
    D.pad_with_trench(S, PAD, TR, TR_YTOP, ydoor)
    D.hazard_bar(S, -3.3, 3.3, -3.28, -3.0, 0.55)
    D.ramp_entrance(S, TR, TR_YTOP, ydoor, TR_BOT, dmg, mouth_top=1.2, nst=4)
    S.bb("hall", fx0, fx1, fy0, fy1, 0.14, HALL_H, "concrete", 0.05)
    S.bb("hplinth", fx0 - 0.1, fx1 + 0.1, fy0 - 0.1, fy1 + 0.1, 0.14, 0.45, "concrete2", 0.04)
    S.bb("hroof", fx0 - 0.1, fx1 + 0.15, fy0 - 0.2, fy1 + 0.1, HALL_H, HALL_H + 0.2, "roof", 0.05)
    zr = HALL_H + 0.2
    S.bb("hparW", fx0 - 0.1, fx0 + 0.05, fy0 - 0.2, fy1 + 0.1, zr, zr + 0.15, "concrete2", 0.02)
    S.bb("hparS", fx0 - 0.1, fx1 + 0.15, fy0 - 0.2, fy0 - 0.06, zr, zr + 0.15, "concrete2", 0.02)
    D.flank(S, "R", TR[1] + 0.35, fx1 - 0.1, fy0, z0=0.45, z1=1.2, stripes=2)
    D.hood(S, TR[0] - 0.25, TR[1] + 0.25, ydoor - 1.0, ydoor - 0.1, dmg, z0=1.25, z1=1.5, band_z=(1.28, 1.47), collapse=0.9)
    D.roof_vents(S, zr, ((-2.6, 0.7, 0.7),))
    D.loudspeaker(S, -0.9, 0.0, zr, dmg, height=1.6)
    # mast collar on the pad
    x0, x1, y0, y1 = COL
    S.bb("collar", x0, x1, y0, y1, 0.14, COL_H, "concrete", 0.05)
    S.bb("collarlip", x0 - 0.12, x1 + 0.12, y0 - 0.12, y1 + 0.12, 0.14, 0.4, "concrete2", 0.04)
    S.bb("collartop", x0, x1, y0, y1, COL_H, COL_H + 0.08, "concrete2", 0.0)
    for k in range(7):
        S.bb(f"chaz{k}", x0 - 0.12 + k * (x1 - x0 + 0.24) / 7, x0 - 0.12 + (k + 1) * (x1 - x0 + 0.24) / 7, y0 - 0.17, y0 - 0.12, 0.14, 0.4, "oxide" if k % 2 == 0 else "bone", 0.0)
    mast(S, i, dmg)
    sx, sy = STACK
    S.bb("furn", sx - 0.7, sx + 0.7, sy - 0.6, sy + 0.6, 0.14, 1.0, "concrete2", 0.04)
    S.bb("furnroof", sx - 0.8, sx + 0.8, sy - 0.7, sy + 0.7, 1.0, 1.12, "roof", 0.04)
    D.furnace_slits(S, sx - 0.4, sy - 0.7, 0.3, 0.65, 2, i + 1, (idle or mode == "build") and not dmg, step=0.45, w=0.25)
    D.chimney(S, sx, sy, 1.12, 2.5 if dmg else 3.3, dmg, idle or mode == "build", i, rise=1.3, lean_len=1.1)
    D.ash_drift(S, ((1.0, -2.2, 1.0), (-2.9, 2.6, 0.6)))
    if dmg:
        L.R.scorch(S, "scH", -1.8, -1.9, 1.6, 1.3, 0.5)
        L.R.scorch(S, "scM", 2.0, -1.4, 1.3, 1.6, 0.6)


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.4, 3.1, -2.8, -2.1, 0.14, 0.7, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.6):
            S.cyl("lpipe", 0.13, 1.8, (0.9, -2.6, z), "steel", axis="X", verts=10)
        S.cyl("barrel", 0.28, 0.65, (-0.2, -2.5, 0.5), "rust", verts=12)
    S.bb("planks", 2.2, 3.2, -0.6, -0.1, 0.14, 0.4, "timber", 0.0)


def _left(S):
    S.bb("planks", 2.4, 3.0, -2.6, -2.0, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.13, 1.6, (0.6, -2.6, 0.3), "steel", axis="X", verts=10)
    L.spark(S, 0.0, -1.0, 1.2, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(MX, MY, 6.4)[0], y=anchor(MX, MY, 6.4)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(2.2, -1.6, 0.5)[0], y=anchor(2.2, -1.6, 0.5)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(-1.6, -2.1, 0.6)[0], y=anchor(-1.6, -2.1, 0.6)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=anchor(STACK[0], STACK[1], 2.5)[0], y=anchor(STACK[0], STACK[1], 2.5)[1], phase=6),
    dict(state="damaged", kind="smoke_plume", x=anchor(MX, MY, 6.6)[0], y=anchor(MX, MY, 6.6)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(STACK[0], STACK[1], 2.6)[0], y=anchor(STACK[0], STACK[1], 2.6)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-3.1, 3.1, -2.8, 3.1), H=TOP_Z,
    boxes=[(MX - 0.8, MX + 0.8, MY - 0.8, MY + 0.8)], rings=[(STACK[0], STACK[1], 0.55)],
    clips=(4.0, 7.2), scaf=[(-3.1, -2.5, -3.1, -2.6), (0.2, 0.8, -3.1, -2.6)],
    derrick=(2.8, -2.4, 4.2, (1.9, -1.6, 4.4)), sparks=[(-1.0, -0.5, 1.8), (MX, MY, 3.0), (-1.0, 2.2, 1.6)],
    hide=D.HIDE_ANIM + ("dish", "blink", "apole", "lampm", "banner", "hrim"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
