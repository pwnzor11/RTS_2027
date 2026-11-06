"""Expedition Marine: amphibious rifle infantry.  Original design.  Same contract/states as auth_rifleman.py (see exp_parts.py).

Navy fatigues, white cross-webbing, a chrome helmet with an orange visor stripe, a sealed face plate with a chrome rebreather canister,
a pack with a white dry-bag and a short radio whip, and a gas-operated rifle (navy stock, chrome receiver, copper collar).
Silhouette at 1x: a round chrome dome over a navy body, white X on the chest, a rounded dry-bag hump and a whip behind.
Team colour: the flash on each shoulder.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_parts as EP

ID = "exp_marine"
TITLE = "Expedition Marine"
CANVAS, ORIGIN, STATES, DIE_FACING = EP.CANVAS, EP.ORIGIN, EP.STATES, EP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = EP.PALETTE, EP.MATERIALS, EP.EMIT_STRENGTH
pose = EP.make_pose(lean=8.0, stride=0.30, hold=((0.10, 0.30, 0.22), -14.0, 38.0), fire=((0.08, 0.32, 0.38), 10.0, 4.0),
                    fire2=((0.08, 0.28, 0.385), 10.0, 8.0), fire_lean=8.0, bobamp=0.05)


def pack(S, U, R):
    S.box((0.30, 0.16, 0.34), (0, -0.25, 0.22), "navy3", U, bevel=0.02)                  # pack frame
    S.cyl(0.11, 0.30, (0, -0.37, 0.24), "white", "X", U, verts=10)                       # white dry-bag roll
    S.cyl(0.04, 0.40, (0.11, -0.28, 0.56), "chrome2", "Z", U, verts=6)                   # radio whip
    S.sphere((0.04, 0.04, 0.04), (0.11, -0.28, 0.78), "orange", U, seg=6)
    S.box((0.06, 0.04, 0.14), (-0.10, -0.33, 0.50), "orange2", U, bevel=0)               # sealed radio set


def extra(S, U, up, R, p):
    S.cyl(0.05, 0.12, (0, 0.20, 0.43), "chrome", "Y", U, verts=10)                       # rebreather canister on the face plate
    S.cyl(0.058, 0.03, (0, 0.265, 0.43), "copper", "Y", U, verts=10)
    S.limb(up((-0.10, 0.12, 0.40)), up((-0.17, -0.10, 0.45)), 0.03, "rubber", bevel=0)    # breathing hose to the pack


def weapon(S, GM, p, R):
    S.box((0.05, 0.27, 0.10), (0, -0.25, -0.01), "navy2", GM, rot=(-6, 0, 0))            # stock
    S.box((0.06, 0.26, 0.09), (0, 0, 0), "chrome2", GM)                                  # receiver
    S.box((0.055, 0.28, 0.07), (0, 0.26, -0.005), "navy2", GM)                           # handguard
    S.cyl(0.022, 0.24, (0, 0.50, 0.005), "chrome", "Y", GM, verts=6)                     # barrel
    S.cyl(0.032, 0.04, (0, 0.40, 0.005), "copper", "Y", GM, verts=6)                     # collar
    S.box((0.04, 0.07, 0.15), (0, 0.02, -0.11), "dark", GM, rot=(8, 0, 0))               # magazine
    S.box((0.04, 0.05, 0.09), (0, -0.10, -0.07), "dark", GM)                             # grip
    S.box((0.02, 0.04, 0.04), (0, 0.04, 0.065), "orange", GM)                            # sight
    if p["flash"]:
        S.cyl(0.07, 0.24, (0, 0.76, 0.005), "flash", "Y", GM, verts=8, r2=0.0)
        S.sphere((0.09, 0.09, 0.09), (0, 0.64, 0.005), "flash", GM, seg=8)


SPEC = dict(shoulder=(0.245, 0.0, 0.37), grips=((0, -0.10, -0.075), (0, 0.20, -0.05)), bulk=1.0,
            pack=pack, extra=extra, weapon=weapon)


def build(S, p):
    EP.build_figure(S, p, SPEC)
