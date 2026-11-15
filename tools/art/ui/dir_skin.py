# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Directorate UI skin art (M10-15 pilot): poured board-form concrete on blast-door steel, oxblood enamel buttons, bolt heads,
red lamp glow strips, and an original loudspeaker-in-vault-ring emblem with a red-and-bone warning band.

Usage:
  placeholder (no Blender):  python tools/art/ui/dir_skin.py --placeholder [--out DIR]
  real render (bpy):         python tools/art/ui/dir_skin.py [--samples 128] [--out DIR]

Each piece is modelled in Blender (straight-on orthographic camera), rendered at 2x and downscaled with Pillow to the size the
9-slice needs. Writes the PNGs (RGBA, hard alpha) and skin.toml into --out (default data/ui/skins/directorate).
"""
import argparse
import math
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
DEFAULT_OUT = os.path.join(ROOT, "data", "ui", "skins", "directorate")

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

TEXT_COLOR, TEXT_DIM, ACCENT = "#EFE7D0", "#B9AF96", "#E2503F"

CONCRETE = (66, 63, 58)
STEEL = (44, 46, 50)
OXBLOOD = (98, 18, 22)
SIGNAL = (176, 30, 28)
BONE = (226, 218, 196)
COLORS = {  # placeholder colours / material colours per kind (sRGB)
    "panel": CONCRETE, "button": OXBLOOD, "hover": (140, 26, 28), "pressed": (64, 12, 16),
    "disabled": (84, 62, 62), "tab": (54, 51, 47), "tab_active": SIGNAL, "back": (62, 59, 54),
    "frame": (88, 84, 77), "card": (72, 69, 63),
}

# sidebar_back layout in 1x pixels from the top-left (SidebarLayout: pad 4, credits row 24 high)
BACK_CREDITS = (6, 5, 250, 27)       # stencil plate under the credits text
BACK_EMBLEM = (128, 432, 27)         # emblem plate centre x, y and radius
BACK_SEAMS = (118, 226, 340, 580)    # horizontal weld seams (y)


def write_toml(out):
    lines = ['name = "Directorate"', f'text_color = "{TEXT_COLOR}"', f'text_dim = "{TEXT_DIM}"', f'accent = "{ACCENT}"', ""]
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
RED = (176, 28, 30, 255)
BONE_A = (226, 218, 196, 255)
INK = (24, 20, 20, 255)


def warning_band(im, rect, S, period=14, flip=False):
    """Diagonal red-and-bone band clipped to rect (1x px), with dark edge rules."""
    x0, y0, x1, y1 = rect
    w, h = im.size
    layer = Image.new("RGBA", im.size, BONE_A)
    ld = ImageDraw.Draw(layer)
    hh = y1 - y0
    sgn = -1 if flip else 1
    for k in range(-hh // period - 2, (x1 - x0 + hh) // period + 3):
        xs = x0 + k * period
        pts = [(xs, y0), (xs + period / 2, y0), (xs + period / 2 + sgn * hh, y1), (xs + sgn * hh, y1)]
        ld.polygon([(x * S, y * S) for x, y in pts], fill=RED)
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rectangle([x0 * S, y0 * S, x1 * S - 1, y1 * S - 1], fill=255)
    im.paste(layer, (0, 0), mask)
    d = ImageDraw.Draw(im)
    d.rectangle([x0 * S, y0 * S, x1 * S - 1, (y0 + 1) * S - 1], fill=INK)
    d.rectangle([x0 * S, (y1 - 1) * S, x1 * S - 1, y1 * S - 1], fill=INK)


def make_back_decal(path, w, h, S=4):
    """RGBA decal for sidebar_back: warning band (bottom) and edge band (left), programme serial plate marks, emblem."""
    im = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    warning_band(im, (0, h - 24, w, h - 4), S, period=14)
    warning_band(im, (0, 0, 4, h - 24), S, period=8, flip=True)
    d = ImageDraw.Draw(im)
    bone = BONE_A
    # programme serial plate: corner brackets, ruled dashes along the bottom, stamped serial ticks at the right end
    x0, y0, x1, y1 = BACK_CREDITS
    for (cx, cy, sx, sy) in ((x0 + 2, y0 + 2, 1, 1), (x1 - 2, y0 + 2, -1, 1), (x0 + 2, y1 - 2, 1, -1), (x1 - 2, y1 - 2, -1, -1)):
        d.line([(cx * S, cy * S), ((cx + 5 * sx) * S, cy * S)], fill=bone, width=S)
        d.line([(cx * S, cy * S), (cx * S, (cy + 5 * sy) * S)], fill=bone, width=S)
    for i in range(0, int(x1 - x0 - 50), 6):
        xx = x0 + 12 + i
        d.rectangle([xx * S, (y1 - 3.2) * S, (xx + 3) * S, (y1 - 2.4) * S], fill=(226, 218, 196, 190))
    sx0 = x1 - 38
    for i, wd in enumerate((1, 2, 1, 1, 2, 1, 3, 1, 2, 1)):
        xx = sx0 + i * 2.6
        d.rectangle([xx * S, (y0 + 7) * S, (xx + wd * 0.8) * S, (y0 + 13) * S], fill=(226, 218, 196, 200))
    d.rectangle([(x1 - 7.5) * S, (y0 + 7) * S, (x1 - 5) * S, (y0 + 13) * S], fill=RED)
    ex, ey, er = BACK_EMBLEM
    draw_emblem(d, ex, ey, er - 3, S, bone)
    im.save(path)


def draw_emblem(d, cx, cy, r, S, col):
    """Original emblem: loudspeaker horn radiating three red broadcast arcs, inside a vault-door ring with eight locking lugs."""
    cx, cy, r = cx * S, cy * S, r * S
    u = S
    rr = 19 * u * (r / (24 * S))
    d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=col, width=int(1.5 * u))
    for i in range(8):  # locking lugs, pointing inward
        a = math.radians(22.5 + i * 45)
        c, s = math.cos(a), math.sin(a)
        pa, pb = rr - 0.6 * u, rr - 3.6 * u
        px_, py_ = -s, c
        wlug = 1.2 * u
        pts = [(cx + pa * c + px_ * wlug, cy + pa * s + py_ * wlug), (cx + pa * c - px_ * wlug, cy + pa * s - py_ * wlug),
               (cx + pb * c - px_ * wlug, cy + pb * s - py_ * wlug), (cx + pb * c + px_ * wlug, cy + pb * s + py_ * wlug)]
        d.polygon(pts, fill=col)
    # horn: throat block, flared cone, rim, mounting bracket
    d.rectangle([cx - 12 * u, cy - 2.2 * u, cx - 8.5 * u, cy + 2.2 * u], fill=col)
    d.polygon([(cx - 8.5 * u, cy - 2.2 * u), (cx - 0.6 * u, cy - 8.2 * u), (cx - 0.6 * u, cy + 8.2 * u), (cx - 8.5 * u, cy + 2.2 * u)], fill=col)
    d.rectangle([cx - 0.6 * u, cy - 9.2 * u, cx + 1.2 * u, cy + 9.2 * u], fill=col)
    d.rectangle([cx - 5.8 * u, cy + 4.6 * u, cx - 3.6 * u, cy + 9.4 * u], fill=col)
    d.rectangle([cx - 8.0 * u, cy + 8.6 * u, cx - 1.4 * u, cy + 10.2 * u], fill=col)
    # three broadcast arcs
    red = (214, 44, 38, 255)
    for k, ra in enumerate((5.5, 9.5, 13.5)):
        ax = cx + 2.4 * u
        d.arc([ax - ra * u, cy - ra * u, ax + ra * u, cy + ra * u], -42, 42, fill=red, width=int(1.5 * u))



# ---------------------------------------------------------------- Blender part
def srgb_to_lin(v):
    v = v / 255.0
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def render_all(out, samples):
    import bpy
    import tempfile

    U = 0.01  # metres per pixel (at 1x)
    tmp = tempfile.mkdtemp(prefix="dir_skin_")
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

    def concrete(name, rgb, board=12.0, pits=False, grime=1.0, streak=1.0, tone_var=0.07):
        """Poured concrete with board-form texture: plank grooves every `board` px (world y), per-board tone, wood-grain
        imprint (noise stretched along x), water streaks (stretched along y), grime in recesses, optional air pits."""
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out_n = N(nt, "ShaderNodeOutputMaterial")
        bsdf = N(nt, "ShaderNodeBsdfPrincipled")
        nt.links.new(bsdf.outputs["BSDF"], out_n.inputs["Surface"])
        geo = N(nt, "ShaderNodeNewGeometry")

        def pnoise(scale, detail=4.0, stretch=None):
            n = N(nt, "ShaderNodeTexNoise")
            n.inputs["Scale"].default_value = scale
            n.inputs["Detail"].default_value = detail
            if stretch:
                mp = N(nt, "ShaderNodeMapping")
                mp.inputs["Scale"].default_value = stretch
                nt.links.new(geo.outputs["Position"], mp.inputs["Vector"])
                nt.links.new(mp.outputs["Vector"], n.inputs["Vector"])
            else:
                nt.links.new(geo.outputs["Position"], n.inputs["Vector"])
            return n.outputs["Fac"]

        sep = N(nt, "ShaderNodeSeparateXYZ")
        nt.links.new(geo.outputs["Position"], sep.inputs["Vector"])
        wob = mth(nt, "MULTIPLY", mth(nt, "SUBTRACT", pnoise(7.0, 3.0), 0.5), 0.22)
        yb = mth(nt, "ADD", mth(nt, "DIVIDE", sep.outputs["Y"], board * U), wob)
        fr = mth(nt, "FRACT", yb)
        dist = mth(nt, "MINIMUM", fr, mth(nt, "SUBTRACT", 1.0, fr))
        groove = ramp(nt, dist, [(0.0, 1.0), (0.05, 0.0)])
        idx = mth(nt, "FLOOR", yb)
        hsh = mth(nt, "FRACT", mth(nt, "MULTIPLY", mth(nt, "SINE", mth(nt, "MULTIPLY", idx, 12.9898)), 43758.5453))
        tvar = mth(nt, "ADD", 1.0, mth(nt, "MULTIPLY", mth(nt, "SUBTRACT", hsh, 0.5), 2.0 * tone_var))
        grain = ramp(nt, pnoise(60.0, 4.0, (0.05, 1.0, 1.0)), [(0.3, 0.90), (0.7, 1.08)])
        blot = ramp(nt, pnoise(4.0, 2.0), [(0.3, 0.95), (0.7, 1.04)])
        tone = mth(nt, "MULTIPLY", mth(nt, "MULTIPLY", tvar, grain), blot)
        col = mixc(nt, 1.0, lin(rgb), tone_to_col(nt, tone), "MULTIPLY")
        st = ramp(nt, pnoise(9.0, 3.0, (1.0, 0.12, 1.0)), [(0.45, 0.0), (0.82, 0.55 * streak)])
        col = mixc(nt, st, col, lin((46, 40, 34)))
        ao = N(nt, "ShaderNodeAmbientOcclusion")
        ao.inputs["Distance"].default_value = 0.016
        occ = mth(nt, "SUBTRACT", 1.0, ao.outputs["AO"])
        gf = mth(nt, "ADD", mth(nt, "MULTIPLY", occ, 1.0 * grime), mth(nt, "MULTIPLY", groove, 0.75), clamp=True)
        col = mixc(nt, gf, col, lin((30, 27, 24)))
        height = mth(nt, "SUBTRACT", mth(nt, "MULTIPLY", grain, 0.3), groove)
        if pits:
            vr = N(nt, "ShaderNodeTexVoronoi")
            vr.inputs["Scale"].default_value = 140.0
            vr.inputs["Randomness"].default_value = 1.0
            nt.links.new(geo.outputs["Position"], vr.inputs["Vector"])
            pit = mth(nt, "MULTIPLY", ramp(nt, vr.outputs["Distance"], [(0.05, 1.0), (0.17, 0.0)]),
                      ramp(nt, pnoise(34.0, 2.0), [(0.56, 0.0), (0.62, 1.0)]))
            col = mixc(nt, mth(nt, "MULTIPLY", pit, 0.8), col, lin((26, 24, 22)))
            height = mth(nt, "SUBTRACT", height, mth(nt, "MULTIPLY", pit, 0.7))
        nt.links.new(col, bsdf.inputs["Base Color"])
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = 0.9
        bp = N(nt, "ShaderNodeBump")
        bp.inputs["Strength"].default_value = 0.8
        bp.inputs["Distance"].default_value = 0.0015
        nt.links.new(height, bp.inputs["Height"])
        nt.links.new(bp.outputs["Normal"], bsdf.inputs["Normal"])
        return m

    def lamp(name, strength):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out_n = N(nt, "ShaderNodeOutputMaterial")
        em = N(nt, "ShaderNodeEmission")
        em.inputs["Color"].default_value = lin((255, 66, 40))
        em.inputs["Strength"].default_value = strength
        nt.links.new(em.outputs["Emission"], out_n.inputs["Surface"])
        return m


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

    def bolt(x, y, z, r, mat, wash):
        """Hex bolt head on a round washer."""
        bpy.ops.mesh.primitive_cylinder_add(radius=r * 1.55, depth=0.6 * U, location=(x, y, z + 0.3 * U), vertices=24)
        o = bpy.context.active_object
        o.data.materials.append(wash)
        bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=1.1 * U, location=(x, y, z + 1.15 * U), vertices=6)
        o = bpy.context.active_object
        o.data.materials.append(mat)
        b = o.modifiers.new("bev", "BEVEL")
        b.width = 0.28 * U
        b.segments = 2

    def mats(kind):
        base = COLORS[kind]
        if kind == "disabled":
            en = paint("enamel", base, wear=0.3, grime=0.5, metal=0.0, rough=0.75)
        elif kind == "pressed":
            en = paint("enamel", base, wear=0.7, grime=1.3, metal=0.0, rough=0.4)
        elif kind == "tab_active":
            en = paint("enamel", base, grime=0.8, metal=0.0, rough=0.3, bare=(150, 148, 142))
        else:
            en = paint("enamel", base, grime=1.0, metal=0.0, rough=0.3, bare=(150, 148, 142))
        small = kind in ("button", "hover", "pressed", "disabled", "tab", "tab_active")
        cn = concrete("conc", base if kind in ("panel", "card", "back", "frame", "tab") else CONCRETE,
                      board=6.0 if small else (12.0 if kind != "back" else 24.0),
                      pits=kind in ("back", "frame"), grime=1.0 if kind != "tab" else 1.2,
                      streak=0.25 if kind in ("panel", "card") else 1.0)
        gm = paint("steel", STEEL, bare=(150, 154, 158), edge=(0.02, 0.05), chip=0.84, grime=0.8, metal=0.55, rough=0.5)
        bt = paint("bolt", (66, 66, 68), bare=(170, 172, 176), edge=(0.1, 0.4), chip=0.9, grime=0.7, metal=0.6, rough=0.42)
        wsh = paint("washer", STEEL, bare=(150, 154, 158), edge=(0.12, 0.4), chip=0.9, metal=0.6, rough=0.45)
        red = paint("signal", SIGNAL, chip=0.7, grime=0.7, metal=0.0, rough=0.32, bare=(150, 148, 142))
        dk = paint("dark", (34, 36, 40), bare=(120, 124, 126), chip=0.82, grime=0.6, metal=0.5, rough=0.5)
        return en, cn, gm, bt, wsh, red, dk

    def build(w, h, kind, sl):
        W, H = w * U, h * U
        en, cn, gm, bt, wsh, red, dk = mats(kind)
        if kind == "back":
            return build_back(W, H, cn, gm, bt, wsh, dk)
        if kind == "frame":
            s = sl[0] * U
            f = 3 * U
            hx, hy = W / 2 - s, H / 2 - s
            # concrete bars between outer and inner steel rings
            box("barL", -W / 2 + s / 2, 0, 0, s, H, 2.5 * U, cn, 1.2 * U)
            box("barR", W / 2 - s / 2, 0, 0, s, H, 2.5 * U, cn, 1.2 * U)
            box("barT", 0, H / 2 - s / 2, 0, W - 2 * s + 0.004, s, 2.5 * U, cn, 1.2 * U)
            box("barB", 0, -H / 2 + s / 2, 0, W - 2 * s + 0.004, s, 2.5 * U, cn, 1.2 * U)
            ring("outer", -W / 2, -H / 2, W / 2, H / 2, f, 0, 4.2 * U, gm, 1.3 * U)
            ring("lip", -hx - f, -hy - f, hx + f, hy + f, f, 0, 5.0 * U, gm, 1.3 * U)
            ring("lipacc", -hx - f + 0.2 * U, -hy - f + 0.2 * U, hx + f - 0.2 * U, hy + f - 0.2 * U, 0.7 * U, 5.0 * U, 0.4 * U, red, 0.1 * U)
            for sx in (-1, 1):
                for sy in (-1, 1):
                    bolt(sx * (W / 2 - s / 2 - 0.2 * U), sy * (H / 2 - s / 2 - 0.2 * U), 2.5 * U, 2.1 * U, bt, wsh)
            return W, H
        small = kind in ("button", "hover", "pressed", "disabled", "tab", "tab_active")
        f = (2.0 if small else 3.0) * U
        m = sl[0] * U
        hf = {"panel": 4.2, "card": 4.2, "button": 3.0, "hover": 3.0, "pressed": 4.6, "disabled": 3.0, "tab": 3.4, "tab_active": 3.0}[kind] * U
        ft = {"panel": 2.4, "card": 2.4, "button": 5.4, "hover": 6.2, "pressed": 0.9, "disabled": 3.6, "tab": 3.6, "tab_active": 6.4}[kind] * U
        field = cn if kind in ("panel", "card", "tab") else en
        ring("frame", -W / 2, -H / 2, W / 2, H / 2, f, 0, hf, gm, 1.3 * U)
        box("field", 0, 0, 0, W - 2 * f + 0.004, H - 2 * f + 0.004, ft, field, 1.2 * U)
        big = kind in ("panel", "card")
        if big:  # raised cast lip just inside the corner zone
            bi = f + (m - f) * 0.62
            ring("lip", -W / 2 + bi, -H / 2 + bi, W / 2 - bi, H / 2 - bi, 1.5 * U, ft, 0.9 * U, cn, 0.4 * U)
        if kind == "hover":  # red lamp glow strip along the lower edge, with a dim halo bar beneath it
            sw = W - 2 * (m + 0.5 * U)
            box("halo", 0, -H / 2 + f + 3.6 * U, ft, sw + 2 * U, 3.4 * U, 0.2 * U, lamp("halo", 0.9), 0.1 * U)
            box("strip", 0, -H / 2 + f + 3.6 * U, ft + 0.2 * U, sw, 1.5 * U, 0.5 * U, lamp("strip", 6.0), 0.2 * U)
        if kind == "tab_active":
            sw = W - 2 * f - 6 * U
            box("halo", 0, H / 2 - f - 1.6 * U, ft, sw + 2 * U, 2.6 * U, 0.2 * U, lamp("halo", 0.9), 0.1 * U)
            box("strip", 0, H / 2 - f - 1.6 * U, ft + 0.2 * U, sw, 1.2 * U, 0.5 * U, lamp("strip", 6.0), 0.2 * U)
        rr = (2.4 if big else 1.6) * U
        off = (m * 0.5) if big else (f + 2.4 * U)
        if kind == "pressed":
            rr = 1.4 * U
        for sx in (-1, 1):
            for sy in (-1, 1):
                bolt(sx * (W / 2 - off), sy * (H / 2 - off), ft, rr, bt, wsh)
        return W, H

    def build_back(W, H, cn, gm, bt, wsh, dk):
        px = lambda x: x * U - W / 2  # noqa: E731
        py = lambda y: H / 2 - y * U  # noqa: E731
        box("base", 0, 0, 0, W, H, 3 * U, gm, 1.5 * U)
        box("plate", 0, 0, 3 * U, W - 8 * U, H - 8 * U, 1.6 * U, cn, 1.2 * U)
        # steel border bolts
        for yy in range(30, 700, 56):
            for xx in (2.0, 254.0):
                bolt(px(xx), py(yy), 3.0 * U, 0.8 * U, bt, wsh)
        # horizontal construction joints with form-tie holes
        for sy_ in BACK_SEAMS:
            box("seamgroove", 0, py(sy_), 4.6 * U, W - 8 * U, 2.6 * U, 0.3 * U, gm, 0.2 * U)
            box("seamlip", 0, py(sy_ + 3), 4.6 * U, W - 12 * U, 1.2 * U, 0.8 * U, cn, 0.3 * U)
            for xx in range(14, 256, 24):
                bpy.ops.mesh.primitive_cylinder_add(radius=1.5 * U, depth=0.5 * U, location=(px(xx), py(sy_ - 6), 4.85 * U), vertices=20)
                o = bpy.context.active_object
                o.data.materials.append(dk)
        # vertical rail seam near the left edge
        box("railgroove", px(11), 0, 4.6 * U, 2.0 * U, H - 8 * U, 0.4 * U, gm, 0.2 * U)
        # programme serial plate (cast steel)
        x0, y0, x1, y1 = BACK_CREDITS
        box("credplate", px((x0 + x1) / 2), py((y0 + y1) / 2), 4.6 * U, (x1 - x0) * U, (y1 - y0) * U, 1.4 * U, dk, 1.0 * U)
        for xx in (x0 + 3, x1 - 3):
            bolt(px(xx), py((y0 + y1) / 2), 6.0 * U, 1.2 * U, bt, wsh)
        # emblem plate: round vault-door disc with a steel bolt ring
        ex, ey, er = BACK_EMBLEM
        bpy.ops.mesh.primitive_cylinder_add(radius=er * U, depth=1.6 * U, location=(px(ex), py(ey), 4.6 * U + 0.8 * U), vertices=48)
        o = bpy.context.active_object
        o.data.materials.append(dk)
        b = o.modifiers.new("bev", "BEVEL")
        b.width = 1.0 * U
        b.segments = 3
        for k in range(8):
            a = math.radians(k * 45)
            bolt(px(ex) + (er - 2.6) * U * math.cos(a), py(ey) + (er - 2.6) * U * math.sin(a), 6.2 * U, 1.0 * U, bt, wsh)
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
