"""Authority Signals Headquarters: a squat concrete command and communications centre bristling with aerials (3x3 cells). Original design.

A heavy two-tier concrete block (operations floor below, a narrower relay floor above) with slit windows lit amber, a house fascia band
and house door panels; on the roof a rotating dish, a radar paddle, a bank of whip aerials and a roof house panel; at the north-east
corner a lattice mast with a red beacon and a cross-arm of aerial wires; a generator shed and fuel drums on the yard side, cable
runs, a sandbagged sentry post.  Idle loop: dish and paddle turn, beacon blinks, a few windows pulse.  Active (played while it
researches): all windows lit, the dish locks on, signal arcs stand over the mast, beacons alternate fast.
Damage is structural only (D-069): relay floor half collapsed, mast bent over, dish torn off, windows dark; fire / smoke are FX.
Not a unit producer: DOCK is the cell below the door.  Contract: see auth_refinery.py; build-site helpers come from auth_conyard.py.
Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import importlib.util
import math
import os

_HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structlib_auth_conyard", os.path.join(_HERE, "auth_conyard.py"))
L = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(L)
R = L.R

ID = "auth_signals_hq"
TITLE = "Authority Signals Headquarters"
FOOTPRINT = (3, 3)
CANVAS = (192, 256)
ORIGIN = (0, 64)
DOCK = (1, 3)
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5), ("active", 4, 3)]
MATERIALS = dict(L.MATERIALS)
MATERIALS.update({
    "steel2": ("#979EA3", dict(rough=0.4, metal=0.5)),
    "beacon": ("#E0452E", dict(rough=0.3, emit="#FF4A30")),
    "cable": ("#23262A", dict(rough=0.8)),
    "arc": ("#BFEFFF", dict(rough=0.3, emit="#9FE0FF")),
    "drum": ("#5B6A4A", dict(rough=0.6, metal=0.3)),
})
SOOT_SKIN = dict(L.SOOT_SKIN)
SOOT_SKIN.update({"steel2": "steel_s", "drum": "hull_s"})
PRIMER_SKIN = dict(L.PRIMER_SKIN)
PRIMER_SKIN.update({"steel2": "primer", "beacon": "dark", "arc": "dark", "drum": "primer", "cable": "dark"})

BX0, BX1, BY0, BY1 = -4.2, 2.4, -2.6, 3.0     # lower block
UH = 2.3                                       # lower block height
UX0, UX1, UY0, UY1 = -3.4, 1.6, -1.6, 2.4      # relay floor
TOPH = 3.9
MAST = (3.7, 2.6)
MAST_H = 5.7
DISH = (-1.3, 0.6)


def finished(S, i, dmg, mode):
    idle = mode in ("idle", "active")
    act = mode == "active"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    yf = BY0 - 0.03
    S.bb("pad", -5.0, 5.0, -5.0, 5.0, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    for k in range(20):
        S.bb(f"haz{k}", -5.0 + k * 0.5, -5.0 + (k + 1) * 0.5, -4.88, -4.55, 0.14, 0.2, "yellow" if k % 2 == 0 else "dark", 0.0)
    # ---- lower block
    S.bb("blk", BX0, BX1, BY0, BY1, 0.14, UH, "concrete", 0.05)
    S.bb("plinth", BX0 - 0.1, BX1 + 0.1, BY0 - 0.12, BY1 + 0.05, 0.14, 0.5, "concrete2", 0.03)
    S.bb("slab", BX0 - 0.15, BX1 + 0.15, BY0 - 0.15, BY1 + 0.1, UH, UH + 0.2, "concrete2", 0.04)
    S.bb("fascia", BX0 - 0.02, BX1 + 0.02, yf - 0.1, yf + 0.04, 1.65, 2.05, "house", 0.0)
    # slit windows, lit amber
    for k in range(7):
        wx = BX0 + 0.55 + k * 0.9
        if abs(wx + 1.0) < 0.9 and False:
            continue
        S.bb(f"wf{k}", wx - 0.28, wx + 0.28, yf - 0.05, yf + 0.04, 0.85, 1.4, "dark", 0.0)
        if dmg and k in (4, 5):
            continue
        lit = act or (idle and (k + i) % 3 != 0)
        S.bb(f"win{k}", wx - 0.22, wx + 0.22, yf - 0.09, yf - 0.05, 0.9, 1.35, "amber" if lit else "amber_dim", 0.0, nocast=True)
    # door: heavy steel door, house panels, canopy
    dx = -1.0
    S.bb("doorf", dx - 0.6, dx + 0.6, yf - 0.1, yf + 0.04, 0.14, 1.5, "dark", 0.0)
    S.bb("door", dx - 0.5, dx + 0.5, yf - 0.16, yf - 0.1, 0.14, 1.4, "steel", 0.02)
    S.bb("canopy", dx - 0.9, dx + 0.9, yf - 0.55, yf - 0.1, 1.55, 1.7, "concrete2", 0.03)
    for sx in (-1, 1):
        if dmg and sx > 0:
            continue
        S.bb("canpost", dx + sx * 0.82 - 0.06, dx + sx * 0.82 + 0.06, yf - 0.55, yf - 0.43, 0.14, 1.55, "steel", 0.0)
    S.bb("candeck", dx - 0.9, dx + 0.9, yf - 0.57, yf - 0.53, 1.62, 1.72, "house", 0.0, nocast=True)
    # ---- relay floor (collapsed on the east half when damaged)
    ux1 = -0.4 if dmg else UX1
    S.bb("up", UX0, ux1, UY0, UY1, UH + 0.2, TOPH, "concrete", 0.05)
    S.bb("upslab", UX0 - 0.12, ux1 + (0.0 if dmg else 0.12), UY0 - 0.12, UY1 + 0.1, TOPH, TOPH + 0.18, "concrete2", 0.04)
    S.bb("upband", UX0, ux1, UY0 - 0.1, UY0 - 0.04, 3.05, 3.5, "house", 0.0)
    for k in range(3):
        wx = UX0 + 0.6 + k * 1.3
        if wx > ux1 - 0.3:
            continue
        S.bb(f"uw{k}", wx - 0.3, wx + 0.3, UY0 - 0.06, UY0 - 0.01, 2.6, 2.95, "dark", 0.0)
        lit = act or (idle and (k + i) % 2 == 0 and not dmg)
        S.bb(f"uwl{k}", wx - 0.24, wx + 0.24, UY0 - 0.09, UY0 - 0.06, 2.65, 2.9, "amber" if lit else "amber_dim", 0.0, nocast=True)
    # roof house panel (large flat area, mid-ramp house2)
    if not dmg:
        S.bb("roofhouse", UX0 + 0.4, UX1 - 0.4, UY0 + 0.4, UY0 + 1.5, TOPH + 0.18, TOPH + 0.22, "house2", 0.0)
    else:
        S.bb("roofhouse", UX0 + 0.4, ux1 - 0.2, UY0 + 0.4, UY0 + 1.5, TOPH + 0.18, TOPH + 0.22, "house2", 0.0)
    # lower-roof house panel on the east terrace
    S.bb("terrace", -3.4, 1.4, -2.35, -1.85, UH + 0.2, UH + 0.23, "house2", 0.0)
    # ---- aerial bank: whips on the lower roof
    for k in range(5):
        ax = 0.0 + k * 0.4
        ay = 1.8 + (k % 2) * 0.35
        h = 1.2 + 0.25 * (k % 3)
        if dmg and k % 2:
            L.beam(S, f"whip{k}", (ax, ay, UH + 0.2), (ax + 0.5, ay - 0.3, UH + 0.2 + h * 0.5), 0.05, "steel")
            continue
        S.cyl(f"whip{k}", 0.03, h, (ax, ay, UH + 0.2 + h / 2), "steel", verts=6)
        S.cyl(f"whipb{k}", 0.08, 0.12, (ax, ay, UH + 0.26), "dark", verts=8, nocast=True)
    # ---- dish on the relay roof (rotates), radar paddle
    dxx, dyy = DISH
    zr = TOPH + 0.18
    if not dmg:
        S.cyl("dishbase", 0.28, 0.4, (dxx, dyy, zr + 0.2), "concrete2", verts=10)
        S.cyl("dishmast", 0.1, 0.5, (dxx, dyy, zr + 0.6), "steel", verts=8)
        ang = (i * 40 if not act else i * 90) % 360
        S.box("dishb", (1.5, 0.12, 1.0), (dxx, dyy * YS, zr + 1.05), "steel2", 0.02, rot=(-18, 0, ang), stretch=False)
        a = math.radians(ang)
        S.box("dishrim", (1.4, 0.08, 0.14), (dxx, dyy * YS, zr + 1.6), "white", 0.0, rot=(-18, 0, ang), stretch=False, nocast=True)
        S.cyl("feed", 0.06, 0.5, (dxx + 0.55 * math.sin(a), dyy * YS + 0.55 * math.cos(a), zr + 1.1), "dark", verts=6, nocast=True, stretch=False) if False else None
        S.bb("dishhouse", dxx - 0.18, dxx + 0.18, dyy - 0.06, dyy + 0.06, zr + 0.92, zr + 1.2, "house", 0.0, nocast=True)
        S.bb("radarp", 0.1, 0.7, 1.0, 1.1, zr, zr + 0.3, "steel", 0.0) if False else None
    else:
        S.cyl("dishbase", 0.28, 0.4, (dxx, dyy, zr + 0.2), "concrete2", verts=10)
        L.beam(S, "dishmastb", (dxx, dyy, zr + 0.3), (dxx - 0.5, dyy - 0.3, zr + 0.9), 0.1, "steel")
        S.box("dishfall", (1.5, 1.0, 0.1), (-2.4, -2.0 * YS, TOPH * 0.55), "steel2", 0.02, rot=(20, 15, 25), stretch=False)
    # radar paddle on the lower roof
    px, py = 2.0, -0.1
    S.cyl("radbase", 0.2, 0.5, (px, py, UH + 0.45), "steel", verts=8)
    if not dmg:
        ang = (i * 55) % 360 if idle else 0
        S.box("paddle", (0.7, 0.08, 0.45), (px, py * YS, UH + 0.95), "steel2", 0.0, rot=(0, 0, ang), stretch=False)
        S.box("paddlehouse", (0.3, 0.09, 0.2), (px, py * YS, UH + 0.95), "house", 0.0, rot=(0, 0, ang), stretch=False, nocast=True)
    # ---- lattice mast, north-east corner
    mx, my = MAST
    top = MAST_H if not dmg else 2.6
    for ax in (-0.15, 0.15):
        for ay in (-0.15, 0.15):
            L.post(S, "mast", mx + ax, my + ay, 0.14, top, 0.08, "steel")
    z = 0.14
    while z < top - 0.4:
        L.beam(S, "mx", (mx - 0.15, my - 0.15, z), (mx + 0.15, my - 0.15, z + 0.8), 0.04, "steel")
        L.beam(S, "mx", (mx + 0.15, my - 0.15, z), (mx + 0.15, my + 0.15, z + 0.8), 0.04, "steel")
        z += 0.8
    S.bb("mastbase", mx - 0.4, mx + 0.4, my - 0.4, my + 0.4, 0.14, 0.45, "concrete2", 0.03)
    if not dmg:
        S.bb("xarm", mx - 0.9, mx + 0.9, my - 0.05, my + 0.05, MAST_H - 1.2, MAST_H - 1.1, "steel", 0.0)
        S.bb("xarm2", mx - 0.6, mx + 0.6, my - 0.05, my + 0.05, MAST_H - 2.3, MAST_H - 2.2, "steel", 0.0)
        S.bb("mastband", mx - 0.2, mx + 0.2, my - 0.2, my - 0.14, 3.0, 3.6, "house", 0.0, nocast=True)
        on = (i % 2 == 0) if mode == "idle" else (i % 2 == 0 or i == 1)
        S.cyl("beacon", 0.16, 0.22, (mx, my, MAST_H + 0.12), "beacon" if on else "dark", verts=10, nocast=True)
        # guy wires to the roof
        L.beam(S, "guy", (mx, my, MAST_H - 1.0), (BX1 - 0.2, BY1 - 0.2, UH + 0.2), 0.03, "cable", nocast=True)
        L.beam(S, "guy2", (mx, my, MAST_H - 1.0), (mx + 1.2, my - 2.2, 0.2), 0.03, "cable", nocast=True)
        if act:
            for k in range(3):
                S.cyl(f"arc{k}", 0.22 + 0.14 * ((i + k) % 3), 0.04, (mx, my, MAST_H + 0.45 + 0.3 * k), "arc", verts=16, nocast=True)
    else:
        L.beam(S, "mastsnap", (mx, my, 2.6), (mx - 1.6, my - 1.6, 0.4), 0.1, "steel")
        L.beam(S, "mastsnap2", (mx + 0.1, my, 2.6), (mx + 1.0, my - 1.8, 0.5), 0.07, "steel")
    # ---- generator shed, drums, cable runs, sentry post
    S.bb("shed", 2.9, 4.5, -2.9, -1.5, 0.14, 1.2, "panel", 0.03)
    S.bb("shedroof", 2.8, 4.6, -3.0, -1.4, 1.2, 1.32, "roof", 0.03)
    S.bb("shedvent", 3.1, 3.7, -2.9, -2.86, 0.4, 0.8, "dark", 0.0, nocast=True)
    S.bb("shedhouse", 3.9, 4.5, -3.0, -2.9, 0.6, 1.1, "house", 0.0, nocast=True)
    for k, (dx_, dy_) in enumerate(((2.9, -0.8), (3.4, -0.5), (3.9, -0.8))):
        if dmg and k == 1:
            S.bb("drumfall", dx_ - 0.35, dx_ + 0.35, dy_ - 0.2, dy_ + 0.2, 0.14, 0.4, "drum", 0.0, rot=(0, 0, 20))
            continue
        S.cyl(f"drum{k}", 0.28, 0.75, (dx_, dy_, 0.52), "drum", verts=12)
    S.bb("cabl", BX1, BX1 + 1.3, 1.8, 1.95, 0.14, 0.22, "cable", 0.0, nocast=True)
    S.bb("cabl2", 3.55, 3.7, 1.8, 2.5, 0.14, 0.22, "cable", 0.0, nocast=True)
    # sentry post with sandbags
    for k in range(6):
        S.bb(f"sb{k}", -4.7 + (k % 3) * 0.5, -4.25 + (k % 3) * 0.5, -3.7 - 0.3 * (k // 3), -3.45 - 0.3 * (k // 3), 0.14 + 0.2 * (k // 3), 0.34 + 0.2 * (k // 3),
             "sandbag" if k % 2 else "sandbag2", 0.05) if not (dmg and k == 4) else None
    if dmg:
        for k, (bx, by, rz) in enumerate(((-2.0, 3.6, 20), (-0.5, 3.7, -30), (1.2, 3.5, 45), (-3.4, 2.7, 10))):
            S.bb(f"rub{k}", bx - 0.4, bx + 0.4, by - 0.25, by + 0.25, 0.14, 0.45, "concrete", 0.05, rot=(0, 0, rz))
        S.bb("rubu", -0.4, 1.8, -1.2, 2.2, UH + 0.2, UH + 0.55, "concrete2", 0.1, rot=(0, 0, 8))
        S.box("slabf", (2.0, 1.5, 0.25), (0.7, 0.4 * YS, UH + 0.8), "concrete", 0.04, rot=(12, -12, 6), stretch=False)
        S.bb("breach", -0.5, 0.5, UY0 - 0.08, UY0 - 0.03, 2.4, 3.8, "dark", 0.0)
        R.scorch(S, "scL", -2.6, yf - 0.16, 1.0, 1.4, 1.0)
        R.scorch(S, "scT", -2.0, 0.5, TOPH + 0.2, 1.6, 1.0, 0.04, plane="T")


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_large", x=_a(0.7, 0.6, UH + 0.7)[0], y=_a(0.7, 0.6, UH + 0.7)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=_a(-2.6, 0.5, TOPH + 0.3)[0], y=_a(-2.6, 0.5, TOPH + 0.3)[1], phase=3),
    dict(state="damaged", kind="fire_small", x=_a(3.7, 2.6, 2.6)[0], y=_a(3.7, 2.6, 2.6)[1], phase=5),
    dict(state="damaged", kind="smoke_plume", x=_a(0.7, 0.6, UH + 0.9)[0], y=_a(0.7, 0.6, UH + 0.9)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(3.7, 2.6, 2.8)[0], y=_a(3.7, 2.6, 2.8)[1], phase=6),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.6, 3.4, -4.4, -3.6, 0.14, 0.8, "crate", 0.03)
        S.bb("crate", 2.75, 3.3, -4.3, -3.7, 0.8, 1.3, "crate", 0.03, rot=(0, 0, 12))
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.6, (-2.0, -4.0, z), "steel", axis="X", verts=10)
        S.cyl("barrel", 0.3, 0.7, (4.2, -3.8, 0.5), "rust", verts=12)
    S.bb("planks", -4.0, -2.0, 4.1, 4.4, 0.14, 0.5, "timber", 0.0)


def _left(S):
    S.bb("planks", -4.5, -3.0, -4.3, -4.0, 0.14, 0.3, "timber", 0.0)
    S.bb("crate", 3.0, 3.9, -4.2, -3.4, 0.14, 0.8, "crate", 0.03)
    L.spark(S, 0.0, -2.8, 1.9, 3, 6)


SPEC = dict(
    pad=(-5.0, 5.0, -5.0, 5.0), env=(BX0, BX1, BY0, BY1), H=TOPH, boxes=[(BX0, BX1, BY0, BY1), (UX0, UX1, UY0, UY1)], rings=[],
    clips=(1.6, 3.0), scaf=[(2.7, 3.6, -3.6, -2.9), (-4.6, -3.7, -3.6, -2.9)],
    derrick=(-4.6, 3.8, 5.0, (-2.0, 2.4, 5.4)), sparks=[(-3.0, -2.6, 2.0), (1.0, 3.0, 2.2), (-1.0, 3.0, 3.0)],
    hide=("mx", "win", "uwl", "dish", "paddle", "beacon", "arc", "xarm", "mast", "guy", "whip", "terrace", "shedhouse"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    if state == "active":
        finished(S, i, False, "active")
        return
    L.stages(S, state, i, SPEC, finished)
