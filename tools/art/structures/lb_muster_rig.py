"""Left Behind Muster Rig (2x2): infantry production, a barracks bus with a mess tent.  Original design.

A long teal bus body on a flatbed (rust patch plates, boarded windows, a canvas-flap door) with a smaller rust-red container stacked
on its west half, a big striped canvas awning over the south side with a house-colour fringe, a cooking pot over a fire in front of
it, a laundry line of cloth strips, a loudspeaker horn and a stovepipe on the roof, a chalk board and a mast flag.
Packs: the awning rolls up along the bus, the container drops onto the deck, the line is taken down.
Idle loop: pot steam and fire flicker, the awning and flag sway, laundry flaps, loudspeaker lamp blinks.  Damage (structural only,
D-069): awning torn down, container slipped, bus side holed, pot overturned; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_muster_rig"
TITLE = "Left Behind Muster Rig"
FOOTPRINT = (2, 2)
CANVAS = (128, 184)
ORIGIN = (0, 56)
DOCK = (0, 2)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", -0.6, 0.2, 2.2, 0),     # holed bus side
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 0.6, -2.4, 0.5, 4),     # overturned pot
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -0.6, 0.2, 2.5, 1),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -2.2, 0.8, 3.9, 6),
]


def g_chassis(S, dmg, i):
    K.pad(S, 3.25, 3.2)
    S.bb("railS", -3.1, 3.0, -1.5, -1.2, 0.55, 1.1, "ash2", 0.04)
    S.bb("railN", -3.1, 3.0, 1.3, 1.6, 0.55, 1.1, "ash2", 0.04)
    S.bb("deck", -3.1, 3.0, -1.6, 1.8, 1.1, 1.28, "wood", 0.03)
    S.bb("bump", -3.15, -3.0, -1.4, 1.5, 0.6, 1.0, "hazard", 0.02)
    for k, (x, r) in enumerate(((-2.3, 0.55), (-0.9, 0.62), (1.4, 0.55), (2.5, 0.6))):
        K.wheel(S, f"w{k}", x, -1.5, r + 0.1, r, 0.5, "steel" if k % 2 else "rust")
        K.wheel(S, f"wn{k}", x, 1.5, r + 0.1, r, 0.5)


def g_bus(S, dmg, i):
    S.bb("bus", -3.0, 2.4, -1.4, 1.5, 1.28, 2.9, "teal", 0.07)
    S.bb("busroof", -3.1, 2.5, -1.5, 1.6, 2.9, 3.04, "canvas2" if not dmg else "canvas2_s", 0.04)
    S.bb("roofp1", 0.6, 2.2, -1.3, 0.2, 3.04, 3.07, "rust", 0.0, nocast=True)
    S.bb("busstripe", -3.0, 2.4, -1.45, -1.35, 2.0, 2.1, "bone", 0.0)
    for k, (x0, x1, m) in enumerate(((-2.8, -2.0, "glass"), (-1.7, -0.9, "wood"), (-0.6, 0.2, "glass"), (0.5, 1.3, "glass"))):
        S.bb(f"win{k}", x0, x1, -1.44, -1.4, 2.25, 2.7, m, 0.0)
    S.bb("door", 1.6, 2.2, -1.44, -1.4, 1.3, 2.5, "ash2", 0.0)
    S.bb("flap", 1.55, 2.25, -1.5, -1.44, 1.6, 2.5, "canvas2", 0.0)
    S.bb("patch1", -2.9, -2.2, -1.47, -1.4, 1.4, 1.9, "rust", 0.0)
    S.bb("patch2", 0.3, 1.0, -1.47, -1.4, 1.45, 1.95, "ash", 0.0)
    S.bb("panel", -1.9, -0.5, -1.5, -1.47, 1.4, 2.0, "house", 0.0)
    # container stacked on the west half
    S.bb("box", -3.0, -0.8, -1.2, 1.2, 3.04, 4.1, "rust", 0.05)
    S.bb("boxdoor", -2.7, -1.1, -1.24, -1.2, 3.2, 3.9, "rust2", 0.0)
    S.bb("boxstripe", -3.0, -0.8, -1.24, -1.2, 3.5, 3.6, "hazard", 0.0, nocast=True)
    if dmg:
        S.bb("boxhole", -2.0, -1.0, -1.24, -1.2, 3.5, 4.0, "char", 0.0, nocast=True)
        S.bb("busholes", -0.5, 0.5, -1.48, -1.44, 1.6, 2.7, "char", 0.0, nocast=True)


def g_roofgear(S, dmg, i):
    K.stovepipe(S, "spipe", 1.4, 0.6, 3.05, 4.4 if not dmg else 3.6, i, smoke=True, broken=dmg)
    # loudspeaker horn on a pole, lamp blinks
    S.cyl("hornpole", 0.05, 0.6, (0.2, 0.8, 3.35), "ash2", verts=6)
    S.cone("horn", 0.1, 0.45, 0.7, (0.2, -0.1 * S.YS, 3.8), "bone", verts=12, nocast=False, rot=(70, 0, 0))
    S.sphere("hornlamp", 0.1, (0.2, 0.8, 3.75), "lamp" if (i % 2 == 0 and not dmg) else "lamp_off")
    S.bb("sacks", 0.4, 1.3, 0.8, 1.4, 3.04, 3.4, "canvas2", 0.12)


def g_tent(S, dmg, i):
    K.awning(S, "awn", -0.1, 2.7, -1.45, -3.1, 2.9, 0.8, i, dmg, mats=("canvas2", "teal"), strip=1.4)
    # laundry line under the awning
    for k in range(4):
        x = 0.1 + k * 0.6
        S.bb(f"cloth{k}", x, x + 0.4, -2.6 + 0.04 * math.sin(i + k), -2.56 + 0.04 * math.sin(i + k), 1.9 if not dmg else 1.2, 2.4 if not dmg else 1.7,
             ("canvas", "ochre", "bone", "rust")[k] if not dmg else "canvas_s", 0.0, nocast=True)


def g_extras(S, dmg, i):
    # cooking pot over a fire, benches, chalk board, flag
    if dmg:
        S.cyl("pot", 0.38, 0.5, (1.3, -2.3 * S.YS, 0.3), "ash2", axis="X", verts=12, stretch=False)
    else:
        S.cyl("potlegs", 0.28, 0.4, (1.3, -2.4, 0.3), "dark", verts=6)
        S.cyl("pot", 0.38, 0.5, (1.3, -2.4, 0.75), "ash2", verts=12)
        S.cyl("flamel", 0.3, 0.05, (1.3, -2.4, 0.12), "flame" if i % 2 == 0 else "flame2", verts=8, nocast=True)
        for q in range(2):
            ph = ((i / 4) + q / 2) % 1
            S.sphere(f"steam{q}", 0.1 + 0.18 * ph, (1.3 + ph * 0.3, -2.4, 1.1 + ph * 0.9), "smoke", squash=0.9)
    S.bb("bench1", -1.8, -0.6, -2.8, -2.55, 0.1, 0.45, "wood", 0.02)
    S.bb("bench2", 1.5, 2.6, -2.8, -2.55, 0.1, 0.45, "wood", 0.02)
    K.post(S, "boardp", 2.5, -3.0, 0.1, 1.0, 0.08, "wood")
    S.bb("board", 2.0, 3.0, -3.08, -3.0, 0.7, 1.4, "dark", 0.0)
    S.bb("boardc", 2.15, 2.85, -3.1, -3.08, 0.85, 1.25, "bone", 0.0, nocast=True)
    K.flag(S, "flag", 2.0, 1.6, 5.2 if not dmg else 4.0, i, w=1.2, h=0.8, dmg=dmg)


GROUPS = [g_chassis, g_bus, g_roofgear, g_tent, g_extras]


def site(S):
    K.stakes_site(S, 2.7, 2.7)


def dmg_extra(S, i):
    S.bb("rubble", -2.9, -2.1, -3.1, -2.7, 0.1, 0.3, "ash2", 0.04)
    S.box("fallen", (1.2, 0.5, 0.1), (1.5, -3.0 * S.YS, 0.2), "teal", 0.0, rot=(0, 0, -25), stretch=False)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
