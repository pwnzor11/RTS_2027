"""Authority Wall: a reinforced concrete wall block (1x1 cell) with a steel cap rail and a dragon-tooth row at its foot.
Original design, no franchise references. Contract: see auth_refinery.py.

Tiling: the block, its cap rail, the house band and the trim lines all run to x = +-1.7 (the cell edges) with no bevel
and no outline at the left/right canvas edges, so a row of walls reads as one continuous wall. The dragon teeth are
spaced on a 0.85 pitch (centres at +-0.425, +-1.275), so the pattern is continuous across cells too. One variant (M2).
The canvas is exactly 64 px wide for that reason: the shadow (falls right/down) is cropped at the cell edge.
"""
import importlib.util
import math
import os

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structures_auth_refinery_ref", os.path.join(_here, "auth_refinery.py"))
_ref = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_ref)

ID = "auth_wall"
TITLE = "Authority Wall"
FOOTPRINT = (1, 1)
CANVAS = (64, 100)
ORIGIN = (0, 32)        # footprint top-left corner inside the frame (32 px headroom above the cell, 4 px below it)
DOCK = (0, 1)           # unused (no dock); required by the contract
CAP_MATERIAL = "concrete2"
STATES = [("idle", 1, 1), ("damaged", 1, 1), ("build", 4, 5)]
# Effect anchors (D-069), pixels in frame space. The wall has none: it is damaged by chipping and cracking only.
FX = []

MATERIALS = dict(_ref.MATERIALS)
MATERIALS.update({
    "steel2": ("#979EA3", dict(rough=0.4, metal=0.5)),
})
SOOT_SKIN = dict(_ref.SOOT_SKIN)
PRIMER_SKIN = dict(_ref.PRIMER_SKIN)

HX = 1.7                 # half cell
WT = 0.5                 # half thickness of the wall (y)
WZ = 2.0                 # top of the block
beam, post = _ref.beam, _ref.post


def teeth(S, which=(0, 1, 2, 3), snapped=(), mat="concrete2"):
    for k in which:
        x = -1.275 + 0.85 * k
        if k in snapped:     # snapped tooth: a stub and a toppled piece
            S.cone(f"tooth{k}", 0.4, 0.3, 0.3, (x, -1.25, 0.15), mat, verts=4, rot=(0, 0, 45), nocast=False)
            S.cone(f"toothf{k}", 0.34, 0.1, 0.55, (x + 0.2, -1.45, 0.17), mat, verts=4, rot=(0, 78, 25), nocast=False)
            continue
        S.cone(f"tooth{k}", 0.46, 0.06, 0.78, (x, -1.25, 0.39), mat, verts=4, rot=(0, 0, 45), nocast=False)


def block_pieces(S, dmg):
    """The block as (x0, x1, top) columns: the damaged wall has a stepped gap but full-height ends."""
    if not dmg:
        return [(-HX, HX, WZ)]
    return [(-HX, -0.1, WZ), (-0.1, 0.35, 1.45), (0.35, 0.95, 1.05), (0.95, 1.25, 1.7), (1.25, HX, WZ)]


def finished(S, dmg=False):
    pcs = block_pieces(S, dmg)
    S.bb("plinth", -HX, HX, -WT - 0.2, WT + 0.15, 0.0, 0.42, "concrete2", 0.0)
    for k, (x0, x1, top) in enumerate(pcs):
        S.bb(f"block{k}", x0, x1, -WT, WT, 0.0, top, "concrete", 0.0)
    # cap rail: steel slab with a raised ridge, continuous over the full cell width (broken in the damaged wall)
    capx = [(-HX, -0.1)] if dmg else [(-HX, HX)]
    for k, (x0, x1) in enumerate(capx):
        S.bb(f"cap{k}", x0, x1, -WT - 0.14, WT + 0.14, WZ, WZ + 0.16, "steel", 0.0)
        S.bb(f"rail{k}", x0, x1, -0.14, 0.14, WZ + 0.16, WZ + 0.3, "steel2", 0.0)
        S.bb(f"capl{k}", x0, x1, -WT - 0.15, -WT - 0.13, WZ + 0.02, WZ + 0.14, "dark", 0.0, nocast=True)
    if dmg:
        S.bb("cap9", 1.25, HX, -WT - 0.14, WT + 0.14, WZ, WZ + 0.16, "steel", 0.0)
        S.bb("rail9", 1.25, HX, -0.14, 0.14, WZ + 0.16, WZ + 0.3, "steel2", 0.0)
    # house band on the south face + dark trim, edge to edge
    ys = -WT - 0.02
    S.bb("trimA", -HX, HX, ys - 0.03, ys, 0.88, 0.97, "dark", 0.0, nocast=True)
    S.bb("trimB", -HX, HX, ys - 0.03, ys, 1.55, 1.64, "dark", 0.0, nocast=True)
    if not dmg:
        S.bb("band", -HX, HX, ys - 0.05, ys, 0.97, 1.55, "house", 0.0, nocast=True)
    else:
        for k, (x0, x1) in enumerate(((-HX, -0.1), (1.25, HX), (0.95, 1.25))):
            S.bb(f"band{k}", x0, x1, ys - 0.05, ys, 0.97, 1.55, "house", 0.0, nocast=True)
    # panel joints (vertical grooves) and a small hazard chevron block at the foot, away from the cell edges
    for gx in (-0.85, 0.85):
        S.bb("groove", gx - 0.03, gx + 0.03, ys - 0.03, ys, 0.45, 0.88, "concrete2", 0.0, nocast=True)
        S.bb("groove", gx - 0.03, gx + 0.03, ys - 0.03, ys, 1.64, WZ - 0.02, "concrete2", 0.0, nocast=True)
    for k in range(8):
        S.bb(f"haz{k}", -HX + 0.425 * k, -HX + 0.425 * (k + 1), -WT - 0.2 - 0.03, -WT - 0.2, 0.28, 0.42, "yellow" if k % 2 == 0 else "dark", 0.0, nocast=True)
    if not dmg:
        teeth(S)
    else:
        teeth(S, snapped=(2,))
        # chipped block: dark bite, cracks, bent rebar, rubble in front
        S.bb("bite", -0.1, 0.35, -WT - 0.01, -WT + 0.04, 1.2, 1.45, "dark", 0.0, nocast=True)
        for k, (a, b) in enumerate((((-0.7, 0.5, 0), (-0.45, 1.25, 0)), ((-0.45, 1.25, 0), (-0.7, 1.75, 0)), ((1.45, 0.4, 0), (1.3, 1.4, 0)),
                                    ((0.2, 0.45, 0), (0.55, 0.9, 0)))):
            beam(S, f"crack{k}", (a[0], ys - 0.03, a[1]), (b[0], ys - 0.03, b[1]), 0.05, "dark", nocast=True)
        for k, (a, b) in enumerate((((0.0, 0.0, 1.45), (-0.25, -0.1, 2.35)), ((0.2, 0.05, 1.45), (0.5, 0.2, 2.2)),
                                    ((0.6, 0.0, 1.05), (0.95, -0.05, 1.9)), ((0.8, 0.1, 1.05), (1.1, 0.2, 1.8)))):
            beam(S, f"rbar{k}", a, b, 0.05, "rebar")
        S.box("capfall", (1.0, 0.9, 0.14), (0.7, -0.5, 1.3), "steel", 0.0, rot=(0, 28, 10), stretch=False, nocast=False)
        S.box("railfall", (1.0, 0.28, 0.14), (0.62, -0.5, 1.45), "steel2", 0.0, rot=(0, 28, 10), stretch=False, nocast=False)
        for k, (x, y, w, h) in enumerate(((0.0, -0.85, 0.34, 0.26), (0.5, -0.8, 0.4, 0.34), (0.95, -0.9, 0.3, 0.22), (-0.45, -0.85, 0.26, 0.18),
                                          (1.1, -1.35, 0.3, 0.2))):
            S.box(f"rub{k}", (w, w * 0.8, h), (x, y, h / 2 + 0.0), "concrete_s", 0.0, rot=(0, 0, 33 * k))
        S.bb("scorch", 0.4, 1.4, -WT - 0.06, -WT - 0.03, 0.3, 0.9, "char", 0.0, nocast=True)
        S.bb("scorch", -1.0, -0.4, -WT - 0.06, -WT - 0.03, 1.7, 1.95, "char", 0.0, nocast=True)


def formwork(S, z1, both=True):
    """Timber shuttering boards on the south (and north) faces with walers and diagonal props."""
    for sy in ((-1, 1) if both else (-1,)):
        y = sy * (WT + 0.1)
        S.bb("fb", -HX, HX, y - 0.04, y + 0.04, 0.1, z1, "timber", 0.0)
        for zz in (0.5, 0.95, 1.4):
            if zz < z1:
                S.bb("waler", -HX, HX, (y - 0.12 if sy < 0 else y + 0.04), (y - 0.04 if sy < 0 else y + 0.12), zz, zz + 0.08, "girder", 0.0)
    for k in range(4):
        x = -1.275 + 0.85 * k
        beam(S, "prop", (x, -1.5, 0.12), (x, -WT - 0.12, min(z1, 1.5)), 0.07, "timber")


def build_site(S, i):
    if i == 0:     # trench dug, spoil heap, stakes and string lines
        S.bb("pit", -HX, HX, -0.75, 0.75, 0.0, 0.05, "soil", 0.0)
        S.bb("pit2", -HX, HX, -0.75, -0.45, 0.0, 0.1, "soil2", 0.0)
        S.bb("pit2", -HX, HX, 0.45, 0.75, 0.0, 0.1, "soil2", 0.0)
        S.bb("mound", -HX, 0.4, 0.85, 1.4, 0.0, 0.5, "soil", 0.18)
        for sx in (-1.6, 1.6):
            for sy in (-1.1, 1.1):
                post(S, "stake", sx, sy, 0.0, 0.8, 0.1, "timber")
                post(S, "staket", sx, sy, 0.62, 0.8, 0.12, "string")
        S.bb("line", -1.6, 1.6, -1.12, -1.08, 0.7, 0.72, "string", 0.0, nocast=True)
        S.bb("line", -1.6, 1.6, 1.08, 1.12, 0.7, 0.72, "string", 0.0, nocast=True)
        S.bb("sign", 0.2, 1.0, -1.45, -1.4, 0.5, 1.1, "yellow", 0.0)
        post(S, "signp", 0.35, -1.42, 0.0, 0.5, 0.07, "timber")
        post(S, "signp", 0.85, -1.42, 0.0, 0.5, 0.07, "timber")
    elif i == 1:   # footing poured, rebar cage standing, first shuttering
        S.bb("footing", -HX, HX, -0.85, 0.85, 0.0, 0.3, "raw", 0.0)
        for k in range(8):
            x = -1.55 + k * 0.44
            for sy in (-0.28, 0.28):
                post(S, "rb", x, sy, 0.3, 1.65, 0.05, "rebar")
        for zz in (0.6, 1.0, 1.4):
            for sy in (-0.28, 0.28):
                S.bb("rbh", -1.58, 1.58, sy - 0.025, sy + 0.025, zz, zz + 0.05, "rebar", 0.0, nocast=True)
        formwork(S, 0.6, both=False)
        S.bb("planks", -1.2, 0.2, 1.0, 1.35, 0.0, 0.3, "timber", 0.0)
        S.bb("crate", 0.9, 1.5, 0.95, 1.5, 0.0, 0.55, "crate", 0.03)
    elif i == 2:   # wall poured in raw concrete, shuttering still on the south face, caps and teeth waiting on the ground
        S.bb("plinth", -HX, HX, -WT - 0.2, WT + 0.15, 0.0, 0.42, "raw", 0.0)
        S.bb("block", -HX, HX, -WT, WT, 0.0, WZ - 0.1, "raw", 0.0)
        formwork(S, 1.1, both=False)
        for k in range(4):
            x = -1.275 + 0.85 * k
            post(S, "rb", x, 0.0, WZ - 0.1, WZ + 0.45, 0.05, "rebar")
        S.bb("capwait", -1.3, 1.0, 0.8, 1.2, 0.0, 0.16, "steel", 0.0)
        S.bb("capwait", -1.3, 1.0, 0.8, 1.2, 0.16, 0.3, "steel2", 0.0)
        for k in range(2):
            S.cone(f"toothw{k}", 0.46, 0.06, 0.78, (-1.0 + k * 0.9, -1.35, 0.39), "raw", verts=4, rot=(0, 0, 45), nocast=False)
        S.bb("crate", 1.1, 1.65, -1.55, -1.0, 0.0, 0.5, "crate", 0.03)


def build(S, state, i, n):
    building = state == "build"
    if building and i < 3:
        build_site(S, i)
        return
    if building and i == 3:
        pass                         # the finished wall, exactly idle frame 0
    dmg = state == "damaged"
    if dmg:
        S.skin.update(SOOT_SKIN)
    finished(S, dmg)
