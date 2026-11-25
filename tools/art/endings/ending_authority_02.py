"""Ending slide authority 02: the map of the Glass Coast finished; the convoys went to the coast and did not all come back.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_authority_02.py   (STILL_PREVIEW=1 for 640x360)
Scene: low gold evening on the coast road; a short convoy of carriers straggling home with gaps between them, a line of survey
stakes marching down to the glassy shore behind, a few walkers beside the road.  Original scene, no text.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import endkit as EK  # noqa: E402
import still_authority_extra as X  # noqa: E402
import still_kit as K  # noqa: E402
import bpy  # noqa: E402

SHORE_Y = 140.0


def roadx(y):
    return 6.0 * math.sin(y * 0.014) + 0.03 * y


def dunes(x, y):
    return (3.4 * (math.sin(x * 0.05 + math.sin(y * 0.021) * 2.0) + 1.0) + 1.5 * (math.sin(y * 0.045 - x * 0.03) + 1.0))


def zfun(x, y):
    dx = abs(x - roadx(y))
    z = X.smooth(9.0, 30.0, dx) * 0.5 * dunes(x, y)
    fade = (1.0 - X.smooth(120.0, 158.0, abs(x))) * X.smooth(-90.0, -60.0, y) * (1.0 - X.smooth(100.0, 130.0, y))
    return z * fade


def build():
    R = random.Random(5302)
    X.flat_ground(X.sand_mat())
    X.terrain(zfun, X.sand_mat(), xr=(-160, 160), yr=(-90, 140), step=2.5).location.z = -0.12
    shore = [(x, SHORE_Y + 5 * math.sin(x * 0.04) + 3 * math.sin(x * 0.11)) for x in range(-900, 901, 20)]
    X.poly_plane(shore + [(900, 1400), (-900, 1400)], 0.05, X.water_mat("#1D6A78", 0.05, 0.35))
    X.foam_band([(x, y + 0.2) for x, y in shore if abs(x) < 200], 1.4, 0.07, R)
    pts = [(roadx(y), y) for y in range(-90, 142, 15)]
    for a, b in zip(pts[:-1], pts[1:]):
        K.road(a, b, 6.0)
    # straggling convoy returning (facing the camera), with gaps
    for yy in (14, 44, 96):
        X.carrier((roadx(yy), yy), yaw=math.pi + 0.03)
    K.tank((roadx(82) + 1.0, 82), yaw=math.pi)
    for (fx, fy) in ((-2.6, 10), (2.4, 34), (-2.2, 70), (2.8, 96)):
        K.figure((roadx(fy) + fx, fy), math.pi, False)
    # survey stakes running down to the coast on both sides, a few glass shards
    for k, yy in enumerate(range(-30, 132, 14)):
        X.survey_stake((roadx(yy) + (5.0 if k % 2 else -5.0), yy), R)
    K.burnt_car((roadx(40) + 7.0, 40), yaw=0.8, rng=R)
    K.crate_stack((roadx(-4) + 6.0, -4), yaw=0.3, rng=R)
    K.tire_tracks([(roadx(y) + 0.3, y) for y in (-60, -45, -30, -15, 0, 15)], gap=1.8, width=0.3, col="#8A7650", z=0.06)
    K.debris_field(R, n=40, xr=(-14, 14), yr=(-20, 30))
    X.tufts_on(R, zfun, 700, (-60, 60), (-30, 120), avoid=lambda x, y: abs(x - roadx(y)) < 4.0, mat=K.grass_mat())
    K.ridge(600, R, n=5, h=(16, 30), tint="#8A8CA4", spread=500)
    sunv = EK.sunvec(165.0, 13.0)
    X.sky_grad(sunv, [(0.0, "#F8D098"), (0.05, "#F0B88C"), (0.14, "#B8A0B0"), (0.32, "#6A88C0"), (0.6, "#3A5CA4"), (1.0, "#1C3A80")],
               strength=1.0, glow="#FFD8A0", glow_pow=(18.0, 160.0), glow_k=(0.8, 1.2),
               cloud_cols=("#FFD6A8", "#F0B4A4", "#B0A8CC"), cloud_range=(0.52, 0.70), cloud_scale=2.2,
               cloud_win=(0.04, 0.14, 0.4, 0.7), cloud_gain=1.5, cloud_stretch=7.0, cloud_alpha=0.8)
    EK.aim_sun(float(os.environ.get("STILL_SUN", "9.0")), sunv, (1.0, 0.72, 0.46), angle=1.6)
    K.area_light((30, -40, 40), (0, 20, 2), 70, 10000.0, (0.55, 0.65, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0008")), colour="#E4D0C0", top=45.0)
    K.camera((-10.0, -20.0, zfun(-10, -20) + 3.0), (4.0, 90.0, 1.5), lens=27)


if __name__ == "__main__":
    EK.run(build, "ending_authority_02", finish=True)
