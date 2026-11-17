"""Booklet hero renders: one full-resolution, transparent-background Cycles render per unit / building id.

Usage (repo root, with bpy installed, i.e. the .venv-art of D-196):
    python tools/booklet/render_heroes.py --pilot                 # 12 ids: one vehicle, infantry, building per faction
    python tools/booklet/render_heroes.py --ids auth_tank,lb_mother_rig
    python tools/booklet/render_heroes.py --all
    options: --samples 128  --long-side 1600  --force  --out art/booklet  --contact art/booklet/contact_<date>.png
             --no-denoise  --threads N (0 = all cores, the default)
On the owner's laptop use tools/booklet/render_heroes.ps1, which wraps this.  On the shared cloud box wrap the call in
`flock -o /home/claude/wt/blender.lock ...` (the script does not lock by itself).

How it works (additive: no existing sprite script is edited).  Each id's model is built by the SAME code that builds its game
sprite: build_vehicle.render / build_infantry.render / build_structure.render, and the tank's own script for auth_tank
(tools/art/build_tank.py, loaded from source with its trailing main() call dropped).  Those functions build the scene, light it
(sun upper-left, 40 degree orthographic camera) and render the colour pass.  This script swaps `bpy.ops` for a thin proxy; at the
first colour-pass render of the one frame asked for it
  1. recolours the magenta house materials (house, house2) to the faction's booklet accent from render_manifest.toml,
  2. re-frames the same orthographic camera tightly around the model (same angle and sun, only the crop and zoom change),
  3. raises the resolution to --long-side px on the long side, samples and denoises (no pixelisation, no palette, no outline),
  4. renders straight to art/booklet/<faction>/<id>.png and stops the builder (mask and shadow passes are never run).
Hero view: the south-west three-quarter facing (225 degrees clockwise from north: sheet facing 10 of 16, 5 of 8), "idle" state,
first frame; buildings: "idle" frame 0.  Vehicles with a separate turret layer are rendered with the "baked" layer (hull and
turret in one pass, the reference the sprite checks use).
"""
import argparse
import datetime
import math
import os
import re
import sys
import time
import tomllib

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
ART = os.path.join(ROOT, "tools", "art")
MANIFEST = os.path.join(HERE, "render_manifest.toml")
FACING_DEG = 225.0       # clockwise from north: the south-west three-quarter view of the pilot pages
# Structures are drawn from the fixed game camera, so a straight wall or gate piece faces it square-on and reads flat in the booklet.
# These ids get their model turned about its centre for the hero only (degrees, positive = counter-clockwise seen from above);
# the camera and sun stay as they are.  --yaw overrides the table for one run.  Negative: the sun is in the west (upper left), so
# turning clockwise keeps the long face lit (s22: +35 put it in shade and it read flat).
STRUCTURE_YAW = {"auth_gate": -35.0, "auth_wall": -35.0, "dir_bunker": -35.0, "lb_scrap_wall": -35.0}
# Turned structures also get half the sprite pipeline's sky light (0.9 -> 0.45), so the sun gives the faces real contrast.
TURNED_WORLD_STRENGTH = 0.45
FACTION_ORDER = ("authority", "directorate", "left_behind", "expedition", "neutral")


# Hero polish (S33): the game sprites are low-poly on purpose; the booklet blows them up, so the hero path only rounds them off.
POLISH_ANGLE_DEG = 30.0      # smooth-by-angle threshold: edges sharper than this stay crisp
POLISH_BEVEL_FRAC = 0.08     # bevel width as a fraction of the object's smallest bounding-box side ...
POLISH_BEVEL_CAP = 0.035     # ... capped at this fraction of its longest side
POLISH_BEVEL_SEGMENTS = 3
POLISH_RES_MULT = 3          # curved primitives: cylinder/cone vertices, sphere segments/rings x this
POLISH_MIN_VERTS = 48
POLISH_SKIP = ("ground", "shadow", "cam", "light", "sun")


class HeroDone(Exception):
    """Raised by the render hook once the hero image is written, to stop the sprite builder before its mask and shadow passes."""


# ------------------------------------------------------------------------------------------------ manifest
def load_manifest():
    with open(MANIFEST, "rb") as f:
        m = tomllib.load(f)
    return m["accent"], m["hero"]


def pick_ids(args, heroes):
    if args.all:
        return list(heroes)
    if args.pilot:
        return [i for i, h in heroes.items() if h.get("pilot")]
    ids = [s for part in (args.ids or []) for s in part.split(",") if s]
    bad = [i for i in ids if i not in heroes]
    if bad:
        sys.exit(f"unknown id(s) (not in render_manifest.toml): {', '.join(bad)}")
    return ids


def out_path(out_dir, h, rid):
    return os.path.join(out_dir, h["faction"], rid + ".png")


# ------------------------------------------------------------------------------------------------ Blender side
def hex_rgb(hx):
    hx = hx.lstrip("#")
    return [int(hx[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def srgb_to_lin(c):
    return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c]


def set_house_colours(accent_hex):
    """Recolour the house paint materials.  'house' is the full accent; 'house2' (structures: darker paint for big flat areas, the
    sprite pipeline's #9A009A against #E000E0) keeps the same ratio, so the two-tone look survives."""
    import bpy
    base = hex_rgb(accent_hex)
    for name, k in (("house", 1.0), ("house2", 0x9A / 0xE0)):
        m = bpy.data.materials.get(name)
        if m is None or not m.use_nodes:
            continue
        b = m.node_tree.nodes.get("Principled BSDF")
        if b is not None:
            b.inputs["Base Color"].default_value = srgb_to_lin([min(1.0, c * k) for c in base]) + [1.0]


def polish_scene():
    """Hero-only polish of the built scene: smooth-by-angle shading, a small angle-limited bevel and Weighted Normal on every
    mesh (skipping ground / shadow / helpers).  Bevel width scales with the object's smallest side so small parts (infantry
    limbs, rivets) do not melt.  Imported .pie meshes (names starting 'pie') get smooth-by-angle only."""
    import bpy
    sc = bpy.context.scene
    bpy.context.view_layer.update()
    meshes = [o for o in sc.objects if o.type == "MESH" and len(o.data.vertices) > 0
              and not o.name.lower().startswith(POLISH_SKIP) and "shadow" not in o.name.lower()]
    if not meshes:
        return 0
    for o in sc.objects:
        o.select_set(False)
    done = set()
    for o in meshes:
        if o.data.name in done:
            continue
        done.add(o.data.name)
        for p in o.data.polygons:
            p.use_smooth = True
    # smooth by angle (operator works on the selected meshes; one call for all)
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(POLISH_ANGLE_DEG))
    except Exception:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(POLISH_ANGLE_DEG))
    for o in meshes:
        o.select_set(False)
    n = 0
    for o in meshes:
        if o.name.lower().startswith("pie"):
            continue
        d = o.dimensions
        side = min(x for x in (d.x, d.y, d.z) if x > 1e-5) if any(x > 1e-5 for x in d) else 0.0
        side = min(side, max(d) * 0.5)
        w = min(side * POLISH_BEVEL_FRAC, max(d) * POLISH_BEVEL_CAP)
        if w < 1e-4 or len(o.data.polygons) > 20000:
            continue
        b = o.modifiers.new("hero_bevel", "BEVEL")
        b.width = w
        b.segments = POLISH_BEVEL_SEGMENTS
        b.limit_method = "ANGLE"
        b.angle_limit = math.radians(POLISH_ANGLE_DEG)
        b.harden_normals = True
        b.use_clamp_overlap = True
        wn = o.modifiers.new("hero_wn", "WEIGHTED_NORMAL")
        wn.keep_sharp = True
        wn.weight = 50
        n += 1
    return n


# ------------------------------------------------------------------------------------------------ hero detail (S33)
DETAIL_MIN_OBJ = 0.22        # a mesh gets greebles only if its longest side is >= this fraction of the model's longest extent
DETAIL_MIN_FACE = 0.035      # ... and only faces whose area is >= this fraction of (model extent)^2
DETAIL_MIN_THICK = 0.012     # ... and whose thinnest side is >= this fraction of the model extent
DETAIL_MAX_POLYS = 40        # boxy meshes only (curved primitives are skipped)


def _seed(rid, name):
    import zlib
    return zlib.crc32((rid + "|" + name).encode("utf-8"))


def _model_extent(meshes):
    import numpy as np
    lo = np.array([1e9] * 3)
    hi = np.array([-1e9] * 3)
    for o in meshes:
        for c in o.bound_box:
            w = o.matrix_world @ __import__("mathutils").Vector(c)
            lo = np.minimum(lo, w[:])
            hi = np.maximum(hi, w[:])
    return float((hi - lo).max())


def greeble_scene(rid, kind):
    """Hero-only geometry greebles on large flat faces (not facing down) of boxy meshes: a recessed panel seam (inset + tiny
    inward extrude), a row of rivets in the rim and a raised hatch on the largest top face.  Deterministic per id + object
    name.  Skipped for infantry, small or curved meshes and WZ pie meshes.  Returns the number of objects greebled."""
    import bpy
    import bmesh
    import random
    from mathutils import Vector
    if kind == "infantry":
        return 0
    sc = bpy.context.scene
    bpy.context.view_layer.update()
    meshes = [o for o in sc.objects if o.type == "MESH" and len(o.data.vertices) > 0 and not o.name.lower().startswith(POLISH_SKIP)
              and "shadow" not in o.name.lower() and not o.name.lower().startswith("pie")]
    if not meshes:
        return 0
    ext = _model_extent(meshes)
    cands = []
    for o in meshes:
        if o.data.users > 1 or len(o.data.polygons) > DETAIL_MAX_POLYS or any(m.type != "BEVEL" for m in o.modifiers):
            continue
        if max(o.dimensions) < DETAIL_MIN_OBJ * ext or min(o.dimensions) < DETAIL_MIN_THICK * ext:
            continue          # small parts and thin decal plates (a recess would cut through them)
        cands.append(o)
    n = 0
    top_objs = None
    for o in cands:
        rnd = random.Random(_seed(rid, o.name))
        mw = o.matrix_world.copy()
        me = o.data
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.transform(mw)                       # work in world units so inset widths are not skewed by object scale
        bm.faces.ensure_lookup_table()
        big = [f for f in bm.faces if len(f.verts) == 4 and f.calc_area() >= DETAIL_MIN_FACE * ext * ext and f.normal.z > -0.2]
        if not big:
            bm.free()
            continue
        rivets = bmesh.new()
        thick = min(o.dimensions)
        topf = max((f for f in big if f.normal.z > 0.8), key=lambda f: f.calc_area(), default=None)
        for f in big:
            area = f.calc_area()
            shortest = min(e.calc_length() for e in f.edges)
            t = min(shortest * 0.07, 0.03 * ext)             # rim width
            depth = min(t * 0.35, thick * 0.3)
            nrm = f.normal.copy()
            cen = f.calc_center_median()
            verts = [v.co.copy() for v in f.verts]
            # rivets along the rim, before the face is replaced
            r = t * 0.22
            for i in range(4):
                a, b = verts[i], verts[(i + 1) % 4]
                L = (b - a).length
                if L < t * 8:
                    continue
                cnt = max(2, int(L / (t * 4.0)))
                inward = (cen - (a + b) / 2)
                inward = (inward - nrm * inward.dot(nrm)).normalized()
                d = (b - a).normalized()
                for k in range(cnt):
                    pt = a + d * (L * (k + 0.5) / cnt) + inward * (t * 0.5)
                    res = bmesh.ops.create_cone(rivets, cap_ends=True, segments=8, radius1=r, radius2=r * 0.6, depth=r * 1.0)
                    rot = nrm.to_track_quat("Z", "Y").to_matrix().to_4x4()
                    bmesh.ops.transform(rivets, matrix=__import__("mathutils").Matrix.Translation(pt + nrm * (r * 0.5)) @ rot, verts=res["verts"])
            res = bmesh.ops.inset_individual(bm, faces=[f], thickness=t, depth=-depth)
            if kind == "vehicle" and f is topf and area > 4 * DETAIL_MIN_FACE * ext * ext:
                # a raised hatch (vehicles only: on buildings it would bury roof decals): inset once more, lift the result
                inner = [x for x in bm.faces if x.is_valid and (x.calc_center_median() - cen).dot(nrm) < 0.0 and
                         x.normal.dot(nrm) > 0.99 and abs((x.calc_center_median() - (cen - nrm * depth)).length) < 1e-4]
                if inner:
                    hs = max(1e-4, min(e.calc_length() for e in inner[0].edges) * 0.28)
                    bmesh.ops.inset_individual(bm, faces=inner[:1], thickness=hs, depth=depth * 2.2)
        bm.transform(mw.inverted())
        bm.to_mesh(me)
        bm.free()
        if len(rivets.verts):
            rme = bpy.data.meshes.new(o.name + "_rivets")
            rivets.to_mesh(rme)
            ro = bpy.data.objects.new(o.name + "_rivets", rme)
            for c in o.users_collection:
                c.objects.link(ro)
            if o.data.materials:
                rme.materials.append(o.data.materials[0])
            if ro.users_collection == ():
                sc.collection.objects.link(ro)
        rivets.free()
        n += 1
    return n


def detail_materials(span):
    """Hero-only material detail on every opaque, non-emissive Principled material: Bevel-node normal (rounded edge highlights),
    edge wear (lightens where the bevel normal departs from the surface normal), AO grime in creases, large-scale colour
    variation and noisy roughness.  Flat faction colours stay recognisable (all effects are low strength)."""
    import bpy
    done = 0
    for m in bpy.data.materials:
        if not m.use_nodes or m.name.lower().startswith(("ground", "marker", "mask")) or "mask" in m.name.lower():
            continue
        nt = m.node_tree
        b = nt.nodes.get("Principled BSDF")
        if b is None or b.inputs["Base Color"].is_linked or b.inputs["Roughness"].is_linked:
            continue
        em = b.inputs["Emission Strength"].default_value
        trans = b.inputs["Transmission Weight"].default_value if "Transmission Weight" in b.inputs else 0.0
        if em > 0 or trans > 0 or b.inputs["Alpha"].default_value < 0.999:
            continue
        base = tuple(b.inputs["Base Color"].default_value)
        rough = b.inputs["Roughness"].default_value
        N = nt.nodes.new
        L = nt.links.new

        def mix(blend, fac, a, bb, facv=None):
            n_ = N("ShaderNodeMix")
            n_.data_type = "RGBA"
            n_.blend_type = blend
            n_.clamp_result = True
            if facv is not None:
                n_.inputs[0].default_value = facv
            if fac is not None:
                L(fac, n_.inputs[0])
            for idx, v in ((6, a), (7, bb)):
                if isinstance(v, tuple):
                    n_.inputs[idx].default_value = v
                else:
                    L(v, n_.inputs[idx])
            return n_.outputs[2]

        def math(op, x, y=None, clamp=False):
            n_ = N("ShaderNodeMath")
            n_.operation = op
            n_.use_clamp = clamp
            for idx, v in ((0, x), (1, y)):
                if v is None:
                    continue
                if isinstance(v, (int, float)):
                    n_.inputs[idx].default_value = v
                else:
                    L(v, n_.inputs[idx])
            return n_.outputs[0]

        geo = N("ShaderNodeNewGeometry")
        # rounded edge highlights without geometry
        bev = N("ShaderNodeBevel")
        bev.samples = 6
        bev.inputs["Radius"].default_value = span * 0.006
        L(geo.outputs["Normal"], bev.inputs["Normal"])
        L(bev.outputs["Normal"], b.inputs["Normal"])
        # edge wear: where the bevel normal departs from the geometric normal
        dot = N("ShaderNodeVectorMath")
        dot.operation = "DOT_PRODUCT"
        L(geo.outputs["Normal"], dot.inputs[0])
        L(bev.outputs["Normal"], dot.inputs[1])
        edge = math("MULTIPLY", math("SUBTRACT", 1.0, dot.outputs["Value"]), 4.0, clamp=True)
        # AO grime
        ao = N("ShaderNodeAmbientOcclusion")
        ao.samples = 8
        ao.inside = False
        ao.inputs["Distance"].default_value = span * 0.05
        grime = math("MULTIPLY", math("SUBTRACT", 1.0, ao.outputs["AO"]), 0.65, clamp=True)
        # large-scale variation + roughness noise (world position, so no stretching on scaled parts)
        nz = N("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = 4.0 / max(span, 1e-3)
        nz.inputs["Detail"].default_value = 3.0
        L(geo.outputs["Position"], nz.inputs["Vector"])
        ramp = N("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color = (0.88, 0.88, 0.88, 1)
        ramp.color_ramp.elements[1].color = (1.05, 1.05, 1.05, 1)
        L(nz.outputs["Fac"], ramp.inputs["Fac"])
        nz2 = N("ShaderNodeTexNoise")
        nz2.inputs["Scale"].default_value = 40.0 / max(span, 1e-3)
        L(geo.outputs["Position"], nz2.inputs["Vector"])
        r_out = math("ADD", rough, math("MULTIPLY", math("SUBTRACT", nz2.outputs["Fac"], 0.5), 0.3), clamp=True)
        c = mix("MULTIPLY", None, base, ramp.outputs["Color"], facv=1.0)
        wear = math("MULTIPLY", edge, 0.30, clamp=True)
        c = mix("MIX", wear, c, (0.96, 0.93, 0.86, 1.0))
        c = mix("MULTIPLY", grime, c, (0.30, 0.28, 0.26, 1.0))
        L(c, b.inputs["Base Color"])
        L(r_out, b.inputs["Roughness"])
        done += 1
    return done


def frame_camera(long_side, pad=0.04):
    """Re-aim the orthographic camera so the visible geometry fills the frame (same view direction), long side = long_side px."""
    import bpy
    import numpy as np
    from mathutils import Vector
    sc = bpy.context.scene
    bpy.context.view_layer.update()
    cam = sc.camera
    mw = cam.matrix_world
    right, up = np.array(mw.col[0][:3]), np.array(mw.col[1][:3])
    origin = np.array(mw.col[3][:3])
    lo = np.array([1e9, 1e9])
    hi = np.array([-1e9, -1e9])
    n = 0
    for o in sc.objects:
        if o.type != "MESH" or o.hide_render or not o.visible_camera or o.name.startswith("ground"):
            continue
        me = o.data
        nv = len(me.vertices)
        if nv == 0:
            continue
        co = np.empty(nv * 3, dtype=np.float32)
        me.vertices.foreach_get("co", co)
        co = co.reshape(nv, 3).astype(np.float64)
        W = np.array([list(r) for r in o.matrix_world])
        p = co @ W[:3, :3].T + W[:3, 3] - origin
        xy = np.stack([p @ right, p @ up], axis=1)
        lo = np.minimum(lo, xy.min(axis=0))
        hi = np.maximum(hi, xy.max(axis=0))
        n += 1
    if n == 0:
        raise RuntimeError("nothing visible to frame")
    size = hi - lo
    span = float(size.max()) * (1 + 2 * pad)
    centre = (lo + hi) / 2
    cam.location = cam.location + Vector(tuple(float(v) for v in (right * centre[0] + up * centre[1])))
    cam.data.sensor_fit = "AUTO"
    cam.data.shift_x = cam.data.shift_y = 0.0
    cam.data.ortho_scale = span
    # AUTO fit: ortho_scale spans the larger resolution side; the other side follows the model's aspect plus the same pad.
    w_px = max(8, int(round(long_side * (size[0] * (1 + 2 * pad)) / span)))
    h_px = max(8, int(round(long_side * (size[1] * (1 + 2 * pad)) / span)))
    sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = w_px, h_px, 100


def turn_model(yaw_deg):
    """Turn every top-level model object (not the ground, camera or lights) about the model's centre on the vertical axis."""
    import bpy
    import numpy as np
    from mathutils import Matrix, Vector
    sc = bpy.context.scene
    bpy.context.view_layer.update()
    roots = [o for o in sc.objects if o.parent is None and o.type in ("MESH", "EMPTY") and not o.name.startswith("ground")]
    pts = []
    for o in sc.objects:
        if o.type != "MESH" or o.name.startswith("ground") or len(o.data.vertices) == 0:
            continue
        nv = len(o.data.vertices)
        co = np.empty(nv * 3, dtype=np.float32)
        o.data.vertices.foreach_get("co", co)
        co = co.reshape(nv, 3).astype(np.float64)
        W = np.array([list(r) for r in o.matrix_world])
        pts.append(co @ W[:3, :3].T + W[:3, 3])
    if not pts:
        return
    allp = np.concatenate(pts)
    c = (allp.min(axis=0) + allp.max(axis=0)) / 2
    pivot = Vector((float(c[0]), float(c[1]), 0.0))
    m = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(yaw_deg), 4, "Z") @ Matrix.Translation(-pivot)
    for o in roots:
        o.matrix_world = m @ o.matrix_world
    bpy.context.view_layer.update()


class Hero:
    """Settings and state for the render hook."""
    yaw = 0.0
    path = ""
    accent = "#E0A020"
    long_side = 1600
    samples = 128
    denoise = True
    threads = 0
    polish = True
    detail = True
    rid = ""
    kind = ""
    done = False


def hero_render(real_render):
    import bpy
    sc = bpy.context.scene
    set_house_colours(Hero.accent)
    for o in sc.objects:
        if o.name.startswith("ground"):
            o.hide_render = True
    if Hero.detail:
        greeble_scene(Hero.rid, Hero.kind)
    if Hero.polish:
        polish_scene()
    if Hero.detail:
        detail_materials(_model_extent([o for o in sc.objects if o.type == "MESH" and not o.name.lower().startswith(POLISH_SKIP)]))
    if Hero.yaw:
        turn_model(Hero.yaw)
        if sc.world is not None and sc.world.use_nodes and "Background" in sc.world.node_tree.nodes:
            sc.world.node_tree.nodes["Background"].inputs[1].default_value = TURNED_WORLD_STRENGTH
    frame_camera(Hero.long_side)
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = Hero.samples
    sc.cycles.use_adaptive_sampling = True
    sc.cycles.adaptive_threshold = 0.01
    sc.cycles.use_denoising = Hero.denoise
    if Hero.denoise:
        try:
            sc.cycles.denoiser = "OPENIMAGEDENOISE"
        except Exception:
            pass
    sc.render.film_transparent = True
    sc.render.threads_mode = "AUTO" if Hero.threads <= 0 else "FIXED"      # the sprite scripts pin 2 threads; a hero can use the whole PC
    if Hero.threads > 0:
        sc.render.threads = Hero.threads
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.image_settings.color_depth = "8"
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    os.makedirs(os.path.dirname(Hero.path), exist_ok=True)
    sc.render.filepath = Hero.path
    real_render(write_still=True)
    Hero.done = True


def install_hook():
    """Replace bpy.ops by a proxy whose render.render() draws the hero image on its first call and then stops the builder."""
    import bpy
    if type(bpy.ops).__name__ == "OpsProxy":     # (a getattr test would be fooled: bpy.ops answers every attribute name)
        return

    class RenderSub:
        def __init__(self, sub):
            self._sub = sub

        def __getattr__(self, n):
            return getattr(self._sub, n)

        def render(self, *a, **kw):
            if Hero.done:
                raise HeroDone()
            hero_render(self._sub.render)
            raise HeroDone()

    class MeshSub:
        """Hero polish: curved primitives get a finer tessellation (the sprite builders ask for ~12-16 sides)."""
        def __init__(self, sub):
            self._sub = sub

        def __getattr__(self, n):
            f = getattr(self._sub, n)
            if not Hero.polish or not n.startswith("primitive_"):
                return f

            def wrapped(*a, **kw):
                if n in ("primitive_cylinder_add", "primitive_cone_add", "primitive_circle_add"):
                    kw["vertices"] = max(POLISH_MIN_VERTS, kw.get("vertices", 32) * POLISH_RES_MULT)
                elif n == "primitive_uv_sphere_add":
                    kw["segments"] = max(POLISH_MIN_VERTS, kw.get("segments", 32) * POLISH_RES_MULT)
                    kw["ring_count"] = max(POLISH_MIN_VERTS // 2, kw.get("ring_count", 16) * POLISH_RES_MULT)
                elif n == "primitive_ico_sphere_add":
                    kw["subdivisions"] = min(5, kw.get("subdivisions", 2) + 2)
                return f(*a, **kw)
            return wrapped

    class OpsProxy:
        def __init__(self, real):
            self._real = real

        def __getattr__(self, n):
            sub = getattr(self._real, n)
            if n == "render":
                return RenderSub(sub)
            return MeshSub(sub) if n == "mesh" else sub

    bpy.ops = OpsProxy(bpy.ops)


# ------------------------------------------------------------------------------------------------ per kind
def facing_index(nf):
    return int(round(nf * FACING_DEG / 360.0)) % nf


def run_builder(call):
    Hero.done = False
    try:
        call()
    except HeroDone:
        pass
    if not Hero.done:
        raise RuntimeError("the builder finished without reaching a colour-pass render")


def render_vehicle(rid, h):
    sys.path.insert(0, ART)
    import build_vehicle as V
    mod = V.load_module(rid)
    lay = V.layout(mod)
    names = [s[0] for s in lay]
    state = "idle" if "idle" in names else names[0]
    _n, _first, facings, _frames, _t = next(s for s in lay if s[0] == state)
    f = facing_index(facings)
    try:
        run_builder(lambda: V.render(rid, mod, Hero.samples, [state], [f], False, layer="baked"))
    finally:   # V.render creates an (empty) work dir for the baked layer; drop it again
        wd = V.work_dir(rid, "baked")
        if os.path.isdir(wd) and not os.listdir(wd):
            os.rmdir(wd)


def render_tank(rid, h):
    """auth_tank: the v2 model lives in build_tank.py, whose main() runs on import, so load the source without that last call."""
    import bpy
    path = os.path.join(ART, "build_tank.py")
    src = open(path, encoding="utf-8").read()
    src = re.sub(r"\n\s*main\(\)\s*\Z", "\n", src)           # no-op when a __main__ guard is ever added
    g = {"__name__": "build_tank_hero", "__file__": path}
    exec(compile(src, path, "exec"), g)
    rig, _objs, _M = g["build"]()
    _sc, ground, _world = g["setup_scene"](Hero.long_side, Hero.samples)
    rig.rotation_euler = (0, 0, -math.radians(FACING_DEG))
    bpy.context.view_layer.update()
    ground.hide_render = True
    run_builder(lambda: bpy.ops.render.render(write_still=True))


def render_infantry(rid, h):
    sys.path.insert(0, ART)
    import build_infantry as I
    mod = I.load_module(rid)
    I.apply_scale(mod, getattr(mod, "SCALE", 1.0))
    frames, _firsts = I.frame_list(mod)
    names = [f[1] for f in frames]
    state = "idle" if "idle" in names else names[0]
    nf = next(f[5] for f in frames if f[1] == state)
    want = facing_index(nf)
    idx = next(f[0] for f in frames if f[1] == state and f[2] == want and f[3] == 0)
    run_builder(lambda: I.render(rid, mod, Hero.samples, {idx}))


def render_structure(rid, h):
    sys.path.insert(0, ART)
    import build_structure as B
    mod = B.load_module(rid)
    frames = B.frame_list(mod)
    fi = next(i for i, (st, k, _n) in enumerate(frames) if st == "idle" and k == 0)
    run_builder(lambda: B.render(rid, mod, Hero.samples, {fi}, False))


def purge_art_modules():
    """Forget every module loaded from tools/art before the next id.  Vehicles and structures each have their own
    `lb_common.py`; without this the vehicle copy stays cached as `lb_common` and every Left Behind structure rendered
    after a Left Behind vehicle in the same run fails (`module 'lb_common' has no attribute 'fx'`)."""
    art = os.path.normcase(os.path.abspath(ART)) + os.sep
    for name, m in list(sys.modules.items()):
        f = getattr(m, "__file__", None)
        if f and os.path.normcase(os.path.abspath(f)).startswith(art):
            del sys.modules[name]


def render_id(rid, h):
    purge_art_modules()
    if h["script"].endswith("build_tank.py"):
        render_tank(rid, h)
    elif h["kind"] == "vehicle":
        render_vehicle(rid, h)
    elif h["kind"] == "infantry":
        render_infantry(rid, h)
    else:
        render_structure(rid, h)


# ------------------------------------------------------------------------------------------------ contact sheet
def contact_sheet(ids, heroes, out_dir, path, cols=8, cell=(300, 240)):
    from PIL import Image, ImageDraw, ImageFont
    have = [i for i in ids if os.path.exists(out_path(out_dir, heroes[i], i))]
    if not have:
        return None
    have.sort(key=lambda i: (FACTION_ORDER.index(heroes[i]["faction"]), heroes[i]["kind"] != "vehicle", heroes[i]["kind"] != "infantry", i))
    try:
        font = ImageFont.load_default(size=15)
    except TypeError:
        font = ImageFont.load_default()
    cw, ch = cell
    rows = (len(have) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * cw, rows * (ch + 22)), (232, 223, 200))
    d = ImageDraw.Draw(sheet)
    for n, rid in enumerate(have):
        im = Image.open(out_path(out_dir, heroes[rid], rid)).convert("RGBA")
        s = min((cw - 16) / im.width, (ch - 16) / im.height)
        im = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
        x, y = (n % cols) * cw, (n // cols) * (ch + 22)
        sheet.paste(im, (x + (cw - im.width) // 2, y + (ch - im.height) // 2), im)
        d.text((x + 8, y + ch + 2), rid, fill=(40, 36, 28), font=font)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sheet.save(path)
    return path, len(have)


# ------------------------------------------------------------------------------------------------ main
def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--pilot", action="store_true", help="12 ids: one vehicle, one infantry, one building per faction")
    g.add_argument("--all", action="store_true", help="every id with a model")
    g.add_argument("--ids", nargs="+", help="comma separated ids (several allowed)")
    ap.add_argument("--samples", type=int, default=128)
    ap.add_argument("--long-side", type=int, default=1600)
    ap.add_argument("--no-denoise", action="store_true")
    ap.add_argument("--threads", type=int, default=0, help="0 = all cores")
    ap.add_argument("--force", action="store_true", help="re-render ids whose PNG already exists (default: skip them)")
    ap.add_argument("--out", default=os.path.join(ROOT, "art", "booklet"))
    ap.add_argument("--contact", default=None, help="contact sheet path (default <out>/contact_<date>.png; 'none' = skip)")
    ap.add_argument("--yaw", type=float, default=None, help="structures only: turn the model this many degrees (overrides STRUCTURE_YAW)")
    ap.add_argument("--no-polish", action="store_true", help="skip the hero polish (smooth-by-angle, bevel, finer curves): the old look")
    ap.add_argument("--no-detail", action="store_true", help="skip the hero detail step (material wear/grime/bevel shading, panel seams, rivets)")
    ap.add_argument("--accent", default=None, help="override the faction accent for this run, e.g. #E0A020")
    a = ap.parse_args()
    if not (a.pilot or a.all or a.ids):
        ap.error("give --pilot, --all or --ids a,b,c")

    accents, heroes = load_manifest()
    ids = pick_ids(a, heroes)
    todo = [i for i in ids if heroes[i]["status"] == "ok"]
    skipped = [i for i in ids if heroes[i]["status"] != "ok"]
    if skipped:
        print(f"SKIPPED (status no_model, nothing to render): {', '.join(skipped)}", flush=True)
    existing = [] if a.force else [i for i in todo if os.path.exists(out_path(a.out, heroes[i], i))]
    if existing:
        print(f"already rendered (use --force to redo): {', '.join(existing)}", flush=True)
    todo = [i for i in todo if i not in existing]
    print(f"{len(todo)} to render, {len(existing)} already done, {len(skipped)} no_model", flush=True)

    t_all = time.time()
    failed, times = [], {}
    if todo:
        import bpy  # noqa: F401  (fails here, early, if the venv has no bpy)
        install_hook()
        Hero.long_side, Hero.samples, Hero.denoise, Hero.threads = a.long_side, a.samples, not a.no_denoise, a.threads
        Hero.polish = not a.no_polish
        Hero.detail = not a.no_detail
        for n, rid in enumerate(todo, 1):
            h = heroes[rid]
            Hero.accent = a.accent or accents[h["faction"]]["color"]
            Hero.path = out_path(a.out, h, rid)
            Hero.rid, Hero.kind = rid, h["kind"]
            Hero.yaw = 0.0 if h["kind"] != "building" else (a.yaw if a.yaw is not None else STRUCTURE_YAW.get(rid, 0.0))
            t0 = time.time()
            print(f"[{n}/{len(todo)}] {rid} ({h['faction']} {h['kind']}, house {Hero.accent}) ...", flush=True)
            try:
                render_id(rid, h)
                times[rid] = time.time() - t0
                print(f"[{n}/{len(todo)}] {rid} done in {times[rid]:.1f} s -> {os.path.relpath(Hero.path, ROOT)}", flush=True)
            except Exception as e:      # one bad model must not stop a 124 id run
                failed.append(rid)
                import traceback
                traceback.print_exc()
                print(f"[{n}/{len(todo)}] {rid} FAILED: {e}", flush=True)
    if a.contact != "none":
        cpath = a.contact or os.path.join(a.out, f"contact_{datetime.date.today().isoformat()}.png")
        r = contact_sheet(ids, heroes, a.out, cpath)
        if r:
            print(f"contact sheet: {r[0]} ({r[1]} renders)", flush=True)
    el = time.time() - t_all
    print(f"ELAPSED {el:.1f} s ({el / 60:.1f} min); rendered {len(times)}, failed {len(failed)}, no_model {len(skipped)}", flush=True)
    if failed:
        print(f"FAILED: {', '.join(failed)}", flush=True)
        sys.exit(1)


if __name__ == "__main__":
    main()
