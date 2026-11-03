"""Authority Barracks: a citizen-army quonset hut (2x2 cells). Original design, no franchise references.

Arched concrete hut, house-colour entrance panels with a dark door, a decontamination shower arch and mat in front,
sandbag walls either side, a flagpole (the flag waves in the idle loop) and an ash-respirator rack by the west wall.
Damage is structural only (D-069): the north half of the arch has collapsed into rubble leaving bent ribs, a torn hole in
the roof, sandbags scattered, the arch and the flagpole bent; fire / smoke are FX anchors.
Exit: units leave below the footprint middle, cell [0, 2] relative (just south of the south edge, x middle).
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

ID = "auth_barracks"
TITLE = "Authority Barracks"
FOOTPRINT = (2, 2)
CANVAS = (128, 160)
ORIGIN = (0, 32)
DOCK = (1, 2)           # exit cell: just below the footprint's middle (x middle of 2 cells = boundary; 1 = east of it)
CAP_MATERIAL = "concrete2"
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = L.MATERIALS
SOOT_SKIN = L.SOOT_SKIN
PRIMER_SKIN = L.PRIMER_SKIN

HR = 2.2                 # hut radius
HY0, HY1 = -0.9, 2.9     # hut front / back
ZC = 0.14                # arch axis height (the pad top)
TOP = ZC + HR            # ridge height


def arch(S, name, y, r, t0, t1, m="steel", n=9, w=0.2, th=0.12):
    """Rib made of short boxes along the arc from angle t0 to t1 (degrees, 0 = east, 180 = west)."""
    for k in range(n):
        t = t0 + (t1 - t0) * (k + 0.5) / n
        a = math.radians(t)
        ln = 2 * math.pi * r * (abs(t1 - t0) / 360) / n + 0.05
        S.box(name, (ln, th, w), (r * math.cos(a), y * S.YS, ZC + r * math.sin(a)), m, 0.0, rot=(0, -(t + 90), 0), stretch=False)


def sandbags(S, x0, x1, y, rows, dmg=False, seedk=0):
    """Wall of sandbags along x at depth y; rows stack with a half-bag offset."""
    nb = int((x1 - x0) / 0.5)
    for r in range(rows):
        for k in range(nb):
            if dmg and ((k * 7 + r * 3 + seedk) % 5 == 0 or r > 1 and k % 2):
                continue
            x = x0 + 0.25 + k * 0.5 + (0.25 if r % 2 else 0)
            if x > x1 - 0.1:
                continue
            m = "sandbag" if (k + r) % 2 == 0 else "sandbag2"
            jit = (((k * 13 + r * 5 + seedk) % 7) - 3) * 0.015
            S.bb("sbag", x - 0.24, x + 0.24, y - 0.17 + jit, y + 0.17 + jit, 0.14 + 0.2 * r, 0.14 + 0.2 * (r + 1), m, 0.05)
    if dmg:      # scattered bags
        for k, (bx, by, rz) in enumerate(((x0 + 0.5, y - 0.55, 20), (x0 + 1.2, y - 0.9, -35), (x1 - 0.7, y - 0.7, 50))):
            S.bb("sbagfall", bx - 0.24, bx + 0.24, by - 0.17, by + 0.17, 0.14, 0.32, "sandbag", 0.05, rot=(0, 0, rz))


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    yf = HY0 - 0.03       # front face plane
    # ---- pad, hazard stripes
    S.bb("pad", -3.3, 3.3, -3.3, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -3.3, 3.3, -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    for k in range(12):
        S.bb(f"haz{k}", -3.2 + k * 0.5, -3.2 + (k + 1) * 0.5, -3.18, -2.82, 0.14, 0.2, "yellow" if k % 2 == 0 else "dark", 0.0)
    # ---- the hut
    y1 = 0.9 if dmg else HY1
    S.cyl("hut", HR, y1 - HY0, (0, (HY0 + y1) / 2, ZC), "concrete", axis="Y", verts=36)
    S.bb("hutbase", -HR - 0.12, HR + 0.12, HY0 - 0.1, y1, 0.14, 0.34, "concrete2", 0.03)
    for k, yy in enumerate((-0.55, 0.45, 1.45, 2.45)):
        if yy < y1 - 0.1:
            S.cyl(f"hrib{k}", HR + 0.05, 0.16, (0, yy, ZC), "concrete2", axis="Y", verts=36)
    S.cyl("hrib", HR + 0.07, 0.2, (0, HY0 + 0.05, ZC), "concrete2", axis="Y", verts=36)
    # house stripe along the ridge + roof vents
    yh1 = 0.7 if dmg else 2.6
    S.bb("roofhouse", -0.55, 0.55, -0.4, yh1, TOP - 0.06, TOP + 0.02, "house2", 0.0)
    if not dmg:
        S.bb("vent", -0.35, 0.35, 1.9, 2.4, TOP - 0.02, TOP + 0.3, "concrete2", 0.04)
        S.bb("ventgr", -0.3, 0.3, 1.88, 2.4, TOP + 0.3, TOP + 0.36, "dark", 0.0)
    # ---- entrance wall: house panels flanking a dark door, stencils
    S.bb("dframe", -1.55, 1.55, yf - 0.02, yf + 0.06, 0.14, 1.62, "dark", 0.0)
    S.bb("door", -0.55, 0.55, yf - 0.1, yf - 0.02, 0.14, 1.3, "steel", 0.02)
    S.bb("doorin", -0.4, 0.4, yf - 0.12, yf - 0.1, 0.2, 1.2, "dark", 0.0)
    for sx in (-1, 1):
        if dmg and sx > 0:
            S.bb("panel", 0.72, 1.35, yf - 0.1, yf - 0.02, 0.2, 1.1, "house", 0.0)
        else:
            S.bb("panel", -1.45 if sx < 0 else 0.72, -0.72 if sx < 0 else 1.45, yf - 0.1, yf - 0.02, 0.2, 1.5, "house", 0.0)
    S.bb("lintel", -0.7, 0.7, yf - 0.14, yf - 0.02, 1.3, 1.55, "concrete2", 0.03)
    S.bb("lintelbar", -0.6, 0.6, yf - 0.16, yf - 0.14, 1.4, 1.46, "yellow", 0.0, nocast=True)
    # ---- decontamination shower arch and mat
    ya = -1.65
    for sx in (-1, 1):
        if dmg and sx > 0:
            L.beam(S, "apost", (1.05, ya, 0.14), (1.45, ya + 0.0, 1.55), 0.18, "steel")
        else:
            S.bb("apost", sx * 1.05 - 0.09, sx * 1.05 + 0.09, ya - 0.09, ya + 0.09, 0.14, 2.0, "steel", 0.02)
    if dmg:
        L.beam(S, "abeam", (-1.05, ya, 2.0), (1.5, ya, 1.55), 0.22, "pipeblue")
    else:
        S.bb("abeam", -1.15, 1.15, ya - 0.14, ya + 0.14, 1.95, 2.2, "pipeblue", 0.03)
        S.bb("asign", -0.45, 0.45, ya - 0.17, ya - 0.14, 1.98, 2.17, "white", 0.0)
        for k in range(3):
            S.bb(f"asignb{k}", -0.33 + k * 0.28, -0.25 + k * 0.28, ya - 0.18, ya - 0.17, 2.02, 2.13, "dark", 0.0, nocast=True)
        for k in range(4):
            S.bb(f"nozzle{k}", -0.75 + k * 0.5, -0.65 + k * 0.5, ya - 0.06, ya + 0.06, 1.8, 1.95, "dark", 0.0)
    S.bb("mat", -0.95, 0.95, -2.35, -1.2, 0.14, 0.2, "dark", 0.0)
    for (a, b, c, d) in ((-0.95, 0.95, -2.35, -2.3), (-0.95, -0.9, -2.35, -1.2), (0.9, 0.95, -2.35, -1.2)):
        S.bb("matline", a, b, c, d, 0.2, 0.22, "yellow", 0.0, nocast=True)
    # ---- sandbags either side of the mat
    sandbags(S, -3.1, -1.1, -2.6, 3, dmg, 0)
    sandbags(S, 1.1, 2.4, -2.6, 3, dmg, 2)
    sandbags(S, -3.1, -2.1, -1.9, 2, dmg, 1)
    # ---- flagpole at the front east corner; the flag waves (4 segments with a travelling offset)
    px, py = 2.9, -2.8
    if dmg:
        S.cyl("pole", 0.06, 2.3, (px, py, 1.3), "steel", verts=8)
        L.beam(S, "poleb", (px, py, 2.4), (px - 0.9, py - 0.1, 2.0), 0.1, "steel")
        for k in range(4):
            S.bb("flagseg", px - 1.1 + k * 0.17, px - 0.93 + k * 0.17, py - 0.07, py - 0.03, 1.3 - k * 0.1, 1.95 - k * 0.1, "house", 0.0)
    else:
        S.cyl("pole", 0.06, 4.1, (px, py, 2.2), "steel", verts=8)
        S.cyl("poleball", 0.1, 0.2, (px, py, 4.35), "yellow", verts=8)
        ph = (i if idle else 0) * math.pi / 2
        for k in range(5):
            dz = 0.13 * math.sin(ph + k * 1.1) * (k + 1) / 3
            S.bb("flagseg", px - 1.45 + (4 - k) * 0.29, px - 1.45 + (5 - k) * 0.29 - 0.0, py - 0.04, py + 0.04, 3.1 + dz, 3.95 + dz - 0.04 * k, "house", 0.0)
    S.bb("polebase", px - 0.28, px + 0.28, py - 0.28, py + 0.28, 0.14, 0.4, "concrete2", 0.03)
    # ---- respirator rack on the west side: frame, rail, hanging masks and canisters
    rx = -3.0
    for yy in (-1.3, 0.1):
        S.bb("rpost", rx - 0.05, rx + 0.05, yy - 0.05, yy + 0.05, 0.14, 1.15, "steel", 0.0)
    S.bb("rrail", rx - 0.05, rx + 0.05, -1.35, 0.15, 1.1, 1.18, "steel", 0.0)
    S.bb("rshelf", rx - 0.2, rx + 0.2, -1.35, 0.15, 0.5, 0.55, "steel", 0.0)
    for k in range(6):
        yy = -1.2 + k * 0.25
        S.bb(f"mask{k}", rx - 0.1, rx + 0.1, yy - 0.08, yy + 0.08, 0.62, 1.08, "dark", 0.02)
        S.bb(f"canis{k}", rx - 0.07, rx + 0.07, yy - 0.05, yy + 0.05, 0.58, 0.7, "yellow" if k % 2 else "steel", 0.0)
    # ---- damage: collapsed north half
    if dmg:
        S.bb("cutlip", -1.9, 1.9, y1 - 0.3, y1, 0.14, 0.3, "concrete2", 0.05)
        # torn roof hole next to the break
        S.bb("hole", -0.9, 0.9, 0.0, 0.85, TOP - 0.1, TOP + 0.03, "dark", 0.0, nocast=True)
        for k, (jx, jz, jw) in enumerate(((-1.0, 1.8, 0.6), (-1.3, 1.5, 0.5), (1.0, 2.0, 0.5), (1.4, 1.7, 0.6), (0.0, 2.3, 0.4))):
            S.bb(f"jag{k}", jx - jw / 2, jx + jw / 2, 0.55, 1.0, jz, jz + 0.35, "concrete", 0.0, rot=(0, 0, 10 * (k - 2)))
        # bent ribs standing in the rubble
        arch(S, "brib", 1.4, HR, 20, 140, n=8)
        arch(S, "brib", 2.3, HR, 5, 65, n=4)
        arch(S, "brib", 2.3, HR, 150, 178, n=3)
        # rubble: slabs, a sagging roof piece, spilled concrete
        S.bb("rub1", -2.2, 0.4, 1.0, 2.9, 0.14, 0.9, "concrete2", 0.1, rot=(0, 0, 0))
        S.bb("rub2", -0.4, 2.3, 1.2, 3.0, 0.14, 1.25, "concrete", 0.1)
        S.bb("rub3", -1.6, -0.2, 1.8, 3.0, 0.9, 1.5, "concrete2", 0.08, rot=(0, 8, 12))
        S.box("slab1", (2.2, 1.4, 0.3), (0.8, 2.0 * YS, 1.5), "concrete", 0.04, rot=(14, -10, 8), stretch=False)
        S.bb("rub4", -2.7, -1.8, 0.8, 2.2, 0.14, 0.6, "concrete2", 0.06, rot=(0, 0, 14))
        S.bb("rub5", 1.9, 2.8, 0.6, 1.8, 0.14, 0.5, "concrete", 0.06, rot=(0, 0, -18))
        S.bb("rubfront", -1.2, 0.8, 0.4, 1.0, 0.14, 0.6, "concrete2", 0.05, rot=(0, 0, 8))
        for k, (cx, zl, zh) in enumerate(((-1.3, 0.5, 1.5), (0.9, 0.8, 1.35))):
            S.bb(f"crack{k}", cx, cx + 0.07, yf - 0.13, yf - 0.1, zl, zh, "crack", 0.0, nocast=True)
        R.scorch(S, "scF", -1.0, yf - 0.16, 0.9, 1.2, 0.7)
        R.scorch(S, "scG", 0.0, -1.8, 0.23, 2.0, 1.2, 0.03, plane="T")
        R.scorch(S, "scR", 0.3, 0.5, TOP + 0.03, 1.0, 0.8, 0.04, plane="T")


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_large", x=_a(0.2, 1.5, 1.3)[0], y=_a(0.2, 1.5, 1.3)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=_a(-1.4, 1.9, 0.8)[0], y=_a(-1.4, 1.9, 0.8)[1], phase=3),
    dict(state="damaged", kind="fire_small", x=_a(1.9, 1.2, 0.6)[0], y=_a(1.9, 1.2, 0.6)[1], phase=5),
    dict(state="damaged", kind="fire_small", x=_a(0.0, 0.3, 2.3)[0], y=_a(0.0, 0.3, 2.3)[1], phase=1),
    dict(state="damaged", kind="smoke_plume", x=_a(0.2, 1.5, 1.4)[0], y=_a(0.2, 1.5, 1.4)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(-1.4, 1.9, 1.0)[0], y=_a(-1.4, 1.9, 1.0)[1], phase=4),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.3, 3.1, -2.9, -2.1, 0.14, 0.8, "crate", 0.03)
        S.bb("crate", 2.45, 3.0, -2.8, -2.2, 0.8, 1.3, "crate", 0.03, rot=(0, 0, 12))
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (-1.5, -2.9, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", -3.0, -1.2, -2.2, -2.0, 0.14, 0.3, "rebar", 0.0)
        S.cyl("barrel", 0.3, 0.7, (3.0, -1.8, 0.5), "rust", verts=12)
    S.bb("planks", 1.2, 3.0, 3.0, 3.3, 0.14, 0.5, "timber", 0.0)
    S.bb("cabin", -3.2, -2.5, 2.2, 3.2, 0.14, 1.1, "primer", 0.03)
    S.bb("cabdoor", -3.2, -3.15, 2.45, 2.8, 0.14, 0.8, "dark", 0.0)


def _left(S):
    S.bb("planks", -3.0, -1.6, -3.0, -2.7, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 2.0, (1.6, -3.0, 0.3), "steel", axis="X", verts=10)
    S.bb("crate", 2.3, 3.1, -2.2, -1.5, 0.14, 0.8, "crate", 0.03)
    L.spark(S, 0.0, -0.9, 1.6, 3, 6)


SPEC = dict(
    pad=(-3.3, 3.3, -3.3, 3.3), env=(-2.4, 2.4, HY0, HY1), H=2.4, boxes=[(-2.2, 2.2, HY0, HY1)], rings=[],
    clips=(1.2, 2.1), scaf=[(2.3, 3.2, -0.3, 0.4), (-3.2, -2.35, -0.3, 0.4)],
    derrick=(-3.0, 2.6, 3.6, (-0.3, 1.5, 4.0)), sparks=[(-2.0, -0.9, 1.9), (2.0, 2.0, 2.2), (0.0, 2.9, 2.2)],
    hide=("flagseg", "nozzle", "asign"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
