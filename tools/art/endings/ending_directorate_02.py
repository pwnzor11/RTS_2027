"""Ending slide directorate 02: the reactors burn on; the broadcast goes out again the next morning.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_directorate_02.py   (STILL_PREVIEW=1 for 640x360)
Scene: plain morning light on the Annex surface: the tall broadcast mast with its lamp, a small bone-concrete studio hut with one
lit window, reactor vent stacks glowing orange, a headframe, and a thin line of figures walking to work.  Original scene, no text.
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
    R = random.Random(5202)
    M = K.mat
    D.ground(D.ash_ground_mat, size=900)
    # the broadcast mast: tall lattice with lamps, guy lines, studio hut
    K.cyl("mast", (6, 60, 22), 0.45, 44, M("#C8C0B0", "#C8C0B0"), verts=6, r2=0.18)
    for a in (0.3, 2.4, 4.5):
        K.cyl("guy", (6 + math.cos(a) * 9, 60 + math.sin(a) * 9, 14), 0.04, 32,
              M("#2A2A2A", "#2A2A2A"), rot=(math.sin(a) * 0.3, -math.cos(a) * 0.3, 0), verts=4)
    K.box("lamp", (6, 60, 44.5), (0.8, 0.8, 0.8), M("#FF4030", "#FF4030", emit="#FF3020", estr=60.0))
    D.speaker_pole((-6, 52), h=8.0, rng=R, n=3, yaw=0.3)
    D.ceilbox("studio", (6, 46, 2.0), (12, 8, 4.0), "bone")
    D.ceilbox("studioroof", (6, 46, 4.2), (12.6, 8.6, 0.4), "iron")
    D.ceilbox("studiowin", (6, 41.9, 2.4), (3.2, 0.1, 1.2), "#FFE0A0")
    D.ceilbox("studioband", (6, 41.9, 0.8), (12.1, 0.12, 0.5), "red")
    K.box("winglow", (6, 41.8, 2.4), (2.6, 0.1, 0.8), M("#FFE0A0", "#FFE0A0", emit="#FFC070", estr=14.0))
    D.vent_stack((-30, 70), h=15.0, r=1.8, glow_k=1.0, rng=R)
    D.vent_stack((-42, 84), h=12.0, r=1.5, glow_k=0.9, rng=R)
    D.vent_stack((34, 78), h=14.0, r=1.7, glow_k=1.0, rng=R)
    D.headframe((52, 100), h=22.0, yaw=-0.2)
    D.hatch((-18, 34), yaw=0.4, s=1.2)
    pts = [(-30 + i * 2.8, 14 + 0.5 * math.sin(i)) for i in range(12)]
    D.squad(pts, yaw=1.57, rng=R, kneel_p=0.0, ash_p=0.0, mask_p=0.0)
    D.footprints([(-34 + i * 3, 13) for i in range(14)])
    K.rubble_pile((12, 12), r=1.4, n=12, rng=R, h=0.7)
    K.debris_field(R, n=40, xr=(-30, 40), yr=(-4, 40))
    K.ridge(460, R, n=9, h=(12, 28), tint="#7A7C92")
    sunv = EK.sunvec(-50.0, 12.0)
    D.sky([(0.0, "#F4E4CA"), (0.05, "#E8D0B8"), (0.2, "#A8B8CC"), (0.5, "#6888B8"), (1.0, "#385890")], sunv,
          glow_col="#FFF0D0", glow_k=0.9, glow_pow=40.0,
          clouds=dict(lo=0.52, hi=0.72, scale=2.4, h0=0.03, h1=0.2, top0=0.5, top1=0.9, opacity=0.8, stretch=4.5,
                      cols=[(0.05, "#FFF4E4"), (0.4, "#D8DCE4"), (0.9, "#A8B8D0")], gain=1.1), strength=0.95)
    EK.aim_sun(float(os.environ.get("STILL_SUN", "4.0")), sunv, (1.0, 0.9, 0.76), angle=2.0)
    K.area_light((34, -30, 30), (0, 30, 2), 70, 7000.0, (0.55, 0.65, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0012")), top=45.0, colour="#C8D0DC")
    K.camera((-4.0, -16.0, 5.0), (8.0, 70.0, 16.0), lens=24)


if __name__ == "__main__":
    EK.run(build, "ending_directorate_02")
