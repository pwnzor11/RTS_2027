"""Authority Blockhouse: a low concrete garrison shell with firing slits and no gun of its own (2x2 cells). Original design.

A squat, thick-walled block with a stepped, sloping roof slab, a row of five firing slits under a house band, a steel door at the
east end between house panels, a roof hatch with a periscope and a whip aerial, sandbag aprons at the corners, a ground-level
spoil berm round the back, a small door lamp.  Idle loop: the periscope turns, the door lamp and an aerial tip blink.
It has no weapon and no turret (rules: primary_weapon empty, garrison 5), so there is no fire state.
Damage is structural only (D-069): one roof corner blown in leaving rebar, a split down the front, a slit knocked out, sandbags
scattered; fire / smoke are FX anchors.  DOCK: the cell below the door end (garrison units enter there).
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

ID = "auth_blockhouse"
TITLE = "Authority Blockhouse"
FOOTPRINT = (2, 2)
CANVAS = (128, 144)
ORIGIN = (0, 24)
DOCK = (1, 2)
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = dict(L.MATERIALS)
MATERIALS["steel2"] = ("#979EA3", dict(rough=0.4, metal=0.5))
MATERIALS["gunmetal"] = ("#2A2E31", dict(rough=0.35, metal=0.7))
MATERIALS["beacon"] = ("#E0452E", dict(rough=0.3, emit="#FF4A30"))
SOOT_SKIN = dict(L.SOOT_SKIN)
PRIMER_SKIN = dict(L.PRIMER_SKIN)
PRIMER_SKIN.update({"steel2": "primer", "gunmetal": "dark", "beacon": "dark"})

X0, X1 = -2.7, 2.7
Y0, Y1 = -1.3, 2.3
H = 1.5
RT = 0.35


def sandbags(S, x0, x1, y, rows, dmg=False, seedk=0):
    nb = int((x1 - x0) / 0.5)
    for r in range(rows):
        for k in range(nb):
            if dmg and ((k * 7 + r * 3 + seedk) % 5 == 0 or (r > 1 and k % 2)):
                continue
            x = x0 + 0.25 + k * 0.5 + (0.25 if r % 2 else 0)
            if x > x1 - 0.1:
                continue
            m = "sandbag" if (k + r) % 2 == 0 else "sandbag2"
            S.bb("sbag", x - 0.24, x + 0.24, y - 0.17, y + 0.17, 0.14 + 0.2 * r, 0.14 + 0.2 * (r + 1), m, 0.05)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    yf = Y0 - 0.03
    S.bb("pad", -3.3, 3.3, -3.3, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -3.3, 3.3, -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    # ---- the shell: thick walls, a stepped roof (lower step, upper step), chamfer blocks at the corners
    S.bb("shell", X0, X1, Y0, Y1, 0.14, H, "concrete", 0.06)
    S.bb("foot", X0 - 0.12, X1 + 0.12, Y0 - 0.14, Y1 + 0.08, 0.14, 0.4, "concrete2", 0.03)
    S.bb("roof1", X0 - 0.1, X1 + 0.1, Y0 - 0.1, Y1 + 0.1, H, H + 0.2, "concrete2", 0.05)
    xu1 = 0.6 if dmg else X1 - 0.3
    S.bb("roof2", X0 + 0.3, xu1, Y0 + 0.25, Y1 - 0.25, H + 0.2, H + RT, "concrete", 0.06)
    # house band above the slits, house roof stripe on the upper step
    S.bb("fascia", X0, X1, yf - 0.1, yf + 0.04, 1.1, 1.4, "house", 0.0)
    S.bb("roofhouse", X0 + 0.6, xu1 - (0.0 if not dmg else 0.2), Y0 + 0.6, Y0 + 1.5, H + RT, H + RT + 0.04, "house2", 0.0)
    # ---- five firing slits, deep, with lintel
    for k in range(5):
        sx = -2.1 + k * 0.78
        if sx > 1.5:
            continue
        S.bb(f"slit{k}", sx - 0.32, sx + 0.32, yf - 0.06, yf + 0.04, 0.55, 0.88, "dark", 0.0)
        S.bb(f"slitl{k}", sx - 0.4, sx + 0.4, yf - 0.16, yf - 0.04, 0.88, 0.98, "concrete2", 0.02)
        S.bb(f"slits{k}", sx - 0.36, sx + 0.36, yf - 0.1, yf - 0.04, 0.48, 0.55, "concrete2", 0.02)
        if dmg and k == 3:
            S.bb("slitbig", sx - 0.5, sx + 0.7, yf - 0.08, yf + 0.0, 0.5, 1.4, "dark", 0.0)
    # ---- door at the east end: steel door between house panels, lamp
    dx = 2.05
    S.bb("doorf", dx - 0.4, dx + 0.4, yf - 0.1, yf + 0.04, 0.14, 1.0, "dark", 0.0)
    S.bb("door", dx - 0.32, dx + 0.32, yf - 0.16, yf - 0.1, 0.14, 0.95, "steel", 0.02)
    lamp = idle and not dmg and i % 2 == 0
    S.cyl("lamp", 0.08, 0.1, (dx, yf - 0.2, 1.05), "amber" if lamp else "amber_dim", verts=8, nocast=True)
    # ---- roof hatch with periscope and whip
    S.bb("hatch", -1.9, -1.2, 0.5, 1.2, H + RT, H + RT + 0.2, "steel", 0.03) if not dmg else None
    if not dmg:
        S.cyl("pscope", 0.07, 0.6, (-1.55, 0.85, H + RT + 0.5), "steel", verts=8)
        a = math.radians((i * 90) if idle else 0)
        S.box("pshead", (0.34, 0.14, 0.14), (-1.55 + 0.1 * math.sin(a), 0.85 * YS, H + RT + 0.85), "gunmetal" if "gunmetal" in MATERIALS else "dark", 0.0, rot=(0, 0, -math.degrees(a)), stretch=False)
        S.cyl("whip", 0.025, 1.3, (1.2, 1.6, H + RT + 0.65), "steel", verts=6)
        S.sphere("whiptip", 0.06, (1.2, 1.6, H + RT + 1.33), "beacon" if (idle and i % 2) else "dark", nocast=True) if "beacon" in MATERIALS else None
        S.bb("vent", 0.0, 0.6, 1.3, 1.9, H + RT, H + RT + 0.25, "concrete2", 0.04)
    # ---- sandbag aprons at the front corners and a berm of spoil behind
    sandbags(S, -3.2, -1.7, -1.75, 2, dmg, 0)
    sandbags(S, 1.0, 1.5, -1.75, 2, dmg, 2) if False else None
    S.bb("berm", X0 - 0.2, X1 + 0.2, Y1 + 0.1, Y1 + 0.6, 0.14, 0.55, "soil", 0.3)
    S.bb("bermt", X0, X1, Y1 + 0.2, Y1 + 0.5, 0.5, 0.62, "ash", 0.2)
    # ---- damage
    if dmg:
        S.bb("hole", 0.6, 2.4, 0.2, 2.0, H + 0.2, H + RT + 0.02, "dark", 0.0)
        for k, (jx, jz) in enumerate(((0.7, 0.35), (1.3, 0.55), (2.0, 0.3))):
            S.bb(f"jag{k}", jx - 0.25, jx + 0.25, 0.1, 0.4, H + 0.2, H + 0.2 + jz, "concrete", 0.0, rot=(0, 0, 12 * k))
        for k, (rx, ry) in enumerate(((1.0, 1.3), (1.5, 1.6), (1.9, 1.1))):
            L.beam(S, f"rebar{k}", (rx, ry, H + 0.2), (rx + 0.2, ry - 0.3, H + 0.9), 0.05, "rebar")
        S.bb("rubR", 0.9, 2.9, -2.2, -1.5, 0.14, 0.45, "concrete2", 0.1, rot=(0, 0, 10))
        S.bb("rubR2", 2.2, 3.1, -1.5, -0.6, 0.14, 0.4, "concrete", 0.08, rot=(0, 0, -20))
        S.bb("split", -0.1, 0.0, yf - 0.12, yf - 0.08, 0.15, 1.45, "crack", 0.0, nocast=True)
        S.box("slabf", (1.4, 1.0, 0.22), (1.6, -2.0 * YS, 0.4), "concrete", 0.04, rot=(8, 10, 25), stretch=False)
        R.scorch(S, "scF", 0.3, yf - 0.16, 0.8, 1.6, 0.8)
        R.scorch(S, "scR", 1.8, 0.5, H + RT + 0.05, 1.3, 0.9, 0.04, plane="T")


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_large", x=_a(1.5, 1.2, H + 0.5)[0], y=_a(1.5, 1.2, H + 0.5)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=_a(0.4, -0.9, 1.0)[0], y=_a(0.4, -0.9, 1.0)[1], phase=4),
    dict(state="damaged", kind="smoke_plume", x=_a(1.5, 1.2, H + 0.7)[0], y=_a(1.5, 1.2, H + 0.7)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(0.4, -0.9, 1.2)[0], y=_a(0.4, -0.9, 1.2)[1], phase=5),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.3, 3.1, -2.9, -2.1, 0.14, 0.8, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.0, (0.0, -2.9, z), "steel", axis="X", verts=10)
    S.bb("planks", -2.5, -0.5, 2.9, 3.2, 0.14, 0.5, "timber", 0.0)


def _left(S):
    S.bb("planks", -3.0, -1.6, -3.0, -2.7, 0.14, 0.3, "timber", 0.0)
    S.bb("crate", 2.3, 3.1, -2.4, -1.7, 0.14, 0.8, "crate", 0.03)
    L.spark(S, 0.0, -1.3, 1.2, 3, 6)


SPEC = dict(
    pad=(-3.3, 3.3, -3.3, 3.3), env=(X0, X1, Y0, Y1), H=H + RT, boxes=[(X0, X1, Y0, Y1)], rings=[],
    clips=(0.9, 1.4), scaf=[(2.3, 3.2, -2.4, -1.7), (-3.2, -2.35, -2.4, -1.7)],
    derrick=(-3.0, 1.3, 2.2, (-0.3, 0.8, 2.6)), sparks=[(-2.0, -1.3, 1.2), (2.0, 2.0, 1.4), (0.0, 2.3, 1.4)],
    hide=("lamp", "pscope", "pshead", "whip", "slits"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
