"""Authority act-break stills (M8-14): helpers shared by still_authority_act3.py and still_authority_act4.py.

Parametrised sky, terrain heightfield, shoreline/river water, sand and ash materials, Authority carrier,
Left Behind rider, Directorate picket structures, the Sunrise Device, ash flakes, and the render driver.
Built on still_kit (style "mix" only).  Import from a bpy script only.
"""
import math
import os
import random
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import still_kit as K  # noqa: E402
from mathutils import Vector  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SCRATCH = os.path.join(ROOT, "scratch", "stills")
OUTDIR = os.path.join(ROOT, "data", "images", "stills")


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


# ------------------------------------------------------------------------------------------------ sky
def sky_grad(sun_vec, stops, strength=1.2, glow="#FFC98A", glow_pow=(22.0, 220.0), glow_k=(0.9, 1.0),
             cloud_cols=("#FFB66E", "#F08A82", "#B0628E"), cloud_range=(0.47, 0.66), cloud_scale=2.6,
             cloud_win=(0.025, 0.11, 0.30, 0.52), cloud_gain=1.9, cloud_stretch=5.5, cloud_alpha=0.92):
    """Generic version of K.sky_dawn: elevation gradient + sun glow + horizontal cloud bands."""
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
    el = N("ShaderNodeMapRange"); el.clamp = True; L(sep.outputs["Z"], el.inputs[0])
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
    gs = N("ShaderNodeMath"); gs.operation = "MULTIPLY_ADD"; gs.inputs[1].default_value = glow_k[0]; L(g1.outputs[0], gs.inputs[0])
    g3 = N("ShaderNodeMath"); g3.operation = "MULTIPLY"; g3.inputs[1].default_value = glow_k[1]; L(g2.outputs[0], g3.inputs[0])
    L(g3.outputs[0], gs.inputs[2])
    gcol = N("ShaderNodeVectorMath"); gcol.operation = "SCALE"; gcol.inputs[0].default_value = tuple(K.lin(glow)[:3])
    L(gs.outputs[0], gcol.inputs[3])
    add = N("ShaderNodeMix"); add.data_type = "RGBA"; add.blend_type = "ADD"; add.inputs[0].default_value = 1.0
    L(ramp.outputs[0], add.inputs[6]); L(gcol.outputs[0], add.inputs[7])
    mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (1.0, 1.0, cloud_stretch)
    L(nrm.outputs[0], mp.inputs[0])
    nz = N("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = cloud_scale; nz.inputs["Detail"].default_value = 5.0
    nz.inputs["Roughness"].default_value = 0.55
    L(mp.outputs[0], nz.inputs["Vector"])
    cm = N("ShaderNodeMapRange"); cm.inputs[1].default_value = cloud_range[0]; cm.inputs[2].default_value = cloud_range[1]; cm.clamp = True
    L(nz.outputs["Fac"], cm.inputs[0])
    w1 = N("ShaderNodeMapRange"); w1.inputs[1].default_value = cloud_win[0]; w1.inputs[2].default_value = cloud_win[1]; w1.clamp = True
    L(sep.outputs["Z"], w1.inputs[0])
    w2 = N("ShaderNodeMapRange"); w2.inputs[1].default_value = cloud_win[2]; w2.inputs[2].default_value = cloud_win[3]
    w2.inputs[3].default_value = 1.0; w2.inputs[4].default_value = 0.0; w2.clamp = True
    L(sep.outputs["Z"], w2.inputs[0])
    wm = N("ShaderNodeMath"); wm.operation = "MULTIPLY"; L(w1.outputs[0], wm.inputs[0]); L(w2.outputs[0], wm.inputs[1])
    cmk = N("ShaderNodeMath"); cmk.operation = "MULTIPLY"; L(cm.outputs[0], cmk.inputs[0]); L(wm.outputs[0], cmk.inputs[1])
    mk = N("ShaderNodeMath"); mk.operation = "MULTIPLY"; mk.inputs[1].default_value = cloud_alpha; L(cmk.outputs[0], mk.inputs[0])
    cramp = N("ShaderNodeValToRGB"); L(el.outputs[0], cramp.inputs[0])
    cc = cramp.color_ramp
    cc.elements[0].position, cc.elements[0].color = 0.04, K.lin(cloud_cols[0])
    cc.elements[1].position, cc.elements[1].color = 0.40, K.lin(cloud_cols[2])
    e = cc.elements.new(0.16); e.color = K.lin(cloud_cols[1])
    cb = N("ShaderNodeVectorMath"); cb.operation = "SCALE"; cb.inputs[3].default_value = cloud_gain
    L(cramp.outputs[0], cb.inputs[0])
    fin = N("ShaderNodeMix"); fin.data_type = "RGBA"; fin.inputs[0].default_value = 1.0
    L(mk.outputs[0], fin.inputs[0]); L(add.outputs[2], fin.inputs[6]); L(cb.outputs[0], fin.inputs[7])
    bg = N("ShaderNodeBackground"); bg.inputs["Strength"].default_value = strength
    out = N("ShaderNodeOutputWorld")
    L(fin.outputs[2], bg.inputs[0]); L(bg.outputs[0], out.inputs[0])


# ------------------------------------------------------------------------------------------------ materials
def sand_mat():
    """Pale wind-rippled sand with turquoise glass-fused patches (Glass Coast)."""
    m, nt, out = K._new_mat("sand_glass")
    N, L = nt.nodes.new, nt.links.new
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    base = K._ramp(nt, K._noise(nt, pos, 0.5, 5).outputs["Fac"], [(0.3, "#B79F72"), (0.55, "#CDB88C"), (0.8, "#DCCBA2")])
    dark = K._ramp(nt, K._noise(nt, pos, 0.11, 3).outputs["Fac"], [(0.58, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
    c = K._blend(nt, dark.outputs[0], base.outputs[0], K._ramp(nt, K._noise(nt, pos, 3.0, 3).outputs["Fac"], [(0.2, "#8F7A58"), (0.8, "#A89268")]).outputs[0])
    glass = K._ramp(nt, K._noise(nt, pos, 0.16, 4).outputs["Fac"], [(0.68, (0, 0, 0, 1)), (0.74, (1, 1, 1, 1))])
    c = K._blend(nt, glass.outputs[0], c, K._ramp(nt, K._noise(nt, pos, 2.0, 3).outputs["Fac"], [(0.2, "#7FA89A"), (0.8, "#B4D4C4")]).outputs[0])
    wave = N("ShaderNodeTexWave"); wave.wave_type = "BANDS"; wave.inputs["Scale"].default_value = 1.6
    wave.inputs["Distortion"].default_value = 7.0; wave.inputs["Detail"].default_value = 2.0
    L(pos, wave.inputs["Vector"])
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.5; bp.inputs["Distance"].default_value = 0.07
    L(wave.outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled")
    rr = N("ShaderNodeMath"); rr.operation = "MULTIPLY_ADD"; rr.inputs[1].default_value = -0.6; rr.inputs[2].default_value = 0.9
    L(glass.outputs[0], rr.inputs[0])
    L(c, b.inputs["Base Color"]); L(rr.outputs[0], b.inputs["Roughness"]); L(bp.outputs[0], b.inputs["Normal"])
    L(b.outputs[0], out.inputs["Surface"])
    return m


def ash_mat():
    """Grey ash plain: dark burnt soil, pale ash drifts, cinder speckle (Ash Reckoning)."""
    m, nt, out = K._new_mat("ash")
    N, L = nt.nodes.new, nt.links.new
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    base = K._ramp(nt, K._noise(nt, pos, 0.4, 5).outputs["Fac"], [(0.3, "#2A2826"), (0.55, "#3E3A36"), (0.8, "#58524B")])
    drift = K._ramp(nt, K._noise(nt, pos, 0.09, 4).outputs["Fac"], [(0.56, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
    c = K._blend(nt, drift.outputs[0], base.outputs[0], K._ramp(nt, K._noise(nt, pos, 1.5, 3).outputs["Fac"], [(0.3, "#6E675E"), (0.7, "#8C857A")]).outputs[0])
    burnt = K._ramp(nt, K._noise(nt, pos, 0.18, 3).outputs["Fac"], [(0.62, (0, 0, 0, 1)), (0.74, (1, 1, 1, 1))])
    c = K._blend(nt, burnt.outputs[0], c, K._ramp(nt, K._noise(nt, pos, 4.0, 3).outputs["Fac"], [(0.2, "#141211"), (0.8, "#26231F")]).outputs[0])
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.4; bp.inputs["Distance"].default_value = 0.08
    L(K._noise(nt, pos, 16.0, 4).outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.95
    L(c, b.inputs["Base Color"]); L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return m


def water_mat(col="#1C6A76", rough=0.06, wave=0.35):
    m, nt, out = K._new_mat("water")
    N, L = nt.nodes.new, nt.links.new
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (1.0, 2.5, 1.0); L(pos, mp.inputs[0])
    nz = K._noise(nt, mp.outputs[0], 0.5, 4)
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = wave; bp.inputs["Distance"].default_value = 0.2
    L(nz.outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled")
    b.inputs["Base Color"].default_value = K.lin(col); b.inputs["Roughness"].default_value = rough
    b.inputs["Specular IOR Level"].default_value = 0.9
    L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return m


# ------------------------------------------------------------------------------------------------ terrain / water geometry
def flat_ground(mat, z=0.0, size=2400):
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, 300, z))
    o = bpy.context.active_object
    o.name = "ground"
    o.data.materials.append(mat)
    return o


def terrain(zfun, mat, xr=(-160, 160), yr=(-60, 260), step=2.5):
    """Heightfield patch (smooth shaded, no outlines); zfun must return ~0 on its border."""
    import bmesh
    nx, ny = int((xr[1] - xr[0]) / step), int((yr[1] - yr[0]) / step)
    bm = bmesh.new()
    vs = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            x, y = xr[0] + i * step, yr[0] + j * step
            vs[(i, j)] = bm.verts.new((x, y, zfun(x, y)))
    for i in range(nx):
        for j in range(ny):
            bm.faces.new((vs[(i, j)], vs[(i + 1, j)], vs[(i + 1, j + 1)], vs[(i, j + 1)]))
    me = bpy.data.meshes.new("terrain")
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    o = bpy.data.objects.new("terrain", me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat)
    return K.nolines(o)


def poly_plane(pts, z, mat, name="water"):
    import bmesh
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, z)) for x, y in pts]
    bm.faces.new(vs)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat)
    return K.nolines(o)


def foam_band(edge_pts, width, z, rng):
    """Irregular pale foam line along a shore polyline (list of (x, y))."""
    m = K.mat("#E4ECE6", rough=0.7)
    for (x0, y0), (x1, y1) in zip(edge_pts[:-1], edge_pts[1:]):
        d = Vector((x1 - x0, y1 - y0, 0))
        w = width * rng.uniform(0.5, 1.4)
        o = K.box("foam", ((x0 + x1) / 2, (y0 + y1) / 2 - w * 0.5, z), (d.length * rng.uniform(0.8, 1.05), w, 0.02), m,
                  rot=(0, 0, math.atan2(d.y, d.x)))
        K.nolines(o)


def lifted(dz, fn, *a, **k):
    """Call a prop builder and raise everything it created by dz (for props on slopes)."""
    before = set(bpy.data.objects.keys())
    r = fn(*a, **k)
    for n in set(bpy.data.objects.keys()) - before:
        bpy.data.objects[n].location.z += dz
    return r


def tufts_on(rng, zfun, n, xr, yr, avoid=None, scale=1.0, mat=None):
    import bmesh
    bm = bmesh.new()
    for i in range(n):
        x, y = rng.uniform(*xr), rng.uniform(*yr)
        if avoid and avoid(x, y):
            continue
        z0 = zfun(x, y)
        k = scale * (1.0 + max(0.0, (y + 30) / 90.0) * 1.4)
        for b in range(rng.randint(4, 8)):
            a = rng.uniform(0, math.tau)
            lean = rng.uniform(0.1, 0.55)
            h = rng.uniform(0.3, 0.8) * k
            bx, by = x + math.cos(a) * 0.07 * k, y + math.sin(a) * 0.07 * k
            px, py = -math.sin(a) * 0.03 * k, math.cos(a) * 0.03 * k
            v = [bm.verts.new((bx - px, by - py, z0)), bm.verts.new((bx + px, by + py, z0)),
                 bm.verts.new((bx + math.cos(a) * lean * h, by + math.sin(a) * lean * h, z0 + h))]
            bm.faces.new(v)
    me = bpy.data.meshes.new("tufts")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("tufts", me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat or K._mats.setdefault("grass", K.grass_mat()))
    return K.nolines(o)


# ------------------------------------------------------------------------------------------------ props
def carrier(loc, yaw=0.0):
    """Authority carrier: long armoured box hull on six wheels, canvas-ribbed cargo bay, yellow flank stripe."""
    x, y = loc
    s = math.sin(yaw), math.cos(yaw)
    r = (s[1], -s[0])

    def P(f, l, z):
        return (x + s[0] * f + r[0] * l, y + s[1] * f + r[1] * l, z)
    rz = (0, 0, -yaw)
    K.box("hull", P(0, 0, 1.15), (2.8, 8.0, 1.3), K.mat("olive"), rot=rz)
    K.box("skirt", P(0, 0, 0.62), (2.9, 7.6, 0.28), K.mat("olive_d"), rot=rz)
    K.box("cab", P(3.0, 0, 2.05), (2.6, 2.0, 1.0), K.mat("olive_l"), rot=rz)
    K.box("glass", P(4.03, 0, 2.15), (2.0, 0.06, 0.45), K.mat("steel_d"), rot=rz)
    K.box("bay", P(-1.3, 0, 2.15), (2.5, 5.0, 1.15), K.mat("sand", "#8F8466"), rot=rz)
    for k in range(5):
        K.box("rib", P(-3.4 + k * 1.05, 0, 2.75), (2.6, 0.08, 0.1), K.mat("steel_d"), rot=rz)
    for sd in (-1, 1):
        K.box("stripe", P(0, sd * 1.41, 1.2), (0.05, 6.0, 0.22), K.mat("yellow"), rot=rz)
        for k in range(3):
            fo = (-2.6, 0.2, 2.8)[k]
            K.cyl("wheel", P(fo, sd * 1.5, 0.55), 0.58, 0.4, K.mat("#1B1A18", rough=1.0), rot=(0, math.pi / 2, -yaw), verts=12)
            K.cyl("hub", P(fo, sd * 1.72, 0.55), 0.2, 0.1, K.mat("steel", "#6A7075"), rot=(0, math.pi / 2, -yaw), verts=8)
    K.cyl("lamp", P(2.6, 0.9, 2.75), 0.18, 0.3, K.mat("steel_l", "#737A80"), verts=8)
    K.cyl("antenna", P(-3.6, 1.0, 3.4), 0.02, 2.4, K.mat("steel_d"), verts=4)
    for k in range(2):
        K.box("crate", P(-3.6 + k * 0.1, -0.6 + k * 1.1, 3.0), (0.7, 0.7, 0.5), K.mat("#6B5A3C"), rot=rz)


def rider(loc, yaw=0.0, rng=None):
    """Left Behind scavenger rider on a patchwork bike with a tattered name-flag."""
    rng = rng or random.Random(5)
    x, y = loc
    s = math.sin(yaw), math.cos(yaw)
    r = (s[1], -s[0])

    def P(f, l, z):
        return (x + s[0] * f + r[0] * l, y + s[1] * f + r[1] * l, z)
    for fo in (0.75, -0.75):
        K.cyl("wheel", P(fo, 0, 0.36), 0.36, 0.12, K.mat("#1B1A18", rough=1.0), rot=(0, math.pi / 2, -yaw), verts=10)
    K.box("frame", P(0, 0, 0.62), (0.16, 1.6, 0.18), K.mat(rng.choice(["#85502F", "#4B5742", "#8A3A2A", "#6F7E62"])), rot=(0, 0, -yaw))
    K.box("tank", P(0.15, 0, 0.82), (0.3, 0.5, 0.25), K.mat("#B07445"), rot=(0, 0, -yaw))
    K.box("torso", P(-0.1, 0, 1.2), (0.42, 0.3, 0.6), K.mat(rng.choice(["#7C5A3C", "#5A5C3E", "#6B4A3A"])), rot=(0.25, 0, -yaw))
    K.sphere("head", P(0.05, 0, 1.62), 0.17, K.mat("#C9A890"))
    K.box("scarf", P(0.05, 0, 1.78), (0.4, 0.4, 0.12), K.mat("#B03A2A"), rot=(0, 0, -yaw))
    K.cyl("pole", P(-0.8, 0.2, 1.8), 0.025, 2.2, K.mat("steel_d"), verts=4)
    K.box("flag", P(-0.8, 0.2, 2.6), (0.03, 0.9, 0.5), K.mat(rng.choice(["#B03A2A", "#C9A640", "#4A6FA5"])), rot=(0.0, 0, -yaw))


def picket(loc, yaw=0.0, tall=False):
    """Directorate picket post: charcoal bunker with oxide-red band and slit, optional lookout tower."""
    x, y = loc
    K.box("bunker", (x, y, 1.1), (6.0, 4.0, 2.2), K.mat("#2E2E33"), rot=(0, 0, yaw))
    K.box("band", (x, y - 2.02, 1.7), (6.05, 0.05, 0.4), K.mat("#8A3A2A"), rot=(0, 0, yaw))
    K.box("slit", (x, y - 2.03, 1.1), (3.2, 0.05, 0.28), K.mat("#0B0B0C"), rot=(0, 0, yaw))
    K.box("roof", (x, y, 2.3), (6.4, 4.4, 0.2), K.mat("#3E3E44"), rot=(0, 0, yaw))
    if tall:
        for dx in (-0.8, 0.8):
            for dy in (-0.8, 0.8):
                K.cyl("leg", (x + dx + 3.8, y + dy, 3.4), 0.08, 6.8, K.mat("steel_d"), verts=5)
        K.box("cab", (x + 3.8, y, 7.0), (2.4, 2.4, 1.3), K.mat("#2E2E33"))
        K.box("redband", (x + 3.8, y - 1.22, 7.1), (2.45, 0.05, 0.3), K.mat("#8A3A2A"))
        K.cyl("beam", (x + 3.8, y - 1.4, 7.1), 0.25, 0.5, K.mat("yellow", "#FFE9A0", emit="#FFD890", estr=30.0), rot=(math.pi / 2, 0, 0), verts=8)


def survey_stake(loc, rng=None):
    """Chief-Surveyor style stake: slim pole, yellow flag, cairn of stones."""
    rng = rng or random.Random(3)
    x, y = loc
    K.cyl("stake", (x, y, 0.8), 0.03, 1.6, K.mat("steel_d"), rot=(rng.uniform(-.1, .1), rng.uniform(-.1, .1), 0), verts=5)
    K.box("sflag", (x + 0.2, y, 1.45), (0.4, 0.02, 0.22), K.mat("yellow"))
    for i in range(3):
        K.sphere("stone", (x + rng.uniform(-.2, .2), y + rng.uniform(-.2, .2), 0.08 + 0.1 * i), 0.18 - 0.03 * i, K.mat("#8A8478"))


def ash_flakes(rng, n=500, xr=(-16, 16), yr=(-12, 45), zr=(0.2, 11), colour="#9A948C"):
    """Falling ash: tiny tumbling flakes (bigger the farther they are so they still read)."""
    m = K.mat(colour, rough=1.0)
    for i in range(n):
        x, y, z = rng.uniform(*xr), rng.uniform(*yr), rng.uniform(*zr)
        sz = 0.03 + 0.012 * max(0.0, y + 14) ** 0.8 * rng.uniform(0.6, 1.2)
        o = K.box("ash", (x, y, z), (sz, sz * rng.uniform(.6, 1.2), sz * 0.15), m,
                  rot=(rng.uniform(0, 3), rng.uniform(0, 3), rng.uniform(0, 3)))
        K.nolines(o)


def sunrise_device(loc, rng=None):
    """The Sunrise Device: ring-walled cradle round a glowing shaft, vent stack with flame, searchlight towers."""
    rng = rng or random.Random(61)
    x, y = loc
    dark, red = K.mat("#262629"), K.mat("#8A3A2A")
    K.cyl("cradle", (x, y, 1.5), 15.0, 3.0, dark, verts=24)
    K.cyl("cradle2", (x, y, 3.3), 12.0, 1.0, K.mat("#34343A"), verts=24)
    K.cyl("redring", (x, y, 3.85), 12.2, 0.25, red, verts=24)
    K.cyl("shaft", (x, y, 3.95), 7.0, 0.1, K.mat("yellow", "#FFE0A0", emit="#FF9A40", estr=60.0), verts=24)
    K.cyl("beam", (x, y, 40), 5.0, 70, K.mat("yellow", "#FFB060", emit="#FF7A20", estr=3.0), verts=16, r2=1.2)
    for k in range(8):
        a = k / 8 * math.tau
        K.box("fin", (x + math.cos(a) * 13.5, y + math.sin(a) * 13.5, 6.0), (0.9, 0.9, 6.0), dark, rot=(0, 0, a))
        K.box("finlamp", (x + math.cos(a) * 13.5, y + math.sin(a) * 13.5, 9.2), (0.5, 0.5, 0.4),
              K.mat("yellow", "#FFC070", emit="#FF8A30", estr=40.0))
    vx, vy = x + 34, y - 8
    K.cyl("vent", (vx, vy, 13), 3.2, 26, K.mat("#34302C"), verts=12, r2=2.4)
    K.cyl("ventfire", (vx, vy, 28), 3.0, 6.0, K.mat("yellow", "#FF8A20", emit="#FF6A10", estr=40.0), verts=10, r2=0.3)
    K.point_light((x, y - 8, 12), 3.5e6, (1.0, 0.5, 0.2), 6.0)
    K.point_light((vx, vy - 6, 24), 1.2e6, (1.0, 0.42, 0.14), 4.0)
    for k in range(7):                      # perimeter wall and lamp towers
        wx = x - 70 + k * 24
        K.box("wall", (wx, y - 30, 2.5), (22, 1.2, 5.0), K.mat("#2A2A2E"))
        if k % 2 == 0:
            K.box("tower", (wx, y - 31, 8.0), (3.0, 3.0, 16.0), K.mat("#2E2E33"))
            K.box("tlamp", (wx, y - 33, 15.2), (2.2, 0.6, 0.6), K.mat("yellow", "#FFD890", emit="#FFC070", estr=60.0))
            K.box("tband", (wx, y - 32.55, 11.0), (3.05, 0.1, 0.8), K.mat("#8A3A2A"))
    for k in range(6):                      # block buildings behind the wall
        bx = x - 60 + rng.uniform(0, 120)
        K.box("blk", (bx, y + rng.uniform(5, 35), 4 + rng.uniform(0, 6)), (rng.uniform(8, 16), rng.uniform(8, 14), 8 + rng.uniform(0, 12)), K.mat("#26262B"))


def run(still_id, build, cam_res=(1280, 720)):
    """Render driver: build(K) -> None; writes data/images/stills/<id>.png (and a scratch copy). STILL_PREVIEW=1 for 640x360."""
    preview = bool(os.environ.get("STILL_PREVIEW"))
    os.makedirs(SCRATCH, exist_ok=True)
    os.makedirs(OUTDIR, exist_ok=True)
    K.clear()
    K.STYLE = "mix"
    build()
    res = (640, 360) if preview else cam_res
    samples = int(os.environ.get("STILL_CYCLES_SAMPLES", "24" if preview else "96"))
    raw = os.path.join(SCRATCH, "raw_" + still_id + ".png")
    K.setup_render(raw, "mix", samples=samples, res=res)
    t = time.time()
    bpy.ops.render.render(write_still=True)
    dt = time.time() - t
    from PIL import Image
    im = K.finish_mix(Image.open(raw).convert("RGB"))
    os.remove(raw)
    if preview:
        path = os.path.join(SCRATCH, "preview_" + still_id + ".png")
        im.save(path)
    else:
        path = os.path.join(OUTDIR, still_id + ".png")
        im.save(path, optimize=True)
        if os.path.getsize(path) > 2_400_000:
            im.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG).save(path, optimize=True)
        im.save(os.path.join(SCRATCH, still_id + ".png"))
    print(f"RENDER {still_id}: {dt:.0f}s -> {path} ({os.path.getsize(path) // 1024} KB)")
    return path
