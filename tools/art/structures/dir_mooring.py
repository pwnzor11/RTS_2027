"""Directorate Mooring Tower: dirigible dock (3x3 cells).  Original design.

The read is a tall concrete-and-steel MOORING MAST on a round collar, with a big bone / oxide DOCKING RING (horizontal ring on arms)
near the top, a mooring cone and a magenta pennant streamer, hazard bands on the mast and cable stays to the pad.  At its foot, a long
low HANGAR on the west whose south face is a wide ramp-mouth bay (hood with magenta band, blast doors folded back, ember glow) where
dirigibles are inflated; a gas-bag tank and ash stack on the east, bone / oxide hazard bar along the south edge.  "The most cheerful
building": bunting-coloured oxide / bone stripes on the ring.  The dirigible itself is NOT part of this sprite.
Idle loop: the ring beacons blink round, the pennant waves, the stack puffs, furnace slits flicker.  Damage is structural only (D-069):
the mast has snapped below the ring (ring lies on the pad), the hood end has fallen, the stack is snapped.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_mooring"
TITLE = "Directorate Mooring Tower"
FOOTPRINT = (3, 3)
CANVAS = (192, 336)
ORIGIN = (0, 144)
DOCK = (1, 3)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-5.0, 5.0, -5.0, 5.0)
TR = (-4.3, -0.9)
TR_YTOP, TR_BOT = -4.3, -1.15
FRONT = (-5.0, 0.0, -2.3, -0.9)
HANGAR = (-5.0, 0.2, -0.9, 3.8)
HALL_H = 2.4
MX, MY = 1.7, 0.9
MAST_R = 0.42
TOP_Z = 10.2
RING_Z = 7.8
RING_R = 1.55
STACK = (3.4, 3.4)


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def mast(S, i, dmg):
    # collar
    S.cyl("collar", 1.35, 0.9, (MX, MY, 0.14 + 0.45), "concrete", verts=20)
    S.cyl("collarlip", 1.5, 0.35, (MX, MY, 0.14 + 0.18), "concrete2", verts=20)
    for k in range(10):
        a0 = k * 36
        x = MX + 1.5 * math.cos(math.radians(a0 + 18)) * 1.0
        y = MY + 1.5 * math.sin(math.radians(a0 + 18))
        if y < MY - 0.4:
            S.bb(f"chaz{k}", x - 0.2, x + 0.2, y - 0.12, y + 0.05, 0.14, 0.4, "oxide" if k % 2 == 0 else "bone", 0.0)
    zt = 5.0 if dmg else TOP_Z
    zb = 1.04
    S.cyl("shaft", MAST_R, zt - zb, (MX, MY, (zt + zb) / 2), "concrete", verts=16)
    S.cyl("shaftcap", MAST_R + 0.1, 0.2, (MX, MY, zb + 0.1), "concrete2", verts=16)
    z = zb + 1.0
    k = 0
    while z < zt - 0.3:
        S.cyl(f"mband{k}", MAST_R + 0.04, 0.35, (MX, MY, z), "bone" if k % 2 == 0 else "oxide", verts=16)
        z += 1.6
        k += 1
    # slim lattice fins on the south side for scale/detail
    for sx in (-1, 1):
        L.beam(S, "fin", (MX + sx * 1.5, MY - 0.6, 0.9), (MX + sx * 0.3, MY - 0.2, zt - 0.8), 0.12, "steel")
    if dmg:
        S.cyl("shaftstub", MAST_R - 0.05, 0.3, (MX, MY, zt + 0.1), "concrete2", verts=12)
        # ring on the pad, shaft top lying across it
        S.cyl("ringD", 1.2, 0.2, (3.2, -3.2, 0.25), "bone", verts=20)
        S.cyl("ringDi", 0.95, 0.24, (3.2, -3.2, 0.25), "dark", verts=18)
        S.cyl("ringDo", 1.2, 0.08, (3.2, -3.2, 0.37), "oxide", verts=20)
        L.beam(S, "shaftD", (4.6, -1.0, 0.45), (2.3, -4.0, 0.45), 0.7, "concrete")
        S.bb("pennD", 3.8, 4.6, -4.2, -4.1, 0.14, 0.25, "house", 0.0)
        return
    # docking ring on four arms
    for q in range(4):
        a = math.radians(q * 90 + 45)
        L.beam(S, "ringarm", (MX, MY, RING_Z - 0.35), (MX + RING_R * math.cos(a), MY + RING_R * math.sin(a), RING_Z), 0.12, "steel")
    segs = 16
    for k in range(segs):
        a0 = 2 * math.pi * k / segs
        a1 = 2 * math.pi * (k + 1) / segs
        p0 = (MX + RING_R * math.cos(a0), MY + RING_R * math.sin(a0), RING_Z)
        p1 = (MX + RING_R * math.cos(a1), MY + RING_R * math.sin(a1), RING_Z)
        L.beam(S, f"ring{k}", p0, p1, 0.22, "oxide" if k % 2 == 0 else "bone")
    for q in range(4):
        a = math.radians(q * 90 + 45)
        on = (q == i % 4)
        S.sphere(f"beacon{q}", 0.17, (MX + RING_R * math.cos(a), MY + RING_R * math.sin(a), RING_Z + 0.3), "redglow" if on else "oxide2")
    # mooring cone above the ring, pole and waving pennant
    S.cone("mcone", 0.7, 0.2, 0.9, (MX, MY, RING_Z + 0.9), "steel", verts=14, nocast=False)
    S.cyl("topcap", 0.2, 1.0, (MX, MY, TOP_Z - 0.3), "hull2", verts=10)
    S.cyl("pole", 0.05, 1.2, (MX, MY, TOP_Z + 0.75), "steel", verts=6)
    sway = (0.0, 0.18, 0.0, -0.18)[i % 4]
    S.bb("pennant", MX, MX + 1.2, MY - 0.04 + sway, MY + 0.04 + sway, TOP_Z + 0.95, TOP_Z + 1.35, "house", 0.0)
    S.bb("pennantb", MX + 1.2, MX + 1.7, MY - 0.04 + sway * 2, MY + 0.04 + sway * 2, TOP_Z + 1.02, TOP_Z + 1.3, "house", 0.0)
    # stay cables to the pad
    for (px, py) in ((MX - 3.2, MY - 2.6), (MX + 2.0, MY - 3.4)):
        L.beam(S, "stay", (MX, MY - 0.3, RING_Z - 1.0), (px, py, 0.2), 0.04, "dark", nocast=True)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    fx0, fx1, fy0, fy1 = FRONT
    hx0, hx1, hy0, hy1 = HANGAR
    ydoor = fy0 + 0.02
    D.pad_with_trench(S, PAD, TR, TR_YTOP, ydoor)
    D.hazard_bar(S, -5.0, 5.0, -4.9, -4.55, 0.6)
    D.ramp_entrance(S, TR, TR_YTOP, ydoor, TR_BOT, dmg, mouth_top=1.9)
    S.bb("front", fx0, fx1, fy0, fy1, 0.14, 2.0, "concrete", 0.05)
    S.bb("fplinth", fx0 - 0.1, fx1 + 0.1, fy0 - 0.1, fy1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("hang", hx0, hx1, hy0, hy1, 0.14, HALL_H, "concrete", 0.05)
    S.bb("hplinth", hx0 - 0.1, hx1 + 0.1, hy0, hy1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    # barrel-vault-feel roof: stepped roof slabs with rib bands
    S.bb("hroof", hx0 - 0.1, hx1 + 0.15, fy0 - 0.2, hy1 + 0.1, HALL_H, HALL_H + 0.22, "roof", 0.05)
    zr = HALL_H + 0.22
    S.bb("hridge", hx0 + 0.3, hx1 - 0.2, hy0 + 0.4, hy1 - 0.4, zr, zr + 0.35, "hull2", 0.04)
    for k in range(5):
        y = hy0 + 0.1 + k * 1.0
        S.bb(f"hrib{k}", hx0 - 0.1, hx1 + 0.15, y, y + 0.12, zr, zr + 0.2, "concrete2", 0.0)
    S.bb("hband", hx0 + 0.4, hx1 - 0.3, fy0 + 0.05, fy0 + 1.0, zr + 0.01, zr + 0.03, "house", 0.0, nocast=True)
    S.bb("hparW", hx0 - 0.1, hx0 + 0.06, fy0 - 0.2, hy1 + 0.1, zr, zr + 0.18, "concrete2", 0.02)
    S.bb("hparS", hx0 - 0.1, hx1 + 0.15, fy0 - 0.2, fy0 - 0.06, zr, zr + 0.18, "concrete2", 0.02)
    D.flank(S, "R", TR[1] + 0.3, fx1 - 0.1, fy0, z0=0.5, z1=1.9)
    D.hood(S, TR[0] - 0.3, TR[1] + 0.3, ydoor - 1.1, ydoor - 0.1, dmg, z0=1.95, z1=2.3, band_z=(1.98, 2.27), collapse=1.9)
    D.roof_vents(S, zr + 0.3, ((-4.2, 2.5, 0.8),))
    D.loudspeaker(S, -2.6, -0.4, zr, dmg, height=1.8)
    mast(S, i, dmg)
    # gas tank and ash stack on the east
    S.cyl("tank", 0.95, 1.3, (4.0, -1.6, 0.14 + 0.65), "lead", verts=18)
    S.cyl("tankring", 0.98, 0.14, (4.0, -1.8, 0.6), "oxide", verts=18)
    S.cyl("tankcap", 0.75, 0.1, (4.0, -1.8, 1.49), "steel", verts=16)
    sx, sy = STACK
    S.bb("furn", sx - 0.8, sx + 0.8, sy - 0.7, sy + 0.7, 0.14, 1.1, "concrete2", 0.04)
    S.bb("furnroof", sx - 0.9, sx + 0.9, sy - 0.8, sy + 0.8, 1.1, 1.24, "roof", 0.04)
    D.furnace_slits(S, sx - 0.5, sy - 0.8, 0.3, 0.7, 3, i + 1, (idle or mode == "build") and not dmg, step=0.4, w=0.22)
    D.chimney(S, sx, sy, 1.24, 3.0 if dmg else 3.9, dmg, idle or mode == "build", i, rise=1.4, lean_len=1.2)
    D.ash_drift(S, ((1.0, -3.8, 1.2), (-4.2, 4.3, 0.8)))
    if dmg:
        L.R.scorch(S, "scH", -2.4, -2.25, 2.0, 1.5, 0.5)
        L.R.scorch(S, "scM", MX, MY - 1.6, 0.9, 1.5, 0.6)


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 3.4, 4.2, -4.4, -3.6, 0.14, 0.8, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (1.0, -3.9, z), "steel", axis="X", verts=10)
        S.cyl("barrel", 0.3, 0.7, (4.3, -3.2, 0.5), "rust", verts=12)
    S.bb("planks", -4.6, -3.2, 3.9, 4.5, 0.14, 0.5, "timber", 0.0)


def _left(S):
    S.bb("planks", -4.5, -4.2, -4.4, -3.4, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 2.0, (2.0, -4.0, 0.3), "steel", axis="X", verts=10)
    L.spark(S, 0.4, -3.4, 1.4, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(MX, MY, 5.0)[0], y=anchor(MX, MY, 5.0)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(3.2, -3.2, 0.6)[0], y=anchor(3.2, -3.2, 0.6)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(-1.4, -3.0, 0.5)[0], y=anchor(-1.4, -3.0, 0.5)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=anchor(STACK[0], STACK[1], 3.0)[0], y=anchor(STACK[0], STACK[1], 3.0)[1], phase=6),
    dict(state="damaged", kind="smoke_plume", x=anchor(MX, MY, 5.2)[0], y=anchor(MX, MY, 5.2)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(STACK[0], STACK[1], 3.1)[0], y=anchor(STACK[0], STACK[1], 3.1)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-4.8, 4.8, -2.2, 4.6), H=TOP_Z,
    boxes=[(-4.8, 0.0, -0.6, 3.6)], rings=[(MX, MY, 1.1), (STACK[0], STACK[1], 0.7)],
    clips=(3.0, 5.6), scaf=[(-5.0, -4.3, -4.7, -4.1), (0.6, 1.6, -4.7, -4.1)],
    derrick=(4.6, -3.4, 5.0, (3.6, -2.6, 5.2)), sparks=[(-2.0, 0.0, 2.3), (MX, MY, 3.0), (-1.0, 3.0, 2.0)],
    hide=D.HIDE_ANIM + ("beacon", "pennant", "ring", "mcone", "pole", "stay"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
