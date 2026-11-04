"""Directorate Assembly Bore: vehicle factory (3x3 cells, size-M tunnel entrance on the south face).  Original design.

The read is a HANGAR-SIZED CAVERN MOUTH under a portal gantry: a wide low umber works hall with a broad ramp mouth (big hood with a magenta
band, blast doors folded back, ember glow) and sawtooth skylight ribs glowing along the roof.  A bone / oxide bridge-crane gantry
straddles the whole hall from the pad, its trolley running along the girder with a hanging vehicle chassis; a magenta banner hangs
from the girder.  Roof vents, loudspeaker, parts stacks on the pad, hazard bar along the south edge.
Idle loop: the trolley and its hanging chassis travel along the girder, skylight slits flicker.  Damage is structural only (D-069): the
east half of the girder has fallen with its leg, the chassis lies on the pad, hood end down.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint x, y in [-5.1, 5.1].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_assembly_bore"
TITLE = "Directorate Assembly Bore"
FOOTPRINT = (3, 3)
CANVAS = (192, 296)
ORIGIN = (0, 80)
DOCK = (1, 3)
CAP_MATERIAL = D.CAP_MATERIAL
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-5.0, 5.0, -5.0, 5.0)
TR = (-1.7, 1.7)
TR_YTOP, TR_BOT = -4.3, -1.0
YD = -1.2
HALL = (-4.4, 4.4, YD, 4.4)
HALL_H = 2.7
ZR = HALL_H + 0.22
GY0, GY1 = 1.3, 1.7           # gantry girder y range
GZ = 5.3                      # girder underside
LEGX = 4.75


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def gantry(S, i, dmg, mode):
    for sgn in (-1, 1):
        if dmg and sgn == 1:
            S.bb("legstub", sgn * LEGX - 0.2, sgn * LEGX + 0.2, 1.2, 1.8, 0.14, 1.6, "hull2", 0.02)
            continue
        S.bb(f"leg{sgn}", sgn * LEGX - 0.2, sgn * LEGX + 0.2, 1.2, 1.8, 0.14, GZ + 0.3, "concrete2", 0.02)
        for zz, m in ((1.0, "bone"), (2.4, "oxide"), (3.8, "bone")):
            S.bb(f"legband{sgn}", sgn * LEGX - 0.23, sgn * LEGX + 0.23, 1.17, 1.83, zz, zz + 0.22, m, 0.0)
    xe = 0.4 if dmg else LEGX + 0.25
    S.bb("girder", -LEGX - 0.25, xe, GY0 - 0.1, GY1 + 0.1, GZ, GZ + 0.45, "steel", 0.02)
    S.bb("girderband", -LEGX - 0.25, xe, GY0 - 0.04, GY0, GZ + 0.1, GZ + 0.3, "oxide", 0.0)
    nb = int((xe + LEGX) / 0.9)
    for k in range(nb):
        xa = -LEGX + k * 0.9
        L.beam(S, "gbrace", (xa, 1.5, GZ), (xa + 0.45, 1.5, GZ + 0.4), 0.07, "steel", nocast=True)
    if dmg:
        L.beam(S, "girderfall", (0.4, 1.5, GZ + 0.2), (3.4, 0.2, 0.5), 0.4, "hull2")
        S.bb("rubG", 2.6, 4.0, -1.2, 0.2, 0.14, 0.5, "concrete_s", 0.0)
        S.bb("chassisd", 3.0, 4.4, -2.9, -2.1, 0.14, 0.6, "steel", 0.03, rot=(0, 0, 14))
        return
    # banner
    S.bb("bannerbar", -4.3, -3.3, GY0 - 0.08, GY0 - 0.04, GZ - 0.05, GZ + 0.02, "steel", 0.0)
    S.bb("banner", -4.2, -3.4, GY0 - 0.1, GY0 - 0.06, GZ - 1.8, GZ - 0.05, "house", 0.0)
    S.bb("bannertrimB", -4.2, -3.4, GY0 - 0.12, GY0 - 0.1, GZ - 1.8, GZ - 1.6, "bone", 0.0)
    # trolley, cable, hanging chassis
    tx = 2.4 * math.sin(2 * math.pi * i / 4) if mode != "build" else 0.0
    S.bb("trolley", tx - 0.5, tx + 0.5, GY0 - 0.12, GY1 + 0.12, GZ - 0.35, GZ, "oxide", 0.02)
    cz = 4.1
    for ox in (-0.4, 0.4):
        L.beam(S, "cable", (tx + ox, 1.5, GZ - 0.35), (tx + ox, 1.5, cz + 0.35), 0.05, "dark", nocast=True)
    S.bb("chassis", tx - 0.8, tx + 0.8, 1.15, 1.85, cz - 0.15, cz + 0.2, "steel", 0.03)
    S.bb("chassisband", tx - 0.8, tx + 0.8, 1.1, 1.15, cz - 0.1, cz + 0.1, "bone", 0.0)
    for sx in (-0.65, 0.65):
        S.cyl("cwheel", 0.2, 0.2, (tx + sx, 1.0, cz - 0.1), "dark", axis="Y", verts=10)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    lit = (idle or mode == "build") and not dmg
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    x0, x1, y0, y1 = HALL
    D.pad_with_trench(S, PAD, TR, TR_YTOP, YD + 0.02)
    D.hazard_bar(S, -5.0, 5.0, -4.9, -4.55, 0.6)
    D.ramp_entrance(S, TR, TR_YTOP, YD + 0.02, TR_BOT, dmg, mouth_top=2.2)
    S.bb("hall", x0, x1, y0, y1, 0.14, HALL_H, "concrete", 0.05)
    S.bb("hplinth", x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("hroof", x0 - 0.1, x1 + 0.15, y0 - 0.2, y1 + 0.1, HALL_H, ZR, "roof", 0.05)
    S.bb("hparS", x0 - 0.1, x1 + 0.15, y0 - 0.2, y0 - 0.06, ZR, ZR + 0.18, "concrete2", 0.02)
    S.bb("hparW", x0 - 0.1, x0 + 0.06, y0 - 0.2, y1 + 0.1, ZR, ZR + 0.18, "concrete2", 0.02)
    S.bb("hparE", x1, x1 + 0.15, y0 - 0.2, y1 + 0.1, ZR, ZR + 0.18, "concrete2", 0.02)
    D.flank(S, "L", -4.1, TR[0] - 0.3, YD, z1=2.2, stripes=4)
    D.flank(S, "R", TR[1] + 0.3, 4.1, YD, z1=2.2, stripes=4)
    D.hood(S, TR[0] - 0.3, TR[1] + 0.3, YD - 1.2, YD - 0.1, dmg, z0=2.2, z1=2.6, band_z=(2.23, 2.57), collapse=2.2)
    # sawtooth skylight ribs with ember slits on their south faces
    for k in range(4):
        a = 2.3 + k * 0.5 if False else 2.2 + k * 0.55
        S.bb(f"tooth{k}", -4.0, 4.0, a, a + 0.12, ZR, ZR + 0.5, "concrete2", 0.03)
        if k >= 1 and not (dmg and k == 3):
            for q in range(6):
                xa = -3.4 + q * 1.2
                on = lit and ((q + k + i) % 3 != 0)
                S.bb(f"glow{k}{q}", xa, xa + 0.7, a - 0.04, a, ZR + 0.15, ZR + 0.38, "redglow" if on else "oxide2", 0.0, nocast=True)
    D.roof_vents(S, ZR, ((-3.4, 3.7, 0.8), (3.0, 3.7, 0.8)))
    D.loudspeaker(S, -3.0, -0.2, ZR, dmg, height=1.5)
    gantry(S, i, dmg, mode)
    # parts stacks on the pad (east): hull plates, track drums
    for k in range(3):
        S.bb(f"plates{k}", 3.4, 4.7, -3.6 + k * 0.0, -2.6, 0.14 + k * 0.18, 0.32 + k * 0.18, "steel" if k != 1 else "oxide", 0.02, rot=(0, 0, 4 * k))
    S.cyl("drum", 0.4, 0.5, (-4.2, -3.4, 0.4), "rust", verts=12)
    S.cyl("drum", 0.4, 0.5, (-3.4, -3.5, 0.4), "lead", verts=12)
    D.ash_drift(S, ((4.0, -4.2, 1.0), (-4.3, 4.0, 0.8)))
    if dmg:
        L.R.scorch(S, "scH", 0.0, -3.0, 2.4, 1.6, 0.5)


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 3.4, 4.2, -4.4, -3.6, 0.14, 0.8, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (3.4, -3.9, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", -4.6, -2.8, -3.2, -3.0, 0.14, 0.3, "rebar", 0.0)
    S.bb("planks", -4.6, -3.2, 4.5, 4.9, 0.14, 0.5, "timber", 0.0)


def _left(S):
    S.bb("planks", -4.5, -4.2, -4.4, -3.4, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 2.0, (2.6, -4.0, 0.3), "steel", axis="X", verts=10)
    L.spark(S, 0.4, -3.4, 1.4, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(3.4, 0.2, 0.8)[0], y=anchor(3.4, 0.2, 0.8)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(3.7, -2.5, 0.7)[0], y=anchor(3.7, -2.5, 0.7)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(-1.0, -2.3, 2.4)[0], y=anchor(-1.0, -2.3, 2.4)[1], phase=2),
    dict(state="damaged", kind="smoke_plume", x=anchor(0.4, 1.5, GZ + 0.3)[0], y=anchor(0.4, 1.5, GZ + 0.3)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(3.4, 0.2, 1.0)[0], y=anchor(3.4, 0.2, 1.0)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-4.4, 4.4, -1.2, 4.4), H=GZ, boxes=[(-4.4, 4.4, YD, 4.4)], rings=[],
    clips=(2.0, 3.6), scaf=[(-5.0, -4.3, -4.7, -4.1), (0.6, 1.6, -4.7, -4.1)],
    derrick=(4.6, -3.2, 5.0, (3.6, -2.4, 5.2)), sparks=[(-2.0, -1.0, 2.3), (0.0, 1.5, 3.0), (-1.0, 3.8, 2.0)],
    hide=D.HIDE_ANIM + ("cable", "trolley", "chassis", "banner", "cwheel"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
