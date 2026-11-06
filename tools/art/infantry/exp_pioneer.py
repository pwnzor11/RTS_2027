"""Expedition Pioneer: field engineer (capture and repair).  Original design.  Same contract/states as auth_rifleman.py (see exp_parts.py).

Navy overalls with white webbing, a chrome hard hat with the orange visor stripe and a flip-up welding visor, a welding torch on one
hip (orange bottle on the back, copper hose) and a white crate of spare valves on the other, a big spanner carried across the body.
Silhouette at 1x: wide hip crate, a tall orange gas bottle behind, a long spanner.  Team colour: shoulder flashes and the crate stripe.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_parts as EP

ID = "exp_pioneer"
TITLE = "Expedition Pioneer"
CANVAS, ORIGIN, STATES, DIE_FACING = EP.CANVAS, EP.ORIGIN, EP.STATES, EP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = EP.PALETTE, EP.MATERIALS, EP.EMIT_STRENGTH
pose = EP.make_pose(lean=10.0, stride=0.25, hold=((0.08, 0.16, 0.12), 20.0, 55.0), fire=((0.1, 0.34, 0.42), 0.0, 20.0),
                    fire2=((0.1, 0.42, 0.18), 0.0, -40.0), flash=False, fire_lean=12.0, bobamp=0.05)


def pack(S, U, R):
    S.cyl(0.085, 0.50, (-0.07, -0.27, 0.30), "orange", "Z", U, verts=10)                      # gas bottle
    S.sphere((0.085, 0.085, 0.06), (-0.07, -0.27, 0.56), "orange2", U, seg=8)
    S.cyl(0.09, 0.04, (-0.07, -0.27, 0.40), "chrome", "Z", U, verts=10)
    S.box((0.20, 0.12, 0.26), (0.12, -0.26, 0.18), "navy3", U, bevel=0.02)


def extra(S, U, up, R, p):
    S.box((0.17, 0.12, 0.15), (0.20, 0.06, -0.08), "white", U, bevel=0.02)                   # valve crate
    S.box((0.175, 0.04, 0.05), (0.20, 0.06, -0.05), "house", U, bevel=0)
    S.cyl(0.03, 0.04, (0.20, 0.06, 0.0), "copper", "Z", U, verts=8)
    S.box((0.19, 0.04, 0.12), (0, 0.145, 0.62), "chrome3", U, bevel=0.01, rot=(35, 0, 0))     # flip-up welding visor
    S.cyl(0.02, 0.22, (-0.20, 0.10, -0.06), "chrome", "Y", U, verts=6)                       # torch on the hip
    S.cyl(0.03, 0.04, (-0.20, 0.23, -0.06), "orange", "Y", U, verts=6)


def weapon(S, GM, p, R):
    S.cyl(0.03, 0.62, (0, 0.16, 0), "chrome2", "Y", GM, verts=6)                             # spanner shaft
    S.box((0.11, 0.13, 0.05), (0, 0.50, 0), "chrome", GM)
    S.box((0.035, 0.09, 0.06), (0.03, 0.55, 0), "dark", GM)
    S.cyl(0.036, 0.12, (0, -0.10, 0), "orange2", "Y", GM, verts=6)


SPEC = dict(shoulder=(0.22, 0.0, 0.37), grips=((0, -0.04, 0.0), (0, 0.10, 0.0)), bulk=1.05,
            body=("navy", "navy2"), pack=pack, extra=extra, weapon=weapon)


def build(S, p):
    EP.build_figure(S, p, SPEC)
