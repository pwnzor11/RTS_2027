"""Directorate Reactor Vent: the power source (2x2 cells).  Original design.

The read is a fat COOLING STACK: a waisted umber concrete tower (bone / oxide bands, strap ribs) rising from a buried reactor, its mouth
a dark ring with a deep ember glow and a pale heat haze pouring out.  A squat reactor hall sits to the west (magenta house panel on its
south face, service door with an ember strip, intake louvres, loudspeaker pair on the roof), two coolant pipes cross to the stack, and a
low heat-exchanger block with ember slits to the south-east.  Bone / oxide hazard bar along the south edge.
Idle loop: haze rises and swells, ember glow pulses.  Damage is structural only (D-069): the stack top has snapped off (a chunk lies on
the pad), a coolant pipe is torn out, the panel is hanging; fire / smoke are FX anchors.  Build stages: the shared L.stages() driver.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_reactor_vent"
TITLE = "Directorate Reactor Vent"
FOOTPRINT = (2, 2)
CANVAS = (128, 232)
ORIGIN = (0, 88)        # headroom for the cooling stack and its haze
DOCK = (0, 0)           # no dock (power)
CAP_MATERIAL = D.CAP_MATERIAL
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-3.3, 3.3, -3.3, 3.3)
SX, SY = 1.2, 0.7                      # stack centre
PROFILE = ((0.14, 1.45), (1.0, 1.3), (2.6, 0.95), (4.4, 1.1))   # (z, radius)
TOP = 4.4
HALL = (-3.1, -0.7, -1.7, 2.9)
HALL_H = 1.7
EXCH = (0.6, 3.0, -2.7, -1.45)


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def rad(z):
    for (za, ra), (zb, rb) in zip(PROFILE, PROFILE[1:]):
        if za <= z <= zb:
            return ra + (rb - ra) * (z - za) / (zb - za)
    return PROFILE[-1][1]


def stack(S, i, dmg, lit, mode):
    top = 3.3 if dmg else TOP
    for k, ((za, ra), (zb, rb)) in enumerate(zip(PROFILE, PROFILE[1:])):
        if za >= top:
            break
        zt = min(zb, top)
        r2 = ra + (rb - ra) * (zt - za) / (zb - za)
        S.cone(f"stack{k}", ra, r2, zt - za, (SX, SY, (za + zt) / 2), "concrete" if k else "concrete2", verts=20, nocast=False)
    # bone / oxide bands
    zz, k = 0.9, 0
    while zz < top - 0.1:
        r = rad(zz) + 0.04
        S.cone(f"sband{k}", r, r, 0.2, (SX, SY, zz), "bone" if k % 2 == 0 else "oxide", verts=20, nocast=False)
        zz += 0.85
        k += 1
    # strap ribs on the south half
    for a in (-55, -20, 20, 55):
        sa, ca = math.sin(math.radians(a)), math.cos(math.radians(a))
        z0, z1 = 0.4, min(top - 0.3, 2.9)
        L.beam(S, "rib", (SX + sa * (rad(z0) + 0.05), SY - ca * (rad(z0) + 0.05), z0), (SX + sa * (rad(z1) + 0.05), SY - ca * (rad(z1) + 0.05), z1), 0.1, "hull2")
    # ember slits at the base
    for k in range(5):
        dx = -0.9 + k * 0.45
        y = SY - math.sqrt(max(rad(0.5) ** 2 - dx * dx, 0.01)) - 0.02
        on = lit and ((k + i) % 3 != 0)
        S.bb(f"glow{k}", SX + dx - 0.12, SX + dx + 0.12, y - 0.06, y + 0.06, 0.3, 0.7, "redglow" if on else "oxide2", 0.0, nocast=True)
    if dmg:
        # jagged broken top and the fallen chunk lying on the pad
        for k in range(5):
            a = k * 72 + 20
            S.bb(f"tooth{k}", SX + 0.7 * math.cos(math.radians(a)) - 0.12, SX + 0.7 * math.cos(math.radians(a)) + 0.12,
                 SY + 0.7 * math.sin(math.radians(a)) - 0.12, SY + 0.7 * math.sin(math.radians(a)) + 0.12, top - 0.1, top + 0.15 + 0.12 * (k % 3), "concrete2", 0.0)
        S.cone("chunk", 0.8, 0.95, 1.5, (2.6, -2.5, 0.9), "concrete", verts=14, nocast=False, rot=(0, 78, 20))
        S.cone("chunkb", 0.84, 0.84, 0.2, (2.4, -2.45, 0.75), "oxide", verts=14, nocast=False, rot=(0, 78, 20))
        return
    # mouth: steel collar, dark throat, deep ember glow, haze
    S.cyl("collar", 1.22, 0.22, (SX, SY, TOP + 0.11), "steel", verts=22)
    S.cyl("throat", 1.0, 0.08, (SX, SY, TOP + 0.24), "dark", verts=20)
    S.cyl("glowm", 0.72, 0.06, (SX, SY, TOP + 0.27), "redglow" if (i % 2 == 0 or mode == "build") else "oxide", verts=16, nocast=True)
    if mode != "build":
        for q in range(4):
            ph = ((i / 4) + q / 4) % 1
            r = 0.2 + 0.4 * math.sin(math.pi * ph) ** 0.6
            S.sphere(f"puff{q}", r, (SX + 0.1 + ph * 0.9, SY, TOP + 0.5 + ph * 1.6), "smoke2", squash=0.6)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    lit = (idle or mode == "build") and not dmg
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    x0, x1, y0, y1 = HALL
    S.bb("pad", PAD[0], PAD[1], PAD[2], PAD[3], 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", PAD[0], PAD[1], PAD[2], PAD[2] + 0.1, 0.14, 0.2, "concrete2", 0.0)
    D.hazard_bar(S, -3.3, 3.3, -3.25, -2.95, 0.6)
    # reactor hall
    S.bb("hall", x0, x1, y0, y1, 0.14, HALL_H, "concrete", 0.05)
    S.bb("hplinth", x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("hroof", x0 - 0.1, x1 + 0.15, y0 - 0.2, y1 + 0.1, HALL_H, HALL_H + 0.22, "roof", 0.05)
    zr = HALL_H + 0.22
    S.bb("hparS", x0 - 0.1, x1 + 0.15, y0 - 0.2, y0 - 0.06, zr, zr + 0.16, "concrete2", 0.02)
    S.bb("hparW", x0 - 0.1, x0 + 0.06, y0 - 0.2, y1 + 0.1, zr, zr + 0.16, "concrete2", 0.02)
    # magenta house panel + stencil on the south face, service door with ember strip
    px0, px1 = x0 + 1.4, x1 - 0.1
    if dmg:
        S.bb("hpanel", px0, px0 + 0.45, y0 - 0.1, y0 - 0.06, 0.6, 1.5, "house", 0.0)
        S.box("hpaneld", (0.7, 0.05, 0.8), (px0 + 0.85, (y0 - 0.5) * S.YS, 0.5), "house", 0.0, rot=(0, 0, 25), stretch=False)
    else:
        S.bb("hpanel", px0, px1, y0 - 0.1, y0 - 0.06, 0.6, 1.5, "house", 0.0)
        S.bb("hpaneltrim", px0, px1, y0 - 0.12, y0 - 0.1, 1.35, 1.5, "oxide", 0.0)
        S.bb("hpaneltrimb", px0, px1, y0 - 0.12, y0 - 0.1, 0.6, 0.73, "bone", 0.0)
    S.bb("door", x0 + 0.3, x0 + 0.95, y0 - 0.06, y0, 0.5, 1.35, "dark", 0.0)
    S.bb("doorglow", x0 + 0.35, x0 + 0.9, y0 - 0.1, y0 - 0.06, 0.5, 0.62, "oxide2" if dmg or not lit else "redglow", 0.0, nocast=True)
    S.bb("doorhood", x0 + 0.2, x0 + 1.05, y0 - 0.3, y0 - 0.0, 1.35, 1.5, "concrete2", 0.03)
    # intake louvres and roof vents, loudspeaker
    S.bb("intake", x0 + 0.2, x0 + 1.9, 1.0, 2.5, zr, zr + 0.4, "concrete2", 0.04)
    for k in range(4):
        S.bb(f"grille{k}", x0 + 0.3 + k * 0.4, x0 + 0.42 + k * 0.4, 1.0, 2.5, zr + 0.4, zr + 0.47, "dark", 0.0)
    D.roof_vents(S, zr, ((x1 - 0.6, 2.3, 0.7),))
    D.loudspeaker(S, -1.9, -0.6, zr, dmg, height=1.8)
    # coolant pipes hall -> stack
    for k, z in enumerate((0.85, 1.35)):
        if dmg and k == 1:
            S.cyl("pipeb", 0.14, 0.4, (x1 + 0.2, SY, z), "steel", axis="X", verts=10)
            S.cyl("pipeb2", 0.14, 0.6, (1.9, -2.5, 0.28), "steel", axis="X", verts=10)
            continue
        S.cyl(f"pipe{k}", 0.15, 0.9, (x1 + 0.2, SY, z), "steel", axis="X", verts=10)
        S.cyl(f"pflange{k}", 0.2, 0.1, (x1 + 0.0, SY, z), "oxide", axis="X", verts=10)
        S.cyl(f"pflangeb{k}", 0.2, 0.1, (x1 + 0.45, SY, z), "bone", axis="X", verts=10)
    # heat-exchanger block (south-east) with ember slits
    ex0, ex1, ey0, ey1 = EXCH
    S.bb("exch", ex0, ex1, ey0, ey1, 0.14, 1.0, "concrete2", 0.04)
    S.bb("exroof", ex0 - 0.08, ex1 + 0.08, ey0 - 0.1, ey1 + 0.08, 1.0, 1.15, "roof", 0.04)
    S.bb("exband", ex0, ex1, ey0 - 0.04, ey0, 0.7, 0.88, "oxide", 0.0)
    D.furnace_slits(S, ex0 + 0.3, ey0 - 0.1, 0.25, 0.6, 4, i, lit, step=0.5, w=0.25)
    for k in range(4):
        S.bb(f"exlouv{k}", ex0 + 0.3 + k * 0.5, ex0 + 0.45 + k * 0.5, ey0 + 0.2, ey1 - 0.2, 1.15, 1.22, "dark", 0.0)
    stack(S, i, dmg, lit, mode)
    D.ash_drift(S, ((-2.6, -2.4, 0.8), (2.9, 2.6, 0.7)))
    if dmg:
        L.R.scorch(S, "scS", SX, SY - 1.8, 1.8, 1.2, 0.5)
        L.R.scorch(S, "scH", -1.8, -2.1, 1.6, 1.0, 0.5)


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.3, 3.0, -3.0, -2.3, 0.14, 0.7, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 1.8, (-0.4, -2.6, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", -2.8, -1.2, -2.5, -2.3, 0.14, 0.3, "rebar", 0.0)
    S.bb("planks", -3.0, -1.8, 2.5, 3.0, 0.14, 0.4, "timber", 0.0)


def _left(S):
    S.bb("planks", -2.9, -1.9, -2.6, -2.2, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 1.8, (0.2, -2.6, 0.3), "steel", axis="X", verts=10)
    S.bb("crate", 2.3, 3.0, -2.9, -2.2, 0.14, 0.7, "crate", 0.03)
    L.spark(S, -0.4, -2.0, 1.4, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(SX, SY, 3.4)[0], y=anchor(SX, SY, 3.4)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(2.6, -2.5, 0.8)[0], y=anchor(2.6, -2.5, 0.8)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(-1.0, -2.0, 0.4)[0], y=anchor(-1.0, -2.0, 0.4)[1], phase=2),
    dict(state="damaged", kind="smoke_plume", x=anchor(SX, SY, 3.6)[0], y=anchor(SX, SY, 3.6)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(-2.0, 1.5, 1.9)[0], y=anchor(-2.0, 1.5, 1.9)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-3.0, 2.6, -2.4, 2.9), H=TOP,
    boxes=[(HALL[0], HALL[1], HALL[2], HALL[3])], rings=[(SX, SY, 1.2)],
    clips=(1.8, 3.4), scaf=[(-3.2, -2.5, -3.1, -2.5), (0.0, 1.0, -3.1, -2.5)],
    derrick=(3.0, -2.8, 4.0, (2.3, -2.0, 4.3)), sparks=[(-1.5, -1.0, 1.5), (SX, SY, 2.0), (-2.0, 2.3, 1.5)],
    hide=D.HIDE_ANIM + ("hpanel",), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
