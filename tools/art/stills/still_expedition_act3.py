"""Act-3 opener still for the Expedition (exp_03 The Fleet Reaches the Coast): stormy day over the Glass Coast cove.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_expedition_act3.py
        (STILL_PREVIEW=1 for a 640x360 look).  Writes data/images/stills/still_expedition_act3.png.
Scene: from the advance party's headland (Flight Deck, SAM Mounts) the carrier group stands into the cove under a
broken storm sky; jets climb on contrails, whitecaps on a dark teal sea, glass spires on the far headland.
High camera, cool daylight with one gap of sun.  Original scene, no text.
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

ZT = 6.5   # plateau top


def build():
    R = random.Random(3303)
    E.ground_with(E.sand_mat(dry="#B8A27C"))
    E.sea(0.0, shallow="#4DB2A8", deep="#0B2A40", width=110, sheen=0.1)
    shore = [(-120 + i * 15, 0.6 * math.sin(i * 1.3) + 1.0) for i in range(17)]
    E.wet_sand([(x, y - 2.5) for x, y in shore], width=5.0)
    E.foam_line(shore, width=1.5, rng=R)
    E.foam_line([(x + 4, y + 5) for x, y in shore], width=0.9, rng=R)
    # whitecaps scattered over the sea
    for _ in range(170):
        x, y = R.uniform(-170, 170), R.uniform(14, 230)
        w = R.uniform(1.2, 3.0) * (1 + y / 80)
        o = K.box("cap", (x, y, 0.09), (w, w * 0.22, 0.02), E.m("#EEF4F2", 1.0), rot=(0, 0, R.uniform(-0.3, 0.3)))
        K.nolines(o)

    # headlands framing the cove
    E.rock_mesa((-24, -16), r=24, h=7.0, rng=R)
    E.rock_mesa((28, -4), r=14, h=6.5, rng=R, tint="#85705A")
    E.rock_mesa((-85, 60), r=34, h=14, rng=R, tint="#6C6858")
    E.rock_mesa((95, 52), r=30, h=11, rng=R, tint="#6E6A5A")
    E.rock_mesa((140, 140), r=40, h=16, rng=R, tint="#5E5E62")
    K.ridge(620, R, n=9, h=(16, 34), tint="#4F5C6C")
    E.glass_shards(R, n=22, xr=(70, 118), yr=(36, 66), h=(2.5, 9.0), tint="#4FA79A")
    E.glass_shards(R, n=16, xr=(-110, -66), yr=(46, 76), h=(2.5, 8.0))
    E.glass_shards(R, n=18, xr=(8, 60), yr=(-18, 4), h=(1.5, 4.5))

    # the plateau: Flight Deck, SAM Mounts, crew, crates
    E.flight_deck((-24, -17), 0.25, 1.0, z=ZT)
    E.sam_mount((-9.5, -24), 0.3, 1.15, z=ZT)
    E.sam_mount((-35, -6), 0.45, 1.15, z=ZT)
    E.sam_mount((27, -3), 0.2, 1.15, z=6.0)
    E.crate_stack_x((-12, -9), 0.2, rng=R, n=5, z=ZT)
    E.crate_stack_x((-38, -24), -0.3, rng=R, n=4, z=ZT)
    for p, y, kn in (((-14.5, -12.5), 0.5, False), ((-16.0, -14.0), 0.9, True), ((-30.0, -8.0), 0.6, False),
                     ((-7.0, -19.5), 0.4, True), ((-5.5, -21.0), 0.2, False)):
        E.xfigure(p, y, kneel=kn, z=ZT)
    E.xfigure((25.0, -6.0), 0.3, z=6.0, kneel=True)
    K.tufts(R, n=300, xr=(-40, 40), yr=(-28, -2), scale=0.6, avoid=lambda x, y: False)

    # the carrier group standing in
    E.carrier((6, 112), math.pi - 0.06, 1.0, jets=6)
    E.destroyer((-34, 92), math.pi - 0.12, 1.15)
    E.destroyer((48, 86), math.pi + 0.1, 1.15)
    E.tender((-6, 168), math.pi, 1.0)
    E.destroyer((-70, 150), math.pi - 0.2, 1.1)
    E.destroyer((68, 160), math.pi + 0.15, 1.1)
    E.command_ship((20, 235), math.pi, 1.1)
    E.landing_craft((-12, 40), 0.2, 1.3, wake_on=True, rng=R)
    E.landing_craft((22, 30), -0.15, 1.3, wake_on=True, rng=R)

    # jets on contrails, climbing away over the cove
    for p0, p1, bank in (((-20, 40, 40), (-48, 100, 60), 0.35), ((70, 60, 50), (40, 130, 74), -0.3)):
        yaw = math.atan2(p1[0] - p0[0], p1[1] - p0[1])
        E.jet(p1, yaw, z=p1[2], s=2.0, bank=bank)
        E.contrail(p0, (p1[0] - math.sin(yaw) * 5, p1[1] - math.cos(yaw) * 5, p1[2]), n=22, r=0.35, rng=R)

    # sky: storm greys and a gap of sun on the right
    az, el = math.radians(28.0), math.radians(22.0)
    sunv = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
    E.sky_custom(sunv, [(0.0, "#D5E0D8"), (0.05, "#A7BDBC"), (0.18, "#5E8296"), (0.5, "#2C4560"), (1.0, "#14213A")],
                 glow="#FFE6B8", cloud_stops=[(0.04, "#F2F0E2"), (0.18, "#8A9AA8"), (0.45, "#3E5068")],
                 strength=1.0, cloud_cov=(0.36, 0.54), cloud_gain=1.5, glow_gain=2.2, cloud_band=(0.0, 0.14, 0.6, 0.9))
    key = K.sun((0, 0, 0), float(os.environ.get("STILL_SUN", "12.0")), colour=(1.0, 0.88, 0.68), angle=1.6)
    key.rotation_euler = (-sunv).to_track_quat("-Z", "Y").to_euler()
    K.area_light((-34, -30, 34), (0, 15, 2), 60, 14000.0, (0.55, 0.65, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0014")), top=60.0, colour="#C4D4DA")
    K.camera((4.0, -38.0, 12.5), (6.0, 90.0, 3.0), lens=24)


if __name__ == "__main__":
    E.run(build, "still_expedition_act3")
