"""Left Behind Harpoon Post (1x1): anti-armour defence, a swivelling harpoon gun on a tyre tower.  Original design.

A tower of stacked tyres and a rust-red scaffold platform in the dust; on the platform's turntable a swivelling head: a long steel
harpoon tube with a hazard-yellow barbed head poking out of the muzzle, a big ochre cable drum winch at the back with a rope
coil, lashed drum counterweights, a canvas gun shield with a house panel, a small pole with a house-colour pennant that turns with
the head.  The head ROTATES like auth_turret (facing 0 = north, clockwise, 22.5 deg steps), built with dir_def_build.py.
STATES are flat frame counts for build_structure.render():
  idle 16 facings x 1, fire 16 x 2 (k0 harpoon leaves with a flash + recoil and a taut cable, k1 flash dying), damaged 16 x 1
  (structural: bent tube, torn shield, scaffold leg gone), build 1 x 6 (stage 5 = idle 0).
Contract: see auth_turret.py / auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_harpoon_post"
TITLE = "Left Behind Harpoon Post"
FOOTPRINT = (1, 1)
CANVAS = (96, 136)
ORIGIN = (16, 48)
DOCK = (0, 1)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
FACINGS = 16
STATES = [("idle", 16, 1), ("fire", 32, 1), ("damaged", 16, 1), ("build", 6, 5)]
MANIFEST_STATES = [("idle", 16, 1, 1), ("fire", 16, 2, 2), ("damaged", 16, 1, 1), ("build", 1, 6, 5)]
ZH = 1.9      # turntable height (top of the platform)


def px(x, y, z):
    return round(ORIGIN[0] + 32 + x * 18.8), round(ORIGIN[1] + 32 - y * 18.8 * math.sin(math.radians(40)) - z * 14.4)


_hx, _hy = px(0.0, 0.0, ZH + 0.8)
FX = [
    dict(state="damaged", kind="fire_small", x=_hx, y=_hy, phase=0),
    dict(state="damaged", kind="smoke_plume", x=_hx, y=_hy - 2, phase=2),
]


def base(S, dmg=False):
    S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.1, "dust", 0.03)
    # tyre tower (three courses) with a scaffold platform on top
    for c in range(3):
        for k in range(4):
            a = math.radians(k * 90 + 45 + c * 20)
            S.cyl(f"tyre{c}{k}", 0.4, 0.34, (0.55 * math.sin(a), 0.55 * math.cos(a), 0.1 + c * 0.34), "rubber", verts=12, stretch=False)
    for sx in (-0.8, 0.8):
        for sy in (-0.8, 0.8):
            if dmg and sx > 0 and sy < 0:
                continue
            K.post(S, "postleg", sx, sy, 0.1, ZH - 0.1, 0.14, "rust2")
    S.bb("platform", -1.0, 1.0, -1.0, 1.0, ZH - 0.12, ZH, "wood", 0.02)
    S.bb("platrail", -1.0, 1.0, -1.0, -0.9, ZH, ZH + 0.35, "ash2", 0.0, nocast=True)
    K.beam(S, "brace", (-0.8, -0.8, 0.3), (0.8, -0.8, ZH - 0.2), 0.07, "rust2")
    S.cyl("turn", 0.75, 0.1, (0, 0, ZH + 0.05), "steel", verts=20, stretch=False)
    S.cyl("d0", 0.3, 0.6, (1.2, -1.1, 0.4), "rust", verts=10, stretch=False)
    S.bb("crate", -1.5, -1.0, -1.5, -1.0, 0.1, 0.5, "wood", 0.03)
    if dmg:
        S.box("chunk", (0.5, 0.4, 0.2), (-1.1, 0.4, 0.2), "ash2", 0.0, rot=(0, 0, 25), stretch=False)
        S.box("legfall", (1.2, 0.14, 0.14), (1.0, -1.1 * S.YS, 0.2), "rust2", 0.0, rot=(0, 0, 40), stretch=False)


def head(S, th, dmg=False, flash=0):
    z0 = ZH + 0.1
    K.cpart(S, "collar", 0.55, 0.18, (0, 0, z0 + 0.09), "ash2", th, verts=16)
    K.part(S, "cradle", (0.7, 1.1, 0.3), (0, 0.0, z0 + 0.35), "rust2", th, 0.04)
    rec = 0.18 if flash == 1 else (0.07 if flash == 2 else 0.0)
    ln = 1.6 if not dmg else 1.0
    # harpoon tube with a heavy breech block, a barbed head, a drum winch at the back and counterweight drums
    pitch = 0 if not dmg else -14
    K.part(S, "tube", (0.3, ln, 0.3), (0, 0.15 + ln / 2 - rec, z0 + 0.65), "steel", th, 0.03, pitch=pitch)
    K.part(S, "tubeband", (0.34, 0.12, 0.34), (0, 0.5 - rec, z0 + 0.65), "rust", th, 0.0, nocast=True, pitch=pitch)
    K.part(S, "breech", (0.5, 0.5, 0.5), (0, -0.2, z0 + 0.65), "gunmetal", th, 0.04)
    if not dmg:
        K.part(S, "barb", (0.12, 0.35, 0.12), (0, 0.15 + ln + 0.1 - rec, z0 + 0.65), "hazard", th, 0.0, nocast=True)
        K.part(S, "barbfin", (0.4, 0.12, 0.05), (0, 0.15 + ln - 0.05 - rec, z0 + 0.65), "hazard", th, 0.0, nocast=True)
        K.part(S, "winchdrum", (0.7, 0.5, 0.5), (0, -0.78, z0 + 0.65), "ochre", th, 0.12)
        K.part(S, "rope", (0.74, 0.1, 0.54), (0, -0.78, z0 + 0.65), "rope", th, 0.0, nocast=True)
        K.part(S, "cw", (0.4, 0.4, 0.4), (-0.55, -0.55, z0 + 0.35), "rust", th, 0.05)
        K.part(S, "cw2", (0.3, 0.3, 0.45), (0.55, -0.5, z0 + 0.35), "teal", th, 0.05)
        K.part(S, "pole", (0.05, 0.05, 0.9), (0.55, -0.5, z0 + 0.95), "wood", th, 0.0)
        K.part(S, "pennant", (0.5, 0.04, 0.3), (0.82, -0.5, z0 + 1.25), "house", th, 0.0)
    else:
        K.part(S, "barb", (0.12, 0.3, 0.12), (0, 0.15 + ln + 0.05, z0 + 0.45), "hazard", th, 0.0, nocast=True)
        K.part(S, "winchdrum", (0.7, 0.5, 0.5), (0, -0.78, z0 + 0.65), "ochre_s", th, 0.12)
        K.part(S, "pennant", (0.3, 0.04, 0.3), (0.55, -0.5, z0 + 0.5), "house", th, 0.0, pitch=-35)
    # canvas gun shield on the left of the tube
    K.part(S, "shield", (0.1, 1.0, 0.9), (-0.45, 0.35, z0 + 0.85), "canvas2" if not dmg else "canvas2_s", th, 0.0)
    K.part(S, "shieldframe", (0.12, 1.06, 0.1), (-0.45, 0.35, z0 + 1.3), "wood", th, 0.0, nocast=True)
    if not dmg:
        K.part(S, "shieldhouse", (0.04, 0.6, 0.4), (-0.52, 0.4, z0 + 0.85), "house", th, 0.0)
    else:
        K.part(S, "shieldtear", (0.12, 0.5, 0.4), (-0.45, 0.4, z0 + 0.9), "char", th, 0.0, nocast=True)
    if flash and not dmg:
        tip = 0.15 + ln + 0.2 - rec
        K.part(S, "fl", (0.2 if flash == 1 else 0.12, 0.4 if flash == 1 else 0.22, 0.2 if flash == 1 else 0.12), (0, tip + 0.25, z0 + 0.65), "spark", th, 0.0, nocast=True)
        if flash == 1:
            K.part(S, "fl2", (0.45, 0.12, 0.45), (0, tip + 0.05, z0 + 0.65), "flame", th, 0.0, nocast=True)
            K.part(S, "cable", (0.04, 0.5, 0.04), (0, tip + 0.5, z0 + 0.65), "dark", th, 0.0, nocast=True)


def site(S):
    S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.1, "dust", 0.03)
    for sx, sy in ((-1.4, -1.4), (1.4, -1.4), (-1.4, 1.4), (1.4, 1.4)):
        K.post(S, "stake", sx, sy, 0.1, 0.9, 0.1, "wood")
    for k in range(3):
        S.cyl(f"tyre{k}", 0.4, 0.34, (-0.6 + k * 0.7, 0.9, 0.27), "rubber", verts=12, stretch=False)
    S.bb("planks", 0.2, 1.2, -0.9, -0.2, 0.1, 0.3, "wood", 0.0)


def build(S, state, i, n):
    K.turret_build(S, state, i, base, head, site, hide_prefixes=("barb", "winch", "rope", "pennant", "pole", "shieldhouse", "fl", "cable", "shieldframe"))
