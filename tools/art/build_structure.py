"""Build a structure (building) procedurally in Blender (bpy), render every frame, then pixel-post-process it.

Usage:  python3 tools/art/build_structure.py <id> [--samples 24] [--frames 0 5 ...] [--render-only | --post-only]
        [--preview]     (preview = colour pass only, 2x supersample, writes art/pilot/work/struct_<id>/preview_XX.png)
<id> names a module in tools/art/structures/<id>.py (see auth_refinery.py for the contract).

Pipeline = arm B of the tank (D-039/D-043): ortho camera, elevation 40 deg (the v2 tank camera), sun upper-left,
4x render -> box downscale -> fixed 32-colour palette incl. the 8 exact magenta house shades -> 1 px outline ->
dithered hard shadow.  Work files: art/pilot/work/struct_<id>/{col,mask,shadow}_XX.png (git-ignored).

Framing: 64 px per cell horizontally, exactly like the tank.  Ground is drawn so that a cell is 64 x 64 px on the
ground plane: structure modules author in *game-ground units* (3.4 per cell, y = south-to-north), and this
file stretches every y by 1/sin(elevation) (Ctx.YS) so that a 3x3 footprint covers 192 x 192 px of ground.
Round things stay round: only locations and box depths are stretched, never radii.
"""
import argparse, importlib.util, math, os, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import common as C

CELL = 3.4            # world units per cell (same as the tank)
CELL_PX = 64
ELEV = 40.0           # camera elevation, degrees (tank v2)
SS = 4


def load_module(sid):
    p = os.path.join(HERE, "structures", f"{sid}.py")
    spec = importlib.util.spec_from_file_location(f"structures_{sid}", p)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def work_dir(sid):
    return os.path.join(C.WORK, f"struct_{sid}")


def frame_list(mod):
    """[(state, index_in_state, count_in_state)] in sheet order."""
    out = []
    for name, n, _tpf in mod.STATES:
        out += [(name, i, n) for i in range(n)]
    return out


# ---------------------------------------------------------------------------------------------- Blender side
def render(sid, mod, samples, only, preview):
    import bpy, bmesh
    from mathutils import Vector, Matrix

    YS = 1.0 / math.sin(math.radians(ELEV))
    fw, fh = mod.CANVAS
    ox, oy = mod.ORIGIN  # footprint top-left inside the frame, px

    def lin(hexstr):
        h = hexstr.lstrip("#")
        c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1.0]

    def mat(name, hexc, rough=0.8, metal=0.0, emit=None):
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
        o = nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(e.outputs[0], o.inputs[0])
        return m

    bpy.ops.wm.read_factory_settings(use_empty=True)
    M = {}
    for name, spec in mod.MATERIALS.items():
        M[name] = mat(name, *spec[:1], **(spec[1] if len(spec) > 1 else {}))
    M["house"] = mat("house", "#E000E0", 0.75)  # house paint is always magenta; every module gets it
    M["house2"] = mat("house2", "#9A009A", 0.75)  # darker house paint for big flat up-facing areas (lands on mid ramp shades)
    mask_on, mask_off = emission_mat("m1", 1.0), emission_mat("m0", 0.0)

    class Ctx:
        """Primitive helpers handed to the structure module. Coordinates: x east, y NORTH (ground units), z up."""
        pass

    S = Ctx()
    S.YS = YS
    S.CELL = CELL
    S.objs = []      # (obj, material name, flags)
    S.clip_z = None
    S.skin = {}                 # material name -> replacement name for this frame (house stays house unless remapped)
    S.hide = lambda name: False  # predicate on the object name; True = do not create the part

    def finish(o, m, nocast, noclip, nomask):
        m = S.skin.get(m, m)                       # per-frame material swap (primer, soot, ...), set by the module
        if S.hide(o.name):                         # per-frame part removal (prefix match on the object name)
            bpy.data.objects.remove(o)
            return None
        for c in list(o.users_collection):
            c.objects.unlink(o)
        bpy.context.scene.collection.objects.link(o)
        o.data.materials.append(M[m])
        if nocast:
            o.visible_shadow = False
        S.objs.append((o, m, noclip))
        return o

    def place(o, loc, wo, stretch):
        x, y, z = loc
        o.location = (x + wo[0], (y * YS if stretch else y) + wo[1], z)

    def box(name, size, loc, m, bevel=0.03, rot=(0, 0, 0), wo=(0, 0), stretch=True, nocast=False, noclip=False):
        bpy.ops.mesh.primitive_cube_add(size=1)
        o = bpy.context.active_object
        o.name = name
        sx, sy, sz = size
        o.scale = (sx, sy * YS if stretch else sy, sz)
        bpy.ops.object.transform_apply(scale=True)
        place(o, loc, wo, stretch)
        o.rotation_euler = [math.radians(r) for r in rot]
        if bevel:
            bm = o.modifiers.new("bev", "BEVEL")
            bm.width = bevel
            bm.segments = 1
        return finish(o, m, nocast, noclip, False)

    def bb(name, x0, x1, y0, y1, z0, z1, m, bevel=0.03, **kw):
        """Box from corner coordinates (ground units)."""
        return box(name, (x1 - x0, y1 - y0, z1 - z0), ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), m, bevel, **kw)

    def cyl(name, r, h, loc, m, axis="Z", verts=24, wo=(0, 0), stretch=True, nocast=False, noclip=False, smooth=True, rot_z=0.0):
        """Cylinder; axis Y length is stretched like a box depth."""
        hh = h * YS if (axis == "Y" and stretch) else h
        bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=hh, vertices=verts)
        o = bpy.context.active_object
        o.name = name
        place(o, loc, wo, stretch)
        if axis == "X":
            o.rotation_euler = (0, math.radians(90), 0)
        elif axis == "Y":
            o.rotation_euler = (math.radians(90), 0, 0)
        else:
            o.rotation_euler = (0, 0, math.radians(rot_z))
        if smooth:
            bpy.ops.object.shade_smooth()
        return finish(o, m, nocast, noclip, False)

    def cone(name, r1, r2, h, loc, m, verts=10, wo=(0, 0), nocast=True, noclip=False, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_cone_add(radius1=r1, radius2=r2, depth=h, vertices=verts)
        o = bpy.context.active_object
        o.name = name
        place(o, loc, wo, True)
        o.rotation_euler = [math.radians(r) for r in rot]
        bpy.ops.object.shade_smooth()
        return finish(o, m, nocast, noclip, False)

    def sphere(name, r, loc, m, wo=(0, 0), nocast=True, noclip=False, squash=1.0):
        bpy.ops.mesh.primitive_ico_sphere_add(radius=r, subdivisions=2)
        o = bpy.context.active_object
        o.name = name
        o.scale = (1, 1, squash)
        place(o, loc, wo, True)
        bpy.ops.object.shade_smooth()
        return finish(o, m, nocast, noclip, False)

    S.box, S.bb, S.cyl, S.cone, S.sphere = box, bb, cyl, cone, sphere

    def reset():
        for o, _m, _c in S.objs:
            me = o.data
            bpy.data.objects.remove(o)
            if me.users == 0:
                bpy.data.meshes.remove(me)
        S.objs.clear()
        S.clip_z = None
        S.skin.clear()
        S.hide = lambda name: False

    def apply_clip(z):
        """Cut every clippable object at height z (keep below), cap the cut so nothing looks hollow."""
        dg = bpy.context.evaluated_depsgraph_get()
        keep = []
        for oi, (o, m, noclip) in enumerate(S.objs):
            zc = z - 0.004 * (oi % 9)   # stagger the caps: coplanar caps give black shadow acne in Cycles
            if noclip:
                keep.append((o, m, noclip))
                continue
            me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
            bm = bmesh.new()
            bm.from_mesh(me)
            bpy.data.meshes.remove(me)
            bmesh.ops.transform(bm, matrix=o.matrix_world, verts=bm.verts)
            geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
            r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, zc), plane_no=(0, 0, 1), clear_outer=True, dist=1e-5)
            cut = [g for g in r["geom_cut"] if isinstance(g, bmesh.types.BMEdge)]
            capf = []
            if cut:
                capf = bmesh.ops.holes_fill(bm, edges=cut, sides=64)["faces"]
                bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
                for fc in capf:
                    fc.material_index = 1          # cut face = darker "poured concrete" slot
                    fc.smooth = False
            if not bm.faces:
                bm.free()
                old = o.data
                bpy.data.objects.remove(o)
                if old.users == 0:
                    bpy.data.meshes.remove(old)
                continue
            nm = bpy.data.meshes.new(o.name)
            bm.to_mesh(nm)
            bm.free()
            old = o.data
            o.modifiers.clear()
            o.data = nm
            o.matrix_world = Matrix.Identity(4)
            nm.materials.append(M[m])
            nm.materials.append(M[mod.CAP_MATERIAL])
            if old.users == 0:
                bpy.data.meshes.remove(old)
            keep.append((o, m, noclip))
        S.objs[:] = keep

    # ---- scene
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.use_denoising = False
    sc.cycles.use_adaptive_sampling = False
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.render.threads = 2
    ss = 2 if preview else SS
    sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = fw * ss, fh * ss, 100
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
    d = Vector((-0.8, 0.22, 1.1)).normalized()      # identical to the tank: upper-left, shadows fall right/down-screen
    so.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.cameras.new("cam")
    cam.type = "ORTHO"
    cam.sensor_fit = "HORIZONTAL"
    cam.ortho_scale = fw / CELL_PX * CELL            # frame width in world units
    co = bpy.data.objects.new("cam", cam)
    sc.collection.objects.link(co)
    el = math.radians(ELEV)
    # Aim so the footprint centre (world origin) lands at pixel (ox + fp_w/2, oy + fp_h/2).
    fp_w, fp_h = mod.FOOTPRINT[0] * CELL_PX, mod.FOOTPRINT[1] * CELL_PX
    off_px = (oy + fp_h / 2) - fh / 2                # >0: origin sits below the frame centre
    up = Vector((0, math.sin(el), math.cos(el)))
    tgt = up * (off_px / CELL_PX * CELL)
    co.location = tgt + Vector((0, -math.cos(el), math.sin(el))) * 40
    co.rotation_euler = (tgt - co.location).to_track_quat("-Z", "Y").to_euler()
    cam.clip_end = 120
    sc.camera = co
    bpy.ops.mesh.primitive_plane_add(size=60)
    ground = bpy.context.active_object
    ground.name = "ground"
    gm = bpy.data.materials.new("gm")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.8, 0.8, 0.8, 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    ground.data.materials.append(gm)

    out = work_dir(sid)
    os.makedirs(out, exist_ok=True)

    def render_to(path):
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)

    frames = frame_list(mod)
    t0 = time.time()
    for fi, (state, i, n) in enumerate(frames):
        if only and fi not in only:
            continue
        reset()
        mod.build(S, state, i, n)
        if S.clip_z is not None:
            apply_clip(S.clip_z)
        bpy.context.view_layer.update()
        orig = {o: list(o.data.materials) for o, _m, _c in S.objs}
        ground.hide_render = True
        sc.cycles.samples = samples
        render_to(os.path.join(out, (f"preview_{fi:02d}.png" if preview else f"col_{fi:02d}.png")))
        if not preview:
            for o, m, _c in S.objs:
                o.data.materials.clear()
                o.data.materials.append(mask_on if m in ("house", "house2") else mask_off)
                o.data.materials.append(mask_off)
            wbg = w.node_tree.nodes["Background"].inputs[1]
            wbg.default_value = 0.0
            sc.cycles.samples = 1
            render_to(os.path.join(out, f"mask_{fi:02d}.png"))
            wbg.default_value = 0.9
            ground.hide_render = False
            for o, m, _c in S.objs:
                o.visible_camera = False
            sc.cycles.samples = max(8, samples // 2)
            render_to(os.path.join(out, f"shadow_{fi:02d}.png"))
        print(f"frame {fi} ({state} {i}) done  t={time.time() - t0:.1f}s", flush=True)
    print(f"TOTAL_RENDER_SECONDS {time.time() - t0:.1f}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--frames", type=int, nargs="*", default=None)
    ap.add_argument("--render-only", action="store_true")
    ap.add_argument("--post-only", action="store_true")
    ap.add_argument("--preview", action="store_true")
    a = ap.parse_args()
    mod = load_module(a.id)
    if not a.post_only:
        render(a.id, mod, a.samples, set(a.frames or []), a.preview)
    if not (a.render_only or a.preview):
        import structpost
        structpost.run(a.id, mod, work_dir(a.id))


if __name__ == "__main__":
    main()
