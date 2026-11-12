"""Authority Ration Hall: a long brick hall with a ledger window on every wall (2x2 cells). Original design.

A low brick hall under a shallow gable roof, a house-colour fascia band and a house roof panel, a row of ledger hatches (counter
sill, shutter, number plate) and a double door; in the yard a roped queue line with six queuing citizens, a chalk board and a
stack of ration sacks beside the east wall; a brick chimney and a roof ventilator.  Idle loop: the queue shuffles one step
towards the door, the lamp over the door and the hatch shutters alternate.  Damage is structural only (D-069): the east half of
the roof has fallen in, the chimney is snapped, hatches are dark and torn, brick strewn, rope posts down; fire / smoke are FX.
Not a producer: DOCK is the cell below the door.  Contract: see auth_refinery.py; build-site helpers come from auth_conyard.py.
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

ID = "auth_ration_hall"
TITLE = "Authority Ration Hall"
FOOTPRINT = (2, 2)
CANVAS = (128, 168)
ORIGIN = (0, 40)
DOCK = (1, 2)
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = dict(L.MATERIALS)
MATERIALS.update({
    "brick": ("#8E5A44", dict(rough=0.95)),
    "brick2": ("#74472F", dict(rough=0.95)),
    "brick_s": ("#5A3E33", dict(rough=0.95)),
    "sack": ("#BBA47A", dict(rough=1.0)),
    "sack_s": ("#7E6F55", dict(rough=1.0)),
    "coat": ("#3F4B45", dict(rough=0.9)),
    "coat2": ("#5B5440", dict(rough=0.9)),
    "skin": ("#C9A584", dict(rough=0.9)),
    "chalk": ("#2E3A33", dict(rough=0.9)),
    "brass": ("#B38F3C", dict(rough=0.4, metal=0.5)),
})
SOOT_SKIN = dict(L.SOOT_SKIN)
SOOT_SKIN.update({"brick": "brick_s", "brick2": "brick_s", "sack": "sack_s"})
PRIMER_SKIN = dict(L.PRIMER_SKIN)
PRIMER_SKIN.update({"brick": "raw", "brick2": "raw", "sack": "primer", "coat": "primer", "coat2": "primer", "skin": "primer",
                    "chalk": "dark", "brass": "primer"})

HX0, HX1 = -3.0, 3.0
HY0, HY1 = -0.5, 2.9
WH = 1.5                 # eave height
RH = 2.5                 # ridge height
YM = (HY0 + HY1) / 2     # ridge line y
RAIN = (HY1 - HY0) / 2


def slope_pt(f, south=True):
    """(ground y, z) at fraction f (0 eave, 1 ridge) of the south or north roof plane."""
    y = YM - RAIN * (1 - f) if south else YM + RAIN * (1 - f)
    return y, WH + (RH - WH) * f


def roof_slab(S, name, xa, xb, f0, f1, south, m, th=0.14, lift=0.0, bevel=0.03):
    """Rectangle on a roof plane between fractions f0..f1 (thickness th, pushed out by `lift` along the normal)."""
    YS = S.YS
    ya, za = slope_pt(f0, south)
    yb, zb = slope_pt(f1, south)
    wya, wyb = ya * YS, yb * YS
    run = abs(wyb - wya)
    rise = zb - za
    ln = math.hypot(run, rise)
    th_ang = math.degrees(math.atan2(rise, run))
    rot = -th_ang if south else th_ang
    nz = math.cos(math.radians(th_ang)) * lift
    ny = math.sin(math.radians(th_ang)) * lift * (-1 if south else 1)
    return S.box(name, (xb - xa, th, ln), ((xa + xb) / 2, (wya + wyb) / 2 + ny, (za + zb) / 2 + nz), m, bevel,
                 rot=(0, 0, 0), stretch=False) if False else \
        S.box(name, (xb - xa, ln, th), ((xa + xb) / 2, (wya + wyb) / 2 + ny, (za + zb) / 2 + nz), m, bevel,
              rot=(rot, 0, 0), stretch=False)


def sacks(S, x, y, nx, ny, nz, dmg=False):
    for a in range(nx):
        for b in range(ny):
            for c in range(nz):
                if dmg and (a + b * 2 + c) % 3 == 0:
                    continue
                S.bb("sack", x + a * 0.42, x + a * 0.42 + 0.4, y + b * 0.3, y + b * 0.3 + 0.28, 0.14 + c * 0.2, 0.14 + c * 0.2 + 0.2,
                     "sack" if (a + b + c) % 2 == 0 else "sack_s", 0.06)


def citizen(S, k, x, y, m="coat"):
    S.cyl(f"cit{k}", 0.13, 0.5, (x, y, 0.14 + 0.25), m, verts=8)
    S.sphere(f"head{k}", 0.1, (x, y, 0.14 + 0.58), "skin", nocast=True)
    S.cyl(f"hat{k}", 0.115, 0.05, (x, y, 0.14 + 0.66), "dark", verts=8, nocast=True)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    yf = HY0 - 0.03
    S.bb("pad", -3.3, 3.3, -3.3, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -3.3, 3.3, -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    # ---- hall walls on a concrete plinth
    S.bb("hall", HX0, HX1, HY0, HY1, 0.14, WH, "brick", 0.04)
    S.bb("plinth", HX0 - 0.08, HX1 + 0.08, HY0 - 0.1, HY1 + 0.05, 0.14, 0.45, "concrete2", 0.03)
    for k in range(5):        # brick courses on the south face: dark joint lines
        S.bb(f"course{k}", HX0 + 0.05, HX1 - 0.05, yf - 0.01, yf + 0.02, 0.62 + k * 0.17 if False else 0.55 + k * 0.2, 0.58 + k * 0.2, "brick2", 0.0, nocast=True)
    # house fascia band under the eaves (wide, reads at every frame)
    S.bb("fascia", HX0 - 0.02, HX1 + 0.02, yf - 0.1, yf + 0.04, 1.18, 1.5, "house", 0.0)
    S.bb("fasciatrim", HX0 - 0.04, HX1 + 0.04, yf - 0.13, yf - 0.08, 1.14, 1.18, "concrete2", 0.0)
    # ---- roof: two planes + ridge cap, house panel on the south plane
    east_gone = dmg
    xr1 = 0.3 if east_gone else HX1 + 0.2
    roof_slab(S, "roofS", HX0 - 0.2, xr1, 0.0, 1.0, True, "roof", 0.14)
    roof_slab(S, "roofN", HX0 - 0.2, xr1, 0.0, 1.0, False, "roof", 0.14)
    S.bb("ridge", HX0 - 0.2, xr1, YM - 0.12, YM + 0.12, RH - 0.02, RH + 0.1, "concrete2", 0.02)
    if not dmg:
        roof_slab(S, "roofhouse", -1.5, 1.5, 0.15, 0.85, True, "house2", 0.05, lift=0.08, bevel=0.0)
    else:
        roof_slab(S, "roofhouse", -1.5, -0.1, 0.15, 0.85, True, "house2", 0.05, lift=0.08, bevel=0.0)
    # ---- ledger hatches along the south wall: sill, frame, shutter, number plate
    for k, wx in enumerate((-2.4, -1.5, 1.5, 2.4)):
        S.bb(f"hf{k}", wx - 0.34, wx + 0.34, yf - 0.06, yf + 0.04, 0.6, 1.1, "dark", 0.0)
        broken = dmg and k >= 2
        if not broken:
            open_ = idle and (k + i) % 2 == 0
            if open_:
                S.bb(f"hopen{k}", wx - 0.28, wx + 0.28, yf - 0.08, yf - 0.05, 0.64, 1.05, "amber", 0.0, nocast=True)
            else:
                S.bb(f"hsh{k}", wx - 0.3, wx + 0.3, yf - 0.1, yf - 0.06, 0.66, 1.08, "steel", 0.0)
        S.bb(f"hsill{k}", wx - 0.4, wx + 0.4, yf - 0.28, yf - 0.04, 0.52, 0.62, "concrete2", 0.02)
        S.bb(f"hplate{k}", wx - 0.12, wx + 0.12, yf - 0.1, yf - 0.06, 1.12, 1.18, "brass", 0.0, nocast=True)
    # double door
    S.bb("doorf", -0.62, 0.62, yf - 0.08, yf + 0.04, 0.14, 1.12, "dark", 0.0)
    S.bb("door", -0.5, 0.5, yf - 0.14, yf - 0.08, 0.14, 1.06, "steel", 0.02)
    S.bb("doorsplit", -0.02, 0.02, yf - 0.16, yf - 0.14, 0.2, 1.04, "dark", 0.0, nocast=True)
    for sx in (-1, 1):
        if dmg and sx > 0:
            S.bb("dpanel", 0.7, 1.0, yf - 0.1, yf - 0.04, 0.2, 0.8, "house", 0.0)
        else:
            S.bb("dpanel", 0.7 * sx - (0.0 if sx > 0 else 0.4), 0.7 * sx + (0.4 if sx > 0 else 0.0), yf - 0.1, yf - 0.04, 0.2, 1.05, "house", 0.0)
    lamp_on = idle and not dmg and (i % 2 == 0)
    S.cyl("doorlamp", 0.1, 0.12, (0, yf - 0.2, 1.3), "amber" if lamp_on else "amber_dim", verts=8, nocast=True)
    S.bb("sign", -0.55, 0.55, yf - 0.14, yf - 0.08, 1.17, 1.28, "white", 0.0, nocast=True)
    for k in range(5):
        S.bb(f"signb{k}", -0.45 + k * 0.2, -0.35 + k * 0.2, yf - 0.15, yf - 0.14, 1.19, 1.26, "dark", 0.0, nocast=True)
    # ---- chimney and roof vent
    if not dmg:
        S.bb("chim", 1.9, 2.5, 1.7, 2.3, WH, 3.1, "brick", 0.03)
        S.bb("chimcap", 1.82, 2.58, 1.62, 2.38, 3.1, 3.25, "concrete2", 0.02)
        S.bb("chimhouse", 1.9, 2.5, 1.7, 1.74, 2.55, 2.75, "house", 0.0, nocast=True)
    else:
        S.bb("chim", 1.9, 2.5, 1.7, 2.3, WH, 2.0, "brick", 0.03)
        for k, (dx, dy, rz) in enumerate(((0.1, -0.7, 20), (0.7, -0.9, -35), (-0.4, -0.6, 50))):
            S.bb(f"chimrub{k}", 2.0 + dx, 2.4 + dx, 1.6 + dy, 1.9 + dy, 0.14, 0.34, "brick", 0.04, rot=(0, 0, rz))
    S.bb("vent", -0.4, 0.4, YM + 0.2, YM + 0.8, RH, RH + 0.35, "concrete2", 0.03)
    S.bb("ventgr", -0.34, 0.34, YM + 0.18, YM + 0.8, RH + 0.35, RH + 0.4, "dark", 0.0)
    # ---- queue line in the yard: posts, rope, floor line, citizens
    for k in range(6):
        px_ = -1.0 if k < 5 else -1.0
        py_ = -0.95 - k * 0.5
        for sx in (-0.62, 0.62):
            if dmg and ((k + (sx > 0)) % 3 == 0):
                S.bb("stanch", px_ + sx - 0.12, px_ + sx + 0.12, py_ - 0.04, py_ + 0.04, 0.14, 0.2, "steel", 0.0, rot=(0, 0, 25))
                continue
            S.cyl("stanch", 0.045, 0.7, (px_ + sx, py_, 0.5), "steel", verts=6)
            S.cyl("stanchb", 0.1, 0.05, (px_ + sx, py_, 0.17), "dark", verts=8, nocast=True)
            S.sphere("stanchk", 0.07, (px_ + sx, py_, 0.88), "brass", nocast=True)
        if k < 5 and not dmg:
            for sx in (-0.62, 0.62):
                S.bb("rope", px_ + sx - 0.015, px_ + sx + 0.015, py_ - 0.5, py_, 0.74, 0.77, "house", 0.0, nocast=True)
    S.bb("qline", -1.7, -0.3, -3.1, -0.65, 0.14, 0.145, "yellow", 0.0, nocast=True) if False else None
    S.bb("queuestrip", -1.62, -1.58, -3.1, -0.65, 0.14, 0.16, "yellow", 0.0, nocast=True)
    S.bb("queuestrip", -0.42, -0.38, -3.1, -0.65, 0.14, 0.16, "yellow", 0.0, nocast=True)
    if not dmg:
        for k in range(6):
            yy = -0.95 - 0.5 * k + (0.125 * i if idle else 0.0)
            if yy > -0.62:
                continue
            citizen(S, k, -1.0 + (0.05 if k % 2 else -0.05), yy, "coat" if k % 3 else "coat2")
    else:
        S.bb("fallen", -1.2, -0.8, -2.0, -1.7, 0.14, 0.26, "coat", 0.05, rot=(0, 0, 20))
        S.bb("papers", -1.6, -0.5, -1.4, -1.0, 0.14, 0.16, "white", 0.0, nocast=True, rot=(0, 0, 15))
    # ---- east side: ration sacks and a chalk board
    sacks(S, 1.5, -2.7, 3, 2, 3 if not dmg else 2, dmg)
    S.bb("boardp", 2.75, 2.85, -2.9, -2.8, 0.14, 1.1, "timber", 0.0)
    S.bb("boardp", 3.05, 3.15, -2.9, -2.8, 0.14, 1.1, "timber", 0.0)
    if not dmg:
        S.bb("board", 2.7, 3.2, -2.95, -2.88, 0.7, 1.3, "chalk", 0.02)
        for k in range(3):
            S.bb(f"chalk{k}", 2.78, 3.1 - 0.1 * k, -2.96, -2.95, 0.85 + k * 0.14, 0.88 + k * 0.14, "white", 0.0, nocast=True)
        S.bb("boardhouse", 2.7, 3.2, -2.97, -2.95, 1.3, 1.38, "house", 0.0, nocast=True)
    # ---- damage
    if dmg:
        # fallen roof east half: sagging planes, bent rafters, brick spill
        yr = YM * YS
        S.box("sagS", (2.9, 1.9, 0.12), (2.0, (YM - 0.8) * YS - 0.0, 1.0), "roof", 0.0, rot=(32, 0, 6), stretch=False)
        S.box("sagN", (2.5, 1.6, 0.12), (2.1, (YM + 0.9) * YS, 0.85), "roof", 0.0, rot=(-22, 0, -8), stretch=False)
        S.bb("rubE", 0.6, 3.3, 0.2, 2.8, WH, WH + 0.05, "dark", 0.0, nocast=True)
        for k in range(4):
            L.beam(S, f"raft{k}", (0.5 + 0.7 * k, YM - 0.6, 1.8 + 0.1 * k), (0.9 + 0.7 * k, YM + 0.9, 1.0 - 0.1 * k), 0.12, "timber")
        for k, (bx, by, rz) in enumerate(((1.2, -0.9, 10), (2.6, -1.2, -30), (3.0, -0.4, 45), (0.3, -1.5, 5))):
            S.bb(f"brk{k}", bx - 0.3, bx + 0.3, by - 0.2, by + 0.2, 0.14, 0.38, "brick", 0.05, rot=(0, 0, rz))
        S.bb("breach", 1.0, 2.0, yf - 0.05, yf + 0.0, 0.3, 1.45, "dark", 0.0)
        R.scorch(S, "scW", 1.5, yf - 0.16, 0.9, 1.4, 0.9)
        R.scorch(S, "scR", 0.0, YM, RH + 0.05, 1.2, 0.8, 0.04, plane="T") if False else None


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_large", x=_a(2.0, 1.2, 1.3)[0], y=_a(2.0, 1.2, 1.3)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=_a(2.1, 2.1, 0.9)[0], y=_a(2.1, 2.1, 0.9)[1], phase=3),
    dict(state="damaged", kind="fire_small", x=_a(-1.2, 1.0, 1.9)[0], y=_a(-1.2, 1.0, 1.9)[1], phase=5),
    dict(state="damaged", kind="smoke_plume", x=_a(2.0, 1.2, 1.5)[0], y=_a(2.0, 1.2, 1.5)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(2.2, 2.0, 2.0)[0], y=_a(2.2, 2.0, 2.0)[1], phase=5),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.3, 3.1, -2.9, -2.1, 0.14, 0.8, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (-1.5, -2.9, z), "steel", axis="X", verts=10)
        S.cyl("barrel", 0.3, 0.7, (3.0, -1.2, 0.5), "rust", verts=12)
    S.bb("planks", 0.4, 2.2, 3.0, 3.3, 0.14, 0.5, "timber", 0.0)


def _left(S):
    S.bb("planks", -3.0, -1.6, -3.0, -2.7, 0.14, 0.3, "timber", 0.0)
    S.bb("crate", 2.3, 3.1, -2.2, -1.5, 0.14, 0.8, "crate", 0.03)
    L.spark(S, 0.0, -0.6, 1.5, 3, 6)


SPEC = dict(
    pad=(-3.3, 3.3, -3.3, 3.3), env=(HX0, HX1, HY0, HY1), H=2.5, boxes=[(HX0, HX1, HY0, HY1)], rings=[],
    clips=(1.0, 1.9), scaf=[(2.3, 3.2, -1.4, -0.7), (-3.2, -2.35, -1.4, -0.7)],
    derrick=(-3.0, 1.9, 2.8, (-0.3, 1.4, 3.2)), sparks=[(-2.0, -0.5, 1.7), (2.0, 2.0, 2.1), (0.0, 2.8, 2.1)],
    hide=("cit", "head", "hat", "rope", "doorlamp", "sign", "hopen", "board", "chalk"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
