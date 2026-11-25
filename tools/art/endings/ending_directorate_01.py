"""Ending slide directorate 01: the Sunrise fires at dusk; the Ash Ridge command is gone; the Annex cheers.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_directorate_01.py   (STILL_PREVIEW=1 for 640x360)
Scene: clear dusk over the ash plain; far on the ridge a tall pale-gold column of light and a rising smoke pillar where the command
stood; in the foreground a crowd of small figures gathered under loudspeaker poles at the Annex hatch, faces turned to it.  No text.
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
    R = random.Random(5201)
    M = K.mat
    D.ground(D.ash_ground_mat, size=900)
    # far column of light and smoke on the ridge
    K.ridge(420, R, n=10, h=(14, 34), tint="#3E3C52")
    K.cyl("column", (-30, 430, 90), 3.0, 190, M("#FFD890", "#FFD890", emit="#FFB860", estr=5.0), verts=10, r2=1.0)
    K.cyl("columnglow", (-30, 430, 70), 8.0, 150, M("#FF9A50", "#FF9A50", emit="#FF7A30", estr=0.9), verts=10, r2=3.0)
    K.point_light((-30, 410, 10), 6.0e6, (1.0, 0.7, 0.4), 12.0)
    D.grey_smoke((-30, 430), h=160, r0=8.0, n=14, tint="#3A3634", rng=R, drift=30)
    # Annex hatch with speaker poles and the cheering crowd
    D.hatch((6, 46), yaw=0.0, s=1.4)
    D.headframe((-34, 70), h=22.0, yaw=0.2)
    D.vent_stack((40, 62), h=12.0, r=1.5, glow_k=0.6, rng=R)
    for x, y in ((-10, 24), (18, 28), (-26, 36), (32, 40)):
        D.speaker_pole((x, y), h=8.0, rng=R, n=3, yaw=0.0)
    pts = []
    for i in range(46):
        a = R.uniform(0, math.tau)
        rr = R.uniform(2.0, 18.0)
        pts.append((6 + math.cos(a) * rr * 1.6, 24 + abs(math.sin(a)) * rr * 0.9))
    D.squad(pts, yaw=0.0, rng=R, kneel_p=0.0, ash_p=0.0, mask_p=0.1)
    D.footprints([(0, 6), (3, 12), (6, 18)])
    K.debris_field(R, n=40, xr=(-30, 40), yr=(-4, 40))
    D.ash_fall(R, n=300, xr=(-26, 30), yr=(0, 70), zr=(0.3, 14), strength=0.8)
    sunv = EK.sunvec(-20.0, 3.0)
    D.sky([(0.0, "#F8C880"), (0.04, "#EE9A66"), (0.14, "#B8648A"), (0.4, "#4A4A92"), (1.0, "#101A4A")], sunv,
          glow_col="#FFC080", glow_k=1.0, glow_pow=30.0,
          clouds=dict(lo=0.5, hi=0.72, scale=2.4, h0=0.02, h1=0.18, top0=0.4, top1=0.8, opacity=0.8, stretch=5.0,
                      cols=[(0.05, "#FFB070"), (0.4, "#C07090"), (0.9, "#5A5090")], gain=1.3), strength=0.95, stars=0.0)
    EK.aim_sun(float(os.environ.get("STILL_SUN", "3.5")), sunv, (1.0, 0.62, 0.34), angle=2.0)
    K.area_light((34, -30, 30), (0, 30, 2), 70, 10000.0, (0.45, 0.55, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0014")), top=45.0, colour="#B0A8C8")
    K.camera((8.0, -12.0, 4.8), (-6.0, 120.0, 12.0), lens=24)


if __name__ == "__main__":
    EK.run(build, "ending_directorate_01")
