"""Authority Marksman: the rifleman's sealed ashfall kit cut for patience: a scrim-draped helmet with a wide sun-shade brim, a long
cape of ash-grey cloth down the back instead of a bulky pack, a slim ammunition bandolier across the chest and a long
scoped bolt rifle with a bipod.  Original design.  Same contract, scale, camera, states and facings as auth_rifleman.py;
poses: slower walk, rifle held across the chest, deeper aiming lean in fire.
Silhouette vs the rifleman at 1x: a rifle clearly longer than the figure is wide, a round scope bulb above the receiver,
a flat-topped cape instead of a square pack.
House colour: band on each upper arm, helmet band, panel on the cape, shoulder pads.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import auth_rifleman as AR

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48
ID = "auth_marksman"
TITLE = "Authority Marksman"
CANVAS, ORIGIN, STATES, DIE_FACING = AR.CANVAS, AR.ORIGIN, AR.STATES, AR.DIE_FACING
MATERIALS = dict(AR.MATERIALS)
MATERIALS.update({"cape": ("#7C8A66", 0.95, 0.0, None), "scrim": ("#9DAA7A", 0.95, 0.0, None)})
EMIT_STRENGTH = AR.EMIT_STRENGTH
SHOULDER = AR.SHOULDER
L_UPPER, L_FORE, L_THIGH, L_SHIN = AR.L_UPPER, AR.L_FORE, AR.L_THIGH, AR.L_SHIN
GRIP_R, GRIP_L = (0, -0.12, -0.075), (0, 0.30, -0.05)


def pose(state, step, n):
    p = AR.pose(state, step, n)
    if state == "idle":
        p.update(gun=((0.10, 0.28, 0.27), -22.0, 18.0), lean=4.0)
    elif state == "walk":
        p.update(gun=((p["gun"][0][0], p["gun"][0][1] + 0.02, p["gun"][0][2] + 0.03), -10.0, 20.0))
    elif state == "fire":
        p.update(lean=10.0, twist=-10.0, feet=((-0.15, 0.10, 0.09), (0.13, -0.24, 0.09)), gun=((0.09, 0.36, 0.385), 10.0, 0.0))
        if step == 1:
            p.update(gun=((0.09, 0.31, 0.39), 10.0, 3.5), lean=8.0)
    return p


def build(S, p):
    from mathutils import Matrix
    import rig as R
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    S.box((0.40, 0.26, 0.24), (0, 0, -0.02), "hull", U, bevel=0.03)
    S.box((0.36, 0.24, 0.40), (0, 0, 0.22), "cloth", U, bevel=0.03)
    S.box((0.42, 0.27, 0.05), (0, 0, 0.05), "rust", U)
    S.limb(up((-0.16, 0.10, 0.38)), up((0.16, 0.14, 0.02)), 0.075, "rust", d=0.05, bevel=0)      # bandolier
    for t in (0.2, 0.4, 0.6, 0.8):
        a = R.Vector((-0.16, 0.14, 0.38)).lerp(R.Vector((0.16, 0.17, 0.02)), t)
        S.box((0.035, 0.03, 0.05), tuple(up(tuple(a))), "steel", None, bevel=0)
    S.box((0.42, 0.28, 0.06), (0, -0.02, 0.43), "cloth", U, bevel=0.03)
    for sx in (-1, 1):
        S.box((0.09, 0.22, 0.12), (sx * 0.255, 0.0, 0.43), "house", U, bevel=0)
    # cape: flat-topped drape down the back with a house panel, small pouch, rolled mat
    S.box((0.40, 0.06, 0.52), (0, -0.19, 0.20), "cape", U, bevel=0.02)
    S.box((0.44, 0.10, 0.07), (0, -0.17, 0.46), "scrim", U, bevel=0.02)
    S.box((0.26, 0.03, 0.22), (0, -0.235, 0.24), "house", U, bevel=0)
    S.box((0.16, 0.10, 0.13), (0.14, -0.22, -0.02), "pack", U, bevel=0.01)
    S.cyl(0.05, 0.36, (0, -0.26, 0.50), "rust", "X", U, verts=8)
    # head: wide-brim scrim helmet, house band, goggles, respirator
    S.box((0.10, 0.10, 0.07), (0, 0, 0.46), "dark", U)
    S.sphere((0.17, 0.18, 0.14), (0, 0, 0.56), "helm", U)
    S.cyl(0.25, 0.03, (0, 0.03, 0.535), "scrim", "Z", U, verts=12)                  # round brim
    S.box((0.26, 0.05, 0.07), (0, 0.0, 0.615), "house", U, bevel=0)                    # house band over the crown
    S.sphere((0.055, 0.19, 0.152), (0, 0, 0.56), "house", U)
    S.box((0.19, 0.12, 0.12), (0, 0.075, 0.47), "skin", U, bevel=0.03)
    S.box((0.085, 0.04, 0.06), (0.058, 0.15, 0.50), "lens", U, bevel=0)
    S.box((0.085, 0.04, 0.06), (-0.058, 0.15, 0.50), "lens", U, bevel=0)
    S.cyl(0.05, 0.10, (0, 0.17, 0.43), "dark", "Y", U, verts=10)
    S.cyl(0.035, 0.07, (0, 0.24, 0.425), "dark", "Y", U, verts=8)
    # long scoped rifle with bipod
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    S.box((0.05, 0.30, 0.10), (0, -0.30, -0.01), "wood", GM, rot=(-6, 0, 0))
    S.box((0.06, 0.28, 0.09), (0, 0, 0), "steel", GM)
    S.box((0.055, 0.32, 0.07), (0, 0.28, -0.005), "wood", GM)
    S.cyl(0.021, 0.36, (0, 0.62, 0.005), "steel", "Y", GM, verts=6)
    S.cyl(0.04, 0.34, (0, 0.04, 0.095), "dark", "Y", GM, verts=8)                      # scope tube
    S.cyl(0.055, 0.07, (0, 0.22, 0.095), "dark", "Y", GM, verts=8)                     # objective bell
    S.cyl(0.05, 0.04, (0, -0.14, 0.095), "lens", "Y", GM, verts=8)                     # eyepiece glint
    S.box((0.03, 0.03, 0.05), (0, 0.04, 0.05), "steel", GM, bevel=0)
    S.box((0.04, 0.07, 0.12), (0, 0.04, -0.10), "dark", GM, rot=(8, 0, 0))
    for sx in (-1, 1):                                                                   # folded bipod legs
        S.box((0.015, 0.015, 0.20), (sx * 0.03, 0.50, -0.09), "steel", GM, rot=(0, 0, sx * -8), bevel=0)
    if p["flash"]:
        S.cyl(0.07, 0.24, (0, 0.94, 0.005), "flash", "Y", GM, verts=8, r2=0.0)
        S.sphere((0.09, 0.09, 0.09), (0, 0.85, 0.005), "flash", GM, seg=8)
    for side, hand_spec in ((-1, p["hands"][0]), (1, p["hands"][1])):
        sh = R.Vector(up((side * SHOULDER[0], SHOULDER[1], SHOULDER[2])))
        if hand_spec == "gun":
            tgt = R.Vector(tuple(GM @ R.Vector(GRIP_R if side > 0 else GRIP_L)))
        elif hand_spec[0] == "w":
            tgt = R.Vector(hand_spec[1])
        elif hand_spec[0] == "u":
            tgt = R.Vector(up(hand_spec[1]))
        else:
            tgt = R.Vector(up(hand_spec))
        pole = U.to_3x3() @ R.Vector((side * 1.2, -0.3, -0.6))
        elbow, wrist = R.ik2(sh, tgt, L_UPPER, L_FORE, pole)
        S.limb(sh, elbow, 0.10, "hull")
        S.limb(sh, sh.lerp(elbow, 0.55), 0.135, "house", bevel=0.0)
        S.limb(elbow, wrist, 0.085, "hull2")
        S.box((0.08, 0.09, 0.08), tuple(wrist), "dark", None, bevel=0.01)
    for side, ft in ((-1, p["feet"][0]), (1, p["feet"][1])):
        hip = R.Vector((side * 0.115, pel[1], pel[2] - 0.04))
        ank = R.Vector(ft)
        knee, ank = R.ik2(hip, ank, L_THIGH, L_SHIN, R.Vector((0, 1, 0)))
        S.limb(hip, knee, 0.13, "pants")
        S.limb(knee, ank, 0.11, "pants")
        S.box((0.13, 0.27, 0.10), (ank.x, ank.y + 0.04, ank.z - 0.035), "dark", None, bevel=0.015)
