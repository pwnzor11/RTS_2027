"""Left Behind Still Rig (2x2): supply building, a water still on a wagon frame.  Original design.

A flatbed on mismatched wheels carrying a rust-red firebox with a glowing mouth, a fat copper-ochre still pot (shoulder, neck and
bulb cap) on top of it, a swan-neck pipe down to a tall teal condenser drum wrapped in rust and ochre coil rings (a wide house-colour
belt round its middle), a tap with a dripping spout over a catch barrel, and a striped rain-catch awning over the south-east deck.
A small chalk board reads "left tap only", a stovepipe vents the firebox, a mast flag in house colour stands at the back.
Packs (into the generic lb_mother_rig_packed, which has its own sheet): the awning rolls up, the condenser coil is laid down along
the deck, the pipe is unbolted; the rig itself has no pack frames, like the other LB rigs.
Idle loop: flame flicker, steam from the cap, the drip falls, awning and flag sway.  Damage (structural only, D-069): still pot
dented and tipped, swan neck snapped, awning torn, condenser leaning, a wheel flat; fire / smoke are FX anchors.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_still_rig"
TITLE = "Left Behind Still Rig"
FOOTPRINT = (2, 2)
CANVAS = (128, 192)
ORIGIN = (0, 64)
DOCK = (0, 2)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
PX, PY = -1.7, 0.15          # still pot centre
CX, CY = 1.5, 0.15           # condenser centre
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", PX, -1.0, 1.9, 0),      # firebox mouth
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", 1.9, -2.4, 2.1, 4),     # torn awning
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", PX, PY, 3.6, 1),       # dented pot
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -2.6, 0.9, 3.4, 6),    # stovepipe
]


def g_chassis(S, dmg, i):
    K.pad(S, 3.25, 3.2)
    S.bb("railS", -3.1, 3.0, -2.4, -2.1, 0.55, 1.1, "ash2", 0.04)
    S.bb("railN", -3.1, 3.0, 2.1, 2.4, 0.55, 1.1, "ash2", 0.04)
    S.bb("deck", -3.1, 3.0, -2.45, 2.45, 1.1, 1.28, "wood", 0.03)
    S.bb("bump", -3.15, -3.0, -2.1, 2.1, 0.6, 1.0, "hazard", 0.02)
    for k, (x, r) in enumerate(((-2.3, 0.55), (-0.7, 0.6), (1.0, 0.5), (2.4, 0.62))):
        rr = r * (0.78 if (dmg and k == 1) else 1.0)
        K.wheel(S, f"w{k}", x, -2.2, rr + 0.1, rr, 0.5, "steel" if k % 2 else "rust")
        K.wheel(S, f"wn{k}", x, 2.2, rr + 0.1, rr, 0.5)


def g_firebox(S, dmg, i):
    S.bb("fbox", -2.9, -0.5, -1.0, 1.3, 1.28, 2.25, "rust2", 0.06)
    S.bb("fboxcap", -3.0, -0.4, -1.1, 1.4, 2.25, 2.4, "ash2", 0.04)
    S.bb("fbrib", -2.9, -0.5, -1.04, -1.0, 1.55, 1.65, "rust", 0.0)
    S.bb("fmouth", -2.3, -1.1, -1.06, -1.0, 1.4, 2.0, "dark", 0.0)
    S.bb("fdoor", -2.35, -1.05, -1.1, -1.06, 1.95, 2.05, "ash", 0.0)
    if not dmg:
        S.bb("fflame", -2.15, -1.25, -1.1, -1.06, 1.45, 1.62 + 0.14 * (i % 2), "flame" if i % 2 == 0 else "flame2", 0.0, nocast=True)
    else:
        S.bb("fchar", -2.3, -1.1, -1.1, -1.06, 1.4, 2.0, "char", 0.0, nocast=True)
    S.bb("fpatch", -0.95, -0.55, -1.04, -1.0, 1.4, 2.05, "teal", 0.0)
    K.stovepipe(S, "spipe", -2.6, 1.0, 2.4, 3.7 if not dmg else 3.0, i, smoke=True, broken=dmg)


def g_still(S, dmg, i):
    z0 = 2.4
    if dmg:     # pot dented and tipped on its side against the firebox
        S.cyl("pot", 0.95, 1.5, (PX + 0.1, PY * S.YS, z0 + 0.95), "ochre_s", axis="Y", verts=18, stretch=False)
        S.cyl("potb", 0.98, 0.14, (PX + 0.1, (PY - 0.3) * S.YS, z0 + 0.95), "rust_s", axis="Y", verts=18, stretch=False)
        S.sphere("potcap", 0.45, (PX + 0.1, PY - 1.0, z0 + 0.95), "ochre_s", nocast=False)
        return
    S.cyl("pot", 1.05, 1.15, (PX, PY, z0 + 0.58), "ochre", verts=20)
    S.cyl("potb", 1.09, 0.14, (PX, PY, z0 + 0.35), "rust", verts=20)
    S.cyl("potb2", 1.09, 0.14, (PX, PY, z0 + 0.95), "rust", verts=20)
    S.cone("shoulder", 1.05, 0.42, 0.75, (PX, PY, z0 + 1.52), "ochre", verts=20, nocast=False)
    S.cyl("neck", 0.4, 0.55, (PX, PY, z0 + 2.15), "ochre", verts=14)
    S.cyl("neckr", 0.46, 0.1, (PX, PY, z0 + 1.95), "rust", verts=14)
    S.sphere("cap", 0.5, (PX, PY, z0 + 2.65), "ochre", nocast=False)
    S.cyl("capk", 0.12, 0.2, (PX, PY, z0 + 3.2), "rust2", verts=8)
    S.bb("hatch", PX - 0.32, PX + 0.32, PY - 1.1, PY - 1.0, z0 + 0.5, z0 + 0.9, "ash2", 0.02)
    for q in range(3):
        ph = ((i / 4) + q / 3) % 1
        rr = 0.12 + 0.3 * math.sin(math.pi * ph) ** 0.7
        S.sphere(f"steam{q}", rr, (PX + 0.2 + ph * 0.9, PY - 0.1, z0 + 3.35 + ph * 1.0), "smoke" if q != 1 else "smoke2", squash=0.85)


def g_condenser(S, dmg, i):
    lean = (1.0, -0.4) if dmg else (0.0, 0.0)
    z0, z1 = 1.28, 3.9 if not dmg else 3.1
    S.cyl("drum", 0.7, z1 - z0, (CX, CY, (z0 + z1) / 2), "teal", verts=18)
    for k in range(5 if not dmg else 3):
        z = z0 + 0.35 + k * 0.5
        S.cyl(f"coil{k}", 0.75, 0.1, (CX, CY, z), "ochre" if k % 2 == 0 else "rust", verts=18)
    S.cyl("hband", 0.73, 0.55, (CX, CY, z0 + 1.55), "house", verts=18)
    S.cone("dtop", 0.7, 0.28, 0.4, (CX, CY, z1 + 0.2), "rust2", verts=18, nocast=False)
    if not dmg:
        # swan-neck pipe from the pot cap to the top of the drum
        K.beam(S, "swan1", (PX + 0.3, PY, 6.0 - 0.9 - 0.5), (PX + 1.2, PY, 5.35 - 0.5), 0.15, "ochre")
        K.beam(S, "swan2", (PX + 1.2, PY, 4.85), (CX - 0.1, CY, z1 + 0.3), 0.15, "ochre")
        K.post(S, "swan3", CX, CY, z1 + 0.2, z1 + 0.5, 0.16, "ochre")
    else:
        K.beam(S, "swanb", (PX + 0.9, PY - 0.6, 3.0), (PX + 1.6, PY - 1.4, 1.5), 0.14, "ochre_s")
        S.bb("drumhole", CX - 0.4, CX + 0.4, CY - 0.75, CY - 0.68, z0 + 0.4, z0 + 1.0, "char", 0.0, nocast=True)
    # tap, spout and catch barrel
    S.bb("tap", CX - 0.1, CX + 0.1, CY - 1.15, CY - 0.55, z0 + 0.25, z0 + 0.45, "steel", 0.0)
    S.bb("tapk", CX - 0.18, CX + 0.18, CY - 1.15, CY - 1.05, z0 + 0.45, z0 + 0.6, "hazard", 0.0)
    if not dmg:
        S.sphere("drip", 0.07, (CX, CY - 1.1, z0 + 0.2 - 0.14 * (i % 4)), "glass", nocast=True)
    K.drum(S, "catch", CX, CY - 1.55, 1.28, m="teal", band="hazard", h=0.8, r=0.4)


def g_awning(S, dmg, i):
    K.awning(S, "awn", 0.5, 3.0, -1.75, -3.05, 2.7, 0.7, i, dmg, mats=("canvas2", "teal"), strip=0.85)
    K.drum(S, "rainb", 2.6, -2.75, 1.28, m="rust", band="ash2", h=0.7, r=0.36)


def g_extras(S, dmg, i):
    S.bb("bench1", -2.8, -1.4, -2.9, -2.65, 0.1, 0.45, "wood", 0.02)
    S.bb("sacks", -3.0, -2.3, 1.2, 2.0, 1.28, 1.7, "canvas2", 0.12)
    S.bb("crate", 1.9, 2.8, 1.3, 2.2, 1.28, 1.95, "wood", 0.03)
    K.post(S, "boardp", -0.4, -3.0, 0.1, 1.0, 0.08, "wood")
    S.bb("board", -0.9, 0.1, -3.08, -3.0, 0.7, 1.4, "dark", 0.0)
    S.bb("boardc", -0.8, 0.0, -3.1, -3.08, 0.85, 1.25, "bone", 0.0, nocast=True)
    K.flag(S, "flag", 2.8, 1.9, 5.0 if not dmg else 3.9, i, w=1.2, h=0.8, dmg=dmg)


GROUPS = [g_chassis, g_firebox, g_condenser, g_still, g_awning, g_extras]


def site(S):
    K.stakes_site(S, 2.7, 2.7)


def dmg_extra(S, i):
    S.bb("rubble", 2.6, 3.2, -3.1, -2.7, 0.1, 0.3, "ash2", 0.04)
    S.box("fallen", (1.2, 0.5, 0.1), (-1.6, -3.0 * S.YS, 0.2), "teal", 0.0, rot=(0, 0, 18), stretch=False)
    S.bb("puddle", 0.4, 1.6, -3.2, -2.9, 0.1, 0.13, "glass", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
