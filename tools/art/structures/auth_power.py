"""Authority Power Plant: a compact atomic plant (2x2 cells). Original design, no franchise references.

Squat containment dome (house band), control annex with glowing amber tube windows, a tapered cooling stack that puffs,
and a fenced transformer yard with insulators.  Idle loop: the windows pulse along the row, the transformers spark,
the stack puffs.  Damage is structural only (D-069): cracked dome with a breach, snapped stack, a toppled transformer,
caved annex roof, dark windows; fire / smoke are FX anchors.
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

ID = "auth_power"
TITLE = "Authority Power Plant"
FOOTPRINT = (2, 2)
CANVAS = (128, 176)
ORIGIN = (0, 48)
DOCK = (0, 0)           # no dock (not a refinery)
CAP_MATERIAL = "concrete2"
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = L.MATERIALS
SOOT_SKIN = L.SOOT_SKIN
PRIMER_SKIN = L.PRIMER_SKIN

DOME = (-1.4, 1.5)
DOME_R = 1.35
STACK = (2.1, 1.8)
ANNEX = (-3.0, 0.3, -0.9, 0.5)
ANNEX_H = 1.9
YARD = (0.9, 3.0, -3.0, -0.4)


def transformer(S, name, x, y, dmg=False, spark=False):
    S.bb(f"{name}", x - 0.5, x + 0.5, y - 0.4, y + 0.4, 0.2, 1.0, "hull2", 0.04)
    S.bb(f"{name}top", x - 0.55, x + 0.55, y - 0.45, y + 0.45, 1.0, 1.12, "steel", 0.03)
    for k in range(5):
        S.bb(f"{name}fin", x - 0.42 + k * 0.21, x - 0.34 + k * 0.21, y - 0.43, y - 0.38, 0.28, 0.92, "dark", 0.0, nocast=True)
    S.bb(f"{name}plate", x - 0.3, x + 0.3, y - 0.43, y - 0.4, 0.62, 0.88, "house", 0.0, nocast=True)
    for k, ix in enumerate((-0.32, 0.0, 0.32)):
        if dmg and k == 1:
            S.cyl(f"{name}ins", 0.08, 0.22, (x + ix, y, 1.23), "white", verts=8)
            continue
        S.cyl(f"{name}ins", 0.08, 0.55, (x + ix, y, 1.38), "white", verts=8)
        for dz in (1.25, 1.42):
            S.cyl(f"{name}insd", 0.15, 0.05, (x + ix, y, dz), "white", verts=10)
        S.cyl(f"{name}inst", 0.1, 0.07, (x + ix, y, 1.68), "steel", verts=8)
        if spark and k == 1:
            S.bb("sparkcore", x + ix - 0.1, x + ix + 0.1, y - 0.1, y + 0.1, 1.76, 1.96, "spark", 0.0, nocast=True)
            S.bb("sparkarm", x + ix + 0.12, x + ix + 0.34, y - 0.04, y + 0.04, 1.86, 1.92, "spark", 0.0, nocast=True)
            S.bb("sparkarm", x + ix - 0.3, x + ix - 0.1, y - 0.04, y + 0.04, 1.78, 1.84, "spark", 0.0, nocast=True)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    dx, dy = DOME
    sx, sy = STACK
    # ---- pad, hazard stripes, yard slab
    S.bb("pad", -3.3, 3.3, -3.3, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -3.3, 3.3, -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    for k in range(12):
        S.bb(f"haz{k}", -3.2 + k * 0.5, -3.2 + (k + 1) * 0.5, -3.18, -2.78, 0.14, 0.2, "yellow" if k % 2 == 0 else "dark", 0.0)
    S.bb("yard", YARD[0], YARD[1], YARD[2] + 0.25, YARD[3], 0.14, 0.2, "concrete2", 0.0)
    # ---- control annex
    ax0, ax1, ay0, ay1 = ANNEX
    S.bb("annex", ax0, ax1, ay0, ay1, 0.14, ANNEX_H, "concrete", 0.04)
    S.bb("aplinth", ax0 - 0.08, ax1 + 0.08, ay0 - 0.08, ay1 + 0.08, 0.14, 0.4, "concrete2", 0.03)
    yw = ay0 - 0.02
    if dmg:
        S.bb("aroofE", -1.2, ax1 + 0.15, ay0 - 0.12, ay1 + 0.1, ANNEX_H, ANNEX_H + 0.2, "roof", 0.04)
        S.bb("aroofW", ax0 - 0.15, -1.2, ay0 - 0.12, ay1 + 0.1, ANNEX_H - 0.6, ANNEX_H - 0.4, "roof", 0.04, rot=(0, 16, 4))
        S.bb("apit", ax0 + 0.3, -1.3, ay0 + 0.35, ay1 - 0.05, ANNEX_H - 0.3, ANNEX_H, "dark", 0.0)
        S.bb("awallj", ax0, ax0 + 0.5, ay0, ay0 + 0.25, ANNEX_H, ANNEX_H + 0.35, "concrete", 0.02)
        S.bb("awallj", -1.7, -1.35, ay0, ay0 + 0.25, ANNEX_H, ANNEX_H + 0.2, "concrete", 0.02)
        S.bb("arubble", -3.2, -2.0, -1.5, -0.8, 0.14, 0.45, "concrete2", 0.05, rot=(0, 0, 18))
        S.bb("arubble", -2.2, -1.5, -1.3, -0.8, 0.14, 0.7, "concrete", 0.05, rot=(0, 0, -22))
    else:
        S.bb("aroof", ax0 - 0.15, ax1 + 0.15, ay0 - 0.12, ay1 + 0.1, ANNEX_H, ANNEX_H + 0.2, "roof", 0.04)
        S.bb("aparapet", ax0 - 0.15, ax1 + 0.15, ay0 - 0.12, ay0 - 0.02, ANNEX_H + 0.2, ANNEX_H + 0.38, "concrete2", 0.02)
    hx0 = -1.15 if dmg else -2.85
    S.bb("aroofhouse", hx0, 0.1, ay0 + 0.2, ay1 - 0.15, ANNEX_H + 0.2, ANNEX_H + 0.24, "house2", 0.0)
    # tube windows: a row of tall amber slots, pulsing along the row
    S.bb("winframe", ax0 + 0.2, ax1 - 0.2, yw - 0.06, yw + 0.04, 0.95, 1.7, "dark", 0.0)
    for k in range(5):
        x = ax0 + 0.42 + k * 0.58
        if dmg:
            m = "amber_dim" if k in (1, 3) else "glass"
        elif idle:
            m = "amber" if (k + i) % 4 in (0, 1) else "amber_dim"
        else:
            m = "amber"
        S.bb(f"awin{k}", x, x + 0.4, yw - 0.1, yw - 0.04, 1.02, 1.62, m, 0.0)
    S.bb("apanel", ax0 + 0.2, ax1 - 0.2, yw - 0.12, yw - 0.06, 0.45, 0.85, "house", 0.0)
    S.bb("adoor", ax1 - 0.05, ax1 + 0.0, ay0 + 0.1, ay0 + 0.7, 0.14, 0.9, "dark", 0.0)
    for k in range(4):
        S.bb(f"sten{k}", ax0 + 0.4 + k * 0.4, ax0 + 0.58 + k * 0.4, yw - 0.1, yw - 0.06, 0.2, 0.36, "white", 0.0, nocast=True)
    # ---- containment dome: plinth, drum with house band, dome, cap
    S.cyl("dplinth", DOME_R + 0.2, 0.3, (dx, dy, 0.29), "concrete2", verts=28)
    S.cyl("drum", DOME_R, 1.0, (dx, dy, 0.94), "hull", verts=28)
    S.cyl("hband", DOME_R + 0.03, 0.42, (dx, dy, 0.8), "house", verts=28)
    S.cyl("hring", DOME_R + 0.06, 0.08, (dx, dy, 1.0 + 0.4), "steel", verts=28)
    S.sphere("dome", DOME_R + 0.02, (dx, dy, 1.44), "hull", nocast=False, squash=0.78)
    S.cyl("dcap", 0.3, 0.25, (dx, dy, 2.42), "steel", verts=12)
    lit = idle and i in (0, 1)
    S.cyl("dlamp", 0.1, 0.14, (dx, dy, 2.62), "lamp" if (lit and not dmg) else "dark", verts=8)
    if dmg:
        # impact breach on the south-east shoulder: dark hole ringed by torn shell, a chunk lying below
        S.bb("breach", dx + 0.2, dx + 1.0, dy - 1.0, dy - 0.7, 1.15, 1.75, "dark", 0.0, nocast=True)
        S.bb("brim1", dx + 0.1, dx + 1.1, dy - 1.06, dy - 0.74, 1.7, 1.9, "concrete2", 0.0, rot=(0, 0, 8))
        S.bb("brim2", dx + 1.0, dx + 1.3, dy - 1.0, dy - 0.6, 1.1, 1.7, "concrete2", 0.0)
        S.bb("brim3", dx + 0.1, dx + 0.4, dy - 1.0, dy - 0.7, 1.1, 1.45, "concrete2", 0.0)
        S.bb("chunk", dx + 0.9, dx + 1.7, dy - 2.0, dy - 1.4, 0.14, 0.55, "hull", 0.05, rot=(0, 0, 25))
        S.bb("chunk2", dx + 0.2, dx + 0.7, dy - 1.9, dy - 1.6, 0.14, 0.4, "hull2", 0.05, rot=(0, 0, -15))
        for k, (cx, cz, h) in enumerate(((-0.8, 1.2, 0.9), (-0.3, 1.5, 0.6), (0.4, 1.9, 0.5))):
            S.bb(f"crack{k}", dx + cx, dx + cx + 0.06, dy - 1.25, dy - 1.19, cz, cz + h, "crack", 0.0, nocast=True, rot=(0, 0, 0))
        S.bb("crackh", dx - 1.0, dx - 0.2, dy - 1.2, dy - 1.14, 0.7, 0.76, "crack", 0.0, nocast=True)
    # ---- cooling stack
    if dmg:
        S.cone("stack", 0.85, 0.7, 2.1, (sx, sy, 0.14 + 1.05), "concrete", verts=20, nocast=False)
        for nm, dxx, dzz, rot in (("jagA", -0.45, 2.3, (0, 25, 0)), ("jagB", 0.4, 2.35, (0, -20, 12)), ("jagC", 0.0, 2.2, (15, 0, 0))):
            S.box(nm, (0.4, 0.4, 0.55), (sx + dxx, sy, dzz), "concrete2", 0.0, rot=rot, stretch=False)
        S.cyl("sdark", 0.5, 0.06, (sx, sy, 2.26), "dark", verts=12)
        # the top section snapped and lies across the yard edge to the south-east
        tl = math.radians(72)
        piece = S.cone("fallen", 0.7, 0.55, 1.6, (0, 0, 0), "concrete", verts=20, nocast=False)
        piece.location = (sx + 1.0, (sy - 1.6) * YS, 0.62)
        piece.rotation_euler = (math.radians(-62), 0, math.radians(22))
        S.bb("srubble", sx - 0.6, sx + 0.6, sy - 1.2, sy - 0.7, 0.14, 0.4, "concrete2", 0.05, rot=(0, 0, 14))
        S.bb("scrack", sx - 0.5, sx - 0.44, sy - 0.8, sy - 0.76, 0.5, 1.9, "crack", 0.0, nocast=True)
    else:
        S.cone("stack", 0.85, 0.6, 3.4, (sx, sy, 0.14 + 1.7), "concrete", verts=20, nocast=False)
        for zz, rr in ((0.7, 0.8), (1.5, 0.74), (2.3, 0.68)):
            S.cyl(f"sband{zz}", rr + 0.05, 0.12, (sx, sy, zz), "rust", verts=20)
        S.cyl("slip", 0.66, 0.14, (sx, sy, 3.5), "concrete2", verts=20)
        S.cyl("sdark", 0.5, 0.06, (sx, sy, 3.57), "dark", verts=16)
        if idle:
            for k in range(2):
                ph = ((i / 4) + k / 2) % 1
                r = 0.1 + 0.34 * math.sin(math.pi * ph) ** 0.7
                S.sphere(f"puff{k}", r, (sx + ph * 0.45, sy, 3.75 + ph * 1.2 + r * 0.3), "smoke", squash=0.85)
    S.bb("sladder", sx - 0.86, sx - 0.8, sy - 0.12, sy + 0.12, 0.14, 1.8 if not dmg else 1.2, "dark", 0.0, nocast=True)
    S.bb("sbase", sx - 1.0, sx + 1.0, sy - 1.0, sy + 1.0, 0.14, 0.3, "concrete2", 0.03)
    # ---- pipes dome -> stack, dome -> annex
    S.cyl("pipeH", 0.15, 2.0, (0.55, dy, 1.0), "steel", axis="X", verts=12)
    for k, xx in enumerate((-0.05, 1.1)):
        S.cyl(f"fl{k}", 0.22, 0.1, (xx, dy, 1.0), "rust", axis="X", verts=12)
    S.cyl("pipeV", 0.15, 0.9, (-0.05 + 0.0, dy, 0.6), "steel", verts=10)
    # ---- transformer yard: fence, two transformers, busbar
    fx0, fx1, fy0, fy1 = 0.7, 3.2, -3.05, -0.3
    for xx in (fx0, 1.5, 2.3, fx1):
        if dmg and xx in (1.5, 2.3):
            continue
        S.bb("fpost", xx - 0.04, xx + 0.04, fy0 - 0.04, fy0 + 0.04, 0.2, 1.0, "steel", 0.0)
    for yy in (-1.95, fy1):
        S.bb("fpost", fx1 - 0.04, fx1 + 0.04, yy - 0.04, yy + 0.04, 0.2, 1.0, "steel", 0.0)
    if dmg:
        S.bb("frail", fx0, 1.2, fy0 - 0.02, fy0 + 0.02, 0.85, 0.89, "steel", 0.0, nocast=True)
        S.bb("frailfall", 1.3, 2.2, fy0 - 0.3, fy0 - 0.26, 0.2, 0.24, "steel", 0.0, nocast=True, rot=(0, 0, 8))
    else:
        for z0 in (0.55, 0.9):
            S.bb("frail", fx0, fx1, fy0 - 0.02, fy0 + 0.02, z0, z0 + 0.04, "steel", 0.0, nocast=True)
        S.bb("frailE", fx1 - 0.02, fx1 + 0.02, fy0, fy1, 0.9, 0.94, "steel", 0.0, nocast=True)
    transformer(S, "trA", 1.55, -1.3, dmg=dmg, spark=idle and i in (1, 3) and not dmg)
    if dmg:
        # second transformer knocked over onto its side, insulators snapped off
        S.box("trB", (1.0, 0.8, 0.8), (2.55, -2.0 * YS, 0.62), "hull2", 0.04, rot=(0, 0, 22), stretch=False)
        S.box("trBtop", (1.1, 0.12, 0.12), (2.6, -2.45 * YS, 0.25), "steel", 0.0, rot=(0, 0, 22), stretch=False)
        for k in range(3):
            S.cyl(f"insF{k}", 0.08, 0.5, (2.9 + 0.1 * k, -2.5 - 0.3 * k, 0.28), "white", axis="X", verts=8)
    else:
        transformer(S, "trB", 2.55, -1.3, spark=idle and i in (0, 2))
    if not dmg:
        L.beam(S, "bus", (1.55, -1.3, 1.62), (2.55, -1.3, 1.62), 0.05, "dark", nocast=True)
    # ---- side details: lamp mast, stencils
    S.cyl("lmast", 0.05, 2.2, (-3.0, -1.7, 1.2), "steel", verts=8)
    S.bb("lhead", -3.12, -2.88, -1.76, -1.64, 2.25, 2.4, "dark", 0.0)
    S.bb("lamp", -3.1, -2.9, -1.78, -1.76, 2.27, 2.38, "lamp" if (not dmg and idle) else "dark", 0.0, nocast=True)
    if dmg:
        R.scorch(S, "scA", -1.5, yw - 0.16, 1.1, 1.5, 0.6)
        R.scorch(S, "scB", 1.3, -1.1, 0.23, 1.6, 1.2, 0.03, plane="T")
        R.scorch(S, "scC", dx + 0.5, dy - 1.3, 1.6, 1.0, 0.7)


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_large", x=_a(-0.9, 0.0, 1.5)[0], y=_a(-0.9, 0.0, 1.5)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=_a(-0.3, 0.5, 2.0)[0], y=_a(-0.3, 0.5, 2.0)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=_a(2.6, -2.0, 0.6)[0], y=_a(2.6, -2.0, 0.6)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=_a(2.1, 1.8, 2.3)[0], y=_a(2.1, 1.8, 2.3)[1], phase=6),
    dict(state="damaged", kind="smoke_plume", x=_a(-0.3, 0.5, 1.9)[0], y=_a(-0.3, 0.5, 1.9)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(2.1, 1.8, 2.3)[0], y=_a(2.1, 1.8, 2.3)[1], phase=5),
    dict(state="damaged", kind="smoke_plume", x=_a(2.6, -2.0, 0.8)[0], y=_a(2.6, -2.0, 0.8)[1], phase=3),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.2, 3.0, -2.9, -2.1, 0.14, 0.8, "crate", 0.03)
        S.bb("crate", 2.35, 2.9, -2.8, -2.2, 0.8, 1.3, "crate", 0.03, rot=(0, 0, 12))
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (1.5, -2.0, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", -2.8, -0.8, -2.4, -2.2, 0.14, 0.3, "rebar", 0.0)
        S.cyl("barrel", 0.3, 0.7, (-3.0, -1.6, 0.5), "rust", verts=12)
    S.bb("planks", 0.8, 2.6, 2.7, 3.1, 0.14, 0.5, "timber", 0.0)
    S.bb("cabin", -3.2, -2.0, 2.5, 3.2, 0.14, 1.1, "primer", 0.03)
    S.bb("cabdoor", -2.9, -2.6, 2.45, 2.5, 0.14, 0.8, "dark", 0.0)


def _left(S):
    S.bb("planks", -2.8, -1.4, -2.9, -2.5, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 2.0, (1.4, -2.4, 0.3), "steel", axis="X", verts=10)
    S.bb("crate", 2.3, 3.0, -2.9, -2.2, 0.14, 0.8, "crate", 0.03)
    L.spark(S, -1.0, -0.9, 2.0, 3, 6)


SPEC = dict(
    pad=(-3.3, 3.3, -3.3, 3.3), env=(-3.1, 3.1, -1.0, 3.0), H=2.0, boxes=[(-3.0, 0.3, -0.9, 0.5)], rings=[(DOME[0], DOME[1], DOME_R), (STACK[0], STACK[1], 0.85)],
    clips=(1.0, 1.9), scaf=[(-2.9, -1.6, -2.1, -1.4), (-1.0, 0.2, -2.1, -1.4)],
    derrick=(3.0, 0.4, 4.0, (0.3, 1.3, 4.4)), sparks=[(-2.0, -0.9, 1.9), (0.3, 0.5, 1.9), (-1.4, 2.8, 2.0)],
    hide=("sten", "sparkcore", "sparkarm", "dlamp", "lamp"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
