"""Ending slide expedition 02: the Admiral brings most of the ships home; the names of those that did not come back.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_expedition_02.py   (STILL_PREVIEW=1 for 640x360)
Scene: cold blue dawn in the home harbour; the fleet lies at anchor in two lines with visible gaps where hulls should be, empty
berths and a single lit lamp on the quay, a last ship still coming in from the haze.  Original scene, no text.
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
    R = random.Random(5102)
    E.ground_with(E.sand_mat(dry="#8E8A82", wet="#5A5650"))
    E.sea(40.0, shallow="#5A8C94", deep="#0C2238", width=70, sheen=0.05)
    shore = [(-200 + i * 20, 40 + 1.0 * math.sin(i * 0.8)) for i in range(21)]
    E.wet_sand([(x, y - 2.0) for x, y in shore], width=5.0)
    E.foam_line(shore, width=0.8, rng=R)
    # quay with piers; the right pier has no ship alongside
    E.pier((-30, 40), 0.0, length=50, width=7.0, z=2.0)
    E.pier((24, 40), 0.0, length=44, width=7.0, z=2.0, crane=False)
    E.tender((-40, 78), math.pi, 1.1, wake_on=False)
    E.destroyer((-18, 120), math.pi, 1.2, wake_on=False)
    E.destroyer((30, 150), math.pi, 1.2, wake_on=False)
    E.carrier((-64, 190), math.pi, 1.2, wake_on=False, jets=0)
    E.tender((60, 205), math.pi, 1.1, wake_on=False)
    E.command_ship((0, 265), math.pi, 1.2, wake_on=False)
    E.destroyer((110, 330), math.pi, 1.2, wake_on=False)
    # one late ship coming in from the haze (small, far)
    E.destroyer((14, 420), math.pi, 1.4, wake_on=True)
    # quay clutter: crates, bollards, a lamp post
    E.crate_stack_x((-8, 20), 0.2, rng=R, n=4)
    E.crate_stack_x((46, 14), -0.3, rng=R, n=5)
    K.barrel_drum((-14, 16), E.m("#C26A1E"))
    K.barrel_drum((-13.2, 16.4), E.m("#C26A1E"))
    E.depot((-70, 8), 0.0, 1.0)
    E.depot((88, 6), 0.1, 1.0)
    E.xfigure((8, 28), 0.0, hold="rifle", scale=1.0)
    E.xfigure((-10, 30), 0.3, hold="rifle")
    K.point_light((-1.0, 16.0, 6.0), 1.6e4, (1.0, 0.72, 0.4), 0.6)
    K.cyl("lamp", (-1.0, 16.0, 3.0), 0.12, 6.0, E.m("#2B2B2B"), verts=6)
    K.box("lampbulb", (-1.0, 16.0, 6.2), (0.5, 0.5, 0.5), E.m("#FFD890", 0.3, emit="#FFB050", estr=20.0))
    K.debris_field(R, n=30, xr=(-30, 30), yr=(-6, 28))
    K.ridge(620, R, n=8, h=(12, 26), tint="#5E6C88")
    sunv = EK.sunvec(200.0, 6.0)
    E.sky_custom(sunv, [(0.0, "#F2C6A0"), (0.05, "#C7A8B4"), (0.2, "#6E84B0"), (0.5, "#2C4478"), (1.0, "#101C42")],
                 glow="#FFD0A0", cloud_stops=[(0.04, "#F6C8A8"), (0.2, "#8E94B8"), (0.5, "#34466E")],
                 strength=0.9, cloud_cov=(0.40, 0.58), cloud_gain=1.3, glow_gain=1.6, cloud_band=(0.0, 0.14, 0.5, 0.85))
    EK.aim_sun(float(os.environ.get("STILL_SUN", "5.0")), sunv, (1.0, 0.76, 0.58), angle=2.0)
    K.area_light((-34, -30, 34), (0, 60, 2), 60, 12000.0, (0.5, 0.6, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0018")), top=50.0, colour="#B8C4D8")
    K.camera((0.0, -20.0, 13.0), (4.0, 130.0, 0.0), lens=24)


if __name__ == "__main__":
    EK.run(build, "ending_expedition_02")
