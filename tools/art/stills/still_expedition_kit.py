"""Expedition props and Glass Coast / Ash Reckoning helpers for the act-break stills (M8-14, s14-stexp).

Built on still_kit (do not edit that file).  Everything here is plain bpy primitives in the "mix" style.
Conventions as in still_kit: metres, +Y north, yaw 0 faces +Y and a positive yaw turns clockwise (towards +X).
Expedition palette: deep navy, lagoon white, polished copper, signal orange for anything that lands or lifts.
"""
import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import still_kit as K  # noqa: E402

NAVY, NAVY_D, NAVY_L = "#1F3354", "#121D33", "#34507A"
WHITE, WHITE_D = "#E4EAE8", "#AEB8B8"
COPPER, COPPER_D = "#B8703E", "#7E4526"
ORANGE = "#F26A1B"
DECK = "#7F888C"


def m(hexc, rough=0.8, **kw):
    return K.mat(hexc, hexc, rough=rough, **kw)


def rot_z(yaw):
    return (0, 0, -yaw)


class Frame:
    """Local frame: f = forward (+Y when yaw=0), l = right (+X when yaw=0), z up."""

    def __init__(self, loc, yaw=0.0):
        self.x, self.y, self.z = loc if len(loc) == 3 else (loc[0], loc[1], 0.0)
        self.yaw = yaw
        self.s, self.c = math.sin(yaw), math.cos(yaw)

    def P(self, f, l, z=0.0):
        return (self.x + self.s * f + self.c * l, self.y + self.c * f - self.s * l, self.z + z)

    def box(self, name, f, l, z, size_lf_z, col, rough=0.8, extra_rot=(0, 0, 0), **kw):
        """size = (width across (l), length along (f), height)."""
        o = K.box(name, self.P(f, l, z), size_lf_z, m(col, rough, **kw), rot=(extra_rot[0], extra_rot[1], -self.yaw + extra_rot[2]))
        return o

    def cyl(self, name, f, l, z, r, h, col, verts=10, r2=None, rough=0.8, axis="z", **kw):
        rot = {"z": (0, 0, 0), "f": (math.pi / 2, 0, -self.yaw), "l": (0, math.pi / 2, -self.yaw)}[axis]
        return K.cyl(name, self.P(f, l, z), r, h, m(col, rough, **kw), rot=rot, verts=verts, r2=r2)


def poly_prism(name, pts, z0, z1, col, loc=(0, 0), yaw=0.0, taper=None, rough=0.85, bottom_shift=0.0):
    """Extruded polygon (x,y outline in local frame), optionally narrowing towards the bottom (hulls)."""
    bm = bmesh.new()
    n = len(pts)
    top = [bm.verts.new((p[0], p[1], z1)) for p in pts]
    t = 1.0 if taper is None else taper
    bot = [bm.verts.new((p[0] * t, p[1] * (1 if p[1] < 0 else 0.86) + bottom_shift, z0)) for p in pts]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((top[i], bot[i], bot[j], top[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.location = (loc[0], loc[1], 0)
    o.rotation_euler = (0, 0, -yaw)
    o.data.materials.append(m(col, rough))
    for p in o.data.polygons:
        p.use_smooth = False
    return o


def hull_outline(L, B, bow=0.55, stern_round=False):
    h = B / 2
    pts = [(0, L / 2), (h * bow, L * 0.30), (h * 0.97, L * 0.06), (h, -L * 0.3), (h, -L / 2 + 0.01),
           (-h, -L / 2 + 0.01), (-h, -L * 0.3), (-h * 0.97, L * 0.06), (-h * bow, L * 0.30)]
    return pts


# ------------------------------------------------------------------------------------------------ sea
def sea_mat(shore_y=0.0, shallow="#4FB8AE", deep="#10304B", sheen=0.07, width=60.0, dir_y=1.0):
    """Glossy sea: turquoise shallows fading to navy; broad swell bump; mirrors the sky."""
    mt, nt, out = K._new_mat("sea")
    N, L = K._nodes(nt)
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    sep = N("ShaderNodeSeparateXYZ"); L(pos, sep.inputs[0])
    d = N("ShaderNodeMapRange"); d.inputs[1].default_value = shore_y; d.inputs[2].default_value = shore_y + width * dir_y
    d.clamp = True; L(sep.outputs["Y"], d.inputs[0])
    col = K._ramp(nt, d.outputs[0], [(0.0, shallow), (0.22, "#2D8C94"), (0.55, "#1B5A74"), (1.0, deep)])
    var = K._ramp(nt, K._noise(nt, pos, 0.05, 3).outputs["Fac"], [(0.35, "#FFFFFF"), (0.7, "#C9D6DA")])
    mx = N("ShaderNodeMix"); mx.data_type = "RGBA"; mx.blend_type = "MULTIPLY"; mx.inputs[0].default_value = 1.0
    L(col.outputs[0], mx.inputs[6]); L(var.outputs[0], mx.inputs[7])
    # stretched ripples
    mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (1.0, 0.35, 1.0); L(pos, mp.inputs[0])
    rip = K._noise(nt, mp.outputs[0], 1.1, 4)
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.22; bp.inputs["Distance"].default_value = 0.35
    L(rip.outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = sheen
    b.inputs["Specular IOR Level"].default_value = 0.9
    L(mx.outputs[2], b.inputs["Base Color"]); L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return mt


def sea(y0, y1=900.0, x0=-700.0, x1=700.0, z=0.05, **kw):
    """Sea rectangle (y0..y1 north of the shore line), glossy, no outlines."""
    bpy.ops.mesh.primitive_plane_add(size=1, location=((x0 + x1) / 2, (y0 + y1) / 2, z))
    o = bpy.context.active_object
    o.name = "sea"
    o.scale = (x1 - x0, y1 - y0, 1)
    o.data.materials.append(sea_mat(shore_y=y0, **kw))
    return K.nolines(o)


def foam_line(pts, width=1.2, z=0.08, rng=None, col="#F2F6F2"):
    """Broken white surf along a polyline of (x,y)."""
    rng = rng or random.Random(3)
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        d = Vector((x1 - x0, y1 - y0, 0))
        ang = math.atan2(d.y, d.x)
        k = max(1, int(d.length / 3.0))
        for i in range(k):
            if rng.random() < 0.3:
                continue
            t = (i + rng.uniform(0.2, 0.8)) / k
            w = width * rng.uniform(0.4, 1.8)
            o = K.box("foam", (x0 + d.x * t, y0 + d.y * t + rng.uniform(-0.9, 0.9), z), (d.length / k * rng.uniform(0.3, 1.1), w, 0.02),
                      m(col, 1.0), rot=(0, 0, ang))
            K.nolines(o)


def wet_sand(pts, width=6.0, z=0.035):
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        d = Vector((x1 - x0, y1 - y0, 0))
        ang = math.atan2(d.y, d.x)
        o = K.box("wet", ((x0 + x1) / 2, (y0 + y1) / 2, z), (d.length * 1.05, width, 0.015), m("#6D5F4C", 0.35), rot=(0, 0, ang))
        K.nolines(o)


def wake(loc, yaw, length=24.0, width=5.0, z=0.09):
    """V-shaped foam wake behind a hull that moves along yaw."""
    f = Frame((loc[0], loc[1], 0), yaw)
    for side in (-1, 1):
        o = K.box("wake", f.P(-length * 0.5, side * width * 0.38, z), (0.9, length, 0.02), m("#E8F2F0", 1.0),
                  rot=(0, 0, -yaw + side * 0.17))
        K.nolines(o)
    o = K.box("wake", f.P(-length * 0.45, 0, z - 0.005), (width * 0.55, length * 0.9, 0.015), m("#BFDDDA", 1.0), rot=(0, 0, -yaw))
    K.nolines(o)


# ------------------------------------------------------------------------------------------------ ships
def _mast(f, fo, l, z, h, col=DECK):
    f.cyl("mast", fo, l, z + h / 2, 0.08, h, col, verts=5)


def destroyer(loc, yaw=0.0, s=1.0, wake_on=True):
    """Escort destroyer: navy hull, white superstructure and funnel with an orange band, foredeck gun, mast and radar."""
    f = Frame((loc[0], loc[1], 0), yaw)
    L, B, H = 34 * s, 5.6 * s, 3.4 * s
    poly_prism("dd_hull", hull_outline(L, B), -1.0 * s, H * 0.62, NAVY, loc=loc, yaw=yaw, taper=0.55)
    f.box("deck", 0, 0, H * 0.62 + 0.04, (B * 0.9, L * 0.82, 0.12 * s), DECK, 0.9)
    f.box("copper", 0, 0, H * 0.18, (B * 1.005, L * 0.55, 0.14 * s), COPPER, 0.5)
    f.box("sup", -L * 0.04, 0, H * 0.62 + 1.1 * s, (B * 0.62, L * 0.30, 2.0 * s), WHITE)
    f.box("bridge", L * 0.07, 0, H * 0.62 + 2.55 * s, (B * 0.66, L * 0.12, 1.1 * s), WHITE)
    f.box("bridgewin", L * 0.13, 0, H * 0.62 + 2.6 * s, (B * 0.6, 0.08, 0.38 * s), "#1B2430", 0.2)
    f.cyl("funnel", -L * 0.10, 0, H * 0.62 + 3.6 * s, 0.85 * s, 2.4 * s, WHITE, verts=10)
    f.cyl("band", -L * 0.10, 0, H * 0.62 + 4.5 * s, 0.9 * s, 0.35 * s, ORANGE, verts=10)
    f.cyl("cap", -L * 0.10, 0, H * 0.62 + 4.85 * s, 0.7 * s, 0.14 * s, NAVY_D, verts=10)
    _mast(f, L * 0.05, 0, H * 0.62 + 3.0 * s, 6.0 * s)
    f.box("radar", L * 0.05, 0, H * 0.62 + 8.9 * s, (2.6 * s, 0.18, 0.28 * s), DECK)
    f.box("spar", L * 0.05, 0, H * 0.62 + 7.6 * s, (3.2 * s, 0.12, 0.1), DECK)
    f.box("turret", L * 0.30, 0, H * 0.62 + 0.5 * s, (2.0 * s, 2.6 * s, 0.9 * s), NAVY_L)
    f.cyl("gun", L * 0.36, 0, H * 0.62 + 0.6 * s, 0.09 * s, 3.0 * s, DECK, verts=6, axis="f")
    f.box("aftgun", -L * 0.32, 0, H * 0.62 + 0.5 * s, (1.8 * s, 2.2 * s, 0.8 * s), NAVY_L)
    f.box("hullnum", L * 0.18, B * 0.5 + 0.01, H * 0.40, (0.04, 3.0 * s, 0.7 * s), WHITE)
    f.box("hullnum", L * 0.18, -B * 0.5 - 0.01, H * 0.40, (0.04, 3.0 * s, 0.7 * s), WHITE)
    f.box("orange", -L * 0.43, 0, H * 0.62 + 0.3 * s, (B * 0.5, 0.5, 0.5 * s), ORANGE)
    if wake_on:
        wake(loc, yaw, length=L * 0.9, width=B * 1.2)


def carrier(loc, yaw=0.0, s=1.0, wake_on=True, jets=5):
    """Carrier: long navy hull, flat pale deck with orange markings, island, parked jets and a lift."""
    f = Frame((loc[0], loc[1], 0), yaw)
    L, B, H = 88 * s, 19 * s, 6.5 * s
    poly_prism("cv_hull", hull_outline(L, B, bow=0.4), -2.0 * s, H * 0.7, NAVY, loc=loc, yaw=yaw, taper=0.6)
    f.box("copper", 0, 0, H * 0.18, (B * 1.003, L * 0.7, 0.3 * s), COPPER, 0.5)
    f.box("hullnum", L * 0.30, B * 0.5 + 0.01, H * 0.42, (0.05, 8 * s, 1.6 * s), WHITE)
    f.box("deck", -L * 0.02, 0, H * 0.7 + 0.3 * s, (B * 1.18, L * 0.96, 0.5 * s), "#8B9498", 0.9)
    f.box("spons", L * 0.36, 0, H * 0.7 + 0.1 * s, (B * 0.86, L * 0.2, 0.6 * s), "#7A8387", 0.9)
    zt = H * 0.7 + 0.6 * s
    f.box("centre", -L * 0.02, 0, zt + 0.02, (0.5 * s, L * 0.9, 0.04), ORANGE)
    f.box("edge", -L * 0.02, B * 0.52, zt + 0.02, (0.4 * s, L * 0.88, 0.04), WHITE)
    f.box("edge", -L * 0.02, -B * 0.52, zt + 0.02, (0.4 * s, L * 0.88, 0.04), WHITE)
    for k in range(7):
        f.box("dash", -L * 0.4 + k * 8 * s, -B * 0.22, zt + 0.02, (0.45 * s, 4 * s, 0.04), ORANGE)
    f.box("lift", L * 0.12, B * 0.4, zt + 0.04, (6 * s, 8 * s, 0.05), COPPER, 0.6)
    # island
    f.box("island", -L * 0.04, B * 0.52, zt + 2.8 * s, (4.2 * s, 14 * s, 5.6 * s), WHITE)
    f.box("bridge", -L * 0.02, B * 0.52, zt + 6.4 * s, (4.8 * s, 8 * s, 1.6 * s), WHITE)
    f.box("win", L * 0.0 + 4.05 * s, B * 0.52, zt + 6.5 * s, (4.4 * s, 0.1, 0.6 * s), "#1B2430", 0.2)
    f.cyl("funnel", -L * 0.1, B * 0.52, zt + 7.3 * s, 1.2 * s, 3.0 * s, WHITE, verts=10)
    f.cyl("band", -L * 0.1, B * 0.52, zt + 8.5 * s, 1.26 * s, 0.5 * s, ORANGE, verts=10)
    f.cyl("mast", -L * 0.02, B * 0.52, zt + 10.5 * s, 0.14 * s, 8 * s, DECK, verts=5)
    f.box("radar", -L * 0.02, B * 0.52, zt + 13.6 * s, (4.2 * s, 0.25, 0.4 * s), DECK)
    f.box("radome", L * 0.03, B * 0.52, zt + 8.2 * s, (2.2 * s, 2.2 * s, 1.4 * s), WHITE)
    rr = random.Random(77)
    for k in range(jets):
        fo = -L * 0.38 + k * (L * 0.62 / max(1, jets - 1)) + rr.uniform(-1, 1)
        jet((f.P(fo, -B * 0.22 + rr.uniform(-1.6, 1.0), zt)[0], f.P(fo, -B * 0.22, zt)[1]), yaw + rr.uniform(-0.1, 0.1),
            z=zt, s=0.8 * s)
    if wake_on:
        wake(loc, yaw, length=L * 0.8, width=B * 0.9)


def command_ship(loc, yaw=0.0, s=1.0, wake_on=True, bow_down=False):
    """Command Ship: broad bluff hull, tall white bridge block, cranes, cargo hatches in copper and a flagpole with the white pennant."""
    f = Frame((loc[0], loc[1], 0), yaw)
    L, B, H = 62 * s, 14 * s, 6.0 * s
    poly_prism("cmd_hull", hull_outline(L, B, bow=0.75), -2.0 * s, H * 0.68, NAVY, loc=loc, yaw=yaw, taper=0.7)
    f.box("copper", 0, 0, H * 0.2, (B * 1.003, L * 0.7, 0.28 * s), COPPER, 0.5)
    f.box("deck", -L * 0.06, 0, H * 0.68 + 0.2 * s, (B * 0.92, L * 0.78, 0.4 * s), DECK, 0.9)
    zt = H * 0.68 + 0.4 * s
    f.box("bridge", -L * 0.14, 0, zt + 4.0 * s, (B * 0.6, L * 0.16, 8 * s), WHITE)
    f.box("bridge2", -L * 0.12, 0, zt + 8.6 * s, (B * 0.7, L * 0.12, 1.8 * s), WHITE)
    f.box("win", -L * 0.12 + L * 0.06 + 0.02, 0, zt + 8.7 * s, (B * 0.62, 0.1, 0.7 * s), "#1B2430", 0.2)
    f.box("orangeband", -L * 0.14, 0, zt + 7.2 * s, (B * 0.62, L * 0.165, 0.5 * s), ORANGE)
    f.cyl("funnel", -L * 0.27, 0, zt + 7.0 * s, 1.7 * s, 6 * s, WHITE, verts=10)
    f.cyl("band", -L * 0.27, 0, zt + 9.4 * s, 1.76 * s, 0.7 * s, ORANGE, verts=10)
    f.cyl("flag", -L * 0.14, 0, zt + 15 * s, 0.1 * s, 10 * s, DECK, verts=5)
    f.box("pennant", -L * 0.14 - 1.4 * s, 0, zt + 18.6 * s, (0.04, 2.8 * s, 0.9 * s), "#F4F4EE")
    for k, fo in enumerate((L * 0.12, L * 0.28, -L * 0.34)):
        col = (COPPER, "#D9D9CE", COPPER_D)[k]
        f.box("hatch", fo, 0, zt + 0.5 * s, (B * 0.6, L * 0.1, 1.0 * s), col, 0.6)
    for fo in (L * 0.2, -L * 0.24):
        for side in (-1, 1):
            f.cyl("crane", fo, side * B * 0.32, zt + 4.5 * s, 0.2 * s, 9 * s, ORANGE, verts=6)
            f.cyl("boom", fo + 3 * s, side * B * 0.32, zt + 8.6 * s, 0.14 * s, 8 * s, ORANGE, verts=5, axis="f")
    # bow ramp (folds down when beached)
    ang = -0.55 if bow_down else 0.0
    f.box("ramp", L * 0.5 + (2.0 * s if bow_down else 0.0), 0, H * 0.4 - (1.2 * s if bow_down else 0.0),
          (B * 0.46, 7 * s, 0.35 * s), ORANGE, 0.6, extra_rot=(ang, 0, 0))
    if wake_on:
        wake(loc, yaw, length=L * 0.7, width=B * 0.9)


def tender(loc, yaw=0.0, s=1.0, wake_on=True, cargo=True):
    """Supply tender: unarmed freighter, hull navy, aft house white, derricks and stacked cargo boxes."""
    f = Frame((loc[0], loc[1], 0), yaw)
    L, B, H = 42 * s, 7.4 * s, 3.8 * s
    poly_prism("td_hull", hull_outline(L, B, bow=0.5), -1.2 * s, H * 0.7, NAVY_L, loc=loc, yaw=yaw, taper=0.6)
    f.box("copper", 0, 0, H * 0.2, (B * 1.005, L * 0.6, 0.18 * s), COPPER, 0.5)
    f.box("deck", 0, 0, H * 0.7 + 0.06, (B * 0.92, L * 0.82, 0.12 * s), DECK, 0.9)
    zt = H * 0.7 + 0.12 * s
    f.box("house", -L * 0.30, 0, zt + 1.7 * s, (B * 0.7, L * 0.16, 3.4 * s), WHITE)
    f.box("bridge", -L * 0.27, 0, zt + 3.8 * s, (B * 0.7, L * 0.08, 1.0 * s), WHITE)
    f.cyl("funnel", -L * 0.36, 0, zt + 4.0 * s, 0.8 * s, 2.4 * s, WHITE, verts=10)
    f.cyl("band", -L * 0.36, 0, zt + 4.7 * s, 0.85 * s, 0.3 * s, ORANGE, verts=10)
    rr = random.Random(int(abs(loc[0] * 13 + loc[1] * 7)) + 5)
    cols = (COPPER, ORANGE, "#D9D9CE", "#3C5C88", COPPER_D, "#8FA3B0")
    if cargo:
        for fo in (L * 0.05, L * 0.20, L * 0.34):
            for l in (-B * 0.2, B * 0.2):
                for k in range(rr.choice((1, 2))):
                    f.box("crate", fo, l, zt + 0.6 * s + k * 1.15 * s, (B * 0.32, L * 0.1, 1.1 * s), rr.choice(cols), 0.6)
    f.cyl("derrick", L * 0.15, 0, zt + 4.0 * s, 0.14 * s, 8 * s, ORANGE, verts=6)
    f.cyl("boom", L * 0.15 + 3.0 * s, 0, zt + 7.8 * s, 0.1 * s, 7 * s, ORANGE, verts=5, axis="f")
    if wake_on:
        wake(loc, yaw, length=L * 0.8, width=B * 1.2)


def landing_craft(loc, yaw=0.0, s=1.0, ramp_down=False, wake_on=False, load=True, rng=None):
    """Flat-bottomed landing craft: squared bow with an orange ramp, navy sides, small bridge aft."""
    f = Frame((loc[0], loc[1], 0), yaw)
    L, B = 12 * s, 4.2 * s
    f.box("hull", 0, 0, 0.55 * s, (B, L, 1.5 * s), NAVY_L)
    f.box("copper", 0, 0, 0.6 * s, (B * 1.01, L * 0.9, 0.12 * s), COPPER, 0.5)
    f.box("deck", 0, 0, 1.31 * s, (B * 0.86, L * 0.92, 0.08 * s), DECK, 0.9)
    f.box("bridge", -L * 0.34, 0, 2.2 * s, (B * 0.5, L * 0.18, 1.7 * s), WHITE)
    f.box("win", -L * 0.25, 0, 2.4 * s, (B * 0.44, 0.06, 0.4 * s), "#1B2430", 0.2)
    f.box("rail", 0, B * 0.43, 1.7 * s, (0.05, L * 0.9, 0.5 * s), WHITE)
    f.box("rail", 0, -B * 0.43, 1.7 * s, (0.05, L * 0.9, 0.5 * s), WHITE)
    if ramp_down:
        f.box("ramp", L * 0.5 + 1.6 * s, 0, 0.55 * s, (B * 0.82, 3.6 * s, 0.14 * s), ORANGE, 0.6, extra_rot=(-0.28, 0, 0))
    else:
        f.box("ramp", L * 0.5 + 0.1 * s, 0, 1.0 * s, (B * 0.82, 0.14 * s, 1.5 * s), ORANGE, 0.6)
    if load:
        rr = rng or random.Random(11)
        for fo in (L * 0.15, L * 0.33):
            f.box("cargo", fo, rr.uniform(-0.7, 0.7), 1.9 * s, (1.6 * s, 1.8 * s, 1.1 * s), rr.choice((COPPER, "#D9D9CE", ORANGE)), 0.6)
    if wake_on:
        wake(loc, yaw, length=L * 1.4, width=B * 1.2)


def hover_skiff(loc, yaw=0.0, s=1.0, z=0.0):
    """Hover skiff: low white hull on a dark skirt, canopy, copper stripe, orange nose and a rear fan duct."""
    f = Frame((loc[0], loc[1], z), yaw)
    f.box("skirt", 0, 0, 0.35 * s, (2.6 * s, 4.6 * s, 0.6 * s), "#1B1F26", 0.95)
    f.box("hull", 0, 0, 0.9 * s, (2.4 * s, 4.2 * s, 0.7 * s), WHITE)
    f.box("copper", 0, 0, 0.78 * s, (2.45 * s, 3.6 * s, 0.1 * s), COPPER, 0.5)
    f.box("nose", 2.0 * s, 0, 0.95 * s, (1.8 * s, 0.9 * s, 0.55 * s), ORANGE, 0.6)
    f.box("canopy", 0.4 * s, 0, 1.55 * s, (1.4 * s, 1.5 * s, 0.7 * s), "#233A55", 0.15)
    f.cyl("duct", -1.9 * s, 0, 1.45 * s, 0.75 * s, 0.35 * s, NAVY_D, verts=12, axis="f")
    f.box("fin", -2.0 * s, 0, 1.9 * s, (0.08, 0.7 * s, 1.0 * s), NAVY)


def jet(loc, yaw=0.0, z=0.0, s=1.0, bank=0.0):
    """Delta-wing jet fighter: navy fuselage and wing, orange wingtips and nose, twin fins."""
    pts = [(0, 5.0 * s), (0.5 * s, 2.0 * s), (4.0 * s, -2.8 * s), (4.0 * s, -3.4 * s), (0.6 * s, -3.0 * s),
           (-0.6 * s, -3.0 * s), (-4.0 * s, -3.4 * s), (-4.0 * s, -2.8 * s), (-0.5 * s, 2.0 * s)]
    o = poly_prism("jetwing", pts, 0.0, 0.18 * s, NAVY, loc=(loc[0], loc[1]), yaw=yaw)
    o.location.z = z + 0.7 * s
    o.rotation_euler = (bank, 0, -yaw)
    f = Frame((loc[0], loc[1], z + 0.7 * s), yaw)
    f.box("fus", 0.2 * s, 0, 0.35 * s, (0.9 * s, 7.4 * s, 0.7 * s), NAVY_L)
    f.box("tipL", -3.0 * s, 3.6 * s, 0.1 * s, (0.08, 0.5 * s, 0.14 * s), ORANGE)
    f.box("nose", 0, 0, 0.35 * s, (0.5 * s, 1.4 * s, 0.5 * s), ORANGE)
    f.box("canopy", 1.0 * s, 0, 0.82 * s, (0.5 * s, 1.5 * s, 0.35 * s), "#233A55", 0.12)
    for side in (-1, 1):
        f.box("fin", -2.4 * s, side * 0.5 * s, 1.0 * s, (0.08, 1.4 * s, 1.2 * s), NAVY)
    f.cyl("exhaust", -3.5 * s, 0, 0.35 * s, 0.3 * s, 0.4 * s, "#2B2B2B", verts=8, axis="f")


def contrail(p0, p1, n=14, r=0.45, rng=None):
    rng = rng or random.Random(8)
    for i in range(n):
        t = i / (n - 1)
        p = (p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t, p0[2] + (p1[2] - p0[2]) * t)
        o = K.sphere("trail", p, r * (0.6 + 1.6 * t), m("#F1ECEA", 1.0, emit="#FFD8B8", estr=0.35), scale=(1, 1, 0.8))
        K.nolines(o)


# ------------------------------------------------------------------------------------------------ people and structures
def xfigure(loc, yaw=0.0, kneel=False, scale=1.0, z=0.0, vest=True, hold="rifle"):
    """Expedition crewman: navy coverall, orange life vest, white cap, copper pack (no helmet)."""
    f = Frame((loc[0], loc[1], z), yaw)
    k = 0.65 if kneel else 1.0
    sc = scale
    f.box("legs", 0, 0, 0.45 * k * sc, (0.35 * sc, 0.28 * sc, 0.9 * k * sc), NAVY_D)
    f.box("torso", 0, 0, (0.9 * k + 0.33) * sc, (0.5 * sc, 0.3 * sc, 0.66 * sc), NAVY)
    if vest:
        f.box("vest", 0, 0, (0.9 * k + 0.4) * sc, (0.54 * sc, 0.34 * sc, 0.42 * sc), ORANGE, 0.6)
    f.cyl("head", 0, 0, (0.9 * k + 0.78) * sc, 0.16 * sc, 0.3 * sc, "#C99A78", verts=8)
    f.cyl("cap", 0, 0, (0.9 * k + 0.95) * sc, 0.2 * sc, 0.12 * sc, WHITE, verts=10)
    f.box("pack", -0.24 * sc, 0, (0.9 * k + 0.36) * sc, (0.38 * sc, 0.22 * sc, 0.5 * sc), COPPER_D)
    f.box("boots", 0, 0, 0.05 * sc, (0.38 * sc, 0.34 * sc, 0.1 * sc), "#16151A")
    if hold == "rifle":
        f.box("rifle", 0.45 * sc, 0, (0.9 * k + 0.5) * sc, (0.07 * sc, 1.0 * sc, 0.09 * sc), "#2A2C30")
    elif hold == "crate":
        f.box("crate", 0.35 * sc, 0, (0.9 * k + 0.35) * sc, (0.5 * sc, 0.5 * sc, 0.4 * sc), COPPER, 0.6)


def crate_x(loc, s=0.9, yaw=0.0, col=None, z=0.0):
    f = Frame((loc[0], loc[1], z), yaw)
    col = col or COPPER
    f.box("xcrate", 0, 0, s * 0.5, (s, s * 1.2, s), col, 0.6)
    f.box("strap", 0, 0, s * 0.5, (s * 1.02, s * 0.12, s * 1.02), "#2B2A2A", 0.7)
    return f


def crate_stack_x(loc, yaw=0.0, rng=None, n=5, s=1.0, z=0.0):
    rng = rng or random.Random(4)
    cols = (COPPER, "#D9D9CE", ORANGE, "#3C5C88", COPPER_D)
    for i in range(n):
        dx, dy = (i % 3) * 1.15 * s - 1.1 * s, (i // 3) * 1.3 * s
        crate_x((loc[0] + dx * math.cos(yaw) + dy * math.sin(yaw), loc[1] + dy * math.cos(yaw) - dx * math.sin(yaw)),
                s=s * rng.uniform(0.85, 1.1), yaw=yaw + rng.uniform(-0.15, 0.15), col=rng.choice(cols), z=z)
    if n > 3:
        crate_x((loc[0], loc[1]), s=s * 0.9, yaw=yaw + 0.2, col=rng.choice(cols), z=z + 1.0 * s)


def pier(loc, yaw=0.0, length=34.0, width=6.0, z=2.2, crane=True, built=1.0):
    """Anchorage: concrete-and-timber pier on piles, with a tide gauge, crane and a telex mast with a lamp.  Origin at the shore end."""
    f = Frame((loc[0], loc[1], 0), yaw)
    n = int(length * built)
    f.box("deck", n / 2, 0, z, (width, n, 0.45), "#8E8A7C", 0.9)
    f.box("edge", n / 2, width * 0.5, z + 0.3, (0.3, n, 0.2), "#C7C2B4", 0.8)
    f.box("edge", n / 2, -width * 0.5, z + 0.3, (0.3, n, 0.2), "#C7C2B4", 0.8)
    for fo in range(2, n + 1, 5):
        for side in (-1, 1):
            f.cyl("pile", fo, side * (width * 0.5 - 0.3), z / 2 - 0.4, 0.28, z + 1.0, "#3A3028", verts=7)
        f.box("brace", fo, 0, z - 0.4, (width, 0.3, 0.2), "#4A3F34", 0.9)
    for fo in range(3, n, 6):
        for side in (-1, 1):
            f.cyl("bollard", fo, side * (width * 0.5 - 0.3), z + 0.55, 0.2, 0.5, "#232426", verts=8)
    if crane:
        fo = n * 0.55
        f.box("crbase", fo, width * 0.22, z + 1.4, (1.8, 1.8, 2.4), WHITE)
        f.cyl("crmast", fo, width * 0.22, z + 5.2, 0.28, 6.0, ORANGE, verts=6)
        f.cyl("crboom", fo + 4.2, width * 0.22, z + 9.0, 0.2, 10.0, ORANGE, verts=5, axis="f", )
        f.box("hook", fo + 8.2, width * 0.22, z + 6.0, (0.1, 0.1, 5.0), "#2B2B2B")
        f.box("load", fo + 8.2, width * 0.22, z + 3.2, (1.4, 1.4, 1.2), COPPER, 0.6)
    # telex mast + tide gauge at the shore end
    f.cyl("telex", 2.5, -width * 0.3, z + 6.5, 0.14, 12.0, WHITE_D, verts=6)
    f.box("tlamp", 2.5, -width * 0.3, z + 12.6, (0.5, 0.5, 0.5), "#FFD890", 0.3, emit="#FFB050", estr=14.0)
    f.box("tarm", 2.5, -width * 0.3, z + 11.0, (2.4, 0.1, 0.1), WHITE_D)
    f.cyl("gauge", n - 1.0, width * 0.5 + 0.5, z - 0.5, 0.2, 5.0, WHITE, verts=6)
    for k in range(5):
        f.box("band", n - 1.0, width * 0.5 + 0.5, z - 2.2 + k * 0.9, (0.45, 0.45, 0.28), ORANGE if k % 2 == 0 else WHITE)
    f.cyl("flag", n - 0.5, 0, z + 6.0, 0.06, 11.0, WHITE_D, verts=5)
    f.box("pennant", n - 0.5 - 0.8, 0, z + 10.8, (0.03, 1.8, 0.6), "#F4F4EE")


def depot(loc, yaw=0.0, s=1.0):
    """Landing Depot: a long white prefab hall with an orange roof stripe, a copper door and a small antenna."""
    f = Frame((loc[0], loc[1], 0), yaw)
    f.box("hall", 0, 0, 2.2 * s, (7 * s, 12 * s, 4.4 * s), "#D9DAD0")
    f.box("roof", 0, 0, 4.6 * s, (7.6 * s, 12.6 * s, 0.3 * s), NAVY_L)
    f.box("stripe", 0, 0, 4.78 * s, (1.2 * s, 12.7 * s, 0.05), ORANGE)
    f.box("door", 6.02 * s, 0, 1.6 * s, (3 * s, 0.1, 3.2 * s), COPPER, 0.6)
    f.box("win", 0, 3.52 * s, 2.7 * s, (0.1, 8 * s, 0.7 * s), "#2A3A4E", 0.2)
    f.cyl("ant", -4 * s, 2 * s, 7 * s, 0.07 * s, 5 * s, DECK, verts=5)
    f.box("annex", -3 * s, -4.3 * s, 1.4 * s, (3 * s, 5 * s, 2.8 * s), "#C2C4B8")


def tent(loc, yaw=0.0, s=1.0, col="#CFCBB8"):
    f = Frame((loc[0], loc[1], 0), yaw)
    poly_prism("tent", [(-1.6 * s, -2.2 * s), (1.6 * s, -2.2 * s), (0.0, -2.2 * s)], 0, 0.1, col, loc=loc, yaw=yaw)
    f.box("tbody", 0, 0, 0.9 * s, (3.2 * s, 4.4 * s, 1.8 * s), col, 0.95)
    f.box("tridge", 0, 0, 2.0 * s, (0.9 * s, 4.4 * s, 0.7 * s), col, 0.95, extra_rot=(0, 0, 0))
    f.box("tflap", 2.25 * s, 0, 0.8 * s, (1.2 * s, 0.06, 1.4 * s), "#2A2F36", 0.9)


def sam_mount(loc, yaw=0.0, s=1.0, elev=0.9, z=0.0):
    """SAM Mount: sandbagged pad, white turntable, twin angled launch rails with orange-tipped missiles, radar drum."""
    f = Frame((loc[0], loc[1], z), yaw)
    f.cyl("pad", 0, 0, 0.15, 3.0 * s, 0.3, "#8E8A7C", verts=12)
    f.cyl("table", 0, 0, 0.8 * s, 1.5 * s, 1.0 * s, WHITE, verts=10)
    f.cyl("ring", 0, 0, 1.35 * s, 1.55 * s, 0.14 * s, ORANGE, verts=10)
    f.box("cab", -0.5 * s, 0, 1.9 * s, (1.6 * s, 1.6 * s, 0.9 * s), NAVY_L)
    for side in (-1, 1):
        # rail tilted up by elev rad about the l axis
        f.box("rail", 1.4 * s, side * 0.85 * s, 2.4 * s, (0.3 * s, 4.2 * s, 0.35 * s), NAVY, extra_rot=(elev * 0.45, 0, 0))
        f.cyl("missile", 1.4 * s + 0.4 * s, side * 0.85 * s, 2.55 * s + 0.4 * s, 0.17 * s, 3.6 * s, WHITE, verts=8,
              axis="f")
        f.cyl("tip", 3.0 * s, side * 0.85 * s, 2.55 * s + 1.12 * s, 0.18 * s, 0.5 * s, ORANGE, verts=8, r2=0.04, axis="f")
    f.cyl("rad", -1.6 * s, 0.9 * s, 2.5 * s, 0.7 * s, 0.25 * s, DECK, verts=10, axis="f")
    f.cyl("radmast", -1.6 * s, 0.9 * s, 1.6 * s, 0.08 * s, 1.6 * s, DECK, verts=5)
    for a in range(0, 360, 40):
        r = 3.9 * s
        K.box("bag", f.P(math.cos(math.radians(a)) * r, math.sin(math.radians(a)) * r, 0.3), (1.1 * s, 0.6 * s, 0.5 * s),
              m("#8A7C60", 1.0), rot=(0, 0, -(yaw + math.radians(a) + math.pi / 2)))


def flight_deck(loc, yaw=0.0, s=1.0, z=0.0, with_jet=True):
    """Flight Deck: raised pad, big orange ring-and-H marking, a small hangar, windsock mast, control cab and a parked jet."""
    f = Frame((loc[0], loc[1], z), yaw)
    f.box("pad", 0, 0, 0.3, (16 * s, 20 * s, 0.6), "#7F8386", 0.95)
    f.cyl("ring", 0, 0, 0.64, 4.2 * s, 0.04, ORANGE, verts=24)
    f.cyl("inner", 0, 0, 0.66, 3.7 * s, 0.04, "#7F8386", verts=24)
    f.box("H1", -1.2 * s, 0, 0.7, (0.45 * s, 3.2 * s, 0.04), WHITE)
    f.box("H2", 1.2 * s, 0, 0.7, (0.45 * s, 3.2 * s, 0.04), WHITE)
    f.box("Hbar", 0, 0, 0.7, (2.4 * s, 0.45 * s, 0.04), WHITE)
    for fo in (-9.0 * s, 9.0 * s):
        for l in (-7 * s, 7 * s):
            f.box("lamp", fo, l, 0.8, (0.3, 0.3, 0.4), "#FFD890", 0.3, emit="#FFB050", estr=10.0)
    f.box("hangar", -4 * s, -10.5 * s, 2.6 * s, (11 * s, 9 * s, 4.4 * s), "#D9DAD0")
    f.cyl("arch", -4 * s, -10.5 * s, 4.8 * s, 5.5 * s, 9.2 * s, NAVY_L, verts=3, axis="f")
    f.box("hdoor", 0.6 * s, -10.5 * s, 1.6 * s, (6 * s, 0.1, 3.0 * s), COPPER, 0.6)
    f.cyl("sockmast", 7 * s, 8.5 * s, 3.5 * s, 0.1, 6.0 * s, WHITE_D, verts=5)
    f.box("sock", 7 * s + 0.0, 8.5 * s + 0.9 * s, 6.4 * s, (1.5 * s, 0.4, 0.4), ORANGE, 0.7)
    f.box("tower", 4 * s, 9.5 * s, 2.4 * s, (2.6 * s, 2.6 * s, 3.6 * s), WHITE)
    f.box("towertop", 4 * s, 9.5 * s, 4.7 * s, (3.4 * s, 3.4 * s, 1.0 * s), "#233A55", 0.2)
    if with_jet:
        jet((f.P(0, 0)[0] + 0, f.P(0, 0)[1]), yaw + 0.7, z=z + 0.65, s=1.1 * s)


def dredge_works(loc, yaw=0.0, s=1.0):
    """Dredge Works: stout white plant on an apron of copper plating, bucket-wheel boom over the ash, conveyor, silos, orange stacks."""
    f = Frame((loc[0], loc[1], 0), yaw)
    f.box("apron", 0, 0, 0.2, (17 * s, 20 * s, 0.4), "#6B6258", 0.95)
    f.box("plant", -2 * s, -1.0 * s, 3.0 * s, (9 * s, 9 * s, 5.6 * s), "#D9DAD0")
    f.box("plantroof", -2 * s, -1.0 * s, 6.0 * s, (9.6 * s, 9.6 * s, 0.4 * s), NAVY_L)
    f.box("copband", -2 * s, -1.0 * s, 4.2 * s, (9.1 * s, 9.1 * s, 0.5 * s), COPPER, 0.5)
    for k, l in enumerate((-6.0 * s, -3.6 * s)):
        f.cyl("silo", -4.5 * s, l, 4.4 * s, 1.5 * s, 8.4 * s, WHITE, verts=12)
        f.cyl("silocap", -4.5 * s, l, 8.8 * s, 1.55 * s, 0.4 * s, ORANGE, verts=12)
    f.cyl("stack", 2 * s, 3.4 * s, 7.8 * s, 0.7 * s, 9.0 * s, WHITE, verts=10)
    f.cyl("stackband", 2 * s, 3.4 * s, 11.6 * s, 0.76 * s, 0.7 * s, ORANGE, verts=10)
    # boom with the wheel
    f.box("boom", 7.5 * s, 0.5 * s, 4.5 * s, (1.1 * s, 15 * s, 1.0 * s), ORANGE, 0.6, extra_rot=(-0.2, 0, 0))
    f.box("boomstrut", 6.5 * s, 0.5 * s, 3.2 * s, (0.3 * s, 8 * s, 0.3 * s), NAVY, extra_rot=(-0.55, 0, 0))
    wheel = f.P(15.2 * s, 0.5 * s, 1.6 * s)
    K.cyl("wheel", wheel, 2.4 * s, 0.8 * s, m(NAVY_L, 0.6), rot=(0, math.pi / 2, -yaw), verts=14)
    for a in range(0, 360, 45):
        ar = math.radians(a)
        wx = f.P(15.2 * s + math.sin(yaw) * 0, 0.5 * s + 0, 0)
        f.box("bucket", 15.2 * s, 0.5 * s + math.cos(ar) * 2.5 * s * math.cos(0), 1.6 * s + math.sin(ar) * 2.5 * s,
              (0.9 * s, 0.9 * s, 0.7 * s), COPPER, 0.5)
    # conveyor out to a heap of dredged ash
    f.box("conv", -9 * s, 5 * s, 3.3 * s, (1.2 * s, 10 * s, 0.5 * s), DECK, extra_rot=(0.0, 0, 0))
    for k in range(4):
        f.cyl("heap", -9 * s - k * 0.4, 9.5 * s + k * 0.3, 0.9 * s, 2.4 * s - k * 0.3, 1.8 * s, "#5E5A56", verts=7, r2=0.5 * s, rough=1.0)
    f.cyl("flag", 4 * s, -6 * s, 6 * s, 0.07, 9.0, WHITE_D, verts=5)
    f.box("pennant", 4 * s - 0.9, -6 * s, 10 * s, (0.03, 1.8, 0.6), "#F4F4EE")


def beached_ship_bow(loc, yaw=0.0, s=1.0):
    """The Command Ship's bow run up on the beach: hull plates open into a landing ramp, depot hall under construction behind."""
    command_ship(loc, yaw, s, wake_on=False, bow_down=True)


# ------------------------------------------------------------------------------------------------ glass coast
def glass_mat(tint="#3F8C88"):
    mt, nt, out = K._new_mat("glass_shard_" + tint)
    N, L = K._nodes(nt)
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    col = K._ramp(nt, K._noise(nt, pos, 0.4, 3).outputs["Fac"], [(0.2, "#173D45"), (0.5, tint), (0.85, "#9AD6C8")])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.06
    b.inputs["Specular IOR Level"].default_value = 1.0
    L(col.outputs[0], b.inputs["Base Color"]); L(b.outputs[0], out.inputs["Surface"])
    return mt


def glass_shards(rng, n=30, xr=(-30, 30), yr=(0, 40), h=(1.2, 4.5), avoid=None, tint="#3F8C88"):
    """Fused-glass spires left by 1962: tilted, faceted prisms in clusters, dark green to pale aqua."""
    gm = glass_mat(tint)
    k = 0
    while k < n:
        cx, cy = rng.uniform(*xr), rng.uniform(*yr)
        if avoid and avoid(cx, cy):
            continue
        k += 1
        for _ in range(rng.randint(2, 5)):
            hh = rng.uniform(*h) * (1.0 if _ == 0 else 0.55)
            x, y = cx + rng.uniform(-1.2, 1.2), cy + rng.uniform(-1.2, 1.2)
            bpy.ops.mesh.primitive_cone_add(vertices=rng.choice((4, 5, 6)), radius1=rng.uniform(0.35, 0.8), radius2=rng.uniform(0.0, 0.12),
                                            depth=hh, location=(x, y, hh * 0.45))
            o = bpy.context.active_object
            o.name = "glass"
            o.rotation_euler = (rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(0, 6.28))
            o.data.materials.append(gm)


def sand_mat(dry="#C8AE80", wet="#7A6648"):
    """Pale beach sand with ripple banding, darker damp patches and scattered pebbles."""
    mt, nt, out = K._new_mat("sand_mix")
    N, L = K._nodes(nt)
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    base = K._ramp(nt, K._noise(nt, pos, 0.5, 5).outputs["Fac"], [(0.3, "#B49A6C"), (0.55, dry), (0.8, "#DCC598")])
    mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (1.0, 6.0, 1.0); L(pos, mp.inputs[0])
    rip = K._ramp(nt, K._noise(nt, mp.outputs[0], 1.6, 2).outputs["Fac"], [(0.35, "#E8E0D0"), (0.7, "#FFFFFF")])
    mx = N("ShaderNodeMix"); mx.data_type = "RGBA"; mx.blend_type = "MULTIPLY"; mx.inputs[0].default_value = 1.0
    L(base.outputs[0], mx.inputs[6]); L(rip.outputs[0], mx.inputs[7])
    scrub = K._ramp(nt, K._noise(nt, pos, 0.08, 4).outputs["Fac"], [(0.55, (0, 0, 0, 1)), (0.66, (1, 1, 1, 1))])
    c = K._blend(nt, scrub.outputs[0], mx.outputs[2], K._ramp(nt, K._noise(nt, pos, 3.0, 3).outputs["Fac"], [(0.2, "#6C6A3A"), (0.8, "#988F58")]).outputs[0])
    vor = N("ShaderNodeTexVoronoi"); vor.inputs["Scale"].default_value = 7.0; L(pos, vor.inputs["Vector"])
    peb = K._ramp(nt, vor.outputs["Distance"], [(0.0, "#6A645A"), (0.6, "#948C7C"), (0.95, "#B9B09A")])
    pm = K._ramp(nt, K._noise(nt, pos, 0.2, 3).outputs["Fac"], [(0.6, (0, 0, 0, 1)), (0.72, (1, 1, 1, 1))])
    c = K._blend(nt, pm.outputs[0], c, peb.outputs[0])
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.3; bp.inputs["Distance"].default_value = 0.06
    L(K._noise(nt, pos, 14.0, 4).outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.95
    L(c, b.inputs["Base Color"]); L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return mt


def ash_mat():
    """Ash plain: dark grey-brown crust, pale windblown drifts, cracks, rust-red scorch."""
    mt, nt, out = K._new_mat("ash_mix")
    N, L = K._nodes(nt)
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    base = K._ramp(nt, K._noise(nt, pos, 0.6, 5).outputs["Fac"], [(0.3, "#3A3532"), (0.55, "#57504A"), (0.8, "#716860")])
    drift = K._ramp(nt, K._noise(nt, pos, 0.05, 4).outputs["Fac"], [(0.5, (0, 0, 0, 1)), (0.64, (1, 1, 1, 1))])
    c = K._blend(nt, drift.outputs[0], base.outputs[0], K._ramp(nt, K._noise(nt, pos, 4.0, 3).outputs["Fac"], [(0.2, "#8C867E"), (0.8, "#B3ACA0")]).outputs[0])
    vor = N("ShaderNodeTexVoronoi"); vor.feature = "DISTANCE_TO_EDGE"; vor.inputs["Scale"].default_value = 0.8; L(pos, vor.inputs["Vector"])
    crack = K._ramp(nt, vor.outputs["Distance"], [(0.0, (1, 1, 1, 1)), (0.04, (0, 0, 0, 1))])
    brk = K._ramp(nt, K._noise(nt, pos, 0.3, 2).outputs["Fac"], [(0.45, (0, 0, 0, 1)), (0.58, (1, 1, 1, 1))])
    cm = N("ShaderNodeMath"); cm.operation = "MULTIPLY"; L(crack.outputs[0], cm.inputs[0]); L(brk.outputs[0], cm.inputs[1])
    c = K._blend(nt, cm.outputs[0], c, K._ramp(nt, K._noise(nt, pos, 3.0, 2).outputs["Fac"], [(0.0, "#1A1210"), (1.0, "#2A1C16")]).outputs[0])
    scorch = K._ramp(nt, K._noise(nt, pos, 0.12, 4).outputs["Fac"], [(0.62, (0, 0, 0, 1)), (0.72, (1, 1, 1, 1))])
    c = K._blend(nt, scorch.outputs[0], c, K._ramp(nt, K._noise(nt, pos, 3.0, 3).outputs["Fac"], [(0.2, "#4A2A22"), (0.8, "#6E4030")]).outputs[0])
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.4; bp.inputs["Distance"].default_value = 0.08
    L(K._noise(nt, pos, 16.0, 4).outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.97
    L(c, b.inputs["Base Color"]); L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return mt


def ground_with(mt, size=1600):
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, 0, 0))
    o = bpy.context.active_object
    o.name = "ground"
    o.data.materials.append(mt)
    return o


def dunes(rng, n=12, xr=(-120, 120), yr=(20, 120), h=(2, 6), r=(14, 30), col=None):
    """Low rounded dunes (squashed cones) that break up the flat ground."""
    mt = col or m("#BFA678", 1.0)
    for _ in range(n):
        x, y = rng.uniform(*xr), rng.uniform(*yr)
        hh = rng.uniform(*h)
        o = K.cyl("dune", (x, y, hh * 0.3 - 0.3), rng.uniform(*r), hh, mt, verts=9, r2=rng.uniform(2, 6))
        o.scale = (1.0, rng.uniform(0.6, 1.2), 1.0)
        K.nolines(o)


def rock_mesa(loc, r=14.0, h=8.0, rng=None, tint="#7C6A55", top_flat=True):
    """Headland: faceted rock cone with a flat top."""
    rng = rng or random.Random(9)
    o = K.cyl("mesa", (loc[0], loc[1], h / 2 - 0.5), r, h, m(tint, 1.0), verts=11, r2=r * 0.72)
    o.scale = (1.0, rng.uniform(0.8, 1.2), 1.0)
    return o


def ash_flakes(rng, n=250, xr=(-20, 20), yr=(-4, 40), zr=(0.5, 14), size=0.05):
    """Falling ash: small pale flakes drifting in the air."""
    for _ in range(n):
        p = (rng.uniform(*xr), rng.uniform(*yr), rng.uniform(*zr))
        s = size * rng.uniform(0.6, 1.8) * (1 + 0.06 * max(0.0, p[1]))
        o = K.box("flake", p, (s * 1.6, s, s * 0.3), m("#D4CCC0", 1.0, emit="#FFB080", estr=0.6),
                  rot=(rng.uniform(0, 3), rng.uniform(0, 3), rng.uniform(0, 3)))
        K.nolines(o)


# ------------------------------------------------------------------------------------------------ custom skies
def sky_custom(sun_vec, stops, glow="#FFC98A", cloud_stops=None, strength=1.0, cloud_cov=(0.47, 0.66), cloud_scale=2.6,
               cloud_gain=1.9, glow_pow=(14.0, 220.0), glow_gain=1.4, cloud_band=(0.025, 0.11, 0.30, 0.52)):
    """Same construction as still_kit.sky_dawn with a custom gradient and cloud palette (elevation-mapped ramp,
    sun glow and horizon-stretched cloud bands).  stops: [(pos, hex)] from horizon (0) to zenith (1)."""
    w = bpy.data.worlds.new("sky")
    bpy.context.scene.world = w
    w.use_nodes = True
    nt = w.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    N, L = nt.nodes.new, nt.links.new
    tc = N("ShaderNodeTexCoord")
    nrm = N("ShaderNodeVectorMath"); nrm.operation = "NORMALIZE"
    L(tc.outputs["Object"], nrm.inputs[0])
    sep = N("ShaderNodeSeparateXYZ"); L(nrm.outputs[0], sep.inputs[0])
    el = N("ShaderNodeMapRange"); el.inputs[1].default_value = 0.0; el.inputs[2].default_value = 1.0; el.clamp = True
    L(sep.outputs["Z"], el.inputs[0])
    ramp = N("ShaderNodeValToRGB"); L(el.outputs[0], ramp.inputs[0])
    cr = ramp.color_ramp
    cr.elements[0].position, cr.elements[0].color = stops[0][0], K.lin(stops[0][1])
    cr.elements[1].position, cr.elements[1].color = stops[-1][0], K.lin(stops[-1][1])
    for pos, col in stops[1:-1]:
        e = cr.elements.new(pos); e.color = K.lin(col)
    sv = Vector(sun_vec).normalized()
    dot = N("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"; dot.inputs[1].default_value = tuple(sv)
    L(nrm.outputs[0], dot.inputs[0])
    mx = N("ShaderNodeMath"); mx.operation = "MAXIMUM"; mx.inputs[1].default_value = 0.0; L(dot.outputs["Value"], mx.inputs[0])
    g1 = N("ShaderNodeMath"); g1.operation = "POWER"; g1.inputs[1].default_value = glow_pow[0]; L(mx.outputs[0], g1.inputs[0])
    g2 = N("ShaderNodeMath"); g2.operation = "POWER"; g2.inputs[1].default_value = glow_pow[1]; L(mx.outputs[0], g2.inputs[0])
    gs = N("ShaderNodeMath"); gs.operation = "MULTIPLY_ADD"; gs.inputs[1].default_value = glow_gain; L(g1.outputs[0], gs.inputs[0])
    L(g2.outputs[0], gs.inputs[2])
    gcol = N("ShaderNodeVectorMath"); gcol.operation = "SCALE"; gcol.inputs[0].default_value = tuple(K.lin(glow)[:3])
    L(gs.outputs[0], gcol.inputs[3])
    add = N("ShaderNodeMix"); add.data_type = "RGBA"; add.blend_type = "ADD"; add.inputs[0].default_value = 1.0
    L(ramp.outputs[0], add.inputs[6]); L(gcol.outputs[0], add.inputs[7])
    mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (1.0, 1.0, 5.5)
    L(nrm.outputs[0], mp.inputs[0])
    nz = N("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = cloud_scale; nz.inputs["Detail"].default_value = 5.0
    nz.inputs["Roughness"].default_value = 0.55
    L(mp.outputs[0], nz.inputs["Vector"])
    cm = N("ShaderNodeMapRange"); cm.inputs[1].default_value = cloud_cov[0]; cm.inputs[2].default_value = cloud_cov[1]; cm.clamp = True
    L(nz.outputs["Fac"], cm.inputs[0])
    w1 = N("ShaderNodeMapRange"); w1.inputs[1].default_value = cloud_band[0]; w1.inputs[2].default_value = cloud_band[1]; w1.clamp = True
    L(sep.outputs["Z"], w1.inputs[0])
    w2 = N("ShaderNodeMapRange"); w2.inputs[1].default_value = cloud_band[2]; w2.inputs[2].default_value = cloud_band[3]
    w2.inputs[3].default_value = 1.0; w2.inputs[4].default_value = 0.0; w2.clamp = True
    L(sep.outputs["Z"], w2.inputs[0])
    wm = N("ShaderNodeMath"); wm.operation = "MULTIPLY"; L(w1.outputs[0], wm.inputs[0]); L(w2.outputs[0], wm.inputs[1])
    mk = N("ShaderNodeMath"); mk.operation = "MULTIPLY"; mk.inputs[1].default_value = 0.92
    cmk = N("ShaderNodeMath"); cmk.operation = "MULTIPLY"; L(cm.outputs[0], cmk.inputs[0]); L(wm.outputs[0], cmk.inputs[1])
    L(cmk.outputs[0], mk.inputs[0])
    cramp = N("ShaderNodeValToRGB"); L(el.outputs[0], cramp.inputs[0])
    cc = cramp.color_ramp
    cs = cloud_stops
    cc.elements[0].position, cc.elements[0].color = cs[0][0], K.lin(cs[0][1])
    cc.elements[1].position, cc.elements[1].color = cs[-1][0], K.lin(cs[-1][1])
    for pos, col in cs[1:-1]:
        e = cc.elements.new(pos); e.color = K.lin(col)
    cb = N("ShaderNodeVectorMath"); cb.operation = "SCALE"; cb.inputs[3].default_value = cloud_gain
    L(cramp.outputs[0], cb.inputs[0])
    fin = N("ShaderNodeMix"); fin.data_type = "RGBA"; fin.inputs[0].default_value = 1.0
    L(mk.outputs[0], fin.inputs[0]); L(add.outputs[2], fin.inputs[6]); L(cb.outputs[0], fin.inputs[7])
    bg = N("ShaderNodeBackground"); bg.inputs["Strength"].default_value = strength
    out = N("ShaderNodeOutputWorld")
    L(fin.outputs[2], bg.inputs[0]); L(bg.outputs[0], out.inputs[0])
    return w


def ships_far(rng, n, xr, y, scale=(0.8, 1.1), kinds=("destroyer", "tender"), wake_on=False, yaw=(-0.2, 0.2)):
    for i in range(n):
        x = xr[0] + (xr[1] - xr[0]) * (i + rng.uniform(0.1, 0.9)) / n
        k = rng.choice(kinds)
        fn = {"destroyer": destroyer, "tender": tender, "carrier": carrier, "command": command_ship}[k]
        fn((x, y + rng.uniform(-14, 14)), rng.uniform(*yaw) + math.pi / 2 * 0, rng.uniform(*scale), wake_on=wake_on)


# ------------------------------------------------------------------------------------------------ render harness
def run(build, still_id):
    """build() assembles the scene (style 'mix').  STILL_PREVIEW=1 -> 640x360 into scratch/stills/preview_<id>.png,
    otherwise 1280x720 into data/images/stills/<id>.png (optimised to stay under 2.5 MB)."""
    import time
    from PIL import Image
    root = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
    preview = bool(os.environ.get("STILL_PREVIEW"))
    outdir = os.path.join(root, "scratch", "stills") if preview else os.path.join(root, "data", "images", "stills")
    os.makedirs(outdir, exist_ok=True)
    os.makedirs(os.path.join(root, "scratch", "stills"), exist_ok=True)
    raw = os.path.join(root, "scratch", "stills", "raw_" + still_id + ".png")
    final = os.path.join(outdir, ("preview_" if preview else "") + still_id + ".png")
    K.clear()
    K.STYLE = "mix"
    build()
    res = (640, 360) if preview else (1280, 720)
    samples = int(os.environ.get("STILL_CYCLES_SAMPLES", "24" if preview else "96"))
    K.setup_render(raw, "mix", samples=samples, res=res)
    t = time.time()
    bpy.ops.render.render(write_still=True)
    dt = time.time() - t
    im = Image.open(raw).convert("RGB")
    im.save(final, optimize=True)
    os.remove(raw)
    print(f"RENDER {still_id}: {dt:.0f}s -> {final} ({os.path.getsize(final) // 1024} KB)")
