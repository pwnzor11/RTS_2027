"""Authority Engineering Works: a drafting hall and testing yard (2x2 cells). Original design, no franchise references.

Long khaki drafting hall with a saw-tooth skylight roof, house stripe and lit amber windows; in the yard a concrete test
bench with a steel-beam load frame (a hydraulic ram cycles up and down on the specimen block) and a pole with a wind vane
and a flag. A blueprint rack and a few stacked sample blocks stand by the door. Idle loop: the ram cycles, windows pulse,
the vane turns. Damage is structural only (D-069): skylight teeth caved in, the load frame bent over, specimen shattered,
windows dark; fire / smoke are FX anchors.  Contract: see auth_refinery.py; build-site helpers come from auth_conyard.py.
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

ID = "auth_engineering"
TITLE = "Authority Engineering Works"
FOOTPRINT = (2, 2)
CANVAS = (128, 152)
ORIGIN = (0, 24)
DOCK = (0, 0)
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = dict(L.MATERIALS)
MATERIALS["steel2"] = ("#979EA3", dict(rough=0.4, metal=0.5))
SOOT_SKIN = L.SOOT_SKIN
PRIMER_SKIN = dict(L.PRIMER_SKIN)
PRIMER_SKIN["steel2"] = "primer"

HALL = (-3.0, 3.0, 0.3, 2.9)
H = 1.7
RAM = [0.0, 0.25, 0.5, 0.25]


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    x0, x1, y0, y1 = HALL
    yw = y0 - 0.02
    S.bb("pad", -3.3, 3.3, -3.3, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -3.3, 3.3, -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    for k in range(12):
        S.bb(f"haz{k}", -3.2 + k * 0.5, -3.2 + (k + 1) * 0.5, -3.18, -2.86, 0.14, 0.2, "yellow" if k % 2 == 0 else "dark", 0.0)
    # ---- drafting hall
    S.bb("hall", x0, x1, y0, y1, 0.14, H, "concrete", 0.05)
    S.bb("plinth", x0 - 0.08, x1 + 0.08, y0 - 0.08, y1 + 0.05, 0.14, 0.4, "concrete2", 0.03)
    S.bb("band", x0, x1, yw - 0.06, yw + 0.04, 1.1, 1.4, "house", 0.0)
    S.bb("roofbase", x0 - 0.12, x1 + 0.12, y0 - 0.1, y1 + 0.05, H, H + 0.18, "roof", 0.04)
    S.bb("para", x0 - 0.12, x1 + 0.12, y0 - 0.1, y0, H + 0.18, H + 0.3, "concrete2", 0.02)
    # saw-tooth skylights: sloped glass faces toward the south
    for k in range(5):
        xa = x0 + 0.2 + k * 1.15
        if dmg and k in (1, 2):
            S.bb(f"hole{k}", xa, xa + 1.0, y0 + 0.5, y1 - 0.4, H + 0.18, H + 0.22, "dark", 0.0)
            S.bb(f"tj{k}", xa, xa + 0.5, y0 + 0.5, y0 + 0.65, H + 0.2, H + 0.6, "roof", 0.0, rot=(0, 0, 0))
            continue
        S.bb(f"tooth{k}", xa, xa + 1.0, y0 + 0.4, y1 - 0.3, H + 0.18, H + 0.52, "roof", 0.03)
        S.box(f"glaze{k}", (1.0, 0.7, 0.05), (xa + 0.5, (y0 + 0.4) * YS - 0.2, H + 0.4), "glass", 0.0, rot=(-35, 0, 0), stretch=False, nocast=True)
    # windows, door
    for k, wx in enumerate((-2.4, -1.6, 0.9, 1.7, 2.5)):
        S.bb(f"wf{k}", wx - 0.28, wx + 0.28, yw - 0.06, yw + 0.04, 0.55, 1.0, "dark", 0.0)
        pulse = idle and not dmg and (k + i) % 2 == 0
        S.bb(f"win{k}", wx - 0.22, wx + 0.22, yw - 0.1, yw - 0.06, 0.6, 0.95, "amber" if (pulse or (idle and not dmg and k % 2)) else "amber_dim", 0.0, nocast=True)
    S.bb("doorf", -0.7, 0.2, yw - 0.1, yw + 0.04, 0.14, 1.12, "dark", 0.0)
    S.bb("door", -0.6, 0.1, yw - 0.14, yw - 0.1, 0.2, 1.06, "steel", 0.02)
    if not dmg:
        S.bb("roofhouse", 1.4, 2.6, y1 - 1.0, y1 - 0.4, H + 0.52, H + 0.56, "house2", 0.0)
    # ---- blueprint rack and samples by the door
    S.bb("rack", -2.9, -1.4, yw - 0.5, yw - 0.3, 0.14, 0.9, "hull2", 0.03)
    for k in range(4):
        S.bb(f"roll{k}", -2.8 + k * 0.35, -2.6 + k * 0.35, yw - 0.5, yw - 0.3, 0.9, 1.0, "gauge", 0.02)
    S.bb("blkA", 0.5, 1.1, yw - 0.7, yw - 0.1, 0.14, 0.55, "concrete2", 0.04)
    S.bb("blkB", 0.6, 1.0, yw - 0.6, yw - 0.2, 0.55, 0.85, "concrete", 0.04)
    # ---- test bench with load frame
    bx0, bx1, by0, by1 = 0.5, 3.0, -2.4, -0.7
    S.bb("bench", bx0, bx1, by0, by1, 0.14, 0.5, "concrete2", 0.04)
    cx, cy = 1.75, -1.55
    if dmg:
        S.bb("spec", cx - 0.35, cx + 0.35, cy - 0.3, cy + 0.3, 0.5, 0.62, "concrete", 0.0)
        for k, (dx_, dy_) in enumerate(((0.5, -0.3), (-0.5, -0.5), (0.2, 0.5))):
            S.box(f"chip{k}", (0.3, 0.25, 0.15), (cx + dx_, (cy + dy_) * YS, 0.58), "concrete_s", 0.0, rot=(0, 0, 30 * k), stretch=False)
        S.bb("col", bx0 + 0.1, bx0 + 0.3, cy - 0.15, cy + 0.15, 0.5, 2.3, "steel", 0.0)
        L.beam(S, "xbar", (bx0 + 0.2, cy, 2.3), (cx + 0.6, cy - 0.6, 2.9 - 0.3), 0.2, "steel")
        S.box("colE", (0.2, 0.3, 1.8), (bx1 - 0.2, (cy + 0.8) * YS, 0.6), "steel", 0.0, rot=(0, 70, 0), stretch=False)
        S.cyl("ramF", 0.14, 0.9, (cx + 0.9, cy - 0.9, 0.3), "steel2", axis="X", verts=10, rot_z=20)
    else:
        S.bb("spec", cx - 0.35, cx + 0.35, cy - 0.3, cy + 0.3, 0.5, 0.78, "concrete", 0.03)
        for sx in (bx0 + 0.1, bx1 - 0.3):
            S.bb("col", sx, sx + 0.2, cy - 0.15, cy + 0.15, 0.5, 2.3, "steel", 0.0)
        S.bb("xbar", bx0 + 0.1, bx1 - 0.1, cy - 0.15, cy + 0.15, 2.3, 2.5, "steel2", 0.02)
        ram = RAM[i % 4] if idle else 0.0
        S.cyl("ram", 0.12, 1.0 - ram, (cx, cy, 2.3 - (1.0 - ram) / 2), "steel", verts=10)
        S.cyl("ramc", 0.2, 0.4, (cx, cy, 2.3 - 0.2 + 0.0), "hull2", verts=12) if False else None
        S.bb("ramh", cx - 0.2, cx + 0.2, cy - 0.2, cy + 0.2, 1.3 - ram + 0.0, 1.45 - ram, "steel2", 0.02)
        S.bb("gaugebox", bx1 - 0.7, bx1 - 0.3, by0 + 0.05, by0 + 0.2, 0.55, 0.95, "hull2", 0.02)
        S.bb("gauge", bx1 - 0.6, bx1 - 0.4, by0 - 0.02, by0 + 0.05, 0.68, 0.84, "gauge", 0.0, nocast=True)
        S.bb("hpanel", bx0 + 0.3, bx0 + 0.9, by0 - 0.02, by0 + 0.05, 0.2, 0.46, "house", 0.0, nocast=True)
    # ---- vane pole and flag
    mx, my = 2.8, -2.95
    S.bb("pole", mx - 0.05, mx + 0.05, my - 0.05, my + 0.05, 0.14, 2.6 if not dmg else 1.4, "steel", 0.0)
    if not dmg:
        a = math.radians(i * 40 if idle else 0)
        S.bb("vane", mx - 0.4 * abs(math.cos(a)) - 0.05, mx + 0.4 * abs(math.cos(a)) + 0.05, my - 0.03, my + 0.03, 2.6, 2.66, "white", 0.0, nocast=True)
        S.bb("flag", mx + 0.05, mx + 0.6, my - 0.02, my + 0.02, 2.0, 2.4, "house", 0.0, nocast=True)
    if dmg:
        R.scorch(S, "scA", 0.5, yw - 0.14, 0.9, 1.5, 0.6)
        R.scorch(S, "scB", 0.0, 1.5, H + 0.2, 1.8, 1.0, 0.03, plane="T")


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_large", x=_a(-0.4, 1.6, 1.8)[0], y=_a(-0.4, 1.6, 1.8)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=_a(1.8, -1.5, 0.7)[0], y=_a(1.8, -1.5, 0.7)[1], phase=4),
    dict(state="damaged", kind="smoke_plume", x=_a(-0.4, 1.6, 1.9)[0], y=_a(-0.4, 1.6, 1.9)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(1.8, -1.5, 0.9)[0], y=_a(1.8, -1.5, 0.9)[1], phase=5),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", -3.1, -2.3, -2.9, -2.1, 0.14, 0.8, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (-1.4, -2.7, z), "steel", axis="X", verts=10)
        S.cyl("barrel", 0.3, 0.7, (-3.0, -1.0, 0.5), "rust", verts=12)
    S.bb("planks", 0.2, 2.0, 3.0, 3.3, 0.14, 0.5, "timber", 0.0)


def _left(S):
    S.bb("planks", -2.8, -1.4, -2.9, -2.6, 0.14, 0.3, "timber", 0.0)
    S.bb("crate", 2.3, 3.1, 3.0, 3.3, 0.14, 0.6, "crate", 0.03)
    L.spark(S, 0.5, -1.5, 1.6, 3, 6)


SPEC = dict(
    pad=(-3.3, 3.3, -3.3, 3.3), env=(-3.0, 3.0, 0.3, 2.9), H=2.0, boxes=[HALL], rings=[],
    clips=(0.9, 1.6), scaf=[(-2.9, -1.7, -0.4, 0.1), (0.2, 1.4, -0.4, 0.1)],
    derrick=(3.1, 2.6, 3.0, (1.5, 1.8, 3.4)), sparks=[(-2.0, 0.3, 1.6), (0.0, 2.8, 1.7), (2.5, 0.3, 1.6)],
    hide=("sten", "win", "flag", "vane", "gauge", "hpanel"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
