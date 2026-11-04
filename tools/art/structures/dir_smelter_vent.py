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
    "pad": ("#7A6E5E", dict(rough=0.95)), "concrete": ("#B0A28C", dict(rough=0.95)), "concrete2": ("#7E7262", dict(rough=0.95)),
    "roof": ("#85735C", dict(rough=0.9)), "hull": ("#8A7C68", dict(rough=0.8)), "hull2": ("#6E6150", dict(rough=0.8)),
    "white": ("#E8DFC8", dict(rough=0.8)), "bone": ("#E8DFC8", dict(rough=0.8)),
    "oxide": ("#A33426", dict(rough=0.8)), "oxide2": ("#5E2018", dict(rough=0.85)),
    "door": ("#5A5148", dict(rough=0.6, metal=0.3)), "rampfloor": ("#3A332D", dict(rough=0.9)), "redglow": ("#FF4A2A", dict(rough=0.5, emit="#FF3A20")),
    "smoke": ("#8C857A", dict(rough=1.0)), "smoke2": ("#4F4943", dict(rough=1.0)),
    "concrete_s": ("#7A7367", dict(rough=0.95)), "concrete2_s": ("#544E46", dict(rough=0.95)),
    "roof_s": ("#46413B", dict(rough=0.9)), "hull_s": ("#5F584E", dict(rough=0.8)), "hull2_s": ("#4D473F", dict(rough=0.8)),
    "pad_s": ("#4D4943", dict(rough=0.95)), "bone_s": ("#9A927F", dict(rough=0.8)), "oxide_s": ("#5E241C", dict(rough=0.8)),
    "oxide2_s": ("#3E1612", dict(rough=0.85)), "door_s": ("#2B2A29", dict(rough=0.5, metal=0.5)),
})
SOOT_SKIN = dict(L.SOOT_SKIN)
SOOT_SKIN.update({"rampfloor": "door_s", "redglow": "oxide2_s", "white": "bone_s", "bone": "bone_s", "oxide": "oxide_s", "oxide2": "oxide2_s", "door": "door_s"})
PRIMER_SKIN = dict(L.PRIMER_SKIN)
PRIMER_SKIN.update({"bone": "primer", "white": "primer", "oxide": "primer", "oxide2": "dark", "door": "dark", "lamp": "dark", "redglow": "dark", "rampfloor": "dark"})

HALL = (-4.6, 2.2, -0.9, 2.9)       # x0, x1, y0, y1 of the hall behind the ramp mouth
HALL_H = 2.3
TR = (-3.6, 1.6)                    # ramp trench x range: 5.2 wide = just over half the 10.2-wide south face
HOOD = (-3.7, 1.7, -1.9, -0.9)      # small cantilevered hood over the foot of the ramp: x0, x1, y0, y1
HOOD_Z = (1.95, 2.3)
STACK = (3.3, 1.4)
TR_Y0, TR_Y1, TR_BOT = -3.1, -1.0, -1.15   # ramp runs from the pad (z 0.14) down to z = TR_BOT at the doors


def anchor(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


def ramp_z(y):
    return 0.14 + (TR_BOT - 0.14) * (y - TR_Y0) / (TR_Y1 - TR_Y0)


def tilted(S, name, x0, x1, ya, za, yb, zb, th, m, bevel=0.0, nocast=False):
    """Thin slab (x0..x1 wide, th thick) from point a to point b in the y/z plane (game y, stretched here)."""
    YS = S.YS
    dy, dz = (yb - ya) * YS, zb - za
    ln = math.hypot(dy, dz)
    ang = math.degrees(math.atan2(dz, dy))
    return S.box(name, (x1 - x0, ln, th), ((x0 + x1) / 2, (ya + yb) / 2 * YS, (za + zb) / 2), m, bevel,
                 rot=(ang, 0, 0), stretch=False, nocast=nocast)


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
    S.bb("pad", TR[0], TR[1], -3.3, TR_Y0, 0.0, 0.14, "pad", 0.02)         # pad strip across the mouth's south end
    S.bb("padlip", -5.0, 5.0, -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    for k in range(17):                                  # red / bone hazard bar along the south edge
        x0 = -5.0 + k * 0.6
        S.bb(f"haz{k}", x0, min(x0 + 0.6, 5.0), -3.15, -2.8, 0.14, 0.2, "oxide" if k % 2 == 0 else "bone", 0.0)
    # trench cheeks: stepped to follow the ramp so nothing hangs below the pad edge
    for xa, xb in ((TR[0] - 0.25, TR[0]), (TR[1], TR[1] + 0.25)):
        for k in range(7):
            ya = TR_Y0 + (TR_Y1 - TR_Y0) * k / 7
            yb = TR_Y0 + (TR_Y1 - TR_Y0) * (k + 1) / 7 + (0.1 if k == 6 else 0.0)
            zb = max(ramp_z(yb) - 0.12, 0.0) if k < 1 else ramp_z(yb) - 0.12
            S.bb("cheek", xa, xb, ya, yb, zb, 0.2, "concrete2", 0.0)
    S.bb("landing", TR[0], TR[1], -1.4, -0.9, -1.6, TR_BOT, "dark", 0.0)
    tilted(S, "ramp", TR[0], TR[1], TR_Y0, 0.14 - 0.06, TR_Y1, TR_BOT - 0.06, 0.12, "rampfloor")
    # stepped treads across the ramp: flat, alternating light / dark concrete, lit toward the top, dimming as it descends
    NST = 7
    for k in range(NST):
        ya, yb = TR_Y0 + (TR_Y1 - TR_Y0) * k / NST, TR_Y0 + (TR_Y1 - TR_Y0) * (k + 1) / NST - 0.07
        zt_ = ramp_z(ya + (yb - ya) / 2) + 0.03
        m = ("white", "hull", "concrete", "concrete2", "hull2", "rampfloor", "rampfloor")[k] if k % 2 == 0 else ("concrete2", "hull2", "rampfloor", "dark", "dark", "dark", "dark")[k]
        S.bb(f"tread{k}", TR[0] + 0.15, TR[1] - 0.15, ya, yb, zt_ - 0.2, zt_, m, 0.0)
    for xa in (TR[0] + 0.1, TR[1] - 0.3):
        tilted(S, "rampline", xa, xa + 0.2, TR_Y0, 0.14, TR_Y1, TR_BOT, 0.1, "hull", nocast=True)
    # ------------------------------------------------------------------ the mouth: dark recess, red-lit interior, doors swung open to the sides
    ydoor = hy0 + 0.02
    S.bb("mouth", TR[0], TR[1], ydoor - 0.04, ydoor, TR_BOT - 0.05, 1.95, "dark", 0.0)
    glow = "oxide2" if dmg else "redglow"
    # warm glow only deep at the far bottom of the ramp: a small patch low on the back wall + a sliver of floor
    S.bb("glowb", TR[0] + 2.0, TR[1] - 2.0, ydoor - 0.1, ydoor - 0.04, TR_BOT + 0.02, TR_BOT + 0.4, glow, 0.0, nocast=True)
    S.bb("glowfloor", TR[0] + 2.0, TR[1] - 2.0, ydoor - 0.3, ydoor - 0.1, TR_BOT + 0.0, TR_BOT + 0.04, glow, 0.0, nocast=True)
    S.bb("mouthlip", TR[0] - 0.1, TR[1] + 0.1, ydoor - 0.06, ydoor, 1.95, 2.1, "concrete2", 0.0)
    for k, (xa, xb) in enumerate(((TR[0], TR[0] + 0.1), (TR[1] - 0.1, TR[1]))):
        yz0, yz1 = ydoor - 1.9, ydoor - 0.1                # leaf folded flat against the cheek (thin slab, flush)
        if dmg and k == 1:
            S.box("doorB", (0.1, 1.2, 0.8), (xb - 0.25, (ydoor - 0.9) * YS, TR_BOT + 0.5), "door", 0.0, rot=(0, 0, 18), stretch=False)
            continue
        for q in range(5):                               # follows the ramp so nothing pokes out below it
            qa, qb = yz0 + (yz1 - yz0) * q / 5, yz0 + (yz1 - yz0) * (q + 1) / 5
            S.bb(f"door{k}", xa, xb, qa, qb, ramp_z(qb) - 0.04, ramp_z(qa) + 0.85, "door", 0.0)
        S.bb(f"doorband{k}", xa - 0.01 if k == 0 else xa, xb if k == 0 else xb + 0.01, yz0 + 0.1, yz1 - 0.1, TR_BOT + 0.45, TR_BOT + 0.6, "oxide", 0.0, nocast=True)
    # ------------------------------------------------------------------ hall: concrete mass behind the mouth, roof, parapets
    S.bb("hall", hx0, hx1, hy0, hy1, 0.14, HALL_H, "concrete", 0.05)
    S.bb("hplinth", hx0 - 0.1, hx1 + 0.1, hy0 - 0.1, hy1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    S.bb("hroof", hx0 - 0.2, hx1 + 0.2, hy0 - 0.2, hy1 + 0.2, HALL_H, HALL_H + 0.22, "roof", 0.05)
    zr = HALL_H + 0.22
    for sgn in (-1, 1):
        S.bb("hpar", (hx0 - 0.2) if sgn < 0 else hx1 + 0.06, (hx0 - 0.06) if sgn < 0 else hx1 + 0.2, hy0 - 0.2, hy1 + 0.2, zr, zr + 0.18, "concrete2", 0.02)
    S.bb("hparN", hx0 - 0.2, hx1 + 0.2, hy1 + 0.06, hy1 + 0.2, zr, zr + 0.18, "concrete2", 0.02)
    hx_hi = hx0 + 2.5 if dmg else hx0 + 3.3
    S.bb("roofhouse", hx0 + 0.6, hx_hi, 0.1, 1.7, zr, zr + 0.04, "house2", 0.0)
    S.bb("roofframe", hx0 + 0.5, hx_hi + 0.1, 0.0, 1.8, zr, zr + 0.02, "dark", 0.0)
    # flanks beside the mouth: bone stencil plate over an oxide band
    for sgn, xa, xb in ((-1, hx0 + 0.1, TR[0] - 0.3), (1, TR[1] + 0.3, hx1 - 0.05)):
        S.bb(f"flank{sgn}", xa, xb, hy0 - 0.06, hy0, 0.5, 1.95, "concrete2", 0.0)
        S.bb(f"flankband{sgn}", xa, xb, hy0 - 0.1, hy0 - 0.06, 0.55, 0.8, "oxide", 0.0)
        S.bb(f"flankplate{sgn}", xa + 0.05, xb - 0.05, hy0 - 0.1, hy0 - 0.06, 0.95, 1.8, "bone", 0.0)
        for k in range(3):
            S.bb(f"sten_f{sgn}{k}", xa + 0.1, xb - 0.1, hy0 - 0.12, hy0 - 0.1, 1.05 + k * 0.25, 1.17 + k * 0.25, "dark", 0.0, nocast=True)
    # roof vents with louvred hoods and the intake block
    for k, (vx, vy, vh) in enumerate(((-3.0, 2.0, 0.9), (-2.1, 2.3, 1.3), (-1.2, 2.0, 0.7))):
        S.cyl(f"vent{k}", 0.26, vh, (vx, vy, zr + vh / 2), "hull2", verts=12)
        S.cyl(f"venth{k}", 0.38, 0.12, (vx, vy, zr + vh + 0.06), "steel", verts=12)
        S.cyl(f"ventd{k}", 0.2, 0.05, (vx, vy, zr + vh + 0.14), "dark", verts=10)
    S.bb("intake", -1.6, 0.2, 1.7, 2.7, zr, zr + 0.5, "concrete2", 0.04)
    for k in range(5):
        S.bb(f"grille{k}", -1.5 + k * 0.33, -1.35 + k * 0.33, 1.65, 2.7, zr + 0.5, zr + 0.57, "dark", 0.0)
    S.cyl("flue", 0.22, 3.5, (0.6, 1.4, zr + 0.55), "steel", axis="X", verts=12)
    for k, xx in enumerate((-0.6, 0.9)):
        S.cyl(f"flange{k}", 0.3, 0.1, (xx, 1.4, zr + 0.55), "rust", axis="X", verts=12)
    # ------------------------------------------------------------------ small hood: a thin cantilevered lintel over the foot of the ramp
    ha, hb, hya, hyb = HOOD
    if dmg:
        S.bb("hood", ha, -0.9, hya, hyb, HOOD_Z[0], HOOD_Z[1], "concrete2", 0.04)
        S.box("hoodslab", (1.2, 0.9, 0.35), (2.1, -1.9 * YS, 0.0), "concrete2", 0.02, rot=(0, 0, 14), stretch=False)
        S.box("hoodslab2", (0.7, 0.6, 0.3), (2.3, -2.5 * YS, 0.35), "concrete2", 0.0, rot=(0, 0, -30), stretch=False)
        for k, (a_, b_, c_, d_) in enumerate(((-0.9, -0.5, -1.8, -1.4), (-0.8, -0.3, -1.2, -1.0))):
            L.beam(S, f"rbar{k}", (a_, c_, 2.1), (b_, d_, 2.6 + 0.2 * k), 0.05, "rebar")
        S.bb("banding", ha + 0.1, -0.95, hya - 0.06, hya - 0.02, 1.98, 2.27, "house", 0.0)
        for k, (x, y, w, h) in enumerate(((-3.0, -2.2, 0.4, 0.3), (1.2, -2.0, 0.35, 0.28), (-3.1, -1.6, 0.3, 0.25), (1.3, -1.5, 0.3, 0.22),
                                           (0.3, -3.0, 0.5, 0.35), (-2.7, -2.9, 0.4, 0.4), (-0.5, -1.6, 0.28, 0.2), (2.2, -2.9, 0.5, 0.3))):
            S.box(f"rub{k}", (w, w * 0.8, h), (x, y * YS, (ramp_z(y) if TR[0] < x < TR[1] and TR_Y0 < y < TR_Y1 else 0.14) + h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k), stretch=False)
    else:
        S.bb("hood", ha, hb, hya, hyb, HOOD_Z[0], HOOD_Z[1], "concrete2", 0.04)
        S.bb("banding", ha + 0.1, hb - 0.1, hya - 0.06, hya - 0.02, 1.98, 2.27, "house", 0.0)   # house colour band across the hood front
    # ------------------------------------------------------------------ furnace block + smelter chimney
    S.bb("furn", 2.35, 4.4, 0.1, 2.7, 0.14, 1.5, "concrete2", 0.04)
    S.bb("furnroof", 2.25, 4.5, 0.0, 2.8, 1.5, 1.65, "roof", 0.04)
    S.bb("furnband", 2.35, 4.4, 0.04, 0.1, 1.2, 1.4, "oxide", 0.0)
    for k in range(3):
        lit = (idle or mode == "build") and not dmg and ((k + i) % 3 != 0)
        S.bb(f"glow{k}", 2.6 + k * 0.55, 2.9 + k * 0.55, 0.0, 0.1, 0.45, 0.9, "redglow" if lit else "oxide2", 0.0, nocast=True)
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
            for k in range(3):
                ph = ((i / 4) + k / 3) % 1
                r = 0.14 + 0.42 * math.sin(math.pi * ph) ** 0.7
                S.sphere(f"puff{k}", r, (sx_ + ph * 1.1, sy_, top + 0.45 + ph * 1.9 + r * 0.3), "smoke2" if k != 1 else "smoke", squash=0.85)
    else:
        S.cyl("cstump", 0.45, 0.18, (sx_, sy_, top + 0.05), "concrete2", verts=12)
        S.cyl("cmouthd", 0.32, 0.06, (sx_, sy_, top + 0.15), "dark", verts=10)
        tl = math.radians(-52)
        L_ = 1.6
        o = S.cone("chleanA", 0.5, 0.4, L_, (0, 0, 0), "concrete", verts=14, nocast=False)
        o.location = (sx_ - 0.1 + (L_ / 2) * math.sin(tl), sy_ * YS, top + 0.12 + (L_ / 2) * math.cos(tl))
        o.rotation_euler = (0, tl, 0)
    S.bb("cladder", sx_ - 0.7, sx_ - 0.64, sy_ - 0.2, sy_ + 0.2, 2.0, min(top, 4.5) - 0.2, "dark", 0.0, nocast=True)
    # ------------------------------------------------------------------ loudspeaker pair on a pole at the hall's south-west roof corner:
    # two flared trumpets pointing out to the sides (side-on, so the flare reads), bone rims, dark throats
    px_, py_, zb_ = 1.55, -0.45, HALL_H + 0.22   # hall roof SE corner, between the magenta panel and the chimney
    if dmg:
        S.cyl("hpole", 0.07, 1.3, (px_, py_, zb_ + 0.65), "steel", verts=8)
        S.box("hpoleF", (1.6, 0.08, 0.08), (px_ - 0.9, (py_ + 0.5) * YS, zb_ + 0.12), "steel", 0.0, rot=(0, 0, 25), stretch=False)
        S.cone("hornF", 0.08, 0.3, 0.8, (px_ - 0.3, py_ + 0.9, zb_ + 0.3), "door", verts=10, rot=(0, 90, 25), nocast=False)
    else:
        zt = zb_ + 2.3
        S.cyl("hpole", 0.07, 2.3, (px_, py_, zb_ + 1.15), "steel", verts=8)
        S.bb("hpoleb", px_ - 0.2, px_ + 0.2, py_ - 0.2, py_ + 0.2, zb_, zb_ + 0.3, "concrete2", 0.02)
        S.bb("harm", px_ - 0.35, px_ + 0.35, py_ - 0.05, py_ + 0.05, zt - 0.05, zt + 0.05, "steel", 0.0)
        for sgn in (-1, 1):
            S.cyl(f"hdrv{sgn}", 0.13, 0.28, (px_ + sgn * 0.45, py_, zt), "oxide", axis="X", verts=10)
            S.cone(f"horn{sgn}", 0.07, 0.36, 0.9, (px_ + sgn * 0.98, py_, zt), "door", verts=14, rot=(0, 90 * sgn, 0), nocast=False)
            S.cyl(f"hrim{sgn}", 0.38, 0.05, (px_ + sgn * 1.45, py_, zt), "bone", axis="X", verts=14)
            S.cyl(f"hthr{sgn}", 0.29, 0.06, (px_ + sgn * 1.46, py_, zt), "dark", axis="X", verts=12)
    # ------------------------------------------------------------------ streaks, ash drift on the pad
    for k, (ax_, ay_, aw) in enumerate(((3.5, -1.5, 1.2), (-4.4, -1.8, 0.9), (4.2, 3.0, 0.8))):
        S.bb(f"ash{k}", ax_ - aw / 2, ax_ + aw / 2, ay_ - 0.3, ay_ + 0.3, 0.14, 0.17, "smoke", 0.0, nocast=True)
    if dmg:
        L.R.scorch(S, "scH", -0.4, hya - 0.12, 2.1, 1.6, 0.5)
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
    dict(state="damaged", kind="fire_large", x=anchor(-0.4, -1.7, 2.1)[0], y=anchor(-0.4, -1.7, 2.1)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(0.6, -1.9, 0.5)[0], y=anchor(0.6, -1.9, 0.5)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=anchor(-2.0, -1.2, 0.3)[0], y=anchor(-2.0, -1.2, 0.3)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=anchor(3.3, 1.4, 4.1)[0], y=anchor(3.3, 1.4, 4.1)[1], phase=6),
    dict(state="damaged", kind="smoke_plume", x=anchor(-0.4, -1.7, 2.2)[0], y=anchor(-0.4, -1.7, 2.2)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(3.3, 1.4, 4.2)[0], y=anchor(3.3, 1.4, 4.2)[1], phase=5),
]

SPEC = dict(
    pad=(-5.0, 5.0, -3.3, 3.3), env=(-4.1, 4.5, -2.8, 2.9), H=3.0,
    boxes=[(-4.6, 2.2, -0.9, 2.9)], rings=[(STACK[0], STACK[1], 0.7)],
    clips=(1.3, 2.5), scaf=[(-4.7, -3.6, -3.1, -2.5), (2.2, 3.4, -3.1, -2.5)],
    derrick=(4.4, -1.2, 4.2, (3.4, -0.4, 4.4)), sparks=[(-2.0, -2.0, 2.3), (2.0, 0.4, 1.5), (-3.0, 1.5, 2.2)],
    hide=("sten", "glow", "puff", "horn", "hdrv", "hrim", "hthr", "harm", "hpole", "chev"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
