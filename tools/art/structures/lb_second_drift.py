"""Left Behind Second Drift (3x3): the superweapon, a wind engine that calls the ash.  Original design.

The caravans have no bomb; they know how the ash moves.  A tall rust lattice tower in the middle carries a huge six-bladed sailcloth fan
and a pair of loudspeaker horns; a ring of mast-mounted cup anemometers (stolen, mismatched, hazard-banded) turns round it, two
sails lean on tilted masts to steer the storm, a teal control bus with a house panel and canvas awning sits at the south-west corner
with a stovepipe, a big house-colour banner hangs from the west mast, guy ropes run from tower to pad.
Charge state: the engine only knows idle / damaged / build, so idle is an 8-frame SLOW CHARGE: the fan turns 15 deg per frame (six
blades, 60 deg cycle: seamless over 8 frames), anemometer cups spin, four ash puffs spiral round the foot (90 deg cycle), lamps pulse.
Damage (structural only, D-069): fan bent with three blades gone, tower leaning, two anemometers snapped, bus roof torn, sail ripped;
fire / smoke are FX anchors.  Build: surveyed site, bus, ring masts, tower, fan and sails.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_second_drift"
TITLE = "Left Behind Second Drift"
FOOTPRINT = (3, 3)
CANVAS = (192, 280)
ORIGIN = (0, 88)
DOCK = (1, 3)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 8, 6), ("damaged", 4, 5), ("build", 8, 5)]
TX, TY = 0.4, 1.0          # tower centre
TOP = 6.6
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -2.8, -2.8, 2.6, 0),    # bus roof
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -2.8, -2.8, 2.9, 3),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", TX + 1.0, TY, 5.4, 5),  # broken tower
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", TX + 1.0, TY, 5.8, 7),
]
ANEMS = ((-4.0, -0.4), (4.2, -2.6), (-4.3, 3.2), (4.3, 2.4), (-1.6, 4.2))


def g_pad(S, dmg, i):
    K.pad(S, 4.9, 4.8)
    for k in range(10):
        S.bb(f"ray{k}", -4.6 + k * 0.95, -3.9 + k * 0.95, -4.7, -4.4, 0.1, 0.16, "hazard" if k % 2 == 0 else "dark", 0.0)
    S.cyl("ring", 3.3, 0.06, (TX, TY, 0.12), "dust2", verts=40)
    S.cyl("ring2", 3.0, 0.06, (TX, TY, 0.16), "dust", verts=40)
    for k in range(6):
        a = math.radians(k * 60 + 30)
        K.tyre(S, f"ty{k}", TX + 2.3 * math.sin(a), TY + 2.3 * math.cos(a), 0.14, 0.4, 0.3)


def g_bus(S, dmg, i):
    S.bb("bus", -4.6, -1.2, -4.0, -1.7, 0.1, 2.5, "teal", 0.07)
    S.bb("busroof", -4.7, -1.1, -4.1, -1.6, 2.5, 2.64, "canvas2" if not dmg else "canvas2_s", 0.04)
    S.bb("roofp", -4.4, -3.0, -3.9, -2.8, 2.64, 2.67, "rust", 0.0, nocast=True)
    S.bb("stripe", -4.6, -1.2, -4.05, -3.95, 1.55, 1.65, "bone", 0.0)
    for k, (x0, x1, m) in enumerate(((-4.4, -3.6, "glass"), (-3.3, -2.6, "wood"), (-2.3, -1.5, "glass"))):
        S.bb(f"win{k}", x0, x1, -4.04, -4.0, 1.8, 2.3, m, 0.0)
    S.bb("panel", -4.3, -2.9, -4.08, -4.04, 0.45, 1.3, "house", 0.0)
    S.bb("patch", -2.6, -1.6, -4.07, -4.0, 0.4, 1.0, "rust", 0.0)
    K.stovepipe(S, "spipe", -1.9, -2.2, 2.6, 3.8 if not dmg else 3.0, i, smoke=True, broken=dmg)
    if dmg:
        S.bb("roofhole", -4.0, -2.8, -3.6, -2.4, 2.64, 2.68, "char", 0.0, nocast=True)


def g_ring(S, dmg, i):
    for k, (x, y) in enumerate(ANEMS):
        broken = dmg and k in (1, 3)
        top = 3.2 if not broken else 2.0
        K.post(S, f"am{k}", x, y, 0.1, top, 0.12, "wood")
        S.cyl(f"amhub{k}", 0.1, 0.16, (x, y, top + 0.08), "ash2", verts=8)
        if broken:
            K.beam(S, f"amfall{k}", (x, y, top), (x + 0.9, y - 0.4, 0.3), 0.1, "wood")
            continue
        a0 = i * 15.0 + k * 31
        for b in range(3):
            a = math.radians(a0 + b * 120)
            ex, ey = x + 0.55 * math.cos(a), y + 0.55 * math.sin(a) / S.YS * S.YS * 0.0 + 0.0
            S.box(f"amarm{k}{b}", (0.6, 0.06, 0.06), (x + 0.3 * math.cos(a), (y + 0.3 * math.sin(a) * 0.9) * S.YS, top + 0.1), "steel", 0.0, rot=(0, 0, math.degrees(a)), stretch=False, nocast=True)
            S.cyl(f"amcup{k}{b}", 0.12, 0.16, (x + 0.6 * math.cos(a), y + 0.6 * math.sin(a) * 0.9, top + 0.1), ("rust", "hazard", "teal")[(b + k) % 3], verts=8)
        S.sphere(f"amlamp{k}", 0.07, (x, y, top + 0.3), "lamp" if (i + k) % 2 == 0 else "lamp_off", nocast=True)


def g_tower(S, dmg, i):
    top = TOP if not dmg else 4.2
    for ax in (-0.55, 0.55):
        for ay in (-0.55, 0.55):
            K.post(S, "leg", TX + ax * (1.0 if not dmg else 1.0), TY + ay, 0.1, top, 0.14, "rust2")
    z = 0.4
    while z < top - 0.6:
        for (x0, x1) in ((-0.55, 0.55),):
            K.beam(S, "br", (TX - 0.55, TY - 0.55, z), (TX + 0.55, TY - 0.55, z + 0.9), 0.07, "steel", nocast=True)
            K.beam(S, "br", (TX + 0.55, TY - 0.55, z), (TX - 0.55, TY - 0.55, z + 0.9), 0.07, "steel", nocast=True)
        S.bb("ringp", TX - 0.65, TX + 0.65, TY - 0.65, TY + 0.65, z, z + 0.08, "steel", 0.0, nocast=True)
        z += 0.9
    S.bb("base", TX - 1.0, TX + 1.0, TY - 1.0, TY + 1.0, 0.1, 0.45, "ash2", 0.04)
    S.bb("basestripe", TX - 1.0, TX + 1.0, TY - 1.02, TY - 0.98, 0.15, 0.4, "hazard", 0.0, nocast=True)
    if dmg:
        K.beam(S, "lean", (TX, TY, top), (TX + 1.4, TY - 0.6, 5.2), 0.3, "rust2")
        S.cyl("hubb", 0.25, 0.3, (TX + 1.5, TY - 0.7, 5.3), "ochre", verts=10)
        return
    S.bb("platform", TX - 0.9, TX + 0.9, TY - 0.9, TY + 0.9, 3.6, 3.7, "wood", 0.0)
    S.bb("platrail", TX - 0.9, TX + 0.9, TY - 0.92, TY - 0.88, 3.7, 4.2, "ash2", 0.0, nocast=True)
    for k, x in enumerate((-0.4, 0.4)):          # loudspeaker horns on the platform, aimed at the fan
        S.cone(f"horn{k}", 0.1, 0.45, 0.8, (TX + x, (TY - 0.2) * S.YS * 0.0 + (TY - 0.2), 4.3), "bone", verts=12, nocast=False, rot=(70, 0, 0))
        S.cyl(f"hornring{k}", 0.46, 0.05, (TX + x, TY - 0.6, 4.38), "rust", verts=12, rot_z=0)


def g_fan(S, dmg, i):
    if dmg:
        for k in range(3):
            a = math.radians(k * 120 + 20)
            S.box(f"bl{k}", (0.5, 0.06, 1.4), (TX + 1.5 + 0.7 * math.sin(a), (TY - 0.8) * S.YS, 5.3 + 0.7 * math.cos(a)), "canvas2_s", 0.0, rot=(0, math.degrees(a), 0), stretch=False)
        S.box("sailrip", (1.4, 0.06, 2.0), (3.6, 2.0 * S.YS, 2.2), "canvas_s", 0.0, rot=(0, 18, 0), stretch=False)
        K.flag(S, "flag", -4.6, -0.6, 3.6, i, w=1.5, h=1.0, dmg=True)
        return
    hz = TOP + 0.8
    hy = TY - 1.0
    S.cyl("hubcyl", 0.3, 0.5, (TX, hy * S.YS, hz), "ochre", axis="Y", verts=14, stretch=False)
    S.sphere("hubn", 0.28, (TX, hy - 0.4, hz), "rust")
    a0 = i * 15.0
    for b in range(6):
        a = math.radians(a0 + b * 60)
        L = 1.9
        cx, cz = TX + math.sin(a) * L / 2 * 1.1, hz + math.cos(a) * L / 2 * 1.1
        S.box(f"blade{b}", (0.6, 0.07, L), (cx, (hy - 0.2) * S.YS, cz), ("canvas", "canvas2", "teal")[b % 3], 0.0, rot=(0, math.degrees(a), 0), stretch=False)
        tx, tz = TX + math.sin(a) * L * 0.97, hz + math.cos(a) * L * 0.97
        S.box(f"bladetip{b}", (0.62, 0.08, 0.2), (tx, (hy - 0.2) * S.YS, tz), "rust" if b % 2 == 0 else "hazard", 0.0, rot=(0, math.degrees(a), 0), stretch=False, nocast=True)
    S.sphere("charge", 0.18, (TX, hy - 0.7, hz), "lamp" if i % 4 < 2 else "lamp_off", nocast=True)
    K.beam(S, "guy1", (TX, TY, 5.4), (TX + 3.6, TY - 3.0, 0.1), 0.04, "rope", nocast=True)
    K.beam(S, "guy2", (TX, TY, 5.4), (TX - 3.2, TY - 3.2, 0.1), 0.04, "rope", nocast=True)
    # two sails on tilted masts east of the tower, billowing
    for k, (sx, sy, h) in enumerate(((3.4, 1.6, 3.4), (2.6, -0.8, 2.8))):
        K.beam(S, f"smast{k}", (sx, sy, 0.1), (sx + 0.3, sy - 0.2, h + 0.8), 0.12, "wood")
        bill = 0.18 * math.sin((i + k * 3) * math.pi / 4)
        S.box(f"sail{k}", (1.3 + bill, 0.07, h * 0.8), (sx + 0.7 + bill / 2, (sy - 0.25) * S.YS, h * 0.5 + 0.5), "canvas" if k == 0 else "canvas2", 0.0, rot=(0, 6 * math.sin(i + k), 0), stretch=False)
    S.bb("sailhouse", 3.7, 4.5, 1.34, 1.38, 2.2, 3.0, "house", 0.0, nocast=True)
    K.flag(S, "flag", -4.6, -0.6, 6.2, i, w=1.9, h=1.3)
    # ash swirling round the foot (4 puffs, 90 deg cycle over 8 frames)
    for q in range(4):
        a = math.radians(q * 90 + i * 11.25)
        S.sphere(f"ash{q}", 0.34, (TX + 2.7 * math.cos(a), TY + 2.4 * math.sin(a), 0.5 + 0.12 * q), "smoke2" if q % 2 else "smoke", squash=0.7)


GROUPS = [g_pad, g_bus, g_ring, g_tower, g_fan]


def site(S):
    K.stakes_site(S, 4.3, 4.2)


def dmg_extra(S, i):
    S.bb("rubble", 2.8, 4.2, -4.4, -3.7, 0.1, 0.3, "ash2", 0.04)
    S.bb("scorch", TX - 1.5, TX + 1.5, TY - 1.5, TY + 1.5, 0.1, 0.12, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
