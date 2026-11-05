"""Left Behind Dynamo Rig (2x2): cheap small power, a wind and pedal generator on a flatbed.  Original design.

A short flatbed on mismatched wheels carrying a teal battery-bus (rust patch plates, a house panel, glowing bulb bank on the roof),
a copper-coloured coil drum and car batteries in a crate rack, and a tall mast with a three-bladed sailcloth rotor that turns in
the wind (rust-trimmed canvas blades, ochre hub).  Cables run from the mast to the cabinet, a stovepipe vents the pedal shed.
Packs: the mast folds down along the deck and the rotor blades are tied together, so it can sit on a crawler.
Idle loop: the rotor turns 30 deg per frame (3 blades = 120 deg cycle), the bulb bank flickers, a flag flaps.  Damage (structural
only, D-069): mast snapped and leaning, one blade gone, cabinet roof torn, a wheel flat; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_dynamo_rig"
TITLE = "Left Behind Dynamo Rig"
FOOTPRINT = (2, 2)
CANVAS = (128, 192)
ORIGIN = (0, 64)
DOCK = (0, 2)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MX, MY = 1.7, 0.3        # mast foot
MAST_H = 4.6
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", -1.2, -0.6, 2.6, 0),    # cabinet roof
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -1.2, -0.6, 2.9, 2),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 2.3, 0.4, 1.5, 5),      # mast stump
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 2.3, 0.4, 1.8, 6),
]


def g_chassis(S, dmg, i):
    K.pad(S, 3.25, 3.2)
    S.bb("railS", -3.1, 3.0, -2.5, -2.2, 0.55, 1.1, "ash2", 0.04)
    S.bb("railN", -3.1, 3.0, 2.2, 2.5, 0.55, 1.1, "ash2", 0.04)
    S.bb("deck", -3.1, 3.0, -2.55, 2.55, 1.1, 1.28, "wood", 0.03)
    S.bb("bump", -3.15, -3.0, -2.2, 2.2, 0.6, 1.0, "hazard", 0.02)
    for k, (x, r) in enumerate(((-2.2, 0.6), (-0.8, 0.5), (1.2, 0.6), (2.4, 0.62))):
        rr = r * (0.78 if (dmg and k == 2) else 1.0)
        K.wheel(S, f"w{k}", x, -2.3, rr + 0.1, rr, 0.5, "steel" if k % 2 == 0 else "rust")
        K.wheel(S, f"wn{k}", x, 2.3, rr + 0.1, rr, 0.5)


def g_cabinet(S, dmg, i):
    S.bb("cab", -3.0, 0.5, -2.2, 1.6, 1.28, 3.0, "teal", 0.07)
    S.bb("cabroof", -3.1, 0.6, -2.3, 1.7, 3.0, 3.14, "canvas2" if not dmg else "canvas2_s", 0.04)
    S.bb("roofp1", -3.0, -1.6, 0.2, 1.5, 3.14, 3.17, "rust", 0.0, nocast=True)
    S.bb("roofp2", -0.8, 0.5, -2.2, -0.9, 3.14, 3.17, "teal", 0.0, nocast=True)
    S.bb("cabstripe", -3.0, 0.5, -2.25, -2.15, 1.9, 2.0, "bone", 0.0)
    S.bb("cabwin1", -2.7, -1.7, -2.24, -2.2, 2.1, 2.7, "glass", 0.0)
    S.bb("cabwin2", -1.4, -0.9, -2.24, -2.2, 2.1, 2.7, "wood", 0.0)
    S.bb("cabdoor", -0.3, 0.3, -2.24, -2.2, 1.3, 2.4, "ash2", 0.0)
    S.bb("cabpatch1", -2.9, -2.1, -2.27, -2.2, 1.4, 1.85, "rust", 0.0)
    S.bb("cabpatch2", -1.3, -0.6, -2.27, -2.2, 1.45, 1.9, "ash", 0.0)
    S.bb("cabpanel", -2.0, -0.4, -2.3, -2.27, 1.45, 2.0, "house", 0.0)
    if dmg:
        S.bb("roofhole", -2.0, -0.6, -1.4, 0.2, 3.14, 3.18, "char", 0.0, nocast=True)
        S.bb("scorch", -2.4, -1.4, -2.3, -2.28, 2.0, 2.9, "char", 0.0, nocast=True)
    else:
        for k in range(4):         # bulb bank on the roof, flickers
            lit = (i + k) % 3 != 0
            S.sphere(f"bulb{k}", 0.13, (-2.6 + k * 0.55, -1.6, 3.35), "lamp" if lit else "lamp_off", nocast=True)
        S.bb("bulbrail", -2.8, -0.5, -1.7, -1.5, 3.14, 3.24, "ash2", 0.0)


def g_gear(S, dmg, i):
    # coil drum, batteries and a crate rack on the deck east of the cabinet
    S.cyl("coil", 0.55, 1.3, (1.0, -1.5, 1.28 + 0.55), "ochre", axis="X", verts=16)
    for k, x in enumerate((0.6, 0.9, 1.2, 1.5)):
        S.cyl(f"coilw{k}", 0.58, 0.07, (x, -1.5, 1.28 + 0.55), "rust", axis="X", verts=16)
    for k in range(3):
        S.bb(f"batt{k}", 0.8 + k * 0.5, 1.2 + k * 0.5, 0.8, 1.35, 1.28, 1.7, "ash2", 0.02)
        S.bb(f"battc{k}", 0.85 + k * 0.5, 1.15 + k * 0.5, 0.85, 1.3, 1.7, 1.76, "hazard" if k == 1 else "rust", 0.0, nocast=True)
    S.bb("crate", -3.0, -2.2, 1.7, 2.4, 1.28, 2.0, "wood", 0.03)
    S.bb("crateb", -2.9, -2.3, 1.8, 2.3, 2.0, 2.3, "canvas2", 0.1)
    K.stovepipe(S, "spipe", -0.4, 0.9, 3.1, 4.4 if not dmg else 3.6, i, smoke=True, broken=dmg)


def g_mast(S, dmg, i):
    # mast (lattice of two poles), rotor with three sailcloth blades; damaged: snapped and leaning, one blade gone
    top = MAST_H if not dmg else 2.0
    K.post(S, "mastA", MX, MY, 1.28, top, 0.14, "wood")
    K.post(S, "mastB", MX + 0.35, MY, 1.28, top - 0.4, 0.1, "rust2")
    for k in range(3):
        S.bb(f"mastx{k}", MX - 0.07, MX + 0.45, MY - 0.06, MY + 0.06, 1.8 + k * 0.9, 1.88 + k * 0.9, "wood", 0.0, nocast=True) if top > 3.0 else None
    if dmg:
        K.beam(S, "mastleaning", (MX, MY, top), (MX + 1.0, MY - 0.8, 2.9), 0.14, "wood")
        S.sphere("hub", 0.2, (MX + 1.0, MY - 0.8, 2.95), "ochre")
        S.box("blade", (0.3, 0.08, 1.2), (MX + 1.0 - 0.3, (MY - 0.9) * S.YS, 2.5), "canvas_s", 0.0, rot=(0, 25, 0), stretch=False)
        return
    hz = top + 0.2
    S.cyl("hubcyl", 0.18, 0.4, (MX, (MY - 0.35) * S.YS, hz), "ochre", axis="Y", verts=12, stretch=False)
    S.sphere("hubn", 0.2, (MX, MY - 0.6, hz), "rust")
    a0 = i * 30.0
    for b in range(3):
        a = math.radians(a0 + b * 120)
        L = 1.55
        cx, cz = MX + math.sin(a) * L / 2 * 1.05, hz + math.cos(a) * L / 2 * 1.05
        S.box(f"blade{b}", (0.42, 0.06, L), (cx, (MY - 0.5) * S.YS, cz), "canvas" if b != 1 else "canvas2", 0.0, rot=(0, math.degrees(a), 0), stretch=False)
        tx, tz = MX + math.sin(a) * L * 0.92, hz + math.cos(a) * L * 0.92
        S.box(f"bladetip{b}", (0.44, 0.07, 0.2), (tx, (MY - 0.5) * S.YS, tz), "rust", 0.0, rot=(0, math.degrees(a), 0), stretch=False, nocast=True)
    # tail vane behind the hub (house colour), guy ropes to the deck
    S.bb("tail", MX + 0.25, MX + 1.15, MY - 0.9, MY - 0.84, hz - 1.9, hz - 1.3, "house", 0.0)
    K.beam(S, "guy1", (MX, MY, 3.8), (MX + 1.0, MY - 1.6, 1.28), 0.03, "rope", nocast=True)
    K.beam(S, "guy2", (MX, MY, 3.8), (MX - 0.6, MY - 1.8, 1.28), 0.03, "rope", nocast=True)


def g_extras(S, dmg, i):
    K.beam(S, "cable", (MX, MY - 0.1, 1.5), (0.4, -0.9, 3.0), 0.05, "dark", nocast=True)
    K.beam(S, "cable2", (0.4, -0.9, 3.0), (-0.5, -1.0, 3.1), 0.05, "dark", nocast=True)
    K.flag(S, "flag", -2.9, -1.6, 4.8 if not dmg else 3.8, i, w=1.2, h=0.8, dmg=dmg)
    K.drum(S, "drum0", 2.5, -1.9, 1.28, m="rust", band="hazard")
    K.drum(S, "drum1", 2.4, -0.9, 1.28, m="teal")
    S.bb("pedalshed", 1.9, 3.0, 1.2, 2.3, 1.28, 2.3, "rust2", 0.04)
    S.bb("pedalroof", 1.8, 3.1, 1.1, 2.4, 2.3, 2.4, "canvas2", 0.03)


GROUPS = [g_chassis, g_cabinet, g_gear, g_mast, g_extras]


def site(S):
    K.stakes_site(S, 2.7, 2.7)


def dmg_extra(S, i):
    S.bb("rubble", 2.6, 3.2, -3.1, -2.7, 0.1, 0.3, "ash2", 0.04)
    S.box("fallen", (1.2, 0.5, 0.1), (-0.6, -3.0 * S.YS, 0.2), "teal", 0.0, rot=(0, 0, 20), stretch=False)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
