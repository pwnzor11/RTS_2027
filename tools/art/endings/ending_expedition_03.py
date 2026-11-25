"""Ending slide expedition 03: Dr. Roque stays on the coast with the failing valves; the Air Marshal's jets in the dunes.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_expedition_03.py   (STILL_PREVIEW=1 for 640x360)
Scene: grey noon on the Glass Coast; a silent Dredge Works with no smoke, one small figure sitting at its foot; beyond, in the pale
dunes, a row of jets parked nose-down with nothing near them, the empty carrier far off on the sea.  Original scene, no text.
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
    R = random.Random(5103)
    E.ground_with(E.sand_mat(dry="#B8A27C"))
    E.dunes(R, n=18, xr=(-140, 140), yr=(40, 160), h=(3, 8), r=(16, 34))
    E.sea(210.0, shallow="#5AA09C", deep="#16384C", width=60, sheen=0.1)
    shore = [(-240 + i * 20, 210 + 1.0 * math.sin(i * 0.7)) for i in range(25)]
    E.foam_line(shore, width=1.0, rng=R)
    E.carrier((30, 300), math.pi / 2 + 0.2, 1.2, wake_on=False, jets=0)
    # silent Dredge Works at the foot of the view, valves in the foreground
    E.dredge_works((-14, 36), 0.35, 1.25)
    for k in range(4):
        K.cyl("valve", (14 + k * 1.6, 8 + k * 0.3, 0.55), 0.35, 1.1, E.m("#B0402A"), verts=8)
        K.cyl("valvewheel", (14 + k * 1.6, 8 + k * 0.3, 1.2), 0.45, 0.08, E.m("#8C2E1E"), verts=10)
    K.cyl("pipe", (17, 8.8, 0.45), 0.3, 12.0, E.m("#6A6E70"), rot=(0, math.pi / 2, 0.05), verts=8)
    E.xfigure((11.0, 6.0), 1.2, kneel=True, hold="crate", scale=1.0)
    E.crate_stack_x((-6, 12), 0.2, rng=R, n=4)
    # jets abandoned in the dunes
    for i, (x, y) in enumerate(((22, 44), (36, 56), (50, 68), (64, 80), (78, 92))):
        E.jet((x, y), 2.4 + 0.05 * i, z=1.2 + 0.1 * i, s=3.0, bank=0.0)
    K.tire_tracks([(30, 40), (38, 56), (46, 72), (62, 90)], gap=2.0, width=0.3, col="#8A7650", z=0.06)
    E.glass_shards(R, n=24, xr=(-120, 120), yr=(60, 190), h=(2, 7), tint="#5AA79C")
    E.glass_shards(R, n=10, xr=(-20, 40), yr=(2, 30), h=(0.8, 2.2), tint="#5AA79C")
    K.debris_field(R, n=40, xr=(-30, 40), yr=(-6, 30))
    K.tufts(R, n=200, xr=(-30, 40), yr=(-6, 40), scale=0.6)
    K.ridge(620, R, n=8, h=(14, 28), tint="#7A8898")
    sunv = EK.sunvec(10.0, 40.0)
    E.sky_custom(sunv, [(0.0, "#DCE2DC"), (0.06, "#B4C6C8"), (0.2, "#8CA4B8"), (0.5, "#5C7898"), (1.0, "#3A5276")],
                 glow="#FFF0D0", cloud_stops=[(0.04, "#F4F4EC"), (0.2, "#B8C4CC"), (0.5, "#788CA0")],
                 strength=0.95, cloud_cov=(0.38, 0.55), cloud_gain=1.3, glow_gain=1.0, cloud_band=(0.0, 0.18, 0.6, 0.9))
    EK.aim_sun(float(os.environ.get("STILL_SUN", "4.0")), sunv, (1.0, 0.94, 0.84), angle=3.0)
    K.area_light((-34, -30, 34), (0, 40, 2), 60, 9000.0, (0.6, 0.7, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0010")), top=50.0, colour="#C8D4D8")
    K.camera((-6.0, -22.0, 8.0), (10.0, 90.0, 2.0), lens=24)


if __name__ == "__main__":
    EK.run(build, "ending_expedition_03")
