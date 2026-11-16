"""Procedural Blender props (M10-07 / M12-15): WreckHulk, GlassCrest, IceSlab, BrokenPylon, 3 variants each, static, no facings.

Same world as units and buildings: orthographic camera at 40 deg elevation, 3.4 world units per 64 px cell, the tank sun (design 18 section 7),
4x supersampled colour pass + shadow pass, box downscale, 1-bit alpha, 1 px outline, checker-dithered hard shadow (same rules as
tools/art/zeroad/render_props.py and tools/art/structpost.py). Frame 64 px, origin (32, 40), like the 0 A.D. prop atlas.
All geometry is built here from primitives: no imported meshes, no third-party art.

Usage (render inside the shared Blender lock; the render is theater-neutral, the theater grade happens in `post`):
  flock /home/claude/wt/blender.lock python3 tools/art/props/build_props.py render [--kind WreckHulk|all] [--samples 48]
  python3 tools/art/props/build_props.py atlas      # grade + pack: data/sprites/props/blender/<theater>.png + props.toml (also copied to scratch out/)
  python3 tools/art/props/build_props.py sheets     # contact sheets per theater into scratch/sheets/
Wrecks and pylons never use a live-unit palette: rust, charcoal, bare steel, faded paint only as small patches (lead review, session 29).
"""
import argparse, math, os, random, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SCRATCH = os.environ.get("PROPS_SCRATCH", "/tmp/claude-0/s29-props")
RAW = os.path.join(SCRATCH, "raw")

CELL = 3.4        # world units per cell (tank / structures)
CELL_PX = 64
FRAME = 64
ORIGIN = (32, 40)
ELEV = 40.0
SS = 4


def hx(h):
    h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


OUTLINE = hx("#0d0f0b")
SHADOW = hx("#1a1714")

# ----------------------------------------------------------------------------------------------------------------- scene helpers (bpy)
THEATERS = ("rustbelt", "glasscoast", "mountains", "ashplains")
GROUND = {"rustbelt": "#8E8371", "glasscoast": "#E4DABC", "mountains": "#918572", "ashplains": "#827D52"}   # [land.clear] base of data/theaters/*.toml


def lin(c):
    f = lambda v: (v / 255) ** 2.2
    return (f(c[0]), f(c[1]), f(c[2]), 1)


def make_mat(name, dark, base, patch, scale, rough=0.9, metal=0.0):
    """Flat-banded material: a low-frequency noise through a CONSTANT colour ramp gives big flat regions (dark / base / small patch)."""
    import bpy
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = scale; nz.inputs["Detail"].default_value = 0.0
    cr = nt.nodes.new("ShaderNodeValToRGB"); cr.color_ramp.interpolation = "CONSTANT"
    cr.color_ramp.elements[0].position = 0.0; cr.color_ramp.elements[0].color = lin(hx(dark))
    cr.color_ramp.elements[1].position = 0.40; cr.color_ramp.elements[1].color = lin(hx(base))
    e = cr.color_ramp.elements.new(0.86); e.color = lin(hx(patch))
    nt.links.new(tc.outputs["Object"], nz.inputs["Vector"]); nt.links.new(nz.outputs["Fac"], cr.inputs["Fac"])
    nt.links.new(cr.outputs["Color"], b.inputs["Base Color"])
    return m


def solid_mat(name, col, rough=0.9, metal=0.0, spec=0.5):
    import bpy
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(hx(col)); b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    return m


def mats():
    """Generic, faction-less colours (lead review): rust, charcoal/soot, bare steel; faded paint only as a rare small patch inside them."""
    return {
        "rust": make_mat("w_rust", "#3b2418", "#8a4a2a", "#7d7a62", 2.2, metal=0.1),
        "char": make_mat("w_char", "#0f0e0e", "#26221f", "#4a2a1a", 2.6),
        "steel": make_mat("w_steel", "#3c3f41", "#7d8184", "#6b6f78", 2.0, metal=0.2),
        "soot": solid_mat("w_soot", "#141211"),
        "tyre": solid_mat("w_tyre", "#1b1b1b"),
        "pylon": make_mat("w_pylon", "#1d1b1a", "#3a3836", "#8a4a2a", 1.8, metal=0.1),
        "glass": make_mat("g_glass", "#3f8f88", "#7fd0c4", "#d5fbf3", 2.4, rough=0.12),
        "glassdark": make_mat("g_dark", "#27655f", "#3f8f88", "#7fd0c4", 3.0, rough=0.2),
        "ice": make_mat("i_ice", "#8fb8d0", "#bcdcec", "#eef9ff", 2.0, rough=0.2),
    }


def add(o, mat):
    o.data.materials.append(mat); return o


def box(mat, loc, size, rot=(0, 0, 0), bevel=0.04):
    import bpy
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.scale = size; o.rotation_euler = tuple(math.radians(a) for a in rot)
    bpy.ops.object.transform_apply(scale=True)
    if bevel:
        md = o.modifiers.new("bv", "BEVEL"); md.width = bevel; md.segments = 1
    return add(o, mat)


def cyl(mat, loc, r, depth, rot=(0, 0, 0), verts=14):
    import bpy
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=depth, vertices=verts, location=loc, rotation=tuple(math.radians(a) for a in rot))
    return add(bpy.context.active_object, mat)


def cone(mat, loc, r1, r2, depth, rot=(0, 0, 0), verts=5):
    import bpy
    bpy.ops.mesh.primitive_cone_add(radius1=r1, radius2=r2, depth=depth, vertices=verts, location=loc, rotation=tuple(math.radians(a) for a in rot))
    o = bpy.context.active_object
    for pl in o.data.polygons: pl.use_smooth = False
    return add(o, mat)


def strut(mat, p0, p1, t=0.06, bevel=0.0):
    """A square bar from p0 to p1 (world units)."""
    import bpy
    from mathutils import Vector
    a, b = Vector(p0), Vector(p1); d = b - a
    bpy.ops.mesh.primitive_cube_add(size=1, location=(a + b) / 2)
    o = bpy.context.active_object
    o.scale = (t, t, d.length)
    o.rotation_euler = d.normalized().to_track_quat("Z", "Y").to_euler()
    return add(o, mat)


def wedge(mat, loc, size, rot=(0, 0, 0)):
    """A box with the top front edge pulled back: a sloped plate (glacis). Front = -x after the rotation given by the caller."""
    import bpy, bmesh
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    bm = bmesh.new(); bm.from_mesh(o.data)
    for v in bm.verts:
        if v.co.z > 0 and v.co.x < 0: v.co.x += 0.7
    bm.to_mesh(o.data); bm.free()
    o.scale = size; o.rotation_euler = tuple(math.radians(a) for a in rot)
    bpy.ops.object.transform_apply(scale=True)
    return add(o, mat)


# ----------------------------------------------------------------------------------------------------------------- geometry
# Units: world units; 1 cell = 3.4; length along x, depth along y, up = z. Camera looks from -y at 40 deg. 1 world unit of height = ~14 px.

def wreck_car(m):
    """Variant 0: burnt saloon, 0.62 cell. Bonnet up, doors hanging, two wheels gone; everything touches the body (no loose bits)."""
    L = 0.62 * CELL
    box(m["rust"], (0, 0, 0.22), (L, 0.30 * CELL, 0.20), bevel=0.05)
    box(m["soot"], (0.02 * L, 0, 0.40), (L * 0.46, 0.27 * CELL, 0.14), rot=(0, 3, 0), bevel=0.05)           # burnt-out cabin
    box(m["rust"], (-0.27 * L, 0, 0.42), (L * 0.28, 0.29 * CELL, 0.03), rot=(0, -28, 0), bevel=0.01)       # bonnet flung up
    box(m["char"], (-0.38 * L, 0, 0.30), (L * 0.18, 0.28 * CELL, 0.14), bevel=0.03)                        # scorched engine bay
    box(m["steel"], (0.30 * L, 0.0, 0.34), (L * 0.22, 0.29 * CELL, 0.05), rot=(0, 10, 0), bevel=0.01)      # boot lid ajar
    box(m["steel"], (0.05 * L, -0.19 * CELL, 0.20), (L * 0.30, 0.03, 0.16), rot=(0, 0, 14), bevel=0.01)    # door hanging off
    for x, y in ((-0.30 * L, -0.15 * CELL), (0.30 * L, 0.15 * CELL)):
        cyl(m["tyre"], (x, y, 0.17), 0.17, 0.13, rot=(90, 0, 0))
    box(m["tyre"], (0.30 * L, -0.15 * CELL, 0.06), (0.30, 0.10, 0.10), rot=(0, 0, 10), bevel=0.02)         # bare axle stub, wheel gone


def wreck_truck(m):
    """Variant 1: truck, 0.95 cell. Charred cab, rust bed, splayed side board, collapsed axle."""
    L = 0.95 * CELL
    box(m["steel"], (0.05 * L, 0, 0.17), (L * 0.88, 0.28 * CELL, 0.07), bevel=0.03)
    box(m["rust"], (-0.32 * L, 0, 0.42), (L * 0.26, 0.31 * CELL, 0.34), bevel=0.06)
    box(m["soot"], (-0.41 * L, 0, 0.50), (L * 0.06, 0.26 * CELL, 0.14), bevel=0.02)                       # blown-out windscreen
    box(m["char"], (-0.20 * L, 0, 0.62), (L * 0.14, 0.30 * CELL, 0.05), rot=(0, -4, 0), bevel=0.01)        # sooty roof
    box(m["rust"], (0.22 * L, 0, 0.28), (L * 0.54, 0.31 * CELL, 0.04), rot=(0, 2, 0), bevel=0.01)
    box(m["steel"], (0.22 * L, -0.15 * CELL, 0.36), (L * 0.54, 0.025, 0.13), rot=(14, 0, 0), bevel=0.01)
    box(m["soot"], (0.40 * L, 0.14 * CELL, 0.33), (L * 0.20, 0.10 * CELL, 0.10), rot=(0, 0, 25), bevel=0.02)  # scorched cargo
    for x in (-0.30 * L, 0.34 * L):
        cyl(m["tyre"], (x, 0.17 * CELL, 0.19), 0.19, 0.16, rot=(90, 0, 0))
    cyl(m["tyre"], (-0.30 * L, -0.19 * CELL, 0.19), 0.19, 0.16, rot=(90, 0, 0))
    box(m["steel"], (0.34 * L, -0.16 * CELL, 0.10), (0.30, 0.30 * CELL, 0.07), rot=(0, 0, 8), bevel=0.02)


def wreck_tank(m):
    """Variant 2: generic tank hull, 0.92 cell: bare grey steel, soot engine deck and turret ring, turret thrown clear, barrel on the ground."""
    L = 0.92 * CELL
    for s in (-1, 1):
        box(m["tyre"], (0, s * 0.255 * CELL, 0.15), (L, 0.15 * CELL, 0.20), bevel=0.04)
        box(m["char"], (0, s * 0.255 * CELL, 0.27), (L * 0.97, 0.16 * CELL, 0.05), bevel=0.02)
    box(m["steel"], (0.02 * L, 0, 0.30), (L * 0.80, 0.34 * CELL, 0.20), bevel=0.04)
    wedge(m["steel"], (-0.43 * L, 0, 0.32), (L * 0.20, 0.34 * CELL, 0.20))
    box(m["soot"], (0.20 * L, 0, 0.44), (L * 0.30, 0.26 * CELL, 0.05), bevel=0.02)                          # burnt engine deck
    cyl(m["soot"], (-0.05 * L, 0, 0.42), 0.30 * CELL * 0.55, 0.06, verts=16)                                # sooty empty turret ring
    cyl(m["rust"], (0.12 * L, 0.46 * CELL, 0.18), 0.30 * CELL * 0.55, 0.20, rot=(0, 18, 0), verts=16)       # turret thrown clear
    cyl(m["steel"], (-0.35 * L, 0.52 * CELL, 0.07), 0.045 * CELL, 0.50 * CELL, rot=(0, 90, 12))
    box(m["rust"], (0.38 * L, -0.12 * CELL, 0.46), (L * 0.10, 0.12 * CELL, 0.02), rot=(0, -25, 8), bevel=0.01)


def crest(m, spikes, seed, big):
    """A cluster of fused-glass spikes on a fused lump. Hard-faceted (5-sided cones), leaning, one or two dark inclusions."""
    rnd = random.Random(seed)
    cone(m["glassdark"], (0, 0, 0.08), 0.30 * CELL * big, 0.18 * CELL * big, 0.16, verts=7)
    for i in range(spikes):
        a = 2 * math.pi * i / spikes + rnd.uniform(-0.4, 0.4)
        rr = rnd.uniform(0.08, 0.2) * CELL * big * (0 if i == 0 else 1)
        h = (1.0 + rnd.uniform(0, 0.8) * (1.0 if i else 0)  + (0.9 if i == 0 else 0)) * big * 1.2
        r = rnd.uniform(0.15, 0.22) * CELL * big
        cone(m["glass" if i % 3 != 2 else "glassdark"], (rr * math.cos(a), rr * math.sin(a), 0.1 + h / 2), r, 0.012, h,
             rot=(rnd.uniform(-14, 14), rnd.uniform(-14, 14), rnd.uniform(0, 72)), verts=5)


def crest0(m): crest(m, 3, 11, 0.72)
def crest1(m): crest(m, 5, 23, 0.9)
def crest2(m): crest(m, 7, 37, 1.1)


def slab(m, loc, size, rot, mat="ice"):
    box(m[mat], loc, size, rot=rot, bevel=0.03)


def ice0(m):
    slab(m, (0, 0, 0.17), (0.62 * CELL, 0.42 * CELL, 0.14), (6, -9, 14))
    slab(m, (0.18 * CELL, 0.12 * CELL, 0.12), (0.22 * CELL, 0.18 * CELL, 0.10), (0, 0, 40))                 # chip


def ice1(m):
    slab(m, (-0.10 * CELL, 0.05 * CELL, 0.18), (0.55 * CELL, 0.40 * CELL, 0.16), (-5, 11, -10))
    slab(m, (0.22 * CELL, -0.12 * CELL, 0.34), (0.40 * CELL, 0.30 * CELL, 0.13), (14, -17, 24))             # second slab leaning on the first


def ice2(m):
    slab(m, (-0.20 * CELL, 0.10 * CELL, 0.16), (0.50 * CELL, 0.38 * CELL, 0.14), (4, 8, 8))
    slab(m, (0.24 * CELL, 0.0, 0.26), (0.46 * CELL, 0.34 * CELL, 0.15), (-12, -14, -22))
    slab(m, (0.02 * CELL, -0.26 * CELL, 0.40), (0.34 * CELL, 0.26 * CELL, 0.12), (18, 12, 35))


def pylon(m, height, lean, side, seed):
    """Snapped lattice stub that reads as one solid dark shape at 1x: two thick legs standing (one bent over at the break), two legs snapped near the
    base, one brace and one rung, and two dangling cable stubs. Members are >= 2 px thick at 1x (0.11 world units); `side` picks which pair stands."""
    rnd = random.Random(seed)
    bw, tw = 0.30 * CELL, 0.16 * CELL
    steel = m["pylon"]
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    W = lambda z: bw + (tw - bw) * z / height
    standing = [(0 + side) % 4, (1 + side) % 4]
    for i, (sx, sy) in enumerate(corners):
        z = height if i in standing else height * 0.22
        w = W(z)
        strut(steel, (sx * bw, sy * bw, 0), (sx * w, sy * w, z), 0.24)
    i0, i1 = standing
    (sx0, sy0), (sx1, sy1) = corners[i0], corners[i1]
    w = W(height)
    strut(steel, (sx0 * w, sy0 * w, height), (sx0 * w + 0.5 + lean, sy0 * w - 0.25, height - 0.35), 0.20)   # the bent top of one leg
    zb = height * 0.5
    strut(steel, (sx0 * W(0), sy0 * W(0), 0.05), (sx1 * W(zb), sy1 * W(zb), zb), 0.17)                     # one brace across the standing pair
    strut(steel, (sx0 * W(zb), sy0 * W(zb), zb), (sx1 * W(zb), sy1 * W(zb), zb), 0.20)                    # one rung
    for j, i in enumerate((i1, (i1 + 1) % 4)):                                                             # dangling cables
        sx, sy = corners[i]; z = height if i == i1 else height * 0.22; ww = W(z)
        strut(m["soot"], (sx * ww, sy * ww, z), (sx * (ww + 0.5 + 0.4 * j), sy * (ww + 0.6), 0.05), 0.14)


def pylon0(m): pylon(m, 1.3, 0.2, 0, 5)
def pylon1(m): pylon(m, 2.0, 0.3, 0, 8)
def pylon2(m): pylon(m, 2.7, 0.4, 0, 13)


KINDS = {
    "GlassCrest": dict(fns=[crest0, crest1, crest2], yaw=(0, 25, -15), scale=(1.0, 1.0, 0.9), weather=False, glass=True, scorch=False, exp=0.95),
    "IceSlab": dict(fns=[ice0, ice1, ice2], yaw=(0, 0, 0), scale=(1.0, 0.88, 0.78), weather=False, glass=True, scorch=False, exp=0.92),
    "WreckHulk": dict(fns=[wreck_car, wreck_truck, wreck_tank], yaw=(-18, 12, -8), scale=(1.0, 0.85, 0.78), weather=True, lift=True, glass=False, scorch=True, exp=0.72),
    "BrokenPylon": dict(fns=[pylon0, pylon1, pylon2], yaw=(0, 8, -6), scale=(0.85, 0.72, 0.62), weather=False, lift=True, glass=False, scorch=False, exp=1.2),
}
ROW = {"GlassCrest": 0, "IceSlab": 1, "WreckHulk": 2, "BrokenPylon": 3}   # atlas rows = manifest rows


def render(kinds, samples):
    import bpy
    from mathutils import Vector
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.use_denoising = False; sc.cycles.use_adaptive_sampling = False
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
    sc.render.threads = 2
    W = FRAME * SS
    sc.render.resolution_x = sc.render.resolution_y = W; sc.render.resolution_percentage = 100
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.65, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    sc.world = w
    sun = bpy.data.lights.new("sun", "SUN"); sun.energy = 4.5; sun.angle = math.radians(4)
    so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so)
    d = Vector((-0.8, 0.22, 1.1)).normalized()      # identical to the tank (see tools/art/zeroad/render_props.py)
    so.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.cameras.new("cam"); cam.type = "ORTHO"; cam.sensor_fit = "HORIZONTAL"
    cam.ortho_scale = FRAME / CELL_PX * CELL
    co = bpy.data.objects.new("cam", cam); sc.collection.objects.link(co)
    el = math.radians(ELEV)
    up = Vector((0, math.sin(el), math.cos(el)))
    tgt = up * ((ORIGIN[1] - FRAME / 2) / CELL_PX * CELL)
    co.location = tgt + Vector((0, -math.cos(el), math.sin(el))) * 40
    co.rotation_euler = (tgt - co.location).to_track_quat("-Z", "Y").to_euler()
    cam.clip_end = 120; sc.camera = co
    bpy.ops.mesh.primitive_plane_add(size=60)
    ground = bpy.context.active_object
    gm = solid_mat("gm", "#cccccc", rough=1.0)
    ground.data.materials.append(gm)
    # top-face pass material: white where the world normal points up (z > 0.72), else black; used as a material override
    tm = bpy.data.materials.new("topmask"); tm.use_nodes = True
    nt = tm.node_tree; nt.nodes.clear()
    g = nt.nodes.new("ShaderNodeNewGeometry"); sp = nt.nodes.new("ShaderNodeSeparateXYZ"); gt = nt.nodes.new("ShaderNodeMath"); gt.operation = "GREATER_THAN"
    gt.inputs[1].default_value = 0.88
    em = nt.nodes.new("ShaderNodeEmission"); out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(g.outputs["Normal"], sp.inputs[0]); nt.links.new(sp.outputs["Z"], gt.inputs[0])
    nt.links.new(gt.outputs[0], em.inputs["Strength"]); nt.links.new(em.outputs[0], out.inputs["Surface"])
    em.inputs["Color"].default_value = (1, 1, 1, 1)
    os.makedirs(RAW, exist_ok=True)
    m = mats()
    vl = bpy.context.view_layer
    for kind in kinds:
        spec = KINDS[kind]
        for var, fn in enumerate(spec["fns"]):
            before = set(bpy.data.objects)
            fn(m)
            new = [o for o in bpy.data.objects if o not in before]
            root = bpy.data.objects.new("root", None); sc.collection.objects.link(root)
            for o in new: o.parent = root
            root.rotation_euler = (0, 0, math.radians(spec["yaw"][var])); root.scale = (spec["scale"][var],) * 3   # keeps every frame clear of the 64 px edge
            bpy.context.view_layer.update()
            ground.hide_render = True
            for o in new: o.visible_camera = True
            sc.cycles.samples = samples
            sc.render.filepath = os.path.join(RAW, f"{kind}_{var}_col.png"); bpy.ops.render.render(write_still=True)
            vl.material_override = tm; sc.cycles.samples = 4
            sc.render.filepath = os.path.join(RAW, f"{kind}_{var}_top.png"); bpy.ops.render.render(write_still=True)
            vl.material_override = None
            ground.hide_render = False
            for o in new: o.visible_camera = False
            sc.cycles.samples = max(8, samples // 2)
            sc.render.filepath = os.path.join(RAW, f"{kind}_{var}_sh.png"); bpy.ops.render.render(write_still=True)
            for o in new: bpy.data.objects.remove(o)
            bpy.data.objects.remove(root)
            print("rendered", kind, var, flush=True)


# ----------------------------------------------------------------------------------------------------------------- pixel post (numpy / PIL)
# Base colours never change per theater (rust / charcoal / steel, glass, ice); the grade is weather only:
#   snow / dust on top faces (Bayer-dithered, hard pixels), a coat over everything, a value gain.
GRADE = {
    "rustbelt":   dict(cover=0.15, soft=0.40, cover_col="#dfe4e6", coat=0.10, coat_col="#5c5753", sat=0.90, gain=1.00),   # a little snow on top faces + ash
    "glasscoast": dict(cover=0.15, soft=0.38, cover_col="#d9c996", coat=0.06, coat_col="#d9c996", sat=0.70, gain=1.06),   # sun-bleached, sand-dusted
    "mountains":  dict(cover=0.15, soft=0.40, cover_col="#e8eef2", coat=0.00, coat_col="#ffffff", sat=0.85, gain=1.00),   # snow on top faces
    "ashplains":  dict(cover=0.0, soft=0.0, cover_col="#8f8a87", coat=0.30, coat_col="#6f6a68", sat=0.55, gain=0.88),   # grey ash coat, darker
}
LEVELS = 4


def blob(cx, cy, salt):
    """Deterministic 0..1 value per 3x3 pixel block: snow/dust lands in small flat clusters instead of a regular dither."""
    h = (cx * 73856093) ^ (cy * 19349663) ^ (salt * 83492791) ^ 0x5bd1e995
    h = (h ^ (h >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65536.0
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def mode_filter(lab, solid):
    """3x3 majority over solid pixels (centre wins ties): removes single-pixel speckle, keeps 2-4 px clusters."""
    out = lab.copy()
    H, W = lab.shape
    for y in range(H):
        for x in range(W):
            if not solid[y, x]: continue
            cnt = {}
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < H and 0 <= xx < W and solid[yy, xx]:
                        cnt[lab[yy, xx]] = cnt.get(lab[yy, xx], 0) + 1
            best = max(cnt.values())
            if cnt.get(lab[y, x], 0) < best:
                out[y, x] = min(k for k, v in cnt.items() if v == best)
    return out


def post_frame(kind, var, theater):
    import numpy as np
    import colorsys
    from PIL import Image, ImageFilter
    g = GRADE[theater]; spec = KINDS[kind]
    L = lambda n: np.asarray(Image.open(os.path.join(RAW, f"{kind}_{var}_{n}.png")).convert("RGBA"), dtype=float)
    col, top, shd = L("col"), L("top"), L("sh")
    k = SS
    bm = lambda x: x.reshape(x.shape[0] // k, k, x.shape[1] // k, k, *x.shape[2:]).mean(axis=(1, 3))
    a = col[..., 3] / 255
    A = bm(a)
    base = bm(col[..., :3] * a[..., None]) / np.maximum(A[..., None], 1e-6) / 255
    topm = bm(top[..., 0] / 255 * a) / np.maximum(A, 1e-6) > 0.5
    solid = A >= 0.5
    # 1) posterise value into LEVELS flat bands per hue; lift top faces one band (the readable lighter top band)
    H, W = solid.shape
    rgb = np.zeros((H, W, 3))
    lab = np.zeros((H, W), dtype=np.int64)
    for y, x in zip(*np.nonzero(solid)):
        h, s, v = colorsys.rgb_to_hsv(*base[y, x])
        v = min(1.0, v * g["gain"] * spec["exp"] + (0.10 if topm[y, x] and spec.get("lift") else 0.0))
        v = round(v * (LEVELS - 1)) / (LEVELS - 1)
        s = round(min(1, s * g["sat"]) * 3) / 3
        r, gg, b = colorsys.hsv_to_rgb(h, s, v)
        c = np.array([r, gg, b])
        rgb[y, x] = c
        lab[y, x] = int(c[0] * 255) << 16 | int(c[1] * 255) << 8 | int(c[2] * 255)
    lab = mode_filter(lab, solid)
    for y, x in zip(*np.nonzero(solid)):
        rgb[y, x] = [(lab[y, x] >> 16) & 255, (lab[y, x] >> 8) & 255, lab[y, x] & 255]
    rgb = rgb if rgb.max() > 1.5 else rgb * 255
    # 2) weather: coat over everything, snow / dust on top faces
    coat = np.array(hx(g["coat_col"])); cc = np.array(hx(g["cover_col"]))
    cover = g["cover"] if spec["weather"] else g["cover"] * 0.0
    coatmix = g["coat"] if spec["weather"] else g["coat"] * 0.5
    out = np.zeros((FRAME, FRAME, 4), dtype=np.uint8)
    dusted = 0
    for y, x in zip(*np.nonzero(solid)):
        c = rgb[y, x] * (1 - coatmix) + coat * coatmix
        if topm[y, x] and blob(x // 3, y // 3, var) < cover:
            c = c * (1 - g["soft"]) + cc * g["soft"]       # soft dust / snow tint on upward faces only: the rust / char / steel base stays dominant
            dusted += 1
        out[y, x] = (*np.clip(c, 0, 255).astype(np.uint8), 255)
    assert dusted <= 0.2 * solid.sum(), f"{kind}/{var}/{theater}: dust covers {dusted} of {solid.sum()} px"
    # 3) glints on glass / ice: the brightest pixels of the object become a bright spark, spread out (never adjacent)
    if spec["glass"]:
        lum = np.where(solid, out[..., :3].mean(axis=2), 0)
        thr = np.percentile(lum[solid], 94)
        placed = []
        for y, x in sorted(zip(*np.nonzero(lum >= thr)), key=lambda p: (-lum[p], p)):
            if all(abs(y - py) + abs(x - px) > 5 for py, px in placed) and len(placed) < 6:
                out[y, x] = (244, 255, 250, 255); placed.append((y, x))
    edge = np.zeros_like(solid)
    edge[1:, :] |= solid[:-1, :]; edge[:-1, :] |= solid[1:, :]; edge[:, 1:] |= solid[:, :-1]; edge[:, :-1] |= solid[:, 1:]
    edge &= ~solid
    out[edge] = (*OUTLINE, 255)
    sl = shd[..., :3].mean(axis=2) / 255
    sl = 1 - sl / max(np.percentile(sl, 99.5), 1e-6)
    sl = np.asarray(Image.fromarray((np.clip(sl, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(SS * 0.8)), dtype=float) / 255
    sh = np.clip((bm(sl) - 0.05) / 0.8, 0, 1)
    yy, xx = np.mgrid[0:FRAME, 0:FRAME]
    chk = (xx + yy) % 2 == 0
    sm = (~solid) & (~edge) & ((sh > 0.55) | ((sh > 0.25) & chk))
    out[sm] = (*SHADOW, 255)
    # 4) wrecks: a faint dithered scorch patch on the ground under them (low contrast, so it stays a prop and not a crater decal)
    if spec["scorch"]:
        ys, xs = np.nonzero(solid)
        cx, cy = (xs.min() + xs.max()) / 2, ys.max() - 4
        rx, ry = (xs.max() - xs.min()) / 2 + 3, (xs.max() - xs.min()) * 0.2 + 3
        inside = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2 <= 1
        free = inside & (out[..., 3] == 0)
        sparse = (xx % 2 == 0) & (yy % 2 == 0)
        out[free & sparse] = (*hx("#2c2622"), 255)
    return out


def atlas_for(theater):
    import numpy as np
    from PIL import Image
    img = np.zeros((FRAME * len(ROW), FRAME * 3, 4), dtype=np.uint8)
    for kind, r in ROW.items():
        for v in range(3):
            img[r * FRAME:(r + 1) * FRAME, v * FRAME:(v + 1) * FRAME] = post_frame(kind, v, theater)
    return Image.fromarray(img, "RGBA")


def write_atlas():
    outdirs = [os.path.join(ROOT, "data", "sprites", "props", "blender"), os.path.join(SCRATCH, "out")]
    lines = ["# Blender prop atlas manifest (generated by tools/art/props/build_props.py -- do not hand-edit).",
             "# Original art built from primitives in Blender (AI-assisted, procedural; see docs/CREDITS.md). Same format as the 0 A.D. atlas.",
             f"frame = {FRAME}", f"origin_x = {ORIGIN[0]}", f"origin_y = {ORIGIN[1]}", ""]
    for kind, r in ROW.items():
        lines += ["[[kind]]", f'name = "{kind}"', f"row = {r}", "variants = 3", ""]
    lines += ["# One atlas PNG per theater id; same layout (row = kind above, column = variant).", "[theaters]"]
    for t in THEATERS:
        im = atlas_for(t)
        for d in outdirs:
            os.makedirs(d, exist_ok=True); im.save(os.path.join(d, f"{t}.png"), optimize=True)
        lines.append(f'{t} = "{t}.png"')
    for d in outdirs:
        open(os.path.join(d, "props.toml"), "w", newline="\n").write("\n".join(lines) + "\n")
    print("wrote atlases + manifest to", outdirs)


def live_unit(name, tur=None):
    """A live unit (facing 0) in a 96x96 cell with its ground point at (48, 72), for scale."""
    import re
    from PIL import Image
    vd = os.path.join(ROOT, "data", "sprites", "vehicles")
    t = open(os.path.join(vd, f"{name}.sprite.toml")).read()
    gi = lambda key: int(re.search(rf"^{key} = (\d+)", t, re.M).group(1))
    w, ox, oy = gi("frame_w"), gi("origin_x"), gi("origin_y")
    b = Image.open(os.path.join(vd, f"{name}.png")).convert("RGBA").crop((0, 0, w, w))
    if tur and os.path.exists(os.path.join(vd, f"{tur}.png")):
        b.alpha_composite(Image.open(os.path.join(vd, f"{tur}.png")).convert("RGBA").crop((0, 0, w, w)))
    im = Image.new("RGBA", (96, 96), (0, 0, 0, 0)); im.alpha_composite(b, (48 - ox, 72 - oy)); return im


def write_sheets():
    from PIL import Image
    os.makedirs(os.path.join(SCRATCH, "sheets"), exist_ok=True)
    tank = live_unit("auth_tank", "auth_tank_turret")
    for t in THEATERS:
        bg = hx(GROUND[t]) + (255,)
        at = atlas_for(t)
        S = Image.new("RGBA", (300 + 3 * 74 + 3 * 200 + 20, 4 * 200 + 10), bg)
        S.alpha_composite(tank, (10, 10)); S.alpha_composite(tank.resize((288, 288), Image.NEAREST), (10, 110))
        for kind, r in ROW.items():
            for v in range(3):
                fr = at.crop((v * FRAME, r * FRAME, (v + 1) * FRAME, (r + 1) * FRAME))
                S.alpha_composite(fr, (310 + v * 74, 10 + r * 200))
                S.alpha_composite(fr.resize((192, 192), Image.NEAREST), (310 + 3 * 74 + 10 + v * 200, 5 + r * 200))
        p = os.path.join(SCRATCH, "sheets", f"sheet_{t}.png"); S.save(p); print("wrote", p, S.size)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["render", "atlas", "sheets"])
    ap.add_argument("--kind", default="all")
    ap.add_argument("--samples", type=int, default=48)
    a = ap.parse_args()
    kinds = list(KINDS) if a.kind == "all" else a.kind.split(",")
    if a.cmd == "render": render(kinds, a.samples)
    elif a.cmd == "atlas": write_atlas()
    else: write_sheets()
