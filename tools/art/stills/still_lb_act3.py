"""Act-3 opener still for the Left Behind campaign (lb_03 Hatch Field): blue hour on the Glass Coast, camp lit, hatch field glowing.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_lb_act3.py     (STILL_PREVIEW=1 for 640x360)
Writes data/images/stills/still_left_behind_act3.png.  Original scene, no text in the image.
Scene: from a dune crest, the Drift's camp (lit rigs, harpoon wagons lined up) in the glass-strewn dunes; across the sand the
Directorate hatch field: three open hatches throwing green light, a reactor vent, pillboxes, a loudspeaker mast; sea at the horizon.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import still_kit as K  # noqa: E402
import still_lb_kit as B  # noqa: E402
import bpy  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
CAM = (-7.0, -31.0)


def height(x, y):
    h = B.dune_height(x, y, amp=8.0, centre=(0, 40), r0=62.0, r1=125.0, seed=1.7)
    far = B.smoothstep(150.0, 195.0, y)
    h = h * (1 - far) - 2.2 * far
    # crest under the camera
    h += 6.5 * math.exp(-((x - CAM[0]) ** 2 + (y - CAM[1]) ** 2) / (2 * 11.0 ** 2))
    return h


def build():
    R = random.Random(2103)
    gm = B.dune_mat([(0.25, "#7A6C58"), (0.5, "#9A8A70"), (0.8, "#BFA982")], ripple=0.7, speck=("#58503F", "#C4B08A"))
    B.terrain(None, None, height, gm, cell=1.6, grow=0.07, flat=45.0, ext=(300, -60, 420))
    B.sea(172.0, z=-0.5)
    B.glass_shards(R, 520, (-70, 70), (-4, 120), avoid=lambda x, y: abs(x) < 0 and False)
    B.glass_shards(R, 160, (-30, 30), (55, 100))
    K.ridge(560, R, n=9, h=(14, 30), tint="#35406E")

    # camp: lit rigs in a horseshoe, harpoon wagons facing the field
    B.mother_rig((-15.0, 10.0), yaw=0.35, rng=R, lit=True)
    B.kettle_rig((-31.0, 28.0), yaw=0.9, rng=R, lit=True, steam=True, horn_yaw=0.0)
    B.rig_bus((15.0, 8.0), yaw=-0.25, body="teal", trim="red", length=9.0, rng=R, lit=True, flagcol="haz", smoke=True)
    B.rig_bus((25.0, 22.0), yaw=-0.5, body="plum", trim="ochre", length=8.5, rng=R, lit=True, flagcol="turq")
    B.rig_bus((-7.0, 2.5), yaw=0.15, body="ochre", trim="teal", length=8.0, rng=R, lit=True, flagcol="red", awning=False)
    for x, y, yw in ((-5.0, 20.0, 0.05), (3.0, 23.0, -0.04), (11.0, 21.0, -0.15), (-12.0, 25.0, 0.2)):
        B.harpoon_wagon((x, y), yaw=yw, rng=R, aim=0.0, lit=True)
    B.buggy((8.0, 3.0), yaw=0.6, body="haz", rng=R, lit=True)
    B.buggy((-2.0, 11.0), yaw=2.9, body="red", rng=R, lit=True)
    # fires and lanterns, people
    for fp in ((2.5, 6.5), (-9.0, 16.0), (19.0, 15.0)):
        K.fire(fp, size=0.5, rng=R, light=False)
        B.lantern((fp[0], fp[1], 0.5), "#FF9A3C", 70.0, light=900.0)
        for a in (0.4, 1.9, 3.3, 4.8):
            B.lb_figure((fp[0] + math.cos(a) * 1.8, fp[1] + math.sin(a) * 1.8), a + math.pi, R, R.choice(["scav", "tinker", "walker"]), lit=True)
    B.crowd(R, [(-1.0, 16.0), (0.5, 17.0), (6.0, 18.0), (7.0, 19.0), (-8.0, 22.0), (14.0, 17.0), (-3.0, 26.0), (9.0, 27.0)], yaw=0.0, lit=True)
    for x in (-18.0, -9.0, 0.0, 9.0, 18.0):
        B.lantern((x, 30.0 + R.uniform(-1, 1), 1.0), R.choice(["#FFB347", "#FF8A3C"]), 40.0)
    K.crate_stack((-4.0, 8.0), yaw=0.5, rng=R)
    K.drum_cluster((5.5, 12.0), rng=R, n=4)
    K.crate_stack((22.0, 12.0), yaw=0.2, rng=R)
    K.tufts(R, n=500, xr=(-40, 40), yr=(-6, 60))
    K.debris_field(R, n=60, xr=(-30, 30), yr=(-4, 50))

    # the Directorate hatch field
    for p, yw, op in (((-14.0, 70.0), 0.4, 1.1), ((11.0, 66.0), -0.3, 1.0), ((26.0, 84.0), 0.2, 1.2)):
        B.hatch(p, yw, open_ang=op)
    B.vent_tower((-4.0, 94.0), h=24.0)
    for p, yw in (((-26.0, 82.0), -0.2), ((-6.0, 80.0), 0.0), ((17.0, 96.0), 0.3), ((38.0, 74.0), -0.4)):
        B.pillbox(p, yw)
    B.loudspeaker_mast((34.0, 104.0), h=28.0)
    K.sandbag_wall((-9.0, 78.0), (-1.0, 78.0), rows=2, rng=R)
    K.dust_cloud((-90, 170), n=22, length=40, rng=R, tint="#D8A07A")

    # light: the sun has just gone, a last orange glow low front-left; cool blue sky fill; green and lantern light do the rest
    sunv = B.sun_dir(-52.0, 2.5)
    B.sky(sunv, stops=[(0.0, "#F7B25C"), (0.03, "#EE8A5E"), (0.09, "#BA5F82"), (0.2, "#5E5CA2"), (0.45, "#24366F"), (1.0, "#0B1440")],
          cloud_stops=[(0.03, "#FF9C5E"), (0.15, "#C0607E"), (0.4, "#4A4A92")], glow="#FF9A5A", glow_amt=1.6,
          strength=float(os.environ.get("STILL_SKY", "1.15")))
    B.key_sun(sunv, float(os.environ.get("STILL_SUN", "7.0")), (1.0, 0.55, 0.28), angle=2.0)
    K.area_light((40, -30, 36), (0, 30, 2), 70, float(os.environ.get("STILL_FILL", "32000")), (0.40, 0.52, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0016")), top=40.0, colour="#A9B2E8")
    K.camera((CAM[0], CAM[1], 9.6), (2.0, 52.0, 2.5), lens=26)


if __name__ == "__main__":
    B.run(build, "still_left_behind_act3", ROOT)
