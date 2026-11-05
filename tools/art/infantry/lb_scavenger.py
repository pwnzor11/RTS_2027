"""Left Behind Scavenger: cheap rifle infantry who salvages wrecks.  Original design.  Same contract/states as dir_conscript.py.

Layered sun-bleached canvas and dusty-brown wraps under a knee-length coat, tall pointed canvas hood, yellow goggles, a filter-tin
respirator, a long house-colour scarf, a lumpy rope-tied salvage sack on the back with a pipe and a bone-white pot poking out, and
a rope-lashed bolt rifle.  Silhouette vs the Directorate conscript at 1x: a rounded sack hump behind, pale canvas not umber, no
hunch, a tall peaked hood, a house scarf tail.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_parts as LP

ID = "lb_scavenger"
TITLE = "Left Behind Scavenger"
CANVAS, ORIGIN, STATES, DIE_FACING = LP.CANVAS, LP.ORIGIN, LP.STATES, LP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = LP.PALETTE, LP.MATERIALS, LP.EMIT_STRENGTH
pose = LP.make_pose(lean=10.0)


def pack(S, U, R):
    S.sphere((0.19, 0.14, 0.22), (0.0, -0.26, 0.20), "wrapB", U)                  # lumpy sack
    S.sphere((0.11, 0.10, 0.11), (-0.08, -0.29, 0.40), "wrapA", U)
    S.cyl(0.16, 0.03, (0, -0.26, 0.27), "rope", "Z", U, verts=8)
    S.cyl(0.035, 0.40, (0.12, -0.30, 0.55), "steel", "Z", U, verts=6)           # pipe poking out
    S.sphere((0.08, 0.07, 0.06), (-0.10, -0.32, 0.52), "bone", U)                # a pot


def weapon(S, GM, p, R):
    S.box((0.05, 0.30, 0.09), (0, -0.26, -0.01), "wood", GM, rot=(-6, 0, 0))
    S.box((0.05, 0.22, 0.075), (0, 0.0, 0), "wood", GM)
    S.cyl(0.02, 0.42, (0, 0.32, 0.01), "steel", "Y", GM, verts=6)
    S.cyl(0.03, 0.03, (0, 0.05, 0.0), "rope", "Y", GM, verts=6)
    S.box((0.035, 0.05, 0.035), (0.045, -0.02, 0.035), "dark", GM)
    if p["flash"]:
        S.cyl(0.07, 0.24, (0, 0.76, 0.01), "flash", "Y", GM, verts=8, r2=0.0)
        S.sphere((0.09, 0.09, 0.09), (0, 0.64, 0.01), "flash", GM, seg=8)


SPEC = dict(shoulder=(0.18, 0.02, 0.36), grips=((0, -0.10, -0.06), (0, 0.22, -0.045)), bulk=1.0,
            coat=("wrapA", "wrapB", "wrapE", "wrapC"), hem_z=-0.20, cape="wrapA", scarf="house",
            hood=("wrapA", 0.30, 0.10), pack=pack, weapon=weapon, sleeves=("wrapB", "wrapA", "rope"))


def build(S, p):
    LP.build_figure(S, p, SPEC)
