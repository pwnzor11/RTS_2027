"""Neutral Ruined Tenement (3x2): a burnt-out 1960s apartment block, garrisonable map feature, never built.  Original design.

NEUTRAL: no faction paint.  Weathered brick and bare concrete in the default (grey-green / rust) palette.  The only magenta is one small
cloth strip hung from a window and a rag on a roof pole: it is the claim marker, so the sprite still reads once a garrison takes the
building (a ruin has no team colour until then, the runtime remaps the ramp to the owner or to neutral).
The read: a three-storey slab block with a south face of brick piers and burnt window openings (black soot streaks above them), concrete
balcony slabs on the middle bays, a taller stairwell tower at the east end with slit windows, the roof gone (charred joists across the
top, floor slabs showing through), a rusty fire escape on the west end, a fallen section of the top floor spilled as a rubble heap
with weeds in front, a crumpled sign board and a dead tree stump.
States: idle (4: the rag and cloth sway, a thin smoke wisp from a smouldering heap), damaged (4, STRUCTURAL only: a south wall bay
collapsed to the ground floor, the top slab sagging, the tower top broken; fire / smoke are FX anchors).  No build state (never built).
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint x in [-5.1, 5.1], y in [-3.4, 3.4].
"""
import importlib.util
import math
import os

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structures_auth_refinery_ref", os.path.join(_here, "auth_refinery.py"))
_ref = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_ref)

ID = "neu_ruin_tenement"
TITLE = "Neutral Ruined Tenement"
FOOTPRINT = (3, 2)
CANVAS = (192, 216)
ORIGIN = (0, 72)
DOCK = (1, 2)
CAP_MATERIAL = "concrete2"
STATES = [("idle", 4, 5), ("damaged", 4, 5)]

MATERIALS = dict(_ref.MATERIALS)
MATERIALS.update({
    "brick": ("#8A5532", dict(rough=1.0)), "brick2": ("#6B3F26", dict(rough=1.0)), "brick3": ("#A16A40", dict(rough=1.0)),
    "plaster": ("#9DA08E", dict(rough=1.0)), "burnt": ("#1C1B17", dict(rough=1.0)), "ruinpad": ("#5A5E52", dict(rough=1.0)),
    "weed": ("#5C6A51", dict(rough=1.0)), "weed2": ("#3B4533", dict(rough=1.0)), "ironrust": ("#5E3420", dict(rough=0.8, metal=0.3)),
    "brick_s": ("#5B3A26", dict(rough=1.0)), "brick2_s": ("#46301F", dict(rough=1.0)), "brick3_s": ("#6D4A30", dict(rough=1.0)),
    "plaster_s": ("#6D6F63", dict(rough=1.0)),
})
SOOT_SKIN = {"brick": "brick_s", "brick2": "brick2_s", "brick3": "brick3_s", "plaster": "plaster_s",
             "concrete": "concrete_s", "concrete2": "concrete2_s"}
PALETTE = None

PX0, PX1 = -4.8, 4.8
YS_, YN = -2.0, 2.4
T = 0.55                    # wall thickness
FH = 1.5                    # storey height
Z0 = 0.14
STOREYS = 3
WIN_X = (-4.0, -2.4, -0.8, 0.8, 2.4)      # window centres on the main block
WIN_W = 0.45               # half width
TOWER = (3.6, 4.8)


def px(x, y, z):
    return round(96 + x * 18.8), round(72 + 64 - y * 18.8 - z * 14.4)


def south_wall(S, dmg, i):
    """Brick piers + spandrels + lintels with window openings; storeys 0..2.  dmg: bay 3 (x 0.8) collapsed above the ground floor."""
    xs = [PX0] + [v for w in WIN_X for v in (w - WIN_W, w + WIN_W)] + [TOWER[0]]
    for f in range(STOREYS):
        zb = Z0 + f * FH
        top_gone = {(0, 2): False}
        # spandrel under the windows and lintel above, full length of the block
        for seg in range(len(WIN_X)):
            xa, xb = WIN_X[seg] - WIN_W, WIN_X[seg] + WIN_W
            gone = dmg and f >= 1 and seg in (2, 3)
            if gone and f == 2:
                continue
            if not gone:
                S.bb(f"spand{f}{seg}", xa, xb, YS_, YS_ + T, zb, zb + 0.5, "brick" if (f + seg) % 2 else "brick3", 0.0)
                S.bb(f"lintel{f}{seg}", xa, xb, YS_ - 0.04, YS_ + T, zb + 1.2, zb + FH, "concrete", 0.0)
            else:
                S.bb(f"spand{f}{seg}", xa, xb, YS_, YS_ + T, zb, zb + 0.3, "brick2", 0.0)
        # piers between the windows (top storey ragged: heights vary)
        edges = [PX0] + [v for w in WIN_X for v in (w - WIN_W, w + WIN_W)] + [TOWER[0]]
        for k in range(0, len(edges) - 1, 2):
            xa, xb = edges[k], edges[k + 1]
            if xb - xa < 0.05:
                continue
            h = FH
            if f == 2:
                h = (FH, FH * 0.6, FH * 0.9, FH * 0.45, FH * 0.8, FH * 0.55)[(k // 2) % 6]
            if dmg and f >= 1 and edges[k] > -0.5 and edges[k + 1] < 1.8 and f == 1:
                h = FH * 0.5
            if dmg and f == 2 and xa > -0.5 and xb < 1.8:
                continue
            S.bb(f"pier{f}_{k}", xa, xb, YS_ - 0.04, YS_ + T, zb, zb + h, "brick" if (k // 2 + f) % 2 == 0 else "brick2", 0.0)
        # burnt window interiors (dark) and soot streaks above the openings
        for seg in range(len(WIN_X)):
            gone = dmg and f >= 1 and seg in (2, 3)
            if gone:
                continue
            xa, xb = WIN_X[seg] - WIN_W, WIN_X[seg] + WIN_W
            if f == 2 and seg in (1, 3):
                continue
            S.bb(f"wdark{f}{seg}", xa, xb, YS_ + 0.3, YS_ + T, zb + 0.5, zb + 1.2, "burnt", 0.0, nocast=True)
            if f < 2 and seg % 2 == 0:
                S.bb(f"soot{f}{seg}", xa - 0.05, xb + 0.05, YS_ - 0.06, YS_ - 0.04, zb + FH, zb + FH + 0.5, "burnt", 0.0, nocast=True)
    # ground floor: doorway with a fallen lintel, boarded openings
    S.bb("doorway", -2.7, -2.0, YS_ - 0.02, YS_ + T, Z0, Z0 + 1.1, "burnt", 0.0, nocast=True)
    S.bb("plinth", PX0 - 0.05, TOWER[0], YS_ - 0.1, YS_, Z0, Z0 + 0.35, "concrete2", 0.0)
    # concrete balcony slabs on the 1st and 2nd floors, middle bays, rusty rails
    for f in (1, 2):
        zb = Z0 + f * FH
        for seg in (1, 4):
            xa, xb = WIN_X[seg] - 0.6, WIN_X[seg] + 0.6
            if dmg and f == 2:
                continue
            S.bb(f"balc{f}{seg}", xa, xb, YS_ - 0.6, YS_ - 0.04, zb - 0.05, zb + 0.1, "concrete", 0.0)
            S.bb(f"rail{f}{seg}", xa, xb, YS_ - 0.6, YS_ - 0.55, zb + 0.1, zb + 0.55, "ironrust", 0.0, nocast=True)


def shell(S, dmg, i):
    # side and back walls: ragged tops
    zt = Z0 + STOREYS * FH
    S.bb("wallW", PX0, PX0 + T, YS_, YN, Z0, zt - 0.7, "brick2", 0.0)
    S.bb("wallW2", PX0, PX0 + T, YS_, YS_ + 1.6, zt - 0.7, zt - 0.2, "brick2", 0.0)
    S.bb("wallN", PX0, TOWER[0], YN - T, YN, Z0, zt - 0.3, "brick2", 0.0)
    S.bb("wallN2", -2.0, 0.2, YN - T, YN, zt - 0.3, zt + 0.4, "brick", 0.0)
    S.bb("wallN3", 1.8, 3.0, YN - T, YN, zt - 0.3, zt + 0.1, "brick", 0.0)
    # tower: taller, with slit windows, broken top
    th = zt + 1.1 if not dmg else zt + 0.3
    S.bb("tower", TOWER[0], TOWER[1], YS_ - 0.04, YS_ + 2.2, Z0, th, "brick", 0.04)
    S.bb("towerc", TOWER[0] - 0.1, TOWER[1] + 0.1, YS_ - 0.12, YS_ + 0.1, Z0, Z0 + 0.4, "concrete2", 0.0)
    for f in range(3):
        zb = Z0 + 0.5 + f * FH
        S.bb(f"slit{f}", 3.95, 4.15, YS_ - 0.08, YS_ - 0.03, zb, zb + 0.9, "burnt", 0.0, nocast=True)
        S.bb(f"slitb{f}", 4.25, 4.45, YS_ - 0.08, YS_ - 0.03, zb, zb + 0.9, "burnt", 0.0, nocast=True)
    S.bb("towcap", TOWER[0] - 0.05, TOWER[1] + 0.05, YS_ - 0.1, YS_ + 2.25, th, th + 0.12, "concrete2", 0.0)
    S.bb("towgap", 4.0, 4.7, YS_, YS_ + 0.8, th - 0.15, th + 0.14, "burnt", 0.0, nocast=True)
    # interior: floor slabs showing, burnt joists across the top
    for f in (1, 2, 3):
        zf = Z0 + f * FH
        if f == 3:
            for k in range(6):
                x = PX0 + 1.0 + k * 1.15
                if dmg and 0 < x < 2.0:
                    continue
                S.bb(f"joist{k}", x, x + 0.14, YS_ + T, YN - T, zf - 0.15 - (0.5 if k == 3 else 0.0), zf - 0.02 - (0.5 if k == 3 else 0.0), "burnt", 0.0)
            continue
        sag = 0.5 if (dmg and f == 2) else 0.0
        S.bb(f"floor{f}", PX0 + T, TOWER[0], YS_ + T, YN - T, zf - 0.2 - sag, zf - 0.04 - sag, "concrete2", 0.0)
    # fire escape on the west end: zig-zag stairs and landings
    for f in range(1, 3):
        zb = Z0 + f * FH
        S.bb(f"fland{f}", PX0 - 0.9, PX0, YS_ + 0.2, YS_ + 1.2, zb, zb + 0.06, "ironrust", 0.0, nocast=True)
        S.bb(f"frail{f}", PX0 - 0.9, PX0 - 0.84, YS_ + 0.2, YS_ + 1.2, zb + 0.06, zb + 0.55, "ironrust", 0.0, nocast=True)
        S.box(f"fstair{f}", (0.3, 1.5, 0.06), (PX0 - 0.7, (YS_ + 1.9) * S.YS, zb - 0.55), "ironrust", 0.0, rot=(32, 0, 0), stretch=False, nocast=True)
    S.bb("fpost", PX0 - 0.95, PX0 - 0.88, YS_ + 0.2, YS_ + 0.28, Z0, Z0 + 2 * FH, "ironrust", 0.0, nocast=True)


def claim(S, dmg, i):
    # the one splash of house colour: a cloth strip hung from the 2nd-floor window, a rag on a pole at the tower
    sw = 0.06 * math.sin(i * math.pi / 2)
    zb = Z0 + FH
    S.bb("claim", WIN_X[3] - 0.38 + sw, WIN_X[3] + 0.38 + sw, YS_ - 0.08, YS_ - 0.04, zb + 0.15, zb + 1.35, "house", 0.0, nocast=True) if not dmg else None
    pxx, pyy = 4.2, 0.4
    top = Z0 + STOREYS * FH + (1.1 if not dmg else 0.3)
    S.cyl("pole", 0.05, 1.5, (pxx, pyy, top + 0.75), "steel", verts=6)
    s2 = 0.12 * math.sin(i * math.pi / 2)
    S.box("rag", (0.9 + s2, 0.05, 0.55), (pxx + 0.5 + s2 / 2, (pyy - 0.05) * S.YS, top + 1.15), "house", 0.0,
          rot=(0, 0, 5 * math.sin(i * math.pi / 2)), stretch=False, nocast=True)


def ground(S, dmg, i):
    S.bb("pad", -5.0, 5.0, -3.3, 3.3, 0.0, 0.14, "ruinpad", 0.03)
    S.bb("pad2", -4.9, -2.0, -3.3, -2.6, 0.14, 0.17, "concrete2", 0.0, nocast=True)
    S.bb("crack1", -1.0, 3.0, -2.8, -2.74, 0.14, 0.16, "burnt", 0.0, nocast=True)
    # rubble heap from the fallen top storey (SW of the face) with weeds
    for k, (x, y, w, h, m) in enumerate(((-3.2, -2.6, 0.9, 0.5, "brick"), (-2.5, -2.9, 0.6, 0.35, "brick2"), (-3.8, -2.9, 0.5, 0.3, "concrete"),
                                         (-2.8, -2.3, 0.5, 0.45, "brick3"), (2.9, -2.9, 0.7, 0.4, "brick2"), (3.6, -2.5, 0.5, 0.3, "concrete"),
                                         (-1.4, -3.0, 0.4, 0.25, "brick"), (1.0, -3.0, 0.35, 0.22, "plaster"))):
        S.box(f"rub{k}", (w, w * 0.8, h), (x, y * S.YS, Z0 + h / 2), m, 0.0, rot=(0, 0, 31 * k), stretch=False)
    if dmg:
        for k, (x, y, w, h, m) in enumerate(((0.6, -2.7, 0.9, 0.55, "brick"), (1.4, -2.5, 0.7, 0.5, "brick2"), (0.0, -2.6, 0.6, 0.4, "concrete"),
                                             (1.0, -3.0, 0.6, 0.35, "brick3"), (1.9, -2.9, 0.5, 0.3, "plaster"), (0.4, -3.1, 0.4, 0.25, "brick2"))):
            S.box(f"crub{k}", (w, w * 0.8, h), (x, y * S.YS, Z0 + h / 2), m, 0.0, rot=(0, 0, 41 * k), stretch=False)
        S.box("slabfall", (1.8, 1.0, 0.2), (1.2, -3.0 * S.YS, Z0 + 0.5), "concrete", 0.0, rot=(0, 10, 12), stretch=False)
    for k, (x, y) in enumerate(((-3.0, -2.2), (-4.2, -2.7), (3.3, -2.8), (-1.7, -2.5))):
        S.bb(f"weed{k}", x, x + 0.18, y, y + 0.18, Z0 + 0.35, Z0 + 0.6, "weed" if k % 2 else "weed2", 0.0, nocast=True)
    # crumpled sign board and a dead tree stump
    S.box("sign", (1.0, 0.08, 0.6), (-4.2, -3.0 * S.YS, 0.5), "plaster", 0.0, rot=(0, 0, -12), stretch=False)
    S.cyl("stump", 0.22, 0.9, (4.3, -2.4, Z0 + 0.45), "brick2", verts=8)
    S.bb("stumpb", 4.15, 4.45, -2.5, -2.3, Z0 + 0.9, Z0 + 1.4, "burnt", 0.0)
    # smouldering heap: faint wisp (idle only)
    if not dmg:
        for q in range(3):
            ph = ((i / 4) + q / 3) % 1
            r = 0.1 + 0.28 * math.sin(math.pi * ph) ** 0.7
            S.sphere(f"wisp{q}", r, (-0.2 + ph * 0.7, 1.2, Z0 + 1.2 + ph * 1.3), "smoke2", squash=0.85)


FX = [
    dict(state="damaged", kind="fire_large", x=px(1.2, 0.0, 2.0)[0], y=px(1.2, 0.0, 2.0)[1], phase=0),             # collapsed bay
    dict(state="damaged", kind="fire_small", x=px(1.4, -2.5, 0.5)[0], y=px(1.4, -2.5, 0.5)[1], phase=4),           # rubble heap
    dict(state="damaged", kind="fire_small", x=px(4.2, 0.6, 5.0)[0], y=px(4.2, 0.6, 5.0)[1], phase=2),             # tower top
    dict(state="damaged", kind="smoke_plume", x=px(1.2, 0.0, 2.3)[0], y=px(1.2, 0.0, 2.3)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=px(4.2, 0.6, 5.2)[0], y=px(4.2, 0.6, 5.2)[1], phase=5),
]


def build(S, state, i, n):
    dmg = state == "damaged"
    if dmg:
        S.skin.update(SOOT_SKIN)
    ground(S, dmg, i)
    shell(S, dmg, i)
    south_wall(S, dmg, i)
    claim(S, dmg, i)
