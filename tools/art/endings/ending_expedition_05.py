"""Ending slide expedition 05: the fleet looks at the old continent: a coast, a harbour and a field of ash.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_expedition_05.py   (STILL_PREVIEW=1 for 640x360)
Scene: from the sea at low evening light, the fleet's lead hull in the foreground with a white pennant, a calm teal sea, and across
it the old continent: a small empty harbour with a pier and a long grey ash plain behind.  Original scene, no text.
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
    R = random.Random(5105)
    E.ground_with(E.ash_mat())
    E.sea(-400.0, y1=110.0, shallow="#3E8E92", deep="#0C2A40", width=500, sheen=0.05)
    shore = [(-300 + i * 25, 110 + 1.2 * math.sin(i * 0.7)) for i in range(25)]
    E.wet_sand([(x, y - 2.0) for x, y in shore], width=6.0)
    E.foam_line(shore, width=1.2, rng=R)
    E.pier((10, 110), 0.0, length=40, width=6.5, z=2.0, crane=False)
    E.depot((-30, 136), 0.1, 1.4)
    E.dredge_works((60, 190), -0.2, 1.4)
    K.crater((-60, 180), R=6.0, rng=R, rim=0.3)
    for k in range(10):
        x, y = R.uniform(-160, 160), R.uniform(130, 330)
        o = K.cyl("drift", (x, y, 0.5), R.uniform(8, 18), 1.8, E.m("#6F6860", 1.0), verts=9, r2=R.uniform(1, 3))
        K.nolines(o)
    K.ridge(520, R, n=9, h=(14, 30), tint="#6A5E72")
    # foreground: the lead destroyer from astern-quarter, a white pennant pole
    E.destroyer((-10, -4), 0.12, 1.5, wake_on=True)
    E.destroyer((92, 62), 0.05, 1.3, wake_on=True)
    E.tender((-96, 80), 0.1, 1.2, wake_on=True)
    E.carrier((170, 150), 0.0, 1.2, wake_on=False, jets=3)
    sunv = EK.sunvec(-70.0, 5.0)
    E.sky_custom(sunv, [(0.0, "#F8C080"), (0.05, "#EA9A70"), (0.18, "#B07C94"), (0.45, "#4C5C8C"), (1.0, "#16204A")],
                 glow="#FFC890", cloud_stops=[(0.04, "#FFC890"), (0.2, "#B8809C"), (0.5, "#44507C")],
                 strength=0.95, cloud_cov=(0.40, 0.58), cloud_gain=1.5, glow_gain=1.8, cloud_band=(0.0, 0.16, 0.55, 0.9))
    EK.aim_sun(float(os.environ.get("STILL_SUN", "8.0")), sunv, (1.0, 0.66, 0.40), angle=1.8)
    K.area_light((34, -30, 34), (0, 40, 2), 60, 12000.0, (0.5, 0.6, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0013")), top=50.0, colour="#E0B8A0")
    K.camera((30.0, -60.0, 9.0), (4.0, 120.0, 3.0), lens=24)


if __name__ == "__main__":
    EK.run(build, "ending_expedition_05")
