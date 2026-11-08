"""Expedition Ironbound Trooper: powered-suit heavy infantry with shoulder anti-air missile pods.  Original design.
Same contract/states as auth_rifleman.py (see exp_parts.py).

A bulky valve-driven suit: navy armour plates with white edging, a chrome dome helmet with orange visor stripe and a glass slit,
hydraulic gauntlets (big chrome fists with copper pistons), a boxy back-frame with twin steam stacks, and a missile pod on each
shoulder (white tubes, orange warheads, team colour on the pod sides).  Right-arm pod is fired in the fire frames.
Silhouette at 1x: twice as wide as a Marine, broad square shoulders with pods, small head.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_parts as EP

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48, team-colour emphasis
ID = "exp_ironbound"
TITLE = "Expedition Ironbound Trooper"
CANVAS, ORIGIN, STATES, DIE_FACING = EP.CANVAS, EP.ORIGIN, EP.STATES, EP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = EP.PALETTE, EP.MATERIALS, EP.EMIT_STRENGTH
pose = EP.make_pose(lean=5.0, stride=0.22, hold=((0.12, 0.26, 0.14), 0.0, 25.0), fire=((0.12, 0.30, 0.22), 0.0, 6.0),
                    fire2=((0.12, 0.28, 0.225), 0.0, 8.0), fire_lean=5.0, bobamp=0.035)


def pack(S, U, R):
    S.box((0.38, 0.20, 0.42), (0, -0.28, 0.24), "navy3", U, bevel=0.03)
    S.box((0.30, 0.03, 0.16), (0, -0.385, 0.28), "house", U, bevel=0)
    for sx in (-1, 1):
        S.cyl(0.05, 0.40, (sx * 0.13, -0.30, 0.62), "chrome2", "Z", U, verts=8)               # steam stacks
        S.cyl(0.058, 0.04, (sx * 0.13, -0.30, 0.82), "copper", "Z", U, verts=8)


def extra(S, U, up, R, p):
    for sx in (-1, 1):
        S.box((0.20, 0.34, 0.20), (sx * 0.33, 0.0, 0.52), "navy", U, bevel=0.03)            # shoulder plate
        S.box((0.07, 0.30, 0.15), (sx * 0.435, 0.0, 0.54), "house", U, bevel=0)               # team colour pod side
        S.box((0.21, 0.35, 0.03), (sx * 0.33, 0.0, 0.625), "white", U, bevel=0)
        for k in (-1, 1):
            S.cyl(0.04, 0.34, (sx * 0.33 + k * 0.045, 0.06, 0.64), "white2", "Y", U, verts=8)  # missile tubes
            S.cyl(0.042, 0.08, (sx * 0.33 + k * 0.045, 0.26, 0.64), "orange", "Y", U, verts=8, r2=0.0)
    S.box((0.34, 0.10, 0.08), (0, 0.14, 0.34), "white", U, bevel=0.01)                      # chest plate edging
    S.box((0.19, 0.04, 0.045), (0, 0.17, 0.52), "glass", U, bevel=0.01, )                    # helmet slit (over visor)


def weapon(S, GM, p, R):
    S.box((0.16, 0.20, 0.16), (0, 0.10, 0), "chrome", GM, bevel=0.03)                        # hydraulic fist
    S.cyl(0.03, 0.30, (0.07, -0.08, 0.0), "copper", "Y", GM, verts=6)                        # piston
    S.box((0.17, 0.05, 0.17), (0, 0.20, 0), "chrome3", GM, bevel=0.01)
    if p["flash"]:
        S.cyl(0.08, 0.36, (0.0, 0.62, 0.0), "flash", "Y", GM, verts=8, r2=0.0)


SPEC = dict(house_boost=True, shoulder=(0.32, 0.0, 0.36), grips=((0, 0.10, 0.0), (0, 0.10, 0.0)), bulk=1.35,
            body=("navy", "navy2"), sleeves=("navy", "chrome2", "chrome"), helm="chrome", pack=pack, extra=extra, weapon=weapon,
            legs=("navy2", 0.13, (0.12, 0.10)), boot="chrome3")


def build(S, p):
    EP.build_figure(S, p, SPEC)
