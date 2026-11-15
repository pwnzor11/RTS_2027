# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Left Behind UI skin art (M10-15), pilot: faded teal paint over rust on patched sheet metal, road-sign button plates in
ochre and bone, rope lashings, canvas flaps, welded patch plates in mismatched tones, hand-painted numerals and a wheel-and-sail
family mark. Dark painted lettering reads on every field.

Usage:
  placeholder (no Blender):  python tools/art/ui/lb_skin.py --placeholder [--out DIR]
  real render (bpy):         python tools/art/ui/lb_skin.py [--samples 128] [--out DIR]

Each piece is modelled in Blender (straight-on orthographic camera), rendered at 2x and downscaled with Pillow to the size the
9-slice needs. Writes the PNGs (RGBA, hard alpha) and skin.toml into --out (default data/ui/skins/left_behind).
"""
import argparse
import math
import os
import random
import sys

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
DEFAULT_OUT = os.path.join(ROOT, "data", "ui", "skins", "left_behind")

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

# dark painted lettering on light-ish faded fields
TEXT_COLOR, TEXT_DIM, ACCENT = "#1F170E", "#4A3E2C", "#8A2E18"

TEAL = (108, 150, 140)
COLORS = {  # placeholder colours / paint colours per kind (sRGB)
    "panel": TEAL, "button": (206, 162, 78), "hover": (232, 184, 76), "pressed": (170, 128, 60),
    "disabled": (170, 162, 142), "tab": (176, 160, 122), "tab_active": (208, 192, 148), "back": (104, 142, 132),
    "frame": TEAL, "card": (118, 154, 136),
}
RUST = (140, 70, 38)
ASH = (112, 106, 98)
BONE = (214, 202, 164)
HAZARD = (222, 178, 32)
ROPE = (176, 150, 104)

# sidebar_back layout in 1x pixels from the top-left (SidebarLayout: pad 4, credits row 24 high)
BACK_CREDITS = (6, 5, 250, 27)       # painted sign plate under the credits text
BACK_EMBLEM = (128, 432, 27)         # emblem plate centre x, y and radius
BACK_SEAMS = (118, 226, 340, 580)    # horizontal lashed seams (y)
BACK_CANVAS = (36, 46)               # canvas strip y range


def write_toml(out):
    lines = ['name = "Left Behind"', f'text_color = "{TEXT_COLOR}"', f'text_dim = "{TEXT_DIM}"', f'accent = "{ACCENT}"', ""]
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
DIGITS = {  # hand-painted numerals as polylines on a 0..1 x 0..2 grid
    "0": [[(0, 0), (1, 0), (1, 2), (0, 2), (0, 0)]], "1": [[(0.2, 0.5), (0.6, 0), (0.6, 2)]],
    "2": [[(0, 0), (1, 0), (1, 1), (0, 1), (0, 2), (1, 2)]], "3": [[(0, 0), (1, 0), (1, 2), (0, 2)], [(0.2, 1), (1, 1)]],
    "4": [[(0, 0), (0, 1), (1, 1)], [(1, 0), (1, 2)]], "7": [[(0, 0), (1, 0), (0.4, 2)]], "9": [[(1, 1), (0, 1), (0, 0), (1, 0), (1, 2)]],
}


def painted_line(d, pts, col, width, S, rnd, wob=0.35):
    """A brush stroke: slightly wobbly polyline with round ends."""
    out = []
    for (x, y) in pts:
        out.append(((x + rnd.uniform(-wob, wob)) * S, (y + rnd.uniform(-wob, wob)) * S))
    d.line(out, fill=col, width=max(1, int(width * S)), joint="curve")
    for (x, y) in out:
        r = width * S / 2
        d.ellipse([x - r, y - r, x + r, y + r], fill=col)


def paint_numerals(d, text, x, y, h, col, S, rnd, width=1.3):
    cw = h * 0.5
    for i, ch in enumerate(text):
        for stroke in DIGITS.get(ch, []):
            painted_line(d, [(x + i * (cw * 1.5) + px * cw, y + py * h / 2) for px, py in stroke], col, width, S, rnd, 0.18)


def make_back_decal(path, w, h, S=4):
    """RGBA decal for sidebar_back: sign-plate lettering marks, sail stripes on the canvas strip, tally and numerals, emblem."""
    rnd = random.Random(27)
    im = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    ink = (52, 36, 24, 255)
    rust = (150, 58, 34, 255)
    ochre = (206, 150, 56, 255)
    # canvas strip: sail stripes (ochre / rust) and a stitched hem
    y0, y1 = BACK_CANVAS
    for i, x in enumerate(range(8, w - 8, 22)):
        col = ochre if i % 2 == 0 else rust
        d.polygon([(x * S, (y0 + 2) * S), ((x + 9) * S, (y0 + 2) * S), ((x + 11) * S, (y1 - 2) * S), ((x + 2) * S, (y1 - 2) * S)], fill=col)
    for x in range(8, w - 8, 5):
        d.line([(x * S, (y0 + 0.9) * S), ((x + 2.2) * S, (y0 + 0.9) * S)], fill=ink, width=S // 2)
        d.line([(x * S, (y1 - 0.9) * S), ((x + 2.2) * S, (y1 - 0.9) * S)], fill=ink, width=S // 2)
    # credits plate: painted border, a double rule and end dots (the plate itself is modelled bone-coloured)
    x0, y0c, x1, y1c = BACK_CREDITS
    painted_line(d, [(x0 + 2.2, y0c + 2.2), (x1 - 2.2, y0c + 2.2), (x1 - 2.2, y1c - 2.2), (x0 + 2.2, y1c - 2.2), (x0 + 2.2, y0c + 2.2)], ink, 0.7, S, rnd, 0.12)
    for xx in (x0 + 6, x1 - 6):
        d.ellipse([(xx - 1.1) * S, ((y0c + y1c) / 2 - 1.1) * S, (xx + 1.1) * S, ((y0c + y1c) / 2 + 1.1) * S], fill=rust)
    # hand-painted rig numerals and tally marks on the lower plates
    paint_numerals(d, "47", 24, 664, 18, ink, S, rnd, 1.6)
    for i in range(5):
        painted_line(d, [(150 + i * 5, 664), (150 + i * 5 + rnd.uniform(-0.6, 0.6), 682)], ink, 1.0, S, rnd, 0.2)
    painted_line(d, [(148, 683), (176, 663)], ink, 1.0, S, rnd, 0.2)
    paint_numerals(d, "19", 196, 668, 12, rust, S, rnd, 1.2)
    paint_numerals(d, "3", 24, 262, 10, rust, S, rnd, 1.1)
    # emblem
    ex, ey, er = BACK_EMBLEM
    draw_emblem(d, ex, ey, er - 3, S, ink, rust, rnd)
    im.save(path)


def draw_emblem(d, cx, cy, r, S, col, acc, rnd):
    """Original family mark, hand painted: a spoked wheel with a patched sail on a mast rising through it, rope pennant."""
    cx, cy, r = cx * S, cy * S, r * S
    wob = lambda v: v + rnd.uniform(-0.25, 0.25) * S  # noqa: E731
    # wheel rim (two painted passes so it looks brushed)
    for k in range(2):
        d.ellipse([wob(cx - r), wob(cy - r), wob(cx + r), wob(cy + r)], outline=col, width=int(1.5 * S))
    rr = r * 0.78
    d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=col, width=int(0.7 * S))
    # eight spokes
    for i in range(8):
        a = math.radians(i * 45 + 8)
        d.line([(cx + 1.6 * S * math.cos(a), cy + 1.6 * S * math.sin(a)), (cx + r * 0.96 * math.cos(a), cy + r * 0.96 * math.sin(a))],
               fill=col, width=int(0.8 * S))
    d.ellipse([cx - 2 * S, cy - 2 * S, cx + 2 * S, cy + 2 * S], fill=col)
    u = r / 10.0
    # mast and a triangular sail with a patch
    d.line([(cx, cy + 1 * u), (cx, cy - 9.2 * u)], fill=col, width=int(1.0 * S))
    d.polygon([(cx + 0.6 * u, cy - 8.4 * u), (cx + 0.6 * u, cy - 1.4 * u), (cx + 6.4 * u, cy - 2.4 * u)], fill=acc)
    d.polygon([(cx - 0.6 * u, cy - 6.4 * u), (cx - 0.6 * u, cy - 1.8 * u), (cx - 4.2 * u, cy - 2.4 * u)], fill=col)
    d.rectangle([cx + 1.8 * u, cy - 4.6 * u, cx + 3.4 * u, cy - 3.4 * u], outline=col, width=max(1, int(0.4 * S)))
    # pennant at the masthead
    d.polygon([(cx, cy - 9.2 * u), (cx + 3.2 * u, cy - 8.4 * u), (cx, cy - 7.6 * u)], fill=col)


# ---------------------------------------------------------------- Blender part
def srgb_to_lin(v):
    v = v / 255.0
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def render_all(out, samples):
    import bpy
    import tempfile

    U = 0.01  # metres per pixel (at 1x)
    tmp = tempfile.mkdtemp(prefix="lb_skin_")
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

    def paint(name, rgb, bare=(168, 166, 156), edge=(0.02, 0.045), chip=0.70, grime=1.5, metal=0.1, rough=0.62,
              bump_noise=0.12, weld=False, wear=1.0, weave=False):
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
        patchy = ramp(nt, noise(nt, 38.0, 5.0), [(0.20, 0.35), (0.45, 1.0)])
        edge_w = mth(nt, "MULTIPLY", edge_m, patchy)
        flat_w = ramp(nt, noise(nt, 20.0, 4.0), [(chip, 0.0), (chip + 0.035, 1.0)])
        wear_m = mth(nt, "MULTIPLY", mth(nt, "MAXIMUM", edge_w, flat_w), wear, clamp=True)
        tone = ramp(nt, noise(nt, 5.0, 2.0), [(0.3, 0.86), (0.7, 1.08)])
        col = mixc(nt, 1.0, lin(rgb), tone_to_col(nt, tone), "MULTIPLY")
        # grime: occlusion in recesses plus a faint dirt noise
        ao = N(nt, "ShaderNodeAmbientOcclusion")
        ao.inputs["Distance"].default_value = 0.016
        occ = mth(nt, "SUBTRACT", 1.0, ao.outputs["AO"])
        dirt = ramp(nt, noise(nt, 70.0, 6.0), [(0.40, 0.0), (0.75, 0.40)])
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
        elif weave:
            tc = N(nt, "ShaderNodeTexCoord")
            ck = N(nt, "ShaderNodeTexChecker")
            ck.inputs["Scale"].default_value = 420.0
            nt.links.new(tc.outputs["Object"], ck.inputs["Vector"])
            height = ck.outputs["Fac"]
            strength = 0.35
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
        M = {}
        if kind == "disabled":
            M["field"] = paint("sign", base, bare=ASH, wear=0.35, grime=0.6, metal=0.05, rough=0.8)
        elif kind == "pressed":
            M["field"] = paint("sign", base, bare=ASH, wear=0.7, grime=1.6)
        elif kind in ("panel", "card", "frame", "back"):
            M["field"] = paint("teal", base, bare=RUST, chip=1.2, grime=1.2)  # no flat chips: the stretched middle would blow them up
        elif kind in ("tab", "tab_active"):
            M["field"] = paint("canvas", base, bare=(150, 136, 104), chip=1.2, grime=1.0, metal=0.0, rough=0.9, weave=True, wear=0.4)
        else:
            M["field"] = paint("sign", base, bare=ASH)
        M["metal"] = paint("metal", (100, 94, 86), bare=(128, 70, 40), edge=(0.02, 0.05), chip=0.80, grime=0.9, metal=0.5, rough=0.55)
        M["rust"] = paint("rustplate", (146, 78, 44), bare=(110, 58, 34), chip=0.85, grime=1.0, metal=0.35, rough=0.7)
        M["ash"] = paint("ashplate", (152, 148, 138), bare=(126, 118, 104), chip=0.8, grime=1.0, metal=0.4, rough=0.6)
        M["bus"] = paint("busplate", (168, 118, 56), bare=(120, 84, 44), chip=0.75, grime=1.0, metal=0.3, rough=0.6)
        M["bone"] = paint("boneplate", BONE, bare=ASH, chip=0.8, grime=1.2, metal=0.1, rough=0.7)
        M["rope"] = paint("rope", ROPE, bare=(140, 116, 80), chip=1.3, grime=1.4, metal=0.0, rough=0.95, weld=True, wear=0.2)
        M["weld"] = paint("weld", (116, 104, 88), weld=True, wear=0.4, grime=1.2, metal=0.4)
        M["rivet"] = paint("rivet", (128, 120, 108), edge=(0.12, 0.45), chip=0.9, grime=0.7, metal=0.5, rough=0.5)
        M["washer"] = paint("washer", (74, 70, 64), bare=(130, 126, 118), edge=(0.12, 0.4), chip=0.9, metal=0.6, rough=0.45)
        M["haz"] = paint("hazard", HAZARD, bare=ASH, chip=0.7, grime=0.8, rough=0.5)
        M["cv"] = paint("sail", (196, 180, 140), bare=(160, 146, 112), chip=1.3, grime=1.1, metal=0.0, rough=0.9, weave=True, wear=0.3)
        M["dark"] = paint("dark", (48, 40, 32), bare=(120, 110, 96), chip=0.82, grime=0.6, metal=0.3, rough=0.6)
        return M

    def rivet(x, y, z, r, mat, wash):
        bpy.ops.mesh.primitive_cylinder_add(radius=r * 1.5, depth=0.7 * U, location=(x, y, z + 0.35 * U), vertices=20)
        o = bpy.context.active_object
        o.data.materials.append(wash)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=(x, y, z + 0.6 * U), segments=20, ring_count=10)
        o = bpy.context.active_object
        o.scale = (1, 1, 0.7)
        o.data.materials.append(mat)
        bpy.ops.object.shade_smooth()

    def patch(M, cx, cy, pw, ph, z, hgt, mat, rivets=2, tilt=0.0):
        """Welded patch plate: plate + weld bead ring + rivets on a diagonal (tilt kept for signature, patches are axis aligned)."""
        o = box("patch", cx, cy, z, pw, ph, hgt, mat, 0.35 * U)
        ring("pw", cx - pw / 2 - 0.3 * U, cy - ph / 2 - 0.3 * U, cx + pw / 2 + 0.3 * U, cy + ph / 2 + 0.3 * U, 0.7 * U, z, hgt + 0.3 * U, M["weld"], 0.2 * U)
        if rivets:
            for sx, sy in ((-1, 1), (1, -1))[:rivets]:
                rivet(cx + sx * (pw / 2 - 1.5 * U), cy + sy * (ph / 2 - 1.5 * U), z + hgt, 1.0 * U, M["rivet"], M["washer"])

    def stitches(M, x0, y0, x1, y1, z, step=3.2 * U):
        t = x0
        while t < x1 - step:
            for yy in (y0, y1):
                box("st", t + 0.8 * U, yy, z, 1.6 * U, 0.5 * U, 0.35 * U, M["dark"])
            t += step
        t = y0 + step
        while t < y1 - step:
            for xx in (x0, x1):
                box("st", xx, t + 0.8 * U, z, 0.5 * U, 1.6 * U, 0.35 * U, M["dark"])
            t += step

    def build(w, h, kind, sl):
        W, H = w * U, h * U
        M = mats(kind)
        if kind == "back":
            return build_back(W, H, M)
        if kind == "frame":
            s = sl[0] * U
            f = 2.6 * U
            hx, hy = W / 2 - s, H / 2 - s
            box("barL", -W / 2 + s / 2, 0, 0, s, H, 2.5 * U, M["field"], 1.0 * U)
            box("barR", W / 2 - s / 2, 0, 0, s, H, 2.5 * U, M["field"], 1.0 * U)
            box("barT", 0, H / 2 - s / 2, 0, W - 2 * s + 0.004, s, 2.5 * U, M["field"], 1.0 * U)
            box("barB", 0, -H / 2 + s / 2, 0, W - 2 * s + 0.004, s, 2.5 * U, M["field"], 1.0 * U)
            ring("outer", -W / 2, -H / 2, W / 2, H / 2, f, 0, 4.2 * U, M["metal"], 1.3 * U)
            ring("lip", -hx - f, -hy - f, hx + f, hy + f, f, 0, 5.0 * U, M["metal"], 1.3 * U)
            ring("lipacc", -hx - f + 0.3 * U, -hy - f + 0.3 * U, hx + f - 0.3 * U, hy + f - 0.3 * U, 0.7 * U, 5.0 * U, 0.4 * U, M["haz"], 0.1 * U)
            # mismatched welded patches on two corners, bolts on the others
            patch(M, -W / 2 + s / 2 + 0.2 * U, H / 2 - s / 2 - 0.2 * U, 8.4 * U, 7.6 * U, 2.5 * U, 0.9 * U, M["rust"], 2, 2.0)
            patch(M, W / 2 - s / 2 - 0.2 * U, -H / 2 + s / 2 + 0.2 * U, 7.4 * U, 8.4 * U, 2.5 * U, 0.9 * U, M["ash"], 2, -1.5)
            rivet(W / 2 - s / 2, H / 2 - s / 2, 2.5 * U, 1.8 * U, M["rivet"], M["washer"])
            rivet(-W / 2 + s / 2, -H / 2 + s / 2, 2.5 * U, 1.8 * U, M["rivet"], M["washer"])
            # rope lashings wrapped across the bars
            for k in (-1, 1):
                for (cx, cy, wx, wy) in ((k * 18 * U, H / 2 - s / 2, 2.4 * U, s - 1.4 * U), (k * 18 * U, -H / 2 + s / 2, 2.4 * U, s - 1.4 * U),
                                         (-W / 2 + s / 2, k * 18 * U, s - 1.4 * U, 2.4 * U), (W / 2 - s / 2, k * 18 * U, s - 1.4 * U, 2.4 * U)):
                    box("lash", cx, cy, 2.5 * U, wx, wy, 1.3 * U, M["rope"], 0.5 * U)
            return W, H
        f = (2.0 if kind in ("button", "hover", "pressed", "disabled") else 1.6 if kind in ("tab", "tab_active") else 3.0) * U
        m = sl[0] * U
        hf = {"panel": 4.2, "card": 4.2, "button": 3.0, "hover": 3.2, "pressed": 4.4, "disabled": 3.0, "tab": 3.0, "tab_active": 3.0}[kind] * U
        ft = {"panel": 2.4, "card": 2.4, "button": 5.0, "hover": 5.8, "pressed": 1.0, "disabled": 3.4, "tab": 3.4, "tab_active": 6.0}[kind] * U
        frame_mat = M["haz"] if kind == "hover" else M["metal"]
        ring("frame", -W / 2, -H / 2, W / 2, H / 2, f, 0, hf, frame_mat, 1.0 * U)
        box("field", 0, 0, 0, W - 2 * f + 0.004, H - 2 * f + 0.004, ft, M["field"], 1.2 * U)
        if kind in ("panel", "card"):
            c = m / 2 + f / 2 - 0.2 * U
            patch(M, -W / 2 + c + 0.5 * U, H / 2 - c - 0.5 * U, 8.0 * U, 7.0 * U, ft, 1.0 * U, M["rust"] if kind == "panel" else M["bus"], 2, 1.5)
            patch(M, W / 2 - c - 0.5 * U, -H / 2 + c + 0.5 * U, 7.0 * U, 8.0 * U, ft, 0.8 * U, M["ash"] if kind == "panel" else M["rust"], 2, -2.0)
            if kind == "panel":
                patch(M, W / 2 - c, H / 2 - c, 5.0 * U, 4.6 * U, ft, 0.7 * U, M["bone"], 1, 4.0)
            rivet(-W / 2 + c, -H / 2 + c, ft, 1.9 * U, M["rivet"], M["washer"])
            if kind == "card":
                rivet(W / 2 - c, H / 2 - c, ft, 1.9 * U, M["rivet"], M["washer"])
        elif kind in ("tab", "tab_active"):
            # canvas flap hanging from a metal rail along the top, stitched hem, grommets low
            box("rail", 0, H / 2 - 2.2 * U, 0, W, 4.4 * U, hf + 1.4 * U, M["metal"], 1.0 * U)
            if kind == "tab_active":
                box("accbar", 0, H / 2 - 5.2 * U, ft, W - 2 * f - 2 * U, 1.1 * U, 0.7 * U, M["haz"], 0.2 * U)
            stitches(M, -W / 2 + f + 2.2 * U, -H / 2 + f + 2.2 * U, W / 2 - f - 2.2 * U, H / 2 - 7.5 * U, ft)
            for sx in (-1, 1):
                rivet(sx * (W / 2 - 4.0 * U), H / 2 - 2.2 * U, hf + 1.4 * U, 1.3 * U, M["rivet"], M["washer"])
                rivet(sx * (W / 2 - 4.5 * U), -H / 2 + 5.0 * U, ft, 1.2 * U, M["dark"], M["washer"])
        else:
            # road-sign plate: rope-lashed frame ring + bolts
            ri = f + 1.0 * U
            ring("rope", -W / 2 + ri, -H / 2 + ri, W / 2 - ri, H / 2 - ri, 1.7 * U, ft, 1.3 * U, M["rope"], 0.5 * U)
            for sx in (-1, 1):
                for sy in (-1, 1):
                    rivet(sx * (W / 2 - f - 4.6 * U), sy * (H / 2 - f - 4.6 * U), ft, 1.4 * U if kind != "pressed" else 1.2 * U, M["rivet"], M["washer"])
            # lashing cross-wraps at both ends
            for sx in (-1, 1):
                box("wrap", sx * (W / 2 - ri - 2.4 * U), 0, ft, 1.6 * U, H - 2 * ri - 2 * U, 1.5 * U, M["rope"], 0.5 * U)
        return W, H

    def build_back(W, H, M):
        px = lambda x: x * U - W / 2  # noqa: E731
        py = lambda y: H / 2 - y * U  # noqa: E731
        box("base", 0, 0, 0, W, H, 3 * U, M["metal"], 1.5 * U)
        ys = [0] + list(BACK_SEAMS) + [720]
        tones = ["field", "field", "field", "field", "field"]
        rnd = random.Random(5)
        for i in range(len(ys) - 1):
            ya, yb = ys[i] + (4 if i == 0 else 1.6), ys[i + 1] - (4 if i == len(ys) - 2 else 1.6)
            inset = 4 + rnd.uniform(0, 1.2)
            hh = rnd.uniform(1.2, 2.2)
            box("plate%d" % i, 0, (py(ya) + py(yb)) / 2, 3 * U, W - 2 * inset * U, (yb - ya) * U, hh * U, M[tones[i]], 1.0 * U)
        # lashed seams: rope along the seam and wraps across it, bolts above
        for sy_ in BACK_SEAMS:
            box("seamrope", 0, py(sy_), 5.0 * U, W - 12 * U, 2.0 * U, 1.3 * U, M["rope"], 0.6 * U)
            for xx in (22, 76, 130, 184, 234):
                box("wrap", px(xx), py(sy_), 5.0 * U, 2.6 * U, 10 * U, 1.6 * U, M["rope"], 0.6 * U)
                rivet(px(xx + 8), py(sy_ - 5.5), 5.0 * U, 1.3 * U, M["rivet"], M["washer"])
        # canvas strip with grommets at the ends
        c0, c1 = BACK_CANVAS
        box("canvas", 0, py((c0 + c1) / 2), 4.4 * U, W - 12 * U, (c1 - c0) * U, 1.5 * U, M["cv"], 0.8 * U)
        for xx in (10, 246):
            rivet(px(xx), py((c0 + c1) / 2), 5.9 * U, 1.4 * U, M["dark"], M["washer"])
        # mismatched patch plates near the edges (kept light so dark lettering stays readable)
        patch(M, px(238), py(150), 18 * U, 24 * U, 4.6 * U, 1.0 * U, M["ash"], 2, 3.0)
        patch(M, px(17), py(280), 16 * U, 22 * U, 4.6 * U, 0.9 * U, M["bus"], 2, -2.5)
        patch(M, px(236), py(510), 20 * U, 16 * U, 4.6 * U, 1.1 * U, M["bone"], 2, 1.5)
        patch(M, px(20), py(620), 18 * U, 20 * U, 4.6 * U, 0.9 * U, M["ash"], 2, -3.0)
        # credits sign plate (bone, bolted)
        x0, y0, x1, y1 = BACK_CREDITS
        box("credplate", px((x0 + x1) / 2), py((y0 + y1) / 2), 4.6 * U, (x1 - x0) * U, (y1 - y0) * U, 1.4 * U, M["bone"], 1.0 * U)
        for xx in (x0 + 3, x1 - 3):
            rivet(px(xx), py((y0 + y1) / 2), 6.0 * U, 1.3 * U, M["rivet"], M["washer"])
        # emblem sign plate (octagon, bone) with bolts
        ex, ey, er = BACK_EMBLEM
        bpy.ops.mesh.primitive_cylinder_add(radius=er * U, depth=1.6 * U, location=(px(ex), py(ey), 4.6 * U + 0.8 * U), vertices=8, rotation=(0, 0, math.radians(22.5)))
        o = bpy.context.active_object
        o.data.materials.append(M["bone"])
        b = o.modifiers.new("bev", "BEVEL")
        b.width = 1.0 * U
        b.segments = 3
        for k in range(8):
            a = math.radians(k * 45)
            rivet(px(ex) + (er - 3) * U * math.cos(a), py(ey) + (er - 3) * U * math.sin(a), 6.2 * U, 1.1 * U, M["rivet"], M["washer"])
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
        sun_d.energy = 3.6
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
