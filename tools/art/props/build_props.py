"""Procedural Blender props (M10-07 / M12-15): WreckHulk first (pilot), other kinds follow after review.

Same world as units and buildings: orthographic camera at 40 deg elevation, 3.4 world units per 64 px cell, the tank sun (design 18 section 7),
4x supersampled colour pass + shadow pass, box downscale, 1-bit alpha, 1 px outline, checker-dithered hard shadow (same rules as
tools/art/zeroad/render_props.py and tools/art/structpost.py). Frame 64 px, origin (32, 40), like the 0 A.D. prop atlas.
All geometry is built here from primitives: no imported meshes, no third-party art.

Usage (inside the shared Blender lock):
  flock /home/claude/wt/blender.lock python3 tools/art/props/build_props.py render [--kind WreckHulk] [--samples 32]
  python3 tools/art/props/build_props.py post [--kind WreckHulk] [--theater rustbelt]
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


def lin(c):
    f = lambda v: (v / 255) ** 2.2
    return (f(c[0]), f(c[1]), f(c[2]), 1)


def make_mat(name, base, rough=0.85, noise=None, metal=0.0):
    """Principled material; noise = (dark_hex, scale) mixes a darker/rusty tone in by a noise texture."""
    import bpy
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if noise:
        dark, scale = noise
        tc = nt.nodes.new("ShaderNodeTexCoord")
        nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = scale; nz.inputs["Detail"].default_value = 2
        cr = nt.nodes.new("ShaderNodeValToRGB")
        cr.color_ramp.elements[0].position = 0.35; cr.color_ramp.elements[1].position = 0.75
        cr.color_ramp.elements[0].color = lin(dark); cr.color_ramp.elements[1].color = lin(base)
        nt.links.new(tc.outputs["Object"], nz.inputs["Vector"]); nt.links.new(nz.outputs["Fac"], cr.inputs["Fac"])
        nt.links.new(cr.outputs["Color"], b.inputs["Base Color"])
    else:
        b.inputs["Base Color"].default_value = lin(base)
    return m


def box(sc, mat, loc, size, rot=(0, 0, 0), bevel=0.04):
    import bpy
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.scale = size; o.rotation_euler = tuple(math.radians(a) for a in rot)
    bpy.ops.object.transform_apply(scale=True)
    if bevel:
        md = o.modifiers.new("bv", "BEVEL"); md.width = bevel; md.segments = 1
    o.data.materials.append(mat)
    return o


def cyl(sc, mat, loc, r, depth, rot=(0, 0, 0), verts=14):
    import bpy
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=depth, vertices=verts, location=loc, rotation=tuple(math.radians(a) for a in rot))
    o = bpy.context.active_object
    o.data.materials.append(mat)
    return o


def wedge(sc, mat, loc, size, rot=(0, 0, 0)):
    """A box with the top-front edge pulled back: a sloped plate (tank glacis, car windscreen). size = (x, y, z); front = -y."""
    import bpy, bmesh
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    bm = bmesh.new(); bm.from_mesh(o.data)
    for v in bm.verts:
        if v.co.z > 0 and v.co.y < 0: v.co.y += 0.7
    bm.to_mesh(o.data); bm.free()
    o.scale = size; o.rotation_euler = tuple(math.radians(a) for a in rot)
    bpy.ops.object.transform_apply(scale=True)
    o.data.materials.append(mat)
    return o


# ----------------------------------------------------------------------------------------------------------------- WreckHulk geometry
# Units: world units; 1 cell = 3.4. Length along x, depth along y, up = z. Camera looks from -y.
# Hard rule: every wreck is dead (burnt, stripped, lopsided) and low: tallest point about 0.45 cell so it does not read as a live unit.

PAL = {
    "rust": ("#8a4a2a", "#3b2418"), "olive": ("#5d6340", "#2b2f1e"), "grey": ("#6e6f6c", "#2e2f2f"),
    "black": ("#1c1a19", "#0d0c0c"), "tyre": ("#202020", "#101010"), "steel": ("#7b7f82", "#35383a"),
}


def mats():
    return {k: make_mat(f"w_{k}", hx(v[0]), noise=(hx(v[1]), 2.5 + (i % 3) * 1.5), rough=0.9, metal=0.15 if k in ("steel", "rust") else 0.0)
            for i, (k, v) in enumerate(PAL.items())}


def wreck_car(sc, m):
    """Variant 0: a burnt-out saloon car, 0.62 cell long. Doors hanging, bonnet up, two wheels gone, roof caved."""
    L = 0.62 * CELL
    box(sc, m["rust"], (0, 0, 0.22), (L, 0.30 * CELL, 0.20), bevel=0.05)                       # lower body
    box(sc, m["black"], (0.02 * L, 0, 0.40), (L * 0.46, 0.27 * CELL, 0.14), rot=(0, 3, 0), bevel=0.05)  # burnt cabin, roof sagging
    box(sc, m["rust"], (-0.27 * L, 0, 0.42), (L * 0.28, 0.29 * CELL, 0.03), rot=(0, -28, 0), bevel=0.01)   # bonnet flung up
    box(sc, m["grey"], (0.30 * L, 0.0, 0.34), (L * 0.22, 0.29 * CELL, 0.05), rot=(0, 10, 0), bevel=0.01)  # boot lid ajar
    box(sc, m["rust"], (0.05 * L, -0.19 * CELL, 0.20), (L * 0.30, 0.03, 0.16), rot=(0, 0, 14), bevel=0.01)  # door hanging off, near side
    for x, y in ((-0.30 * L, -0.15 * CELL), (0.30 * L, 0.15 * CELL)):                           # two wheels left
        cyl(sc, m["tyre"], (x, y, 0.17), 0.17, 0.13, rot=(90, 0, 0))
    cyl(sc, m["tyre"], (0.1 * L, -0.34 * CELL, 0.04), 0.17, 0.12, rot=(8, 0, 0))                # a loose tyre on the ground


def wreck_truck(sc, m):
    """Variant 1: a truck, 0.95 cell long. Cab leaning, flat bed with split sides, one axle collapsed."""
    L = 0.95 * CELL
    box(sc, m["steel"], (0.05 * L, 0, 0.17), (L * 0.88, 0.28 * CELL, 0.07), bevel=0.03)          # chassis
    box(sc, m["olive"], (-0.32 * L, 0, 0.42), (L * 0.26, 0.31 * CELL, 0.34), rot=(0, 0, 0), bevel=0.06)   # cab
    box(sc, m["black"], (-0.40 * L, 0, 0.50), (L * 0.06, 0.26 * CELL, 0.14), bevel=0.02)         # blown-out windscreen
    box(sc, m["olive"], (-0.20 * L, 0, 0.62), (L * 0.14, 0.30 * CELL, 0.04), rot=(0, -4, 0), bevel=0.01)  # roof
    box(sc, m["rust"], (0.22 * L, 0, 0.28), (L * 0.54, 0.31 * CELL, 0.04), rot=(0, 2, 0), bevel=0.01)     # flat bed
    box(sc, m["rust"], (0.22 * L, -0.15 * CELL, 0.36), (L * 0.54, 0.025, 0.13), rot=(14, 0, 0), bevel=0.01)  # side board splayed out
    box(sc, m["black"], (0.40 * L, 0.14 * CELL, 0.33), (L * 0.20, 0.10 * CELL, 0.10), rot=(0, 0, 25), bevel=0.02)  # scorched cargo
    for x in (-0.30 * L, 0.34 * L):
        cyl(sc, m["tyre"], (x, 0.17 * CELL, 0.19), 0.19, 0.16, rot=(90, 0, 0))
    cyl(sc, m["tyre"], (-0.30 * L, -0.19 * CELL, 0.19), 0.19, 0.16, rot=(90, 0, 0))
    box(sc, m["steel"], (0.34 * L, -0.16 * CELL, 0.10), (0.30, 0.30 * CELL, 0.07), rot=(0, 0, 8), bevel=0.02)   # collapsed axle, wheel gone


def wreck_tank(sc, m):
    """Variant 2: a tank hull, 0.92 cell long: tracks, a sloped glacis, an empty turret ring, the turret thrown to one side, barrel lying free."""
    L = 0.92 * CELL; W = 0.62 * CELL
    for s in (-1, 1):
        box(sc, m["tyre"], (0, s * 0.255 * CELL, 0.15), (L, 0.15 * CELL, 0.20), bevel=0.04)       # tracks
        box(sc, m["black"], (0, s * 0.255 * CELL, 0.27), (L * 0.97, 0.16 * CELL, 0.05), bevel=0.02)                   # mudguards
    box(sc, m["olive"], (0.02 * L, 0, 0.30), (L * 0.80, 0.34 * CELL, 0.20), bevel=0.04)                              # hull
    wedge(sc, m["olive"], (-0.43 * L, 0, 0.32), (L * 0.20, 0.30 * CELL, 0.20), rot=(0, 0, 0))                          # glacis (front = -x side)
    box(sc, m["black"], (0.20 * L, 0, 0.44), (L * 0.30, 0.26 * CELL, 0.05), bevel=0.02)                              # scorched engine deck
    cyl(sc, m["steel"], (-0.05 * L, 0, 0.42), 0.30 * CELL * 0.55, 0.06, verts=16)                                    # turret ring (empty)
    cyl(sc, m["olive"], (0.12 * L, 0.46 * CELL, 0.18), 0.30 * CELL * 0.55, 0.20, rot=(0, 18, 0), verts=16)           # turret thrown clear, tilted
    cyl(sc, m["steel"], (-0.35 * L, 0.52 * CELL, 0.07), 0.045 * CELL, 0.50 * CELL, rot=(0, 90, 12))                  # barrel lying on the ground
    box(sc, m["rust"], (0.38 * L, -0.12 * CELL, 0.46), (L * 0.10, 0.12 * CELL, 0.02), rot=(0, -25, 8), bevel=0.01)   # peeled plate


VARIANTS = [wreck_car, wreck_truck, wreck_tank]
YAW = (-18, 12, -8)    # degrees about z: wrecks sit slightly off-axis


def render(kind, samples):
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
    gm = bpy.data.materials.new("gm"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.8, 0.8, 0.8, 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    ground.data.materials.append(gm)
    os.makedirs(RAW, exist_ok=True)
    m = mats()
    for var, fn in enumerate(VARIANTS):
        before = set(bpy.data.objects)
        fn(sc, m)
        new = [o for o in bpy.data.objects if o not in before]
        root = bpy.data.objects.new("root", None); sc.collection.objects.link(root)
        for o in new: o.parent = root
        root.rotation_euler = (0, 0, math.radians(YAW[var]))
        bpy.context.view_layer.update()
        ground.hide_render = True
        for o in new: o.visible_camera = True
        sc.cycles.samples = samples
        sc.render.filepath = os.path.join(RAW, f"{kind}_{var}_col.png"); bpy.ops.render.render(write_still=True)
        ground.hide_render = False
        for o in new: o.visible_camera = False
        sc.cycles.samples = max(8, samples // 2)
        sc.render.filepath = os.path.join(RAW, f"{kind}_{var}_sh.png"); bpy.ops.render.render(write_still=True)
        for o in new: bpy.data.objects.remove(o)
        bpy.data.objects.remove(root)
        print("rendered", kind, var, flush=True)


# ----------------------------------------------------------------------------------------------------------------- pixel post (numpy / PIL)

# per theater: grade = (saturation keep, value gain). Wrecks keep their own hue (rust, olive, grey) but get dusted toward the theater.
GRADE = {
    "rustbelt":   dict(sat=0.80, gain=1.05, dust="#8E8371", dust_mix=0.10),
    "glasscoast": dict(sat=0.65, gain=1.10, dust="#B7C2BD", dust_mix=0.14),
    "mountains":  dict(sat=0.60, gain=1.00, dust="#787C84", dust_mix=0.12),
    "ashplains":  dict(sat=0.55, gain=0.90, dust="#6B6264", dust_mix=0.14),
}
LEVELS = 5


def post_frame(kind, var, theater):
    import numpy as np
    import colorsys
    from PIL import Image, ImageFilter
    g = GRADE[theater]
    col = np.asarray(Image.open(os.path.join(RAW, f"{kind}_{var}_col.png")).convert("RGBA"), dtype=float)
    shd = np.asarray(Image.open(os.path.join(RAW, f"{kind}_{var}_sh.png")).convert("RGBA"), dtype=float)
    k = SS
    bm = lambda x: x.reshape(x.shape[0] // k, k, x.shape[1] // k, k, *x.shape[2:]).mean(axis=(1, 3))
    a = col[..., 3] / 255
    A = bm(a)
    base = bm(col[..., :3] * a[..., None]) / np.maximum(A[..., None], 1e-6) / 255
    solid = A >= 0.5
    dust = np.array(hx(g["dust"])) / 255
    out = np.zeros((FRAME, FRAME, 4), dtype=np.uint8)
    ys, xs = np.nonzero(solid)
    for y, x in zip(ys, xs):
        h, s, v = colorsys.rgb_to_hsv(*base[y, x])
        v = min(1.0, v * g["gain"]); s *= g["sat"]
        v = round(v * (LEVELS - 1)) / (LEVELS - 1)          # posterise value: hard pixel-art bands
        s = round(s * 3) / 3
        r, gg, b = colorsys.hsv_to_rgb(h, s, v)
        c = np.array([r, gg, b]) * (1 - g["dust_mix"]) + dust * g["dust_mix"]
        out[y, x] = (*np.clip(c * 255, 0, 255).astype(np.uint8), 255)
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
    return out


def post(kind, theater):
    import numpy as np
    from PIL import Image
    row = np.concatenate([post_frame(kind, v, theater) for v in range(3)], axis=1)
    p = os.path.join(SCRATCH, "out", f"{kind}_{theater}.png")
    os.makedirs(os.path.dirname(p), exist_ok=True)
    Image.fromarray(row, "RGBA").save(p, optimize=True)
    print("wrote", p)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["render", "post"])
    ap.add_argument("--kind", default="WreckHulk")
    ap.add_argument("--theater", default="rustbelt")
    ap.add_argument("--samples", type=int, default=32)
    a = ap.parse_args()
    if a.kind != "WreckHulk":
        sys.exit("only WreckHulk is built so far (pilot)")
    if a.cmd == "render": render(a.kind, a.samples)
    else: post(a.kind, a.theater)
