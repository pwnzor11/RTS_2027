"""Ending slide expedition 04: the Directorate redoubt a crater by autumn; the white pennant on the coast road.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_expedition_04.py   (STILL_PREVIEW=1 for 640x360)
Scene: overcast autumn afternoon; a broad scorched crater with one thin smoke thread where the redoubt stood, headframe stump leaning
at its lip; in the foreground the coast road with a pole carrying a white pennant, a Authority roadside post beside it with its
door open.  Original scene, no text.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import endkit as EK  # noqa: E402
import still_expedition_kit as E  # noqa: E402
import still_kit as K  # noqa: E402
import bpy  # noqa: E402


def build():
    R = random.Random(5104)
    E.ground_with(E.ash_mat())
    # the crater: a dark bowl built from stacked discs, rim of ash
    K.cyl("rim", (6, 90, 0.5), 66, 2.2, E.m("#4A4440", 1.0), verts=22, r2=52)
    K.cyl("bowl", (6, 90, 1.2), 44, 0.5, E.m("#1E1A18", 1.0), verts=22)
    K.crater((6, 90), R=34.0, rng=R, rim=0.35, z0=0.1)
    for a in range(0, 360, 36):
        ar = math.radians(a)
        K.rubble_pile((6 + 60 * math.cos(ar), 90 + 50 * math.sin(ar)), r=3.0, n=10, rng=R, h=1.6)
    K.cyl("leg1", (-34, 96, 6), 0.5, 13, E.m("#2E2A2C"), rot=(0, 0.35, 0.1), verts=6)
    K.cyl("leg2", (-31, 96, 5), 0.5, 11, E.m("#2E2A2C"), rot=(0, -0.2, 0.1), verts=6)
    K.smoke_column((6, 88), h=34, r0=1.6, n=7, rng=R, tint="#8A8682")
    K.fire((6, 88), size=1.0, rng=R)
    # coast road in the foreground with the pennant
    K.road((-70, 22), (70, 36), 7.0)
    K.road((70, 36), (190, 60), 7.0)
    K.cyl("pole", (18, 26, 7.0), 0.14, 14.0, E.m("#E8E8E0"), verts=6)
    K.box("pennant", (18, 24.4, 13.0), (0.05, 3.4, 1.4), E.m("#F4F4EE"))
    K.crate_stack((22, 18), yaw=0.2, rng=R)
    E.xfigure((14, 26), 0.3, hold="rifle")
    K.ruin((-42, 30), w=7, d=5, h=4, rng=R, rot=0.2)
    K.ruin((-56, 40), w=6, d=5, h=3.2, rng=R, rot=-0.1)
    K.road_sign((-12, 18), yaw=0.2)
    K.debris_field(R, n=50, xr=(-30, 40), yr=(-6, 30))
    for k in range(7):
        x, y = R.uniform(-90, 100), R.uniform(40, 220)
        o = K.cyl("drift", (x, y, 0.5), R.uniform(5, 12), 1.4, E.m("#5F5850", 1.0), verts=9, r2=R.uniform(1, 3))
        K.nolines(o)
    E.ash_flakes(R, n=120, xr=(-24, 24), yr=(-6, 50), zr=(0.4, 10), size=0.03)
    K.ridge(560, R, n=8, h=(16, 32), tint="#6A6A78")
    sunv = EK.sunvec(-30.0, 22.0)
    E.sky_custom(sunv, [(0.0, "#C8C0B4"), (0.06, "#A8A8AC"), (0.2, "#8890A0"), (0.5, "#5C6880"), (1.0, "#363E58")],
                 glow="#F0DCC0", cloud_stops=[(0.04, "#DAD2C4"), (0.2, "#98A0AC"), (0.5, "#5C6678")],
                 strength=0.9, cloud_cov=(0.30, 0.46), cloud_gain=1.2, glow_gain=0.9, cloud_band=(0.0, 0.2, 0.7, 0.95))
    EK.aim_sun(float(os.environ.get("STILL_SUN", "2.6")), sunv, (1.0, 0.88, 0.74), angle=4.0)
    K.area_light((34, -30, 34), (0, 40, 2), 60, 9000.0, (0.6, 0.7, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0012")), top=50.0, colour="#B8B8B8")
    K.camera((-14.0, -8.0, 11.0), (12.0, 90.0, 0.0), lens=24)


if __name__ == "__main__":
    EK.run(build, "ending_expedition_04")
