"""Directorate props and atmosphere helpers for the act-break stills (M8-14).  Sits on top of still_kit.py (not edited).

Look: bone-white concrete, oxide-red signal paint, brass speaker horns, grey-brown conscript greatcoats with red armbands,
ash everywhere.  Everything here is built from the kit's box/cyl/sphere primitives so the 'mix' materials and outlines apply.
Units: metres, +Y north, yaw 0 = north, positive yaw turns clockwise (same as still_kit).
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

M = K.mat
box, cyl, sphere = K.box, K.cyl, K.sphere

D = {
    "bone": "#CFC8B4", "bone_d": "#8F8978", "bone_l": "#E6E0CC",
    "red": "#B03A2A", "red_d": "#5E1F18", "red_l": "#D9664C",
    "brass": "#C9A640", "brass_d": "#8A6D26",
    "coat": "#5B5448", "coat_d": "#3B362E", "coat_l": "#7A7262",
    "iron": "#3A3D40", "iron_d": "#1E2022", "iron_l": "#6A7075",
    "ash": "#6B6760",
}


def ceilbox(name, loc, size, col, rot=(0, 0, 0), rough=0.85):
    return box(name, loc, size, M(D.get(col, col), rough=rough), rot=rot)


def glow(col, strength):
    return M("brass", col, emit=col, estr=strength)


# ------------------------------------------------------------------------------------------------ materials
def ash_ground_mat(dark="#3A3834", mid="#5E5A54", light="#8A857B", crust="#2A2826", scale=0.8):
    """Wind-rippled ash plain: broad light/dark drifts, fine speckle, ripple lines, dark crust patches."""
    m, nt, out = K._new_mat("ash_ground")
    N, L = K._nodes(nt)
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    base = K._ramp(nt, K._noise(nt, pos, scale, 5).outputs["Fac"], [(0.3, dark), (0.55, mid), (0.78, light)])
    # ripples: wave texture, stretched along x
    mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (0.5, 2.2, 1.0); L(pos, mp.inputs[0])
    wv = N("ShaderNodeTexWave"); wv.inputs["Scale"].default_value = 2.2; wv.inputs["Distortion"].default_value = 6.0
    L(mp.outputs[0], wv.inputs["Vector"])
    rp = K._ramp(nt, wv.outputs["Color"], [(0.35, (0.82, 0.82, 0.82, 1)), (0.65, (1.15, 1.15, 1.15, 1))])
    mx = N("ShaderNodeMix"); mx.data_type = "RGBA"; mx.blend_type = "MULTIPLY"; mx.inputs[0].default_value = 1.0
    L(base.outputs[0], mx.inputs[6]); L(rp.outputs[0], mx.inputs[7])
    cm = K._ramp(nt, K._noise(nt, pos, 0.11, 4).outputs["Fac"], [(0.60, (0, 0, 0, 1)), (0.70, (1, 1, 1, 1))])
    c = K._blend(nt, cm.outputs[0], mx.outputs[2], K._ramp(nt, K._noise(nt, pos, 4.0, 3).outputs["Fac"], [(0.2, crust), (0.8, dark)]).outputs[0])
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.45; bp.inputs["Distance"].default_value = 0.06
    L(K._noise(nt, pos, 14.0, 4).outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.95
    L(c, b.inputs["Base Color"]); L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return m


def glass_sand_mat():
    """Fused coastal sand: pale green-grey with glassy turquoise veins and a little sheen."""
    m, nt, out = K._new_mat("glass_sand")
    N, L = K._nodes(nt)
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    base = K._ramp(nt, K._noise(nt, pos, 0.5, 5).outputs["Fac"], [(0.3, "#4E4D3E"), (0.55, "#726E58"), (0.8, "#94907A")])
    vor = N("ShaderNodeTexVoronoi"); vor.feature = "DISTANCE_TO_EDGE"; vor.inputs["Scale"].default_value = 0.9
    L(pos, vor.inputs["Vector"])
    vein = K._ramp(nt, vor.outputs["Distance"], [(0.0, (1, 1, 1, 1)), (0.06, (0, 0, 0, 1))])
    vm = K._ramp(nt, K._noise(nt, pos, 0.18, 3).outputs["Fac"], [(0.45, (0, 0, 0, 1)), (0.6, (1, 1, 1, 1))])
    vmul = N("ShaderNodeMath"); vmul.operation = "MULTIPLY"; L(vein.outputs[0], vmul.inputs[0]); L(vm.outputs[0], vmul.inputs[1])
    c = K._blend(nt, vmul.outputs[0], base.outputs[0], K._ramp(nt, K._noise(nt, pos, 2.0, 2).outputs["Fac"], [(0.0, "#2E7F7A"), (1.0, "#5FB8A8")]).outputs[0])
    dark = K._ramp(nt, K._noise(nt, pos, 0.1, 4).outputs["Fac"], [(0.62, (0, 0, 0, 1)), (0.72, (1, 1, 1, 1))])
    c = K._blend(nt, dark.outputs[0], c, K._ramp(nt, K._noise(nt, pos, 5.0, 3).outputs["Fac"], [(0.2, "#35332B"), (0.8, "#55503F")]).outputs[0])
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.4; bp.inputs["Distance"].default_value = 0.05
    L(K._noise(nt, pos, 16.0, 4).outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.6
    L(c, b.inputs["Base Color"]); L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return m


def water_mat(col="#1D4F58", rough=0.08, ripple=0.5):
    m, nt, out = K._new_mat("water")
    N, L = K._nodes(nt)
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (1.0, 0.45, 1.0); L(pos, mp.inputs[0])
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = ripple; bp.inputs["Distance"].default_value = 0.05
    L(K._noise(nt, mp.outputs[0], 0.7, 5).outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled")
    b.inputs["Base Color"].default_value = K.lin(col)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Specular IOR Level"].default_value = 0.9
    L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return m


def beam_mat(col, strength, alpha):
    """Translucent emissive shell for light shafts and searchlight beams."""
    m, nt, out = K._new_mat("beam")
    N, L = K._nodes(nt)
    em = N("ShaderNodeEmission"); em.inputs["Color"].default_value = K.lin(col); em.inputs["Strength"].default_value = strength
    tr = N("ShaderNodeBsdfTransparent")
    mx = N("ShaderNodeMixShader"); mx.inputs[0].default_value = alpha
    L(tr.outputs[0], mx.inputs[1]); L(em.outputs[0], mx.inputs[2]); L(mx.outputs[0], out.inputs["Surface"])
    m.use_backface_culling = False
    return m


def ground(mat_fn=ash_ground_mat, size=900, **kw):
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, 120, 0))
    o = bpy.context.active_object
    o.name = "ground"
    o.data.materials.append(mat_fn(**kw))
    return o


def water(z=-0.15, size=1800, loc=(0, 400), **kw):
    bpy.ops.mesh.primitive_plane_add(size=size, location=(loc[0], loc[1], z))
    o = bpy.context.active_object
    o.name = "water"
    o.data.materials.append(water_mat(**kw))
    return K.nolines(o)


def strip(p0, p1, width, mat_obj, z=0.03, name="strip"):
    a, b = Vector(p0), Vector(p1)
    d = b - a
    mid = (a + b) / 2
    o = box(name, (mid.x, mid.y, z), (d.length, width, 0.04), mat_obj, rot=(0, 0, math.atan2(d.y, d.x)))
    return o


# ------------------------------------------------------------------------------------------------ sky
def sky(stops, sun_vec, glow_col="#FFC98A", glow_k=1.0, glow_pow=14.0, clouds=None, strength=1.0, stars=0.0):
    """Parametric sky.  stops = [(elevation 0..1, hex)], clouds = dict(lo, hi, scale, low, high, gain, h0, h1)."""
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
    ramp = K._ramp(nt, el.outputs[0], stops)
    sv = Vector(sun_vec).normalized()
    dot = N("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"; dot.inputs[1].default_value = tuple(sv)
    L(nrm.outputs[0], dot.inputs[0])
    mx = N("ShaderNodeMath"); mx.operation = "MAXIMUM"; mx.inputs[1].default_value = 0.0; L(dot.outputs["Value"], mx.inputs[0])
    g1 = N("ShaderNodeMath"); g1.operation = "POWER"; g1.inputs[1].default_value = glow_pow; L(mx.outputs[0], g1.inputs[0])
    g2 = N("ShaderNodeMath"); g2.operation = "POWER"; g2.inputs[1].default_value = glow_pow * 15; L(mx.outputs[0], g2.inputs[0])
    gs = N("ShaderNodeMath"); gs.operation = "MULTIPLY_ADD"; gs.inputs[1].default_value = 1.4 * glow_k; L(g1.outputs[0], gs.inputs[0])
    L(g2.outputs[0], gs.inputs[2])
    gcol = N("ShaderNodeVectorMath"); gcol.operation = "SCALE"; gcol.inputs[0].default_value = tuple(K.lin(glow_col)[:3])
    L(gs.outputs[0], gcol.inputs[3])
    add = N("ShaderNodeMix"); add.data_type = "RGBA"; add.blend_type = "ADD"; add.inputs[0].default_value = 1.0
    L(ramp.outputs[0], add.inputs[6]); L(gcol.outputs[0], add.inputs[7])
    last = add.outputs[2]
    if clouds:
        c = clouds
        mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (1.0, 1.0, c.get("stretch", 5.5))
        mp.inputs["Location"].default_value = c.get("offset", (0, 0, 0))
        L(nrm.outputs[0], mp.inputs[0])
        nz = N("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = c.get("scale", 2.6); nz.inputs["Detail"].default_value = 5.0
        nz.inputs["Roughness"].default_value = 0.55
        L(mp.outputs[0], nz.inputs["Vector"])
        cm = N("ShaderNodeMapRange"); cm.inputs[1].default_value = c.get("lo", 0.47); cm.inputs[2].default_value = c.get("hi", 0.66); cm.clamp = True
        L(nz.outputs["Fac"], cm.inputs[0])
        w1 = N("ShaderNodeMapRange"); w1.inputs[1].default_value = c.get("h0", 0.025); w1.inputs[2].default_value = c.get("h1", 0.11); w1.clamp = True
        L(sep.outputs["Z"], w1.inputs[0])
        w2 = N("ShaderNodeMapRange"); w2.inputs[1].default_value = c.get("top0", 0.30); w2.inputs[2].default_value = c.get("top1", 0.52)
        w2.inputs[3].default_value = 1.0; w2.inputs[4].default_value = 0.0; w2.clamp = True
        L(sep.outputs["Z"], w2.inputs[0])
        wm = N("ShaderNodeMath"); wm.operation = "MULTIPLY"; L(w1.outputs[0], wm.inputs[0]); L(w2.outputs[0], wm.inputs[1])
        cmk = N("ShaderNodeMath"); cmk.operation = "MULTIPLY"; L(cm.outputs[0], cmk.inputs[0]); L(wm.outputs[0], cmk.inputs[1])
        mk = N("ShaderNodeMath"); mk.operation = "MULTIPLY"; mk.inputs[1].default_value = c.get("opacity", 0.92); L(cmk.outputs[0], mk.inputs[0])
        cramp = K._ramp(nt, el.outputs[0], c["cols"])
        cb = N("ShaderNodeVectorMath"); cb.operation = "SCALE"; cb.inputs[3].default_value = c.get("gain", 1.9)
        L(cramp.outputs[0], cb.inputs[0])
        fin = N("ShaderNodeMix"); fin.data_type = "RGBA"
        L(mk.outputs[0], fin.inputs[0]); L(last, fin.inputs[6]); L(cb.outputs[0], fin.inputs[7])
        last = fin.outputs[2]
    if stars:
        sc = N("ShaderNodeVectorMath"); sc.operation = "SCALE"; sc.inputs[3].default_value = 70.0; L(nrm.outputs[0], sc.inputs[0])
        vor = N("ShaderNodeTexVoronoi"); vor.feature = "F1"; vor.inputs["Scale"].default_value = 1.0
        L(sc.outputs[0], vor.inputs["Vector"])
        st = N("ShaderNodeMapRange"); st.inputs[1].default_value = 0.10; st.inputs[2].default_value = 0.0; st.clamp = True
        L(vor.outputs["Distance"], st.inputs[0])
        he = N("ShaderNodeMapRange"); he.inputs[1].default_value = 0.08; he.inputs[2].default_value = 0.35; he.clamp = True
        L(sep.outputs["Z"], he.inputs[0])
        sm = N("ShaderNodeMath"); sm.operation = "MULTIPLY"; sm.inputs[1].default_value = stars; L(st.outputs[0], sm.inputs[0])
        sm2 = N("ShaderNodeMath"); sm2.operation = "MULTIPLY"; L(sm.outputs[0], sm2.inputs[0]); L(he.outputs[0], sm2.inputs[1])
        scol = N("ShaderNodeVectorMath"); scol.operation = "SCALE"; scol.inputs[0].default_value = (0.85, 0.92, 1.0); L(sm2.outputs[0], scol.inputs[3])
        sadd = N("ShaderNodeMix"); sadd.data_type = "RGBA"; sadd.blend_type = "ADD"; sadd.inputs[0].default_value = 1.0
        L(last, sadd.inputs[6]); L(scol.outputs[0], sadd.inputs[7])
        last = sadd.outputs[2]
    bg = N("ShaderNodeBackground"); bg.inputs["Strength"].default_value = strength
    out = N("ShaderNodeOutputWorld")
    L(last, bg.inputs[0]); L(bg.outputs[0], out.inputs[0])
    return w


def spot(loc, look_at, energy, colour, size_deg=12, blend=0.3, radius=0.3):
    bpy.ops.object.light_add(type="SPOT", location=loc)
    o = bpy.context.active_object
    o.data.energy = energy
    o.data.color = colour
    o.data.spot_size = math.radians(size_deg)
    o.data.spot_blend = blend
    o.data.shadow_soft_size = radius
    o.rotation_euler = (Vector(look_at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


def beam(p0, p1, r0, r1, col, strength=2.0, alpha=0.10):
    """Visible light shaft (translucent emissive cone) from p0 to p1."""
    a, b = Vector(p0), Vector(p1)
    d = b - a
    o = K.cyl("beam", tuple((a + b) / 2), r0, d.length, beam_mat(col, strength, alpha), verts=16, r2=r1)
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = d.to_track_quat("Z", "Y")
    o.visible_shadow = False
    return K.nolines(o)


def rain(rng, n=900, xr=(-26, 26), yr=(-2, 70), zr=(0.0, 16.0), slant=0.25, col="#C8D2E8", strength=1.6, alpha=0.35):
    """Rain streaks as ONE mesh of thin slanted slivers, longer and thicker with distance so they still read."""
    import bmesh
    bm = bmesh.new()
    for i in range(n):
        x, y, z = rng.uniform(*xr), rng.uniform(*yr), rng.uniform(*zr)
        k = 1.0 + max(0.0, y) / 22.0
        L_ = rng.uniform(0.7, 1.3) * k
        w = 0.012 * k
        dx, dz = slant * L_, L_
        v = [bm.verts.new((x - w, y, z)), bm.verts.new((x + w, y, z)),
             bm.verts.new((x + w + dx, y, z + dz)), bm.verts.new((x - w + dx, y, z + dz))]
        bm.faces.new(v)
    me = bpy.data.meshes.new("rain")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("rain", me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(beam_mat(col, strength, alpha))
    o.visible_shadow = False
    return K.nolines(o)


def ash_fall(rng, n=700, xr=(-30, 30), yr=(0, 90), zr=(0.2, 18.0), col="#B9B2A4", strength=0.6):
    """Drifting ash flecks: tiny flat flakes, larger with distance."""
    import bmesh
    bm = bmesh.new()
    for i in range(n):
        x, y, z = rng.uniform(*xr), rng.uniform(*yr), rng.uniform(*zr)
        s = 0.035 * (1.0 + y / 14.0) * rng.uniform(0.6, 1.4)
        a = rng.uniform(0, math.tau)
        c, sn = math.cos(a) * s, math.sin(a) * s
        v = [bm.verts.new((x - c, y - sn, z)), bm.verts.new((x + sn, y - c, z + s * 0.4)), bm.verts.new((x + c, y + sn, z))]
        bm.faces.new(v)
    me = bpy.data.meshes.new("ashfall")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("ashfall", me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(M("bone", col, emit=col, estr=strength))
    o.visible_shadow = False
    return K.nolines(o)


# ------------------------------------------------------------------------------------------------ people
def conscript(loc, yaw=0.0, kneel=False, scale=1.0, kind="rifle", mask=False):
    """Conscript in service: long grey-brown greatcoat, red armband, flat cap or gas mask, pack.  kind: rifle | ashthrower."""
    x, y = loc
    s = math.sin(yaw), math.cos(yaw)
    k = 0.65 if kneel else 1.0
    sc = scale
    box("legs", (x, y, 0.42 * k * sc), (0.34 * sc, 0.26 * sc, 0.84 * k * sc), M("coat_d", D["coat_d"]), rot=(0, 0, -yaw))
    box("coat", (x, y, (0.84 * k + 0.30) * sc), (0.52 * sc, 0.34 * sc, 0.74 * sc), M("coat", D["coat"]), rot=(0, 0, -yaw))
    box("skirt", (x, y, (0.84 * k - 0.05) * sc), (0.56 * sc, 0.38 * sc, 0.34 * sc), M("coat", D["coat"]), rot=(0, 0, -yaw))
    box("band", (x, y, (0.84 * k + 0.42) * sc), (0.55 * sc, 0.37 * sc, 0.10 * sc), M("red", D["red"]), rot=(0, 0, -yaw))
    sphere("head", (x, y, (0.84 * k + 0.80) * sc), 0.17 * sc, M("#8E7A66" if not mask else D["iron_l"]), scale=(1, 1, 0.95))
    if mask:
        cyl("filter", (x + s[0] * 0.16 * sc, y + s[1] * 0.16 * sc, (0.84 * k + 0.76) * sc), 0.07 * sc, 0.12 * sc, M("iron_d", D["iron_d"]),
            rot=(math.radians(-90), 0, -yaw), verts=8)
        for sd in (-1, 1):
            sphere("lens", (x + s[0] * 0.15 * sc + s[1] * sd * 0.07 * sc, y + s[1] * 0.15 * sc - s[0] * sd * 0.07 * sc, (0.84 * k + 0.84) * sc),
                   0.035 * sc, M("brass", "#FFC070", emit="#FFA040", estr=3.0))
    cyl("cap", (x, y, (0.84 * k + 0.93) * sc), 0.19 * sc, 0.07 * sc, M("coat_d", D["coat_d"]), verts=10)
    box("pack", (x - s[0] * 0.24 * sc, y - s[1] * 0.24 * sc, (0.84 * k + 0.34) * sc), (0.36 * sc, 0.2 * sc, 0.46 * sc), M("#3F4136"), rot=(0, 0, -yaw))
    box("boots", (x, y, 0.05 * sc), (0.36 * sc, 0.32 * sc, 0.1 * sc), M("#1B1A18"), rot=(0, 0, -yaw))
    if kind == "ashthrower":
        cyl("tank", (x - s[0] * 0.3 * sc, y - s[1] * 0.3 * sc, (0.84 * k + 0.5) * sc), 0.15 * sc, 0.7 * sc, M("red", D["red"]), verts=10)
        cyl("tank2", (x - s[0] * 0.3 * sc + 0.2 * s[1] * sc, y - s[1] * 0.3 * sc - 0.2 * s[0] * sc, (0.84 * k + 0.5) * sc), 0.13 * sc, 0.6 * sc, M("brass", D["brass_d"]), verts=10)
        cyl("nozzle", (x + s[0] * 0.6 * sc, y + s[1] * 0.6 * sc, (0.84 * k + 0.48) * sc), 0.045 * sc, 1.2 * sc, M("iron_d", D["iron_d"]),
            rot=(math.radians(-90), 0, -yaw), verts=8)
        sphere("pilot", (x + s[0] * 1.25 * sc, y + s[1] * 1.25 * sc, (0.84 * k + 0.48) * sc), 0.07 * sc, M("brass", "#FFB050", emit="#FF7A20", estr=30.0))
    else:
        box("rifle", (x + s[0] * 0.42 * sc, y + s[1] * 0.42 * sc, (0.84 * k + 0.5) * sc), (0.06 * sc, 0.95 * sc, 0.08 * sc), M("iron_d", D["iron_d"]), rot=(0, 0, -yaw))


def squad(points, yaw, rng, kneel_p=0.3, ash_p=0.2, mask_p=0.35):
    for p in points:
        kind = "ashthrower" if rng.random() < ash_p else "rifle"
        conscript(p, yaw + rng.uniform(-0.25, 0.25), kneel=rng.random() < kneel_p, kind=kind, mask=rng.random() < mask_p,
                  scale=rng.uniform(0.95, 1.08))


# ------------------------------------------------------------------------------------------------ structures
def horn(loc, yaw=0.0, tilt=0.0, r=0.38, length=0.8, col=None):
    """Brass loudspeaker horn pointing along yaw."""
    o = cyl("horn", loc, 0.09, length, M("brass", col or D["brass"], rough=0.35), rot=(math.radians(-90) + tilt, 0, -yaw), verts=10, r2=r)
    return o


def speaker_pole(loc, h=7.0, rng=None, n=3, yaw=0.0):
    """Mast with a cluster of brass horns and a small red lamp."""
    rng = rng or random.Random(3)
    x, y = loc
    cyl("pole", (x, y, h / 2), 0.1, h, M("iron", D["iron"]), verts=8)
    box("arm", (x, y, h - 0.6), (1.3, 0.1, 0.1), M("iron", D["iron"]), rot=(0, 0, -yaw))
    for i in range(n):
        a = yaw + (i - (n - 1) / 2) * 0.9 + rng.uniform(-0.1, 0.1)
        horn((x + math.sin(a) * 0.3, y + math.cos(a) * 0.3, h - 0.25 - 0.45 * (i % 2)), yaw=a, tilt=rng.uniform(-0.08, 0.12))
    sphere("lamp", (x, y, h + 0.2), 0.14, M("red", "#FF3A20", emit="#FF2A10", estr=14.0))


def hatch(loc, yaw=0.0, s=1.0):
    """Directorate surface Hatch: low bone-concrete blister over a ramp, heavy blast door with a red frame, hazard chevrons, lamp."""
    x, y = loc
    cs, sn = math.sin(yaw), math.cos(yaw)

    def P(f, l, z):
        return (x + cs * f * s + sn * l * s, y + sn * f * s - cs * l * s, z * s)

    def R():
        return (0, 0, -yaw)

    box("slab", P(0, 0, 0.15), (9.0 * s, 8.0 * s, 0.3 * s), M("bone_d", D["bone_d"]), rot=R())
    box("block", P(-0.6, 0, 1.5), (6.8 * s, 6.0 * s, 2.4 * s), M("bone", D["bone"]), rot=R())
    box("roofcap", P(-0.6, 0, 2.85), (7.2 * s, 6.4 * s, 0.3 * s), M("bone_l", D["bone_l"]), rot=R())
    box("slope", P(2.9, 0, 0.9), (2.6 * s, 6.0 * s, 1.5 * s), M("bone", D["bone"]), rot=(0, 0, -yaw))
    box("frame", P(3.35, 0, 1.55), (0.35 * s, 3.5 * s, 2.7 * s), M("red", D["red"]), rot=R())
    box("door", P(3.58, 0, 1.45), (0.25 * s, 2.8 * s, 2.3 * s), M("iron", D["iron"]), rot=R())
    for i in range(4):
        box("rib", P(3.74, -1.0 + i * 0.66, 1.45), (0.08 * s, 0.1 * s, 2.1 * s), M("iron_l", D["iron_l"]), rot=R())
    for i in range(6):
        box("chev", P(3.4, -2.2 + i * 0.88, 0.45), (0.12 * s, 0.4 * s, 0.5 * s), M("red" if i % 2 else "yellow", D["red"] if i % 2 else "#D9C060"), rot=R())
    for sd in (-1, 1):
        box("lampbox", P(3.45, sd * 2.1, 2.9), (0.3 * s, 0.4 * s, 0.4 * s), M("iron_d", D["iron_d"]), rot=R())
        sphere("lamp", P(3.62, sd * 2.1, 2.9), 0.17 * s, M("red", "#FF3A20", emit="#FF2A10", estr=18.0))
    box("stripe", P(-0.6, 3.02, 2.1), (6.3 * s, 0.06 * s, 0.45 * s), M("red", D["red"]), rot=R())
    box("stripe", P(-0.6, -3.02, 2.1), (6.3 * s, 0.06 * s, 0.45 * s), M("red", D["red"]), rot=R())
    cyl("vent", P(-2.2, 1.8, 3.4), 0.5 * s, 1.0 * s, M("iron", D["iron"]), verts=10)
    box("conduit", P(-0.6, -3.15, 1.0), (5.0 * s, 0.18 * s, 0.18 * s), M("iron_d", D["iron_d"]), rot=R())
    speaker_pole(P(-2.8, -2.4, 0)[:2], h=6.0 * s, rng=random.Random(int(x * 7 + y)), n=2, yaw=yaw)


def vent_stack(loc, h=14.0, r=1.7, glowcol="#FF6A20", glow_k=1.0, rng=None):
    """Reactor / smelter vent: tapered bone stack with red bands, brass collar, glowing mouth, heat shimmer puffs."""
    rng = rng or random.Random(5)
    x, y = loc
    cyl("stack", (x, y, h / 2), r, h, M("bone", D["bone"]), verts=14, r2=r * 0.72)
    for t in (0.18, 0.5, 0.82):
        rr = r * (1 - 0.28 * t) + 0.05
        cyl("band", (x, y, h * t), rr, h * 0.07, M("red", D["red"]), verts=14)
    cyl("collar", (x, y, h + 0.15), r * 0.84, 0.35, M("brass", D["brass_d"]), verts=14)
    cyl("mouth", (x, y, h + 0.36), r * 0.62, 0.06, glow(glowcol, 26.0 * glow_k), verts=14)
    box("base", (x, y, 0.4), (r * 3.0, r * 3.0, 0.8), M("bone_d", D["bone_d"]))
    for i in range(3):
        a = i * 2.1 + rng.uniform(0, 1)
        cyl("pipe", (x + math.cos(a) * (r + 0.9), y + math.sin(a) * (r + 0.9), h * 0.22), 0.22, h * 0.45, M("iron", D["iron"]), verts=8)
    K.point_light((x, y, h + 3.0), 9000 * glow_k * (r / 1.7) ** 2, (1.0, 0.45, 0.18), 1.5)


def headframe(loc, h=22.0, yaw=0.0):
    """Annex shaft head: concrete headframe with a winding wheel, brass speaker bank, red bands."""
    x, y = loc
    cs, sn = math.sin(yaw), math.cos(yaw)

    def P(f, l, z):
        return (x + cs * f + sn * l, y + sn * f - cs * l, z)

    box("foot", P(0, 0, 0.5), (12, 10, 1.0), M("bone_d", D["bone_d"]), rot=(0, 0, -yaw))
    for sd in (-1, 1):
        box("leg", P(-1.0, sd * 3.2, h * 0.46), (1.3, 1.3, h * 0.92), M("bone", D["bone"]), rot=(0, 0, -yaw))
        box("brace", P(-1.0, sd * 1.6, h * 0.4), (0.5, 4.2, 0.5), M("bone_d", D["bone_d"]), rot=(0, 0, math.atan2(0, 1) - yaw))
    box("head", P(-1.0, 0, h * 0.94), (2.0, 8.4, 2.2), M("bone", D["bone"]), rot=(0, 0, -yaw))
    box("band", P(-1.0, 0, h * 0.72), (1.4, 7.6, 0.8), M("red", D["red"]), rot=(0, 0, -yaw))
    box("band", P(-1.0, 0, h * 0.35), (1.4, 7.6, 0.8), M("red", D["red"]), rot=(0, 0, -yaw))
    cyl("wheel", P(-1.0, 0, h * 1.0 + 1.6), 2.6, 0.5, M("iron", D["iron"]), rot=(0, math.pi / 2, -yaw), verts=18)
    cyl("hub", P(-1.0, 0, h * 1.0 + 1.6), 0.6, 0.9, M("brass", D["brass_d"]), rot=(0, math.pi / 2, -yaw), verts=10)
    for i in range(5):
        a = i * math.tau / 5 + 0.3
        box("spoke", P(-1.0, math.cos(a) * 1.3, h + 1.6 + math.sin(a) * 1.3), (0.15, 2.5, 0.15), M("iron_l", D["iron_l"]), rot=(a, 0, -yaw))
    for i in range(4):
        horn(P(1.0, -2.4 + i * 1.6, h * 0.86), yaw=yaw, r=0.7, length=1.5)
    box("shed", P(2.4, 0, 2.2), (4.0, 7.0, 3.6), M("bone", D["bone"]), rot=(0, 0, -yaw))
    box("shedroof", P(2.4, 0, 4.15), (4.4, 7.4, 0.3), M("iron", D["iron"]), rot=(0, 0, -yaw))
    sphere("lamp", P(-0.1, 0, h * 1.0 - 0.3), 0.3, M("red", "#FF3A20", emit="#FF2A10", estr=20.0))


def crawler_speaker(loc, yaw=0.0):
    """Loudspeaker crawler: tracked bone-and-red hull with a rack of brass horns that carry the Voice into the field."""
    x, y = loc
    s = math.sin(yaw), math.cos(yaw)
    r = (s[1], -s[0])

    def P(f, l, z):
        return (x + s[0] * f + r[0] * l, y + s[1] * f + r[1] * l, z)

    for sd in (-1, 1):
        box("track", P(0, sd * 1.5, 0.5), (0.9, 5.0, 1.0), M("iron_d", D["iron_d"]), rot=(0, 0, -yaw))
        box("trackcap", P(0, sd * 1.5, 1.05), (1.0, 5.1, 0.12), M("iron", D["iron"]), rot=(0, 0, -yaw))
        for k in range(6):
            cyl("wheel", P(-2.0 + k * 0.8, sd * 1.98, 0.5), 0.38, 0.1, M("iron_l", D["iron_l"]), rot=(0, math.pi / 2, -yaw), verts=10)
    box("hull", P(0, 0, 1.3), (2.8, 4.8, 0.9), M("bone", D["bone"]), rot=(0, 0, -yaw))
    box("stripe", P(0, 1.41, 1.3), (0.05, 4.0, 0.3), M("red", D["red"]), rot=(0, 0, -yaw))
    box("stripe2", P(0, -1.41, 1.3), (0.05, 4.0, 0.3), M("red", D["red"]), rot=(0, 0, -yaw))
    box("cab", P(1.6, 0, 2.0), (2.0, 1.4, 0.9), M("red", D["red"]), rot=(0, 0, -yaw))
    box("glass", P(2.32, 0, 2.05), (1.6, 0.06, 0.4), M("brass", "#FFC070", emit="#FFA040", estr=1.5), rot=(0, 0, -yaw))
    box("rack", P(-0.8, 0, 2.2), (0.3, 2.4, 0.2), M("iron", D["iron"]), rot=(0, 0, -yaw))
    for i in range(6):
        row = i // 3
        horn(P(-0.9 + 0.2 * row, -1.0 + (i % 3) * 1.0, 2.55 + row * 0.7), yaw=yaw + 0.04 * (i - 2.5), r=0.5, length=1.1)
    cyl("mast", P(-1.8, 0.8, 3.2), 0.04, 2.6, M("iron_d", D["iron_d"]), verts=4)
    sphere("lamp", P(1.0, 0.0, 2.55), 0.12, M("red", "#FF3A20", emit="#FF2A10", estr=14.0))


# Authority things the Directorate sees on its raids (olive/steel like the Authority kit)
def auth_truck(loc, yaw=0.0, roll=0.0, burning=False, rng=None):
    x, y = loc
    s = math.sin(yaw), math.cos(yaw)
    r = (s[1], -s[0])

    def P(f, l, z):
        return (x + s[0] * f + r[0] * l, y + s[1] * f + r[1] * l, z)

    rot = (roll, 0, -yaw)
    box("bed", P(-1.2, 0, 1.55), (2.6, 4.6, 1.8), M("olive", "#4B5742"), rot=rot)
    box("tarp", P(-1.2, 0, 2.6), (2.4, 4.3, 0.5), M("#6F6A52"), rot=rot)
    box("cab", P(2.1, 0, 1.7), (2.5, 1.7, 1.7), M("olive_l", "#5E6B52"), rot=rot)
    box("glass", P(2.96, 0, 2.05), (2.0, 0.05, 0.6), M("iron_d", D["iron_d"]), rot=rot)
    box("chassis", P(0, 0, 0.75), (2.0, 6.8, 0.35), M("iron", D["iron"]), rot=rot)
    box("stripe", P(-1.2, 1.31, 1.6), (0.05, 3.8, 0.2), M("yellow", "#C9A640"), rot=rot)
    for f in (-2.0, -0.7, 2.2):
        for sd in (-1, 1):
            cyl("wheel", P(f, sd * 1.15, 0.55), 0.55, 0.35, M("#1B1A18"), rot=(0, math.pi / 2, -yaw), verts=10)
    if burning:
        K.fire((x + s[0] * -1.0, y + s[1] * -1.0), size=2.4, rng=rng or random.Random(8))


def search_mast(loc, h=14.0, yaw=0.0, lamp_col="#FFF2D0"):
    """Authority searchlight mast: four-leg lattice with a lamp housing.  Returns the lamp position."""
    x, y = loc
    for i in range(4):
        dx, dy = (1 if i & 1 else -1) * 0.8, (1 if i & 2 else -1) * 0.8
        box("leg", (x + dx * (1 - 0.0), y + dy, h / 2), (0.14, 0.14, h), M("steel", "#555B61"), rot=(0, 0, 0))
    for k in range(1, 6):
        z = h * k / 6
        for dx, dy, w, d in ((0, 0.8, 1.7, 0.07), (0, -0.8, 1.7, 0.07), (0.8, 0, 0.07, 1.7), (-0.8, 0, 0.07, 1.7)):
            box("cross", (x + dx, y + dy, z), (w, d, 0.07), M("steel_d", "#24272A"))
    box("deck", (x, y, h), (2.4, 2.4, 0.2), M("steel", "#555B61"))
    box("housing", (x, y, h + 0.9), (1.5, 1.7, 1.2), M("olive", "#4B5742"), rot=(0, 0, -yaw))
    s = math.sin(yaw), math.cos(yaw)
    lp = (x + s[0] * 0.9, y + s[1] * 0.9, h + 0.9)
    cyl("lens", lp, 0.5, 0.18, M("yellow", lamp_col, emit=lamp_col, estr=60.0), rot=(math.radians(-90), 0, -yaw), verts=12)
    return lp


def gantry(loc, h=26.0, yaw=0.0, collapsed=True):
    """Old launch gantry: steel lattice tower, swing arm, a rusted service platform; the upper arm hangs broken."""
    x, y = loc
    cs, sn = math.sin(yaw), math.cos(yaw)

    def P(f, l, z):
        return (x + cs * f + sn * l, y + sn * f - cs * l, z)

    rr = M("rust", "#85502F")
    for a in (-1, 1):
        for b in (-1, 1):
            box("leg", P(a * 1.6, b * 1.6, h / 2), (0.35, 0.35, h), M("steel", "#6A7075"), rot=(0, 0, -yaw))
    for k in range(1, 9):
        z = h * k / 9
        for a, b, w, d in ((0, 1.6, 3.4, 0.16), (0, -1.6, 3.4, 0.16), (1.6, 0, 0.16, 3.4), (-1.6, 0, 0.16, 3.4)):
            box("ring", P(a, b, z), (w, d, 0.16), M("steel_d", "#3A3F44"), rot=(0, 0, -yaw))
        zz = h * (k - 1) / 9 + h / 18
        box("diag", P(1.6, 0, zz), (0.1, 0.1, h / 8.5), M("steel_d", "#3A3F44"), rot=(0.55, 0, -yaw)) if False else None
    box("deckL", P(0, 0, h * 0.5), (4.2, 4.2, 0.25), rr, rot=(0, 0, -yaw))
    box("deckH", P(0, 0, h * 0.86), (4.2, 4.2, 0.25), rr, rot=(0, 0, -yaw))
    box("arm", P(3.6, 0, h * 0.5 + 0.2), (6.0, 0.7, 0.4), M("steel", "#6A7075"), rot=(0, 0, -yaw))
    box("arm2", P(3.2, 0.3, h * 0.86 + 0.2), (5.0, 0.6, 0.35), M("steel", "#6A7075"), rot=(0, 0.0, -yaw + (0.0)))
    if collapsed:
        box("armbroken", P(5.2, 0.8, h * 0.86 - 2.0), (4.6, 0.6, 0.35), M("rust", "#85502F"), rot=(0, 0.5, -yaw))
    cyl("ladder", P(1.9, 0, h / 2), 0.05, h, M("yellow", "#C9A640"), verts=4)
    box("cabin", P(0, 0, h + 0.8), (3.0, 3.0, 1.6), M("steel_d", "#3A3F44"), rot=(0, 0, -yaw))
    cyl("mast", P(0, 0, h + 3.0), 0.06, 3.2, M("steel_d", "#24272A"), verts=4)


def pad(loc, r=11.0, rng=None):
    """Launch pad: round concrete apron with scorched centre, painted rings, flame trench and bollards."""
    rng = rng or random.Random(12)
    x, y = loc
    cyl("pad", (x, y, 0.15), r, 0.3, M("bone_d", "#7C786C"), verts=40)
    cyl("ring", (x, y, 0.32), r * 0.78, 0.04, M("yellow", "#B08C34"), verts=40)
    cyl("ring2", (x, y, 0.34), r * 0.74, 0.04, M("#7C786C"), verts=40)
    cyl("burn", (x, y, 0.36), r * 0.42, 0.04, M("#1E1C1A"), verts=28)
    box("trench", (x, y + 0.5, 0.2), (r * 0.35, r * 1.2, 0.5), M("#141211"))
    for i in range(10):
        a = i * math.tau / 10
        cyl("bollard", (x + math.cos(a) * r * 0.96, y + math.sin(a) * r * 0.96, 0.7), 0.18, 0.9, M("red" if i % 2 else "bone", D["red"] if i % 2 else D["bone"]), verts=8)
    for i in range(14):
        a = rng.uniform(0, math.tau)
        d = rng.uniform(0.3, 1.0) * r
        box("crack", (x + math.cos(a) * d, y + math.sin(a) * d, 0.35), (rng.uniform(1, 3), 0.08, 0.03), M("#1E1C1A"), rot=(0, 0, rng.uniform(0, 3)))


def ship(loc, yaw=0.0, s=1.0, lit=True):
    """Distant fleet hull: low grey slab, bridge block, stack, lit windows (original silhouette)."""
    x, y = loc
    cs, sn = math.sin(yaw), math.cos(yaw)

    def P(f, l, z):
        return (x + cs * f * s + sn * l * s, y + sn * f * s - cs * l * s, z * s)

    box("hull", P(0, 0, 3), (14 * s, 70 * s, 6 * s), M("#4A525A", rough=0.9), rot=(0, 0, -yaw))
    box("bow", P(0, 38, 2.6), (11 * s, 8 * s, 5 * s), M("#4A525A", rough=0.9), rot=(0, 0, -yaw))
    box("deck", P(0, -4, 6.3), (12 * s, 56 * s, 0.8 * s), M("#6A7075"), rot=(0, 0, -yaw))
    box("bridge", P(0, -14, 10), (9 * s, 14 * s, 7 * s), M("#8A9096"), rot=(0, 0, -yaw))
    box("bridge2", P(0, -14, 14.5), (7 * s, 9 * s, 3 * s), M("#A3A9AE"), rot=(0, 0, -yaw))
    cyl("stack", P(0, -24, 13.5), 2.2 * s, 7 * s, M("rust", "#85502F"), verts=10)
    for k in range(6):
        box("crane", P(0, 10 + k * 3.5, 9), (0.4 * s, 0.4 * s, 5 * s), M("#6A7075"), rot=(0, 0, -yaw))
    if lit:
        for k in range(6):
            box("win", P(4.52, -14 + k * 2.0, 10.5), (0.1 * s, 1.0 * s, 0.8 * s), M("yellow", "#FFE0A0", emit="#FFC870", estr=9.0), rot=(0, 0, -yaw))
            box("win", P(-4.52, -14 + k * 2.0, 10.5), (0.1 * s, 1.0 * s, 0.8 * s), M("yellow", "#FFE0A0", emit="#FFC870", estr=9.0), rot=(0, 0, -yaw))
        sphere("lamp", P(0, -14, 17), 0.8 * s, M("red", "#FF3A20", emit="#FF2A10", estr=40.0))


def glass_shards(rng, n=40, xr=(-30, 30), yr=(0, 60), avoid=None):
    """Spikes of fused glass jutting from the Glass Coast sand: cool teal, glossy, faintly self-lit."""
    cols = ("#2E8F86", "#4FB8A6", "#76CFC0", "#2A6F7A")
    for i in range(n):
        x, y = rng.uniform(*xr), rng.uniform(*yr)
        if avoid and avoid(x, y):
            continue
        h = rng.uniform(0.4, 1.8) * (1 + y / 90.0)
        r = h * rng.uniform(0.12, 0.3)
        c = rng.choice(cols)
        o = cyl("shard", (x, y, h / 2 - 0.05), r, h, M("brass", c, rough=0.12, emit=c, estr=0.35), verts=5, r2=0.01)
        o.rotation_euler = (rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(0, 3))


def footprints(pts, col="#2A2826"):
    """Dark trampled-ash trail along a polyline: overlapping flat patches (no outlines)."""
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        d = Vector((x1 - x0, y1 - y0, 0))
        ang = math.atan2(d.y, d.x)
        o = box("trail", ((x0 + x1) / 2, (y0 + y1) / 2, 0.025), (d.length * 1.05, 2.4, 0.012), M(col, rough=1.0), rot=(0, 0, ang))
        K.nolines(o)


def wall_segment(p0, p1, h=2.4, col="bone"):
    """Concrete barrier wall with a red top stripe."""
    a, b = Vector(p0), Vector(p1)
    d = b - a
    mid = (a + b) / 2
    ang = math.atan2(d.y, d.x)
    box("wall", (mid.x, mid.y, h / 2), (d.length, 0.5, h), M(col, D[col]), rot=(0, 0, ang))
    box("wallcap", (mid.x, mid.y, h + 0.1), (d.length, 0.7, 0.2), M("red", D["red"]), rot=(0, 0, ang))


def sandbags(p0, p1, rows=3, rng=None):
    K.sandbag_wall(p0, p1, rows=rows, rng=rng or random.Random(6))


def grey_smoke(loc, h=40, r0=2.4, n=10, tint="#4A4642", rng=None, drift=14):
    rng = rng or random.Random(5)
    x, y = loc
    for i in range(n):
        t = i / (n - 1)
        sm = M(tint, rough=1.0)
        if i < max(2, n // 3):
            sm = M("#3A2A22", rough=1.0, emit="#FF6A20", estr=0.12 * (1 - i / max(2, n // 3)))
        sphere("smoke", (x - t * t * drift + rng.uniform(-1.5, 1.5), y + rng.uniform(-1.5, 1.5), 2 + t * h), r0 * (1 + t * 2.2), sm, scale=(1, 1, 0.8))


# ------------------------------------------------------------------------------------------------ render driver
def render_still(still_id, build_fn):
    """Build + render one still in the 'mix' style (Cycles).  STILL_PREVIEW=1 gives 640x360 at 24 samples.
    Writes scratch/stills/<id>.png (preview_ prefix in preview mode); the caller copies finals to data/images/stills."""
    import time
    root = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
    out = os.path.join(root, "scratch", "stills")
    os.makedirs(out, exist_ok=True)
    preview = bool(os.environ.get("STILL_PREVIEW"))
    name = ("preview_" if preview else "") + still_id + ".png"
    raw = os.path.join(out, "raw_" + name)
    final = os.path.join(out, name)
    K.clear()
    K.STYLE = "mix"
    build_fn()
    res = (640, 360) if preview else (1280, 720)
    samples = int(os.environ.get("STILL_CYCLES_SAMPLES", "24" if preview else "96"))
    K.setup_render(raw, "mix", samples=samples, res=res)
    t = time.time()
    bpy.ops.render.render(write_still=True)
    dt = time.time() - t
    from PIL import Image
    Image.open(raw).convert("RGB").save(final)
    os.remove(raw)
    print(f"RENDER {still_id}: {dt:.0f}s -> {final}")
    return final
