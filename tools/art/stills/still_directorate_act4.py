"""Act-4 opener still for the Directorate campaign (dir_04 The Sunrise): clear night on the Ash Reckoning.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_directorate_act4.py
Low view from behind the keep: conscripts stand to along the river bank in the ash, red-lit vents and the Annex headframe around
the chamber where the Sunrise charges, a shaft of red-white light rising from it into a star-filled sky; far south across the
river the Authority base lies under searchlight beams.  Moon and stars, no text in the image.
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
    R = random.Random(2004)
    M = K.mat
    D.ground(D.ash_ground_mat, dark="#22211F", mid="#3A3834", light="#54514A", crust="#171615", scale=0.9)
    # the river: a wide dark band across the middle distance (y 62..92) that mirrors the sky and the glow
    for o in bpy.data.objects:
        if o.name == "ground":
            pass
    riv = K.box("river", (0, 78, 0.03), (900, 26, 0.05), D.water_mat("#0E1C2C", rough=0.04, ripple=0.3))
    K.nolines(riv)
    # the causeway across it, and the south bank
    K.road((-6, 50), (-6, 100), 6.0)
    K.road((-6, 100), (-6, 170), 6.0)

    # --- Directorate keep (north bank, around the camera) ---
    D.headframe((-24.0, 30.0), h=24.0, yaw=0.4)
    D.vent_stack((6.0, 24.0), h=15.0, r=1.9, glow_k=1.6, rng=R)
    D.vent_stack((-8.0, 30.0), h=11.0, r=1.5, glow_k=1.2, rng=R)
    D.vent_stack((22.0, 36.0), h=13.0, r=1.7, glow_k=1.2, rng=R)
    D.hatch((-4.0, 54.0), yaw=0.0, s=1.2)           # the tunnel-head hatch on the south bank side of the causeway
    D.wall_segment((-30, 26), (-12, 24), h=1.8)
    D.wall_segment((14, 26), (36, 28), h=1.8)
    D.crawler_speaker((16.0, 12.0), yaw=0.25)
    D.speaker_pole((-12.0, 14.0), h=7.5, rng=R, n=3, yaw=0.1)
    D.speaker_pole((30.0, 30.0), h=7.5, rng=R, n=3, yaw=0.0)
    # the chamber: a glowing pit, the Sunrise Device, and the shaft of light it throws into the sky
    cx, cy = 0.0, 36.0
    K.cyl("pit", (cx, cy, 0.1), 5.0, 0.2, M("#101010"), verts=24)
    K.cyl("pitlip", (cx, cy, 0.3), 5.3, 0.3, M("red", D.D["red"]), verts=24)
    K.cyl("core", (cx, cy, 0.5), 3.8, 0.05, D.glow("#FF8A60", 40.0), verts=24)
    K.cyl("devbody", (cx, cy, 2.2), 1.2, 3.4, M("iron", D.D["iron"]), verts=12)
    for i in range(4):
        a = i * math.tau / 4 + 0.4
        K.box("fin", (cx + math.cos(a) * 1.4, cy + math.sin(a) * 1.4, 1.6), (0.3, 0.3, 2.6), M("brass", D.D["brass_d"]), rot=(0, 0, a))
    K.cyl("dev_tip", (cx, cy, 4.3), 0.4, 0.8, D.glow("#FFD8B0", 60.0), verts=10)
    D.beam((cx, cy, 0.6), (cx, cy + 4.0, 160.0), 3.4, 14.0, "#FF7A50", strength=1.0, alpha=0.035)
    D.beam((cx, cy, 0.6), (cx, cy + 4.0, 160.0), 1.4, 6.0, "#FFD0A8", strength=1.6, alpha=0.05)
    K.point_light((cx, cy - 2, 6.0), 1.2e5, (1.0, 0.45, 0.28), 2.5)
    K.point_light((cx, cy - 6, 2.5), 4.0e4, (1.0, 0.55, 0.35), 2.0)
    # cables from the chamber to the vents
    for tx, ty in ((6.0, 24.0), (-8.0, 30.0), (22.0, 36.0)):
        d = Vector((tx - cx, ty - cy, 0))
        K.box("cable", ((tx + cx) / 2, (ty + cy) / 2, 0.25), (d.length, 0.35, 0.3), M("iron_d", D.D["iron_d"]), rot=(0, 0, math.atan2(d.y, d.x)))

    # conscripts standing to: foreground line facing the river, backs to camera, red armbands catching the glow
    pts = [(-9 + i * 1.7 + R.uniform(-0.3, 0.3), 14 + R.uniform(-1.6, 1.6)) for i in range(9)] + \
          [(-5 + i * 2.0, 18.5 + R.uniform(-0.8, 0.8)) for i in range(5)]
    D.squad(pts, yaw=0.0, rng=R, kneel_p=0.4, ash_p=0.2, mask_p=0.6)
    D.sandbags((-12, 20), (14, 20), rows=3, rng=R)
    D.conscript((-3.0, 5.5), yaw=0.1, kind="rifle", mask=True, scale=1.1)
    D.conscript((-1.0, 6.2), yaw=-0.1, kind="ashthrower", mask=True, scale=1.1)
    K.crate_stack((-9.6, 4.4), yaw=0.4, rng=R)
    K.drum_cluster((8.5, 5.0), rng=R, n=4)
    K.rubble_pile((3.6, 2.0), r=1.3, n=12, rng=R, h=0.7)
    K.rubble_pile((-6.4, 2.5), r=1.1, n=10, rng=R, h=0.6)
    K.crater((12.5, 8.0), R=2.0, rng=R, rim=0.3)
    K.debris_field(R, n=70, xr=(-24, 30), yr=(-6, 40))

    # --- Authority Ash Ridge base, far south across the river, searchlights sweeping ---
    K.skyline(R, 200, n=10, xr=(-120, 120), h=(6, 16), tint="#1C2038")
    for i, (x, y) in enumerate(((-28, 150), (4, 160), (34, 148), (-52, 170), (58, 172))):
        lp = D.search_mast((x, y), h=13.0, yaw=0.0)
        tgt = (x + R.uniform(-60, 60), y - R.uniform(60, 110), R.uniform(10, 60))
        D.beam(lp, tgt, 0.5, 4.5, "#E8F0FF", strength=1.0, alpha=0.03)
    for p, yw in (((-8, 150), 0.0), ((16, 156), 0.0), ((-42, 162), 0.1)):
        D.auth_truck(p, yaw=yw)
    K.tank((-16.0, 144.0), yaw=3.14) if False else None
    K.turret((-2.0, 148.0), yaw=3.14)
    K.turret((22.0, 140.0), yaw=3.14)
    K.sandbag_wall((-6.0, 143.0), (30.0, 143.0), rows=2, rng=R)
    K.fire((40.0, 156.0), size=2.5, rng=R)
    K.ridge(430, R, n=9, h=(30, 60), tint="#1A1E36")

    # sky: clear, moonlit, deep blue with a faint rose glow at the southern horizon (the Ash Ridge command's lights) and a thick spray of stars
    moon = Vector((0.55, -0.55, 0.55)).normalized()
    D.sky([(0.0, "#4A2A3C"), (0.05, "#2A2446"), (0.18, "#1A2146"), (0.5, "#0C1230"), (1.0, "#060A1E")], moon,
          glow_col="#9EB4FF", glow_k=0.6, glow_pow=60.0, strength=0.45, stars=9.0,
          clouds=dict(lo=0.55, hi=0.72, scale=2.0, h0=0.02, h1=0.2, top0=0.4, top1=0.7, opacity=0.5, stretch=5.0, offset=(1, 5, 0),
                      cols=[(0.05, "#5C3A58"), (0.4, "#26305A"), (0.9, "#141A3A")], gain=0.5))
    mk = K.sun((0, 0, 0), 0.9, colour=(0.62, 0.72, 1.0), angle=1.0)
    mk.rotation_euler = (-moon).to_track_quat("-Z", "Y").to_euler()
    K.haze(0.0011, colour="#8090C8", top=60.0, aniso=0.55)
    D.ash_fall(R, n=500, xr=(-24, 28), yr=(0, 70), zr=(0.5, 20), strength=1.2, col="#FFC8A0")
    K.camera((-3.0, -4.0, 2.6), (1.0, 45.0, 7.0), lens=22)


if __name__ == "__main__":
    D.render_still("still_directorate_act4", build)
