"""Directorate Cistern: a buried concentrate tank (2x2 cells).  Original design.

Almost all of it is underground: what shows is a broad round concrete lid flush with the pad (bone / oxide rim bands, a cross of
ribs, a magenta house plate), a small gatehouse at the south-west corner with a narrow dark hatch door under a magenta-banded
hood, two snorkel vents (one breathes ash), a fill pipe with a valve wheel on the east side and a level gauge whose lamp blinks.
Idle loop: ash puffs from a vent, the gauge lamp blinks, the hatch ember flickers.  Damage is structural only (D-069): the lid has
caved in on the east half (dark pit, an upturned slab, rebar), the hood end has fallen, one vent is snapped, the fill pipe is
broken and the gauge leans; fire / smoke are FX anchors.  Build stages: the shared L.stages() driver.
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

ID = "dir_cistern"
TITLE = "Directorate Cistern"
FOOTPRINT = (2, 2)
CANVAS = (128, 168)
ORIGIN = (0, 36)
DOCK = (0, 0)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = DD.MATERIALS
SOOT_SKIN = DD.SOOT_SKIN
PRIMER_SKIN = DD.PRIMER_SKIN

PAD = (-3.3, 3.3, -3.3, 3.3)
LC, LR, LZ = (0.3, 1.0), 2.3, 0.75       # lid centre, radius, top height
GATE = (-3.0, -1.1, -2.3, -0.6)          # gatehouse x0, x1, y0, y1
GATE_H = 1.55
VENTS = ((-0.9, 2.4), (1.9, 2.5))
GAUGE = (2.85, -2.1)


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    YS = S.YS
    if dmg:
        S.skin.update(SOOT_SKIN)
    cx, cy = LC
    # ---- pad and hazard bar
    S.bb("pad", PAD[0], PAD[1], PAD[2], PAD[3], 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", PAD[0], PAD[1], PAD[2], PAD[2] + 0.1, 0.14, 0.2, "concrete2", 0.0)
    D.hazard_bar(S, PAD[0] + 0.1, PAD[1] - 0.1, -3.18, -2.85, 0.6)
    # ---- lid: stepped round slab, rim bands, ribs
    S.cyl("lidstep", LR + 0.18, 0.3, (cx, cy, 0.14 + 0.15), "concrete2", verts=32)
    S.cyl("lidband1", LR + 0.1, 0.12, (cx, cy, 0.5), "oxide", verts=32)
    S.cyl("lidband2", LR + 0.1, 0.12, (cx, cy, 0.62), "bone", verts=32)
    S.cyl("lid", LR, LZ - 0.14, (cx, cy, 0.14 + (LZ - 0.14) / 2), "concrete", verts=32)
    S.bb("ribx", cx - LR + 0.2, cx + LR - 0.2, cy - 0.09, cy + 0.09, LZ, LZ + 0.05, "concrete2", 0.0)
    S.bb("riby", cx - 0.09, cx + 0.09, cy - LR + 0.2, cy + LR - 0.2, LZ, LZ + 0.05, "concrete2", 0.0)
    S.cyl("lidhub", 0.4, 0.1, (cx, cy, LZ + 0.05), "steel", verts=14)
    S.cyl("lidhubd", 0.24, 0.12, (cx, cy, LZ + 0.06), "dark", verts=12)
    if not dmg:
        S.bb("plate", cx + 0.7, cx + 1.9, cy - 1.3, cy - 0.3, LZ, LZ + 0.05, "house2", 0.0)
        S.bb("platet", cx + 0.8, cx + 1.8, cy - 0.45, cy - 0.38, LZ + 0.05, LZ + 0.07, "dark", 0.0, nocast=True)
    # ---- gatehouse with the narrow hatch
    gx0, gx1, gy0, gy1 = GATE
    S.bb("gate", gx0, gx1, gy0, gy1, 0.14, GATE_H, "concrete", 0.05)
    S.bb("gplinth", gx0 - 0.1, gx1 + 0.1, gy0 - 0.1, gy1, 0.14, 0.45, "concrete2", 0.04)
    S.bb("groof", gx0 - 0.1, gx1 + 0.12, gy0 - 0.15, gy1 + 0.05, GATE_H, GATE_H + 0.2, "roof", 0.04)
    S.bb("gpara", gx0 - 0.1, gx1 + 0.12, gy0 - 0.15, gy0 - 0.03, GATE_H + 0.2, GATE_H + 0.34, "concrete2", 0.02)
    D.roof_vents(S, GATE_H + 0.2, ((gx1 - 0.5, gy1 - 0.5, 0.5),))
    yd = gy0 - 0.02
    dx0, dx1 = -2.4, -1.6
    S.bb("hatchf", dx0 - 0.1, dx1 + 0.1, yd - 0.06, yd + 0.04, 0.14, 1.22, "dark", 0.0)
    S.bb("hatchin", dx0, dx1, yd - 0.08, yd - 0.06, 0.2, 1.12, "dark", 0.0)
    g = "oxide2" if dmg else ("redglow" if (i % 2 == 0 or not idle) else "oxide")
    S.bb("hatchglow", dx0 + 0.15, dx1 - 0.15, yd - 0.1, yd - 0.08, 0.3, 0.5, g, 0.0, nocast=True)
    S.box("hatchdoor", (0.5, 0.08, 1.05), (dx1 + 0.45, (yd - 0.28) * YS, 0.7), "door", 0.0, rot=(0, 0, 55), stretch=False)
    D.flank(S, "G", gx0 + 0.15, dx0 - 0.25, yd + 0.04, z0=0.5, z1=1.4, stripes=2)
    D.hood(S, dx0 - 0.3, dx1 + 0.3, yd - 0.8, yd - 0.05, dmg, z0=1.25, z1=1.52, band_z=(1.28, 1.49), collapse=1.0)
    # ---- snorkel vents
    for k, (vx, vy) in enumerate(VENTS):
        broken = dmg and k == 1
        h = 0.9 if broken else 1.5
        S.cyl(f"vbase{k}", 0.34, 0.2, (vx, vy, LZ + 0.1), "concrete2", verts=12)
        S.cyl(f"vent{k}", 0.2, h, (vx, vy, LZ + h / 2), "hull2", verts=12)
        S.cyl(f"vband{k}", 0.22, 0.14, (vx, vy, LZ + 0.55), "oxide", verts=12)
        if broken:
            S.cyl(f"vtop{k}", 0.2, 0.06, (vx, vy, LZ + h + 0.02), "dark", verts=10)
            S.box("ventfall", (0.4, 0.4, 1.0), (vx + 0.9, (vy - 0.5) * YS, LZ + 0.25), "hull2", 0.0, rot=(0, 78, 30), stretch=False)
            continue
        S.cyl(f"vcap{k}", 0.3, 0.12, (vx, vy, LZ + h + 0.06), "steel", verts=12)
        S.cyl(f"vmouth{k}", 0.2, 0.05, (vx, vy, LZ + h + 0.13), "dark", verts=10)
        S.cyl(f"vband2{k}", 0.22, 0.12, (vx, vy, LZ + 1.0), "bone", verts=12)
        if k == 0 and idle:
            for q in range(3):
                ph = ((i / 4) + q / 3) % 1
                r = 0.12 + 0.34 * math.sin(math.pi * ph) ** 0.7
                S.sphere(f"puff{q}", r, (vx + ph * 0.9, vy, LZ + h + 0.4 + ph * 0.6), "smoke2" if q != 1 else "smoke", squash=0.85)
    # ---- fill pipe and valve on the east side
    px = 3.0
    if dmg:
        S.cyl("pipeA", 0.16, 0.8, (2.5, cy - 0.5, 0.5), "steel", axis="X", verts=10)
        S.cyl("pipeB", 0.16, 0.6, (3.0, -0.4, 0.3), "steel", axis="Y", verts=10)
    else:
        S.cyl("pipe", 0.16, 1.2, (2.75, cy - 0.5, 0.5), "steel", axis="X", verts=10)
        S.cyl("flange", 0.24, 0.1, (3.25, cy - 0.5, 0.5), "rust", axis="X", verts=10)
        S.bb("valve", 2.5, 2.8, cy - 0.7, cy - 0.3, 0.14, 0.8, "hull2", 0.03)
        S.cyl("wheel", 0.24, 0.06, (2.65, cy - 0.75, 0.62), "bone", axis="Y", verts=14)
    # ---- level gauge: post with a scale plate and a blinking lamp
    gx, gy = GAUGE
    if dmg:
        S.box("gpost", (0.14, 0.14, 1.5), (gx + 0.35, gy * YS, 0.8), "dark", 0.0, rot=(0, 26, 0), stretch=False)
        S.bb("gbase", gx - 0.2, gx + 0.2, gy - 0.2, gy + 0.2, 0.14, 0.4, "concrete2", 0.02)
    else:
        S.bb("gbase", gx - 0.2, gx + 0.2, gy - 0.2, gy + 0.2, 0.14, 0.4, "concrete2", 0.02)
        S.bb("gpost", gx - 0.07, gx + 0.07, gy - 0.07, gy + 0.07, 0.4, 1.5, "dark", 0.0)
        S.bb("gscale", gx - 0.3, gx - 0.07, gy - 0.1, gy - 0.07, 0.55, 1.35, "bone", 0.0)
        S.bb("gband", gx - 0.3, gx - 0.07, gy - 0.11, gy - 0.1, 0.95, 1.05, "oxide", 0.0, nocast=True)
        S.bb("glamp", gx - 0.12, gx + 0.12, gy - 0.12, gy + 0.12, 1.5, 1.72, "redglow" if (idle and i % 2 == 0) else "oxide2", 0.0, nocast=True)
    # ---- ash and damage
    D.ash_drift(S, ((2.9, -1.0, 0.9), (-3.0, 2.6, 0.8), (0.6, -2.7, 0.9)))
    if dmg:
        S.bb("pit", cx + 0.2, cx + 2.0, cy - 0.3, cy + 1.9, LZ, LZ + 0.06, "dark", 0.0)
        S.bb("pitg", cx + 0.6, cx + 1.4, cy + 0.1, cy + 0.6, LZ + 0.06, LZ + 0.08, "oxide2", 0.0, nocast=True)
        S.box("slab", (1.4, 1.0, 0.2), (cx + 0.7, (cy + 0.1) * YS, LZ + 0.35), "concrete2", 0.0, rot=(0, 20, -18), stretch=False)
        S.box("slab2", (0.9, 0.7, 0.18), (cx + 1.9, (cy - 0.6) * YS, LZ + 0.25), "concrete", 0.0, rot=(18, 0, 30), stretch=False)
        S.bb("plated", cx + 0.2, cx + 0.9, cy - 1.2, cy - 0.4, LZ, LZ + 0.05, "house2", 0.0)
        L.beam(S, "rbar0", (cx + 0.4, cy + 0.9, LZ + 0.1), (cx + 0.9, cy + 1.0, LZ + 1.0), 0.05, "rebar")
        L.beam(S, "rbar1", (cx + 1.2, cy + 1.2, LZ + 0.1), (cx + 1.5, cy + 1.1, LZ + 0.8), 0.05, "rebar")
        for k, (x, y, w, h) in enumerate(((cx + 2.2, cy + 1.2, 0.4, 0.3), (cx - 0.2, cy + 1.7, 0.35, 0.25), (cx + 2.5, cy + 0.1, 0.3, 0.22))):
            S.box(f"rub{k}", (w, w * 0.8, h), (x, y * YS, LZ + h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k), stretch=False)
        L.R.scorch(S, "scL", cx + 0.6, cy + 0.8, 1.6, 1.8, 0.6)


# ---------------------------------------------------------------------------------------------- FX / build
FX = [
    dict(state="damaged", kind="fire_large", x=anchor(LC[0] + 1.1, LC[1] + 0.8, LZ + 0.2)[0], y=anchor(LC[0] + 1.1, LC[1] + 0.8, LZ + 0.2)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(LC[0] + 1.1, LC[1] + 0.8, LZ + 0.4)[0], y=anchor(LC[0] + 1.1, LC[1] + 0.8, LZ + 0.4)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=anchor(VENTS[1][0], VENTS[1][1], LZ + 1.0)[0], y=anchor(VENTS[1][0], VENTS[1][1], LZ + 1.0)[1], phase=5),
]

SPEC = DD.stage_spec(PAD, (-2.0, 2.6, -1.3, 3.3), 2.0, [GATE], [(LC[0], LC[1], LR - 0.2)],
                     [(0.5, 1.4, -2.9, -2.3), (2.0, 2.8, -3.0, -2.5)], (3.0, -1.3, 3.4, (2.3, -0.5, 3.6)),
                     [(-2.0, -1.5, 1.5), (0.3, 1.0, 1.2)], (0.9, 1.5), hide=("wheel", "glamp", "hatchglow", "hatchdoor"))


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
