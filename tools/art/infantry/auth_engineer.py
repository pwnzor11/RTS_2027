"""Authority Field Engineer: the rifleman's sealed ashfall kit with the soldier's gear swapped for a tradesman's: a pale hard hat with a
house-colour band, goggles pushed up on the brim, a big tool satchel and a rolled survey tripod on the back, a hip bag, and a long
adjustable wrench carried in both hands instead of a rifle.  No firearm (the archive: "No weapon issued").  Original design.
Same contract, scale, camera, states and facings as auth_rifleman.py.  The "fire" state is the work gesture: wrench raised, then
brought down with a spark (the repair / capture action).
Silhouette vs the rifleman at 1x: pale round hard hat with a brim, boxy satchel plus tripod legs sticking up behind, a wrench head
with open jaws instead of a rifle barrel.
House colour: band on each upper arm, hard-hat band, panel on the satchel flap, shoulder pads.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import auth_rifleman as AR

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48
ID = "auth_engineer"
TITLE = "Authority Field Engineer"
CANVAS, ORIGIN, STATES, DIE_FACING = AR.CANVAS, AR.ORIGIN, AR.STATES, AR.DIE_FACING
MATERIALS = dict(AR.MATERIALS)
MATERIALS.update({"hat": ("#D8D2A0", 0.55, 0.0, None), "spark": ("#FFE79A", 0.3, 0.0, "#FFE08A"), "chrome": ("#A7AEB3", 0.5, 0.15, None)})
EMIT_STRENGTH = {"lens": 3.5, "flash": 8.0, "spark": 8.0}
SHOULDER = AR.SHOULDER
L_UPPER, L_FORE, L_THIGH, L_SHIN = AR.L_UPPER, AR.L_FORE, AR.L_THIGH, AR.L_SHIN
GRIP_R, GRIP_L = (0, -0.16, 0.0), (0, 0.06, 0.0)       # hand points in wrench coords


def pose(state, step, n):
    p = AR.pose(state, step, n)
    if state == "idle":
        p.update(gun=((0.10, 0.30, 0.22), -18.0, 38.0))
    elif state == "walk":
        p.update(gun=((p["gun"][0][0] - 0.12, p["gun"][0][1] + 0.02, p["gun"][0][2] + 0.10), -10.0, 40.0))
    elif state == "fire":
        if step == 0:        # wrench cocked high over the right shoulder
            p.update(lean=2.0, twist=-18.0, gun=((0.14, 0.05, 0.62), -10.0, 84.0), flash=False)
        else:                # strike down and forward, spark at the jaws
            p.update(lean=10.0, twist=-8.0, gun=((0.12, 0.34, 0.16), -4.0, 12.0), flash=True)
    return p


def build(S, p):
    from mathutils import Matrix
    import rig as R
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    # torso, coat, mantle, belt (as the rifleman)
    S.box((0.40, 0.26, 0.24), (0, 0, -0.02), "hull", U, bevel=0.03)
    S.box((0.36, 0.24, 0.40), (0, 0, 0.22), "cloth", U, bevel=0.03)
    S.box((0.42, 0.27, 0.05), (0, 0, 0.05), "rust", U)
    S.box((0.42, 0.28, 0.06), (0, -0.02, 0.43), "cloth", U, bevel=0.03)
    S.box((0.34, 0.05, 0.09), (0, 0.145, 0.06), "dark", U)                             # tool apron along the belt
    for x in (-0.11, 0.0, 0.11):
        S.box((0.035, 0.04, 0.12), (x, 0.17, 0.13), "steel", U, bevel=0)               # tool handles in the apron pockets
    for sx in (-1, 1):
        S.box((0.09, 0.22, 0.12), (sx * 0.255, 0.0, 0.43), "house", U, bevel=0)
    # satchel on the back (house flap), rolled tripod legs above it, filter canister kept from the standard kit
    S.box((0.34, 0.19, 0.30), (0, -0.27, 0.16), "pack", U, bevel=0.02)
    S.box((0.30, 0.03, 0.24), (0, -0.375, 0.20), "house", U, bevel=0)
    S.box((0.36, 0.20, 0.03), (0, -0.27, 0.32), "rust", U)
    for sx, lean in ((-0.07, -4), (0.0, 0), (0.07, 4)):                                # tripod legs poking above the satchel
        S.cyl(0.026, 0.52, (sx, -0.27, 0.56), "steel", "Z", U, verts=6)
    S.box((0.20, 0.07, 0.07), (0, -0.27, 0.36), "hull2", U, bevel=0.01)                # strap holding them
    S.cyl(0.07, 0.28, (-0.21, -0.22, 0.14), "steel", "Z", U, verts=10)
    # head: hard hat with house band, goggles pushed up, respirator
    S.box((0.10, 0.10, 0.07), (0, 0, 0.46), "dark", U)
    S.sphere((0.175, 0.185, 0.14), (0, 0, 0.56), "hat", U)                              # hard hat shell
    S.box((0.18, 0.20, 0.03), (0, 0.0, 0.69), "hat", U, bevel=0)                      # centre ridge
    S.box((0.27, 0.05, 0.075), (0, 0.0, 0.595), "house", U, bevel=0)                  # house band across the hat
    S.box((0.34, 0.10, 0.03), (0, 0.14, 0.535), "hat", U, bevel=0.01)                  # front brim
    S.box((0.19, 0.12, 0.12), (0, 0.075, 0.47), "skin", U, bevel=0.03)
    S.box((0.085, 0.04, 0.06), (0.058, 0.17, 0.55), "lens", U, bevel=0)               # goggles pushed up onto the brim
    S.box((0.085, 0.04, 0.06), (-0.058, 0.17, 0.55), "lens", U, bevel=0)
    S.box((0.19, 0.03, 0.03), (0, 0.15, 0.50), "dark", U, bevel=0)                       # goggle strap line
    S.cyl(0.05, 0.10, (0, 0.17, 0.43), "dark", "Y", U, verts=10)
    S.cyl(0.035, 0.07, (0, 0.24, 0.425), "dark", "Y", U, verts=8)
    # wrench (gun coords: y along the shaft)
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    S.box((0.05, 0.62, 0.035), (0, -0.02, 0), "chrome", GM, bevel=0.008)                 # shaft
    S.box((0.054, 0.20, 0.04), (0, -0.20, 0), "rust", GM, bevel=0)                      # taped grip
    S.box((0.13, 0.10, 0.04), (0, 0.33, 0), "chrome", GM, bevel=0)                       # jaw block
    S.box((0.04, 0.12, 0.045), (-0.05, 0.42, 0), "chrome", GM, bevel=0)                  # open jaw prongs
    S.box((0.04, 0.12, 0.045), (0.05, 0.42, 0), "chrome", GM, bevel=0)
    S.cyl(0.035, 0.05, (0.0, 0.31, 0.0), "dark", "Z", GM, verts=8)                      # adjusting screw
    if p["flash"]:
        S.sphere((0.10, 0.10, 0.10), (0, 0.50, 0), "spark", GM, seg=8)
        S.cyl(0.0, 0.20, (0, 0.62, 0.0), "spark", "Y", GM, verts=6, r2=0.05)
        S.box((0.20, 0.03, 0.03), (0, 0.50, 0), "spark", GM, rot=(0, 0, 40), bevel=0)
    # arms
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
        S.box((0.085, 0.09, 0.085), tuple(wrist), "rust", None, bevel=0.01)              # work gloves
    for side, ft in ((-1, p["feet"][0]), (1, p["feet"][1])):
        hip = R.Vector((side * 0.115, pel[1], pel[2] - 0.04))
        ank = R.Vector(ft)
        knee, ank = R.ik2(hip, ank, L_THIGH, L_SHIN, R.Vector((0, 1, 0)))
        S.limb(hip, knee, 0.13, "pants")
        S.limb(knee, ank, 0.11, "pants")
        S.box((0.13, 0.27, 0.10), (ank.x, ank.y + 0.04, ank.z - 0.035), "dark", None, bevel=0.015)
