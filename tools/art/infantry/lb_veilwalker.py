"""Left Behind Veilwalker: stealth scout and sniper in the ash.  Original design.  Same contract/states as dir_conscript.py.

The tallest and narrowest of the six: an ankle-length ragged veil of three grey layers (ash, dark ash, bone) under a very tall
pointed hood with a house-colour band, yellow goggles and a filter-tin respirator, a long scarf, and a long canvas-wrapped sniper
rifle with a scope.  Ash grey on purpose, so it merges into the ash ground at 1x.
Silhouette vs the other Left Behind infantry at 1x: a slim tall cone with a needle-long rifle, no sack, no pole.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_parts as LP

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48, team-colour emphasis
ID = "lb_veilwalker"
TITLE = "Left Behind Veilwalker"
CANVAS, ORIGIN, STATES, DIE_FACING = LP.CANVAS, LP.ORIGIN, LP.STATES, LP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = LP.PALETTE, LP.MATERIALS, LP.EMIT_STRENGTH
pose = LP.make_pose(lean=8.0, stride=0.20, hold=((0.04, 0.17, 0.10), 18.0, 62.0), fire=((0.07, 0.30, 0.38), 8.0, 4.0),
                    fire2=((0.07, 0.26, 0.385), 8.0, 8.0), bobamp=0.05, fire_lean=10.0)


def extra(S, U, up, R):
    S.cyl(0.27, 0.40, (0, -0.02, -0.04), "ashA", "Z", U, verts=8, r2=0.21)           # outer veil over the skirt
    S.cyl(0.275, 0.06, (0, -0.02, -0.22), "bone", "Z", U, verts=8)                    # ragged bone-pale hem
    S.box((0.18, 0.03, 0.14), (0.0, -0.215, 0.16), "ashB", U, rot=(8, 0, 0), bevel=0)  # back flap
    S.limb(up((-0.13, 0.05, 0.62)), up((0.13, 0.05, 0.62)), 0.04, "house", d=0.03, bevel=0)   # house band on the hood


def pack(S, U, R):
    S.cyl(0.04, 0.34, (0.12, -0.24, 0.30), "rope", "Z", U, verts=6)                  # rolled ground sheet


def weapon(S, GM, p, R):
    S.box((0.05, 0.30, 0.09), (0, -0.28, -0.01), "wrapC", GM, rot=(-6, 0, 0))
    S.box((0.05, 0.22, 0.07), (0, 0.0, 0), "wood", GM)
    S.cyl(0.02, 0.70, (0, 0.46, 0.01), "steel", "Y", GM, verts=6)                    # long barrel
    for y in (0.25, 0.45, 0.65):
        S.cyl(0.03, 0.04, (0, y, 0.01), "wrapA", "Y", GM, verts=6)                    # canvas wraps
    S.cyl(0.03, 0.16, (0, 0.05, 0.06), "dark", "Y", GM, verts=6)                      # scope
    if p["flash"]:
        S.cyl(0.07, 0.24, (0, 1.06, 0.01), "flash", "Y", GM, verts=8, r2=0.0)
        S.sphere((0.09, 0.09, 0.09), (0, 0.94, 0.01), "flash", GM, seg=8)


SPEC = dict(house_boost=True, panel=((0.14,0.03,0.10),(0.0,-0.235,0.17)), shoulder=(0.16, 0.02, 0.36), grips=((0, -0.12, -0.05), (0, 0.24, -0.04)), bulk=0.9,
            coat=("ashA", "ashB", "wrapC", "bone"), hem_z=-0.50, cape="wrapC", scarf="house",
            hood=("ashA", 0.34, 0.12), extra=extra, pack=pack, weapon=weapon, sleeves=("wrapC", "ashA", "rope"),
            legs=("pants", 0.085, (0.085, 0.075)))


def build(S, p):
    LP.build_figure(S, p, SPEC)
