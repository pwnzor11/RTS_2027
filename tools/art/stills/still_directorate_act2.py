"""Act-2 opener still for the Directorate campaign (dir_02 The Convoy Roads): wet dusk on the Rust Belt road.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_directorate_act2.py
Conscripts crouch in the roadside ditch while an Authority convoy burns on the road and a searchlight post sweeps the dark;
a Directorate hatch and speaker crawler glow behind.  Drizzle, low sun through a cloud slit.  No text in the image.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import still_directorate_kit as D  # noqa: E402
import still_kit as K  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402


def build():
    R = random.Random(2002)
    M = K.mat
    D.ground(D.ash_ground_mat, dark="#34312D", mid="#524D45", light="#6F685B")
    # wet cracked road running north, slightly right of centre, bending nowhere (the convoy road)
    K._mats["road"] = K.road_mat()
    K.road((6, -20), (6, 170), 7.0)
    for i in range(7):
        K.puddle((6 + R.uniform(-2.5, 2.5), -4 + i * 8 + R.uniform(-2, 2)), R.uniform(1.0, 2.4), R.uniform(0.5, 1.0), R.uniform(-.3, .3))
    K.puddle((1.5, -3.5), 1.7, 0.8, 0.2)

    # far silhouettes
    K.skyline(R, 190, n=11, xr=(-150, 150), h=(8, 26), tint="#2E3150")
    K.skyline(R, 120, n=7, xr=(-90, 90), h=(6, 15), tint="#3B3A55")
    K.ridge(430, R, n=9, h=(28, 60), tint="#363A62")
    K.ruin((-24, 40), 12, 5, 9, R)
    K.ruin((34, 55), 14, 5, 7, R, rot=0.2)
    K.ruin((-12, 75), 9, 4, 6, R)

    # burning convoy on the road: lead truck jackknifed, second on its side, third far back
    D.auth_truck((7.0, 34.0), yaw=2.9, burning=True, rng=R)
    D.auth_truck((3.6, 47.0), yaw=0.35, roll=0.55, burning=True, rng=R)
    D.auth_truck((7.5, 66.0), yaw=0.05)
    D.auth_truck((6.0, 84.0), yaw=-0.04)
    K.crate_stack((9.8, 18.0), yaw=0.4, rng=R)
    K.crate((10.5, 24.5, 0), 0.9, 1.0, "#5A5C3E")
    K.drum_cluster((10.2, 28.0), rng=R, n=4)
    K.barrel_drum((2.0, 26.5))
    K.debris_field(R, n=90, xr=(-20, 24), yr=(-10, 60))
    K.crater((1.4, 14.0), R=2.0, rng=R, rim=0.3)
    K.burnt_car((12.5, 38.0), yaw=1.0, rng=R)
    D.grey_smoke((7.0, 34.0), h=36, r0=2.4, n=18, tint="#2A2827", rng=R, drift=-20)
    D.grey_smoke((3.6, 47.0), h=54, r0=2.7, n=20, tint="#2A2827", rng=R, drift=-30)
    K.fire((15.0, 90.0), size=3.5, rng=R)

    # the searchlight post on the east verge: mast, a rifleman's sandbag nest, beam sweeping west over the road
    lp = D.search_mast((22.0, 44.0), h=15.0, yaw=-1.9)
    K.sandbag_wall((17.5, 41.0), (17.5, 49.0), rows=3, rng=R)
    K.sandbag_wall((17.5, 41.0), (24.0, 40.0), rows=2, rng=R)
    for p, yw, kn in (((19.0, 44.0), -1.9, True), ((20.0, 47.0), -1.8, False)):
        K.figure(p, yw, kn)
    D.beam(lp, (-30.0, 38.0, 1.0), 0.5, 7.0, "#FFF0C8", strength=1.6, alpha=0.035)
    D.spot(lp, (-6.0, 40.0, 0.5), 1.2e6, (1.0, 0.94, 0.78), size_deg=9, blend=0.5, radius=0.5)

    # Directorate: hatch + speaker crawler on the ash behind the ditch, vents glowing, speaker poles with wires
    D.hatch((-17.0, 34.0), yaw=0.9, s=1.2)
    D.vent_stack((-30.0, 52.0), h=12.0, r=1.5, glow_k=0.8, rng=R)
    D.crawler_speaker((-8.5, 17.0), yaw=0.75)
    D.speaker_pole((-3.8, 7.0), h=6.5, rng=R, n=3, yaw=0.5)
    D.speaker_pole((-9.0, 58.0), h=6.5, rng=R, n=2, yaw=0.2)

    # foreground: the ditch lip left, conscripts crouching with rifles and an ashthrower, looking up the road
    K.rubble_pile((-5.6, -5.0), r=1.6, n=16, rng=R, h=0.9)
    K.rubble_pile((-2.6, -6.3), r=1.0, n=10, rng=R, h=0.55)
    K.drum_cluster((-9.0, 1.0), rng=R, n=4)
    K.crate_stack((-7.2, -1.4), yaw=0.3, rng=R)
    K.broken_fence((-11, 6), (-13, 22), rng=R)
    D.squad([(-4.2, 0.0), (-3.0, 3.4), (-5.8, 4.0), (-1.4, 6.6), (-2.6, 10.0), (-6.8, 9.5)], yaw=0.2, rng=R, kneel_p=0.6, ash_p=0.25)
    D.conscript((-3.6, -3.0), yaw=0.15, kneel=True, kind="ashthrower", mask=True)
    D.sandbags((-6.2, 0.6), (-6.0, 7.2), rows=2, rng=R)

    # power line along the west verge, grass
    pl = [(-22, 20), (-22, 45), (-22, 70), (-22, 98), (-22, 130)]
    for p in pl:
        K.pylon(p, 12)
    K.power_line(pl, 12, sag=1.6)
    K.tufts(R, n=700, xr=(-50, 50), yr=(-16, 90), avoid=lambda x, y: 2.4 < x < 9.6, scale=1.0)

    # weather: drizzle and drifting ash, low cold light through a cloud slit, red-orange glow from the fires
    sunv = Vector((math.sin(math.radians(-62)) * math.cos(math.radians(4)), math.cos(math.radians(-62)) * math.cos(math.radians(4)), math.sin(math.radians(4))))
    D.sky([(0.0, "#F29A52"), (0.03, "#C86A6A"), (0.09, "#7A5A82"), (0.25, "#3C4668"), (0.6, "#1E2A48"), (1.0, "#0F1830")], sunv,
          glow_col="#FFB070", glow_k=1.0, glow_pow=20.0,
          clouds=dict(lo=0.38, hi=0.58, scale=2.2, h0=0.0, h1=0.05, top0=0.55, top1=0.95, opacity=0.97, stretch=3.5,
                      cols=[(0.04, "#B8603A"), (0.2, "#50506E"), (0.7, "#242C48")], gain=1.1),
          strength=0.85)
    key = K.sun((0, 0, 0), 7.0, colour=(1.0, 0.55, 0.30), angle=1.5)
    key.rotation_euler = (-sunv).to_track_quat("-Z", "Y").to_euler()
    K.area_light((30, -30, 34), (0, 15, 2), 60, 9000.0, (0.45, 0.55, 1.0))
    K.haze(0.0021, colour="#A8B0D8", top=40.0)
    D.rain(R, n=420, xr=(-24, 24), yr=(0, 75), zr=(0, 18), slant=0.22, strength=0.9, alpha=0.18)
    D.ash_fall(R, n=300, xr=(-20, 24), yr=(0, 60), zr=(0.5, 12))
    K.camera((-1.2, -13.0, 1.5), (4.5, 40.0, 3.6), lens=24)


if __name__ == "__main__":
    D.render_still("still_directorate_act2", build)
