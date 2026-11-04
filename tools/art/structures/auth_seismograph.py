"""Authority Seismograph Station: a drum-recorder hut over a bedrock pillar (2x2 cells). Original design, no franchise references.

Squat khaki concrete hut with a house stripe, lit amber windows and a roof cradle carrying a paper drum (stripes roll round,
a pen carriage slides along it); in front a bedrock pillar ringed by a fan of steel geophone spikes with cabling back to the
door; a radio mast with a blinking lamp and a small house flag. Idle loop: drum turns, pen slides, lamp blinks.
Damage is structural only (D-069): half the roof fallen in, the drum thrown off its cradle, spikes bent, mast leaning;
fire / smoke are FX anchors.  Contract: see auth_refinery.py; build-site helpers come from auth_conyard.py.
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

ID = "auth_seismograph"
TITLE = "Authority Seismograph Station"
FOOTPRINT = (2, 2)
CANVAS = (128, 156)
ORIGIN = (0, 28)
DOCK = (0, 0)
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = dict(L.MATERIALS)
MATERIALS.update({
    "paper": ("#C2BAA0", dict(rough=0.8)),      # warm cream chart paper (not white, so the shading reads)
    "steeld": ("#3A3F46", dict(rough=0.4, metal=0.5)),   # dark steel end caps / axle stubs
})
SOOT_SKIN = dict(L.SOOT_SKIN)
SOOT_SKIN.update({"paper": "concrete2_s", "steeld": "dark"})
PRIMER_SKIN = dict(L.PRIMER_SKIN)
PRIMER_SKIN.update({"paper": "primer"})

HUT = (-2.9, 0.7, -0.5, 2.8)
H = 1.8
DRUM = (-1.1, 1.15)          # drum centre (x, y); axis along x
PILLAR = (2.1, -1.3)
MAST = (2.7, 1.9)
PEN = [-0.45, 0.0, 0.45, 0.0]


def spike(S, name, x, y, bent=0):
    if bent:
        S.box(name, (0.1, 0.1, 0.9), (x, y * S.YS, 0.5), "steel", 0.0, rot=(bent, 0, 20), stretch=False)
        return
    S.cyl(name, 0.06, 0.95, (x, y, 0.6), "steel", verts=8)
    S.cone(name + "t", 0.1, 0.06, 0.12, (x, y, 1.12), "dark", verts=8)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    x0, x1, y0, y1 = HUT
    yw = y0 - 0.02
    S.bb("pad", -3.3, 3.3, -3.3, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -3.3, 3.3, -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    for k in range(12):
        S.bb(f"haz{k}", -3.2 + k * 0.5, -3.2 + (k + 1) * 0.5, -3.18, -2.86, 0.14, 0.2, "yellow" if k % 2 == 0 else "dark", 0.0)
    # ---- hut
    S.bb("hut", x0, x1, y0, y1, 0.14, H, "concrete", 0.05)
    S.bb("plinth", x0 - 0.08, x1 + 0.08, y0 - 0.08, y1 + 0.05, 0.14, 0.4, "concrete2", 0.03)
    S.bb("band", x0 - 0.02, x1 + 0.02, yw - 0.1, yw + 0.06, 1.15, 1.45, "house", 0.0)
    S.bb("bandE", x1, x1 + 0.1, y0, y1, 1.15, 1.45, "house", 0.0)
    if dmg:
        S.bb("roofW", x0 - 0.12, -1.4, y0 - 0.1, y1 + 0.05, H, H + 0.2, "roof", 0.04)
        S.bb("pit", -1.4, x1 + 0.1, y0 + 0.2, y1 - 0.1, H - 0.4, H - 0.3, "dark", 0.0)
        S.bb("roofF", -1.3, 0.2, y0 - 0.1, y0 + 0.8, H - 0.5, H - 0.3, "roof", 0.03, rot=(14, 0, 0))
        S.bb("jag", 0.0, 0.75, y0, y0 + 0.2, H, H + 0.3, "concrete", 0.02)
        S.bb("jag", -1.6, -1.2, y1 - 0.25, y1, H, H + 0.35, "concrete", 0.02)
        for k, (cx, cz, h) in enumerate(((-2.0, 0.5, 0.8), (0.3, 0.7, 0.7))):
            S.bb(f"crack{k}", cx, cx + 0.06, yw - 0.1, yw - 0.06, cz, cz + h, "crack", 0.0, nocast=True)
    else:
        S.bb("roof", x0 - 0.12, x1 + 0.12, y0 - 0.1, y1 + 0.05, H, H + 0.2, "roof", 0.04)
        S.bb("para", x0 - 0.12, x1 + 0.12, y0 - 0.1, y0, H + 0.2, H + 0.32, "concrete2", 0.02)
    # windows (lit) and door
    for k, wx in enumerate((-2.3, -1.5)):
        S.bb(f"wf{k}", wx - 0.3, wx + 0.3, yw - 0.06, yw + 0.04, 0.6, 1.1, "dark", 0.0)
        S.bb(f"win{k}", wx - 0.24, wx + 0.24, yw - 0.1, yw - 0.06, 0.66, 1.04, "amber" if (idle and not dmg) else "amber_dim", 0.0, nocast=True)
    S.bb("doorf", -0.6, 0.4, yw - 0.1, yw + 0.04, 0.14, 1.12, "dark", 0.0)
    S.bb("door", -0.5, 0.3, yw - 0.14, yw - 0.1, 0.2, 1.06, "steel", 0.02)
    S.bb("sign", -0.3, 0.1, yw - 0.16, yw - 0.14, 0.8, 0.95, "yellow", 0.0, nocast=True)
    if not dmg:
        S.bb("roofhouse", -2.6, -1.9, 1.9, 2.5, H + 0.2, H + 0.24, "house2", 0.0)
    # ---- drum recorder on the roof
    dx, dy = DRUM
    dz = H + 0.2 + 0.6
    if dmg:
        for sx in (-0.7, 0.7):
            S.bb("cradle", dx + sx - 0.08, dx + sx + 0.08, dy - 0.4, dy + 0.4, H + 0.2, H + 0.7, "steel", 0.02)
        # the drum thrown off its cradle: cream paper, dark end discs and ink bands still read
        fx_, fy_, fz_ = dx + 1.9, -2.3, 0.62
        S.cyl("drumF", 0.5, 1.4, (fx_, fy_, fz_), "paper", axis="X", verts=24)
        S.cyl("drumFc", 0.61, 0.1, (fx_ + 0.75, fy_, fz_), "steeld", axis="X", verts=24)
        S.cyl("drumFc", 0.61, 0.1, (fx_ - 0.75, fy_, fz_), "steeld", axis="X", verts=24)
        S.cyl("drumFa", 0.1, 0.3, (fx_ - 0.95, fy_, fz_), "steel", axis="X", verts=10)
        for k, bx in enumerate((-0.42, -0.05, 0.38)):
            S.cyl(f"drumFt{k}", 0.512, 0.08, (fx_ + bx, fy_, fz_), "dark", axis="X", verts=24, nocast=True)
    else:
        # A-frame cradles (two legs meeting under each axle) carrying a cream paper drum between dark steel end discs
        zb, za = H + 0.2, dz - 0.05
        for sx in (-1.0, 1.0):
            for sy in (-1, 1):
                L.beam(S, "cradle", (dx + sx, dy + sy * 0.55, zb), (dx + sx, dy + sy * 0.1, za), 0.1, "steel")
            S.bb("cradleb", dx + sx - 0.07, dx + sx + 0.07, dy - 0.6, dy + 0.6, zb, zb + 0.08, "steel", 0.0)
        S.cyl("drum", 0.5, 1.5, (dx, dy, dz), "paper", axis="X", verts=28)
        S.cyl("drumc", 0.61, 0.1, (dx + 0.8, dy, dz), "steeld", axis="X", verts=28)
        S.cyl("drumc", 0.61, 0.1, (dx - 0.8, dy, dz), "steeld", axis="X", verts=28)
        S.cyl("axle", 0.1, 0.5, (dx + 0.98, dy, dz), "steel", axis="X", verts=10)
        S.cyl("axle", 0.1, 0.5, (dx - 0.98, dy, dz), "steel", axis="X", verts=10)
        # continuous ink bands wrapping the whole circumference (thin rings just proud of the paper)
        for k, (bx, bw) in enumerate(((-0.5, 0.13), (-0.12, 0.09), (0.3, 0.13))):
            S.cyl(f"band{k}", 0.512, bw, (dx + bx, dy, dz), "dark", axis="X", verts=28, nocast=True)
        # wiggly trace + rolling ticks: 12 zig-zag dashes (period 60 deg) and 4 ticks (period 90 deg) that
        # advance with the frame so the idle loop closes after 4 frames
        for k in range(12):
            a = math.radians(k * 30 + (i * 15.0 if idle else 0))
            off = 0.06 if k % 2 == 0 else -0.06
            S.bb(f"wig{k}", dx + 0.62 + off - 0.05, dx + 0.62 + off + 0.05, dy + 0.5 * math.cos(a) - 0.05, dy + 0.5 * math.cos(a) + 0.05,
                 dz + 0.5 * math.sin(a) - 0.04, dz + 0.5 * math.sin(a) + 0.04, "dark", 0.0, nocast=True)
        for k in range(4):
            a = math.radians(k * 90 + (i * 22.5 if idle else 0))
            S.bb(f"tick{k}", dx - 0.36, dx - 0.22, dy + 0.5 * math.cos(a) - 0.05, dy + 0.5 * math.cos(a) + 0.05,
                 dz + 0.5 * math.sin(a) - 0.04, dz + 0.5 * math.sin(a) + 0.04, "dark", 0.0, nocast=True)
        # pen carriage: rail on posts above the drum, arm reaching down so the red pen touches the paper
        zr = dz + 0.95
        for sx in (-1.0, 1.0):
            S.bb("railpost", dx + sx - 0.05, dx + sx + 0.05, dy - 0.05, dy + 0.05, za, zr, "steel", 0.0)
        S.bb("penrail", dx - 1.05, dx + 1.05, dy - 0.04, dy + 0.04, zr, zr + 0.07, "steel", 0.0)
        px = dx + (PEN[i % 4] if idle else 0.0)
        S.bb("penblock", px - 0.1, px + 0.1, dy - 0.08, dy + 0.08, zr - 0.12, zr + 0.07, "steeld", 0.0)
        S.bb("penarm", px - 0.035, px + 0.035, dy - 0.035, dy + 0.035, dz + 0.55, zr - 0.1, "steel", 0.0)
        S.bb("pen", px - 0.07, px + 0.07, dy - 0.07, dy + 0.07, dz + 0.46, dz + 0.6, "red", 0.0)
    # ---- bedrock pillar and spike fan
    qx, qy = PILLAR
    S.cyl("pillar", 0.6, 0.5, (qx, qy, 0.4), "concrete2", verts=16)
    S.cyl("pillarcap", 0.5, 0.1, (qx, qy, 0.7), "steel", verts=16)
    S.bb("pillarpl", qx - 0.15, qx + 0.15, qy - 0.62, qy - 0.5, 0.35, 0.55, "house", 0.0, nocast=True)
    for k in range(5):
        a = math.radians(200 + k * 35)
        sx_, sy_ = qx + 1.2 * math.cos(a), qy + 1.2 * math.sin(a)
        spike(S, f"spike{k}", sx_, sy_, bent=(25 if dmg and k in (1, 3) else 0))
        L.beam(S, f"cab{k}", (sx_, sy_, 0.22), (qx, qy, 0.22), 0.04, "dark", nocast=True)
    L.beam(S, "cabH", (qx, qy, 0.22), (0.2, y0 - 0.3, 0.22), 0.06, "dark", nocast=True)
    # ---- mast
    mx, my = MAST
    if dmg:
        S.box("mast", (0.12, 0.12, 3.0), (mx + 0.4, my * YS, 1.5), "steel", 0.0, rot=(0, 18, 0), stretch=False)
        S.bb("lamp", mx + 0.9, mx + 1.1, my - 0.1, my + 0.1, 2.9, 3.0, "dark", 0.0)
    else:
        S.bb("mast", mx - 0.06, mx + 0.06, my - 0.06, my + 0.06, 0.14, 3.0, "steel", 0.0)
        for z in (1.0, 1.9):
            S.bb("mstay", mx - 0.4, mx + 0.4, my - 0.03, my + 0.03, z, z + 0.05, "steel", 0.0, nocast=True)
        S.bb("flag", mx + 0.06, mx + 0.62, my - 0.02, my + 0.02, 2.5, 2.9, "house", 0.0, nocast=True)
        S.bb("lamp", mx - 0.12, mx + 0.12, my - 0.12, my + 0.12, 3.0, 3.25, "amber" if (idle and i % 2 == 0) else "amber_dim", 0.02)
    S.bb("mbase", mx - 0.3, mx + 0.3, my - 0.3, my + 0.3, 0.14, 0.35, "concrete2", 0.03)
    if dmg:
        R.scorch(S, "scA", -1.2, yw - 0.14, 1.0, 1.6, 0.7)
        R.scorch(S, "scB", -0.5, 1.5, H - 0.25, 1.4, 1.0, 0.03, plane="T")


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_large", x=_a(-0.4, 1.4, 1.7)[0], y=_a(-0.4, 1.4, 1.7)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=_a(0.6, -2.3, 0.7)[0], y=_a(0.6, -2.3, 0.7)[1], phase=4),
    dict(state="damaged", kind="smoke_plume", x=_a(-0.4, 1.4, 1.8)[0], y=_a(-0.4, 1.4, 1.8)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(0.6, -2.3, 0.8)[0], y=_a(0.6, -2.3, 0.8)[1], phase=5),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.3, 3.1, -2.9, -2.1, 0.14, 0.8, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (-1.5, -2.8, z), "steel", axis="X", verts=10)
        S.cyl("barrel", 0.3, 0.7, (3.0, 0.2, 0.5), "rust", verts=12)
    S.bb("planks", 0.9, 2.5, 2.6, 3.0, 0.14, 0.5, "timber", 0.0)
    S.bb("cabin", -3.2, -2.3, -3.0, -2.2, 0.14, 1.1, "primer", 0.03)


def _left(S):
    S.bb("planks", -2.6, -1.2, -3.0, -2.7, 0.14, 0.3, "timber", 0.0)
    S.bb("crate", 2.3, 3.1, -2.9, -2.2, 0.14, 0.8, "crate", 0.03)
    L.spark(S, 0.0, -1.0, 1.5, 3, 6)


SPEC = dict(
    pad=(-3.3, 3.3, -3.3, 3.3), env=(-3.0, 0.8, -0.6, 2.9), H=2.2, boxes=[HUT], rings=[(DRUM[0], DRUM[1], 0.55)],
    clips=(1.0, 1.8), scaf=[(-2.9, -1.7, -1.6, -1.0), (-1.5, -0.4, -1.6, -1.0)],
    derrick=(3.0, 2.6, 3.4, (1.0, 1.4, 3.8)), sparks=[(-2.0, -0.5, 1.7), (0.0, 1.2, 1.8), (-1.0, 2.8, 1.8)],
    hide=("sten", "lamp", "win", "trace", "pen", "flag"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
