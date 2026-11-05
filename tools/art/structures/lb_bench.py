"""Left Behind Tinker's Bench (2x2): tech and upgrades.  Original design.

A fixed workshop corner of the camp: a patched canvas lean-to over a long workbench (vise, anvil block, tool pegboard, a half-opened
radio chassis), a tall rack of tube radios with glowing bakelite dials, a scrap-metal back wall, a hand cart with two big wheels in
the front yard carrying a teal cabinet with a house panel, tyre stools, a crate stack and an antenna bundle with a small dish.
It does not pack.  Idle loop: tube glow flickers, welding sparks flash on alternate frames, the lamp string flickers, the flag flaps.
Damage (structural only, D-069): lean-to fallen in, rack toppled, antenna snapped, cart tipped; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_bench"
TITLE = "Left Behind Tinker's Bench"
FOOTPRINT = (2, 2)
CANVAS = (128, 176)
ORIGIN = (0, 48)
DOCK = (0, 2)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 0.4, 1.0, 2.8, 0),      # fallen lean-to
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 0.4, 1.0, 3.1, 3),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", -2.3, 1.7, 1.6, 5),     # toppled rack
]


def g_ground(S, dmg, i):
    K.pad(S, 3.25, 3.2)
    S.bb("back", -3.2, 3.2, 2.5, 2.9, 0.1, 3.0, "ash2", 0.04)
    for k, (x0, x1, m) in enumerate(((-3.1, -1.8, "rust"), (-1.7, -0.2, "teal"), (-0.1, 1.3, "ash"), (1.4, 3.1, "ochre"))):
        S.bb(f"backp{k}", x0, x1, 2.46, 2.5, 0.3 + (k % 2) * 0.2, 2.5 + (k % 3) * 0.2, m, 0.0)
    S.bb("peg", -0.8, 1.6, 2.44, 2.46, 1.3, 2.4, "wood", 0.0)
    for k in range(6):
        S.bb(f"tool{k}", -0.6 + k * 0.38, -0.5 + k * 0.38, 2.42, 2.44, 1.5 + (k % 2) * 0.3, 2.2 + (k % 3) * 0.1, "steel" if k % 2 else "rust", 0.0, nocast=True)


def g_bench(S, dmg, i):
    # workbench with vise, anvil, half-open radio chassis
    S.bb("benchtop", -1.1, 2.4, 0.6, 1.8, 1.0, 1.14, "wood", 0.02)
    for x in (-1.0, 2.2):
        for y in (0.7, 1.7):
            K.post(S, "bleg", x, y, 0.1, 1.0, 0.14, "wood")
    S.bb("anvil", -0.8, -0.2, 0.9, 1.5, 1.14, 1.5, "gunmetal", 0.03)
    S.bb("anvilh", -0.9, 0.0, 1.0, 1.4, 1.5, 1.62, "gunmetal", 0.02)
    S.bb("vise", 0.4, 0.8, 0.7, 1.0, 1.14, 1.5, "rust2", 0.02)
    S.bb("chassis", 1.0, 1.9, 0.9, 1.6, 1.14, 1.4, "ash", 0.03)
    S.bb("chassist", 1.1, 1.8, 1.0, 1.5, 1.4, 1.43, "dark", 0.0, nocast=True)
    for k in range(3):
        S.cyl(f"tube{k}", 0.07, 0.3, (1.2 + k * 0.25, 1.25, 1.55), "glass", verts=8)
    if not dmg and i % 2 == 0:
        K.spark(S, 0.1, 1.2, 1.8, i)
    S.bb("scope", -0.4, 0.1, 1.5, 1.8, 1.14, 1.6, "teal2", 0.03)
    S.bb("scopescr", -0.35, 0.05, 1.46, 1.5, 1.25, 1.5, "lamp" if (i % 3 != 1 and not dmg) else "lamp_off", 0.0, nocast=True)


def g_rack(S, dmg, i):
    # tall rack of tube radios on the west side
    if dmg:
        S.box("rackfall", (1.4, 2.2, 0.9), (-2.4, 1.6 * S.YS, 0.6), "ash2", 0.0, rot=(0, 0, 20), stretch=False)
        S.bb("radio", -3.0, -2.2, 0.5, 1.2, 0.1, 0.55, "teal", 0.04)
        return
    for x in (-3.0, -1.7):
        K.post(S, "rp", x, 1.0, 0.1, 3.2, 0.12, "wood")
        K.post(S, "rp", x, 2.3, 0.1, 3.2, 0.12, "wood")
    for k, z in enumerate((0.7, 1.5, 2.3, 3.1)):
        S.bb(f"shelf{k}", -3.05, -1.65, 0.95, 2.35, z, z + 0.08, "wood", 0.0)
    for k, (z, m) in enumerate(((0.78, "teal"), (1.58, "rust"), (2.38, "ochre"))):
        for j in range(2):
            x0 = -2.95 + j * 0.7
            S.bb(f"radio{k}{j}", x0, x0 + 0.6, 1.0, 1.7, z, z + 0.55, m, 0.04)
            lit = (i + k + j) % 3 != 0
            S.bb(f"dial{k}{j}", x0 + 0.18, x0 + 0.42, 0.97, 1.0, z + 0.2, z + 0.4, "lamp" if lit else "lamp_off", 0.0, nocast=True)


def g_roof(S, dmg, i):
    rise = 0.5
    run = 1.1
    ang = math.degrees(math.atan2(rise, run * S.YS))
    ln = math.hypot(run * S.YS, rise) / S.YS
    zr = 3.5
    for k, (x0, x1, m) in enumerate(((-1.2, 0.2, "canvas2"), (0.2, 1.6, "teal"), (1.6, 3.2, "canvas2"))):
        if dmg and k == 2:
            S.box(f"roof{k}", (x1 - x0, ln, 0.07), ((x0 + x1) / 2, 1.0, 1.0), "canvas2_s", 0.0, rot=(-35, 0, 6))
            continue
        S.box(f"roof{k}", (x1 - x0, ln, 0.07), ((x0 + x1) / 2, 2.05, zr - rise / 2), m if not dmg else m + "_s", 0.0, rot=(ang, 0, 0))
    for x in (-1.2, 3.0):
        if dmg and x > 0:
            continue
        K.post(S, "rpost", x, 1.5, 0.1, zr - rise + 0.05, 0.12, "wood")
    for k in range(5):
        x = -1.1 + k * 0.8
        S.bb(f"fringe{k}", x, x + 0.4, 1.5 + 0.04 * math.sin(i + k), 1.56 + 0.04 * math.sin(i + k), zr - rise - 0.3, zr - rise, "house" if k == 2 else ("canvas2" if k % 2 else "rust"), 0.0, nocast=True)
    # bulb string under the roof edge
    for k in range(4):
        lit = (i + k) % 2 == 0 and not dmg
        S.sphere(f"bulb{k}", 0.09, (-0.9 + k * 1.2, 1.45, 2.95 - 0.12 * math.sin(k * 1.9)), "lamp" if lit else "lamp_off", nocast=True)


def g_yard(S, dmg, i):
    # hand cart in the front yard carrying a teal cabinet with a house panel
    cx, cy = 1.6, -2.1
    ang = 0 if not dmg else 12
    S.bb("cartbed", cx - 1.0, cx + 1.0, cy - 0.5, cy + 0.5, 0.6, 0.72, "wood", 0.02)
    S.bb("cabinet", cx - 0.8, cx + 0.5, cy - 0.45, cy + 0.4, 0.72, 1.6, "teal" if not dmg else "teal_s", 0.05)
    S.bb("cabpanel", cx - 0.7, cx + 0.4, cy - 0.48, cy - 0.45, 0.95, 1.4, "house", 0.0)
    S.bb("cabtop", cx - 0.9, cx + 0.6, cy - 0.5, cy + 0.5, 1.6, 1.68, "rust2", 0.03)
    K.beam(S, "handle", (cx - 1.0, cy, 0.7), (cx - 2.0, cy, 0.9), 0.1, "wood")
    for k, x in enumerate((cx - 0.5, cx + 0.6)):
        K.wheel(S, f"cw{k}", x, cy - 0.5, 0.6, 0.55 if k == 0 else 0.5, 0.2, "rust")
    for k, (x, y) in enumerate(((-1.7, -1.8), (-2.4, -2.5))):
        K.tyre(S, f"stool{k}", x, y, 0.1, 0.45, 0.4)
    S.bb("crate", -3.0, -2.3, -0.2, 0.6, 0.1, 0.8, "wood", 0.03)
    S.bb("crateb", -2.9, -2.4, -0.1, 0.5, 0.8, 1.1, "canvas2", 0.1)
    K.drum(S, "d0", 2.9, -0.4, 0.1, m="rust", band="hazard")
    # antenna bundle and a small dish on the north-east corner
    ax, ay = 2.8, 1.9
    top = 3.6 if not dmg else 2.2
    for k, (dx, h) in enumerate(((-0.15, 1.0), (0.0, 1.5), (0.15, 0.7))):
        S.cyl(f"ant{k}", 0.03, top - 0.3 + h * 0.5, (ax + dx, ay, 0.3 + (top - 0.3 + h * 0.5) / 2), "dark", verts=5)
    S.cyl("antpole", 0.07, top, (ax, ay, top / 2 + 0.1), "wood", verts=8)
    if not dmg:
        S.cyl("dish", 0.4, 0.06, (ax - 0.3, ay - 0.2, 3.0), "canvas2", verts=14)
    K.flag(S, "flag", -2.9, -0.9, 4.2 if not dmg else 3.0, i, w=1.2, h=0.8, dmg=dmg)


GROUPS = [g_ground, g_bench, g_rack, g_roof, g_yard]


def site(S):
    K.stakes_site(S, 2.7, 2.7)


def dmg_extra(S, i):
    S.bb("scorch", -0.8, 2.0, 0.6, 1.8, 1.14, 1.16, "char", 0.0, nocast=True)
    S.bb("rubble", 2.0, 3.0, -3.0, -2.6, 0.1, 0.3, "ash2", 0.04)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
