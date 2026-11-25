"""Ending slide authority 03: Shaft Four opened; the first people to see daylight in thirty years; water sent down to the failing Annex.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_authority_03.py   (STILL_PREVIEW=1 for 640x360)
Scene: cold clear dawn on the ash plain; the old headframe over the open shaft mouth, a shaft of daylight on a slow line of small
figures in grey blankets coming up, decon tents and carriers with water tanks lined up behind them.  Original scene, no text.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import endkit as EK  # noqa: E402
import still_authority_extra as X  # noqa: E402
import still_kit as K  # noqa: E402
import bpy  # noqa: E402

import still_directorate_kit as D  # noqa: E402
import still_expedition_kit as E  # noqa: E402


def build():
    R = random.Random(5303)
    X.flat_ground(X.ash_mat())
    D.headframe((-8, 90), h=24.0, yaw=0.1)
    K.cyl("mouth", (-8, 80, 0.3), 7.0, 0.6, K.mat("#0E0C0C"), verts=16)
    # a column of daylight on the mouth, and people filing out in a line toward the tents
    D.beam((-8, 80, 1), (-30, 150, 140), 6.0, 20.0, "#FFF0D0", strength=2.0, alpha=0.07)
    for i in range(16):
        t = i / 15.0
        K.figure((-8 + 20 * t + 0.5 * math.sin(i), 70 - 56 * t), math.pi + 0.3, False, 0.95)
        K.box("blanket", (-8 + 20 * t + 0.5 * math.sin(i), 70 - 56 * t, 1.1), (0.55, 0.38, 0.5), K.mat("#8C9094"), rot=(0, 0, 0.3))
    # decon tents and tankers
    for k, (x, y) in enumerate(((22, 30), (32, 40), (42, 50), (-30, 40))):
        E.tent((x, y), 0.3 + 0.1 * k, 1.5, col="#D6D4C4")
    for k, yy in enumerate((6, 22, 38)):
        X.carrier((-26 + k * 0.4, yy - 14 + k * 8), yaw=0.15)
        K.cyl("tank", (-26 + k * 0.4, yy - 14 + k * 8, 2.4), 1.0, 3.4, K.mat("#6A7C8A"), rot=(math.pi / 2, 0, 0.15), verts=10)
    for p in ((-16, 22), (-14, 34), (18, 20)):
        K.figure(p, 0.4, False)
    K.pylon((60, 60), h=14)
    K.crate_stack((10, 12), yaw=0.4, rng=R)
    K.rubble_pile((-2, 56), r=1.6, n=14, rng=R, h=0.9)
    K.debris_field(R, n=60, xr=(-30, 40), yr=(-4, 70))
    K.ridge(560, R, n=8, h=(14, 30), tint="#8C94B0")
    sunv = EK.sunvec(-60.0, 7.0)
    X.sky_grad(sunv, [(0.0, "#FBE6C4"), (0.06, "#EEC8B4"), (0.2, "#A8B8D4"), (0.5, "#5C84B8"), (1.0, "#2A4C88")],
               strength=1.0, glow="#FFE8C0", glow_pow=(18.0, 160.0), glow_k=(0.8, 1.2),
               cloud_cols=("#FFE4C0", "#E8C0B8", "#A8B0D0"), cloud_range=(0.50, 0.68), cloud_scale=2.2,
               cloud_win=(0.04, 0.16, 0.5, 0.8), cloud_gain=1.4, cloud_stretch=6.0, cloud_alpha=0.8)
    EK.aim_sun(float(os.environ.get("STILL_SUN", "8.0")), sunv, (1.0, 0.82, 0.62), angle=1.6)
    K.area_light((30, -40, 40), (0, 40, 2), 70, 9000.0, (0.6, 0.7, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0010")), colour="#DCCCC4", top=45.0)
    K.camera((-2.0, -16.0, 6.0), (0.0, 80.0, 6.0), lens=26)


if __name__ == "__main__":
    EK.run(build, "ending_authority_03", finish=True)
