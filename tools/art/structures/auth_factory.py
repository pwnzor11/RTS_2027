"""Authority Vehicle Factory: a war-factory hall (3x3 cells). Original design, no franchise references.
Contract: see auth_refinery.py (this module imports its helpers and materials, so keep the two side by side).

Sawtooth roof (glazed faces toward the camera), big roller door on the south (exit) face with hazard stripes, an
overhead crane rail on end pylons with a trolley that travels, a test-track apron in front of the door, a chimney.
States: idle (4: door lamps and crane trolley), damaged (4, STRUCTURAL only: caved-in roof teeth, breach in the wall,
jammed door, collapsed crane leg, broken chimney; fire/smoke are separate effects, see FX), build (8 stages, same
recipe as the Refinery), active (4: roller door open, work lights and beacons; the renderer plays it while a vehicle
is produced).
"""
import importlib.util
import math
import os

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structures_auth_refinery_ref", os.path.join(_here, "auth_refinery.py"))
_ref = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_ref)

ID = "auth_factory"
TITLE = "Authority Vehicle Factory"
FOOTPRINT = (3, 3)
CANVAS = (192, 224)
ORIGIN = (0, 32)        # footprint top-left corner inside the frame (32 px of headroom for the chimney and crane)
DOCK = (1, 3)           # exit: the cell just below the middle of the footprint (rules: factory exit below the middle)
CAP_MATERIAL = "concrete2"
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5), ("active", 4, 3)]

HALL_X = 4.1
HALL_Y0, HALL_Y1 = -2.6, 2.2
WALL_H = 3.3
ROOF_T = 0.2
ZR = WALL_H + ROOF_T            # roof deck top
FLAT_Y1 = -1.45                 # the flat strip of roof along the south edge ends here; the sawtooth starts
TOOTH_W = (HALL_Y1 + 0.2 - FLAT_Y1) / 3
TOOTH_H = 0.8
CHIM = (3.35, 1.55)
RAIL_Y = -0.35
RAIL_Z = ZR + 1.85
Z_DOOR = 2.55


def px(x, y, z):
    """Frame pixel of a game-unit point (same mapping as the Refinery's FX note)."""
    return round(96 + x * 18.8), round(128 - y * 18.8 - z * 14.4)


# Effect anchors (D-069), pixels in frame space: the effect's base-centre lands here.
FX = [
    dict(state="damaged", kind="fire_large", x=px(1.5, 0.2, ZR - 0.1)[0], y=px(1.5, 0.2, ZR - 0.1)[1], phase=0),   # collapsed roof teeth
    dict(state="damaged", kind="fire_small", x=px(0.4, -1.9, ZR)[0], y=px(0.4, -1.9, ZR)[1], phase=3),            # roof edge beside it
    dict(state="damaged", kind="fire_small", x=px(3.0, -2.75, 1.2)[0], y=px(3.0, -2.75, 1.2)[1], phase=5),        # wall breach
    dict(state="damaged", kind="fire_small", x=px(CHIM[0] - 0.3, CHIM[1], ZR + 1.2)[0], y=px(CHIM[0] - 0.3, CHIM[1], ZR + 1.2)[1], phase=1),
    dict(state="damaged", kind="smoke_plume", x=px(1.5, 0.2, ZR)[0], y=px(1.5, 0.2, ZR)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=px(CHIM[0] - 0.3, CHIM[1], ZR + 1.3)[0], y=px(CHIM[0] - 0.3, CHIM[1], ZR + 1.3)[1], phase=6),
    dict(state="damaged", kind="smoke_plume", x=px(3.0, -2.75, 2.8)[0], y=px(3.0, -2.75, 2.8)[1], phase=3),
]

MATERIALS = dict(_ref.MATERIALS)
MATERIALS.update({
    "glass": ("#A9B8B4", dict(rough=0.25)),
    "glass_lit": ("#D8E6DC", dict(rough=0.3, emit="#BFD6C8")),
    "asphalt": ("#3A3F44", dict(rough=1.0)),
    "steel2": ("#979EA3", dict(rough=0.4, metal=0.5)),
    "beacon": ("#FFB347", dict(rough=0.3, emit="#FFB347")),
    "glass_s": ("#6B7774", dict(rough=0.3)),
    "asphalt_s": ("#2C3033", dict(rough=1.0)),
})
SOOT_SKIN = dict(_ref.SOOT_SKIN)
SOOT_SKIN.update({"glass": "glass_s", "asphalt": "asphalt_s"})
PRIMER_SKIN = dict(_ref.PRIMER_SKIN)
PRIMER_SKIN.update({"glass": "dark", "glass_lit": "dark", "asphalt": "raw", "beacon": "dark", "steel2": "primer"})
beam, post, spark, scorch, scaffold, crane = _ref.beam, _ref.post, _ref.spark, _ref.scorch, _ref.scaffold, _ref.crane
hall_frame, cladding, site_clutter = _ref.hall_frame, _ref.cladding, _ref.site_clutter
COLX, COLY = _ref.COLX, _ref.COLY


def slab_beam(S, name, y0, y1, z0, z1, x0, x1, m, t=0.14, nocast=False):
    """Sloped slab from (y0, z0) to (y1, z1) (ground y, so it is stretched like everything else), spanning x0..x1."""
    YS = S.YS
    dy, dz = (y1 - y0) * YS, z1 - z0
    ln = math.hypot(dy, dz)
    ang = math.degrees(math.atan2(dz, dy))
    return S.box(name, (x1 - x0, ln, t), ((x0 + x1) / 2, (y0 + y1) / 2 * YS, (z0 + z1) / 2), m, 0.0, rot=(ang, 0, 0), stretch=False, nocast=nocast)


def hazard_x(S, x0, x1, y0, y1, z, w=0.4):
    n = int(round((x1 - x0) / w))
    for k in range(n):
        S.bb(f"haz{k}", x0 + k * (x1 - x0) / n, x0 + (k + 1) * (x1 - x0) / n, y0, y1, z, z + 0.03, "yellow" if k % 2 == 0 else "dark", 0.0, nocast=True)


def hazard_z(S, x0, x1, y, z0, z1, h=0.3):
    n = int(round((z1 - z0) / h))
    for k in range(n):
        S.bb(f"hazz{k}", x0, x1, y - 0.04, y + 0.04, z0 + k * (z1 - z0) / n, z0 + (k + 1) * (z1 - z0) / n, "yellow" if k % 2 == 0 else "dark", 0.0)


# ====================================================================================================== finished model
def apron(S, active, i):
    S.bb("pad", -5.0, 5.0, -5.0, 5.0, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    # test-track apron: asphalt lane in front of the door with centre dashes, side lines and turning arrows
    S.bb("asph", -4.6, 4.6, -4.8, -2.85, 0.14, 0.17, "asphalt", 0.0)
    S.bb("edgeA", -4.6, 4.6, -4.8, -4.72, 0.17, 0.19, "yellow", 0.0, nocast=True)
    for k in range(7):
        S.bb(f"dash{k}", -4.2 + k * 1.4, -3.55 + k * 1.4, -3.9, -3.82, 0.17, 0.19, "white", 0.0, nocast=True)
    for sx in (-1, 1):
        S.bb("guide", sx * 1.75 - 0.07, sx * 1.75 + 0.07, -4.1, -2.9, 0.17, 0.2, "yellow", 0.0, nocast=True)
        for k in range(3):    # chevrons pointing at the door
            S.bb("chev", sx * (2.6 + 0.5 * k) - 0.2, sx * (2.6 + 0.5 * k) + 0.2, -4.35, -4.28, 0.17, 0.19, "white", 0.0, nocast=True)
    hazard_x(S, -1.95, 1.95, -3.2, -2.85, 0.17, 0.39)


def work_lights(S, lit, dmg=False):
    for sx in (-1, 1):
        x = sx * 2.45
        post(S, "lpole", x, -3.3, 0.17, 2.6, 0.1, "steel")
        S.bb("lhead", x - 0.28, x + 0.28, -3.42, -3.18, 2.6, 2.75, "steel", 0.0)
        S.bb("lamp", x - 0.22, x + 0.22, -3.46, -3.42, 2.62, 2.72, "lamp" if lit else "dark", 0.0, nocast=True)


def door(S, yw, state_open, dmg, i):
    zb = 0.14
    S.bb("bay", -1.7, 1.7, yw - 0.06, yw + 0.1, zb, Z_DOOR + 0.05, "dark", 0.0)
    S.bb("lintel", -2.0, 2.0, yw - 0.16, yw + 0.1, Z_DOOR + 0.05, 3.05, "concrete2", 0.04)
    S.bb("housing", -1.75, 1.75, yw - 0.3, yw - 0.12, 2.35, 2.95, "steel", 0.03)          # roller drum housing above the door
    for sx in (-1, 1):
        hazard_z(S, sx * 1.8 - 0.14, sx * 1.8 + 0.14, yw - 0.1, zb, Z_DOOR + 0.05)
        S.bb("jamb", sx * 1.95 - 0.1, sx * 1.95 + 0.1, yw - 0.12, yw + 0.1, zb, Z_DOOR + 0.05, "concrete2", 0.03)
    S.bb("hzstrip", -1.95, 1.95, yw - 0.2, yw - 0.14, 2.72, 3.0, "dark", 0.0, nocast=True)
    for k in range(12):
        S.bb(f"hzv{k}", -1.95 + k * 0.325, -1.95 + (k + 1) * 0.325, yw - 0.22, yw - 0.19, 2.74, 2.98, "yellow" if k % 2 == 0 else "dark", 0.0, nocast=True)
    # roller door slats
    zt = 2.3
    if state_open:
        # rolled up: a thin bundle in the housing, the bay is a dark cavity with work lights on its ceiling and back wall
        S.bb("slatroll", -1.6, 1.6, yw - 0.14, yw - 0.06, 2.3, 2.42, "steel", 0.0)
        for k, lx in enumerate((-1.05, 0.0, 1.05)):
            on = (i + k) % 3 != 2
            S.bb(f"wl{k}", lx - 0.3, lx + 0.3, yw - 0.07, yw - 0.03, 2.05, 2.2, "lamp", 0.0, nocast=True)
        S.bb("glow", -1.2, 1.2, yw - 0.9, yw - 0.1, 0.17, 0.19, "yellow", 0.0, nocast=True)
        for sx in (-1, 1):
            S.bb("rail", sx * 1.55 - 0.05, sx * 1.55 + 0.05, yw - 0.12, yw - 0.04, zb, 2.3, "steel2", 0.0, nocast=True)
    elif dmg:
        # jammed: the left part of the door still down, the right part buckled and hanging by one side
        S.bb("slatL", -1.6, 0.1, yw - 0.14, yw - 0.06, zb, zt, "steel", 0.0)
        for k in range(7):
            S.bb(f"groove{k}", -1.6, 0.1, yw - 0.15, yw - 0.14, 0.35 + k * 0.28, 0.4 + k * 0.28, "dark", 0.0, nocast=True)
        S.box("slatR", (1.5, 0.08, 1.3), (0.85, yw - 0.45, 1.75), "steel", 0.0, rot=(18, 0, 14), stretch=False)
        S.box("slatR2", (0.9, 0.08, 0.7), (1.0, yw - 0.9, 0.7), "steel_s", 0.0, rot=(60, 0, -20), stretch=False)
        beam(S, "slatbar", (0.2, yw - 0.1, 2.25), (1.6, yw - 0.9, 0.35), 0.07, "girder")
    else:
        S.bb("slats", -1.6, 1.6, yw - 0.14, yw - 0.06, zb, zt, "steel", 0.0)
        for k in range(8):
            S.bb(f"groove{k}", -1.6, 1.6, yw - 0.15, yw - 0.14, 0.35 + k * 0.25, 0.4 + k * 0.25, "dark", 0.0, nocast=True)
        S.bb("handle", -0.25, 0.25, yw - 0.18, yw - 0.14, 0.4, 0.5, "yellow", 0.0, nocast=True)
    # door lamps above the lintel (house-side beacons in the active state)
    for k, lx in enumerate((-1.65, 1.65)):
        if state_open:
            on = (i % 2 == k)
            S.cyl(f"beac{k}", 0.17, 0.2, (lx, yw - 0.28, 3.17), "beacon" if on else "dark", verts=10, nocast=True)
        else:
            on = ((i % 2 == 0) == (k == 0)) and not dmg
            if dmg:
                on = (k == 0 and i % 2 == 0)
            S.bb(f"dl{k}", lx - 0.2, lx + 0.2, yw - 0.22, yw - 0.14, 3.05, 3.2, "lamp" if on else "dark", 0.0, nocast=True)


def sawtooth(S, dmg):
    """Flat strip with the house marking at the south edge, then three teeth: slope falling north, glazed face south."""
    zr = ZR
    x0, x1 = -HALL_X - 0.25, HALL_X + 0.25
    yn = HALL_Y1 + 0.2
    flat_y0 = HALL_Y0 - 0.2
    S.bb("roofF", x0, x1, flat_y0, FLAT_Y1, WALL_H, zr, "roof", 0.04)
    S.bb("parapet", x0, x1, flat_y0, flat_y0 + 0.18, zr, zr + 0.2, "concrete2", 0.03)
    for sx in (-1, 1):
        S.bb("parapetS", sx * (HALL_X + 0.18) - 0.07, sx * (HALL_X + 0.18) + 0.07, flat_y0, yn, zr, zr + 0.17, "concrete2", 0.02)
    S.bb("roofhouse", 0.9 if not dmg else 0.9, 3.5 if not dmg else 2.3, -2.38, -1.62, zr, zr + 0.04, "house2", 0.0)
    S.bb("roofframe", 0.8, (3.6 if not dmg else 2.4), -2.45, -1.55, zr, zr + 0.02, "dark", 0.0)
    S.bb("hatch", -3.8, -2.5, -2.3, -1.65, zr, zr + 0.18, "concrete2", 0.04)
    S.bb("hatchd", -3.65, -2.65, -2.2, -1.75, zr + 0.18, zr + 0.22, "steel", 0.02)
    for k in range(4):
        S.bb(f"grille{k}", -2.1 + k * 0.4, -1.8 + k * 0.4, -2.3, -1.65, zr, zr + 0.3, "dark" if k % 2 else "concrete2", 0.0)
    for t in range(3):
        ya = FLAT_Y1 + t * TOOTH_W
        yb = ya + TOOTH_W
        gap = None
        if dmg:
            gap = {0: (None), 1: (-0.9, 3.0), 2: (-0.1, 3.5)}[t]     # x-range of the tooth that has collapsed
        xs = [(-HALL_X - 0.2, HALL_X + 0.2)]
        if gap:
            xs = [(-HALL_X - 0.2, gap[0]), (gap[1], HALL_X + 0.2)]
        for si, (a, b) in enumerate(xs):
            slab_beam(S, f"slope{t}_{si}", ya, yb, zr + TOOTH_H, zr, a, b, "roof", 0.14)
            for sk in range(int((b - a) / 1.3)):
                xx = a + 1.3 * (sk + 1)
                if xx < b - 0.1:
                    S.bb(f"rib{t}", xx - 0.03, xx + 0.03, ya + 0.05, yb - 0.02, zr + 0.02, zr + TOOTH_H + 0.02 - 0.45 * 0, "roof_s" if False else "concrete2", 0.0, nocast=True)
            # glazed south face with mullions
            S.bb(f"glass{t}_{si}", a, b, ya - 0.04, ya + 0.02, zr, zr + TOOTH_H, "glass_lit" if not dmg else "glass", 0.0)
            S.bb(f"gcap{t}_{si}", a, b, ya - 0.06, ya + 0.02, zr + TOOTH_H - 0.07, zr + TOOTH_H + 0.04, "steel", 0.0)
            for mk in range(int((b - a) / 1.0) + 1):
                mx = a + 1.0 * mk
                if mx < b:
                    S.bb(f"mull{t}", mx - 0.025, mx + 0.025, ya - 0.07, ya - 0.03, zr, zr + TOOTH_H, "steel", 0.0, nocast=True)
        if dmg and gap:
            # shattered glass: dark teeth along the broken edge
            S.bb(f"void{t}", gap[0], gap[1], ya + 0.02, yb, zr - 0.03, zr + 0.01, "dark", 0.0)
            for k in range(5):
                gx = gap[0] + 0.25 + k * (gap[1] - gap[0] - 0.5) / 4
                S.box(f"shard{t}_{k}", (0.12, 0.06, 0.25 + 0.12 * (k % 2)), (gx, ya - 0.02, zr + 0.16), "glass_s", 0.0, rot=(0, 0, 0), stretch=True)
    # roof vents on the first tooth flat; ridge
    S.bb("vent", -0.3, 0.4, FLAT_Y1 - 0.8, FLAT_Y1 - 0.2, zr, zr + 0.4, "concrete2", 0.04)
    S.bb("ventg", -0.2, 0.3, FLAT_Y1 - 0.85, FLAT_Y1 - 0.8, zr + 0.1, zr + 0.3, "dark", 0.0, nocast=True)


def chimney(S, dmg):
    cx, cy = CHIM
    top = ZR + (1.05 if dmg else 2.5)
    S.cyl("chim", 0.36, top - ZR, (cx, cy, (top + ZR) / 2), "concrete", verts=16)
    S.cyl("chimb", 0.52, 0.5, (cx, cy, ZR + 0.25), "concrete2", verts=16)
    for k, zz in enumerate((ZR + 0.8, ZR + 1.5, ZR + 2.1)):
        if zz < top - 0.1:
            S.cyl(f"cband{k}", 0.4, 0.14, (cx, cy, zz), "rust", verts=16)
    if not dmg:
        S.cyl("ccollar", 0.46, 0.15, (cx, cy, top + 0.05), "steel", verts=16)
        S.cyl("cdark", 0.28, 0.06, (cx, cy, top + 0.13), "dark", verts=12)
    else:
        S.box("cjag", (0.3, 0.3, 0.3), (cx - 0.2, cy, top + 0.08), "concrete2", 0.0, rot=(0, 20, 10), stretch=False)
        S.cyl("cdarkb", 0.28, 0.06, (cx, cy, top + 0.03), "dark", verts=12)
        tl = math.radians(-40)
        L = 1.3
        for nm, rr, hh, along, m_ in (("chimlean", 0.34, L, L / 2, "concrete"), ("cbandL", 0.38, 0.14, 0.4, "rust")):
            o = S.cyl(nm, rr, hh, (0, 0, 0), m_, verts=16)
            o.location = (cx - 0.2 + along * math.sin(tl), cy * S.YS, top + 0.06 + along * math.cos(tl))
            o.rotation_euler = (0, tl, 0)


def crane_rail(S, dmg, i, anim):
    """Overhead crane: two lattice pylons on the pad, a yellow rail along x over the roof, a trolley with hoist and hook."""
    zt = RAIL_Z
    for sx in (-1, 1):
        x = sx * 4.62
        if dmg and sx > 0:
            # the east leg has buckled: a stump and a bent strut
            for ax in (-0.15, 0.15):
                for ay in (-0.15, 0.15):
                    post(S, "pyl", x + ax, RAIL_Y + ay, 0.17, 1.1 + 0.2 * (ax > 0), 0.09, "yellow")
            beam(S, "bent", (x, RAIL_Y, 1.2), (x + 0.7, RAIL_Y - 1.2, 0.25), 0.1, "yellow")
            beam(S, "bent", (x - 0.1, RAIL_Y + 0.1, 1.15), (x + 0.4, RAIL_Y + 1.0, 0.3), 0.08, "steel")
            continue
        for ax in (-0.15, 0.15):
            for ay in (-0.15, 0.15):
                post(S, "pyl", x + ax, RAIL_Y + ay, 0.17, zt, 0.09, "yellow")
        z = 0.17
        while z < zt - 0.5:
            beam(S, "pb", (x - 0.15, RAIL_Y - 0.15, z), (x + 0.15, RAIL_Y - 0.15, z + 0.9), 0.05, "yellow")
            beam(S, "pb", (x + 0.15, RAIL_Y - 0.15, z + 0.9), (x + 0.15, RAIL_Y + 0.15, z + 1.8), 0.05, "yellow")
            z += 1.8
        S.bb("saddle", x - 0.3, x + 0.3, RAIL_Y - 0.3, RAIL_Y + 0.3, zt, zt + 0.12, "steel", 0.0)
    if dmg:
        # sagging rail: west end on its pylon, the east end dropped onto the roof edge
        beam(S, "rail", (-4.62, RAIL_Y, zt + 0.18), (4.3, RAIL_Y - 0.15, ZR + 0.7), 0.3, "yellow")
        beam(S, "rail2", (-4.62, RAIL_Y, zt + 0.32), (4.3, RAIL_Y - 0.15, ZR + 0.84), 0.1, "steel")
        tx = 1.0 + 0.25 * math.sin(i * 1.57)
        tz = zt + 0.18 + (ZR + 0.7 - zt - 0.18) * (tx + 4.62) / 8.92
        S.bb("trolley", tx - 0.45, tx + 0.45, RAIL_Y - 0.28, RAIL_Y + 0.28, tz - 0.42, tz - 0.05, "yellow", 0.02)
        sway = 0.2 * math.sin(i * 1.57 + 0.6)
        beam(S, "cable", (tx, RAIL_Y, tz - 0.4), (tx + sway, RAIL_Y, tz - 1.55), 0.045, "dark")
        S.bb("hook", tx + sway - 0.12, tx + sway + 0.12, RAIL_Y - 0.12, RAIL_Y + 0.12, tz - 1.75, tz - 1.5, "dark", 0.0)
        return
    S.bb("rail", -4.8, 4.8, RAIL_Y - 0.15, RAIL_Y + 0.15, zt, zt + 0.3, "yellow", 0.02)
    S.bb("rail2", -4.8, 4.8, RAIL_Y - 0.1, RAIL_Y + 0.1, zt + 0.3, zt + 0.4, "steel", 0.02)
    tx = 1.6 * math.sin(2 * math.pi * anim / 4) if anim is not None else 0.0
    S.bb("trolley", tx - 0.5, tx + 0.5, RAIL_Y - 0.3, RAIL_Y + 0.3, zt - 0.4, zt, "yellow", 0.02)
    S.bb("motor", tx - 0.15, tx + 0.35, RAIL_Y - 0.26, RAIL_Y + 0.26, zt - 0.6, zt - 0.4, "dark", 0.0)
    hz = zt - 1.8 + 0.12 * math.sin(2 * math.pi * anim / 4 * 2) if anim is not None else zt - 1.8
    beam(S, "cable", (tx, RAIL_Y, zt - 0.6), (tx, RAIL_Y, hz + 0.3), 0.045, "dark")
    S.bb("hook", tx - 0.14, tx + 0.14, RAIL_Y - 0.14, RAIL_Y + 0.14, hz, hz + 0.3, "dark", 0.0)
    S.bb("block", tx - 0.1, tx + 0.1, RAIL_Y - 0.1, RAIL_Y + 0.1, hz + 0.3, hz + 0.4, "yellow", 0.0)


def south_face(S, dmg, active, i):
    yw = HALL_Y0 - 0.02
    S.bb("hall", -HALL_X, HALL_X, HALL_Y0, HALL_Y1, 0.14, WALL_H, "concrete", 0.05)
    S.bb("plinth", -HALL_X - 0.1, HALL_X + 0.1, HALL_Y0 - 0.1, HALL_Y1 + 0.1, 0.14, 0.5, "concrete2", 0.04)
    for px_ in (-HALL_X, -2.0, 2.0, HALL_X):
        S.bb(f"pil{px_}", px_ - 0.2, px_ + 0.2, HALL_Y0 - 0.14, HALL_Y0 + 0.1, 0.14, WALL_H + 0.05, "concrete2", 0.04)
    # house panels either side of the door, on dark frames
    for sx in (-1, 1):
        cx = sx * 3.03
        S.bb("pframe", cx - 1.0, cx + 1.0, yw - 0.08, yw + 0.02, 0.6, 3.0, "dark", 0.0)
        if dmg and sx > 0:
            S.bb("panel", 2.0, 2.45, yw - 0.14, yw - 0.06, 0.7, 2.9, "house", 0.0)
        else:
            S.bb("panel", cx - 0.9, cx + 0.9, yw - 0.14, yw - 0.06, 0.7, 2.9, "house", 0.0)
        for k in range(3):
            S.bb("sten", cx - 0.5 + k * 0.45, cx - 0.28 + k * 0.45, yw - 0.1, yw - 0.06, 0.2, 0.46, "white", 0.0, nocast=True)
    for k, (rx, rw, rh) in enumerate(((-2.0, 0.16, 0.8), (0.5, 0.14, 0.5), (2.2, 0.18, 0.7), (-4.0, 0.16, 0.5))):
        S.bb(f"streak{k}", rx, rx + rw, yw - 0.12, yw - 0.1, 3.2 - rh, 3.2, "rust", 0.0, nocast=True)
    door(S, yw, active, dmg, i)
    # gantry-style canopy post pair with a plate over the door (not in the way of the exit)
    S.bb("gplate", -0.6, 0.6, yw - 0.36, yw - 0.3, 3.1, 3.38, "dark", 0.0)
    for k in range(4):
        S.bb(f"gnum{k}", -0.45 + k * 0.25, -0.33 + k * 0.25, yw - 0.38, yw - 0.36, 3.14, 3.34, "white", 0.0, nocast=True)
    return yw


def damaged_wall(S, yw):
    """Breach east of the door, torn left plate, rubble and a fallen girder."""
    yb = yw - 0.2
    S.bb("breach", 2.3, 3.8, yw - 0.11, yw - 0.07, 0.8, 3.05, "dark", 0.0)
    for (x0, x1, z0, z1) in ((2.3, 3.0, 3.05, 3.35), (3.0, 3.5, 3.05, 3.2), (3.5, 3.8, 3.05, 3.3), (2.1, 2.3, 1.4, 2.5), (3.8, 3.95, 0.9, 1.8)):
        S.bb("breachj", x0, x1, yw - 0.11, yw - 0.07, z0, z1, "dark", 0.0)
    post(S, "ig", 2.75, yb, 0.8, 3.05, 0.15, "girder")
    post(S, "ig", 3.4, yb, 0.8, 2.5, 0.15, "girder")
    beam(S, "ig", (2.75, yb, 0.9), (3.4, yb, 2.45), 0.1, "girder")
    beam(S, "ig", (2.5, yb, 2.0), (3.7, yb, 1.5), 0.1, "girder")
    beam(S, "ig", (3.4, yb, 2.5), (3.75, yb, 3.0), 0.09, "rebar")
    S.box("chunk", (0.5, 0.22, 0.35), (2.15, yw - 0.16, 3.2), "concrete2_s", 0.0, rot=(0, 18, 12), stretch=False)
    S.box("chunk", (0.35, 0.2, 0.3), (3.9, yw - 0.16, 3.0), "concrete2_s", 0.0, rot=(0, -20, -15), stretch=False)
    for k, (x, y, w, h) in enumerate(((2.8, -3.2, 0.6, 0.4), (3.4, -3.45, 0.5, 0.32), (3.8, -3.05, 0.4, 0.5), (2.4, -3.6, 0.45, 0.28), (3.1, -3.0, 0.35, 0.45))):
        S.box(f"rub{k}", (w, w * 0.8, h), (x, y, 0.17 + h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k))
    beam(S, "fallen", (2.0, -4.1, 0.3), (3.8, -3.4, 0.3), 0.15, "girder")
    S.bb("pmiss", -3.9, -3.2, yw - 0.16, yw - 0.1, 2.2, 2.9, "dark", 0.0)
    S.bb("pmiss", -3.2, -2.8, yw - 0.16, yw - 0.1, 2.55, 2.9, "dark", 0.0)
    post(S, "ig", -3.55, yw - 0.22, 2.2, 2.9, 0.12, "girder")
    for k, (a, b) in enumerate((((-3.0, 0.75), (-2.6, 1.7)), ((-2.6, 1.7), (-3.0, 2.3)), ((-3.7, 0.8), (-3.4, 1.5)))):
        beam(S, f"crack{k}", (a[0], yw - 0.17, a[1]), (b[0], yw - 0.17, b[1]), 0.05, "dark")
    scorch(S, "scW", -3.6, yw - 0.18, 2.6, 1.4, 0.8)
    scorch(S, "scA", 3.0, yw - 0.16, 3.3, 2.4, 0.5)
    scorch(S, "scR", 3.0, yw - 0.14, 0.4, 2.0, 0.4)
    scorch(S, "scT", 1.4, 0.4, ZR + 0.03, 3.0, 1.6, 0.04, plane="T")
    scorch(S, "scP", 3.0, -3.7, 0.16, 2.4, 1.4, 0.03, plane="T")
    # collapsed roof: bent girders and dangling slabs over the void
    beam(S, "bg", (-0.6, -0.9, ZR - 0.05), (0.2, 0.2, ZR + 1.1), 0.13, "girder")
    beam(S, "bg", (1.4, -0.8, ZR - 0.05), (1.6, 0.4, ZR + 0.9), 0.13, "girder")
    beam(S, "bg", (2.8, -1.0, ZR - 0.05), (3.4, 0.3, ZR + 1.3), 0.13, "girder")
    beam(S, "bg", (-0.8, 0.9, ZR + 0.0), (3.2, 1.1, ZR + 0.2), 0.12, "girder")
    for k, (a, b) in enumerate((((0.0, 0.6, ZR + 0.1), (-0.3, 1.1, ZR + 0.8)), ((2.0, 0.8, ZR + 0.1), (2.3, 1.3, ZR + 0.7)))):
        beam(S, f"rbar{k}", a, b, 0.05, "rebar")
    S.box("slabH", (1.3, 0.9, 0.18), (2.0, -0.5 * S.YS, ZR + 0.55), "roof", 0.0, rot=(0, 30, 8), stretch=False)
    S.box("slabH2", (0.9, 0.7, 0.16), (-0.1, 0.2 * S.YS, ZR + 0.3), "roof", 0.0, rot=(-26, 0, 18), stretch=False)
    for k, (x, y, w) in enumerate(((0.4, -0.4, 0.4), (1.5, 0.6, 0.35), (2.6, -0.3, 0.3), (3.2, 0.8, 0.3))):
        S.box(f"debris{k}", (w, w * 0.8, 0.2), (x, y, ZR + 0.02), "concrete_s", 0.0, rot=(0, 0, 25 * k + 10))


def finished(S, i, anim, dmg=False, active=False):
    if dmg:
        S.skin.update(SOOT_SKIN)
    apron(S, active, i)
    yw = south_face(S, dmg, active, i)
    sawtooth(S, dmg)
    chimney(S, dmg)
    crane_rail(S, dmg, i, anim if not dmg else i)
    work_lights(S, active)
    # east side service tank and pipe run (northeast, hidden behind the hall mostly), a fuel bowser by the door on the pad
    S.cyl("tankE", 0.45, 3.2, (4.62, 1.2, 0.65), "steel", axis="Y", verts=16)
    for yy in (-0.1, 1.2, 2.5):
        S.cyl(f"tband{yy}", 0.5, 0.12, (4.62, yy, 0.65), "rust", axis="Y", verts=16)
    S.bb("tsaddle", 4.35, 4.9, -0.3, 2.9, 0.14, 0.25, "concrete2", 0.02)
    S.bb("crate", -4.7, -4.0, 3.2, 3.9, 0.14, 0.7, "crate", 0.03)
    S.bb("crate", -4.6, -4.0, 3.3, 3.8, 0.7, 1.15, "crate", 0.03, rot=(0, 0, 10))
    if dmg:
        damaged_wall(S, yw)
        S.bb("rubble1", -1.0, -0.4, -4.6, -4.1, 0.17, 0.4, "concrete2", 0.04)


def frame_state(state, i):
    """(anim index 0..3 for the idle loop or None, damaged, active) for a frame."""
    if state == "damaged":
        return i, True, False
    if state == "active":
        return i, False, True
    if state == "build":
        return 0, False, False       # the finished model (build frames 6, 7) is exactly idle frame 0
    return i, False, False


# ====================================================================================================== build stages
COLY_ = _ref.COLY


def site_slab(S, top=0.5):
    S.bb("slab", -4.3, 4.3, -2.8, 2.4, 0.14, top, "raw", 0.02)
    S.bb("form", -4.3, 4.3, -2.86, -2.8, 0.14, top + 0.15, "timber", 0.0)
    S.bb("form", -4.36, -4.3, -2.8, 2.4, 0.14, top + 0.15, "timber", 0.0)


def sawtooth_ribs(S, upto=3, deck=0):
    """Steel rafters of the sawtooth: a post and a sloped rafter per column line per tooth; `deck` teeth already have primed slabs."""
    zr = WALL_H + 0.15
    for t in range(upto):
        ya = FLAT_Y1 + t * TOOTH_W
        yb = ya + TOOTH_W
        for x in COLX:
            post(S, "rp", x, ya, zr, zr + TOOTH_H + 0.05, 0.1, "girder")
            beam(S, "raf", (x, ya, zr + TOOTH_H + 0.05), (x, yb, zr + 0.02), 0.1, "girder")
        beam(S, "ridge", (-4.0, ya, zr + TOOTH_H + 0.05), (4.0, ya, zr + TOOTH_H + 0.05), 0.1, "girder")
        if t < deck:
            slab_beam(S, f"pslope{t}", ya, yb, zr + TOOTH_H + 0.12, zr + 0.08, -4.3, 4.3, "primer", 0.1)
            S.bb(f"pglass{t}", -4.3, 4.3, ya - 0.03, ya + 0.02, zr + 0.05, zr + TOOTH_H + 0.1, "dark", 0.0)


def build_site(S, i):
    S.bb("pad", -5.0, 5.0, -5.0, 5.0, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    if i == 0:       # surveyed plot: dug foundation, spoil heaps, stakes, string lines
        S.bb("pit", -4.3, 4.3, -2.8, 2.4, 0.14, 0.17, "soil", 0.0)
        S.bb("pit2", -4.3, 4.3, -2.8, -2.45, 0.14, 0.2, "soil2", 0.0)
        S.bb("pit2", -4.3, -3.95, -2.8, 2.4, 0.14, 0.2, "soil2", 0.0)
        S.bb("pit2", 3.95, 4.3, -2.8, 2.4, 0.14, 0.2, "soil2", 0.0)
        S.bb("pit2", -4.3, 4.3, 2.05, 2.4, 0.14, 0.2, "soil2", 0.0)
        S.bb("pitT", -3.6, 3.6, -2.0, 1.6, 0.14, 0.2, "soil", 0.0)
        S.bb("pitT2", -3.2, 3.2, -1.4, 1.0, 0.14, 0.22, "soil2", 0.0)
        for (mx, my, mw, mh) in ((-4.9, -1.0, 0.6, 2.8), (4.7, -1.8, 0.6, 3.2), (-1.0, 3.0, 4.0, 0.5), (1.0, -3.3, 2.4, 0.4)):
            S.bb("mound", mx - mw / 2, mx + mw / 2, my - mh / 2, my + mh / 2, 0.14, 0.5, "soil", 0.18)
        for (sx, sy) in [(-4.85, -3.2), (4.85, -3.2), (-4.85, 2.9), (4.85, 2.9)]:
            post(S, "stake", sx, sy, 0.14, 1.0, 0.1, "timber")
            post(S, "staket", sx, sy, 0.8, 1.0, 0.12, "string")
        S.bb("line", -4.85, 4.85, -3.22, -3.18, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("line", -4.85, 4.85, 2.88, 2.92, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("line", -4.87, -4.83, -3.2, 2.9, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("line", 4.83, 4.87, -3.2, 2.9, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("sign", 1.4, 2.6, -4.4, -4.3, 0.9, 1.7, "white", 0.0)
        post(S, "signp", 1.55, -4.35, 0.14, 0.9, 0.08, "timber")
        post(S, "signp", 2.45, -4.35, 0.14, 0.9, 0.08, "timber")
        S.bb("signs", 1.5, 2.5, -4.42, -4.38, 1.2, 1.5, "yellow", 0.0)
    elif i == 1:     # poured foundation, rebar, column stubs, shuttering, mixer
        site_slab(S, 0.5)
        for x in COLX:
            for y in COLY:
                S.bb("bp", x - 0.22, x + 0.22, y - 0.22, y + 0.22, 0.5, 0.58, "raw", 0.0)
                post(S, "stub", x, y, 0.5, 1.5, 0.2, "girder")
                for ax in (-0.3, 0.3):
                    post(S, "rb", x + ax, y + ax * 0.7, 0.5, 1.15, 0.05, "rebar")
        for yy in (-1.5, -0.4, 0.7, 1.7):
            S.bb("mesh", -3.7, 3.7, yy - 0.025, yy + 0.025, 0.52, 0.56, "rebar", 0.0, nocast=True)
        for k in range(8):      # door track rails set into the slab
            pass
        S.cyl("mixer", 0.38, 0.8, (-3.2, -4.3, 0.7), "primer", verts=14)
        S.bb("mixer", -3.55, -2.85, -4.6, -4.0, 0.14, 0.4, "girder", 0.0)
        S.bb("sandpile", 1.0, 2.6, 3.0, 4.0, 0.14, 0.7, "soil", 0.3)
    elif i == 2:     # first steel storey: columns to 2.1, floor girders, scaffold, crane
        site_slab(S, 0.5)
        hall_frame(S, 2.15, (1.95,), braces=True)
        scaffold(S, -3.9, -2.6, -3.5, -2.8, (0.8, 1.7))
        crane(S)
        spark(S, -2.0, -2.5, 2.1, i, 1)
    elif i == 3:     # full steel frame of the hall; the door header is going in
        site_slab(S, 0.5)
        hall_frame(S, 3.65, (1.95, 3.55), roof=True)
        for sx in (-1, 1):
            post(S, "dpost", sx * 1.75, -2.55, 0.5, 3.0, 0.2, "yellow")
        beam(S, "dhead", (-1.75, -2.55, 3.0), (1.75, -2.55, 3.0), 0.2, "yellow")
        scaffold(S, -3.9, -2.6, -3.5, -2.8, (0.8, 1.7, 2.6))
        scaffold(S, 2.6, 3.9, -3.5, -2.8, (0.8, 1.7))
        crane(S, "girder")
        spark(S, 0.0, -2.5, 3.55, i, 3)
        spark(S, -2.6, 2.1, 3.6, i, 7)
    elif i == 4:     # sawtooth rafters, first wall panels, slab rails
        site_slab(S, 0.5)
        hall_frame(S, 3.65, (1.95, 3.55), roof=True)
        sawtooth_ribs(S, 3)
        for sx in (-1, 1):
            post(S, "dpost", sx * 1.75, -2.55, 0.5, 3.0, 0.2, "yellow")
        beam(S, "dhead", (-1.75, -2.55, 3.0), (1.75, -2.55, 3.0), 0.2, "yellow")
        cladding(S, [(-4.0, -2.0, 0.5, 2.0), (2.0, 4.0, 0.5, 1.4)])
        scaffold(S, -3.9, -2.6, -3.5, -2.8, (0.8, 1.7, 2.6, 3.4))
        scaffold(S, 2.6, 3.9, -3.5, -2.8, (0.8, 1.7, 2.6))
        S.cyl("pipeY", 0.3, 2.6, (4.62, -0.5, 0.5), "steel", axis="Y", verts=14)
        crane(S, "pallet")
        spark(S, -2.0, -2.5, 1.9, i, 5)
        spark(S, 4.0, 2.1, 3.6, i, 2)
    elif i == 5:     # walls mostly clad, roof half decked, crane pylons up, chimney base
        site_slab(S, 0.5)
        hall_frame(S, 3.65, (1.95, 3.55), roof=True, braces=False)
        sawtooth_ribs(S, 3, deck=1)
        cladding(S, [(-4.0, -1.3, 0.5, 2.5), (-4.0, -2.9, 2.5, 3.55), (-2.9, -1.3, 2.5, 3.0), (1.3, 4.1, 0.5, 2.5), (1.3, 2.5, 2.5, 3.55)])
        S.bb("lintel", -1.75, 1.75, HALL_Y0 - 0.12, HALL_Y0 + 0.05, 2.5, 3.0, "raw", 0.02)
        S.bb("deckA", -4.35, 4.35, HALL_Y0 - 0.2, FLAT_Y1, 3.6, 3.82, "primer", 0.04)
        for k in range(8):
            S.bb("dseam", -4.3 + k * 1.15, -4.3 + k * 1.15 + 0.04, HALL_Y0 - 0.2, FLAT_Y1, 3.82, 3.84, "girder", 0.0, nocast=True)
        S.cyl("chimb", 0.5, 0.8, (CHIM[0], CHIM[1], 3.82 + 0.4), "raw", verts=14)
        for sx in (-1, 1):
            x = sx * 4.62
            for ax in (-0.15, 0.15):
                for ay in (-0.15, 0.15):
                    post(S, "pyl", x + ax, RAIL_Y + ay, 0.17, 3.2, 0.09, "yellow")
        S.cyl("pipeY", 0.3, 4.6, (4.62, 0.3, 0.5), "steel", axis="Y", verts=14)
        scaffold(S, -3.9, -2.6, -3.5, -2.8, (0.8, 1.7, 2.6, 3.4), top=4.3)
        scaffold(S, 2.6, 3.9, -3.5, -2.8, (0.8, 1.7, 2.6, 3.4), top=4.3)
        crane(S, "panel")
        spark(S, 2.5, -2.7, 3.3, i, 4)
    site_clutter(S, i if i < 5 else 9)


def build(S, state, i, n):
    building = state == "build"
    if building and i < 6:
        build_site(S, i)
        return
    anim, dmg, active = frame_state(state, i)
    if building and i == 6:          # finished model unpainted (primer), no stencils; scaffold coming down
        S.skin.update(PRIMER_SKIN)
        S.hide = lambda nm: nm.startswith(("sten", "gnum", "chev", "dash"))
    finished(S, i if not building else 0, anim, dmg, active)
    if building and i == 6:
        scaffold(S, 2.6, 3.9, -3.5, -2.8, (0.8, 1.7))
        S.bb("planks", -3.9, -2.0, -3.9, -3.5, 0.14, 0.3, "timber", 0.0)
        S.bb("polepile", -4.5, -1.5, -4.3, -4.15, 0.14, 0.3, "timber", 0.0)
        S.bb("crate2", 3.7, 4.5, -4.7, -4.0, 0.14, 0.8, "crate", 0.03)
        spark(S, -1.5, -2.7, 3.3, 3, 6)
