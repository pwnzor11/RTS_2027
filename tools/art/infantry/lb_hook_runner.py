"""Left Behind Hook Runner: fast raider who steals from silos.  Original design.  Same contract/states as dir_conscript.py.

Slim and leaning into a run: hip-length teal wrap coat over long dark legs, a tall thin pointed hood, yellow goggles, filter-tin
respirator, a very long house-colour scarf, a big coil of rope on the back and a grapple hook (steel, three tines) swung in the
right hand on a short line.  Silhouette vs the conscript and the scavenger at 1x: narrow, long-legged, forward-leaning, with a
round rope coil and a hooked tool instead of a rifle.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_parts as LP

ID = "lb_hook_runner"
TITLE = "Left Behind Hook Runner"
CANVAS, ORIGIN, STATES, DIE_FACING = LP.CANVAS, LP.ORIGIN, LP.STATES, LP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = LP.PALETTE, LP.MATERIALS, LP.EMIT_STRENGTH
pose = LP.make_pose(lean=20.0, stride=0.34, hold=((0.17, 0.12, 0.02), -5.0, 25.0), fire=((0.12, 0.34, 0.44), 0.0, -12.0),
                    fire2=((0.10, 0.52, 0.40), 0.0, -26.0), hands=(("u", (-0.22, 0.16, 0.02)), "gun"), bobamp=0.09, flash=False,
                    fire_lean=16.0)


def pack(S, U, R):
    S.cyl(0.20, 0.13, (0.0, -0.24, 0.20), "rope", "Y", U, verts=10)              # rope coil on the back
    S.cyl(0.12, 0.15, (0.0, -0.24, 0.20), "dark", "Y", U, verts=10)
    S.cyl(0.205, 0.04, (0.0, -0.24, 0.20), "wrapE", "Y", U, verts=10)
    S.box((0.16, 0.10, 0.20), (0.0, -0.20, 0.0), "wrapB", U)


def weapon(S, GM, p, R):
    S.cyl(0.025, 0.30, (0, 0.05, 0), "wood", "Y", GM, verts=6)                    # short haft
    S.cyl(0.02, 0.45, (0, 0.32, -0.04), "rope", "Y", GM, verts=5)                 # line
    S.box((0.05, 0.05, 0.16), (0, 0.55, -0.04), "steel", GM)                      # hook shank
    for dx in (-0.07, 0.0, 0.07):
        S.box((0.03, 0.03, 0.12), (dx, 0.60, -0.10 + abs(dx)), "steel", GM, rot=(0, 0, dx * 200))
    S.box((0.22, 0.04, 0.03), (0, 0.58, -0.12), "steel", GM)


SPEC = dict(shoulder=(0.15, 0.02, 0.36), grips=((0, -0.04, 0.0), (0, 0.02, 0.0)), bulk=0.85,
            coat=("wrapD", "wrapB", "ashB", "wrapA"), hem_z=-0.02, cape=None, scarf="house",
            hood=("wrapA", 0.36, 0.16), pack=pack, weapon=weapon, sleeves=("wrapD", "wrapB", "rope"),
            legs=("pants", 0.09, (0.085, 0.075)))


def build(S, p):
    LP.build_figure(S, p, SPEC)
