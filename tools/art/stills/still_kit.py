"""Scene kit for the act-break stills (M8-14): primitives-built low-poly props, house palette, two render styles.

Style "game": Principled materials, warm low sun, clean render (matches the sprite kit's grey-green/steel/rust palette).
Style "poster": ShaderToRGB banded (toon) materials + Freestyle outlines, limited palette.
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


def mat(key, hexc=None, rough=0.85, emit=None):
    """Material for the current STYLE.  key may be a P[] name or a hex colour."""
    hexc = hexc or P.get(key, key)
    k = (STYLE, hexc, rough, emit)
    if k in _mats:
        return _mats[k]
    m = bpy.data.materials.new(f"m_{len(_mats)}")
    m.use_nodes = True
    nt = m.node_tree
    if emit:
        b = nt.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = lin(hexc)
        b.inputs["Emission Color"].default_value = lin(emit)
        b.inputs["Emission Strength"].default_value = 3.0
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


def _assign(o, m):
    o.data.materials.append(m)
    return o


def box(name, loc, size, m, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
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
    _assign(o, mat("sand"))
    return o


def dirt_patches(rng, n=40, extent=40):
    """Darker and lighter flat patches so the ground is not one flat colour."""
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
    o = box("road", (mid.x, mid.y, 0.02), (d.length, width, 0.04), m or mat("asphalt", "#3A3F38"), rot=(0, 0, ang))
    # worn centre line (dashes)
    n = int(d.length // 4)
    for i in range(n):
        t = (i + 0.5) / n
        pt = a + d * t
        box("dash", (pt.x, pt.y, 0.05), (1.6, 0.14, 0.02), mat("bone", "#A8A78F"), rot=(0, 0, ang))
    return o


def junction(center=(0, 0), width=7):
    cx, cy = center
    road((cx - 120, cy), (cx + 120, cy), width)      # east-west
    road((cx, cy - 20), (cx, cy + 160), width)       # south-north (to the horizon)
    box("jpatch", (cx, cy, 0.045), (width, width, 0.03), mat("asphalt", "#343B33"))


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


def pylon(loc, h=14):
    x, y = loc
    m = mat("steel_d")
    cyl("pylon", (x, y, h / 2), 0.25, h, m, verts=6)
    box("arm", (x, y, h * 0.92), (4, 0.2, 0.2), m)
    box("arm2", (x, y, h * 0.78), (3, 0.2, 0.2), m)


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


def barrel_drum(loc, m=None):
    cyl("drum", (loc[0], loc[1], 0.45), 0.35, 0.9, m or mat("rust"), verts=10)


# ------------------------------------------------------------------------------------------------ atmosphere
def smoke_column(loc, h=45, r0=2.5, n=9, tint="#3A3733", rng=None):
    """Stack of dark puffs drifting to the west (towards camera-left)."""
    import random
    rng = rng or random.Random(5)
    x, y = loc
    for i in range(n):
        t = i / (n - 1)
        o = sphere("smoke", (x - t * t * 18 + rng.uniform(-1.5, 1.5), y + rng.uniform(-1.5, 1.5), 2 + t * h),
                   r0 * (1 + t * 2.2), mat(tint, rough=1.0), scale=(1, 1, 0.8))


def dust_cloud(loc, n=7, length=12, rng=None, tint="#A08C6A"):
    """Billowing dust plume above/behind the column: puffs rise and spread with distance so it reads as a cloud."""
    import random
    rng = rng or random.Random(7)
    x, y = loc
    for i in range(n):
        t = i / max(1, n - 1)
        r = 3.0 + 4.0 * (1 - t) + rng.uniform(0, 2)
        sphere("dust", (x + rng.uniform(-7, 7), y - t * length, 2 + rng.uniform(0, 1) * (6 + 12 * (1 - t))),
               r, mat(tint, rough=1.0), scale=(1.5, 1.0, 0.8))


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
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.filepath = path
    sc.render.image_settings.file_format = "PNG"
    try:
        sc.eevee.taa_render_samples = samples
    except Exception:
        pass
    if style == "poster":
        sc.view_settings.view_transform = "Standard"
        sc.render.use_freestyle = True
        sc.render.line_thickness = 1.6
        ls = sc.view_layers[0].freestyle_settings.linesets
        ls = ls[0] if len(ls) else sc.view_layers[0].freestyle_settings.linesets.new("outline")
        if ls.linestyle is None:
            ls.linestyle = bpy.data.linestyles.new('outline')
        ls.select_silhouette = True
        ls.select_border = True
        ls.select_crease = True
        ls.linestyle.color = lin(P["outline"])[:3]
        ls.linestyle.thickness = 2.2
    else:
        try:
            sc.view_settings.view_transform = "AgX"
        except TypeError:
            pass
        sc.view_settings.look = "AgX - Medium High Contrast"
        sc.render.use_freestyle = False


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()
