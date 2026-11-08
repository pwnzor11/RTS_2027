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
# TURRET-LAYER RULES (reactor crawler, rocket truck, decon truck; dir_agitprop uses the same convention).  Goal: hull sheet +
# turret sheet read as ONE object at every hull/turret facing; a baked single-pass render (layer "baked") is the reference.
#  1. MOUNT (module dict MOUNT, built by S.mount() inside the BODY group, all of it rotationally symmetric about the pivot):
#     a raised collar ("lip", radius r_lip, top exactly at PIVOT z) wider than the turret's base, around a dark recessed well
#     (r_well, optional) -- the turret base always sits ON this collar, never floats.  PIVOT z = the collar top, so the turret
#     parts start at pz with no gap.
#  2. CONTACT SHADOW: S.mount() also adds an invisible marker disc (radius r_shadow, on the deck plane) that only the mask pass
#     sees.  post() turns it into a dithered darkening of the hull's deck pixels: solid inside 0.7 r, checker dither out to r,
#     centre nudged to the lower right (sun is upper left).  Marker pixels hidden behind other geometry are not darkened.
#  3. OUTLINE: the turret sheet keeps the 1 px dark outline only where its edge is likely to border empty space.  Edge pixels
#     within soft_r (ground-plane distance from the pivot, world units, 3.4 = 1 cell; = the radius that is always over hull) get a SOFT RIM instead:
#     the neighbouring turret colour darkened (x0.62, palette-quantised), never the near-black outline.
#  4. LIGHTING: hull and turret use the same sun/world/materials/frame_b recipe; the house ramp is calibrated on the UNION of
#     the layers (one lo/hi), the turret layer gets an invisible deck proxy (S.mount) so it sees the same bounce/AO as on the
#     hull.  Neither layer carries the other's shadow: the hull sheet has the whole vehicle's ground shadow, the turret none.
#  5. Check: `--layers baked --facings a b c d` renders the whole vehicle in one pass; veh_contact.py compares it with
#     the composite (hull + turret at equal facings) and prints differing pixels per frame.
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


def work_dir(vid, layer="body"):
    return os.path.join(C.WORK, f"veh_{vid}" if layer == "body" else f"veh_{vid}_{layer}")


def layers_of(mod):
    """Optional module attribute LAYERS (default ["body"]): "body" (hull; with a "turret" layer the turret parts are left
    out of it), "turret" (turret parts only, rotated about PIVOT, no shadow) and "shadow" (ground shadow only, for AIR units).
    Parts are assigned to the turret group by calling S.begin("turret", pivot=(x, y, z)) inside build()."""
    return list(getattr(mod, "LAYERS", ["body"]))


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
def render(vid, mod, samples, states, facing_sel, preview, layer="body"):
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
    S.layer = layer
    S.group, S.pivot = "body", (0.0, 0.0, 0.0)
    marker_mat = emission_mat("mk", 1.0)
    marker_mat.node_tree.nodes["Emission"].inputs["Color"].default_value = (0, 0, 1, 1)

    S.pose = None   # optional (pivot, pitch_deg, dz): rigid pose applied to every primitive added while set (S.set_pose)

    def set_pose(pitch=0.0, dz=0.0, pivot=(0.0, 0.0, 0.0)):
        """Pitch the following primitives about the world X axis through pivot (+deg = nose down), then drop them by dz.
        set_pose() with no args clears it.  Used for burrow 'dive' states; default pipeline behaviour is unchanged."""
        S.pose = (pitch, dz, pivot) if (pitch or dz) else None

    def begin(group, pivot=(0.0, 0.0, 0.0)):
        """Following parts belong to `group` ("body" or "turret"); a turret layer is rendered with `pivot` at the origin."""
        S.group, S.pivot = group, pivot
    S.begin = begin

    def finish(o, name, m, nocast, kind="part"):
        for c in list(o.users_collection):
            c.objects.unlink(o)
        if S.pose is not None:
            from mathutils import Matrix
            pitch, dz, pv = S.pose
            pv = Vector(pv)
            P = (Matrix.Translation((0, 0, -dz)) @ Matrix.Translation(pv) @ Matrix.Rotation(math.radians(-pitch), 4, "X")
                 @ Matrix.Translation(-pv))
            o.matrix_basis = P @ o.matrix_basis
        o.name = name
        sc.collection.objects.link(o)
        o.parent = rig
        o["grp"] = S.group
        o["nocast"] = bool(nocast)
        o["kind"] = kind
        if layer == "turret" and S.group == "turret":
            o.location.x -= S.pivot[0]
            o.location.y -= S.pivot[1]
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

    def cone(name, r1, r2, h, loc, m, verts=16, rot=None):
        bpy.ops.mesh.primitive_cone_add(radius1=r1, radius2=r2, depth=h, vertices=verts)
        o = bpy.context.active_object
        o.location = loc
        if rot is not None:      # degrees (XYZ euler); rot=(-90,0,0) points the r2 end along +Y, rot=(90,0,0) along -Y
            o.rotation_euler = [math.radians(r_) for r_ in rot]
        bpy.ops.object.shade_smooth()
        return finish(o, name, m, False)

    S.maskmats = {}      # object -> mask-pass material, for textured meshes whose house area comes from a texture mask (S.mesh)

    def mesh(name, o, m, mask=None, nocast=False):
        """Add a ready-made bpy mesh object `o` (unlinked, vertices in vehicle space, e.g. from tools/art/wz/pie_import.build_object).
        `m` is a material name (BASE_MATERIALS / MATERIALS) or a bpy Material; `mask` is an emission material (white = house) for the
        mask pass, used when the textured material itself paints the house area."""
        if not isinstance(m, str):
            M[m.name] = m
            m = m.name
        o.name = name
        ob = finish(o, name, m, nocast)
        if mask is not None:
            S.maskmats[ob] = mask
        return ob

    S.box, S.cyl, S.sphere, S.cone, S.mesh = box, cyl, sphere, cone, mesh
    S.set_pose = set_pose
    S.clip = False

    def clip_below_ground():
        """Request that everything below z=0 is cut away (boolean) once the frame is built: burrow 'dive' states."""
        S.clip = True

    S.clip_below_ground = clip_below_ground

    def apply_clip():
        from mathutils import Vector as V
        cut = bpy.data.objects.get("groundcut")
        if cut is None:
            bpy.ops.mesh.primitive_cube_add(size=1)
            cut = bpy.context.active_object
            cut.name = "groundcut"
            cut.scale = (60, 60, 20)
            cut.location = (0, 0, -10)
            cut.hide_render = True
            cut.hide_viewport = True
        for o in objs:
            zmin = min((o.matrix_basis @ V(c)).z for c in o.bound_box)
            if zmin < 0.0:
                bm = o.modifiers.new("clip", "BOOLEAN")
                bm.operation = "DIFFERENCE"
                bm.object = cut
                bm.solver = "EXACT"

    def mount(pivot, deck_z, r_lip, r_shadow, r_well=0.0, lip="hull2", well="dark", proxy_r=1.6, shadow_z=None, verts=24, **_ignored):
        """Turret mount, call once inside the BODY group (see the TURRET-LAYER RULES at the top of this file).
        deck_z = height of the hull deck under the pivot; the collar top is at pivot z."""
        px, py, pz = pivot
        g = S.group
        S.group = "body"
        if r_well > 0:
            cyl("mountwell", r_well, 0.03, (px, py, deck_z + 0.015), well, "Z", verts)
        cyl("mountlip", r_lip, pz - deck_z, (px, py, (pz + deck_z) / 2), lip, "Z", verts)
        mk = cyl("mountshadow", r_shadow, 0.01, (px, py, (deck_z + 0.02) if shadow_z is None else shadow_z), "dark", "Z", verts, nocast=True)   # mask-pass marker
        mk["kind"] = "marker"
        S.group = "turret"        # shifted by the pivot in the turret layer, like the turret parts
        pr = cyl("mountproxy", proxy_r, 0.1, (px, py, pz - 0.05), lip, "Z", verts, nocast=True)
        pr["kind"] = "proxy"
        S.group = g
    S.mount = mount

    def render_to(path):
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)

    def vis(o):
        if layer == "baked":
            return True
        return (o["grp"] == "turret") if layer == "turret" else (o["grp"] != "turret")

    out = work_dir(vid, layer)
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
            S.pose = None
            S.clip = False
            S.group, S.pivot = "body", (0.0, 0.0, 0.0)
            S.maskmats.clear()
            mod.build(S, name, k, frames)
            S.pose = None
            if S.clip:
                apply_clip()
            orig = {o: list(o.data.materials) for o in objs}
            for f in (facing_sel if facing_sel else range(facings)):
                idx = first + f * frames + k
                rig.rotation_euler = (0, 0, -math.radians(360.0 / facings * f))
                bpy.context.view_layer.update()
                ground.hide_render = True
                if layer != "shadow":
                    for o in objs:
                        kd = o["kind"]
                        if kd == "part":
                            v = vis(o)     # parts of the other layer are fully invisible (no shadow / bounce on this layer's parts)
                            o.visible_camera = o.visible_diffuse = o.visible_glossy = o.visible_transmission = v
                            o.visible_shadow = v and not o["nocast"]
                        else:       # marker: mask pass only; proxy: only seen by the turret layer's bounce rays, never by the camera
                            o.visible_camera = o.visible_shadow = False
                            o.visible_diffuse = o.visible_glossy = o.visible_transmission = (kd == "proxy" and layer == "turret")
                        o.data.materials.clear()
                        for m in orig[o]:
                            o.data.materials.append(m)
                    sc.cycles.samples = 12 if preview else samples
                    render_to(os.path.join(out, f"{'prev' if preview else 'col'}_{idx:02d}.png"))
                    if preview:
                        continue
                    for o in objs:
                        o.data.materials.clear()
                        if o["kind"] == "marker":
                            o.data.materials.append(marker_mat)
                            o.visible_camera = layer == "body"
                        else:
                            o.data.materials.append(S.maskmats.get(o) or (mask_on if orig[o][0] is M["house"] else mask_off))
                    old = wbg.default_value
                    wbg.default_value = 0.0
                    sc.cycles.samples = 1
                    render_to(os.path.join(out, f"mask_{idx:02d}.png"))
                    wbg.default_value = old
                    if layer == "turret":      # turret layer carries no shadow (the body sheet has the whole vehicle's)
                        continue
                elif preview:
                    continue
                ground.hide_render = False
                for o in objs:      # shadow pass: everything casts (the body sheet's shadow holds the whole vehicle)
                    o.visible_camera = False
                    o.visible_diffuse = o.visible_glossy = o.visible_transmission = o["kind"] == "part"
                    o.visible_shadow = not o["nocast"]
                sc.cycles.samples = max(8, samples // 2)
                noshadow = name in getattr(mod, "NO_SHADOW_STATES", ())   # overlay layers (turret) cast no ground shadow
                prev_vs = {o: o.visible_shadow for o in objs}
                if noshadow:
                    for o in objs:
                        o.visible_shadow = False
                render_to(os.path.join(out, f"shadow_{idx:02d}.png"))
                for o in objs:
                    o.visible_camera = True
                    o.visible_shadow = prev_vs[o]
            print(f"state {name} frame {k} done  t={time.time() - t0:.1f}s", flush=True)
    print(f"TOTAL_RENDER_SECONDS {time.time() - t0:.1f}")


# ----------------------------------------------------------------------------------------------- post
def write_toml(path, mod, sheet_name, layer="body"):
    ox, oy = origin_px(mod)
    cv = mod.CANVAS
    lines = [
        f"# Sprite manifest for {mod.ID} ({mod.TITLE}). Generated by tools/art/build_vehicle.py -- do not hand-edit.",
        f"# Sheet: {COLS} columns of {cv} px frames, row-major. Frame 0 faces north, facings run clockwise in 22.5 deg steps.",
        "# Frame of facing f, animation step k in a state = first + f * frames + k.",
        "# Origin: pixel on the ground contact point (cell centre), counted from the frame's top-left corner.",
        "# Camera: orthographic, 40 deg elevation, sun upper-left, 64 px per cell (art/pilot/README.md).",
    ]
    if layer == "turret":
        px, py, pz = mod.PIVOT
        lines += ["# LAYER turret: the turret only, 16 facings = TURRET facing (independent of the hull). The turret pivot sits at the",
                  "# frame origin: draw this frame with its origin on the ground point under the pivot, i.e. at the hull origin plus",
                  "# the pivot offset rotated by the HULL facing (pivot_x east / pivot_y forward, in cells at hull facing 0; forward",
                  "# is up-screen at 40 deg elevation, so screen dy = -(forward * 64 * sin 40); no height term: the turret keeps its height inside the frame, pivot_z is informational). No shadow here:",
                  "# the body sheet's shadow already contains the turret."]
    elif layer == "shadow":
        lines += ["# LAYER shadow: ground shadow only (1-bit, dithered, colour = palette index 1), same frame, origin and facing as the",
                  "# body sheet. Draw it first, at the unit's ground position; the body sheet (no shadow) is drawn on top. The body is",
                  f"# baked at altitude {mod.ALT:.2f} world units ({mod.ALT / CELL:.2f} cell), so the body sprite already floats above its",
                  "# ground origin and the shadow falls to the lower right of it."]
    elif "turret" in layers_of(mod):
        lines += ["# LAYER body: hull without the turret (the turret is a separate sheet, <id>_turret.png) but with the whole vehicle's shadow."]
    elif "shadow" in layers_of(mod):
        lines += ["# LAYER body: airframe at altitude, no shadow (the shadow is a separate sheet, <id>_shadow.png)."]
    lines += ["",
        f'file = "{sheet_name}"', f"frame_w = {cv}", f"frame_h = {cv}", f"origin_x = {ox}", f"origin_y = {oy}", ""]
    if layer != "body" or len(layers_of(mod)) > 1:
        lines += [f'layer = "{layer}"']
        if layer == "turret":
            lines += [f"pivot_x = {mod.PIVOT[0] / CELL:.4f}", f"pivot_y = {mod.PIVOT[1] / CELL:.4f}", f"pivot_z = {mod.PIVOT[2] / CELL:.4f}"]
        if layer == "shadow" or "shadow" in layers_of(mod):
            lines += [f"altitude = {mod.ALT / CELL:.4f}"]
        lines += [""]
    lines += [
        "# House paint is the 8-shade magenta ramp, darkest first. Exact RGB matches are house pixels; alpha is always 0 or 255.",
        "[house]",
        "ramp = [" + ", ".join('"#%02X%02X%02X"' % c for c in C.HOUSE) + "]", "",
    ]
    for name, first, facings, frames, ticks in layout(mod):
        lines += ["[[state]]", f'name = "{name}"', f"facings = {facings}", f"frames = {frames}", f"first = {first}",
                  f"ticks_per_frame = {ticks}", ""]
    open(path, "w").write("\n".join(lines))


def shadow_only(wd, f, cv, pal):
    """Ground-shadow-only frame from shadow_XX.png (same threshold + checker dither as SP.frame_b)."""
    import numpy as np
    from PIL import Image, ImageFilter
    s = np.asarray(Image.open(os.path.join(wd, f"shadow_{f:02d}.png")).convert("RGBA"), dtype=float)
    lum = s[..., :3].mean(axis=2) / 255
    lit = np.percentile(lum, 99.5)
    sh = np.clip(1 - lum / max(lit, 1e-6), 0, 1)
    sh = np.asarray(Image.fromarray((sh * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(SS * 0.8)), dtype=float) / 255
    sh = np.clip((sh - 0.05) / 0.8, 0, 1)
    sh = C.block_mean(sh, SS)
    yy, xx = np.mgrid[0:cv, 0:cv]
    on = (sh > 0.55) | ((sh > 0.25) & ((xx + yy) % 2 == 0))
    out = np.zeros((cv, cv, 4), dtype=np.uint8)
    out[on] = (*pal[1], 255)
    return Image.fromarray(out, "RGBA")


def union_cal(vid, mod, n):
    """House-ramp (lo, hi) luminance calibrated on the union of the body and turret layers, so both map to the same ramp."""
    import numpy as np
    from PIL import Image
    vals = []
    for ly in [x for x in ("body", "turret") if x in layers_of(mod)]:
        wd = work_dir(vid, ly)
        for f in range(n):
            cp, mp = os.path.join(wd, f"col_{f:02d}.png"), os.path.join(wd, f"mask_{f:02d}.png")
            if not (os.path.exists(cp) and os.path.exists(mp)):
                continue
            c = np.asarray(Image.open(cp).convert("RGBA"), dtype=float)
            m = np.asarray(Image.open(mp).convert("RGBA"), dtype=float)
            sel = (m[..., 0] > 127) & (c[..., 3] > 250)
            vals.append((c[..., 0][sel] + c[..., 2][sel]) / 2)
    v = np.concatenate(vals)
    return np.percentile(v, 2), np.percentile(v, 98)


def pivot_px(mod, hull_facing, z):
    """Screen position (x, y, float) of the point above the pivot at height z for a hull facing (frame of the body sheet)."""
    ox, oy = origin_px(mod)
    th = math.radians(22.5 * hull_facing)
    x, y = mod.PIVOT[0] / CELL, mod.PIVOT[1] / CELL
    xr, yr = x * math.cos(th) + y * math.sin(th), -x * math.sin(th) + y * math.cos(th)
    el = math.radians(ELEV)
    return ox + xr * 64, oy - yr * 64 * math.sin(el) - (z / CELL) * 64 * math.cos(el)


def contact_shadow(img, wd, f, mod, pal):
    """Dither the deck pixels under the invisible marker disc (rule 2 of the TURRET-LAYER RULES)."""
    import numpy as np
    from PIL import Image
    import pixelize as P
    mt = mod.MOUNT
    cv = mod.CANVAS
    m = np.asarray(Image.open(os.path.join(wd, f"mask_{f:02d}.png")).convert("RGBA"), dtype=float)
    cov = np.where((m[..., 0] < 60) & (m[..., 1] < 60) & (m[..., 3] > 127), m[..., 2] / 255, 0.0)
    cov = C.block_mean(cov, SS)
    cx, cy = pivot_px(mod, f, mt.get("shadow_z", mt["deck_z"] + 0.02))      # one state, one frame per facing: frame index = hull facing
    sx, sy = 0.18, -0.10                         # shadow centre nudged to the lower right on screen, in ground cells
    cx += sx * 64
    cy += -sy * 64 * math.sin(math.radians(ELEV))
    yy, xx = np.mgrid[0:cv, 0:cv]
    t = np.hypot((xx + 0.5 - cx) / 64, (yy + 0.5 - cy) / (64 * math.sin(math.radians(ELEV)))) / (mt["r_shadow"] / CELL)
    g = np.clip((1 - t) / 0.3, 0, 1)
    s_ = cov * g
    a = np.asarray(img).copy()
    house = np.zeros(a.shape[:2], bool)
    for hc in C.HOUSE:
        house |= (a[..., :3] == np.array(hc)).all(axis=2)
    on = (a[..., 3] > 0) & ~house & ((s_ > 0.5) | ((s_ > 0.2) & ((xx + yy) % 2 == 0)))
    ys, xs = np.nonzero(on)
    if len(ys):
        q = P.quantise(a[ys, xs, :3].astype(float) * 0.66, pal)
        for y, x, qi in zip(ys, xs, q):
            a[y, x, :3] = pal[qi]
    return Image.fromarray(a, "RGBA"), int(on.sum())


def frame_turret(wd, f, cal, cv, pal, mod):
    """frame_b without shadow, with the turret outline rule (3): soft rim over the hull, hard outline in open space."""
    import numpy as np
    from PIL import Image
    import structpost as SP
    import pixelize as P
    pal = C.PAL if pal is None else pal
    L = SP.load_layers(wd, f, cal)
    base, A, hfrac, hidx, _sh = C.downscale_layers(L, cv)
    base = np.clip((base - 128) * 1.12 + 128, 0, 255)
    solid = A >= 0.5
    out = np.zeros((cv, cv, 4), dtype=np.uint8)
    ys, xs = np.nonzero(solid)
    q = P.quantise(base[ys, xs], pal)
    for y, x, qi in zip(ys, xs, q):
        out[y, x] = (*C.HOUSE[hidx[y, x]], 255) if hfrac[y, x] >= 0.5 else (*pal[qi], 255)
    edge = np.zeros_like(solid)
    edge[1:, :] |= solid[:-1, :]
    edge[:-1, :] |= solid[1:, :]
    edge[:, 1:] |= solid[:, :-1]
    edge[:, :-1] |= solid[:, 1:]
    edge &= ~solid
    ox, oy = origin_px(mod)
    el = math.radians(ELEV)
    cy = oy - (mod.PIVOT[2] / CELL) * 64 * math.cos(el)
    yy, xx = np.mgrid[0:cv, 0:cv]
    dyp = yy + 0.5 - cy
    r = np.hypot((xx + 0.5 - ox) / 64, np.where(dyp > 0, dyp / (64 * math.sin(el)), dyp / (64 * (math.sin(el) + 0.8 * math.cos(el)))))
    soft = edge & (r < mod.MOUNT["soft_r"] / CELL)
    hard = edge & ~soft
    out[hard] = (*C.OUTLINE, 255)
    for y, x in zip(*np.nonzero(soft)):
        nb = [out[yy_, xx_, :3] for yy_, xx_ in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1))
              if 0 <= yy_ < cv and 0 <= xx_ < cv and solid[yy_, xx_]]
        col = np.mean(nb, axis=0) * 0.62
        out[y, x] = (*pal[P.quantise(col[None, :], pal)[0]], 255)
    return Image.fromarray(out, "RGBA")


def post(vid, mod, layer="body"):
    import numpy as np
    import structpost as SP
    wd = work_dir(vid, layer)
    cv = mod.CANVAS
    pal = getattr(mod, "PALETTE", None)
    n = sum(f * fr for _n, _f, f, fr, _t in layout(mod))
    if layer == "shadow":
        frames = [shadow_only(wd, f, cv, C.PAL if pal is None else pal) for f in range(n)]
    else:
        if layer == "turret":      # no shadow pass was rendered: a blank (fully lit) stand-in keeps the shared post-process happy
            from PIL import Image as _I
            for f in range(n):
                sp = os.path.join(wd, f"shadow_{f:02d}.png")
                if not os.path.exists(sp):
                    _I.new("RGBA", (cv * SS, cv * SS), (255, 255, 255, 255)).save(sp)
        if hasattr(mod, "MOUNT"):
            cal = union_cal(vid, mod, n)
        else:
            cal = SP.calibrate(wd, n)
        # no shadow in the sheet when it lives in another layer (turret layer, or the body of an air unit)
        noshadow = layer == "turret" or "shadow" in layers_of(mod)
        from PIL import Image as _I
        frames, cs = [], []
        for f in range(n):
            if not os.path.exists(os.path.join(wd, f"col_{f:02d}.png")):      # baked reference: only the facings asked for
                frames.append(_I.new("RGBA", (cv, cv), (0, 0, 0, 0)))
                continue
            if layer == "turret" and hasattr(mod, "MOUNT"):
                im = frame_turret(wd, f, cal, cv, pal, mod)
            else:
                im = SP.frame_b(wd, f, cal, cv, cv, pal, shadow=not noshadow)
                if layer == "body" and hasattr(mod, "MOUNT"):
                    im, npx = contact_shadow(im, wd, f, mod, C.PAL if pal is None else pal)
                    cs.append(npx)
            frames.append(im)
        if cs:
            print("contact-shadow pixels per frame:", cs)
    for f, im in enumerate(frames):
        a = np.asarray(im)
        if a[0, :, 3].any() or a[-1, :, 3].any() or a[:, 0, 3].any() or a[:, -1, 3].any():
            print(f"WARNING {layer} frame {f}: art touches the canvas edge (cropped?)")
    sheet = C.pack_sheet(frames, COLS, cv, cv)
    outdir = os.path.join(C.ROOT, "data", "sprites", "vehicles") if layer != "baked" else os.path.join(C.PILOT, "baked")   # reference only, not a game asset
    os.makedirs(outdir, exist_ok=True)
    name = vid if layer == "body" else f"{vid}_{layer}"
    sheet.save(os.path.join(outdir, f"{name}.png"))
    write_toml(os.path.join(outdir, f"{name}.sprite.toml"), mod, f"{name}.png", layer)
    arr = np.asarray(sheet).reshape(-1, 4)
    assert set(np.unique(arr[:, 3])) <= {0, 255}
    print("wrote", name, sheet.size, "colours used:", len({tuple(c[:3]) for c in arr if c[3]}))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--states", nargs="*", default=None)
    ap.add_argument("--facings", type=int, nargs="*", default=None)
    ap.add_argument("--render-only", action="store_true")
    ap.add_argument("--post-only", action="store_true")
    ap.add_argument("--preview", action="store_true", help="colour-only half-res frames to work/veh_<id>/prev_XX.png")
    ap.add_argument("--layers", nargs="*", default=None, help="only these layers (default: the module's LAYERS, else body)")
    a = ap.parse_args()
    mod = load_module(a.id)
    for layer in (a.layers or layers_of(mod)):
        if not a.post_only:
            render(a.id, mod, a.samples, a.states, a.facings, a.preview, layer)
        if not (a.render_only or a.preview):
            post(a.id, mod, layer)


if __name__ == "__main__":
    main()
