"""Authority Airfield: an aircraft apron with a barrel-vault hangar and a control hut (3x3 cells). Original design.
Contract: see auth_refinery.py (this module imports its helpers and materials, so keep the two side by side).

South part: a concrete runway segment (east-west) with threshold bars, dashed centre line, edge lights and a big team-colour
apron roundel/pad; the aircraft leaves the hangar through the south doors onto it (DOCK like the Factory).  North-west: a
corrugated barrel-vault hangar with team-colour bands over the arches and house panels either side of the doors.  North-east:
a two-storey control hut with a glazed cab, a rotating beacon, a radar paddle and a windsock mast.
States: idle (4: beacon, windsock, edge lights), damaged (4, STRUCTURAL only: caved-in vault, buckled door, cracked hut,
snapped mast, broken runway edge; fire/smoke are separate effects, see FX), build (8 stages, same recipe as the Refinery),
active (4: hangar doors rolled open with work lights, runway lights chasing, beacon lit; played while an aircraft is produced).
"""
import importlib.util
import math
import os

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structures_auth_refinery_ref", os.path.join(_here, "auth_refinery.py"))
_ref = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_ref)

ID = "auth_airfield"
TITLE = "Authority Airfield"
FOOTPRINT = (3, 3)
CANVAS = (192, 224)
ORIGIN = (0, 32)
DOCK = (1, 3)           # exit: the cell just below the middle of the footprint
CAP_MATERIAL = "concrete2"
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5), ("active", 4, 3)]

# hangar block (north-west), control hut (north-east), runway (south)
HX0, HX1 = -4.7, -0.1
HY0, HY1 = -1.0, 3.4
WALL_H = 1.1
VR = (HX1 - HX0) / 2                # vault radius
VCX = (HX0 + HX1) / 2
VZ = WALL_H + 0.1                   # vault axis height (half-cylinder above the wall tops)
HUT = (3.0, 1.6)                    # hut centre (x, y)
RWY_Y0, RWY_Y1 = -4.8, -2.3
RWY_Z = 0.17
MAST = (4.5, -0.5)


def px(x, y, z):
    return round(96 + x * 18.8), round(128 - y * 18.8 - z * 14.4)


FX = [
    dict(state="damaged", kind="fire_large", x=px(VCX, 2.0, VZ + 0.7)[0], y=px(VCX, 2.0, VZ + 0.7)[1], phase=0),    # caved-in vault
    dict(state="damaged", kind="fire_small", x=px(HUT[0], HUT[1], 2.9)[0], y=px(HUT[0], HUT[1], 2.9)[1], phase=3),    # hut roof
    dict(state="damaged", kind="fire_small", x=px(-3.0, -0.6, 1.0)[0], y=px(-3.0, -0.6, 1.0)[1], phase=5),           # hangar wall
    dict(state="damaged", kind="fire_small", x=px(1.0, -3.2, 0.2)[0], y=px(1.0, -3.2, 0.2)[1], phase=1),             # runway crater
    dict(state="damaged", kind="smoke_plume", x=px(VCX, 2.0, VZ + 0.8)[0], y=px(VCX, 2.0, VZ + 0.8)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=px(HUT[0], HUT[1], 3.0)[0], y=px(HUT[0], HUT[1], 3.0)[1], phase=6),
]

MATERIALS = dict(_ref.MATERIALS)
MATERIALS.update({
    "glass": ("#A9B8B4", dict(rough=0.25)),
    "glass_lit": ("#D8E6DC", dict(rough=0.3, emit="#BFD6C8")),
    "asphalt": ("#3A3F44", dict(rough=1.0)),
    "asphalt_s": ("#2C3033", dict(rough=1.0)),
    "glass_s": ("#6B7774", dict(rough=0.3)),
    "steel2": ("#979EA3", dict(rough=0.4, metal=0.5)),
    "beacon": ("#FFB347", dict(rough=0.3, emit="#FFB347")),
    "vault": ("#7C8C72", dict(rough=0.6, metal=0.2)),
    "vault_s": ("#57634F", dict(rough=0.6, metal=0.2)),
    "red": ("#B5402B", dict(rough=0.6)),
    "orange": ("#E8782A", dict(rough=0.8)),
})
SOOT_SKIN = dict(_ref.SOOT_SKIN)
SOOT_SKIN.update({"glass": "glass_s", "asphalt": "asphalt_s", "vault": "vault_s"})
PRIMER_SKIN = dict(_ref.PRIMER_SKIN)
PRIMER_SKIN.update({"glass": "dark", "glass_lit": "dark", "asphalt": "raw", "beacon": "dark", "steel2": "primer", "vault": "primer",
                    "red": "primer", "orange": "primer"})
beam, post, spark, scorch, scaffold, crane = _ref.beam, _ref.post, _ref.spark, _ref.scorch, _ref.scaffold, _ref.crane
site_clutter = _ref.site_clutter


# ====================================================================================================== pieces
def pad_and_runway(S, dmg, active, i):
    S.bb("pad", -5.0, 5.0, -5.0, 5.0, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    # runway strip with edge lines, dashed centre line, threshold bars and an arrowed apron link to the hangar doors
    S.bb("rwy", -5.0, 5.0, RWY_Y0, RWY_Y1, 0.14, RWY_Z, "asphalt", 0.0)
    S.bb("edgeN", -5.0, 5.0, RWY_Y1 - 0.09, RWY_Y1, RWY_Z, RWY_Z + 0.02, "white", 0.0, nocast=True)
    S.bb("edgeS", -5.0, 5.0, RWY_Y0 + 0.02, RWY_Y0 + 0.11, RWY_Z, RWY_Z + 0.02, "white", 0.0, nocast=True)
    for k in range(7):
        S.bb(f"dash{k}", -4.5 + k * 1.45, -3.7 + k * 1.45, -3.43, -3.27, RWY_Z, RWY_Z + 0.02, "white", 0.0, nocast=True)
    for k in range(5):                         # threshold piano keys at the east end
        S.bb(f"thr{k}", 4.0, 4.7, RWY_Y0 + 0.3 + k * 0.5, RWY_Y0 + 0.5 + k * 0.5, RWY_Z, RWY_Z + 0.02, "white", 0.0, nocast=True)
    # team-colour apron marking: a big pad where the aircraft stops, on the run from the doors to the runway
    S.bb("taxi", -3.9, -1.0, RWY_Y1, HY0, 0.14, 0.17, "concrete2", 0.0)
    S.bb("apronhouse", -3.7, -1.4, -1.9, -1.35, 0.17, 0.19, "house2", 0.0)
    S.bb("apronframe", -3.8, -1.3, -2.0, -1.25, 0.17, 0.18, "yellow", 0.0, nocast=True)
    for k in range(3):
        S.bb(f"hold{k}", -3.2 + k * 0.7, -2.8 + k * 0.7, -2.2, -2.05, 0.17, 0.2, "yellow", 0.0, nocast=True)
    # runway edge lights (chase in the active state)
    for k in range(8):
        on = active and ((k + i) % 4 == 0) or (not active and not dmg and k % 4 == i % 4)
        S.cyl(f"rl{k}", 0.08, 0.1, (-4.5 + k * 1.3, RWY_Y1 + 0.28, 0.22), "beacon" if on else "dark", verts=8, nocast=True)
    if dmg:
        for k, (x, y, w) in enumerate(((1.0, -3.2, 1.1), (2.6, -3.9, 0.8), (-1.2, -4.2, 0.6))):
            S.box(f"crater{k}", (w, w * 0.7, 0.06), (x, y, RWY_Z + 0.03), "char", 0.0, rot=(0, 0, 20 * k), nocast=True)
            for j in range(4):
                S.box(f"clump{k}{j}", (0.25, 0.2, 0.2), (x + 0.45 * math.cos(1.6 * j), y + 0.3 * math.sin(1.6 * j), RWY_Z + 0.1), "asphalt_s", 0.0, rot=(0, 0, 30 * j))


def hangar(S, dmg, active, i):
    wl = HX0
    # walls + plinth
    S.bb("hwall", HX0, HX1, HY0, HY1, 0.14, WALL_H, "concrete", 0.04)
    S.bb("hplinth", HX0 - 0.08, HX1 + 0.08, HY0 - 0.08, HY1 + 0.08, 0.14, 0.45, "concrete2", 0.03)
    # barrel vault: a long cylinder (axis y) whose lower half hides inside the wall block
    ln = HY1 - HY0 + 0.3
    if not dmg:
        S.cyl("vault", VR, ln, (VCX, (HY0 + HY1) / 2, VZ), "vault", axis="Y", verts=28)
    else:
        # caved in: the south third and the north end stand, the middle is a black void with bent ribs
        S.cyl("vaultS", VR, 1.3, (VCX, HY0 + 0.65 - 0.1, VZ), "vault", axis="Y", verts=28)
        S.cyl("vaultN", VR, 1.2, (VCX, HY1 - 0.5, VZ), "vault", axis="Y", verts=28)
        S.bb("void", HX0 + 0.5, HX1 - 0.5, HY0 + 1.2, HY1 - 1.1, WALL_H - 0.02, WALL_H + 0.01, "dark", 0.0)
        for k, (a, b) in enumerate((((-4.3, 1.2), (-3.2, 2.6)), ((-2.0, 1.6), (-1.5, 2.9)), ((0.5, 1.3), (-0.4, 2.7)))):
            beam(S, f"bent{k}", (a[0], a[1], WALL_H), (b[0], b[1], VZ + 1.0 - 0.3 * k), 0.12, "girder")
        S.box("slabV", (1.6, 1.0, 0.12), (-1.9, 2.1 * S.YS, VZ + 0.4), "vault_s", 0.0, rot=(-22, 0, 14), stretch=False)
        S.box("slabV2", (1.1, 0.8, 0.12), (-3.4, 3.1 * S.YS, WALL_H + 0.3), "vault_s", 0.0, rot=(30, 0, -20), stretch=False)
        for k in range(4):
            S.box(f"deb{k}", (0.3, 0.25, 0.2), (-4.0 + 1.2 * k, (1.6 + 0.4 * (k % 2)) * S.YS, WALL_H + 0.1), "concrete_s", 0.0, rot=(0, 0, 25 * k))
    # rib rings + team bands across the arch (house colour reads on the curve)
    rings = (0.0, 0.0, 0.0) if False else None
    for k, yy in enumerate((HY0 + 0.45, HY0 + 1.5, HY0 + 2.55, HY0 + 3.6)):
        if dmg and 1.1 < yy - HY0 < 3.2:
            continue
        S.cyl(f"rib{k}", VR + 0.07, 0.14, (VCX, yy, VZ), "concrete2", axis="Y", verts=28)
    for k, yy in enumerate((HY0 + 0.95, HY0 + 3.1)):
        if dmg and 1.1 < yy - HY0 < 3.2:
            continue
        S.cyl(f"vband{k}", VR + 0.05, 0.75, (VCX, yy, VZ), "house", axis="Y", verts=28)
    # south gable: door frame, roller door, house panels either side, signboard
    yw = HY0 - 0.04
    dw = 1.35
    S.bb("bay", VCX - dw, VCX + dw, yw - 0.06, yw + 0.12, 0.14, 1.9, "dark", 0.0)
    S.bb("lintel", VCX - dw - 0.2, VCX + dw + 0.2, yw - 0.14, yw + 0.12, 1.9, 2.25, "concrete2", 0.03)
    S.bb("lintelhaz", VCX - dw - 0.15, VCX + dw + 0.15, yw - 0.18, yw - 0.12, 1.96, 2.18, "yellow", 0.0, nocast=True)
    for sx in (-1, 1):
        S.bb(f"jamb{sx}", VCX + sx * (dw + 0.1) - 0.1, VCX + sx * (dw + 0.1) + 0.1, yw - 0.14, yw + 0.12, 0.14, 1.95, "concrete2", 0.03)
        cx = VCX + sx * (dw + 0.78)
        S.bb(f"pframe{sx}", cx - 0.5, cx + 0.5, yw - 0.07, yw + 0.02, 0.5, 2.15, "dark", 0.0)
        if dmg and sx > 0:
            S.bb(f"panel{sx}", cx - 0.38, cx - 0.05, yw - 0.12, yw - 0.06, 0.6, 2.0, "house", 0.0)
        else:
            S.bb(f"panel{sx}", cx - 0.4, cx + 0.4, yw - 0.12, yw - 0.06, 0.6, 2.05, "house", 0.0)
    if active:
        S.bb("slatroll", VCX - dw, VCX + dw, yw - 0.16, yw - 0.04, 1.78, 1.9, "steel", 0.0)
        for k, lx in enumerate((-0.8, 0.0, 0.8)):
            on = (i + k) % 3 != 2
            S.bb(f"wl{k}", VCX + lx - 0.25, VCX + lx + 0.25, yw - 0.05, yw - 0.01, 1.55, 1.7, "lamp" if on else "dark", 0.0, nocast=True)
        S.bb("glow", VCX - 1.1, VCX + 1.1, yw - 1.1, yw - 0.1, 0.17, 0.19, "yellow", 0.0, nocast=True)
    elif dmg:
        S.bb("slatL", VCX - dw, VCX + 0.1, yw - 0.14, yw - 0.06, 0.14, 1.9, "steel", 0.0)
        S.box("slatR", (1.3, 0.08, 1.0), (VCX + 0.75, yw - 0.4, 1.2), "steel", 0.0, rot=(20, 0, 12), stretch=False)
        S.box("slatR2", (0.8, 0.08, 0.5), (VCX + 0.9, yw - 0.85, 0.5), "steel_s", 0.0, rot=(60, 0, -20), stretch=False)
    else:
        S.bb("slats", VCX - dw, VCX + dw, yw - 0.14, yw - 0.06, 0.14, 1.9, "steel", 0.0)
        for k in range(6):
            S.bb(f"groove{k}", VCX - dw, VCX + dw, yw - 0.15, yw - 0.14, 0.3 + k * 0.27, 0.35 + k * 0.27, "dark", 0.0, nocast=True)
    for k, lx in enumerate((-1.3, 1.3)):
        on = active and (i % 2 == k)
        S.cyl(f"beac{k}", 0.13, 0.16, (VCX + lx, yw - 0.22, 2.4), "beacon" if on else "dark", verts=10, nocast=True)
    # stencilled plate and hangar number
    S.bb("gplate", VCX - 0.5, VCX + 0.5, yw - 0.24, yw - 0.18, 2.3, 2.55, "dark", 0.0)
    for k in range(3):
        S.bb(f"gnum{k}", VCX - 0.35 + k * 0.25, VCX - 0.23 + k * 0.25, yw - 0.26, yw - 0.24, 2.34, 2.5, "white", 0.0, nocast=True)
    for k, (rx, rw, rh) in enumerate(((HX0 + 0.6, 0.15, 0.7), (HX1 - 0.9, 0.14, 0.5))):
        S.bb(f"streak{k}", rx, rx + rw, yw - 0.1, yw - 0.08, 1.5 - rh, 1.5, "rust", 0.0, nocast=True)
    if dmg:
        S.bb("breach", HX0 + 0.2, HX0 + 1.2, yw - 0.08, yw - 0.04, 0.5, 1.45, "dark", 0.0)
        scorch(S, "scH", HX0 + 0.8, yw - 0.1, 1.0, 1.4, 0.9)
        scorch(S, "scR", VCX + 2.0, yw - 0.1, 1.0, 1.2, 0.8)


def hut(S, dmg, active, i):
    cx, cy = HUT
    S.bb("hutb", cx - 1.1, cx + 1.1, cy - 0.9, cy + 0.9, 0.14, 1.6, "concrete", 0.04)
    S.bb("hutpl", cx - 1.18, cx + 1.18, cy - 0.98, cy + 0.98, 0.14, 0.4, "concrete2", 0.03)
    S.bb("hutslab", cx - 1.3, cx + 1.3, cy - 1.1, cy + 1.1, 1.6, 1.8, "concrete2", 0.03)
    # house band round the top of the hut + door
    S.bb("hutband", cx - 1.12, cx + 1.12, cy - 0.92 - 0.04, cy - 0.9, 0.9, 1.5, "house", 0.0)
    S.bb("hutdoor", cx - 0.9, cx - 0.4, cy - 0.94, cy - 0.9, 0.14, 1.0, "dark", 0.0)
    for k in range(3):
        S.bb(f"hwin{k}", cx - 0.1 + k * 0.35, cx + 0.15 + k * 0.35, cy - 0.93, cy - 0.9, 0.9, 1.3, "glass_lit" if not dmg else "glass", 0.0)
    if dmg:
        S.bb("hbreach", cx + 0.2, cx + 1.1, cy - 0.94, cy - 0.9, 0.4, 1.5, "dark", 0.0)
        S.box("cab_broken", (1.5, 1.1, 0.5), (cx - 0.1, cy * S.YS, 2.0), "glass_s", 0.0, rot=(0, 0, 8), stretch=False)
        post(S, "cabp", cx - 0.8, cy - 0.5, 1.8, 2.4, 0.1, "girder")
        post(S, "cabp", cx + 0.6, cy + 0.3, 1.8, 2.1, 0.1, "girder")
        scorch(S, "scHut", cx + 0.1, cy - 0.97, 1.0, 1.6, 1.0)
        S.bb("hutrubble", cx - 1.2, cx + 0.6, cy - 1.6, cy - 1.1, 0.14, 0.4, "concrete_s", 0.2)
        beam(S, "hutsnap", (cx + 0.9, cy - 0.2, 1.8), (cx + 1.5, cy - 1.2, 0.3), 0.12, "girder")
        return
    # glazed cab on top with a hip roof
    S.bb("cab", cx - 0.9, cx + 0.9, cy - 0.7, cy + 0.7, 1.8, 2.55, "glass_lit" if not dmg else "glass", 0.0)
    for k in range(4):
        S.bb(f"cmull{k}", cx - 0.9 + k * 0.6, cx - 0.86 + k * 0.6, cy - 0.74, cy - 0.7, 1.8, 2.55, "steel", 0.0, nocast=True)
    S.bb("cabroof", cx - 1.15, cx + 1.15, cy - 0.95, cy + 0.95, 2.55, 2.72, "roof", 0.04)
    S.bb("cabhouse", cx - 0.9, cx + 0.9, cy - 0.97, cy - 0.7, 2.58, 2.76, "house", 0.0)
    # beacon on a stub (rotates; lit in active), radar paddle
    post(S, "bstub", cx + 0.7, cy + 0.55, 2.72, 3.1, 0.1, "steel")
    beacon_on = active or (i % 2 == 0)
    S.cyl("bhead", 0.2, 0.24, (cx + 0.7, cy + 0.55, 3.2), "beacon" if beacon_on else "dark", verts=10, nocast=True)
    a = i * math.pi / 2
    S.bb("bsweep", cx + 0.7 + 0.28 * math.cos(a) - 0.08, cx + 0.7 + 0.28 * math.cos(a) + 0.08, cy + 0.55 - 0.08, cy + 0.55 + 0.08, 3.15, 3.25, "lamp" if beacon_on else "dark", 0.0, nocast=True)
    post(S, "radm", cx - 0.6, cy + 0.2, 2.72, 3.0, 0.1, "steel")
    ang = (i * 25) % 360
    S.box("dish", (0.9, 0.08, 0.5), (cx - 0.6, cy * S.YS + 0.2, 3.15), "steel2", 0.0, rot=(0, 0, ang), stretch=False)
    # hut ladder + rain pipe
    S.bb("pipe", cx + 1.0, cx + 1.1, cy - 0.97, cy - 0.9, 0.14, 1.6, "steel", 0.0)


def mast_and_extras(S, dmg, active, i):
    mx, my = MAST
    top = 2.4 if not dmg else 1.0
    post(S, "mast", mx, my, 0.14, top, 0.12, "steel")
    S.cyl("mastbase", 0.22, 0.2, (mx, my, 0.24), "concrete2", verts=10)
    if not dmg:
        sway = math.sin(i * math.pi / 2) * 0.12
        # windsock: orange/white tapered segments blown toward the west
        for k in range(4):
            r = 0.2 - 0.035 * k
            S.cyl(f"sock{k}", r, 0.26, (mx - 0.15 - 0.26 * k - 0.1 * sway, my + sway * k, 2.3 - 0.03 * k), "orange" if k % 2 == 0 else "white", axis="X", verts=10, nocast=True)
        S.cyl("sockring", 0.22, 0.04, (mx - 0.1, my, 2.32), "steel", axis="X", verts=10, nocast=True)
    else:
        beam(S, "mastsnap", (mx, my, 1.0), (mx - 0.9, my - 0.5, 0.3), 0.1, "steel")
        S.bb("sockrag", mx - 1.1, mx - 0.7, my - 0.7, my - 0.4, 0.14, 0.18, "orange", 0.0, nocast=True)
    # fuel bowser on the apron and a tug
    S.cyl("tank", 0.3, 1.5, (3.9, -1.4, 0.5), "steel", axis="X", verts=14)
    S.bb("tankcr", 3.1, 4.7, -1.55, -1.25, 0.14, 0.24, "concrete2", 0.02)
    S.bb("tankband", 3.55, 3.7, -1.75, -1.05, 0.3, 0.8, "house", 0.0)
    S.bb("crate", 1.4, 1.9, -0.9, -0.4, 0.14, 0.55, "crate", 0.03)
    S.bb("crate2", 2.0, 2.5, -1.0, -0.5, 0.14, 0.4, "crate", 0.03, rot=(0, 0, 14))


def finished(S, i, dmg=False, active=False):
    if dmg:
        S.skin.update(SOOT_SKIN)
    pad_and_runway(S, dmg, active, i)
    hangar(S, dmg, active, i)
    hut(S, dmg, active, i)
    mast_and_extras(S, dmg, active, i)


# ====================================================================================================== build stages
def site_slab(S, top=0.5):
    S.bb("slab", HX0, HX1, HY0, HY1, 0.14, top, "raw", 0.02)
    S.bb("form", HX0, HX1, HY0 - 0.06, HY0, 0.14, top + 0.15, "timber", 0.0)


def runway_raw(S, done):
    """Runway strip: bare earth with kerb forms (done=0), sub-base (1), asphalt strip without markings (2)."""
    S.bb("pad", -5.0, 5.0, -5.0, 5.0, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    if done == 0:
        S.bb("rwysoil", -5.0, 5.0, RWY_Y0, RWY_Y1, 0.14, 0.17, "soil", 0.0)
        S.bb("rwysoil2", -5.0, 5.0, RWY_Y1 - 0.2, RWY_Y1, 0.14, 0.2, "soil2", 0.0)
    elif done == 1:
        S.bb("rwybase", -5.0, 5.0, RWY_Y0, RWY_Y1, 0.14, 0.18, "raw", 0.0)
        S.bb("rwyform", -5.0, 5.0, RWY_Y1, RWY_Y1 + 0.06, 0.14, 0.3, "timber", 0.0)
    else:
        S.bb("rwy", -5.0, 5.0, RWY_Y0, RWY_Y1, 0.14, RWY_Z, "asphalt", 0.0)
        S.bb("rwyform", -5.0, 5.0, RWY_Y1, RWY_Y1 + 0.06, 0.14, 0.3, "timber", 0.0)


def hangar_ribs(S, upto, deck):
    """Steel arch ribs of the vault: posts and a few rafter segments approximating the arc; `deck` ribs get primed cladding."""
    for k, yy in enumerate((HY0 + 0.3, HY0 + 1.4, HY0 + 2.5, HY0 + 3.6)):
        if k >= upto:
            break
        for sx in (HX0 + 0.1, HX1 - 0.1):
            post(S, "arch", sx, yy, 0.5, WALL_H + 0.1, 0.14, "girder")
        n = 7
        for j in range(n):
            a0 = math.pi * j / n
            a1 = math.pi * (j + 1) / n
            p0 = (VCX - VR * math.cos(a0) * 0.97, yy, VZ + VR * math.sin(a0) * 0.97)
            p1 = (VCX - VR * math.cos(a1) * 0.97, yy, VZ + VR * math.sin(a1) * 0.97)
            beam(S, "arch", p0, p1, 0.12, "girder")
    if upto > 1:
        for sx in (HX0 + 0.1, HX1 - 0.1):
            beam(S, "purlin", (sx, HY0 + 0.3, WALL_H + 0.1), (sx, HY0 + 0.3 + 1.1 * (upto - 1), WALL_H + 0.1), 0.1, "girder")
        beam(S, "ridge", (VCX, HY0 + 0.3, VZ + VR * 0.97), (VCX, HY0 + 0.3 + 1.1 * (upto - 1), VZ + VR * 0.97), 0.1, "girder")
    if deck > 0:
        S.cyl("vaultprimed", VR, 1.1 * deck + 0.2, (VCX, HY0 + 0.3 + 0.55 * deck, VZ), "primer", axis="Y", verts=24)


def build_site(S, i):
    runway_raw(S, 0 if i == 0 else (1 if i == 1 else 2))
    if i == 0:       # surveyed plot: dug foundation for the hangar, hut pad, stakes and string lines
        S.bb("pit", HX0, HX1, HY0, HY1, 0.14, 0.17, "soil", 0.0)
        S.bb("pit2", HX0, HX1, HY0, HY0 + 0.35, 0.14, 0.2, "soil2", 0.0)
        S.bb("pit2", HX0, HX0 + 0.35, HY0, HY1, 0.14, 0.2, "soil2", 0.0)
        S.bb("pit2", HX1 - 0.35, HX1, HY0, HY1, 0.14, 0.2, "soil2", 0.0)
        S.bb("hutpit", HUT[0] - 1.2, HUT[0] + 1.2, HUT[1] - 1.0, HUT[1] + 1.0, 0.14, 0.17, "soil", 0.0)
        for (mx, my, mw, mh) in ((-5.0, 2.0, 0.5, 2.8), (2.0, 4.5, 3.0, 0.5), (1.2, -0.1, 0.6, 1.6)):
            S.bb("mound", mx - mw / 2, mx + mw / 2, my - mh / 2, my + mh / 2, 0.14, 0.5, "soil", 0.18)
        for (sx, sy) in ((HX0 - 0.15, HY0 - 0.15), (HX1 + 0.15, HY0 - 0.15), (HX0 - 0.15, HY1 + 0.15), (HX1 + 0.15, HY1 + 0.15),
                         (HUT[0] - 1.35, HUT[1] - 1.2), (HUT[0] + 1.35, HUT[1] + 1.2)):
            post(S, "stake", sx, sy, 0.14, 1.0, 0.1, "timber")
            post(S, "staket", sx, sy, 0.8, 1.0, 0.12, "string")
        S.bb("line", HX0 - 0.17, HX1 + 0.17, HY0 - 0.17, HY0 - 0.13, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("line", HX0 - 0.17, HX1 + 0.17, HY1 + 0.13, HY1 + 0.17, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("sign", 2.2, 3.4, -1.7, -1.6, 0.9, 1.7, "white", 0.0)
        post(S, "signp", 2.35, -1.65, 0.14, 0.9, 0.08, "timber")
        post(S, "signp", 3.25, -1.65, 0.14, 0.9, 0.08, "timber")
        S.bb("signs", 2.3, 3.3, -1.72, -1.68, 1.2, 1.5, "yellow", 0.0)
    elif i == 1:     # poured hangar slab and hut slab, rebar stubs, forms, mixer
        site_slab(S, 0.5)
        S.bb("hutslab0", HUT[0] - 1.3, HUT[0] + 1.3, HUT[1] - 1.1, HUT[1] + 1.1, 0.14, 0.5, "raw", 0.02)
        for sx in (HX0 + 0.1, HX1 - 0.1):
            for yy in (HY0 + 0.3, HY0 + 1.4, HY0 + 2.5, HY0 + 3.6):
                post(S, "stub", sx, yy, 0.5, 1.3, 0.16, "girder")
                post(S, "rb", sx + 0.2, yy, 0.5, 1.1, 0.05, "rebar")
        S.cyl("mixer", 0.38, 0.8, (-1.0, -1.2, 0.7), "primer", verts=14)
        S.bb("mixer", -1.35, -0.65, -1.5, -0.9, 0.14, 0.4, "girder", 0.0)
        S.bb("sandpile", 1.7, 3.3, 3.8, 4.8, 0.14, 0.7, "soil", 0.3)
    elif i == 2:     # first arch ribs, hut walls rising, taxi apron concrete poured, scaffold
        site_slab(S, 0.5)
        hangar_ribs(S, 2, 0)
        S.bb("hutw", HUT[0] - 1.1, HUT[0] + 1.1, HUT[1] - 0.9, HUT[1] + 0.9, 0.5, 1.2, "raw", 0.03)
        S.bb("taxi0", -3.9, -1.0, RWY_Y1, HY0, 0.14, 0.17, "concrete2", 0.0)
        scaffold(S, -3.9, -2.6, -2.0, -1.4, (0.8, 1.7))
        spark(S, -3.4, -1.0, 1.5, i, 1)
    elif i == 3:     # all four arches, hut walls to cab level, door header
        site_slab(S, 0.5)
        hangar_ribs(S, 4, 0)
        S.bb("hutw", HUT[0] - 1.1, HUT[0] + 1.1, HUT[1] - 0.9, HUT[1] + 0.9, 0.5, 1.7, "raw", 0.03)
        S.bb("hutslab1", HUT[0] - 1.3, HUT[0] + 1.3, HUT[1] - 1.1, HUT[1] + 1.1, 1.7, 1.85, "primer", 0.03)
        S.bb("taxi0", -3.9, -1.0, RWY_Y1, HY0, 0.14, 0.17, "concrete2", 0.0)
        for sx in (-1, 1):
            post(S, "dpost", VCX + sx * 1.5, HY0 - 0.05, 0.5, 2.0, 0.2, "yellow")
        beam(S, "dhead", (VCX - 1.5, HY0 - 0.05, 2.0), (VCX + 1.5, HY0 - 0.05, 2.0), 0.2, "yellow")
        scaffold(S, -3.9, -2.6, -2.0, -1.4, (0.8, 1.7))
        spark(S, VCX, HY0, 2.05, i, 3)
        crane(S, "girder")
    elif i == 4:     # vault half clad, cab frame on the hut, runway kerb lights in
        site_slab(S, 0.5)
        hangar_ribs(S, 4, 2)
        S.bb("hutw", HUT[0] - 1.1, HUT[0] + 1.1, HUT[1] - 0.9, HUT[1] + 0.9, 0.5, 1.7, "concrete", 0.03)
        S.bb("hutslab1", HUT[0] - 1.3, HUT[0] + 1.3, HUT[1] - 1.1, HUT[1] + 1.1, 1.7, 1.85, "concrete2", 0.03)
        for sx in (-1, 1):
            for sy in (-1, 1):
                post(S, "cabp", HUT[0] + sx * 0.85, HUT[1] + sy * 0.65, 1.85, 2.6, 0.1, "girder")
        S.bb("taxi0", -3.9, -1.0, RWY_Y1, HY0, 0.14, 0.17, "concrete2", 0.0)
        for k in range(8):
            S.cyl(f"rl{k}", 0.08, 0.1, (-4.5 + k * 1.3, RWY_Y1 + 0.28, 0.22), "dark", verts=8, nocast=True)
        spark(S, 0.2, 1.0, 1.7, i, 2)
        S.bb("planks", -2.8, -1.2, -1.8, -1.45, 0.14, 0.3, "timber", 0.0)
    elif i == 5:     # vault clad, gable wall going up, cab glazed, mast base set
        site_slab(S, 0.5)
        S.bb("hwall", HX0, HX1, HY0, HY1, 0.14, 0.95, "raw", 0.04)
        hangar_ribs(S, 4, 4)
        S.bb("hutw", HUT[0] - 1.1, HUT[0] + 1.1, HUT[1] - 0.9, HUT[1] + 0.9, 0.14, 1.6, "concrete", 0.04)
        S.bb("hutslab1", HUT[0] - 1.3, HUT[0] + 1.3, HUT[1] - 1.1, HUT[1] + 1.1, 1.6, 1.8, "concrete2", 0.03)
        S.bb("cab", HUT[0] - 0.9, HUT[0] + 0.9, HUT[1] - 0.7, HUT[1] + 0.7, 1.8, 2.55, "dark", 0.0)
        S.cyl("mastbase", 0.22, 0.2, (MAST[0], MAST[1], 0.24), "concrete2", verts=10)
        S.bb("taxi", -3.9, -1.0, RWY_Y1, HY0, 0.14, 0.17, "concrete2", 0.0)
        scaffold(S, -3.9, -2.6, -2.0, -1.4, (0.8, 1.7), top=2.2)
        spark(S, HUT[0], HUT[1] - 0.9, 2.3, i, 4)
    site_clutter(S, i if i < 5 else 9)


def build(S, state, i, n):
    building = state == "build"
    if building and i < 6:
        build_site(S, i)
        return
    dmg = state == "damaged"
    active = state == "active"
    ia = i if not building else 0
    if building and i == 6:          # almost done: the finished model unpainted (primer), no stencils; scaffold coming down
        S.skin.update(PRIMER_SKIN)
        S.hide = lambda nm: nm.startswith(("gnum", "dash", "thr", "hold", "apronhouse", "apronframe"))
    finished(S, ia, dmg, active)
    if building and i == 6:
        scaffold(S, -3.9, -2.6, -2.0, -1.4, (0.8, 1.7))
        S.bb("planks", -2.8, -1.2, -1.8, -1.45, 0.14, 0.3, "timber", 0.0)
        S.bb("crate2", 2.5, 3.3, -4.6, -4.0, 0.14, 0.8, "crate", 0.03)
        spark(S, -3.4, -0.9, 1.6, 3, 6)
