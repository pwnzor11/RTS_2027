"""Authority Refinery: a fixed centrifuge refinery (3x3 cells). Original design, no franchise references.

Contract for a structure module (read by tools/art/build_structure.py):
  ID, TITLE, FOOTPRINT (cells), CANVAS (frame w,h px), ORIGIN (footprint top-left in the frame, px), DOCK (cell,
  footprint-relative), STATES [(name, frames, ticks_per_frame)], MATERIALS {name: (hex, {principled kwargs})},
  build(S, state, i, n)  -> add primitives to S for one frame; set S.clip_z to cut the building off at a height.

Authoring units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-5.1, 5.1].
The camera looks north from the south, so the south face (y = -5.1 side) and the roofs are what you see.
Screen size of things: 1 unit of x = 18.8 px; 1 unit of z = 14.4 px (cos 40 deg); 1 unit of y = 18.8 px of ground.
"""
import math

ID = "auth_refinery"
TITLE = "Authority Refinery"
FOOTPRINT = (3, 3)
CANVAS = (192, 224)
ORIGIN = (0, 32)        # footprint top-left corner inside the frame (32 px of headroom for the drum and the stack)
DOCK = (1, 3)           # harvester parks in the cell just below the middle of the footprint
CAP_MATERIAL = "concrete2"   # material of the faces made by the build-state cut
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]

MATERIALS = {
    "pad": ("#686C63", dict(rough=0.95)),
    "concrete": ("#8F9389", dict(rough=0.95)),
    "concrete2": ("#6E7269", dict(rough=0.95)),
    "roof": ("#4C5A44", dict(rough=0.85)),
    "hull": ("#566448", dict(rough=0.7)),
    "hull2": ("#4A5840", dict(rough=0.7)),
    "steel": ("#7B8187", dict(rough=0.45, metal=0.5)),
    "rust": ("#85502F", dict(rough=0.95)),
    "dark": ("#15170F", dict(rough=0.9)),
    "white": ("#EDEDE2", dict(rough=0.8)),
    "yellow": ("#C9A640", dict(rough=0.8)),
    "lamp": ("#FFE9A0", dict(rough=0.3, emit="#FFE9A0")),
    "smoke": ("#A3AA9B", dict(rough=1.0)),
    "smoke2": ("#6F766A", dict(rough=1.0)),
    "char": ("#3A4034", dict(rough=1.0)),
    "fire": ("#FFD36A", dict(rough=0.5, emit="#FFC850")),
    # build site (D-064 pass 2): raw materials that the finished building does not use
    "timber": ("#A9824D", dict(rough=0.9)),
    "rebar": ("#7A4A2C", dict(rough=0.7, metal=0.3)),
    "raw": ("#6F6A5E", dict(rough=1.0)),
    "primer": ("#9298A0", dict(rough=0.8)),
    "soil": ("#6E4224", dict(rough=1.0)),
    "soil2": ("#46291A", dict(rough=1.0)),
    "crate": ("#8C6A3C", dict(rough=0.9)),
    "girder": ("#5A6067", dict(rough=0.5, metal=0.6)),
    "string": ("#F0EAD0", dict(rough=0.8)),
    "spark": ("#FFF6C8", dict(rough=0.3, emit="#FFF2B0")),
    # damage (D-064 pass 2): sooted copies of the finished materials, fire ramp, dark smoke
    "concrete_s": ("#6B6E67", dict(rough=0.95)),
    "concrete2_s": ("#50534D", dict(rough=0.95)),
    "roof_s": ("#3D4736", dict(rough=0.85)),
    "hull_s": ("#455039", dict(rough=0.7)),
    "hull2_s": ("#3B4633", dict(rough=0.7)),
    "steel_s": ("#62676C", dict(rough=0.45, metal=0.5)),
    "pad_s": ("#4E5A47", dict(rough=0.95)),
    "yellow_s": ("#A08832", dict(rough=0.8)),
    "fire2": ("#FF9A3C", dict(rough=0.5, emit="#FF8A30")),
    "fire3": ("#E0502A", dict(rough=0.5, emit="#E04820")),
    "fire1": ("#FFF0A0", dict(rough=0.5, emit="#FFF0A0")),
    "soot": ("#25281F", dict(rough=1.0)),
    "soot2": ("#3A3F35", dict(rough=1.0)),
}
SOOT_SKIN = {"concrete": "concrete_s", "concrete2": "concrete2_s", "roof": "roof_s", "hull": "hull_s", "hull2": "hull2_s",
             "steel": "steel_s", "pad": "pad_s", "yellow": "yellow_s"}
PRIMER_SKIN = {"house": "primer", "house2": "primer", "roof": "primer", "hull": "primer", "hull2": "primer", "concrete": "raw", "yellow": "primer",
               "white": "primer", "lamp": "dark"}

HALL_X = 4.1
HALL_Y0, HALL_Y1 = -2.6, 2.2     # south wall / north wall (ground units)
WALL_H = 3.6                     # roof slab bottom
ROOF_T = 0.22
DRUM = (-1.9, 0.7)               # drum centre (x, y)
DRUM_R = 1.45
STACK = (3.3, 0.7)
Z_BUILD = [0.3, 1.1, 2.0, 2.9, 3.85, 4.8, 5.9]   # clip heights for the build frames 0..6; frame 7 = complete


def frame_params(state, i):
    return dict(theta=i * 22.5, damaged=(state == "damaged"))


def hazard(S, y0, y1, z=0.15):
    """Yellow / dark hazard stripes across the dock apron."""
    x0, w = -4.8, 0.6
    for k in range(16):
        S.bb(f"haz{k}", x0 + k * w, x0 + (k + 1) * w, y0, y1, 0.14, z, "yellow" if k % 2 == 0 else "dark", 0.0)


def scorch(S, name, x, y, z, w, h, d=0.03, plane="S"):
    """Irregular scorch patch made of a few dark boxes, on the south wall (plane S) or the roof (plane T)."""
    spec = [(0, 0, 1.0, 1.0), (-0.35, 0.3, 0.5, 0.6), (0.4, 0.35, 0.45, 0.5), (0.1, -0.4, 0.7, 0.35)]
    for k, (dx, dz, sw, sh) in enumerate(spec):
        if plane == "S":
            S.box(f"{name}{k}", (w * sw, d, h * sh), (x + dx * w * 0.5, y, z + dz * h * 0.5), "char", 0.0, nocast=True)
        else:
            S.box(f"{name}{k}", (w * sw, h * sh, d), (x + dx * w * 0.5, y + dz * h * 0.5, z), "char", 0.0, nocast=True)


# ====================================================================================================== build site
COLX = (-4.0, -2.0, 0.0, 2.0, 4.0)
COLY = (-2.5, 2.1)


def beam(S, name, p0, p1, w, m, nocast=False):
    """Square-section member between two points given in game units (x, y, z); y is stretched here."""
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
    S.bb(name, x - w / 2, x + w / 2, y - w / 2, y + w / 2, z0, z1, m, 0.0)


def spark(S, x, y, z, i, seed=0):
    """Welding flash: a bright core and a few flying sparks (different per frame)."""
    S.bb("weld", x - 0.1, x + 0.1, y - 0.1, y + 0.1, z - 0.1, z + 0.1, "spark", 0.0, nocast=True)
    for k in range(5):
        a = (i * 2.1 + k * 1.7 + seed) * 1.3
        r = 0.22 + 0.2 * ((k * 5 + i * 3 + seed) % 4)
        cx, cy = x + r * math.cos(a), y + r * math.sin(a) * 0.5
        S.bb(f"sprk{k}", cx - 0.05, cx + 0.05, cy - 0.05, cy + 0.05, z + 0.15 * k - 0.15, z + 0.15 * k - 0.05, "spark", 0.0, nocast=True)


def scaffold(S, x0, x1, y0, y1, levels, top=None, ladder=True):
    """Timber scaffold tower in front of the south face: poles, plank decks, guard rails, diagonal braces, ladder."""
    top = top if top is not None else max(levels) + 0.85
    for px in (x0, x1):
        for py in (y0, y1):
            post(S, "sp", px, py, 0.14, top, 0.08, "timber")
    for z in levels:
        S.bb("deck", x0 - 0.05, x1 + 0.05, y0 - 0.05, y1 + 0.05, z, z + 0.07, "timber", 0.0)
        beam(S, "rl", (x0, y0, z + 0.65), (x1, y0, z + 0.65), 0.05, "timber")
        beam(S, "rl", (x0, y0, z + 0.65), (x0, y1, z + 0.65), 0.05, "timber")
        beam(S, "rl", (x1, y0, z + 0.65), (x1, y1, z + 0.65), 0.05, "timber")
    beam(S, "br", (x0, y0, 0.14), (x1, y0, max(levels) + 0.1), 0.06, "timber")
    beam(S, "br", (x1, y0, 0.14), (x0, y0, max(levels) * 0.55), 0.06, "timber")
    if ladder:
        for lx in (x0 + 0.2, x0 + 0.5):
            post(S, "lad", lx, y0 - 0.2, 0.14, max(levels) + 0.3, 0.05, "timber")
        z = 0.4
        while z < max(levels):
            S.bb("rung", x0 + 0.2, x0 + 0.5, y0 - 0.22, y0 - 0.18, z, z + 0.04, "timber", 0.0)
            z += 0.32


def crane(S, load=None):
    """Small derrick crane on the east pad edge: lattice mast, tilted boom over the roof, guy strut, hook block + load."""
    mx, my = 4.75, 1.3
    for ax in (-0.15, 0.15):
        for ay in (-0.15, 0.15):
            post(S, "cm", mx + ax, my + ay, 0.14, 5.9, 0.1, "yellow")
    z = 0.14
    while z < 5.8:
        beam(S, "cb", (mx - 0.15, my - 0.15, z), (mx + 0.15, my - 0.15, z + 1.0), 0.05, "yellow")
        beam(S, "cb", (mx + 0.15, my - 0.15, z), (mx + 0.15, my + 0.15, z + 1.0), 0.05, "yellow")
        z += 1.0
    S.bb("cab", mx - 0.28, mx + 0.28, my - 0.28, my + 0.28, 5.9, 6.25, "yellow", 0.02)
    S.bb("cabw", mx - 0.26, mx + 0.0, my - 0.3, my - 0.26, 6.0, 6.18, "dark", 0.0)
    tip = (0.2, 0.5, 6.9)
    beam(S, "boom", (mx, my, 6.05), tip, 0.24, "yellow")
    beam(S, "boom2", (mx, my, 6.45), tip, 0.1, "steel")
    beam(S, "strut", (mx, my, 6.35), (4.95, 3.2, 0.14), 0.1, "steel")
    hx, hy, hz = 1.11, 0.66, 6.75
    hook_z = 5.0
    beam(S, "cable", (hx, hy, hz), (hx, hy, hook_z + 0.3), 0.045, "dark")
    S.bb("hook", hx - 0.12, hx + 0.12, hy - 0.12, hy + 0.12, hook_z + 0.2, hook_z + 0.42, "dark", 0.0)
    if load == "girder":
        beam(S, "load", (hx - 0.9, hy, hook_z), (hx + 0.9, hy, hook_z), 0.17, "girder")
    elif load == "pallet":
        S.bb("load", hx - 0.55, hx + 0.55, hy - 0.35, hy + 0.35, hook_z - 0.2, hook_z + 0.05, "primer", 0.0)
        S.bb("loadb", hx - 0.6, hx + 0.6, hy - 0.4, hy + 0.4, hook_z - 0.3, hook_z - 0.2, "timber", 0.0)
    elif load == "panel":
        S.bb("load", hx - 0.8, hx + 0.8, hy - 0.5, hy + 0.5, hook_z - 0.1, hook_z + 0.0, "primer", 0.0)
    if load:
        for sx in (-1, 1):
            beam(S, "sling", (hx, hy, hook_z + 0.25), (hx + sx * 0.6, hy, hook_z + 0.02), 0.03, "dark")


def site_clutter(S, i):
    """Materials lying about: crates, pipes, barrels, planks, rebar bundle, a site cabin. Thinned out as the job proceeds."""
    if i <= 1:
        S.bb("crate", -4.7, -3.8, -4.5, -3.6, 0.14, 0.85, "crate", 0.03)
        S.bb("crate", -4.55, -3.95, -4.4, -3.75, 0.85, 1.4, "crate", 0.03, rot=(0, 0, 12))
        S.bb("crate", 3.5, 4.4, -4.6, -3.7, 0.14, 0.8, "crate", 0.03)
    if i <= 2:
        for k, (x, z) in enumerate(((-1.0, 0.3), (-1.0, 0.65), (-1.3, 0.98))):
            S.cyl("lpipe", 0.14, 2.6, (x, -4.3, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", 0.7, 3.0, -4.3, -4.1, 0.14, 0.3, "rebar", 0.0)
        S.bb("rebarpile", 0.8, 2.9, -4.1, -3.9, 0.14, 0.25, "rebar", 0.0)
        S.cyl("barrel", 0.3, 0.7, (4.4, -3.2, 0.5), "rust", verts=12)
        S.cyl("barrel", 0.3, 0.7, (3.8, -3.0, 0.5), "rust", verts=12)
    S.bb("planks", 2.4, 4.4, 3.3, 3.8, 0.14, 0.5, "timber", 0.0)
    S.bb("cabin", -4.6, -2.8, 3.1, 4.3, 0.14, 1.15, "primer", 0.03)
    S.bb("cabdoor", -4.2, -3.8, 3.05, 3.12, 0.14, 0.85, "dark", 0.0)
    S.bb("cabwin", -3.5, -3.05, 3.05, 3.12, 0.5, 0.85, "girder", 0.0)


def site_slab(S, top=0.5):
    S.bb("slab", -4.3, 4.3, -2.8, 2.4, 0.14, top, "raw", 0.02)
    S.cyl("slabD", 1.8, top - 0.14 + 0.03, (DRUM[0], DRUM[1], (top + 0.14 + 0.03) / 2), "raw", verts=28)
    S.bb("form", -4.3, 4.3, -2.86, -2.8, 0.14, top + 0.15, "timber", 0.0)
    S.bb("form", -4.36, -4.3, -2.8, 2.4, 0.14, top + 0.15, "timber", 0.0)


def drum_posts(S, z0, z1, rings, r=1.4):
    dx, dy = DRUM
    pts = [(dx + r * math.cos(math.radians(k * 45 + 22.5)), dy + r * math.sin(math.radians(k * 45 + 22.5))) for k in range(8)]
    for (x, y) in pts:
        post(S, "dp", x, y, z0, z1, 0.14, "girder")
    for z in rings:
        for k in range(8):
            a, b = pts[k], pts[(k + 1) % 8]
            beam(S, "dring", (a[0], a[1], z), (b[0], b[1], z), 0.09, "girder")


def hall_frame(S, h, girders, roof=False, braces=True, truss=False):
    for x in COLX:
        for y in COLY:
            S.bb("bp", x - 0.22, x + 0.22, y - 0.22, y + 0.22, 0.5, 0.58, "raw", 0.0)
            post(S, "col", x, y, 0.5, h, 0.2, "girder")
    for z in girders:
        for y in COLY:
            beam(S, "gx", (-4.0, y, z), (4.0, y, z), 0.15, "girder")
        for x in COLX:
            beam(S, "gy", (x, -2.5, z), (x, 2.1, z), 0.15, "girder")
    if braces:
        zs = [0.5] + list(girders)
        for a, b in zip(zs, zs[1:]):
            for k in range(len(COLX) - 1):
                if k in (1, 2):
                    continue       # the bay stays open
                beam(S, "xb", (COLX[k], -2.5, a), (COLX[k + 1], -2.5, b), 0.07, "girder")
                beam(S, "xb", (COLX[k + 1], -2.5, a), (COLX[k], -2.5, b), 0.07, "girder")
    if roof:
        beam(S, "purl", (-4.0, -0.2, 3.55), (4.0, -0.2, 3.55), 0.12, "girder")
        if truss:
            for x in COLX:
                for k in range(6):
                    y0, y1 = -2.5 + 4.6 * k / 6, -2.5 + 4.6 * (k + 1) / 6
                    beam(S, "tz", (x, y0, 3.5 if k % 2 == 0 else 3.9), (x, y1, 3.9 if k % 2 == 0 else 3.5), 0.06, "girder")
                beam(S, "tt", (x, -2.5, 3.9), (x, 2.1, 3.9), 0.08, "girder")


def cladding(S, rects, m="primer"):
    for (x0, x1, z0, z1) in rects:
        S.bb("clad", x0, x1, HALL_Y0 - 0.06, HALL_Y0 + 0.04, z0, z1, m, 0.0)
        for k in range(int((x1 - x0) / 0.75)):
            S.bb("seam", x0 + 0.75 * (k + 1) - 0.02, x0 + 0.75 * (k + 1) + 0.02, HALL_Y0 - 0.08, HALL_Y0 - 0.06, z0, z1, "girder", 0.0, nocast=True)


def build_site(S, i):
    """Construction stages 0..5 (stage 6 is the finished model in primer, see build())."""
    dx, dy = DRUM
    S.bb("pad", -5.0, 5.0, -5.0, 5.0, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    if i == 0:       # surveyed plot: dug foundation, spoil heaps, stakes, string lines
        S.bb("pit", -4.3, 4.3, -2.8, 2.4, 0.14, 0.17, "soil", 0.0)
        S.bb("pit2", -4.3, 4.3, -2.8, -2.45, 0.14, 0.2, "soil2", 0.0)
        S.bb("pit2", -4.3, -3.95, -2.8, 2.4, 0.14, 0.2, "soil2", 0.0)
        S.bb("pit2", 3.95, 4.3, -2.8, 2.4, 0.14, 0.2, "soil2", 0.0)
        S.bb("pit2", -4.3, 4.3, 2.05, 2.4, 0.14, 0.2, "soil2", 0.0)
        S.cyl("pitD", 1.8, 0.07, (dx, dy, 0.17), "soil", verts=28)
        S.cyl("pitD2", 1.55, 0.09, (dx, dy, 0.18), "soil2", verts=28)
        S.cyl("pitD3", 1.3, 0.1, (dx, dy, 0.19), "soil", verts=28)
        for (mx, my, mw, mh) in ((-4.9, -1.0, 0.6, 2.8), (4.7, -1.8, 0.6, 3.2), (-1.0, 3.0, 4.0, 0.5), (1.0, -3.3, 2.4, 0.4)):
            S.bb("mound", mx - mw / 2, mx + mw / 2, my - mh / 2, my + mh / 2, 0.14, 0.5, "soil", 0.18)
        for (sx, sy) in [(-4.85, -3.2), (4.85, -3.2), (-4.85, 2.9), (4.85, 2.9)]:
            post(S, "stake", sx, sy, 0.14, 1.0, 0.1, "timber")
            post(S, "staket", sx, sy, 0.8, 1.0, 0.12, "string")
        S.bb("line", -4.85, 4.85, -3.22, -3.18, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("line", -4.85, 4.85, 2.88, 2.92, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("line", -4.87, -4.83, -3.2, 2.9, 0.88, 0.9, "string", 0.0, nocast=True)
        S.bb("line", 4.83, 4.87, -3.2, 2.9, 0.88, 0.9, "string", 0.0, nocast=True)
        for k in range(4):
            a = math.radians(k * 90 + 45)
            post(S, "dstake", dx + 2.2 * math.cos(a), dy + 2.2 * math.sin(a) * 0.9, 0.14, 0.75, 0.09, "timber")
            post(S, "dstake", dx + 2.2 * math.cos(a), dy + 2.2 * math.sin(a) * 0.9, 0.6, 0.75, 0.11, "string")
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
        for k in range(10):
            a = math.radians(k * 36)
            post(S, "rbD", dx + 1.5 * math.cos(a), dy + 1.5 * math.sin(a), 0.5, 1.3, 0.05, "rebar")
        for yy in (-1.5, 0.0, 1.5):
            S.bb("mesh", -3.7, 3.7, yy - 0.025, yy + 0.025, 0.52, 0.56, "rebar", 0.0, nocast=True)
        S.cyl("mixer", 0.38, 0.8, (-3.2, -4.3, 0.7), "primer", verts=14)
        S.bb("mixer", -3.55, -2.85, -4.6, -4.0, 0.14, 0.4, "girder", 0.0)
        S.bb("sandpile", 1.0, 2.6, 3.0, 4.0, 0.14, 0.7, "soil", 0.3)
    elif i == 2:     # first steel storey: columns to 2.1, floor girders, ring posts
        site_slab(S, 0.5)
        hall_frame(S, 2.15, (1.95,), braces=True)
        drum_posts(S, 0.5, 2.5, (2.3,))
        scaffold(S, -3.9, -2.6, -3.5, -2.8, (0.8, 1.7))
        crane(S)
        spark(S, -2.0, -2.5, 2.1, i, 1)
    elif i == 3:     # full steel frame of the hall, drum ring going up
        site_slab(S, 0.5)
        hall_frame(S, 3.65, (1.95, 3.55), roof=True)
        drum_posts(S, 0.5, 5.7, (2.3, 3.7, 4.9, 5.7))
        scaffold(S, -3.9, -2.6, -3.5, -2.8, (0.8, 1.7, 2.6))
        scaffold(S, 2.6, 3.9, -3.5, -2.8, (0.8, 1.7))
        crane(S, "girder")
        spark(S, 0.0, -2.5, 3.55, i, 3)
        spark(S, -2.6, 2.1, 3.6, i, 7)
    elif i == 4:     # roof trusses, first wall panels, drum base plated, pipes laid
        site_slab(S, 0.5)
        hall_frame(S, 3.65, (1.95, 3.55), roof=True, truss=True)
        drum_posts(S, 0.5, 5.7, (2.3, 3.7, 4.9, 5.7))
        S.cyl("shellD", 1.45, 1.0, (dx, dy, 3.6 + 0.5), "primer", verts=28)
        S.cyl("ringD", 1.5, 0.12, (dx, dy, 3.6 + 1.05), "girder", verts=28)
        cladding(S, [(-4.0, -2.0, 0.5, 2.0), (2.0, 4.0, 0.5, 1.4)])
        scaffold(S, -3.9, -2.6, -3.5, -2.8, (0.8, 1.7, 2.6, 3.4))
        scaffold(S, 2.6, 3.9, -3.5, -2.8, (0.8, 1.7, 2.6))
        S.cyl("pipeY", 0.3, 2.6, (4.62, -0.5, 0.5), "steel", axis="Y", verts=14)
        S.cyl("pipeY", 0.3, 2.0, (4.62, 2.8, 0.5), "steel", axis="Y", verts=14)
        crane(S, "pallet")
        spark(S, -2.0, -2.5, 1.9, i, 5)
        spark(S, 4.0, 2.1, 3.6, i, 2)
    elif i == 5:     # walls mostly clad, roof half decked, drum shell half plated, stack base
        site_slab(S, 0.5)
        hall_frame(S, 3.65, (1.95, 3.55), roof=True, truss=True, braces=False)
        cladding(S, [(-4.0, -1.3, 0.5, 2.5), (-4.0, -2.9, 2.5, 3.55), (-2.9, -1.3, 2.5, 3.0), (1.3, 4.1, 0.5, 2.5), (1.3, 2.5, 2.5, 3.55)])
        S.bb("lintel", -1.5, 1.5, HALL_Y0 - 0.12, HALL_Y0 + 0.05, 2.5, 2.9, "raw", 0.02)
        S.bb("deckA", -4.35, 4.35, HALL_Y0 - 0.2, -0.8, 3.6, 3.82, "primer", 0.04)
        S.bb("deckB", 2.2, 4.35, -0.8, 2.4, 3.6, 3.82, "primer", 0.04)
        for k in range(8):
            S.bb("dseam", -4.3 + k * 1.15, -4.3 + k * 1.15 + 0.04, HALL_Y0 - 0.2, -0.8, 3.82, 3.84, "girder", 0.0, nocast=True)
        S.cyl("plinthD", 1.7, 0.7, (dx, dy, 3.6 + 0.35), "raw", verts=28)
        drum_posts(S, 4.3, 5.8, (5.2, 5.8))
        S.cyl("shellD", 1.45, 1.3, (dx, dy, 4.3 + 0.65), "primer", verts=28)
        S.cyl("ringD", 1.5, 0.1, (dx, dy, 4.3 + 1.3), "girder", verts=28)
        for k in range(2):
            c, s_ = 1.4 * math.cos(k * 1.57), 1.4 * math.sin(k * 1.57)
            beam(S, "spk", (dx - c, dy - s_, 5.8), (dx + c, dy + s_, 5.8), 0.1, "girder")
        S.cyl("stackb", 0.42, 0.8, (STACK[0], STACK[1], 3.82 + 0.4), "raw", verts=14)
        S.cyl("pipeY", 0.3, 4.6, (4.62, 0.3, 0.5), "steel", axis="Y", verts=14)
        scaffold(S, -3.9, -2.6, -3.5, -2.8, (0.8, 1.7, 2.6, 3.4), top=4.3)
        scaffold(S, 2.6, 3.9, -3.5, -2.8, (0.8, 1.7, 2.6, 3.4), top=4.3)
        crane(S, "panel")
        spark(S, 2.5, -2.7, 3.3, i, 4)
        spark(S, -1.2, -0.8, 5.8, i, 9)
    site_clutter(S, i if i < 5 else 9)


# ====================================================================================================== damage
def fire(S, name, x, y, z0, r, h, i, ph0):
    """Layered flame: red outer, orange middle, yellow-white core; height and sway flicker with the frame."""
    for t in range(2):
        f = 0.72 + 0.28 * math.sin(2 * math.pi * (i / 4 + ph0 + t * 0.37))
        sw = 0.12 * math.sin(2 * math.pi * (i / 4 + ph0 * 1.7 + t * 0.2))
        xx = x + t * 0.22 * r + sw
        S.cone(f"{name}r{t}", r * (1.0 - 0.25 * t), 0.05, h * f * (1 - 0.18 * t), (xx, y, z0 + h * f * (1 - 0.18 * t) / 2), "fire3")
        S.cone(f"{name}o{t}", r * 0.72 * (1.0 - 0.25 * t), 0.04, h * f * 0.8 * (1 - 0.18 * t), (xx, y - 0.02, z0 + 0.02 + h * f * 0.8 * (1 - 0.18 * t) / 2), "fire2")
        S.cone(f"{name}y{t}", r * 0.5 * (1.0 - 0.25 * t), 0.03, h * f * 0.62 * (1 - 0.18 * t), (xx, y - 0.04, z0 + 0.03 + h * f * 0.62 * (1 - 0.18 * t) / 2), "fire")
    S.sphere(f"{name}c", r * 0.3, (x, y - 0.06, z0 + 0.12), "fire1", squash=0.8)


def damaged_roof(S):
    z0, z1 = WALL_H, WALL_H + ROOF_T
    S.bb("roof", -4.35, 2.3, -2.2, 2.4, z0, z1, "roof", 0.06)
    S.bb("roof", -4.35, 2.3, -2.8, -2.2, z0, z1, "roof", 0.06)
    S.bb("roof", 2.3, 2.8, -2.8, -2.5, z0, z1, "roof", 0.02)
    S.bb("roof", 2.3, 4.35, -0.9, 2.4, z0, z1, "roof", 0.06)
    S.bb("roof", 2.3, 3.0, -1.7, -0.9, z0, z1, "roof", 0.02)
    S.bb("roof", 3.0, 3.6, -1.25, -0.9, z0, z1, "roof", 0.02)
    S.bb("roof", 3.9, 4.35, -1.3, -0.9, z0, z1, "roof", 0.02)
    S.bb("parapet", -4.35, 2.3, HALL_Y0 - 0.2, HALL_Y0 - 0.02, z1, z1 + 0.22, "concrete2", 0.03)
    S.bb("parapet", 2.3, 2.8, HALL_Y0 - 0.2, HALL_Y0 - 0.02, z1, z1 + 0.09, "concrete2", 0.02)
    S.bb("parapetS", -HALL_X - 0.14, -HALL_X, HALL_Y0 - 0.2, HALL_Y1 + 0.2, z1, z1 + 0.18, "concrete2", 0.02)
    S.bb("parapetS", HALL_X, HALL_X + 0.14, -0.9, HALL_Y1 + 0.2, z1, z1 + 0.18, "concrete2", 0.02)
    # the collapsed corner: dark void down into the hall, a dangling slab, bent girders and rebar, rubble
    S.bb("void", 2.3, 4.1, -2.6, -0.9, z0 + 0.01, z0 + 0.05, "dark", 0.0)
    S.bb("voidw", 2.3, 4.1, -2.6, -2.35, z0 + 0.05, z0 + 0.4, "soot", 0.0)
    S.box("slabH", (1.3, 0.9, 0.2), (2.75, -1.35, 3.95), "roof", 0.0, rot=(0, 34, 6))
    S.box("slabH2", (0.8, 0.7, 0.18), (3.9, -2.2, 3.78), "roof", 0.0, rot=(-25, 0, 20))
    beam(S, "bg", (2.5, -2.4, 3.62), (2.9, -1.2, 4.75), 0.13, "girder")
    beam(S, "bg", (3.5, -2.4, 3.62), (3.62, -1.1, 4.3), 0.13, "girder")
    beam(S, "bg", (2.2, -1.7, 3.95), (4.1, -2.1, 3.72), 0.12, "girder")
    for k, (a, b) in enumerate((((2.3, -1.0, 3.82), (1.95, -0.7, 4.5)), ((3.0, -0.95, 3.82), (3.2, -0.6, 4.3)), ((3.95, -1.3, 3.82), (4.2, -1.0, 4.4)))):
        beam(S, f"rbar{k}", a, b, 0.05, "rebar")
    for k, (x, y, w) in enumerate(((2.4, -2.3, 0.4), (3.2, -2.45, 0.35), (3.7, -1.5, 0.3), (2.7, -1.0, 0.3))):
        S.box(f"debris{k}", (w, w * 0.8, 0.22), (x, y, 3.72), "concrete_s", 0.0, rot=(0, 0, 25 * k + 10))


def damaged_wall(S, yw):
    """Blown-out section right of the bay, cracked/missing panel pieces on the left, scorch radiating from the breaches."""
    yb = yw - 0.2
    S.bb("breach", 2.65, 4.0, yw - 0.11, yw - 0.07, 0.85, 3.05, "dark", 0.0)
    for (x0, x1, z0, z1) in ((2.65, 3.3, 3.05, 3.42), (3.3, 3.7, 3.05, 3.22), (3.7, 4.0, 3.05, 3.32), (2.5, 2.65, 1.4, 2.6),
                             (4.0, 4.12, 0.9, 1.8), (2.65, 2.9, 0.62, 0.85), (3.5, 4.0, 0.62, 0.85)):
        S.bb("breachj", x0, x1, yw - 0.11, yw - 0.07, z0, z1, "dark", 0.0)
    # interior: bent girders in front of the black void
    post(S, "ig", 2.95, yb, 0.85, 3.05, 0.15, "girder")
    post(S, "ig", 3.6, yb, 0.85, 2.55, 0.15, "girder")
    beam(S, "ig", (2.95, yb, 0.95), (3.6, yb, 2.5), 0.1, "girder")
    beam(S, "ig", (2.7, yb, 2.05), (3.95, yb, 1.6), 0.1, "girder")
    beam(S, "ig", (3.6, yb, 2.55), (3.95, yb, 3.0), 0.09, "rebar")
    S.box("chunk", (0.5, 0.22, 0.35), (2.45, yw - 0.16, 3.25), "concrete2_s", 0.0, rot=(0, 18, 12))
    S.box("chunk", (0.35, 0.2, 0.3), (4.05, yw - 0.16, 3.0), "concrete2_s", 0.0, rot=(0, -20, -15))
    S.box("shard1", (0.9, 0.06, 0.4), (3.25, yb - 0.04, 0.98), "house", 0.0, rot=(0, 0, 14))
    # rubble and a fallen girder in front of the breach
    for k, (x, y, w, h) in enumerate(((2.9, -3.15, 0.6, 0.4), (3.5, -3.4, 0.5, 0.32), (3.9, -3.0, 0.4, 0.5), (2.5, -3.55, 0.45, 0.28), (3.2, -3.0, 0.35, 0.45))):
        S.box(f"rub{k}", (w, w * 0.8, h), (x, y, 0.14 + h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k))
    beam(S, "fallen", (2.2, -4.0, 0.3), (3.9, -3.3, 0.3), 0.15, "girder")
    # left panel: missing top-left piece, cracks
    S.bb("pmiss", -4.0, -3.25, yw - 0.16, yw - 0.1, 2.3, 3.0, "dark", 0.0)
    S.bb("pmiss", -3.25, -2.85, yw - 0.16, yw - 0.1, 2.65, 3.0, "dark", 0.0)
    post(S, "ig", -3.6, yw - 0.22, 2.3, 3.0, 0.12, "girder")
    for k, (a, b) in enumerate((((-3.0, 0.75, 0), (-2.55, 1.7, 0)), ((-2.55, 1.7, 0), (-3.0, 2.3, 0)), ((-1.9, 0.8, 0), (-2.3, 1.6, 0)))):
        beam(S, f"crack{k}", (a[0], yw - 0.17, a[1]), (b[0], yw - 0.17, b[1]), 0.05, "dark")
    # scorch radiating from the breaches (wall, roof, pad)
    scorch(S, "scW", -3.7, yw - 0.18, 2.75, 1.4, 0.8)
    scorch(S, "scA", 3.35, yw - 0.16, 3.35, 2.4, 0.5)
    scorch(S, "scB", 2.2, yw - 0.12, 3.3, 1.2, 0.4)
    scorch(S, "scR", 3.3, yw - 0.14, 0.4, 2.0, 0.4)
    scorch(S, "scC", 1.7, yw - 0.12, 1.2, 0.5, 1.2)
    scorch(S, "scT", 3.3, -0.1, WALL_H + ROOF_T + 0.03, 2.4, 1.2, 0.04, plane="T")
    scorch(S, "scT", 0.9, -2.4, WALL_H + ROOF_T + 0.03, 1.5, 0.6, 0.04, plane="T")
    scorch(S, "scP", 3.3, -3.7, 0.16, 2.4, 1.4, 0.03, plane="T")


def damage_fx(S, i, yw, zr, stack_top):
    """Fire (yellow -> orange -> red), smoke plume that drifts and grows, embers. Loops over the 4 frames."""
    fire(S, "fA", 3.55, -1.8, zr - 0.2, 0.7, 1.8, i, 0.0)
    fire(S, "fB", 3.95, -2.3, zr - 0.2, 0.5, 1.3, i, 0.35)
    fire(S, "fC", 3.25, yw - 0.28, 0.95, 0.5, 1.2, i, 0.6)
    fire(S, "fD", 3.3, 0.7, stack_top - 0.05, 0.28, 0.8, i, 0.15)
    # main plume from the roof breach: dark at the bottom, greyer and wider further up, drifting east and north
    n = 10
    for k in range(n):
        ph = (k / n + i / 4) % 1.0
        r = 0.42 + 0.42 * ph
        r *= 1 - max(0.0, (ph - 0.85) / 0.15) * 0.75
        jx = 0.3 * math.sin(k * 2.7) * (0.4 + ph)
        x = 3.7 + 0.35 * ph + jx + 0.12 * math.sin(2 * math.pi * (ph * 1.3 + k * 0.37))
        y = -1.8 + 1.2 * ph + 0.25 * math.cos(k * 1.9)
        z = zr + 0.2 + 2.9 * ph
        S.sphere(f"plume{k}", r, (x, y, z), "soot" if ph < 0.55 else "soot2", squash=0.9)
    for k in range(3):
        ph = (k / 3 + 0.1 + i / 4) % 1.0
        r = (0.28 + 0.3 * ph) * (1 - max(0.0, (ph - 0.8) / 0.2) * 0.7)
        S.sphere(f"smk{k}", r, (2.7 - 0.45 * ph + 0.1 * math.sin(6 * ph + k), 0.7 + 0.3 * ph, stack_top + 0.9 + 1.0 * ph), "soot2", squash=0.9)
    # embers: a few bright specks drifting up from the breaches
    for k in range(6):
        ph = (k / 6 + i / 4 * 1.0 + 0.07 * k) % 1.0
        ex = 3.0 + 0.7 * math.sin(k * 2.4 + 1.0) + 0.5 * ph
        ey = -1.9 + 0.5 * math.cos(k * 1.7)
        S.bb(f"ember{k}", ex - 0.06, ex + 0.06, ey - 0.05, ey + 0.05, zr + 0.5 + 2.3 * ph, zr + 0.62 + 2.3 * ph, "fire2" if k % 2 else "fire", 0.0, nocast=True)


def build(S, state, i, n):
    if state == "build":
        i_anim = 0          # the completed building (build frame 7) is exactly idle frame 0
    else:
        i_anim = i
    P = frame_params(state, i_anim)
    dmg = P["damaged"]
    th = P["theta"]
    YS = S.YS
    building = state == "build"
    if building and i < 6:
        build_site(S, i)
        return
    if building and i == 6:           # almost done: the finished model, unpainted (primer) and without the drum plates
        S.skin.update(PRIMER_SKIN)
        S.hide = lambda nm: nm.startswith(("plate", "sten", "puff"))
    if dmg:
        S.skin.update(SOOT_SKIN)

    # ------------------------------------------------------------------ pad, apron, hazard stripes
    S.bb("pad", -5.0, 5.0, -5.0, 5.0, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    hazard(S, -4.88, -4.4)
    for sx in (-1.45, 1.45):
        S.bb("guide", sx - 0.07, sx + 0.07, -4.4, -2.9, 0.14, 0.17, "yellow", 0.0)

    # ------------------------------------------------------------------ hall: walls, pilasters, roof
    S.bb("hall", -HALL_X, HALL_X, HALL_Y0, HALL_Y1, 0.14, WALL_H, "concrete", 0.05)
    S.bb("plinth", -HALL_X - 0.1, HALL_X + 0.1, HALL_Y0 - 0.1, HALL_Y1 + 0.1, 0.14, 0.55, "concrete2", 0.04)
    for px in (-HALL_X, -2.55, 2.55, HALL_X):
        S.bb(f"pil{px}", px - 0.22, px + 0.22, HALL_Y0 - 0.14, HALL_Y0 + 0.1, 0.14, WALL_H + 0.05, "concrete2", 0.04)
    if dmg:
        damaged_roof(S)
    else:
        S.bb("roof", -HALL_X - 0.25, HALL_X + 0.25, HALL_Y0 - 0.2, HALL_Y1 + 0.2, WALL_H, WALL_H + ROOF_T, "roof", 0.06)
        S.bb("parapet", -HALL_X - 0.25, HALL_X + 0.25, HALL_Y0 - 0.2, HALL_Y0 - 0.02, WALL_H + ROOF_T, WALL_H + ROOF_T + 0.22, "concrete2", 0.03)
        for sx in (-1, 1):
            S.bb("parapetS", sx * (HALL_X + 0.07) - 0.07, sx * (HALL_X + 0.07) + 0.07, HALL_Y0 - 0.2, HALL_Y1 + 0.2, WALL_H + ROOF_T, WALL_H + ROOF_T + 0.18, "concrete2", 0.02)

    # ------------------------------------------------------------------ south face: bay, house panels, stencils
    yw = HALL_Y0 - 0.02
    S.bb("bay", -1.25, 1.25, yw - 0.06, yw + 0.1, 0.14, 2.5, "dark", 0.0)
    S.bb("lintel", -1.55, 1.55, yw - 0.16, yw + 0.1, 2.5, 2.95, "concrete2", 0.04)
    S.bb("lintelstripe", -1.5, 1.5, yw - 0.18, yw - 0.14, 2.62, 2.82, "yellow", 0.0)
    for sx in (-1, 1):
        S.bb("jamb", sx * 1.42 - 0.17, sx * 1.42 + 0.17, yw - 0.14, yw + 0.1, 0.14, 2.55, "concrete2", 0.03)
        # house panels: big plaque on each side of the bay, framed (23 px x 20 px at 1x)
        cx = sx * 2.9
        S.bb("pframe", cx - 1.2, cx + 1.2, yw - 0.08, yw + 0.02, 0.62, 3.1, "dark", 0.0)
        if dmg and sx > 0:
            S.bb("panel", 1.8, 2.65, yw - 0.14, yw - 0.06, 0.72, 3.0, "house", 0.0)
        else:
            S.bb("panel", cx - 1.1, cx + 1.1, yw - 0.14, yw - 0.06, 0.72, 3.0, "house", 0.0)
        # stencil bars on the plinth band
        for k in range(3):
            S.bb("sten", cx - 0.55 + k * 0.5, cx - 0.3 + k * 0.5, yw - 0.1, yw - 0.06, 0.22, 0.5, "white", 0.0, nocast=True)
    # rust streaks under the parapet
    for k, (rx, rw, rh) in enumerate(((-2.0, 0.18, 0.9), (0.2, 0.14, 0.5), (1.9, 0.2, 0.8), (-4.0, 0.16, 0.6))):
        S.bb(f"streak{k}", rx, rx + rw, yw - 0.12, yw - 0.1, 3.6 - rh, 3.6, "rust", 0.0, nocast=True)
    # hopper chute: funnel out of the bay toward the dock, sloped steel with concrete cheeks
    run, rise = 1.9 * YS, 1.05
    ang = -math.degrees(math.atan2(rise, run))   # tilt about X: top end toward the wall
    S.box("chute", (2.3, math.hypot(run, rise) / YS, 0.12), (0, yw - 0.95 - 0.35, 0.85), "steel", 0.03, rot=(ang, 0, 0))
    for sx in (-1, 1):
        S.box("cheek", (0.22, math.hypot(run, rise) / YS, 0.5), (sx * 1.3, yw - 0.95 - 0.35, 0.92), "concrete2", 0.03, rot=(ang, 0, 0))
    S.bb("hopperlip", -1.3, 1.3, yw - 2.25, yw - 2.05, 0.2, 0.55, "hull2", 0.04)
    # gantry arch over the bay with a stencilled plate
    for sx in (-1, 1):
        S.bb("gpost", sx * 1.7 - 0.1, sx * 1.7 + 0.1, yw - 1.05, yw - 0.85, 0.14, 3.3, "steel", 0.02)
    S.bb("gbeam", -1.8, 1.8, yw - 1.05, yw - 0.85, 3.2, 3.45, "yellow", 0.03)
    S.bb("gplate", -0.5, 0.5, yw - 1.08, yw - 1.04, 3.22, 3.43, "dark", 0.0)
    for k in range(4):
        S.bb("gnum", -0.38 + k * 0.22, -0.26 + k * 0.22, yw - 1.1, yw - 1.07, 3.25, 3.4, "white", 0.0, nocast=True)

    # ------------------------------------------------------------------ roof detail: house marking, tube rack, hatches
    zr = WALL_H + ROOF_T
    px1 = 2.3 if dmg else 3.3        # damaged: the corner of the roof (and half the plaque) is gone
    S.bb("roofhouse", 1.1, px1, -2.1, -1.2, zr, zr + 0.04, "house2", 0.0)    # house2 = darker house paint -> mid ramp shades
    S.bb("roofframe", 1.0, px1 + 0.1, -2.2, -1.1, zr, zr + 0.02, "dark", 0.0)
    S.bb("hatch", -3.8, -2.4, -2.1, -0.9, zr, zr + 0.18, "concrete2", 0.04)
    # roof duct block with grille slots, between the hatch and the house marking
    S.bb("duct", -2.0, -0.2, -2.1, -1.0, zr, zr + 0.42, "concrete2", 0.04)
    for k in range(5):
        S.bb(f"grille{k}", -1.85 + k * 0.33, -1.67 + k * 0.33, -2.12, -1.0, zr + 0.42, zr + 0.5, "dark", 0.0)
    S.bb("hatchd", -3.65, -2.55, -1.95, -1.05, zr + 0.18, zr + 0.22, "steel", 0.02)
    S.bb("rack", 0.3, 2.5, -0.5, 0.3, zr, zr + 0.25, "steel", 0.03)
    for k in range(6):
        x = 0.55 + k * 0.36
        S.cyl(f"tube{k}", 0.15, 0.7, (x, -0.1, zr + 0.6), "steel", verts=12)
        lit = (k + i_anim) % 2 == 0
        S.cyl(f"tubecap{k}", 0.115, 0.14, (x, -0.1, zr + 1.01), "lamp" if (lit and not dmg) or (dmg and k in (1, 4)) else "dark", verts=10)

    # ------------------------------------------------------------------ centrifuge drum (rotates: plates, ribs, spokes)
    dx, dy = DRUM
    zb = zr
    S.cyl("plinthD", DRUM_R + 0.25, 0.45, (dx, dy, zb + 0.22), "concrete2", verts=32)
    S.cyl("drum", DRUM_R, 2.25, (dx, dy, zb + 0.45 + 1.12), "hull", verts=32)
    for k, zz in enumerate((zb + 0.83, zb + 1.63)):
        S.cyl(f"ring{k}", DRUM_R + 0.05, 0.1, (dx, dy, zz), "steel", verts=32)
    for k in range(4):
        ph = math.radians(th + 90 * k + 45)
        # vertical ribs (steel) and house plates between them, both ride on the drum
        S.box(f"rib{k}", (0.16, 0.12, 1.5), (dx, dy, zb + 1.23), "steel", 0.02, wo=(math.cos(ph) * (DRUM_R + 0.04), math.sin(ph) * (DRUM_R + 0.04)),
              rot=(0, 0, math.degrees(ph) - 90), stretch=False)
        ph2 = math.radians(th + 90 * k)
        if not (dmg and k == 1):
            S.box(f"plate{k}", (1.0, 0.07, 0.62), (dx, dy, zb + 2.03), "house", 0.0, wo=(math.cos(ph2) * (DRUM_R + 0.03), math.sin(ph2) * (DRUM_R + 0.03)),
                  rot=(0, 0, math.degrees(ph2) - 90), stretch=False)
        S.box(f"bolt{k}", (0.5, 0.08, 0.16), (dx, dy, zb + 0.53), "rust", 0.0, wo=(math.cos(ph2) * (DRUM_R + 0.02), math.sin(ph2) * (DRUM_R + 0.02)),
              rot=(0, 0, math.degrees(ph2) - 90), stretch=False)
    S.cyl("drumtop", DRUM_R + 0.08, 0.14, (dx, dy, zb + 2.5), "hull2", verts=32)
    S.cyl("drumcap", 1.0, 0.1, (dx, dy, zb + 2.62), "concrete2", verts=32)
    for k in range(2):
        S.box(f"spoke{k}", (1.9, 0.18, 0.1), (dx, dy, zb + 2.74), "steel", 0.02, rot=(0, 0, th + 90 * k), stretch=False)
    S.cyl("hub", 0.3, 0.2, (dx, dy, zb + 2.8), "rust", verts=14)
    S.cyl("hubtop", 0.1, 0.2, (dx, dy, zb + 2.96), "dark", verts=8)

    # ------------------------------------------------------------------ stack with rust bands, vent cap
    sx_, sy_ = STACK
    stack_top = zr + 2.2
    if dmg:
        stack_top = zr + 1.0
    S.cyl("stack", 0.38, stack_top - zr, (sx_, sy_, (stack_top + zr) / 2), "concrete", verts=16)
    for k, zz in enumerate((zr + 0.5, zr + 1.2, zr + 1.9)):
        if zz < stack_top - 0.1:
            S.cyl(f"sband{k}", 0.42, 0.14, (sx_, sy_, zz), "rust", verts=16)
    if not dmg:
        S.cyl("scollar", 0.5, 0.16, (sx_, sy_, stack_top + 0.05), "steel", verts=16)
        S.cyl("sdark", 0.3, 0.08, (sx_, sy_, stack_top + 0.15), "dark", verts=12)
    else:
        S.box("sjag1", (0.3, 0.3, 0.3), (sx_ - 0.2, sy_, stack_top + 0.08), "concrete2", 0.0, rot=(0, 20, 10), stretch=False)
        S.cyl("sdarkb", 0.3, 0.06, (sx_, sy_, stack_top + 0.02), "dark", verts=12)
        # the upper part broke off and leans east, still hanging on the stump
        tl = math.radians(-38)
        L = 1.35
        for nm, rr, hh, along, m_ in (("stacklean", 0.36, L, L / 2, "concrete"), ("sbandL", 0.4, 0.14, 0.45, "rust")):
            o = S.cyl(nm, rr, hh, (0, 0, 0), m_, verts=16)
            o.location = (sx_ - 0.22 + along * math.sin(tl), sy_ * S.YS, stack_top + 0.06 + along * math.cos(tl))
            o.rotation_euler = (0, tl, 0)
    # ladder rungs on the stack
    S.bb("ladder", sx_ - 0.04 - 0.3, sx_ + 0.04 - 0.3, sy_ - 0.5, sy_ - 0.42, zr, stack_top - 0.2, "dark", 0.0)

    # ------------------------------------------------------------------ pipes
    zp = zr + 1.4
    S.cyl("pipeH", 0.17, 3.9, (1.4, dy, zp), "steel", axis="X", verts=12)
    for k, xx in enumerate((-0.2, 1.4, 2.8)):
        S.cyl(f"fl{k}", 0.24, 0.1, (xx, dy, zp), "rust", axis="X", verts=12)
    S.cyl("pipeV", 0.17, zp - zr, (STACK[0] - 0.1, dy, (zp + zr) / 2), "steel", verts=12)
    # side tank and riser on the pad strips
    if dmg:
        S.cyl("tankE", 0.6, 4.2, (4.62, 0.3, 0.85), "steel", axis="Y", verts=18)
    else:
        S.cyl("tankE", 0.6, 4.6, (4.62, 0.3, 0.85), "steel", axis="Y", verts=18)
    for k, yy in enumerate((-1.0, 0.3, 1.6)):
        S.cyl(f"tband{k}", 0.66, 0.12, (4.62, yy, 0.85), "rust", axis="Y", verts=18)
    S.bb("tsaddle", 4.35, 4.9, -1.3, 1.9, 0.14, 0.3, "concrete2", 0.02)
    S.cyl("riser", 0.13, 3.8, (-4.5, dy, 2.05), "steel", verts=10)
    S.cyl("valve", 0.24, 0.08, (-4.5, dy - 0.2, 1.1), "rust", axis="X", verts=10)
    # drum feed pipe down the hall's west side and across the roof
    S.cyl("feed", 0.14, 1.1, (-3.95, dy, zr + 0.14), "steel", axis="X", verts=10)

    # ------------------------------------------------------------------ damage: breaches, fire, smoke
    if dmg:
        damaged_wall(S, yw)
        # gash in the drum
        S.box("gash", (0.9, 0.1, 0.5), (dx, dy, zb + 1.2), "char", 0.0, wo=(-0.3, -DRUM_R * 0.98), stretch=False, rot=(0, 0, 0))
        S.bb("rubble1", -0.9, -0.3, -4.6, -4.1, 0.14, 0.4, "concrete2", 0.04)
        damage_fx(S, i, yw, zr, stack_top)
    else:
        if not building or i == 7:
            for k in range(2):
                ph = ((i_anim / 4) + k / 2) % 1
                r = 0.06 + 0.34 * math.sin(math.pi * ph) ** 0.7
                S.sphere(f"puff{k}", r, (sx_ + ph * 0.5, sy_, stack_top + 0.35 + ph * 1.4 + r * 0.3), "smoke", squash=0.85)

    # ------------------------------------------------------------------ stage 6: scaffold coming down, leftovers on the pad
    if building and i == 6:
        scaffold(S, 2.6, 3.9, -3.5, -2.8, (0.8, 1.7))
        S.bb("planks", -3.9, -2.0, -3.9, -3.5, 0.14, 0.3, "timber", 0.0)
        S.bb("polepile", -4.5, -1.5, -4.3, -4.15, 0.14, 0.3, "timber", 0.0)
        S.cyl("lpipe", 0.14, 2.6, (2.2, -4.2, 0.3), "steel", axis="X", verts=10)
        S.bb("crate", 3.7, 4.5, -4.5, -3.8, 0.14, 0.8, "crate", 0.03)
        spark(S, -1.5, -2.7, 3.3, 3, 6)
