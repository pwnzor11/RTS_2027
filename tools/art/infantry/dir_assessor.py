"""Directorate Assessor: a Committee clerk.  Stooped but tidy: knee-length dark umber clerk's coat with a big bone ruff collar, flat
peaked clerk's cap with a house band, round brass spectacles over a pale face, a fat ledger carried in both hands against the chest
(red seal tab, bone pages), a satchel of rolled forms and a stamp hanging on the back.  No weapon (archive: "a ledger"). Original design.
Same contract, scale, camera, states and facings as dir_conscript.py.  The "fire" state is the work gesture: ledger lifted to
inspect, then slammed forward with a seal stamp (repair / capture action).
Silhouette vs the conscript at 1x: square flat cap instead of a pointed hood, a big pale rectangle (the ledger) in front, a short
bone ruff, no rifle.
House colour: cap band, armbands, panel on the satchel, shoulder boards.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C
import auth_rifleman as AR
import dir_conscript as DC

SCALE = 1.25                  # D-185: figure 1.25 x, frame 48 x 48
ID = "dir_assessor"
TITLE = "Directorate Assessor"
CANVAS, ORIGIN, STATES, DIE_FACING = AR.CANVAS, AR.ORIGIN, AR.STATES, AR.DIE_FACING
PALETTE = C.PAL_DIR
MATERIALS = dict(DC.MATERIALS)
MATERIALS["lens"] = ("#D9B84A", 0.3, 0.0, None)
EMIT_STRENGTH = DC.EMIT_STRENGTH
SHOULDER = DC.SHOULDER
GRIP_R, GRIP_L = (0.17, -0.04, -0.03), (-0.17, -0.04, -0.03)       # hands on the ledger's two edges (ledger coords)
HOLD = ((0.0, 0.34, 0.12), 0.0, 52.0)                            # ledger held up against the chest, face tilted to the front


def pose(state, step, n):
    p = DC.pose(state, step, n)
    p.update(flash=False, pistol=False)
    if state == "idle":
        p.update(gun=HOLD, lean=11.0)
    elif state == "walk":
        p.update(gun=((HOLD[0][0], HOLD[0][1], HOLD[0][2] + 0.01 * math.cos(2 * math.pi * step / n)), 0.0, 52.0),
                 hands=("gun", "gun"))
    elif state == "fire":
        if step == 0:        # lifted high to read
            p.update(lean=8.0, twist=-6.0, gun=((0.0, 0.22, 0.42), -8.0, 80.0))
        else:                # slammed forward, stamp comes down
            p.update(lean=14.0, twist=-14.0, gun=((0.0, 0.38, 0.22), -12.0, 38.0), flash=True)
    else:
        p.update(gun_space=p["gun_space"])
    return p


def build(S, p):
    from mathutils import Matrix
    import rig as R
    import dir_parts as P
    U = R.tr(0, p["dy"], 0.62 + p["bob"]) @ R.rz(p["twist"]) @ R.rx(-p["lean"]) @ R.rz(p["roll"])
    pel = (0, p["dy"], 0.62 + p["bob"])

    def up(pt):
        return tuple(U @ R.Vector(pt))

    # clerk's coat: fitted, mid-shin shorter than the conscript's greatcoat, bone-piped hem, dark belt with a rubber stamp
    S.cyl(0.20, 0.32, (0, 0, 0.21), "coat2", "Z", U, verts=8, r2=0.165)
    S.cyl(0.235, 0.26, (0, 0, -0.08), "coat3", "Z", U, verts=8, r2=0.20)
    S.cyl(0.24, 0.025, (0, 0, -0.205), "bone", "Z", U, verts=8)                       # bone hem piping
    S.cyl(0.206, 0.04, (0, 0, 0.05), "dark", "Z", U, verts=8)
    S.box((0.05, 0.04, 0.10), (0.16, 0.14, 0.0), "hood", U, bevel=0)                  # stamp hanging at the hip
    S.box((0.07, 0.05, 0.03), (0.16, 0.14, -0.07), "red", U, bevel=0)
    S.cyl(0.215, 0.07, (0, 0.0, 0.345), "hood", "Z", U, verts=8, r2=0.17)             # shoulder cape
    for sx in (-1, 1):
        S.box((0.13, 0.17, 0.04), (sx * 0.19, 0.0, 0.395), "house", U, rot=(0, sx * -8, 0), bevel=0)
    # satchel of rolled forms on the back, house panel on its flap
    S.box((0.30, 0.12, 0.26), (0, -0.27, 0.12), "coat", U, bevel=0.02)
    S.box((0.24, 0.025, 0.20), (0, -0.335, 0.15), "house", U, bevel=0)
    for sx in (-0.08, 0.08):
        S.cyl(0.04, 0.30, (sx, -0.26, 0.31), "bone", "Z", U, verts=8)
    # head: bone ruff, pale face with brass spectacles, flat peaked cap with a house band
    S.box((0.08, 0.09, 0.07), (0, 0.06, 0.44), "dark", U)
    S.cyl(0.14, 0.07, (0, 0.06, 0.40), "bone", "Z", U, verts=8)                       # ruff collar
    S.sphere((0.14, 0.15, 0.125), (0, 0.10, 0.555), "bone", U)                         # head (pale)
    S.box((0.27, 0.22, 0.05), (0, 0.10, 0.66), "hood", U, bevel=0.01)                 # flat cap crown
    S.box((0.28, 0.20, 0.035), (0, 0.10, 0.62), "house", U, bevel=0)                  # house band
    S.box((0.25, 0.09, 0.025), (0, 0.25, 0.60), "dark", U, rot=(-10, 0, 0), bevel=0)  # cap peak
    for sx in (-1, 1):
        S.box((0.075, 0.04, 0.07), (sx * 0.06, 0.25, 0.52), "lens", U, bevel=0)       # spectacles
    S.box((0.19, 0.03, 0.03), (0, 0.25, 0.52), "dark", U, bevel=0)
    # ledger (gun coords: x width, y height, z thickness; held tilted so the cover faces the front)
    GM = (U if p["gun_space"] == "u" else Matrix.Identity(4)) @ R.tr(*p["gun"][0]) @ R.rz(p["gun"][1]) @ R.rx(p["gun"][2])
    S.box((0.38, 0.30, 0.05), (0, 0, 0), "red", GM, bevel=0.01)                        # cover
    S.box((0.33, 0.25, 0.04), (0, 0.0, -0.045), "bone", GM, bevel=0)                    # page block underneath
    S.box((0.04, 0.30, 0.06), (-0.18, 0, 0), "dark", GM, bevel=0)                       # dark spine
    S.box((0.10, 0.07, 0.07), (0.07, 0.10, 0.0), "bone", GM, bevel=0)                   # bone seal tab
    S.box((0.15, 0.05, 0.02), (0.0, -0.05, 0.035), "bone", GM, bevel=0)                 # title label
    if p["flash"]:
        S.cyl(0.05, 0.14, (0.10, 0.10, 0.12), "red", "Z", GM, verts=8)                  # stamp pressed on the page
        S.cyl(0.04, 0.07, (0.10, 0.10, 0.22), "bone", "Z", GM, verts=8)
    P.arms(S, p, U, up, GM, SHOULDER, (GRIP_R, GRIP_L), ("coat2", "coat3"))
    P.legs(S, p, pel, thick=(0.10, 0.085))
    if p["spread"]:
        S.cyl(0.40, 0.03, (0.0, p["dy"] + 0.12, 0.03), "coat2", "Z", None, verts=8)
        S.cyl(0.28, 0.035, (0.05, p["dy"] + 0.12, 0.05), "coat3", "Z", None, verts=8)
