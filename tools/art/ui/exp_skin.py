# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Overseas Expedition UI skin art (M10-15 pilot): glossy navy enamel on rounded brushed-chrome frames with copper trim,
signal-orange landing lights, a navy hull plate with double rules, a hull-number credits plate and an original anchor-and-
compass-rose roundel.

Usage:
  placeholder (no Blender):  python tools/art/ui/exp_skin.py --placeholder [--out DIR]
  real render (bpy):         python tools/art/ui/exp_skin.py [--samples 128] [--out DIR]

Each piece is modelled in Blender (straight-on orthographic camera), rendered at 2x and downscaled with Pillow to the size the
9-slice needs. Writes the PNGs (RGBA, hard alpha) and skin.toml into --out (default data/ui/skins/expedition).
"""
import argparse
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
DEFAULT_OUT = os.path.join(ROOT, "data", "ui", "skins", "expedition")

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

TEXT_COLOR, TEXT_DIM, ACCENT = "#F2F6F4", "#9DB0C8", "#FF7A2B"

COLORS = {  # enamel colours per kind (sRGB); text_color (lagoon white) must read on these
    "panel": (24, 44, 82), "button": (32, 60, 106), "hover": (42, 76, 128), "pressed": (18, 34, 66),
    "disabled": (66, 74, 90), "tab": (20, 38, 72), "tab_active": (40, 70, 118), "back": (20, 36, 70),
    "frame": (24, 44, 82), "card": (28, 50, 92),
}
CHROME = (200, 204, 208)
COPPER = (222, 130, 70)
ORANGE = (255, 122, 43)
ASH = (122, 124, 124)

# sidebar_back layout in 1x pixels from the top-left (SidebarLayout: pad 4, credits row 24 high)
BACK_CREDITS = (6, 5, 250, 27)       # hull-number plate under the credits text
BACK_EMBLEM = (128, 432, 27)         # emblem plate centre x, y and radius
BACK_SEAMS = (118, 226, 340, 580)    # horizontal double rules (y)


def write_toml(out):
    lines = ['name = "Expedition"', f'text_color = "{TEXT_COLOR}"', f'text_dim = "{TEXT_DIM}"', f'accent = "{ACCENT}"', ""]
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
        d.rectangle([0, 0, w - 1, h - 1], outline=CHROME + (255,))
        if kind == "frame":
            s = sl[0]
            d.rectangle([s, s, w - s - 1, h - s - 1], fill=(0, 0, 0, 0))
        im.save(os.path.join(out, name + ".png"))


# ---------------------------------------------------------------- decal textures (Pillow, drawn at 4x of the 1x size)
def draw_emblem(d, cx, cy, r, S):
    """Original emblem: a winged-free anchor crossed over an eight-point compass rose inside a roundel ring."""
    cx, cy, r = cx * S, cy * S, r * S
    white, cop, org = (240, 244, 242, 255), COPPER + (255,), ORANGE + (255,)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=white, width=int(1.2 * S))
    u = r / 10.0
    # eight-point compass rose behind the anchor
    pts = []
    for i in range(16):
        a = math.radians(i * 22.5 - 90)
        rr = 9.0 * u if i % 4 == 0 else (6.4 * u if i % 2 == 0 else 2.4 * u)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    d.polygon(pts, fill=cop)
    # north pointer in signal orange
    d.polygon([(cx, cy - 9.6 * u), (cx - 1.5 * u, cy - 6.0 * u), (cx + 1.5 * u, cy - 6.0 * u)], fill=org)
    # anchor: ring, stock, shank, arms and flukes
    w = max(1, int(1.1 * u))
    d.ellipse([cx - 1.1 * u, cy - 7.6 * u, cx + 1.1 * u, cy - 5.4 * u], outline=white, width=w)
    d.line([(cx - 2.8 * u, cy - 3.6 * u), (cx + 2.8 * u, cy - 3.6 * u)], fill=white, width=w)
    d.line([(cx, cy - 5.4 * u), (cx, cy + 6.4 * u)], fill=white, width=w)
    d.arc([cx - 4.9 * u, cy - 1.6 * u, cx + 4.9 * u, cy + 6.6 * u], 0, 180, fill=white, width=w)
    for sx in (-1, 1):
        d.polygon([(cx + sx * 4.9 * u, cy + 2.6 * u), (cx + sx * 7.0 * u, cy + 0.6 * u), (cx + sx * 3.4 * u, cy + 0.6 * u)], fill=white)


def make_back_decal(path, w, h, S=4):
    """RGBA decal for sidebar_back: hull-number plate marks, boot-top stripe, hull number and the emblem."""
    im = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    white = (240, 244, 242, 255)
    x0, y0, x1, y1 = BACK_CREDITS
    # hull-number plate: white double outline and tick marks like a ship's draught marks
    d.rounded_rectangle([(x0 + 2.2) * S, (y0 + 2.2) * S, (x1 - 2.2) * S, (y1 - 2.2) * S], radius=2.5 * S, outline=white, width=int(0.7 * S))
    for i in range(0, 14):
        xx = x0 + 6 + i * 3.0
        d.line([(xx * S, (y1 - 5.2) * S), (xx * S, (y1 - (3.9 if i % 2 else 4.6)) * S)], fill=white, width=int(0.5 * S) or 1)
    # boot-top stripe in signal orange with a chrome-white keyline; the ash tidemark itself is in the plate material
    yb = 640
    d.rectangle([10 * S, yb * S, (w - 10) * S, (yb + 3) * S], fill=ORANGE + (255,))
    d.rectangle([10 * S, (yb + 4.2) * S, (w - 10) * S, (yb + 4.8) * S], fill=white)
    # white hull number low on the plate, set in a clean grotesque
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 15 * S)
        d.text((w / 2 * S, 689 * S), "EX 62", font=font, fill=white, anchor="mm")
    except OSError:
        pass
    ex, ey, er = BACK_EMBLEM
    draw_emblem(d, ex, ey, er - 4, S)
    im.save(path)


# ---------------------------------------------------------------- Blender part
def srgb_to_lin(v):
    v = v / 255.0
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def render_all(out, samples):
    import bpy
    import bmesh
    import tempfile

    U = 0.01  # metres per pixel (at 1x)
    tmp = tempfile.mkdtemp(prefix="exp_skin_")
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

    def ramp(nt, src, stops):
        r = N(nt, "ShaderNodeValToRGB")
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

    def noise(nt, scale, detail=4.0, stretch=None, coord="Object"):
        tc = N(nt, "ShaderNodeTexCoord")
        n = N(nt, "ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        if stretch:
            mp = N(nt, "ShaderNodeMapping")
            mp.inputs["Scale"].default_value = stretch
            nt.links.new(tc.outputs[coord], mp.inputs["Vector"])
            nt.links.new(mp.outputs["Vector"], n.inputs["Vector"])
        else:
            nt.links.new(tc.outputs[coord], n.inputs["Vector"])
        return n.outputs["Fac"]

    def gen_y(nt):
        tc = N(nt, "ShaderNodeTexCoord")
        sp = N(nt, "ShaderNodeSeparateXYZ")
        nt.links.new(tc.outputs["Generated"], sp.inputs["Vector"])
        return sp.outputs["Y"]

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

    def grey_col(nt, f):
        c = N(nt, "ShaderNodeCombineColor")
        for k in ("Red", "Green", "Blue"):
            nt.links.new(f, c.inputs[k])
        return c.outputs["Color"]

    def new_mat(name):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out_n = N(nt, "ShaderNodeOutputMaterial")
        bsdf = N(nt, "ShaderNodeBsdfPrincipled")
        nt.links.new(bsdf.outputs["BSDF"], out_n.inputs["Surface"])
        return m, nt, bsdf

    def enamel(name, rgb, rough=0.2, tide=None, tide_amt=0.0, wear=1.0, edge=(0.03, 0.09), grime=0.5, sheen=(0.9, 1.12)):
        """Glossy navy enamel: clear-coat gloss, soft top-to-bottom sheen, light edge wear down to chrome and a grey ash tidemark."""
        m, nt, bsdf = new_mat(name)
        geo = N(nt, "ShaderNodeNewGeometry")
        dot = N(nt, "ShaderNodeVectorMath", operation="DOT_PRODUCT")
        dot.inputs[1].default_value = (0, 0, 1)
        nt.links.new(geo.outputs["Normal"], dot.inputs[0])
        tilt = mth(nt, "SUBTRACT", 1.0, dot.outputs["Value"])
        edge_m = ramp(nt, tilt, [(edge[0], 0.0), (edge[1], 1.0)])
        patchy = ramp(nt, noise(nt, 40.0, 5.0), [(0.30, 0.0), (0.55, 1.0)])
        wear_m = mth(nt, "MULTIPLY", mth(nt, "MULTIPLY", edge_m, patchy), wear, clamp=True)
        gy = gen_y(nt)
        sh = ramp(nt, gy, [(0.0, sheen[0] / 1.2), (1.0, sheen[1] / 1.2)])
        tone = ramp(nt, noise(nt, 4.0, 2.0), [(0.3, 0.9), (0.7, 1.05)])
        col = mixc(nt, 1.0, lin(rgb), grey_col(nt, mth(nt, "MULTIPLY", sh, mth(nt, "MULTIPLY", tone, 1.2))), "MULTIPLY")
        # soft grime in recesses
        ao = N(nt, "ShaderNodeAmbientOcclusion")
        ao.inputs["Distance"].default_value = 0.012
        occ = mth(nt, "SUBTRACT", 1.0, ao.outputs["AO"])
        dirt = ramp(nt, noise(nt, 60.0, 6.0), [(0.45, 0.0), (0.8, 0.3)])
        gf = mth(nt, "ADD", mth(nt, "MULTIPLY", occ, grime), mth(nt, "MULTIPLY", dirt, grime * 0.6), clamp=True)
        col = mixc(nt, gf, col, lin((14, 18, 26)))
        if tide is not None:
            lvl = mth(nt, "ADD", tide, mth(nt, "MULTIPLY", mth(nt, "SUBTRACT", noise(nt, 14.0, 4.0, (1.0, 4.0, 1.0)), 0.5), 0.05))
            diff = mth(nt, "SUBTRACT", lvl, gy)
            mask = ramp(nt, diff, [(0.0, 0.0), (0.008, 1.0)])
            fade = ramp(nt, diff, [(0.0, 0.0), (0.3, 1.0)])
            ash_f = mth(nt, "ADD", mth(nt, "MULTIPLY", mask, tide_amt), mth(nt, "MULTIPLY", fade, tide_amt * 0.35), clamp=True)
            col = mixc(nt, ash_f, col, lin(ASH))
        col = mixc(nt, wear_m, col, lin(CHROME))
        nt.links.new(col, bsdf.inputs["Base Color"])
        nt.links.new(mixf(nt, wear_m, 0.0, 0.95), bsdf.inputs["Metallic"])
        nt.links.new(mixf(nt, wear_m, rough, 0.3), bsdf.inputs["Roughness"])
        bsdf.inputs["Coat Weight"].default_value = 0.55
        bsdf.inputs["Coat Roughness"].default_value = 0.04
        bp = N(nt, "ShaderNodeBump")
        bp.inputs["Strength"].default_value = 0.05
        bp.inputs["Distance"].default_value = 0.001
        nt.links.new(noise(nt, 160.0, 3.0), bp.inputs["Height"])
        nt.links.new(bp.outputs["Normal"], bsdf.inputs["Normal"])
        return m

    def metal(name, rgb, rough=0.3, brushed=True, grad=(0.75, 1.1)):
        """Brushed chrome / copper: horizontal streak bump, sheen gradient, faint ash dusting."""
        m, nt, bsdf = new_mat(name)
        gy = gen_y(nt)
        g = ramp(nt, gy, [(0.0, grad[0] / 1.2), (1.0, grad[1] / 1.2)])
        col = mixc(nt, 1.0, lin(rgb), grey_col(nt, mth(nt, "MULTIPLY", g, 1.2)), "MULTIPLY")
        dust = ramp(nt, noise(nt, 30.0, 5.0), [(0.5, 0.0), (0.8, 0.22)])
        col = mixc(nt, dust, col, lin(ASH))
        nt.links.new(col, bsdf.inputs["Base Color"])
        bsdf.inputs["Metallic"].default_value = 1.0
        bsdf.inputs["Roughness"].default_value = rough
        if brushed:
            bp = N(nt, "ShaderNodeBump")
            bp.inputs["Strength"].default_value = 0.5
            bp.inputs["Distance"].default_value = 0.0008
            nt.links.new(noise(nt, 6.0, 2.0, (2.0, 260.0, 1.0)), bp.inputs["Height"])
            nt.links.new(bp.outputs["Normal"], bsdf.inputs["Normal"])
        return m

    def lamp(name, rgb, strength=3.0):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out_n = N(nt, "ShaderNodeOutputMaterial")
        em = N(nt, "ShaderNodeEmission")
        em.inputs["Color"].default_value = lin(rgb)
        em.inputs["Strength"].default_value = strength
        nt.links.new(em.outputs["Emission"], out_n.inputs["Surface"])
        return m

    # --- geometry helpers
    def rr_pts(w, h, radii, n=8):
        """Rounded-rect outline (CCW), corners tl,tr,br,bl radii; always 4*(n+1) points so insets pair up."""
        tl, tr, br, bl = [max(r, 0.04 * U) for r in radii]
        pts = []
        for (cx, cy, r, a0) in ((w / 2 - br, -h / 2 + br, br, -90), (w / 2 - tr, h / 2 - tr, tr, 0),
                                (-w / 2 + tl, h / 2 - tl, tl, 90), (-w / 2 + bl, -h / 2 + bl, bl, 180)):
            for i in range(n + 1):
                a = math.radians(a0 + 90.0 * i / n)
                pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        return pts

    def inset(w, h, radii, t):
        return rr_pts(w - 2 * t, h - 2 * t, [r - t for r in radii])

    def finish(o, mat, bev, bw):
        o.data.materials.append(mat)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(o.data)
        bm.free()
        if bev > 0:
            md = o.modifiers.new("bev", "BEVEL")
            md.width = bev
            md.segments = 3
            md.limit_method = "ANGLE"
            md.angle_limit = math.radians(50)
            bpy.context.view_layer.objects.active = o
            o.select_set(True)
            bpy.ops.object.modifier_apply(modifier="bev")
        try:
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
        except Exception:
            bpy.ops.object.shade_smooth()
        o.select_set(False)

    def prism(name, pts, z0, z1, mat, cx=0.0, cy=0.0, bev=0.0):
        n = len(pts)
        verts = [(cx + x, cy + y, z0) for x, y in pts] + [(cx + x, cy + y, z1) for x, y in pts]
        faces = [list(range(n, 2 * n)), list(range(n - 1, -1, -1))]
        for i in range(n):
            j = (i + 1) % n
            faces.append([i, j, n + j, n + i])
        me = bpy.data.meshes.new(name)
        me.from_pydata(verts, [], faces)
        me.update()
        o = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(o)
        finish(o, mat, bev, bev)
        return o

    def rring(name, outer, inner, z0, z1, mat, cx=0.0, cy=0.0, bev=0.0):
        n = len(outer)
        verts = [(cx + x, cy + y, z0) for x, y in outer] + [(cx + x, cy + y, z1) for x, y in outer] + \
                [(cx + x, cy + y, z0) for x, y in inner] + [(cx + x, cy + y, z1) for x, y in inner]
        faces = []
        for i in range(n):
            j = (i + 1) % n
            faces.append([n + i, n + j, 3 * n + j, 3 * n + i])   # top
            faces.append([i, j, n + j, n + i])                   # outer wall
            faces.append([2 * n + j, 2 * n + i, 3 * n + i, 3 * n + j])  # inner wall
        me = bpy.data.meshes.new(name)
        me.from_pydata(verts, [], faces)
        me.update()
        o = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(o)
        finish(o, mat, bev, bev)
        return o

    def bulb(x, y, z, r, mat, hot):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=(x, y, z + 0.2 * r), segments=20, ring_count=10)
        o = bpy.context.active_object
        o.scale = (1, 1, 0.55)
        o.data.materials.append(mat)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r * 0.45, location=(x, y, z + 0.5 * r), segments=12, ring_count=6)
        o = bpy.context.active_object
        o.scale = (1, 1, 0.5)
        o.data.materials.append(hot)

    def build(w, h, kind, sl):
        W, H = w * U, h * U
        base = COLORS[kind]
        chrome = metal("chrome", CHROME if kind != "disabled" else (150, 154, 158), rough=0.3 if kind != "disabled" else 0.5)
        copper = metal("copper", COPPER, rough=0.32, brushed=False, grad=(0.85, 1.1))
        org = lamp("orange", ORANGE, 3.0)
        hot = lamp("hot", (255, 214, 150), 4.0)
        if kind == "back":
            return build_back(W, H, base, chrome, copper, org, hot)
        big = kind in ("panel", "card", "frame")
        enam = enamel("navy", base, rough=0.3 if kind == "disabled" else 0.2, tide=0.16 if big else 0.1,
                      tide_amt=0.0 if kind in ("disabled", "pressed") else (0.22 if big else 0.1),
                      wear=0.3 if kind == "disabled" else 1.0, grime=0.25 if kind == "disabled" else 0.5)
        if kind == "frame":
            s = sl[0] * U
            f = 3 * U
            R = 9 * U
            rad = (R,) * 4
            rring("outer", rr_pts(W, H, rad), inset(W, H, rad, f), 0, 4.4 * U, chrome, bev=1.0 * U)
            # navy enamel band between the two chrome rings, copper trim line down its middle
            hole = rr_pts(W - 2 * s, H - 2 * s, (2 * U,) * 4)
            rring("band", inset(W, H, rad, f - 0.3 * U), hole, 0, 2.4 * U, enam, bev=0.6 * U)
            rring("lip", inset(W, H, rad, s - f), hole, 0, 4.8 * U, chrome, bev=1.0 * U)
            rring("trim", inset(W, H, rad, 5.4 * U), inset(W, H, rad, 6.3 * U), 2.4 * U, 3.0 * U, copper, bev=0.2 * U)
            for sx in (-1, 1):
                for sy in (-1, 1):
                    bulb(sx * (W / 2 - 7.6 * U), sy * (H / 2 - 7.6 * U), 2.4 * U, 1.3 * U, org, hot)
            return W, H
        if kind in ("tab", "tab_active"):
            rad = (8 * U, 8 * U, 0.4 * U, 0.4 * U)
            f = 2.0 * U
        elif kind == "panel" or kind == "card":
            rad = (9 * U,) * 4
            f = 3.0 * U
        else:
            rad = (7 * U,) * 4
            f = 2.0 * U
        hf = {"panel": 4.2, "card": 4.2}.get(kind, 3.2) * U
        ft = {"panel": 2.4, "card": 2.4, "button": 2.2, "hover": 2.6, "pressed": 0.8, "disabled": 1.8, "tab": 1.8, "tab_active": 2.8}[kind] * U
        rring("bezel", rr_pts(W, H, rad), inset(W, H, rad, f), 0, hf, chrome, bev=0.9 * U)
        prism("field", inset(W, H, rad, f - 0.2 * U), 0, ft, enam, bev=0.6 * U)
        if big:
            t0 = 6.4 * U
            rring("trim", inset(W, H, rad, t0), inset(W, H, rad, t0 + 0.9 * U), ft, ft + 0.5 * U, copper, bev=0.15 * U)
            if kind == "card":
                for sx in (-1, 1):
                    for sy in (-1, 1):
                        bulb(sx * (W / 2 - 9.4 * U), sy * (H / 2 - 9.4 * U), ft, 1.3 * U, org, hot)
        if kind == "hover":
            # signal-orange landing-light strip along the foot of the face, with a lit lamp at each end
            sw, sh = W - 2 * f - 9 * U, 2.0 * U
            prism("strip", rr_pts(sw, sh, (0.9 * U,) * 4), ft, ft + 0.5 * U, org, cy=-H / 2 + f + 3.4 * U, bev=0.15 * U)
            for sx in (-1, 1):
                bulb(sx * (sw / 2 + 2.4 * U), -H / 2 + f + 3.4 * U, ft, 1.15 * U, org, hot)
        elif kind == "tab_active":
            sw = W - 2 * f - 12 * U
            prism("tick", rr_pts(sw, 1.6 * U, (0.7 * U,) * 4), ft, ft + 0.5 * U, org, cy=H / 2 - f - 2.4 * U, bev=0.1 * U)
        elif kind == "pressed":
            sw = W - 2 * f - 12 * U
            prism("dimstrip", rr_pts(sw, 1.4 * U, (0.6 * U,) * 4), ft, ft + 0.2 * U, lamp("dim", (150, 70, 30), 0.8), cy=-H / 2 + f + 3.2 * U)
        return W, H

    def build_back(W, H, base, chrome, copper, org, hot):
        px = lambda x: x * U - W / 2  # noqa: E731
        py = lambda y: H / 2 - y * U  # noqa: E731
        hull = enamel("hull", base, rough=0.24, tide=0.085, tide_amt=0.17, wear=0.8, grime=0.4)
        dk = enamel("dkplate", (12, 24, 50), rough=0.18, wear=0.5, grime=0.3, sheen=(0.95, 1.05))
        rad = (8 * U,) * 4
        rring("frame", rr_pts(W, H, rad), inset(W, H, rad, 3 * U), 0, 4.0 * U, chrome, bev=1.2 * U)
        prism("plate", inset(W, H, rad, 2.8 * U), 0, 2.0 * U, hull, bev=0.8 * U)
        # horizontal double rules: two thin raised lines each, chrome for the upper pair, copper for the lower
        for k, sy_ in enumerate(BACK_SEAMS):
            mat = chrome if k < 2 else copper
            for dy in (-1.7, 1.7):
                prism("rule", rr_pts(W - 22 * U, 1.1 * U, (0.5 * U,) * 4), 2.0 * U, 2.8 * U, mat, cx=0, cy=py(sy_ + dy), bev=0.15 * U)
        # credits plate: chrome-rimmed deep-navy plate (text lands here in white)
        x0, y0, x1, y1 = BACK_CREDITS
        cw, ch = (x1 - x0) * U, (y1 - y0) * U
        ccx, ccy = px((x0 + x1) / 2), py((y0 + y1) / 2)
        crad = (5 * U,) * 4
        rring("credrim", rr_pts(cw, ch, crad), inset(cw, ch, crad, 1.3 * U), 2.0 * U, 3.6 * U, chrome, cx=ccx, cy=ccy, bev=0.4 * U)
        prism("credplate", inset(cw, ch, crad, 1.1 * U), 2.0 * U, 2.8 * U, dk, cx=ccx, cy=ccy, bev=0.3 * U)
        # emblem roundel: chrome ring round a deep-navy disc, copper keyline, four orange marker lamps
        ex, ey, er = BACK_EMBLEM
        n = 64
        circ = lambda r: [(r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n)) for i in range(n)]  # noqa: E731
        cx_, cy_ = px(ex), py(ey)
        rring("emrim", circ(er * U), circ((er - 3.2) * U), 2.0 * U, 4.4 * U, chrome, cx=cx_, cy=cy_, bev=0.9 * U)
        prism("emdisc", circ((er - 3.0) * U), 2.0 * U, 2.8 * U, dk, cx=cx_, cy=cy_, bev=0.3 * U)
        rring("emtrim", circ((er - 3.6) * U), circ((er - 4.5) * U), 2.8 * U, 3.3 * U, copper, cx=cx_, cy=cy_, bev=0.1 * U)
        for k in range(4):
            a = math.radians(k * 90 + 45)
            bulb(cx_ + (er + 6.5) * U * math.cos(a), cy_ + (er + 6.5) * U * math.sin(a), 2.0 * U, 1.4 * U, org, hot)
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
        bs.inputs["Roughness"].default_value = 0.3
        nt.links.new(tex.outputs["Color"], bs.inputs["Base Color"])
        tr = N(nt, "ShaderNodeBsdfTransparent")
        chip = ramp(nt, noise(nt, 55.0, 5.0), [(0.10, 0.0), (0.20, 1.0)])  # only the odd chip: the enamel is new
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
        sun_d.energy = 3.4
        sun_d.angle = 0.1
        sun = bpy.data.objects.new("sun", sun_d)
        sc.collection.objects.link(sun)
        sun.rotation_euler = (math.radians(38), math.radians(-30), math.radians(-35))
        # studio-style world: a bright side and a dark side so bevelled chrome picks up highlights
        w_ = bpy.data.worlds.new("w")
        w_.use_nodes = True
        nt = w_.node_tree
        bg = nt.nodes["Background"]
        tc = N(nt, "ShaderNodeTexCoord")
        sp = N(nt, "ShaderNodeSeparateXYZ")
        nt.links.new(tc.outputs["Object"], sp.inputs["Vector"])
        gx = ramp(nt, mth(nt, "ADD", sp.outputs["X"], mth(nt, "MULTIPLY", sp.outputs["Y"], 0.6)), [(0.0, 0.05), (0.55, 0.45), (1.0, 1.0)])
        nt.links.new(grey_col(nt, mth(nt, "MULTIPLY", gx, 1.0)), bg.inputs["Color"])
        bg.inputs["Strength"].default_value = 1.1
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
