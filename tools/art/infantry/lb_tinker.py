"""Left Behind Tinker: engineer, repair aura, salvage.  Original design.  Same contract/states as dir_conscript.py.

Bone-and-teal wraps under a tool apron, a short hood, a big brass-yellow magnifier lens over one eye next to the goggles, a
filter-tin respirator, a boxy rust-red toolbox pack on the back with a tall wooden mast carrying a house-colour pennant (the
tallest silhouette of the six, a repair beacon), and a long steel wrench carried across the body (swung in the fire frames).
Silhouette vs the other Left Behind infantry at 1x: a vertical pole and flag above the head, a square pack, a bone-pale front.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_parts as LP

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48, team-colour emphasis
ID = "lb_tinker"
TITLE = "Left Behind Tinker"
CANVAS, ORIGIN, STATES, DIE_FACING = LP.CANVAS, LP.ORIGIN, LP.STATES, LP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = LP.PALETTE, LP.MATERIALS, LP.EMIT_STRENGTH
pose = LP.make_pose(lean=10.0, stride=0.25, hold=((0.06, 0.16, 0.12), 20.0, 55.0), fire=((0.1, 0.34, 0.42), 0.0, 20.0),
                    fire2=((0.1, 0.42, 0.18), 0.0, -40.0), flash=False, fire_lean=14.0)


def extra(S, U, up, R):
    S.box((0.20, 0.04, 0.26), (0.0, 0.215, 0.0), "wrapA", U, bevel=0)               # apron
    S.box((0.20, 0.03, 0.05), (0.0, 0.225, 0.11), "rust", U, bevel=0)               # pocket
    S.cyl(0.075, 0.05, (0.07, 0.31, 0.57), "hazard", "Y", U, verts=10)              # magnifier lens frame
    S.cyl(0.055, 0.055, (0.07, 0.315, 0.57), "lens", "Y", U, verts=10)


def pack(S, U, R):
    S.box((0.30, 0.16, 0.34), (0.0, -0.27, 0.22), "rust", U, bevel=0.02)             # toolbox
    S.box((0.32, 0.03, 0.08), (0.0, -0.27, 0.40), "steel", U, bevel=0)
    S.box((0.10, 0.03, 0.10), (0.0, -0.355, 0.22), "hazard", U, bevel=0)
    S.cyl(0.025, 0.62, (0.10, -0.30, 0.55), "wood", "Z", U, verts=6)                 # mast
    S.box((0.03, 0.26, 0.17), (0.10, -0.44, 0.78), "house", U, bevel=0)              # pennant


def weapon(S, GM, p, R):
    S.cyl(0.03, 0.62, (0, 0.16, 0), "steel", "Y", GM, verts=6)                     # wrench shaft
    S.box((0.10, 0.12, 0.05), (0, 0.50, 0), "steel", GM)
    S.box((0.03, 0.08, 0.06), (0.025, 0.55, 0), "dark", GM)                        # jaw gap
    S.cyl(0.035, 0.1, (0, -0.10, 0), "rope", "Y", GM, verts=6)                     # grip tape


SPEC = dict(house_boost=True, panel=((0.20,0.03,0.07),(0.0,-0.355,0.33)), shoulder=(0.17, 0.02, 0.36), grips=((0, -0.04, 0.0), (0, 0.10, 0.0)), bulk=1.0,
            coat=("wrapF", "wrapD", "ashB", "rust"), hem_z=-0.14, cape="wrapD", scarf="house",
            hood=("wrapF", 0.12, 0.06), extra=extra, pack=pack, weapon=weapon, sleeves=("wrapD", "wrapF", "rope"))


def build(S, p):
    LP.build_figure(S, p, SPEC)
