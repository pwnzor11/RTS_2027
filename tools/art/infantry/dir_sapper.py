"""Directorate Sapper: the conscript's stooped hooded build but a SHORT coat (hip length, bare dark trousers and boots show),
satchel charges strapped across the chest, a hand drill / auger carried on the back, goggles over the bone plate and a short carbine.
Original design.  Same contract, scale, camera, states and facings as dir_conscript.py (same poses, carbine at port).
Silhouette vs the conscript at 1x: noticeably shorter coat with long legs, a boxy chest load, a pale auger shaft poking above the shoulder.
House colour: armbands, a broad harness strap across the chest, a panel on the auger case."""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C
import auth_rifleman as AR
import dir_conscript as DC

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48, house shoulder boards / back panel
ID = "dir_sapper"
TITLE = "Directorate Sapper"
CANVAS, ORIGIN, STATES, DIE_FACING = AR.CANVAS, AR.ORIGIN, AR.STATES, AR.DIE_FACING
PALETTE = C.PAL_DIR
MATERIALS = dict(DC.MATERIALS)
MATERIALS["lens"] = ("#D9B84A", 0.3, 0.0, None)
EMIT_STRENGTH = DC.EMIT_STRENGTH
SHOULDER = DC.SHOULDER
GRIP_R, GRIP_L = (0, -0.08, -0.06), (0, 0.15, -0.05)


def pose(state, step, n):
    p = DC.pose(state, step, n)
    if state != "die":
        p["lean"] += 2.0
    return p


def build(S, p):
    from mathutils import Matrix
    import rig as R
    import dir_parts as P
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"]) @ R.rz(p["roll"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    # padded vest (quilted bands) over a dark shirt; no coat skirt, short dark trousers show
    S.cyl(0.20, 0.32, (0, 0, 0.21), "coat2", "Z", U, verts=8, r2=0.17)
    for z in (0.12, 0.20, 0.28):
        S.cyl(0.205 - (z - 0.12) * 0.12, 0.018, (0, 0, z), "coat3", "Z", U, verts=8)      # quilting seams
    S.cyl(0.206, 0.04, (0, 0, 0.03), "dark", "Z", U, verts=8)                               # belt
    S.box((0.07, 0.04, 0.05), (0, 0.205, 0.03), "bone", U, bevel=0)
    S.cyl(0.215, 0.07, (0, 0.0, 0.345), "hood", "Z", U, verts=8, r2=0.17)
    for sx in (-1, 1):                                                                 # D-185 house shoulder boards
        S.box((0.13, 0.17, 0.04), (sx * 0.19, 0.0, 0.395), "house", U, rot=(0, sx * -8, 0), bevel=0)
    # harness strap (house) shoulder to hip, bone strap crossing it
    S.limb(up((-0.09, 0.19, 0.32)), up((0.13, 0.225, -0.02)), 0.09, "house", d=0.04, bevel=0)
    S.limb(up((-0.17, 0.02, 0.32)), up((-0.09, 0.19, 0.32)), 0.09, "house", d=0.04, bevel=0)
    S.limb(up((0.09, 0.19, 0.32)), up((-0.13, 0.225, -0.02)), 0.05, "bone", d=0.035, bevel=0)
    for sx in (-1, 1):
        S.box((0.11, 0.09, 0.13), (sx * 0.075, 0.245, 0.20), "bone", U, bevel=0.01)
        S.box((0.115, 0.095, 0.03), (sx * 0.075, 0.245, 0.225), "red", U, bevel=0)
        S.box((0.03, 0.03, 0.04), (sx * 0.075, 0.30, 0.15), "dark", U, bevel=0)
    # big rounded coil drum on the back (mid umber, ~1.5x the old pack width), bone hubs, dark bands
    S.cyl(0.19, 0.36, (0, -0.30, 0.16), "coat", "X", U, verts=12)
    S.cyl(0.20, 0.05, (-0.10, -0.30, 0.16), "coat3", "X", U, verts=12)
    S.cyl(0.20, 0.05, (0.10, -0.30, 0.16), "coat3", "X", U, verts=12)
    S.cyl(0.07, 0.40, (0, -0.30, 0.16), "dark", "X", U, verts=8)
    S.box((0.24, 0.03, 0.22), (0, -0.495, 0.16), "house", U, bevel=0)
    # fat pale-bone drill bit rising diagonally up and out over the right shoulder: tapering segments
    A0, A1 = R.Vector((0.10, -0.28, 0.30)), R.Vector((0.40, -0.02, 0.86))
    for t0, t1, w in ((0.0, 0.3, 0.13), (0.3, 0.55, 0.115), (0.55, 0.78, 0.09), (0.78, 1.0, 0.055)):
        S.limb(up(A0.lerp(A1, t0)), up(A0.lerp(A1, t1)), w, "bone", d=w, bevel=0)
    for t in (0.18, 0.42, 0.66):
        S.limb(up(A0.lerp(A1, t - 0.04)), up(A0.lerp(A1, t + 0.04)), 0.16 - 0.1 * t, "coat3", d=0.16 - 0.1 * t, bevel=0)
    # head: round hood-helmet (no peak), bone plate, goggles, brass headlamp
    S.box((0.08, 0.09, 0.07), (0, 0.07, 0.44), "dark", U)
    S.sphere((0.14, 0.15, 0.14), (0, 0.10, 0.565), "hood", U)
    S.box((0.20, 0.09, 0.20), (0, 0.255, 0.49), "bone", U, bevel=0.03)
    S.box((0.23, 0.05, 0.075), (0, 0.285, 0.545), "dark", U, bevel=0.01)
    for sx in (-1, 1):
        S.box((0.075, 0.04, 0.06), (sx * 0.058, 0.32, 0.545), "lens", U, bevel=0)
    S.cyl(0.05, 0.06, (0, 0.31, 0.46), "dark", "Y", U, verts=10)
    S.box((0.075, 0.05, 0.075), (0, 0.235, 0.655), "lens", U, bevel=0)                     # brass headlamp
    # short carbine
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    S.box((0.05, 0.20, 0.09), (0, -0.19, -0.01), "wood", GM, rot=(-6, 0, 0))
    S.box((0.055, 0.22, 0.08), (0, 0.0, 0), "wood", GM)
    S.cyl(0.021, 0.22, (0, 0.20, 0.01), "steel", "Y", GM, verts=6)
    S.box((0.035, 0.07, 0.10), (0, 0.02, -0.08), "dark", GM, rot=(8, 0, 0))
    S.box((0.02, 0.03, 0.03), (0, 0.30, 0.04), "dark", GM)
    if p["flash"]:
        S.cyl(0.07, 0.24, (0, 0.54, 0.01), "flash", "Y", GM, verts=8, r2=0.0)
        S.sphere((0.09, 0.09, 0.09), (0, 0.42, 0.01), "flash", GM, seg=8)
    P.arms(S, p, U, up, GM, SHOULDER, (GRIP_R, GRIP_L), ("coat2", "coat3"))
    P.legs(S, p, pel, thick=(0.10, 0.085))
    if p["spread"]:
        S.cyl(0.30, 0.03, (0.0, p["dy"] + 0.12, 0.03), "coat2", "Z", None, verts=8)
        S.cyl(0.24, 0.035, (0.05, p["dy"] + 0.12, 0.05), "coat", "Z", None, verts=8)
        S.box((0.5, 0.06, 0.03), (0.0, p["dy"] + 0.52, 0.03), "steel", None, rot=(0, 0, 8))
