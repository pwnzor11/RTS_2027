"""Left Behind Mother Rig, unpacked (3x3 cells).  Original design.

The caravan's home and workshop, unfolded: a flatbed crawler chassis on mismatched wheels carrying an old teal bus body (rust patch
plates, boarded windows, a hand-painted house panel), a smaller rust-red truck cab bolted on at the west end, a patched canvas
lean-to roof sagging off the east side, a striped awning over the south face, two stovepipes (one smokes), a water tank on the roof,
lashed barrels and crates, an antenna bundle with a dish and a loudspeaker horn, and a tall mast flying the house colour as a canvas
flag.  Lopsided on purpose: the east is high and heavy, the west low.  Dust ochre ground patch, no clean surfaces.
Assumption: FOOTPRINT 3x3 like auth_conyard (the lead may change it with the rules data).
States: idle (4 frames: awning sways, flag flaps, smoke, lamp flicker), damaged (4 frames, structural damage only per D-069:
awning down, lean-to half fallen, snapped stovepipe, torn flag, hole in the bus side; fire/smoke are FX anchors), build (8 frames:
the rig being unfolded and rigged, frame 7 is exactly idle frame 0).
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math


ID = "lb_mother_rig"
TITLE = "Left Behind Mother Rig"
FOOTPRINT = (3, 3)
CANVAS = (192, 248)
ORIGIN = (0, 56)
DOCK = (1, 3)
CAP_MATERIAL = "dust"
HOUSE2_MID = False
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]

import os
import sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C
PALETTE = C.PAL_LB

CX, CY = 96, 152      # pixel of the footprint centre in the frame (ORIGIN + 96)


def anchor(x, y, z):
    """Frame pixel of a point in game units (same maths as the other structures)."""
    return round(CX + x * 18.8), round(CY - y * 18.8 - z * 14.4)


def _fx(state, kind, x, y, z, phase):
    px, py = anchor(x, y, z)
    return dict(state=state, kind=kind, x=px, y=py, phase=phase)


FX = [
    _fx("damaged", "fire_large", 2.6, 0.2, 3.0, 0),     # hole in the lean-to
    _fx("damaged", "fire_small", -1.4, -3.4, 2.2, 3),   # fallen awning
    _fx("damaged", "fire_small", 0.3, -2.6, 2.4, 5),    # bus side breach
    _fx("damaged", "smoke_plume", 2.6, 0.2, 3.3, 0),
    _fx("damaged", "smoke_plume", -1.2, 0.4, 4.7, 6),   # snapped stovepipe stump
]

MATERIALS = {
    "dust": ("#B08B52", dict(rough=1.0)),
    "dust2": ("#8C6C3C", dict(rough=1.0)),
    "canvas": ("#D6C8A2", dict(rough=1.0)),
    "canvas2": ("#B5A57C", dict(rough=1.0)),
    "ochre": ("#B38A44", dict(rough=0.9)),
    "rust": ("#8A3E24", dict(rough=0.95)),
    "rust2": ("#5A2A1A", dict(rough=0.95)),
    "teal": ("#4F746E", dict(rough=0.8)),
    "teal2": ("#34524E", dict(rough=0.8)),
    "ash": ("#7A766F", dict(rough=0.8)),
    "ash2": ("#55524D", dict(rough=0.8)),
    "bone": ("#E6DDC4", dict(rough=0.85)),
    "hazard": ("#DDB93A", dict(rough=0.8)),
    "dark": ("#1A140E", dict(rough=0.9)),
    "rubber": ("#2A2320", dict(rough=0.95)),
    "steel": ("#8E8A82", dict(rough=0.5, metal=0.5)),
    "wood": ("#8A6A3E", dict(rough=0.95)),
    "rope": ("#C8B88A", dict(rough=1.0)),
    "glass": ("#2F4A47", dict(rough=0.25, metal=0.3)),
    "lamp": ("#FFE08A", dict(rough=0.3, emit="#FFE08A")),
    "lamp_off": ("#6B5A2A", dict(rough=0.6)),
    "smoke": ("#B5B0A6", dict(rough=1.0)),
    "smoke2": ("#7A766F", dict(rough=1.0)),
    # soot copies for the damaged state
    "teal_s": ("#2E4440", dict(rough=0.8)),
    "canvas_s": ("#7F7458", dict(rough=1.0)),
    "canvas2_s": ("#645B44", dict(rough=1.0)),
    "rust_s": ("#5C2A1A", dict(rough=0.95)),
    "ochre_s": ("#6E5429", dict(rough=0.9)),
    "bone_s": ("#8F8872", dict(rough=0.85)),
    "ash_s": ("#47443F", dict(rough=0.8)),
    "dust_s": ("#6E5636", dict(rough=1.0)),
    "char": ("#241C14", dict(rough=1.0)),
    "primer": ("#9A8F78", dict(rough=0.9)),
}
SOOT_SKIN = {"teal": "teal_s", "canvas": "canvas_s", "canvas2": "canvas2_s", "rust": "rust_s", "ochre": "ochre_s",
             "bone": "bone_s", "ash": "ash_s", "dust": "dust_s", "hazard": "ochre_s"}

AWN_Y0, AWN_Y1 = -2.6, -4.5       # awning: wall edge, far edge (y north)
AWN_Z0 = 3.2                      # height at the wall
LEAN_ANG = 17.0                   # lean-to slope, degrees (east end down)
LEAN_X0, LEAN_X1 = 0.9, 4.6


def beam(S, name, p0, p1, w, m, nocast=False):
    """Square-section member between two points given in game units (x, y, z); y is stretched here."""
    YS = S.YS
    a = (p0[0], p0[1] * YS, p0[2])
    b = (p1[0], p1[1] * YS, p1[2])
    dx, dy, dz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    ln = math.sqrt(dx * dx + dy * dy + dz * dz)
    yaw = math.degrees(math.atan2(dy, dx))
    pitch = -math.degrees(math.atan2(dz, math.hypot(dx, dy)))
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2)
    return S.box(name, (ln, w, w), mid, m, 0.0, rot=(0, pitch, yaw), stretch=False, nocast=nocast)


def post(S, name, x, y, z0, z1, w, m):
    S.bb(name, x - w / 2, x + w / 2, y - w / 2, y + w / 2, z0, z1, m, 0.0)


def lean_z(x):
    """Height of the lean-to sheet's centre plane at x."""
    return 3.5 - (x - LEAN_X0) * math.tan(math.radians(LEAN_ANG))


def lean_patch(S, name, x0, x1, y0, y1, m, lift=0.06):
    """Patch lying on the lean-to sheet (same slope), x0..x1 along the slope."""
    xm = (x0 + x1) / 2
    ln = (x1 - x0) / math.cos(math.radians(LEAN_ANG))
    S.box(name, (ln, y1 - y0, 0.05), (xm, (y0 + y1) / 2, lean_z(xm) + lift), m, 0.0, rot=(0, LEAN_ANG, 0), nocast=True)


def site(S, lvl):
    """Dust pad, trampled strips, stakes and clutter."""
    S.bb("pad", -5.0, 5.0, -4.9, 4.6, 0.0, 0.1, "dust", 0.03)
    S.bb("padn", -4.2, 3.8, 4.6, 5.0, 0.0, 0.08, "dust", 0.03)
    S.bb("pade", 5.0, 5.1, -3.4, 2.6, 0.0, 0.07, "dust", 0.0)
    S.bb("padw", -5.1, -5.0, -3.0, 3.0, 0.0, 0.07, "dust", 0.0)
    for k, (x0, x1, y0, y1) in enumerate(((-4.6, -1.0, -4.8, -4.3), (1.4, 4.4, -4.7, -4.4), (-4.9, -4.5, -2.0, 2.0), (2.0, 4.8, 3.6, 4.2))):
        S.bb(f"scuff{k}", x0, x1, y0, y1, 0.1, 0.13, "dust2", 0.0, nocast=True)
    if lvl == 0:
        for (sx, sy) in ((-4.7, -3.8), (4.5, -3.8), (-4.7, 3.6), (4.5, 3.6)):
            post(S, "stake", sx, sy, 0.1, 1.0, 0.12, "wood")
        S.bb("line", -4.7, 4.5, -3.84, -3.76, 0.88, 0.92, "rope", 0.0, nocast=True)
        S.bb("line", -4.7, 4.5, 3.56, 3.64, 0.88, 0.92, "rope", 0.0, nocast=True)
        # the chalk board: "MOTHER RIG UP" (planks on two posts)
        post(S, "boardp", -1.0, -4.4, 0.1, 1.3, 0.1, "wood")
        post(S, "boardp", 1.0, -4.4, 0.1, 1.3, 0.1, "wood")
        S.bb("board", -1.3, 1.3, -4.5, -4.35, 0.8, 1.7, "dark", 0.0)
        S.bb("boardc", -1.0, 1.0, -4.52, -4.5, 1.1, 1.5, "bone", 0.0, nocast=True)
        # a heap of folded kit on pallets
        S.bb("pallet", 2.0, 4.0, -2.0, 0.5, 0.1, 0.3, "wood", 0.02)
        S.bb("kit", 2.2, 3.8, -1.8, 0.2, 0.3, 0.95, "canvas2", 0.12)
        S.bb("kitb", 2.6, 3.6, -1.4, -0.4, 0.95, 1.4, "teal", 0.1)
        S.cyl("kitroll", 0.35, 1.8, (-2.8, 0.5, 0.45), "canvas", axis="X", verts=12)
        S.cyl("kitroll2", 0.3, 1.5, (-2.6, -0.2, 0.4), "ochre", axis="X", verts=12)
        for k in range(3):
            S.cyl(f"bar{k}", 0.4, 0.9, (-4.0 + k * 0.9, 2.6, 0.55), "rust" if k != 1 else "teal", verts=12)
    else:
        S.cyl("bar0", 0.4, 0.9, (-4.3, 3.9, 0.55), "rust", verts=12)
        S.cyl("bar1", 0.4, 0.9, (-3.5, 4.1, 0.55), "teal", verts=12)
        S.bb("plankpile", 3.6, 4.8, 3.3, 3.8, 0.1, 0.35, "wood", 0.0)


def wheels(S, dmg):
    # mismatched wheels: south row (seen) and north row (barely), lopsided spacing, the second one is a smaller spare
    for k, (x, r) in enumerate(((-3.7, 0.66), (-2.4, 0.58), (1.5, 0.66), (3.3, 0.7))):
        y = -3.05 * S.YS
        hh = 0.55
        rr = r * (0.8 if (dmg and k == 2) else 1.0)
        S.cyl(f"tyre{k}", rr, hh, (x, y, rr + 0.1), "rubber", axis="Y", verts=18, stretch=False)
        S.cyl(f"hub{k}", rr * 0.5, hh + 0.05, (x, y - 0.02, rr + 0.1), "steel" if k % 2 == 0 else "rust", axis="Y", verts=12, stretch=False)
        S.cyl(f"nut{k}", rr * 0.17, hh + 0.12, (x, y - 0.03, rr + 0.1), "dark", axis="Y", verts=6, stretch=False)
        S.cyl(f"tyren{k}", rr, hh, (x, 3.0 * S.YS, rr + 0.1), "rubber", axis="Y", verts=18, stretch=False)


def chassis(S, dmg):
    S.bb("railS", -4.9, 4.7, -3.5, -3.15, 0.62, 1.25, "ash2", 0.04)
    S.bb("railN", -4.9, 4.7, 3.1, 3.5, 0.62, 1.25, "ash2", 0.04)
    S.bb("deck", -4.9, 4.7, -3.55, 3.55, 1.25, 1.45, "wood", 0.03)
    S.bb("deckplate", -2.0, 0.6, -3.58, -3.3, 1.3, 1.5, "ash", 0.02)
    S.bb("bump", -4.95, -4.75, -3.0, 3.0, 0.7, 1.1, "hazard", 0.02)       # hazard bumper
    S.bb("hitch", 4.7, 5.0, -0.5, 0.5, 0.8, 1.1, "dark", 0.02)
    for k in range(6):                                                    # hazard tags on the south rail
        S.bb(f"tag{k}", -4.3 + k * 1.6, -3.9 + k * 1.6, -3.52, -3.48, 0.7, 1.15, "hazard" if k % 3 == 0 else "ash", 0.0, nocast=True)
    wheels(S, dmg)


def bus(S, dmg, anim):
    # truck cab bolted to the west end (lower, rust red)
    S.bb("cab", -4.8, -3.85, -2.4, 0.9, 1.45, 3.05, "rust", 0.08)
    S.bb("cabroof", -4.9, -3.75, -2.5, 1.0, 3.05, 3.2, "ash2", 0.04)
    S.bb("cabwin", -4.85, -4.5, -2.45, -0.9, 2.2, 2.85, "glass", 0.0)
    S.bb("cabwin2", -4.6, -3.85, -2.43, -2.38, 2.2, 2.85, "glass", 0.0)
    S.bb("cabhood", -5.0, -4.3, -1.8, 0.4, 1.45, 2.0, "rust2", 0.06)
    S.cyl("headlamp", 0.18, 0.1, (-4.95, -1.2, 1.7), "lamp" if (anim % 2 == 0 and not dmg) else "lamp_off", axis="X", verts=10)
    # the bus body (faded teal), taller and further east
    S.bb("bus", -3.8, 0.9, -2.5, 1.6, 1.45, 3.55, "teal", 0.07)
    S.bb("busroof", -3.9, 1.0, -2.6, 1.7, 3.55, 3.7, "ash", 0.04)
    S.bb("busstripe", -3.8, 0.9, -2.55, -2.45, 2.25, 2.4, "bone", 0.0)
    S.bb("busstripe2", -3.8, 0.9, -2.54, -2.46, 1.5, 1.62, "ochre", 0.0)
    # windows, some boarded or sheeted
    wins = ((-3.5, -2.5, "glass"), (-2.2, -1.2, "wood"), (-0.9, 0.2, "glass"), (0.35, 0.8, "canvas2"))
    for k, (x0, x1, m) in enumerate(wins):
        S.bb(f"win{k}", x0, x1, -2.54, -2.5, 2.55, 3.2, m, 0.0)
    for k, x in enumerate((-3.0, -1.7, -0.3)):
        S.bb(f"mullion{k}", x - 0.04, x + 0.04, -2.56, -2.52, 2.5, 3.25, "ash2", 0.0, nocast=True)
    S.bb("door", 0.2, 0.85, -2.54, -2.5, 1.5, 2.45, "ash2", 0.0)
    # patch plates in other metal tones (asymmetric)
    for k, (x0, x1, z0, z1, m) in enumerate(((-3.7, -2.8, 1.65, 2.15, "rust"), (-1.6, -0.8, 1.7, 2.2, "ash"), (-2.6, -2.0, 2.45, 2.52, "rust2"),
                                              (0.0, 0.35, 2.6, 3.3, "rust"))):
        S.bb(f"patch{k}", x0, x1, -2.57, -2.5, z0, z1, m, 0.0)
        for rv in (x0 + 0.06, x1 - 0.06):
            S.bb(f"rivet{k}", rv - 0.03, rv + 0.03, -2.6, -2.57, z0 + 0.08, z0 + 0.14, "dark", 0.0, nocast=True)
    # big hand-painted house panel on the bus side (a white-ringed canvas sheet nailed up)
    S.bb("panelbed", -2.3, -0.2, -2.6, -2.55, 1.62, 2.22, "canvas", 0.0)
    S.bb("panel", -2.15, -0.35, -2.64, -2.6, 1.68, 2.16, "house", 0.0)
    # west end of the bus roof: water tank and rack
    S.cyl("tank", 0.55, 2.0, (-2.5, 0.0, 4.35), "ochre", axis="X", verts=16)
    for k, x in enumerate((-3.2, -1.8)):
        S.cyl(f"tankband{k}", 0.58, 0.12, (x, 0.0, 4.35), "rust", axis="X", verts=16)
    S.bb("tanksaddle", -3.4, -1.6, -0.5, 0.5, 3.7, 3.85, "ash2", 0.0)
    for k, x in enumerate((-3.9, -1.0)):
        S.bb(f"railp{k}", x, x + 0.1, -2.4, 1.5, 3.7, 3.95, "ash2", 0.0)
    # roof sacks / canvas bundle
    S.sphere("bundle1", 0.7, (-0.3, 0.6, 4.1), "canvas2", squash=0.5, nocast=False)
    S.sphere("bundle2", 0.5, (0.35, -0.5, 3.95), "teal", squash=0.55, nocast=False)


def stovepipes(S, dmg, anim, lvl):
    # pipe 1 (smokes) rises from the bus roof; pipe 2 is shorter, bent
    px, py = -1.2, 0.4
    top = 4.7 if dmg else 5.9
    S.cyl("pipe1", 0.2, top - 3.7, (px, py, (top + 3.7) / 2), "ash", verts=12)
    S.cyl("pipe1b", 0.24, 0.14, (px, py, 4.3), "rust", verts=12)
    S.cyl("pipe1c", 0.24, 0.14, (px, py, 5.0 if not dmg else 4.5), "rust", verts=12)
    if dmg:
        S.cyl("pipe1stump", 0.2, 0.12, (px, py, top + 0.04), "dark", verts=10)
        beam(S, "pipe1fall", (px + 0.2, py, top), (px + 1.4, py - 0.5, 3.75), 0.34, "ash")
    else:
        S.cyl("pipe1cap", 0.34, 0.12, (px, py, top + 0.06), "rust2", verts=12)
        S.cyl("pipe1cap2", 0.12, 0.2, (px, py, top + 0.2), "ash", verts=8)
        for q in range(3):
            ph = ((anim / 4) + q / 3) % 1
            r = 0.14 + 0.36 * math.sin(math.pi * ph) ** 0.7
            S.sphere(f"puff{q}", r, (px + ph * 0.9, py, top + 0.5 + ph * 0.8), "smoke2" if q != 1 else "smoke", squash=0.85)
    qx, qy = 0.25, -0.7
    S.cyl("pipe2", 0.15, 1.35, (qx, qy, 4.4), "rust", verts=10)
    beam(S, "pipe2b", (qx, qy, 5.0), (qx + 0.7, qy, 5.5), 0.26, "rust" if not dmg else "ash")
    S.cyl("pipe2band", 0.19, 0.1, (qx, qy, 4.0), "ash2", verts=10)


def awning(S, dmg, anim):
    """Striped canvas awning over the south face; sways (idle), drops (damaged)."""
    YS = S.YS
    rise = 0.9
    if dmg:
        rise = 1.9
    else:
        rise += 0.12 * math.sin(anim * math.pi / 2)
    run = (AWN_Y0 - AWN_Y1)
    ang = math.atan2(rise, run * YS)
    ln = math.hypot(run * YS, rise) / YS
    ym = (AWN_Y0 + AWN_Y1) / 2
    zm = AWN_Z0 - rise / 2
    zm_off = zm
    deg = math.degrees(ang)
    xs = (-3.7, -2.45, -1.2, 0.05, 1.3)
    for k, x0 in enumerate(xs):
        m = ("canvas", "teal", "canvas", "rust", "canvas")[k] if not dmg else ("canvas_s", "teal_s", "canvas_s", "rust_s", "canvas_s")[k]
        w = 1.25 if k < 4 else 0.6
        if dmg and k == 3:
            continue          # a strip is gone
        S.box(f"awn{k}", (w, ln, 0.07), (x0 + w / 2, ym, zm_off), m, 0.0, rot=(deg, 0, 0))
    # fringe along the far edge, hanging
    zf = AWN_Z0 - rise
    for k in range(10):
        x = -3.6 + k * 0.52
        if dmg and k in (3, 4, 7):
            continue
        sway = 0.04 * math.sin((anim + k) * 1.7)
        hz = 0.32 if not dmg else 0.15
        S.bb(f"fringe{k}", x, x + 0.34, AWN_Y1 - 0.06 + sway, AWN_Y1 + 0.0 + sway, zf - hz, zf, "house" if k in (2, 7) else ("canvas2" if k % 2 else "rust"), 0.0, nocast=True)
    # poles
    for px_ in (-3.55, 1.55):
        if dmg and px_ > 0:
            beam(S, "awnpole", (px_, AWN_Y1 + 0.1, 0.1), (px_ - 0.4, AWN_Y1 + 1.3, 1.6), 0.1, "wood")
        else:
            post(S, "awnpole", px_, AWN_Y1 + 0.1, 0.1, zf + 0.05, 0.1, "wood")
    beam(S, "awnrope", (-3.55, AWN_Y1 + 0.1, zf), (-3.4, AWN_Y0, AWN_Z0), 0.04, "rope", nocast=True)


def lean_to(S, dmg, lvl):
    """Patched canvas roof sagging off the bus's east side onto the deck's posts."""
    ang = LEAN_ANG
    xm = (LEAN_X0 + LEAN_X1) / 2
    ln = (LEAN_X1 - LEAN_X0) / math.cos(math.radians(ang))
    y0, y1 = -3.3, 2.9
    if dmg:
        # west half still up, east half has collapsed to a steeper slope with a hole between
        S.box("lean", (ln * 0.55, y1 - y0, 0.08), (LEAN_X0 + (LEAN_X1 - LEAN_X0) * 0.27, (y0 + y1) / 2, lean_z(LEAN_X0 + (LEAN_X1 - LEAN_X0) * 0.27)),
              "canvas", 0.0, rot=(0, ang, 0))
        S.box("leanfall", (ln * 0.45, y1 - y0 - 1.4, 0.08), (LEAN_X0 + (LEAN_X1 - LEAN_X0) * 0.77, (y0 + y1) / 2 - 0.6, 2.25), "canvas2", 0.0, rot=(8, 40, 0))
        S.bb("hole", 2.0, 3.0, -1.2, 0.4, lean_z(2.5) + 0.0, lean_z(2.5) + 0.07, "char", 0.0, nocast=True)
        post(S, "lpost", 4.4, -3.0, 1.45, 1.6, 0.14, "wood")
        beam(S, "lpostB", (4.4, 2.6, 1.45), (4.1, 2.6, 2.55), 0.14, "wood")
    else:
        S.box("lean", (ln, y1 - y0, 0.08), (xm, (y0 + y1) / 2, lean_z(xm)), "canvas", 0.0, rot=(0, ang, 0))
        for px_, py_ in ((4.4, -3.0), (4.4, 2.6)):
            post(S, "lpost", px_, py_, 1.45, lean_z(4.45) - 0.03, 0.14, "wood")
        beam(S, "lbrace", (4.4, -3.0, 1.9), (3.4, -3.0, 2.7), 0.08, "wood")
        beam(S, "lbrace2", (4.4, 2.6, 1.9), (3.4, 2.6, 2.7), 0.08, "wood")
    # patchwork on the sheet, different tones and sizes
    lean_patch(S, "lp0", 1.3, 2.4, -2.9, -1.4, "teal")
    lean_patch(S, "lp1", 2.8, 3.8, -0.4, 1.2, "rust" if not dmg else "char")
    lean_patch(S, "lp2", 1.6, 2.2, 0.8, 2.4, "ochre")
    lean_patch(S, "lp3", 3.5, 4.4, -3.0, -2.0, "canvas2")
    if not dmg:
        lean_patch(S, "lphouse", 2.2, 3.5, -2.2, -1.2, "house", 0.07)       # house colour patch on the roof, facing the camera
    # lashing lines over the roof
    for k, y in enumerate((-2.6, -0.8, 1.2)):
        S.box(f"lash{k}", (3.7 / math.cos(math.radians(ang)), 0.07, 0.05), (xm, y, lean_z(xm) + 0.07), "rope", 0.0, rot=(0, ang, 0), nocast=True)
    # clutter under and on the lean-to: crates, tubs
    S.bb("crateA", 1.2, 2.3, 0.4, 1.5, 1.45, 2.3, "wood", 0.03)
    S.bb("crateB", 2.35, 3.2, 0.7, 1.5, 1.45, 2.0, "ochre", 0.03)


def deck_clutter(S, dmg):
    # barrels lashed against the bus's south-east corner, on the deck
    for k, (x, y, z, m) in enumerate(((1.4, -3.05, 1.45, "rust"), (2.3, -3.1, 1.45, "teal"), (3.2, -3.0, 1.45, "rust"), (1.85, -3.08, 2.35, "ochre"))):
        if dmg and k == 3:
            continue
        S.cyl(f"drum{k}", 0.42, 0.9, (x, y, z + 0.45), m, verts=12)
        S.cyl(f"drumband{k}", 0.44, 0.1, (x, y, z + 0.5), "hazard" if k == 2 else "ash2", verts=12)
    S.bb("crateS", 3.7, 4.6, -3.2, -2.4, 1.45, 2.2, "wood", 0.03)
    S.bb("crateSb", 3.9, 4.5, -3.0, -2.5, 2.2, 2.55, "canvas2", 0.1)
    # ladder up the bus's west end
    for lx in (-4.0, -3.7):
        S.bb("lad", lx, lx + 0.06, -2.55, -2.5, 1.45, 3.7, "wood", 0.0)
    for k in range(7):
        S.bb(f"rung{k}", -4.0, -3.7, -2.58, -2.54, 1.7 + k * 0.3, 1.76 + k * 0.3, "wood", 0.0, nocast=True)
    # jerrycans and a coil on the north side of the deck
    S.bb("jerry", -3.0, -2.5, 2.4, 2.9, 1.45, 2.0, "rust2", 0.04)
    S.bb("jerry2", -2.4, -1.9, 2.4, 2.9, 1.45, 2.0, "hazard", 0.04)


def antennas(S, dmg, anim, lvl):
    """Bundle of whips, a lattice mast, a dish and a loudspeaker horn on the bus roof's east end."""
    bx, by = 0.55, 0.9
    S.bb("antbase", bx - 0.4, bx + 0.4, by - 0.4, by + 0.4, 3.7, 3.85, "ash2", 0.02)
    for k, (dx, dy, h, lean) in enumerate(((-0.25, -0.1, 3.0, -4), (0.0, 0.2, 3.6, 3), (0.25, -0.15, 2.3, 6), (-0.1, 0.25, 2.8, -7), (0.3, 0.2, 3.3, 2))):
        if dmg and k in (1, 3):
            h *= 0.4
        S.box(f"whip{k}", (0.05, 0.05, h), (bx + dx, (by + dy) * S.YS, 3.85 + h / 2), "dark", 0.0, rot=(0, lean, 0), stretch=False)
        S.sphere(f"whiptip{k}", 0.07, (bx + dx + math.tan(math.radians(lean)) * h / 2, by + dy, 3.85 + h), "hazard" if k == 0 else "rust")
    # lattice mast
    mx, my = -0.2, 1.2
    for dx_, dy_ in ((-0.12, -0.12), (0.12, -0.12), (-0.12, 0.12), (0.12, 0.12)):
        S.cyl("mast", 0.03, 2.8 if not dmg else 1.6, (mx + dx_, my + dy_, 3.7 + (1.4 if not dmg else 0.8)), "steel", verts=6)
    for k in range(6 if not dmg else 3):
        S.bb(f"mastx{k}", mx - 0.14, mx + 0.14, my - 0.14, my + 0.14, 3.9 + k * 0.4, 3.94 + k * 0.4, "steel", 0.0, nocast=True)
    # dish
    if not dmg:
        S.cyl("dish", 0.55, 0.07, (mx + 0.1, my - 0.15, 6.35), "bone", verts=16, rot_z=0)
    else:
        S.cyl("dish", 0.5, 0.07, (mx + 0.9, my - 0.4, 3.85), "bone", verts=14)
    # guy wires down to the lean-to
    beam(S, "guy", (0.55, 0.9, 5.5 if not dmg else 4.5), (3.3, 1.8, lean_z(3.3)), 0.03, "dark", nocast=True)
    # loudspeaker horn (the out-of-tune accordion lives behind it)
    S.cone("horn", 0.1, 0.5, 0.8, (-3.3, -1.3 * S.YS, 4.1), "bone", verts=12, nocast=False, rot=(70, 0, 0))
    S.cyl("hornring", 0.5, 0.05, (-3.3, -1.7, 4.25), "rust", verts=12, rot_z=0)


def mast_flag(S, dmg, anim):
    """Tall mast at the west end flying the house colour as a canvas flag (flaps)."""
    mx, my = -4.3, -2.1
    top = 7.2 if not dmg else 5.4
    post(S, "mast", mx, my, 1.45, top, 0.12, "wood")
    S.sphere("mastknob", 0.12, (mx, my, top + 0.1), "hazard")
    beam(S, "mastbrace", (mx, my + 0.6, 1.45), (mx, my, 3.4), 0.07, "wood")
    ph = anim * math.pi / 2
    if not dmg:
        fw = 1.5 + 0.2 * math.sin(ph)
        fh = 1.05
        fz = top - 0.75
        S.box("flag", (fw, 0.05, fh), (mx + 0.06 + fw / 2, (my - 0.08 + 0.05 * math.cos(ph)) * S.YS, fz), "house", 0.0, rot=(0, 0, 5 * math.sin(ph)), stretch=False)
        S.box("flagtrim", (fw, 0.06, 0.12), (mx + 0.06 + fw / 2, (my - 0.1) * S.YS, fz - fh / 2 + 0.03), "bone", 0.0, rot=(0, 0, 5 * math.sin(ph)), stretch=False, nocast=True)
        S.box("flagtrim2", (fw, 0.06, 0.1), (mx + 0.06 + fw / 2, (my - 0.1) * S.YS, fz + fh / 2 - 0.03), "rust", 0.0, rot=(0, 0, 5 * math.sin(ph)), stretch=False, nocast=True)
    else:
        S.box("flag", (0.9, 0.05, 1.0), (mx + 0.1, (my - 0.1) * S.YS, top - 0.8), "house", 0.0, stretch=False)    # torn, hangs straight
        S.box("flagt", (0.4, 0.05, 0.5), (mx + 0.35, (my - 0.1) * S.YS, top - 1.1), "canvas_s", 0.0, stretch=False, nocast=True)
    # string of lamps from the mast to the awning post, flicker
    for k in range(4):
        t = (k + 1) / 5
        lit = (anim + k) % 2 == 0 and not dmg
        S.sphere(f"lamp{k}", 0.09, (mx + t * 0.75, my - t * 1.9, 3.1 - 0.18 * math.sin(t * math.pi) - t * 0.2), "lamp" if lit else "lamp_off", nocast=True)


def damage_marks(S):
    S.bb("scorchbus", -1.4, 0.0, -2.6, -2.58, 1.8, 3.2, "char", 0.0, nocast=True)
    S.bb("holebus", -0.6, 0.1, -2.56, -2.5, 2.3, 3.0, "char", 0.0, nocast=True)
    S.bb("scorchbus2", -1.8, -1.2, -2.6, -2.58, 3.1, 3.6, "char", 0.0, nocast=True)
    S.box("fallenpanel", (1.1, 0.8, 0.1), (-0.9, -3.3 * S.YS, 1.5), "teal", 0.0, rot=(0, 0, 25), stretch=False)
    S.box("fallenpanel2", (0.8, 0.5, 0.08), (1.0, -4.0 * S.YS, 0.25), "rust", 0.0, rot=(10, 0, -30), stretch=False)
    S.bb("rubble", 1.0, 2.0, -4.2, -3.6, 0.1, 0.3, "ash2", 0.04)
    S.bb("rubble2", -3.2, -2.4, -4.0, -3.5, 0.1, 0.25, "wood", 0.0)


def build(S, state, i, n):
    building = state == "build"
    dmg = state == "damaged"
    lvl = i if building else 7
    anim = 0 if building else i
    if dmg:
        S.skin.update(SOOT_SKIN)
    if building and lvl < 7:
        pass
    site(S, 0 if (building and lvl == 0) else 1)
    if building and lvl == 0:
        return
    chassis(S, dmg)
    if lvl >= 2:
        bus(S, dmg, anim)
    if lvl >= 3:
        stovepipes(S, dmg, anim, lvl)
        deck_clutter(S, dmg)
    if lvl >= 4:
        lean_to(S, dmg, lvl)
        awning(S, dmg, anim)
    if lvl >= 5:
        antennas(S, dmg, anim, lvl)
    if lvl >= 6:
        mast_flag(S, dmg, anim)
    if dmg:
        damage_marks(S)
