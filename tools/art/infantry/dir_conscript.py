"""Directorate Conscript: gaunt, stooped levy in a knee-length ash-brown greatcoat, pointed rubberised hood, bone-white
respirator plate with a dark filter dot, a bone-white-piped house-colour sash and armbands, and a cheap long bolt rifle.
Original design.  Same contract, scale, camera, states and facings as auth_rifleman.py; own poses (stiff march with the
rifle at port, deep stoop, rolled-over death fall with the coat spread).

Silhouette vs the Authority rifleman at 1x: no backpack, no round helmet; a narrow-shouldered tapering coat that stops at
the knee so thin legs and dark boots show, a pointed hood pushed forward of the shoulders, a bone face plate, a long thin rifle.
House colour: sash across the chest and a band on each upper arm (material "house", magenta, remapped in game).
Palette: Directorate-specific PAL_DIR (warm concrete, bone, oxide red, umber ramp).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C
import auth_rifleman as AR

ID = "dir_conscript"
TITLE = "Directorate Conscript"
CANVAS = AR.CANVAS
ORIGIN = AR.ORIGIN
STATES = AR.STATES
DIE_FACING = AR.DIE_FACING
PALETTE = C.PAL_DIR

MATERIALS = {
    "coat": ("#B08F68", 0.95, 0.0, None), "coat2": ("#8A6C4B", 0.95, 0.0, None), "coat3": ("#5E4833", 0.95, 0.0, None),
    "hood": ("#4B3B2D", 0.5, 0.0, None),
    "pants": ("#2E2824", 0.9, 0.0, None), "dark": ("#15130F", 0.85, 0.0, None), "steel": ("#2F3338", 0.4, 0.6, None),
    "wood": ("#9A6B3E", 0.7, 0.0, None), "bone": ("#EDE4CC", 0.8, 0.0, None),
    "red": ("#A8362A", 0.85, 0.0, None), "flash": ("#FFE79A", 0.3, 0.0, "#FFE08A"),
    "house": ("#E000E0", 0.75, 0.0, None),
}
EMIT_STRENGTH = {"flash": 8.0}

SHOULDER = (0.17, 0.02, 0.36)         # narrow, gaunt
L_UPPER, L_FORE = AR.L_UPPER, AR.L_FORE
L_THIGH, L_SHIN = AR.L_THIGH, AR.L_SHIN
GRIP_R, GRIP_L = (0, -0.10, -0.06), (0, 0.22, -0.045)
PORT = ((0.03, 0.17, 0.10), 24.0, 60.0)      # rifle at port: diagonal across the chest, muzzle up and to the left


def pose(state, step, n):
    base = dict(bob=0.0, lean=14.0, twist=0.0, dy=0.0, flash=False, gun_space="u", roll=0.0, spread=False,
                feet=((-0.11, 0.03, 0.09), (0.11, -0.03, 0.09)), gun=PORT, hands=("gun", "gun"))
    if state == "idle":
        base["bob"] = 0.03
        return base
    if state == "walk":
        # stiff march: straight-leg swing, almost no foot lift, upper body locked, rifle held at port
        ph = 2 * math.pi * step / n
        feet = []
        for off, xx in ((0.0, -0.11), (math.pi, 0.11)):
            p = ph + off
            feet.append((xx, 0.27 * math.cos(p), 0.09 + 0.07 * max(0.0, -math.sin(p))))
        base.update(feet=tuple(feet), bob=0.07 + 0.012 * abs(math.cos(ph)), lean=13.0, twist=3.0 * math.cos(ph),
                    gun=((PORT[0][0], PORT[0][1], PORT[0][2] + 0.008 * math.cos(2 * ph)), PORT[1], PORT[2]))
        return base
    if state == "fire":
        base.update(lean=12.0, twist=-12.0, bob=0.03, feet=((-0.14, 0.12, 0.09), (0.12, -0.20, 0.09)),
                    gun=((0.07, 0.30, 0.36), 12.0, 10.0))
        if step == 1:
            base.update(flash=True, lean=10.0, gun=((0.07, 0.26, 0.365), 12.0, 14.0))
        return base
    # die: forward fall as the rifleman (shared keys), plus a roll onto the side so the bone plate faces the camera,
    # and the coat spreads on the ground
    d = AR.pose(state, step, n)
    d.update(roll=[0.0, 0.0, -25.0, -55.0, -80.0, -90.0][step], spread=step >= 2)
    return d


def build(S, p):
    from mathutils import Matrix
    import rig as R
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"]) @ R.rz(p["roll"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    # ---------------- knee-length coat in three tones: umber upper, darker skirt, darkest hem band with red piping
    S.cyl(0.20, 0.32, (0, 0, 0.21), "coat", "Z", U, verts=8, r2=0.165)                # chest and shoulders (lit tone)
    S.cyl(0.245, 0.26, (0, 0, -0.08), "coat2", "Z", U, verts=8, r2=0.20)              # skirt (mid tone)
    S.cyl(0.255, 0.06, (0, 0, -0.20), "coat3", "Z", U, verts=8)                       # hem (dark tone)
    S.cyl(0.258, 0.022, (0, 0, -0.17), "red", "Z", U, verts=8)                        # red hem piping
    S.cyl(0.206, 0.04, (0, 0, 0.05), "dark", "Z", U, verts=8)                         # belt
    S.box((0.07, 0.04, 0.05), (0, 0.205, 0.05), "bone", U, bevel=0)                  # belt plate
    S.cyl(0.215, 0.07, (0, 0.0, 0.345), "hood", "Z", U, verts=8, r2=0.17)             # drooping shoulder cape
    S.box((0.17, 0.02, 0.07), (0, -0.215, 0.15), "bone", U, rot=(6, 0, 0), bevel=0)  # bone stencil bar on the back
    S.box((0.17, 0.02, 0.035), (0, -0.222, 0.06), "red", U, rot=(6, 0, 0), bevel=0)
    # ---------------- sash (house colour) across the chest, shoulder to opposite hip
    S.limb(up((-0.09, 0.19, 0.32)), up((0.13, 0.225, -0.10)), 0.10, "house", d=0.04, bevel=0)
    S.limb(up((-0.17, 0.02, 0.32)), up((-0.09, 0.19, 0.32)), 0.09, "house", d=0.04, bevel=0)
    # ---------------- head: far forward of the shoulders (stoop), pointed hood, bone respirator plate, dark filter dot
    S.box((0.08, 0.09, 0.07), (0, 0.07, 0.44), "dark", U)                             # neck
    S.sphere((0.13, 0.14, 0.135), (0, 0.10, 0.565), "hood", U)                          # hood
    S.box((0.10, 0.22, 0.09), (0, 0.04, 0.69), "hood", U, rot=(-40, 0, 0), bevel=0.02)  # pointed peak, swept back
    S.box((0.20, 0.09, 0.20), (0, 0.255, 0.49), "bone", U, bevel=0.03)              # bone respirator / face plate
    for sx in (-1, 1):
        S.box((0.06, 0.03, 0.04), (sx * 0.05, 0.30, 0.545), "dark", U, bevel=0)     # eye slits
    S.cyl(0.05, 0.06, (0, 0.31, 0.46), "dark", "Y", U, verts=10)                      # filter dot
    # ---------------- cheap bolt rifle: long thin barrel, plain wood stock, bolt knob, no magazine
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    S.box((0.05, 0.30, 0.09), (0, -0.26, -0.01), "wood", GM, rot=(-6, 0, 0))
    S.box((0.05, 0.22, 0.075), (0, 0.0, 0), "wood", GM)
    S.cyl(0.02, 0.42, (0, 0.32, 0.01), "steel", "Y", GM, verts=6)
    S.box((0.035, 0.05, 0.035), (0.045, -0.02, 0.035), "dark", GM)
    S.box((0.02, 0.04, 0.03), (0, 0.50, 0.045), "dark", GM)
    if p["flash"]:
        S.cyl(0.07, 0.24, (0, 0.76, 0.01), "flash", "Y", GM, verts=8, r2=0.0)
        S.sphere((0.09, 0.09, 0.09), (0, 0.64, 0.01), "flash", GM, seg=8)
    # ---------------- arms: coat sleeves, house armband on the upper arm, bone cuff
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
        S.limb(sh, elbow, 0.085, "coat2")
        S.limb(sh.lerp(elbow, 0.3), sh.lerp(elbow, 0.75), 0.12, "house", bevel=0.0)
        S.limb(elbow, wrist, 0.072, "coat3")
        S.limb(elbow.lerp(wrist, 0.72), wrist, 0.082, "bone", d=0.082, bevel=0)
        S.box((0.07, 0.08, 0.07), tuple(wrist), "dark", None, bevel=0.01)
    # ---------------- legs: thin dark trousers below the knee-length coat, tall boots
    for side, ft in ((-1, p["feet"][0]), (1, p["feet"][1])):
        hip = R.Vector((side * 0.095, pel[1], pel[2] - 0.04))
        ank = R.Vector(ft)
        knee, ank = R.ik2(hip, ank, L_THIGH, L_SHIN, R.Vector((0, 1, 0)))
        S.limb(hip, knee, 0.095, "pants")
        S.limb(knee, ank, 0.08, "pants")
        S.box((0.105, 0.25, 0.10), (ank.x, ank.y + 0.035, ank.z - 0.035), "dark", None, bevel=0.015)
    # ---------------- fallen body: the coat lies spread on the ground around the pelvis
    if p["spread"]:
        S.cyl(0.40, 0.03, (0.0, p["dy"] + 0.12, 0.03), "coat2", "Z", None, verts=8)
        S.cyl(0.28, 0.035, (0.05, p["dy"] + 0.12, 0.05), "coat", "Z", None, verts=8)
        S.box((0.5, 0.06, 0.03), (0.0, p["dy"] + 0.52, 0.03), "coat3", None, rot=(0, 0, 8))
