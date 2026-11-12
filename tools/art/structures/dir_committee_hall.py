"""Directorate Committee Chamber (dir_committee_hall): tier-3 seat of government and research hall (2x3 cells).  Original design.

The read is a TALL WINDOWLESS CHAMBER BLOCK behind a MONUMENTAL PORTAL: a stair-mouth ramp under a wide cantilevered hood (magenta
band, blast doors folded back) flanked by two squat pylons with house banners; behind it the chamber rises with a long clerestory of
amber lamp slits (the late-night sessions), a heavy flat roof slab with a parapet, a ribbed roof lantern with a lit ring, a research
dish and a loudspeaker pair on the roof, and one banded stack.  Bone / oxide hazard bar on the pad edge.
States: idle (4: clerestory slits flicker, ash puffs), damaged (4, STRUCTURAL only: hood end fallen, lantern slumped, stack snapped,
a pylon cracked), build (8, same recipe as the other Directorate buildings), active (4: clerestory fully lit, lantern ring pulses,
dish turns; played while research runs).  Fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint x in [-3.4, 3.4], y in [-5.1, 5.1].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L
ID = "dir_committee_hall"
TITLE = "Directorate Committee Chamber"
FOOTPRINT = (2, 3)
CANVAS = (128, 280)
ORIGIN = (0, 72)
DOCK = (1, 3)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5), ("active", 4, 3)]
MATERIALS = D.MATERIALS
SOOT_SKIN = D.SOOT_SKIN
PRIMER_SKIN = D.PRIMER_SKIN

PAD = (-3.3, 3.3, -5.0, 5.0)
TR = (-0.9, 0.9)
TR_YTOP, TR_BOT = -4.9, -2.4
YD = -3.2                      # mouth plane
BLOCK = (-3.0, 3.0, YD, 4.8)
BLK_H = 3.0
ZR = BLK_H + 0.25
STK = (2.3, 3.9)
STK_TOP = 5.0
LANT = (0.0, 2.2)


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def lantern(S, i, dmg, mode):
    cx, cy = LANT
    z0 = ZR + 0.12
    S.bb("lbase", cx - 1.3, cx + 1.3, cy - 1.0, cy + 1.0, ZR, z0, "concrete2", 0.03)
    if dmg:
        S.box("ldrum", (1.9, 1.5, 0.7), (cx + 0.2, cy * S.YS, z0 + 0.3), "concrete_s", 0.05, rot=(0, 10, 15), stretch=False)
        S.box("lcap", (1.2, 1.0, 0.2), (cx + 1.4, (cy - 0.6) * S.YS, 0.35 + ZR), "roof_s", 0.0, rot=(0, 0, -30), stretch=False)
        return
    S.cyl("ldrum", 1.0, 0.9, (cx, cy, z0 + 0.45), "concrete", verts=16)
    pulse = mode == "active" and i % 2 == 0
    ring = "redglow" if (mode == "active" or (mode == "idle" and i % 2 == 0)) else "oxide2"
    if mode == "build":
        ring = "oxide2"
    S.cyl("lring", 1.04, 0.2, (cx, cy, z0 + 0.5), ring if not pulse else "lamp", verts=16, nocast=True)
    for k in range(8):
        a = k * math.pi / 4
        S.bb("lrib", cx + 0.98 * math.cos(a) - 0.06, cx + 0.98 * math.cos(a) + 0.06 + 0.0, cy + 0.98 * math.sin(a) - 0.06,
             cy + 0.98 * math.sin(a) + 0.06, z0, z0 + 0.9, "concrete2", 0.0)
    S.cyl("lcap", 1.15, 0.2, (cx, cy, z0 + 1.0), "roof", verts=16)
    S.cyl("lcapb", 1.18, 0.1, (cx, cy, z0 + 1.0), "house", verts=16)
    S.cone("lcone", 0.9, 0.2, 0.45, (cx, cy, z0 + 1.3), "hull2", verts=16, nocast=False)


def dish(S, i, dmg, mode):
    px_, py_ = -1.8, 3.4
    S.cyl("dpost", 0.1, 1.0, (px_, py_, ZR + 0.5), "steel", verts=8)
    if dmg:
        S.box("ddish", (1.0, 0.1, 1.0), (px_ + 0.4, (py_ - 0.5) * S.YS, ZR + 0.2), "steel", 0.0, rot=(0, 60, 25), stretch=False)
        return
    a = (i * 40.0 if mode == "active" else 20.0) * math.pi / 180
    S.cyl("dhub", 0.25, 0.15, (px_, py_, ZR + 1.05), "hull2", verts=10)
    S.box("ddish", (1.5, 0.12, 1.1), (px_ + 0.25 * math.sin(a), (py_ - 0.35) * S.YS, ZR + 1.5), "steel", 0.0, rot=(-20, 0, 30 * math.sin(a)), stretch=False)
    S.bb("dfeed", px_ - 0.06, px_ + 0.06, py_ - 0.5, py_ - 0.3, ZR + 1.5, ZR + 1.62, "oxide", 0.0, nocast=True)


def finished(S, i, dmg, mode):
    idle = mode == "idle" or mode == "active"
    active = mode == "active"
    lit = (idle or mode == "build") and not dmg
    if dmg:
        S.skin.update(D.SOOT_SKIN)
    x0, x1, y0, y1 = BLOCK
    D.pad_with_trench(S, PAD, TR, TR_YTOP, YD + 0.02)
    D.hazard_bar(S, -3.3, 3.3, -4.95, -4.65, 0.6)
    D.ramp_entrance(S, TR, TR_YTOP, YD + 0.02, TR_BOT, dmg, mouth_top=2.2, nst=7)
    # chamber block with plinth, roof slab and parapets
    S.bb("hall", x0, x1, y0, y1, 0.14, BLK_H, "concrete", 0.05)
    S.bb("hplinth", x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("hroof", x0 - 0.15, x1 + 0.15, y0 - 0.3, y1 + 0.1, BLK_H, ZR, "roof", 0.05)
    S.bb("hparS", x0 - 0.15, x1 + 0.15, y0 - 0.3, y0 - 0.12, ZR, ZR + 0.2, "concrete2", 0.02)
    S.bb("hparW", x0 - 0.15, x0 + 0.05, y0 - 0.3, y1 + 0.1, ZR, ZR + 0.2, "concrete2", 0.02)
    S.bb("hparE", x1 - 0.05, x1 + 0.15, y0 - 0.3, y1 + 0.1, ZR, ZR + 0.2, "concrete2", 0.02)
    # coffered roof: concrete ribs across the slab and a skylight strip of lead glass either side of the lantern
    for k in range(6):
        yy = y0 + 0.9 + k * 1.2
        if abs(yy - LANT[1]) < 1.2:
            continue
        S.bb(f"rib{k}", x0 - 0.05, x1 + 0.05, yy, yy + 0.22, ZR, ZR + 0.12, "concrete2", 0.0)
    for k in range(3):
        S.bb(f"skyl{k}", -2.6 + k * 0.4, -2.3 + k * 0.4, 0.4, 4.2, ZR + 0.0, ZR + 0.06, "dark", 0.0, nocast=True)
    # clerestory: a long band of lamp slits high on the south face, either side of the portal
    for sgn in (-1, 1):
        xa = 1.25 if sgn > 0 else -2.8
        S.bb(f"clerb{sgn}", xa, xa + 1.6, y0 - 0.06, y0, 1.7, 2.5, "oxide", 0.0)
        for k in range(5):
            on = active or (lit and (k + i + (0 if sgn > 0 else 2)) % 4 != 0)
            S.bb(f"glowc{sgn}{k}", xa + 0.1 + 0.3 * k, xa + 0.3 + 0.3 * k, y0 - 0.1, y0 - 0.06, 1.85, 2.35,
                 ("lamp" if active and (k + i) % 2 == 0 else "redglow") if on else "oxide2", 0.0, nocast=True)
    # wide cantilevered hood with the magenta band, two pylons with banners
    D.hood(S, -1.5, 1.5, y0 - 1.3, y0 - 0.1, dmg, z0=2.2, z1=2.6, band_z=(2.25, 2.55), collapse=1.2)
    for sgn in (-1, 1):
        if dmg and sgn > 0:
            S.bb("pylon", 1.9, 2.7, y0 - 0.9, y0 - 0.1, 0.14, 1.6, "concrete2", 0.03)
            S.box("pylcap", (0.9, 0.9, 0.3), (3.0, (y0 - 1.3) * S.YS, 0.3), "concrete2", 0.0, rot=(0, 0, 28), stretch=False)
            continue
        xa = 1.9 if sgn > 0 else -2.7
        S.bb("pylon", xa, xa + 0.8, y0 - 0.9, y0 - 0.1, 0.14, 2.3, "concrete2", 0.03)
        S.bb("pylcap", xa - 0.08, xa + 0.88, y0 - 0.98, y0 - 0.02, 2.3, 2.45, "concrete", 0.0)
        S.bb("banner", xa + 0.12, xa + 0.68, y0 - 0.96, y0 - 0.9, 0.5, 2.0, "house", 0.0)
        S.bb("bannerb", xa + 0.1, xa + 0.7, y0 - 0.98, y0 - 0.96, 0.45, 0.55, "oxide", 0.0, nocast=True)
    lantern(S, i, dmg, "active" if active else ("idle" if lit else "build"))
    dish(S, i, dmg, "active" if active else "idle")
    D.loudspeaker(S, 1.9, 0.6, ZR, dmg, height=1.7)
    D.roof_vents(S, ZR, ((-2.2, 0.8, 0.5), (-0.6, 4.2, 0.5)))
    D.chimney(S, STK[0], STK[1], ZR, STK_TOP - (0.6 if dmg else 0.0), dmg, lit, i, rise=1.0, lean_len=1.1)
    D.ash_drift(S, ((2.6, -4.3, 0.8), (-2.9, -3.9, 0.6)))
    if dmg:
        L.R.scorch(S, "scC", -1.8, y0 - 0.08, 1.5, 1.2, 0.8)
        L.R.scorch(S, "scT", 0.6, 3.0, ZR, 1.4, 1.0, plane="T")


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.2, 3.0, -4.7, -4.0, 0.14, 0.7, "crate", 0.03)
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 1.8, (-1.9, -4.4, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", 1.2, 2.8, -4.2, -4.0, 0.14, 0.3, "rebar", 0.0)
    S.bb("planks", -3.0, -1.8, 4.2, 4.8, 0.14, 0.4, "timber", 0.0)


def _left(S):
    S.bb("planks", -2.9, -1.9, -4.5, -4.1, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 1.8, (1.8, -4.4, 0.3), "steel", axis="X", verts=10)
    L.spark(S, 0.4, -3.8, 1.4, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(LANT[0] + 0.2, LANT[1], ZR + 0.6)[0], y=anchor(LANT[0] + 0.2, LANT[1], ZR + 0.6)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(0.9, YD - 1.0, 2.3)[0], y=anchor(0.9, YD - 1.0, 2.3)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(STK[0], STK[1], STK_TOP - 0.4)[0], y=anchor(STK[0], STK[1], STK_TOP - 0.4)[1], phase=2),
    dict(state="damaged", kind="smoke_plume", x=anchor(LANT[0], LANT[1], ZR + 0.8)[0], y=anchor(LANT[0], LANT[1], ZR + 0.8)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(STK[0], STK[1], STK_TOP - 0.3)[0], y=anchor(STK[0], STK[1], STK_TOP - 0.3)[1], phase=5),
]

SPEC = dict(
    pad=PAD, env=(-3.0, 3.0, YD, 4.8), H=2.6, boxes=[(-3.0, 3.0, YD, 4.8)], rings=[(STK[0], STK[1], 0.6), (LANT[0], LANT[1], 1.0)],
    clips=(1.3, 2.5), scaf=[(-3.2, -2.5, -4.9, -4.2), (1.5, 2.5, -4.9, -4.2)],
    derrick=(3.0, -4.5, 5.0, (2.3, -3.6, 5.3)), sparks=[(-2.0, -1.5, 2.4), (1.0, 1.0, 2.4), (-2.0, 3.8, 2.5)],
    hide=D.HIDE_ANIM + ("banner", "clerb", "lring"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    if state == "active":
        finished(S, i, False, "active")
        return
    L.stages(S, state, i, SPEC, finished)
