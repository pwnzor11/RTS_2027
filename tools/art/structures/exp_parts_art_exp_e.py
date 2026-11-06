"""Extra parts for the Expedition structure fan-out, package art-exp-e (not an asset itself).  Original design.

Helpers on top of exp_common (K): rectangular apron, navy plinth with the ash tidemark, liner decks with window bands, quay edge and
pontoons for the water buildings, gun turrets, missiles, radar bars, a surveyed water site for build frame 0.
Units: 3.4 per cell, x east, y NORTH, z up.  All helpers take the Ctx `S` handed in by build_structure.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K


def apron(S, x0, x1, y0, y1, z=0.12, mat="pad", lines=True):
    """Poured apron on an arbitrary rectangle with an orange edge line and scuffs."""
    S.bb("apron", x0, x1, y0, y1, 0.0, z, mat, 0.03)
    if lines:
        for sx in (x0 + 0.25, x1 - 0.25):
            S.bb("aedgeX", sx - 0.06, sx + 0.06, y0 + 0.2, y1 - 0.2, z, z + 0.02, "orange", 0.0, nocast=True)
        S.bb("aedgeS", x0 + 0.2, x1 - 0.2, y0 + 0.2, y0 + 0.32, z, z + 0.02, "orange", 0.0, nocast=True)
    S.bb("ascuff1", x0 + 0.5, (x0 + x1) / 2, y1 - 0.9, y1 - 0.5, z, z + 0.02, "pad2", 0.0, nocast=True)
    S.bb("ascuff2", (x0 + x1) / 2 + 0.4, x1 - 0.6, y0 + 0.8, y0 + 1.1, z, z + 0.02, "pad2", 0.0, nocast=True)


def tide(S, x0, x1, y0, y1, z0=0.12, z1=0.34):
    """Grey ash tidemark band wrapped round a block (slightly proud of it)."""
    S.bb("tide", x0 - 0.03, x1 + 0.03, y0 - 0.03, y1 + 0.03, z0, z1, "ash", 0.0)


def block(S, name, x0, x1, y0, y1, z0, z1, mat, bevel=0.08, tidemark=False):
    S.bb(name, x0, x1, y0, y1, z0, z1, mat, bevel)
    if tidemark:
        tide(S, x0, x1, y0, y1, z0, z0 + 0.22)


def windows(S, name, x0, x1, y, z0, z1, n=1, gap=0.18, mat="glass"):
    """Window band on a south face at game y, n panes between x0 and x1."""
    w = (x1 - x0 - gap * (n - 1)) / n
    for k in range(n):
        a = x0 + k * (w + gap)
        S.bb(f"{name}{k}", a, a + w, y - 0.03, y, z0, z1, mat, 0.0, nocast=True)


def trim(S, name, x0, x1, y, z, h=0.1, mat="copper"):
    S.bb(name, x0, x1, y - 0.04, y, z, z + h, mat, 0.0, nocast=True)


def stripe(S, name, x0, x1, y, z0, z1, mat="house"):
    S.bb(name, x0, x1, y - 0.04, y, z0, z1, mat, 0.0, nocast=True)


def door(S, name, x0, x1, y, z0=0.12, z1=1.2, mat="orange"):
    S.bb(name, x0, x1, y - 0.04, y, z0, z1, mat, 0.0, nocast=True)
    S.bb(name + "c", x0 - 0.15, x1 + 0.15, y - 0.4, y, z1, z1 + 0.1, "navy2", 0.02)


def radar_bar(S, name, x, y, z, i, n=8, span=2.0, dmg=False, mat="chrome"):
    """Turning radar array: mast, slab antenna rotated by frame i of n (full circle over n frames) and a copper feed."""
    K.post(S, name + "m", x, y, z, z + 0.5, 0.18, "chrome2")
    S.cyl(name + "hub", 0.25, 0.2, (x, y, z + 0.55), "copper", verts=12)
    if dmg:
        ang = 28
    else:
        ang = (i * 180.0 / n) % 360
    a = math.radians(ang)
    S.box(name + "ant", (span, 0.12, 0.7), (x, y * S.YS, z + 1.0), mat, 0.02, rot=(0, 0, ang), stretch=False)
    S.box(name + "ant2", (span * 0.8, 0.14, 0.12), (x + 0.12 * math.sin(a), (y - 0.12 * math.cos(a) * 0) * S.YS, z + 1.4), "white2", 0.0, rot=(0, 0, ang), stretch=False, nocast=True)
    S.sphere(name + "feed", 0.1, (x + math.cos(a) * span * 0.2, y + math.sin(a) * span * 0.2, z + 1.05), "orange")


def dome(S, name, x, y, z, r, mat="white", band=True, squash=0.9):
    S.cyl(name + "b", r * 0.92, 0.22, (x, y, z + 0.11), "chrome2", verts=20)
    S.sphere(name, r, (x, y, z + 0.2), mat, nocast=False, squash=squash)
    if band:
        S.cyl(name + "r", r * 0.99, 0.08, (x, y, z + 0.24), "copper", verts=20)


def gun_turret(S, name, x, y, z, dx, dy, dmg=False, recoil=0.0, size=0.85, barrel=2.2, pitch=0.25):
    """Heavy twin gun in a round navy shield on a chrome race, pointing along (dx, dy)."""
    d = math.hypot(dx, dy)
    ux, uy = dx / d, dy / d
    px, py = -uy, ux
    S.cyl(name + "ring", size * 1.15, 0.28, (x, y, z + 0.14), "chrome2", verts=20)
    S.cyl(name + "base", size, 0.5, (x, y, z + 0.5), "navy", verts=20)
    S.cyl(name + "band", size * 1.02, 0.1, (x, y, z + 0.4), "house", verts=20, nocast=True)
    hx, hy, hz = x + ux * 0.1, y + uy * 0.1, z + 0.95
    S.box(name + "house", (size * 1.2, size * 1.6, 0.6), (hx, hy * S.YS, hz), "navy", 0.14, rot=(0, 0, math.degrees(math.atan2(uy, ux)) - 90), stretch=False)
    S.box(name + "roof", (size * 1.0, size * 1.3, 0.1), (hx, hy * S.YS, hz + 0.33), "white2", 0.03, rot=(0, 0, math.degrees(math.atan2(uy, ux)) - 90), stretch=False, nocast=True)
    for s in (-1, 1):
        o = s * 0.26
        x0, y0 = hx + px * o + ux * 0.5, hy + py * o + uy * 0.5
        ln = barrel - recoil
        if dmg and s > 0:
            K.beam(S, name + f"bar{s}", (x0, y0, hz), (x0 + ux * ln * 0.55, y0 + uy * ln * 0.55, hz - 0.5), 0.2, "chrome2")
        else:
            K.beam(S, name + f"bar{s}", (x0, y0, hz), (x0 + ux * ln, y0 + uy * ln, hz + pitch * ln), 0.2, "chrome")
            K.beam(S, name + f"muz{s}", (x0 + ux * (ln - 0.2), y0 + uy * (ln - 0.2), hz + pitch * (ln - 0.2)), (x0 + ux * ln, y0 + uy * ln, hz + pitch * ln), 0.28, "orange2")


def missile(S, name, x, y, z, dy, dz, w=0.34, mat="white", tip="orange", dx=0.0):
    """Missile body from (x, y, z) towards (+dx, +dy, +dz) with an orange nose and a navy fin collar."""
    p0 = (x, y, z)
    p1 = (x + dx, y + dy, z + dz)
    K.beam(S, name, p0, p1, w, mat)
    f = 0.82
    K.beam(S, name + "t", (x + dx * f, y + dy * f, z + dz * f), p1, w * 1.05, tip)
    f = 0.3
    K.beam(S, name + "f", (x + dx * f, y + dy * f, z + dz * f), (x + dx * (f + 0.12), y + dy * (f + 0.12), z + dz * (f + 0.12)), w * 1.9, "navy2", nocast=True)


# ----------------------------------------------------------------------------------------------------------------- water side
def quay_edge(S, x0, x1, y, hz=0.46):
    """Concrete quay wall along game y (land is north of it) with copper coping, chrome fender posts and signal-orange bollards."""
    S.bb("quay", x0, x1, y - 0.5, y, -0.05, hz, "pad2", 0.05)
    S.bb("quaytide", x0, x1, y - 0.52, y - 0.46, -0.05, 0.2, "ash", 0.0)
    S.bb("coping", x0, x1, y - 0.1, y, hz, hz + 0.06, "copper", 0.0, nocast=True)
    n = max(2, int((x1 - x0) / 1.6))
    for k in range(n + 1):
        px = x0 + 0.3 + (x1 - x0 - 0.6) * k / n
        K.post(S, "fend", px, y - 0.58, -0.05, 0.42, 0.14, "chrome2")
        if k % 2 == 0:
            S.cyl("bollard", 0.13, 0.28, (px, y - 0.1, hz + 0.2), "orange", verts=10)


def pontoon(S, name, x0, x1, y0, y1, z=0.1, dmg=False):
    """Floating pontoon walkway: navy tank with a chrome grating deck and orange handrail posts."""
    S.bb(name, x0, x1, y0, y1, -0.1, z, "navy2", 0.05)
    S.bb(name + "d", x0 + 0.06, x1 - 0.06, y0 + 0.06, y1 - 0.06, z, z + 0.05, "chrome2", 0.0, nocast=True)
    S.bb(name + "t", x0 - 0.02, x1 + 0.02, y0 - 0.02, y1 + 0.02, -0.1, 0.0, "ash", 0.0)
    ln = max(x1 - x0, y1 - y0)
    for k in range(int(ln / 1.4) + 1):
        if x1 - x0 > y1 - y0:
            K.post(S, name + "p", x0 + 0.2 + k * 1.4, (y0 + y1) / 2, z, z + 0.45, 0.08, "orange")
        else:
            K.post(S, name + "p", (x0 + x1) / 2, y0 + 0.2 + k * 1.4, z, z + 0.45, 0.08, "orange")


def buoy(S, name, x, y, anim=0, lit=True):
    """Small navy/orange marker buoy with a lamp."""
    S.cyl(name, 0.3, 0.5, (x, y, 0.1), "orange", verts=10)
    S.cyl(name + "b", 0.31, 0.12, (x, y, 0.12), "white", verts=10, nocast=True)
    K.post(S, name + "m", x, y, 0.3, 0.9, 0.06, "chrome2")
    S.sphere(name + "l", 0.1, (x, y, 0.95), "lamp" if lit else "lamp_off")


def site_water(S, hx, hy, water_y):
    """Build frame 0 for a straddling building: surveyed land plot north of water_y, buoys marking the berth in the water row."""
    S.bb("pad", -hx - 0.1, hx + 0.1, water_y, hy + 0.1, 0.0, 0.1, "pad2", 0.03)
    for (sx, sy) in ((-hx, hy), (hx, hy), (-hx, water_y + 0.3), (hx, water_y + 0.3)):
        K.post(S, "stake", sx, sy, 0.1, 1.0, 0.12, "timber")
        S.bb("stakeflag", sx, sx + 0.35, sy - 0.02, sy + 0.02, 0.7, 1.0, "orange", 0.0, nocast=True)
    S.bb("line", -hx, hx, hy - 0.04, hy + 0.04, 0.88, 0.92, "rope", 0.0, nocast=True)
    S.bb("line", -hx, hx, water_y + 0.26, water_y + 0.34, 0.5, 0.54, "rope", 0.0, nocast=True)
    for k, bx in enumerate((-hx, -hx / 3, hx / 3, hx)):
        buoy(S, f"sb{k}", bx, water_y - 1.0, lit=k % 2 == 0)
    K.post(S, "boardp", -0.5, water_y + 0.8, 0.1, 1.2, 0.1, "timber")
    K.post(S, "boardp", 0.5, water_y + 0.8, 0.1, 1.2, 0.1, "timber")
    S.bb("board", -0.8, 0.8, water_y + 0.7, water_y + 0.85, 0.7, 1.5, "navy2", 0.0)
    S.bb("boardc", -0.6, 0.6, water_y + 0.68, water_y + 0.7, 0.95, 1.3, "white", 0.0, nocast=True)
    S.bb("kitcrate", hx * 0.2, hx * 0.7, hy * 0.2, hy * 0.6, 0.1, 0.6, "navy", 0.06)
    S.cyl("kitroll", 0.3, 1.4, (-hx * 0.4, hy * 0.3, 0.4), "white2", axis="X", verts=12)


def scorch(S, name, x0, x1, y0, y1, z):
    S.bb(name, x0, x1, y0, y1, z, z + 0.02, "char", 0.0, nocast=True)


def rail_ring(S, x0, x1, y0, y1, z, t=0.1, mat="copper", h=0.08):
    """Copper coping as a four-strip ring (never a slab over the roof)."""
    S.bb("rl_s", x0, x1, y0, y0 + t, z, z + h, mat, 0.0, nocast=True)
    S.bb("rl_n", x0, x1, y1 - t, y1, z, z + h, mat, 0.0, nocast=True)
    S.bb("rl_w", x0, x0 + t, y0, y1, z, z + h, mat, 0.0, nocast=True)
    S.bb("rl_e", x1 - t, x1, y0, y1, z, z + h, mat, 0.0, nocast=True)
