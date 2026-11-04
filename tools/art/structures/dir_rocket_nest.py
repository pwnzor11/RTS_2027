"""Directorate Rocket Nest: a sandbagged pit with a rack of rockets pointed at the sky (1x1 cell, anti-air).  Original design.

The read is a SUNK ROUND EMPLACEMENT: a low umber concrete collar with bone / oxide ticks, a double course of umber sandbags
around a dark pit, and in the middle a squat pedestal carrying a cradle with four rockets tilted steeply up and slightly north
(bone body, oxide warhead rings, a glowing ember at each tail).  A magenta house plate on the pedestal, an ember vent stub and an
ammunition crate on the pad.  Static (fixed mount, per the rules: no turret given); only the tail embers and a lamp animate.
Idle loop: tail embers flicker, the lamp blinks.  Damage is structural only (D-069): the cradle has slumped over, two rockets
are gone / snapped, sandbags burst and scattered, a scorch on the pit wall; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-1.7, 1.7].
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_def as DD
D = DD.D
L = DD.L

ID = "dir_rocket_nest"
TITLE = "Directorate Rocket Nest"
FOOTPRINT = (1, 1)
CANVAS = (64, 112)
ORIGIN = (0, 48)
DOCK = (0, 1)
CAP_MATERIAL = D.CAP_MATERIAL
HOUSE2_MID = True
PALETTE = D.PALETTE
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
MATERIALS = DD.MATERIALS
SOOT_SKIN = DD.SOOT_SKIN
PRIMER_SKIN = DD.PRIMER_SKIN

PAD = (-1.7, 1.7, -1.7, 1.7)
PIV = (0.0, -0.05, 0.85)      # cradle pivot
PITCH = 64.0


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def rack(S, i, dmg, idle):
    pitch = 28.0 if dmg else PITCH
    th = 14.0 if dmg else 0.0
    base = PIV
    p3 = DD.p3
    # cradle: two side rails and cross ribs along the tilted axis
    for sx in (-0.4, 0.4):
        p3(S, "rail", (0.08, 1.5, 0.08), base, (sx, 0.55, -0.02), "steel", pitch, th, 0.0)
    for f in (0.1, 0.7, 1.2):
        p3(S, "rib", (0.9, 0.07, 0.07), base, (0.0, f, -0.02), "steel", pitch, th, 0.0)
    for k, (rx, ru) in enumerate(((-0.2, 0.2), (0.2, 0.2), (-0.2, -0.12), (0.2, -0.12))):
        if dmg and k in (1, 2):
            if k == 1:      # snapped stump
                p3(S, f"rk{k}", (0.18, 0.5, 0.18), base, (rx, 0.45, ru), "bone", pitch, th, 0.03)
            continue
        p3(S, f"rk{k}", (0.19, 1.35, 0.19), base, (rx, 0.72, ru), "bone", pitch, th, 0.03)
        p3(S, f"rkb{k}", (0.2, 0.12, 0.2), base, (rx, 0.55, ru), "oxide", pitch, th, 0.0, nocast=True)
        p3(S, f"rkn{k}", (0.17, 0.3, 0.17), base, (rx, 1.52, ru), "oxide", pitch, th, 0.05)
        p3(S, f"rkt{k}", (0.1, 0.1, 0.1), base, (rx, 1.7, ru), "dark", pitch, th, 0.0, nocast=True)
        on = idle and ((k + i) % 3 != 0) and not dmg
        p3(S, f"glow{k}", (0.15, 0.1, 0.15), base, (rx, 0.0, ru), "redglow" if on else "oxide2", pitch, th, 0.0, nocast=True)
    if dmg:
        S.box("rkfall", (0.2, 1.1, 0.2), (0.95, -1.0 * S.YS, 0.35), "bone", 0.03, rot=(0, 0, 40), stretch=False)
        S.box("rkfalln", (0.18, 0.28, 0.18), (1.35, -1.55 * S.YS, 0.3), "oxide", 0.03, rot=(0, 0, 40), stretch=False)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    YS = S.YS
    if dmg:
        S.skin.update(SOOT_SKIN)
    S.bb("pad", PAD[0], PAD[1], PAD[2], PAD[3], 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", PAD[0], PAD[1], PAD[2], PAD[2] + 0.08, 0.14, 0.2, "concrete2", 0.0)
    D.hazard_bar(S, PAD[0] + 0.05, PAD[1] - 0.05, -1.62, -1.38, 0.34)
    # collar and pit
    S.cyl("collar", 1.3, 0.3, (0, 0, 0.29), "concrete2", verts=28)
    S.cyl("collarband", 1.33, 0.1, (0, 0, 0.32), "oxide", verts=28)
    S.cyl("pit", 1.12, 0.12, (0, 0, 0.45), "dark", verts=24)
    S.cyl("pitfloor", 0.95, 0.06, (0, 0, 0.5), "earth2", verts=24)
    DD.sack_ring(S, 0.0, 0.0, 1.15, 1.15, 2, gaps=(7, 8) if not dmg else (7, 8, 3), dmg=dmg, n=14, bag=(0.62, 0.34, 0.24), tag="sb")
    # pedestal, house plate, pivot post
    S.cyl("ped", 0.6, 0.45, (0, 0, 0.5 + 0.22), "hull2", verts=14)
    S.cyl("pedband", 0.62, 0.1, (0, 0, 0.62), "bone", verts=14)
    S.cyl("pedtop", 0.5, 0.08, (0, 0, 0.74), "steel", verts=14)
    S.bb("plate", -0.28, 0.28, -0.72, -0.5, 0.58, 0.64, "house", 0.0, nocast=True)
    rack(S, i, dmg, idle)
    # ember vent stub and crate in the pad corners
    S.cyl("vstub", 0.16, 0.3, (1.3, -1.15, 0.29), "hull2", verts=10)
    S.cyl("vstubh", 0.22, 0.06, (1.3, -1.15, 0.45), "steel", verts=10)
    S.cyl("glowv", 0.12, 0.04, (1.3, -1.15, 0.49), "oxide2" if dmg else "redglow", verts=8, nocast=True)
    S.bb("crate", -1.62, -1.2, -1.5, -1.12, 0.14, 0.45, "crate", 0.03)
    S.bb("crateb", -1.6, -1.22, -1.52, -1.5, 0.28, 0.36, "oxide", 0.0, nocast=True)
    S.bb("lampp", 1.4 - 0.04, 1.4 + 0.04, 1.3 - 0.04, 1.3 + 0.04, 0.14, 0.7, "steel", 0.0)
    S.bb("lamp", 1.4 - 0.1, 1.4 + 0.1, 1.3 - 0.1, 1.3 + 0.1, 0.7, 0.88, "redglow" if (idle and i % 2 == 0 and not dmg) else "oxide2", 0.0, nocast=True)
    if dmg:
        L.R.scorch(S, "scN", -0.2, 0.9, 1.2, 0.8, 0.45)
        S.bb("crackp", 0.3, 0.38, -1.3, -1.28, 0.3, 0.45, "dark", 0.0, nocast=True)
        DD.spill(S, ((1.4, 0.3, 0.5, 0.24), (-1.25, 0.9, 0.46, 0.2), (0.9, -1.45, 0.4, 0.2)))
    D.ash_drift(S, ((1.2, 1.1, 0.5),))


FX = [
    dict(state="damaged", kind="fire_small", x=anchor(0.0, 0.2, 1.5)[0], y=anchor(0.0, 0.2, 1.5)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(0.0, 0.2, 1.7)[0], y=anchor(0.0, 0.2, 1.7)[1], phase=3),
]

SPEC = DD.stage_spec(PAD, (-1.4, 1.4, -1.4, 1.4), 2.4, [], [(0.0, 0.0, 1.05)], [(-1.5, -0.9, -1.55, -1.2)],
                     (1.2, 0.9, 2.6, (0.6, 0.5, 2.8)), [(0.0, 0.0, 1.0)], (0.9, 1.6), hide=("glow", "lamp", "rkt"))


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
