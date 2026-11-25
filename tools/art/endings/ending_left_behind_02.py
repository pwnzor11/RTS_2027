"""Ending slide left_behind 02: Marrow writes the last debt into the ledger; she keeps the books anyway.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_left_behind_02.py   (STILL_PREVIEW=1 for 640x360)
Scene: blue hour in the Drift's camp; the Mother Rig with its lantern-lit canvas street and flag mast at the centre, the other rigs
dark and quiet around it, one lit window and a thin thread of cookfire smoke; nobody gathered.  Original scene, no text.
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
    h = B.dune_height(x, y, amp=5.0, centre=(0, 30), r0=60.0, r1=120.0, seed=2.3)
    h += 4.0 * math.exp(-((x - CAM[0]) ** 2 + (y - CAM[1]) ** 2) / (2 * 10.0 ** 2))
    return h * (1 - B.smoothstep(150.0, 190.0, y))


def build():
    R = random.Random(5402)
    gm = B.dune_mat([(0.25, "#5C5A62"), (0.5, "#7E7A80"), (0.8, "#A09C98")], ripple=0.6, speck=("#46444C", "#A4A0A0"))
    B.terrain(None, None, height, gm, cell=1.6, grow=0.07, flat=45.0, ext=(300, -60, 420))
    B.mother_rig((0.0, 40.0), yaw=0.15, rng=R, lit=True)
    B.kettle_rig((-24.0, 26.0), yaw=0.6, rng=R, lit=False, steam=False)
    B.rig_bus((22.0, 30.0), yaw=-0.4, body="plum", trim="ochre", length=8.5, rng=R, lit=False, flagcol="turq")
    B.rig_bus((-10.0, 14.0), yaw=0.1, body="ochre", trim="teal", length=8.0, rng=R, lit=False, flagcol="red", awning=False)
    B.rig_bus((14.0, 12.0), yaw=-0.2, body="teal", trim="red", length=9.0, rng=R, lit=False, flagcol="haz", smoke=True)
    for x, y, yw in ((-18.0, 52.0, 0.2), (30.0, 52.0, -0.3), (40.0, 22.0, -0.6)):
        B.harpoon_wagon((x, y), yaw=yw, rng=R, aim=0.0, lit=False)
    for fp in ((4.0, 22.0),):
        K.fire(fp, size=0.5, rng=R, light=False)
        B.lantern((fp[0], fp[1], 0.5), "#FF9A3C", 70.0, light=900.0)
        for a in (0.6, 2.4):
            B.lb_figure((fp[0] + math.cos(a) * 1.6, fp[1] + math.sin(a) * 1.6), a + math.pi, R, "scav", lit=True)
    for x in (-12.0, 0.0, 12.0):
        B.lantern((x, 24.0, 1.0), "#FFB347", 40.0)
    K.crate_stack((-4.0, 8.0), yaw=0.5, rng=R)
    K.drum_cluster((8.0, 6.0), rng=R, n=4)
    B.glass_shards(R, 200, (-70, 70), (-4, 120))
    K.tufts(R, n=300, xr=(-40, 40), yr=(-6, 60))
    K.ridge(560, R, n=9, h=(14, 30), tint="#2E3660")
    sunv = B.sun_dir(-60.0, -3.0)
    B.sky(sunv, stops=[(0.0, "#8A9CC8"), (0.04, "#6A80B8"), (0.12, "#44609E"), (0.4, "#26407E"), (1.0, "#0E1C50")],
          cloud_stops=[(0.03, "#A8A8C8"), (0.15, "#6878B0"), (0.4, "#34447C")], glow="#C0A0B0", glow_amt=0.5, strength=1.1)
    K.area_light((40, -30, 36), (0, 30, 2), 70, 30000.0, (0.45, 0.58, 1.0))
    K.haze(0.0016, top=40.0, colour="#8C9CD0")
    K.camera((CAM[0], CAM[1], 8.0), (2.0, 44.0, 3.0), lens=26)


if __name__ == "__main__":
    EK.run(build, "ending_left_behind_02", finish=True)
