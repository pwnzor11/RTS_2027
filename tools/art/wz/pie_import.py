"""Reader for Warzone 2100's text .pie model format (PIE 2, 3 and 4) plus a Blender (bpy) mesh builder.

Own code, written from reading the format as it is documented by the engine's loader (lib/ivis_opengl/imdload.cpp, GPL-2.0-or-later):
  PIE <version> / TYPE <hex flags> / TEXTURE <n> <file> [w h] / TCMASK <n> <file> (PIE 4) / LEVELS <n>
  LEVEL <k> / POINTS <n> (x y z per line) / POLYGONS <n> (flags npts i0 i1 i2 [anim 4 values if flag 0x4000] u v u v u v if flag 0x200)
  optional per level: CONNECTORS <n> (3 ints per line), ANIMOBJECT, SHADOWPOINTS ...
Polygons are always triangles.  UVs are pixels/256 in PIE 2, floats 0..1 in PIE 3/4.  Texture V runs top to bottom (image row 0 = v 0).

Axes.  A PIE point is (x right, y up, z BACK): the barrel of a gun runs towards -z.  Connectors are written (x, depth z, height y), i.e. the
engine swaps the last two.  Our Blender scenes are x east, y north (front), z up, so  blender = (x, -z, y) * scale.  Connector (cx, cz, cy)
-> blender (cx, -cz, cy) * scale.

parse_pie() needs no Blender.  build_object() / build_materials() import bpy lazily and are used by the art vehicle/structure modules.
"""
import os

# bounding box helper values are in WZ units; the caller chooses a scale (world units per WZ unit).


def _tokens(line):
    return line.replace("\t", " ").split()


def parse_pie(path):
    """Parse a .pie file.  Returns a dict with keys: version, flags, texture, tcmask, levels (list of level dicts).
    A level dict has: points [(x,y,z)], polys [(i0,i1,i2,flags,[(u,v)*3] or None)], connectors [(x,y,z)] (raw file order)."""
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        lines = [ln.strip() for ln in fh.read().splitlines()]
    pos = 0

    def nxt():
        nonlocal pos
        while pos < len(lines) and not lines[pos]:
            pos += 1
        if pos >= len(lines):
            return None
        ln = lines[pos]
        pos += 1
        return ln

    head = _tokens(nxt())
    assert head[0] == "PIE", path
    out = {"version": int(head[1]), "flags": 0, "texture": None, "tcmask": None, "levels": []}
    ver = out["version"]
    nlevels = 0
    # header: TYPE, TEXTURE..., TCMASK..., LEVELS
    while True:
        ln = nxt()
        if ln is None:
            break
        t = _tokens(ln)
        if t[0] == "TYPE":
            out["flags"] = int(t[1], 16)
        elif t[0] == "TEXTURE":
            if out["texture"] is None:
                out["texture"] = t[2]
        elif t[0] == "TCMASK":
            if out["tcmask"] is None:
                out["tcmask"] = t[2]
        elif t[0] == "LEVELS":
            nlevels = int(t[1])
            break
        # EVENT / other header lines are ignored
    for _ in range(nlevels):
        ln = nxt()
        while ln is not None and _tokens(ln)[0] != "LEVEL":
            ln = nxt()
        if ln is None:
            break
        lv = {"points": [], "polys": [], "connectors": []}
        while True:
            save = pos
            ln = nxt()
            if ln is None:
                break
            t = _tokens(ln)
            key = t[0]
            if key == "LEVEL":
                pos = save
                break
            if key == "POINTS":
                for _i in range(int(t[1])):
                    p = _tokens(nxt())
                    lv["points"].append(tuple(float(v) for v in p[:3]))
            elif key == "POLYGONS":
                for _i in range(int(t[1])):
                    p = _tokens(nxt())
                    flags, n = int(p[0], 16), int(p[1])
                    assert n == 3, "only triangles"
                    idx = [int(v) for v in p[2:5]]
                    rest = p[5:]
                    uv = None
                    if flags & 0x200:
                        if flags & 0x4000:
                            rest = rest[4:]          # texture animation: frames, rate, tile w, tile h
                        vals = [float(v) for v in rest[:6]]
                        if ver < 3:
                            vals = [v / 256.0 for v in vals]
                        uv = [(vals[0], vals[1]), (vals[2], vals[3]), (vals[4], vals[5])]
                    lv["polys"].append((idx[0], idx[1], idx[2], flags, uv))
            elif key == "CONNECTORS":
                for _i in range(int(t[1])):
                    p = _tokens(nxt())
                    lv["connectors"].append(tuple(int(float(v)) for v in p[:3]))
            elif key in ("SHADOWPOINTS", "SHADOWPOLYGONS"):
                for _i in range(int(t[1])):
                    nxt()
            elif key == "ANIMOBJECT":
                for _i in range(int(t[3]) if len(t) > 3 else 0):
                    nxt()
            # unknown lines: skipped
        out["levels"].append(lv)
    return out


def to_blender(p, scale=1.0, offset=(0.0, 0.0, 0.0)):
    """PIE point (x, y, z) -> Blender (x, -z, y) * scale + offset."""
    return ((p[0]) * scale + offset[0], (-p[2]) * scale + offset[1], (p[1]) * scale + offset[2])


def connector(pie, idx, scale=1.0, level=0):
    """Connector idx of a parsed model in Blender axes (file order is x, depth z, height y)."""
    cx, cz, cy = pie["levels"][level]["connectors"][idx]
    return (cx * scale, -cz * scale, cy * scale)


def bbox(pie, scale=1.0, level=0):
    pts = [to_blender(p, scale) for p in pie["levels"][level]["points"]]
    return tuple(min(p[i] for p in pts) for i in range(3)), tuple(max(p[i] for p in pts) for i in range(3))


# --------------------------------------------------------------------------------------------- Blender side
def build_object(name, pie, scale=1.0, offset=(0.0, 0.0, 0.0), level=0, rot_z_deg=0.0, flip=None, only=None, skip=None, ystretch=1.0, zstretch=1.0):
    """Create a bpy mesh object (not linked to any collection) from a parsed model, with a UV map 'uv'.
    Triangles keep file order (index = position in the POLYGONS list); `only` / `skip` are sets of triangle indices to keep / drop, which is how
    a model is split into a painted part and a house-colour part.  `zstretch` multiplies the height; `ystretch` multiplies the front/back axis (the structure pipeline authors in
    ground units that it stretches by 1/sin(elevation)).  Winding is reversed when `flip` is true (default _WINDING_FLIP)."""
    import bpy, math
    lv = pie["levels"][level]
    ca, sa = math.cos(math.radians(rot_z_deg)), math.sin(math.radians(rot_z_deg))
    verts = []
    for p in lv["points"]:
        x, y, z = to_blender(p, scale)
        verts.append(((x * ca - y * sa) + offset[0], (x * sa + y * ca) * ystretch + offset[1], z * zstretch + offset[2]))
    keep = [i for i in range(len(lv["polys"])) if (only is None or i in only) and not (skip and i in skip)]
    faces = [lv["polys"][i][:3] for i in keep]
    if flip is None:
        flip = _WINDING_FLIP
    if flip:
        faces = [(a, c, b) for a, b, c in faces]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    uvl = me.uv_layers.new(name="uv")
    for fi, pi in enumerate(keep):
        uv = lv["polys"][pi][4]
        loops = me.polygons[fi].loop_indices
        order = [0, 2, 1] if flip else [0, 1, 2]
        if uv is None:
            continue
        for li, k in zip(loops, order):
            u, v = uv[k]
            uvl.data[li].uv = (u, 1.0 - v)          # image row 0 is at the top; Blender's v=1 is the top
    for poly in me.polygons:
        poly.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    return ob


# PIE triangles are counter-clockwise seen from outside (OpenGL convention) and (x, -z, y) is a proper rotation, so the file order is kept.
# Verified on drmbod05: the normal of the deck triangles (cross(B-A, C-A)) points up, that of the floor triangles down.
_WINDING_FLIP = False


def uv_texture_coverage(pie, mask_img_path, level=0, threshold=0.5):
    """Per-face flag: does the tcmask cover more than `threshold` of the face's UV triangle?  (mask = red channel, like the engine.)"""
    import numpy as np
    from PIL import Image
    m = np.asarray(Image.open(mask_img_path).convert("RGBA"), dtype=float)[..., 0] / 255.0
    h, w = m.shape
    res = []
    for a, b, c, _f, uv in pie["levels"][level]["polys"]:
        if uv is None:
            res.append(False)
            continue
        pts = [(u * w, v * h) for u, v in uv]
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        x0, x1 = int(max(0, min(xs))), int(min(w - 1, max(xs)))
        y0, y1 = int(max(0, min(ys))), int(min(h - 1, max(ys)))
        if x1 < x0 or y1 < y0:
            res.append(False)
            continue
        yy, xx = np.mgrid[y0:y1 + 1, x0:x1 + 1]
        px, py = xx + 0.5, yy + 0.5
        (ax, ay), (bx, by), (cx, cy) = pts
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-9:
            res.append(False)
            continue
        l1 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / den
        l2 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / den
        inside = (l1 >= -0.01) & (l2 >= -0.01) & (1 - l1 - l2 >= -0.01)
        if not inside.any():
            res.append(False)
            continue
        res.append(bool(m[y0:y1 + 1, x0:x1 + 1][inside].mean() > threshold))
    return res


def image_material(name, tex_path, mask_path=None, house_hex="#E000E0", rough=0.8, metal=0.0, mask_threshold=0.35):
    """Textured Principled material (nearest-neighbour sampling, like the engine's pixel look).  With mask_path the tcmask (red channel)
    is thresholded and mixed over the colour as flat house magenta.  Returns (material, mask_material): mask_material is an emission
    material showing the same thresholded mask (white = house) for the pipeline's mask pass; None without a mask."""
    import bpy

    def lin(hexstr):
        h = hexstr.lstrip("#")
        c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1.0]

    def img_node(nt, path, cs):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(path, check_existing=True)
        n.image.colorspace_settings.name = cs
        n.interpolation = "Closest"
        n.extension = "CLIP"
        return n

    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    tex = img_node(nt, tex_path, "sRGB")
    col = tex.outputs["Color"]
    mm = None
    if mask_path:
        mk = img_node(nt, mask_path, "Non-Color")
        thr = nt.nodes.new("ShaderNodeMath")
        thr.operation = "GREATER_THAN"
        thr.inputs[1].default_value = mask_threshold
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(mk.outputs["Color"], sep.inputs[0])
        nt.links.new(sep.outputs[0], thr.inputs[0])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.inputs[7].default_value = lin(house_hex)
        nt.links.new(thr.outputs[0], mix.inputs[0])
        nt.links.new(col, mix.inputs[6])
        col = mix.outputs[2]
        # mask-pass material: emission = thresholded mask
        mm = bpy.data.materials.new(name + "_mask")
        mm.use_nodes = True
        t2 = mm.node_tree
        for n in list(t2.nodes):
            t2.nodes.remove(n)
        mk2 = img_node(t2, mask_path, "Non-Color")
        sp2 = t2.nodes.new("ShaderNodeSeparateColor")
        th2 = t2.nodes.new("ShaderNodeMath")
        th2.operation = "GREATER_THAN"
        th2.inputs[1].default_value = mask_threshold
        em = t2.nodes.new("ShaderNodeEmission")
        out = t2.nodes.new("ShaderNodeOutputMaterial")
        t2.links.new(mk2.outputs["Color"], sp2.inputs[0])
        t2.links.new(sp2.outputs[0], th2.inputs[0])
        t2.links.new(th2.outputs[0], em.inputs["Color"])
        t2.links.new(em.outputs[0], out.inputs[0])
    nt.links.new(col, bsdf.inputs["Base Color"])
    return m, mm
