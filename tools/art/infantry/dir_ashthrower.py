"""Directorate Ashthrower: conscript silhouette (stooped, hooded, knee-length coat, bone face plate) made heavy and
dangerous: a big bone-white canister backpack with a red radiation band and a hose to a stubby, fat-muzzled grenade
launcher, and a thick dark rubber apron hanging over the chest and thighs.  Original design.
Same contract, scale, camera, states and facings as dir_conscript.py (poses are the conscript's: march at port, stoop, roll).
Silhouette vs the conscript at 1x: a wide pale cylinder on the back, a dark apron block on the front, short fat launcher.
House colour: armbands, a short collar sash above the apron, a panel on top of the canister harness."""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C
import auth_rifleman as AR
import dir_conscript as DC
import boost

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48, house shoulder boards / back panel
ID = "dir_ashthrower"
TITLE = "Directorate Ashthrower"
CANVAS, ORIGIN, STATES, DIE_FACING = AR.CANVAS, AR.ORIGIN, AR.STATES, AR.DIE_FACING
PALETTE = C.PAL_DIR
MATERIALS = dict(DC.MATERIALS)
MATERIALS["rubber"] = ("#26211D", 0.6, 0.0, None)
EMIT_STRENGTH = DC.EMIT_STRENGTH
SHOULDER = DC.SHOULDER
GRIP_R, GRIP_L = (0, -0.08, -0.07), (0, 0.13, -0.06)


def pose(state, step, n):
    p = DC.pose(state, step, n)
    if state != "die":
        p["lean"] += 3.0          # the pack drags the stoop deeper
    return p


def build(S, p):
    from mathutils import Matrix
    import rig as R
    import dir_parts as P
    B = boost.params()                                   # S43 pilot boost
    H, LB, SB = B["house"], B["limb"], B["stance"]
    pp = dict(p, feet=tuple((f[0] * SB, f[1], f[2]) for f in p["feet"]))   # wider stance for the legs helper
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"]) @ R.rz(p["roll"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    # coat (as conscript)
    S.cyl(0.20, 0.32, (0, 0, 0.21), "coat", "Z", U, verts=8, r2=0.165)
    S.cyl(0.245, 0.26, (0, 0, -0.08), "coat2", "Z", U, verts=8, r2=0.20)
    S.cyl(0.255, 0.06, (0, 0, -0.20), "coat3", "Z", U, verts=8)
    S.cyl(0.258, 0.022, (0, 0, -0.17), "red", "Z", U, verts=8)
    S.cyl(0.206, 0.04, (0, 0, 0.05), "dark", "Z", U, verts=8)
    S.cyl(0.215, 0.07, (0, 0.0, 0.345), "hood", "Z", U, verts=8, r2=0.17)
    # thick rubber apron: chest bib to below the knee, heavy hem roll, bone buckle
    S.box((0.30, 0.07, 0.40), (0, 0.215, 0.12), "rubber", U, rot=(-4, 0, 0), bevel=0.03)
    S.box((0.34, 0.09, 0.07), (0, 0.235, -0.12), "rubber", U, bevel=0.03)
    S.box((0.09, 0.03, 0.05), (0, 0.255, 0.25), "bone", U, bevel=0)
    # short house sash over the shoulders above the apron
    S.limb(up((-0.17, 0.02, 0.33)), up((-0.06, 0.19, 0.31)), 0.09 * H, "house", d=0.04 * H, bevel=0)
    S.limb(up((0.17, 0.02, 0.33)), up((0.06, 0.19, 0.31)), 0.09 * H, "house", d=0.04 * H, bevel=0)
    for sx in (-1, 1):                                                                 # D-185 house shoulder boards, S43 larger
        S.box((0.13 * (1 + 0.5 * (H - 1)), 0.17 * (1 + 0.3 * (H - 1)), 0.04 * (1 + 0.6 * (H - 1))), (sx * (0.19 + 0.02 * (H - 1)), 0.0, 0.395), "house", U, rot=(0, sx * -8, 0), bevel=0)
    # canister backpack: big bone-white tank, red radiation band, dark caps, twin small tanks, harness
    S.cyl(0.125, 0.52, (0, -0.30, 0.17), "bone", "Z", U, verts=10)
    S.cyl(0.132, 0.075, (0, -0.30, 0.19), "red", "Z", U, verts=10)
    S.cyl(0.128, 0.05, (0, -0.30, 0.42), "dark", "Z", U, verts=10, r2=0.07)
    S.cyl(0.128, 0.05, (0, -0.30, -0.09), "dark", "Z", U, verts=10)
    S.cyl(0.07, 0.38, (-0.17, -0.27, 0.10), "bone", "Z", U, verts=8)
    S.cyl(0.07, 0.38, (0.17, -0.27, 0.10), "bone", "Z", U, verts=8)
    S.box((0.25 * (1 + 0.15 * (H - 1)), 0.03, 0.20 * H), (0, -0.425, 0.28), "house", U, bevel=0)
    S.limb(up((0.0, -0.18, 0.40)), up((0.0, 0.20, 0.42)), 0.04, "dark", d=0.03, bevel=0)   # harness strap over the shoulders
    # head: hood, bone plate, filter dot (as conscript; S43: scaled about the head centre by B["head"])
    h = B["head"]
    HC = (0.0, 0.12, 0.52)

    def hp(loc):
        return tuple(HC[i] + (loc[i] - HC[i]) * h for i in range(3))
    S.box((0.08, 0.09, 0.07), (0, 0.07, 0.44), "dark", U)
    S.sphere((0.13 * h, 0.14 * h, 0.135 * h), hp((0, 0.10, 0.565)), "hood", U)
    S.box((0.10 * h, 0.22 * h, 0.09 * h), hp((0, 0.04, 0.69)), "hood", U, rot=(-40, 0, 0), bevel=0.02)
    S.box((0.20 * h, 0.09 * h, 0.20 * h), hp((0, 0.255, 0.49)), "bone", U, bevel=0.03)
    for sx in (-1, 1):
        S.box((0.06 * h, 0.03 * h, 0.04 * h), hp((sx * 0.05, 0.30, 0.545)), "dark", U, bevel=0)
    S.cyl(0.05 * h, 0.06 * h, hp((0, 0.31, 0.46)), "dark", "Y", U, verts=10)
    # stubby grenade launcher: fat body, short wide muzzle, drum, small stock; hose back to the tank
    # (S43: longer and thicker by B["gun"] / B["gunw"]; GM carries the scale, so the grips below stay in unscaled gun coords)
    GM0 = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    GM = GM0 @ Matrix.Diagonal((B["gunw"], B["gun"], B["gunw"], 1.0))
    S.box((0.05, 0.16, 0.08), (0, -0.20, -0.01), "wood", GM, rot=(-6, 0, 0))
    S.box((0.09, 0.26, 0.10), (0, 0.0, 0), "steel", GM)
    S.cyl(0.055, 0.14, (0, 0.20, 0.01), "dark", "Y", GM, verts=8)
    S.cyl(0.075, 0.07, (0, 0.04, -0.08), "steel", "X", GM, verts=8)
    S.box((0.03, 0.04, 0.03), (0, 0.27, 0.07), "dark", GM)
    if p["flash"]:
        fl, flw = B["flash"] / B["gun"], B["flash"] / B["gunw"]
        S.cyl(0.09 * flw, 0.28 * fl, (0, 0.46, 0.01), "flash", "Y", GM, verts=8, r2=0.0)
        S.sphere((0.11 * flw, 0.11 * fl, 0.11 * flw), (0, 0.33, 0.01), "flash", GM, seg=8)
    hose_a = R.Vector(up((0.0, -0.17, 0.0)))
    S.limb(hose_a, R.Vector(tuple(GM @ R.Vector((0.03, -0.02, -0.04)))), 0.035 * LB, "dark", d=0.035 * LB, bevel=0)
    P.arms(S, p, U, up, GM, SHOULDER, (GRIP_R, GRIP_L), ("coat2", "coat3"), thick=(0.09 * LB, 0.075 * LB), band=0.13 * LB * (1 + 0.2 * (H - 1)))
    P.legs(S, pp, pel, spread=0.095 * SB, thick=(0.095 * LB, 0.08 * LB))
    if p["spread"]:
        S.cyl(0.40, 0.03, (0.0, p["dy"] + 0.12, 0.03), "coat2", "Z", None, verts=8)
        S.cyl(0.28, 0.035, (0.05, p["dy"] + 0.12, 0.05), "rubber", "Z", None, verts=8)
        S.box((0.5, 0.06, 0.03), (0.0, p["dy"] + 0.52, 0.03), "coat3", None, rot=(0, 0, 8))
