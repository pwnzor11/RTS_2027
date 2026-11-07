"""Scene kit for the act-break stills (M8-14): primitives-built low-poly props, house palette, two render styles.

Style "game": Principled materials, warm low sun, clean render (matches the sprite kit's grey-green/steel/rust palette).
Style "poster": ShaderToRGB banded (toon) materials + Freestyle outlines, limited palette.
Style "mix": game materials and lighting (as "game") plus the poster's Freestyle outlines at about half thickness.
Import from a bpy script only.  Units: metres; +Y is north, +X east, camera looks roughly north-west.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import common as C  # noqa: E402  (house palette; sprite kit)

STYLE = "game"
NOLINES = []   # objects kept out of the Freestyle outlines (decals, haze, wires, grass, far ridges)


def nolines(o):
    NOLINES.append(o)
    return o


def hexs(*names):
    return names


# Colours taken from common.PAL (authority) so stills match the sprites.
P = {
    "olive": "#4B5742", "olive_d": "#2C3326", "olive_l": "#6F7E62", "sage": "#859577",
    "steel": "#555B61", "steel_d": "#24272A", "steel_l": "#979EA3",
    "rust": "#85502F", "rust_d": "#5E3420", "rust_l": "#B07445",
    "yellow": "#C9A640", "bone": "#C9CFC0", "dust": "#8A8478", "sand": "#8F7150",
    "asphalt": "#2C3326", "outline": "#0D0F0B",
}


def lin(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1.0]


_mats = {}


def mat(key, hexc=None, rough=0.85, emit=None, estr=3.0):
    """Material for the current STYLE.  key may be a P[] name or a hex colour."""
    hexc = hexc or P.get(key, key)
    k = (STYLE, hexc, rough, emit, estr)
    if k in _mats:
        return _mats[k]
    m = bpy.data.materials.new(f"m_{len(_mats)}")
    m.use_nodes = True
    nt = m.node_tree
    if emit:
        b = nt.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = lin(hexc)
        b.inputs["Emission Color"].default_value = lin(emit)
        b.inputs["Emission Strength"].default_value = estr
    elif STYLE == "mix":
        nt.nodes.remove(nt.nodes["Principled BSDF"])
        _mix_surface(nt, hexc, rough)
    elif STYLE == "poster":
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
        s2r = nt.nodes.new("ShaderNodeShaderToRGB")
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1.0
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(diff.outputs[0], s2r.inputs[0])
        # light -> 3 hard bands (shadow / mid / lit)
        ramp.color_ramp.interpolation = "CONSTANT"
        ramp.color_ramp.elements[0].position = 0.0
        ramp.color_ramp.elements[0].color = (0.42, 0.40, 0.48, 1)
        e = ramp.color_ramp.elements.new(0.16)
        e.color = (0.78, 0.74, 0.70, 1)
        e = ramp.color_ramp.elements.new(0.42)
        e.color = (1.0, 0.97, 0.90, 1)
        # use luminance of shader result
        sep = nt.nodes.new("ShaderNodeRGBToBW")
        nt.links.new(s2r.outputs[0], sep.inputs[0])
        nt.links.new(sep.outputs[0], ramp.inputs[0])
        nt.links.new(ramp.outputs[0], mix.inputs[6])
        mix.inputs[7].default_value = lin(hexc)
        em = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(mix.outputs[2], em.inputs[0])
        nt.links.new(em.outputs[0], out.inputs[0])
    else:
        b = nt.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = lin(hexc)
        b.inputs["Roughness"].default_value = rough
    _mats[k] = m
    return m



# ------------------------------------------------------------------------------------------------ mix (Cycles) materials
def _nodes(nt):
    return nt.nodes.new, nt.links.new


def _mix_surface(nt, hexc, rough, jitter=1.0, bump=0.12, fine=14.0, broad=1.3):
    """Principled surface with per-object hue/value jitter, broad + fine noise variation, bump and bevel highlights."""
    N, L = _nodes(nt)
    geo = N("ShaderNodeNewGeometry")
    oi = N("ShaderNodeObjectInfo")
    base = N("ShaderNodeRGB"); base.outputs[0].default_value = lin(hexc)
    hsv = N("ShaderNodeHueSaturation")
    for sock, lo, hi in ((hsv.inputs["Hue"], 0.5 - 0.022 * jitter, 0.5 + 0.022 * jitter),
                         (hsv.inputs["Saturation"], 1.0 - 0.10 * jitter, 1.0 + 0.10 * jitter),
                         (hsv.inputs["Value"], 1.0 - 0.13 * jitter, 1.0 + 0.13 * jitter)):
        mr = N("ShaderNodeMapRange"); mr.inputs[3].default_value = lo; mr.inputs[4].default_value = hi
        L(oi.outputs["Random"], mr.inputs[0]); L(mr.outputs[0], sock)
    L(base.outputs[0], hsv.inputs["Color"])
    n1 = N("ShaderNodeTexNoise"); n1.inputs["Scale"].default_value = broad; n1.inputs["Detail"].default_value = 4.0
    L(geo.outputs["Position"], n1.inputs["Vector"])
    m1 = N("ShaderNodeMapRange"); m1.inputs[3].default_value = 0.78; m1.inputs[4].default_value = 1.18
    L(n1.outputs["Fac"], m1.inputs[0])
    n2 = N("ShaderNodeTexNoise"); n2.inputs["Scale"].default_value = fine; n2.inputs["Detail"].default_value = 3.0
    L(geo.outputs["Position"], n2.inputs["Vector"])
    m2 = N("ShaderNodeMapRange"); m2.inputs[3].default_value = 0.88; m2.inputs[4].default_value = 1.08
    L(n2.outputs["Fac"], m2.inputs[0])
    sc1 = N("ShaderNodeVectorMath"); sc1.operation = "SCALE"; L(hsv.outputs[0], sc1.inputs[0]); L(m1.outputs[0], sc1.inputs[3])
    sc2 = N("ShaderNodeVectorMath"); sc2.operation = "SCALE"; L(sc1.outputs[0], sc2.inputs[0]); L(m2.outputs[0], sc2.inputs[3])
    bv = N("ShaderNodeBevel"); bv.inputs["Radius"].default_value = 0.035; bv.samples = 4
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = bump; bp.inputs["Distance"].default_value = 0.05
    L(n2.outputs["Fac"], bp.inputs["Height"]); L(bv.outputs[0], bp.inputs["Normal"])
    mr = N("ShaderNodeMapRange"); mr.inputs[3].default_value = max(0.0, rough - 0.12); mr.inputs[4].default_value = min(1.0, rough + 0.12)
    L(n2.outputs["Fac"], mr.inputs[0])
    b = N("ShaderNodeBsdfPrincipled")
    L(sc2.outputs[0], b.inputs["Base Color"]); L(mr.outputs[0], b.inputs["Roughness"]); L(bp.outputs[0], b.inputs["Normal"])
    out = nt.nodes.get("Material Output") or N("ShaderNodeOutputMaterial")
    L(b.outputs[0], out.inputs["Surface"])
    return b


def _ramp(nt, src, stops, interp="LINEAR"):
    r = nt.nodes.new("ShaderNodeValToRGB")
    r.color_ramp.interpolation = interp
    cr = r.color_ramp
    cr.elements[0].position, cr.elements[0].color = stops[0][0], (lin(stops[0][1]) if isinstance(stops[0][1], str) else stops[0][1])
    cr.elements[1].position, cr.elements[1].color = stops[-1][0], (lin(stops[-1][1]) if isinstance(stops[-1][1], str) else stops[-1][1])
    for pos, col in stops[1:-1]:
        e = cr.elements.new(pos); e.color = lin(col) if isinstance(col, str) else col
    nt.links.new(src, r.inputs[0])
    return r


def _noise(nt, vec, scale, detail=4.0, rough=0.5):
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    nt.links.new(vec, n.inputs["Vector"])
    return n


def _blend(nt, fac, a, b):
    m = nt.nodes.new("ShaderNodeMix"); m.data_type = "RGBA"
    nt.links.new(fac, m.inputs[0]); nt.links.new(a, m.inputs[6]); nt.links.new(b, m.inputs[7])
    return m.outputs[2]


def _new_mat(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.name = "Material Output"
    return m, nt, out


def ground_mat():
    """Noise mix of dirt, dry grass, gravel and burnt patches (world-space, so it is continuous)."""
    m, nt, out = _new_mat("ground_mix")
    N, L = _nodes(nt)
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    dirt = _ramp(nt, _noise(nt, pos, 0.9, 5).outputs["Fac"], [(0.3, "#6E5A3E"), (0.5, "#8F7150"), (0.7, "#A58A5E")])
    gcol = _ramp(nt, _noise(nt, pos, 7.0, 3).outputs["Fac"], [(0.35, "#5F5A34"), (0.6, "#9A8F52")])
    gmask = _ramp(nt, _noise(nt, pos, 0.07, 4).outputs["Fac"], [(0.50, (0, 0, 0, 1)), (0.62, (1, 1, 1, 1))])
    # grass patches break up into speckle
    spk = _ramp(nt, _noise(nt, pos, 9.0, 2).outputs["Fac"], [(0.44, (0, 0, 0, 1)), (0.58, (1, 1, 1, 1))])
    gm2 = N("ShaderNodeMath"); gm2.operation = "MULTIPLY"; L(gmask.outputs[0], gm2.inputs[0]); L(spk.outputs[0], gm2.inputs[1])
    c = _blend(nt, gm2.outputs[0], dirt.outputs[0], gcol.outputs[0])
    vor = N("ShaderNodeTexVoronoi"); vor.inputs["Scale"].default_value = 5.5; L(pos, vor.inputs["Vector"])
    grav = _ramp(nt, vor.outputs["Distance"], [(0.0, "#6B665B"), (0.5, "#8A8478"), (0.9, "#A29C8C")])
    gvm = _ramp(nt, _noise(nt, pos, 0.22, 3).outputs["Fac"], [(0.55, (0, 0, 0, 1)), (0.68, (1, 1, 1, 1))])
    c = _blend(nt, gvm.outputs[0], c, grav.outputs[0])
    burn = _ramp(nt, _noise(nt, pos, 0.14, 4).outputs["Fac"], [(0.64, (0, 0, 0, 1)), (0.74, (1, 1, 1, 1))])
    c = _blend(nt, burn.outputs[0], c, _ramp(nt, _noise(nt, pos, 5.0, 3).outputs["Fac"], [(0.2, "#1E1B18"), (0.8, "#3A332C")]).outputs[0])
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.35; bp.inputs["Distance"].default_value = 0.08
    L(_noise(nt, pos, 18.0, 4).outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.92
    L(c, b.inputs["Base Color"]); L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return m


def road_mat():
    """Worn asphalt: broad patches, cracks, aggregate speckle, oil stains."""
    m, nt, out = _new_mat("road_mix")
    N, L = _nodes(nt)
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    base = _ramp(nt, _noise(nt, pos, 0.35, 5).outputs["Fac"], [(0.3, "#2C3029"), (0.55, "#3B3F38"), (0.8, "#4A4C45")])
    patch = _ramp(nt, _noise(nt, pos, 0.12, 3).outputs["Fac"], [(0.58, (0, 0, 0, 1)), (0.64, (1, 1, 1, 1))])
    c = _blend(nt, patch.outputs[0], base.outputs[0], _ramp(nt, _noise(nt, pos, 1.5, 3).outputs["Fac"], [(0.3, "#23251F"), (0.7, "#2F312B")]).outputs[0])
    vor = N("ShaderNodeTexVoronoi"); vor.feature = "DISTANCE_TO_EDGE"; vor.inputs["Scale"].default_value = 0.55
    L(pos, vor.inputs["Vector"])
    crack = _ramp(nt, vor.outputs["Distance"], [(0.0, (1, 1, 1, 1)), (0.035, (0, 0, 0, 1))])
    # break the cracks up so they are not a full web
    brk = _ramp(nt, _noise(nt, pos, 0.25, 2).outputs["Fac"], [(0.5, (0, 0, 0, 1)), (0.6, (1, 1, 1, 1))])
    cm = N("ShaderNodeMath"); cm.operation = "MULTIPLY"; L(crack.outputs[0], cm.inputs[0]); L(brk.outputs[0], cm.inputs[1])
    c = _blend(nt, cm.outputs[0], c, _ramp(nt, pos_noise := _noise(nt, pos, 3.0, 2).outputs["Fac"], [(0.0, "#101210"), (1.0, "#1A1C18")]).outputs[0])
    oil = _ramp(nt, _noise(nt, pos, 0.5, 3).outputs["Fac"], [(0.66, (0, 0, 0, 1)), (0.74, (1, 1, 1, 1))])
    c = _blend(nt, oil.outputs[0], c, _ramp(nt, _noise(nt, pos, 2.0, 2).outputs["Fac"], [(0.0, "#15171A"), (1.0, "#23262B")]).outputs[0])
    bp = N("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.4; bp.inputs["Distance"].default_value = 0.04
    L(_noise(nt, pos, 40.0, 3).outputs["Fac"], bp.inputs["Height"])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.78
    L(c, b.inputs["Base Color"]); L(bp.outputs[0], b.inputs["Normal"]); L(b.outputs[0], out.inputs["Surface"])
    return m


def grass_mat():
    m, nt, out = _new_mat("grass_mix")
    N, L = _nodes(nt)
    geo = N("ShaderNodeNewGeometry")
    sep = N("ShaderNodeSeparateXYZ"); L(geo.outputs["Position"], sep.inputs[0])
    zr = N("ShaderNodeMapRange"); zr.inputs[2].default_value = 0.7; zr.clamp = True; L(sep.outputs["Z"], zr.inputs[0])
    r = _ramp(nt, zr.outputs[0], [(0.0, "#3E3A22"), (0.5, "#8A7E46"), (1.0, "#C2B070")])
    var = _ramp(nt, _noise(nt, geo.outputs["Position"], 0.6, 2).outputs["Fac"], [(0.3, "#FFFFFF"), (0.7, "#B8A890")])
    mx = N("ShaderNodeMix"); mx.data_type = "RGBA"; mx.blend_type = "MULTIPLY"; mx.inputs[0].default_value = 1.0
    L(r.outputs[0], mx.inputs[6]); L(var.outputs[0], mx.inputs[7])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.9
    L(mx.outputs[2], b.inputs["Base Color"]); L(b.outputs[0], out.inputs["Surface"])
    return m


def puddle_mat():
    """Dark glossy water that mirrors the sky."""
    m, nt, out = _new_mat("puddle")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    b.inputs["Base Color"].default_value = lin("#1B1F22")
    b.inputs["Roughness"].default_value = 0.02
    b.inputs["Specular IOR Level"].default_value = 1.0
    nt.links.new(b.outputs[0], out.inputs["Surface"])
    return m


def crater_mat():
    m, nt, out = _new_mat("crater")
    N, L = _nodes(nt)
    tc = N("ShaderNodeTexCoord")
    vsub = N("ShaderNodeVectorMath"); vsub.operation = "SUBTRACT"; vsub.inputs[1].default_value = (0.5, 0.5, 0.0)
    ln = N("ShaderNodeVectorMath"); ln.operation = "LENGTH"
    L(tc.outputs["Generated"], vsub.inputs[0]); L(vsub.outputs[0], ln.inputs[0])
    rr = N("ShaderNodeMapRange"); rr.inputs[1].default_value = 0.2; rr.inputs[2].default_value = 0.5; rr.clamp = True
    L(ln.outputs["Value"], rr.inputs[0])
    pos = N("ShaderNodeNewGeometry").outputs["Position"]
    nz = _noise(nt, pos, 2.5, 4).outputs["Fac"]
    ramp = _ramp(nt, rr.outputs[0], [(0.0, "#14110E"), (0.5, "#2A221B"), (0.85, "#5E4C36"), (1.0, "#8F7150")])
    mm = N("ShaderNodeMix"); mm.data_type = "RGBA"; mm.blend_type = "MULTIPLY"; mm.inputs[0].default_value = 1.0
    nv = _ramp(nt, nz, [(0.3, "#FFFFFF"), (0.7, "#9A8E80")])
    L(ramp.outputs[0], mm.inputs[6]); L(nv.outputs[0], mm.inputs[7])
    b = N("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.95
    L(mm.outputs[2], b.inputs["Base Color"])
    # irregular alpha edge so the patch dissolves into the ground (no square outline)
    en = _noise(nt, tc.outputs["Generated"], 6.0, 3).outputs["Fac"]
    ev = N("ShaderNodeMath"); ev.operation = "MULTIPLY_ADD"; ev.inputs[1].default_value = 0.1; L(en, ev.inputs[0])
    ev.inputs[2].default_value = -0.05
    ed = N("ShaderNodeMath"); ed.operation = "ADD"; L(ln.outputs["Value"], ed.inputs[0]); L(ev.outputs[0], ed.inputs[1])
    em = N("ShaderNodeMapRange"); em.inputs[1].default_value = 0.30; em.inputs[2].default_value = 0.44
    em.inputs[3].default_value = 1.0; em.inputs[4].default_value = 0.0; em.clamp = True
    L(ed.outputs[0], em.inputs[0])
    tr = N("ShaderNodeBsdfTransparent")
    ms = N("ShaderNodeMixShader"); L(em.outputs[0], ms.inputs[0]); L(tr.outputs[0], ms.inputs[1]); L(b.outputs[0], ms.inputs[2])
    L(ms.outputs[0], out.inputs["Surface"])
    return m


def _bevel_mod(o, width):
    md = o.modifiers.new("bev", "BEVEL")
    md.width = width
    md.segments = 1
    md.limit_method = "ANGLE"
    return o


def _assign(o, m):
    o.data.materials.append(m)
    return o


def box(name, loc, size, m, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    if STYLE == "mix" and min(size) >= 0.22:
        # apply scale so the bevel width is metric
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        _bevel_mod(o, min(0.06, 0.18 * min(size)))
    return _assign(o, m)


def cyl(name, loc, r, h, m, rot=(0, 0, 0), verts=12, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=h, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    return _assign(o, m)


def sphere(name, loc, r, m, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=r, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    return _assign(o, m)


# ------------------------------------------------------------------------------------------------ world
def ground(size=400):
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, 0, 0))
    o = bpy.context.active_object
    o.name = "ground"
    _assign(o, ground_mat() if STYLE == "mix" else mat("sand"))
    return o


def dirt_patches(rng, n=40, extent=40):
    """Darker and lighter flat patches so the ground is not one flat colour."""
    if STYLE == "mix":
        return  # the ground shader already breaks the colour up; patch discs would only add outlines
    cols = ["#7A6446", "#9A8258", "#6E5A3E", "#857A62"]
    for i in range(n):
        x, y = rng.uniform(-extent, extent), rng.uniform(-extent * 0.2, extent * 2.2)
        r = rng.uniform(1.5, 5)
        o = cyl(f"patch{i}", (x, y, 0.01), r, 0.02, mat(rng.choice(cols)), verts=9)
        o.scale = (1, rng.uniform(0.5, 1.2), 1)


def road(p0, p1, width, m=None):
    """Flat strip between two ground points."""
    a, b = Vector(p0), Vector(p1)
    d = b - a
    mid = (a + b) / 2
    ang = math.atan2(d.y, d.x)
    if STYLE == "mix" and m is None:
        if "road" not in _mats:
            _mats["road"] = road_mat()
        m = _mats["road"]
    o = box("road", (mid.x, mid.y, 0.02), (d.length, width, 0.04), m or mat("asphalt", "#3A3F38"), rot=(0, 0, ang))
    # worn centre line (dashes)
    n = int(d.length // 4)
    for i in range(n):
        t = (i + 0.5) / n
        pt = a + d * t
        dsh = box("dash", (pt.x, pt.y, 0.05), (1.6, 0.14, 0.02), mat("bone", "#A8A78F"), rot=(0, 0, ang))
        if STYLE == "mix":
            nolines(dsh)
    return o


def junction(center=(0, 0), width=7):
    cx, cy = center
    road((cx - 120, cy), (cx + 120, cy), width)      # east-west
    road((cx, cy - 20), (cx, cy + 160), width)       # south-north (to the horizon)
    jp = box("jpatch", (cx, cy, 0.045), (width, width, 0.03),
             _mats["road"] if STYLE == "mix" else mat("asphalt", "#343B33"))
    if STYLE == "mix":
        nolines(jp)


def ruin(loc, w=6, d=4, h=5, rng=None, rot=0.0):
    """Broken concrete wall shell with ragged top and a few slabs/rebar posts."""
    import random
    rng = rng or random.Random(1)
    x, y = loc
    cm = mat("dust", "#7B776B")
    cd = mat("dust", "#5C5950")
    for i in range(6):
        seg = w / 6
        hh = h * rng.uniform(0.35, 1.0)
        box("wall", (x + (i - 2.5) * seg, y, hh / 2), (seg * 0.98, 0.5, hh), cm if i % 2 else cd, rot=(0, 0, rot))
    for i in range(4):
        hh = h * rng.uniform(0.3, 0.9)
        box("side", (x - w / 2, y + (i - 1.5) * d / 4, hh / 2), (0.5, d / 4, hh), cd)
    for i in range(5):
        box("slab", (x + rng.uniform(-w, w), y + rng.uniform(-d * 1.4, -0.8), 0.3),
            (rng.uniform(0.8, 1.8), rng.uniform(0.6, 1.2), rng.uniform(0.3, 0.7)), cm,
            rot=(rng.uniform(-.3, .3), rng.uniform(-.3, .3), rng.uniform(0, 3)))
    for i in range(3):
        cyl("rebar", (x + rng.uniform(-w / 2, w / 2), y, h * 0.8), 0.05, 1.6, mat("rust_d"),
            rot=(rng.uniform(-.4, .4), rng.uniform(-.4, .4), 0), verts=5)
    if STYLE == "mix":
        rubble_pile((x + rng.uniform(-1, 1), y - d * 0.7), r=w * 0.28, n=int(10 + w), rng=rng, h=h * 0.12)
        for i in range(2):      # standing columns and a cantilevered slab
            ch = h * rng.uniform(0.7, 1.15)
            box("col", (x + rng.uniform(-w / 2, w / 2), y + 0.9, ch / 2), (0.5, 0.5, ch), cd)
        box("ledge", (x + w * 0.1, y + 0.1, h * 0.55), (w * 0.4, d * 0.55, 0.22), cm, rot=(0.0, rng.uniform(-.12, .12), 0))
        box("cap", (x - w * 0.3, y, h * 1.02), (w * 0.2, 0.7, 0.18), cd, rot=(0, rng.uniform(-.3, .3), 0))


def pylon(loc, h=14):
    x, y = loc
    m = mat("steel_d")
    cyl("pylon", (x, y, h / 2), 0.25, h, m, verts=6)
    box("arm", (x, y, h * 0.92), (4, 0.2, 0.2), m)
    box("arm2", (x, y, h * 0.78), (3, 0.2, 0.2), m)
    if STYLE == "mix":
        for ox, zz in ((-2.0, 0.92), (2.0, 0.92), (-1.5, 0.78), (1.5, 0.78)):
            cyl("insul", (x + ox, y, h * zz + 0.2), 0.07, 0.4, mat("steel_l", "#737A80"), verts=6)
        for sgn in (-1, 1):
            box("brace", (x + sgn * 0.7, y, h * 0.55), (0.08, 0.08, h * 0.55), m, rot=(0, sgn * 0.12, 0))


# ------------------------------------------------------------------------------------------------ props
def sandbag_wall(p0, p1, rows=3, rng=None):
    import random
    rng = rng or random.Random(3)
    a, b = Vector(p0), Vector(p1)
    d = b - a
    n = max(2, int(d.length / 0.9))
    ang = math.atan2(d.y, d.x)
    cols = ["#8F8466", "#7C7254", "#9A9070"]
    for r in range(rows):
        for i in range(n - (r % 2)):
            t = (i + 0.5 + 0.5 * (r % 2)) / n
            pt = a + d * t
            sphere("bag", (pt.x, pt.y, 0.22 + r * 0.38), 0.5, mat(rng.choice(cols)), scale=(0.95, 0.6, 0.38))
            bpy.context.active_object.rotation_euler = (0, 0, ang + rng.uniform(-.1, .1))


def turret(loc, yaw=0.0):
    """Authority gun turret: round concrete base, olive armoured head, twin barrels, safety-yellow ring."""
    x, y = loc
    box("pad", (x, y, 0.1), (4.2, 4.2, 0.2), mat("dust", "#6B685D"))
    cyl("base", (x, y, 0.55), 1.7, 0.7, mat("steel"), verts=16)
    cyl("base2", (x, y, 1.05), 1.35, 0.4, mat("steel_l", "#737A80"), verts=16)
    cyl("ring", (x, y, 0.86), 1.42, 0.08, mat("yellow"), verts=16)
    s = math.sin(yaw), math.cos(yaw)      # forward = (sin, cos) with yaw 0 = north
    hx, hy = x, y
    head = box("head", (hx, hy, 1.7), (2.0, 2.2, 1.0), mat("olive"), rot=(0, 0, -yaw))
    box("headtop", (hx, hy, 2.3), (1.5, 1.7, 0.3), mat("olive_l"), rot=(0, 0, -yaw))
    for side in (-0.4, 0.4):
        bx = hx + s[0] * 1.9 + s[1] * side
        by = hy + s[1] * 1.9 - s[0] * side
        cyl("barrel", (bx, by, 1.8), 0.12, 2.6, mat("steel_d"),
            rot=(math.radians(90), 0, -yaw), verts=8)
        # cylinder default axis Z -> rotate 90 about X points along Y; then yaw
    box("mast", (hx - s[1] * 0.7, hy + s[0] * 0.7, 2.9), (0.06, 0.06, 1.2), mat("steel_d"))
    if STYLE == "mix":
        r = (s[1], -s[0])
        for sd in (-1, 1):
            box("vent", (hx + r[0] * sd * 1.02, hy + r[1] * sd * 1.02, 1.8), (0.08, 0.7, 0.4), mat("steel_d"), rot=(0, 0, -yaw))
            box("ammo", (hx + r[0] * sd * 2.2 - s[0] * 0.5, hy + r[1] * sd * 2.2 - s[1] * 0.5, 0.35), (0.5, 0.8, 0.5),
                mat("#4F5A48"), rot=(0, 0, -yaw))
        cyl("mantlet", (hx + s[0] * 1.15, hy + s[1] * 1.15, 1.8), 0.4, 0.4, mat("olive_l"), rot=(math.radians(90), 0, -yaw), verts=10)
    return head


def tank(loc, yaw=0.0):
    """Authority tank: two tracks, hull, squat turret with a long gun, yellow stripe on the hull side."""
    x, y = loc
    s = math.sin(yaw), math.cos(yaw)
    r = (s[1], -s[0])                     # right vector
    for side in (-1, 1):
        px, py = x + r[0] * side * 1.3, y + r[1] * side * 1.3
        box("track", (px, py, 0.45), (0.9, 4.6, 0.9), mat("steel_d"), rot=(0, 0, -yaw))
        box("trackcap", (px, py, 0.95), (1.0, 4.7, 0.12), mat("steel"), rot=(0, 0, -yaw))
    box("hull", (x, y, 1.15), (2.6, 4.2, 0.9), mat("olive"), rot=(0, 0, -yaw))
    box("glacis", (x + s[0] * 2.0, y + s[1] * 2.0, 1.1), (2.4, 1.0, 0.7), mat("olive_l"),
        rot=(0, 0, -yaw))
    box("stripe", (x + r[0] * 1.31, y + r[1] * 1.31, 1.2), (0.05, 3.2, 0.2), mat("yellow"), rot=(0, 0, -yaw))
    box("stripe2", (x - r[0] * 1.31, y - r[1] * 1.31, 1.2), (0.05, 3.2, 0.2), mat("yellow"), rot=(0, 0, -yaw))
    tx, ty = x - s[0] * 0.2, y - s[1] * 0.2
    box("turret", (tx, ty, 1.85), (2.0, 2.2, 0.8), mat("olive"), rot=(0, 0, -yaw))
    box("turrettop", (tx, ty, 2.3), (1.4, 1.5, 0.2), mat("olive_l"), rot=(0, 0, -yaw))
    cyl("gun", (tx + s[0] * 2.4, ty + s[1] * 2.4, 1.9), 0.14, 3.6, mat("steel_d"),
        rot=(math.radians(90), 0, -yaw), verts=8)
    cyl("hatch", (tx - s[0] * 0.4, ty - s[1] * 0.4, 2.5), 0.4, 0.15, mat("steel"), verts=8)
    if STYLE == "mix":
        def P(f, l, z):
            return (x + s[0] * f + r[0] * l, y + s[1] * f + r[1] * l, z)
        for side in (-1, 1):
            for k in range(6):                                            # road wheels along each track
                cyl("rwheel", P(-1.9 + k * 0.76, side * 1.78, 0.5), 0.37, 0.1, mat("steel", "#6A7075"),
                    rot=(0, math.pi / 2, -yaw), verts=10)
                cyl("rwhub", P(-1.9 + k * 0.76, side * 1.84, 0.5), 0.14, 0.06, mat("rust"), rot=(0, math.pi / 2, -yaw), verts=8)
            box("fender", P(0.0, side * 1.45, 1.08), (1.25, 4.9, 0.07), mat("olive_d"), rot=(0, 0, -yaw))
            box("skirt", P(0.0, side * 1.9, 0.75), (0.05, 4.4, 0.35), mat("olive_d"), rot=(0, 0, -yaw))
        cyl("tarp", P(-1.75, 0.0, 1.78), 0.22, 1.7, mat("sand", "#6F6A52"), rot=(0, math.pi / 2, -yaw), verts=8)
        box("box", P(-1.5, 0.75, 1.72), (0.5, 0.5, 0.36), mat("#4F5A48"), rot=(0, 0, -yaw))
        for l in (-0.7, -0.45):
            box("jerry", P(-1.9, l, 1.68), (0.22, 0.12, 0.34), mat("rust"), rot=(0, 0, -yaw))
        cyl("cable", P(1.9, 0.0, 1.45), 0.04, 2.0, mat("steel_d"), rot=(0, math.pi / 2, -yaw), verts=5)
        cyl("antenna", P(-1.0, -0.8, 3.0), 0.02, 2.2, mat("steel_d"), verts=4)
        cyl("muzzle", (tx + s[0] * 4.2, ty + s[1] * 4.2, 1.9), 0.2, 0.35, mat("steel_d"), rot=(math.radians(90), 0, -yaw), verts=8)
        box("mantlet", P(0.95, 0.0, 1.9), (0.9, 0.5, 0.65), mat("olive_l"), rot=(0, 0, -yaw))
        box("cupola", P(-0.6, 0.55, 2.55), (0.45, 0.45, 0.28), mat("olive_l"), rot=(0, 0, -yaw))


def figure(loc, yaw=0.0, kneel=False, scale=1.0):
    """Rifle figure: legs, torso, helmet, rifle.  Olive kit with a yellow armband."""
    x, y = loc
    s = math.sin(yaw), math.cos(yaw)
    k = 0.65 if kneel else 1.0
    box("legs", (x, y, 0.45 * k * scale), (0.35 * scale, 0.28 * scale, 0.9 * k * scale), mat("olive_d"))
    box("torso", (x, y, (0.9 * k + 0.33) * scale), (0.5 * scale, 0.3 * scale, 0.66 * scale), mat("olive"), rot=(0, 0, -yaw))
    box("band", (x, y, (0.9 * k + 0.45) * scale), (0.53 * scale, 0.33 * scale, 0.1 * scale), mat("yellow"), rot=(0, 0, -yaw))
    sphere("helm", (x, y, (0.9 * k + 0.82) * scale), 0.19 * scale, mat("olive_l"), scale=(1, 1, 0.85))
    box("rifle", (x + s[0] * 0.45 * scale, y + s[1] * 0.45 * scale, (0.9 * k + 0.5) * scale),
        (0.07 * scale, 1.0 * scale, 0.09 * scale), mat("steel_d"), rot=(0, 0, -yaw))
    if STYLE == "mix":
        z0 = (0.9 * k + 0.33) * scale
        box("pack", (x - s[0] * 0.24 * scale, y - s[1] * 0.24 * scale, z0 + 0.02), (0.38 * scale, 0.22 * scale, 0.5 * scale),
            mat("olive_d", "#3B4332"), rot=(0, 0, -yaw))
        box("bedroll", (x - s[0] * 0.26 * scale, y - s[1] * 0.26 * scale, z0 + 0.33 * scale), (0.4 * scale, 0.14 * scale, 0.12 * scale),
            mat("sand", "#7C7254"), rot=(0, 0, -yaw))
        cyl("brim", (x, y, (0.9 * k + 0.78) * scale), 0.24 * scale, 0.035 * scale, mat("olive_d"), verts=10)
        box("boots", (x, y, 0.05 * scale), (0.38 * scale, 0.34 * scale, 0.1 * scale), mat("#1B1A18"), rot=(0, 0, -yaw))
        box("belt", (x, y, (0.9 * k + 0.12) * scale), (0.53 * scale, 0.33 * scale, 0.07 * scale), mat("#3A3226"), rot=(0, 0, -yaw))


def barrel_drum(loc, m=None):
    cyl("drum", (loc[0], loc[1], 0.45), 0.35, 0.9, m or mat("rust"), verts=10)


# ------------------------------------------------------------------------------------------------ detail props (mix)
def rubble_pile(loc, r=1.6, n=14, rng=None, h=0.9, tint=None):
    """Heap of broken concrete blocks, slabs and a few rebar stubs."""
    import random
    rng = rng or random.Random(11)
    x, y = loc
    cols = tint or ["#7B776B", "#5C5950", "#8A8478", "#6B685D", "#9A9484"]
    for i in range(n):
        a = rng.uniform(0, math.tau)
        d = abs(rng.gauss(0, r * 0.45))
        px, py = x + math.cos(a) * d, y + math.sin(a) * d
        fall = max(0.15, 1.0 - d / (r * 1.2))
        sz = (rng.uniform(0.25, 0.9), rng.uniform(0.25, 0.8), rng.uniform(0.18, 0.55))
        box("rubble", (px, py, sz[2] * 0.35 + h * fall * rng.uniform(0.1, 0.5)), sz, mat(rng.choice(cols)),
            rot=(rng.uniform(-.5, .5), rng.uniform(-.5, .5), rng.uniform(0, 3.1)))
    for i in range(rng.randint(2, 4)):
        cyl("rebar", (x + rng.uniform(-r, r) * 0.5, y + rng.uniform(-r, r) * 0.5, h * 0.7), 0.025, rng.uniform(0.8, 1.6),
            mat("rust_d"), rot=(rng.uniform(-.7, .7), rng.uniform(-.7, .7), 0), verts=5)


def crate(loc, s=0.9, yaw=0.0, col=None):
    x, y, z = loc[0], loc[1], loc[2] if len(loc) > 2 else 0.0
    col = col or "#6B5A3C"
    box("crate", (x, y, z + s / 2), (s, s, s), mat(col), rot=(0, 0, yaw))
    lc = mat("#8A7650")
    c, sn = math.cos(yaw), math.sin(yaw)
    for ox in (-0.5, 0.5):
        box("batten", (x + c * ox * s, y + sn * ox * s, z + s / 2), (0.07, s * 1.02, s * 1.02), lc, rot=(0, 0, yaw))
    box("lid", (x, y, z + s + 0.015), (s * 1.04, s * 1.04, 0.05), lc, rot=(0, 0, yaw))


def crate_stack(loc, yaw=0.0, rng=None):
    import random
    rng = rng or random.Random(4)
    x, y = loc
    cols = ["#6B5A3C", "#5A5C3E", "#7A6644", "#4F5A48"]
    crate((x, y, 0), 0.9, yaw, rng.choice(cols))
    crate((x + 1.0 * math.cos(yaw + 0.2), y + 1.0 * math.sin(yaw + 0.2), 0), 0.8, yaw + 0.3, rng.choice(cols))
    crate((x + 0.1, y + 0.05, 0.9), 0.7, yaw + 0.5, rng.choice(cols))


def drum_cluster(loc, rng=None, n=4):
    import random
    rng = rng or random.Random(9)
    x, y = loc
    cols = ["#85502F", "#4B5742", "#555B61", "#B07445", "#5E3420"]
    for i in range(n):
        px, py = x + rng.uniform(-0.9, 0.9), y + rng.uniform(-0.9, 0.9)
        if i == n - 1:      # one tipped over
            cyl("drum", (px, py, 0.35), 0.35, 0.9, mat(rng.choice(cols)), rot=(math.radians(90), 0, rng.uniform(0, 3)), verts=10)
        else:
            cyl("drum", (px, py, 0.45), 0.35, 0.9, mat(rng.choice(cols)), verts=10)
            cyl("drumrim", (px, py, 0.9), 0.36, 0.04, mat("steel_d"), verts=10)


def broken_fence(p0, p1, rng=None, h=1.5):
    """Post-and-rail fence: leaning posts, missing rails, sagging wire."""
    import random
    rng = rng or random.Random(13)
    a, b = Vector(p0), Vector(p1)
    d = b - a
    n = max(2, int(d.length / 1.7))
    ang = math.atan2(d.y, d.x)
    pm = mat("#5A4A32")
    posts = []
    for i in range(n + 1):
        t = i / n
        pt = a + d * t
        if rng.random() < 0.15:
            posts.append(None)
            continue
        lean = rng.uniform(-0.25, 0.25)
        cyl("post", (pt.x, pt.y, h / 2 * 0.95), 0.07, h * rng.uniform(0.6, 1.0), pm, rot=(lean, rng.uniform(-.2, .2), 0), verts=6)
        posts.append(pt)
    for i in range(n):
        if posts[i] is None or posts[i + 1] is None:
            continue
        m_ = (posts[i] + posts[i + 1]) / 2
        for zz in (h * 0.85, h * 0.5):
            if rng.random() < 0.65:
                box("rail", (m_.x, m_.y, zz - rng.uniform(0, 0.25)), (d.length / n, 0.05, 0.12), pm,
                    rot=(0, rng.uniform(-.25, .25), ang))
        if rng.random() < 0.5:
            box("mesh", (m_.x, m_.y, h * 0.4), (d.length / n, 0.012, h * 0.55), mat("steel", "#6A7075"), rot=(0, 0, ang))


def road_sign(loc, yaw=0.0, lean=0.12):
    x, y = loc
    cyl("signpole", (x, y, 1.6), 0.05, 3.2, mat("steel_d"), rot=(lean, 0, 0), verts=6)
    box("signplate", (x, y - 0.06, 2.9), (1.3, 0.04, 0.9), mat("#9AA08F"), rot=(lean, 0, yaw))
    box("signrim", (x, y - 0.08, 2.9), (1.4, 0.03, 1.0), mat("steel", "#6A7075"), rot=(lean, 0, yaw))


def power_line(pylons, h, sag=1.4, rng=None):
    """Catenary wires between consecutive pylons (3 per span, from the cross-arm tips)."""
    for (x0, y0), (x1, y1) in zip(pylons[:-1], pylons[1:]):
        for ox, zz, w in ((-2.0, 0.92, 0), (2.0, 0.92, 1), (-1.5, 0.78, 2), (1.5, 0.78, 3)):
            cu = bpy.data.curves.new("wire", "CURVE")
            cu.dimensions = "3D"
            cu.bevel_depth = 0.03
            cu.bevel_resolution = 1
            sp = cu.splines.new("POLY")
            n = 14
            sp.points.add(n)
            for i in range(n + 1):
                t = i / n
                sp.points[i].co = (x0 + (x1 - x0) * t + ox, y0 + (y1 - y0) * t, h * zz - sag * 4 * t * (1 - t), 1.0)
            o = bpy.data.objects.new("wire", cu)
            bpy.context.scene.collection.objects.link(o)
            o.data.materials.append(mat("steel_d"))
            nolines(o)


def burnt_car(loc, yaw=0.0, rng=None):
    """Burnt-out sedan: charred shell with rust patches, no glass, flat tyres, a few embers inside."""
    import random
    rng = rng or random.Random(21)
    x, y = loc
    s = math.sin(yaw), math.cos(yaw)
    r = (s[1], -s[0])
    ch = mat("#2B2724", rough=0.9)
    ch2 = mat("#3A332C", rough=0.9)
    rs = mat("rust")
    def P(f, l, z):
        return (x + s[0] * f + r[0] * l, y + s[1] * f + r[1] * l, z)
    box("chassis", P(0, 0, 0.55), (1.8, 4.3, 0.5), ch, rot=(0, 0, -yaw))
    box("hood", P(1.5, 0, 0.88), (1.7, 1.2, 0.12), ch2, rot=(-0.12, 0, -yaw))
    box("trunk", P(-1.7, 0, 0.9), (1.7, 0.9, 0.12), ch, rot=(0.05, 0, -yaw))
    box("roof", P(-0.1, 0, 1.55), (1.6, 1.7, 0.1), ch2, rot=(0, 0, -yaw))
    for fx in (0.75, -0.95):
        for l in (-0.76, 0.76):
            box("pillar", P(fx, l, 1.2), (0.08, 0.08, 0.7), ch, rot=(0, 0, -yaw))
    box("dash", P(0.65, 0, 1.0), (1.5, 0.15, 0.3), ch, rot=(0, 0, -yaw))
    box("seat", P(-0.2, 0.4, 0.95), (0.6, 0.55, 0.35), rs, rot=(0, 0, -yaw))
    box("seat", P(-0.2, -0.4, 0.95), (0.6, 0.55, 0.35), ch2, rot=(0, 0, -yaw))
    box("rustpatch", P(0.4, 0.91, 0.75), (0.04, 1.1, 0.25), rs, rot=(0, 0, -yaw))
    box("rustpatch", P(-1.0, -0.91, 0.7), (0.04, 0.9, 0.3), rs, rot=(0, 0, -yaw))
    for fx in (1.35, -1.35):
        for l in (-0.95, 0.95):
            cyl("wheel", P(fx, l, 0.33), 0.33, 0.24, mat("#1B1A18", rough=1.0), rot=(0, math.pi / 2, -yaw), verts=10)
            cyl("hub", P(fx, l * 1.05, 0.33), 0.14, 0.26, mat("steel_l", "#737A80"), rot=(0, math.pi / 2, -yaw), verts=8)
    for i in range(9):
        embers_at = P(rng.uniform(-1, 1), rng.uniform(-0.6, 0.6), 1.0 + rng.uniform(0, 0.6))
        sphere("ember", embers_at, rng.uniform(0.025, 0.05), mat("yellow", "#FF7A22", emit="#FF6A12", estr=40.0))
    point_light(P(0, 0, 1.0), 120.0, (1.0, 0.45, 0.15), 0.4)


def debris_field(rng, n=60, xr=(-30, 30), yr=(-14, 50), avoid=None):
    """Scattered plates, pipes, blocks and planks."""
    cols = ["#7B776B", "#5C5950", "#85502F", "#555B61", "#6B5A3C", "#2B2724"]
    for i in range(n):
        x, y = rng.uniform(*xr), rng.uniform(*yr)
        if avoid and avoid(x, y):
            continue
        k = rng.random()
        c = mat(rng.choice(cols))
        if k < 0.35:
            box("plate", (x, y, 0.04), (rng.uniform(0.5, 1.4), rng.uniform(0.4, 1.0), 0.05), c, rot=(rng.uniform(-.1, .1), rng.uniform(-.1, .1), rng.uniform(0, 3)))
        elif k < 0.6:
            cyl("pipe", (x, y, 0.07), 0.07, rng.uniform(0.8, 2.2), c, rot=(0, math.pi / 2, rng.uniform(0, 3)), verts=7)
        elif k < 0.85:
            box("block", (x, y, 0.12), (rng.uniform(0.25, 0.6), rng.uniform(0.25, 0.5), rng.uniform(0.2, 0.4)), c, rot=(rng.uniform(-.3, .3), rng.uniform(-.3, .3), rng.uniform(0, 3)))
        else:
            box("plank", (x, y, 0.05), (rng.uniform(1.0, 2.0), 0.14, 0.04), mat("#6B5A3C"), rot=(0, 0, rng.uniform(0, 3)))


def tufts(rng, n=900, xr=(-45, 45), yr=(-16, 80), avoid=None, scale=1.0):
    """Dry grass / weed clumps as ONE mesh of thin leaning blades (cheap, no outlines)."""
    import bmesh
    bm = bmesh.new()
    for i in range(n):
        x, y = rng.uniform(*xr), rng.uniform(*yr)
        if avoid and avoid(x, y):
            continue
        k = scale * (1.0 + max(0.0, (y + 16) / 60.0) * 1.6)     # bigger with distance so they still read
        for b in range(rng.randint(4, 8)):
            a = rng.uniform(0, math.tau)
            lean = rng.uniform(0.1, 0.55)
            h = rng.uniform(0.25, 0.75) * k
            bx, by = x + math.cos(a) * 0.07 * k, y + math.sin(a) * 0.07 * k
            px, py = -math.sin(a) * 0.03 * k, math.cos(a) * 0.03 * k
            tip = (bx + math.cos(a) * lean * h, by + math.sin(a) * lean * h, h)
            v = [bm.verts.new((bx - px, by - py, 0)), bm.verts.new((bx + px, by + py, 0)), bm.verts.new(tip)]
            bm.faces.new(v)
    me = bpy.data.meshes.new("tufts")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("tufts", me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(_mats.setdefault("grass", grass_mat()))
    return nolines(o)


def puddle(loc, rx=1.6, ry=0.8, rot=0.0, z=0.058):
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=1.0, depth=0.01, location=(loc[0], loc[1], z))
    o = bpy.context.active_object
    o.name = "puddle"
    o.scale = (rx, ry, 1)
    o.rotation_euler = (0, 0, rot)
    o.data.materials.append(_mats.setdefault("puddle", puddle_mat()))
    return nolines(o)


def tire_tracks(pts, gap=1.7, width=0.32, col="#2E261C", z=0.03):
    """Pair of dirt tyre tracks following a polyline of (x, y) points."""
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        d = Vector((x1 - x0, y1 - y0, 0))
        ang = math.atan2(d.y, d.x)
        nx, ny = -math.sin(ang), math.cos(ang)
        for side in (-0.5, 0.5):
            o = box("track", ((x0 + x1) / 2 + nx * gap * side, (y0 + y1) / 2 + ny * gap * side, z),
                    (d.length * 1.04, width, 0.012), mat(col, rough=1.0), rot=(0, 0, ang))
            nolines(o)


def crater(loc, R=2.2, rng=None, rim=0.42, z0=0.06):
    """Shell crater: scorched floor and a raised, ragged rim of thrown earth (heightfield on a grid)."""
    import bmesh, random
    rng = rng or random.Random(31)
    bm = bmesh.new()
    n = 36
    ext = R * 1.9
    verts = {}
    for i in range(n + 1):
        for j in range(n + 1):
            u, v = (i / n - 0.5) * 2 * ext, (j / n - 0.5) * 2 * ext
            r = math.hypot(u, v)
            th = math.atan2(v, u)
            Rr = R * (1 + 0.18 * math.sin(3 * th + 1.0) + 0.1 * math.sin(7 * th))
            z = z0 + rim * math.exp(-((r - Rr) / (0.42 * R * 0.4)) ** 2) * (0.7 + 0.5 * math.sin(5 * th + 2.0)) \
                + (0.02 if r < Rr else 0.0) + 0.05 * math.exp(-(r / (0.35 * R)) ** 2)
            fade = max(0.0, min(1.0, (ext - r) / (ext * 0.35)))
            z = 0.003 + (z - 0.003) * fade
            verts[(i, j)] = bm.verts.new((loc[0] + u, loc[1] + v, z))
    for i in range(n):
        for j in range(n):
            bm.faces.new((verts[(i, j)], verts[(i + 1, j)], verts[(i + 1, j + 1)], verts[(i, j + 1)]))
    me = bpy.data.meshes.new("crater")
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    o = bpy.data.objects.new("crater", me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(crater_mat())
    nolines(o)
    # a few thrown clods around the lip
    for i in range(10):
        a = rng.uniform(0, math.tau)
        d = R * rng.uniform(0.9, 1.7)
        box("clod", (loc[0] + math.cos(a) * d, loc[1] + math.sin(a) * d, 0.12), (rng.uniform(.25, .5), rng.uniform(.25, .45), rng.uniform(.18, .3)),
            mat(rng.choice(["#5A4A34", "#6E5A3E", "#3A2F25"])), rot=(rng.uniform(-.4, .4), rng.uniform(-.4, .4), rng.uniform(0, 3)))
    return o


def ridge(y, rng, n=9, h=(25, 55), tint="#4A4A70", spread=700):
    """Far silhouette hills; the haze lightens them."""
    for i in range(n):
        x = -spread + (i + rng.uniform(-0.3, 0.3)) * (2 * spread / (n - 1))
        hh = rng.uniform(*h)
        o = cyl("hill", (x, y + rng.uniform(-30, 30), hh * 0.5 - 2), rng.uniform(60, 120), hh, mat(tint, rough=1.0), verts=7, r2=rng.uniform(8, 25))
        nolines(o)


def skyline(rng, y, n=14, xr=(-120, 120), h=(8, 30), tint="#4D4A55"):
    """Far ruined-tower silhouettes between the road and the hills."""
    for i in range(n):
        x = rng.uniform(*xr)
        if abs(x) < 6:
            continue
        w, d, hh = rng.uniform(6, 14), rng.uniform(6, 12), rng.uniform(*h)
        box("far", (x, y + rng.uniform(-15, 15), hh / 2), (w, d, hh), mat(tint, rough=1.0))
        if rng.random() < 0.5:
            box("far", (x + w * 0.4, y, hh * 0.35), (w * 0.5, d, hh * 0.7), mat(tint, rough=1.0))


def embers(loc, n=20, spread=(6, 6, 14), r=0.4, rng=None):
    import random
    rng = rng or random.Random(41)
    for i in range(n):
        p = (loc[0] + rng.uniform(-spread[0], spread[0]), loc[1] + rng.uniform(-spread[1], spread[1]),
             loc[2] + rng.uniform(0.5, spread[2]))
        sphere("ember", p, r * rng.uniform(0.4, 1.0), mat("yellow", "#FFAA40", emit="#FF8A20", estr=150.0))


def fire(loc, size=3.0, rng=None, light=True):
    """Flames at the base of a smoke column: stacked emissive cones plus a warm point light and drifting embers."""
    import random
    rng = rng or random.Random(43)
    x, y = loc
    cols = (("#FF5A14", 14.0), ("#FF8A20", 18.0), ("#FFC060", 22.0))
    for i in range(7):
        k = rng.choice(cols)
        r = size * rng.uniform(0.35, 0.8)
        cyl("flame", (x + rng.uniform(-size, size) * 0.6, y + rng.uniform(-size, size) * 0.3, r * 0.9), r, r * 2.2,
            mat("yellow", k[0], emit=k[0], estr=k[1]), verts=7, r2=0.02)
    if light:
        point_light((x, y - size, size * 1.5), 8.0e4 * (size / 3.0) ** 2, (1.0, 0.45, 0.16), size)
    embers((x, y, 0), n=int(12 + size * 2), spread=(size * 2, size * 1.5, size * 5), r=size * 0.075, rng=rng)


# ------------------------------------------------------------------------------------------------ atmosphere
def smoke_column(loc, h=45, r0=2.5, n=9, tint="#3A3733", rng=None):
    """Stack of dark puffs drifting to the west (towards camera-left)."""
    import random
    rng = rng or random.Random(5)
    x, y = loc
    for i in range(n):
        t = i / (n - 1)
        sm = mat(tint, rough=1.0)
        if STYLE == "mix":
            # dark soot at the base -> ash grey-brown at the top, with the lower puffs lit from the fire below
            a, b = (0x0A, 0x08, 0x08), (0x3C, 0x36, 0x34)
            cc = "#%02X%02X%02X" % tuple(int(a[k] + (b[k] - a[k]) * min(1.0, t * 1.15) ** 0.8) for k in range(3))
            lo = max(2, n // 2)
            if i < lo:
                sm = mat(cc, rough=1.0, emit="#FF6A20", estr=1.1 * (1 - i / lo) ** 1.5 + 0.05)
            else:
                sm = mat(cc, rough=1.0)
        o = sphere("smoke", (x - t * t * 18 + rng.uniform(-1.5, 1.5), y + rng.uniform(-1.5, 1.5), 2 + t * h),
                   r0 * (1 + t * 2.2), sm, scale=(1, 1, 0.8))


def dust_cloud(loc, n=7, length=12, rng=None, tint="#A08C6A"):
    """Billowing dust plume above/behind the column: puffs rise and spread with distance so it reads as a cloud."""
    import random
    rng = rng or random.Random(7)
    x, y = loc
    for i in range(n):
        t = i / max(1, n - 1)
        r = 3.0 + 4.0 * (1 - t) + rng.uniform(0, 2)
        dm = mat(tint, rough=1.0, emit="#FFC283", estr=0.55) if STYLE == "mix" else mat(tint, rough=1.0)
        sphere("dust", (x + rng.uniform(-7, 7), y - t * length, 2 + rng.uniform(0, 1) * (6 + 12 * (1 - t))),
               r, dm, scale=(1.5, 1.0, 0.8))


def column_trucks(start_y=110, n=4, x=0.0, gap=9):
    """Tiny silhouettes of the relief column far up the road."""
    for i in range(n):
        yy = start_y + i * gap
        box("truck", (x, yy, 1.4), (2.6, 6.0, 2.4), mat("olive_d"))
        box("cab", (x, yy - 3.2, 1.7), (2.4, 1.6, 2.0), mat("olive"))


def sky_world(top, horizon, sun_glow=None, strength=1.0, sun_dir=(-0.75, 0.55)):
    """Gradient sky built from the world node tree (dawn): zenith -> horizon, warm glow on the sun side."""
    w = bpy.data.worlds.new("sky")
    bpy.context.scene.world = w
    w.use_nodes = True
    nt = w.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    bg = nt.nodes.new("ShaderNodeBackground")
    out = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    # Generated z runs 0..1 over the sphere; map the upper half (0.5..1) to the gradient
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs[1].default_value = 0.5
    mr.inputs[2].default_value = 0.60
    mr.clamp = True
    nt.links.new(sep.outputs["Z"], mr.inputs[0])
    nt.links.new(mr.outputs[0], ramp.inputs[0])
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = lin(horizon)
    e = ramp.color_ramp.elements.new(0.45)
    e.color = lin(sun_glow or horizon)
    ramp.color_ramp.elements[-1].position = 1.0
    ramp.color_ramp.elements[-1].color = lin(top)
    ramp.color_ramp.elements[1].position = 0.18
    ramp.color_ramp.elements[1].color = lin(sun_glow or horizon)
    ramp.color_ramp.elements[2].position = 1.0
    ramp.color_ramp.elements[2].color = lin(top)
    bg.inputs["Strength"].default_value = strength
    nt.links.new(ramp.outputs[0], bg.inputs[0])
    nt.links.new(bg.outputs[0], out.inputs[0])


def sky_dawn(sun_vec, strength=1.8):
    """Dawn sky for Cycles: deep blue-violet zenith -> violet -> rose -> orange horizon, a hot sun glow towards
    sun_vec (direction TO the sun), and layered cloud bands lit from below.  Also lights the scene (blue fill)."""
    w = bpy.data.worlds.new("sky")
    bpy.context.scene.world = w
    w.use_nodes = True
    nt = w.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    N = nt.nodes.new
    L = nt.links.new
    tc = N("ShaderNodeTexCoord")
    sub = N("ShaderNodeVectorMath"); sub.operation = "SUBTRACT"; sub.inputs[1].default_value = (0.5, 0.5, 0.5)
    sc2 = N("ShaderNodeVectorMath"); sc2.operation = "SCALE"; sc2.inputs[3].default_value = 2.0
    nrm = N("ShaderNodeVectorMath"); nrm.operation = "NORMALIZE"
    L(tc.outputs["Object"], nrm.inputs[0])
    sep = N("ShaderNodeSeparateXYZ"); L(nrm.outputs[0], sep.inputs[0])
    el = N("ShaderNodeMapRange"); el.inputs[1].default_value = 0.0; el.inputs[2].default_value = 1.0; el.clamp = True
    L(sep.outputs["Z"], el.inputs[0])
    ramp = N("ShaderNodeValToRGB"); L(el.outputs[0], ramp.inputs[0])
    cr = ramp.color_ramp
    stops = [(0.0, "#FFA25A"), (0.045, "#F58A5E"), (0.13, "#D96C88"), (0.30, "#8A5FA0"), (0.58, "#3F4C92"), (1.0, "#16215A")]
    cr.elements[0].position, cr.elements[0].color = stops[0][0], lin(stops[0][1])
    cr.elements[1].position, cr.elements[1].color = stops[-1][0], lin(stops[-1][1])
    for pos, col in stops[1:-1]:
        e = cr.elements.new(pos); e.color = lin(col)
    # sun glow
    sv = Vector(sun_vec).normalized()
    dot = N("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"; dot.inputs[1].default_value = tuple(sv)
    L(nrm.outputs[0], dot.inputs[0])
    mx = N("ShaderNodeMath"); mx.operation = "MAXIMUM"; mx.inputs[1].default_value = 0.0; L(dot.outputs["Value"], mx.inputs[0])
    g1 = N("ShaderNodeMath"); g1.operation = "POWER"; g1.inputs[1].default_value = 22.0; L(mx.outputs[0], g1.inputs[0])
    g2 = N("ShaderNodeMath"); g2.operation = "POWER"; g2.inputs[1].default_value = 220.0; L(mx.outputs[0], g2.inputs[0])
    gs = N("ShaderNodeMath"); gs.operation = "MULTIPLY_ADD"; gs.inputs[1].default_value = 0.9; L(g1.outputs[0], gs.inputs[0])
    L(g2.outputs[0], gs.inputs[2])
    gcol = N("ShaderNodeVectorMath"); gcol.operation = "SCALE"; gcol.inputs[0].default_value = tuple(lin("#FFC98A")[:3])
    L(gs.outputs[0], gcol.inputs[3])
    add = N("ShaderNodeMix"); add.data_type = "RGBA"; add.blend_type = "ADD"; add.inputs[0].default_value = 1.0
    L(ramp.outputs[0], add.inputs[6]); L(gcol.outputs[0], add.inputs[7])
    # cloud bands: noise stretched along the horizon
    mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (1.0, 1.0, 5.5)
    L(nrm.outputs[0], mp.inputs[0])
    nz = N("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 2.6; nz.inputs["Detail"].default_value = 5.0
    nz.inputs["Roughness"].default_value = 0.55
    L(mp.outputs[0], nz.inputs["Vector"])
    cm = N("ShaderNodeMapRange"); cm.inputs[1].default_value = 0.47; cm.inputs[2].default_value = 0.66; cm.clamp = True
    L(nz.outputs["Fac"], cm.inputs[0])
    w1 = N("ShaderNodeMapRange"); w1.inputs[1].default_value = 0.025; w1.inputs[2].default_value = 0.11; w1.clamp = True
    L(sep.outputs["Z"], w1.inputs[0])
    w2 = N("ShaderNodeMapRange"); w2.inputs[1].default_value = 0.30; w2.inputs[2].default_value = 0.52
    w2.inputs[3].default_value = 1.0; w2.inputs[4].default_value = 0.0; w2.clamp = True
    L(sep.outputs["Z"], w2.inputs[0])
    wm = N("ShaderNodeMath"); wm.operation = "MULTIPLY"; L(w1.outputs[0], wm.inputs[0]); L(w2.outputs[0], wm.inputs[1])
    mk = N("ShaderNodeMath"); mk.operation = "MULTIPLY"; mk.inputs[1].default_value = 0.92
    cmk = N("ShaderNodeMath"); cmk.operation = "MULTIPLY"; L(cm.outputs[0], cmk.inputs[0]); L(wm.outputs[0], cmk.inputs[1])
    L(cmk.outputs[0], mk.inputs[0])
    cramp = N("ShaderNodeValToRGB"); L(el.outputs[0], cramp.inputs[0])
    cc = cramp.color_ramp
    cc.elements[0].position, cc.elements[0].color = 0.04, lin("#FFB66E")
    cc.elements[1].position, cc.elements[1].color = 0.40, lin("#B0628E")
    e = cc.elements.new(0.16); e.color = lin("#F08A82")
    cb = N("ShaderNodeVectorMath"); cb.operation = "SCALE"; cb.inputs[3].default_value = 1.9
    L(cramp.outputs[0], cb.inputs[0])
    fin = N("ShaderNodeMix"); fin.data_type = "RGBA"; fin.inputs[0].default_value = 1.0
    L(mk.outputs[0], fin.inputs[0]); L(add.outputs[2], fin.inputs[6]); L(cb.outputs[0], fin.inputs[7])
    bg = N("ShaderNodeBackground"); bg.inputs["Strength"].default_value = strength
    out = N("ShaderNodeOutputWorld")
    L(fin.outputs[2], bg.inputs[0]); L(bg.outputs[0], out.inputs[0])
    return w


def haze(density=0.0045, colour="#C8D0F5", aniso=0.2, top=70.0):
    """Aerial perspective: a big bounded volume (so the sky stays visible behind it) around the whole scene.
    Distance goes lighter and bluer; low sun makes shafts through it.  Cycles only."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 380, top / 2 - 5))
    o = bpy.context.active_object
    o.name = "haze"
    o.scale = (1600, 1000, top)
    m = bpy.data.materials.new("hazem")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    v = nt.nodes.new("ShaderNodeVolumeScatter")
    v.inputs["Density"].default_value = density
    v.inputs["Color"].default_value = lin(colour)
    v.inputs["Anisotropy"].default_value = aniso
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(v.outputs[0], out.inputs["Volume"])
    o.data.materials.append(m)
    o.display_type = "WIRE"
    o.visible_shadow = False
    return nolines(o)


def area_light(loc, look_at, size, energy, colour):
    bpy.ops.object.light_add(type="AREA", location=loc)
    o = bpy.context.active_object
    o.data.size = size
    o.data.energy = energy
    o.data.color = colour
    o.rotation_euler = (Vector(look_at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


def point_light(loc, energy, colour, radius=1.0):
    bpy.ops.object.light_add(type="POINT", location=loc)
    o = bpy.context.active_object
    o.data.energy = energy
    o.data.color = colour
    o.data.shadow_soft_size = radius
    return o


def sun(rot_deg, energy, colour=(1.0, 0.72, 0.5), angle=3.0):
    bpy.ops.object.light_add(type="SUN")
    o = bpy.context.active_object
    o.rotation_euler = [math.radians(a) for a in rot_deg]
    o.data.energy = energy
    o.data.color = colour
    o.data.angle = math.radians(angle)
    return o


def fog(density=0.004, colour=(0.80, 0.62, 0.50)):
    """Volumetric haze via a big cube with a Volume Scatter material (cheap but works in Eevee)."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 60, 25))
    o = bpy.context.active_object
    o.name = "fog"
    o.scale = (400, 400, 60)
    m = bpy.data.materials.new("fogm")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    v = nt.nodes.new("ShaderNodeVolumeScatter")
    v.inputs["Density"].default_value = density
    v.inputs["Color"].default_value = (*colour, 1)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(v.outputs[0], out.inputs["Volume"])
    o.data.materials.append(m)
    o.display_type = "WIRE"
    nolines(o)
    return o


def camera(loc, look_at, lens=28):
    bpy.ops.object.camera_add(location=loc)
    cam = bpy.context.active_object
    d = Vector(look_at) - Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    cam.data.clip_end = 1000
    bpy.context.scene.camera = cam
    return cam


def setup_render(path, style, samples=48, res=(1280, 720)):
    sc = bpy.context.scene
    cycles = style == "mix" and os.environ.get("STILL_ENGINE", "cycles") == "cycles"
    if cycles:
        sc.render.engine = "CYCLES"
        cy = sc.cycles
        cy.device = "CPU"
        cy.samples = samples
        cy.use_adaptive_sampling = True
        cy.adaptive_threshold = 0.02
        cy.use_denoising = True
        try:
            cy.denoiser = "OPENIMAGEDENOISE"
        except TypeError:
            pass
        cy.max_bounces = 5
        cy.diffuse_bounces = 3
        cy.glossy_bounces = 3
        cy.transmission_bounces = 2
        cy.volume_bounces = 1
        cy.volume_step_rate = 2.0
        cy.volume_max_steps = 256
        cy.sample_clamp_indirect = 8.0
    else:
        for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
            try:
                sc.render.engine = eng
                break
            except TypeError:
                continue
        try:
            sc.eevee.taa_render_samples = samples
        except Exception:
            pass
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.filepath = path
    sc.render.image_settings.file_format = "PNG"
    if style in ("poster", "mix"):
        if style == "poster":
            sc.view_settings.view_transform = "Standard"
        else:
            try:
                sc.view_settings.view_transform = "AgX"
            except TypeError:
                pass
            sc.view_settings.look = "AgX - Medium High Contrast"
            if style == "mix":
                sc.view_settings.exposure = -0.25
        sc.render.use_freestyle = True
        sc.render.line_thickness = 1.6 * (res[0] / 1280.0 if style == "mix" else 1.0)
        fs = sc.view_layers[0].freestyle_settings
        ls = fs.linesets
        ls = ls[0] if len(ls) else fs.linesets.new("outline")
        if ls.linestyle is None:
            ls.linestyle = bpy.data.linestyles.new('outline')
        objs = []
        for o in NOLINES:
            try:
                if o.name in bpy.data.objects and o not in objs:
                    objs.append(o)
            except ReferenceError:
                pass
        if objs:
            # decals, haze box, wires, grass, far ridges: no outlines
            fc = bpy.data.collections.new("nolines")
            sc.collection.children.link(fc)
            for o in objs:
                for c in list(o.users_collection):
                    c.objects.unlink(o)
                fc.objects.link(o)
            ls.select_by_collection = True
            ls.collection = fc
            ls.collection_negation = "EXCLUSIVE"
        ls.select_silhouette = True
        ls.select_border = True
        ls.select_crease = True
        lsty = ls.linestyle
        lsty.color = lin(P["outline"])[:3]
        lsty.thickness = 2.2 if style == "poster" else 1.1
        if style == "mix":
            # lines recede with the scene: thinner, lighter, hazier with distance
            try:
                t = lsty.thickness_modifiers.new(name="dist", type="DISTANCE_FROM_CAMERA")
                t.range_min, t.range_max = 12.0, 260.0
                t.value_min, t.value_max = 0.45, 1.5
                t.invert = True
                a = lsty.alpha_modifiers.new(name="dist", type="DISTANCE_FROM_CAMERA")
                a.range_min, a.range_max = 60.0, 420.0
                a.invert = True
                c = lsty.color_modifiers.new(name="dist", type="DISTANCE_FROM_CAMERA")
                c.range_min, c.range_max = 15.0, 300.0
                c.color_ramp.elements[0].color = (*lin("#120E0C")[:3], 1)
                c.color_ramp.elements[1].color = (*lin("#6E5A78")[:3], 1)
            except Exception as ex:  # keep rendering with plain lines
                print("line modifiers skipped:", ex)
    else:
        try:
            sc.view_settings.view_transform = "AgX"
        except TypeError:
            pass
        sc.view_settings.look = "AgX - Medium High Contrast"
        sc.render.use_freestyle = False


def finish_mix(im):
    """Final grade for mix stills (PIL image in, PIL image out): a touch more saturation and contrast, same palette."""
    from PIL import ImageEnhance
    im = ImageEnhance.Color(im).enhance(1.14)
    return ImageEnhance.Contrast(im).enhance(1.06)


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()
    del NOLINES[:]
