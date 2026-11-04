"""Directorate Pillbox: a squat concrete turret with a narrow firing slit and a loudspeaker (1x1 cell).  Original design.

The head ROTATES like auth_turret (frames indexed like vehicles: facing 0 = north, clockwise, 22.5 deg steps) and is built by
tools/art/structures/dir_def_build.py (same sheet layout as turret_build.py).  Read at 1x: a low, wide umber concrete block that
is mostly buried in a sunk round collar (bone / oxide ticks), a heavy overhanging roof slab, a bone lintel over a long dark slit
with a stubby machine gun poking out of it, a magenta house band down each side, a small loudspeaker horn on the roof and an
ember vent stub on the pad.  Facing is legible from the slit, the gun and the asymmetric roof fittings.
STATES are flat frame counts for build_structure.render():
  idle 16 facings x 1, fire 16 x 2 (k0 flash + recoil, k1 dying flash), damaged 16 x 1 (structural), build 1 x 6 (stage 5 = idle 0).
Contract: see auth_turret.py / auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0).
"""
import importlib.util
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_def as DD
D = DD.D
L = DD.L
part, cpart = DD.part, DD.cpart


def _load(fname, name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(_HERE, fname))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


A = _load("auth_turret.py", "structlib_auth_turret")      # read-only: generic build stages 0-2

ID = "dir_pillbox"
TITLE = "Directorate Pillbox"
FOOTPRINT = (1, 1)
CANVAS = (80, 104)
ORIGIN = (8, 32)
DOCK = (0, 1)
CAP_MATERIAL = D.CAP_MATERIAL
PALETTE = D.PALETTE
FACINGS = 16
STATES = [("idle", 16, 1), ("fire", 32, 1), ("damaged", 16, 1), ("build", 6, 5)]
MANIFEST_STATES = [("idle", 16, 1, 1), ("fire", 16, 2, 2), ("damaged", 16, 1, 1), ("build", 1, 6, 5)]
MATERIALS = DD.MATERIALS
SOOT_SKIN = DD.SOOT_SKIN
PRIMER_SKIN = DD.PRIMER_SKIN
beam, post = L.beam, L.post

ZH = 0.62


def px(x, y, z):
    return round(ORIGIN[0] + 32 + x * 18.8), round(ORIGIN[1] + 32 - y * 18.8 * math.sin(math.radians(40)) - z * 14.4)


_hx, _hy = px(0.0, 0.0, 1.55)
FX = [
    dict(state="damaged", kind="fire_small", x=_hx, y=_hy, phase=0),
    dict(state="damaged", kind="smoke_plume", x=_hx, y=_hy - 2, phase=2),
]


def base(S, dmg=False):
    S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.12, "pad", 0.03)
    S.cyl("step", 1.5, 0.24, (0, 0, 0.12 + 0.12), "concrete2", verts=32, stretch=False)
    S.cyl("ring", 1.28, 0.44, (0, 0, 0.12 + 0.22), "concrete", verts=32, stretch=False)
    for k in range(8):
        a = math.radians(k * 45 + 22.5)
        S.box(f"tick{k}", (0.3, 0.12, 0.04), (1.4 * math.sin(a), 1.4 * math.cos(a), 0.37), "oxide" if k % 2 == 0 else "bone", 0.0,
              rot=(0, 0, -math.degrees(a)), stretch=False, nocast=True)
    S.cyl("track", 1.1, 0.1, (0, 0, ZH - 0.05 + 0.03), "steel", verts=32, stretch=False)
    S.cyl("trackd", 1.0, 0.04, (0, 0, ZH + 0.02 + 0.03), "dark", verts=32, stretch=False)
    # ember vent stub and an ammunition locker on the corners, off the barrel's sweep
    S.cyl("vstub", 0.2, 0.34, (1.2, -1.2, 0.29), "hull2", verts=10, stretch=False)
    S.cyl("vstubh", 0.27, 0.07, (1.2, -1.2, 0.48), "steel", verts=10, stretch=False)
    S.cyl("vstubd", 0.16, 0.05, (1.2, -1.2, 0.52), "dark" if dmg else "redglow", verts=8, stretch=False, nocast=True)
    S.bb("locker", -1.5, -0.95, -1.5, -1.05, 0.12, 0.5, "concrete2", 0.03)
    S.bb("lockerb", -1.45, -1.0, -1.52, -1.5, 0.3, 0.4, "oxide", 0.0, nocast=True)
    if dmg:
        S.box("crackc", (0.5, 0.06, 0.5), (0.2, -1.27, 0.4), "dark", 0.0, rot=(0, 0, 20), stretch=False, nocast=True)
        S.box("chunkc", (0.5, 0.4, 0.22), (1.2, -0.5, 0.2), "concrete_s", 0.0, rot=(0, 0, 25), stretch=False)
        S.box("chunkc", (0.3, 0.26, 0.18), (-0.9, -1.2, 0.18), "concrete2_s", 0.0, rot=(0, 0, -30), stretch=False)
        S.bb("scorchb", 0.5, 1.4, -1.58, -1.56, 0.0, 0.4, "char", 0.0, nocast=True)


def head(S, th, dmg=False, flash=0):
    z0 = ZH + 0.08
    cpart(S, "collar", 1.0, 0.16, (0, 0, z0 + 0.08), "steel", th, verts=24)
    # low, wide block with a sloped front and a heavy overhanging roof slab
    part(S, "hull", (1.8, 1.6, 0.55), (0, -0.05, z0 + 0.43), "concrete", th, 0.08)
    part(S, "glacis", (1.7, 0.5, 0.5), (0, 0.8, z0 + 0.4), "concrete2", th, 0.08, pitch=-24)
    part(S, "rearb", (1.6, 0.4, 0.45), (0, -0.88, z0 + 0.4), "concrete2", th, 0.06, pitch=20)
    part(S, "roof", (2.0, 1.5, 0.16), (0, -0.2, z0 + 0.78), "roof", th, 0.05)
    part(S, "roofstripe", (2.0, 0.08, 0.1), (0, 0.57, z0 + 0.76), "oxide", th, 0.0, nocast=True)
    # house bands on both sides, small house plate on the roof (asymmetric)
    if not dmg:
        part(S, "houseL", (0.04, 1.1, 0.22), (-0.92, -0.05, z0 + 0.5), "house", th, 0.0)
        part(S, "houseR", (0.04, 1.1, 0.22), (0.92, -0.05, z0 + 0.5), "house", th, 0.0)
        part(S, "housetop", (0.6, 0.5, 0.03), (0.4, -0.35, z0 + 0.87), "house2", th, 0.0)
    else:
        part(S, "houseL", (0.04, 1.1, 0.22), (-0.92, -0.05, z0 + 0.5), "house", th, 0.0)
    # firing slit under a bone lintel, with the gun in it
    part(S, "lintel", (1.3, 0.08, 0.1), (0, 1.07, z0 + 0.62), "bone", th, 0.0)
    part(S, "slit", (1.1, 0.06, 0.14), (0, 1.07, z0 + 0.5), "dark", th, 0.0, nocast=True)
    part(S, "slitsill", (1.3, 0.1, 0.05), (0, 1.07, z0 + 0.4), "concrete2", th, 0.0)
    # loudspeaker horn on the roof (rear left)
    part(S, "hspost", (0.1, 0.1, 0.35), (-0.55, -0.55, z0 + 1.0), "steel", th, 0.0)
    if not dmg:
        cpart(S, "horn", 0.14, 0.3, (-0.55, -0.4, z0 + 1.26), "door", th, axis="Y", verts=12)
        cpart(S, "hornr", 0.18, 0.05, (-0.55, -0.22, z0 + 1.26), "bone", th, axis="Y", verts=12)
        cpart(S, "hornd", 0.11, 0.06, (-0.55, -0.2, z0 + 1.26), "dark", th, axis="Y", verts=10)
    else:
        part(S, "hornf", (0.3, 0.3, 0.2), (-0.55, -0.45, z0 + 1.05), "door", th, 0.0, pitch=35)
    cpart(S, "hatch", 0.22, 0.07, (0.55, -0.5, z0 + 0.9), "steel", th, verts=12)
    # stubby twin-less MG: barrel and shroud poking out of the slit
    rec = 0.12 if flash == 1 else (0.05 if flash == 2 else 0.0)
    gone = dmg
    L_ = 0.85 - (0.45 if gone else 0.0)
    cy = 1.1 + L_ / 2 - rec
    part(S, "mant", (0.42, 0.24, 0.26), (0, 1.1 - rec, z0 + 0.5), "gunmetal", th, 0.03)
    if dmg:
        part(S, "barrel", (0.1, L_, 0.1), (0.05, cy, z0 + 0.48), "gunmetal", th, 0.0, pitch=-22)
    else:
        part(S, "barrel", (0.1, L_, 0.1), (0, cy, z0 + 0.5), "gunmetal", th, 0.0)
        part(S, "shroud", (0.2, 0.4, 0.2), (0, 1.3 - rec, z0 + 0.5), "steel2", th, 0.02)
        part(S, "brake", (0.15, 0.1, 0.15), (0, 1.1 + L_ - 0.03 - rec, z0 + 0.5), "dark", th, 0.0)
    if flash and not dmg:
        tip = 1.1 + L_ - rec
        size = 0.55 if flash == 1 else 0.3
        part(S, "fl", (0.2 if flash == 1 else 0.12, size, 0.2 if flash == 1 else 0.12), (0, tip + size / 2 + 0.02, z0 + 0.5), "flash", th, 0.0, nocast=True)
        if flash == 1:
            part(S, "fl2", (0.4, 0.14, 0.4), (0, tip + 0.06, z0 + 0.5), "flash2", th, 0.0, nocast=True)
    if dmg:
        part(S, "gash", (0.8, 0.05, 0.28), (-0.4, 1.0, z0 + 0.35), "dark", th, 0.0, nocast=True)
        part(S, "tear", (0.04, 0.6, 0.25), (0.93, 0.05, z0 + 0.5), "dark", th, 0.0, nocast=True)
        part(S, "roofgone", (0.9, 0.8, 0.06), (0.5, 0.3, z0 + 0.88), "dark", th, 0.0, nocast=True)
        part(S, "strut", (0.05, 0.05, 0.5), (0.5, 0.0, z0 + 1.1), "rebar", th, 0.0, pitch=18)
        part(S, "strut2", (0.05, 0.05, 0.4), (0.3, 0.3, z0 + 1.05), "rebar", th, 0.0, pitch=-14)


# ====================================================================================================== build stages
def build_frame(S, i):
    if i == 3:
        base(S)
        S.cyl("collar", 1.0, 0.16, (0, 0, ZH + 0.16), "steel", verts=24, stretch=False)
        for sx in (-0.7, 0.7):
            for sy in (-0.6, 0.6):
                post(S, "hp", sx, sy, ZH + 0.24, ZH + 1.0, 0.12, "girder")
        for z in (ZH + 0.55, ZH + 1.0):
            for sy in (-0.6, 0.6):
                beam(S, "hx", (-0.7, sy, z), (0.7, sy, z), 0.1, "girder")
            for sx in (-0.7, 0.7):
                beam(S, "hy", (sx, -0.6, z), (sx, 0.6, z), 0.1, "girder")
        beam(S, "hb", (-0.7, -0.6, ZH + 0.3), (0.7, -0.6, ZH + 0.55), 0.06, "girder")
        S.bb("pl1", -1.5, -0.8, 0.6, 1.5, 0.12, 0.4, "timber", 0.0)
        L.spark(S, 0.7, -0.6, ZH + 1.0, 1, 3)
    else:
        base(S)
        head(S, 0.0)
        for sx in (-1.55, -1.1):
            post(S, "sp", sx, -1.3, 0.12, 2.0, 0.07, "timber")
            post(S, "sp", sx, -0.4, 0.12, 2.0, 0.07, "timber")
        for z in (0.9, 1.5):
            S.bb("deck", -1.62, -1.02, -1.4, -0.3, z, z + 0.06, "timber", 0.0)
        beam(S, "br", (-1.55, -1.3, 0.12), (-1.1, -1.3, 1.6), 0.05, "timber")
        L.spark(S, 0.0, -0.9, ZH + 0.9, 1, 5)


def build(S, state, i, n):
    if state == "idle":
        base(S)
        head(S, i * 22.5)
    elif state == "fire":
        f, k = divmod(i, 2)
        base(S)
        head(S, f * 22.5, flash=1 + k)
    elif state == "damaged":
        S.skin.update(SOOT_SKIN)
        base(S, dmg=True)
        head(S, i * 22.5, dmg=True)
    else:
        if i < 3:
            A.build_site(S, i)
            return
        if i == 4:
            S.hide = lambda nm: nm.startswith(("barrel", "shroud", "brake", "mant", "house", "hatch", "horn", "hs", "tick", "slit", "lintel", "roofstripe", "vstubd", "lockerb"))
            S.skin.update(PRIMER_SKIN)
        if i == 5:
            base(S)
            head(S, 0.0)
            return
        build_frame(S, i)
