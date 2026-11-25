"""Ending slide authority 04: the labour year extended again and again; the caravans keep their ledger; the fleet waits offshore.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_authority_04.py   (STILL_PREVIEW=1 for 640x360)
Scene: wide amber dusk over the dune sea: a lone Authority post with a pylon and sandbags on a crest, a long string of caravan riders
crossing the dunes below it, and at the far edge of the sea a row of small dark ships standing off.  Original scene, no text.
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



def dunes(x, y):
    return (4.5 * (math.sin(x * 0.045 + math.sin(y * 0.02) * 2.0) + 1.0)
            + 2.5 * (math.sin(x * 0.1 + y * 0.06) + 1.0) * 0.5 + 2.0 * (math.sin(y * 0.04 - x * 0.03) + 1.0))


def zfun(x, y):
    z = 0.6 * dunes(x, y) + 7.0 * math.exp(-((x - 14) ** 2 + (y - 40) ** 2) / 200.0)
    fade = (1.0 - X.smooth(120.0, 158.0, abs(x))) * X.smooth(-60.0, -30.0, y) * (1.0 - X.smooth(130.0, 160.0, y))
    return z * fade


def build():
    R = random.Random(5304)
    X.flat_ground(X.sand_mat())
    X.terrain(zfun, X.sand_mat(), xr=(-160, 160), yr=(-60, 170), step=2.5)
    shore = [(x, 190.0 + 4 * math.sin(x * 0.03)) for x in range(-900, 901, 20)]
    X.poly_plane(shore + [(900, 1500), (-900, 1500)], -0.3, X.water_mat("#1D5A74", 0.05, 0.3))
    # post on the crest: pylon, hut, sandbags, a lowered flag
    zc = zfun(14, 40)
    X.lifted(zc, K.pylon, (14.0, 40.0), h=16)
    K.box("posthut", (16, 37, zfun(16, 37) + 0.6), (5, 4, 2.8), K.mat("#8A8A7A"), rot=(0, 0, 0.3))
    X.lifted(zc, K.sandbag_wall, (6.0, 33.0), (12.0, 31.0), rows=2, rng=R)
    X.lifted(zc, K.figure, (9.0, 36.0), 0.5, False)
    # caravan: long string of riders and pack wagons winding across the dunes below
    for i in range(18):
        t = i / 17.0
        x = -80 + 140 * t
        y = 70 - 28 * t + 8 * math.sin(t * 7)
        X.lifted(zfun(x, y), X.rider, (x, y), yaw=1.35 + 0.1 * math.sin(i), rng=R)
    for i in range(4):
        x, y = -70 + i * 22, 84 - i * 6
        K.box("wagon", (x, y, zfun(x, y) + 1.2), (2.6, 4.2, 2.2), K.mat("#8A7A5A"), rot=(0, 0, 1.3))
    # far ships standing off
    for sx in (-120, -60, 0, 70, 130):
        K.box("ship", (sx, 330 + R.uniform(-20, 20), 3), (34, 8, 5), K.mat("#3A4258"))
        K.box("shipcab", (sx - 7, 330, 7), (9, 6, 5), K.mat("#4A5268"))
    X.tufts_on(R, zfun, 500, (-60, 60), (-30, 120), mat=K.grass_mat())
    K.debris_field(R, n=30, xr=(-30, 30), yr=(-10, 30))
    K.ridge(640, R, n=5, h=(10, 20), tint="#7E7C98", spread=500)
    sunv = EK.sunvec(60.0, 5.0)
    X.sky_grad(sunv, [(0.0, "#F8C080"), (0.05, "#EEA078"), (0.16, "#B88098"), (0.4, "#5A6CA8"), (1.0, "#1E3078")],
               strength=1.0, glow="#FFC890", glow_pow=(18.0, 160.0), glow_k=(0.9, 1.2),
               cloud_cols=("#FFC890", "#E8A0A0", "#9C90C4"), cloud_range=(0.52, 0.70), cloud_scale=2.2,
               cloud_win=(0.04, 0.16, 0.5, 0.8), cloud_gain=1.5, cloud_stretch=7.0, cloud_alpha=0.8)
    EK.aim_sun(float(os.environ.get("STILL_SUN", "8.0")), sunv, (1.0, 0.64, 0.38), angle=1.6)
    K.area_light((-30, -40, 40), (0, 40, 2), 70, 9000.0, (0.55, 0.62, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0009")), colour="#E8C8B0", top=45.0)
    K.camera((-30.0, -24.0, zfun(-30, -24) + 6.0), (10.0, 90.0, 2.0), lens=26)


if __name__ == "__main__":
    EK.run(build, "ending_authority_04", finish=True)
