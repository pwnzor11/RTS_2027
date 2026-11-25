"""Ending slide left_behind 03: the Directorate's hatches stay shut; the notebook in the ash; the dunes quiet at night.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_left_behind_03.py   (STILL_PREVIEW=1 for 640x360)
Scene: clear night on the Glass Coast; three shut hatches and a dead loudspeaker mast over the hatch field, one vent glowing faint
green, pillboxes dark; in the foreground a small dropped notebook in grey ash and a handful of unlit rigs on the far dune.  No text.
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
    h = B.dune_height(x, y, amp=6.0, centre=(0, 40), r0=62.0, r1=125.0, seed=1.7)
    far = B.smoothstep(150.0, 195.0, y)
    h = h * (1 - far) - 2.2 * far
    h += 5.0 * math.exp(-((x - CAM[0]) ** 2 + (y - CAM[1]) ** 2) / (2 * 11.0 ** 2))
    return h


def build():
    R = random.Random(5403)
    gm = B.dune_mat([(0.25, "#3C4048"), (0.5, "#566068"), (0.8, "#7A8690")], ripple=0.7, speck=("#2C3036", "#788088"))
    B.terrain(None, None, height, gm, cell=1.6, grow=0.07, flat=45.0, ext=(300, -60, 420))
    B.sea(172.0, z=-0.5, col="#16304A")
    B.glass_shards(R, 400, (-70, 70), (-4, 120))
    for p, yw in (((-14.0, 60.0), 0.4), ((11.0, 56.0), -0.3), ((26.0, 74.0), 0.2)):
        EK.closed_hatch(p, yw)
    B.vent_tower((-4.0, 90.0), h=24.0, glow="#3A8A4A")
    for p, yw in (((-26.0, 72.0), -0.2), ((-6.0, 70.0), 0.0), ((17.0, 86.0), 0.3), ((38.0, 64.0), -0.4)):
        B.pillbox(p, yw)
    B.loudspeaker_mast((34.0, 94.0), h=28.0, light="#2A0A08")
    K.sandbag_wall((-9.0, 68.0), (-1.0, 68.0), rows=2, rng=R)
    # the dropped notebook in the ash, near the camera
    K.box("book", (-3.0, 0.5, 0.06), (0.32, 0.44, 0.07), K.mat("#6A2A22"), rot=(0, 0, 0.5))
    K.box("pages", (-3.0, 0.5, 0.11), (0.28, 0.4, 0.04), K.mat("#D8D0B8"), rot=(0, 0, 0.5))
    K.rubble_pile((-5.0, 2.0), r=1.0, n=8, rng=R, h=0.4)
    # unlit rigs on a far dune, left
    B.rig_bus((-44.0, 100.0), yaw=0.6, body="teal", trim="red", length=9.0, rng=R, lit=False, flagcol="red")
    B.rig_bus((-58.0, 112.0), yaw=0.4, body="ochre", trim="teal", length=8.0, rng=R, lit=False, flagcol="turq")
    B.mother_rig((-76.0, 124.0), yaw=0.5, rng=R, lit=False)
    K.tufts(R, n=250, xr=(-30, 30), yr=(-6, 40))
    K.debris_field(R, n=40, xr=(-30, 30), yr=(-4, 40))
    K.ridge(560, R, n=9, h=(14, 30), tint="#1C2240")
    sunv = B.sun_dir(40.0, -10.0)
    B.sky(sunv, stops=[(0.0, "#26386A"), (0.1, "#18265A"), (0.4, "#0C1640"), (1.0, "#050A26")],
          cloud_stops=[(0.03, "#2A3A6A"), (0.15, "#1C2A5A"), (0.4, "#101C46")], glow="#3A4C8A", glow_amt=0.3, strength=1.0)
    K.area_light((40, -30, 36), (0, 30, 2), 70, 26000.0, (0.45, 0.58, 1.0))
    K.haze(0.0014, top=40.0, colour="#4A5C98")
    K.camera((CAM[0], CAM[1], 9.0), (0.0, 40.0, 2.0), lens=26)
    # moon-ish tiny stars: scatter a few emissive points high in the sky behind the field
    for _ in range(90):
        a, e = R.uniform(-1.0, 1.0), R.uniform(0.15, 0.7)
        d = 700.0
        K.sphere("star", (math.sin(a) * d * math.cos(e) + CAM[0], math.cos(a) * d * math.cos(e), math.sin(e) * d + 30),
                 0.8, K.mat("#FFFFFF", "#FFFFFF", emit="#DDE8FF", estr=40.0))


if __name__ == "__main__":
    EK.run(build, "ending_left_behind_03", finish=True)
