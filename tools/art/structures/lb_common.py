"""Shared kit for the Left Behind structure modules (not an asset itself).  Original design.

Materials in PAL_LB tones (dust ochre, canvas, rust, faded teal, ash, bone, a little hazard yellow), a soot skin for the damaged
state, a generic unfinished "build" skin, geometry helpers (beams, posts, tyres, drums, awnings, flags, stovepipes with smoke,
sparks), pixel-anchor maths and the build-stage driver.  Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C

PALETTE = C.PAL_LB

MATERIALS = {
    "dust": ("#7A6040", dict(rough=1.0)), "dust2": ("#58452B", dict(rough=1.0)),
    "canvas": ("#D6C8A2", dict(rough=1.0)), "canvas2": ("#B5A57C", dict(rough=1.0)),
    "ochre": ("#B38A44", dict(rough=0.9)), "rust": ("#8A3E24", dict(rough=0.95)), "rust2": ("#5A2A1A", dict(rough=0.95)),
    "teal": ("#4F746E", dict(rough=0.8)), "teal2": ("#34524E", dict(rough=0.8)),
    "ash": ("#7A766F", dict(rough=0.8)), "ash2": ("#55524D", dict(rough=0.8)),
    "bone": ("#E6DDC4", dict(rough=0.85)), "hazard": ("#DDB93A", dict(rough=0.8)),
    "dark": ("#1A140E", dict(rough=0.9)), "rubber": ("#2A2320", dict(rough=0.95)),
    "steel": ("#8E8A82", dict(rough=0.5, metal=0.5)), "wood": ("#6B5232", dict(rough=0.95)),
    "rope": ("#C8B88A", dict(rough=1.0)), "glass": ("#2F4A47", dict(rough=0.25, metal=0.3)),
    "lamp": ("#FFE08A", dict(rough=0.3, emit="#FFE08A")), "lamp_off": ("#6B5A2A", dict(rough=0.6)),
    "flame": ("#F2B84A", dict(rough=0.5, emit="#F2A030")), "flame2": ("#D9602A", dict(rough=0.5, emit="#D05020")),
    "spark": ("#FFF0B0", dict(rough=0.3, emit="#FFF0B0")),
    "smoke": ("#B5B0A6", dict(rough=1.0)), "smoke2": ("#7A766F", dict(rough=1.0)),
    "char": ("#241C14", dict(rough=1.0)), "primer": ("#9A8F78", dict(rough=0.9)),
    "gunmetal": ("#3A3733", dict(rough=0.4, metal=0.6)),
    "teal_s": ("#2E4440", dict(rough=0.8)), "canvas_s": ("#7F7458", dict(rough=1.0)), "canvas2_s": ("#645B44", dict(rough=1.0)),
    "rust_s": ("#5C2A1A", dict(rough=0.95)), "ochre_s": ("#6E5429", dict(rough=0.9)), "bone_s": ("#8F8872", dict(rough=0.85)),
    "ash_s": ("#47443F", dict(rough=0.8)), "dust_s": ("#4E3C26", dict(rough=1.0)), "teal2_s": ("#243836", dict(rough=0.8)),
}
SOOT_SKIN = {"teal": "teal_s", "teal2": "teal2_s", "canvas": "canvas_s", "canvas2": "canvas2_s", "rust": "rust_s", "ochre": "ochre_s",
             "bone": "bone_s", "ash": "ash_s", "dust": "dust_s", "hazard": "ochre_s", "steel": "ash_s"}
# unfinished look for the second-to-last build frame: bare primer instead of paint, no house colour
PRIMER_SKIN = {"house": "primer", "teal": "primer", "teal2": "primer", "canvas": "primer", "canvas2": "primer", "rust": "primer",
               "ochre": "primer", "bone": "primer", "hazard": "primer", "lamp": "dark", "flame": "dark", "flame2": "dark"}


def anchor(fp, origin, x, y, z):
    """Frame pixel of a point in game units for a footprint of fp cells with its top-left corner at `origin`."""
    cx = origin[0] + fp[0] * 32
    cy = origin[1] + fp[1] * 32
    return round(cx + x * 18.8), round(cy - y * 18.8 - z * 14.4)


def fx(fp, origin, state, kind, x, y, z, phase=0):
    px, py = anchor(fp, origin, x, y, z)
    return dict(state=state, kind=kind, x=px, y=py, phase=phase)


def beam(S, name, p0, p1, w, m, nocast=False):
    """Square-section member between two points in game units (x, y, z); y is stretched here."""
    YS = S.YS
    a = (p0[0], p0[1] * YS, p0[2])
    b = (p1[0], p1[1] * YS, p1[2])
    dx, dy, dz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    ln = math.sqrt(dx * dx + dy * dy + dz * dz)
    yaw = math.degrees(math.atan2(dy, dx))
    pitch = -math.degrees(math.atan2(dz, math.hypot(dx, dy)))
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2)
    return S.box(name, (ln, w, w), mid, m, 0.0, rot=(0, pitch, yaw), stretch=False, nocast=nocast)


def post(S, name, x, y, z0, z1, w, m):
    return S.bb(name, x - w / 2, x + w / 2, y - w / 2, y + w / 2, z0, z1, m, 0.0)


def pad(S, hx, hy, z=0.1, m="dust"):
    """Trampled dust pad with a few scuffs (never clean)."""
    S.bb("pad", -hx, hx, -hy, hy, 0.0, z, m, 0.03)
    S.bb("scuff1", -hx + 0.3, -hx * 0.2, -hy + 0.1, -hy + 0.45, z, z + 0.03, "dust2", 0.0, nocast=True)
    S.bb("scuff2", hx * 0.1, hx - 0.3, -hy + 0.15, -hy + 0.5, z, z + 0.03, "dust2", 0.0, nocast=True)
    S.bb("scuff3", -hx + 0.1, -hx + 0.45, -hy * 0.4, hy * 0.5, z, z + 0.03, "dust2", 0.0, nocast=True)


def tyre(S, name, x, y, z, r=0.5, w=0.4, m="rubber", axis="Z"):
    """Tyre standing (axis Y) or lying flat (axis Z); y is the game y."""
    if axis == "Z":
        S.cyl(name, r, w, (x, y, z + w / 2), m, verts=16)
        S.cyl(name + "h", r * 0.55, w + 0.02, (x, y, z + w / 2), "dust2", verts=12, nocast=True)
    else:
        S.cyl(name, r, w, (x, y * S.YS, z + r), m, axis="Y", verts=16, stretch=False)


def wheel(S, name, x, y, z, r, w=0.5, hub="steel"):
    """Wheel with its axle along Y at game y (south-facing disc)."""
    yy = y * S.YS
    S.cyl(name, r, w, (x, yy, z), "rubber", axis="Y", verts=18, stretch=False)
    S.cyl(name + "h", r * 0.5, w + 0.05, (x, yy - 0.02, z), hub, axis="Y", verts=12, stretch=False)
    S.cyl(name + "n", r * 0.17, w + 0.1, (x, yy - 0.03, z), "dark", axis="Y", verts=6, stretch=False)


def drum(S, name, x, y, z, m="rust", band="ash2", h=0.9, r=0.42):
    S.cyl(name, r, h, (x, y, z + h / 2), m, verts=12)
    S.cyl(name + "b", r + 0.02, 0.1, (x, y, z + h * 0.55), band, verts=12)


def stovepipe(S, name, x, y, z0, z1, anim=0, smoke=True, m="ash", r=0.2, broken=False):
    S.cyl(name, r, z1 - z0, (x, y, (z0 + z1) / 2), m, verts=12)
    S.cyl(name + "r", r + 0.04, 0.14, (x, y, z0 + (z1 - z0) * 0.4), "rust", verts=12)
    if broken:
        S.cyl(name + "x", r, 0.1, (x, y, z1 + 0.03), "dark", verts=10)
        return
    S.cyl(name + "c", r + 0.12, 0.12, (x, y, z1 + 0.06), "rust2", verts=12)
    if smoke:
        for q in range(3):
            ph = ((anim / 4) + q / 3) % 1
            rr = 0.14 + 0.36 * math.sin(math.pi * ph) ** 0.7
            S.sphere(f"{name}puff{q}", rr, (x + ph * 0.9, y, z1 + 0.5 + ph * 0.8), "smoke2" if q != 1 else "smoke", squash=0.85)


def flag(S, name, mx, my, ztop, anim, w=1.4, h=0.95, dmg=False, mast=True):
    """House-colour canvas flag on a pole; flaps with anim (0..3)."""
    ph = anim * math.pi / 2
    if mast:
        post(S, name + "mast", mx, my, 0.0, ztop, 0.1, "wood")
        S.sphere(name + "knob", 0.1, (mx, my, ztop + 0.08), "hazard")
    if dmg:
        S.box(name, (w * 0.6, 0.05, h), (mx + 0.1 + w * 0.3, (my - 0.08) * S.YS, ztop - h / 2 - 0.1), "house", 0.0, stretch=False)
        return
    fw = w + 0.18 * math.sin(ph)
    zc = ztop - h / 2 - 0.08
    rz = 5 * math.sin(ph)
    S.box(name, (fw, 0.05, h), (mx + 0.06 + fw / 2, (my - 0.08 + 0.05 * math.cos(ph)) * S.YS, zc), "house", 0.0, rot=(0, 0, rz), stretch=False)
    S.box(name + "t", (fw, 0.06, 0.1), (mx + 0.06 + fw / 2, (my - 0.1) * S.YS, zc + h / 2 - 0.03), "rust", 0.0, rot=(0, 0, rz), stretch=False, nocast=True)


def awning(S, name, x0, x1, y_wall, y_far, z_wall, rise, anim=0, dmg=False, mats=("canvas2", "teal"), strip=1.25, ph=0.0, fringe=True):
    """Striped canvas awning off a wall at y_wall down to y_far (y_far < y_wall, south); sways with anim."""
    YS = S.YS
    if dmg:
        rise = rise * 2.0
    else:
        rise = rise + 0.12 * math.sin(anim * math.pi / 2 + ph)
    run = y_wall - y_far
    ang = math.degrees(math.atan2(rise, run * YS))
    ln = math.hypot(run * YS, rise) / YS
    ym = (y_wall + y_far) / 2
    zm = z_wall - rise / 2
    n = max(1, int(round((x1 - x0) / strip)))
    sw = (x1 - x0) / n
    for k in range(n):
        m = mats[k % len(mats)]
        if dmg:
            m = {"canvas2": "canvas2_s", "teal": "teal_s", "canvas": "canvas_s", "rust": "rust_s"}.get(m, m)
            if k == 1 and n > 2:
                continue
        S.box(f"{name}{k}", (sw, ln, 0.07), (x0 + sw * (k + 0.5), ym, zm), m, 0.0, rot=(ang, 0, 0))
    zf = z_wall - rise
    if fringe:
        for k in range(n * 2):
            if dmg and k % 3 == 1:
                continue
            x = x0 + k * sw / 2
            sway = 0.04 * math.sin((anim + k) * 1.7)
            S.bb(f"{name}fr{k}", x, x + sw * 0.4, y_far - 0.06 + sway, y_far + sway, zf - (0.28 if not dmg else 0.12), zf,
                 "house" if k == 2 else ("canvas2" if k % 2 else "rust"), 0.0, nocast=True)
    for px in (x0 + 0.05, x1 - 0.05):
        post(S, name + "p", px, y_far + 0.1, 0.0, zf + 0.05, 0.1, "wood")


def spark(S, x, y, z, i, seed=0):
    S.bb("weld", x - 0.1, x + 0.1, y - 0.1, y + 0.1, z - 0.1, z + 0.1, "spark", 0.0, nocast=True)
    for k in range(5):
        a = (i * 2.1 + k * 1.7 + seed) * 1.3
        r = 0.22 + 0.2 * ((k * 5 + i * 3 + seed) % 4)
        cx, cy = x + r * math.cos(a), y + r * math.sin(a) * 0.5
        S.bb(f"sprk{k}", cx - 0.05, cx + 0.05, cy - 0.05, cy + 0.05, z + 0.15 * k - 0.15, z + 0.15 * k - 0.05, "spark", 0.0, nocast=True)


def stakes_site(S, hx, hy, sign=True):
    """Build frame 0: surveyed plot with stakes, rope and the chalk board (MOTHER-RIG style site)."""
    S.bb("pad", -hx - 0.1, hx + 0.1, -hy - 0.1, hy + 0.1, 0.0, 0.1, "dust", 0.03)
    for (sx, sy) in ((-hx, -hy), (hx, -hy), (-hx, hy), (hx, hy)):
        post(S, "stake", sx, sy, 0.1, 1.0, 0.12, "wood")
    S.bb("line", -hx, hx, -hy - 0.04, -hy + 0.04, 0.88, 0.92, "rope", 0.0, nocast=True)
    S.bb("line", -hx, hx, hy - 0.04, hy + 0.04, 0.88, 0.92, "rope", 0.0, nocast=True)
    if sign:
        post(S, "boardp", -0.5, -hy - 0.2, 0.1, 1.2, 0.1, "wood")
        post(S, "boardp", 0.5, -hy - 0.2, 0.1, 1.2, 0.1, "wood")
        S.bb("board", -0.8, 0.8, -hy - 0.3, -hy - 0.15, 0.7, 1.5, "dark", 0.0)
        S.bb("boardc", -0.6, 0.6, -hy - 0.32, -hy - 0.3, 0.95, 1.3, "bone", 0.0, nocast=True)
    S.cyl("kitroll", 0.3, 1.4, (-hx * 0.4, hy * 0.3, 0.4), "canvas", axis="X", verts=12)
    S.bb("kitpile", hx * 0.2, hx * 0.7, hy * 0.1, hy * 0.6, 0.1, 0.5, "canvas2", 0.1)


def stage_driver(S, state, i, n, groups, site_fn, dmg_fn=None, finished_n=None):
    """Generic Left Behind build/idle/damaged driver.

    groups: list of callables f(S, dmg, anim) each adding one tier of the structure (bottom to top).  The finished structure is
    every group in order.  In the "build" state frame 0 is the surveyed site (site_fn), frame 7 (the last) is the finished
    structure, frame 6 the finished structure in primer without the last group; frames in between add groups progressively.
    """
    building = state == "build"
    dmg = state == "damaged"
    if dmg:
        S.skin.update(SOOT_SKIN)
    if not building:
        for g in groups:
            g(S, dmg, i)
        if dmg and dmg_fn:
            dmg_fn(S, i)
        return
    last = n - 1
    if i == 0:
        site_fn(S)
        return
    if i == last:
        for g in groups:
            g(S, False, 0)
        return
    k = max(1, round(i * len(groups) / last))
    if i == last - 1:
        k = len(groups) - 1 if len(groups) > 1 else 1
        S.skin.update(PRIMER_SKIN)
    for g in groups[:k]:
        g(S, False, 0)


# ---------------------------------------------------------------------------- turret helpers (head-local coordinates)
def part(S, name, size, c, m, th, bevel=0.03, pitch=0.0, nocast=False):
    t = math.radians(th)
    wx = c[0] * math.cos(t) + c[1] * math.sin(t)
    wy = -c[0] * math.sin(t) + c[1] * math.cos(t)
    return S.box(name, size, (wx, wy, c[2]), m, bevel, rot=(pitch, 0, -th), stretch=False, nocast=nocast)


def cpart(S, name, r, h, c, m, th, axis="Z", verts=16):
    t = math.radians(th)
    wx = c[0] * math.cos(t) + c[1] * math.sin(t)
    wy = -c[0] * math.sin(t) + c[1] * math.cos(t)
    o = S.cyl(name, r, h, (wx, wy, c[2]), m, axis="Z", verts=verts, stretch=False)
    if axis == "Y" and o is not None:
        o.rotation_euler = (math.radians(90), 0, -t)
    return o


def turret_build(S, state, i, base, head, site_fn, hide_prefixes=()):
    """Frame driver for rotating-head defences (same sheet layout as auth_turret / dir_pillbox):
    idle 16 facings x 1, fire 16 x 2 (k0 flash + recoil, k1 dying), damaged 16 x 1, build 1 x 6 (stage 5 = idle facing 0)."""
    if state == "idle":
        base(S, False)
        head(S, i * 22.5, False, 0)
    elif state == "fire":
        f, k = divmod(i, 2)
        base(S, False)
        head(S, f * 22.5, False, 1 + k)
    elif state == "damaged":
        S.skin.update(SOOT_SKIN)
        base(S, True)
        head(S, i * 22.5, True, 0)
    else:
        if i == 0:
            site_fn(S)
            return
        if i == 1:
            base(S, False)
            return
        if i == 2:
            base(S, False)
            head(S, 0.0, False, 0)
            S.hide = lambda nm: not nm.startswith(("pad", "ring", "tyre", "base", "step", "turn", "collar", "col", "post", "ty", "plank", "deck", "sb", "d0", "d1"))
            return
        if i == 3 or i == 4:
            if i == 3:
                S.skin.update(PRIMER_SKIN)
                S.hide = lambda nm: nm.startswith(hide_prefixes)
            base(S, False)
            head(S, 0.0, False, 0)
            return
        base(S, False)
        head(S, 0.0, False, 0)
