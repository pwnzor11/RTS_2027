"""Left Behind Spike Nest (1x1): basic defence, a swivelling spike thrower behind a road-sign shield.  Original design.

A lopsided ring of lying tyres and scrap plates round a plank deck, a steel turntable and on it a swivelling head: a wooden cradle with
a long steel rail, a drum magazine (a rust barrel with a hazard band) feeding a bundle of sharpened rebar spikes, a road-sign shield
plate in faded teal with an ochre strip and a house-colour panel, a rope-coil winch, a lamp.  Facing is legible from the shield, the
long rail and the magazine drum.  The head ROTATES like auth_turret (frames indexed like vehicles: facing 0 = north, clockwise,
22.5 deg steps) and is built with dir_def_build.py (same sheet layout as turret_build.py).
STATES are flat frame counts for build_structure.render():
  idle 16 facings x 1, fire 16 x 2 (k0 spike leaves the rail + flash + recoil, k1 flash dying), damaged 16 x 1 (structural),
  build 1 x 6 (stage 5 = idle 0).
Contract: see auth_turret.py / auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_spike_nest"
TITLE = "Left Behind Spike Nest"
FOOTPRINT = (1, 1)
CANVAS = (80, 104)
ORIGIN = (8, 32)
DOCK = (0, 1)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
FACINGS = 16
STATES = [("idle", 16, 1), ("fire", 32, 1), ("damaged", 16, 1), ("build", 6, 5)]
MANIFEST_STATES = [("idle", 16, 1, 1), ("fire", 16, 2, 2), ("damaged", 16, 1, 1), ("build", 1, 6, 5)]
ZH = 0.62


def px(x, y, z):
    return round(ORIGIN[0] + 32 + x * 18.8), round(ORIGIN[1] + 32 - y * 18.8 * math.sin(math.radians(40)) - z * 14.4)


_hx, _hy = px(0.0, 0.0, 1.5)
FX = [
    dict(state="damaged", kind="fire_small", x=_hx, y=_hy, phase=0),
    dict(state="damaged", kind="smoke_plume", x=_hx, y=_hy - 2, phase=2),
]


def base(S, dmg=False):
    S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.1, "dust", 0.03)
    S.cyl("deck", 1.15, 0.2, (0, 0, 0.2), "wood", verts=24, stretch=False)
    # lopsided ring of lying tyres (gaps left open) and scrap plates
    for k in range(10):
        if k in (2, 7):
            continue
        a = math.radians(k * 36 + 10)
        r = 1.28 + 0.06 * (k % 3)
        S.cyl(f"tyre{k}", 0.34, 0.3, (r * math.sin(a), r * math.cos(a), 0.15 + 0.15 * (k % 2)), "rubber", verts=12, stretch=False)
    S.cyl("turn", 0.95, 0.12, (0, 0, ZH - 0.02), "steel", verts=24, stretch=False)
    S.cyl("turnd", 0.8, 0.04, (0, 0, ZH + 0.05), "dark", verts=24, stretch=False)
    S.bb("plate1", -1.55, -0.95, 0.9, 1.2, 0.1, 0.65, "rust", 0.03)
    S.bb("plank", 0.9, 1.5, -1.5, -1.2, 0.1, 0.35, "wood", 0.02)
    S.cyl("d0", 0.22, 0.5, (1.2, -1.1, 0.35), "teal", verts=10, stretch=False)
    if dmg:
        S.box("chunk", (0.5, 0.4, 0.2), (1.1, -0.4, 0.2), "ash2", 0.0, rot=(0, 0, 25), stretch=False)
        S.bb("scorch", 0.4, 1.3, -1.58, -1.56, 0.0, 0.4, "char", 0.0, nocast=True)


def head(S, th, dmg=False, flash=0):
    z0 = ZH + 0.08
    K.cpart(S, "collar", 0.6, 0.18, (0, 0, z0 + 0.09), "ash2", th, verts=16)
    K.part(S, "cradle", (0.7, 1.3, 0.3), (0, 0.0, z0 + 0.35), "wood", th, 0.04)
    K.part(S, "pivot", (0.3, 0.3, 0.5), (0, -0.3, z0 + 0.3), "rust2", th, 0.03)
    # road-sign shield on the front-left, a house panel on its face
    K.part(S, "shield", (1.5, 0.1, 1.0), (0.0, 0.62, z0 + 0.9), "teal" if not dmg else "teal_s", th, 0.03)
    K.part(S, "shieldrim", (1.58, 0.12, 0.1), (0.0, 0.62, z0 + 1.42), "ochre", th, 0.0, nocast=True)
    K.part(S, "shieldfoot", (1.2, 0.2, 0.2), (0.0, 0.58, z0 + 0.38), "rust", th, 0.03)
    if not dmg:
        K.part(S, "shieldhouse", (0.7, 0.04, 0.45), (-0.3, 0.7, z0 + 1.0), "house", th, 0.0)
        K.part(S, "shieldbolt", (0.1, 0.06, 0.1), (0.55, 0.69, z0 + 1.25), "steel", th, 0.0, nocast=True)
    else:
        K.part(S, "shieldhouse", (0.5, 0.04, 0.4), (-0.4, 0.7, z0 + 0.9), "house", th, 0.0)
        K.part(S, "shieldgash", (0.5, 0.12, 0.3), (0.4, 0.65, z0 + 1.2), "char", th, 0.0, nocast=True)
    rec = 0.15 if flash == 1 else (0.06 if flash == 2 else 0.0)
    ln = 1.5 if not dmg else 1.0
    # long rail with a drum magazine and a bundle of spikes
    K.part(S, "rail", (0.16, ln, 0.16), (0, 0.2 + ln / 2 - rec, z0 + 0.62), "steel", th, 0.02)
    K.part(S, "rail2", (0.1, ln * 0.9, 0.1), (0.2, 0.2 + ln * 0.45 - rec, z0 + 0.58), "gunmetal", th, 0.0, nocast=True)
    K.part(S, "rail3", (0.1, ln * 0.9, 0.1), (-0.2, 0.2 + ln * 0.45 - rec, z0 + 0.58), "gunmetal", th, 0.0, nocast=True)
    if not dmg:
        K.cpart(S, "magazine", 0.3, 0.45, (0.0, -0.25, z0 + 0.95), "rust", th, verts=12)
        K.cpart(S, "magband", 0.32, 0.08, (0.0, -0.25, z0 + 1.0), "hazard", th, verts=12)
        K.part(S, "spikes", (0.28, 1.1, 0.1), (0, 0.5 - rec, z0 + 0.8), "rust2", th, 0.0)
        K.part(S, "spiketip", (0.04, 0.4, 0.04), (0, 1.15 - rec, z0 + 0.7), "steel", th, 0.0, nocast=True)
        K.cpart(S, "winch", 0.2, 0.3, (0.55, -0.15, z0 + 0.55), "rope", th, axis="Y", verts=10)
    else:
        K.part(S, "magfall", (0.5, 0.5, 0.4), (0.6, -0.4, z0 + 0.35), "rust_s", th, 0.04, pitch=15)
        K.part(S, "spikesb", (0.2, 0.6, 0.08), (0.1, 0.5, z0 + 0.8), "rust2", th, 0.0, pitch=-20)
    if flash and not dmg:
        tip = 0.2 + ln - rec
        sz = 0.35 if flash == 1 else 0.2
        K.part(S, "fl", (0.18 if flash == 1 else 0.1, sz, 0.18 if flash == 1 else 0.1), (0, tip + sz / 2, z0 + 0.62), "spark", th, 0.0, nocast=True)
        if flash == 1:
            K.part(S, "fl2", (0.4, 0.12, 0.4), (0, tip + 0.05, z0 + 0.62), "flame", th, 0.0, nocast=True)
            K.part(S, "flyspike", (0.05, 0.3, 0.05), (0, tip + 0.3, z0 + 0.62), "steel", th, 0.0, nocast=True)


def site(S):
    S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.1, "dust", 0.03)
    for sx, sy in ((-1.4, -1.4), (1.4, -1.4), (-1.4, 1.4), (1.4, 1.4)):
        K.post(S, "stake", sx, sy, 0.1, 0.9, 0.1, "wood")
    S.cyl("tyre", 0.34, 0.3, (-0.6, 0.6, 0.25), "rubber", verts=12, stretch=False)
    S.bb("planks", 0.2, 1.1, -0.6, 0.2, 0.1, 0.3, "wood", 0.0)


def build(S, state, i, n):
    K.turret_build(S, state, i, base, head, site, hide_prefixes=("spike", "mag", "winch", "shieldhouse", "shieldrim", "lamp", "fl"))
