"""Shared Left Behind infantry parts (PAL_LB): materials, a pose factory and one parametrised figure builder.
Contract of every lb_* infantry module is that of auth_rifleman.py / dir_conscript.py (same canvas, origin, states, facings, camera).
Look (design 06 sec. 9): layered cloth wraps in sun-bleached canvas, dust ochre and ash grey, a tall pointed hood, goggles,
a respirator made from a filter tin (steel snout with a rust band), a long scarf, house colour on the scarf and armbands, rope and
patches.  Each unit differs by silhouette (coat length, bulk, hood height, back load, held item), not just by colour.
Blender-side code is imported lazily (build() only), so the manifest tools can import this without bpy."""
import math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import common as C
import auth_rifleman as AR

CANVAS, ORIGIN, STATES, DIE_FACING = AR.CANVAS, AR.ORIGIN, AR.STATES, AR.DIE_FACING
PALETTE = C.PAL_LB
L_UPPER, L_FORE, L_THIGH, L_SHIN = AR.L_UPPER, AR.L_FORE, AR.L_THIGH, AR.L_SHIN

MATERIALS = {
    "wrapA": ("#C2B28A", 0.95, 0.0, None), "wrapB": ("#8C6A33", 0.95, 0.0, None), "wrapC": ("#6B6762", 0.95, 0.0, None),
    "wrapD": ("#4B6F69", 0.95, 0.0, None), "wrapE": ("#7A3320", 0.95, 0.0, None), "wrapF": ("#D8CFB4", 0.95, 0.0, None),
    "ashA": ("#918D86", 0.95, 0.0, None), "ashB": ("#4A4743", 0.95, 0.0, None),
    "teal": ("#2F4A47", 0.9, 0.0, None), "rust": ("#A04A2C", 0.9, 0.0, None), "bone": ("#E6DDC4", 0.85, 0.0, None),
    "pants": ("#2A2018", 0.9, 0.0, None), "dark": ("#16100A", 0.85, 0.0, None), "steel": ("#6B6762", 0.45, 0.6, None),
    "wood": ("#8C6A33", 0.7, 0.0, None), "rope": ("#DACB9E", 0.95, 0.0, None), "hazard": ("#D9B72E", 0.8, 0.0, None),
    "lens": ("#F2D45C", 0.3, 0.0, "#F2D45C"), "flash": ("#FFE79A", 0.3, 0.0, "#FFE08A"),
    "house": ("#E000E0", 0.75, 0.0, None),
}
EMIT_STRENGTH = {"lens": 2.2, "flash": 8.0}


def make_pose(lean=14.0, stride=0.27, hold=((0.03, 0.17, 0.10), 24.0, 60.0), fire=((0.07, 0.30, 0.36), 12.0, 10.0),
              fire2=((0.07, 0.26, 0.365), 12.0, 14.0), hands=("gun", "gun"), bobamp=0.07, flash=True, fire_lean=12.0):
    """Pose function factory: stiff-ish shuffling walk, weapon at `hold`, aimed at `fire`, recoil/throw frame `fire2`."""
    def pose(state, step, n):
        base = dict(bob=0.0, lean=lean, twist=0.0, dy=0.0, flash=False, gun_space="u", roll=0.0, spread=False,
                    feet=((-0.11, 0.03, 0.09), (0.11, -0.03, 0.09)), gun=hold, hands=hands)
        if state == "idle":
            base["bob"] = 0.03
            return base
        if state == "walk":
            ph = 2 * math.pi * step / n
            feet = []
            for off, xx in ((0.0, -0.11), (math.pi, 0.11)):
                p = ph + off
                feet.append((xx, stride * math.cos(p), 0.09 + 0.08 * max(0.0, -math.sin(p))))
            base.update(feet=tuple(feet), bob=bobamp + 0.012 * abs(math.cos(ph)), twist=3.0 * math.cos(ph),
                        gun=((hold[0][0], hold[0][1], hold[0][2] + 0.008 * math.cos(2 * ph)), hold[1], hold[2]))
            return base
        if state == "fire":
            base.update(lean=fire_lean, twist=-12.0, bob=0.03, feet=((-0.14, 0.12, 0.09), (0.12, -0.20, 0.09)), gun=fire)
            if step == 1:
                base.update(flash=flash, lean=fire_lean - 2, gun=fire2)
            return base
        d = AR.pose(state, step, n)
        d.update(roll=[0.0, 0.0, -25.0, -55.0, -80.0, -90.0][step], spread=step >= 2)
        return d
    return pose


def build_figure(S, p, spec):
    """spec keys: shoulder, grips, bulk, coat (upper, mid, hem, wrap), hem_z (lowest coat z, pelvis relative), hood (mat, peak_h,
    peak_back), cape (mat or None), scarf (mat), band (armband material), sleeves (upper, fore, cuff), legs (mat, spread, thick),
    pack(S, U, R), extra(S, U, up, R), weapon(S, GM, p, R), tin_len."""
    from mathutils import Matrix
    import rig as R
    import dir_parts as P
    b = spec.get("bulk", 1.0)
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"]) @ R.rz(p["roll"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    c_up, c_mid, c_hem, c_wrap = spec["coat"]
    hz = spec.get("hem_z", -0.21)
    sk_h = 0.05 - hz
    S.cyl(0.20 * b, 0.32, (0, 0, 0.21), c_up, "Z", U, verts=8, r2=0.165 * b)                  # chest / shoulders
    S.cyl(0.245 * b, sk_h, (0, 0, (0.05 + hz) / 2), c_mid, "Z", U, verts=8, r2=0.20 * b)           # skirt
    S.cyl(0.258 * b, 0.07, (0, 0, hz + 0.03), c_hem, "Z", U, verts=8)                             # ragged hem band
    S.cyl(0.262 * b, 0.025, (0, 0, hz + 0.11), c_wrap, "Z", U, verts=8)                           # patch strip
    S.cyl(0.215 * b, 0.05, (0, 0, 0.05), "dark", "Z", U, verts=8)                                 # belt
    S.cyl(0.255 * b, 0.07, (0.0, 0.0, 0.17), c_wrap, "Z", U, verts=8, r2=0.235 * b)               # layered cloth wrap, mid chest
    S.box((0.08, 0.03, 0.06), (0.05, 0.215 * b, 0.05), "rust", U, bevel=0)                         # belt buckle / tin lid
    if spec.get("cape"):
        S.cyl(0.225 * b, 0.07, (0, 0, 0.345), spec["cape"], "Z", U, verts=8, r2=0.175 * b)         # drooping shoulder wrap
    # ---- scarf (house colour): wraps the neck, one long tail down the back, one over the chest
    S.limb(up((-0.05, 0.12, 0.40)), up((0.12, 0.17, 0.22)), 0.07, spec.get("scarf", "house"), d=0.04, bevel=0)
    S.limb(up((0.0, -0.04, 0.42)), up((0.07, -0.25 * b, 0.0)), 0.075, spec.get("scarf", "house"), d=0.035, bevel=0)
    # ---- head
    hm, ph_, pb = spec["hood"]
    S.box((0.08, 0.09, 0.07), (0, 0.07, 0.44), "dark", U)
    S.sphere((0.135, 0.145, 0.14), (0, 0.10, 0.565), hm, U)                                        # hood
    S.cyl(0.115, ph_, (0, 0.10 - pb * 0.5, 0.60 + ph_ / 2), hm, "Z", U, verts=8, r2=0.0)           # tall pointed peak
    S.cyl(0.14, 0.04, (0, 0.10, 0.50), c_wrap, "Z", U, verts=8)                                   # hood hem wrap
    S.box((0.18, 0.08, 0.10), (0, 0.255, 0.475), "ashB", U, bevel=0.015)                         # face cloth
    S.cyl(0.06, spec.get("tin_len", 0.09), (0, 0.325, 0.46), "steel", "Y", U, verts=10)           # filter-tin respirator
    S.cyl(0.068, 0.03, (0, 0.30, 0.46), "rust", "Y", U, verts=10)
    for sx in (-1, 1):
        S.box((0.085, 0.04, 0.065), (sx * 0.06, 0.29, 0.55), "lens", U, bevel=0)               # goggles
        S.box((0.075, 0.03, 0.02), (sx * 0.055, 0.28, 0.58), "dark", U, bevel=0)
    S.box((0.23, 0.03, 0.025), (0, 0.255, 0.545), "dark", U, bevel=0)                             # goggle strap
    # ---- back load and extras
    if spec.get("pack"):
        spec["pack"](S, U, R)
    if spec.get("house_boost"):          # D-185: scavenged team colour: painted rag sash, patched shoulder cloth, painted pack panel
        S.limb(up((0.15 * b, 0.19 * b, 0.38)), up((-0.17 * b, 0.235 * b, 0.03)), 0.12 * b, "house", d=0.04, bevel=0)   # painted rag sash
        for sx in (-1, 1):
            S.box((0.13, 0.20 * b, 0.05), (sx * (0.21 * b + 0.02), 0.0, 0.395), "house", U, rot=(0, sx * -10, 0), bevel=0)   # patched shoulder cloth
        if spec.get("panel"):
            sz_, ps_ = spec["panel"]
            S.box(sz_, ps_, "house", U, bevel=0)                      # painted panel on the pack
    if spec.get("extra"):
        spec["extra"](S, U, up, R)
    # ---- held item
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    spec["weapon"](S, GM, p, R)
    # ---- arms and legs (shared two-bone IK builders)
    su, sf, sc_ = spec.get("sleeves", ("wrapB", "wrapA", "rope"))
    P.arms(S, p, U, up, GM, spec["shoulder"], spec["grips"], (su, sf), thick=(0.085 * b, 0.072 * b), band=0.12 * b,
           band_mat="house", cuff_mat=sc_, hand_mat="dark", hand_size=0.07, cuff_len=0.28)
    lm, lsp, lth = spec.get("legs", ("pants", 0.095, (0.095, 0.08)))
    P.legs(S, p, pel, spread=lsp, thick=lth, mat=lm, boot="dark")
    if p["spread"]:
        S.cyl(0.40 * b, 0.03, (0.0, p["dy"] + 0.12, 0.03), c_mid, "Z", None, verts=8)
        S.cyl(0.28 * b, 0.035, (0.05, p["dy"] + 0.12, 0.05), c_up, "Z", None, verts=8)
        S.box((0.5, 0.06, 0.03), (0.0, p["dy"] + 0.52, 0.03), c_hem, None, rot=(0, 0, 8))
