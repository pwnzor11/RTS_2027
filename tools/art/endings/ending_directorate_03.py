"""Ending slide directorate 03: the Glass Coast launch sites become Directorate ground; the fleet's lights go out one by one.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_directorate_03.py   (STILL_PREVIEW=1 for 640x360)
Scene: blue hour on the glass shore: Pad Seven and its gantry now ringed by a Directorate picket and a hatch, a small line of wagons
moving inland in the dunes, and out on the water a row of ships with only one or two lit windows left.  Original scene, no text.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import endkit as EK  # noqa: E402
import still_directorate_kit as D  # noqa: E402
import still_kit as K  # noqa: E402
import bpy  # noqa: E402


def build():
    R = random.Random(5203)
    M = K.mat
    D.ground(D.glass_sand_mat, size=700)
    D.water(z=-0.4, size=2400, loc=(0, 700), col="#16444E", rough=0.05, ripple=0.3)
    for o in bpy.data.objects:
        if o.name == "ground":
            o.location.y = -137
            o.scale = (1, 0.62, 1)
    K.nolines(K.box("foam", (0, 82, 0.01), (900, 4.0, 0.04), M("#BCD0CC", rough=0.5)))
    D.pad((14.0, 50.0), r=13.0, rng=R)
    D.gantry((28.0, 60.0), h=28.0, yaw=0.3, collapsed=False)
    D.hatch((-26.0, 40.0), yaw=0.5, s=1.2)
    D.vent_stack((-40.0, 56.0), h=11.0, r=1.4, glow_k=0.8, rng=R)
    D.wall_segment((-8, 30), (36, 24), h=2.2)
    D.sandbags((-12, 22), (-2, 20), rows=3, rng=R)
    D.speaker_pole((-14, 18), h=7.0, rng=R, n=3, yaw=0.6)
    D.squad([(-4 + 2.4 * i, 14 + (i % 2)) for i in range(7)], yaw=0.1, rng=R, kneel_p=0.0, ash_p=0.1, mask_p=0.2)
    # wagons and walkers moving inland, left, small
    for i in range(6):
        K.box("wagon", (-70 - i * 9, 30 + i * 6, 1.3), (2.4, 4.0, 2.2), M("#7A6A4C"), rot=(0, 0, 0.9))
        K.figure((-66 - i * 9, 26 + i * 6), 0.9, False)
    # the fleet, lights dying: lit only on the nearest hulls
    D.ship((70.0, 160.0), yaw=1.45, s=1.0, lit=True)
    D.ship((-10.0, 210.0), yaw=1.5, s=1.1, lit=False)
    D.ship((150.0, 250.0), yaw=1.4, s=1.1, lit=False)
    D.ship((-110.0, 290.0), yaw=1.5, s=1.2, lit=False)
    D.ship((60.0, 340.0), yaw=1.5, s=1.2, lit=False)
    D.glass_shards(R, n=60, xr=(-50, 50), yr=(-10, 78))
    K.debris_field(R, n=40, xr=(-30, 40), yr=(-10, 60))
    K.ridge(520, R, n=9, h=(18, 34), tint="#2E3C62")
    sunv = EK.sunvec(-70.0, 4.0)
    D.sky([(0.0, "#B8C4D8"), (0.04, "#8C9CC8"), (0.15, "#4E64A0"), (0.4, "#26387A"), (1.0, "#0C1440")], sunv,
          glow_col="#E8B898", glow_k=0.6, glow_pow=40.0,
          clouds=dict(lo=0.52, hi=0.72, scale=2.4, h0=0.02, h1=0.16, top0=0.5, top1=0.9, opacity=0.7, stretch=5.0,
                      cols=[(0.05, "#C8B0C0"), (0.4, "#6C78B0"), (0.9, "#34448A")], gain=1.0), strength=0.9)
    EK.aim_sun(float(os.environ.get("STILL_SUN", "3.0")), sunv, (0.9, 0.7, 0.62), angle=2.0)
    K.area_light((34, -30, 30), (0, 30, 2), 70, 22000.0, (0.4, 0.5, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0014")), top=45.0, colour="#90A0C8")
    K.camera((-6.0, -22.0, 6.5), (6.0, 80.0, 3.5), lens=24)


if __name__ == "__main__":
    EK.run(build, "ending_directorate_03")
