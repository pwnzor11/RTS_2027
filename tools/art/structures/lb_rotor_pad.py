"""Left Behind Rotor Pad (2x2): aircraft production, a landing pad with a rebuilt autogyro.  Original design.

A ring of half-buried drums round a trampled dust pad marked with a bone-painted H, an autogyro on its three little wheels (rust
tube keel, canvas seat, ash engine pod, a pusher propeller at the back, a tall mast with a two-bladed canvas rotor), a lean-to repair
hangar of patched panels on the north edge with a canvas roof and a house panel, fuel drums, a tyre stack and a windsock in the
house colour.  It does not pack.
Idle loop: the rotor turns 45 deg per frame (two symmetric blades, 4 frames = half a turn), the windsock flaps, the pad lamp blinks.
Damage (structural only, D-069): one rotor blade snapped, the autogyro tipped on its side, hangar roof down, windsock torn;
fire / smoke are FX anchors.  Build: surveyed pad, drum ring, hangar, the gyro assembled, finish.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_rotor_pad"
TITLE = "Left Behind Rotor Pad"
FOOTPRINT = (2, 2)
CANVAS = (128, 176)
ORIGIN = (0, 48)
DOCK = (0, 2)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
GX, GY = -0.3, -0.3
HUB_Z = 3.1
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", GX + 0.6, GY, 0.9, 0),   # the tipped gyro
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", GX + 0.6, GY, 1.2, 3),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 1.2, 2.0, 2.2, 5),       # hangar roof
]


def g_pad(S, dmg, i):
    K.pad(S, 3.25, 3.2)
    S.cyl("padring", 2.55, 0.05, (GX, GY, 0.12), "dust2", verts=32)
    S.cyl("padcenter", 2.35, 0.05, (GX, GY, 0.16), "dust", verts=32)
    # painted H
    S.bb("Hl", GX - 0.9, GX - 0.65, GY - 1.0, GY + 1.0, 0.2, 0.22, "bone", 0.0, nocast=True)
    S.bb("Hr", GX + 0.65, GX + 0.9, GY - 1.0, GY + 1.0, 0.2, 0.22, "bone", 0.0, nocast=True)
    S.bb("Hm", GX - 0.65, GX + 0.65, GY - 0.12, GY + 0.12, 0.2, 0.22, "bone", 0.0, nocast=True)
    # lopsided ring of drums (gaps on purpose)
    for k in range(11):
        if k in (3, 8):
            continue
        a = math.radians(k * 360 / 11 + 12)
        K.drum(S, f"ring{k}", GX + 2.75 * math.sin(a), GY + 2.65 * math.cos(a), 0.0, m=("rust", "teal", "ash2", "ochre")[k % 4], band="hazard" if k == 5 else "ash2", h=0.6, r=0.34)


def g_hangar(S, dmg, i):
    S.bb("hwall", -3.1, 3.1, 2.4, 2.8, 0.1, 2.6, "ash2", 0.04)
    for k, (x0, x1, m) in enumerate(((-3.0, -1.7, "teal"), (-1.6, 0.0, "rust"), (0.1, 1.5, "ash"), (1.6, 3.0, "teal"))):
        S.bb(f"hp{k}", x0, x1, 2.36, 2.4, 0.3, 2.3 + (k % 2) * 0.2, m, 0.0)
    S.bb("hpanel", -2.8, -1.9, 2.32, 2.36, 1.0, 1.9, "house", 0.0)
    K.post(S, "hpost1", -3.0, 1.3, 0.1, 2.6, 0.12, "wood")
    K.post(S, "hpost2", 3.0, 1.3, 0.1, 2.6, 0.12, "wood")
    if dmg:
        S.box("hroof", (3.0, 1.6, 0.07), (1.5, 1.7, 1.0), "canvas2_s", 0.0, rot=(-40, 0, 5))
        S.box("hroof2", (3.0, 1.4, 0.07), (-1.5, 1.7, 2.45), "canvas_s", 0.0, rot=(8, 0, 0))
    else:
        S.box("hroof", (6.4, 1.6, 0.07), (0, 1.7, 2.7), "canvas2", 0.0, rot=(-9, 0, 0))
        S.bb("hroofp1", -2.5, -1.0, 1.4, 2.2, 2.62, 2.66, "teal", 0.0, nocast=True)
        S.bb("hroofp2", 0.8, 2.2, 1.5, 2.3, 2.68, 2.72, "rust", 0.0, nocast=True)
    K.drum(S, "hd0", 2.3, 1.7, 0.1, m="rust", band="hazard")
    S.bb("hcrate", -2.9, -2.1, 1.5, 2.2, 0.1, 0.8, "wood", 0.03)


def g_gyro(S, dmg, i):
    gx, gy = GX, GY
    if dmg:
        # tipped on its side: keel rotated and lying on the pad
        S.box("keel", (2.4, 0.35, 0.3), (gx + 0.4, gy * S.YS, 0.45), "rust", 0.04, rot=(0, 0, 30), stretch=False)
        S.box("pod", (0.7, 0.8, 0.7), (gx + 1.1, (gy + 0.5) * S.YS, 0.55), "ash2", 0.04, rot=(0, 0, 30), stretch=False)
        S.box("seat", (0.6, 0.6, 0.15), (gx - 0.3, (gy - 0.3) * S.YS, 0.35), "canvas2_s", 0.0, rot=(0, 0, 30), stretch=False)
        K.beam(S, "mastfall", (gx - 0.4, gy + 0.2, 0.4), (gx - 1.8, gy + 1.2, 0.9), 0.14, "wood")
        S.box("blade", (2.2, 0.25, 0.06), (gx - 1.6, (gy + 0.9) * S.YS, 0.3), "canvas_s", 0.0, rot=(0, 0, 25), stretch=False)
        return
    # wheels (small) and keel along y
    S.box("keel", (0.4, 2.6, 0.3), (gx, gy * S.YS, 0.85), "rust", 0.04, stretch=False)
    S.box("keelx", (1.5, 0.2, 0.15), (gx, (gy - 0.6) * S.YS, 0.5), "ash2", 0.02, stretch=False)
    for sx in (-1, 1):
        S.cyl(f"gw{sx}", 0.28, 0.14, (gx + sx * 0.75, (gy - 0.6) * S.YS, 0.3), "rubber", axis="X", verts=12, stretch=False)
    S.cyl("gwf", 0.2, 0.12, (gx, (gy + 1.1) * S.YS, 0.25), "rubber", axis="X", verts=10, stretch=False)
    S.box("seat", (0.7, 0.7, 0.2), (gx, (gy - 0.1) * S.YS, 1.1), "canvas2", 0.1, stretch=False)
    S.box("seatback", (0.7, 0.12, 0.6), (gx, (gy - 0.5) * S.YS, 1.4), "canvas", 0.05, stretch=False)
    S.box("pod", (0.7, 0.9, 0.7), (gx, (gy + 0.9) * S.YS, 1.05), "ash2", 0.06, stretch=False)
    S.box("podstripe", (0.72, 0.9, 0.12), (gx, (gy + 0.9) * S.YS, 1.2), "hazard", 0.0, stretch=False, nocast=True)
    S.cyl("exhaust", 0.07, 0.6, (gx + 0.35, (gy + 0.4) * S.YS, 1.45), "dark", verts=6, stretch=False)
    # pusher propeller at the back: a thin disc of two blades
    S.cyl("prophub", 0.1, 0.2, (gx, (gy - 1.3) * S.YS, 1.0), "ochre", axis="Y", verts=8, stretch=False)
    S.box("prop", (0.14, 0.05, 1.3), (gx, (gy - 1.42) * S.YS, 1.0), "wood", 0.0, rot=(0, (i * 60) % 180, 0), stretch=False)
    # mast and rotor (two canvas blades with rust tips), turning 45 deg per frame
    K.beam(S, "mast", (gx, gy - 0.3, 1.1), (gx, gy - 0.1, HUB_Z), 0.16, "wood")
    K.beam(S, "mastb", (gx, gy + 0.6, 1.1), (gx, gy - 0.1, HUB_Z - 0.4), 0.1, "rust2")
    S.cyl("hub", 0.18, 0.2, (gx, (gy - 0.1) * S.YS, HUB_Z + 0.1), "ochre", verts=10, stretch=False)
    a = i * 45.0
    S.box("rotor", (5.0, 0.3, 0.07), (gx, (gy - 0.1) * S.YS, HUB_Z + 0.25), "canvas2", 0.0, rot=(0, 0, a), stretch=False)
    for sgn in (-1, 1):
        t = math.radians(a)
        S.box(f"rtip{sgn}", (0.7, 0.32, 0.08), (gx + sgn * 2.15 * math.cos(t), ((gy - 0.1) + sgn * 2.15 * math.sin(t) / S.YS) * S.YS, HUB_Z + 0.26), "rust", 0.0, rot=(0, 0, a), stretch=False, nocast=True)
    S.box("rotorfeather", (4.9, 0.1, 0.1), (gx, (gy - 0.1) * S.YS, HUB_Z + 0.2), "ash2", 0.0, rot=(0, 0, a), stretch=False, nocast=True)


def g_extras(S, dmg, i):
    # windsock in the house colour, fuel drums, tyre stack, pad lamp
    K.post(S, "sockpole", 2.5, -2.4, 0.1, 3.6 if not dmg else 2.2, 0.1, "wood")
    ph = i * math.pi / 2
    if dmg:
        S.box("sock", (0.3, 0.7, 0.3), (2.5, -2.5 * S.YS, 2.0), "house", 0.0, rot=(60, 0, 0), stretch=False)
    else:
        S.box("sock", (0.9, 0.34, 0.34), (2.5 + 0.45, -2.4 * S.YS, 3.4), "house", 0.0, rot=(0, 0, 6 * math.sin(ph)), stretch=False)
        S.box("sockband", (0.14, 0.36, 0.36), (2.5 + 0.55, -2.4 * S.YS, 3.4), "bone", 0.0, rot=(0, 0, 6 * math.sin(ph)), stretch=False, nocast=True)
    K.drum(S, "f0", -2.7, -2.5, 0.1, m="rust", band="hazard")
    K.drum(S, "f1", -2.1, -2.8, 0.1, m="ochre")
    for k in range(3):
        K.tyre(S, f"ty{k}", 1.4, -2.6, 0.1 + k * 0.36, 0.5, 0.34)
    S.sphere("padlamp", 0.1, (-2.9, 0.4, 0.9), "lamp" if (i % 2 == 0 and not dmg) else "lamp_off")
    K.post(S, "lampp", -2.9, 0.4, 0.1, 0.85, 0.08, "wood")


GROUPS = [g_pad, g_hangar, g_gyro, g_extras]


def site(S):
    K.stakes_site(S, 2.7, 2.7)


def dmg_extra(S, i):
    S.bb("scorch", GX - 0.2, GX + 1.4, GY - 0.6, GY + 0.8, 0.2, 0.22, "char", 0.0, nocast=True)
    S.bb("rubble", 2.0, 3.0, -1.2, -0.6, 0.1, 0.3, "ash2", 0.04)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
