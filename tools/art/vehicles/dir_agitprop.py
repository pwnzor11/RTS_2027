"""Directorate agitprop loudspeaker unit for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Small tracked chassis (red oxide, bone stripes, house flank panels) carrying a tall mast with a cluster of big flared
loudspeaker horns and a hanging house-colour banner.  Turret-layer support (additive, build_vehicle.py): the mast, horn
cluster and banner are the TURRET; the chassis is the HULL.
States: idle  = complete unit, 16 facings (hull and turret turned together, for previews / fallback);
        hull  = chassis only, 16 facings, with an empty turntable;
        turret = horn cluster + mast + banner only, 16 facings, no ground shadow, same canvas and origin as hull
                 (draw hull, then turret, at the unit's world position; turret facing is independent).
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_dig_common as D

ID = "dir_agitprop"
TITLE = "Directorate agitprop loudspeaker unit"
CANVAS = 120
TARGET = (0.0, 0.0, 1.3)
STATES = [("idle", 16, 1, 1), ("hull", 16, 1, 1), ("turret", 16, 1, 1)]
NO_SHADOW_STATES = ("turret",)
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS


def horn(S, name, x, y, z, r, depth, yaw, up=0.0):
    """Flared loudspeaker horn: wide mouth toward +Y turned by yaw (deg, + = to the left), tilted up by `up` degrees."""
    import math
    ya, ua = math.radians(yaw), math.radians(up)
    u = (-math.sin(ya) * math.cos(ua), math.cos(ya) * math.cos(ua), math.sin(ua))
    rot = (90 + up, 0, yaw)
    S.cone(name, r, r * 0.22, depth, (x, y, z), "bone", 16, rot=rot)
    m = depth / 2 + 0.02
    S.cyl(name + "m", r * 0.86, 0.04, (x + u[0] * m, y + u[1] * m, z + u[2] * m), "dark", rot=rot, verts=14)
    t = depth / 2 + 0.12
    S.cyl(name + "t", r * 0.3, 0.3, (x - u[0] * t, y - u[1] * t, z - u[2] * t), "steel2", rot=rot, verts=10)


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    do_hull = state in ("idle", "hull")
    do_tur = state in ("idle", "turret")
    if do_hull:
        D.tracks(S, 0.78, 2.5, 0.5, 0.62, y0=0.0, nwheel=4)
        box("hull", (1.4, 2.3, 0.5), (0, 0.0, 0.84), "red", 0.09)
        box("deck", (1.4, 2.3, 0.07), (0, 0.0, 1.12), "conc2", 0.03)
        box("nose", (1.4, 0.4, 0.42), (0, 1.25, 0.82), "conc2", 0.08, rot=(-12, 0, 0))
        D.vbands(S, "nb", 1.47, -0.6, 0.6, 0.78, 0.26, 4)
        box("rearplate", (1.4, 0.1, 0.46), (0, -1.18, 0.84), "conc3", 0.03)
        box("genbox", (0.9, 0.6, 0.38), (0, -0.75, 1.36), "conc2", 0.06)
        box("genvent", (0.7, 0.04, 0.2), (0, -1.06, 1.38), "dark", 0.0)
        for s in (-1, 1):
            box(f"flank{s}", (0.05, 1.5, 0.3), (s * 0.71, -0.1, 0.88), "house", 0.0)
            D.bands(S, f"fb{s}_", s * 0.71, 0.7, 1.2, 0.88, 0.3, 3)
        box("cab", (0.6, 0.5, 0.3), (0, 0.8, 1.35), "red2", 0.06)
        box("cabslit", (0.46, 0.04, 0.08), (0, 1.06, 1.38), "glass", 0.0)
        cyl("turntable", 0.42, 0.08, (0, 0.05, 1.18), "steel2", "Z", 18)
    if do_tur:
        ty = 0.05
        cyl("mastbase", 0.34, 0.12, (0, ty, 1.26), "conc3", "Z", 16)
        cyl("mast", 0.09, 1.4, (0, ty, 1.95), "steel2", "Z", 10)
        cyl("mastring", 0.14, 0.06, (0, ty, 1.5), "redhi", "Z", 10)
        box("crossarm", (1.2, 0.12, 0.1), (0, ty + 0.06, 2.6), "steel2", 0.02)
        # five big horns: three fanned forward at the top, two below angled outward
        horn(S, "hornC", 0, ty + 0.55, 2.78, 0.4, 0.9, 0, 6)
        horn(S, "hornL", -0.6, ty + 0.5, 2.62, 0.34, 0.8, 30, 4)
        horn(S, "hornR", 0.6, ty + 0.5, 2.62, 0.34, 0.8, -30, 4)
        horn(S, "hornLl", -0.4, ty + 0.42, 2.12, 0.27, 0.65, 48, 0)
        horn(S, "hornRl", 0.4, ty + 0.42, 2.12, 0.27, 0.65, -48, 0)
        # banner panel hanging from the crossarm behind the horns... on a pole at the back
        box("bpole", (0.07, 0.07, 1.0), (0, ty - 0.35, 2.1), "steel2", 0.0)
        box("banner", (0.95, 0.07, 0.9), (0, ty - 0.4, 2.0), "house", 0.0)
        box("bannertrim", (1.03, 0.06, 0.06), (0, ty - 0.4, 2.48), "bone", 0.0)
        box("bannertrimB", (1.03, 0.06, 0.06), (0, ty - 0.4, 1.54), "bone", 0.0)
        box("bannerstar", (0.2, 0.09, 0.2), (0, ty - 0.4, 2.0), "bone", 0.0, rot=(0, 45, 0))
