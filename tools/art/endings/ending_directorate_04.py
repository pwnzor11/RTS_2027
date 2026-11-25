"""Ending slide directorate 04: the surface renamed, not reclaimed; shafts flooding; the people below still want the sky.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_directorate_04.py   (STILL_PREVIEW=1 for 640x360)
Scene: night on the ash plain under a clear starry sky: an open hatch throws a pale beam upward, a small knot of figures stands at its
rim looking up, the headframe stands over a flooded shaft mouth (black water, red lamp), reactor vents glow.  Original scene, no text.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import endkit as EK  # noqa: E402
import still_directorate_kit as D  # noqa: E402
import still_kit as K  # noqa: E402
import bpy  # noqa: E402


def build():
    R = random.Random(5204)
    M = K.mat
    D.ground(D.ash_ground_mat, size=900)
    # flooded shaft: a dark pool with a headframe and red lamp
    D.water(z=0.04, size=60, loc=(-30, 62), col="#10303A", rough=0.04, ripple=0.2)
    D.headframe((-30, 70), h=22.0, yaw=0.0)
    K.box("redlamp", (-30, 62, 6.0), (0.6, 0.6, 0.6), M("#FF3A20", "#FF3A20", emit="#FF2A10", estr=80.0))
    # the open hatch with a beam climbing into the sky
    D.hatch((10, 46), yaw=0.2, s=1.3)
    D.beam((10, 46, 3), (10, 50, 120), 2.0, 6.0, "#E8F4FF", strength=1.2, alpha=0.07)
    K.point_light((10, 42, 4), 6.0e4, (0.8, 0.9, 1.0), 2.0)
    D.squad([(4 + 1.6 * (i % 5), 28 + 1.6 * (i // 5)) for i in range(15)], yaw=0.0, rng=R, kneel_p=0.0, ash_p=0.0, mask_p=0.0)
    D.vent_stack((44, 70), h=14.0, r=1.6, glow_k=1.0, rng=R)
    D.vent_stack((-58, 90), h=12.0, r=1.4, glow_k=0.9, rng=R)
    D.hatch((-60, 40), yaw=-0.3, s=1.0)
    D.speaker_pole((-12, 20), h=7.0, rng=R, n=3, yaw=0.4)
    D.wall_segment((24, 24), (60, 30), h=2.2)
    K.rubble_pile((-10, 8), r=1.4, n=12, rng=R, h=0.7)
    K.debris_field(R, n=40, xr=(-30, 40), yr=(-4, 50))
    K.ridge(460, R, n=9, h=(12, 28), tint="#1E2444")
    sunv = EK.sunvec(60.0, -8.0)
    D.sky([(0.0, "#2A3A6A"), (0.1, "#1A2652"), (0.4, "#0C1438"), (1.0, "#050A22")], sunv,
          glow_col="#4060A0", glow_k=0.2, glow_pow=20.0, clouds=None, strength=0.8, stars=1.0)
    K.area_light((34, -30, 30), (0, 40, 2), 70, 3500.0, (0.4, 0.5, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0010")), top=45.0, colour="#4A5C90")
    K.camera((-2.0, -10.0, 4.0), (6.0, 80.0, 18.0), lens=22)


if __name__ == "__main__":
    EK.run(build, "ending_directorate_04")
