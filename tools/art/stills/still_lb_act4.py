"""Act-4 opener still for the Left Behind campaign (lb_04 The Banner): winter on the Ash Reckoning, the Expedition's beachhead on the south shore.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_lb_act4.py     (STILL_PREVIEW=1 for 640x360)
Writes data/images/stills/still_left_behind_act4.png.  Original scene, no text in the image.
Scene: from the Drift's camp, the war-captain's banner snaps over the foreground; Rolling Fort, harpoon wagons and autogyros hold the ash
plain; a counter-landing of marines crosses the flats; far off the Expedition beachhead with guns, pile houses, depot and ships on a cold sea.
Pale winter light breaks through grey cloud on the right; fires and gun flashes glow on the shore; ash drifts through the air.
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
from mathutils import Vector  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))


def height(x, y):
    h = B.dune_height(x, y, amp=3.2, centre=(0, 60), r0=75.0, r1=150.0, seed=4.1)
    h = h * (1 - B.smoothstep(175.0, 205.0, y)) - 2.6 * B.smoothstep(196.0, 236.0, y)
    return h


def banner(loc, h=15.0, length=9.0, yaw=0.0, rng=None):
    """The war-captain's banner: a tall mast and a long patchwork flag streaming left in the wind."""
    rng = rng or random.Random(61)
    x, y = loc
    K.cyl("mast", (x, y, h / 2), 0.14, h, K.mat("ash_d", B.LB["ash_d"]), verts=8)
    K.sphere("finial", (x, y, h + 0.2), 0.25, K.mat("haz", B.LB["haz"]))
    rows = ["red", "canvas_l", "teal_l", "haz", "red_l", "plum", "ochre"]
    n = 5
    for r in range(7):
        z = h - 0.8 - r * 1.15
        for s in range(n):
            t = (s + 0.5) / n
            wave = math.sin(t * 5.5 + r * 0.6) * 0.5 * t
            col = B.LB[rows[(r + (s // 3)) % len(rows)]] if (s + r) % 4 else B.LB[rows[(r * 2 + s) % len(rows)]]
            K.box("cloth", (x - length * t - 0.0, y + wave + 0.4 * t * t, z - 0.35 * t * t), (length / n * 1.03, 0.05, 1.12),
                  K.mat(col, rough=1.0), rot=(0, 0.06 * t, 0.12 * math.sin(t * 5.5 + r * 0.6)))
    for i in range(4):
        B.pole((x, y, 1.0 + i * 0.1), (x + 3.0 * (i - 1.5), y - 1.5, 0.05), 0.02, K.mat("ash", B.LB["ash"]))


def build():
    R = random.Random(2104)
    gm = B.dune_mat([(0.25, "#5A5653"), (0.5, "#7C7872"), (0.8, "#B2AEA6")], ripple=0.35, speck=("#3F3C3A", "#C9C5BE"), ripple_scale=1.2)
    B.terrain(None, None, height, gm, cell=1.8, grow=0.065, flat=45.0, ext=(320, -60, 360))
    B.sea(203.0, z=-0.45, col="#27414F")
    K.ridge(480, R, n=7, h=(6, 12), tint="#46506A")

    # the Drift: Rolling Fort, kettle, harpoon wagons, rigs, crowd
    B.rolling_fort((-16.0, 17.0), yaw=0.35, rng=R)
    B.kettle_rig((-33.0, 38.0), yaw=0.15, rng=R, steam=True, horn_yaw=0.0)
    B.rig_bus((24.0, 14.0), yaw=-0.3, body="teal", trim="red", length=9.0, rng=R, flagcol="haz", smoke=True)
    B.rig_bus((36.0, 34.0), yaw=-0.1, body="ochre", trim="teal", length=8.5, rng=R, flagcol="red")
    for x, y, yw in ((-2.0, 30.0, 0.05), (7.0, 36.0, -0.06), (16.0, 44.0, -0.1), (-9.0, 42.0, 0.1)):
        B.harpoon_wagon((x, y), yaw=yw, rng=R, aim=0.0)
    B.buggy((3.5, 52.0), yaw=0.1, body="haz", rng=R)
    B.buggy((-6.0, 58.0), yaw=-0.2, body="red", rng=R)
    B.buggy((24.0, 62.0), yaw=0.3, body="turq", rng=R)
    B.rotor((-6.0, 34.0), yaw=0.1, z=15.0, rng=R)
    B.rotor((20.0, 66.0), yaw=-0.3, z=11.0, rng=R)
    B.rotor((-24.0, 82.0), yaw=0.2, z=9.0, rng=R)
    B.crowd(R, [(-4.0, 22.0), (-2.0, 24.0), (4.0, 26.0), (5.5, 28.0), (11.0, 28.0), (-8.0, 30.0), (13.0, 34.0), (0.0, 38.0), (2.0, 46.0),
                (-4.0, 48.0), (9.0, 50.0), (-12.0, 54.0), (14.0, 56.0)], yaw=0.0)
    for p in ((-1.6, 0.5), (0.9, 2.2), (-3.4, 3.4)):
        B.lb_figure(p, 0.05 + R.uniform(-0.2, 0.2), R, R.choice(["scav", "slinger", "walker"]), scale=1.12)
    banner((5.6, 3.5), h=15.5, length=10.0, rng=R)
    K.crate_stack((-7.0, 8.0), yaw=0.4, rng=R)
    K.drum_cluster((9.0, 12.0), rng=R, n=4)
    K.rubble_pile((-6.0, -6.0), r=1.4, n=14, rng=R, h=0.8)
    K.rubble_pile((10.0, -5.0), r=1.2, n=10, rng=R, h=0.6)
    K.debris_field(R, n=80, xr=(-30, 30), yr=(-8, 70))
    K.tire_tracks([(-3, -12), (-2, 0), (-1.5, 12), (-1, 24)], gap=1.8, col="#4C4A47")
    K.tire_tracks([(6, -12), (6.4, 0), (6.6, 12), (6.5, 24)], gap=1.8, col="#4C4A47")
    K.tufts(R, n=300, xr=(-30, 30), yr=(-8, 50), scale=0.8)

    # the counter-landing crossing the plain, and the wreck of the supply lander on the east plain
    marines = []
    for i in range(20):
        marines.append((R.uniform(-30, 20), R.uniform(98, 132)))
    for p in marines:
        B.marine(p, math.pi + R.uniform(-0.4, 0.4), R, scale=1.0)
    B.landing_craft((-8.0, 150.0), yaw=math.pi + 0.15)
    K.fire((-8.0, 146.0), size=1.6, rng=R)
    # lander wreck
    wx, wy = 62.0, 72.0
    K.box("lander", (wx, wy, 1.8), (6.0, 10.0, 3.4), K.mat(B.EXP["white"], rough=0.6), rot=(0.12, 0.08, 0.5))
    K.box("lander2", (wx + 2.0, wy - 5.0, 1.4), (5.0, 5.0, 2.4), K.mat(B.EXP["orange"], rough=0.6), rot=(0.1, 0.0, 0.3))
    for i in range(7):
        K.crate((wx + R.uniform(-9, 9), wy + R.uniform(-10, 4), 0), R.uniform(0.7, 1.4), R.uniform(0, 3), R.choice(["#6B5A3C", "#8A7650", "#4F5A48"]))
    K.fire((wx - 1, wy + 3), size=1.8, rng=R)
    K.smoke_column((wx - 1, wy + 3), h=34, r0=1.2, n=10, rng=R)

    # the Expedition beachhead
    B.pile_house((88.0, 196.0), yaw=0.0)
    B.pile_house((110.0, 204.0), yaw=0.1)
    B.gun_battery((-22.0, 190.0), yaw=-0.1)
    B.gun_battery((24.0, 186.0), yaw=0.05)
    for i in range(6):
        B.container((46 + (i % 3) * 3.4, 194 + (i // 3) * 7.0, 2.6 if i == 5 else 0), yaw=0.05, col=None, s=(2.4, 6.0, 2.6))
    for i in range(5):
        K.box("tent", (58 + i * 4.5, 186 + R.uniform(-3, 3), 1.4), (3.4, 4.0, 2.6), K.mat(B.EXP["white"], rough=0.9), rot=(0, 0, 0.05 * i))
    K.box("crane", (40.0, 214.0, 7.0), (1.0, 1.0, 14.0), K.mat(B.EXP["orange"], rough=0.6))
    B.pole((40.0, 214.0, 14.0), (52.0, 214.0, 11.0), 0.2, K.mat(B.EXP["orange"], rough=0.6))
    B.ship((-70.0, 300.0), yaw=math.pi / 2 - 1.45, lit=True)
    B.ship((55.0, 340.0), yaw=math.pi / 2 - 1.62, L=70.0, W=12.0, lit=True)
    B.ship((150.0, 280.0), yaw=math.pi / 2 - 1.50, L=55.0, W=10.0, lit=True)
    B.landing_craft((10.0, 207.0), yaw=0.0)
    # fires, shell bursts and gun flashes on the shore
    for p, sz in (((14.0, 198.0), 3.5), ((62.0, 200.0), 4.5), ((-40.0, 196.0), 3.0), ((100.0, 190.0), 3.5)):
        K.fire(p, size=sz, rng=R)
    K.smoke_column((62, 202), h=70, r0=3.0, n=14, rng=R)
    K.smoke_column((14, 200), h=50, r0=2.4, n=12, rng=R)
    K.smoke_column((-40, 198), h=46, r0=2.0, n=10, rng=R)
    for gx in (-22.0, 24.0):
        fl = (gx, 190.0 - 5.0 if gx < 0 else 186.0 - 5.0, 3.2)
        K.sphere("flash", fl, 1.4, K.mat("haz", "#FFD27A", emit="#FFD27A", estr=120.0))
        K.point_light(fl, 9.0e4, (1.0, 0.7, 0.35), 1.5)
    B.falling_ash(R, 380, (-24, -8, 0.2), (30, 70, 16), r=(0.02, 0.06))
    B.falling_ash(R, 160, (-60, 60, 1), (60, 150, 30), r=(0.05, 0.12))

    # light: pale low winter sun breaking through on the right, heavy grey cloud, cool fill
    sunv = B.sun_dir(30.0, 5.0)
    B.sky(sunv, stops=[(0.0, "#F4CCA0"), (0.04, "#DDB097"), (0.12, "#A398AC"), (0.30, "#6E7A94"), (0.60, "#4B5873"), (1.0, "#2B3449")],
          cloud_stops=[(0.04, "#F6BE90"), (0.2, "#A98C9E"), (0.5, "#5A647C")], glow="#FFD8A8", glow_amt=1.3, cloud=(0.30, 0.52),
          band=(0.02, 0.07, 0.62, 0.98), cloud_gain=1.35, cloud_scale=2.2,
          strength=float(os.environ.get("STILL_SKY", "1.05")))
    B.key_sun(sunv, float(os.environ.get("STILL_SUN", "9.0")), (1.0, 0.78, 0.55), angle=2.0)
    K.area_light((-34, -30, 34), (0, 20, 2), 60, 14000.0, (0.55, 0.65, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0018")), top=45.0, colour="#C4C8E0")
    K.camera((1.0, -24.0, 6.8), (6.0, 110.0, 5.5), lens=24)


if __name__ == "__main__":
    B.run(build, "still_left_behind_act4", ROOT)
