"""Directorate Isotope Works: shielded research laboratory (2x2 cells).  Original design.

The read is a squat LEAD-ROOFED LAB BLOCK across the north of the pad with a BANK OF GLOWING TUBES set into a recessed bay on its
south face (a row of vertical ember tubes behind a bone / oxide frame), a fat shielded cooling vent (finned lead drum on a stack) at the
north-east, and a ramp mouth under a small hood (magenta band, blast doors folded back, ember glow) at the south-west.  Two lead
containment drums with hazard rings stand on the pad, a roof dish and a loudspeaker; bone / oxide hazard bar on the south edge.
Idle loop: the tubes pulse in turn, the vent breathes ash, furnace slits flicker.  Damage is structural only (D-069): the tube bay is
smashed (tubes snapped, a drum overturned), the hood end has fallen, the vent drum has slumped off its stack; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_isotope_works"
TITLE = "Directorate Isotope Works"
FOOTPRINT = (2, 2)
CANVAS = (128, 200)
ORIGIN = (0, 72)
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
FRONT = (-3.3, -0.4, -1.6, -0.1)        # front block holding the ramp mouth
LAB = (-3.2, 3.2, -0.1, 3.1)            # main lab block
LAB_H = 2.0
BAY = (-0.1, 2.9)                       # tube bay x range on the lab's south face
VENT = (2.4, 2.1)                       # cooling vent stack centre
DRUMS = ((0.9, -2.0), (2.3, -2.4))


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def tubes(S, i, dmg, mode):
    bx0, bx1 = BAY
    yf = LAB[2]
    S.bb("bayrec", bx0 - 0.15, bx1 + 0.15, yf - 0.12, yf, 0.45, 1.85, "oxide", 0.0)
    S.bb("bayin", bx0, bx1, yf - 0.14, yf - 0.1, 0.55, 1.75, "dark", 0.0)
    S.bb("bayfloor", bx0 - 0.15, bx1 + 0.15, yf - 0.4, yf - 0.1, 0.14, 0.45, "bone", 0.0)
    n = 6
    step = (bx1 - bx0) / n
    lit = mode != "build" and not dmg
    for k in range(n):
        x = bx0 + step * (k + 0.5)
        if dmg and k in (1, 3, 4):
            S.cyl(f"tubeD{k}", 0.15, 0.4 + 0.1 * k, (x, yf - 0.3, 0.7), "oxide2", verts=10)
            continue
        on = lit and ((k + i) % 3 != 0)
        h = 1.0 if not dmg else 0.8
        S.cyl(f"glow{k}", 0.17, h, (x, yf - 0.3, 0.55 + h / 2 + 0.05), "redglow" if on else "oxide2", verts=10, nocast=True)
        S.cyl(f"tubecap{k}", 0.2, 0.08, (x, yf - 0.3, 0.55 + h + 0.12), "steel", verts=10)
        S.cyl(f"tubebase{k}", 0.2, 0.08, (x, yf - 0.3, 0.55), "steel", verts=10)
    for k in range(n + 1):
        x = bx0 + step * k
        S.bb(f"bayrib{k}", x - 0.04, x + 0.04, yf - 0.2, yf - 0.1, 0.5, 1.8, "steel", 0.0)
    S.bb("baystencil", bx0, bx1, yf - 0.14, yf - 0.1, 1.85, 2.0, "bone", 0.0)


def vent(S, i, dmg, idle):
    vx, vy = VENT
    S.bb("vbase", vx - 0.8, vx + 0.8, vy - 0.8, vy + 0.8, LAB_H + 0.2, LAB_H + 0.55, "concrete2", 0.03)
    zt = LAB_H + 0.55
    if dmg:
        S.cyl("vstumpD", 0.4, 1.0, (vx, vy, zt + 0.5), "lead", verts=14)
        S.cyl("vdrumD", 0.6, 1.0, (1.9, -2.2, 0.55), "lead", verts=16, axis="X")
        S.cyl("vdrumDd", 0.4, 1.02, (1.9, -2.2, 0.55), "dark", verts=12, axis="X")
        return
    S.cyl("vstem", 0.42, 1.0, (vx, vy, zt + 0.5), "lead", verts=14)
    S.cyl("vdrum", 0.95, 0.9, (vx, vy, zt + 1.45), "lead", verts=18)
    for k in range(3):
        S.cyl(f"vfin{k}", 1.02, 0.1, (vx, vy, zt + 1.15 + 0.3 * k), "steel", verts=18)
    S.cyl("vband", 0.97, 0.16, (vx, vy, zt + 1.45), "oxide", verts=18)
    S.cyl("vcap", 0.78, 0.14, (vx, vy, zt + 1.97), "steel", verts=16)
    S.cyl("vmouth", 0.5, 0.06, (vx, vy, zt + 2.06), "dark", verts=14)
    if idle:
        for q in range(3):
            ph = ((i / 4) + q / 3) % 1
            r = 0.14 + 0.38 * math.sin(math.pi * ph) ** 0.7
            S.sphere(f"puff{q}", r, (vx + ph * 0.9, vy, zt + 2.35 + ph * 1.2 + r * 0.3), "smoke2" if q != 1 else "smoke", squash=0.85)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    fx0, fx1, fy0, fy1 = FRONT
    lx0, lx1, ly0, ly1 = LAB
    ydoor = fy0 + 0.02
    D.pad_with_trench(S, PAD, TR, TR_YTOP, ydoor)
    D.hazard_bar(S, -3.3, 3.3, -3.28, -3.0, 0.55)
    D.ramp_entrance(S, TR, TR_YTOP, ydoor, TR_BOT, dmg, mouth_top=1.2, nst=4)
    S.bb("front", fx0, fx1, fy0, fy1, 0.14, 1.5, "concrete", 0.05)
    S.bb("fplinth", fx0 - 0.1, fx1 + 0.1, fy0 - 0.1, fy1 + 0.1, 0.14, 0.45, "concrete2", 0.04)
    S.bb("froof", fx0 - 0.1, fx1 + 0.15, fy0 - 0.2, fy1, 1.5, 1.7, "roof", 0.05)
    D.flank(S, "R", TR[1] + 0.3, fx1 - 0.1, fy0, z0=0.45, z1=1.2, stripes=2)
    D.hood(S, TR[0] - 0.25, TR[1] + 0.25, ydoor - 1.0, ydoor - 0.1, dmg, z0=1.25, z1=1.5, band_z=(1.28, 1.47), collapse=0.9)
    # lab block: concrete walls, lead roof, parapet, roof furniture
    S.bb("lab", lx0, lx1, ly0, ly1, 0.14, LAB_H, "concrete", 0.05)
    S.bb("lplinth", lx0 - 0.1, lx1 + 0.1, ly0 + 0.0, ly1 + 0.1, 0.14, 0.45, "concrete2", 0.04)
    S.bb("lroof", lx0 - 0.1, lx1 + 0.1, ly0 - 0.1, ly1 + 0.1, LAB_H, LAB_H + 0.2, "lead", 0.04)
    zr = LAB_H + 0.2
    S.bb("lparS", lx0 - 0.1, lx1 + 0.1, ly0 - 0.1, ly0 + 0.05, zr, zr + 0.15, "concrete2", 0.02)
    S.bb("lparW", lx0 - 0.1, lx0 + 0.05, ly0 - 0.1, ly1 + 0.1, zr, zr + 0.15, "concrete2", 0.02)
    S.bb("lparE", lx1 - 0.05, lx1 + 0.1, ly0 - 0.1, ly1 + 0.1, zr, zr + 0.15, "concrete2", 0.02)
    S.bb("lroofband", -2.9, -0.4, ly0 + 0.1, ly0 + 0.9, zr, zr + 0.04, "house", 0.0, nocast=True)
    S.bb("lflank", lx0 + 0.1, -0.35, ly0 - 0.1, ly0, 0.55, 1.85, "concrete2", 0.0)
    S.bb("lflankplate", lx0 + 0.25, -0.5, ly0 - 0.14, ly0 - 0.1, 0.8, 1.6, "bone", 0.0)
    for k in range(3):
        S.bb(f"sten_l{k}", lx0 + 0.35, -0.65, ly0 - 0.16, ly0 - 0.14, 0.95 + 0.22 * k, 1.07 + 0.22 * k, "dark", 0.0, nocast=True)
    D.roof_vents(S, zr, ((-2.5, 1.9, 0.6), (-1.5, 2.4, 0.8)))
    if not dmg:
        # roof dish (static) and loudspeaker
        S.cyl("rdpole", 0.08, 0.6, (0.0, 2.3, zr + 0.3), "steel", verts=8)
        S.cone("rdish", 0.08, 0.55, 0.3, (0.0, 2.3, zr + 0.75), "bone", verts=16, nocast=False)
    D.loudspeaker(S, 0.9, 1.0, zr, dmg, height=1.5)
    tubes(S, i, dmg, mode)
    vent(S, i, dmg, idle or mode == "build")
    # containment drums on the pad
    for k, (dx, dy) in enumerate(DRUMS):
        if dmg and k == 1:
            continue
        S.cyl(f"drum{k}", 0.55, 1.0 - 0.3 * k, (dx, dy, 0.14 + (1.0 - 0.3 * k) / 2), "lead", verts=14)
        S.cyl(f"drumring{k}", 0.57, 0.14, (dx, dy, 0.5), "oxide" if k == 0 else "bone", verts=14)
        S.cyl(f"drumcap{k}", 0.45, 0.06, (dx, dy, 0.17 + 1.0 - 0.3 * k), "steel", verts=12)
    D.ash_drift(S, ((1.6, -2.7, 1.0), (-2.9, 2.7, 0.6)))
    if dmg:
        L.R.scorch(S, "scB", 1.1, -0.15, 1.4, 1.2, 0.5)
        L.R.scorch(S, "scH", -1.8, -1.55, 1.4, 1.0, 0.5)


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.4, 3.1, -2.8, -2.1, 0.14, 0.7, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.6):
            S.cyl("lpipe", 0.13, 1.8, (1.0, -2.6, z), "steel", axis="X", verts=10)
        S.cyl("barrel", 0.28, 0.65, (-0.2, -2.5, 0.5), "rust", verts=12)
    S.bb("planks", 2.2, 3.2, -1.2, -0.6, 0.14, 0.4, "timber", 0.0)


def _left(S):
    S.bb("planks", 2.4, 3.0, -2.6, -2.0, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.13, 1.6, (0.6, -2.6, 0.3), "steel", axis="X", verts=10)
    L.spark(S, 0.0, -1.0, 1.2, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(1.3, -0.4, 1.0)[0], y=anchor(1.3, -0.4, 1.0)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(3.5, -3.3, 0.7)[0], y=anchor(3.5, -3.3, 0.7)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(VENT[0], VENT[1], 3.6)[0], y=anchor(VENT[0], VENT[1], 3.6)[1], phase=2),
    dict(state="damaged", kind="smoke_plume", x=anchor(1.3, -0.4, 1.4)[0], y=anchor(1.3, -0.4, 1.4)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(VENT[0], VENT[1], 3.7)[0], y=anchor(VENT[0], VENT[1], 3.7)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-3.1, 3.1, -1.5, 3.0), H=LAB_H + 2.6,
    boxes=[(-2.9, 2.9, 0.1, 2.9)], rings=[(VENT[0], VENT[1], 0.6)],
    clips=(1.5, 3.0), scaf=[(-3.1, -2.5, -3.1, -2.6), (0.2, 0.8, -3.1, -2.6)],
    derrick=(2.8, -2.4, 4.0, (1.9, -1.6, 4.2)), sparks=[(-1.0, 0.5, 1.8), (1.0, 1.0, 2.0), (-1.0, 2.2, 1.6)],
    hide=D.HIDE_ANIM + ("rdish", "rdpole", "vfin", "puff", "glow"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
