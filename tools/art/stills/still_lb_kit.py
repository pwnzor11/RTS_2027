"""Left Behind props and helpers for the act-break stills (M8-14, package s14-stlb).  Built on still_kit (not edited).

Everything here is a primitives-built low-poly prop in the Left Behind look (dust ochre, sun-bleached canvas, rust red, faded teal,
ash grey, bone, hazard yellow; cloth wraps, patched plates, flags with names, lanterns) plus a few enemy props (Authority post plant,
Directorate hatch / vent / mast / pillbox, Expedition ships / depot / pile houses) and scene helpers (parametric sky, dune ground,
sea, falling ash, light beams).  Import from a bpy script only.  +Y is forward (north); yaw 0 = north, clockwise.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import still_kit as K  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

lin = K.lin
mat = K.mat
box = K.box
cyl = K.cyl
sphere = K.sphere

# Left Behind palette, pushed a little more saturated than the sprites so the stills have colour variety.
LB = {
    "canvas": "#C9B98F", "canvas_l": "#E0D2A6", "ochre": "#C08E3E", "ochre_d": "#8C6A33",
    "red": "#B9502F", "red_d": "#7A3320", "red_l": "#D8703F",
    "teal": "#2F6F69", "teal_d": "#1F3331", "teal_l": "#58948B", "turq": "#3FA79A",
    "ash": "#6B6762", "ash_d": "#3A3733", "ash_l": "#A19D94", "bone": "#E6DDC4", "haz": "#E3BE2E",
    "blue": "#3C5F8A", "plum": "#6A3F5E", "dark": "#1B1A18", "wood": "#7A5A33", "wood_d": "#4F3B22",
}
WRAPS = ["canvas", "ochre", "red", "teal_l", "ash_l", "canvas_l", "blue", "ochre_d"]
BODY_COLS = ["teal", "red", "ochre", "turq", "canvas", "blue", "plum", "haz"]


def frame(x, y, yaw):
    s = (math.sin(yaw), math.cos(yaw))
    r = (s[1], -s[0])
    return lambda f, l, z: (x + s[0] * f + r[0] * l, y + s[1] * f + r[1] * l, z)


def emat(col, strength, base=None):
    return mat(base or col, col, emit=col, estr=strength)


def wheel(p, r, w, yaw, tire="#1B1A18", hub="#A19D94"):
    cyl("wheel", p, r, w, mat(tire, rough=1.0), rot=(0, math.pi / 2, -yaw), verts=12)
    cyl("hubcap", p, r * 0.5, w * 1.06, mat(hub), rot=(0, math.pi / 2, -yaw), verts=8)


def pole(p0, p1, r, m, verts=6):
    a, b = Vector(p0), Vector(p1)
    d = b - a
    o = cyl("pole", tuple((a + b) / 2), r, d.length, m, verts=verts)
    up = "X" if abs(d.normalized().x) < 0.9 else "Y"
    o.rotation_euler = d.to_track_quat("Z", up).to_euler()
    return o


def cone(loc, r, h, m, rot=(0, 0, 0), verts=10, tip=0.02):
    return cyl("cone", loc, r, h, m, rot=rot, verts=verts, r2=tip)


# ------------------------------------------------------------------------------------------------ cloth, flags, lights
def flag(loc, h=5.0, col="red", yaw=0.0, rng=None, w=1.6, hh=1.0, bands=True):
    """Pole with a patchwork flag (the names flag): a few cloth panels at a slight wind-blown angle."""
    rng = rng or random.Random(5)
    x, y = loc
    F = frame(x, y, yaw)
    cyl("flagpole", (x, y, h / 2), 0.05, h, mat("ash_d", LB["ash_d"]), verts=6)
    cols = [col, "canvas_l", "ochre", "teal_l"] if bands else [col]
    n = 3 if bands else 1
    for i in range(n):
        c = LB[cols[i % len(cols)]] if cols[i % len(cols)] in LB else cols[i % len(cols)]
        box("cloth", F(w * (i + 0.5) / n, 0, h - hh * 0.5 - rng.uniform(0, 0.05)), (0.03, w / n * 1.02, hh),
            mat(c, rough=1.0), rot=(0, 0, -yaw + math.pi / 2 + rng.uniform(-0.05, 0.05)))


def lantern(loc, col="#FFB347", estr=70.0, light=0.0, r=0.12):
    """Glowing lantern.  light > 0 also adds a point light (use sparingly, they cost render time)."""
    sphere("lantern", loc, r, mat(col, col, emit=col, estr=estr))
    if light:
        K.point_light(loc, light, tuple(lin(col)[:3]), 0.25)


def tarp(p0, p1, width, yaw, col, sag=0.0, thick=0.04):
    """Canvas sheet slanting from p0 (high) to p1 (low), width across."""
    a, b = Vector(p0), Vector(p1)
    d = b - a
    mid = (a + b) / 2
    flat = math.hypot(d.x, d.y)
    pitch = math.atan2(d.z, flat)
    o = box("tarp", tuple(mid), (width, d.length, thick), mat(col, rough=1.0), rot=(pitch, 0, -yaw))
    return o


# ------------------------------------------------------------------------------------------------ people
def lb_figure(loc, yaw=0.0, rng=None, kind="scav", scale=1.0, lit=False):
    """Hooded Left Behind figure: layered wraps, pointed hood, goggles, filter-tin respirator, scarf in a bright colour."""
    rng = rng or random.Random(3)
    x, y = loc
    s = scale
    F = frame(x, y, yaw)
    coat = LB[rng.choice(WRAPS)]
    coat2 = LB[rng.choice(WRAPS)]
    scarf = LB[rng.choice(["red", "haz", "turq", "plum", "red_l"])]
    long_coat = kind in ("scav", "tinker", "walker", "slinger")
    coat_h = (1.05 if long_coat else 0.8) * s
    box("legs", (x, y, 0.4 * s), (0.34 * s, 0.26 * s, 0.8 * s), mat("dark", LB["dark"]), rot=(0, 0, -yaw))
    cyl("coat", (x, y, 0.55 * s + coat_h / 2), 0.31 * s, coat_h, mat(coat, rough=1.0), verts=8, r2=0.22 * s)
    cyl("wrap2", (x, y, 1.05 * s + 0.2 * s), 0.26 * s, 0.38 * s, mat(coat2, rough=1.0), verts=8, r2=0.2 * s)
    box("scarf", F(0.1 * s, 0, 1.52 * s), (0.34 * s, 0.26 * s, 0.1 * s), mat(scarf, rough=1.0), rot=(0, 0, -yaw))
    box("scarftail", F(-0.18 * s, 0.1 * s, 1.35 * s), (0.08 * s, 0.05 * s, 0.5 * s), mat(scarf, rough=1.0), rot=(0.25, 0, -yaw))
    sphere("head", F(0.02 * s, 0, 1.68 * s), 0.17 * s, mat(coat2, rough=1.0))
    cone(F(-0.03 * s, 0, 1.74 * s), 0.21 * s, 0.4 * s, mat(coat, rough=1.0), rot=(0.18, 0, -yaw), tip=0.05, verts=8)
    for sd in (-1, 1):
        box("goggle", F(0.16 * s, sd * 0.07 * s, 1.65 * s), (0.07 * s, 0.05 * s, 0.07 * s),
            mat("haz", "#F2D45C", emit="#F2D45C", estr=3.0 if not lit else 12.0), rot=(0, 0, -yaw))
    cyl("tin", F(0.2 * s, 0, 1.55 * s), 0.05 * s, 0.1 * s, mat("ash", LB["ash_l"]), rot=(math.pi / 2, 0, -yaw), verts=8)
    if kind in ("scav", "slinger", "walker"):
        box("pack", F(-0.24 * s, 0, 1.15 * s), (0.4 * s, 0.22 * s, 0.5 * s), mat(LB[rng.choice(["wood", "ash", "red_d"])]), rot=(0, 0, -yaw))
        box("roll", F(-0.26 * s, 0, 1.45 * s), (0.42 * s, 0.14 * s, 0.14 * s), mat(LB["canvas_l"], rough=1.0), rot=(0, 0, -yaw))
    if kind in ("scav", "walker"):
        box("rifle", F(0.4 * s, 0.18 * s, 1.1 * s), (0.06 * s, 1.1 * s, 0.08 * s), mat("dark", LB["dark"]), rot=(0.1, 0, -yaw))
    if kind == "tinker":
        box("bag", F(0.0, 0.34 * s, 0.85 * s), (0.18 * s, 0.4 * s, 0.34 * s), mat(LB["wood"]), rot=(0, 0, -yaw))
        pole(F(0.1, 0.3 * s, 1.0 * s), F(0.5 * s, 0.4 * s, 1.5 * s), 0.025, mat("ash", LB["ash_l"]))
    if kind == "slinger":
        pole(F(0.1, 0.25 * s, 1.1 * s), F(0.9 * s, 0.35 * s, 1.9 * s), 0.03, mat(LB["wood"]))
        sphere("charge", F(0.95 * s, 0.35 * s, 1.95 * s), 0.1 * s, mat("ash", LB["ash"]))
    if kind == "brute":
        box("plate", F(0.18 * s, 0, 1.0 * s), (0.55 * s, 0.2 * s, 0.6 * s), mat("ash", LB["ash"]), rot=(0, 0, -yaw))
        cyl("dusttank", F(-0.3 * s, 0, 1.2 * s), 0.18 * s, 0.7 * s, mat("red", LB["red"]), verts=8)


def crowd(rng, pts, kinds=("scav", "scav", "tinker", "slinger", "walker"), yaw=0.0, jitter=0.5, lit=False, scale=1.0):
    for p in pts:
        lb_figure((p[0] + rng.uniform(-jitter, jitter), p[1] + rng.uniform(-jitter, jitter)),
                  yaw + rng.uniform(-0.5, 0.5), rng, rng.choice(kinds), scale=scale * rng.uniform(0.92, 1.08), lit=lit)


# ------------------------------------------------------------------------------------------------ rigs and vehicles
def rig_bus(loc, yaw=0.0, body="teal", trim="red", length=9.0, height=2.3, rng=None, lit=False, awning=True, chimney=True,
            junk=True, flagcol="red", smoke=False, patches=7):
    """Bus-chassis caravan rig: patched body, dark windows, roof deck with junk, rain-catch awning, chimney, big wheels."""
    rng = rng or random.Random(17)
    x, y = loc
    F = frame(x, y, yaw)
    bc, tc = LB[body], LB[trim]
    L = length
    box("chassis", F(0, 0, 0.85), (2.4, L, 0.55), mat("ash_d", LB["ash_d"]), rot=(0, 0, -yaw))
    box("body", F(-0.3, 0, 0.85 + 0.27 + height / 2), (2.5, L * 0.88, height), mat(bc), rot=(0, 0, -yaw))
    box("cab", F(L * 0.44, 0, 1.0 + 0.9), (2.4, L * 0.16, 1.8), mat(trim if trim in LB else tc, LB.get(trim, tc)), rot=(0, 0, -yaw))
    box("bonnet", F(L * 0.55, 0, 1.3), (2.3, 1.4, 1.0), mat(tc), rot=(0, 0, -yaw))
    box("windscreen", F(L * 0.505, 0, 2.3), (2.0, 0.06, 0.75), mat("dark", "#10181A", rough=0.15), rot=(0.25, 0, -yaw))
    # windows (warm when lit)
    nwin = max(3, int(L / 1.6))
    wcol = "#FFB347"
    for i in range(nwin):
        f = -L * 0.38 + i * (L * 0.76 / (nwin - 1))
        for sd in (-1, 1):
            if lit and rng.random() < 0.7:
                m = mat(wcol, wcol, emit=wcol, estr=14.0)
            else:
                m = mat("dark", "#10181A", rough=0.2)
            box("win", F(f, sd * 1.26, 0.85 + 0.27 + height * 0.62), (0.05, 0.85, 0.62), m, rot=(0, 0, -yaw))
    # patched plates in other colours
    for i in range(patches):
        sd = rng.choice((-1, 1))
        pc = LB[rng.choice(BODY_COLS + ["ash", "canvas", "red_d"])]
        box("patch", F(rng.uniform(-L * 0.4, L * 0.3), sd * 1.27, 0.85 + 0.27 + height * rng.uniform(0.18, 0.5)),
            (0.05, rng.uniform(0.6, 1.8), rng.uniform(0.35, 0.8)), mat(pc), rot=(0, 0, -yaw + rng.uniform(-0.04, 0.04)))
    # stripe
    box("stripe", F(-0.3, 1.255, 0.85 + 0.27 + height * 0.1), (0.04, L * 0.88, 0.16), mat("haz", LB["haz"]), rot=(0, 0, -yaw))
    box("stripe", F(-0.3, -1.255, 0.85 + 0.27 + height * 0.1), (0.04, L * 0.88, 0.16), mat("haz", LB["haz"]), rot=(0, 0, -yaw))
    # roof
    rz = 0.85 + 0.27 + height
    box("deck", F(-0.3, 0, rz + 0.06), (2.4, L * 0.84, 0.12), mat("wood", LB["wood_d"]), rot=(0, 0, -yaw))
    for sd in (-1, 1):
        box("rail", F(-0.3, sd * 1.15, rz + 0.5), (0.06, L * 0.82, 0.07), mat("ash", LB["ash"]), rot=(0, 0, -yaw))
        for i in range(5):
            box("rpost", F(-L * 0.38 + i * L * 0.19, sd * 1.15, rz + 0.3), (0.06, 0.06, 0.55), mat("ash", LB["ash"]), rot=(0, 0, -yaw))
    if junk:
        cyl("tank", F(-L * 0.2, 0.4, rz + 0.65), 0.6, 1.0, mat("ash", LB["ash_l"]), verts=10)
        cyl("tankband", F(-L * 0.2, 0.4, rz + 0.65), 0.62, 0.1, mat("red", LB["red_d"]), verts=10)
        box("solar", F(L * 0.12, -0.4, rz + 0.55), (1.6, 1.2, 0.06), mat("blue", "#2C4A78", rough=0.2), rot=(0.3, 0, -yaw))
        box("bundle", F(-L * 0.36, -0.3, rz + 0.45), (1.4, 0.9, 0.7), mat("canvas", LB["canvas"], rough=1.0), rot=(0, 0, -yaw + 0.1))
        K.crate((F(-L * 0.05, 0.6, rz + 0.12)[0], F(-L * 0.05, 0.6, rz + 0.12)[1], rz + 0.12), 0.7, yaw + 0.2, LB["wood"])
        for i in range(2):
            p = F(L * 0.2 + i * 0.4, 0.7, rz + 0.12)
            box("bike", p, (0.06, 1.2, 0.5), mat("ash", LB["ash_l"]), rot=(0.9, 0, -yaw + i * 0.6))
        pole(F(-L * 0.42, 0.0, rz + 0.1), F(-L * 0.42, 0.0, rz + 3.2), 0.04, mat("ash_d", LB["ash_d"]))
    if chimney:
        c = F(-L * 0.12, -0.7, rz + 0.8)
        cyl("chimney", c, 0.2, 1.7, mat("ash", LB["ash"]), verts=8)
        cyl("chimcap", (c[0], c[1], c[2] + 0.95), 0.3, 0.1, mat("red", LB["red_d"]), verts=8)
        if smoke:
            for i in range(5):
                sphere("steam", (c[0] - i * 0.35 + rng.uniform(-.1, .1), c[1] + rng.uniform(-.1, .1), c[2] + 1.3 + i * 0.75),
                       0.3 + i * 0.22, mat("#CFC4B0", rough=1.0, emit="#FFD9A0", estr=0.5), scale=(1, 1, 0.8))
    # wheels
    for fx in (L * 0.36, -L * 0.3, -L * 0.4):
        for sd in (-1, 1):
            wheel(F(fx, sd * 1.15, 0.58), 0.62, 0.42, yaw)
    # headlights, push bar
    for sd in (-1, 1):
        sphere("lamp", F(L * 0.62, sd * 0.8, 1.35), 0.14, mat("haz", "#FFE9B0", emit="#FFE9B0", estr=30.0 if lit else 2.0))
    box("pushbar", F(L * 0.64, 0, 0.85), (2.3, 0.1, 0.1), mat("ash", LB["ash"]), rot=(0, 0, -yaw))
    for sd in (-1, 1):
        pole(F(L * 0.62, sd * 0.9, 0.8), F(L * 0.72, sd * 0.6, 1.5), 0.04, mat("red_d", LB["red_d"]))
    if awning:
        col = LB[rng.choice(["canvas_l", "red_l", "turq", "haz"])]
        a0 = F(-L * 0.1, -1.3, 0.85 + 0.27 + height * 0.95)
        a1 = F(-L * 0.1, -3.4, 2.0)
        tarp(a0, a1, L * 0.5, yaw, col)
        for i in (-1, 1):
            pole(F(-L * 0.1 + i * L * 0.22, -3.35, 0.0), F(-L * 0.1 + i * L * 0.22, -3.35, 2.0), 0.05, mat(LB["wood_d"]))
    # tail ladder and flag
    box("ladder", F(-L * 0.5, 0.6, 1.9), (0.5, 0.06, 2.4), mat("ash", LB["ash"]), rot=(0, 0, -yaw))
    fp = F(-L * 0.42, 0.0, rz + 3.2)
    flag((fp[0], fp[1]), h=1.1, col=flagcol, yaw=yaw + 1.2, rng=rng, w=1.3, hh=0.8)
    for sd in (-1, 1):
        pass


def kettle_rig(loc, yaw=0.0, rng=None, lit=False, steam=True, horn_yaw=0.0):
    """Calloway-Ross's rig: a flatbed truck carrying a huge riveted boiler (the kettle) and a loudspeaker mast on a swivel."""
    rng = rng or random.Random(23)
    x, y = loc
    F = frame(x, y, yaw)
    box("chassis", F(0, 0, 0.9), (2.6, 10.5, 0.55), mat("ash_d", LB["ash_d"]), rot=(0, 0, -yaw))
    box("bed", F(-1.0, 0, 1.28), (2.9, 8.0, 0.25), mat("wood", LB["wood_d"]), rot=(0, 0, -yaw))
    # cab: red with yellow roof stripe
    box("cab", F(4.2, 0, 2.2), (2.6, 2.2, 2.2), mat("red", LB["red"]), rot=(0, 0, -yaw))
    box("cabroof", F(4.15, 0, 3.4), (2.7, 2.4, 0.18), mat("red_d", LB["red_d"]), rot=(0, 0, -yaw))
    box("cabstripe", F(4.2, 0, 2.75), (2.65, 2.25, 0.2), mat("haz", LB["haz"]), rot=(0, 0, -yaw))
    box("hood", F(5.7, 0, 1.7), (2.5, 1.8, 1.1), mat("red", LB["red"]), rot=(0, 0, -yaw))
    box("grille", F(6.62, 0, 1.7), (2.3, 0.08, 0.8), mat("ash_d", LB["ash_d"]), rot=(0, 0, -yaw))
    box("wscreen", F(5.28, 0, 2.7), (2.3, 0.06, 0.9), mat("dark", "#10181A", rough=0.15), rot=(0.2, 0, -yaw))
    for sd in (-1, 1):
        sphere("lamp", F(6.7, sd * 0.85, 1.9), 0.17, mat("haz", "#FFE9B0", emit="#FFE9B0", estr=20.0))
        box("exhaust", F(3.2, sd * 1.45, 3.4), (0.14, 0.14, 2.6), mat("ash", LB["ash"]))
        box("exhaust", F(3.2, sd * 1.45, 4.8), (0.22, 0.22, 0.16), mat("ash", LB["ash_l"]))
    # the kettle: boiler on its side, riveted bands, dome end, filler cap and a pipe with a steam puff
    c = F(-1.4, 0, 2.75)
    cyl("boiler", c, 1.25, 6.2, mat("teal", "#2F7F78"), rot=(math.pi / 2, 0, -yaw), verts=16)
    for f in (-3.4, -2.4, -0.4, 0.8):
        b = F(f, 0, 2.75)
        cyl("band", b, 1.31, 0.18, mat("ochre_d", LB["ochre_d"]), rot=(math.pi / 2, 0, -yaw), verts=16)
    cyl("dome", F(1.7, 0, 2.75), 1.25, 0.5, mat("teal_l", LB["teal_l"]), rot=(math.pi / 2, 0, -yaw), verts=16, r2=0.5)
    cyl("cap", F(-0.9, 0, 4.1), 0.32, 0.3, mat("ash", LB["ash_l"]), verts=8)
    p = F(-0.9, 0, 4.3)
    pole(p, F(0.2, 0, 5.4), 0.1, mat("ash", LB["ash"]))
    cyl("spout", F(0.25, 0, 5.45), 0.18, 0.4, mat("ochre", LB["ochre"]), rot=(0.3, 0, -yaw), verts=8)
    if steam:
        for i in range(7):
            t = i / 6
            q = F(0.4 + t * 1.6, 0, 5.8 + t * 3.0)
            sphere("steam", (q[0] + rng.uniform(-.2, .2), q[1] + rng.uniform(-.2, .2), q[2]), 0.35 + t * 0.9,
                   mat("#E6D6BC", rough=1.0, emit="#FFE2B0", estr=0.9), scale=(1, 1, 0.85))
    # mast with loudspeaker horns (retuned Voice)
    mp = F(-4.2, 0.9, 1.4)
    pole(mp, (mp[0], mp[1], 7.2), 0.09, mat("ash_d", LB["ash_d"]))
    for i, ang in enumerate((0.0, 0.9, -0.9)):
        h = (mp[0], mp[1], 6.6 - i * 0.5)
        hy = yaw + horn_yaw + ang
        hp = frame(h[0], h[1], hy)
        cone(hp(0.6, 0, h[2]), 0.45, 1.3, mat("haz", LB["haz"]), rot=(-math.pi / 2, 0, -hy), verts=10, tip=0.12)
        cyl("driver", hp(-0.15, 0, h[2]), 0.18, 0.35, mat("ash", LB["ash"]), rot=(math.pi / 2, 0, -hy), verts=8)
    for i in range(2):
        pole(mp, F(-4.2 + (i * 2 - 1) * 2.5, 2.5, 3.6), 0.02, mat("ash", LB["ash"]))
    # wheels
    for fx in (4.6, -1.0, -3.2):
        for sd in (-1, 1):
            wheel(F(fx, sd * 1.3, 0.68), 0.74, 0.5, yaw)
    # rear: stacked water cans and rope
    for i in range(4):
        cyl("can", F(-4.8, -0.8 + i * 0.5, 1.7), 0.2, 0.55, mat(LB[rng.choice(["red", "haz", "teal", "ochre"])]), verts=8)
    flag(F(-4.3, -1.0, 1.4)[:2], h=6.0, col="red", yaw=yaw + 1.4, rng=rng, w=2.0, hh=1.2)
    if lit:
        for i in range(5):
            lantern(F(-4.5 + i * 2.2, 1.9, 3.4), estr=60.0)


def mother_rig(loc, yaw=0.0, rng=None, lit=False):
    """The Mother Rig, unpacked: long flatbed with a canvas street of lean-tos, a tall flag mast with the names flag, lanterns and stalls."""
    rng = rng or random.Random(29)
    x, y = loc
    F = frame(x, y, yaw)
    box("flatbed", F(0, 0, 1.0), (4.6, 13.0, 0.6), mat("ash_d", LB["ash_d"]), rot=(0, 0, -yaw))
    box("deck", F(0, 0, 1.4), (4.8, 13.0, 0.22), mat("wood", LB["wood_d"]), rot=(0, 0, -yaw))
    # kitchen box, market stalls, workshop with open front
    box("kitchen", F(-3.5, 0.6, 2.7), (3.0, 3.0, 2.2), mat("canvas", LB["canvas"]), rot=(0, 0, -yaw))
    box("kroof", F(-3.5, 0.6, 3.9), (3.3, 3.3, 0.15), mat("red_d", LB["red_d"]), rot=(0, 0, -yaw))
    cyl("kchim", F(-4.2, 1.2, 4.6), 0.18, 1.5, mat("ash", LB["ash"]), verts=8)
    box("workshop", F(1.5, -0.4, 2.55), (3.2, 3.4, 1.9), mat("teal", LB["teal"]), rot=(0, 0, -yaw))
    box("wsroof", F(1.5, -0.4, 3.62), (3.5, 3.6, 0.15), mat("ochre_d", LB["ochre_d"]), rot=(0, 0, -yaw))
    box("wsopen", F(1.5, 1.35, 2.45), (2.4, 0.06, 1.3), mat("dark", "#1A1410"), rot=(0, 0, -yaw))
    for i in range(3):
        col = LB[rng.choice(["red_l", "canvas_l", "turq", "haz", "plum"])]
        sx = 0.8 + i * 0.4
        a0 = F(-5.0 + i * 0, 0.0, 3.0)
        a0 = F(-1.5 + i * 3.4, 4.5, 3.4)
        a1 = F(-1.5 + i * 3.4, 6.7, 2.3)
        tarp(a0, a1, 3.0, yaw, col)
        for sd in (-1, 1):
            pole(F(-1.5 + i * 3.4 + sd * 1.45, 6.7, 1.3), F(-1.5 + i * 3.4 + sd * 1.45, 6.7, 2.3), 0.05, mat(LB["wood_d"]))
        box("stall", F(-1.5 + i * 3.4, 6.0, 0.55), (2.4, 0.8, 1.1), mat(LB["wood"]), rot=(0, 0, -yaw))
        K.crate(F(-1.0 + i * 3.4, 6.0, 1.1), 0.5, yaw + i, LB[rng.choice(["red", "ochre", "wood"])])
    # mast with big flag
    mp = F(5.6, -4.0, 1.4)
    pole(mp, (mp[0], mp[1], 13.0), 0.14, mat("ash_d", LB["ash_d"]))
    for i in range(4):
        box("cloth", (mp[0] + 1.3 * math.cos(yaw), mp[1] - 1.3 * math.sin(yaw), 12.1 - i * 0.9), (0.05, 2.6, 0.85),
            mat(LB[["red", "canvas_l", "teal_l", "haz"][i]], rough=1.0), rot=(0, 0, -yaw + math.pi / 2 + 0.05 * i))
    for i in range(7):
        pole((mp[0], mp[1], 12.8), F(-5 + i * 1.6, rng.uniform(-3, 3), 3.2), 0.015, mat("ash", LB["ash"]))
        lantern(F(-5 + i * 1.6, rng.uniform(-3, 3), 3.1), estr=45.0 if lit else 6.0, col=rng.choice(["#FFB347", "#FF8A3C", "#FFD27A"]))
    for sd in (-1, 1):
        for fx in (-4.5, 4.5):
            wheel(F(fx, sd * 2.1, 0.7), 0.8, 0.5, yaw)


def buggy(loc, yaw=0.0, body="haz", rng=None, lit=False):
    rng = rng or random.Random(31)
    x, y = loc
    F = frame(x, y, yaw)
    bc = LB[body]
    box("frame", F(0, 0, 0.6), (1.4, 2.8, 0.18), mat("ash_d", LB["ash_d"]), rot=(0, 0, -yaw))
    box("hood", F(1.1, 0, 0.85), (1.2, 1.0, 0.4), mat(bc), rot=(0, 0, -yaw))
    box("seat", F(-0.3, 0.4, 0.95), (0.5, 0.5, 0.45), mat(LB["red_d"]), rot=(0, 0, -yaw))
    box("seat", F(-0.3, -0.4, 0.95), (0.5, 0.5, 0.45), mat(LB["red_d"]), rot=(0, 0, -yaw))
    for sd in (-1, 1):
        pole(F(0.6, sd * 0.6, 0.7), F(0.5, sd * 0.6, 2.0), 0.04, mat("ash", LB["ash_l"]))
        pole(F(-1.2, sd * 0.6, 0.7), F(-1.1, sd * 0.6, 2.0), 0.04, mat("ash", LB["ash_l"]))
    pole(F(0.5, 0.6, 2.0), F(-1.1, 0.6, 2.0), 0.04, mat("ash", LB["ash_l"]))
    pole(F(0.5, -0.6, 2.0), F(-1.1, -0.6, 2.0), 0.04, mat("ash", LB["ash_l"]))
    pole(F(-0.3, 0, 0.9), F(-0.3, 0, 1.6), 0.05, mat("ash_d", LB["ash_d"]))
    pole(F(-0.3, 0, 1.6), F(0.7, 0, 1.75), 0.045, mat("dark", LB["dark"]))
    pole(F(-1.0, -0.5, 0.9), F(-1.6, -0.6, 3.4), 0.012, mat("ash", LB["ash"]))
    box("pennant", F(-1.6, -0.6, 3.35), (0.02, 0.5, 0.2), mat("red", LB["red"]), rot=(0, 0, -yaw + 0.3))
    for fx in (0.95, -0.95):
        for sd in (-1, 1):
            wheel(F(fx, sd * 0.85, 0.42), 0.44, 0.3, yaw)
    sphere("lamp", F(1.7, 0.3, 0.9), 0.1, mat("haz", "#FFE9B0", emit="#FFE9B0", estr=25.0 if lit else 2.0))


def harpoon_wagon(loc, yaw=0.0, rng=None, aim=0.0, lit=False):
    rng = rng or random.Random(37)
    x, y = loc
    F = frame(x, y, yaw)
    box("chassis", F(0, 0, 0.8), (2.4, 6.0, 0.5), mat("ash_d", LB["ash_d"]), rot=(0, 0, -yaw))
    box("bed", F(-0.6, 0, 1.15), (2.6, 4.4, 0.2), mat("wood", LB["wood_d"]), rot=(0, 0, -yaw))
    box("cab", F(2.0, 0, 1.9), (2.4, 1.6, 1.7), mat("ochre", LB["ochre"]), rot=(0, 0, -yaw))
    box("hood", F(3.1, 0, 1.5), (2.3, 1.0, 0.9), mat("ochre", LB["ochre"]), rot=(0, 0, -yaw))
    box("wscreen", F(2.8, 0, 2.2), (2.0, 0.06, 0.7), mat("dark", "#10181A", rough=0.15), rot=(0.2, 0, -yaw))
    # whaling gun on a swivel + cable drum + stacked harpoons
    cyl("swivel", F(-0.4, 0, 1.55), 0.45, 0.5, mat("ash", LB["ash"]), verts=10)
    g = frame(F(-0.4, 0, 0)[0], F(-0.4, 0, 0)[1], yaw + aim)
    cyl("gun", g(1.3, 0, 2.1), 0.17, 3.0, mat("ash_d", LB["ash_d"]), rot=(math.radians(80), 0, -(yaw + aim)), verts=8)
    cyl("muzzle", g(2.9, 0, 2.5), 0.24, 0.3, mat("red", LB["red_d"]), rot=(math.radians(80), 0, -(yaw + aim)), verts=8)
    pole(g(2.9, 0, 2.5), g(0.1, 0, 1.55), 0.025, mat("ash", LB["ash_l"]))
    cyl("drum", F(-2.0, 0, 1.75), 0.6, 0.9, mat("red", LB["red"]), rot=(0, math.pi / 2, -yaw), verts=12)
    cyl("drumhub", F(-2.0, 0, 1.75), 0.3, 1.0, mat("ash", LB["ash_l"]), rot=(0, math.pi / 2, -yaw), verts=8)
    for i in range(3):
        pole(F(-1.0, -0.8 + i * 0.1, 1.35 + i * 0.1), F(-1.2, -1.0, 3.0), 0.03, mat("ash", LB["ash_l"]))
    for fx in (2.0, -1.6):
        for sd in (-1, 1):
            wheel(F(fx, sd * 1.15, 0.55), 0.6, 0.42, yaw)
    for sd in (-1, 1):
        sphere("lamp", F(3.65, sd * 0.8, 1.7), 0.14, mat("haz", "#FFE9B0", emit="#FFE9B0", estr=24.0 if lit else 2.0))


def rolling_fort(loc, yaw=0.0, rng=None, lit=False):
    """Three wrecked buses on one engine and a girder: tiered, plated, flagged."""
    rng = rng or random.Random(41)
    x, y = loc
    F = frame(x, y, yaw)
    box("hull", F(0, 0, 1.2), (4.6, 12.0, 0.9), mat("ash_d", LB["ash_d"]), rot=(0, 0, -yaw))
    for i, (col, z, ln) in enumerate((("red", 0, 12.0), ("ochre", 2.3, 10.0), ("teal", 4.4, 6.5))):
        h = 2.2
        box("tier", F(-0.5 - i * 0.2, 0, 1.65 + z + h / 2), (4.2 - i * 0.6, ln, h), mat(LB[col]), rot=(0, 0, -yaw))
        box("tierroof", F(-0.5 - i * 0.2, 0, 1.65 + z + h), (4.4 - i * 0.6, ln + 0.3, 0.18), mat("ash", LB["ash"]), rot=(0, 0, -yaw))
        for sd in (-1, 1):
            for k in range(5):
                f = -ln * 0.4 + k * ln * 0.2
                box("slit", F(f - 0.5 - i * 0.2, sd * (2.12 - i * 0.3), 1.65 + z + h * 0.6), (0.05, 0.9, 0.35),
                    mat("dark", "#10181A") if not lit else mat("#FFB347", "#FFB347", emit="#FFB347", estr=10.0), rot=(0, 0, -yaw))
            for k in range(4):
                box("plate", F(rng.uniform(-ln * 0.4, ln * 0.4) - 0.5, sd * (2.13 - i * 0.3), 1.65 + z + rng.uniform(0.4, 1.8)),
                    (0.06, rng.uniform(0.8, 2.0), rng.uniform(0.5, 1.0)), mat(LB[rng.choice(BODY_COLS + ["ash", "canvas"])]), rot=(0, 0, -yaw))
    # girder ram and cannon on the roof
    box("girder", F(6.6, 0, 1.2), (0.5, 3.6, 0.5), mat("rust", LB["red_d"]), rot=(0.0, 0, -yaw))
    box("girder2", F(6.6, 0, 1.55), (0.12, 3.6, 0.6), mat("rust", LB["red_d"]), rot=(0.0, 0, -yaw))
    cyl("gun", F(1.8, 0, 8.4), 0.22, 4.0, mat("ash_d", LB["ash_d"]), rot=(math.radians(88), 0, -yaw), verts=8)
    box("mount", F(-0.5, 0, 8.0), (1.4, 1.4, 0.7), mat("ash", LB["ash"]), rot=(0, 0, -yaw))
    for fx in (4.2, 1.6, -1.2, -4.0):
        for sd in (-1, 1):
            wheel(F(fx, sd * 2.1, 0.75), 0.85, 0.7, yaw)
    for sd in (-1, 1):
        cyl("stack", F(-5.2, sd * 1.0, 3.5), 0.22, 3.4, mat("ash", LB["ash"]), verts=8)
    for i in range(3):
        flag(F(-2.0 + i * 2.0, (i - 1) * 0.8, 8.8)[:2], h=2.2, col=["red", "haz", "canvas_l"][i], yaw=yaw + 1.2 + i * 0.2, rng=rng, w=0.9, hh=0.6)


def rotor(loc, yaw=0.0, z=6.0, bank=0.0, rng=None):
    """Autogyro gunship: pod, tail boom, three-blade rotor on a mast, skids, gun pod."""
    rng = rng or random.Random(43)
    x, y = loc
    F = frame(x, y, yaw)
    box("pod", F(0, 0, z), (0.9, 2.2, 0.9), mat("ochre", LB["ochre"]), rot=(0, 0, -yaw))
    box("canopy", F(0.5, 0, z + 0.35), (0.7, 0.9, 0.5), mat("dark", "#10181A", rough=0.1), rot=(0, 0, -yaw))
    box("boom", F(-2.0, 0, z + 0.2), (0.16, 3.2, 0.16), mat("ash", LB["ash"]), rot=(0, 0, -yaw))
    box("fin", F(-3.5, 0, z + 0.55), (0.06, 0.7, 0.9), mat("red", LB["red"]), rot=(0, 0, -yaw))
    box("tailplane", F(-3.4, 0, z + 0.25), (1.2, 0.5, 0.05), mat("red", LB["red"]), rot=(0, 0, -yaw))
    pole(F(0, 0, z + 0.4), F(0, 0, z + 1.5), 0.07, mat("ash_d", LB["ash_d"]))
    for k in range(3):
        a = k * math.tau / 3 + 0.4
        tip = F(math.cos(a) * 3.2, math.sin(a) * 3.2, z + 1.55)
        pole(F(0, 0, z + 1.5), tip, 0.045, mat("ash_d", LB["ash_d"]), verts=5)
        mid = F(math.cos(a) * 1.9, math.sin(a) * 1.9, z + 1.55)
        box("blade", mid, (0.35, 2.6, 0.03), mat("haz" if k == 0 else "canvas_l", LB["haz" if k == 0 else "canvas_l"]),
            rot=(0, 0, -(yaw + math.pi / 2 - a) ))
    for sd in (-1, 1):
        pole(F(0.4, sd * 0.45, z - 0.45), F(-0.6, sd * 0.45, z - 0.45), 0.035, mat("ash", LB["ash"]))
        pole(F(0.2, sd * 0.4, z - 0.1), F(0.2, sd * 0.45, z - 0.45), 0.03, mat("ash", LB["ash"]))
    cyl("gunpod", F(1.0, 0.55, z - 0.1), 0.12, 1.0, mat("ash_d", LB["ash_d"]), rot=(math.pi / 2, 0, -yaw), verts=8)


# ------------------------------------------------------------------------------------------------ enemy props
def power_plant(loc, yaw=0.0, burning=False, rng=None):
    """Authority post power plant: olive shed, stack, transformer cans, safety-yellow hazard band."""
    rng = rng or random.Random(47)
    x, y = loc
    F = frame(x, y, yaw)
    box("shed", F(0, 0, 1.5), (4.5, 3.2, 3.0), mat("olive", "#4B5742"), rot=(0, 0, -yaw))
    box("roof", F(0, 0, 3.1), (4.8, 3.5, 0.2), mat("steel", "#555B61"), rot=(0, 0, -yaw))
    box("band", F(0, 0, 0.7), (4.55, 3.25, 0.25), mat("yellow", "#C9A640"), rot=(0, 0, -yaw))
    cyl("stack", F(1.2, 0.8, 5.0), 0.35, 4.0, mat("steel_d", "#24272A"), verts=8)
    for i in range(3):
        cyl("tx", F(-3.2 + i * 0.1, -1.0 + i * 1.0, 0.8), 0.45, 1.6, mat("steel", "#6A7075"), verts=8)
    if burning:
        K.fire((x, y - 1.0), size=2.2, rng=rng)
        K.smoke_column((x + 1, y), h=26, r0=1.6, n=9, rng=rng)


def hatch(loc, yaw=0.0, open_ang=1.1, glow="#9BE04A", rng=None):
    """Directorate hatch: concrete collar, steel lid swung open, a shaft of radioactive green light.  Adds a green point light."""
    rng = rng or random.Random(53)
    x, y = loc
    F = frame(x, y, yaw)
    cyl("collar", (x, y, 0.35), 2.6, 0.7, mat("dust", "#7B776B"), verts=14)
    cyl("collar2", (x, y, 0.75), 2.1, 0.2, mat("steel", "#555B61"), verts=14)
    cyl("hole", (x, y, 0.82), 1.8, 0.1, mat(glow, glow, emit=glow, estr=22.0), verts=14)
    lid = box("lid", F(-0.6, 0.0, 1.9), (3.6, 0.3, 3.4), mat("steel", "#555B61"), rot=(open_ang, 0, -yaw))
    box("lidband", F(-0.6, -0.1, 1.9), (3.7, 0.12, 0.4), mat("yellow", "#C9A640"), rot=(open_ang, 0, -yaw))
    for i in range(4):
        a = i * math.tau / 4
        box("hazard", (x + math.cos(a) * 2.3, y + math.sin(a) * 2.3, 0.5), (0.5, 0.3, 0.3), mat("yellow", "#C9A640"), rot=(0, 0, a))
    K.point_light((x, y, 3.0), 2200.0, tuple(lin(glow)[:3]), 1.0)
    beam((x, y), 22.0, 1.6, glow, 0.5)


def beam(loc, h, r, col, strength=0.4, fac=0.10):
    """Faint emissive shaft (additive-looking light column)."""
    o = cyl("beam", (loc[0], loc[1], h / 2), r, h, mat(col), verts=14, r2=r * 2.2)
    m = bpy.data.materials.new("beam")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = lin(col)
    e.inputs["Strength"].default_value = strength * 4
    t = nt.nodes.new("ShaderNodeBsdfTransparent")
    mx = nt.nodes.new("ShaderNodeMixShader")
    mx.inputs[0].default_value = fac
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(t.outputs[0], mx.inputs[1])
    nt.links.new(e.outputs[0], mx.inputs[2])
    nt.links.new(mx.outputs[0], out.inputs[0])
    o.data.materials.clear()
    o.data.materials.append(m)
    o.visible_shadow = False
    return K.nolines(o)


def vent_tower(loc, h=20.0, glow="#9BE04A"):
    """Reactor vent: tall ribbed concrete stack with a glowing throat and a plume."""
    x, y = loc
    cyl("vent", (x, y, h / 2), 2.4, h, mat("dust", "#6B685D"), verts=12, r2=1.8)
    for z in range(3, int(h), 4):
        cyl("ring", (x, y, z), 2.55 - z * 0.03, 0.4, mat("steel", "#555B61"), verts=12)
    cyl("throat", (x, y, h + 0.1), 1.9, 0.5, mat(glow, glow, emit=glow, estr=30.0), verts=12)
    K.point_light((x, y, h + 2), 9000.0, tuple(lin(glow)[:3]), 1.5)
    for i in range(7):
        t = i / 6
        sphere("plume", (x - t * t * 8, y + t * 2, h + 2 + t * 22), 1.8 + t * 4.2,
               mat("#5C7A4A", rough=1.0, emit=glow, estr=0.5 * (1 - t) + 0.05), scale=(1, 1, 0.8))
    beam((x, y), h + 30, 1.8, glow, 0.45, 0.08)
    box("ladder", (x + 2.1, y, h / 2), (0.1, 0.5, h), mat("steel_d", "#24272A"))


def pillbox(loc, yaw=0.0):
    x, y = loc
    F = frame(x, y, yaw)
    sphere("dome", (x, y, 0.4), 2.0, mat("dust", "#7B776B"), scale=(1, 1, 0.65))
    box("base", F(0, 0, 0.5), (3.6, 3.6, 1.0), mat("dust", "#5C5950"), rot=(0, 0, -yaw))
    box("slit", F(0, 1.75, 1.0), (1.8, 0.3, 0.3), mat("dark", "#0D0F0B"), rot=(0, 0, -yaw))
    box("lip", F(0, 1.7, 1.2), (2.2, 0.2, 0.12), mat("steel", "#555B61"), rot=(0, 0, -yaw))
    cyl("hatch", (x, y, 1.45), 0.35, 0.12, mat("steel", "#555B61"), verts=8)


def loudspeaker_mast(loc, h=26.0, light="#FF3B2E"):
    x, y = loc
    m = mat("steel_d", "#2A2D30")
    for sx in (-1, 1):
        for sy in (-1, 1):
            pole((x + sx * 1.6, y + sy * 1.6, 0), (x + sx * 0.5, y + sy * 0.5, h), 0.12, m, verts=5)
    for z in range(3, int(h), 4):
        k = 1.6 - z / h * 1.1
        for a in range(4):
            sx, sy = (-1, 1)[a % 2], (-1, 1)[a // 2]
        box("rung", (x, y, z), (k * 2, 0.07, 0.07), m)
        box("rung", (x, y, z), (0.07, k * 2, 0.07), m)
    for i in range(4):
        a = i * math.pi / 2 + 0.4
        cone((x + math.cos(a) * 1.2, y + math.sin(a) * 1.2, h - 1.0), 0.8, 1.8, mat("steel", "#6A7075"),
             rot=(0, math.pi / 2, a), verts=10, tip=0.2)
    sphere("beacon", (x, y, h + 0.8), 0.3, mat(light, light, emit=light, estr=60.0))
    K.point_light((x, y, h + 1), 900.0, tuple(lin(light)[:3]), 0.4)


# Expedition (navy, white, chrome, signal orange, copper)
EXP = {"navy": "#24385A", "navy_l": "#41608A", "white": "#DDE2E2", "chrome": "#B8C0C6", "orange": "#E8742A", "copper": "#B46A3A",
       "grey": "#6D7A86", "red": "#8A3430", "dark": "#1B222B"}


def ship(loc, yaw=0.0, L=60.0, W=11.0, cols=None, lit=False):
    """Low-poly steam freighter / landing ship: hull, superstructure, funnel, cranes, deck cargo."""
    x, y = loc
    F = frame(x, y, yaw)
    hull = EXP["navy"]
    box("hull", F(0, 0, 3.0), (W, L, 6.0), mat(hull, rough=0.6), rot=(0, 0, -yaw))
    box("bow", F(L * 0.5 + 3, 0, 3.0), (W * 0.55, 8.0, 6.0), mat(hull, rough=0.6), rot=(0, 0, -yaw + 0.0))
    box("bow2", F(L * 0.5 + 8, 0, 3.0), (W * 0.22, 6.0, 6.0), mat(hull, rough=0.6), rot=(0, 0, -yaw))
    box("waterline", F(0, 0, 0.9), (W * 1.02, L * 1.0, 0.7), mat(EXP["red"], rough=0.7), rot=(0, 0, -yaw))
    box("stripe", F(0, 0, 5.7), (W * 1.02, L * 1.0, 0.35), mat(EXP["orange"], rough=0.7), rot=(0, 0, -yaw))
    box("deck", F(0, 0, 6.1), (W * 0.96, L * 0.98, 0.25), mat(EXP["grey"], rough=0.8), rot=(0, 0, -yaw))
    box("bridge", F(-L * 0.18, 0, 9.5), (W * 0.8, L * 0.2, 6.0), mat(EXP["white"], rough=0.6), rot=(0, 0, -yaw))
    box("bridge2", F(-L * 0.18, 0, 13.2), (W * 0.6, L * 0.13, 2.4), mat(EXP["white"], rough=0.6), rot=(0, 0, -yaw))
    box("wheelhouse", F(-L * 0.18 + L * 0.075, 0, 13.4), (W * 0.62, 0.15, 1.2), mat("dark", EXP["dark"], rough=0.1), rot=(0, 0, -yaw))
    cyl("funnel", F(-L * 0.3, 0, 12.5), 1.9, 8.0, mat(EXP["orange"], rough=0.6), verts=10, r2=1.6)
    cyl("funnelcap", F(-L * 0.3, 0, 16.6), 1.7, 0.6, mat("dark", EXP["dark"]), verts=10)
    for sd in (-1, 1):
        for k in range(4):
            box("crate", F(L * 0.18 + k * 6.0, sd * 2.2, 8.0), (4.0, 4.0, 3.6),
                mat([EXP["copper"], EXP["grey"], EXP["red"], EXP["navy_l"]][(k + (sd > 0)) % 4], rough=0.8), rot=(0, 0, -yaw))
    pole(F(L * 0.3, 0, 6.2), F(L * 0.3, 0, 17.0), 0.25, mat("dark", EXP["dark"]))
    pole(F(L * 0.3, 0, 16.0), F(L * 0.3 + 11, 0, 11.0), 0.15, mat("dark", EXP["dark"]))
    pole(F(-L * 0.05, 0, 6.2), F(-L * 0.05, 0, 15.0), 0.2, mat("dark", EXP["dark"]))
    if lit:
        for k in range(8):
            sphere("pl", F(-L * 0.4 + k * L * 0.1, W * 0.5 + 0.1, 5.0), 0.25, mat("#FFD27A", "#FFD27A", emit="#FFD27A", estr=20.0))


def container(loc, yaw=0.0, col=None, s=(2.4, 6.0, 2.6)):
    col = col or random.choice([EXP["copper"], EXP["navy_l"], EXP["red"], EXP["grey"], EXP["orange"], EXP["white"]])
    box("container", (loc[0], loc[1], loc[2] if len(loc) > 2 else 0) if False else (loc[0], loc[1], (loc[2] if len(loc) > 2 else 0) + s[2] / 2),
        s, mat(col, rough=0.7), rot=(0, 0, -yaw))
    F = frame(loc[0], loc[1], yaw)
    for k in range(-2, 3):
        box("rib", F(k * (s[1] / 5.2), 0, (loc[2] if len(loc) > 2 else 0) + s[2] / 2), (s[0] * 1.02, 0.06, s[2] * 0.98),
            mat(col, rough=0.7), rot=(0, 0, -yaw))


def pile_house(loc, yaw=0.0, glow=True):
    """Expedition reactor pile house: windowless concrete block, orange hazard band, short stack, vents and a power-cable gantry."""
    x, y = loc
    F = frame(x, y, yaw)
    box("pile", F(0, 0, 3.0), (9.0, 12.0, 6.0), mat("dust", "#8A8478"), rot=(0, 0, -yaw))
    box("bandhaz", F(0, 0, 1.2), (9.1, 12.1, 0.8), mat(EXP["orange"], rough=0.7), rot=(0, 0, -yaw))
    box("roof", F(0, 0, 6.2), (9.4, 12.4, 0.4), mat("steel", "#555B61"), rot=(0, 0, -yaw))
    cyl("stack", F(2.0, -2.0, 10.0), 1.0, 8.0, mat(EXP["white"], rough=0.6), verts=10)
    cyl("stackband", F(2.0, -2.0, 12.5), 1.05, 0.7, mat(EXP["orange"], rough=0.7), verts=10)
    for k in range(3):
        box("vent", F(-2.5 + k * 2.5, 6.05, 4.2), (1.4, 0.2, 1.4), mat("dark", "#0D0F0B"), rot=(0, 0, -yaw))
        if glow:
            box("ventglow", F(-2.5 + k * 2.5, 6.12, 4.2), (1.0, 0.06, 1.0), mat("#FFB060", "#FFB060", emit="#FFB060", estr=8.0), rot=(0, 0, -yaw))
    for k in range(5):
        pole(F(5.0, -6 + k * 3, 6.4), F(5.0, -6 + k * 3, 11.0), 0.12, mat("steel_d", "#2A2D30"))


def gun_battery(loc, yaw=0.0):
    """Expedition coastal gun battery: sandbagged pit, twin long barrels, orange blast shield."""
    x, y = loc
    F = frame(x, y, yaw)
    cyl("pit", (x, y, 0.5), 5.0, 1.0, mat("dust", "#6B685D"), verts=14)
    cyl("ring", (x, y, 1.2), 3.6, 0.5, mat("steel", "#555B61"), verts=14)
    box("housing", F(0, -0.6, 2.2), (3.4, 3.4, 1.8), mat(EXP["navy_l"], rough=0.6), rot=(0.0, 0, -yaw))
    box("shield", F(0, 0.9, 2.2), (3.0, 0.3, 1.6), mat(EXP["orange"], rough=0.6), rot=(0, 0, -yaw))
    for sd in (-0.7, 0.7):
        cyl("barrel", F(1.6, sd, 2.3), 0.28, 6.5, mat("dark", EXP["dark"]), rot=(math.radians(82), 0, -yaw), verts=8)
        cyl("brake", F(5.0, sd, 3.1), 0.38, 0.5, mat("steel", "#6A7075"), rot=(math.radians(82), 0, -yaw), verts=8)
    for i in range(14):
        a = i * math.tau / 14
        sphere("bag", (x + math.cos(a) * 4.6, y + math.sin(a) * 4.6, 1.4), 0.7, mat("#8F8466"), scale=(1, 1, 0.6))


def landing_craft(loc, yaw=0.0, ramp_down=True):
    x, y = loc
    F = frame(x, y, yaw)
    box("hull", F(0, 0, 1.1), (6.0, 16.0, 2.2), mat(EXP["navy"], rough=0.6), rot=(0, 0, -yaw))
    box("deck", F(0, -1.0, 2.3), (5.4, 13.0, 0.2), mat(EXP["grey"], rough=0.8), rot=(0, 0, -yaw))
    box("ramp", F(0, 8.6, 0.9), (5.4, 3.4, 0.2), mat(EXP["chrome"], rough=0.5), rot=(-0.28 if ramp_down else 0.9, 0, -yaw))
    box("cab", F(0, -6.0, 3.5), (3.2, 2.6, 2.4), mat(EXP["white"], rough=0.6), rot=(0, 0, -yaw))
    box("stripe", F(0, 0, 2.0), (6.05, 16.05, 0.3), mat(EXP["orange"], rough=0.6), rot=(0, 0, -yaw))


def marine(loc, yaw=0.0, rng=None, scale=1.0):
    """Expedition marine: navy fatigues, white webbing, chrome helmet with orange visor stripe, sealed face plate."""
    x, y = loc
    s = scale
    F = frame(x, y, yaw)
    box("legs", (x, y, 0.42 * s), (0.34 * s, 0.26 * s, 0.84 * s), mat(EXP["navy"], rough=0.9), rot=(0, 0, -yaw))
    box("torso", F(0, 0, 1.15 * s), (0.5 * s, 0.3 * s, 0.7 * s), mat(EXP["navy_l"], rough=0.9), rot=(0, 0, -yaw))
    box("strap", F(0.16 * s, 0, 1.15 * s), (0.14 * s, 0.04 * s, 0.7 * s), mat(EXP["white"], rough=0.9), rot=(0, 0.0, -yaw + 0.5))
    box("belt", F(0, 0, 0.85 * s), (0.54 * s, 0.34 * s, 0.1 * s), mat(EXP["white"], rough=0.9), rot=(0, 0, -yaw))
    sphere("helm", F(0, 0, 1.7 * s), 0.2 * s, mat(EXP["chrome"], rough=0.3), scale=(1, 1, 0.9))
    box("visor", F(0.17 * s, 0, 1.68 * s), (0.2 * s, 0.05 * s, 0.07 * s), mat(EXP["orange"], rough=0.5), rot=(0, 0, -yaw))
    box("rifle", F(0.4 * s, 0.2 * s, 1.1 * s), (0.06 * s, 1.0 * s, 0.08 * s), mat("dark", EXP["dark"]), rot=(0.1, 0, -yaw))
    box("pack", F(-0.25 * s, 0, 1.1 * s), (0.4 * s, 0.22 * s, 0.5 * s), mat(EXP["white"], rough=0.9), rot=(0, 0, -yaw))


# ------------------------------------------------------------------------------------------------ ground, sea, sky, particles
def terrain(xs_f, ys_f, hfun, mat_, name="terrain", cell=1.6, grow=0.07, flat=40.0, ext=(260, -70, 420)):
    """Heightfield on a non-uniform grid (fine near the camera, coarse far away).  hfun(x, y) -> z."""
    import bmesh

    def axis(lo, hi):
        pts = [0.0]
        step = cell
        while pts[-1] < hi:
            pts.append(pts[-1] + step)
            if pts[-1] > flat:
                step *= (1 + grow)
        neg = [0.0]
        step = cell
        while neg[-1] > lo:
            neg.append(neg[-1] - step)
            if neg[-1] < -flat * 0.4:
                step *= (1 + grow)
        return sorted(set(neg + pts))
    xs = axis(-ext[0], ext[0])
    ys = axis(ext[1], ext[2])
    bm = bmesh.new()
    vs = [[bm.verts.new((x, y, hfun(x, y))) for y in ys] for x in xs]
    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            bm.faces.new((vs[i][j], vs[i + 1][j], vs[i + 1][j + 1], vs[i][j + 1]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat_)
    return K.nolines(o)


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def dune_height(x, y, amp=6.0, centre=(0, 0), r0=45.0, r1=90.0, seed=0.0):
    """Long rolling crests (sum of rotated sines), flat inside r0 of `centre` and fading out beyond ext."""
    d = math.hypot(x - centre[0], y - centre[1])
    m = smoothstep(r0, r1, d)
    h = 0.0
    for k, (ang, wl, a) in enumerate(((0.35, 70.0, 1.0), (1.2, 38.0, 0.45), (-0.5, 120.0, 0.9), (2.4, 21.0, 0.2))):
        u = x * math.cos(ang) + y * math.sin(ang)
        v = -x * math.sin(ang) + y * math.cos(ang)
        s = math.sin(u / wl * math.tau + seed + k + 0.8 * math.sin(v / (wl * 1.7) * math.tau))
        h += a * (1 - abs(s))       # ridged: sharp crests, soft troughs
    return amp * m * h * 0.6


def dune_mat(stops, ripple=0.6, speck=("#6B5A3C", "#B79A6A"), ripple_scale=1.6, wet=None):
    """Sand / ash / glassy ground: broad colour ramp, wind ripples (bump), speckle."""
    m, nt, out = K._new_mat("dune_mix")
    N, L = K._nodes(nt)
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    base = K._ramp(nt, K._noise(nt, pos, 0.05, 5).outputs["Fac"], stops)
    fine = K._ramp(nt, K._noise(nt, pos, 6.0, 3).outputs["Fac"], [(0.2, speck[0]), (0.8, speck[1])])
    mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = "RGBA"; mx.blend_type = "OVERLAY"; mx.inputs[0].default_value = 0.35
    L(base.outputs[0], mx.inputs[6]); L(fine.outputs[0], mx.inputs[7])
    wave = N("ShaderNodeTexWave"); wave.wave_type = "BANDS"; wave.bands_direction = "Y"
    wave.inputs["Scale"].default_value = ripple_scale; wave.inputs["Distortion"].default_value = 6.0; wave.inputs["Detail"].default_value = 2.0
    L(pos, wave.inputs["Vector"])
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = ripple; bp.inputs["Distance"].default_value = 0.1
    L(wave.outputs["Color"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.93
    L(mx.outputs[2], b.inputs["Base Color"]); L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return m


def glassy(col="#3FA79A", rough=0.06, metal=0.35):
    m = bpy.data.materials.new("glass")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(col)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    return m


def glass_shards(rng, n, xr, yr, avoid=None, cols=("#3FA79A", "#58948B", "#7FD0C0", "#2F6F69")):
    """Fused-sand glass: thin tilted translucent-looking shards and flat glossy pools (the Glass Coast signature)."""
    ms = [glassy(c) for c in cols]
    for i in range(n):
        x, y = rng.uniform(*xr), rng.uniform(*yr)
        if avoid and avoid(x, y):
            continue
        k = 1.0 + max(0.0, y) / 40.0
        if rng.random() < 0.55:
            o = box("shard", (x, y, 0.2 * k), (rng.uniform(0.2, 0.7) * k, rng.uniform(0.04, 0.1), rng.uniform(0.5, 1.6) * k),
                    ms[0], rot=(rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), rng.uniform(0, 3.1)))
            o.data.materials.clear(); o.data.materials.append(rng.choice(ms))
            K.nolines(o)
        else:
            bpy.ops.mesh.primitive_cylinder_add(vertices=14, radius=1.0, depth=0.03, location=(x, y, 0.05))
            o = bpy.context.active_object
            o.scale = (rng.uniform(1.2, 4.0) * k, rng.uniform(0.8, 2.5) * k, 1)
            o.rotation_euler = (0, 0, rng.uniform(0, 3))
            o.data.materials.append(rng.choice(ms))
            K.nolines(o)


def sea(y0, z=-0.3, size=3000.0, col="#27506A"):
    """Big glossy water plane starting at y0; mirrors the sky so the horizon glows."""
    m = bpy.data.materials.new("sea")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(col)
    b.inputs["Roughness"].default_value = 0.09
    b.inputs["Specular IOR Level"].default_value = 0.9
    nz = m.node_tree.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 0.4
    nz.inputs["Detail"].default_value = 2.0
    mp = m.node_tree.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1.0, 0.2, 1.0)
    tc = m.node_tree.nodes.new("ShaderNodeNewGeometry")
    m.node_tree.links.new(tc.outputs["Position"], mp.inputs[0])
    m.node_tree.links.new(mp.outputs[0], nz.inputs["Vector"])
    bp = m.node_tree.nodes.new("ShaderNodeBump")
    bp.inputs["Strength"].default_value = 0.25
    bp.inputs["Distance"].default_value = 0.5
    m.node_tree.links.new(nz.outputs["Fac"], bp.inputs["Height"])
    m.node_tree.links.new(bp.outputs[0], b.inputs["Normal"])
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, y0 + size / 2, z))
    o = bpy.context.active_object
    o.name = "sea"
    o.data.materials.append(m)
    return K.nolines(o)


def falling_ash(rng, n, box_min, box_max, col="#D8D4CC", r=(0.02, 0.07)):
    """Drifting ash flakes / snow as tiny emissive-white spheres (one mesh per flake, so keep n moderate)."""
    m = mat(col, col, emit=col, estr=1.2)
    for i in range(n):
        p = (rng.uniform(box_min[0], box_max[0]), rng.uniform(box_min[1], box_max[1]), rng.uniform(box_min[2], box_max[2]))
        k = 1.0 + max(0.0, p[1]) / 30.0
        o = sphere("ash", p, rng.uniform(*r) * k, m)
        o.data.polygons.foreach_set("use_smooth", [False] * len(o.data.polygons))
        K.nolines(o)


def sky(sun_vec, stops, cloud_stops, glow="#FFC98A", glow_pow=(14.0, 220.0), glow_amt=1.4, strength=1.0,
        cloud=(0.47, 0.66), cloud_scale=2.6, band=(0.025, 0.11, 0.30, 0.52), cloud_gain=1.9, stretch=5.5, seed=(0, 0, 0)):
    """Parametric version of still_kit.sky_dawn: elevation gradient stops, sun glow, cloud bands (lit by cloud_stops)."""
    w = bpy.data.worlds.new("sky")
    bpy.context.scene.world = w
    w.use_nodes = True
    nt = w.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    N, Lk = nt.nodes.new, nt.links.new
    tc = N("ShaderNodeTexCoord")
    nrm = N("ShaderNodeVectorMath"); nrm.operation = "NORMALIZE"
    Lk(tc.outputs["Object"], nrm.inputs[0])
    sep = N("ShaderNodeSeparateXYZ"); Lk(nrm.outputs[0], sep.inputs[0])
    el = N("ShaderNodeMapRange"); el.inputs[1].default_value = 0.0; el.inputs[2].default_value = 1.0; el.clamp = True
    Lk(sep.outputs["Z"], el.inputs[0])
    ramp = K._ramp(nt, el.outputs[0], stops)
    sv = Vector(sun_vec).normalized()
    dot = N("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"; dot.inputs[1].default_value = tuple(sv)
    Lk(nrm.outputs[0], dot.inputs[0])
    mx = N("ShaderNodeMath"); mx.operation = "MAXIMUM"; mx.inputs[1].default_value = 0.0; Lk(dot.outputs["Value"], mx.inputs[0])
    g1 = N("ShaderNodeMath"); g1.operation = "POWER"; g1.inputs[1].default_value = glow_pow[0]; Lk(mx.outputs[0], g1.inputs[0])
    g2 = N("ShaderNodeMath"); g2.operation = "POWER"; g2.inputs[1].default_value = glow_pow[1]; Lk(mx.outputs[0], g2.inputs[0])
    gs = N("ShaderNodeMath"); gs.operation = "MULTIPLY_ADD"; gs.inputs[1].default_value = glow_amt; Lk(g1.outputs[0], gs.inputs[0])
    Lk(g2.outputs[0], gs.inputs[2])
    gcol = N("ShaderNodeVectorMath"); gcol.operation = "SCALE"; gcol.inputs[0].default_value = tuple(lin(glow)[:3])
    Lk(gs.outputs[0], gcol.inputs[3])
    add = N("ShaderNodeMix"); add.data_type = "RGBA"; add.blend_type = "ADD"; add.inputs[0].default_value = 1.0
    Lk(ramp.outputs[0], add.inputs[6]); Lk(gcol.outputs[0], add.inputs[7])
    mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (1.0, 1.0, stretch); mp.inputs["Location"].default_value = seed
    Lk(nrm.outputs[0], mp.inputs[0])
    nz = N("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = cloud_scale; nz.inputs["Detail"].default_value = 5.0
    nz.inputs["Roughness"].default_value = 0.55
    Lk(mp.outputs[0], nz.inputs["Vector"])
    cm = N("ShaderNodeMapRange"); cm.inputs[1].default_value = cloud[0]; cm.inputs[2].default_value = cloud[1]; cm.clamp = True
    Lk(nz.outputs["Fac"], cm.inputs[0])
    w1 = N("ShaderNodeMapRange"); w1.inputs[1].default_value = band[0]; w1.inputs[2].default_value = band[1]; w1.clamp = True
    Lk(sep.outputs["Z"], w1.inputs[0])
    w2 = N("ShaderNodeMapRange"); w2.inputs[1].default_value = band[2]; w2.inputs[2].default_value = band[3]
    w2.inputs[3].default_value = 1.0; w2.inputs[4].default_value = 0.0; w2.clamp = True
    Lk(sep.outputs["Z"], w2.inputs[0])
    wm = N("ShaderNodeMath"); wm.operation = "MULTIPLY"; Lk(w1.outputs[0], wm.inputs[0]); Lk(w2.outputs[0], wm.inputs[1])
    cmk = N("ShaderNodeMath"); cmk.operation = "MULTIPLY"; Lk(cm.outputs[0], cmk.inputs[0]); Lk(wm.outputs[0], cmk.inputs[1])
    mk = N("ShaderNodeMath"); mk.operation = "MULTIPLY"; mk.inputs[1].default_value = 0.94; Lk(cmk.outputs[0], mk.inputs[0])
    cramp = K._ramp(nt, el.outputs[0], cloud_stops)
    cb = N("ShaderNodeVectorMath"); cb.operation = "SCALE"; cb.inputs[3].default_value = cloud_gain
    Lk(cramp.outputs[0], cb.inputs[0])
    fin = N("ShaderNodeMix"); fin.data_type = "RGBA"; fin.inputs[0].default_value = 1.0
    Lk(mk.outputs[0], fin.inputs[0]); Lk(add.outputs[2], fin.inputs[6]); Lk(cb.outputs[0], fin.inputs[7])
    bg = N("ShaderNodeBackground"); bg.inputs["Strength"].default_value = strength
    out = N("ShaderNodeOutputWorld")
    Lk(fin.outputs[2], bg.inputs[0]); Lk(bg.outputs[0], out.inputs[0])
    return w


def sun_dir(az_deg, el_deg):
    az, el = math.radians(az_deg), math.radians(el_deg)
    return Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))


def key_sun(sunv, energy, colour, angle=1.2):
    k = K.sun((0, 0, 0), energy, colour=colour, angle=angle)
    k.rotation_euler = (-sunv).to_track_quat("-Z", "Y").to_euler()
    return k


# ------------------------------------------------------------------------------------------------ render driver
def run(build, still_id, root):
    """Build with K.STYLE='mix', render in Cycles (STILL_PREVIEW=1 -> 640x360), write data/images/stills/<id>.png (or scratch preview)."""
    import time
    preview = bool(os.environ.get("STILL_PREVIEW"))
    outdir = os.path.join(root, "scratch", "stills") if preview else os.path.join(root, "data", "images", "stills")
    os.makedirs(outdir, exist_ok=True)
    name = ("preview_" if preview else "") + still_id + ".png"
    raw = os.path.join(root, "scratch", "stills", "raw_" + name)
    os.makedirs(os.path.dirname(raw), exist_ok=True)
    K.clear()
    K.STYLE = "mix"
    build()
    res = (640, 360) if preview else (1280, 720)
    samples = int(os.environ.get("STILL_CYCLES_SAMPLES", "24" if preview else "96"))
    K.setup_render(raw, "mix", samples=samples, res=res)
    t = time.time()
    bpy.ops.render.render(write_still=True)
    dt = time.time() - t
    from PIL import Image
    im = Image.open(raw).convert("RGB")
    final = os.path.join(outdir, name)
    im.save(final, optimize=True)
    if not preview and os.path.getsize(final) > 2.4e6:
        im.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG).convert("RGB").save(final, optimize=True)
    os.remove(raw)
    print(f"RENDER {still_id}: {dt:.0f}s {os.path.getsize(final) / 1e6:.2f} MB -> {final}")
    return final
