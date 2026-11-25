"""Ending slide authority 01: the device destroyed, the ledger closed under Maintenance; the grain moves over the junction all winter.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_authority_01.py   (STILL_PREVIEW=1 for 640x360)
Scene: pale overcast winter noon at the Authority crossroads; a long grain convoy of carriers rolling over the junction, drainage
ditches beside the road, a small administration hut with one lit window and a pylon line.  Original scene, no text.
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


def build():
    R = random.Random(5301)
    K.ground(size=900)
    K.junction((0, 60), width=8)
    K.tire_tracks([(-1.5, 0), (-1.5, 30), (-1.5, 60)], col="#6E6454", z=0.06)
    # convoy of carriers over the junction and down the north road
    for yy in (14, 36, 58, 84, 108):
        X.carrier((1.6, yy), yaw=0.0)
    for xx in (-52, -78):
        X.carrier((xx, 61.6), yaw=math.pi / 2)
    K.tank((-1.8, -2), yaw=0.0)
    # drainage ditches along the road: dark channels with water
    for side in (-1, 1):
        K.box("ditch", (side * 9.5, 40, 0.02), (1.6, 240, 0.05), K.mat("#2C3A3C", rough=0.15))
    for k in range(6):
        K.puddle((R.uniform(-30, 30), R.uniform(70, 120)), 2.0, 0.9, R.uniform(0, 1))
    # admin hut and pylon line
    K.box("hut", (-22, 52, 2.0), (9, 6, 4.0), K.mat("#8A8A7A"), rot=(0, 0, 0.1))
    K.box("hutroof", (-22, 52, 4.2), (9.6, 6.6, 0.4), K.mat("#4A5058"), rot=(0, 0, 0.1))
    K.box("hutwin", (-22.4, 48.9, 2.4), (1.8, 0.1, 1.0), K.mat("yellow", "#FFE0A0", emit="#FFC070", estr=14.0))
    K.crate_stack((-12, 48), yaw=0.2, rng=R)
    K.drum_cluster((-30, 46), rng=R, n=4)
    pyl = [(24 + 6 * i, 8 + 24 * i) for i in range(6)]
    for p in pyl:
        K.pylon(p, h=14)
    K.power_line(pyl, 13.0, rng=R)
    K.road_sign((9, 52), yaw=0.2)
    for p in ((-6, 22), (6, 70), (-8, 92)):
        K.figure(p, 0.0, False)
    K.debris_field(R, n=40, xr=(-30, 30), yr=(0, 60))
    K.tufts(R, n=300, xr=(-40, 40), yr=(-6, 100), scale=0.8)
    K.ridge(620, R, n=8, h=(14, 30), tint="#7A8498")
    sunv = EK.sunvec(-40.0, 30.0)
    X.sky_grad(sunv, [(0.0, "#E6E6E2"), (0.06, "#D2D8DC"), (0.2, "#B0BCC8"), (0.5, "#8A9CB4"), (1.0, "#647A9C")],
               strength=1.0, glow="#FFFFFF", glow_pow=(12.0, 80.0), glow_k=(0.5, 0.6),
               cloud_cols=("#F2F2F0", "#CED4DA", "#9AA6B4"), cloud_range=(0.40, 0.56), cloud_scale=2.0,
               cloud_win=(0.04, 0.2, 0.7, 0.95), cloud_gain=1.2, cloud_stretch=4.0, cloud_alpha=0.9)
    EK.aim_sun(float(os.environ.get("STILL_SUN", "3.0")), sunv, (1.0, 0.96, 0.9), angle=5.0)
    K.area_light((30, -40, 40), (0, 50, 2), 70, 9000.0, (0.7, 0.78, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0012")), colour="#D4DCE4", top=45.0)
    K.camera((-14.0, -22.0, 9.0), (2.0, 80.0, 1.0), lens=26)


if __name__ == "__main__":
    EK.run(build, "ending_authority_01", finish=True)
