"""Directorate Commissar: the faction's officer.  Tall, upright and imposing: a high peaked cap with a bone crown and red band,
a long dark greatcoat with pale red-brown trim (hem, cuffs, collar, front seam) and a dark leather cross-belt, a megaphone slung at the hip,
and in fire a raised pistol arm.  Original design.  Same contract, scale, camera, states and facings as dir_conscript.py;
own poses (straight back, slow stride, one fist at the belt, the pistol arm raised and extended in fire, forward fall in die).
Revised: dark umber greatcoat with pale red-brown trim, wide flat dark peaked cap, bone megaphone.
House colour (D-184): a wide sash across the chest, shoulder boards, a back panel, cap piping and badge."""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C
import auth_rifleman as AR

SCALE = 1.25                  # D-184 pilot: figure 1.25 x, frame 48 x 48, house boards / back panel / cap piping
ID = "dir_commissar"
TITLE = "Directorate Commissar"
CANVAS, ORIGIN, STATES, DIE_FACING = AR.CANVAS, AR.ORIGIN, AR.STATES, AR.DIE_FACING
PALETTE = C.PAL_DIR
MATERIALS = {
    "coat": ("#4A3829", 0.85, 0.0, None), "coat2": ("#33281F", 0.85, 0.0, None), "coat3": ("#1F1B19", 0.85, 0.0, None),
    "trim": ("#B8573F", 0.8, 0.0, None),
    "red": ("#A8362A", 0.8, 0.0, None), "cap": ("#2E2926", 0.6, 0.0, None), "pants": ("#2E2824", 0.9, 0.0, None),
    "dark": ("#15130F", 0.85, 0.0, None), "leather": ("#2A211B", 0.5, 0.0, None), "steel": ("#2F3338", 0.4, 0.6, None),
    "bone": ("#FFF8E4", 0.8, 0.0, None), "face": ("#C9BFA6", 0.7, 0.0, None), "brass": ("#C9A640", 0.4, 0.5, None),
    "flash": ("#FFE79A", 0.3, 0.0, "#FFE08A"), "house": ("#E000E0", 0.75, 0.0, None),
}
EMIT_STRENGTH = {"flash": 8.0}
SHOULDER = (0.19, 0.0, 0.38)
Z0 = 0.70                      # taller pelvis than the conscript (0.62): longer legs


def pose(state, step, n):
    base = dict(bob=0.0, lean=0.0, twist=0.0, dy=0.0, flash=False, gun_space="u", roll=0.0, spread=False, pistol=False,
                feet=((-0.10, 0.03, 0.09), (0.10, -0.03, 0.09)), gun=((0.14, 0.34, 0.42), -6.0, 0.0),
                hands=(("u", (-0.22, 0.05, -0.10)), ("u", (0.12, 0.17, 0.0))))
    if state == "idle":
        base["bob"] = 0.01
        return base
    if state == "walk":
        ph = 2 * math.pi * step / n
        feet = []
        for off, xx in ((0.0, -0.10), (math.pi, 0.10)):
            p = ph + off
            feet.append((xx, 0.30 * math.cos(p), 0.09 + 0.10 * max(0.0, -math.sin(p))))
        sw = -math.cos(ph)
        base.update(feet=tuple(feet), bob=0.02 * -math.cos(2 * ph), twist=4.0 * math.cos(ph),
                    hands=(("u", (-0.22, 0.20 * sw, -0.09)), ("u", (0.12, 0.17, 0.0))))
        return base
    if state == "fire":
        base.update(twist=-14.0, pistol=True, feet=((-0.13, 0.12, 0.09), (0.11, -0.18, 0.09)),
                    hands=(("u", (-0.20, 0.06, -0.06)), "gun"))
        if step == 1:
            base.update(flash=True, gun=((0.14, 0.31, 0.43), -6.0, 5.0))
        return base
    d = AR.pose(state, step, n)
    d.update(roll=[0.0, 0.0, -25.0, -55.0, -80.0, -90.0][step], spread=step >= 2, pistol=False)
    return d


def build(S, p):
    from mathutils import Matrix
    import rig as R
    import dir_parts as P
    z = Z0 + p["bob"]
    U = R.tr(0, p["dy"], z) @ R.rz(p["twist"]) @ R.rx(-p["lean"]) @ R.rz(p["roll"])
    pel = (0, p["dy"], z)

    def up(pt):
        return tuple(U @ R.Vector(pt))

    # greatcoat to mid-calf: lit chest, mid skirt, shaded hem band, red hem piping, red front seam, collar
    S.cyl(0.20, 0.38, (0, 0, 0.22), "coat", "Z", U, verts=8, r2=0.17)
    S.cyl(0.255, 0.40, (0, 0, -0.17), "coat2", "Z", U, verts=8, r2=0.205)
    S.cyl(0.265, 0.05, (0, 0, -0.39), "coat3", "Z", U, verts=8)
    S.cyl(0.268, 0.028, (0, 0, -0.35), "trim", "Z", U, verts=8)
    S.box((0.045, 0.03, 0.62), (0, 0.215, -0.02), "trim", U, bevel=0)                      # red front trim
    S.cyl(0.215, 0.06, (0, 0.0, 0.40), "coat2", "Z", U, verts=8, r2=0.18)                  # shoulders
    S.cyl(0.19, 0.07, (0, 0.0, 0.45), "trim", "Z", U, verts=8, r2=0.14)                     # red standing collar
    # leather cross-belt and house sash at the waist
    S.cyl(0.212, 0.06, (0, 0, 0.03), "leather", "Z", U, verts=8)
    S.limb(up((0.12, 0.18, 0.40)), up((-0.14, 0.215, 0.04)), 0.05, "leather", d=0.035, bevel=0)
    S.limb(up((-0.12, 0.17, 0.41)), up((0.17, 0.225, 0.00)), 0.12, "house", d=0.05, bevel=0)      # house sash across the chest
    S.limb(up((-0.19, 0.0, 0.41)), up((-0.12, 0.17, 0.41)), 0.11, "house", d=0.05, bevel=0)
    S.box((0.05, 0.035, 0.05), (0.0, 0.225, 0.05), "brass", U, bevel=0)
    # shoulder boards (house) and back stencil
    for sx in (-1, 1):
        S.box((0.14, 0.18, 0.045), (sx * 0.20, 0.0, 0.455), "house", U, rot=(0, sx * -8, 0), bevel=0)   # D-184 house shoulder boards
    S.box((0.20, 0.025, 0.16), (0, -0.218, 0.22), "house", U, bevel=0)                    # D-184 house panel on the back of the coat
    # megaphone slung at the left hip: bone cone, dark mouthpiece, strap
    S.cyl(0.085, 0.30, (-0.25, 0.10, -0.05), "bone", "Y", U, verts=8, r2=0.035)
    S.cyl(0.095, 0.03, (-0.25, 0.25, -0.05), "trim", "Y", U, verts=8)
    S.cyl(0.04, 0.05, (-0.25, -0.06, -0.05), "dark", "Y", U, verts=8)
    S.limb(up((-0.11, 0.18, 0.40)), up((-0.25, 0.02, 0.0)), 0.025, "leather", d=0.02, bevel=0)
    # head: tall, pale face, dark peaked cap: red-brown band, wide flat crown, dark visor, 1 px house dot badge
    S.box((0.08, 0.08, 0.07), (0, 0.01, 0.52), "dark", U)
    S.box((0.15, 0.13, 0.14), (0, 0.03, 0.63), "face", U, bevel=0.02)
    S.box((0.11, 0.03, 0.025), (0, 0.10, 0.65), "dark", U, bevel=0)
    S.cyl(0.12, 0.05, (0, 0.03, 0.725), "trim", "Z", U, verts=10)                           # cap band
    S.cyl(0.125, 0.09, (0, 0.03, 0.795), "cap", "Z", U, verts=10, r2=0.195)                 # wide flaring crown
    S.cyl(0.195, 0.02, (0, 0.03, 0.845), "cap", "Z", U, verts=10)                           # flat top
    S.box((0.17, 0.12, 0.022), (0, 0.15, 0.745), "dark", U, rot=(-12, 0, 0), bevel=0)      # visor
    S.box((0.07, 0.03, 0.06), (0, 0.108, 0.795), "house", U, bevel=0)                       # house cap badge (D-184: larger)
    S.cyl(0.13, 0.025, (0, 0.03, 0.765), "house", "Z", U, verts=10)                         # D-184 thin house piping under the crown
    # pistol (fire only), raised in the right hand
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    if p["pistol"]:
        S.box((0.05, 0.17, 0.08), (0, 0.04, 0.0), "steel", GM)
        S.box((0.04, 0.05, 0.10), (0, -0.04, -0.07), "dark", GM, rot=(10, 0, 0))
        S.cyl(0.02, 0.10, (0, 0.17, 0.01), "steel", "Y", GM, verts=6)
        if p["flash"]:
            S.cyl(0.07, 0.24, (0, 0.33, 0.01), "flash", "Y", GM, verts=8, r2=0.0)
            S.sphere((0.09, 0.09, 0.09), (0, 0.22, 0.01), "flash", GM, seg=8)
    P.arms(S, p, U, up, GM, SHOULDER, ((0, -0.03, -0.04), (0, 0, -0.04)), ("coat2", "coat"), thick=(0.09, 0.078), band=0,
           cuff_mat="trim", hand_mat="leather", hand_size=0.07, cuff_len=0.22)
    P.legs(S, p, pel, spread=0.09, thick=(0.095, 0.085), boot="leather")
    if p["spread"]:
        S.cyl(0.42, 0.03, (0.0, p["dy"] + 0.12, 0.03), "coat2", "Z", None, verts=8)
        S.cyl(0.30, 0.035, (0.05, p["dy"] + 0.12, 0.05), "coat", "Z", None, verts=8)
        S.cyl(0.20, 0.04, (0.05, p["dy"] + 0.62, 0.04), "cap", "Z", None, verts=10)
