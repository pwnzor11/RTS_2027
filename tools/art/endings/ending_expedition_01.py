"""Ending slide expedition 01: three seasons of dredging on the ash field; the fleet loads its holds at the pier.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_expedition_01.py   (STILL_PREVIEW=1 for 640x360)
Scene: calm amber evening over the ash plain; the Dredge Works at work mid-field with its conveyor and heaps, a tender and a
destroyer alongside the pier at the far shore, crates stacked ready, a telex mast lamp lit.  Original scene, no text.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import endkit as EK  # noqa: E402
import still_expedition_kit as E  # noqa: E402
import still_kit as K  # noqa: E402


def build():
    R = random.Random(5101)
    E.ground_with(E.ash_mat())
    E.sea(104.0, shallow="#4E8E8C", deep="#0E2A40", width=60, sheen=0.08)
    shore = [(-200 + i * 20, 104 + 1.2 * math.sin(i * 0.9)) for i in range(21)]
    E.wet_sand([(x, y - 2.0) for x, y in shore], width=5.0)
    E.foam_line(shore, width=1.0, rng=R)
    # pier and the fleet alongside
    E.pier((34, 104), 0.0, length=40, width=6.5, z=2.0)
    E.tender((46, 142), math.pi, 1.0, wake_on=False)
    E.destroyer((-34, 168), math.pi - 0.1, 1.1, wake_on=False)
    E.carrier((100, 240), math.pi + 0.05, 1.1, wake_on=False, jets=3)
    E.crate_stack_x((26, 94), 0.2, rng=R, n=5)
    E.crate_stack_x((40, 90), -0.1, rng=R, n=4)
    E.crate_stack_x((12, 84), 0.3, rng=R, n=5, s=1.0)
    E.depot((-6, 86), 0.1, 1.0)
    # the Dredge Works at work, a second smaller one behind, hover skiffs ferrying
    E.dredge_works((-18, 58), 0.3, 1.4)
    E.dredge_works((70, 70), -0.2, 0.9)
    E.hover_skiff((12, 66), 1.3, 1.1, z=0.4)
    E.hover_skiff((-4, 80), 1.5, 1.0, z=0.4)
    # worked ground: dredged cuts, heaps, tracks
    K.tire_tracks([(10, 28), (12, 50), (16, 75), (22, 80), (28, 96)], gap=2.2, width=0.4, col="#1E1815")
    for k in range(9):
        x, y = R.uniform(-60, 90), R.uniform(10, 120)
        o = K.cyl("heap", (x, y, 0.5), R.uniform(4, 9), 1.5, E.m("#5E5850", 1.0), verts=9, r2=R.uniform(1, 3))
        K.nolines(o)
    K.debris_field(R, n=60, xr=(-30, 30), yr=(-6, 40))
    for p in ((-3, 12), (-9, 24), (6, 36)):
        E.xfigure(p, 0.3, kneel=False, hold="crate")
    E.crate_stack_x((-14, 4), 0.2, rng=R, n=4)
    K.rubble_pile((4, 2), r=1.2, n=10, rng=R, h=0.6)
    E.ash_flakes(R, n=160, xr=(-24, 24), yr=(-6, 50), zr=(0.4, 10), size=0.03)
    E.rock_mesa((-120, 200), r=30, h=9, rng=R, tint="#5E5E62")
    K.ridge(560, R, n=7, h=(14, 28), tint="#6A6478")

    sunv = EK.sunvec(-58.0, 8.0)
    E.sky_custom(sunv, [(0.0, "#F6C88A"), (0.06, "#E49C7A"), (0.2, "#B08C98"), (0.5, "#4E5E8C"), (1.0, "#182246")],
                 glow="#FFD8A0", cloud_stops=[(0.04, "#FFD2A0"), (0.2, "#B48098"), (0.5, "#4A4C78")],
                 strength=0.95, cloud_cov=(0.42, 0.60), cloud_gain=1.5, glow_gain=2.0, cloud_band=(0.0, 0.14, 0.5, 0.85))
    EK.aim_sun(float(os.environ.get("STILL_SUN", "9.0")), sunv, (1.0, 0.72, 0.46), angle=1.6)
    K.area_light((34, -30, 34), (0, 40, 2), 60, 12000.0, (0.55, 0.62, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0013")), top=50.0, colour="#D8B8A8")
    K.camera((-2.0, -22.0, 15.0), (6.0, 90.0, -2.0), lens=24)


if __name__ == "__main__":
    EK.run(build, "ending_expedition_01")
