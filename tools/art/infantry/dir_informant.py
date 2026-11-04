"""Directorate Informant: civilian-looking, upright and slim.  Long dark trench coat to mid-shin, wide-brimmed hat, raised collar,
pale narrow face, no visible rifle (a small pistol appears only in the fire frames).  Original design.
Same contract, scale, camera, states and facings as dir_conscript.py; own poses (no stoop, relaxed arms swinging in the walk,
one arm out with the pistol in fire, forward fall in die).
Silhouette vs the conscript at 1x: straight vertical column with a flat round hat brim instead of a pointed hood, no rifle, no sash.
House colour is deliberately small: a lapel badge and the hat band only (material "house").
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C
import auth_rifleman as AR

ID = "dir_informant"
TITLE = "Directorate Informant"
CANVAS, ORIGIN, STATES, DIE_FACING = AR.CANVAS, AR.ORIGIN, AR.STATES, AR.DIE_FACING
PALETTE = C.PAL_DIR
MATERIALS = {
    "coat": ("#928A7E", 0.9, 0.0, None), "coat2": ("#6E675F", 0.9, 0.0, None), "coat3": ("#3A3531", 0.9, 0.0, None),
    "hat": ("#3A3531", 0.7, 0.0, None), "pants": ("#2E2824", 0.9, 0.0, None), "dark": ("#15130F", 0.85, 0.0, None),
    "steel": ("#2F3338", 0.4, 0.6, None), "bone": ("#EDE4CC", 0.8, 0.0, None), "face": ("#E3DAC2", 0.7, 0.0, None),
    "flash": ("#FFE79A", 0.3, 0.0, "#FFE08A"), "house": ("#E000E0", 0.75, 0.0, None),
}
EMIT_STRENGTH = {"flash": 8.0}
SHOULDER = (0.155, 0.0, 0.36)


def pose(state, step, n):
    base = dict(bob=0.0, lean=1.5, twist=0.0, dy=0.0, flash=False, gun_space="u", roll=0.0, spread=False, pistol=False,
                feet=((-0.09, 0.03, 0.09), (0.09, -0.03, 0.09)), gun=((0.12, 0.30, 0.38), -8.0, 0.0),
                hands=(("u", (-0.20, 0.03, -0.10)), ("u", (0.20, 0.03, -0.10))))
    if state == "idle":
        return base
    if state == "walk":
        ph = 2 * math.pi * step / n
        feet = []
        for off, xx in ((0.0, -0.09), (math.pi, 0.09)):
            p = ph + off
            feet.append((xx, 0.30 * math.cos(p), 0.09 + 0.12 * max(0.0, -math.sin(p))))
        sw = -math.cos(ph)
        base.update(feet=tuple(feet), bob=0.03 * -math.cos(2 * ph), lean=3.0, twist=5.0 * math.cos(ph),
                    hands=(("u", (-0.20, 0.22 * sw, -0.08 + 0.05 * max(0.0, sw))),
                           ("u", (0.20, -0.22 * sw, -0.08 + 0.05 * max(0.0, -sw)))))
        return base
    if state == "fire":
        base.update(lean=2.0, twist=-14.0, pistol=True, feet=((-0.12, 0.10, 0.09), (0.10, -0.16, 0.09)),
                    hands=(("u", (-0.12, 0.12, 0.12)), "gun"))
        if step == 1:
            base.update(flash=True, lean=3.0, gun=((0.12, 0.27, 0.385), -8.0, 4.0))
        return base
    d = AR.pose(state, step, n)
    d.update(roll=[0.0, 0.0, -25.0, -55.0, -80.0, -90.0][step], spread=step >= 2, pistol=False)
    return d


def build(S, p):
    from mathutils import Matrix
    import rig as R
    import dir_parts as P
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"]) @ R.rz(p["roll"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    # long trench coat: fitted chest, flared skirt to mid-shin, belt, double-breasted buttons, raised collar
    S.cyl(0.17, 0.34, (0, 0, 0.21), "coat", "Z", U, verts=8, r2=0.145)
    S.cyl(0.215, 0.40, (0, 0, -0.14), "coat2", "Z", U, verts=8, r2=0.17)
    S.cyl(0.222, 0.04, (0, 0, -0.35), "coat3", "Z", U, verts=8)
    S.cyl(0.176, 0.045, (0, 0, 0.05), "dark", "Z", U, verts=8)
    S.box((0.05, 0.03, 0.045), (0, 0.175, 0.05), "steel", U, bevel=0)
    S.box((0.02, 0.03, 0.26), (0.0, 0.17, -0.12), "dark", U, bevel=0)                      # coat front seam
    S.box((0.065, 0.05, 0.15), (-0.06, 0.15, 0.27), "coat3", U, rot=(0, 0, 22), bevel=0)   # lapels
    S.box((0.065, 0.05, 0.15), (0.06, 0.15, 0.27), "coat3", U, rot=(0, 0, -22), bevel=0)
    S.box((0.05, 0.03, 0.05), (0.09, 0.185, 0.29), "house", U, bevel=0)                  # lapel badge (house colour)
    S.box((0.22, 0.07, 0.07), (0, -0.04, 0.385), "coat3", U, bevel=0.01)                  # raised collar, back
    S.cyl(0.165, 0.06, (0, 0.0, 0.37), "coat3", "Z", U, verts=8, r2=0.12)                  # shoulder yoke
    # head: pale narrow face, dark eye shadow under the brim, wide-brimmed hat with a house band
    S.box((0.07, 0.07, 0.06), (0, 0.01, 0.44), "face", U)
    S.box((0.115, 0.10, 0.10), (0, 0.03, 0.515), "face", U, bevel=0.02)
    S.box((0.12, 0.04, 0.03), (0, 0.075, 0.545), "dark", U, bevel=0)
    S.cyl(0.235, 0.02, (0, 0.02, 0.60), "hat", "Z", U, verts=12)                          # brim
    S.cyl(0.13, 0.12, (0, 0.02, 0.665), "hat", "Z", U, verts=10, r2=0.12)                  # crown
    S.cyl(0.138, 0.032, (0, 0.02, 0.632), "house", "Z", U, verts=10)                        # hat band
    # pistol only in the fire frames
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    if p["pistol"]:
        S.box((0.045, 0.14, 0.07), (0, 0.04, 0.0), "steel", GM)
        S.box((0.04, 0.05, 0.09), (0, -0.04, -0.06), "dark", GM, rot=(10, 0, 0))
        S.cyl(0.018, 0.08, (0, 0.14, 0.01), "steel", "Y", GM, verts=6)
        if p["flash"]:
            S.cyl(0.06, 0.20, (0, 0.28, 0.01), "flash", "Y", GM, verts=8, r2=0.0)
            S.sphere((0.08, 0.08, 0.08), (0, 0.19, 0.01), "flash", GM, seg=8)
    P.arms(S, p, U, up, GM, SHOULDER, ((0, -0.03, -0.03), (0, 0.0, -0.03)), ("coat", "coat2"), thick=(0.075, 0.065), band=0,
           cuff_mat="coat3", hand_mat="face", hand_size=0.06, cuff_len=0.2)
    # legs: slim trousers and shoes under the coat hem
    P.legs(S, p, pel, spread=0.08, thick=(0.08, 0.07))
    if p["spread"]:
        S.cyl(0.36, 0.03, (0.0, p["dy"] + 0.12, 0.03), "coat2", "Z", None, verts=8)
        S.cyl(0.25, 0.035, (0.05, p["dy"] + 0.12, 0.05), "coat", "Z", None, verts=8)
        S.cyl(0.26, 0.02, (0.05, p["dy"] + 0.62, 0.03), "hat", "Z", None, verts=10)
