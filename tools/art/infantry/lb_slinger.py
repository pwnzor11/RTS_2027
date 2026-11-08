"""Left Behind Slinger: throws shaped charges (anti-armour).  Original design.  Same contract/states as dir_conscript.py.

Rust-red wrap coat to the shin under a wide ochre shoulder wrap, a short peaked hood, yellow goggles and a filter-tin respirator, a
house-colour scarf, a bandolier of three hazard-banded charge tubes across the chest and a canvas charge satchel on the hip.  The
right hand carries a fat shaped charge (rust tube, hazard band, bone cone) cocked up beside the head, thrown in the fire frames.
Silhouette vs the scavenger and conscript at 1x: red not pale or umber, a raised arm with a bright banded lump, no long weapon.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_parts as LP

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48, team-colour emphasis
ID = "lb_slinger"
TITLE = "Left Behind Slinger"
CANVAS, ORIGIN, STATES, DIE_FACING = LP.CANVAS, LP.ORIGIN, LP.STATES, LP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = LP.PALETTE, LP.MATERIALS, LP.EMIT_STRENGTH
pose = LP.make_pose(lean=12.0, stride=0.27, hold=((0.24, 0.14, 0.34), 0.0, 30.0), fire=((0.2, 0.42, 0.46), 0.0, -20.0),
                    fire2=((0.17, 0.52, 0.24), 0.0, -40.0), hands=(("u", (-0.24, 0.12, -0.02)), "gun"), flash=False, fire_lean=16.0)


def extra(S, U, up, R):
    for i, z in enumerate((0.30, 0.20, 0.10)):                                    # bandolier of charge tubes
        S.cyl(0.04, 0.17, (0.01 * i, 0.215, z), "rust" if i != 1 else "hazard", "X", U, verts=8, r2=0.04)
    S.limb(up((-0.12, 0.18, 0.34)), up((0.17, 0.22, 0.02)), 0.05, "rope", d=0.03, bevel=0)
    S.box((0.16, 0.08, 0.14), (0.22, 0.05, -0.06), "wrapA", U)                    # satchel on the hip
    S.box((0.14, 0.02, 0.05), (0.22, 0.10, -0.01), "hazard", U, bevel=0)


def pack(S, U, R):
    S.box((0.26, 0.10, 0.26), (0.0, -0.24, 0.12), "wrapB", U)
    S.cyl(0.06, 0.20, (0.0, -0.31, 0.30), "rust", "X", U, verts=8)


def weapon(S, GM, p, R):
    S.cyl(0.075, 0.28, (0, 0.14, 0), "rust", "Y", GM, verts=10)                   # fat shaped charge
    S.cyl(0.08, 0.06, (0, 0.10, 0), "hazard", "Y", GM, verts=10)
    S.cyl(0.075, 0.12, (0, 0.34, 0), "bone", "Y", GM, verts=10, r2=0.02)           # cone nose
    S.cyl(0.02, 0.1, (0, -0.02, 0.0), "rope", "Y", GM, verts=5)


SPEC = dict(house_boost=True, panel=((0.18,0.03,0.14),(0.0,-0.305,0.12)), shoulder=(0.19, 0.02, 0.36), grips=((0, -0.03, 0.0), (0, 0.0, 0.0)), bulk=1.0,
            coat=("wrapE", "wrapE", "ashB", "hazard"), hem_z=-0.28, cape="wrapB", scarf="house",
            hood=("wrapB", 0.18, 0.10), extra=extra, pack=pack, weapon=weapon, sleeves=("wrapE", "wrapB", "rope"))


def build(S, p):
    LP.build_figure(S, p, SPEC)
