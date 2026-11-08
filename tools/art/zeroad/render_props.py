"""Render 0 A.D. prop meshes (CC-BY-SA 3.0, Wildfire Games / 0 A.D. contributors) as 64-px game sprites with OUR camera and sun.

Usage (always inside the shared Blender lock):
  flock -o /home/claude/wt/blender.lock python3 tools/art/zeroad/render_props.py render [--samples 24] [--only name,...] [--preview]
  python3 tools/art/zeroad/render_props.py post [--only ...]          # pixel post: biome grade, outline, dithered hard shadow, atlas + manifest

Pipeline: dae_loader.py (Blender 5 has no Collada importer) -> bpy mesh + 0 A.D. texture -> ortho camera at 40 deg elevation, the tank sun
(design 18 section 7: upper-left, ~40 deg) -> 4x colour pass + shadow pass -> box downscale -> 1-bit alpha, 1 px outline, checker-dithered
hard shadow (same rule as tools/art/structpost.py) -> per-biome colour grade (dusty, desaturated).
Output PNGs are derived from CC-BY-SA 3.0 art and stay under data/sprites/props/zeroad/ (see docs/CREDITS.md); they are never mixed into
GPL-only sheets.
"""
import argparse, json, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ZA = os.environ.get("ZEROAD_ROOT", "/home/claude/ref/zeroad")
ART = os.path.join(ZA, "binaries/data/mods/public/art")
SCRATCH = os.environ.get("ART_SCRATCH", "/tmp/claude-0/art-0ad")
RAW = os.path.join(SCRATCH, "raw")
OUTDIR = os.path.join(ROOT, "data", "sprites", "props", "zeroad")
sys.path.insert(0, HERE)

CELL = 3.4       # world units per cell (same as the tank / structures)
CELL_PX = 64
ELEV = 40.0
SS = 4
FRAME = 64

# name -> dict(dae, tex, mode 'uv'|'box', kind (PropKind name), widths = footprint width in cells per variant (small, medium, large), yaw per variant,
#              tint = multiply colour, height_cap = max height in cells)
P = os.path.join
PIECES = {
    "rock_pile": dict(dae="props/rock_pile.dae", tex="textures/terrain/types/cliff_granite.dds", mode="box", widths=(0.42, 0.58, 0.72), yaw=(10, 100, 200)),
    "stone_pile": dict(dae="props/stone_pile.dae", tex="textures/terrain/types/cliff_darkgray.dds", mode="box", widths=(0.4, 0.55, 0.75), yaw=(40, 140, 250)),
    "block_pile": dict(dae="props/wrld_block_1_pile_a.dae", tex="textures/terrain/types/cliff_granite.dds", mode="box", widths=(0.5, 0.7, 0.9), yaw=(0, 90, 180)),
    "burned_wood": dict(dae="props/rubble_wood_3x3.dae", tex="textures/skins/props/wood_burned.png", mode="uv", widths=(0.6, 0.8, 1.0), yaw=(20, 110, 200)),
    "wood_pile": dict(dae="props/wood_pile.dae", tex="textures/skins/props/wood_c.png", mode="uv", widths=(0.5, 0.7, 0.9), yaw=(0, 90, 180)),
    "plank_pile": dict(dae="props/plank_big_pile_a.dae", tex="textures/skins/props/wood_b.png", mode="uv", widths=(0.5, 0.7, 0.9), yaw=(0, 90, 180)),
}
# Rejected after the pilot look: rubble_stone_{2x2,3x3,4x4} and rubble_stone_wall_short (a square dirt pad under the debris reads as a decal, not a
# prop), tree_dead_a{1,2}_branches (alpha-card foliage, no trunk; needs the actor's alpha texture).

# PropKind (src/Game/Render/PropKind.cs) -> frames (piece, variant index into its widths/yaw/ size); small -> large.
KINDS = {
    "Boulder": [("rock_pile", 0), ("rock_pile", 1), ("rock_pile", 2)],
    "Rubble": [("stone_pile", 0), ("stone_pile", 1), ("stone_pile", 2)],
    "ConcreteBlock": [("block_pile", 0), ("block_pile", 1), ("block_pile", 2)],
    "Snag": [("plank_pile", 0), ("burned_wood", 1), ("wood_pile", 2)],
}
STONE = {"rock_pile", "stone_pile", "block_pile"}


def load_image_png(path, dst):
    from PIL import Image
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    Image.open(path).convert("RGBA").save(dst)
    return dst


def render(only, samples, preview):
    import numpy as np
    import bpy
    from mathutils import Vector
    import dae_loader
    ss = 2 if preview else SS
    sc = bpy.context.scene
    bpy.ops.wm.read_factory_settings(use_empty=True)
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
    W = FRAME * ss
    sc.render.resolution_x = sc.render.resolution_y = W
    sc.render.resolution_percentage = 100
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.65, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    sc.world = w
    sun = bpy.data.lights.new("sun", "SUN"); sun.energy = 4.5; sun.angle = math.radians(4)
    so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so)
    d = Vector((-0.8, 0.22, 1.1)).normalized()       # identical to the tank
    so.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.cameras.new("cam"); cam.type = "ORTHO"; cam.sensor_fit = "HORIZONTAL"
    cam.ortho_scale = FRAME / CELL_PX * CELL
    co = bpy.data.objects.new("cam", cam); sc.collection.objects.link(co)
    el = math.radians(ELEV)
    GROUND_PX = 40                                     # the prop's ground centre sits at pixel y=40 of the 64 frame (room above for trees)
    up = Vector((0, math.sin(el), math.cos(el)))
    tgt = up * ((GROUND_PX - FRAME / 2) / CELL_PX * CELL)
    co.location = tgt + Vector((0, -math.cos(el), math.sin(el))) * 40
    co.rotation_euler = (tgt - co.location).to_track_quat("-Z", "Y").to_euler()
    cam.clip_end = 120
    sc.camera = co
    bpy.ops.mesh.primitive_plane_add(size=60)
    ground = bpy.context.active_object
    gm = bpy.data.materials.new("gm"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.8, 0.8, 0.8, 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    ground.data.materials.append(gm)
    os.makedirs(RAW, exist_ok=True)

    for name, spec in PIECES.items():
        if only and name not in only:
            continue
        V, UV = dae_loader.load(os.path.join(ART, "meshes", spec["dae"]))
        flat = V.reshape(-1, 3)
        lo, hi = flat.min(0), flat.max(0)
        ext = hi - lo
        span = max(ext[0], ext[1])
        cen = np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]])
        tex_png = load_image_png(os.path.join(ART, spec["tex"]), os.path.join(RAW, "tex", os.path.basename(spec["tex"]) + ".png"))
        for var in range(3):
            sc_f = spec["widths"][var] * CELL / span
            vv = (V - cen) * sc_f
            ca, sa = math.cos(math.radians(spec["yaw"][var])), math.sin(math.radians(spec["yaw"][var]))
            x, y = vv[..., 0] * ca - vv[..., 1] * sa, vv[..., 0] * sa + vv[..., 1] * ca
            vv = np.stack([x, y, vv[..., 2]], -1)
            verts = vv.reshape(-1, 3)
            me = bpy.data.meshes.new(f"{name}{var}")
            me.from_pydata([tuple(p) for p in verts], [], [tuple(range(i, i + 3)) for i in range(0, len(verts), 3)])
            me.update()
            uvl = me.uv_layers.new(name="uv")
            for li, loop in enumerate(me.loops):
                uvl.data[li].uv = tuple(UV.reshape(-1, 2)[li])
            for pl in me.polygons:
                pl.use_smooth = False
            ob = bpy.data.objects.new(f"{name}{var}", me); sc.collection.objects.link(ob)
            mat = bpy.data.materials.new(f"m{name}{var}"); mat.use_nodes = True
            nt = mat.node_tree
            bsdf = nt.nodes["Principled BSDF"]; bsdf.inputs["Roughness"].default_value = 0.95
            tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images.load(tex_png)
            tn.interpolation = "Smart"
            if spec["mode"] == "box":
                tn.projection = "BOX"; tn.projection_blend = 0.25
                tc = nt.nodes.new("ShaderNodeTexCoord")
                mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (0.9, 0.9, 0.9)
                nt.links.new(tc.outputs["Object"], mp.inputs["Vector"]); nt.links.new(mp.outputs["Vector"], tn.inputs["Vector"])
            nt.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"])
            ob.data.materials.append(mat)
            ground.hide_render = True
            ob.visible_camera = True
            bpy.context.view_layer.update()
            sc.cycles.samples = samples
            sc.render.filepath = os.path.join(RAW, f"{name}_{var}_col.png"); bpy.ops.render.render(write_still=True)
            if not preview:
                ground.hide_render = False
                ob.visible_camera = False
                sc.cycles.samples = max(8, samples // 2)
                sc.render.filepath = os.path.join(RAW, f"{name}_{var}_sh.png"); bpy.ops.render.render(write_still=True)
            bpy.data.objects.remove(ob); bpy.data.meshes.remove(me)
            print("rendered", name, var, flush=True)


def hx(h):
    h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# per biome: (stone ramp, wood ramp), dark -> light. Dusty and desaturated; a prop stays a clear step lighter than the ground it stands on.
GRADE = {
    "rustbelt":    (("#3a3836", "#6a655e", "#a39b8d"), ("#2e2823", "#5a4d41", "#8d7b66")),
    "glasscoast":  (("#4a5252", "#7e8a88", "#b7c2bd"), ("#5a5144", "#93856f", "#d0c4a8")),
    "mountains":   (("#44464c", "#787c84", "#b4b8c0"), ("#2b2622", "#574b3f", "#85766a")),
    "ashplains":   (("#3b3535", "#6b6264", "#a39a9b"), ("#1d1a19", "#3d3532", "#6e6259")),
}
OUTLINE = hx("#0d0f0b")
SHADOW = hx("#1a1714")
LEVELS = 4
ORIGIN = (FRAME // 2, 40)


def ramp(t, cols):
    import numpy as np
    c = np.array([hx(x) for x in cols], dtype=float)
    t = np.clip(t, 0, 1) * 2
    lo = np.clip(np.floor(t).astype(int), 0, 1)
    f = (t - lo)[..., None]
    return c[lo] * (1 - f) + c[lo + 1] * f


def post_frame(piece, var, biome, kind):
    import numpy as np
    from PIL import Image, ImageFilter
    col = np.asarray(Image.open(os.path.join(RAW, f"{piece}_{var}_col.png")).convert("RGBA"), dtype=float)
    shd = np.asarray(Image.open(os.path.join(RAW, f"{piece}_{var}_sh.png")).convert("RGBA"), dtype=float)
    k = SS
    def bm(x):
        h, w = x.shape[:2]; return x.reshape(h // k, k, w // k, k, *x.shape[2:]).mean(axis=(1, 3))
    a = col[..., 3] / 255
    A = bm(a)
    base = bm(col[..., :3] * a[..., None]) / np.maximum(A[..., None], 1e-6)
    lum = (base @ [0.3, 0.55, 0.15]) / 255
    solid = A >= 0.5
    ys, xs = np.nonzero(solid)
    lo, hi = np.percentile(lum[solid], 3), np.percentile(lum[solid], 97)
    t = np.clip((lum - lo) / max(hi - lo, 1e-6), 0, 1)
    t = np.round(t * (LEVELS - 1)) / (LEVELS - 1)
    cols = GRADE[biome][0 if piece in STONE else 1]
    graded = ramp(t, cols)
    # keep a little of the source colour variation (desaturated) so rubble and planks do not go flat
    grey = base.mean(axis=2, keepdims=True)
    src = grey + (base - grey) * 0.35
    graded = np.clip(graded * 0.85 + (src - src.mean()) * 0.18 + 0, 0, 255)
    out = np.zeros((FRAME, FRAME, 4), dtype=np.uint8)
    out[solid, :3] = graded[solid].astype(np.uint8); out[solid, 3] = 255
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


def post(only):
    import numpy as np
    from PIL import Image
    os.makedirs(OUTDIR, exist_ok=True)
    kinds = list(KINDS)
    lines = ["# 0 A.D. prop atlas manifest (generated by tools/art/zeroad/render_props.py -- do not hand-edit).",
             "# Art derived from 0 A.D. (Wildfire Games / 0 A.D. contributors), CC-BY-SA 3.0: these PNGs stay separately licensed (docs/CREDITS.md).",
             f"frame = {FRAME}", f"origin_x = {ORIGIN[0]}", f"origin_y = {ORIGIN[1]}", ""]
    for r, kind in enumerate(kinds):
        lines += ["[[kind]]", f'name = "{kind}"', f"row = {r}", f"variants = {len(KINDS[kind])}", ""]
    lines += ["# One atlas PNG per theater id; same layout (row = kind above, column = variant).", "[theaters]"]
    for biome in GRADE:
        atlas = np.zeros((FRAME * len(kinds), FRAME * 3, 4), dtype=np.uint8)
        for r, kind in enumerate(kinds):
            for c, (piece, var) in enumerate(KINDS[kind]):
                atlas[r * FRAME:(r + 1) * FRAME, c * FRAME:(c + 1) * FRAME] = post_frame(piece, var, biome, kind)
        Image.fromarray(atlas, "RGBA").save(os.path.join(OUTDIR, f"{biome}.png"), optimize=True)
        lines.append(f'{biome} = "{biome}.png"')
    open(os.path.join(OUTDIR, "props.toml"), "w", newline="\n").write("\n".join(lines) + "\n")
    print("wrote", OUTDIR)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["render", "post"])
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--only", default="")
    ap.add_argument("--preview", action="store_true")
    a = ap.parse_args()
    only = set(filter(None, a.only.split(",")))
    if a.cmd == "render":
        render(only, a.samples, a.preview)
    else:
        post(only)
