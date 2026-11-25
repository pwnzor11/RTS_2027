#!/usr/bin/env python3
"""Volumetric smoke for the damage effects (S43 pilot): Cycles volumes instead of the bubbly puff stacks.

Writes sheets with the existing names, frame sizes and origins, so the game needs no code or manifest change:
  data/sprites/fx/smoke_plume.png   12 frames 32x64, loops seamlessly, origin = base-centre pixel (16, 63)
  data/sprites/fx/puffs_smoke.png   4 frames 32x32: a dark wispy puff that grows and thins out (PuffArt contract)
  data/sprites/fx/puffs_dust.png    4 frames 32x32: the same for dust (lighter, flatter)

Usage (wrap in the render lock):
  flock /tmp/render.lock nice -n 5 python3 tools/art/fx/build_smoke_vol.py [--what plume|puffs|all] [--samples 12]
        [--out DIR] [--frames 0,4,8] [--raw DIR]
  --out DIR    write the sheets here (default data/sprites/fx); previews write to a scratch folder
  --frames     render only these frame indices (preview: the sheet then holds only those frames)
  --raw DIR    keep every 4x render as a PNG for inspection

Look: orthographic camera at the game's 40 degrees, one sun from the upper left (north-west; shadows fall south-east), no
other light (docs/design/18_art_guidance.md section 7). Each frame renders at 4x and is reduced by 4x4 box averages: the alpha
becomes coverage, the lit luminance picks the shade. Edges and partial alpha are hard pixels from the ordered Bayer dither that
build_fx.py uses; smoke takes build_fx.SMOKE (five brown-greys, soot-dark base, paler top) with no dark outline (that read as bubbles); puffs take the
three grey shades of import_kenney_puffs.py (255 / 228 / 198). The puffs thin out through their density (no fixed hole
grid, which read as a sieve); the Bayer threshold keeps the edges hard.

The plume's detail noise flows upward and loops: it is sampled at p - U*t and p - U*(t-1) and cross-faded by t = frame/12, so
frame 12 equals frame 0. The column drifts west (screen left, -X) with the square of its height.
"""
import argparse
import math
import os
import tempfile
import time

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT_DEFAULT = os.path.join(ROOT, "data", "sprites", "fx")

SS = 4                                  # render scale over the sheet pixels
ELEV = math.radians(40.0)               # game camera: orthographic, 40 degrees above the horizon
SMOKE_RAMP = ["#26231F", "#423E39", "#615C55", "#847E74", "#A9A296"]   # build_fx.SMOKE, darkest first
PUFF_SHADES = (255, 228, 198)           # import_kenney_puffs.SHADES
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0

PLUME_W, PLUME_H, PLUME_N = 32, 64, 12
PLUME_TOP = 17.5                        # world height of the column
PLUME_DRIFT = 1.6                       # world units west at the top
PUFF_N, PUFF_PX = 4, 32
PUFF_RADII = (2.2, 2.8, 3.3, 3.8)       # world radius per frame (about 15, 20, 23, 27 px across at 1x)
PUFF_DENS = {"smoke": (5.0, 4.0, 2.6, 1.4), "dust": (4.0, 3.2, 2.0, 1.0)}   # density per frame: thins out


def hx(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


SMOKE = np.array([hx(c) for c in SMOKE_RAMP], dtype=np.uint8)


def bpy_mod():
    import bpy
    return bpy


# ------------------------------------------------------------------------------------------ scene
def new_scene(bpy, samples, res_px, ortho_scale):
    """A clean scene: orthographic camera at 40 degrees looking north, one sun from the north-west, transparent film."""
    from mathutils import Vector
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    cy = sc.cycles
    cy.device = "CPU"
    cy.samples = samples
    cy.use_denoising = False
    cy.volume_bounces = 1
    cy.volume_step_rate = 0.5
    sc.render.resolution_x, sc.render.resolution_y = res_px
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = True
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.image_settings.color_depth = "8"
    view = Vector((0.0, math.cos(ELEV), -math.sin(ELEV)))          # camera looks north and down
    cd = bpy.data.cameras.new("cam")
    cd.type = "ORTHO"
    cd.ortho_scale = ortho_scale
    cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam)
    cam.location = -view * 60.0                                     # the origin is the look-at point
    cam.rotation_euler = view.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    # sun from the north-west, 40 degrees up: its +Z axis points at the sun
    to_sun = Vector((-math.cos(ELEV) * 0.7071, math.cos(ELEV) * 0.7071, math.sin(ELEV)))
    sd = bpy.data.lights.new("sun", "SUN")
    sd.energy = 4.0
    sun = bpy.data.objects.new("sun", sd)
    sc.collection.objects.link(sun)
    sun.rotation_euler = to_sun.to_track_quat("Z", "Y").to_euler()
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.55, 0.55, 0.55, 1.0)            # soft ambient fill for the shaded faces
    bg.inputs[1].default_value = 0.35
    sc.world = world
    return sc, cam


def place_origin(bpy, sc, cam, xy):
    """Move the camera shift so the world origin lands at normalised frame position xy (0..1, from the bottom left)."""
    from bpy_extras.object_utils import world_to_camera_view
    from mathutils import Vector
    origin = Vector((0.0, 0.0, 0.0))

    def at():
        bpy.context.view_layer.update()
        c = world_to_camera_view(sc, cam, origin)
        return c.x, c.y

    cam.data.shift_x, cam.data.shift_y = 0.0, 0.0
    _, y0 = at()
    cam.data.shift_y = 0.1
    _, y1 = at()
    cam.data.shift_y = (xy[1] - y0) / ((y1 - y0) / 0.1)
    xr, yr = at()
    assert abs(xr - 0.5) < 1e-3 and abs(yr - xy[1]) < 1e-3, (xr, yr, xy)   # the origin is centred in x, on the row


# ------------------------------------------------------------------------------------------ shader helpers
class Nodes:
    """Shader-tree helper: maths, vector maths, noise, map range, the volume output."""

    def __init__(self, mat):
        self.nt = mat.node_tree
        self.L = self.nt.links
        self.N = self.nt.nodes

    def _set(self, sock, v):
        if isinstance(v, (int, float)):
            sock.default_value = float(v)
        elif isinstance(v, tuple):
            sock.default_value = v
        else:
            self.L.new(v, sock)

    def math(self, op, a, b=None, c=None, clamp=False):
        n = self.N.new("ShaderNodeMath")
        n.operation = op
        n.use_clamp = clamp
        for k, v in enumerate((a, b, c)):
            if v is not None:
                self._set(n.inputs[k], v)
        return n.outputs[0]

    def vmath(self, op, a, b=None, scale=None):
        n = self.N.new("ShaderNodeVectorMath")
        n.operation = op
        self._set(n.inputs[0], a)
        if b is not None:
            self._set(n.inputs[1], b)
        if scale is not None:
            self._set(n.inputs[3], scale)
        return n.outputs[0]

    def sep(self, v):
        n = self.N.new("ShaderNodeSeparateXYZ")
        self.L.new(v, n.inputs[0])
        return n.outputs[0], n.outputs[1], n.outputs[2]

    def noise(self, vec, scale, detail, rough=0.5, distortion=0.0):
        n = self.N.new("ShaderNodeTexNoise")
        n.noise_dimensions = "3D"
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = rough
        n.inputs["Distortion"].default_value = distortion
        self._set(n.inputs["Vector"], vec)
        return n.outputs[0]                   # the Fac socket: "Fac" in 4.x, "Factor" in 5.x, index 0 in both

    def map(self, v, a0, a1, b0=0.0, b1=1.0):
        n = self.N.new("ShaderNodeMapRange")
        n.interpolation_type = "SMOOTHSTEP"
        n.clamp = True
        self._set(n.inputs[0], v)
        n.inputs[1].default_value = a0
        n.inputs[2].default_value = a1
        n.inputs[3].default_value = b0
        n.inputs[4].default_value = b1
        return n.outputs[0]

    def value(self, v):
        n = self.N.new("ShaderNodeValue")
        n.outputs[0].default_value = v
        return n.outputs[0]

    def position(self):
        return self.N.new("ShaderNodeNewGeometry").outputs["Position"]

    def volume(self, density, albedo):
        out = self.N.new("ShaderNodeOutputMaterial")
        pv = self.N.new("ShaderNodeVolumePrincipled")
        pv.inputs["Color"].default_value = (albedo, albedo, albedo, 1.0)
        pv.inputs["Anisotropy"].default_value = 0.3
        pv.inputs["Emission Strength"].default_value = 0.0
        self._set(pv.inputs["Density"], density)
        self.L.new(pv.outputs[0], out.inputs["Volume"])


def new_material(bpy, name):
    m = bpy.data.materials.new(name)
    if not m.use_nodes:
        m.use_nodes = True
    for n in list(m.node_tree.nodes):
        m.node_tree.nodes.remove(n)
    return m


def plume_material(bpy):
    """Column density. Returns (material, t_value): set t_value to frame / PLUME_N before each render."""
    m = new_material(bpy, "smoke_plume_vol")
    g = Nodes(m)
    t_value = g.value(0.0)
    pos = g.position()
    x, y, z = g.sep(pos)
    tt = g.math("DIVIDE", g.math("SUBTRACT", z, -0.3), PLUME_TOP, clamp=True)                 # 0 at the base, 1 at the top
    cx = g.math("SUBTRACT", 0.0, g.math("MULTIPLY", g.math("MULTIPLY", tt, tt), PLUME_DRIFT))  # west drift, t squared
    dx = g.math("SUBTRACT", x, cx)
    rad = g.math("MULTIPLY", 1.8, g.math("ADD", 1.0, g.math("MULTIPLY", tt, 0.6)))           # radius grows with height
    lob = g.noise(g.vmath("ADD", pos, (3.7, 1.9, 8.3)), 0.9, 3.0, 0.7, 0.6)                  # cauliflower lobes
    dist = g.math("DIVIDE", g.math("SQRT", g.math("ADD", g.math("MULTIPLY", dx, dx), g.math("MULTIPLY", y, y))),
                  g.math("MULTIPLY", rad, g.math("ADD", 0.6, lob)))
    fall = g.map(dist, 0.9, 0.35)                                                              # 1 in the core, 0 at the rim (sharp)
    # wisps flow upward and loop: cross-fade of two samples, p - U t and p - U (t - 1), weight t
    U = 0.9
    vA = g.vmath("ADD", pos, g.vmath("SCALE", (0.0, 0.0, 1.0), g.math("MULTIPLY", t_value, -U)))
    vB = g.vmath("ADD", pos, g.vmath("SCALE", (0.0, 0.0, 1.0), g.math("MULTIPLY", g.math("SUBTRACT", t_value, 1.0), -U)))
    nA = g.noise(vA, 1.6, 6.0, 0.62)
    nB = g.noise(vB, 1.6, 6.0, 0.62)
    det = g.math("ADD", g.math("MULTIPLY", nA, g.math("SUBTRACT", 1.0, t_value)), g.math("MULTIPLY", nB, t_value))
    detm = g.map(det, 0.24, 0.58)
    bite = g.math("ADD", g.math("MULTIPLY", detm, g.math("SUBTRACT", 1.0, g.math("MULTIPLY", fall, 0.6))),
                  g.math("MULTIPLY", fall, 0.6))                                              # the noise bites harder at the rim
    tin = g.math("MULTIPLY", tt, 8.0, clamp=True)                                              # no hard base
    tout = g.math("SUBTRACT", 1.0, g.math("MULTIPLY", g.math("SUBTRACT", tt, 0.6), 2.0), clamp=True)   # dissolves at the top
    shape = g.math("MULTIPLY", g.math("MULTIPLY", fall, bite), g.math("MULTIPLY", tin, tout))
    dens = g.math("MULTIPLY", g.math("MULTIPLY", shape, 2.2), g.math("SUBTRACT", 1.4, g.math("MULTIPLY", tt, 0.8)))
    g.volume(dens, 0.95)
    return m, t_value


def puff_material(bpy, name, r, cx_off, flat, dens0, albedo, warp, seed, breakup, stretch=1.0):
    """One billowing puff at the origin: an ellipsoid falloff broken by noise; density falls to zero at the rim."""
    m = new_material(bpy, name)
    g = Nodes(m)
    pos = g.position()
    x, y, z = g.sep(pos)
    ex = g.math("DIVIDE", g.math("SUBTRACT", x, cx_off), r * stretch)
    ey = g.math("DIVIDE", y, r)
    ez = g.math("DIVIDE", z, r * flat)
    d0 = g.math("SQRT", g.math("ADD", g.math("ADD", g.math("MULTIPLY", ex, ex), g.math("MULTIPLY", ey, ey)),
                               g.math("MULTIPLY", ez, ez)))
    wn = g.noise(g.vmath("ADD", pos, (seed * 3.1, seed * 5.3, seed * 1.7)), 1.0 / r, 3.0, 0.6, 0.4)
    d = g.math("ADD", d0, g.math("MULTIPLY", g.math("SUBTRACT", wn, 0.5), warp))
    fall = g.map(d, 0.3, 1.0, 1.0, 0.0)
    det = g.noise(g.vmath("ADD", pos, (seed * 5.9, seed * 11.1, seed * 2.3)), breakup / r, 6.0, 0.6)
    detm = g.map(det, 0.36, 0.6)
    bite = g.math("ADD", g.math("MULTIPLY", detm, g.math("SUBTRACT", 1.0, g.math("MULTIPLY", fall, 0.5))),
                  g.math("MULTIPLY", fall, 0.5))
    g.volume(g.math("MULTIPLY", g.math("MULTIPLY", fall, bite), dens0), albedo)
    return m


def hull_sphere(bpy, material, size):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1.0, segments=24, ring_count=12, location=(0, 0, 0))
    o = bpy.context.active_object
    o.scale = size
    o.data.materials.append(material)
    return o


def plume_hull(bpy, material, z0, h, r0, growth, drift, pad, segs=20, ring=12):
    """Bounding tube of the column (the volume's domain), following the drift curve."""
    import bmesh
    me = bpy.data.meshes.new("plume_hull")
    bm = bmesh.new()
    rings = []
    for k in range(segs + 1):
        t = k / segs
        r = r0 * (1 + t * growth) * pad
        cx, cz = -t * t * drift, z0 + t * h
        rings.append([bm.verts.new((cx + r * math.cos(a), r * math.sin(a), cz))
                      for a in (2 * math.pi * j / ring for j in range(ring))])
    for k in range(segs):
        for j in range(ring):
            bm.faces.new((rings[k][j], rings[k][(j + 1) % ring], rings[k + 1][(j + 1) % ring], rings[k + 1][j]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("plume_hull", me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(material)
    return o


# ------------------------------------------------------------------------------------------ renders
def render(bpy, path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.float32) / 255.0


def render_plume(bpy, samples, f, tmp):
    sc, cam = new_scene(bpy, samples, (PLUME_W * SS, PLUME_H * SS), 18.0)
    place_origin(bpy, sc, cam, (0.5, 2.0 / (PLUME_H * SS)))           # base-centre pixel on the bottom row
    m, tv = plume_material(bpy)
    tv.default_value = f / PLUME_N
    plume_hull(bpy, m, -0.3, PLUME_TOP + 0.3, 1.8, 0.6, PLUME_DRIFT, 1.7)
    return render(bpy, os.path.join(tmp, f"plume_{f:02d}.png"))


def render_puff(bpy, samples, kind, f, tmp):
    sc, cam = new_scene(bpy, samples, (PUFF_PX * SS, PUFF_PX * SS), 9.0)
    place_origin(bpy, sc, cam, (0.5, 0.5))
    r = PUFF_RADII[f]
    if kind == "smoke":
        m = puff_material(bpy, "puff_smoke", r, -0.25 * f / 3.0, 0.85, PUFF_DENS["smoke"][f], 0.55, 1.2, 2.0 + f, 1.2, stretch=1.3)
        hull_sphere(bpy, m, (r * 1.6, r * 1.6, r * 1.6 * 0.85))
    else:
        m = puff_material(bpy, "puff_dust", r, 0.0, 0.6, PUFF_DENS["dust"][f], 0.9, 1.0, 5.0 + f, 1.0, stretch=1.2)
        hull_sphere(bpy, m, (r * 1.6, r * 1.6, r * 1.6 * 0.6))
    return render(bpy, os.path.join(tmp, f"{kind}_{f}.png"))


# ------------------------------------------------------------------------------------------ reduce and dither
def reduce(rgba):
    """4x render -> (coverage, lit luminance), both at sheet size; luminance is weighted by coverage."""
    H, W = rgba.shape[0] // SS, rgba.shape[1] // SS
    blk = rgba.reshape(H, SS, W, SS, 4)
    a = blk[..., 3].mean(axis=(1, 3))
    lum = blk[..., :3].mean(axis=4)
    lw = (lum * blk[..., 3]).sum(axis=(1, 3)) / np.maximum(blk[..., 3].sum(axis=(1, 3)), 1e-6)
    return a, lw


def edge_mask(keep):
    p = np.pad(keep, 1, constant_values=False)
    inner = p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:]
    return keep & ~inner


def plume_sheet(frames):
    yy, xx = np.mgrid[0:PLUME_H, 0:PLUME_W]
    thr = BAYER[yy % 4, xx % 4] + 1.0 / 32.0                          # ordered dither of the partial alpha
    shade_dither = BAYER[(yy + 2) % 4, (xx + 1) % 4] - 0.5           # build_fx: bayer(x + 1, y + 2)
    hf = np.clip((PLUME_H - 1 - yy) / 50.0, 0.0, 1.0)                # 0 at the base (soot), 1 at the top (pale)
    vals = np.concatenate([lw[a > 0.5] for a, lw in frames])
    lo, hi = np.percentile(vals, 6), np.percentile(vals, 94)
    out = np.zeros((PLUME_H, PLUME_W * len(frames), 4), dtype=np.uint8)
    for i, (a, lw) in enumerate(frames):
        v = np.clip((lw - lo) / max(hi - lo, 1e-3), 0.0, 1.0)
        s = 0.4 * v + 0.45 * hf + 0.3 + 0.2 * shade_dither
        idx = np.clip(np.floor(s * 5.0), 0, 4).astype(int)
        keep = a >= thr
        cell = np.zeros((PLUME_H, PLUME_W, 4), dtype=np.uint8)
        cell[keep, :3] = SMOKE[idx[keep]]
        cell[keep, 3] = 255
        out[:, i * PLUME_W:(i + 1) * PLUME_W] = cell
    return out


def puff_sheet(frames, dust):
    yy, xx = np.mgrid[0:PUFF_PX, 0:PUFF_PX]
    thr = BAYER[yy % 4, xx % 4] + 1.0 / 32.0
    vals = np.concatenate([lw[a > 0.5] for a, lw in frames])
    lo, hi = np.percentile(vals, 6), np.percentile(vals, 94)
    out = np.zeros((PUFF_PX, PUFF_PX * len(frames), 4), dtype=np.uint8)
    for i, (a, lw) in enumerate(frames):
        v = np.clip((lw - lo) / max(hi - lo, 1e-3), 0.0, 1.0)
        g = np.where(v > 0.62, PUFF_SHADES[0], np.where(v > 0.38, PUFF_SHADES[1], PUFF_SHADES[2])).astype(np.uint8)
        keep = a >= thr
        cell = np.zeros((PUFF_PX, PUFF_PX, 4), dtype=np.uint8)
        cell[..., :3] = g[..., None]
        cell[..., 3] = np.where(keep, 255, 0)
        out[:, i * PUFF_PX:(i + 1) * PUFF_PX] = cell
    return out


# ------------------------------------------------------------------------------------------ main
def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--what", default="all", choices=["plume", "puffs", "all"])
    ap.add_argument("--samples", type=int, default=12)
    ap.add_argument("--out", default=OUT_DEFAULT)
    ap.add_argument("--frames", default="", help="comma list of frame indices to render (preview)")
    ap.add_argument("--raw", default="", help="folder that keeps the 4x renders")
    args = ap.parse_args()
    bpy = bpy_mod()
    tmp = args.raw or tempfile.mkdtemp(prefix="smokevol_")
    os.makedirs(tmp, exist_ok=True)
    os.makedirs(args.out, exist_ok=True)
    sel = lambda n: [int(s) for s in args.frames.split(",")] if args.frames else list(range(n))
    if args.what in ("plume", "all"):
        frames, t0 = [], time.time()
        idx = sel(PLUME_N)
        for f in idx:
            frames.append(reduce(render_plume(bpy, args.samples, f, tmp)))
        out = plume_sheet(frames)
        Image.fromarray(out, "RGBA").save(os.path.join(args.out, "smoke_plume.png"))
        print(f"smoke_plume: {len(idx)} frames, {(time.time() - t0) / len(idx):.1f} s per frame at {args.samples} samples")
    if args.what in ("puffs", "all"):
        for kind, name, dust in (("smoke", "puffs_smoke", False), ("dust", "puffs_dust", True)):
            frames, t0 = [], time.time()
            idx = sel(PUFF_N)
            for f in idx:
                frames.append(reduce(render_puff(bpy, args.samples, kind, f, tmp)))
            Image.fromarray(puff_sheet(frames, dust), "RGBA").save(os.path.join(args.out, name + ".png"))
            print(f"{name}: {len(idx)} frames, {(time.time() - t0) / len(idx):.1f} s per frame at {args.samples} samples")


if __name__ == "__main__":
    main()
