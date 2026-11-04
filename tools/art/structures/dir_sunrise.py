"""Directorate Sunrise Device: buried-warhead superweapon (3x3 cells).  Original design.

The read is a big round SILO COLLAR in the middle of the pad, ringed with bone / oxide sun-ray segments, its two hatch halves slid
apart over a glowing pit; a launch CRADLE of four steel arms holds the warhead nose (a bone-banded ogive with an ember tip) just
above the lip.  A control bunker block with a magenta roof panel and a loudspeaker sits on the north side, a tall ash vent stack at
the north-east, a short hooded service door on the west, bone / oxide hazard bar along the south edge and rays of oxide / bone
paving radiating from the collar.  The Directorate treats it as holy: a magenta banner hangs on a mast beside the cradle.
Charge state: the engine only knows idle / damaged / build, so there is no separate "ready" state.  Idle is an 8-frame SLOW CHARGE
GLOW: the pit and warhead tip pulse dim -> bright -> dim, the collar's ray segments light up one after another, ash puffs.
Damage is structural only (D-069): hatch half fallen in, cradle arm snapped, nose tilted, stack snapped; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_sunrise"
TITLE = "Directorate Sunrise Device"
FOOTPRINT = (3, 3)
CANVAS = (192, 280)
ORIGIN = (0, 88)
DOCK = (1, 3)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 8, 6), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-5.0, 5.0, -5.0, 5.0)
CX, CY = -0.6, -0.4          # silo centre
R_OUT, R_IN = 2.3, 1.55
COL_H = 0.7
BUNK = (-4.2, 1.6, 2.4, 4.8)  # control bunker (north)
BUNK_H = 1.9
STACK = (3.8, 3.4)
GLOW_CYCLE = ("oxide2", "oxide2", "oxide", "redglow", "redglow", "redglow", "oxide", "oxide2")


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def ring_pt(r, a):
    return CX + r * math.cos(a), CY + r * math.sin(a)


def silo(S, i, dmg, mode):
    ring = 20
    lit_n = (i * 20 // 8) if mode == "idle" and not dmg else 0
    for k in range(ring):
        a0 = 2 * math.pi * k / ring
        a1 = 2 * math.pi * (k + 1) / ring
        am = (a0 + a1) / 2
        x0, y0 = ring_pt(R_OUT, a0)
        x1, y1 = ring_pt(R_OUT, a1)
        if dmg and 6 <= k <= 8:
            continue
        L.beam(S, f"cw{k}", (x0, y0, COL_H / 2 + 0.14), (x1, y1, COL_H / 2 + 0.14), 0.62, "concrete" if k % 2 else "concrete2")
        # ray segment on the top of the collar: bone / oxide, with a lit one marching round
        xm, ym = ring_pt(R_OUT - 0.05, am)
        lit = (mode == "idle") and (not dmg) and ((k - lit_n) % 20 < 3)
        L.beam(S, f"cr{k}", (x0, y0, COL_H + 0.18), (x1, y1, COL_H + 0.18), 0.38, "redglow" if lit else ("oxide" if k % 2 == 0 else "bone"), nocast=lit)
    S.cyl("pit", R_IN + 0.1, 0.1, (CX, CY, 0.3), "dark", verts=24)
    g = GLOW_CYCLE[i % 8] if (mode != "build" and not dmg) else "oxide2"
    S.cyl("glowpit", R_IN - 0.35, 0.06, (CX, CY, 0.36), g, verts=24, nocast=True)
    S.cyl("glowpit2", R_IN - 0.8, 0.07, (CX, CY, 0.4), "redglow" if g == "redglow" else g, verts=20, nocast=True)
    # hatch halves slid apart (north / south), each a thick half-disc made of slabs
    for sgn in (-1, 1):
        if dmg and sgn == 1:
            S.box("hatchD", (2.6, 1.0, 0.3), (CX + 0.3, (CY - 0.2) * S.YS, 0.5), "door", 0.02, rot=(0, 0, 28), stretch=False)
            continue
        yo = CY + sgn * (R_IN + 0.05)
        for q in range(5):
            w = 2 * math.sqrt(max(R_OUT ** 2 - (sgn * (0.35 + q * 0.0)) ** 2, 0)) * 0.5
        S.bb(f"hatch{sgn}", CX - 1.9, CX + 1.9, yo if sgn > 0 else yo - 0.9, yo + 0.9 if sgn > 0 else yo, COL_H + 0.0, COL_H + 0.28, "door", 0.05)
        S.bb(f"hatchband{sgn}", CX - 1.9, CX + 1.9, (yo + 0.3) if sgn > 0 else (yo - 0.35), (yo + 0.55) if sgn > 0 else (yo - 0.1), COL_H + 0.28, COL_H + 0.32, "house" if sgn > 0 else "oxide", 0.0)
    # warhead on its cradle: pedestal, body, ogive, ember tip
    zb = 0.34
    rise = (0.0, 0.1, 0.2, 0.3, 0.35, 0.3, 0.2, 0.1)[i % 8] if mode == "idle" and not dmg else 0.2
    if dmg:
        tl = 14
        S.cyl("wbodyD", 0.62, 2.0, (CX + 0.35, CY, 1.7), "bone", verts=16, rot_z=0.0)
        S.cone("wnoseD", 0.62, 0.1, 1.2, (CX + 0.9, CY, 2.9), "steel", verts=16, nocast=False, rot=(0, tl, 0))
        S.sphere("wtipD", 0.14, (CX + 1.2, CY, 3.5), "oxide2")
    else:
        S.cyl("wbody", 0.62, 3.0, (CX, CY, zb + rise + 1.5), "bone", verts=16)
        for k, (zz, m) in enumerate(((0.6, "oxide"), (1.7, "oxide"), (2.7, "house"))):
            S.cyl(f"wband{k}", 0.64, 0.22, (CX, CY, zb + rise + zz), m, verts=16)
        S.cone("wnose", 0.62, 0.08, 1.3, (CX, CY, zb + rise + 3.0 + 0.65), "steel", verts=16, nocast=False)
        S.sphere("wtip", 0.16, (CX, CY, zb + rise + 4.4), "redglow" if g in ("redglow",) else ("oxide" if g == "oxide" else "oxide2"))
    # cradle: four arms up from the collar to a clasp ring
    for q in range(4):
        a = math.radians(q * 90 + 45)
        if dmg and q == 3:
            L.beam(S, "armD", (CX + 1.5, CY - 1.2, 0.3), (CX + 3.0, CY - 3.0, 0.3), 0.2, "steel")
            continue
        x0, y0 = ring_pt(R_OUT - 0.2, a)
        x1, y1 = ring_pt(0.7, a)
        L.beam(S, f"arm{q}", (x0, y0, COL_H + 0.2), (x1, y1, 2.1 if not dmg else 1.6), 0.2, "steel")
    if not dmg:
        for k in range(8):
            a0, a1 = k * math.pi / 4, (k + 1) * math.pi / 4
            p0, p1 = ring_pt(0.72, a0), ring_pt(0.72, a1)
            L.beam(S, f"clasp{k}", (p0[0], p0[1], 2.1), (p1[0], p1[1], 2.1), 0.16, "oxide" if k % 2 else "bone")


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    # pad, rays of paving radiating from the collar, hazard bar
    S.bb("pad", *PAD[:2], *PAD[2:], 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    D.hazard_bar(S, -5.0, 5.0, -4.9, -4.55, 0.6)
    for k in range(8):
        a = math.radians(k * 45 + 22.5)
        if k in (1, 2, 3):
            continue            # north rays are hidden by the bunker
        x0, y0 = ring_pt(R_OUT + 0.3, a)
        x1, y1 = ring_pt(R_OUT + 1.9, a)
        L.beam(S, f"ray{k}", (x0, y0, 0.15), (x1, y1, 0.15), 0.4, "bone" if k % 2 == 0 else "oxide", nocast=True)
    silo(S, i, dmg, mode)
    # control bunker with magenta roof panel and loudspeaker
    x0, x1, y0, y1 = BUNK
    S.bb("bunk", x0, x1, y0, y1, 0.14, BUNK_H, "concrete", 0.05)
    S.bb("bplinth", x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("broof", x0 - 0.1, x1 + 0.15, y0 - 0.2, y1 + 0.1, BUNK_H, BUNK_H + 0.22, "roof", 0.05)
    zr = BUNK_H + 0.22
    S.bb("bpar", x0 - 0.1, x1 + 0.15, y0 - 0.2, y0 - 0.06, zr, zr + 0.16, "concrete2", 0.02)
    S.bb("broofband", x0 + 0.4, x0 + 3.0, y0 + 0.2, y0 + 1.2, zr, zr + 0.04, "house", 0.0, nocast=True)
    S.bb("bband", x0, x1, y0 - 0.05, y0 - 0.0, 1.0, 1.35, "oxide", 0.0)
    D.flank(S, "B", x0 + 0.3, x0 + 3.0, y0 - 0.05, z0=0.55, z1=1.7)
    D.roof_vents(S, zr, ((x0 + 3.6, 3.3, 0.8),))
    D.loudspeaker(S, x1 - 0.9, 3.6, zr, dmg, height=1.7)
    # small hooded service door on the west
    S.bb("sdoor", -5.0, -4.2, -2.4, -0.4, 0.14, 1.5, "concrete", 0.04)
    S.bb("sdhood", -5.05, -3.9, -2.55, -0.3, 1.5, 1.75, "concrete2", 0.04)
    S.bb("sdband", -5.05, -3.9, -2.58, -2.55, 1.52, 1.72, "house", 0.0)
    S.bb("sddark", -4.95, -4.25, -2.43, -2.4, 0.2, 1.2, "dark", 0.0)
    # ash vent stack (NE)
    sx, sy = STACK
    S.bb("furn", sx - 0.9, sx + 0.9, sy - 0.8, sy + 0.8, 0.14, 1.3, "concrete2", 0.04)
    S.bb("furnroof", sx - 1.0, sx + 1.0, sy - 0.9, sy + 0.9, 1.3, 1.45, "roof", 0.04)
    S.bb("furnband", sx - 0.9, sx + 0.9, sy - 0.84, sy - 0.8, 0.9, 1.1, "oxide", 0.0)
    D.furnace_slits(S, sx - 0.6, sy - 0.9, 0.35, 0.75, 3, i + 1, (idle or mode == "build") and not dmg, step=0.45, w=0.25)
    D.chimney(S, sx, sy, 1.45, 3.3 if dmg else 4.4, dmg, idle or mode == "build", i % 4, rise=1.5, lean_len=1.3)
    # banner mast beside the cradle
    if not dmg:
        S.cyl("bmast", 0.07, 4.2, (2.6, -0.9, 2.2), "steel", verts=8)
        S.bb("bmbase", 2.35, 2.85, -1.15, -0.65, 0.14, 0.5, "concrete2", 0.02)
        S.bb("bbar", 2.3, 2.9, -0.93, -0.87, 4.1, 4.2, "steel", 0.0)
        S.bb("banner", 2.35, 2.85, -0.97, -0.93, 2.1, 4.1, "house", 0.0)
        S.bb("bannerT", 2.35, 2.85, -0.99, -0.97, 3.7, 4.1, "oxide", 0.0)
        S.bb("bannerB", 2.35, 2.85, -0.99, -0.97, 2.1, 2.4, "bone", 0.0)
    D.ash_drift(S, ((3.9, -2.6, 1.3), (-4.4, 0.6, 0.8)))
    if dmg:
        L.R.scorch(S, "scC", CX, CY - 2.8, 0.8, 1.6, 0.6)
        L.R.scorch(S, "scB", -2.0, 2.3, 1.2, 1.4, 0.5)


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 3.4, 4.2, -4.4, -3.6, 0.14, 0.8, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (1.6, -3.9, z), "steel", axis="X", verts=10)
        S.cyl("barrel", 0.3, 0.7, (4.3, -2.6, 0.5), "rust", verts=12)
    S.bb("planks", -4.6, -3.2, -4.4, -3.8, 0.14, 0.5, "timber", 0.0)


def _left(S):
    S.bb("planks", -4.5, -4.2, -4.4, -3.4, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 2.0, (2.6, -4.0, 0.3), "steel", axis="X", verts=10)
    L.spark(S, 0.4, -3.4, 1.4, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(CX, CY + 1.0, 1.0)[0], y=anchor(CX, CY + 1.0, 1.0)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(CX + 2.4, CY - 2.8, 0.5)[0], y=anchor(CX + 2.4, CY - 2.8, 0.5)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(CX + 0.9, CY, 3.0)[0], y=anchor(CX + 0.9, CY, 3.0)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=anchor(STACK[0], STACK[1], 3.3)[0], y=anchor(STACK[0], STACK[1], 3.3)[1], phase=6),
    dict(state="damaged", kind="smoke_plume", x=anchor(CX, CY, 1.2)[0], y=anchor(CX, CY, 1.2)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(STACK[0], STACK[1], 3.4)[0], y=anchor(STACK[0], STACK[1], 3.4)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-4.8, 4.8, -4.0, 4.8), H=4.4,
    boxes=[(BUNK[0] + 0.2, BUNK[1] - 0.2, BUNK[2] + 0.2, BUNK[3] - 0.2)], rings=[(CX, CY, R_OUT - 0.2), (STACK[0], STACK[1], 0.7)],
    clips=(2.0, 3.6), scaf=[(-5.0, -4.3, -4.7, -4.1), (0.6, 1.6, -4.7, -4.1)],
    derrick=(4.6, -3.2, 5.0, (3.6, -2.4, 5.2)), sparks=[(-2.0, 2.0, 1.8), (CX, CY, 2.0), (3.0, 3.0, 2.0)],
    hide=D.HIDE_ANIM + ("glowpit", "wtip", "bmast", "banner", "bbar", "cr", "wband", "wbody", "wnose", "arm", "clasp"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
