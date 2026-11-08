"""Act-2 opener still for the Expedition (exp_02 The Supply Line): golden hour on the Glass Coast north shore.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_expedition_act2.py
        (STILL_PREVIEW=1 for a 640x360 look).  Writes data/images/stills/still_expedition_act2.png.
Scene: the advance party is ashore; the Anchorage pier is going up on the shallows while the first convoy of tenders
and escorts sails in from a hazy fleet on the horizon.  Low gold sun over the sea, glass spires on the dunes,
Authority dust and smoke on the coast road to the east.  Original scene, no text.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import still_expedition_kit as E  # noqa: E402
import still_kit as K  # noqa: E402
from mathutils import Vector  # noqa: E402

SHORE = 30.0


def build():
    R = random.Random(2202)
    E.ground_with(E.sand_mat())
    E.sea(SHORE, shallow="#58C8B6", deep="#0A2848", width=120)
    shore = [(-90 + i * 15, SHORE + 0.8 * math.sin(i * 1.7)) for i in range(13)]
    E.wet_sand([(x, y - 3.0) for x, y in shore], width=6.0)
    E.foam_line(shore, width=1.3, rng=R)
    E.foam_line([(x + 5, y + 4.5) for x, y in shore], width=0.8, rng=R)

    # far fleet on the horizon, hazed to silhouettes
    E.carrier((-95, 330), math.pi * 0.52, 1.5, wake_on=False, jets=0)
    E.command_ship((20, 300), math.pi * 0.5, 1.3, wake_on=False)
    for i, (x, y) in enumerate(((-150, 260), (-40, 285), (80, 270), (140, 310), (-10, 360), (60, 340))):
        (E.destroyer if i % 2 else E.tender)((x, y), math.pi * 0.5 + R.uniform(-0.2, 0.2), 1.2, wake_on=False)

    # the first convoy standing in towards the pier
    E.tender((46, 128), math.pi + 0.18, 1.0)
    E.destroyer((70, 168), math.pi + 0.12, 1.0)
    E.tender((8, 190), math.pi - 0.08, 1.0)
    E.destroyer((-48, 150), math.pi - 0.2, 1.0)
    E.tender((-70, 230), math.pi + 0.1, 1.0)
    E.landing_craft((6, 78), math.pi - 0.1, 1.3, wake_on=True, rng=R)
    E.landing_craft((30, 62), math.pi + 0.35, 1.3, wake_on=True, rng=R)

    # the Anchorage going up on the shallows, crane working
    E.pier((-9.5, SHORE - 6), 0.0, length=80, width=6.4, z=2.0, built=0.85)
    # patrol skiffs and a tender alongside
    E.landing_craft((-1.5, 50), 0.05, 1.2, ramp_down=False, load=True, rng=R)

    # beach: beached landing craft with ramp down and a file of crew coming ashore
    E.landing_craft((11.5, 20.0), -0.35, 1.0, ramp_down=True, rng=R)
    for i, (x, y) in enumerate(((13.0, 15.0), (13.9, 13.4), (13.5, 11.5), (12.8, 9.8))):
        E.xfigure((x - 2.5, y - 2.0), -0.45, scale=1.0, hold="crate" if i % 2 else "rifle")
    for p, y, kn in (((-2.0, 8.0), 0.3, True), ((-4.5, 10.0), 0.5, False), ((2.5, 16.0), 0.2, False), ((-8.5, 14.0), 0.9, True)):
        E.xfigure(p, y, kneel=kn)
    E.crate_stack_x((-11.5, -2.0), 0.3, rng=R, n=6)
    E.crate_stack_x((9.5, -3.0), -0.4, rng=R, n=4, s=0.9)
    E.crate_stack_x((-1.5, 22.0), 0.1, rng=R, n=5)
    K.sandbag_wall((-16, 14), (-16, 22), rows=2, rng=R)
    K.sandbag_wall((11, 26), (4, 27), rows=2, rng=R)
    K.barrel_drum((-5.2, -1.0), E.m("#C26A1E"))
    K.barrel_drum((-4.4, -1.4), E.m("#C26A1E"))

    # advance-party camp: depot, tents, hover skiffs
    E.depot((-48, 8), 0.5, 1.2)
    E.tent((-38, -2), 0.4, 1.0)
    E.tent((-26, 2), -0.2, 1.0, col="#C8C6B2")
    E.hover_skiff((-1.0, 26.0), 0.9, 1.15, z=0.0)
    E.hover_skiff((22, 3), -0.6, 1.0, z=0.1)

    # dunes, glass spires and wreck-iron of the old coast
    E.dunes(R, n=14, xr=(-120, 130), yr=(-4, 12), h=(0.6, 1.6), r=(6, 12))
    E.glass_shards(R, n=26, xr=(-70, 70), yr=(-12, 26), h=(1.5, 5.5),
                   avoid=lambda x, y: abs(x) < 9 and y < 22)
    E.glass_shards(R, n=22, xr=(40, 130), yr=(-4, 28), h=(2.5, 8.0), tint="#4FA79A")
    K.ridge(560, R, n=9, h=(14, 28), tint="#54527A")
    K.rubble_pile((-5.0, -9.2), r=1.4, n=14, rng=R, h=0.8)
    K.rubble_pile((7.5, -6.0), r=1.0, n=10, rng=R, h=0.6)
    K.debris_field(R, n=70, xr=(-24, 24), yr=(-14, 26))
    K.tufts(R, n=500, xr=(-60, 60), yr=(2, 27), scale=0.7)
    K.tufts(R, n=40, xr=(-12, 12), yr=(-12, -4), scale=0.6)

    # Authority column on the coast road to the east: dust and smoke
    K.dust_cloud((190, 90), n=14, length=60, rng=R, tint="#E3C79A")
    K.smoke_column((150, 58), h=44, r0=2.2, n=12, rng=R)
    K.fire((150, 58), size=4.0, rng=R)
    K.smoke_column((-130, 40), h=26, r0=1.5, n=8, rng=R)

    # light: gold sun low over the sea, ahead and to the left
    az, el = math.radians(-32.0), math.radians(7.0)
    sunv = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
    K.sky_dawn(sunv, strength=float(os.environ.get("STILL_SKY", "1.05")))
    key = K.sun((0, 0, 0), float(os.environ.get("STILL_SUN", "10.0")), colour=(1.0, 0.66, 0.36), angle=1.2)
    key.rotation_euler = (-sunv).to_track_quat("-Z", "Y").to_euler()
    K.area_light((34, -30, 34), (0, 15, 2), 60, 15000.0, (0.50, 0.58, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0011")), top=45.0)
    K.camera((-10.0, -24.0, 9.0), (-13.0, 70.0, 1.5), lens=24)


if __name__ == "__main__":
    E.run(build, "still_expedition_act2")
