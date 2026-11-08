"""Expedition Combat Diver: underwater saboteur.  Original design.  Same contract/states as auth_rifleman.py (see exp_parts.py).

Close-fitting dark navy suit with minimal white webbing, a chrome helmet with a big glass dive mask (orange stripe above it), twin
chrome rebreather tanks with copper valves on the back, long flippers, a sealed wrist watch and a limpet charge held in the hand
(a copper-banded orange disc, thrown in the fire frames).  Silhouette at 1x: low and slim, twin tanks, wide flat feet.
Team colour: shoulder flashes and a stripe on the tanks.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_parts as EP

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48, house shoulder pads / helmet band / tank stripes
ID = "exp_diver"
TITLE = "Expedition Combat Diver"
CANVAS, ORIGIN, STATES, DIE_FACING = EP.CANVAS, EP.ORIGIN, EP.STATES, EP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = EP.PALETTE, EP.MATERIALS, EP.EMIT_STRENGTH
pose = EP.make_pose(lean=12.0, stride=0.22, hold=((0.06, 0.16, 0.12), 20.0, 55.0), fire=((0.1, 0.34, 0.42), 0.0, 20.0),
                    fire2=((0.1, 0.44, 0.20), 0.0, -40.0), flash=False, fire_lean=14.0, bobamp=0.04)


def pack(S, U, R):
    for sx in (-1, 1):
        S.cyl(0.075, 0.46, (sx * 0.10, -0.26, 0.24), "chrome", "Z", U, verts=10)              # twin tanks
        S.cyl(0.082, 0.07, (sx * 0.10, -0.26, 0.14), "house", "Z", U, verts=10)
        S.cyl(0.05, 0.05, (sx * 0.10, -0.26, 0.49), "copper", "Z", U, verts=8)
    S.box((0.24, 0.05, 0.05), (0, -0.26, 0.46), "chrome3", U, bevel=0)                       # manifold


def extra(S, U, up, R, p):
    S.box((0.21, 0.06, 0.09), (0, 0.15, 0.525), "glass", U, bevel=0.02)                      # big dive mask
    S.box((0.23, 0.04, 0.03), (0, 0.14, 0.58), "chrome2", U, bevel=0)
    S.limb(up((0.06, 0.13, 0.46)), up((0.15, -0.12, 0.46)), 0.03, "rubber", bevel=0)         # hose


def weapon(S, GM, p, R):
    S.cyl(0.10, 0.06, (0, 0.05, 0), "orange", "Z", GM, verts=12)                             # limpet charge
    S.cyl(0.105, 0.025, (0, 0.05, 0.035), "copper", "Z", GM, verts=12)
    S.sphere((0.03, 0.03, 0.03), (0, 0.05, 0.065), "lens", GM, seg=6)


SPEC = dict(shoulder=(0.20, 0.0, 0.36), grips=((0, 0.0, 0.0), (0, 0.10, 0.0)), bulk=0.88,
            body=("navy3", "navy2"), sleeves=("navy3", "navy2", "chrome2"), legs=("navy2", 0.085, (0.085, 0.07)), boot="chrome3",
            pack=pack, extra=extra, weapon=weapon, house_boost=True)


def build(S, p):
    EP.build_figure(S, p, SPEC)
