"""Directorate Conscript: gaunt, hunched levy in a long rubberised greatcoat, ash-cape and respirator hood, with a
bone-white (house-colour) sash and armband and a cheap long bolt rifle.  Original design.  Same contract, scale, camera,
states and facings as auth_rifleman.py (see its docstring); only the model, materials and palette differ.

Silhouette goals against the Authority rifleman at 1x: NO backpack and no round helmet; instead a tall pointed hood,
a long tapering coat column down to the shins (legs show only below the hem), narrow shoulders with a drooping cape,
head pushed forward (hunch), and a long thin rifle.  House colour: diagonal sash across the chest, band on each upper arm
(material "house", rendered magenta, remapped in game).  Palette: Directorate-specific PAL_DIR (red / bone / concrete).
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
    "coat": ("#A2977F", 0.95, 0.0, None), "coat2": ("#7A705F", 0.95, 0.0, None), "cape": ("#4A423B", 0.45, 0.0, None),
    "pants": ("#3A352F", 0.9, 0.0, None), "dark": ("#15130F", 0.85, 0.0, None), "steel": ("#2F3338", 0.4, 0.6, None),
    "wood": ("#9A6B3E", 0.7, 0.0, None), "bone": ("#E6DCC4", 0.8, 0.0, None), "mask": ("#E0D6BC", 0.6, 0.0, None),
    "red": ("#A8362A", 0.85, 0.0, None), "rust": ("#6A4326", 0.95, 0.0, None),
    "lens": ("#17150F", 0.2, 0.0, None), "flash": ("#FFE79A", 0.3, 0.0, "#FFE08A"),
    "house": ("#E000E0", 0.75, 0.0, None),
}
EMIT_STRENGTH = {"flash": 8.0}

SHOULDER = (0.2, 0.0, 0.36)
L_UPPER, L_FORE = AR.L_UPPER, AR.L_FORE
L_THIGH, L_SHIN = AR.L_THIGH, AR.L_SHIN
GRIP_R, GRIP_L = (0, -0.10, -0.06), (0, 0.22, -0.045)


def pose(state, step, n):
    p = AR.pose(state, step, n)
    if state in ("idle", "walk", "fire"):
        p["lean"] = p["lean"] + 7.0          # hunched
        p["bob"] = p["bob"] - 0.015
        g = p["gun"]
        p["gun"] = ((g[0][0], g[0][1] - 0.02, g[0][2] - 0.02), g[1], g[2])
    return p


def build(S, p):
    from mathutils import Matrix
    import rig as R
    R.SCALE  # (module constant; same figure scale as the rifleman)
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    # ---------------- long greatcoat: octagonal cone from the shins to the shoulders, belt, placket, red collar tabs
    S.cyl(0.255, 0.74, (0, 0, 0.0), "coat", "Z", U, verts=8, r2=0.175)
    S.cyl(0.262, 0.07, (0, 0, -0.33), "red", "Z", U, verts=8)                       # red hem band
    S.cyl(0.215, 0.045, (0, 0, 0.0), "dark", "Z", U, verts=8, r2=0.215)             # belt
    S.box((0.07, 0.04, 0.05), (0, 0.215, 0.0), "bone", U, bevel=0)                  # belt plate
    
    # ---------------- drooping rubberised ash-cape: shoulder cape and a back flap
    S.cyl(0.245, 0.09, (0, -0.01, 0.355), "cape", "Z", U, verts=8, r2=0.18)
    S.box((0.30, 0.035, 0.52), (0, -0.2, 0.06), "cape", U, rot=(7, 0, 0), bevel=0.01)
    S.box((0.20, 0.02, 0.07), (0, -0.225, 0.17), "bone", U, rot=(7, 0, 0), bevel=0)    # bone stencil bar on the cape back
    S.box((0.20, 0.02, 0.035), (0, -0.225, 0.07), "red", U, rot=(7, 0, 0), bevel=0)
    # ---------------- sash (house colour) across the chest, shoulder to opposite hip
    S.limb(up((-0.12, 0.215, 0.32)), up((0.16, 0.235, -0.16)), 0.105, "house", d=0.04, bevel=0)
    S.limb(up((-0.19, 0.0, 0.30)), up((-0.12, 0.215, 0.32)), 0.10, "house", d=0.04, bevel=0)     # sash over the shoulder
    # ---------------- head: hood with a long peak, respirator, round lenses, hose to a chest canister
    S.box((0.09, 0.09, 0.07), (0, 0.04, 0.44), "dark", U)                           # neck (pushed forward by the hunch)
    S.sphere((0.135, 0.15, 0.15), (0, 0.06, 0.55), "cape", U)                       # hood
    S.box((0.11, 0.24, 0.10), (0, -0.04, 0.66), "cape", U, rot=(-38, 0, 0), bevel=0.02)   # hood peak, swept back
    S.box((0.15, 0.10, 0.13), (0, 0.15, 0.51), "mask", U, bevel=0.03)               # respirator face piece
    for sx in (-1, 1):
        S.cyl(0.05, 0.05, (sx * 0.055, 0.205, 0.545), "bone", "Y", U, verts=10)     # lens rims
        S.cyl(0.036, 0.06, (sx * 0.055, 0.212, 0.545), "lens", "Y", U, verts=10)    # lenses (dark, no glow)
    S.cyl(0.05, 0.11, (0, 0.225, 0.46), "steel", "Y", U, verts=10)                  # filter canister
    S.cyl(0.052, 0.03, (0, 0.285, 0.46), "bone", "Y", U, verts=10)
    S.limb(up((0, 0.20, 0.44)), up((0.05, 0.19, 0.20)), 0.03, "dark", d=0.03)         # hose to the chest canister
    S.cyl(0.045, 0.14, (0.05, 0.215, 0.16), "steel", "Z", U, verts=8)
    # ---------------- cheap bolt rifle: long thin barrel, plain wood stock, bolt knob, no magazine
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    S.box((0.05, 0.30, 0.09), (0, -0.26, -0.01), "wood", GM, rot=(-6, 0, 0))
    S.box((0.05, 0.22, 0.075), (0, 0.0, 0), "wood", GM)
    S.cyl(0.02, 0.42, (0, 0.32, 0.01), "steel", "Y", GM, verts=6)
    S.box((0.035, 0.05, 0.035), (0.045, -0.02, 0.035), "dark", GM)                  # bolt knob
    S.box((0.02, 0.04, 0.03), (0, 0.50, 0.045), "dark", GM)                         # front sight
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
        S.limb(sh, elbow, 0.095, "coat")
        S.limb(elbow.lerp(wrist, 0.7), wrist, 0.092, "bone", d=0.092, bevel=0)      # bone cuff
        S.limb(sh.lerp(elbow, 0.3), sh.lerp(elbow, 0.75), 0.13, "house", bevel=0.0)
        S.limb(elbow, wrist, 0.08, "coat2")
        S.box((0.075, 0.08, 0.075), tuple(wrist), "dark", None, bevel=0.01)
    # ---------------- legs: thin trousers, tall boots (mostly hidden by the coat)
    for side, ft in ((-1, p["feet"][0]), (1, p["feet"][1])):
        hip = R.Vector((side * 0.10, pel[1], pel[2] - 0.04))
        ank = R.Vector(ft)
        knee, ank = R.ik2(hip, ank, L_THIGH, L_SHIN, R.Vector((0, 1, 0)))
        S.limb(hip, knee, 0.10, "pants")
        S.limb(knee, ank, 0.085, "pants")
        S.box((0.11, 0.25, 0.10), (ank.x, ank.y + 0.035, ank.z - 0.035), "dark", None, bevel=0.015)
