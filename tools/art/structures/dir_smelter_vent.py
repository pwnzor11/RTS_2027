"""Directorate Smelter Vent: a refinery that is also a bunker entrance (3x2 cells). Original design.

The Directorate lives underground, so its refinery is mostly a hole: a brutalist concrete hood over a ramp that descends
below ground to a pair of blast doors, a furnace block feeding a tall smelter chimney that vents ash, short roof vents, and a
loudspeaker pair on a pole.  House colour (magenta, remapped in game): the broad banding across the hood front and the roof
panel.  Fixed Directorate oxide red and bone-white stencil panels are body paint (Directorate palette PAL_DIR).
Idle loop: ash puffs rise from the chimney, furnace slits flicker.  Damage is structural only (D-069): collapsed hood corner,
snapped chimney, toppled loudspeaker pole, buckled blast door; fire / smoke are FX anchors.  Build stages come from the shared
`stages()` driver of auth_conyard (primer model cut at a rising height inside a steel skeleton).
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x in [-5.1, 5.1],
y in [-3.4, 3.4].  The ramp trench dips below z = 0 (the ground plane is not rendered, so the trench is built from solids).
"""
import importlib.util
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_HERE))
import common as _C
_spec = importlib.util.spec_from_file_location("structlib_auth_conyard", os.path.join(_HERE, "auth_conyard.py"))
L = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(L)

ID = "dir_smelter_vent"
TITLE = "Directorate Smelter Vent"
FOOTPRINT = (3, 2)
CANVAS = (192, 232)
ORIGIN = (0, 88)        # headroom for the chimney, loudspeaker pole and ash puffs
DOCK = (1, 2)           # harvesters park in the cell below the middle and drive down the ramp
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True
PALETTE = _C.PAL_DIR
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]

MATERIALS = dict(L.MATERIALS)
MATERIALS.update({
    "pad": ("#6F695F", dict(rough=0.95)), "concrete": ("#A39B8C", dict(rough=0.95)), "concrete2": ("#756D62", dict(rough=0.95)),
    "roof": ("#625B53", dict(rough=0.9)), "hull": ("#7E766B", dict(rough=0.8)), "hull2": ("#665F55", dict(rough=0.8)),
    "white": ("#E8DFC8", dict(rough=0.8)), "bone": ("#E8DFC8", dict(rough=0.8)),
    "oxide": ("#A33426", dict(rough=0.8)), "oxide2": ("#5E2018", dict(rough=0.85)),
    "door": ("#44413E", dict(rough=0.5, metal=0.5)),
    "smoke": ("#8C857A", dict(rough=1.0)), "smoke2": ("#4F4943", dict(rough=1.0)),
    "concrete_s": ("#7A7367", dict(rough=0.95)), "concrete2_s": ("#544E46", dict(rough=0.95)),
    "roof_s": ("#46413B", dict(rough=0.9)), "hull_s": ("#5F584E", dict(rough=0.8)), "hull2_s": ("#4D473F", dict(rough=0.8)),
    "pad_s": ("#4D4943", dict(rough=0.95)), "bone_s": ("#9A927F", dict(rough=0.8)), "oxide_s": ("#5E241C", dict(rough=0.8)),
    "oxide2_s": ("#3E1612", dict(rough=0.85)), "door_s": ("#2B2A29", dict(rough=0.5, metal=0.5)),
})
SOOT_SKIN = dict(L.SOOT_SKIN)
SOOT_SKIN.update({"white": "bone_s", "bone": "bone_s", "oxide": "oxide_s", "oxide2": "oxide2_s", "door": "door_s"})
PRIMER_SKIN = dict(L.PRIMER_SKIN)
PRIMER_SKIN.update({"bone": "primer", "white": "primer", "oxide": "primer", "oxide2": "dark", "door": "dark", "lamp": "dark"})

HALL = (-4.0, 2.2, -0.9, 2.9)       # x0, x1, y0, y1 of the hall behind the hood
HALL_H = 2.3
TR = (-2.3, 0.5)                    # ramp trench x range
HOOD = (-3.4, 1.6, -2.7, -0.9)      # hood slab x0, x1, y0, y1
HOOD_Z = (2.2, 2.8)
STACK = (3.3, 1.4)
TR_Y0, TR_Y1, TR_BOT = -3.1, -1.0, -1.15   # ramp runs from the pad (z 0.14) down to z = TR_BOT at the doors


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def tilted(S, name, x0, x1, ya, za, yb, zb, th, m, bevel=0.0, nocast=False):
    """Thin slab (x0..x1 wide, th thick) from point a to point b in the y/z plane (game y, stretched here)."""
    YS = S.YS
    dy, dz = (yb - ya) * YS, zb - za
    ln = math.hypot(dy, dz)
    ang = math.degrees(math.atan2(dz, dy))             # rotation about X: +ve raises the north end
    return S.box(name, (x1 - x0, ln, th), ((x0 + x1) / 2, (ya + yb) / 2 * YS, (za + zb) / 2), m, bevel,
                 rot=(ang, 0, 0), stretch=False, nocast=nocast)


def frame_anim(state, i):
    return i if state != "build" else 0


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    hx0, hx1, hy0, hy1 = HALL
    sx_, sy_ = STACK
    # ------------------------------------------------------------------ pad with the ramp trench cut out of it
    S.bb("pad", -5.0, TR[0], -3.3, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("pad", TR[1], 5.0, -3.3, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("pad", TR[0], TR[1], -0.9, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, TR[0], -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    S.bb("padlip", TR[1], 5.0, -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    for k in range(16):                                  # red / bone hazard bar along the south edge
        x0 = -4.8 + k * 0.6
        if TR[0] - 0.1 < x0 + 0.6 and x0 < TR[1] + 0.1:
            continue
        S.bb(f"haz{k}", x0, x0 + 0.6, -3.15, -2.8, 0.14, 0.2, "oxide" if k % 2 == 0 else "bone", 0.0)
    # trench: concrete cheek walls, ramp floor, dark landing, painted chevrons
    for xa, xb in ((TR[0] - 0.2, TR[0]), (TR[1], TR[1] + 0.2)):       # stepped cheeks follow the ramp (nothing hangs below the pad edge)
        for k in range(7):
            ya = TR_Y0 + (TR_Y1 - TR_Y0) * k / 7 - (0.2 if k == 0 else 0.0)
            yb = TR_Y0 + (TR_Y1 - TR_Y0) * (k + 1) / 7 + (0.1 if k == 6 else 0.0)
            zb = 0.14 + (TR_BOT - 0.14) * (k + 1) / 7 - 0.1
            S.bb("cheek", xa, xb, ya, yb, zb, 0.2, "concrete2", 0.0)
    S.bb("landing", TR[0], TR[1], -1.4, -0.9, -1.6, TR_BOT, "dark", 0.0)
    tilted(S, "ramp", TR[0], TR[1], TR_Y0, 0.14 - 0.06, TR_Y1, TR_BOT - 0.06, 0.12, "door")
    for k in range(7):                                   # chevron bars down the ramp (alternate bone / oxide)
        t = (k + 0.5) / 7
        ya, yb = TR_Y0 + (TR_Y1 - TR_Y0) * (t - 0.05), TR_Y0 + (TR_Y1 - TR_Y0) * (t + 0.05)
        za, zb = 0.14 + (TR_BOT - 0.14) * (t - 0.05) + 0.01, 0.14 + (TR_BOT - 0.14) * (t + 0.05) + 0.01
        tilted(S, f"chev{k}", TR[0] + 0.3, TR[1] - 0.3, ya, za, yb, zb, 0.1, "oxide" if k % 2 == 0 else "bone", nocast=True)
    for xa in (TR[0] + 0.05, TR[1] - 0.25):               # edge lines
        tilted(S, "rampline", xa, xa + 0.2, TR_Y0, 0.14 - 0.0, TR_Y1, TR_BOT - 0.0, 0.1, "bone", nocast=True)
    # ------------------------------------------------------------------ blast doors, set back at the foot of the ramp
    ydoor = hy0 + 0.02
    S.bb("doorframe", TR[0] - 0.05, TR[1] + 0.05, ydoor - 0.12, ydoor, TR_BOT - 0.05, 2.25, "concrete2", 0.0)
    for k, (xa, xb) in enumerate(((TR[0] + 0.1, -0.9), (-0.9, TR[1] - 0.1))):
        if dmg and k == 1:
            # east leaf buckled: bent inward and hanging crooked, black gap beside it
            S.box("doorB", (xb - xa - 0.1, 0.12, 2.2), ((xa + xb) / 2 + 0.2, (ydoor - 0.25) * YS, 0.1), "door", 0.0, rot=(0, 16, 6), stretch=False)
            S.bb("gap", -0.8, TR[1] - 0.1, ydoor - 0.08, ydoor - 0.04, TR_BOT, 2.0, "dark", 0.0, nocast=True)
            continue
        S.bb(f"door{k}", xa, xb, ydoor - 0.2, ydoor - 0.1, TR_BOT, 2.1, "door", 0.0)
        S.bb(f"doorstripe{k}", xa + 0.05, xb - 0.05, ydoor - 0.22, ydoor - 0.2, 0.55, 0.85, "oxide", 0.0, nocast=True)
        S.bb(f"doorstripe{k}", xa + 0.05, xb - 0.05, ydoor - 0.22, ydoor - 0.2, 1.35, 1.65, "oxide", 0.0, nocast=True)
        for r in range(3):                               # rivet column
            S.bb(f"sten{k}{r}", xa + 0.15 + r * 0.3, xa + 0.3 + r * 0.3, ydoor - 0.22, ydoor - 0.2, 0.1, 0.35, "bone", 0.0, nocast=True)
    S.bb("doorseam", -0.93, -0.87, ydoor - 0.22, ydoor - 0.19, TR_BOT, 2.1, "dark", 0.0, nocast=True)
    # ------------------------------------------------------------------ hall: concrete mass behind the hood, roof, pilasters
    S.bb("hall", hx0, hx1, hy0, hy1, 0.14, HALL_H, "concrete", 0.05)
    S.bb("hplinth", hx0 - 0.1, hx1 + 0.1, hy0 - 0.1, hy1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("hroof", hx0 - 0.2, hx1 + 0.2, hy0 - 0.2, hy1 + 0.2, HALL_H, HALL_H + 0.22, "roof", 0.05)
    zr = HALL_H + 0.22
    for sgn in (-1, 1):
        S.bb("hpar", (hx0 - 0.2) if sgn < 0 else hx1 + 0.06, (hx0 - 0.06) if sgn < 0 else hx1 + 0.2, hy0 - 0.2, hy1 + 0.2, zr, zr + 0.18, "concrete2", 0.02)
    S.bb("hparN", hx0 - 0.2, hx1 + 0.2, hy1 + 0.06, hy1 + 0.2, zr, zr + 0.18, "concrete2", 0.02)
    # house marking on the roof and bone / red banding on the visible hall flanks
    hx_hi = hx0 + 2.3 if dmg else hx0 + 3.0
    S.bb("roofhouse", hx0 + 0.5, hx_hi, 0.0, 1.6, zr, zr + 0.04, "house2", 0.0)
    S.bb("roofframe", hx0 + 0.4, hx_hi + 0.1, -0.1, 1.7, zr, zr + 0.02, "dark", 0.0)
    for sgn, xa, xb in ((-1, hx0, TR[0] - 0.7), (1, 1.6, hx1)):
        S.bb(f"flank{sgn}", xa, xb, hy0 - 0.06, hy0, 0.5, 1.9, "concrete2", 0.0)
        S.bb(f"flankband{sgn}", xa, xb, hy0 - 0.1, hy0 - 0.06, 1.1, 1.35, "oxide", 0.0)
        for k in range(3):
            S.bb(f"sten_f{sgn}{k}", xa + 0.1 + k * 0.17, xa + 0.2 + k * 0.17, hy0 - 0.1, hy0 - 0.06, 0.62, 0.95, "bone", 0.0, nocast=True)
    # roof vents with louvred hoods and the intake block
    for k, (vx, vy, vh) in enumerate(((-3.0, 2.0, 0.9), (-2.1, 2.3, 1.3), (-1.2, 2.0, 0.7))):
        S.cyl(f"vent{k}", 0.26, vh, (vx, vy, zr + vh / 2), "hull2", verts=12)
        S.cyl(f"venth{k}", 0.38, 0.12, (vx, vy, zr + vh + 0.06), "steel", verts=12)
        S.cyl(f"ventd{k}", 0.2, 0.05, (vx, vy, zr + vh + 0.14), "dark", verts=10)
    S.bb("intake", -1.6, 0.2, 1.7, 2.7, zr, zr + 0.5, "concrete2", 0.04)
    for k in range(5):
        S.bb(f"grille{k}", -1.5 + k * 0.33, -1.35 + k * 0.33, 1.65, 2.7, zr + 0.5, zr + 0.57, "dark", 0.0)
    S.cyl("flue", 0.22, 3.5, (0.6, 1.4, zr + 0.55), "steel", axis="X", verts=12)   # flue pipe hall -> chimney base
    for k, xx in enumerate((-0.6, 0.9)):
        S.cyl(f"flange{k}", 0.3, 0.1, (xx, 1.4, zr + 0.55), "rust", axis="X", verts=12)
    # ------------------------------------------------------------------ the hood: slab on two piers, banding on its front face
    ha, hb, hya, hyb = HOOD
    if dmg:
        S.bb("hood", ha, -0.4, hya, hyb, HOOD_Z[0], HOOD_Z[1], "concrete", 0.05)             # west part still stands
        S.bb("hood", -0.4, 0.3, hya, -1.8, HOOD_Z[0], HOOD_Z[1], "concrete", 0.04)           # ragged remainder
        S.box("hoodslab", (1.5, 1.1, 0.5), (0.55, -1.55 * YS, 1.4), "concrete", 0.02, rot=(24, 0, 12), stretch=False)   # fallen chunk
        S.box("hoodslab2", (0.8, 0.7, 0.35), (1.3, -2.7 * YS, 0.35), "concrete2", 0.0, rot=(0, 0, -30), stretch=False)
        S.bb("hoodvoid", -0.4, 1.6, hya, hyb, HOOD_Z[0] - 0.02, HOOD_Z[0] + 0.02, "dark", 0.0, nocast=True)
        for k, (a, b, c, d) in enumerate(((-0.5, 0.2, -2.6, -2.4), (0.3, 0.9, -1.4, -1.0), (0.9, 1.5, -2.0, -1.5))):
            L.beam(S, f"rbar{k}", (a, c, 2.25), (b, d, 2.9 + 0.2 * k), 0.05, "rebar")
        S.bb("pierW", ha, ha + 0.8, -2.6, -1.9, 0.14, HOOD_Z[0], "concrete2", 0.04)
        S.bb("pierE", TR[1], TR[1] + 0.8, -2.6, -1.9, 0.14, 1.2, "concrete2", 0.04)           # east pier broken off at the knee
        S.box("pierEj", (0.5, 0.5, 0.4), (TR[1] + 0.5, -2.2 * YS, 1.35), "concrete2", 0.0, rot=(0, 20, 30), stretch=False)
        for k, (x, y, w, h) in enumerate(((0.8, -3.0, 0.5, 0.35), (1.5, -2.6, 0.4, 0.4), (-0.2, -3.2, 0.35, 0.3), (1.9, -1.8, 0.4, 0.3))):
            S.box(f"rub{k}", (w, w * 0.8, h), (x, y * YS, 0.14 + h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k), stretch=False)
        S.bb("banding", ha + 0.1, -0.5, hya - 0.06, hya - 0.02, 2.3, 2.7, "house", 0.0)         # banding, east end gone
    else:
        S.bb("hood", ha, hb, hya, hyb, HOOD_Z[0], HOOD_Z[1], "concrete", 0.05)
        S.bb("pierW", ha, ha + 0.8, -2.6, -1.9, 0.14, HOOD_Z[0], "concrete2", 0.04)
        S.bb("pierE", TR[1], TR[1] + 0.8, -2.6, -1.9, 0.14, HOOD_Z[0], "concrete2", 0.04)
        S.bb("banding", ha + 0.1, hb - 0.1, hya - 0.06, hya - 0.02, 2.3, 2.7, "house", 0.0)    # big house band across the hood front
        # roof lip and dark hood soffit strip over the opening
        S.bb("hoodlip", ha - 0.05, hb + 0.05, hya - 0.1, hya, HOOD_Z[1], HOOD_Z[1] + 0.16, "concrete2", 0.02)
    S.bb("soffit", TR[0], TR[1], hya + 0.05, -0.95, HOOD_Z[0] - 0.06, HOOD_Z[0], "dark", 0.0, nocast=True)
    # stencil panels on the pier fronts: bone plate with dark glyph bars, oxide cap stripe
    for sx0 in (ha, TR[1]):
        if dmg and sx0 == TR[1]:
            continue
        S.bb("plate", sx0 + 0.1, sx0 + 0.7, -2.64, -2.6, 0.7, 1.7, "bone", 0.0)
        S.bb("platecap", sx0 + 0.1, sx0 + 0.7, -2.65, -2.6, 1.7, 1.95, "oxide", 0.0)
        for k in range(3):
            S.bb(f"sten_p{k}", sx0 + 0.18, sx0 + 0.62, -2.66, -2.64, 0.82 + k * 0.28, 0.98 + k * 0.28, "dark", 0.0, nocast=True)
    # ------------------------------------------------------------------ furnace block + smelter chimney
    S.bb("furn", 2.35, 4.4, 0.1, 2.7, 0.14, 1.5, "concrete2", 0.04)
    S.bb("furnroof", 2.25, 4.5, 0.0, 2.8, 1.5, 1.65, "roof", 0.04)
    S.bb("furnband", 2.35, 4.4, 0.04, 0.1, 1.2, 1.4, "oxide", 0.0)
    for k in range(3):                                     # glowing slits flicker in the idle loop
        lit = (idle or mode == "build") and not dmg and ((k + i) % 3 != 0)
        S.bb(f"glow{k}", 2.6 + k * 0.55, 2.9 + k * 0.55, 0.0, 0.1, 0.45, 0.9, "lamp" if lit else "oxide2", 0.0, nocast=True)
    S.bb("chbase", sx_ - 0.7, sx_ + 0.7, sy_ - 0.7, sy_ + 0.7, 1.65, 2.0, "concrete2", 0.03)
    top = 4.0 if dmg else 5.7
    h0 = 1.65
    S.cone("chimney", 0.62, 0.42, top - h0, (sx_, sy_, (top + h0) / 2), "concrete", verts=16, nocast=False)
    for k, (zz, m) in enumerate(((2.5, "bone"), (3.2, "oxide"), (3.9, "bone"), (4.6, "oxide"), (5.3, "bone"))):
        if zz < top - 0.15:
            S.cone(f"cband{k}", 0.6 - 0.05 * (zz - 1.65) / 1.0 * 0.28, 0.58 - 0.05 * (zz - 1.65) / 1.0 * 0.28, 0.22, (sx_, sy_, zz), m, verts=16, nocast=False)
    if not dmg:
        S.cyl("ccollar", 0.55, 0.2, (sx_, sy_, top + 0.1), "steel", verts=16)
        S.cyl("cmouth", 0.4, 0.08, (sx_, sy_, top + 0.21), "dark", verts=12)
        if idle or mode == "build":
            for k in range(3):                                   # ash puffs, a loop of 4 frames
                ph = ((i / 4) + k / 3) % 1
                r = 0.14 + 0.42 * math.sin(math.pi * ph) ** 0.7
                S.sphere(f"puff{k}", r, (sx_ + ph * 1.1, sy_, top + 0.45 + ph * 1.9 + r * 0.3), "smoke2" if k != 1 else "smoke", squash=0.85)
    else:
        S.cyl("cstump", 0.45, 0.18, (sx_, sy_, top + 0.05), "concrete2", verts=12)
        S.cyl("cmouthd", 0.32, 0.06, (sx_, sy_, top + 0.15), "dark", verts=10)
        tl = math.radians(-52)
        L_ = 1.6
        for nm, rr, along, m_ in (("chleanA", 0.46, 0.0, "concrete"),):
            o = S.cone(nm, 0.5, 0.4, L_, (0, 0, 0), m_, verts=14, nocast=False)
            o.location = (sx_ - 0.1 + (L_ / 2) * math.sin(tl) * 1.0, sy_ * YS, top + 0.12 + (L_ / 2) * math.cos(tl))
            o.rotation_euler = (0, tl, 0)
    S.bb("cladder", sx_ - 0.7, sx_ - 0.64, sy_ - 0.2, sy_ + 0.2, 2.0, min(top, 4.5) - 0.2, "dark", 0.0, nocast=True)
    # ------------------------------------------------------------------ loudspeaker pair on a pole, on the hood
    px_, py_ = -2.9, -2.2
    pz0 = HOOD_Z[1]
    if dmg:
        S.cyl("hpole", 0.07, 1.3, (px_, py_, pz0 + 0.65), "steel", verts=8)
        # the upper pole and horns have fallen across the hood
        S.box("hpoleF", (1.8, 0.08, 0.08), (-2.0, -1.8 * YS, pz0 + 0.12), "steel", 0.0, rot=(0, 0, 20), stretch=False)
        S.cone("hornF", 0.1, 0.42, 0.8, (-1.3, -1.4, pz0 + 0.4), "steel", verts=10, rot=(0, 90, 20), nocast=False)
    else:
        S.cyl("hpole", 0.07, 3.0, (px_, py_, pz0 + 1.5), "steel", verts=8)
        S.bb("hpoleb", px_ - 0.2, px_ + 0.2, py_ - 0.2, py_ + 0.2, pz0, pz0 + 0.3, "concrete2", 0.02)
        S.bb("harm", px_ - 0.55, px_ + 0.55, py_ - 0.05, py_ + 0.05, pz0 + 2.85, pz0 + 2.93, "steel", 0.0)
        for sgn in (-1, 1):
            S.cone(f"horn{sgn}", 0.1, 0.42, 0.85, (px_ + sgn * 0.42, py_ - 0.35, pz0 + 3.05), "door", verts=12, rot=(90, 0, 0), nocast=False)
            S.cyl(f"hdrv{sgn}", 0.14, 0.3, (px_ + sgn * 0.42, py_ + 0.28, pz0 + 3.05), "oxide", axis="Y", verts=10)
            S.cyl(f"hrim{sgn}", 0.45, 0.04, (px_ + sgn * 0.42, py_ - 0.76, pz0 + 3.05), "bone", axis="Y", verts=12)
    # ------------------------------------------------------------------ rust / ash streaks, ash drift on the pad
    for k, (rx, rw, rh) in enumerate(((-3.7, 0.16, 0.8), (1.8, 0.14, 0.6), (-1.8, 0.14, 0.5))):
        S.bb(f"streak{k}", rx, rx + rw, hy0 - 0.12, hy0 - 0.1, 1.9 - rh, 1.9, "concrete2", 0.0, nocast=True)
    for k, (ax_, ay_, aw) in enumerate(((3.5, -1.5, 1.2), (-4.2, -0.6, 0.9), (4.2, 3.0, 0.8))):
        S.bb(f"ash{k}", ax_ - aw / 2, ax_ + aw / 2, ay_ - 0.3, ay_ + 0.3, 0.14, 0.17, "smoke", 0.0, nocast=True)
    if dmg:
        L.R.scorch(S, "scH", -1.2, hya - 0.12, 2.5, 1.6, 0.5)
        L.R.scorch(S, "scF", 3.3, 0.06, 0.9, 1.6, 0.6)


# ---------------------------------------------------------------------------------------------- build stages / damage FX
def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 3.4, 4.2, -2.9, -2.1, 0.14, 0.8, "crate", 0.03)
        S.bb("crate", 3.55, 4.1, -2.8, -2.2, 0.8, 1.3, "crate", 0.03, rot=(0, 0, 12))
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (3.0, -2.3, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", -4.6, -3.0, -1.6, -1.4, 0.14, 0.3, "rebar", 0.0)
        S.cyl("barrel", 0.3, 0.7, (-4.4, -2.5, 0.5), "rust", verts=12)
    S.bb("planks", -4.6, -3.2, 2.5, 2.9, 0.14, 0.5, "timber", 0.0)


def _left(S):
    S.bb("planks", -4.5, -3.2, -2.9, -2.5, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 2.0, (3.4, -2.6, 0.3), "steel", axis="X", verts=10)
    S.bb("crate", 3.5, 4.3, -2.9, -2.2, 0.14, 0.8, "crate", 0.03)
    L.spark(S, -1.0, -3.0, 2.0, 3, 6)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(0.6, -1.6, 2.3)[0], y=anchor(0.6, -1.6, 2.3)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(1.4, -2.0, 0.5)[0], y=anchor(1.4, -2.0, 0.5)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(-0.4, -1.0, 0.3)[0], y=anchor(-0.4, -1.0, 0.3)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=anchor(3.3, 1.4, 4.1)[0], y=anchor(3.3, 1.4, 4.1)[1], phase=6),
    dict(state="damaged", kind="smoke_plume", x=anchor(0.6, -1.6, 2.4)[0], y=anchor(0.6, -1.6, 2.4)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(3.3, 1.4, 4.2)[0], y=anchor(3.3, 1.4, 4.2)[1], phase=5),
]

SPEC = dict(
    pad=(-5.0, 5.0, -3.3, 3.3), env=(-4.1, 4.5, -2.8, 2.9), H=3.0,
    boxes=[(-4.0, 2.2, -0.9, 2.9), (-3.2, 1.4, -2.7, -0.9)], rings=[(STACK[0], STACK[1], 0.7)],
    clips=(1.3, 2.5), scaf=[(-4.7, -3.6, -3.1, -2.5), (2.2, 3.4, -3.1, -2.5)],
    derrick=(4.4, -1.2, 4.2, (3.4, -0.4, 4.4)), sparks=[(-2.0, -2.0, 2.3), (2.0, 0.4, 1.5), (-3.0, 1.5, 2.2)],
    hide=("sten", "glow", "puff", "horn", "hdrv", "hrim", "harm", "hpole"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
