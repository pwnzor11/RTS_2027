"""Authority Atomic Artillery Battery: a guarded gun pit with one huge elevated gun, a control bunker and the shell hoist (3x3 cells). Original design.

Tier-3 superweapon (rules: superweapon auth_atomic, charge 3000 ticks).  West-centre: a round concrete gun pit with a heavy
carriage and a very long barrel raised about 52 degrees towards the north-east, house-colour bands and a muzzle brake.  South-east: the
control bunker (slit windows, house fascia and door panels, roof house panel, periscope, aerial, key-switch lamp).  North-east: the
shell hoist, a trolley with the single warhead standing on end (white body, house band, dark nose, fins).  South-west: a sandbagged
guard pit with a twin machine gun.  Corner beacons on poles ring the pit, a cable trench links bunker and pit.
Idle loop: periscope turns, one beacon blinks.  Active (played while the weapon charges / is ready): all beacons chase, the warhead
and the bunker key lamp glow, glow rings stand over the muzzle.  No fire state: the shell is a projectile (auth_atomic_blast).
Damage is structural only (D-069): the barrel snapped with the muzzle half lying in the pit, ring wall cracked open, bunker roof
split, warhead toppled, beacons down; fire / smoke are FX anchors.  Not a unit producer: DOCK is the cell below the bunker.
Contract: see auth_refinery.py; build-site helpers come from auth_conyard.py.
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

ID = "auth_atomic_battery"
TITLE = "Authority Atomic Artillery Battery"
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
    "gunmetal": ("#2A2E31", dict(rough=0.35, metal=0.7)),
    "beacon": ("#E0452E", dict(rough=0.3, emit="#FF4A30")),
    "glow": ("#BFFFB0", dict(rough=0.3, emit="#9FFF90")),
    "warhead": ("#D9DBD0", dict(rough=0.5, metal=0.2)),
    "warhead_s": ("#8C8F86", dict(rough=0.5, metal=0.2)),
    "cable": ("#23262A", dict(rough=0.8)),
})
SOOT_SKIN = dict(L.SOOT_SKIN)
SOOT_SKIN.update({"steel2": "steel_s", "warhead": "warhead_s"})
PRIMER_SKIN = dict(L.PRIMER_SKIN)
PRIMER_SKIN.update({"steel2": "primer", "gunmetal": "dark", "beacon": "dark", "glow": "dark", "warhead": "primer", "cable": "dark"})

PIT = (-1.5, 1.0)       # gun pit centre
PIT_R = 2.0
BK = (1.3, 4.5, -3.9, -0.9)   # control bunker x0, x1, y0, y1
BH = 1.7
EL = 52.0
AZ = math.atan2(0.45, 1.0)     # barrel heading: north, leaning east
PIVOT = (PIT[0], PIT[1] - 0.5, 1.5)
BLEN = 4.2
HOIST = (3.4, 2.5)


def bpt(t):
    """Point on the barrel axis at distance t from the pivot."""
    c, s = math.cos(math.radians(EL)), math.sin(math.radians(EL))
    return (PIVOT[0] + t * c * math.sin(AZ), PIVOT[1] + t * c * math.cos(AZ), PIVOT[2] + t * s)


def sandbag_ring(S, cx, cy, r, n, rows, dmg=False, name="sbr"):
    for row in range(rows):
        for k in range(n):
            if dmg and (k * 3 + row) % 5 == 0:
                continue
            a = 2 * math.pi * (k + 0.5 * (row % 2)) / n
            S.box(f"{name}{row}_{k}", (0.5, 0.3, 0.2), (cx + r * math.sin(a), (cy + r * math.cos(a)) * S.YS, 0.14 + 0.2 * row + 0.1),
                  "sandbag" if (k + row) % 2 == 0 else "sandbag2", 0.05, rot=(0, 0, -math.degrees(a)), stretch=False)


def finished(S, i, dmg, mode):
    idle = mode in ("idle", "active")
    act = mode == "active"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    px_, py_ = PIT
    S.bb("pad", -5.0, 5.0, -5.0, 5.0, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    for k in range(20):
        S.bb(f"haz{k}", -5.0 + k * 0.5, -5.0 + (k + 1) * 0.5, -4.88, -4.55, 0.14, 0.2, "yellow" if k % 2 == 0 else "dark", 0.0)
    # ---- gun pit: thick ring wall with a dark floor, rim, carriage
    S.cyl("ring", PIT_R, 0.9, (px_, py_, 0.59), "concrete", verts=36)
    S.cyl("rim", PIT_R + 0.1, 0.12, (px_, py_, 1.0), "concrete2", verts=36)
    S.cyl("floor", PIT_R - 0.3, 0.06, (px_, py_, 1.05), "dark", verts=32)
    for k in range(8):       # hazard ticks on the rim
        a = math.radians(k * 45 + 22.5)
        S.box(f"tick{k}", (0.35, 0.14, 0.04), ((px_ + (PIT_R + 0.02) * math.sin(a)), (py_ + (PIT_R + 0.02) * math.cos(a)) * YS, 1.08),
              "yellow" if k % 2 == 0 else "dark", 0.0, rot=(0, 0, -math.degrees(a)), stretch=False, nocast=True)
    if dmg:     # a bite out of the ring wall, rubble in front
        S.bb("bite", px_ + 0.8, px_ + 1.9, py_ - 1.9, py_ - 0.7, 0.14, 1.12, "dark", 0.0, rot=(0, 0, 0)) if False else None
        S.bb("breachw", px_ - 1.7, px_ - 0.7, py_ - 2.05, py_ - 1.55, 0.3, 1.12, "dark", 0.0)
        for k, (bx, by, rz) in enumerate(((-2.2, -1.6, 20), (-0.6, -2.4, -30), (-3.2, 0.4, 40), (0.6, -2.1, 10))):
            S.bb(f"brub{k}", bx - 0.4, bx + 0.4, by - 0.25, by + 0.25, 0.14, 0.45, "concrete2", 0.05, rot=(0, 0, rz))
    # carriage: turntable, two cheek plates (house), trunnion housing
    S.cyl("turn", 1.0, 0.25, (px_, py_ - 0.3, 1.2), "steel", verts=24)
    for sx in (-1, 1):
        S.bb(f"cheek{sx}", px_ + sx * 0.65 - 0.14, px_ + sx * 0.65 + 0.14, py_ - 1.0, py_ + 0.2, 1.3, 2.2, "hull", 0.04)
        S.bb(f"cheekh{sx}", px_ + sx * 0.8 - 0.03, px_ + sx * 0.8 + 0.03, py_ - 0.85, py_ + 0.05, 1.45, 2.05, "house", 0.0, nocast=True)
    S.bb("rearbox", px_ - 0.7, px_ + 0.7, py_ - 1.5, py_ - 0.9, 1.3, 1.9, "hull2", 0.05)
    S.bb("rearhouse", px_ - 0.5, px_ + 0.5, py_ - 1.54, py_ - 1.5, 1.4, 1.8, "house", 0.0, nocast=True)
    S.bb("reartop", px_ - 0.5, px_ + 0.5, py_ - 1.4, py_ - 1.0, 1.9, 1.94, "house2", 0.0, nocast=True)
    # barrel (square section stand-in for the round tube) with recoil cylinders, sleeve, bands, brake
    if not dmg:
        L.beam(S, "barrel", bpt(-0.5), bpt(BLEN), 0.42, "steel2")
        L.beam(S, "sleeve", bpt(-0.2), bpt(1.2), 0.6, "hull2")
        for sx in (-0.2, 0.2):
            a0, a1 = bpt(0.4), bpt(1.9)
            L.beam(S, "recoil", (a0[0] + sx, a0[1], a0[2] + 0.36), (a1[0] + sx, a1[1], a1[2] + 0.36), 0.1, "steel")
        L.beam(S, "band1", bpt(2.0), bpt(2.5), 0.5, "house")
        L.beam(S, "band2", bpt(3.1), bpt(3.3), 0.48, "yellow", nocast=True)
        L.beam(S, "brake", bpt(BLEN - 0.45), bpt(BLEN + 0.1), 0.58, "gunmetal")
        L.beam(S, "breech", bpt(-0.9), bpt(-0.3), 0.6, "gunmetal")
        if act:
            for k in range(3):
                t = BLEN + 0.25 + 0.3 * ((i + k) % 3)
                c = bpt(t)
                S.cyl(f"muz{k}", 0.2 + 0.12 * ((i + k) % 3), 0.05, (c[0], c[1], c[2]), "glow", verts=12, nocast=True)
    else:
        L.beam(S, "barrel", bpt(-0.5), bpt(2.0), 0.42, "steel2")
        L.beam(S, "sleeve", bpt(-0.2), bpt(1.2), 0.6, "hull2")
        L.beam(S, "band1", bpt(1.2), bpt(1.7), 0.5, "house")
        L.beam(S, "breech", bpt(-0.9), bpt(-0.3), 0.6, "gunmetal")
        L.beam(S, "fallen", (px_ + 1.6, py_ - 0.4, 1.15), (px_ + 0.2, py_ + 0.4, 1.3), 0.42, "steel2")
        L.beam(S, "fallenb", (px_ + 0.9, py_ - 0.1, 1.28), (px_ + 0.6, py_ + 0.1, 1.3), 0.5, "house")
    # ---- corner beacons round the pit
    for k, (bx, by) in enumerate(((px_ - 2.45, py_ - 2.0), (px_ + 2.45, py_ - 2.0), (px_ - 2.45, py_ + 2.0), (px_ + 2.45, py_ + 2.0))):
        if by > 4.6 or bx < -4.9:
            continue
        if dmg and k % 2:
            L.beam(S, f"bpole{k}", (bx, by, 0.14), (bx + 0.5, by - 0.2, 0.5), 0.1, "steel")
            continue
        S.cyl(f"bpole{k}", 0.05, 1.3, (bx, by, 0.8), "steel", verts=6)
        on = (act and (i + k) % 2 == 0) or (not act and idle and k == i % 4) if not dmg else False
        S.cyl(f"beac{k}", 0.15, 0.2, (bx, by, 1.5), "beacon" if on else "dark", verts=10, nocast=True)
    # ---- control bunker
    x0, x1, y0, y1 = BK
    yf = y0 - 0.03
    S.bb("bunker", x0, x1, y0, y1, 0.14, BH, "concrete", 0.06)
    S.bb("bfoot", x0 - 0.1, x1 + 0.1, y0 - 0.12, y1 + 0.08, 0.14, 0.4, "concrete2", 0.03)
    S.bb("broof", x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, BH, BH + 0.2, "concrete2", 0.05)
    S.bb("broof2", x0 + 0.3, x1 - 0.3, y0 + 0.3, y1 - 0.3, BH + 0.2, BH + 0.4, "concrete", 0.05)
    S.bb("bfascia", x0, x1, yf - 0.1, yf + 0.04, 1.15, 1.45, "house", 0.0)
    S.bb("broofhouse", x0 + 0.6, x1 - 0.6, y0 + 0.5, y0 + 1.5, BH + 0.4, BH + 0.44, "house2", 0.0)
    for k in range(3):
        sx = x0 + 0.55 + k * 0.7
        S.bb(f"slit{k}", sx - 0.28, sx + 0.28, yf - 0.06, yf + 0.04, 0.6, 0.95, "dark", 0.0)
        S.bb(f"slitl{k}", sx - 0.36, sx + 0.36, yf - 0.16, yf - 0.04, 0.95, 1.04, "concrete2", 0.02)
    dx = x1 - 0.75
    S.bb("doorf", dx - 0.4, dx + 0.4, yf - 0.1, yf + 0.04, 0.14, 1.05, "dark", 0.0)
    S.bb("door", dx - 0.32, dx + 0.32, yf - 0.16, yf - 0.1, 0.14, 1.0, "steel", 0.02)
    S.bb("dhouse", dx - 0.62, dx - 0.42, yf - 0.12, yf - 0.06, 0.2, 1.05, "house", 0.0)
    keylamp = act or (idle and i % 2 == 0 and False)
    S.cyl("keylamp", 0.1, 0.12, (dx, yf - 0.2, 1.2), "glow" if act else "amber_dim", verts=8, nocast=True)
    if not dmg:
        S.cyl("pscope", 0.07, 0.5, (x0 + 0.8, y1 - 0.8, BH + 0.65), "steel", verts=8)
        a = (i * 90) if idle else 0
        S.box("pshead", (0.34, 0.14, 0.14), (x0 + 0.8, (y1 - 0.8) * YS, BH + 0.95), "gunmetal", 0.0, rot=(0, 0, -a), stretch=False)
        S.cyl("whip", 0.025, 1.4, (x1 - 0.5, y1 - 0.6, BH + 0.4 + 0.7), "steel", verts=6)
        S.bb("bvent", x0 + 1.6, x0 + 2.3, y1 - 1.0, y1 - 0.4, BH + 0.4, BH + 0.62, "concrete2", 0.04)
    else:
        S.bb("bhole", x0 + 0.8, x1 - 0.8, y0 + 0.8, y1 - 0.5, BH + 0.2, BH + 0.42, "dark", 0.0)
        for k, (jx, jz) in enumerate(((x0 + 0.9, 0.4), (x0 + 1.5, 0.6), (x1 - 1.0, 0.35))):
            S.bb(f"bjag{k}", jx - 0.25, jx + 0.25, y1 - 1.0, y1 - 0.7, BH + 0.4, BH + 0.4 + jz, "concrete", 0.0, rot=(0, 0, 14 * k))
        S.bb("bsplit", x0 + 1.5, x0 + 1.6, yf - 0.12, yf - 0.08, 0.15, 1.5, "crack", 0.0, nocast=True)
        R.scorch(S, "scB", x0 + 1.2, yf - 0.16, 0.9, 1.4, 0.9)
    # cable trench from bunker to pit
    S.bb("trench", -0.2, 1.35, -1.3, -1.1, 0.14, 0.17, "cable", 0.0, nocast=True)
    S.bb("trench2", -0.2, 0.0, -1.3, 0.2, 0.14, 0.17, "cable", 0.0, nocast=True)
    # ---- shell hoist: trolley on rails with the warhead standing on end
    hx, hy = HOIST
    S.bb("rail", hx - 1.1, hx + 1.1, hy - 0.5, hy - 0.4, 0.14, 0.2, "steel", 0.0)
    S.bb("rail", hx - 1.1, hx + 1.1, hy + 0.4, hy + 0.5, 0.14, 0.2, "steel", 0.0)
    S.bb("trolley", hx - 0.7, hx + 0.7, hy - 0.5, hy + 0.5, 0.2, 0.5, "hull", 0.04)
    S.bb("trollh", hx - 0.7, hx - 0.2, hy - 0.54, hy - 0.5, 0.25, 0.45, "house", 0.0, nocast=True)
    if not dmg:
        S.cyl("warhead", 0.32, 1.5, (hx, hy, 0.5 + 0.75), "warhead", verts=16)
        S.cone("nose", 0.32, 0.04, 0.55, (hx, hy, 2.5), "gunmetal", verts=16, nocast=False)
        S.cyl("whband", 0.34, 0.28, (hx, hy, 1.4), "house", verts=16)
        S.cyl("whband2", 0.335, 0.1, (hx, hy, 1.85), "glow" if act else "yellow", verts=16, nocast=True)
        for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            S.bb("fin", hx + sx * 0.4 - (0.03 if sy == 0 else 0.15), hx * 0 + hx + sx * 0.4 + (0.03 if sy == 0 else 0.15), hy + sy * 0.4 - (0.15 if sy == 0 else 0.03),
                 hy + sy * 0.4 + (0.15 if sy == 0 else 0.03), 0.5, 0.95, "gunmetal", 0.0) if False else None
        for sx in (-1, 1):
            S.bb("fin", hx + (0.3 if sx > 0 else -0.55), hx + (0.55 if sx > 0 else -0.3), hy - 0.03, hy + 0.03, 0.5, 0.95, "gunmetal", 0.0)
        for sy in (-1, 1):
            S.bb("fin", hx - 0.03, hx + 0.03, hy + (0.3 if sy > 0 else -0.55), hy + (0.55 if sy > 0 else -0.3), 0.5, 0.95, "gunmetal", 0.0)
        for sx in (-0.55, 0.55):
            L.beam(S, "cradle", (hx + sx, hy - 0.4, 0.5), (hx + sx * 0.5, hy - 0.2, 1.7), 0.07, "steel")
    else:
        S.cyl("warhead", 0.32, 1.5, (hx - 0.2, hy - 0.2, 0.62), "warhead", axis="X", verts=16, rot_z=0)
        S.cone("nose", 0.32, 0.04, 0.55, (hx + 0.9, hy - 0.2, 0.62), "gunmetal", verts=16, nocast=False, rot=(0, 90, 0))
        S.cyl("whband", 0.34, 0.28, (hx - 0.3, hy - 0.2, 0.62), "house", axis="X", verts=16)
        R.scorch(S, "scW", hx, hy - 0.6, 0.2, 1.6, 0.6, 0.03, plane="T")
    # ---- guard pit: sandbag ring with a twin MG
    gx, gy = -3.6, -2.7
    sandbag_ring(S, gx, gy, 0.95, 10, 2, dmg, "gsb")
    S.cyl("gfloor", 0.7, 0.06, (gx, gy, 0.17), "dark", verts=16)
    S.cyl("gped", 0.1, 0.5, (gx, gy, 0.42), "steel", verts=8)
    ga = 25 if not dmg else 80
    for sx in (-0.07, 0.07):
        S.box("gmg", (0.07, 0.8, 0.07), (gx + sx + 0.2 * math.sin(math.radians(ga)), gy * YS + 0.2 * math.cos(math.radians(ga)), 0.7), "gunmetal", 0.0,
              rot=(0, 0, -ga), stretch=False)
    S.box("gshield", (0.5, 0.05, 0.3), (gx + 0.1, gy * YS - 0.08, 0.66), "house", 0.0, rot=(0, 0, -ga), stretch=False)
    # signal board
    S.bb("boardp", -0.1, 0.0, -4.1, -4.0, 0.14, 1.0, "timber", 0.0)
    S.bb("boardp", 0.5, 0.6, -4.1, -4.0, 0.14, 1.0, "timber", 0.0)
    S.bb("board", -0.2, 0.7, -4.15, -4.08, 0.6, 1.3, "yellow", 0.02)
    S.cyl("boardd", 0.22, 0.04, (0.25, -4.17, 0.95), "dark", axis="Y", verts=12, nocast=True, stretch=False) if False else None
    S.bb("boardx", 0.1, 0.4, -4.17, -4.15, 0.8, 1.1, "dark", 0.0, nocast=True)
    if dmg:
        R.scorch(S, "scG", px_ + 0.3, py_ - 0.3, 1.08, 2.4, 1.6, 0.03, plane="T")


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


_bt = bpt(2.0)
FX = [
    dict(state="damaged", kind="fire_large", x=_a(PIT[0], PIT[1], 1.6)[0], y=_a(PIT[0], PIT[1], 1.6)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=_a(2.9, -2.4, BH + 0.5)[0], y=_a(2.9, -2.4, BH + 0.5)[1], phase=3),
    dict(state="damaged", kind="fire_small", x=_a(HOIST[0], HOIST[1], 0.8)[0], y=_a(HOIST[0], HOIST[1], 0.8)[1], phase=5),
    dict(state="damaged", kind="smoke_plume", x=_a(PIT[0], PIT[1], 1.8)[0], y=_a(PIT[0], PIT[1], 1.8)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(_bt[0], _bt[1], _bt[2])[0], y=_a(_bt[0], _bt[1], _bt[2])[1], phase=4),
    dict(state="damaged", kind="smoke_plume", x=_a(2.9, -2.4, BH + 0.7)[0], y=_a(2.9, -2.4, BH + 0.7)[1], phase=6),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.6, 3.4, -4.7, -4.1, 0.14, 0.8, "crate", 0.03)
        S.bb("crate", 2.75, 3.3, -4.6, -4.2, 0.8, 1.3, "crate", 0.03, rot=(0, 0, 12))
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.6, (-1.0, -4.3, z), "steel", axis="X", verts=10)
        S.cyl("barrel", 0.3, 0.7, (4.5, 0.5, 0.5), "rust", verts=12)
    S.bb("planks", 0.5, 2.5, 4.2, 4.5, 0.14, 0.5, "timber", 0.0)


def _left(S):
    S.bb("planks", -4.5, -3.0, -4.4, -4.1, 0.14, 0.3, "timber", 0.0)
    S.bb("crate", 3.0, 3.9, 0.2, 1.0, 0.14, 0.8, "crate", 0.03)
    L.spark(S, 2.0, -2.0, 1.7, 3, 6)


SPEC = dict(
    pad=(-5.0, 5.0, -5.0, 5.0), env=(-3.5, 4.5, -3.9, 3.0), H=2.0, boxes=[BK], rings=[(PIT[0], PIT[1], PIT_R)],
    clips=(1.0, 1.7), scaf=[(0.0, 0.9, -2.6, -1.9), (-4.6, -3.7, 1.5, 2.2)],
    derrick=(4.6, 3.8, 5.0, (2.5, 3.0, 5.4)), sparks=[(2.5, -2.4, 1.6), (-1.5, 1.0, 1.6), (4.0, 2.5, 1.4)],
    hide=("muz", "beac", "keylamp", "warhead", "nose", "whband", "fin", "pshead", "pscope", "whip", "cradle", "gshield", "band1", "cheekh"),
    clutter=_clutter, left=_left)


def build(S, state, i, n):
    if state == "active":
        finished(S, i, False, "active")
        return
    L.stages(S, state, i, SPEC, finished)
