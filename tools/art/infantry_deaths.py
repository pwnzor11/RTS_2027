"""Extra infantry death states for build_infantry.py --deaths (S24, M10-11, D-243): die_blast, die_burn, die_rad.

The three states are appended after the module's own states (facings 1, drawn facing east like "die", ticks_per_frame 3).
They animate the module's existing rig: death_pose() starts from the module's own idle pose dict and overrides the keys every
infantry module understands (bob, dy, lean, twist, roll, feet, hands, gun_space, gun, flash); the original "die" poses are reused for
the last frames of the burn and radiation falls, so the fallen body matches the plain death.
  die_blast  8 frames: flung backwards, tumbles over (arms up, rifle thrown), lands prone.
  die_burn   8 frames: staggers with flames on the body, drops to the knees, falls; the cloth chars (darkens) frame by frame.
  die_rad    8 frames: knees buckle, slumps forward; green tint grows; the last two frames dissolve (ordered dither, alpha stays 0/255).
Tint and flames ride along in the pose dict (keys _tint, _flames, _dissolve); a module's build() ignores unknown keys, and
build_infantry applies them (materials) or wraps build (flames).  Pure python at import: Blender-side imports are lazy.
"""
import math

DEATH_STATES = [("die_blast", 1, 8, 3), ("die_burn", 1, 8, 3), ("die_rad", 1, 8, 3)]
DEATH_NAMES = [s[0] for s in DEATH_STATES]

CHAR = (0.019, 0.013, 0.010)         # linear colour the burn blends towards
GREEN = (0.10, 0.90, 0.05)          # linear colour the radiation blends towards
FLAME_LEVEL = [1.0, 1.0, 1.0, 1.0, 0.85, 0.5, 0.0, 0.0]
DISSOLVE = [0, 0, 0, 0, 0, 0, 0.35, 0.65]


def _anchor(p, local):
    """World (rig) position of a point given in upper-body coords for the pose dict p."""
    import rig as R
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"]) @ R.rz(p.get("roll", 0.0))
    return tuple(U @ R.Vector(local))


def _legs(p, bend=0.0, spread=0.11):
    """Foot targets that follow the body (legs trail along the torso axis), in rig coords, kept above the ground."""
    out = []
    for sx in (-1, 1):
        f = _anchor(p, (sx * spread, bend * (1 if sx > 0 else -0.4), -0.52))
        out.append((f[0], f[1], max(0.06, f[2])))
    return tuple(out)


def death_pose(orig, state, step, n):
    """Pose dict for one frame of a death state; `orig` is the module's own pose function."""
    p = dict(orig("idle", 0, 1))
    p["flash"] = False
    k = step
    last = n - 1
    if state == "die_blast":
        #   pz    dy    lean    twist roll
        K = [(0.72, -0.04, -18, 0, 0), (0.95, -0.10, -75, 0, 10), (1.05, -0.17, -140, 0, 25), (0.92, -0.24, -205, 0, 25),
             (0.66, -0.30, -250, 0, 15), (0.34, -0.35, -266, 0, 8), (0.20, -0.38, -270, 0, 0), (0.17, -0.38, -270, 0, 0)]
        pz, dy, lean, tw, roll = K[k]
        air = k <= 4
        arms = (("u", (-0.36, 0.05, 0.62)), ("u", (0.36, 0.10, 0.60))) if air else (("u", (-0.30, 0.30, 0.18)), ("u", (0.30, 0.34, 0.14)))
        gun_pos = [(0.30, 0.45, 1.00), (0.15, 0.20, 1.35), (-0.05, -0.20, 1.45), (-0.25, -0.45, 1.20), (-0.40, -0.55, 0.70),
                   (-0.50, -0.60, 0.25), (-0.52, -0.60, 0.05), (-0.52, -0.60, 0.045)][k]
        p.update(bob=pz - 0.62, dy=dy, lean=lean, twist=tw, roll=roll, hands=arms, gun_space="w",
                 gun=(gun_pos, 15.0 + 40.0 * k, 20.0 * min(k, 4) - 80.0 + (80.0 if k >= 5 else 0.0)))
        p["feet"] = _legs(p, bend=0.18 if air else 0.0)
        return p

    if state in ("die_burn", "die_rad"):
        fall = orig("die", 0, 6)
        t = k / last
        if state == "die_burn":
            S = [  # pz, dy, lean, twist, hands(u), feet bend
                (0.62, 0.00, 8, 14, (("u", (-0.34, 0.30, 0.55)), ("u", (0.34, 0.28, 0.62)))),
                (0.60, 0.03, -6, -20, (("u", (-0.38, 0.12, 0.66)), ("u", (0.30, 0.34, 0.50)))),
                (0.56, 0.05, 12, 18, (("u", (-0.30, 0.34, 0.60)), ("u", (0.38, 0.14, 0.64)))),
                (0.40, 0.08, 30, 8, (("u", (-0.30, 0.26, 0.40)), ("u", (0.30, 0.28, 0.42)))),
            ]
            if k < len(S):
                pz, dy, lean, tw, hands = S[k]
                feet = ((-0.12, 0.08 + 0.06 * (k % 2), 0.09), (0.12, -0.10 - 0.05 * (k % 2), 0.09))
                if k == 3:      # on the knees
                    feet = ((-0.10, -0.20, 0.07), (0.10, -0.26, 0.07))
                gun_pos = [((0.34, 0.45, 0.30), 10.0, 30.0), ((0.38, 0.50, 0.12), 10.0, 0.0), ((0.38, 0.55, 0.05), 10.0, 0.0),
                           ((0.36, 0.50, 0.045), 10.0, 0.0)][k]
                p.update(bob=pz - 0.62, dy=dy, lean=lean, twist=tw, hands=hands, feet=feet, gun_space="w", gun=gun_pos)
            else:               # 4..7 = the plain fall's frames 2..5
                q = orig("die", k - 2, 6)
                p.update(q)
                p["flash"] = False
            p["_tint"] = (CHAR, 0.25 + 0.72 * t)
            p["_flames"] = FLAME_LEVEL[k]
            p["_smoke"] = SMOKE[k]
            p["_k"] = k
            return p
        # die_rad
        S = [
            (0.60, 0.01, 10, 0, (("u", (-0.30, 0.22, 0.20)), ("u", (0.30, 0.22, 0.20))), "gun"),
            (0.50, 0.04, 24, 6, (("u", (-0.32, 0.10, 0.05)), ("u", (0.32, 0.10, 0.05))), "drop"),
            (0.38, 0.08, 38, -6, (("u", (-0.30, 0.20, -0.10)), ("u", (0.30, 0.20, -0.10))), "drop"),
        ]
        if k < len(S):
            pz, dy, lean, tw, hands, g = S[k]
            feet = ((-0.12, 0.04, 0.09), (0.12, -0.04, 0.09)) if k == 0 else ((-0.10, -0.18, 0.07), (0.10, -0.24, 0.07))
            p.update(bob=pz - 0.62, dy=dy, lean=lean, twist=tw, hands=hands if g != "gun" else ("gun", "gun"), feet=feet)
            if g == "drop":
                p.update(gun_space="w", gun=((0.36, 0.40 + 0.1 * k, 0.05 + 0.12 * (2 - k)), 10.0, 0.0))
        else:                   # 3..5 = the plain fall's frames 3..5, 6..7 lie still and dissolve
            q = orig("die", min(k, 5), 6)
            p.update(q)
            p["flash"] = False
        p["_tint"] = (GREEN, 0.30 + 0.55 * t)
        p["_dissolve"] = DISSOLVE[k]
        return p
    raise KeyError(state)


FIRE_MATERIALS = {"flame_o": ("#FF6A10", 0.5, 0.0, "#FF6A10"), "flame_y": ("#FFC030", 0.5, 0.0, "#FFC030"), "smoke": ("#6B6B66", 0.95, 0.0, None)}
FIRE_EMIT = {"flame_o": 1.3, "flame_y": 1.0}
# Extra palette entries for the die_burn frames only (the 32-colour sheet palette has no orange): fire ramp and two char tones.
FIRE_PALETTE = [(0x7A, 0x1E, 0x0A), (0xC8, 0x40, 0x0F), (0xFF, 0x7A, 0x14), (0xFF, 0xB0, 0x2E), (0xFF, 0xE4, 0x6A),
                (0x0F, 0x0C, 0x0A), (0x1D, 0x16, 0x12)]
SMOKE = [0, 0, 0, 0, 0.35, 0.6, 1.0, 1.0]


def add_flames(S, p):
    """Fire on the body (orange cone with a smaller yellow core) and, in the last frames, a few grey smoke puffs above it."""
    lvl, k = p.get("_flames", 0.0), p.get("_k", 0)
    if lvl >= 0.2:
        spots = ((0.20, 0.0, 0.30), (-0.20, 0.05, 0.12), (0.0, -0.24, 0.45), (0.0, 0.10, 0.62), (-0.18, 0.0, 0.52), (0.22, 0.05, 0.55),
                 (0.0, 0.14, 0.25))
        for i, loc in enumerate(spots):
            a = _anchor(p, loc)
            h = (0.26 + 0.07 * ((i + k) % 3)) * lvl
            r = 0.10 * lvl + 0.02
            S.cyl(r, h, (a[0], a[1], a[2] + h * 0.45), "flame_o", "Z", None, verts=6, r2=0.0)
            S.cyl(r * 0.55, h * 0.65, (a[0], a[1], a[2] + h * 0.30), "flame_y", "Z", None, verts=6, r2=0.0)
    sm = p.get("_smoke", 0.0)
    if sm > 0:
        for i, loc in enumerate(((0.0, 0.0, 0.2), (0.12, -0.1, 0.1), (-0.1, 0.1, 0.05))):
            a = _anchor(p, loc)
            z = a[2] + 0.25 + 0.2 * i * sm
            r = 0.07 + 0.05 * sm + 0.02 * i
            S.sphere((r, r, r), (a[0] + 0.04 * i, a[1], z), "smoke", None, seg=8)


def dissolve_mask(w, h, frac):
    """Boolean array (h, w): True = pixel removed by the ordered 4 x 4 dither at fraction frac."""
    import numpy as np
    bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
    yy, xx = np.mgrid[0:h, 0:w]
    return bayer[yy % 4, xx % 4] < frac
