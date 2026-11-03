"""Authority Construction Yard: the mobile-construction depot (3x3 cells). Original design, no franchise references.

A concrete hangar at the back, an open assembly bay in front with a gantry crane (the hook sways in the idle loop),
stacked prefab panels, a radio mast with a blinking lamp, a command flag. Damage is structural only (D-069): the east end
of the hangar roof has fallen in, the gantry sags, the mast leans, panels are strewn. Fire / smoke come from FX anchors.

This module also holds the build-site helpers shared by the other fan-out A structures (auth_power, auth_barracks,
auth_silo load it): generic stages 0-1 (plot, foundation), steel skeleton, scaffold, derrick crane, and the stage driver
`stages()` that renders stages 2-6 as "finished model in primer, cut at a rising height, steel skeleton above".
Contract: see auth_refinery.py.  Units: 3.4 per cell, x east, y NORTH, z up.
"""
import importlib.util
import math
import os

_HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name):
    spec = importlib.util.spec_from_file_location(f"structlib_{name}", os.path.join(_HERE, f"{name}.py"))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


R = _load("auth_refinery")      # read-only: materials and the hazard-stripe helper

ID = "auth_conyard"
TITLE = "Authority Construction Yard"
FOOTPRINT = (3, 3)
CANVAS = (192, 248)
ORIGIN = (0, 56)
DOCK = (1, 3)
CAP_MATERIAL = "concrete2"
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]

MATERIALS = dict(R.MATERIALS)
MATERIALS.update({
    "sandbag": ("#A39574", dict(rough=1.0)),
    "sandbag2": ("#877B5C", dict(rough=1.0)),
    "sandbag_s": ("#6E664F", dict(rough=1.0)),
    "amber": ("#FFB43A", dict(rough=0.3, emit="#FFA830")),
    "amber_dim": ("#8A5E22", dict(rough=0.5)),
    "glass": ("#25363A", dict(rough=0.2)),
    "ash": ("#9C9C94", dict(rough=1.0)),
    "ash2": ("#7C7C75", dict(rough=1.0)),
    "gauge": ("#D8D4C4", dict(rough=0.6)),
    "pipeblue": ("#5E7F94", dict(rough=0.5, metal=0.3)),
    "crack": ("#262922", dict(rough=1.0)),
    "panel": ("#9A9D92", dict(rough=0.9)),
    "panel2": ("#7E8279", dict(rough=0.9)),
    "red": ("#B8341F", dict(rough=0.6, emit="#FF3A20")),
})
SOOT_SKIN = dict(R.SOOT_SKIN)
SOOT_SKIN.update({"sandbag": "sandbag_s", "panel": "concrete_s", "panel2": "concrete2_s"})
PRIMER_SKIN = dict(R.PRIMER_SKIN)
PRIMER_SKIN.update({"sandbag": "primer", "amber": "dark", "amber_dim": "dark", "red": "dark", "panel": "primer", "panel2": "primer",
                    "glass": "dark", "pipeblue": "primer", "ash": "raw", "ash2": "raw", "gauge": "primer"})

FX = []     # filled below once ORIGIN/FOOTPRINT are known


def anchor(fp, origin, x, y, z):
    """Frame-space pixel of a game-unit point (D-069): px = ox + fpw/2 + x*18.8, py = oy + fph/2 - y*18.8 - z*14.4."""
    px = origin[0] + fp[0] * 32 + x * 18.82
    py = origin[1] + fp[1] * 32 - y * 18.82 - z * 14.42
    return int(round(px)), int(round(py))


# ====================================================================================================== shared helpers
def post(S, name, x, y, z0, z1, w, m, noclip=False):
    S.bb(name, x - w / 2, x + w / 2, y - w / 2, y + w / 2, z0, z1, m, 0.0, noclip=noclip)


def beam(S, name, p0, p1, w, m, nocast=False, noclip=False):
    """Square-section member between two game-unit points (y is stretched here)."""
    YS = S.YS
    a = (p0[0], p0[1] * YS, p0[2])
    b = (p1[0], p1[1] * YS, p1[2])
    dx, dy, dz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    ln = math.sqrt(dx * dx + dy * dy + dz * dz)
    yaw = math.degrees(math.atan2(dy, dx))
    pitch = -math.degrees(math.atan2(dz, math.hypot(dx, dy)))
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2)
    return S.box(name, (ln, w, w), mid, m, 0.0, rot=(0, pitch, yaw), stretch=False, nocast=nocast, noclip=noclip)


def spark(S, x, y, z, i, seed=0):
    S.bb("weld", x - 0.1, x + 0.1, y - 0.1, y + 0.1, z - 0.1, z + 0.1, "spark", 0.0, nocast=True, noclip=True)
    for k in range(5):
        a = (i * 2.1 + k * 1.7 + seed) * 1.3
        r = 0.22 + 0.2 * ((k * 5 + i * 3 + seed) % 4)
        cx, cy = x + r * math.cos(a), y + r * math.sin(a) * 0.5
        S.bb(f"sprk{k}", cx - 0.05, cx + 0.05, cy - 0.05, cy + 0.05, z + 0.15 * k - 0.15, z + 0.15 * k - 0.05, "spark", 0.0, nocast=True, noclip=True)


def scaffold(S, x0, x1, y0, y1, levels, top=None, ladder=True):
    """Timber scaffold tower (all parts survive the build cut)."""
    top = top if top is not None else max(levels) + 0.85
    for px in (x0, x1):
        for py in (y0, y1):
            post(S, "sp", px, py, 0.14, top, 0.08, "timber", True)
    for z in levels:
        S.bb("deck", x0 - 0.05, x1 + 0.05, y0 - 0.05, y1 + 0.05, z, z + 0.07, "timber", 0.0, noclip=True)
        beam(S, "rl", (x0, y0, z + 0.65), (x1, y0, z + 0.65), 0.05, "timber", noclip=True)
        beam(S, "rl", (x0, y0, z + 0.65), (x0, y1, z + 0.65), 0.05, "timber", noclip=True)
        beam(S, "rl", (x1, y0, z + 0.65), (x1, y1, z + 0.65), 0.05, "timber", noclip=True)
    beam(S, "br", (x0, y0, 0.14), (x1, y0, max(levels) + 0.1), 0.06, "timber", noclip=True)
    if ladder:
        for lx in (x0 + 0.2, x0 + 0.5):
            post(S, "lad", lx, y0 - 0.2, 0.14, max(levels) + 0.3, 0.05, "timber", True)
        z = 0.4
        while z < max(levels):
            S.bb("rung", x0 + 0.2, x0 + 0.5, y0 - 0.22, y0 - 0.18, z, z + 0.04, "timber", 0.0, noclip=True)
            z += 0.32


def derrick(S, mx, my, h, tip, load=None):
    """Small yellow derrick: lattice mast, boom to `tip` (x, y, z), hanging cable + hook (+ girder/pallet load)."""
    for ax in (-0.12, 0.12):
        for ay in (-0.12, 0.12):
            post(S, "cm", mx + ax, my + ay, 0.14, h, 0.08, "yellow", True)
    z = 0.14
    while z < h - 0.1:
        beam(S, "cb", (mx - 0.12, my - 0.12, z), (mx + 0.12, my - 0.12, z + 0.9), 0.04, "yellow", noclip=True)
        z += 0.9
    S.bb("cab", mx - 0.24, mx + 0.24, my - 0.24, my + 0.24, h, h + 0.3, "yellow", 0.02, noclip=True)
    beam(S, "boom", (mx, my, h + 0.12), tip, 0.18, "yellow", noclip=True)
    hz = max(tip[2] - 1.6, 1.0)
    beam(S, "cable", (tip[0], tip[1], tip[2] - 0.05), (tip[0], tip[1], hz + 0.3), 0.04, "dark", noclip=True)
    S.bb("hook", tip[0] - 0.1, tip[0] + 0.1, tip[1] - 0.1, tip[1] + 0.1, hz + 0.18, hz + 0.38, "dark", 0.0, noclip=True)
    if load == "girder":
        S.bb("ld", tip[0] - 0.8, tip[0] + 0.8, tip[1] - 0.1, tip[1] + 0.1, hz - 0.1, hz + 0.1, "girder", 0.0, noclip=True)
    elif load == "pallet":
        S.bb("ld", tip[0] - 0.5, tip[0] + 0.5, tip[1] - 0.4, tip[1] + 0.4, hz - 0.3, hz + 0.1, "crate", 0.02, noclip=True)


def plot(S, pad, env, i):
    """Stages 0 (surveyed plot) and 1 (poured foundation + rebar) for any footprint. pad = (x0,x1,y0,y1), env = building bbox."""
    px0, px1, py0, py1 = pad
    ex0, ex1, ey0, ey1 = env
    S.bb("pad", px0, px1, py0, py1, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", px0, px1, py0, py0 + 0.1, 0.14, 0.2, "concrete2", 0.0)
    cx, cy = (ex0 + ex1) / 2, (ey0 + ey1) / 2
    if i == 0:
        S.bb("pit", ex0, ex1, ey0, ey1, 0.14, 0.17, "soil", 0.0)
        for (a, b, c, d) in ((ex0, ex1, ey0, ey0 + 0.35), (ex0, ex0 + 0.35, ey0, ey1), (ex1 - 0.35, ex1, ey0, ey1), (ex0, ex1, ey1 - 0.35, ey1)):
            S.bb("pit2", a, b, c, d, 0.14, 0.2, "soil2", 0.0)
        S.cyl("pitD", min(ex1 - ex0, ey1 - ey0) * 0.28, 0.08, (cx, cy, 0.18), "soil2", verts=24)
        w, h = px1 - px0, py1 - py0
        for (mx, my, mw, mh) in ((px0 + 0.3, cy, 0.6, h * 0.35), (px1 - 0.3, cy - 0.5, 0.6, h * 0.3), (cx, py1 - 0.5, w * 0.4, 0.5), (cx + 0.5, ey0 - 0.45, w * 0.25, 0.4)):
            S.bb("mound", mx - mw / 2, mx + mw / 2, my - mh / 2, my + mh / 2, 0.14, 0.5, "soil", 0.18)
        corners = [(px0 + 0.15, py0 + 0.9), (px1 - 0.15, py0 + 0.9), (px0 + 0.15, py1 - 0.4), (px1 - 0.15, py1 - 0.4)]
        for (sx, sy) in corners:
            post(S, "stake", sx, sy, 0.14, 1.0, 0.1, "timber")
            post(S, "staket", sx, sy, 0.8, 1.0, 0.12, "string")
        S.bb("line", corners[0][0], corners[1][0], corners[0][1] - 0.02, corners[0][1] + 0.02, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("line", corners[2][0], corners[3][0], corners[2][1] - 0.02, corners[2][1] + 0.02, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("line", corners[0][0] - 0.02, corners[0][0] + 0.02, corners[0][1], corners[2][1], 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("line", corners[1][0] - 0.02, corners[1][0] + 0.02, corners[1][1], corners[3][1], 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("sign", px1 - 1.7, px1 - 0.5, py0 + 0.15, py0 + 0.25, 0.9, 1.7, "white", 0.0)
        post(S, "signp", px1 - 1.55, py0 + 0.2, 0.14, 0.9, 0.08, "timber")
        post(S, "signp", px1 - 0.65, py0 + 0.2, 0.14, 0.9, 0.08, "timber")
        S.bb("signs", px1 - 1.6, px1 - 0.6, py0 + 0.12, py0 + 0.16, 1.2, 1.5, "yellow", 0.0)
        S.bb("crate", px0 + 0.3, px0 + 1.1, py0 + 0.2, py0 + 1.0, 0.14, 0.8, "crate", 0.03)
    else:
        S.bb("slab", ex0, ex1, ey0, ey1, 0.14, 0.5, "raw", 0.02)
        S.bb("form", ex0, ex1, ey0 - 0.06, ey0, 0.14, 0.65, "timber", 0.0)
        S.bb("form", ex0 - 0.06, ex0, ey0, ey1, 0.14, 0.65, "timber", 0.0)
        for xx in (ex0 + 0.3, cx, ex1 - 0.3):
            for yy in (ey0 + 0.3, ey1 - 0.3):
                S.bb("bp", xx - 0.22, xx + 0.22, yy - 0.22, yy + 0.22, 0.5, 0.58, "raw", 0.0)
                post(S, "stub", xx, yy, 0.5, 1.5, 0.2, "girder")
                for ax in (-0.3, 0.3):
                    post(S, "rb", xx + ax, yy + ax * 0.7, 0.5, 1.15, 0.05, "rebar")
        for k in range(3):
            yy = ey0 + (ey1 - ey0) * (k + 0.5) / 3
            S.bb("mesh", ex0 + 0.4, ex1 - 0.4, yy - 0.025, yy + 0.025, 0.52, 0.56, "rebar", 0.0, nocast=True)
        S.cyl("mixer", 0.38, 0.8, (px0 + 0.8, py0 + 0.5, 0.7), "primer", verts=14)
        S.bb("mixer", px0 + 0.45, px0 + 1.15, py0 + 0.2, py0 + 0.8, 0.14, 0.4, "girder", 0.0)
        S.bb("sandpile", px1 - 1.7, px1 - 0.4, py1 - 1.2, py1 - 0.3, 0.14, 0.7, "soil", 0.3)


def skeleton(S, boxes, rings, z0, z1):
    """Steel skeleton from z0 up to z1 (never clipped): box frames (x0,x1,y0,y1) and round ring frames (cx,cy,r)."""
    levels = []
    z = 0.5 + 1.5
    while z < z1 + 0.01:
        levels.append(z)
        z += 1.5
    for (x0, x1, y0, y1) in boxes:
        nx = max(2, int(round((x1 - x0) / 2.0)) + 1)
        xs = [x0 + (x1 - x0) * k / (nx - 1) for k in range(nx)]
        for x in xs:
            for y in (y0, y1):
                post(S, "col", x, y, max(0.5, z0), z1, 0.2, "girder", True)
        for z in levels:
            if z <= z0:
                continue
            for y in (y0, y1):
                beam(S, "gx", (x0, y, z), (x1, y, z), 0.14, "girder", noclip=True)
            for x in xs:
                beam(S, "gy", (x, y0, z), (x, y1, z), 0.14, "girder", noclip=True)
        if z0 <= 0.6:
            for a, b in zip(xs, xs[1:]):
                beam(S, "xb", (a, y0, 0.5), (b, y0, min(2.0, z1)), 0.07, "girder", noclip=True)
    for (cx, cy, r) in rings:
        pts = [(cx + r * math.cos(math.radians(k * 45 + 22.5)), cy + r * math.sin(math.radians(k * 45 + 22.5))) for k in range(8)]
        for (x, y) in pts:
            post(S, "dp", x, y, max(0.5, z0), z1, 0.14, "girder", True)
        for z in levels:
            if z > z0:
                for k in range(8):
                    a, b = pts[k], pts[(k + 1) % 8]
                    beam(S, "dring", (a[0], a[1], z), (b[0], b[1], z), 0.09, "girder", noclip=True)


def stages(S, state, i, spec, finished):
    """Drive one frame. spec: pad, env, H (envelope height), boxes, rings, clips (z for stages 4, 5), scaf [(x0,x1,y0,y1)],
    derrick (mx, my, h, tip), hide (name prefixes hidden in stage 6), left (fn for stage-6 leftovers).
    finished(S, i_anim, damaged, mode): mode 'idle' (all details) or 'build' (primer stages: no animated bits)."""
    if state != "build":
        finished(S, i, state == "damaged", "idle")
        return
    if i == 7:
        finished(S, 0, False, "idle")
        return
    H = spec["H"]
    if i in (0, 1):
        plot(S, spec["pad"], spec["env"], i)
        if i == 0:
            return
        spec["clutter"](S, i)
        return
    if i in (2, 3):
        plot(S, spec["pad"], spec["env"], 1)
        z1 = H * (0.5 if i == 2 else 1.0)
        # drop the stubs of stage 1 (they are replaced by the real columns)
        S.hide = lambda nm: nm.startswith(("stub", "rb", "mesh", "mixer", "sandpile", "form"))
        skeleton(S, spec["boxes"], spec["rings"], 0.5, z1)
        sc = spec["scaf"]
        lv = (0.8, 1.7) if i == 2 else (0.8, 1.7, 2.6)
        for k, t in enumerate(sc):
            scaffold(S, *t, lv if k == 0 else lv[:2])
        mx, my, mh, tip = spec["derrick"]
        derrick(S, mx, my, mh * (0.8 if i == 2 else 1.0), tip, None if i == 2 else "girder")
        for k, (sx, sy, sz) in enumerate(spec["sparks"]):
            spark(S, sx, sy, min(sz, z1), i, k + 1)
        spec["clutter"](S, 2)
        return
    S.skin.update(PRIMER_SKIN)
    if i in (4, 5):
        S.clip_z = spec["clips"][i - 4]
        finished(S, 0, False, "build")
        skeleton(S, spec["boxes"], spec["rings"], S.clip_z - 0.3, H)
        sc = spec["scaf"]
        for k, t in enumerate(sc):
            scaffold(S, *t, (0.8, 1.7, 2.6) if k == 0 else (0.8, 1.7), top=H + 0.5)
        mx, my, mh, tip = spec["derrick"]
        derrick(S, mx, my, mh, tip, "pallet" if i == 4 else "girder")
        for k, (sx, sy, sz) in enumerate(spec["sparks"][:2]):
            spark(S, sx, sy, min(sz, S.clip_z + 0.6), i, k + 3)
        return
    # stage 6: finished, primer, no house plates / animated bits, one scaffold tower left, leftovers on the pad
    S.hide = lambda nm: nm.startswith(tuple(spec["hide"]))
    finished(S, 0, False, "build")
    scaffold(S, *spec["scaf"][0], (0.8, 1.7), top=H * 0.7)
    spec["left"](S)


# ====================================================================================================== conyard
HX = 4.2
HY0, HY1 = 0.5, 3.7             # hangar south / north wall
WALL_H = 3.5
ROOF_T = 0.22
Z_ROOF = WALL_H + ROOF_T
GANTRY_Y = -3.0
SWAY = [0.0, 0.4, 0.0, -0.4]


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    yw = HY0 - 0.02
    # ---- pad, hazard apron, assembly bay slab with yellow border
    S.bb("pad", -5.0, 5.0, -5.0, 5.0, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    R.hazard(S, -4.88, -4.4)
    S.bb("bay", -3.9, 3.9, -4.0, -0.3, 0.14, 0.2, "concrete2", 0.0)
    for (a, b, c, d) in ((-3.9, 3.9, -4.0, -3.9), (-3.9, -3.8, -4.0, -0.3), (3.8, 3.9, -4.0, -0.3)):
        S.bb("bayline", a, b, c, d, 0.2, 0.22, "yellow", 0.0, nocast=True)
    # ---- hangar: walls, plinth, pilasters, roof
    west_x1 = 1.3 if dmg else HX
    S.bb("hall", -HX, west_x1, HY0, HY1, 0.14, WALL_H, "concrete", 0.05)
    S.bb("plinth", -HX - 0.1, west_x1 + (0.1 if not dmg else 0), HY0 - 0.1, HY1 + 0.1, 0.14, 0.55, "concrete2", 0.04)
    for px in (-HX, -2.7, 2.7, HX):
        if dmg and px > 1.3:
            continue
        S.bb(f"pil{px}", px - 0.22, px + 0.22, HY0 - 0.14, HY0 + 0.1, 0.14, WALL_H + 0.05, "concrete2", 0.04)
    if dmg:
        S.bb("roof", -HX - 0.25, 1.3, HY0 - 0.2, HY1 + 0.2, WALL_H, Z_ROOF, "roof", 0.06)
        S.bb("roofedge", 1.3, 1.5, HY0 - 0.2, HY1 + 0.2, WALL_H - 0.5, Z_ROOF - 0.05, "concrete2", 0.0)
        # collapsed east end: the walls stand ragged, the inside is a dark pit full of fallen slab
        S.bb("eastfloor", 1.3, HX, HY0, HY1, 0.14, 1.0, "dark", 0.0)
        S.bb("wallS", 1.3, HX, HY0, HY0 + 0.4, 0.14, 1.9, "concrete", 0.03)
        S.bb("wallSj", 3.0, HX, HY0, HY0 + 0.4, 1.9, 2.5, "concrete", 0.03)
        S.bb("wallSj", 3.6, HX, HY0, HY0 + 0.4, 2.5, 3.0, "concrete", 0.03)
        S.bb("wallN", 1.3, HX, HY1 - 0.4, HY1, 0.14, 3.0, "concrete", 0.03)
        S.bb("wallE", HX - 0.4, HX, HY0, HY1, 0.14, 2.2, "concrete", 0.03)
        S.bb("wallEj", HX - 0.4, HX, 1.4, HY1, 2.2, 3.4, "concrete", 0.03)
        S.bb("slabA", 1.6, 3.4, 0.9, 2.4, 0.9, 1.25, "roof", 0.04, rot=(0, -9, 6))
        S.bb("slabB", 2.9, 4.0, 1.9, 3.3, 1.0, 1.5, "roof", 0.04, rot=(0, 12, -8))
        S.bb("slabC", 1.8, 2.8, 2.4, 3.4, 1.2, 1.9, "concrete2", 0.04, rot=(10, 0, 14))
        for k, (gx, gz, tl) in enumerate(((1.7, 3.6, 18), (2.4, 3.5, -25), (3.2, 3.2, 40), (3.9, 3.0, -15))):
            S.box(f"bgird{k}", (0.14, 3.0, 0.14), (gx, 2.1 * YS, gz - 1.0), "girder", 0.0, rot=(tl, 0, 0), stretch=False)
        S.bb("rubbleS", 1.4, 3.3, -0.2, 0.3, 0.14, 0.5, "concrete2", 0.04)
        S.bb("rubbleS", 3.4, 4.4, -0.5, 0.4, 0.14, 0.8, "concrete", 0.04, rot=(0, 0, 25))
    else:
        S.bb("roof", -HX - 0.25, HX + 0.25, HY0 - 0.2, HY1 + 0.2, WALL_H, Z_ROOF, "roof", 0.06)
        S.bb("parapet", -HX - 0.25, HX + 0.25, HY0 - 0.2, HY0 - 0.02, Z_ROOF, Z_ROOF + 0.22, "concrete2", 0.03)
        for sx in (-1, 1):
            S.bb("parapetS", sx * (HX + 0.07) - 0.07, sx * (HX + 0.07) + 0.07, HY0 - 0.2, HY1 + 0.2, Z_ROOF, Z_ROOF + 0.18, "concrete2", 0.02)
    # ---- south face: big door, house panels on concrete frames, stencils
    S.bb("door", -1.7, 1.7, yw - 0.06, yw + 0.1, 0.14, 2.7, "dark", 0.0)
    S.bb("lintel", -2.0, 2.0, yw - 0.16, yw + 0.1, 2.7, 3.15, "concrete2", 0.04)
    S.bb("lintelstripe", -1.95, 1.95, yw - 0.18, yw - 0.14, 2.85, 3.05, "yellow", 0.0)
    for k in range(6):          # door ribs
        S.bb(f"rib{k}", -1.45 + k * 0.58, -1.37 + k * 0.58, yw - 0.1, yw - 0.04, 0.2, 2.6, "steel", 0.0, nocast=True)
    for sx in (-1, 1):
        S.bb("jamb", sx * 1.9 - 0.17, sx * 1.9 + 0.17, yw - 0.14, yw + 0.1, 0.14, 2.75, "concrete2", 0.03)
        cx = sx * 3.15
        if dmg and sx > 0:
            S.bb("pframe", 2.35, 3.45, yw - 0.08, yw + 0.02, 0.62, 3.1, "dark", 0.0)
            S.bb("panel", 2.45, 3.3, yw - 0.14, yw - 0.06, 0.72, 2.3, "house", 0.0)
        else:
            S.bb("pframe", cx - 0.95, cx + 0.95, yw - 0.08, yw + 0.02, 0.62, 3.1, "dark", 0.0)
            S.bb("panel", cx - 0.85, cx + 0.85, yw - 0.14, yw - 0.06, 0.72, 3.0, "house", 0.0)
        for k in range(3):
            S.bb("sten", cx - 0.5 + k * 0.45, cx - 0.28 + k * 0.45, yw - 0.1, yw - 0.06, 0.2, 0.45, "white", 0.0, nocast=True)
    for k, (rx, rw, rh) in enumerate(((-2.7, 0.16, 0.9), (0.2, 0.14, 0.5), (2.0, 0.18, 0.7), (-4.0, 0.16, 0.6))):
        S.bb(f"streak{k}", rx, rx + rw, yw - 0.12, yw - 0.1, 3.5 - rh, 3.5, "rust", 0.0, nocast=True)
    # ---- roof: big house marking, vents, duct, radio mast
    px1 = 0.6 if dmg else 3.4
    S.bb("roofhouse", -3.5, px1, 1.0, 2.4, Z_ROOF, Z_ROOF + 0.04, "house2", 0.0)
    S.bb("roofframe", -3.6, px1 + 0.1, 0.9, 2.5, Z_ROOF, Z_ROOF + 0.02, "dark", 0.0)
    if not dmg:
        S.bb("vent", 1.2, 2.4, 2.6, 3.4, Z_ROOF, Z_ROOF + 0.4, "concrete2", 0.04)
        for k in range(4):
            S.bb(f"grille{k}", 1.3 + k * 0.28, 1.45 + k * 0.28, 2.58, 3.4, Z_ROOF + 0.4, Z_ROOF + 0.46, "dark", 0.0)
    S.bb("hatch", -3.9, -2.6, 2.7, 3.5, Z_ROOF, Z_ROOF + 0.18, "concrete2", 0.04)
    mx, my = 3.5, 3.0
    if dmg:      # the mast is sheared and leans over the pit
        for ax in (-0.12, 0.12):
            for ay in (-0.12, 0.12):
                S.box("mast", (0.07, 0.07, 2.1), (-3.0 + ax, (my + ay) * YS, Z_ROOF + 1.0), "steel", 0.0, rot=(0, -12, 0), stretch=False, wo=(0.0, 0.0))
        S.bb("mastbase", -3.3, -2.7, my - 0.3, my + 0.3, Z_ROOF, Z_ROOF + 0.3, "concrete2", 0.04)
    else:
        for ax in (-0.14, 0.14):
            for ay in (-0.14, 0.14):
                post(S, "mast", mx + ax, my + ay, Z_ROOF, Z_ROOF + 2.0, 0.07, "steel")
        z = Z_ROOF
        while z < Z_ROOF + 1.9:
            beam(S, "mb", (mx - 0.14, my - 0.14, z), (mx + 0.14, my - 0.14, z + 0.65), 0.04, "steel")
            beam(S, "mb", (mx + 0.14, my - 0.14, z + 0.65), (mx - 0.14, my - 0.14, z + 1.3), 0.04, "steel", nocast=True)
            z += 1.3
        S.bb("dish", mx - 0.5, mx + 0.5, my - 0.08, my + 0.08, Z_ROOF + 1.3, Z_ROOF + 1.8, "white", 0.03)
        S.bb("mastbase", mx - 0.4, mx + 0.4, my - 0.4, my + 0.4, Z_ROOF, Z_ROOF + 0.3, "concrete2", 0.04)
        lit = idle and i in (0, 1)
        S.bb("mastlamp", mx - 0.12, mx + 0.12, my - 0.12, my + 0.12, Z_ROOF + 2.0, Z_ROOF + 2.25, "red" if lit else "dark", 0.0)
    # ---- command flag area: pole on the east pad strip, big house flag
    S.cyl("pole", 0.06, 4.4, (4.65, -0.2, 2.35), "steel", verts=8)
    S.bb("flag", 3.35, 4.62, -0.24, -0.16, 3.2, 4.3, "house", 0.0)
    S.bb("flagbar", 3.3, 3.37, -0.26, -0.14, 3.15, 4.35, "white", 0.0, nocast=True)
    S.bb("flagbase", 4.35, 4.95, -0.5, 0.1, 0.14, 0.4, "concrete2", 0.03)
    # ---- gantry crane over the assembly bay: two portal frames + yellow bridge beam + trolley + swaying hook
    gy0, gy1 = GANTRY_Y - 0.18, GANTRY_Y + 0.18
    for sx in (-1, 1):
        x = sx * 4.3
        rail_y0, rail_y1 = -3.9, -0.5
        S.bb("rail", x - 0.2, x + 0.2, rail_y0, rail_y1, 0.14, 0.3, "steel", 0.02)
        for yy in (GANTRY_Y - 0.4, GANTRY_Y + 0.4):
            if dmg and sx > 0 and yy > GANTRY_Y:
                continue
            post(S, "gleg", x, yy, 0.3, 3.3 if not (dmg and sx > 0) else 1.0, 0.22, "steel")
        if dmg and sx > 0:
            S.bb("legstump", x - 0.17, x + 0.17, GANTRY_Y - 0.57, GANTRY_Y - 0.23, 0.3, 1.2, "steel", 0.02)
    if dmg:     # the bridge beam has slumped: west end still on its leg, east end resting on the stump / bay floor
        beam(S, "gbeam", (-4.3, GANTRY_Y, 3.45), (4.0, GANTRY_Y - 0.25, 1.4), 0.34, "yellow")
        beam(S, "gbeam2", (-4.3, GANTRY_Y - 0.3, 3.45), (4.0, GANTRY_Y - 0.5, 1.45), 0.12, "steel")
        sw = SWAY[i % 4] * 0.4 if idle else 0.0
        beam(S, "gcable", (0.0, GANTRY_Y, 2.5), (1.6 + sw, -3.6, 0.3), 0.04, "dark")
        S.bb("ghook", 1.45 + sw, 1.75 + sw, -3.75, -3.45, 0.14, 0.42, "dark", 0.0)
    else:
        S.bb("gbeam", -4.5, 4.5, gy0, gy1, 3.3, 3.58, "yellow", 0.03)
        S.bb("gbeamd", -4.5, 4.5, gy0 - 0.03, gy0, 3.35, 3.5, "dark", 0.0, nocast=True)
        tx = 0.7
        S.bb("trolley", tx - 0.45, tx + 0.45, GANTRY_Y - 0.28, GANTRY_Y + 0.28, 3.58, 3.88, "steel", 0.03)
        hx_ = tx + SWAY[i % 4] if idle else tx
        beam(S, "cable", (tx, GANTRY_Y, 3.58), (hx_, GANTRY_Y, 2.3), 0.04, "dark")
        S.bb("hook", hx_ - 0.13, hx_ + 0.13, GANTRY_Y - 0.13, GANTRY_Y + 0.13, 2.1, 2.34, "dark", 0.0)
        S.bb("load", hx_ - 0.75, hx_ + 0.75, GANTRY_Y - 0.06, GANTRY_Y + 0.06, 1.15, 2.1, "panel", 0.02)
        S.bb("loadst", hx_ - 0.7, hx_ + 0.7, GANTRY_Y - 0.1, GANTRY_Y - 0.05, 1.3, 1.75, "house", 0.0)
        for sx in (-0.7, 0.7):
            beam(S, "sling", (hx_, GANTRY_Y, 2.1), (hx_ + sx, GANTRY_Y, 2.1 - 0.02), 0.02, "dark", nocast=True)
            beam(S, "sling", (hx_ + sx, GANTRY_Y, 2.08), (hx_ + sx, GANTRY_Y, 1.9), 0.03, "dark", nocast=True)
    # ---- assembly bay contents: chassis frame under construction, stacked prefab panels, crates
    S.bb("chassis", -1.6, 1.2, -2.35, -1.75, 0.2, 0.55, "steel", 0.03)
    for k in range(5):
        S.bb(f"cframe{k}", -1.55 + k * 0.65, -1.45 + k * 0.65, -2.3, -1.8, 0.55, 1.25, "girder", 0.0)
    S.bb("cbeam", -1.6, 1.2, -2.35, -2.3, 1.15, 1.3, "girder", 0.0)
    S.bb("cpanel", -1.45, -0.2, -2.38, -2.34, 0.58, 1.2, "house", 0.0)
    for k, (m, w) in enumerate((("panel", 1.7), ("panel2", 1.6), ("panel", 1.5), ("panel2", 1.4))):
        if dmg and k >= 2:
            S.bb(f"stack{k}", -3.7 + 0.1 * k, -3.7 + w + 0.1 * k + 0.3, -3.6 + 0.35 * (k - 2), -2.8 + 0.35 * (k - 2), 0.22, 0.4, m, 0.02, rot=(0, 0, 28 - 20 * k))
        else:
            S.bb(f"stack{k}", -3.7, -3.7 + w, -3.7, -2.9, 0.2 + 0.26 * k, 0.2 + 0.26 * (k + 1), m, 0.02)
    S.bb("stackstrap", -3.4, -3.3, -3.72, -2.88, 0.2, 0.6 if dmg else 1.25, "yellow", 0.0, nocast=True)
    S.bb("crate", 2.5, 3.4, -3.8, -3.0, 0.2, 0.9, "crate", 0.03)
    S.bb("crate", 2.65, 3.25, -3.7, -3.1, 0.9, 1.4, "crate", 0.03, rot=(0, 0, 10))
    S.cyl("barrel", 0.28, 0.7, (3.65, -3.7, 0.55), "rust", verts=12)
    if dmg:
        scorch = R.scorch
        scorch(S, "scW", 3.0, yw - 0.18, 1.3, 1.6, 0.8)
        scorch(S, "scH", 2.0, yw - 0.12, 2.2, 1.2, 0.5)
        scorch(S, "scR", -0.4, 2.2, Z_ROOF + 0.03, 1.8, 1.2, 0.04, plane="T")
        scorch(S, "scG", 2.6, -3.7, 0.23, 2.0, 1.4, 0.03, plane="T")
        # cracks in the intact walls
        for k, (cx_, zl, zh) in enumerate(((-3.1, 0.9, 3.4), (0.4, 2.8, 3.4), (-0.9, 0.3, 1.2))):
            S.bb(f"crack{k}", cx_, cx_ + 0.07, yw - 0.17, yw - 0.13, zl, zh, "crack", 0.0, nocast=True, rot=(0, 0, 0))
            S.bb(f"crackb{k}", cx_ - 0.25, cx_ + 0.3, yw - 0.17, yw - 0.13, (zl + zh) / 2, (zl + zh) / 2 + 0.07, "crack", 0.0, nocast=True)


FX = [
    dict(state="damaged", kind="fire_large", x=anchor(FOOTPRINT, ORIGIN, 3.0, 2.0, 1.1)[0], y=anchor(FOOTPRINT, ORIGIN, 3.0, 2.0, 1.1)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=anchor(FOOTPRINT, ORIGIN, 2.0, 0.6, 1.4)[0], y=anchor(FOOTPRINT, ORIGIN, 2.0, 0.6, 1.4)[1], phase=3),
    dict(state="damaged", kind="fire_small", x=anchor(FOOTPRINT, ORIGIN, 3.9, -0.3, 0.6)[0], y=anchor(FOOTPRINT, ORIGIN, 3.9, -0.3, 0.6)[1], phase=5),
    dict(state="damaged", kind="smoke_plume", x=anchor(FOOTPRINT, ORIGIN, 3.0, 2.0, 1.2)[0], y=anchor(FOOTPRINT, ORIGIN, 3.0, 2.0, 1.2)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=anchor(FOOTPRINT, ORIGIN, 3.8, -2.5, 1.0)[0], y=anchor(FOOTPRINT, ORIGIN, 3.8, -2.5, 1.0)[1], phase=6),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", -4.7, -3.8, -4.5, -3.6, 0.14, 0.85, "crate", 0.03)
        S.bb("crate", 3.5, 4.4, -4.6, -3.7, 0.14, 0.8, "crate", 0.03)
    if i <= 2:
        for k, (x, z) in enumerate(((-1.0, 0.3), (-1.0, 0.65), (-1.3, 0.98))):
            S.cyl("lpipe", 0.14, 2.6, (x, -4.3, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", 0.7, 3.0, -4.3, -4.1, 0.14, 0.3, "rebar", 0.0)
        S.cyl("barrel", 0.3, 0.7, (4.4, -3.2, 0.5), "rust", verts=12)
    S.bb("planks", 2.4, 4.4, 4.0, 4.5, 0.14, 0.5, "timber", 0.0)
    S.bb("cabin", -4.7, -3.0, -0.4, 0.4, 0.14, 1.15, "primer", 0.03)
    S.bb("cabdoor", -4.2, -3.8, -0.46, -0.4, 0.14, 0.85, "dark", 0.0)


def _left(S):
    S.bb("planks", -3.9, -2.0, -3.9, -3.5, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 2.6, (2.2, -4.2, 0.3), "steel", axis="X", verts=10)
    S.bb("crate", 3.7, 4.5, -4.5, -3.8, 0.14, 0.8, "crate", 0.03)
    spark(S, -1.5, -0.4, 3.3, 3, 6)


SPEC = dict(
    pad=(-5.0, 5.0, -5.0, 5.0), env=(-HX, HX, HY0, HY1), H=WALL_H, boxes=[(-HX, HX, HY0, HY1)], rings=[],
    clips=(1.5, 2.9), scaf=[(-3.9, -2.6, -0.6, 0.1), (2.6, 3.9, -0.6, 0.1)],
    derrick=(4.7, 2.0, 5.4, (1.4, 1.5, 6.1)), sparks=[(-2.0, 0.5, 2.1), (1.0, 3.7, 3.5), (3.0, 0.5, 3.4)],
    hide=("sten", "cpanel", "loadst", "mastlamp", "flag"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    stages(S, state, i, SPEC, finished)
