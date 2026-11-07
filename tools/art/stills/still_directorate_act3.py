"""Act-3 opener still for the Directorate campaign (dir_03 The Launch Sites): bright cold day on the Glass Coast.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_directorate_act3.py
High vantage over the fused-glass shore: a column of conscripts and a speaker crawler marches down to Pad Seven, its gantry and
a toppled Authority searchlight mast beyond, the Expedition fleet standing off on the glassy sea.  Pale glass-coloured sky, ash on the wind.
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
    R = random.Random(2003)
    M = K.mat
    D.ground(D.glass_sand_mat, size=700)
    # sea: starts east/north of the pad; the shore line runs diagonally
    D.water(z=-0.4, size=2400, loc=(0, 700), col="#1C5B66", rough=0.05, ripple=0.35)
    # carve the land: a big sand plane over the near half (ground already covers; push the water visible beyond y=95)
    # (ground plane is 700 wide centred y=120 -> ends at y=470; water is below it, so lift the ground edge with a shore ramp)
    for o in bpy.data.objects:
        if o.name == "ground":
            o.location.y = -92       # land spans y -309..125
            o.scale = (1, 0.62, 1)
    # water-level foam band along the shore
    K.nolines(K.box("foam", (0, 127, 0.01), (900, 4.0, 0.04), M("#D8E8E0", rough=0.5)))

    # Pad Seven and its gantry, scorched, with the toppled searchlight mast
    D.pad((14.0, 52.0), r=13.0, rng=R)
    D.gantry((26.0, 62.0), h=28.0, yaw=0.3)
    K.crater((2.0, 44.0), R=2.4, rng=R, rim=0.3)
    lp = D.search_mast((-2.0, 70.0), h=12.0, yaw=0.5)
    K.sandbag_wall((-6.0, 66.0), (-6.0, 74.0), rows=2, rng=R)
    K.figure((-4.0, 66.5), 0.8, True)
    K.figure((-4.5, 72.0), 0.6, False)
    # a downed mast on the pad's edge: lattice lying on the sand with a lamp spark
    K.box("fallen", (30.0, 40.0, 0.5), (0.5, 9.0, 0.5), M("steel", "#555B61"), rot=(0, 0.1, 0.9))
    K.fire((30.0, 40.0), size=2.2, rng=R)
    D.grey_smoke((30.0, 40.0), h=26, r0=1.8, n=14, tint="#34312E", rng=R, drift=-22)

    # a Directorate hatch on the dune right, vents, and the long loudspeaker crawler leading the column
    D.hatch((-30.0, 28.0), yaw=0.5, s=1.1)
    D.vent_stack((-40.0, 44.0), h=11.0, r=1.4, glow_k=0.7, rng=R)
    D.speaker_pole((-16.0, 20.0), h=7.0, rng=R, n=3, yaw=0.6)
    D.crawler_speaker((3.0, 21.0), yaw=0.55)
    # column of conscripts, winding down toward the pad in two files, with an ash trail
    trail = [(-18, -4), (-12, 2), (-6, 8), (0, 14), (6, 24), (10, 36)]
    D.footprints([(-18, -4), (-10, 4), (-2, 12), (4, 22), (9, 34)])
    pts = []
    for i in range(14):
        t = i / 13.0
        bx = -16 + 25 * t + 0.9 * math.sin(t * 6)
        by = -2 + 40 * t
        pts += [(bx - 1.1, by), (bx + 1.4, by + 1.2)] if i % 2 == 0 else [(bx, by)]
    D.squad(pts, yaw=0.55, rng=R, kneel_p=0.0, ash_p=0.18)
    # foreground at the vantage: sandbag lip, crates and a conscript officer-ish pair looking out
    K.crate_stack((-9.0, -6.0), yaw=0.4, rng=R)
    K.drum_cluster((-12.5, -3.5), rng=R, n=4)
    K.rubble_pile((-3.0, -8.5), r=1.4, n=14, rng=R, h=0.8)
    D.sandbags((-14.5, -8.0), (-6.0, -9.5), rows=3, rng=R)
    D.conscript((-10.0, -7.4), yaw=0.5, kind="rifle", mask=True, scale=1.05)
    D.conscript((-12.0, -7.0), yaw=0.45, kind="ashthrower", mask=False)
    D.glass_shards(R, n=90, xr=(-40, 45), yr=(-10, 95), avoid=lambda x, y: (x - 14) ** 2 + (y - 52) ** 2 < 200)
    K.debris_field(R, n=60, xr=(-30, 40), yr=(-10, 90))
    K.tufts(R, n=260, xr=(-40, 45), yr=(-10, 95), scale=1.1)

    # the Expedition fleet standing off, lights on; far headland
    D.ship((70.0, 215.0), yaw=1.2, s=1.4)
    D.ship((-10.0, 250.0), yaw=1.45, s=1.8)
    D.ship((150.0, 290.0), yaw=1.0, s=1.5)
    K.ridge(520, R, n=9, h=(20, 40), tint="#6E8A96")

    # light: high cold sun upper left, pale teal 'old glass' sky with long thin cloud bands
    sunv = Vector((-0.45, 0.55, 0.70)).normalized()
    D.sky([(0.0, "#CFE4DE"), (0.06, "#A8D2CC"), (0.2, "#7FB8BE"), (0.5, "#4F8FB2"), (1.0, "#2A5C94")], sunv,
          glow_col="#FFF4DA", glow_k=0.8, glow_pow=40.0,
          clouds=dict(lo=0.50, hi=0.70, scale=2.4, h0=0.03, h1=0.2, top0=0.5, top1=0.9, opacity=0.8, stretch=4.5, offset=(3, 1, 0),
                      cols=[(0.05, "#F4F2EA"), (0.4, "#C8DCE4"), (0.9, "#9CB8D0")], gain=1.15),
          strength=1.05)
    key = K.sun((0, 0, 0), 4.2, colour=(1.0, 0.92, 0.80), angle=1.0)
    key.rotation_euler = (-sunv).to_track_quat("-Z", "Y").to_euler()
    K.area_light((34, -30, 30), (0, 30, 2), 70, 6000.0, (0.55, 0.7, 1.0))
    K.haze(0.0012, colour="#B8D8E0", top=45.0)
    D.ash_fall(R, n=450, xr=(-26, 30), yr=(0, 80), zr=(0.3, 14), strength=0.9)
    K.camera((-6.0, -22.0, 6.5), (6.0, 60.0, 4.0), lens=24)


if __name__ == "__main__":
    D.render_still("still_directorate_act3", build)
