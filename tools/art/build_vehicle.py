"""Build a vehicle procedurally in Blender (bpy), render every frame and facing, then pixel-post-process it.

Usage:  python3 tools/art/build_vehicle.py <id> [--samples 24] [--states idle harvest] [--facings 0 4 ...]
        [--render-only | --post-only] [--preview]
Wrap renders in:  flock /home/claude/wt/blender.lock python3 tools/art/build_vehicle.py <id>
<id> names a module in tools/art/vehicles/<id>.py (contract in the docstring of vehicles/auth_jeep.py).

Same pipeline as the approved v2 tank (arm B, D-039/D-043, 40 deg camera D-064): ortho camera, sun upper-left, 4x render
-> box downscale -> fixed 32-colour palette incl. the 8 exact magenta house shades -> 1 px outline -> dithered hard shadow.
64 px per map cell: ortho_scale = CELL * canvas / 64, so a bigger canvas only adds room, never changes the scale.
Frame 0 faces north (+Y, up-screen); facings run clockwise in 22.5 deg steps. Sheet index = first + facing * frames + k,
4 columns, row-major (so with 4 frames per facing one facing is one sheet row).
Work files: art/pilot/work/veh_<id>/{col,mask,shadow}_XX.png (git-ignored).  The tank keeps its own script (build_tank.py).
"""
import argparse, importlib.util, math, os, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import common as C

CELL = 3.4
ELEV = 40.0
SS = 4
COLS = 4

# shared materials: name -> (hex, kwargs).  Modules may add or override via MATERIALS.
BASE_MATERIALS = {
    "hull": ("#566448", {}), "hull2": ("#4D5B42", {}), "track": ("#2B2D29", dict(rough=0.9)),
    "steel": ("#6E747A", dict(rough=0.5, metal=0.6)), "rust": ("#7C4A2B", dict(rough=0.95)),
    "light": ("#8A9580", {}), "white": ("#EDEDE2", dict(rough=0.8)),
    "lens": ("#FFF0A8", dict(rough=0.3, emit="#FFE9A0")), "dark": ("#15170F", dict(rough=0.9)),
    "glass": ("#2C3A3E", dict(rough=0.25, metal=0.3)),
    "yellow": ("#C9A640", {}), "lamp_off": ("#5A4A22", {}),
    "ash": ("#2B2B28", dict(rough=1.0)), "ash2": ("#3E3D38", dict(rough=1.0)),
    "puff": ("#6E695F", dict(rough=1.0)),
}


def load_module(vid):
    p = os.path.join(HERE, "vehicles", f"{vid}.py")
    spec = importlib.util.spec_from_file_location(f"vehicles_{vid}", p)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def work_dir(vid):
    return os.path.join(C.WORK, f"veh_{vid}")


def layout(mod):
    """[(state, first, facings, frames, ticks)] with states packed in order."""
    out, first = [], 0
    for name, facings, frames, ticks in mod.STATES:
        out.append((name, first, facings, frames, ticks))
        first += facings * frames
    return out


def origin_px(mod):
    """Pixel of the ground origin (0,0,0) in a frame, from the camera target."""
    el = math.radians(ELEV)
    tx, ty, tz = mod.TARGET
    up = -ty * math.sin(el) - tz * math.cos(el)
    cv = mod.CANVAS
    return cv // 2, round(cv / 2 - up / (CELL * cv / 64) * cv)


# ----------------------------------------------------------------------------------------------- Blender side
def render(vid, mod, samples, states, facing_sel, preview):
    import bpy
    from mathutils import Vector

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

    bpy.ops.wm.read_factory_settings(use_empty=True)
    specs = dict(BASE_MATERIALS)
    specs.update(getattr(mod, "MATERIALS", {}))
    M = {n: mat(n, s[0], **s[1]) for n, s in specs.items()}
    M["house"] = mat("house", "#E000E0", 0.75)   # house paint is always magenta
    mask_on, mask_off = emission_mat("m1", 1.0), emission_mat("m0", 0.0)

    sc = bpy.context.scene
    cv = mod.CANVAS
    px = cv * SS
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.use_denoising = False
    sc.cycles.use_adaptive_sampling = False
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = px // 2 if preview else px
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
    sun = bpy.data.lights.new("sun", "SUN")
    sun.energy = 4.5
    sun.angle = math.radians(4)
    so = bpy.data.objects.new("sun", sun)
    sc.collection.objects.link(so)
    d = Vector((-0.8, 0.22, 1.1)).normalized()
    so.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.cameras.new("cam")
    cam.type = "ORTHO"
    cam.ortho_scale = CELL * cv / 64
    cam.clip_end = 100
    co = bpy.data.objects.new("cam", cam)
    sc.collection.objects.link(co)
    el = math.radians(ELEV)
    tgt = Vector(mod.TARGET)
    co.location = tgt + Vector((0, -math.cos(el), math.sin(el))) * 30
    co.rotation_euler = (tgt - co.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = co
    bpy.ops.mesh.primitive_plane_add(size=30)
    ground = bpy.context.active_object
    ground.name = "ground"
    gm = bpy.data.materials.new("gm")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.8, 0.8, 0.8, 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    ground.data.materials.append(gm)
    rig = bpy.data.objects.new("rig", None)
    sc.collection.objects.link(rig)

    objs = []

    class Ctx:
        """Primitive helpers handed to the vehicle module. x east, y north (front), z up; 3.4 units = 1 cell."""

    S = Ctx()
    S.CELL = CELL

    def finish(o, name, m, nocast):
        for c in list(o.users_collection):
            c.objects.unlink(o)
        o.name = name
        sc.collection.objects.link(o)
        o.parent = rig
        o.data.materials.append(M[m])
        if nocast:
            o.visible_shadow = False
        objs.append(o)
        return o

    def box(name, size, loc, m, bevel=0.03, rot=(0, 0, 0), nocast=False):
        bpy.ops.mesh.primitive_cube_add(size=1)
        o = bpy.context.active_object
        o.scale = size
        bpy.ops.object.transform_apply(scale=True)
        o.location = loc
        o.rotation_euler = [math.radians(r) for r in rot]
        if bevel:
            bm = o.modifiers.new("bev", "BEVEL")
            bm.width = bevel
            bm.segments = 1
        return finish(o, name, m, nocast)

    def cyl(name, r, h, loc, m, axis="Z", verts=24, nocast=False, rot=None):
        bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, vertices=verts)
        o = bpy.context.active_object
        o.location = loc
        if rot is not None:
            o.rotation_euler = [math.radians(r_) for r_ in rot]
        elif axis == "X":
            o.rotation_euler = (0, math.radians(90), 0)
        elif axis == "Y":
            o.rotation_euler = (math.radians(90), 0, 0)
        bpy.ops.object.shade_smooth()
        return finish(o, name, m, nocast)

    def sphere(name, r, loc, m, scale=(1, 1, 1), nocast=False):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, segments=16, ring_count=10)
        o = bpy.context.active_object
        o.scale = scale
        bpy.ops.object.transform_apply(scale=True)
        o.location = loc
        bpy.ops.object.shade_smooth()
        return finish(o, name, m, nocast)

    def cone(name, r1, r2, h, loc, m, verts=16):
        bpy.ops.mesh.primitive_cone_add(radius1=r1, radius2=r2, depth=h, vertices=verts)
        o = bpy.context.active_object
        o.location = loc
        bpy.ops.object.shade_smooth()
        return finish(o, name, m, False)

    S.box, S.cyl, S.sphere, S.cone = box, cyl, sphere, cone

    def render_to(path):
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)

    out = work_dir(vid)
    os.makedirs(out, exist_ok=True)
    wbg = w.node_tree.nodes["Background"].inputs[1]
    t0 = time.time()
    for name, first, facings, frames, _t in layout(mod):
        if states and name not in states:
            continue
        for k in range(frames):
            for o in objs:
                bpy.data.objects.remove(o, do_unlink=True)
            objs.clear()
            mod.build(S, name, k, frames)
            orig = {o: list(o.data.materials) for o in objs}
            for f in (facing_sel if facing_sel else range(facings)):
                idx = first + f * frames + k
                rig.rotation_euler = (0, 0, -math.radians(360.0 / facings * f))
                bpy.context.view_layer.update()
                ground.hide_render = True
                for o in objs:
                    o.visible_camera = True
                    o.data.materials.clear()
                    for m in orig[o]:
                        o.data.materials.append(m)
                sc.cycles.samples = 12 if preview else samples
                render_to(os.path.join(out, f"{'prev' if preview else 'col'}_{idx:02d}.png"))
                if preview:
                    continue
                for o in objs:
                    o.data.materials.clear()
                    o.data.materials.append(mask_on if orig[o][0] is M["house"] else mask_off)
                old = wbg.default_value
                wbg.default_value = 0.0
                sc.cycles.samples = 1
                render_to(os.path.join(out, f"mask_{idx:02d}.png"))
                wbg.default_value = old
                ground.hide_render = False
                for o in objs:
                    o.visible_camera = False
                sc.cycles.samples = max(8, samples // 2)
                render_to(os.path.join(out, f"shadow_{idx:02d}.png"))
                for o in objs:
                    o.visible_camera = True
            print(f"state {name} frame {k} done  t={time.time() - t0:.1f}s", flush=True)
    print(f"TOTAL_RENDER_SECONDS {time.time() - t0:.1f}")


# ----------------------------------------------------------------------------------------------- post
def write_toml(path, mod, sheet_name):
    ox, oy = origin_px(mod)
    cv = mod.CANVAS
    lines = [
        f"# Sprite manifest for {mod.ID} ({mod.TITLE}). Generated by tools/art/build_vehicle.py -- do not hand-edit.",
        f"# Sheet: {COLS} columns of {cv} px frames, row-major. Frame 0 faces north, facings run clockwise in 22.5 deg steps.",
        "# Frame of facing f, animation step k in a state = first + f * frames + k.",
        "# Origin: pixel on the ground contact point (cell centre), counted from the frame's top-left corner.",
        "# Camera: orthographic, 40 deg elevation, sun upper-left, 64 px per cell (art/pilot/README.md).",
        "",
        f'file = "{sheet_name}"', f"frame_w = {cv}", f"frame_h = {cv}", f"origin_x = {ox}", f"origin_y = {oy}", "",
        "# House paint is the 8-shade magenta ramp, darkest first. Exact RGB matches are house pixels; alpha is always 0 or 255.",
        "[house]",
        "ramp = [" + ", ".join('"#%02X%02X%02X"' % c for c in C.HOUSE) + "]", "",
    ]
    for name, first, facings, frames, ticks in layout(mod):
        lines += ["[[state]]", f'name = "{name}"', f"facings = {facings}", f"frames = {frames}", f"first = {first}",
                  f"ticks_per_frame = {ticks}", ""]
    open(path, "w").write("\n".join(lines))


def post(vid, mod):
    import numpy as np
    import structpost as SP
    wd = work_dir(vid)
    cv = mod.CANVAS
    n = sum(f * fr for _n, _f, f, fr, _t in layout(mod))
    cal = SP.calibrate(wd, n)
    frames = [SP.frame_b(wd, f, cal, cv, cv) for f in range(n)]
    for f, im in enumerate(frames):
        a = np.asarray(im)
        if a[0, :, 3].any() or a[-1, :, 3].any() or a[:, 0, 3].any() or a[:, -1, 3].any():
            print(f"WARNING frame {f}: art touches the canvas edge (cropped?)")
    sheet = C.pack_sheet(frames, COLS, cv, cv)
    outdir = os.path.join(C.ROOT, "data", "sprites", "vehicles")
    os.makedirs(outdir, exist_ok=True)
    sheet.save(os.path.join(outdir, f"{vid}.png"))
    write_toml(os.path.join(outdir, f"{vid}.sprite.toml"), mod, f"{vid}.png")
    arr = np.asarray(sheet).reshape(-1, 4)
    assert set(np.unique(arr[:, 3])) <= {0, 255}
    print("wrote", vid, sheet.size, "colours used:", len({tuple(c[:3]) for c in arr if c[3]}))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--states", nargs="*", default=None)
    ap.add_argument("--facings", type=int, nargs="*", default=None)
    ap.add_argument("--render-only", action="store_true")
    ap.add_argument("--post-only", action="store_true")
    ap.add_argument("--preview", action="store_true", help="colour-only half-res frames to work/veh_<id>/prev_XX.png")
    a = ap.parse_args()
    mod = load_module(a.id)
    if not a.post_only:
        render(a.id, mod, a.samples, a.states, a.facings, a.preview)
    if not (a.render_only or a.preview):
        post(a.id, mod)


if __name__ == "__main__":
    main()
