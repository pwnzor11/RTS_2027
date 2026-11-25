"""Ending slide left_behind 04: the Authority keeps its posts out of the Belt, the Directorate goes back underground.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_left_behind_04.py   (STILL_PREVIEW=1 for 640x360)
Scene: grey late afternoon at the Belt's edge; an abandoned Authority post (pylon, shed, sandbags, a dropped barrier) on the near
dune, a shut Directorate hatch far off in the flats, and a small line of rigs passing between them unchallenged.  No text.
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
    h = B.dune_height(x, y, amp=6.0, centre=(0, 40), r0=62.0, r1=125.0, seed=3.1)
    h += 5.0 * math.exp(-((x + 12.0) ** 2 + (y - 22.0) ** 2) / (2 * 10.0 ** 2))
    return h * (1 - B.smoothstep(150.0, 195.0, y))


def build():
    R = random.Random(5404)
    gm = B.dune_mat([(0.25, "#6E6A60"), (0.5, "#8C8878"), (0.8, "#ABA690")], ripple=0.6, speck=("#54503F", "#B0AA90"))
    B.terrain(None, None, height, gm, cell=1.6, grow=0.07, flat=45.0, ext=(300, -60, 420))
    zc = height(-12.0, 22.0)
    # abandoned Authority post on the near dune
    for fn, args, kw in ((K.pylon, ((-12.0, 22.0),), dict(h=16)),):
        K.cyl("pylon", (-12.0, 22.0, zc + 8), 0.25, 16, K.mat("steel_d"), verts=6)
    B.power_plant((-6.0, 18.0), yaw=0.3, burning=False, rng=R)
    K.sandbag_wall((-18.0, 14.0), (-10.0, 12.0), rows=2, rng=R)
    K.box("barrier", (-3.0, 10.0, 0.5), (4.0, 0.15, 0.15), K.mat("yellow", "#C9A640"), rot=(0.0, 0.5, 0.2))
    K.road_sign((-1.0, 12.0), yaw=0.3, lean=0.4)
    K.crate_stack((-16.0, 8.0), yaw=0.3, rng=R)
    # shut Directorate hatch far off, with a dead vent
    EK.closed_hatch((30.0, 100.0), 0.3, s=1.4)
    B.vent_tower((42.0, 118.0), h=22.0, glow="#3A8A4A")
    B.pillbox((16.0, 110.0), 0.2)
    # rigs passing in a line between them
    for i in range(6):
        x = 40.0 - i * 14.0
        y = 62.0 + 3.0 * math.sin(i)
        B.rig_bus((x, y), yaw=-1.45, body=["teal", "red", "ochre", "plum", "teal", "ochre"][i], trim="red", length=8.0, rng=R, lit=False,
                  flagcol=["red", "turq", "haz", "red", "turq", "haz"][i])
    B.glass_shards(R, 200, (-70, 70), (-4, 120))
    K.tufts(R, n=300, xr=(-40, 40), yr=(-6, 60))
    K.debris_field(R, n=40, xr=(-30, 30), yr=(-4, 40))
    K.ridge(560, R, n=9, h=(14, 30), tint="#6E7494")
    sunv = B.sun_dir(-40.0, 14.0)
    B.sky(sunv, stops=[(0.0, "#D8D4CC"), (0.06, "#BCC0C4"), (0.2, "#98A8BC"), (0.5, "#6A82A8"), (1.0, "#3A5484")],
          cloud_stops=[(0.03, "#E8E4DC"), (0.15, "#A8B0BC"), (0.4, "#6C7C98")], glow="#F0E0C8", glow_amt=0.6, strength=1.05)
    B.key_sun(sunv, 4.0, (1.0, 0.9, 0.78), angle=3.0)
    K.area_light((40, -30, 36), (0, 30, 2), 70, 12000.0, (0.6, 0.7, 1.0))
    K.haze(0.0010, top=40.0, colour="#C4CCD8")
    K.camera((CAM[0] + 6, CAM[1], 9.0), (6.0, 70.0, 4.0), lens=26)


if __name__ == "__main__":
    EK.run(build, "ending_left_behind_04", finish=True)
