"""Act-2 opener still for the Left Behind campaign (lb_02 Road Posts): golden late afternoon, Rust Belt, the camp is down.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_lb_act2.py     (STILL_PREVIEW=1 for 640x360)
Writes data/images/stills/still_left_behind_act2.png.  Original scene, no text in the image.
Scene: Calloway-Ross's Kettle rig and the bus rigs parked on a ruined road, a campfire and buggy in front; far up the road an
Authority road post with its power plant burning.  Low gold sun front-left, blue-teal sky, rim-lit steam and smoke.
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


def build():
    R = random.Random(2102)
    K.ground()
    # road north through the belt, with a cross road far up where the post stands
    K.road((0, -40), (0, 220), 7)
    K.road((-120, 96), (120, 96), 6)
    jp = K.box("jpatch", (0, 96, 0.045), (7, 7, 0.03), K._mats["road"])
    K.nolines(jp)

    # ruins and pylons
    for loc, w, d, h, rot in (((-30, 14), 12, 5, 8, 0.1), ((26, 30), 10, 4, 6, -0.2), ((-26, 58), 14, 5, 9, 0.0),
                              ((32, 66), 12, 5, 7, 0.15), ((-48, 36), 10, 4, 12, 0.0), ((-20, 88), 9, 4, 6, 0.1)):
        K.ruin(loc, w, d, h, R, rot=rot)
    for i, py in enumerate((-4, 22, 50, 80, 112)):
        K.pylon((-15.0, py), 12.5 - i * 0.4)
    K.power_line([(-15.0, p) for p in (-4, 22, 50, 80, 112)], 12.5, sag=1.5)
    K.skyline(R, 210, n=12, xr=(-160, 160), h=(8, 26), tint="#4A4258")
    K.skyline(R, 140, n=7, xr=(-100, 100), h=(6, 15), tint="#575069")
    K.ridge(430, R, n=9, h=(28, 55), tint="#4E5578")

    # caravan: the Kettle in 3/4 view on the left, buses along the right, the Mother Rig up the west side
    B.kettle_rig((-9.0, 17.0), yaw=-0.45, rng=R, lit=False, steam=True, horn_yaw=0.3)
    B.rig_bus((9.8, 6.0), yaw=0.12, body="turq", trim="red", length=9.5, rng=R, smoke=True, flagcol="haz")
    B.rig_bus((11.5, 24.0), yaw=-0.08, body="red", trim="canvas", length=8.5, rng=R, flagcol="turq", awning=True)
    B.rig_bus((-17.0, 33.0), yaw=1.45, body="ochre", trim="teal", length=9.0, rng=R, flagcol="red")
    B.mother_rig((-24.0, 50.0), yaw=1.2, rng=R)
    B.buggy((6.4, -4.8), yaw=2.4, body="haz", rng=R)
    B.buggy((-2.0, 9.0), yaw=0.2, body="red", rng=R)
    B.harpoon_wagon((5.0, 40.0), yaw=0.05, rng=R, aim=0.2)

    # campfire with people (foreground right)
    fp = (5.8, -3.5)
    K.fire(fp, size=0.55, rng=R, light=False)
    B.lantern((fp[0], fp[1], 0.6), "#FF9A3C", 60.0, light=700.0)
    for a in (0.3, 1.5, 2.6, 3.9, 5.1):
        B.lb_figure((fp[0] + math.cos(a) * 1.9, fp[1] + math.sin(a) * 1.9), a + math.pi, R, R.choice(["scav", "tinker", "walker"]))
    B.crowd(R, [(-5.6, 6.5), (-4.8, 8.0), (6.0, 14.0), (6.8, 16.0), (-5.0, 17.0), (4.0, 28.0)], yaw=0.6)
    B.lb_figure((-3.0, 8.5), 0.4, R, "slinger")
    B.lb_figure((1.6, 15.0), -0.5, R, "slinger")
    B.lb_figure((-4.3, 4.0), 0.2, R, "tinker")

    # foreground and midground clutter
    K.rubble_pile((-5.6, -9.0), r=1.5, n=16, rng=R, h=0.9)
    K.rubble_pile((-7.2, -6.2), r=1.0, n=8, rng=R, h=0.5)
    K.drum_cluster((-7.0, -2.0), rng=R, n=4)
    K.crate_stack((8.2, -0.5), yaw=0.4, rng=R)
    K.burnt_car((4.0, 20.0), yaw=2.9, rng=R)
    K.crate_stack((-4.0, 36.0), yaw=0.9, rng=R)
    K.broken_fence((6.0, 44), (6.4, 68), rng=R)
    K.road_sign((4.9, 56.0), yaw=-0.3, lean=-0.1)
    K.debris_field(R, n=90, xr=(-24, 24), yr=(-14, 60), avoid=lambda x, y: abs(x) < 3.3)
    K.puddle((-1.8, -3.5), 1.5, 0.7, 0.1)
    K.puddle((1.2, 9.0), 2.0, 0.9, 0.0)
    K.puddle((-1.0, 30.0), 2.6, 1.0, 0.05)
    K.tire_tracks([(-1.4, -20), (-1.2, -8), (-1.0, 4), (-0.8, 16)], gap=1.8)
    K.tire_tracks([(1.6, 0), (1.9, 12), (2.1, 24), (2.3, 36)], gap=1.8, col="#352B20")
    K.tufts(R, n=1500, xr=(-50, 50), yr=(-16, 90), avoid=lambda x, y: abs(x) < 3.6)
    # line of carried-off wrecks on the cart track: salvage on the horizon

    # the Authority road post up the road, its power plant burning (the hint of the mission)
    K.turret((5.5, 78.0), yaw=0.2)
    K.sandbag_wall((9.0, 74.0), (9.0, 82.0), rows=3, rng=R)
    K.sandbag_wall((1.5, 74.0), (-3.0, 74.0), rows=2, rng=R)
    K.tank((-4.0, 70.0), yaw=2.0)
    for p in ((8.0, 76.0), (7.5, 80.0), (2.5, 75.0)):
        K.figure(p, 0.4, True)
    B.power_plant((-18.0, 100.0), yaw=0.2, burning=True, rng=R)
    K.smoke_column((-17, 101), h=70, r0=2.2, n=14, rng=R)
    K.smoke_column((40, 110), h=40, r0=1.4, n=10, rng=R)
    K.fire((40, 108), size=3.0, rng=R)
    K.dust_cloud((-6, 190), n=24, length=55, rng=R, tint="#E8C690")

    # light: low gold sun front-left, cool sky fill, haze with shafts
    sunv = B.sun_dir(-36.0, 7.0)
    B.sky(sunv, stops=[(0.0, "#FFC878"), (0.05, "#F5A86A"), (0.15, "#D88F85"), (0.30, "#8C8FB5"), (0.55, "#3F73B5"), (1.0, "#16327C")],
          cloud_stops=[(0.04, "#FFC27E"), (0.16, "#F29A78"), (0.42, "#A0709A")], glow="#FFD08A",
          strength=float(os.environ.get("STILL_SKY", "1.05")))
    B.key_sun(sunv, float(os.environ.get("STILL_SUN", "10.5")), (1.0, 0.64, 0.32), angle=1.2)
    K.area_light((34, -30, 34), (0, 15, 2), 60, 15000.0, (0.46, 0.60, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0013")), top=40.0, colour="#D8D4F0")
    K.camera((-1.6, -15.0, 1.45), (0.5, 45.0, 4.8), lens=26)


if __name__ == "__main__":
    B.run(build, "still_left_behind_act2", ROOT)
