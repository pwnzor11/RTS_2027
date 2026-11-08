"""Shared Expedition infantry parts (PAL_EXP): materials, a pose factory and one parametrised figure builder.
Contract of every exp_* infantry module is that of auth_rifleman.py / lb_parts.py (same 32 px canvas, origin, states, facings, camera).
Look (design 07 sec. 8): navy fatigues, white webbing (cross straps, belt, pouches), a chrome helmet with a signal-orange visor stripe,
a sealed face plate (no skin tones in the Expedition palette), copper trim; team colour on a flash on each shoulder.  Each unit differs by
silhouette (bulk, back load, held item, helmet), not just by colour.
Blender-side code is imported lazily (build() only), so the manifest tools can import this without bpy."""
import importlib.util, math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import common as C
import auth_rifleman as AR
import lb_parts as LP

CANVAS, ORIGIN, STATES, DIE_FACING = AR.CANVAS, AR.ORIGIN, AR.STATES, AR.DIE_FACING
L_UPPER, L_FORE, L_THIGH, L_SHIN = AR.L_UPPER, AR.L_FORE, AR.L_THIGH, AR.L_SHIN


def _palette():
    """PAL_EXP from the shared vehicle kit (loaded by path: the structures folder has an exp_common of the same name)."""
    p = os.path.join(os.path.dirname(HERE), "vehicles", "exp_common.py")
    spec = importlib.util.spec_from_file_location("exp_vehicle_common", p)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m.PAL_EXP


PALETTE = _palette()

MATERIALS = {
    "navy": ("#2B4A82", 0.8, 0.0, None), "navy2": ("#1B2D52", 0.85, 0.0, None), "navy3": ("#1E3158", 0.85, 0.0, None),
    "white": ("#E2EEEA", 0.7, 0.0, None), "white2": ("#8FA6AC", 0.75, 0.0, None),
    "chrome": ("#BDD0D2", 0.28, 0.85, None), "chrome2": ("#79838C", 0.35, 0.8, None), "chrome3": ("#4F575F", 0.4, 0.7, None),
    "copper": ("#B7692F", 0.32, 0.8, None), "orange": ("#F2751F", 0.45, 0.0, None), "orange2": ("#B8481A", 0.5, 0.0, None),
    "glass": ("#2B7F95", 0.12, 0.4, None), "dark": ("#1D2024", 0.9, 0.0, None), "rubber": ("#30343A", 0.95, 0.0, None),
    "lens": ("#FFB45E", 0.3, 0.0, "#FFB45E"), "flash": ("#FFE79A", 0.3, 0.0, "#FFE08A"),
    "house": ("#E000E0", 0.75, 0.0, None),
}
EMIT_STRENGTH = {"lens": 2.2, "flash": 8.0}

make_pose = LP.make_pose


def build_figure(S, p, spec):
    """spec keys: shoulder, grips, bulk, body (torso, hips mats), helm (mat), visor (face plate mat), hip_h (extra pelvis block), sleeves
    (upper, fore, glove), legs (mat, spread, thick), boot, flash_size, pack(S, U, R), extra(S, U, up, R, p), weapon(S, GM, p, R)."""
    from mathutils import Matrix
    import rig as R
    import dir_parts as P
    b = spec.get("bulk", 1.0)
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"]) @ R.rz(p["roll"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    tm, hm_ = spec.get("body", ("navy", "navy2"))
    # ---- torso, hips, belt, cross straps, pouches
    S.box((0.40 * b, 0.26 * b, 0.20), (0, 0, -0.02), hm_, U, bevel=0.03)                       # hips / jacket skirt
    S.box((0.37 * b, 0.25 * b, 0.40), (0, 0, 0.22), tm, U, bevel=0.03)                          # chest
    S.box((0.43 * b, 0.28 * b, 0.05), (0, 0, 0.05), "white", U, bevel=0.005)                    # webbing belt
    S.box((0.06, 0.03, 0.05), (0.0, 0.145 * b, 0.05), "copper", U, bevel=0)                     # buckle
    S.limb(up((-0.14 * b, 0.13 * b, 0.40)), up((0.14 * b, 0.13 * b, 0.07)), 0.05, "white", d=0.025, bevel=0)   # cross straps
    S.limb(up((0.14 * b, 0.13 * b, 0.40)), up((-0.14 * b, 0.13 * b, 0.07)), 0.05, "white", d=0.025, bevel=0)
    for sx in (-1, 1):
        S.box((0.10, 0.05, 0.11), (sx * 0.12 * b, 0.145 * b, 0.12), "white2", U, bevel=0.01)    # chest pouches
        S.box((0.11, 0.07, 0.08), (sx * 0.14 * b, 0.1, -0.06), "white2", U, bevel=0.01)         # hip pouches
        if spec.get("house_boost"):      # D-184: a real shoulder pad in the house ramp (wider, deeper, taller than the flash)
            S.box((0.10, 0.22 * b, 0.13), (sx * (0.23 * b + 0.03), 0.0, 0.405), "house", U, bevel=0)
        else:
            S.box((0.065, 0.14 * b, 0.10), (sx * (0.23 * b + 0.015), 0.0, 0.40), "house", U, bevel=0)   # team colour shoulder flash
        S.box((0.07, 0.15 * b, 0.025), (sx * (0.23 * b + 0.015), 0.0, 0.455), "white", U, bevel=0)
    # ---- head: neck, face plate, chrome helmet with orange visor stripe
    S.box((0.10, 0.10, 0.07), (0, 0, 0.46), "dark", U)
    S.box((0.19, 0.13, 0.12), (0, 0.065, 0.47), spec.get("mask", "chrome3"), U, bevel=0.03)      # sealed face plate
    S.sphere((0.17, 0.18, 0.14), (0, 0, 0.565), spec.get("helm", "chrome"), U)                    # helmet shell
    S.box((0.25, 0.14, 0.03), (0, 0.075, 0.585), spec.get("helm", "chrome"), U, bevel=0.01)       # brim
    S.box((0.19, 0.05, 0.06), (0, 0.145, 0.525), "glass", U, bevel=0.01)                        # visor glass
    S.box((0.21, 0.05, 0.028), (0, 0.15, 0.568), "orange", U, bevel=0)                          # orange visor stripe
    S.box((0.04, 0.17, 0.02), (0, 0.0, 0.70), "copper", U, bevel=0)                              # helmet ridge trim
    if spec.get("house_boost"):          # D-184: house-ramp band over the helmet crown, seen from every side
        S.box((0.20, 0.07, 0.05), (0, 0.0, 0.625), "house", U, bevel=0)
    # ---- back load and extras
    if spec.get("pack"):
        spec["pack"](S, U, R)
    if spec.get("extra"):
        spec["extra"](S, U, up, R, p)
    # ---- held item
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    spec["weapon"](S, GM, p, R)
    # ---- arms and legs (shared two-bone IK builders)
    su, sf, gl = spec.get("sleeves", ("navy", "navy2", "white2"))
    P.arms(S, p, U, up, GM, spec["shoulder"], spec["grips"], (su, sf), thick=(0.085 * b, 0.072 * b), band=0.0,
           cuff_mat=gl, hand_mat="dark", hand_size=0.07 * b, cuff_len=0.22)
    lm, lsp, lth = spec.get("legs", ("navy2", 0.095 * b, (0.095 * b, 0.08 * b)))
    P.legs(S, p, pel, spread=lsp, thick=lth, mat=lm, boot=spec.get("boot", "dark"))
    if p["spread"]:
        S.cyl(0.40 * b, 0.03, (0.0, p["dy"] + 0.12, 0.03), hm_, "Z", None, verts=8)
        S.cyl(0.28 * b, 0.035, (0.05, p["dy"] + 0.12, 0.05), tm, "Z", None, verts=8)
