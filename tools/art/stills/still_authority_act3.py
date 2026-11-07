"""Act-3 opener still for the Authority campaign (auth_03 The Coast Road): two carriers on the dune road to the sea.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_authority_act3.py
Env: STILL_PREVIEW=1 (640x360, 24 samples).  Writes data/images/stills/still_authority_act3.png.
Glass Coast, clear late afternoon: high camera on a dune, hard side light from the left, pale sand and glass,
teal sea ahead with the Directorate picket on the beach, Left Behind riders on the left crest.  No text.
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

SHORE_Y = 150.0


def roadx(y):
    return 6.0 * math.sin(y * 0.014) + 0.03 * y


def dunes(x, y):
    return (3.4 * (math.sin(x * 0.05 + math.sin(y * 0.021) * 2.0) + 1.0)
            + 2.2 * (math.sin(x * 0.11 + y * 0.07) + 1.0) * 0.5
            + 1.5 * (math.sin(y * 0.045 - x * 0.03) + 1.0))


def zfun(x, y):
    dx = abs(x - roadx(y))
    mask = X.smooth(9.0, 30.0, dx)
    z = mask * (0.55 * dunes(x, y) + 0.03 * max(0.0, dx - 12.0))
    z += 4.0 * math.exp(-((x + 12) ** 2 + (y + 14) ** 2) / 200.0)          # camera dune
    z += 9.0 * math.exp(-((x + 50) ** 2 + (y - 74) ** 2) / 420.0)          # left crest (riders)
    fade = 1.0 - X.smooth(120.0, 158.0, abs(x))
    fade *= X.smooth(-90.0, -60.0, y) * (1.0 - X.smooth(110.0, 140.0, y))
    return z * fade


def build():
    R = random.Random(303)
    X.flat_ground(X.sand_mat())
    X.terrain(zfun, X.sand_mat(), xr=(-160, 160), yr=(-90, 160), step=2.5)

    # sea ahead and an irregular surf line
    shore = [(x, SHORE_Y + 5 * math.sin(x * 0.04) + 3 * math.sin(x * 0.11)) for x in range(-900, 901, 20)]
    X.poly_plane(shore + [(900, 1400), (-900, 1400)], 0.05, X.water_mat("#1D7482", 0.05, 0.4))
    X.foam_band([(x, y + 0.2) for x, y in shore if abs(x) < 200], 1.4, 0.07, R)
    X.foam_band([(x, y + 6) for x, y in shore if abs(x) < 200], 0.7, 0.07, R)

    # coast road: winding weathered asphalt
    pts = [(roadx(y), y) for y in range(-90, 151, 15)]
    for a, b in zip(pts[:-1], pts[1:]):
        K.road(a, b, 6.0)

    # carriers, escorts and walking infantry on the road (the heart of the scene)
    for yy, yw in ((24, 0.0), (44, 0.04)):
        X.carrier((roadx(yy) + 0.2, yy), yaw=math.atan2(roadx(yy + 1) - roadx(yy), 1.0))
    K.tank((roadx(8) - 1.6, 8), yaw=0.1)
    K.tank((roadx(64) + 1.4, 64), yaw=0.0)
    for (fx, fy, yw, kn) in ((-2.5, 14, 0.1, False), (2.6, 18, 0.1, False), (-2.4, 33, 0.0, False), (2.8, 52, 0.0, True), (-2.6, 56, 0.0, False)):
        K.figure((roadx(fy) + fx, fy), yw, kn)

    # Left Behind riders on the left crest, small and ominous
    for k, (rx, ry) in enumerate(((-52, 66), (-46, 74), (-56, 80), (-41, 84))):
        X.lifted(zfun(rx, ry), X.rider, (rx, ry), yaw=0.9 + k * 0.1, rng=R)

    # Directorate picket on the beach
    for k, bx in enumerate((-34, -18, -4, 12, 28, 44)):
        X.picket((bx + roadx(SHORE_Y - 12), SHORE_Y - 13 + R.uniform(-3, 3)), yaw=R.uniform(-.15, .15), tall=(k in (1, 4)))
    K.smoke_column((20, SHORE_Y - 6), h=22, r0=1.4, n=8, rng=R)
    K.fire((20, SHORE_Y - 6), size=2.0, rng=R)

    # foreground and midground detail
    K.burnt_car((-12.5, -14.0), yaw=0.35, rng=R)
    X.lifted(zfun(-9, 4), K.rubble_pile, (-9.0, 4.0), r=1.4, n=12, rng=R, h=0.8)
    K.crate_stack((8.5, -6.0), yaw=0.5, rng=R)
    K.drum_cluster((9.8, 20.0), rng=R, n=4)
    K.broken_fence((roadx(-20) + 6.0, -22), (roadx(5) + 6.2, 6), rng=R)
    K.road_sign((roadx(-6) - 4.2, -6.0), yaw=0.15)
    for k, yy in enumerate(range(-30, 140, 22)):
        X.survey_stake((roadx(yy) + (4.2 if k % 2 else -4.2), yy), R)
    K.tire_tracks([(roadx(y) + 0.3, y) for y in (-60, -45, -30, -15, 0, 15)], gap=1.8, width=0.3, col="#8A7650", z=0.06)
    K.debris_field(R, n=50, xr=(-14, 14), yr=(-20, 30))
    for p in ((roadx(-4) + 2.2, -4.0), (roadx(14) - 2.0, 14.0)):
        K.puddle(p, 1.4, 0.65, 0.1)
    X.tufts_on(R, zfun, 1300, (-70, 70), (-30, 135), avoid=lambda x, y: abs(x - roadx(y)) < 4.0,
               mat=K.grass_mat())
    # far headland and distant ships on the sea
    K.ridge(560, R, n=5, h=(18, 34), tint="#6E8CA0", spread=500)
    for sx, sy in ((-60, 420), (110, 520)):
        K.box("ship", (sx, sy, 3), (30, 8, 5), K.mat("#4A5260"))
        K.box("shipcab", (sx - 6, sy, 7), (8, 6, 5), K.mat("#5A6070"))

    # light: clear late afternoon, hard key from the left, cool sky fill, bright sea glare
    az, el = math.radians(-58.0), math.radians(24.0)
    sunv = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
    X.sky_grad(sunv, [(0.0, "#F4DDB8"), (0.05, "#E8D2B4"), (0.14, "#BCCFD8"), (0.32, "#7FAAD2"), (0.6, "#4A82BE"), (1.0, "#274F95")],
               strength=1.05, glow="#FFE2B0", glow_pow=(18.0, 160.0), glow_k=(0.8, 1.2),
               cloud_cols=("#FFEAC8", "#F4D6C0", "#C8CCE0"), cloud_range=(0.52, 0.70), cloud_scale=2.2,
               cloud_win=(0.04, 0.14, 0.4, 0.7), cloud_gain=1.6, cloud_stretch=7.0, cloud_alpha=0.8)
    key = K.sun((0, 0, 0), 9.0, colour=(1.0, 0.80, 0.58), angle=1.4)
    key.rotation_euler = (-sunv).to_track_quat("-Z", "Y").to_euler()
    K.area_light((30, -40, 40), (0, 20, 2), 70, 14000.0, (0.55, 0.65, 1.0))
    K.haze(0.0012, colour="#CFE0EA", top=45.0)
    K.camera((-10.0, -16.0, zfun(-10, -16) + 6.0), (2.0, 80.0, 1.5), lens=27)


if __name__ == "__main__":
    X.run("still_authority_act3", build)
