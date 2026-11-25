"""Ending slide left_behind 01: the Expedition's depot burns for three days; Jessup keeps the banner.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_left_behind_01.py   (STILL_PREVIEW=1 for 640x360)
Scene: dusk on the beach; the Expedition depot (pile houses, containers) burning in the middle distance under black smoke, a ship
standing out to sea with the wounded, and in the foreground a lone rig with the names flag planted on the dune crest.  No text.
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
    h = B.dune_height(x, y, amp=6.0, centre=(0, 40), r0=62.0, r1=125.0, seed=0.9)
    far = B.smoothstep(130.0, 175.0, y)
    h = h * (1 - far) - 2.2 * far
    h += 5.0 * math.exp(-((x - CAM[0]) ** 2 + (y - CAM[1]) ** 2) / (2 * 11.0 ** 2))
    return h


def build():
    R = random.Random(5401)
    gm = B.dune_mat([(0.25, "#6A5E50"), (0.5, "#8A7B66"), (0.8, "#B09B78")], ripple=0.7, speck=("#4C4438", "#B09A78"))
    B.terrain(None, None, height, gm, cell=1.6, grow=0.07, flat=45.0, ext=(300, -60, 420))
    B.sea(150.0, z=-0.5)
    # the burning depot
    B.pile_house((-18.0, 78.0), yaw=0.2, glow=False)
    B.pile_house((10.0, 90.0), yaw=-0.3, glow=False)
    for i, (x, y) in enumerate(((-30, 70), (-4, 66), (22, 74), (30, 96), (-12, 100))):
        B.container((x, y), yaw=R.uniform(-0.6, 0.6))
    for p, sz in (((-18, 78), 5.0), ((10, 90), 6.0), ((-4, 66), 3.5), ((22, 74), 3.0), ((-30, 70), 3.0)):
        K.fire(p, size=sz, rng=R)
    K.smoke_column((-18, 78), h=60, r0=3.4, n=12, rng=R)
    K.smoke_column((10, 90), h=70, r0=3.8, n=12, rng=R)
    B.ship((60.0, 215.0), yaw=1.5, L=60.0, W=11.0, lit=True)
    B.ship((-70.0, 260.0), yaw=1.55, L=60.0, W=11.0, lit=True)
    # foreground: a rig on the crest, banner, a few figures watching
    B.rig_bus((-14.0, -6.0), yaw=0.5, body="teal", trim="red", length=9.0, rng=R, lit=True, flagcol="red")
    B.flag((-8.0, -2.0), h=7.0, col="red", yaw=0.4, rng=R, w=2.4, hh=1.4)
    for p, yw in (((-5.0, 0.0), 3.0), ((-3.0, 2.0), 3.1), ((-9.5, 3.5), 2.8)):
        B.lb_figure(p, yw, R, "scav", lit=True)
    K.fire((-2.0, 6.0), size=0.5, rng=R, light=False)
    K.crate_stack((-1.0, 12.0), yaw=0.5, rng=R)
    B.glass_shards(R, 120, (-60, 60), (-4, 90))
    K.tufts(R, n=300, xr=(-40, 40), yr=(-6, 60))
    K.debris_field(R, n=40, xr=(-30, 30), yr=(-4, 50))
    K.ridge(560, R, n=9, h=(14, 30), tint="#3A3252")
    sunv = B.sun_dir(-60.0, 3.0)
    B.sky(sunv, stops=[(0.0, "#F7A050"), (0.04, "#E8785A"), (0.1, "#A85A7E"), (0.22, "#4E4E92"), (0.5, "#1E2C62"), (1.0, "#0A1238")],
          cloud_stops=[(0.03, "#FF8A50"), (0.15, "#B05878"), (0.4, "#3E3E80")], glow="#FF8A50", glow_amt=1.4, strength=1.1)
    B.key_sun(sunv, 5.0, (1.0, 0.5, 0.26), angle=2.0)
    K.area_light((40, -30, 36), (0, 30, 2), 70, 28000.0, (0.40, 0.52, 1.0))
    K.haze(0.0016, top=40.0, colour="#A09AC0")
    K.camera((CAM[0], CAM[1], 9.0), (6.0, 80.0, 8.0), lens=26)


if __name__ == "__main__":
    EK.run(build, "ending_left_behind_01", finish=True)
