"""Ending slide left_behind 05: the Second Drift buries the south beach; the ships sail; the rigs take the coast road.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_left_behind_05.py   (STILL_PREVIEW=1 for 640x360)
Scene: bright cold morning on the coast road: a long line of rigs, buggies and harpoon wagons rolling along the shore with flags up,
the beach to the right scarred and heaped with glassed sand, ships sailing away on the horizon under a clear sky.  No text.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import endkit as EK  # noqa: E402
import still_kit as K  # noqa: E402
import still_lb_kit as B  # noqa: E402
import bpy  # noqa: E402

CAM = (-7.0, -31.0)


def height(x, y):
    h = B.dune_height(x, y, amp=5.0, centre=(0, 40), r0=62.0, r1=125.0, seed=0.4)
    h += 4.0 * math.exp(-((x - CAM[0]) ** 2 + (y - CAM[1]) ** 2) / (2 * 10.0 ** 2))
    # scarred heap on the south beach (right)
    h += 4.0 * math.exp(-((x - 46.0) ** 2 + (y - 70.0) ** 2) / (2 * 14.0 ** 2))
    return h * (1 - B.smoothstep(130.0, 170.0, y)) - 2.0 * B.smoothstep(130.0, 170.0, y)


def build():
    R = random.Random(5405)
    gm = B.dune_mat([(0.25, "#8A7C66"), (0.5, "#B09E7E"), (0.8, "#D2C09A")], ripple=0.7, speck=("#6A5C44", "#D8C8A0"))
    B.terrain(None, None, height, gm, cell=1.6, grow=0.07, flat=45.0, ext=(300, -60, 420))
    B.sea(150.0, z=-0.5, col="#2A6280")
    # coast road
    pts = [(-70, 36), (-40, 42), (-10, 48), (20, 54), (50, 60), (90, 68)]
    for a, b in zip(pts[:-1], pts[1:]):
        K.road(a, b, 6.0)
    # the Drift rolling along the road, flags up
    cols = ["teal", "red", "ochre", "plum", "teal", "turq", "red", "ochre"]
    for i in range(8):
        x = -62.0 + i * 17.0
        y = 36 + (x + 70) * 0.2 + R.uniform(-0.6, 0.6)
        B.rig_bus((x, y), yaw=1.4, body=cols[i], trim="red", length=8.5, rng=R, lit=False, flagcol=["red", "turq", "haz"][i % 3])
    for i in range(5):
        x = -50.0 + i * 24.0
        B.harpoon_wagon((x, 30 + (x + 70) * 0.2 - 4.0), yaw=1.4, rng=R, aim=0.0, lit=False)
    for i in range(4):
        x = -30.0 + i * 26.0
        B.buggy((x, 31 + (x + 70) * 0.2 - 7.0), yaw=1.4, body=["haz", "red"][i % 2], rng=R, lit=False)
    B.mother_rig((-76.0, 30.0), yaw=1.4, rng=R, lit=False)
    # ships sailing away on the horizon
    for sx, sy, yw in ((-70, 300, 1.4), (10, 340, 1.5), (90, 380, 1.45)):
        B.ship((sx, sy), yaw=yw, L=60.0, W=11.0, lit=False)
    B.glass_shards(R, 300, (-70, 70), (-4, 100))
    K.tufts(R, n=300, xr=(-40, 40), yr=(-6, 40))
    K.debris_field(R, n=40, xr=(-30, 30), yr=(-4, 40))
    K.ridge(600, R, n=9, h=(12, 26), tint="#7A88AC")
    sunv = B.sun_dir(-55.0, 22.0)
    B.sky(sunv, stops=[(0.0, "#F8EAD0"), (0.06, "#F0DCC0"), (0.2, "#B0CCE0"), (0.5, "#6AA0D4"), (1.0, "#3470B8")],
          cloud_stops=[(0.03, "#FFF8E8"), (0.15, "#DCE8F4"), (0.4, "#A0BCE0")], glow="#FFF0D0", glow_amt=0.8, strength=1.05)
    B.key_sun(sunv, 8.0, (1.0, 0.92, 0.8), angle=2.0)
    K.area_light((40, -30, 36), (0, 30, 2), 70, 12000.0, (0.6, 0.7, 1.0))
    K.haze(0.0008, top=40.0, colour="#D8E4EC")
    K.camera((CAM[0], CAM[1], 12.0), (14.0, 60.0, 3.0), lens=26)


if __name__ == "__main__":
    EK.run(build, "ending_left_behind_05", finish=True)
