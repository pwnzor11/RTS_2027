"""Authority Gate: a reinforced sliding perimeter gate (1x1 cell). Original design, no franchise references.

Two concrete gate piers (same block, plinth, steel caps and house band as auth_wall, so it joins a wall run east and west)
with a steel-framed sliding leaf between them: heavy box frame, diagonal bracing, a house-colour panel, a floor track with
a hazard-striped threshold, a keyed card reader and an amber guard lamp on the east pier that blinks in the idle loop.
States idle / damaged / build show the CLOSED gate; since M3-21 the game also plays a manifest state named `open`
(SpriteStates.Open) for an open gate, so a 4-frame `open` state (appended last, first = 16) shows the leaf slid fully east
behind the east pier on a rear track, the gap and floor track clear for a unit to pass; posts, lamp blink and reader as idle. Damage is structural only (D-069): the leaf is buckled and has sprung off its track at one end, the
west pier is chipped and cracked, the lamp is dark; fire / smoke are FX anchors.
Contract: see auth_refinery.py; materials and helpers from auth_wall.py / auth_conyard.py (read-only).
Units: 3.4 per cell, x east, y NORTH, z up: x, y in [-1.7, 1.7].
"""
import importlib.util
import math
import os

_HERE = os.path.dirname(os.path.abspath(__file__))


def _load(n, f):
    sp = importlib.util.spec_from_file_location(n, os.path.join(_HERE, f))
    m = importlib.util.module_from_spec(sp)
    sp.loader.exec_module(m)
    return m


L = _load("structlib_auth_conyard", "auth_conyard.py")
W = _load("structlib_auth_wall", "auth_wall.py")
R = L.R

ID = "auth_gate"
TITLE = "Authority Gate"
FOOTPRINT = (1, 1)
CANVAS = (64, 100)
ORIGIN = (0, 32)
DOCK = (0, 1)
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5), ("open", 4, 5)]
MATERIALS = dict(W.MATERIALS)
MATERIALS.update({k: v for k, v in L.MATERIALS.items() if k in ("amber", "amber_dim", "crack", "gauge")})
SOOT_SKIN = dict(W.SOOT_SKIN)
PRIMER_SKIN = dict(W.PRIMER_SKIN)
PRIMER_SKIN.update({"steel2": "primer", "amber": "dark", "amber_dim": "dark"})

HX = 1.7
WT = 0.5
WZ = 2.0
PIER = (0.55, 1.7)       # pier extents in |x|


def pier(S, sx, dmg):
    x0, x1 = sorted((sx * PIER[0], sx * PIER[1]))
    ys = -WT - 0.02
    S.bb("plinth", x0, x1, -WT - 0.2, WT + 0.15, 0.0, 0.42, "concrete2", 0.0)
    S.bb("block", x0, x1, -WT, WT, 0.0, WZ, "concrete", 0.0)
    S.bb("cap", x0, x1, -WT - 0.14, WT + 0.14, WZ, WZ + 0.16, "steel", 0.0)
    S.bb("rail", x0, x1, -0.14, 0.14, WZ + 0.16, WZ + 0.3, "steel2", 0.0)
    S.bb("trimA", x0, x1, ys - 0.03, ys, 0.88, 0.97, "dark", 0.0, nocast=True)
    S.bb("trimB", x0, x1, ys - 0.03, ys, 1.55, 1.64, "dark", 0.0, nocast=True)
    S.bb("band", x0, x1, ys - 0.05, ys, 0.97, 1.55, "house", 0.0, nocast=True)
    # S22 detail pass: same cladding as the wall (panel tones, seams, bolts, grime, rust), laid out on the same grid
    brk = (x0 + 0.05, x0 + 0.45) if (dmg and sx < 0) else None
    W.detail(S, x0, x1, ys, brk, False)
    # inner pier face: steel jamb strip and a stop block
    ix = sx * PIER[0]
    S.bb("jamb", min(ix, ix - sx * 0.1), max(ix, ix - sx * 0.1), -WT - 0.06, WT + 0.06, 0.42, WZ, "steel", 0.0)
    if dmg and sx < 0:
        S.bb("bite", x0 + 0.1, x0 + 0.4, ys - 0.02, ys + 0.03, 1.3, 1.95, "dark", 0.0, nocast=True)
        for k, (a, b, c, d) in enumerate(((x0 + 0.2, 0.5, x0 + 0.45, 1.3), (x0 + 0.45, 1.3, x0 + 0.3, 1.9))):
            L.beam(S, f"crack{k}", (a, ys - 0.04, b), (c, ys - 0.04, d), 0.05, "dark", nocast=True)
        L.beam(S, "rbar", (x0 + 0.2, 0.0, WZ + 0.3), (x0 + 0.05, -0.1, WZ + 0.85), 0.05, "rebar")
        S.box("rub", (0.4, 0.32, 0.2), (x0 + 0.3, -0.95, 0.1), "concrete_s", 0.0, rot=(0, 0, 28), stretch=False)


def finished(S, i, dmg, mode, opened=False):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    for sx in (-1, 1):
        pier(S, sx, dmg)
    # ---- floor track and hazard threshold across the gap
    S.bb("track", -PIER[0], PIER[0], -0.12, 0.12, 0.0, 0.1, "steel", 0.0)
    for k in range(4):
        S.bb(f"haz{k}", -PIER[0] + k * 0.275, -PIER[0] + (k + 1) * 0.275, -WT - 0.22, -WT - 0.02, 0.0, 0.06, "yellow" if k % 2 == 0 else "dark", 0.0, nocast=True)
    # ---- sliding leaf: box frame, infill, braces, house panel
    a, b = -PIER[0] + 0.02, PIER[0] - 0.02
    lz0, lz1 = 0.12, 1.85
    yl = -0.1
    if dmg:
        # buckled leaf: east end sprung out of its track, tilted forward
        S.box("leaf", (1.0, 0.16, 1.5), (0.0, (yl - 0.05) * YS, 0.95), "steel2", 0.03, rot=(-10, 0, -4), stretch=False)
        S.box("leafd", (0.9, 0.05, 1.3), (0.0, (yl - 0.16) * YS, 0.95), "hull2", 0.0, rot=(-10, 0, -4), stretch=False)
        S.box("leaftop", (1.0, 0.2, 0.12), (0.0, (yl - 0.3) * YS, 1.7), "steel", 0.0, rot=(-10, 0, -4), stretch=False)
        for k, (u, h) in enumerate(((-0.3, 0.8), (0.2, 1.1))):
            S.bb(f"lcrack{k}", u, u + 0.05, yl - 0.4, yl - 0.36, 0.3, 0.3 + h, "dark", 0.0, nocast=True)
        S.bb("lamp", PIER[0] + 0.28, PIER[0] + 0.52, -WT - 0.1, -WT - 0.02, WZ + 0.3, WZ + 0.55, "dark", 0.0)
    else:
        if opened:
            # leaf slid east behind the east pier on a rear track (its top still peeks over the pier cap)
            sh, yl = 1.06, 0.8
            S.bb("rtrack", PIER[0], HX - 0.05, yl - 0.1, yl + 0.1, 0.0, 0.1, "steel", 0.0)
            a, b = a + sh, b + sh
        S.bb("leafg", a, b, yl - 0.1, yl + 0.1, lz0, lz1, "hull2", 0.02)
        for zz in (lz0, lz1 - 0.14):
            S.bb("lfh", a, b, yl - 0.14, yl + 0.14, zz, zz + 0.14, "steel2", 0.02)
        for xx in (a, b - 0.14):
            S.bb("lfv", xx, xx + 0.14, yl - 0.14, yl + 0.14, lz0, lz1, "steel2", 0.02)
        L.beam(S, "lbr", (a + 0.1, yl - 0.13, lz0 + 0.14), (b - 0.1, yl - 0.13, lz1 - 0.14), 0.07, "steel")
        L.beam(S, "lbr", (b - 0.1, yl - 0.13, lz0 + 0.14), (a + 0.1, yl - 0.13, lz1 - 0.14), 0.07, "steel")
        for zz in (lz0 + 0.03, lz1 - 0.11):                      # bolt heads on the frame, grime and rust on the leaf
            for k in range(5):
                bx = a + 0.1 + k * (b - a - 0.2) / 4
                S.bb("lbolt", bx - 0.04, bx + 0.04, yl - 0.16, yl - 0.14, zz, zz + 0.08, "bolt", 0.0, nocast=True)
        S.bb("lgrime", a, b, yl - 0.16, yl - 0.14, lz0, lz0 + 0.3, "grime", 0.0, nocast=True)
        for bx in (a + 0.3, b - 0.35):
            S.bb("lrust", bx, bx + 0.1, yl - 0.16, yl - 0.14, 1.1, lz1 - 0.14, "rust2", 0.0, nocast=True)
        S.bb("lpanel", (a + b) / 2 - 0.3, (a + b) / 2 + 0.3, yl - 0.16, yl - 0.13, 0.7, 1.2, "house", 0.0, nocast=True)
        for k in range(4):
            xx = a + 0.2 + k * 0.18
            S.bb(f"lbar{k}", xx, xx + 0.04, yl - 0.125, yl - 0.11, lz0 + 0.16, lz1 - 0.16, "dark", 0.0, nocast=True)
        S.bb("lamp", PIER[0] + 0.28, PIER[0] + 0.52, -WT - 0.1, -WT - 0.02, WZ + 0.3, WZ + 0.55, "amber" if (idle and i % 2 == 0) else "amber_dim", 0.02)
    S.bb("lampb", PIER[0] + 0.3, PIER[0] + 0.5, -0.1, 0.1, WZ + 0.3, WZ + 0.34, "steel", 0.0)
    # card reader post on the east approach
    S.bb("rdr", 0.7, 0.9, -WT - 0.35, -WT - 0.17, 0.42, 1.2, "hull2", 0.02)
    S.bb("rdrface", 0.74, 0.86, -WT - 0.37, -WT - 0.35, 0.9, 1.1, "amber" if (idle and not dmg and i % 2 == 1) else "dark", 0.0, nocast=True)
    # dragon teeth as on the wall (south of the east pier)
    if not dmg:
        S.cone("tooth", 0.46, 0.06, 0.78, (1.25, -1.25, 0.39), "concrete2", verts=4, rot=(0, 0, 45), nocast=False)
        S.cone("tooth", 0.46, 0.06, 0.78, (-1.25, -1.25, 0.39), "concrete2", verts=4, rot=(0, 0, 45), nocast=False)


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_small", x=_a(0.0, 0.0, 1.3)[0], y=_a(0.0, 0.0, 1.3)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(0.0, 0.0, 1.5)[0], y=_a(0.0, 0.0, 1.5)[1], phase=3),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 0.9, 1.5, -1.5, -0.95, 0.14, 0.6, "crate", 0.03)
    S.bb("planks", -1.5, -0.5, 0.9, 1.3, 0.14, 0.4, "timber", 0.0)


def _left(S):
    S.bb("crate", 0.9, 1.5, -1.5, -0.95, 0.14, 0.6, "crate", 0.03)


SPEC = dict(
    pad=(-1.7, 1.7, -1.7, 1.7), env=(-1.7, 1.7, -0.6, 0.6), H=2.3, boxes=[(-1.6, -0.6, -0.4, 0.4), (0.6, 1.6, -0.4, 0.4)], rings=[],
    clips=(1.0, 1.9), scaf=[(-1.5, -0.6, -1.5, -1.0)],
    derrick=(1.3, 1.1, 3.2, (0.2, 0.4, 3.6)), sparks=[(-1.0, -0.4, 1.5), (1.0, -0.4, 1.5)],
    hide=("lamp", "rdr", "lpanel", "lbar"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    if state == "open":
        finished(S, i, False, "idle", opened=True)
        return
    L.stages(S, state, i, SPEC, finished)
