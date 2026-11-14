# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Authority UI skin art (M10-15, D-243): bevelled stamped-steel panels with rivets, olive/khaki finish.

Usage:
  blender-less placeholder:  python tools/art/ui/auth_skin.py --placeholder [--out DIR]
  real render (bpy):         python tools/art/ui/auth_skin.py [--samples 128] [--out DIR]

Each piece is modelled in Blender (straight-on orthographic camera), rendered at 2x and downscaled with Pillow to the size the
9-slice needs. Writes the PNGs (RGBA, hard alpha) and skin.toml into --out (default data/ui/skins/authority).
"""
import argparse
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
DEFAULT_OUT = os.path.join(ROOT, "data", "ui", "skins", "authority")

# name -> (w, h, slice (l,t,r,b) or None, kind)
PIECES = {
    "panel": (48, 48, (12, 12, 12, 12), "panel"),
    "button": (48, 28, (8, 8, 8, 8), "button"),
    "button_hover": (48, 28, (8, 8, 8, 8), "hover"),
    "button_pressed": (48, 28, (8, 8, 8, 8), "pressed"),
    "button_disabled": (48, 28, (8, 8, 8, 8), "disabled"),
    "tab": (48, 24, (8, 8, 8, 8), "tab"),
    "tab_active": (48, 24, (8, 8, 8, 8), "tab_active"),
    "sidebar_back": (240, 720, None, "back"),
    "minimap_frame": (96, 96, (10, 10, 10, 10), "frame"),
    "card_frame": (64, 64, (10, 10, 10, 10), "card"),
}

TEXT_COLOR, TEXT_DIM, ACCENT = "#E4DDB8", "#9A9670", "#C8A838"

# base colour (olive/khaki) per kind, sRGB 0-255
COLORS = {
    "panel": (92, 98, 66), "button": (108, 112, 76), "hover": (130, 134, 90), "pressed": (74, 79, 54),
    "disabled": (84, 84, 76), "tab": (84, 90, 62), "tab_active": (122, 124, 84), "back": (62, 67, 48),
    "frame": (88, 94, 64), "card": (96, 100, 70),
}


def write_toml(out):
    lines = ['name = "Authority"', f'text_color = "{TEXT_COLOR}"', f'text_dim = "{TEXT_DIM}"', f'accent = "{ACCENT}"', ""]
    for name, (_, _, sl, _) in PIECES.items():
        lines.append(f"[{name}]")
        lines.append(f'file = "{name}.png"')
        if sl:
            lines.append(f"slice = [{sl[0]}, {sl[1]}, {sl[2]}, {sl[3]}]")
        lines.append("")
    with open(os.path.join(out, "skin.toml"), "w", newline="\n") as f:
        f.write("\n".join(lines))


def placeholders(out):
    """Tiny flat-colour pieces with a 1 px darker border; no Blender needed."""
    for name, (w, h, sl, kind) in PIECES.items():
        c = COLORS[kind]
        im = Image.new("RGBA", (w, h), c + (255,))
        d = ImageDraw.Draw(im)
        d.rectangle([0, 0, w - 1, h - 1], outline=tuple(max(0, v - 40) for v in c) + (255,))
        if kind == "frame":
            s = sl[0]
            d.rectangle([s, s, w - s - 1, h - s - 1], fill=(0, 0, 0, 0))
        im.save(os.path.join(out, name + ".png"))


# ---------------------------------------------------------------- Blender part
def srgb_to_lin(v):
    v = v / 255.0
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def render_all(out, samples):
    import bpy
    import bmesh  # noqa: F401
    import tempfile

    U = 0.01  # metres per pixel (at 1x)
    tmp = tempfile.mkdtemp(prefix="auth_skin_")

    def reset():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        sc = bpy.context.scene
        sc.render.engine = "CYCLES"
        sc.cycles.device = "CPU"
        sc.cycles.samples = samples
        sc.cycles.use_denoising = False
        sc.render.film_transparent = True
        sc.render.image_settings.file_format = "PNG"
        sc.render.image_settings.color_mode = "RGBA"
        sc.view_settings.view_transform = "Standard"
        return sc

    def material(name, rgb, metallic=0.55, rough=0.42, bump=0.25):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        bsdf = nt.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = tuple(srgb_to_lin(v) for v in rgb) + (1,)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = rough
        tc = nt.nodes.new("ShaderNodeTexCoord")
        noise = nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 90.0
        noise.inputs["Detail"].default_value = 6.0
        mapn = nt.nodes.new("ShaderNodeMapping")
        mapn.inputs["Scale"].default_value = (1.0, 0.06, 1.0)  # brushed streaks
        bp = nt.nodes.new("ShaderNodeBump")
        bp.inputs["Strength"].default_value = bump
        bp.inputs["Distance"].default_value = 0.002
        nt.links.new(tc.outputs["Object"], mapn.inputs["Vector"])
        nt.links.new(mapn.outputs["Vector"], noise.inputs["Vector"])
        nt.links.new(noise.outputs["Fac"], bp.inputs["Height"])
        nt.links.new(bp.outputs["Normal"], bsdf.inputs["Normal"])
        return m

    def box(name, cx, cy, z0, sx, sy, sz, mat, bevel):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(cx, cy, z0 + sz / 2))
        o = bpy.context.active_object
        o.name = name
        o.scale = (sx, sy, sz)
        bpy.ops.object.transform_apply(scale=True)
        o.data.materials.append(mat)
        if bevel > 0:
            b = o.modifiers.new("bev", "BEVEL")
            b.width = bevel
            b.segments = 3
            b.limit_method = "ANGLE"
        return o

    def rivet(x, y, z, r, mat):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=(x, y, z), segments=16, ring_count=8)
        o = bpy.context.active_object
        o.scale = (1, 1, 0.55)
        o.data.materials.append(mat)
        bpy.ops.object.shade_smooth()

    def plate_set(w, h, kind, sl):
        """Build one piece centred on the origin. Units: metres, 1 px = U."""
        W, H = w * U, h * U
        base = COLORS[kind]
        steel = material("steel", base)
        dark = material("dark", tuple(int(v * 0.62) for v in base), metallic=0.4, rough=0.55)
        light = material("light", tuple(min(255, int(v * 1.22)) for v in base))
        rivm = material("rivet", tuple(min(255, int(v * 1.12)) for v in base), metallic=0.8, rough=0.3, bump=0.05)
        if kind == "disabled":
            steel = material("steel", base, metallic=0.2, rough=0.7)
        sunk = kind in ("pressed",)
        bev = 2.0 * U
        if kind == "back":
            box("base", 0, 0, 0, W, H, 3 * U, steel, bev)
            # vertical stamped seams and horizontal rails
            for fx in (0.04, 0.96):
                box("seam", (fx - 0.5) * W, 0, 3 * U, 3 * U, H, 1.5 * U, dark, 0.4 * U)
            n = 6
            for i in range(1, n):
                yy = (i / n - 0.5) * H
                box("rail", 0, yy, 3 * U, W, 2 * U, 1.0 * U, dark, 0.3 * U)
                for fx in (0.08, 0.92):
                    rivet((fx - 0.5) * W, yy + 5 * U, 4 * U, 1.6 * U, rivm)
            return W, H
        if kind == "frame":
            s = sl[0] * U
            # four bars forming a frame, inner hole open
            t = 4 * U
            box("L", -W / 2 + s / 2, 0, 0, s, H, t, steel, bev)
            box("R", W / 2 - s / 2, 0, 0, s, H, t, steel, bev)
            box("T", 0, H / 2 - s / 2, 0, W - 2 * s, s, t, steel, bev)
            box("B", 0, -H / 2 + s / 2, 0, W - 2 * s, s, t, steel, bev)
            # inner lip (dark, lower) around the hole
            lip = 1.5 * U
            box("lipT", 0, H / 2 - s - lip / 2, 0, W - 2 * s, lip, t + 1.5 * U, dark, 0.3 * U)
            box("lipB", 0, -H / 2 + s + lip / 2, 0, W - 2 * s, lip, t + 1.5 * U, dark, 0.3 * U)
            box("lipL", -W / 2 + s + lip / 2, 0, 0, lip, H - 2 * s, t + 1.5 * U, dark, 0.3 * U)
            box("lipR", W / 2 - s - lip / 2, 0, 0, lip, H - 2 * s, t + 1.5 * U, dark, 0.3 * U)
            for sx in (-1, 1):
                for sy in (-1, 1):
                    rivet(sx * (W / 2 - s / 2), sy * (H / 2 - s / 2), t, 2.6 * U, rivm)
            return W, H
        m = sl[0] * U if sl else 6 * U
        thick = 3 * U
        box("base", 0, 0, 0, W, H, thick, steel, bev)
        # stamped inner field: raised for normal, lowered/darker for pressed
        inset = m * 0.55
        field = dark if sunk else (light if kind in ("hover", "tab_active") else steel)
        fz = thick if not sunk else thick - 0.01
        box("field", 0, 0, fz, W - 2 * inset, H - 2 * inset, (2.0 if not sunk else 0.2) * U, field, 0.7 * U)
        if kind in ("panel", "card"):
            # extra stamped border groove
            g = m * 0.9
            box("groove", 0, 0, thick, W - 2 * g, H - 2 * g, 0.6 * U, dark, 0.2 * U)
            box("field2", 0, 0, thick, W - 2 * g - 3 * U, H - 2 * g - 3 * U, 1.3 * U, steel, 0.4 * U)
        rr = (2.4 if kind in ("panel", "card") else 1.8) * U
        off = m * 0.5 + (0.5 * U if kind not in ("panel", "card") else 0)
        z = thick + 0.3 * U
        for sx in (-1, 1):
            for sy in (-1, 1):
                rivet(sx * (W / 2 - off), sy * (H / 2 - off), z, rr, rivm)
        return W, H

    for name, (w, h, sl, kind) in PIECES.items():
        sc = reset()
        # 2x supersample, then downscale
        rw, rh = w * 2, h * 2
        sc.render.resolution_x, sc.render.resolution_y = rw, rh
        sc.render.resolution_percentage = 100
        plate_set(w, h, kind, sl)
        cam_d = bpy.data.cameras.new("cam")
        cam_d.type = "ORTHO"
        cam_d.ortho_scale = max(w, h) * U
        cam = bpy.data.objects.new("cam", cam_d)
        sc.collection.objects.link(cam)
        cam.location = (0, 0, 2.0)
        sc.camera = cam
        # sun upper left (D-184 sun rule), shallow so bevels read
        sun_d = bpy.data.lights.new("sun", "SUN")
        sun_d.energy = 4.5
        sun_d.angle = 0.1
        sun = bpy.data.objects.new("sun", sun_d)
        sc.collection.objects.link(sun)
        import math
        sun.rotation_euler = (math.radians(38), math.radians(-30), math.radians(-35))
        w_ = bpy.data.worlds.new("w")
        w_.use_nodes = True
        w_.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.57, 0.5, 1)
        w_.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5
        sc.world = w_
        p = os.path.join(tmp, name + "_raw.png")
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)
        im = Image.open(p).convert("RGBA").resize((w, h), Image.LANCZOS)
        a = im.getchannel("A").point(lambda v: 255 if v >= 128 else 0)  # hard alpha, no premultiply
        im.putalpha(a)
        im.save(os.path.join(out, name + ".png"))
        print("wrote", name)


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--samples", type=int, default=128)
    ap.add_argument("--placeholder", action="store_true", help="flat-colour Pillow placeholders, no Blender")
    a = ap.parse_args(argv)
    os.makedirs(a.out, exist_ok=True)
    if a.placeholder:
        placeholders(a.out)
    else:
        render_all(a.out, a.samples)
    write_toml(a.out)
    print("skin written to", a.out)


if __name__ == "__main__":
    main(sys.argv[1:])
