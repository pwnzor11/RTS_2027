"""Directorate Bunker Wall: a low bone-white slab set into the ground with an oxide-red stripe along the top (1x1 cell).  Original.

Same connection-variant scheme as auth_wall (h, v, end_*, corner_*, t_*, cross, post, each with a `_damaged` twin; `idle` /
`damaged` = h; `build` generic) so the game's wall logic works unchanged: the letters name the directions the wall continues in.
Look: a chunky bone slab only 1.35 high on an umber footing sunk into the ground, a red stripe capping it, a magenta house
panel inset low on the south face, shallow panel joints and a row of small dark firing notches.  Joints (corner / T / cross / post)
get a taller squat umber pillar with a magenta-banded cap.  Tiling: arms, stripes and footings run to the cell edge (+-1.7) with
no bevel, so rows and columns read as one wall; the canvas is 64 px wide like auth_wall.
Damage is structural only (D-069): a stepped bite out of the slab, bent rebar, rubble; no FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up.
"""
import importlib.util
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_def as DD
D = DD.D


def _load(fname, name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(_HERE, fname))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


W = _load("auth_wall.py", "structlib_auth_wall")     # read-only: VARIANTS, T(), ab(), build_site()

ID = "dir_bunker"
TITLE = "Directorate Bunker Wall"
FOOTPRINT = (1, 1)
CANVAS = (64, 100)
ORIGIN = (0, 32)
DOCK = (0, 1)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
VARIANTS = W.VARIANTS
STATES = [("idle", 1, 1), ("damaged", 1, 1)]
for _v in VARIANTS:
    STATES.append((_v, 1, 1))
    STATES.append((_v + "_damaged", 1, 1))
STATES.append(("build", 4, 5))
FX = []
MATERIALS = DD.MATERIALS
SOOT_SKIN = DD.SOOT_SKIN
PRIMER_SKIN = DD.PRIMER_SKIN

HX = 1.7
WT = 0.55           # half thickness of the slab
WZ = 1.35           # top of the slab
FT = 0.7            # half thickness of the footing
PW = 0.62            # half width of a joint pillar
PZ = 1.8           # joint pillar height
T, ab = W.T, W.ab
beam = D.L.beam


def steps(dmg_arm, d):
    if d != dmg_arm:
        return [(0.0, HX, WZ)]
    return [(0.0, 0.45, WZ), (0.45, 0.85, 0.85), (0.85, 1.25, 0.55), (1.25, 1.5, 1.0), (1.5, HX, WZ)]


def finished(S, arms, dmg=False):
    pillar = W.is_pillar(arms)
    dd = W.damaged_arm(arms) if dmg else None
    ys = -WT - 0.02
    # ---- footing + slab + red stripe per arm
    if pillar:
        S.bb("foot", -PW - 0.12, PW + 0.12, -PW - 0.12, PW + 0.12, 0.0, 0.3, "concrete2", 0.0)
    else:
        S.bb("foot", -FT, FT, -FT, FT, 0.0, 0.3, "concrete2", 0.0)
    for d in arms:
        ab(S, f"foot{d}", d, 0.0, HX, -FT, FT, 0.0, 0.3, "concrete2")
        for k, (u0, u1, top) in enumerate(steps(dd, d)):
            ab(S, f"slab{d}{k}", d, u0, u1, -WT, WT, 0.0, top, "bone")
        spans = [(0.0, 0.45), (1.5, HX)] if d == dd else [(0.0, HX)]
        for k, (u0, u1) in enumerate(spans):
            ab(S, f"stripe{d}{k}", d, u0, u1, -WT - 0.03, WT + 0.03, WZ - 0.28, WZ + 0.02, "oxide")
            ab(S, f"ridge{d}{k}", d, u0, u1, -WT + 0.12, WT - 0.12, WZ + 0.02, WZ + 0.1, "concrete2")
    # ---- joint pillar
    if pillar:
        if dmg and not arms:
            S.bb("pillar", -PW, PW, -PW, PW, 0.0, 1.35, "concrete2", 0.0)
            S.bb("pbite", -PW, 0.1, -PW - 0.01, -PW + 0.04, 0.9, 1.35, "dark", 0.0, nocast=True)
            S.box("pcapfall", (0.9, 0.8, 0.2), (0.6, -1.0, 0.12), "concrete", 0.0, rot=(0, 0, 25), stretch=False, nocast=False)
            beam(S, "prb0", (0.0, 0.0, 1.35), (-0.2, -0.05, 2.1), 0.05, "rebar")
            beam(S, "prb1", (0.3, 0.1, 1.35), (0.5, 0.15, 1.95), 0.05, "rebar")
        else:
            S.bb("pillar", -PW, PW, -PW, PW, 0.0, PZ, "concrete2", 0.0)
            S.bb("pband", -PW - 0.03, PW + 0.03, -PW - 0.03, PW + 0.03, PZ - 0.55, PZ - 0.2, "house", 0.0, nocast=True)
            S.bb("pcap", -PW - 0.08, PW + 0.08, -PW - 0.08, PW + 0.08, PZ, PZ + 0.14, "concrete", 0.0)
            for nm, (a, b, c, d) in (("pa", (-PW - 0.09, PW + 0.09, -PW - 0.09, -PW + 0.05)), ("pb", (-PW - 0.09, PW + 0.09, PW - 0.05, PW + 0.09)),
                                     ("pc", (-PW - 0.09, -PW + 0.05, -PW, PW)), ("pd", (PW - 0.05, PW + 0.09, -PW, PW))):
                S.bb(nm, a, b, c, d, PZ + 0.14, PZ + 0.22, "oxide", 0.0)
            S.bb("pslot", -0.2, 0.2, -0.2, 0.2, PZ + 0.14, PZ + 0.2, "dark", 0.0, nocast=True)
    # ---- south-facing decoration: panel joints, firing notches, house panel low on the face
    inner = PW if pillar else 0.0
    spans = []
    if "E" in arms:
        spans.append((inner, HX))
    if "W" in arms:
        spans.append((-HX, -inner))
    broken = None
    if dd == "E":
        broken = (0.4, 1.55)
    elif dd == "W":
        broken = (-1.55, -0.4)
    for (a, b) in spans:
        parts = [(a, b)]
        if broken:
            parts = [(p0, p1) for (p0, p1) in ((a, min(b, broken[0])), (max(a, broken[1]), b)) if p1 - p0 > 0.01]
        for (p0, p1) in parts:
            if p1 - p0 > 0.5:
                m0, m1 = p0 + 0.12, p1 - 0.12
                S.bb("hpanel", m0, m1, ys - 0.04, ys, 0.28, 0.72, "house", 0.0, nocast=True)
                S.bb("hpanelt", m0, m1, ys - 0.05, ys - 0.04, 0.72, 0.78, "dark", 0.0, nocast=True)
                S.bb("hpanelb", m0, m1, ys - 0.05, ys - 0.04, 0.22, 0.28, "dark", 0.0, nocast=True)
        for gx in (-0.85, 0.85):
            if a + 0.1 < gx < b - 0.1 and not (broken and broken[0] < gx < broken[1]):
                S.bb("joint", gx - 0.03, gx + 0.03, ys - 0.03, ys, 0.3, WZ - 0.28, "concrete2", 0.0, nocast=True)
        for gx in (-0.42, 0.42, -1.27, 1.27):
            if a + 0.15 < gx < b - 0.15 and not (broken and broken[0] - 0.1 < gx < broken[1] + 0.1) and abs(gx) > 0.0:
                S.bb("notch", gx - 0.12, gx + 0.12, ys - 0.03, ys, 0.88, 1.0, "dark", 0.0, nocast=True)
    if not arms or ("E" not in arms and "W" not in arms and "S" not in arms) or (pillar and "S" not in arms):
        pass
    # north/south runs show the east face end-on only; mark the ridge with a small house tab so the house colour still reads
    for d in arms:
        if d in "NS" and d != dd:
            ab(S, f"mark{d}", d, 0.6, 1.1, -0.18, 0.18, WZ + 0.1, WZ + 0.14, "house2", nocast=True)
    # hazard ticks at the foot of exposed fronts
    for k in range(8):
        x0, x1 = -HX + 0.425 * k, -HX + 0.425 * (k + 1)
        mid = (x0 + x1) / 2
        front = ("E" in arms and mid > 0) or ("W" in arms and mid < 0)
        if pillar and abs(mid) < PW + 0.12:
            front = front and False
        if "S" in arms and abs(mid) < 0.85:
            front = False
        if front and not (broken and broken[0] < mid < broken[1]):
            yy = -FT - 0.02
            S.bb(f"haz{k}", x0, x1, yy, yy + 0.03, 0.08, 0.26, "oxide" if k % 2 == 0 else "bone", 0.0, nocast=True)
    if dmg and dd is not None:
        damage(S, dd)
    elif dmg:
        pass


def damage(S, d):
    if d in "EW":
        ab(S, "bite", d, 0.45, 0.85, -WT - 0.01, -WT + 0.04, 0.6, 0.85, "dark", nocast=True)
        ys = -WT - 0.05
        for k, (a, b) in enumerate((((0.3, 0.3), (0.5, 1.0)), ((0.5, 1.0), (0.3, 1.3)), ((1.6, 0.3), (1.45, 1.1)))):
            p0, p1 = T(d, a[0], 0), T(d, b[0], 0)
            beam(S, f"crack{k}", (p0[0], ys, a[1]), (p1[0], ys, b[1]), 0.05, "dark", nocast=True)
        sc = (0.5, 1.5)
        S.bb("scorch", min(T(d, sc[0], 0)[0], T(d, sc[1], 0)[0]), max(T(d, sc[0], 0)[0], T(d, sc[1], 0)[0]), -WT - 0.06, -WT - 0.03, 0.15, 0.6, "char", 0.0, nocast=True)
    for k, ((u0, v0, z0), (u1, v1, z1)) in enumerate((((0.55, 0.0, 0.85), (0.3, -0.1, 1.7)), ((0.95, 0.05, 0.55), (1.2, 0.2, 1.5)), ((1.3, 0.0, 1.0), (1.55, -0.05, 1.7)))):
        a, b = T(d, u0, v0), T(d, u1, v1)
        beam(S, f"rbar{k}", (a[0], a[1], z0), (b[0], b[1], z1), 0.05, "rebar")
    fx, fy = T(d, 1.0, -0.65)
    S.box("fall", (0.9, 0.5, 0.3), (fx, fy, 0.45), "bone", 0.0, rot=(0, 0, 20), stretch=False, nocast=False)
    S.box("fallstripe", (0.9, 0.2, 0.1), (fx - 0.05, fy - 0.1, 0.65), "oxide", 0.0, rot=(0, 0, 20), stretch=False, nocast=False)
    for k, (u, v, w, h) in enumerate(((0.55, -0.9, 0.34, 0.26), (1.35, -0.95, 0.3, 0.22), (0.15, -0.9, 0.26, 0.18), (1.45, -1.4, 0.3, 0.2))):
        x, y = T(d, u, v)
        S.box(f"rub{k}", (w, w * 0.8, h), (x, y, h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k))


def build(S, state, i, n):
    building = state == "build"
    if building and i < 3:
        W.build_site(S, i)
        return
    dmg = state.endswith("damaged")
    base = state[:-len("_damaged")] if state.endswith("_damaged") else state
    arms = "EW" if (building or base in ("idle", "damaged")) else VARIANTS[base]
    if dmg:
        S.skin.update(SOOT_SKIN)
    finished(S, arms, dmg)
