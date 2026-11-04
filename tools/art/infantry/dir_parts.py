"""Shared Blender-side limb builders for the Directorate infantry modules (arms and legs by two-bone IK).
Only used inside the Blender render process.  Same joint maths as dir_conscript.py, parametrised for the other soldiers."""
import rig as R
import auth_rifleman as AR

L_UPPER, L_FORE, L_THIGH, L_SHIN = AR.L_UPPER, AR.L_FORE, AR.L_THIGH, AR.L_SHIN


def arms(S, p, U, up, GM, shoulder, grips, mats, thick=(0.085, 0.072), band=0.12, band_mat="house", cuff_mat="bone",
         hand_mat="dark", hand_size=0.07, cuff_len=0.28):
    """grips = (grip_right, grip_left) in gun coords (None = no weapon hand); mats = (upper sleeve, forearm)."""
    for side, hand_spec in ((-1, p["hands"][0]), (1, p["hands"][1])):
        sh = R.Vector(up((side * shoulder[0], shoulder[1], shoulder[2])))
        if hand_spec == "gun":
            tgt = R.Vector(tuple(GM @ R.Vector(grips[0] if side > 0 else grips[1])))
        elif hand_spec[0] == "w":
            tgt = R.Vector(hand_spec[1])
        elif hand_spec[0] == "u":
            tgt = R.Vector(up(hand_spec[1]))
        else:
            tgt = R.Vector(up(hand_spec))
        pole = U.to_3x3() @ R.Vector((side * 1.2, -0.3, -0.6))
        elbow, wrist = R.ik2(sh, tgt, L_UPPER, L_FORE, pole)
        S.limb(sh, elbow, thick[0], mats[0])
        if band:
            S.limb(sh.lerp(elbow, 0.3), sh.lerp(elbow, 0.75), band, band_mat, bevel=0.0)
        S.limb(elbow, wrist, thick[1], mats[1])
        if cuff_mat:
            S.limb(elbow.lerp(wrist, 1.0 - cuff_len), wrist, thick[1] + 0.01, cuff_mat, d=thick[1] + 0.01, bevel=0)
        S.box((hand_size, hand_size + 0.01, hand_size), tuple(wrist), hand_mat, None, bevel=0.01)


def legs(S, p, pel, spread=0.095, thick=(0.095, 0.08), mat="pants", boot="dark"):
    for side, ft in ((-1, p["feet"][0]), (1, p["feet"][1])):
        hip = R.Vector((side * spread, pel[1], pel[2] - 0.04))
        ank = R.Vector(ft)
        knee, ank = R.ik2(hip, ank, L_THIGH, L_SHIN, R.Vector((0, 1, 0)))
        S.limb(hip, knee, thick[0], mat)
        S.limb(knee, ank, thick[1], mat)
        S.box((thick[1] + 0.025, 0.25, 0.10), (ank.x, ank.y + 0.035, ank.z - 0.035), boot, None, bevel=0.015)
