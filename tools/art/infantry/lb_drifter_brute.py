"""Left Behind Drifter Brute: heavy flame-hammer infantry that tanks the dust.  Original design.  Same contract/states as
dir_conscript.py.

Half as wide again as anyone else: a barrel chest in rust and ash wraps, a bolted steel pauldron with a hazard strip on one
shoulder, a plate on the chest, thick limbs, a low wide hood, yellow goggles and an outsize filter-tin respirator, a house
scarf, and a flame-hammer: a long haft, a steel block head, a rust gas bottle lashed to the shaft and a jet that flares in the fire frames.
Silhouette vs the other Left Behind infantry at 1x: much broader and shorter-necked, one huge shoulder, a big hammer head.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_parts as LP

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48, team-colour emphasis
ID = "lb_drifter_brute"
TITLE = "Left Behind Drifter Brute"
CANVAS, ORIGIN, STATES, DIE_FACING = LP.CANVAS, LP.ORIGIN, LP.STATES, LP.DIE_FACING
PALETTE, MATERIALS, EMIT_STRENGTH = LP.PALETTE, LP.MATERIALS, LP.EMIT_STRENGTH
pose = LP.make_pose(lean=12.0, stride=0.20, hold=((0.22, 0.06, 0.14), -8.0, 72.0), fire=((0.14, 0.36, 0.30), 0.0, -22.0),
                    fire2=((0.12, 0.44, 0.08), 0.0, -48.0), bobamp=0.08, fire_lean=16.0)


def extra(S, U, up, R):
    S.box((0.20, 0.07, 0.15), (-0.27, 0.0, 0.40), "steel", U, bevel=0.02)               # pauldron
    S.box((0.21, 0.075, 0.04), (-0.27, 0.0, 0.46), "hazard", U, bevel=0)
    S.box((0.26, 0.04, 0.20), (0.0, 0.26, 0.20), "ashA", U, bevel=0.01)                 # chest plate
    S.box((0.12, 0.045, 0.05), (0.0, 0.27, 0.25), "rust", U, bevel=0)


def pack(S, U, R):
    S.box((0.34, 0.12, 0.30), (0.0, -0.30, 0.22), "wrapC", U)


def weapon(S, GM, p, R):
    S.cyl(0.035, 0.80, (0, 0.20, 0), "wood", "Y", GM, verts=6)                        # haft
    S.box((0.16, 0.22, 0.14), (0, 0.62, 0), "steel", GM, bevel=0.015)                 # head block
    S.box((0.17, 0.05, 0.15), (0, 0.50, 0), "hazard", GM, bevel=0)
    S.cyl(0.07, 0.30, (0.05, 0.22, 0.09), "rust", "Y", GM, verts=8)                    # gas bottle
    S.cyl(0.075, 0.03, (0.05, 0.30, 0.09), "rope", "Y", GM, verts=8)
    S.cyl(0.02, 0.28, (0.0, 0.40, 0.07), "dark", "Y", GM, verts=5)                     # hose
    if p["flash"]:
        S.cyl(0.10, 0.36, (0, 0.92, 0), "flash", "Y", GM, verts=8, r2=0.0)
        S.sphere((0.11, 0.11, 0.11), (0, 0.78, 0), "flash", GM, seg=8)


SPEC = dict(house_boost=True, panel=((0.22,0.03,0.16),(0.0,-0.375,0.22)), shoulder=(0.27, 0.02, 0.37), grips=((0, -0.04, 0.0), (0, 0.16, 0.0)), bulk=1.3, tin_len=0.13,
            coat=("wrapE", "wrapC", "ashB", "hazard"), hem_z=-0.12, cape="wrapB", scarf="house",
            hood=("wrapB", 0.10, 0.06), extra=extra, pack=pack, weapon=weapon, sleeves=("wrapC", "wrapE", "rope"),
            legs=("pants", 0.12, (0.125, 0.105)))


def build(S, p):
    LP.build_figure(S, p, SPEC)
