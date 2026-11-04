"""Authority Searchlight Tower: a steel lattice mast with a carbon-arc lamp (1x1 cell). Original design, no franchise references.

Concrete footing with a khaki control cabinet (house panel) and a generator; a four-legged tapering lattice tower up to a
railed lamp deck; on top a big flared reflector with a bright lens on a yoke. Idle loop: the lamp head sweeps left, centre,
right, centre and the lens flickers. Damage is structural only (D-069): the tower leans and one brace is gone, the lamp is
knocked askew with a dark lens, the cabinet door is blown open; fire / smoke are FX anchors.
Contract: see auth_refinery.py; build-site helpers come from auth_conyard.py.
Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-1.7, 1.7].
"""
import importlib.util
import math
import os

_HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structlib_auth_conyard", os.path.join(_HERE, "auth_conyard.py"))
L = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(L)
R = L.R

ID = "auth_searchlight"
TITLE = "Authority Searchlight Tower"
FOOTPRINT = (1, 1)
CANVAS = (96, 156)
ORIGIN = (16, 88)
DOCK = (0, 1)
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = dict(L.MATERIALS)
MATERIALS.update({"steel2": ("#979EA3", dict(rough=0.4, metal=0.5)), "lensbright": ("#FFF6C8", dict(rough=0.3, emit="#FFF2B0"))})
SOOT_SKIN = L.SOOT_SKIN
PRIMER_SKIN = dict(L.PRIMER_SKIN)
PRIMER_SKIN.update({"lensbright": "dark", "steel2": "primer"})

TOP = 3.9          # lamp deck height
YAW = [-32.0, 0.0, 32.0, 0.0]


def lamp(S, name, x, y, z, yaw, up, r, depth, lens):
    """Flared reflector, wide mouth toward -Y (south) turned by yaw (deg), tilted up by `up`."""
    ya, ua = math.radians(180 + yaw), math.radians(up)
    u = (-math.sin(ya) * math.cos(ua), math.cos(ya) * math.cos(ua), math.sin(ua))
    rot = (90 + up, 0, 180 + yaw)
    S.cone(name, r, r * 0.45, depth, (x, y, z), "steel2", 18, rot=rot)
    m = depth / 2 + 0.03
    S.cone(name + "l", r * 0.88, r * 0.88, 0.05, (x + u[0] * m, y + u[1] * m, z + u[2] * m), lens, 16, rot=rot, nocast=False)
    t = depth / 2 + 0.14
    S.cone(name + "b", r * 0.45, r * 0.45, 0.28, (x - u[0] * t, y - u[1] * t, z - u[2] * t), "hull2", 12, rot=rot, nocast=False)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    S.bb("pad", -1.7, 1.7, -1.7, 1.7, 0.0, 0.14, "pad", 0.02)
    S.bb("foot", -1.1, 1.1, -0.9, 0.9, 0.14, 0.42, "concrete2", 0.03)
    for k in range(6):
        S.bb(f"haz{k}", -1.65 + k * 0.55, -1.1 + k * 0.55, -1.66, -1.5, 0.14, 0.2, "yellow" if k % 2 == 0 else "dark", 0.0)
    lean = 0.35 if dmg else 0.0
    # ---- tower: four tapering legs, three bracing levels, a lamp deck
    legs = [(sx, sy) for sx in (-1, 1) for sy in (-1, 1)]
    zs = (0.42, 1.55, 2.7, TOP)
    def hw(z):
        return 0.7 - 0.38 * (z - 0.42) / (TOP - 0.42)
    def off(z):
        return lean * ((z - 0.42) / (TOP - 0.42)) ** 1.5
    for k, (sx, sy) in enumerate(legs):
        for a, b in zip(zs, zs[1:]):
            L.beam(S, f"leg{k}", (sx * hw(a) + off(a), sy * hw(a) * 0.9, a), (sx * hw(b) + off(b), sy * hw(b) * 0.9, b), 0.12, "girder")
    for z in zs[1:]:
        for sy in (-1, 1):
            L.beam(S, "ring", (-hw(z) + off(z), sy * hw(z) * 0.9, z), (hw(z) + off(z), sy * hw(z) * 0.9, z), 0.08, "girder")
        for sx in (-1, 1):
            L.beam(S, "ringS", (sx * hw(z) + off(z), -hw(z) * 0.9, z), (sx * hw(z) + off(z), hw(z) * 0.9, z), 0.08, "girder")
    for n, (a, b) in enumerate(zip(zs, zs[1:])):
        if dmg and n == 1:
            continue          # a front brace is gone
        L.beam(S, "xb", (-hw(a) + off(a), -hw(a) * 0.9, a), (hw(b) + off(b), -hw(b) * 0.9, b), 0.06, "girder")
        L.beam(S, "xb", (hw(a) + off(a), -hw(a) * 0.9, a), (-hw(b) + off(b), -hw(b) * 0.9, b), 0.06, "girder")
    ox = off(TOP)
    # ---- deck with rails and a ladder
    S.bb("deck", -0.62 + ox, 0.62 + ox, -0.6, 0.6, TOP, TOP + 0.1, "steel", 0.02)
    for sy in (-0.58, 0.58):
        S.bb("rail", -0.62 + ox, 0.62 + ox, sy - 0.02, sy + 0.02, TOP + 0.5, TOP + 0.54, "yellow", 0.0, nocast=True)
    for px in (-0.6, 0.6):
        L.post(S, "rp", px + ox, -0.58, TOP + 0.1, TOP + 0.54, 0.05, "yellow")
    for z in [0.6 + 0.3 * k for k in range(11)]:
        S.bb("rung", -0.52, -0.3, -0.98, -0.94, z, z + 0.04, "dark", 0.0, nocast=True)
    # ---- yoke and lamp head
    S.bb("yoke", -0.12 + ox, 0.12 + ox, -0.12, 0.12, TOP + 0.1, TOP + 0.55, "hull2", 0.02)
    S.cyl("turn", 0.3, 0.12, (ox, 0.0, TOP + 0.14), "hull", verts=14)
    if dmg:
        lamp(S, "lamp", ox, 0.0, TOP + 0.95, 40, 38, 0.52, 0.8, "dark")
    else:
        lamp(S, "lamp", ox, 0.0, TOP + 0.95, YAW[i % 4] if idle else 0.0, 22, 0.52, 0.8,
             "lensbright" if (not idle or i % 4 != 3) else "lamp")
    for sx in (-1, 1):
        S.bb("arm", ox + sx * 0.5 - 0.04, ox + sx * 0.5 + 0.04, -0.06, 0.06, TOP + 0.55, TOP + 1.2, "steel", 0.0)
    S.bb("housemark", ox - 0.2, ox + 0.2, -0.15, 0.15, TOP + 0.55, TOP + 0.6, "house", 0.0)
    # ---- control cabinet and generator at the foot
    S.bb("cab", 0.55, 1.45, -0.5, 0.45, 0.42, 1.25, "concrete", 0.04)
    S.bb("cabr", 0.5, 1.5, -0.55, 0.5, 1.25, 1.35, "roof", 0.03)
    S.bb("cabp", 0.7, 1.3, -0.54, -0.5, 0.58, 1.1, "house", 0.0)
    if dmg:
        S.box("cabdoor", (0.5, 0.05, 0.6), (0.7, -0.85 * YS, 0.7), "steel", 0.0, rot=(0, 0, 50), stretch=False)
        S.bb("cabdark", 0.85, 1.25, -0.55, -0.5, 0.55, 1.1, "dark", 0.0)
    S.bb("gen", -1.45, -0.7, -0.35, 0.35, 0.42, 0.95, "hull2", 0.04)
    S.bb("genv", -1.42, -0.73, -0.38, -0.34, 0.55, 0.8, "dark", 0.0, nocast=True)
    S.cyl("gexh", 0.06, 0.4, (-1.2, 0.2, 1.15), "dark", verts=8)
    L.beam(S, "cabl", (0.5, -0.3, 0.45), (0.1, -0.5, 0.45), 0.05, "dark", nocast=True)
    if dmg:
        R.scorch(S, "scA", 1.0, -0.56, 0.9, 0.8, 0.5)
        R.scorch(S, "scB", -1.1, -0.38, 0.7, 0.6, 0.4)
        S.bb("rub", -0.4, 0.2, -1.5, -1.1, 0.14, 0.4, "concrete_s", 0.1, rot=(0, 0, 20))


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_small", x=_a(0.35, 0.0, TOP + 1.1)[0], y=_a(0.35, 0.0, TOP + 1.1)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(0.35, 0.0, TOP + 1.2)[0], y=_a(0.35, 0.0, TOP + 1.2)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=_a(1.0, -0.3, 1.0)[0], y=_a(1.0, -0.3, 1.0)[1], phase=5),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 0.9, 1.5, -1.5, -0.9, 0.14, 0.7, "crate", 0.03)
    if i <= 2:
        S.cyl("lpipe", 0.1, 1.4, (-0.4, -1.4, 0.3), "steel", axis="X", verts=10)
    S.bb("planks", -1.5, -0.5, 0.9, 1.3, 0.14, 0.4, "timber", 0.0)


def _left(S):
    S.bb("planks", -1.5, -0.6, -1.5, -1.2, 0.14, 0.3, "timber", 0.0)
    S.bb("crate", 0.9, 1.5, -1.5, -0.95, 0.14, 0.7, "crate", 0.03)


SPEC = dict(
    pad=(-1.7, 1.7, -1.7, 1.7), env=(-1.1, 1.1, -0.9, 0.9), H=TOP + 1.0, boxes=[(-0.7, 0.7, -0.6, 0.6)], rings=[],
    clips=(1.5, 2.8), scaf=[(-1.5, -0.8, -1.5, -1.1)],
    derrick=(1.4, 1.2, 4.6, (0.3, 0.4, 5.0)), sparks=[(-0.6, -0.5, 1.5), (0.5, 0.5, 2.8)],
    hide=("lamp", "housemark", "arm"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
