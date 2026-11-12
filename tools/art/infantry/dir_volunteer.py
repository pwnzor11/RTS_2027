"""Directorate Volunteer: an irradiated runner.  Thin and lean, hunched far forward in a ragged half-coat tied open at the chest,
bare pale arms and a bandaged head with one dark goggle, reactor-cell canisters strapped across the chest and back (brass lamps lit),
a snub pistol in one hand.  Original design.  Same contract, scale, camera, states and facings as dir_conscript.py; poses:
a fast loping run (big stride, high knees, off arm pumping), one-handed pistol, forward fall in die.
Silhouette vs the conscript at 1x: no long coat or rifle, bare pale limbs, a clump of cylinders on the chest, hunched run.
House colour: sash, armbands, a panel on the back cell-pack, shoulder boards.
NOTE: the radiation-green ramp (D-099) is not used: the infantry mask pass has no radiation channel; the glow is brass lamps.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C
import auth_rifleman as AR
import dir_conscript as DC

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48
ID = "dir_volunteer"
TITLE = "Directorate Volunteer"
CANVAS, ORIGIN, STATES, DIE_FACING = AR.CANVAS, AR.ORIGIN, AR.STATES, AR.DIE_FACING
PALETTE = C.PAL_DIR
MATERIALS = dict(DC.MATERIALS)
MATERIALS.update({"skin": ("#E3DAC2", 0.7, 0.0, None), "wrap": ("#C9BFA6", 0.9, 0.0, None),
                  "lamp": ("#FFE79A", 0.3, 0.0, "#FFD060"), "cell": ("#555B61", 0.5, 0.3, None)})
EMIT_STRENGTH = {"flash": 8.0, "lamp": 4.0}
SHOULDER = (0.18, 0.02, 0.36)
GRIP_R, GRIP_L = (0, -0.04, -0.05), (0, 0.0, 0.0)
PISTOL = ((0.12, 0.30, 0.30), -6.0, 6.0)


def pose(state, step, n):
    base = dict(bob=0.0, lean=18.0, twist=0.0, dy=0.0, flash=False, gun_space="u", roll=0.0, spread=False,
                feet=((-0.11, 0.03, 0.09), (0.11, -0.03, 0.09)), gun=PISTOL,
                hands=(("u", (-0.22, 0.10, -0.05)), "gun"))
    if state == "idle":
        base["bob"] = 0.02
        base["gun"] = ((0.14, 0.18, 0.04), 0.0, -30.0)
        return base
    if state == "walk":
        ph = 2 * math.pi * step / n
        feet = []
        for off, xx in ((0.0, -0.11), (math.pi, 0.11)):
            q = ph + off
            feet.append((xx, 0.38 * math.cos(q), 0.09 + 0.24 * max(0.0, -math.sin(q))))
        sw = -math.cos(ph)
        base.update(feet=tuple(feet), bob=0.05 * -math.cos(2 * ph), lean=24.0, twist=11.0 * math.cos(ph),
                    gun=((0.15, 0.14 + 0.16 * -sw, 0.02 + 0.06 * max(0.0, -sw)), 0.0, -30.0),
                    hands=(("u", (-0.24, 0.30 * sw, -0.02 + 0.12 * max(0.0, sw))), "gun"))
        return base
    if state == "fire":
        base.update(lean=14.0, twist=-14.0, feet=((-0.14, 0.12, 0.09), (0.12, -0.20, 0.09)), gun=((0.12, 0.30, 0.32), -6.0, 6.0),
                    hands=(("u", (-0.20, 0.12, 0.05)), "gun"))
        if step == 1:
            base.update(flash=True, lean=12.0, gun=((0.12, 0.26, 0.325), -6.0, 11.0))
        return base
    d = AR.pose(state, step, n)
    d.update(roll=[0.0, 0.0, -25.0, -55.0, -80.0, -90.0][step], spread=step >= 2)
    return d


def build(S, p):
    from mathutils import Matrix
    import rig as R
    import dir_parts as P
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"]) @ R.rz(p["roll"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    # bare, wrapped torso in a short ragged half-coat that stops at the hip
    S.cyl(0.18, 0.34, (0, 0, 0.20), "wrap", "Z", U, verts=8, r2=0.16)
    S.cyl(0.21, 0.16, (0, -0.02, 0.06), "coat3", "Z", U, verts=8, r2=0.19)             # ragged skirt
    S.cyl(0.19, 0.04, (0, 0, 0.05), "dark", "Z", U, verts=8)
    S.box((0.34, 0.20, 0.06), (0, -0.01, 0.39), "coat2", U, bevel=0.01)                 # ragged yoke
    for sx in (-1, 1):
        S.box((0.12, 0.17, 0.04), (sx * 0.19, 0.0, 0.40), "house", U, rot=(0, sx * -8, 0), bevel=0)
    S.limb(up((-0.10, 0.17, 0.35)), up((0.14, 0.20, -0.04)), 0.10, "house", d=0.04, bevel=0)      # sash
    S.limb(up((-0.17, 0.02, 0.35)), up((-0.10, 0.17, 0.35)), 0.09, "house", d=0.04, bevel=0)
    # reactor cells across the chest: three cylinders, brass lamps on top
    for i, x in enumerate((-0.09, 0.0, 0.09)):
        S.cyl(0.05, 0.17, (x, 0.20, 0.18 - 0.02 * (i % 2)), "cell", "Z", U, verts=8)
        S.cyl(0.035, 0.03, (x, 0.20, 0.285 - 0.02 * (i % 2)), "lamp", "Z", U, verts=6)
    S.box((0.28, 0.05, 0.04), (0, 0.19, 0.10), "dark", U, bevel=0)                      # strap
    # back cell-pack with a house panel and a larger lamp
    S.box((0.28, 0.14, 0.30), (0, -0.25, 0.20), "coat3", U, bevel=0.02)
    S.box((0.22, 0.03, 0.22), (0, -0.325, 0.22), "house", U, bevel=0)
    S.cyl(0.06, 0.30, (0.0, -0.25, 0.46), "cell", "Z", U, verts=8)
    S.cyl(0.05, 0.03, (0.0, -0.25, 0.625), "lamp", "Z", U, verts=6)
    # head: bandaged skull cap, one dark goggle, gaunt pale face
    S.box((0.08, 0.09, 0.07), (0, 0.08, 0.44), "dark", U)
    S.sphere((0.135, 0.145, 0.13), (0, 0.11, 0.56), "wrap", U)
    S.box((0.17, 0.13, 0.11), (0, 0.205, 0.50), "skin", U, bevel=0.03)
    S.box((0.075, 0.04, 0.07), (-0.05, 0.28, 0.53), "dark", U, bevel=0)                  # goggle (left eye)
    S.box((0.05, 0.035, 0.04), (0.05, 0.28, 0.53), "lamp", U, bevel=0)                    # glowing right eye
    S.box((0.20, 0.03, 0.05), (0, 0.115, 0.62), "dark", U, bevel=0)                      # head strap
    # pistol
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    S.box((0.05, 0.18, 0.06), (0, 0.04, 0.02), "steel", GM)
    S.box((0.045, 0.05, 0.11), (0, -0.03, -0.05), "dark", GM, rot=(10, 0, 0))
    S.cyl(0.02, 0.10, (0, 0.19, 0.02), "steel", "Y", GM, verts=6)
    if p["flash"]:
        S.cyl(0.07, 0.22, (0, 0.34, 0.02), "flash", "Y", GM, verts=8, r2=0.0)
        S.sphere((0.09, 0.09, 0.09), (0, 0.26, 0.02), "flash", GM, seg=8)
    P.arms(S, p, U, up, GM, SHOULDER, (GRIP_R, GRIP_L), ("skin", "skin"), thick=(0.07, 0.06), band=0.105,
           cuff_mat="wrap", cuff_len=0.4, hand_mat="skin", hand_size=0.07)
    P.legs(S, p, pel, spread=0.09, thick=(0.085, 0.07))
    if p["spread"]:
        S.cyl(0.32, 0.03, (0.0, p["dy"] + 0.12, 0.03), "coat3", "Z", None, verts=8)
