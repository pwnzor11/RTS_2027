"""Authority Rocketeer (M2-13d): the approved Rifleman body (D-068/D-069) with a shoulder-fired rocket tube and a rocket satchel.

Same contract, canvas, origin, states and house patches as auth_rifleman.py.  Differences: olive tube with flared rear, sighting
frame and a visible warhead instead of the rifle; satchel with two rocket noses on the back; heavier gait; fire frame 1 = tube on
the shoulder aiming, frame 2 = launch with a front flash and a backblast puff behind the tube.

Rifleman docstring follows.

Contract used by tools/art/build_infantry.py:
  ID, TITLE, CANVAS (w, h px at 1x), ORIGIN (feet px), STATES [(name, facings, frames, ticks_per_frame)],
  MATERIALS {name: (hex, rough, metal, emit)}, pose(state, step, n) -> dict, build(S, pose) -> None.

Units: 1 unit = 1/3.4 cell = 18.8 px horizontally at 1x (same scale as the tank).  The figure is about 1.3 units
tall (about 19 px incl. helmet at the 40 deg camera), shoulders 0.5 wide.  Figure coords: x right, y forward (the
facing direction), z up, feet at z = 0.  Pelvis hangs at z = 0.62; the upper body is posed as one rigid part U
(lean / twist), legs and arms are solved with two-bone IK from foot and hand targets.

Look: sealed ashfall kit.  Helmet with visor slit, goggles and a respirator snout, grey-green coat and mantle,
backpack with bedroll and filter canister, 1960s battle rifle (wood stock).  House colour: one band on each upper arm,
a ridge stripe over the helmet, a panel on the back of the pack (all material "house", rendered magenta).
"""
import math

ID = "auth_rocketeer"
TITLE = "Authority Rocketeer"
CANVAS = (32, 32)
ORIGIN = (16, 27)
# name, facings, frames, ticks_per_frame
STATES = [("idle", 8, 1, 1), ("walk", 8, 6, 3), ("fire", 8, 2, 3), ("die", 1, 6, 3)]
DIE_FACING = 2        # the fall is drawn facing east (head to the right), whatever way the soldier stood

MATERIALS = {
    "hull": ("#8C9C6C", 0.8, 0.0, None), "hull2": ("#6E7F55", 0.85, 0.0, None), "cloth": ("#A9B684", 0.9, 0.0, None),
    "pants": ("#2E3626", 0.9, 0.0, None), "pack": ("#3A3A2C", 0.9, 0.0, None), "helm": ("#4F5C3A", 0.6, 0.0, None),
    "dark": ("#15170F", 0.85, 0.0, None), "steel": ("#2A2E33", 0.4, 0.6, None), "rust": ("#6A4326", 0.95, 0.0, None),
    "wood": ("#C98E4E", 0.7, 0.0, None), "skin": ("#E8C9A0", 0.6, 0.0, None),
    "tube": ("#6F7F3C", 0.7, 0.0, None), "warhead": ("#9A4A2A", 0.6, 0.1, None), "band": ("#D8D2A0", 0.7, 0.0, None),
    "puff": ("#8A7A66", 0.95, 0.0, "#6E6050"),          # D-072: grey-brown backblast dust, not white
    "lens": ("#FFF0A8", 0.3, 0.0, "#FFE9A0"), "flash": ("#FF8A18", 0.3, 0.0, "#FF8A18"),   # warm yellow-orange flame
    "hot": ("#FFD36A", 0.3, 0.0, "#FFD36A"),
    "house": ("#E000E0", 0.75, 0.0, None),
}
EMIT_STRENGTH = {"lens": 3.5, "flash": 6.5, "hot": 7.0, "puff": 0.6}

SHOULDER = (0.245, 0.0, 0.37)   # in upper-body coords (relative to the pelvis), x mirrored
L_UPPER, L_FORE = 0.25, 0.23
L_THIGH, L_SHIN = 0.30, 0.30
GRIP_R, GRIP_L = (0, -0.06, -0.12), (0, 0.22, -0.10)   # hand points in gun coords


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(len(a)))


def pose(state, step, n):
    base = dict(bob=0.0, lean=3.0, twist=0.0, dy=0.0, flash=False, gun_space="u",
                feet=((-0.13, 0.03, 0.09), (0.13, -0.03, 0.09)),
                gun=((0.24, 0.10, 0.47), -10.0, 14.0), hands=("gun", "gun"))
    if state == "idle":
        return base
    if state == "walk":
        ph = 2 * math.pi * step / n
        stride, lift = 0.32, 0.16
        feet = []
        for off in (0.0, math.pi):
            p = ph + off
            y = stride * math.cos(p)
            z = 0.09 + lift * max(0.0, -math.sin(p))
            feet.append((-0.13 if off == 0 else 0.13, y, z))
        fwd = -math.cos(ph)                  # left arm swings opposite the left leg
        hl = (-0.28, 0.30 * fwd, -0.03 + 0.10 * max(0.0, fwd))
        bob = 0.05 * -math.cos(2 * ph)
        base.update(feet=tuple(feet), bob=bob, lean=10.0, twist=6.0 * math.cos(ph),
                    gun=((0.24, 0.08 + 0.02 * math.sin(2 * ph), 0.46), -8.0, 12.0), hands=(hl, "gun"))
        return base
    if state == "fire":
        base.update(lean=7.0, twist=-14.0, feet=((-0.15, 0.12, 0.09), (0.13, -0.20, 0.09)), bob=-0.02)
        gun = ((0.25, 0.12, 0.52), 10.0, 2.0)       # tube resting on the shoulder, aiming
        if step == 1:                        # launch: front flash + backblast, a little kick
            gun = ((0.25, 0.08, 0.53), 10.0, 5.0)
            base.update(flash=True, lean=4.0)
        base.update(gun=gun)
        return base
    # die: forward fall, pivoting about the pelvis, facing east in the sheet
    K = [  # pz, dy, lean, feet, hands(world or upper), gun
        (0.58, 0.04, 12, ((-0.10, 0.05, 0.09), (0.10, -0.10, 0.14)), (("u", (-0.30, 0.20, 0.50)), ("u", (0.30, 0.25, 0.45))),
         ("u", ((0.30, 0.55, 0.35), 0.0, 60.0))),
        (0.50, 0.10, 32, ((-0.10, 0.08, 0.09), (0.10, -0.25, 0.09)), (("u", (-0.34, 0.30, 0.25)), ("u", (0.34, 0.30, 0.25))),
         ("w", ((0.34, 0.65, 0.45), 10.0, -25.0))),
        (0.38, 0.18, 58, ((-0.09, -0.15, 0.09), (0.09, -0.32, 0.09)), (("w", (-0.24, 0.55, 0.15)), ("w", (0.24, 0.55, 0.15))),
         ("w", ((0.34, 0.55, 0.10), 10.0, -5.0))),
        (0.27, 0.24, 78, ((-0.09, -0.32, 0.08), (0.09, -0.42, 0.08)), (("w", (-0.24, 0.62, 0.10)), ("w", (0.24, 0.62, 0.10))),
         ("w", ((0.32, 0.40, 0.045), 10.0, 0.0))),
        (0.19, 0.28, 90, ((-0.09, -0.34, 0.07), (0.09, -0.40, 0.07)), (("w", (-0.22, 0.72, 0.08)), ("w", (0.26, 0.68, 0.08))),
         ("w", ((0.34, 0.38, 0.045), 10.0, 0.0))),
        (0.17, 0.28, 90, ((-0.11, -0.40, 0.07), (0.10, -0.34, 0.07)), (("w", (-0.28, 0.70, 0.07)), ("w", (0.30, 0.64, 0.07))),
         ("w", ((0.36, 0.36, 0.045), 12.0, 0.0))),
    ]
    pz, dy, lean, feet, hands, gun = K[step]
    base.update(bob=pz - 0.62, dy=dy, lean=lean, feet=feet, hands=hands, gun_space=gun[0], gun=gun[1])
    return base


def build(S, p):
    from mathutils import Matrix      # Blender-side imports stay lazy so the manifest data loads without bpy
    import rig as R
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    # ---------------- torso, coat, mantle, belt
    S.box((0.40, 0.26, 0.24), (0, 0, -0.02), "hull", U, bevel=0.03)                 # coat skirt
    S.box((0.36, 0.24, 0.40), (0, 0, 0.22), "cloth", U, bevel=0.03)                   # torso
    S.box((0.42, 0.27, 0.05), (0, 0, 0.05), "rust", U)                               # belt
    S.box((0.12, 0.05, 0.13), (0.1, 0.14, 0.2), "rust", U)                           # chest pouches
    S.box((0.12, 0.05, 0.13), (-0.1, 0.14, 0.2), "rust", U)
    S.box((0.42, 0.28, 0.06), (0, -0.02, 0.43), "cloth", U, bevel=0.03)             # ash mantle over the shoulders
        # ---------------- backpack: bedroll, filter canister, house panel on the back
    S.box((0.30, 0.17, 0.36), (0, -0.26, 0.24), "pack", U, bevel=0.02)
    S.box((0.24, 0.025, 0.20), (0, -0.355, 0.27), "house", U, bevel=0)
    S.box((0.28, 0.12, 0.17), (0, -0.37, 0.06), "hull2", U, bevel=0.02)                 # rocket satchel
    S.box((0.29, 0.13, 0.03), (0, -0.37, 0.12), "rust", U)
    for sx in (-0.07, 0.07):                                                              # two rocket noses sticking up
        S.cyl(0.05, 0.30, (sx, -0.33, 0.50), "tube", "Z", U, verts=8)
        S.cyl(0.0, 0.12, (sx, -0.33, 0.71), "warhead", "Z", U, verts=8, r2=0.05)
    S.cyl(0.07, 0.30, (-0.21, -0.25, 0.16), "steel", "Z", U, verts=10)
    S.cyl(0.075, 0.04, (-0.21, -0.25, 0.30), "dark", "Z", U, verts=10)
    # ---------------- head: helmet + ridge stripe, visor, goggles, respirator
    hc = (0, 0.0, 0.54)
    S.box((0.10, 0.10, 0.07), (0, 0, 0.46), "dark", U)                               # neck
    S.sphere((0.17, 0.18, 0.14), (0, 0, 0.56), "helm", U)                                     # helmet
    S.sphere((0.055, 0.19, 0.152), (0, 0, 0.56), "house", U)                                   # ridge stripe over the crown
    S.box((0.19, 0.12, 0.12), (0, 0.075, 0.47), "skin", U, bevel=0.03)               # sealed face hood
    S.box((0.30, 0.12, 0.035), (0, 0.10, 0.545), "dark", U, bevel=0.01)               # helmet peak
    S.box((0.085, 0.04, 0.06), (0.058, 0.15, 0.50), "lens", U, bevel=0)             # goggles
    S.box((0.085, 0.04, 0.06), (-0.058, 0.15, 0.50), "lens", U, bevel=0)
    S.cyl(0.05, 0.10, (0, 0.17, 0.43), "dark", "Y", U, verts=10)                    # respirator snout
    S.cyl(0.035, 0.07, (0, 0.24, 0.425), "dark", "Y", U, verts=8)
    # ---------------- rocket tube (gun coords: y forward along the tube)
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    S.cyl(0.075, 0.85, (0, 0.05, 0), "tube", "Y", GM, verts=10)                      # tube
    S.cyl(0.075, 0.16, (0, -0.44, 0), "hull2", "Y", GM, verts=10, r2=0.125)          # flared rear (r2 end points back)
    S.cyl(0.082, 0.04, (0, 0.20, 0), "band", "Y", GM, verts=10)                      # pale marking bands
    S.cyl(0.082, 0.04, (0, -0.22, 0), "band", "Y", GM, verts=10)
    S.cyl(0.10, 0.12, (0, 0.50, 0), "dark", "Y", GM, verts=10)                       # front collar
    S.cyl(0.0, 0.26, (0, 0.62, 0), "warhead", "Y", GM, verts=10, r2=0.095)            # warhead nose, tip forward
    S.box((0.03, 0.03, 0.12), (0, -0.10, 0.12), "steel", GM)                         # sighting frame: rear post
    S.box((0.03, 0.03, 0.12), (0, 0.28, 0.12), "steel", GM)                          # front post
    S.box((0.03, 0.40, 0.025), (0, 0.09, 0.19), "steel", GM)                         # frame rail
    S.box((0.05, 0.05, 0.12), (0, -0.04, -0.12), "dark", GM)                         # trigger grip
    S.box((0.05, 0.05, 0.10), (0, 0.22, -0.11), "dark", GM)                          # front grip
    if p["flash"]:      # D-072: smaller, warm flame; a 1-2 px hot core; a short grey-brown backblast puff
        S.cyl(0.0, 0.42, (0, 0.98, 0), "flash", "Y", GM, verts=8, r2=0.17)           # launch flame ahead of the nose
        S.sphere((0.10, 0.10, 0.10), (0, 0.86, 0), "flash", GM, seg=8)
        S.sphere((0.05, 0.05, 0.05), (0, 0.84, 0), "hot", GM, seg=8)              # hot core (small)
        S.sphere((0.13, 0.20, 0.13), (0, -0.64, 0), "puff", GM, seg=8)               # backblast puff behind the tube
        S.sphere((0.09, 0.11, 0.09), (0, -0.82, 0.02), "puff", GM, seg=8)

    # ---------------- arms: shoulder -> elbow -> wrist; house band on the upper arm
    for side, hand_spec in ((-1, p["hands"][0]), (1, p["hands"][1])):
        sh = R.Vector(up((side * SHOULDER[0], SHOULDER[1], SHOULDER[2])))
        if hand_spec == "gun":
            tgt = R.Vector(tuple(GM @ R.Vector(GRIP_R if side > 0 else GRIP_L)))
        elif hand_spec[0] == "w":
            tgt = R.Vector(hand_spec[1])
        elif hand_spec[0] == "u":
            tgt = R.Vector(up(hand_spec[1]))
        else:                                     # bare point in upper-body coords
            tgt = R.Vector(up(hand_spec))
        pole = U.to_3x3() @ R.Vector((side * 1.2, -0.3, -0.6))
        elbow, wrist = R.ik2(sh, tgt, L_UPPER, L_FORE, pole)
        S.limb(sh, elbow, 0.10, "hull")
        S.limb(sh, sh.lerp(elbow, 0.55), 0.135, "house", bevel=0.0)
        S.limb(elbow, wrist, 0.085, "hull2")
        S.box((0.08, 0.09, 0.08), tuple(wrist), "dark", None, bevel=0.01)
    # ---------------- legs: hip -> knee -> ankle, boots
    for side, ft in ((-1, p["feet"][0]), (1, p["feet"][1])):
        hip = R.Vector((side * 0.115, pel[1], pel[2] - 0.04))
        ank = R.Vector(ft)
        knee, ank = R.ik2(hip, ank, L_THIGH, L_SHIN, R.Vector((0, 1, 0)))
        S.limb(hip, knee, 0.13, "pants")
        S.limb(knee, ank, 0.11, "pants")
        S.box((0.13, 0.27, 0.10), (ank.x, ank.y + 0.04, ank.z - 0.035), "dark", None, bevel=0.015)
