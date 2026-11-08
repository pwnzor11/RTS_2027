"""Expedition Pathfinder: spotter and designator.  Original design.  Same contract/states as auth_rifleman.py (see exp_parts.py).

Slim navy fatigues in the darker shade, white webbing, chrome helmet with the orange visor stripe plus a side-mounted stabilised
range finder (chrome tube, amber lens), a tall shoulder radio with a long whip and orange pennant tip, and a long scoped rifle.
Silhouette at 1x: slender, a tall whip and a long barrel.  Team colour: shoulder flashes.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_parts as EP

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48, team-colour emphasis
ID = "exp_pathfinder"
TITLE = "Expedition Pathfinder"
CANVAS, ORIGIN, STATES, DIE_FACING = EP.CANVAS, EP.ORIGIN, EP.STATES, EP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = EP.PALETTE, EP.MATERIALS, EP.EMIT_STRENGTH
pose = EP.make_pose(lean=6.0, stride=0.28, hold=((0.10, 0.30, 0.24), -14.0, 36.0), fire=((0.08, 0.32, 0.38), 10.0, 2.0),
                    fire2=((0.08, 0.29, 0.385), 10.0, 5.0), fire_lean=6.0, bobamp=0.045)


def pack(S, U, R):
    S.box((0.26, 0.14, 0.32), (0, -0.24, 0.22), "navy3", U, bevel=0.02)
    S.box((0.20, 0.10, 0.22), (0.0, -0.33, 0.30), "chrome3", U, bevel=0.01)                  # radio set
    S.box((0.16, 0.03, 0.06), (0.0, -0.385, 0.34), "orange2", U, bevel=0)
    S.box((0.22, 0.03, 0.12), (0, -0.325, 0.10), "house", U, bevel=0)                         # D-185 house panel on the pack frame
    S.cyl(0.03, 0.80, (0.12, -0.30, 0.70), "chrome2", "Z", U, verts=6)                       # tall whip
    S.box((0.03, 0.14, 0.09), (0.12, -0.37, 1.04), "orange", U, bevel=0)                    # pennant tip


def extra(S, U, up, R, p):
    S.cyl(0.045, 0.22, (0.15, 0.10, 0.60), "chrome", "Y", U, verts=10)                       # range finder on the helmet side
    S.cyl(0.05, 0.03, (0.15, 0.22, 0.60), "lens", "Y", U, verts=10)
    S.cyl(0.05, 0.04, (0.15, 0.03, 0.60), "copper", "Y", U, verts=10)


def weapon(S, GM, p, R):
    S.box((0.05, 0.27, 0.10), (0, -0.25, -0.01), "navy2", GM, rot=(-6, 0, 0))
    S.box((0.06, 0.26, 0.09), (0, 0, 0), "chrome2", GM)
    S.box((0.05, 0.30, 0.06), (0, 0.27, -0.005), "navy2", GM)
    S.cyl(0.02, 0.50, (0, 0.62, 0.005), "chrome", "Y", GM, verts=6)                          # long barrel
    S.cyl(0.03, 0.30, (0, 0.02, 0.09), "chrome3", "Y", GM, verts=8)                          # scope
    S.cyl(0.04, 0.03, (0, 0.18, 0.09), "lens", "Y", GM, verts=8)
    S.box((0.04, 0.07, 0.12), (0, 0.02, -0.10), "dark", GM, rot=(8, 0, 0))
    if p["flash"]:
        S.cyl(0.07, 0.24, (0, 0.98, 0.005), "flash", "Y", GM, verts=8, r2=0.0)
        S.sphere((0.09, 0.09, 0.09), (0, 0.88, 0.005), "flash", GM, seg=8)


SPEC = dict(house_boost=True, shoulder=(0.22, 0.0, 0.37), grips=((0, -0.10, -0.075), (0, 0.20, -0.05)), bulk=0.9,
            body=("navy2", "navy3"), pack=pack, extra=extra, weapon=weapon)


def build(S, p):
    EP.build_figure(S, p, SPEC)
