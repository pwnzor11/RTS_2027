# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Authority UI skin art (M10-15, D-243), pilot 2: worn olive paint on gunmetal frames, rivets, weld beads, stencil plate, hazard
stripe and an original emblem plate.

Usage:
  placeholder (no Blender):  python tools/art/ui/auth_skin.py --placeholder [--out DIR]
  real render (bpy):         python tools/art/ui/auth_skin.py [--samples 128] [--out DIR]

Each piece is modelled in Blender (straight-on orthographic camera), rendered at 2x and downscaled with Pillow to the size the
9-slice needs. Writes the PNGs (RGBA, hard alpha) and skin.toml into --out (default data/ui/skins/authority).
"""
import argparse
import math
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
DEFAULT_OUT = os.path.join(ROOT, "data", "ui", "skins", "authority")

# name -> (w, h, slice (l,t,r,b) or None, kind)   sizes in 1x pixels
PIECES = {
    "panel": (48, 48, (12, 12, 12, 12), "panel"),
    "button": (48, 28, (8, 8, 8, 8), "button"),
    "button_hover": (48, 28, (8, 8, 8, 8), "hover"),
    "button_pressed": (48, 28, (8, 8, 8, 8), "pressed"),
    "button_disabled": (48, 28, (8, 8, 8, 8), "disabled"),
    "tab": (48, 24, (8, 8, 8, 8), "tab"),
    "tab_active": (48, 24, (8, 8, 8, 8), "tab_active"),
    "sidebar_back": (256, 720, None, "back"),
    "minimap_frame": (96, 96, (12, 12, 12, 12), "frame"),
    "card_frame": (64, 64, (12, 12, 12, 12), "card"),
}

TEXT_COLOR, TEXT_DIM, ACCENT = "#E4DDB8", "#9A9670", "#C8A838"

OLIVE = (92, 100, 62)
GUNMETAL = (52, 58, 62)
COLORS = {  # placeholder colours / paint colours per kind (sRGB)
    "panel": OLIVE, "button": (104, 112, 70), "hover": (128, 136, 82), "pressed": (74, 80, 50),
    "disabled": (88, 88, 80), "tab": (84, 92, 58), "tab_active": (120, 126, 76), "back": (70, 78, 50),
    "frame": OLIVE, "card": (96, 104, 66),
}

# sidebar_back layout in 1x pixels from the top-left (SidebarLayout: pad 4, credits row 24 high)
BACK_CREDITS = (6, 5, 250, 27)       # stencil plate under the credits text
BACK_EMBLEM = (128, 432, 27)         # emblem plate centre x, y and radius
BACK_SEAMS = (118, 226, 340, 580)    # horizontal weld seams (y)


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


# ---------------------------------------------------------------- decal textures (Pillow, drawn at 4x of the 1x size)
def make_back_decal(path, w, h, S=4):
    """RGBA decal for sidebar_back: hazard stripe L (top + left), stencil-plate marks, emblem."""
    im = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    yel, blk = (206, 168, 40, 255), (28, 28, 26, 255)
    # hazard stripes: band across the top (4 px) and down the left edge (4 px), 45 degree stripes, 8 px period
    band = 4
    period = 8
    for (x0, y0, x1, y1) in ((0, 0, w, band), (0, 0, band, h)):
        for k in range(-h // period - 2, (w + h) // period + 2):
            pts = [(k * period, 0), (k * period + period / 2, 0), (k * period + period / 2 - h, h), (k * period - h, h)]
            layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
            ImageDraw.Draw(layer).polygon([(x * S, y * S) for x, y in pts], fill=yel)
            mask = Image.new("L", im.size, 0)
            ImageDraw.Draw(mask).rectangle([x0 * S, y0 * S, x1 * S - 1, y1 * S - 1], fill=255)
            im.paste(layer, (0, 0), Image.composite(layer.getchannel("A"), Image.new("L", im.size, 0), mask))
        # black gaps
        mask = Image.new("L", im.size, 0)
        ImageDraw.Draw(mask).rectangle([x0 * S, y0 * S, x1 * S - 1, y1 * S - 1], fill=255)
        under = Image.new("RGBA", im.size, blk)
        base = Image.new("RGBA", im.size, (0, 0, 0, 0))
        base.paste(under, (0, 0), mask)
        base.alpha_composite(im)
        im = Image.alpha_composite(Image.new("RGBA", im.size, (0, 0, 0, 0)), base)
    d = ImageDraw.Draw(im)
    cream = (214, 204, 160, 255)
    # stencil plate: corner brackets, ruled dashes along the bottom, end ticks
    x0, y0, x1, y1 = BACK_CREDITS
    for (cx, cy, sx, sy) in ((x0 + 2, y0 + 2, 1, 1), (x1 - 2, y0 + 2, -1, 1), (x0 + 2, y1 - 2, 1, -1), (x1 - 2, y1 - 2, -1, -1)):
        d.line([(cx * S, cy * S), ((cx + 5 * sx) * S, cy * S)], fill=cream, width=S)
        d.line([(cx * S, cy * S), (cx * S, (cy + 5 * sy) * S)], fill=cream, width=S)
    for i in range(0, int(x1 - x0 - 24), 6):
        xx = x0 + 12 + i
        d.rectangle([xx * S, (y1 - 3.2) * S, (xx + 3) * S, (y1 - 2.4) * S], fill=(214, 204, 160, 190))
    # emblem
    ex, ey, er = BACK_EMBLEM
    draw_emblem(d, ex, ey, er - 3, S, cream)
    im.save(path)


def draw_emblem(d, cx, cy, r, S, col):
    """Original emblem: gear ring around a bridge arch between two pylons under a four-point star."""
    cx, cy, r = cx * S, cy * S, r * S
    # gear ring: 12 teeth
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=col, width=int(1.6 * S))
    rin = r * 0.86
    for i in range(12):
        a = math.radians(i * 30)
        a0, a1 = a - math.radians(7), a + math.radians(7)
        pts = [(cx + rin * math.cos(a0), cy + rin * math.sin(a0)), (cx + (r + 1.6 * S) * math.cos(a0), cy + (r + 1.6 * S) * math.sin(a0)),
               (cx + (r + 1.6 * S) * math.cos(a1), cy + (r + 1.6 * S) * math.sin(a1)), (cx + rin * math.cos(a1), cy + rin * math.sin(a1))]
        d.polygon(pts, fill=col)
    u = r / 10.0
    # two pylons and a deck
    for sx in (-1, 1):
        d.rectangle([cx + sx * 4.6 * u - 0.9 * u, cy - 1.0 * u, cx + sx * 4.6 * u + 0.9 * u, cy + 5.2 * u], fill=col)
    d.rectangle([cx - 6.4 * u, cy + 3.2 * u, cx + 6.4 * u, cy + 4.4 * u], fill=col)
    # arch
    d.arc([cx - 4.6 * u, cy - 3.6 * u, cx + 4.6 * u, cy + 5.4 * u], 180, 360, fill=col, width=int(1.3 * u))
    # four-point star above
    sy = cy - 5.0 * u
    star = []
    for i in range(8):
        a = math.radians(i * 45 - 90)
        rr = 2.6 * u if i % 2 == 0 else 0.8 * u
        star.append((cx + rr * math.cos(a), sy + rr * math.sin(a)))
    d.polygon(star, fill=col)


# ---------------------------------------------------------------- Blender part
def srgb_to_lin(v):
    v = v / 255.0
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def render_all(out, samples):
    import bpy
    import tempfile

    U = 0.01  # metres per pixel (at 1x)
    tmp = tempfile.mkdtemp(prefix="auth_skin_")
    lin = lambda rgb: tuple(srgb_to_lin(v) for v in rgb) + (1.0,)  # noqa: E731

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

    # --- node helpers
    def N(nt, t, **kw):
        n = nt.nodes.new(t)
        for k, v in kw.items():
            setattr(n, k, v)
        return n

    def ramp(nt, src, stops, interp="LINEAR"):
        r = N(nt, "ShaderNodeValToRGB")
        r.color_ramp.interpolation = interp
        el = r.color_ramp.elements
        while len(el) > len(stops):
            el.remove(el[-1])
        while len(el) < len(stops):
            el.new(0.5)
        for e, (p, v) in zip(el, stops):
            e.position = p
            e.color = (v, v, v, 1)
        nt.links.new(src, r.inputs["Fac"])
        return r.outputs["Color"]

    def noise(nt, scale, detail=4.0, stretch=None):
        tc = N(nt, "ShaderNodeTexCoord")
        n = N(nt, "ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        if stretch:
            mp = N(nt, "ShaderNodeMapping")
            mp.inputs["Scale"].default_value = stretch
            nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
            nt.links.new(mp.outputs["Vector"], n.inputs["Vector"])
        else:
            nt.links.new(tc.outputs["Object"], n.inputs["Vector"])
        return n.outputs["Fac"]

    def mth(nt, op, a, b=None, clamp=False):
        m = N(nt, "ShaderNodeMath", operation=op)
        m.use_clamp = clamp
        for i, v in enumerate((a, b)):
            if v is None:
                continue
            if isinstance(v, (int, float)):
                m.inputs[i].default_value = v
            else:
                nt.links.new(v, m.inputs[i])
        return m.outputs["Value"]

    def mixf(nt, fac, a, b):
        m = N(nt, "ShaderNodeMix", data_type="FLOAT")
        for i, v in ((0, fac), (2, a), (3, b)):
            if isinstance(v, (int, float)):
                m.inputs[i].default_value = v
            else:
                nt.links.new(v, m.inputs[i])
        return m.outputs[0]

    def mixc(nt, fac, a, b, blend="MIX"):
        m = N(nt, "ShaderNodeMix", data_type="RGBA", blend_type=blend)
        if isinstance(fac, (int, float)):
            m.inputs[0].default_value = fac
        else:
            nt.links.new(fac, m.inputs[0])
        for i, v in ((6, a), (7, b)):
            if isinstance(v, tuple):
                m.inputs[i].default_value = v
            else:
                nt.links.new(v, m.inputs[i])
        return m.outputs[2]

    def paint(name, rgb, bare=(168, 166, 156), edge=(0.02, 0.045), chip=0.78, grime=1.0, metal=0.1, rough=0.62,
              bump_noise=0.12, weld=False, wear=1.0):
        """Worn paint over bare steel: wear on bevelled edges and as random chips, grime in recesses (AO) plus a dirt noise."""
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out_n = N(nt, "ShaderNodeOutputMaterial")
        bsdf = N(nt, "ShaderNodeBsdfPrincipled")
        nt.links.new(bsdf.outputs["BSDF"], out_n.inputs["Surface"])
        geo = N(nt, "ShaderNodeNewGeometry")
        dot = N(nt, "ShaderNodeVectorMath", operation="DOT_PRODUCT")
        dot.inputs[1].default_value = (0, 0, 1)
        nt.links.new(geo.outputs["Normal"], dot.inputs[0])
        tilt = mth(nt, "SUBTRACT", 1.0, dot.outputs["Value"])
        edge_m = ramp(nt, tilt, [(edge[0], 0.0), (edge[1], 1.0)])
        patchy = ramp(nt, noise(nt, 38.0, 5.0), [(0.30, 0.15), (0.55, 1.0)])
        edge_w = mth(nt, "MULTIPLY", edge_m, patchy)
        flat_w = ramp(nt, noise(nt, 20.0, 4.0), [(chip, 0.0), (chip + 0.035, 1.0)])
        wear_m = mth(nt, "MULTIPLY", mth(nt, "MAXIMUM", edge_w, flat_w), wear, clamp=True)
        tone = ramp(nt, noise(nt, 5.0, 2.0), [(0.3, 0.86), (0.7, 1.08)])
        col = mixc(nt, 1.0, lin(rgb), tone_to_col(nt, tone), "MULTIPLY")
        # grime: occlusion in recesses plus a faint dirt noise
        ao = N(nt, "ShaderNodeAmbientOcclusion")
        ao.inputs["Distance"].default_value = 0.016
        occ = mth(nt, "SUBTRACT", 1.0, ao.outputs["AO"])
        dirt = ramp(nt, noise(nt, 70.0, 6.0), [(0.45, 0.0), (0.8, 0.28)])
        gf = mth(nt, "ADD", mth(nt, "MULTIPLY", occ, 1.15 * grime), mth(nt, "MULTIPLY", dirt, grime), clamp=True)
        col = mixc(nt, gf, col, lin((26, 21, 13)))
        col = mixc(nt, wear_m, col, lin(bare))
        nt.links.new(col, bsdf.inputs["Base Color"])
        nt.links.new(mixf(nt, wear_m, metal, 0.92), bsdf.inputs["Metallic"])
        nt.links.new(mixf(nt, wear_m, rough, 0.36), bsdf.inputs["Roughness"])
        # bump: fine orange peel, or weld ripples
        if weld:
            v = N(nt, "ShaderNodeTexVoronoi")
            v.inputs["Scale"].default_value = 170.0
            v.inputs["Randomness"].default_value = 0.35
            tc = N(nt, "ShaderNodeTexCoord")
            nt.links.new(tc.outputs["Object"], v.inputs["Vector"])
            height = v.outputs["Distance"]
            strength = 0.9
        else:
            height = noise(nt, 150.0, 3.0)
            strength = bump_noise
        bp = N(nt, "ShaderNodeBump")
        bp.inputs["Strength"].default_value = strength
        bp.inputs["Distance"].default_value = 0.0015
        nt.links.new(height, bp.inputs["Height"])
        nt.links.new(bp.outputs["Normal"], bsdf.inputs["Normal"])
        return m

    def tone_to_col(nt, tone):
        c = N(nt, "ShaderNodeCombineColor")
        for k in ("Red", "Green", "Blue"):
            nt.links.new(tone, c.inputs[k])
        return c.outputs["Color"]

    # --- geometry helpers
    def box(name, cx, cy, z0, sx, sy, sz, mat, bevel=0.0):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(cx, cy, z0 + sz / 2))
        o = bpy.context.active_object
        o.name = name
        o.scale = (sx, sy, sz)
        bpy.ops.object.transform_apply(scale=True)
        o.data.materials.append(mat)
        if bevel > 0:
            b = o.modifiers.new("bev", "BEVEL")
            b.width = min(bevel, 0.45 * min(sx, sy, sz * 2))
            b.segments = 3
            b.limit_method = "ANGLE"
        return o

    def ring(name, x0, y0, x1, y1, t, z0, hgt, mat, bevel=0.0):
        """Rectangular ring: outer rect (x0,y0)-(x1,y1), wall thickness t."""
        w, h = x1 - x0, y1 - y0
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        box(name + "T", cx, y1 - t / 2, z0, w, t, hgt, mat, bevel)
        box(name + "B", cx, y0 + t / 2, z0, w, t, hgt, mat, bevel)
        box(name + "L", x0 + t / 2, cy, z0, t, h - 2 * t + 0.002, hgt, mat, bevel)
        box(name + "R", x1 - t / 2, cy, z0, t, h - 2 * t + 0.002, hgt, mat, bevel)

    def rivet(x, y, z, r, mat, wash):
        bpy.ops.mesh.primitive_cylinder_add(radius=r * 1.5, depth=0.7 * U, location=(x, y, z + 0.35 * U), vertices=20)
        o = bpy.context.active_object
        o.data.materials.append(wash)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=(x, y, z + 0.6 * U), segments=20, ring_count=10)
        o = bpy.context.active_object
        o.scale = (1, 1, 0.7)
        o.data.materials.append(mat)
        bpy.ops.object.shade_smooth()

    def mats(kind):
        base = COLORS[kind]
        if kind == "disabled":
            pt = paint("olive", base, wear=0.35, grime=0.5, metal=0.05, rough=0.8)
        elif kind == "pressed":
            pt = paint("olive", base, wear=0.7, grime=1.5)
        else:
            pt = paint("olive", base)
        gm = paint("gun", GUNMETAL, bare=(150, 154, 158), edge=(0.02, 0.05), chip=0.84, grime=0.8, metal=0.55, rough=0.5)
        wd = paint("weld", tuple(int(v * 0.92) for v in base), weld=True, wear=0.4, grime=1.2)
        rv = paint("rivet", tuple(min(255, int(v * 1.05)) for v in base), edge=(0.12, 0.45), chip=0.9, grime=0.7, metal=0.3, rough=0.5)
        wsh = paint("washer", GUNMETAL, bare=(150, 154, 158), edge=(0.12, 0.4), chip=0.9, metal=0.6, rough=0.45)
        acc = paint("accent", (200, 168, 56), chip=0.7, grime=0.6, rough=0.5)
        dk = paint("dark", (36, 40, 42), bare=(120, 124, 126), chip=0.82, grime=0.6, metal=0.5, rough=0.5)
        return pt, gm, wd, rv, wsh, acc, dk

    def build(w, h, kind, sl):
        W, H = w * U, h * U
        pt, gm, wd, rv, wsh, acc, dk = mats(kind)
        if kind == "back":
            return build_back(W, H, pt, gm, wd, rv, wsh, dk)
        if kind == "frame":
            s = sl[0] * U
            f = 3 * U
            hx, hy = W / 2 - s, H / 2 - s
            # olive bars between outer and inner gunmetal rings
            box("barL", -W / 2 + s / 2, 0, 0, s, H, 2.5 * U, pt, 1.2 * U)
            box("barR", W / 2 - s / 2, 0, 0, s, H, 2.5 * U, pt, 1.2 * U)
            box("barT", 0, H / 2 - s / 2, 0, W - 2 * s + 0.004, s, 2.5 * U, pt, 1.2 * U)
            box("barB", 0, -H / 2 + s / 2, 0, W - 2 * s + 0.004, s, 2.5 * U, pt, 1.2 * U)
            ring("outer", -W / 2, -H / 2, W / 2, H / 2, f, 0, 4.2 * U, gm, 1.3 * U)
            ring("lip", -hx - f, -hy - f, hx + f, hy + f, f, 0, 5.0 * U, gm, 1.3 * U)
            ring("lipacc", -hx - f + 0.2 * U, -hy - f + 0.2 * U, hx + f - 0.2 * U, hy + f - 0.2 * U, 0.7 * U, 5.0 * U, 0.4 * U, acc, 0.1 * U)
            for sx in (-1, 1):
                for sy in (-1, 1):
                    rivet(sx * (W / 2 - s / 2 - 0.2 * U), sy * (H / 2 - s / 2 - 0.2 * U), 2.5 * U, 2.1 * U, rv, wsh)
            return W, H
        f = (2.0 if kind in ("button", "hover", "pressed", "disabled", "tab", "tab_active") else 3.0) * U
        m = sl[0] * U
        hf = {"panel": 4.2, "card": 4.2, "button": 3.0, "hover": 3.0, "pressed": 4.6, "disabled": 3.0, "tab": 3.4, "tab_active": 3.0}[kind] * U
        ft = {"panel": 2.4, "card": 2.4, "button": 5.4, "hover": 6.2, "pressed": 0.9, "disabled": 3.6, "tab": 3.6, "tab_active": 6.4}[kind] * U
        ring("frame", -W / 2, -H / 2, W / 2, H / 2, f, 0, hf, gm, 1.3 * U)
        box("field", 0, 0, 0, W - 2 * f + 0.004, H - 2 * f + 0.004, ft, pt, 1.2 * U)
        big = kind in ("panel", "card")
        # weld bead ring just inside the corner zone
        bi = f + (m - f) * 0.62 if big else f + 1.0 * U
        bw = (1.5 if big else 0.9) * U
        if big or kind in ("button", "hover", "tab_active"):
            ring("weld", -W / 2 + bi, -H / 2 + bi, W / 2 - bi, H / 2 - bi, bw, ft, 0.9 * U, wd, 0.3 * U)
        if kind == "hover":
            ring("glow", -W / 2 + f + 0.4 * U, -H / 2 + f + 0.4 * U, W / 2 - f - 0.4 * U, H / 2 - f - 0.4 * U, 0.6 * U, ft + 0.2 * U, 0.5 * U, acc, 0.1 * U)
        if kind == "tab_active":
            box("accbar", 0, H / 2 - f - 1.0 * U, ft, W - 2 * f - 2 * U, 1.0 * U, 0.8 * U, acc, 0.2 * U)
        rr = (2.4 if big else 1.7) * U
        off = (m * 0.5) if big else (f + 2.4 * U)
        zr = ft + (0.9 * U if big else 0.0)
        if kind != "pressed":
            for sx in (-1, 1):
                for sy in (-1, 1):
                    rivet(sx * (W / 2 - off), sy * (H / 2 - off), ft, rr, rv, wsh)
        else:
            for sx in (-1, 1):
                for sy in (-1, 1):
                    rivet(sx * (W / 2 - off), sy * (H / 2 - off), ft, 1.5 * U, rv, wsh)
        return W, H

    def build_back(W, H, pt, gm, wd, rv, wsh, dk):
        px = lambda x: x * U - W / 2  # noqa: E731
        py = lambda y: H / 2 - y * U  # noqa: E731
        box("base", 0, 0, 0, W, H, 3 * U, gm, 1.5 * U)
        box("plate", 0, 0, 3 * U, W - 8 * U, H - 8 * U, 1.6 * U, pt, 1.2 * U)
        # horizontal weld seams with rivet rows
        for sy_ in BACK_SEAMS:
            box("seamgroove", 0, py(sy_), 4.6 * U, W - 8 * U, 2.6 * U, 0.3 * U, gm, 0.2 * U)
            box("seambead", 0, py(sy_ + 3), 4.6 * U, W - 12 * U, 1.8 * U, 0.9 * U, wd, 0.3 * U)
            for xx in range(14, 256, 24):
                rivet(px(xx), py(sy_ - 4), 4.6 * U, 1.6 * U, rv, wsh)
        # vertical rail seam near the left edge
        box("railgroove", px(11), 0, 4.6 * U, 2.0 * U, H - 8 * U, 0.4 * U, gm, 0.2 * U)
        # credits stencil plate
        x0, y0, x1, y1 = BACK_CREDITS
        box("credplate", px((x0 + x1) / 2), py((y0 + y1) / 2), 4.6 * U, (x1 - x0) * U, (y1 - y0) * U, 1.4 * U, dk, 1.0 * U)
        for xx in (x0 + 3, x1 - 3):
            rivet(px(xx), py((y0 + y1) / 2), 6.0 * U, 1.3 * U, rv, wsh)
        # emblem plate (octagon)
        ex, ey, er = BACK_EMBLEM
        bpy.ops.mesh.primitive_cylinder_add(radius=er * U, depth=1.6 * U, location=(px(ex), py(ey), 4.6 * U + 0.8 * U), vertices=8, rotation=(0, 0, math.radians(22.5)))
        o = bpy.context.active_object
        o.data.materials.append(dk)
        b = o.modifiers.new("bev", "BEVEL")
        b.width = 1.0 * U
        b.segments = 3
        for k in range(8):
            a = math.radians(k * 45)
            rivet(px(ex) + (er - 4) * U * math.cos(a), py(ey) + (er - 4) * U * math.sin(a), 6.2 * U, 1.1 * U, rv, wsh)
        return W, H

    def add_decal(sc, w, h):
        dp = os.path.join(tmp, "back_decal.png")
        make_back_decal(dp, w, h)
        bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 9 * U))
        o = bpy.context.active_object
        o.scale = (w * U, h * U, 1)
        o.visible_shadow = False
        img = bpy.data.images.load(dp)
        img.alpha_mode = "STRAIGHT"
        m = bpy.data.materials.new("decal")
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out_n = N(nt, "ShaderNodeOutputMaterial")
        tex = N(nt, "ShaderNodeTexImage")
        tex.image = img
        tex.interpolation = "Linear"
        bs = N(nt, "ShaderNodeBsdfPrincipled")
        bs.inputs["Roughness"].default_value = 0.55
        nt.links.new(tex.outputs["Color"], bs.inputs["Base Color"])
        tr = N(nt, "ShaderNodeBsdfTransparent")
        chip = ramp(nt, noise(nt, 55.0, 5.0), [(0.22, 0.0), (0.34, 1.0)])
        alpha = mth(nt, "MULTIPLY", tex.outputs["Alpha"], chip)
        mx = N(nt, "ShaderNodeMixShader")
        nt.links.new(alpha, mx.inputs[0])
        nt.links.new(tr.outputs["BSDF"], mx.inputs[1])
        nt.links.new(bs.outputs["BSDF"], mx.inputs[2])
        nt.links.new(mx.outputs["Shader"], out_n.inputs["Surface"])
        o.data.materials.append(m)

    for name, (w, h, sl, kind) in PIECES.items():
        sc = reset()
        rw, rh = w * 2, h * 2
        sc.render.resolution_x, sc.render.resolution_y = rw, rh
        sc.render.resolution_percentage = 100
        build(w, h, kind, sl)
        if kind == "back":
            add_decal(sc, w, h)
        cam_d = bpy.data.cameras.new("cam")
        cam_d.type = "ORTHO"
        cam_d.ortho_scale = max(w, h) * U
        cam = bpy.data.objects.new("cam", cam_d)
        sc.collection.objects.link(cam)
        cam.location = (0, 0, 2.0)
        sc.camera = cam
        sun_d = bpy.data.lights.new("sun", "SUN")
        sun_d.energy = 4.6
        sun_d.angle = 0.1
        sun = bpy.data.objects.new("sun", sun_d)
        sc.collection.objects.link(sun)
        sun.rotation_euler = (math.radians(38), math.radians(-30), math.radians(-35))
        w_ = bpy.data.worlds.new("w")
        w_.use_nodes = True
        w_.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.57, 0.5, 1)
        w_.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55
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
