"""Act-4 opener still for the Authority campaign (auth_04 The Sunrise Device): the river crossing under ashfall.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_authority_act4.py
Env: STILL_PREVIEW=1 (640x360, 24 samples).  Writes data/images/stills/still_authority_act4.png.
Ash Reckoning, night and ashfall: low camera on the north bank among the Authority column, a pontoon bridge over the
black river, and the Directorate's Sunrise Device glowing on the far bank under a burning sky.  No text.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import still_authority_extra as X  # noqa: E402
import still_kit as K  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402


def yc(x):
    return 64.0 + 6.0 * math.sin(x * 0.03)


def zfun(x, y):
    base = 0.5 * math.sin(x * 0.07) * math.cos(y * 0.05) + 0.3 * math.sin(x * 0.19 + y * 0.13)
    base *= X.smooth(10, 40, abs(y - 10))
    d = abs(y - yc(x))
    trench = -3.4 * (1.0 - X.smooth(16.0, 28.0, d))
    z = base + trench
    fade = (1.0 - X.smooth(300.0, 380.0, abs(x))) * X.smooth(-90.0, -60.0, y) * (1.0 - X.smooth(560.0, 640.0, y))
    return z * fade


def dead_tree(loc, R, h=7.0):
    x, y = loc
    m = K.mat("#1E1B18", rough=1.0)
    K.cyl("trunk", (x, y, h / 2), 0.35, h, m, verts=6, r2=0.12)
    for k in range(5):
        a = R.uniform(0, math.tau)
        L = R.uniform(1.8, 3.2)
        K.cyl("branch", (x + math.cos(a) * L * 0.4, y + math.sin(a) * L * 0.4, h * (0.55 + 0.09 * k)), 0.08, L, m,
              rot=(math.sin(a) * 0.9, -math.cos(a) * 0.9, 0), verts=4)


def build():
    R = random.Random(404)
    X.flat_ground(X.ash_mat(), z=-0.01, size=3000)
    X.terrain(zfun, X.ash_mat(), xr=(-400, 400), yr=(-90, 640), step=4.0)
    # black river in the trench, with the sky burning in it
    X.poly_plane([(-1400, yc(-1400) - 30), (1400, yc(1400) - 30), (1400, yc(1400) + 30), (-1400, yc(-1400) + 30)], -1.5,
                 X.water_mat("#0B0D12", 0.035, 0.22))

    # pontoon bridge, and the column moving up to it
    for k in range(10):
        y0 = 38 + k * 5.0
        K.box("deck", (3.0, y0, 0.1), (6.5, 5.0, 0.3), K.mat("#4B4A44"))
        if k > 0:
            K.box("pontoon", (3.0, y0, -1.1), (5.0, 4.2, 1.1), K.mat("#2C3326"))
        for sd in (-1, 1):
            K.box("rail", (3.0 + sd * 3.3, y0, 0.55), (0.1, 5.0, 0.6), K.mat("steel_d"))
    K.tank((3.0, 52), yaw=0.0)
    K.tank((2.5, 12), yaw=0.05)
    K.tank((-8.0, 4), yaw=-0.15)
    K.tank((12.5, 24), yaw=0.1)
    X.carrier((-9.5, 30), yaw=0.04)
    for (fx, fy, yw, kn) in ((-5.0, 18, 0.1, False), (-3.6, 22, 0.05, True), (7.2, 8, 0.0, False), (8.4, 11, 0.1, True), (-12.5, 16, 0.0, False),
                              (0.4, 31, 0.0, False), (5.6, 34, 0.0, True), (-6.4, -2, 0.1, True), (-8.0, -4, 0.0, False)):
        K.figure((fx, fy), yw, kn, 1.0)
    K.sandbag_wall((-7.5, -6.0), (-1.5, -6.0), rows=3, rng=R)
    K.sandbag_wall((9.0, 14.0), (16.0, 14.0), rows=2, rng=R)
    K.turret((-18.0, 20.0), yaw=0.2)
    K.turret((20.0, 34.0), yaw=-0.1)

    # foreground clutter
    K.burnt_car((7.5, -10.0), yaw=2.4, rng=R)
    K.rubble_pile((-4.4, -10.5), r=1.5, n=16, rng=R, h=0.9)
    K.crater((-0.5, -14.0), R=2.4, rng=R, rim=0.3)
    K.crate_stack((-10.2, -8.0), yaw=0.4, rng=R)
    K.drum_cluster((12.0, -5.0), rng=R, n=4)
    K.broken_fence((-14, 6), (-30, 14), rng=R)
    K.debris_field(R, n=70, xr=(-30, 30), yr=(-20, 32))
    K.tire_tracks([(2.0, y) for y in (-80, -60, -40, -20, 0, 20, 36)], gap=2.0, width=0.35, col="#141210", z=0.03)
    for p in ((-3.0, -3.0), (5.5, -6.0)):
        K.puddle(p, 1.2, 0.6, 0.2)
    for tx, ty in ((-24, 26), (30, 12), (-40, 46), (46, 40), (-16, -20)):
        dead_tree((tx, ty), R, h=R.uniform(5, 8))
    # far bank: the Directorate base with the Sunrise Device, pickets and fires
    X.sunrise_device((10, 175), R)
    for k, bx in enumerate((-50, -30, -10, 28, 46)):
        X.picket((bx, 104 + R.uniform(-4, 4)), yaw=R.uniform(-.1, .1), tall=(k % 2 == 0))
    K.smoke_column((-70, 165), h=60, r0=3.0, n=12, rng=R, tint="#2A2624")
    K.fire((-70, 162), size=4.5, rng=R)
    K.smoke_column((95, 150), h=50, r0=2.6, n=11, rng=R, tint="#2A2624")
    K.fire((95, 148), size=4.0, rng=R)
    K.fire((-15, 108), size=2.0, rng=R, light=False)
    K.skyline(R, 260, n=9, xr=(-200, 200), h=(10, 30), tint="#1C1A22")
    K.ridge(760, R, n=9, h=(40, 80), tint="#241F2C", spread=900)
    # falling ash
    X.ash_flakes(R, n=520, xr=(-14, 14), yr=(-12, 50), zr=(0.2, 10), colour="#A8A29A")

    # light: moonless; the device is the sun.  Cool rim from the sky behind the camera, warm light from ahead.
    sunv = Vector((0.06, 1.0, 0.06))
    X.sky_grad(sunv, [(0.0, "#9A3414"), (0.04, "#5E2418"), (0.14, "#34222A"), (0.30, "#16161F"), (0.6, "#0B0C14"), (1.0, "#05060B")],
               strength=0.55, glow="#FF8A38", glow_pow=(5.0, 40.0), glow_k=(2.2, 2.4),
               cloud_cols=("#FF6A28", "#8A3A30", "#2A2030"), cloud_range=(0.44, 0.62), cloud_scale=2.4,
               cloud_win=(0.02, 0.1, 0.38, 0.65), cloud_gain=0.8, cloud_stretch=6.0, cloud_alpha=0.75)
    moon = K.sun((0, 0, 0), 0.9, colour=(0.55, 0.62, 1.0), angle=4.0)
    mv = Vector((-0.5, -0.9, 0.45)).normalized()
    moon.rotation_euler = (-mv).to_track_quat("-Z", "Y").to_euler()
    K.area_light((0, -40, 25), (0, 20, 2), 80, 2500.0, (0.45, 0.5, 0.9))
    K.point_light((0, 60, 30), 1.5e5, (1.0, 0.5, 0.25), 20.0)        # the burning sky over the river
    K.haze(0.0016, colour="#4A3A3A", aniso=0.35, top=80.0)
    K.camera((-3.0, -26.0, 4.2), (5.0, 110.0, 6.0), lens=24)


if __name__ == "__main__":
    X.run("still_authority_act4", build)
