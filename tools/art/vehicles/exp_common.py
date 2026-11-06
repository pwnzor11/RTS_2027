"""Shared Expedition vehicle parts (PAL_EXP) for tools/art/build_vehicle.py modules.  Import with
sys.path.insert(0, <this dir>); `import exp_common as E`.  Atompunk 1962 (design 07 sec. 8): deep navy, lagoon white, brushed
chrome with copper trim, signal orange for lights and markings, team colour on hull stripes and fin flashes ("house" material).
Hovercraft: rubber skirts with a grey ash tidemark; turbine nacelles; bubble canopies; white grotesque hull numbers.  Original design.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C

# Expedition palette: 24 faction colours (index 0 outline, index 1 shadow colour keep their roles) + the shared 8 house shades.
PAL_EXP = [C.OUTLINE] + [C.hx(c) for c in (
    "#1A2333 "                                      # shadow (index 1)
    "#30343A #1E3158 #2B4A82 #3E64A8 #6286C4 "      # deep navy ramp
    "#8FA6AC #BDD0D2 #E2EEEA #F8FCF7 "              # lagoon white ramp
    "#4F575F #79838C #A4AEB6 #CDD5DA "              # brushed chrome
    "#6A3A22 #A25F30 #D18D4C "                      # copper trim
    "#B8481A #F2751F #FFB45E "                      # signal orange
    "#1C5468 #4CA0B4 "                              # canopy glass
    "#5E5C55").split()]                             # ash tidemark grey
assert len(PAL_EXP) == 24 and len(set(PAL_EXP)) == 24 and not (set(PAL_EXP) & set(C.HOUSE))
PALETTE = PAL_EXP

MATERIALS = {
    "navy": ("#2B4A82", dict(rough=0.35, metal=0.15)), "navy2": ("#1B2D52", dict(rough=0.4, metal=0.1)),
    "white": ("#E6F0EC", dict(rough=0.4)), "white2": ("#B2C6C8", dict(rough=0.45)),
    "chrome": ("#A9B3BA", dict(rough=0.28, metal=0.85)), "chrome2": ("#6D767E", dict(rough=0.35, metal=0.8)),
    "copper": ("#B7692F", dict(rough=0.32, metal=0.8)),
    "orange": ("#F2751F", dict(rough=0.45)), "orange2": ("#B8481A", dict(rough=0.5)),
    "glass": ("#2B7F95", dict(rough=0.12, metal=0.4)),
    "ash": ("#5E5C55", dict(rough=1.0)), "rubber": ("#3A3E44", dict(rough=0.95)), "dark": ("#1D2024", dict(rough=0.9)),
    "lens": ("#FFB45E", dict(rough=0.3, emit="#FFB45E")),
}

# 3x5 grotesque digits for hull numbers (rows top to bottom)
FONT = {"0": "111101101101111", "1": "010110010010111", "2": "111001111100111", "3": "111001111001111", "4": "101101111001001",
        "5": "111100111001111", "6": "111100111101111", "7": "111001001001001", "8": "111101111101111", "9": "111101111001111"}


def digits(S, tag, text, x, y, z, px=0.07, mat="white", flat=True):
    """Hull number laid flat on a deck (flat=True: reads along +X, top of the glyph toward +Y) or on a north-facing plate."""
    w = 3 * px + px
    x0 = x - (len(text) * w - px) / 2
    for ci, ch in enumerate(text):
        g = FONT[ch]
        for r in range(5):
            for c in range(3):
                if g[r * 3 + c] == "1":
                    cx = x0 + ci * w + c * px
                    if flat:
                        S.box(f"{tag}{ci}{r}{c}", (px, px, 0.012), (cx, y + (2 - r) * px, z), mat, 0.0)
                    else:
                        S.box(f"{tag}{ci}{r}{c}", (px, 0.012, px), (cx, y, z + (2 - r) * px), mat, 0.0)


def skirt(S, w, ln, y=0.0, h=0.42, lift=0.14):
    """Hovercraft skirt: ONE continuous rounded charcoal rubber bag (no wheels, no links) with vertical pleat lines, a grey ash tidemark
    band near the bottom and a dark plenum slab on top, so the hull above it visibly floats (gap = `lift`).  Hull parts go on top at z >= h + lift."""
    S.box("skirtflare", (w + 0.1, ln + 0.1, 0.2), (0, y, 0.1), "rubber", 0.09)
    S.box("skirtcore", (w, ln, h), (0, y, h / 2 + 0.02), "rubber", 0.22)
    S.box("tide", (w + 0.13, ln + 0.13, 0.09), (0, y, 0.2), "ash", 0.04)
    for s in (-1, 1):
        for k in range(int(ln / 0.55)):
            yy = y - ln / 2 + 0.35 + k * 0.55
            if abs(yy - y) < ln / 2 - 0.25:
                S.box(f"pleat{s}_{k}", (0.05, 0.05, h - 0.18), (s * (w / 2 + 0.035), yy, h / 2 + 0.12), "dark", 0.0)
    for e in (-1, 1):
        for k in range(int(w / 0.55)):
            xx = -w / 2 + 0.35 + k * 0.55
            if abs(xx) < w / 2 - 0.25:
                S.box(f"pleatE{e}_{k}", (0.05, 0.05, h - 0.18), (xx, y + e * (ln / 2 + 0.035), h / 2 + 0.12), "dark", 0.0)
    S.box("plenum", (w - 0.35, ln - 0.35, lift + 0.1), (0, y, h + lift / 2 - 0.02), "dark", 0.04)


class Scaled:
    """Proxy for the build Ctx that multiplies every size, radius and location by k (one model, any size); begin/mount/pose pass through."""

    def __init__(self, S, k):
        self.S, self.k = S, k

    def __getattr__(self, n):
        return getattr(self.S, n)

    def lift(self, dz):
        """Raise every primitive added from now on by dz (model units); lift(0) clears."""
        self.S.set_pose(dz=-dz * self.k) if dz else self.S.set_pose()

    def box(self, name, size, loc, m, bevel=0.03, rot=(0, 0, 0), nocast=False):
        k = self.k
        return self.S.box(name, tuple(v * k for v in size), tuple(v * k for v in loc), m, bevel * k, rot, nocast)

    def cyl(self, name, r, h, loc, m, axis="Z", verts=24, nocast=False, rot=None):
        k = self.k
        return self.S.cyl(name, r * k, h * k, tuple(v * k for v in loc), m, axis, verts, nocast, rot)

    def sphere(self, name, r, loc, m, scale=(1, 1, 1), nocast=False):
        k = self.k
        return self.S.sphere(name, r * k, tuple(v * k for v in loc), m, scale, nocast)

    def cone(self, name, r1, r2, h, loc, m, verts=16, rot=None):
        k = self.k
        return self.S.cone(name, r1 * k, r2 * k, h * k, tuple(v * k for v in loc), m, verts, rot)


def nacelle(S, tag, x, y, z, r=0.4, ln=1.0, trim="copper"):
    """Turbine nacelle along Y: chrome shell, dark intake with a hub at the front, copper ring, dark exhaust at the back."""
    S.cyl(f"nac{tag}", r, ln, (x, y, z), "chrome", "Y", 20)
    S.cyl(f"nacring{tag}", r + 0.03, 0.09, (x, y + ln / 2 - 0.12, z), trim, "Y", 20)
    S.cyl(f"nacin{tag}", r * 0.8, 0.05, (x, y + ln / 2 + 0.01, z), "dark", "Y", 18)
    S.cyl(f"nachub{tag}", r * 0.25, 0.09, (x, y + ln / 2 + 0.03, z), "chrome2", "Y", 10)
    S.cyl(f"nacout{tag}", r * 0.78, 0.05, (x, y - ln / 2 - 0.01, z), "dark", "Y", 18)


def lamp(S, tag, x, y, z, r=0.1):
    S.cyl(f"lamp{tag}", r, 0.07, (x, y, z), "lens", "Y", 10)
    S.cyl(f"lampbez{tag}", r * 1.35, 0.05, (x, y - 0.03, z), "copper", "Y", 10)
