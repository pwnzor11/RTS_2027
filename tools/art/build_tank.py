"""Build the Authority medium tank procedurally in Blender (bpy) and render 16 facings.

Usage:  python3 tools/art/build_tank.py [--sizes 288 384] [--samples 24] [--frames 0 2]
Writes work files to art/pilot/work/raw_<px>/{col,mask,shadow}_XX.png
  col    = lit colour render, transparent background (house area painted magenta)
  mask   = flat white-on-black emission render of the house-colour area only
  shadow = white ground plane lit by the sun, tank invisible to camera but casting (shadow = darkening)
Frame 0 faces north (up-screen); frames go clockwise in 22.5 deg steps.
Canvas = 1.5 cells; 1 cell = CELL world units. 288 px = 4x of a 48 cell canvas (72), 384 = 4x of 64 (96).
"""
import argparse, math, os, sys, time
import bpy
from mathutils import Vector

CELL = 3.4            # world units per cell (tank hull ~ 1 cell long)
ELEV = 47.0           # camera elevation, degrees
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WORK = os.path.join(ROOT, "art", "pilot", "work")


def lin(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1.0]


def mat(name, hexc, rough=0.75, metal=0.0, emit=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(hexc)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = lin(emit)
        b.inputs["Emission Strength"].default_value = 2.0
    return m


def emission_mat(name, v):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = (v, v, v, 1)
    e.inputs["Strength"].default_value = 1.0
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(e.outputs[0], o.inputs[0])
    return m


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    M = {
        "hull": mat("hull", "#566448"), "hull2": mat("hull2", "#4D5B42"),
        "track": mat("track", "#2B2D29", 0.9), "steel": mat("steel", "#6E747A", 0.5, 0.6),
        "rust": mat("rust", "#7C4A2B", 0.95), "light": mat("light", "#8A9580"),
        "white": mat("white", "#EDEDE2", 0.8), "lens": mat("lens", "#FFF0A8", 0.3, 0, "#FFE9A0"),
        "dark": mat("dark", "#15170F", 0.9),
        "house": mat("house", "#E000E0", 0.75),
    }
    rig = bpy.data.objects.new("rig", None)
    bpy.context.scene.collection.objects.link(rig)
    objs = []

    def add(obj, m):
        if obj.name not in bpy.context.scene.collection.objects:
            bpy.context.scene.collection.objects.link(obj)
        obj.parent = rig
        obj.data.materials.append(M[m])
        objs.append(obj)
        return obj

    def prim_done(o, name):
        for c in list(o.users_collection):
            c.objects.unlink(o)
        o.name = name

    def box(name, size, loc, m, bevel=0.04, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_cube_add(size=1)
        o = bpy.context.active_object
        prim_done(o, name)
        o.scale = size
        bpy.ops.object.transform_apply(scale=True)
        o.location = loc
        o.rotation_euler = [math.radians(r) for r in rot]
        if bevel:
            bm = o.modifiers.new("bev", "BEVEL")
            bm.width = bevel
            bm.segments = 2
        return add(o, m)

    def cyl(name, r, h, loc, m, axis="Z", verts=24):
        bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, vertices=verts)
        o = bpy.context.active_object
        prim_done(o, name)
        o.location = loc
        if axis == "X":
            o.rotation_euler = (0, math.radians(90), 0)
        elif axis == "Y":
            o.rotation_euler = (math.radians(90), 0, 0)
        bpy.ops.object.shade_smooth()
        return add(o, m)

    # ---- tracks (x = +-1.07), height ~0.8
    for s, side in ((-1, "L"), (1, "R")):
        x = s * 1.07
        box("track" + side, (0.62, 2.7, 0.7), (x, 0, 0.4), "track", 0.12)
        cyl("trkF" + side, 0.4, 0.62, (x, 1.35, 0.4), "track", "X")
        cyl("trkR" + side, 0.4, 0.62, (x, -1.35, 0.4), "track", "X")
        for i in range(6):
            y = -1.2 + i * 0.48
            cyl(f"wheel{side}{i}", 0.27, 0.1, (x + s * 0.33, y, 0.4), "steel", "X", 16)
        cyl("sprocket" + side, 0.3, 0.1, (x + s * 0.33, 1.4, 0.45), "rust", "X", 12)
        # skirt plate over upper track + house stripe
        box("skirt" + side, (0.08, 2.9, 0.34), (x + s * 0.34, 0.0, 0.78), "hull", 0.02)
        box("stripe" + side, (0.1, 2.4, 0.12), (x + s * 0.355, 0.0, 0.78), "house", 0.0)
        box("fender" + side, (0.66, 3.0, 0.07), (x, 0, 0.98), "hull2", 0.02)
    # ---- hull
    box("hull", (1.78, 3.0, 0.62), (0, 0, 0.8), "hull", 0.07)
    box("glacis", (1.78, 0.95, 0.1), (0, 1.52, 1.0), "hull", 0.04, rot=(-24, 0, 0))
    box("rearplate", (1.78, 0.08, 0.5), (0, -1.52, 0.8), "hull2", 0.02)
    box("deck", (1.7, 2.5, 0.08), (0, -0.2, 1.14), "hull2", 0.03)
    for i in range(4):
        box(f"vent{i}", (0.7, 0.07, 0.04), (0, -1.45 + i * 0.12, 1.2), "dark", 0.0)
    box("toolbox", (0.34, 0.5, 0.18), (-0.62, -0.95, 1.25), "rust", 0.03)
    box("shovel", (0.16, 0.9, 0.04), (0.65, -0.75, 1.2), "steel", 0.0)
    # searchlight
    cyl("slbase", 0.07, 0.15, (0.62, 1.05, 1.24), "dark", "Z", 8)
    cyl("slbody", 0.12, 0.2, (0.62, 1.12, 1.4), "steel", "Y", 12)
    cyl("sllens", 0.095, 0.04, (0.62, 1.23, 1.4), "lens", "Y", 12)
    # ---- reactor housing with ribbed heat-sink collar
    ry = -1.0
    cyl("react", 0.4, 0.78, (0, ry, 1.5), "light", "Z", 28)
    for i in range(5):
        cyl(f"rib{i}", 0.5, 0.05, (0, ry, 1.38 + i * 0.1), "steel", "Z", 28)
    cyl("rcap", 0.3, 0.06, (0, ry, 1.92), "dark", "Z", 20)
    cyl("rpipe", 0.05, 0.3, (0.22, ry, 2.0), "steel", "Z", 8)
    cyl("rband", 0.42, 0.05, (0, ry, 1.2), "rust", "Z", 28)
    # ---- turret (low, long gun)
    ty = -0.1
    box("turret", (1.5, 1.7, 0.5), (0, ty, 1.43), "hull", 0.16)
    box("turtop", (1.1, 1.2, 0.06), (0, ty - 0.05, 1.7), "hull2", 0.03)
    for s in (-1, 1):
        box("panel" + ("L" if s < 0 else "R"), (0.05, 1.1, 0.34), (s * 0.755, ty, 1.4), "house", 0.0)
    box("mantlet", (0.55, 0.34, 0.34), (0, ty + 0.9, 1.5), "steel", 0.06)
    cyl("gun", 0.085, 1.6, (0, ty + 1.65, 1.5), "dark", "Y", 14)
    cyl("gunsleeve", 0.12, 0.8, (0, ty + 1.2, 1.5), "steel", "Y", 14)
    box("brake", (0.24, 0.36, 0.22), (0, ty + 2.52, 1.5), "steel", 0.04)
    for i in range(3):
        box(f"slot{i}", (0.26, 0.04, 0.12), (0, ty + 2.42 + i * 0.1, 1.5), "dark", 0.0)
    cyl("cupola", 0.2, 0.14, (-0.35, ty - 0.45, 1.78), "hull2", "Z", 16)
    cyl("hatch", 0.16, 0.05, (0.35, ty - 0.2, 1.74), "steel", "Z", 16)
    # stencilled number on both turret sides
    for s in (-1, 1):
        bpy.ops.object.text_add()
        t = bpy.context.active_object
        prim_done(t, "num" + str(s))
        t.data.body = "07"
        t.data.size = 0.36
        t.data.extrude = 0.01
        t.data.align_x = "CENTER"
        t.data.align_y = "CENTER"
        t.rotation_euler = (math.radians(90), 0, math.radians(90 if s > 0 else -90))
        t.location = (s * 0.79, ty, 1.4)
        add(t, "white")
    return rig, objs, M


def setup_scene(size, samples):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = False
    sc.cycles.use_adaptive_sampling = False
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = size
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.render.threads = 2
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.65, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    sc.world = w
    # sun upper-left on screen: from -X, slightly away (+Y), high => shadows fall right/down-screen
    sun = bpy.data.lights.new("sun", "SUN")
    sun.energy = 4.5
    sun.angle = math.radians(4)
    so = bpy.data.objects.new("sun", sun)
    sc.collection.objects.link(so)
    d = Vector((-0.8, 0.22, 1.1)).normalized()
    so.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.cameras.new("cam")
    cam.type = "ORTHO"
    cam.ortho_scale = CELL * 1.5
    co = bpy.data.objects.new("cam", cam)
    sc.collection.objects.link(co)
    el = math.radians(ELEV)
    tgt = Vector((0, 0.5, 0.7))
    co.location = tgt + Vector((0, -math.cos(el), math.sin(el))) * 30
    co.rotation_euler = (tgt - co.location).to_track_quat("-Z", "Y").to_euler()
    cam.clip_end = 100
    sc.camera = co
    bpy.ops.mesh.primitive_plane_add(size=30)
    g = bpy.context.active_object
    g.name = "ground"
    gm = bpy.data.materials.new("gm")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.8, 0.8, 0.8, 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    g.data.materials.append(gm)
    return sc, g, w


def render_to(sc, path):
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sizes", type=int, nargs="+", default=[288, 384])
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--frames", type=int, nargs="*", default=None)
    args = ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:])
    rig, objs, M = build()
    sc, ground, world = setup_scene(args.sizes[0], args.samples)
    mask_on = emission_mat("m1", 1.0)
    mask_off = emission_mat("m0", 0.0)
    orig = {o: list(o.data.materials) for o in objs}
    t0 = time.time()
    frames = args.frames if args.frames else range(16)
    for size in args.sizes:
        sc.render.resolution_x = sc.render.resolution_y = size
        out = os.path.join(WORK, f"raw_{size}")
        os.makedirs(out, exist_ok=True)
        for f in frames:
            rig.rotation_euler = (0, 0, -math.radians(22.5 * f))
            bpy.context.view_layer.update()
            ground.hide_render = True
            for o in objs:
                o.visible_camera = True
                o.data.materials.clear()
                for m in orig[o]:
                    o.data.materials.append(m)
            sc.cycles.samples = args.samples
            render_to(sc, os.path.join(out, f"col_{f:02d}.png"))
            for o in objs:
                o.data.materials.clear()
                o.data.materials.append(mask_on if orig[o][0] is M["house"] else mask_off)
            wbg = world.node_tree.nodes["Background"].inputs[1]
            old = wbg.default_value
            wbg.default_value = 0.0
            sc.cycles.samples = 1
            render_to(sc, os.path.join(out, f"mask_{f:02d}.png"))
            wbg.default_value = old
            ground.hide_render = False
            for o in objs:
                o.visible_camera = False
            sc.cycles.samples = max(8, args.samples // 2)
            render_to(sc, os.path.join(out, f"shadow_{f:02d}.png"))
            print(f"size {size} frame {f} done  t={time.time() - t0:.1f}s", flush=True)
    print(f"TOTAL_RENDER_SECONDS {time.time() - t0:.1f}")


main()
