"""Left Behind Salvage Yard (2x2): the caravan's fixed, cheap refinery and silo.  Original design.

A scrapyard that happens to refine: a tall teal silo built from a rolled bus-roof with rust and ochre bands and a sailcloth cone
cap, a squat ochre settling tank on rust legs, a lean-to shed of patched panels with a hand-painted house panel and a stovepipe,
a sloped dump chute and a hazard-banded apron on the south side for the harvester, a derrick with a swinging hook, tyre walls round
the pad, scrap heaps, a mast with the house flag.  Does not pack: it sits in the dust on tyres and drums.
Idle loop: stovepipe smoke, hook swings, flag flaps, lamp blinks.  Damage (structural only, D-069): silo cap and a plate torn off,
tank leaning, shed roof fallen in, derrick bent; fire / smoke are FX anchors.  Build: surveyed site, tyres, tanks, shed, pipes, finish.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_yard"
TITLE = "Left Behind Salvage Yard"
FOOTPRINT = (2, 2)
CANVAS = (128, 176)
ORIGIN = (0, 48)
DOCK = (0, 2)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 2.2, 0.6, 1.8, 0),      # shed roof
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", -1.8, 1.4, 3.4, 4),     # silo breach
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 2.2, 0.6, 2.1, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -1.8, 1.4, 3.7, 5),
]
SILO = (-1.8, 1.3)


def g_ground(S, dmg, i):
    K.pad(S, 3.25, 3.2)
    for k in range(7):                                     # tyre wall along the north and west edge
        K.tyre(S, f"tyN{k}", -3.0 + k * 0.95, 3.0, 0.1, 0.5, 0.4)
        if k < 5:
            K.tyre(S, f"tyW{k}", -3.05, 2.1 - k * 0.95, 0.1, 0.5, 0.4)
    for k in range(5):
        K.tyre(S, f"tyN2{k}", -2.6 + k * 1.05 + (0.3 if k % 2 else 0), 3.0, 0.5, 0.48, 0.38, m="rubber")
    for k in range(8):                                     # hazard apron on the south edge

        S.bb(f"haz{k}", -3.2 + k * 0.8, -2.4 + k * 0.8, -3.2, -2.75, 0.1, 0.16, "hazard" if k % 2 == 0 else "dark", 0.0)


def g_tanks(S, dmg, i):
    sx, sy = SILO
    top = 3.0 if dmg else 3.8
    S.cyl("silo", 1.0, top, (sx, sy, 0.15 + top / 2), "teal", verts=24)
    for k, z in enumerate((0.7, 1.7, 2.7)):
        if z < top:
            S.cyl(f"siloband{k}", 1.04, 0.14, (sx, sy, z), "rust" if k != 1 else "ochre", verts=24)
    S.bb("silopatch", sx - 0.5, sx + 0.5, sy - 1.05, sy - 0.9, 1.0, 2.2, "rust2", 0.0, nocast=True)
    if dmg:
        S.cyl("silorim", 1.0, 0.1, (sx, sy, top + 0.2), "dark", verts=16)
        S.box("sheet", (1.2, 0.8, 0.1), (sx + 1.1, (sy - 0.6) * S.YS, 0.35), "teal", 0.0, rot=(0, 0, 30), stretch=False)
    else:
        S.cone("silocap", 1.15, 0.12, 0.7, (sx, sy, top + 0.5), "canvas2", verts=16, nocast=False)
        S.cyl("silovent", 0.1, 0.4, (sx, sy, top + 0.95), "ash2", verts=8)
    for k in range(5):                                      # ladder
        S.bb(f"rung{k}", sx + 0.95, sx + 1.2, sy - 0.2, sy - 0.16, 0.5 + k * 0.6, 0.56 + k * 0.6, "wood", 0.0, nocast=True)
    tx, ty = 0.7, 1.6                                       # settling tank on rust legs
    lean = 8 if dmg else 0
    S.cyl("tank", 0.8, 2.4, (tx, ty, 1.35), "ochre", axis="X", verts=20)
    for k, x in enumerate((-0.2, 1.6)):
        S.cyl(f"tankband{k}", 0.84, 0.12, (x, ty, 1.35), "rust", axis="X", verts=20)
    for x in (-0.1, 1.5):
        for y in (ty - 0.5, ty + 0.5):
            K.post(S, "leg", x, y, 0.1, 0.65, 0.14, "rust2")
    if dmg:
        K.post(S, "legx", 1.5, ty - 0.5, 0.1, 0.4, 0.14, "rust2")


def g_shed(S, dmg, i):
    # lean-to shed of patched panels on the east side, house panel on its south wall
    S.bb("shed", 1.3, 3.1, -1.5, 0.6, 0.15, 1.7, "rust2", 0.05)
    S.bb("shedp1", 1.4, 2.2, -1.54, -1.5, 0.35, 1.3, "teal", 0.0)
    S.bb("shedp2", 2.3, 3.0, -1.54, -1.5, 0.3, 1.0, "ash", 0.0)
    S.bb("shedwin", 2.3, 2.9, -1.54, -1.5, 1.1, 1.5, "glass", 0.0)
    if not dmg:
        S.box("shedroof", (2.2, 2.7, 0.1), (2.2, -0.45, 1.85), "canvas2", 0.0, rot=(-9, 0, 0))
        S.bb("shedhouse", 1.45, 2.15, -1.58, -1.54, 0.4, 1.1, "house", 0.0)
        K.stovepipe(S, "spipe", 2.1, 0.2, 1.8, 3.1, i, smoke=True)
    else:
        S.box("shedroof", (2.0, 1.6, 0.1), (2.0, -0.5, 0.8), "canvas2_s", 0.0, rot=(-30, 0, 8))
        S.bb("shedhouse", 1.45, 1.9, -1.58, -1.54, 0.4, 1.1, "house", 0.0)
        K.stovepipe(S, "spipe", 2.1, 0.2, 1.7, 2.3, i, broken=True)
    # sloped dump chute from the shed toward the apron
    ang = -math.degrees(math.atan2(0.9, 1.2 * S.YS))
    S.box("chute", (1.5, 1.4, 0.12), (0.9, -2.0, 0.7), "steel", 0.03, rot=(ang, 0, 0))
    for sx in (-1, 1):
        S.box("cheek", (0.14, 1.4, 0.4), (0.9 + sx * 0.75, -2.0, 0.78), "rust", 0.03, rot=(ang, 0, 0))


def g_pipes(S, dmg, i):
    sx, sy = SILO
    S.cyl("pipeH", 0.15, 1.4, (-0.6, sy - 0.2, 1.9), "ash2", axis="X", verts=10)
    S.cyl("pipeV", 0.15, 1.4, (0.05, sy - 0.2, 1.3), "ash2", verts=10)
    S.cyl("pipeF", 0.22, 0.1, (-0.6, sy - 0.2, 1.9), "rust", axis="X", verts=10)
    S.cyl("pipeT", 0.12, 1.8, (2.2, 1.6, 0.7), "ash", axis="Y", verts=10)
    for k, y in enumerate((2.3, 2.9)):
        K.drum(S, f"stack{k}", 2.5 + k * 0.5, y, 0.1, m="rust" if k == 0 else "teal")
    S.bb("heap", -3.0, -1.4, -2.6, -1.3, 0.1, 0.5, "ash2", 0.2)       # scrap heaps
    S.bb("heap2", -2.4, -1.6, -2.2, -1.5, 0.5, 0.8, "rust", 0.1)
    S.bb("heap3", -1.0, -0.2, -2.7, -2.0, 0.1, 0.4, "ochre", 0.15)


def g_extras(S, dmg, i):
    # derrick with a swinging hook, mast and flag, lamp
    dx, dy = 3.0, -2.3
    for ax in (-0.2, 0.2):
        for ay in (-0.2, 0.2):
            K.post(S, "dm", dx + ax, dy + ay, 0.1, 3.2, 0.08, "rust2")
    top = 3.2
    if dmg:
        K.beam(S, "boom", (dx, dy, top), (dx - 1.1, dy + 0.6, top - 0.9), 0.2, "rust")
    else:
        K.beam(S, "boom", (dx, dy, top), (dx - 1.9, dy, top + 0.4), 0.2, "rust")
        sw = 0.18 * math.sin(i * math.pi / 2)
        K.beam(S, "cable", (dx - 1.9, dy, top + 0.35), (dx - 1.9 + sw, dy, 2.0), 0.04, "dark", nocast=True)
        S.bb("hook", dx - 2.0 + sw, dx - 1.8 + sw, dy - 0.1, dy + 0.1, 1.8, 2.0, "hazard", 0.0)
    K.flag(S, "flag", -3.0, -2.4, 4.4 if not dmg else 3.2, i, dmg=dmg)
    S.sphere("lamp", 0.1, (-3.0, -2.2, 1.0), "lamp" if (i % 2 == 0 and not dmg) else "lamp_off")


GROUPS = [g_ground, g_tanks, g_shed, g_pipes, g_extras]


def site(S):
    K.stakes_site(S, 2.8, 2.7)


def dmg_extra(S, i):
    S.bb("scorch1", 1.3, 2.4, -1.6, -1.58, 0.5, 1.5, "char", 0.0, nocast=True)
    S.bb("scorch2", -2.5, -1.2, 0.1, 0.5, 0.15, 0.2, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
