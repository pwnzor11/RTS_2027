"""Directorate Radiation Mortar: a buried mortar tube lobbing shells of irradiated slurry (2x2 cells).  Original design.

The read is a DEEP CONCRETE EMPLACEMENT SUNK IN THE PAD: a big stepped octagonal-looking round collar (umber, bone / oxide bands)
around a dark pit, out of which a fat mortar tube rises at a steep angle, pitched north (bone and oxide bands, a flared dark
muzzle).  A small sickly-green glow sits inside the muzzle and in a sight-glass on the slurry tank beside it (kept small; the
rest is umber and ember red).  Around the pit: a low control block with a magenta house panel and a hazard-striped hatch, two
slurry drums with hazard rings (green sight-glass), a shell rack and a stub vent that breathes ash; bone / oxide hazard bar south.
Static (fixed buried tube, no turret given by the rules).  Idle loop: the muzzle glow pulses, the sight-glass pulses, ash puffs.
Damage is structural only (D-069): the tube is cracked off and lies across the collar, the collar is breached, a drum is split
(dark slurry spill), the control block roof has caved; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_def as DD
D = DD.D
L = DD.L

ID = "dir_rad_mortar"
TITLE = "Directorate Radiation Mortar"
FOOTPRINT = (2, 2)
CANVAS = (128, 184)
ORIGIN = (0, 52)
DOCK = (0, 0)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = DD.MATERIALS
SOOT_SKIN = DD.SOOT_SKIN
PRIMER_SKIN = DD.PRIMER_SKIN

PAD = (-3.3, 3.3, -3.3, 3.3)
PC = (0.2, 0.5)               # pit centre
PR = 1.9                      # collar radius
PIV = (PC[0], PC[1] + 0.1, 0.9)
PITCH = 56.0
BLK = (-3.0, -0.9, -2.5, -0.9)   # control block
BH = 1.4


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def tube(S, i, dmg, idle):
    p3 = DD.p3
    if dmg:
        # tube cracked off at the base and lying across the collar, muzzle on the pad
        p3(S, "stub", (0.8, 0.9, 0.8), PIV, (0.0, 0.3, 0.0), "hull2", 68.0, 0.0, 0.05)
        S.box("tubefall", (0.7, 2.2, 0.7), (PC[0] + 1.6, (PC[1] - 1.3) * S.YS, 0.45), "bone", 0.05, rot=(0, 0, 58), stretch=False)
        S.box("tubefallb", (0.76, 0.14, 0.76), (PC[0] + 1.0, (PC[1] - 0.4) * S.YS, 0.45), "oxide", 0.0, rot=(0, 0, 58), stretch=False, nocast=True)
        S.box("tubefallm", (0.5, 0.1, 0.5), (PC[0] + 2.2, (PC[1] - 2.1) * S.YS, 0.46), "dark", 0.0, rot=(0, 0, 58), stretch=False, nocast=True)
        return
    # trunnion yoke
    for sx in (-0.65, 0.65):
        S.bb(f"yoke", PC[0] + sx - 0.12, PC[0] + sx + 0.12, PC[1] - 0.3, PC[1] + 0.3, 0.55, 1.15, "steel", 0.03)
    p3(S, "tube", (0.78, 2.5, 0.78), PIV, (0.0, 1.0, 0.0), "oxide2", PITCH, 0.0, 0.08)
    p3(S, "tubeb1", (0.84, 0.2, 0.84), PIV, (0.0, 0.5, 0.0), "dark", PITCH, 0.0, 0.0, nocast=True)
    p3(S, "tubeb2", (0.84, 0.2, 0.84), PIV, (0.0, 1.35, 0.0), "dark", PITCH, 0.0, 0.0, nocast=True)
    p3(S, "tubeh", (0.96, 0.34, 0.96), PIV, (0.0, 2.25, 0.0), "steel", PITCH, 0.0, 0.05)
    p3(S, "mring1", (1.02, 0.12, 1.02), PIV, (0.0, 2.05, 0.0), "bone", PITCH, 0.0, 0.0, nocast=True)
    p3(S, "mring2", (1.02, 0.12, 1.02), PIV, (0.0, 2.42, 0.0), "bone", PITCH, 0.0, 0.0, nocast=True)
    p3(S, "mouthrim", (0.8, 0.06, 0.8), PIV, (0.0, 2.49, 0.0), "dark", PITCH, 0.0, 0.0, nocast=True)
    on = idle and (i % 2 == 0)
    p3(S, "glowm", (0.56, 0.06, 0.56), PIV, (0.0, 2.53, 0.0), "redglow" if (idle and i % 4 < 3) else "oxide2", PITCH, 0.0, 0.0, nocast=True)
    p3(S, "glowg", (0.3, 0.07, 0.3), PIV, (0.0, 2.55, 0.0), "sickly" if on else "sickly_dim", PITCH, 0.0, 0.0, nocast=True)
    p3(S, "breech", (0.9, 0.35, 0.9), PIV, (0.0, -0.1, 0.0), "gunmetal", PITCH, 0.0, 0.05)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    YS = S.YS
    if dmg:
        S.skin.update(SOOT_SKIN)
    cx, cy = PC
    S.bb("pad", PAD[0], PAD[1], PAD[2], PAD[3], 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", PAD[0], PAD[1], PAD[2], PAD[2] + 0.1, 0.14, 0.2, "concrete2", 0.0)
    D.hazard_bar(S, PAD[0] + 0.1, PAD[1] - 0.1, -3.18, -2.85, 0.6)
    # collar: stepped, banded; pit inside
    S.cyl("step", PR + 0.25, 0.3, (cx, cy, 0.29), "concrete2", verts=32)
    S.cyl("collar", PR, 0.5, (cx, cy, 0.39), "concrete", verts=32)
    S.cyl("band1", PR + 0.04, 0.1, (cx, cy, 0.42), "oxide", verts=32)
    S.cyl("band2", PR + 0.04, 0.1, (cx, cy, 0.58), "bone", verts=32)
    S.cyl("top", PR - 0.15, 0.08, (cx, cy, 0.66), "concrete2", verts=32)
    S.cyl("pit", 1.15, 0.06, (cx, cy, 0.72), "dark", verts=24)
    S.cyl("pitring", 1.2, 0.08, (cx, cy, 0.71), "steel", verts=24)
    S.cyl("pitg", 1.0, 0.04, (cx, cy, 0.75), "dark", verts=24)
    for k in range(8):                       # hazard ticks on the step
        a = math.radians(k * 45 + 22.5)
        S.box(f"tick{k}", (0.45, 0.16, 0.04), (cx + (PR + 0.12) * math.sin(a), (cy + (PR + 0.12) * math.cos(a)) * YS, 0.46), "oxide" if k % 2 == 0 else "bone", 0.0,
              rot=(0, 0, -math.degrees(a)), stretch=False, nocast=True)
    S.cyl("pivpost", 0.4, 0.4, (cx, cy, 0.8), "hull2", verts=12)
    tube(S, i, dmg, idle)
    # control block with magenta house panel and hatch
    bx0, bx1, by0, by1 = BLK
    S.bb("blk", bx0, bx1, by0, by1, 0.14, BH, "concrete", 0.05)
    S.bb("blkplinth", bx0 - 0.08, bx1 + 0.08, by0 - 0.08, by1, 0.14, 0.4, "concrete2", 0.03)
    if dmg:
        S.bb("blkroofW", bx0 - 0.1, (bx0 + bx1) / 2, by0 - 0.1, by1 + 0.05, BH, BH + 0.18, "roof", 0.04)
        S.bb("blkpit", (bx0 + bx1) / 2, bx1, by0 + 0.2, by1 - 0.1, BH - 0.35, BH, "dark", 0.0)
        S.bb("blkroofE", (bx0 + bx1) / 2 + 0.1, bx1 + 0.1, by0 - 0.1, by0 + 0.5, BH - 0.5, BH - 0.3, "roof", 0.04, rot=(8, 0, 6))
    else:
        S.bb("blkroof", bx0 - 0.1, bx1 + 0.1, by0 - 0.1, by1 + 0.05, BH, BH + 0.18, "roof", 0.04)
        S.bb("blkhouse", bx0 + 0.2, bx1 - 0.2, by1 - 0.9, by1 - 0.35, BH + 0.18, BH + 0.22, "house2", 0.0, nocast=True)
    yw = by0 - 0.02
    S.bb("hatchf", bx0 + 0.3, bx0 + 1.0, yw - 0.08, yw + 0.04, 0.14, 1.0, "dark", 0.0)
    S.bb("hatchd", bx0 + 0.38, bx0 + 0.92, yw - 0.12, yw - 0.08, 0.2, 0.94, "door", 0.0)
    S.bb("hatchbar", bx0 + 0.38, bx0 + 0.92, yw - 0.14, yw - 0.12, 0.5, 0.58, "oxide", 0.0, nocast=True)
    S.bb("panel", bx0 + 1.1, bx1 - 0.1, yw - 0.1, yw - 0.02, 0.35, 1.1, "house", 0.0)
    S.bb("panelf", bx0 + 1.05, bx1 - 0.05, yw - 0.05, yw, 0.3, 1.15, "dark", 0.0)
    # slurry drums with hazard rings, one sight-glass each (small green accent)
    for k, (dx, dy) in enumerate(((2.55, -1.2), (2.55, -2.35))):
        if dmg and k == 1:
            S.box("drumsplit", (0.95, 0.5, 0.5), (dx + 0.5, dy * YS, 0.4), "lead", 0.0, rot=(0, 0, 70), stretch=False)
            S.cyl("spill", 0.6, 0.03, (dx - 0.1, dy - 0.2, 0.16), "dark", verts=14, nocast=True)
            continue
        S.cyl(f"drum{k}", 0.5, 1.0, (dx, dy, 0.64), "lead", verts=14)
        S.cyl(f"drumr{k}", 0.52, 0.14, (dx, dy, 0.45), "oxide" if k == 0 else "bone", verts=14)
        S.cyl(f"drumr2{k}", 0.52, 0.14, (dx, dy, 0.95), "bone" if k == 0 else "oxide", verts=14)
        S.cyl(f"drumc{k}", 0.4, 0.06, (dx, dy, 1.17), "steel", verts=12)
        S.bb(f"glass{k}", dx - 0.22, dx + 0.22, dy - 0.56, dy - 0.48, 0.55, 0.92, "sickly" if (idle and (i + k) % 2 == 1) else "sickly", 0.0, nocast=True)
    # shell rack: three upright shells in a frame, west of the pit
    sx0 = -2.9
    for k in range(3):
        S.cyl(f"shell{k}", 0.18, 0.7, (sx0 + 0.4 * k, 2.4, 0.5), "bone", verts=10)
        S.cyl(f"shellb{k}", 0.19, 0.1, (sx0 + 0.4 * k, 2.4, 0.7), "oxide", verts=10)
        S.cyl(f"shelln{k}", 0.16, 0.26, (sx0 + 0.4 * k, 2.4, 0.97), "sickly", verts=10)
    S.bb("rackf", sx0 - 0.3, sx0 + 1.1, 2.1, 2.7, 0.14, 0.3, "steel", 0.02)
    # ash vent stub (breathes ash)
    vx, vy = 2.8, 2.3
    S.cyl("vbase", 0.34, 0.2, (vx, vy, 0.24), "concrete2", verts=12)
    S.cyl("vent", 0.22, 1.1, (vx, vy, 0.7), "hull2", verts=12)
    S.cyl("vband", 0.24, 0.12, (vx, vy, 0.55), "oxide", verts=12)
    S.cyl("vcap", 0.32, 0.1, (vx, vy, 1.3), "steel", verts=12)
    S.cyl("vmouth", 0.2, 0.05, (vx, vy, 1.36), "dark", verts=10)
    if idle and not dmg:
        for q in range(3):
            ph = ((i / 4) + q / 3) % 1
            r = 0.12 + 0.3 * math.sin(math.pi * ph) ** 0.7
            S.sphere(f"puff{q}", r, (vx + ph * 0.7, vy, 1.6 + ph * 0.6), "smoke2" if q != 1 else "smoke", squash=0.85)
    D.ash_drift(S, ((-2.2, 1.0, 0.8), (2.5, 0.4, 0.8)))
    if dmg:
        S.bb("breach", cx + 0.9, cx + 1.9, cy - 0.7, cy + 0.2, 0.66, 0.74, "dark", 0.0)
        for k, (x, y, w, h) in enumerate(((cx + 1.7, cy + 1.7, 0.45, 0.3), (cx - 1.7, cy - 1.3, 0.4, 0.26), (cx + 2.3, cy + 0.3, 0.35, 0.24))):
            S.box(f"rub{k}", (w, w * 0.8, h), (x, y * YS, 0.14 + h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k), stretch=False)
        L.R.scorch(S, "scM", cx, cy - 0.2, 2.2, 2.2, 0.8)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(PC[0], PC[1] + 0.3, 1.0)[0], y=anchor(PC[0], PC[1] + 0.3, 1.0)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(PC[0], PC[1] + 0.3, 1.2)[0], y=anchor(PC[0], PC[1] + 0.3, 1.2)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=anchor(2.55, -2.35, 0.5)[0], y=anchor(2.55, -2.35, 0.5)[1], phase=5),
]

SPEC = DD.stage_spec(PAD, (-1.8, 2.8, -1.0, 2.8), 3.4, [BLK], [(PC[0], PC[1], 1.6)],
                     [(-2.9, -2.2, -3.0, -2.6), (0.6, 1.4, -2.9, -2.5)], (3.0, 1.2, 3.6, (2.0, 1.0, 3.8)),
                     [(-1.8, -1.0, 1.2), (PC[0], PC[1], 1.8)], (1.2, 2.2), hide=("glow", "puff"))


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
