"""Neutral Ruined Depot (2x2): a small brick storehouse, roof fallen in, garrisonable map feature, never built.  Original design.

NEUTRAL: no faction paint (see neu_ruin_tenement.py, whose materials this module reuses).  The only magenta is a rag on a pole at the
corner, the claim marker that reads once a garrison takes the depot.
The read: thick weathered brick walls around an open shell, a wide loading door on the south face under a rusty steel lintel, small
barred windows, the south-west corner and the east wall broken down to rubble, bare rusty roof trusses leaning across the top, a
concrete loading dock with bollards on the east side, a stubby broken chimney, rusted drums and a pallet stack on the pad, weeds.
States: idle (4: the rag sways, a thin smoke wisp), damaged (4, STRUCTURAL only: more wall down, a truss fallen into the shell; fire /
smoke are FX anchors).  No build state (never built).
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import importlib.util
import math
import os

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structures_neu_ruin_tenement_ref", os.path.join(_here, "neu_ruin_tenement.py"))
_T = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_T)

ID = "neu_ruin_depot"
TITLE = "Neutral Ruined Depot"
FOOTPRINT = (2, 2)
CANVAS = (128, 200)
ORIGIN = (0, 56)
DOCK = (0, 2)
CAP_MATERIAL = "concrete2"
STATES = [("idle", 4, 5), ("damaged", 4, 5)]
MATERIALS = _T.MATERIALS
SOOT_SKIN = _T.SOOT_SKIN
PALETTE = None

X0, X1 = -2.8, 2.4          # wall outer faces (west, east)
Y0, Y1 = -2.3, 2.3          # south, north
T = 0.55
H = 2.6
Z0 = 0.14


def px(x, y, z):
    return round(64 + x * 18.8), round(56 + 64 - y * 18.8 - z * 14.4)


def walls(S, dmg, i):
    # south wall: left section (broken down toward the corner), loading door, right section
    S.bb("sW0", X0, X0 + 1.0, Y0, Y0 + T, Z0, 0.9, "brick2", 0.0)
    S.bb("sW1", X0 + 1.0, -0.8, Y0, Y0 + T, Z0, H - 0.5, "brick", 0.0)
    S.bb("sW1b", X0 + 1.0, -1.6, Y0, Y0 + T, H - 0.5, H, "brick3", 0.0)
    S.bb("sWpier", -0.8, -0.5, Y0 - 0.05, Y0 + T, Z0, H, "concrete", 0.0)
    S.bb("door", -0.5, 1.0, Y0 + 0.3, Y0 + T, Z0, 1.9, "burnt", 0.0, nocast=True)
    S.bb("lintel", -0.6, 1.1, Y0 - 0.1, Y0 + T, 1.9, 2.15, "steel", 0.0)
    S.bb("sWabove", -0.5, 1.0, Y0, Y0 + T, 2.15, H, "brick", 0.0)
    S.bb("sWpier2", 1.0, 1.3, Y0 - 0.05, Y0 + T, Z0, H, "concrete", 0.0)
    if dmg:
        S.bb("sW2", 1.3, X1, Y0, Y0 + T, Z0, 1.0, "brick2", 0.0)
    else:
        S.bb("sW2", 1.3, X1, Y0, Y0 + T, Z0, H, "brick", 0.0)
        S.bb("sWwin", 1.7, 2.1, Y0 - 0.02, Y0 + T, 1.2, 1.9, "burnt", 0.0, nocast=True)
        S.bb("sWbar", 1.7, 2.1, Y0 - 0.06, Y0 - 0.02, 1.2, 1.9, "ironrust", 0.0, nocast=True)
    S.bb("sWwinL", -2.0, -1.5, Y0 - 0.02, Y0 + T, 1.2, 1.9, "burnt", 0.0, nocast=True)
    S.bb("sWbarL", -2.0, -1.5, Y0 - 0.06, Y0 - 0.02, 1.2, 1.9, "ironrust", 0.0, nocast=True)
    S.bb("soot", -0.5, 1.0, Y0 - 0.1, Y0 - 0.08, 2.15, 2.55, "burnt", 0.0, nocast=True)
    S.bb("plinth", X0 - 0.05, X1 + 0.05, Y0 - 0.1, Y0, Z0, Z0 + 0.3, "concrete2", 0.0)
    # west wall (tall), north wall, east wall (broken)
    S.bb("wW", X0, X0 + T, Y0, Y1, Z0, H, "brick2", 0.0)
    S.bb("wWtop", X0, X0 + T, Y0 + 1.5, Y1 - 0.5, H, H + 0.4, "brick2", 0.0)
    S.bb("wN", X0, X1, Y1 - T, Y1, Z0, H + 0.3, "brick2", 0.0)
    S.bb("wNtop", X0 + 0.6, X0 + 2.2, Y1 - T, Y1, H + 0.3, H + 0.8, "brick", 0.0)
    S.bb("wE0", X1 - T, X1, Y0, 0.2, Z0, H * 0.9 if not dmg else 0.9, "brick", 0.0)
    S.bb("wE1", X1 - T, X1, 0.2, Y1, Z0, 1.0, "brick2", 0.0)
    # chimney stub at the NE corner, broken
    S.bb("chim", 1.0, 1.9, Y1 - 1.0, Y1 - 0.1, Z0, H + 1.3 if not dmg else H + 0.6, "brick3", 0.03)
    S.bb("chimc", 0.95, 1.95, Y1 - 1.05, Y1 - 0.05, H + 1.3 if not dmg else H + 0.6, H + 1.4 if not dmg else H + 0.68, "concrete", 0.0)
    S.bb("chimg", 1.2, 1.7, Y1 - 0.8, Y1 - 0.3, H + 1.25 if not dmg else H + 0.55, H + 1.45 if not dmg else H + 0.7, "burnt", 0.0, nocast=True)


def interior(S, dmg, i):
    S.bb("floor", X0 + T, X1 - T, Y0 + T, Y1 - T, Z0, Z0 + 0.12, "concrete2", 0.0)
    S.bb("floorc", -1.0, 0.4, -0.8, -0.74, Z0 + 0.12, Z0 + 0.14, "burnt", 0.0, nocast=True)
    # roof trusses: leaning triangles of rusty steel across the shell
    for k, x in enumerate((-1.9, -0.2, 1.4)):
        if dmg and k == 1:
            S.box("trussfall", (0.1, 3.2, 0.12), (x + 0.5, 0.0, 0.5), "ironrust", 0.0, rot=(0, 28, 0), stretch=False)
            S.box("trussfall2", (0.1, 2.2, 0.12), (x + 0.2, 0.6 * S.YS, 0.35), "ironrust", 0.0, rot=(0, -20, 40), stretch=False)
            continue
        ya, yb = Y0 + T, Y1 - T
        _T.__dict__  # keep the module referenced
        S.bb(f"tie{k}", x, x + 0.1, ya, yb, H + 0.1, H + 0.2, "ironrust", 0.0, nocast=True)
        if k != 2:
            S.box(f"rafS{k}", (0.1, 2.6, 0.1), (x + 0.05, (ya + (yb - ya) * 0.25) * S.YS, H + 0.75), "ironrust", 0.0, rot=(24, 0, 0), stretch=False, nocast=True)
            S.box(f"rafN{k}", (0.1, 2.6, 0.1), (x + 0.05, (ya + (yb - ya) * 0.75) * S.YS, H + 0.75), "ironrust", 0.0, rot=(-24, 0, 0), stretch=False, nocast=True)
    # rubble in the shell and over the broken corners
    for k, (x, y, w, h, m) in enumerate(((-2.2, -1.6, 0.8, 0.5, "brick"), (-1.5, -1.9, 0.5, 0.35, "brick2"), (1.9, 0.9, 0.7, 0.45, "brick3"),
                                         (2.6, 0.2, 0.6, 0.4, "brick2"), (0.4, 0.8, 0.5, 0.3, "concrete"), (-0.9, 1.2, 0.6, 0.35, "plaster"),
                                         (2.0, -1.8, 0.5, 0.3, "brick"))):
        S.box(f"rub{k}", (w, w * 0.8, h), (x, y * S.YS, Z0 + h / 2), m, 0.0, rot=(0, 0, 29 * k), stretch=False)
    if dmg:
        for k, (x, y, w, h, m) in enumerate(((1.9, -1.9, 0.9, 0.5, "brick2"), (0.8, -2.0, 0.6, 0.4, "concrete"), (2.7, -1.2, 0.7, 0.45, "brick"))):
            S.box(f"crub{k}", (w, w * 0.8, h), (x, y * S.YS, Z0 + h / 2), m, 0.0, rot=(0, 0, 37 * k), stretch=False)


def yard(S, dmg, i):
    S.bb("pad", -3.3, 3.3, -3.3, 3.3, 0.0, 0.14, "ruinpad", 0.03)
    S.bb("crack", -2.8, 0.5, -2.95, -2.89, 0.14, 0.16, "burnt", 0.0, nocast=True)
    # loading dock on the east side with bollards
    S.bb("dock", X1, X1 + 0.9, -2.0, 1.4, Z0, 0.75, "concrete", 0.03)
    S.bb("dockl", X1, X1 + 0.9, -2.1, -2.0, Z0, 0.75, "concrete2", 0.0)
    S.bb("dockedge", X1 + 0.82, X1 + 0.9, -2.0, 1.4, 0.75, 0.8, "yellow", 0.0, nocast=True)
    for k, y in enumerate((-1.6, -0.2, 1.1)):
        S.cyl(f"bol{k}", 0.1, 0.5, (X1 + 1.15, y, Z0 + 0.25), "ironrust", verts=8)
    # drums, pallets
    for k, (x, y) in enumerate(((-3.0, -2.9), (-2.5, -2.7), (2.6, -2.9))):
        S.cyl(f"drum{k}", 0.3, 0.7, (x, y, Z0 + 0.35), "ironrust", verts=10)
        S.cyl(f"drumb{k}", 0.32, 0.08, (x, y, Z0 + 0.45), "steel", verts=10)
    if dmg:
        S.cyl("drumf", 0.3, 0.7, (-1.5, -2.9 * S.YS, Z0 + 0.3), "ironrust", axis="X", verts=10, stretch=False)
    S.bb("pallets", 1.3, 2.3, -3.1, -2.7, Z0, Z0 + 0.35, "timber", 0.0)
    for k, (x, y) in enumerate(((-3.1, -1.5), (2.9, 2.5), (-3.2, 1.5), (0.0, -3.0))):
        S.bb(f"weed{k}", x, x + 0.18, y, y + 0.18, Z0 + 0.2, Z0 + 0.5, "weed" if k % 2 else "weed2", 0.0, nocast=True)
    # claim rag on a pole at the south-east corner
    pxx, pyy = 2.1, -2.7
    top = 3.0
    S.cyl("pole", 0.05, top, (pxx, pyy, Z0 + top / 2), "steel", verts=6)
    sw = 0.12 * math.sin(i * math.pi / 2)
    if dmg:
        S.box("rag", (0.7, 0.05, 0.45), (pxx + 0.3, (pyy - 0.05) * S.YS, Z0 + top - 0.9), "house", 0.0, rot=(0, 0, 40), stretch=False, nocast=True)
    else:
        S.box("rag", (1.0 + sw, 0.05, 0.65), (pxx + 0.55 + sw / 2, (pyy - 0.05) * S.YS, Z0 + top - 0.35), "house", 0.0,
              rot=(0, 0, 5 * math.sin(i * math.pi / 2)), stretch=False, nocast=True)
        for q in range(3):
            ph = ((i / 4) + q / 3) % 1
            r = 0.1 + 0.26 * math.sin(math.pi * ph) ** 0.7
            S.sphere(f"wisp{q}", r, (1.5 + ph * 0.6, Y1 - 0.5, H + 1.7 + ph * 1.2), "smoke2", squash=0.85)


FX = [
    dict(state="damaged", kind="fire_large", x=px(0.9, 0.0, 0.8)[0], y=px(0.9, 0.0, 0.8)[1], phase=0),           # inside the shell
    dict(state="damaged", kind="fire_small", x=px(2.0, -1.8, 0.7)[0], y=px(2.0, -1.8, 0.7)[1], phase=4),          # broken corner
    dict(state="damaged", kind="smoke_plume", x=px(0.9, 0.0, 1.1)[0], y=px(0.9, 0.0, 1.1)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=px(1.45, 1.8, H + 0.8)[0], y=px(1.45, 1.8, H + 0.8)[1], phase=5),
]


def build(S, state, i, n):
    dmg = state == "damaged"
    if dmg:
        S.skin.update(SOOT_SKIN)
    yard(S, dmg, i)
    walls(S, dmg, i)
    interior(S, dmg, i)
