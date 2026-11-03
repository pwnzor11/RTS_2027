"""Authority Silo: a concrete ash-storage bunker (2x2 cells). Original design, no franchise references.

Low bunker block in front (house panels, steel hatch), two short domed silos behind it with house bands, a feed pipe over
the domes down to an inlet on the east side, and a free-standing ash-level gauge whose lamp blinks in the idle loop.
Damage is structural only (D-069): the west silo is broken open (dome gone, jagged rim, spilled grey ash), its block roof
has caved, the east silo is cracked, the feed pipe is snapped and the gauge leans; fire / smoke are FX anchors.
Contract: see auth_refinery.py; build-site helpers come from auth_conyard.py.
Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import importlib.util
import math
import os

_HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structlib_auth_conyard", os.path.join(_HERE, "auth_conyard.py"))
L = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(L)
R = L.R

ID = "auth_silo"
TITLE = "Authority Silo"
FOOTPRINT = (2, 2)
CANVAS = (128, 152)
ORIGIN = (0, 24)
DOCK = (0, 0)
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True        # D-072: roof house2 panels pinned to mid ramp shades (structpost)
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = L.MATERIALS
SOOT_SKIN = L.SOOT_SKIN
PRIMER_SKIN = L.PRIMER_SKIN

SILOS = ((-1.5, 1.2), (1.5, 1.2))
SR = 1.3
SH = 2.5            # silo wall top
BLK = (-3.0, 3.0, -2.2, -0.3)
BH = 1.3


def silo(S, name, x, y, dmg, broken=False, idle_i=0):
    S.cyl(f"{name}", SR, SH - 0.14, (x, y, 0.14 + (SH - 0.14) / 2), "concrete", verts=28)
    S.cyl(f"{name}base", SR + 0.1, 0.35, (x, y, 0.32), "concrete2", verts=28)
    for k, zz in enumerate((1.2, 2.3)):
        S.cyl(f"{name}ring", SR + 0.05, 0.1, (x, y, zz), "concrete2", verts=28)
    S.cyl(f"{name}house", SR + 0.03, 0.55, (x, y, 1.75), "house", verts=28)
    if broken:
        # blown open: dome gone, jagged rim, dark inside
        S.cyl(f"{name}pit", SR - 0.15, 0.06, (x, y, SH - 0.02), "dark", verts=24)
        for k in range(7):
            a = math.radians(k * 51 + 20)
            h = 0.25 + 0.3 * ((k * 5) % 3)
            S.box(f"{name}jag", (0.4, 0.3, h), (x + (SR - 0.1) * math.cos(a), (y + (SR - 0.1) * math.sin(a)) * S.YS, SH + h / 2 - 0.05), "concrete2" if k % 2 else "concrete", 0.0,
                  rot=(0, 12 * (k % 3 - 1), math.degrees(a)), stretch=False)
    else:
        S.sphere(f"{name}dome", SR + 0.03, (x, y, SH - 0.05), "concrete2", nocast=False, squash=0.5)
        S.cyl(f"{name}hatch", 0.3, 0.16, (x, y, SH + 0.6), "steel", verts=12)
        S.cyl(f"{name}hatchc", 0.22, 0.1, (x, y, SH + 0.72), "dark", verts=10)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    # ---- pad, hazard stripes
    S.bb("pad", -3.3, 3.3, -3.3, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -3.3, 3.3, -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    for k in range(12):
        S.bb(f"haz{k}", -3.2 + k * 0.5, -3.2 + (k + 1) * 0.5, -3.18, -2.82, 0.14, 0.2, "yellow" if k % 2 == 0 else "dark", 0.0)
    # ---- the two silos
    silo(S, "silW", *SILOS[0], dmg, broken=dmg)
    silo(S, "silE", *SILOS[1], dmg, broken=False)
    # ---- bunker block
    bx0, bx1, by0, by1 = BLK
    yw = by0 - 0.02
    S.bb("blk", bx0, bx1, by0, by1, 0.14, BH, "concrete", 0.05)
    S.bb("blkplinth", bx0 - 0.08, bx1 + 0.08, by0 - 0.08, by1, 0.14, 0.4, "concrete2", 0.03)
    if dmg:
        S.bb("blkroofE", -0.3, bx1 + 0.12, by0 - 0.1, by1 + 0.05, BH, BH + 0.2, "roof", 0.04)
        S.bb("blkroofW", bx0 - 0.12, -0.3, by0 - 0.1, by1 + 0.05, BH - 0.45, BH - 0.25, "roof", 0.04, rot=(8, 0, -6))
        S.bb("blkpit", bx0, -0.4, by0 + 0.2, by1 - 0.1, BH - 0.4, BH, "dark", 0.0)
        S.bb("blkj", bx0, bx0 + 0.6, by0, by0 + 0.2, BH, BH + 0.3, "concrete", 0.02)
        S.bb("blkj", -1.5, -1.0, by0, by0 + 0.2, BH, BH + 0.2, "concrete", 0.02)
    else:
        S.bb("blkroof", bx0 - 0.12, bx1 + 0.12, by0 - 0.1, by1 + 0.05, BH, BH + 0.2, "roof", 0.04)
        S.bb("blkpara", bx0 - 0.12, bx1 + 0.12, by0 - 0.1, by0 - 0.0, BH + 0.2, BH + 0.34, "concrete2", 0.02)
    # hatch door and house panels
    S.bb("hatchf", -0.75, 0.75, yw - 0.1, yw + 0.04, 0.14, 1.12, "dark", 0.0)
    S.bb("hatchd", -0.6, 0.6, yw - 0.14, yw - 0.1, 0.2, 1.05, "steel", 0.02)
    S.bb("hatchbar", -0.6, 0.6, yw - 0.16, yw - 0.14, 0.58, 0.64, "yellow", 0.0, nocast=True)
    for sx in (-1, 1):
        if dmg and sx < 0:
            S.bb("panel", -2.55, -1.3, yw - 0.12, yw - 0.04, 0.3, 0.95, "house", 0.0, rot=(0, 0, 0))
            continue
        S.bb("pframe", sx * 1.95 - 0.95, sx * 1.95 + 0.95, yw - 0.08, yw, 0.28, 1.2, "dark", 0.0)
        S.bb("panel", sx * 1.95 - 0.85, sx * 1.95 + 0.85, yw - 0.12, yw - 0.04, 0.36, 1.12, "house", 0.0)
    # roof marking on the block
    S.bb("roofhouse", 0.1 if dmg else -2.4, 2.6, by0 + 0.35, by1 - 0.3, BH + 0.2, BH + 0.24, "house2", 0.0)
    # ---- feed pipe over the domes, down the east side to an inlet
    pz = 3.55
    if dmg:
        S.cyl("pipeH", 0.14, 2.4, (1.75, 1.2, pz), "steel", axis="X", verts=12)
        S.cyl("pipeV", 0.14, 1.3, (3.05, 1.2, 1.35), "steel", verts=10)
        S.cyl("pipeF", 0.14, 1.4, (3.0, -0.2, 0.3), "steel", axis="Y", verts=10)
        S.cyl("pipeFlange", 0.22, 0.1, (3.05, 1.2, 2.05), "rust", verts=12)
    else:
        S.cyl("pipeH", 0.14, 4.7, (0.7, 1.2, pz), "steel", axis="X", verts=12)
        S.cyl("pipeV", 0.14, pz - 0.2, (3.05, 1.2, (pz + 0.2) / 2), "steel", verts=10)
        for sx_, _ in SILOS:
            S.cyl("drop", 0.12, 0.7, (sx_, 1.2, pz - 0.3), "steel", verts=10)
        S.cyl("pipeFlange", 0.22, 0.1, (3.05, 1.2, 2.05), "rust", verts=12)
        S.cyl("pipeFlange", 0.22, 0.1, (1.5, 1.2, pz), "rust", axis="X", verts=12)
    S.bb("inlet", 2.75, 3.35, 0.9, 1.5, 0.14, 0.7, "hull2", 0.04)
    S.bb("inletlid", 2.7, 3.4, 0.85, 1.55, 0.7, 0.78, "steel", 0.03)
    # ---- ash level gauge: tall post with a scale, a level bar and a lamp
    gx, gy = -3.05, -0.1
    if dmg:
        S.box("gpost", (0.32, 0.16, 2.5), (gx - 0.4, gy * YS, 1.2), "dark", 0.02, rot=(0, 24, 0), stretch=False)
        S.box("gscale", (0.16, 0.04, 2.2), (gx - 0.4 - 0.0, (gy - 0.1) * YS, 1.2), "gauge", 0.0, rot=(0, 24, 0), stretch=False, nocast=True)
        S.bb("glamp", gx - 1.35, gx - 1.1, gy - 0.1, gy + 0.1, 0.14, 0.34, "dark", 0.0)
    else:
        S.bb("gpost", gx - 0.17, gx + 0.17, gy - 0.09, gy + 0.09, 0.14, 2.65, "dark", 0.02)
        S.bb("gscale", gx - 0.08, gx + 0.08, gy - 0.12, gy - 0.09, 0.5, 2.4, "gauge", 0.0)
        S.bb("glevel", gx - 0.06, gx + 0.06, gy - 0.13, gy - 0.12, 0.5, 1.5, "yellow", 0.0)
        for k in range(6):
            S.bb(f"gtick{k}", gx - 0.14, gx + 0.14, gy - 0.13, gy - 0.12, 0.6 + k * 0.35, 0.64 + k * 0.35, "white", 0.0, nocast=True)
        on = idle and i % 2 == 0
        S.bb("glamp", gx - 0.12, gx + 0.12, gy - 0.12, gy + 0.12, 2.65, 2.9, "amber" if on else "amber_dim", 0.02)
    S.bb("gbase", gx - 0.3, gx + 0.3, gy - 0.25, gy + 0.25, 0.14, 0.4, "concrete2", 0.03)
    # ---- damage: spilled ash, cracks, scorch
    if dmg:
        # grey ash heap from the broken west silo spills over the block roof and the pad
        S.bb("ashheap0", -3.3, -1.3, -0.4, 0.8, 0.14, 1.0, "ash", 0.35)
        S.bb("ashheap1", -2.6, -0.8, -1.0, -0.1, BH, BH + 0.7, "ash2", 0.3)
        S.bb("ashheap2", -3.3, -1.9, -1.0, -0.3, 0.14, 0.7, "ash", 0.3)
        S.bb("ashspill", -3.2, -0.9, -3.0, -2.5, 0.14, 0.4, "ash2", 0.15, rot=(0, 0, 5))
        S.bb("ashfan", -2.2, -0.4, -3.2, -2.5, 0.14, 0.3, "ash", 0.12, rot=(0, 0, -8))
        S.sphere("fallendome", 1.0, (-2.4, -2.95, 0.55), "concrete2", nocast=False, squash=0.5)
        S.bb("crackE", 1.9, 1.97, -0.2, -0.12, 1.5, 2.9, "crack", 0.0, nocast=True)
        S.bb("crackE2", 1.5, 2.2, -0.2, -0.12, 2.1, 2.17, "crack", 0.0, nocast=True)
        S.bb("crackB", 0.9, 0.97, yw - 0.16, yw - 0.13, 0.4, 1.25, "crack", 0.0, nocast=True)
        R.scorch(S, "scA", 1.6, 0.0, 2.0, 1.3, 1.0)
        R.scorch(S, "scB", 2.0, yw - 0.14, 0.7, 1.2, 0.5)
        R.scorch(S, "scC", 1.0, -1.2, BH + 0.23, 1.8, 1.0, 0.03, plane="T")


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_large", x=_a(-1.5, 1.2, 2.4)[0], y=_a(-1.5, 1.2, 2.4)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=_a(-2.2, -0.3, 1.2)[0], y=_a(-2.2, -0.3, 1.2)[1], phase=3),
    dict(state="damaged", kind="fire_small", x=_a(1.5, 0.0, 2.0)[0], y=_a(1.5, 0.0, 2.0)[1], phase=5),
    dict(state="damaged", kind="smoke_plume", x=_a(-1.5, 1.2, 2.5)[0], y=_a(-1.5, 1.2, 2.5)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(-1.8, -0.6, 1.3)[0], y=_a(-1.8, -0.6, 1.3)[1], phase=5),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.3, 3.1, -2.9, -2.1, 0.14, 0.8, "crate", 0.03)
        S.bb("crate", 2.45, 3.0, -2.8, -2.2, 0.8, 1.3, "crate", 0.03, rot=(0, 0, 12))
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (-1.2, -2.8, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", -3.0, -1.2, -2.3, -2.1, 0.14, 0.3, "rebar", 0.0)
        S.cyl("barrel", 0.3, 0.7, (3.0, -1.8, 0.5), "rust", verts=12)
    S.bb("planks", 0.5, 2.3, 2.8, 3.2, 0.14, 0.5, "timber", 0.0)
    S.bb("cabin", -3.2, -2.3, 2.3, 3.2, 0.14, 1.1, "primer", 0.03)
    S.bb("cabdoor", -2.9, -2.6, 2.25, 2.3, 0.14, 0.8, "dark", 0.0)


def _left(S):
    S.bb("planks", -2.6, -1.2, -3.0, -2.7, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 2.0, (1.5, -3.0, 0.3), "steel", axis="X", verts=10)
    S.bb("crate", 2.3, 3.1, -2.9, -2.2, 0.14, 0.8, "crate", 0.03)
    L.spark(S, 0.0, -2.2, 1.5, 3, 6)


SPEC = dict(
    pad=(-3.3, 3.3, -3.3, 3.3), env=(-3.0, 3.0, -2.2, 2.5), H=2.5, boxes=[(BLK[0], BLK[1], BLK[2], BLK[3])], rings=[(SILOS[0][0], SILOS[0][1], SR), (SILOS[1][0], SILOS[1][1], SR)],
    clips=(1.3, 2.2), scaf=[(-3.2, -2.2, -2.9, -2.35), (2.1, 3.1, -2.9, -2.35)],
    derrick=(3.0, 2.4, 3.8, (0.0, 1.8, 4.3)), sparks=[(-2.0, -2.2, 1.3), (0.0, 1.2, 2.3), (2.5, -0.3, 1.3)],
    hide=("sten", "glamp", "gtick"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
