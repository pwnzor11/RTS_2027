"""Build an infantry sprite sheet: procedural Blender model -> 4x render -> arm-B pixel post-process.

Usage:  python3 tools/art/build_infantry.py <id> [--samples 24] [--frames 0 5 ...] [--render-only | --post-only]
<id> names a module in tools/art/infantry/<id>.py (contract in the docstring of auth_rifleman.py).
Wrap renders in `flock /home/claude/wt/blender.lock` yourself (one Blender render at a time, wave rule); the script does not lock.

Pipeline = tank arm B (D-039/D-043): ortho camera at 40 deg (D-064), sun upper-left, 4x render, box downscale, fixed
32-colour palette incl. the 8 exact magenta house shades, 1 px outline, dithered hard shadow, alpha always 0 or 255.
Scale is the tank's: 64 px per cell (3.4 world units).  Work files: art/pilot/work/inf_<id>/{col,mask,shadow}_NN.png.

Sheet: 6 columns, row-major, states packed one after another.  Frame index inside a state is
first + facing * frames + step (SpriteManifest.FrameIndex).  Facing 0 = north, clockwise, 45 deg steps.
Frame canvas 32 x 32 px at 1x; origin = the pixel the soldier's feet stand on = the sub-slot centre.
"""
import argparse, importlib.util, math, os, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "infantry"))
import common as C

CELL = 3.4
CELL_PX = 64
ELEV = 40.0
SS = 4
COLS = 6
LOCK = "/home/claude/wt/blender.lock"


def load_module(sid):
    p = os.path.join(HERE, "infantry", f"{sid}.py")
    spec = importlib.util.spec_from_file_location(f"infantry_{sid}", p)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def work_dir(sid):
    return os.path.join(C.WORK, f"inf_{sid}")


def frame_list(mod):
    """[(sheet_index, state, facing, step, frames_in_state, facings_in_state)] and first index per state."""
    out, first, firsts = [], 0, {}
    for name, nf, n, _t in mod.STATES:
        firsts[name] = first
        for f in range(nf):
            for k in range(n):
                out.append((first + f * n + k, name, f, k, n, nf))
        first += nf * n
    return out, firsts


# ---------------------------------------------------------------------------------------------- Blender side
def render(sid, mod, samples, only):
    import bpy
    from mathutils import Vector
    import rig as R

    w, h = mod.CANVAS
    ox, oy = mod.ORIGIN
    px_per_unit = CELL_PX / CELL
    res = w * SS
    wd = work_dir(sid)
    os.makedirs(wd, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {}
    for name, (hexc, rough, metal, emit) in mod.MATERIALS.items():
        mats[name] = R.make_mat(name, hexc, rough, metal, emit, getattr(mod, "EMIT_STRENGTH", {}).get(name, 2.0))
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.use_denoising = False
    sc.cycles.use_adaptive_sampling = False
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.render.threads = 2
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.65, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    sc.world = world
    sun = bpy.data.lights.new("sun", "SUN")
    sun.energy = 4.5
    sun.angle = math.radians(4)
    so = bpy.data.objects.new("sun", sun)
    sc.collection.objects.link(so)
    d = Vector((-0.8, 0.22, 1.1)).normalized()      # same sun as the tank: upper-left on screen
    so.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.cameras.new("cam")
    cam.type = "ORTHO"
    cam.ortho_scale = w / px_per_unit
    co = bpy.data.objects.new("cam", cam)
    sc.collection.objects.link(co)
    el = math.radians(ELEV)
    # frame centre is canvas (w/2, h/2); the ground origin must land on pixel (ox, oy)
    up = (oy - h / 2) / px_per_unit
    tgt = Vector((0, math.sin(el) * up, math.cos(el) * up)) + Vector(((ox - w / 2) / px_per_unit, 0, 0))
    co.location = tgt + Vector((0, -math.cos(el), math.sin(el))) * 30
    co.rotation_euler = (tgt - co.location).to_track_quat("-Z", "Y").to_euler()
    cam.clip_end = 100
    sc.camera = co
    bpy.ops.mesh.primitive_plane_add(size=30)
    ground = bpy.context.active_object
    gm = bpy.data.materials.new("gm")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.8, 0.8, 0.8, 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    ground.data.materials.append(gm)

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
    mask_on, mask_off = emission_mat("m1", 1.0), emission_mat("m0", 0.0)

    def shot(path, spl):
        sc.cycles.samples = spl
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)

    frames, _firsts = frame_list(mod)
    todo = [f for f in frames if (only is None or f[0] in only)]
    t0 = time.time()
    S = None
    last_pose = None
    # group by (state, step) so each pose is built once and rotated for the facings
    todo.sort(key=lambda f: (f[1], f[3], f[2]))
    for idx, state, facing, step, n, nf in todo:
        if last_pose != (state, step):
            if S:
                S.destroy()
            S = R.Scene(mats)
            mod.build(S, mod.pose(state, step, n))
            last_pose = (state, step)
        face = getattr(mod, "DIE_FACING", 0) if nf == 1 else facing
        S.rig.rotation_euler = (0, 0, -math.radians(45.0 * face))   # 0 = north, clockwise
        bpy.context.view_layer.update()
        # colour
        ground.hide_render = True
        for o, _m in S.objs:
            o.visible_camera = True
            o.data.materials.clear()
            o.data.materials.append(mats[_m])
        shot(os.path.join(wd, f"col_{idx:02d}.png"), samples)
        # house mask
        for o, m in S.objs:
            o.data.materials.clear()
            o.data.materials.append(mask_on if m == "house" else mask_off)
        wbg = world.node_tree.nodes["Background"].inputs[1]
        old = wbg.default_value
        wbg.default_value = 0.0
        shot(os.path.join(wd, f"mask_{idx:02d}.png"), 1)
        wbg.default_value = old
        # shadow
        ground.hide_render = False
        for o, _m in S.objs:
            o.visible_camera = False
        shot(os.path.join(wd, f"shadow_{idx:02d}.png"), max(8, samples // 2))
        print(f"frame {idx} ({state} f{facing} s{step}) t={time.time() - t0:.1f}s", flush=True)
    print(f"TOTAL_RENDER_SECONDS {time.time() - t0:.1f}")


# ---------------------------------------------------------------------------------------------- post-process
def post(sid, mod):
    import numpy as np
    from PIL import Image
    import structpost as SP

    w, h = mod.CANVAS
    wd = work_dir(sid)
    frames, firsts = frame_list(mod)
    total = len(frames)
    have = [i for i in range(total) if os.path.exists(os.path.join(wd, f"shadow_{i:02d}.png"))]
    # house calibration: the median house-paint luminance maps to ramp shade 4 (the "mid" colour), the 2..98 percentile
    # spread covers about shades 1..6, so remapped houses keep their hue and are not pastel (see README notes)
    vals = []
    for i in have:
        c = np.asarray(Image.open(os.path.join(wd, f"col_{i:02d}.png")).convert("RGBA"), dtype=float)
        m = np.asarray(Image.open(os.path.join(wd, f"mask_{i:02d}.png")).convert("RGBA"), dtype=float)
        sel = (m[..., 0] > 127) & (c[..., 3] > 250)
        vals.append((c[..., 0][sel] + c[..., 2][sel]) / 2)
    v = np.concatenate(vals)
    med, lo98, hi98 = np.median(v), np.percentile(v, 2), np.percentile(v, 98)
    span = max((hi98 - lo98) * 1.15, 24.0)
    lo = med - 0.57 * span
    cal = (lo, lo + span)
    print("house calibration", cal)
    imgs = []
    for i in range(total):
        if i in have:
            imgs.append(SP.frame_b(wd, i, cal, w, h))
        else:
            imgs.append(Image.new("RGBA", (w, h), (0, 0, 0, 0)))
    for i in have:
        a = np.asarray(imgs[i])
        if a[0, :, 3].any() or a[-1, :, 3].any() or a[:, 0, 3].any() or a[:, -1, 3].any():
            print(f"WARNING frame {i}: art touches the canvas edge (cropped?)")
    sheet = C.pack_sheet(imgs, COLS, w, h)
    outdir = os.path.join(C.ROOT, "data", "sprites", "infantry")
    os.makedirs(outdir, exist_ok=True)
    png = os.path.join(outdir, f"{sid}.png")
    sheet.save(png)
    write_toml(os.path.join(outdir, f"{sid}.sprite.toml"), mod, f"{sid}.png", firsts, total)
    arr = np.asarray(sheet).reshape(-1, 4)
    assert set(np.unique(arr[:, 3])) <= {0, 255}
    print("wrote", png, sheet.size, "colours used:", len({tuple(c[:3]) for c in arr if c[3]}))


def write_toml(path, mod, sheet_name, firsts, total):
    w, h = mod.CANVAS
    ox, oy = mod.ORIGIN
    rows = (total + COLS - 1) // COLS
    ramp = ", ".join(f'"#{r:02X}{g:02X}{b:02X}"' for r, g, b in C.HOUSE)
    L = [
        f"# Sprite manifest for {mod.ID} ({mod.TITLE}). Generated by tools/art/build_infantry.py -- do not hand-edit.",
        f"# Sheet: {w * COLS} x {h * rows} px, {COLS} columns x {rows} rows of {w} x {h} px frames, row-major.",
        "# Frame index inside a state = first + facing * frames + step. Facing 0 = north, clockwise, 45 deg steps (8 facings).",
        "# Camera: orthographic, elevation 40 deg, sun upper-left, same scale as the tank (64 px per cell).",
        "#",
        "# Origin: the pixel the soldier's feet stand on, i.e. the sub-slot centre (up to 5 soldiers share a 64 px cell),",
        "# counted from the frame's top-left corner. The ground shadow falls to the right/below of it.",
        "# State \"die\" has one facing (drawn facing east: a forward fall to the right); mirror it in code if wanted.",
        "",
        f'file = "{sheet_name}"',
        f"frame_w = {w}",
        f"frame_h = {h}",
        f"origin_x = {ox}",
        f"origin_y = {oy}",
        "",
        "# House paint is the 8-shade magenta ramp, darkest first. Exact RGB matches are house pixels; alpha is always 0 or 255.",
        "[house]",
        f"ramp = [{ramp}]",
        "",
    ]
    for name, nf, n, tpf in mod.STATES:
        L += ["[[state]]", f'name = "{name}"', f"facings = {nf}", f"frames = {n}", f"first = {firsts[name]}", f"ticks_per_frame = {tpf}", ""]
    open(path, "w").write("\n".join(L))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--frames", type=int, nargs="*", default=None, help="sheet indices to render (default all)")
    ap.add_argument("--render-only", action="store_true")
    ap.add_argument("--post-only", action="store_true")
    a = ap.parse_args()
    mod = load_module(a.id)
    if not a.post_only:
        # No self-locking: callers wrap renders in `flock /home/claude/wt/blender.lock …` (wave rule). A self re-exec under
        # the same lock deadlocks when the caller already holds it (flock is not re-entrant across processes).
        render(a.id, mod, a.samples, set(a.frames) if a.frames else None)
    if not a.render_only:
        post(a.id, mod)


if __name__ == "__main__":
    main()
