"""Shared Directorate structure parts (M3-ART batch): materials, ramp entrances, hoods, hazard bars, stacks, loudspeakers.

Everything here is lifted from / consistent with the approved dir_smelter_vent so the Directorate set reads as one family:
umber / warm concrete brutalism, mostly underground (dark ramp mouths, hoods, stacks, ash), magenta house panels, small red
ember glows, bone-and-oxide hazard stripes on the edges.  Units: 3.4 per cell, x east, y NORTH, z up (see auth_conyard).
Modules use these through `import dir_struct_common as D`; the helpers take the Ctx `S` that build_structure hands to build().
"""
import importlib.util
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_HERE))
import common as _C


def _load(fname, name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(_HERE, fname))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


L = _load("auth_conyard.py", "structlib_auth_conyard")      # stages(), beam(), post(), anchor(), scaffold() ...
V = _load("dir_smelter_vent.py", "structlib_dir_vent")      # approved reference: materials + tilted()
PALETTE = _C.PAL_DIR
CAP_MATERIAL = "concrete2"
tilted = V.tilted
anchor = L.anchor
beam = L.beam

MATERIALS = dict(V.MATERIALS)
MATERIALS.update({
    "brass": ("#C9963A", dict(rough=0.45, emit="#E0A030")), "brass_dim": ("#6A5226", dict(rough=0.6)),
    "brass_s": ("#3E3322", dict(rough=0.6)), "earth": ("#6B5440", dict(rough=1.0)), "earth2": ("#4C3B2D", dict(rough=1.0)),
    "lead": ("#5F5B57", dict(rough=0.7, metal=0.2)), "lead_s": ("#3A3836", dict(rough=0.7)),
})
SOOT_SKIN = dict(V.SOOT_SKIN)
SOOT_SKIN.update({"brass": "oxide2_s", "brass_dim": "brass_s", "lead": "lead_s", "earth": "pad_s", "earth2": "pad_s"})
PRIMER_SKIN = dict(V.PRIMER_SKIN)
PRIMER_SKIN.update({"brass": "dark", "brass_dim": "dark", "lead": "primer", "earth": "raw", "earth2": "raw"})
HIDE_ANIM = ("sten", "glow", "puff", "horn", "hdrv", "hrim", "hthr", "harm", "hpole", "chev")


def ramp_z_fn(ytop, yb, bot):
    return lambda y: 0.14 + (bot - 0.14) * (y - ytop) / (yb - ytop)


# ------------------------------------------------------------------------------------------------ pad, hazard bar
def hazard_bar(S, x0, x1, y0, y1, seg=0.6, z0=0.14, z1=0.2, tag="haz"):
    n = int(math.ceil((x1 - x0) / seg - 1e-6))
    for k in range(n):
        xa = x0 + k * seg
        S.bb(f"{tag}{k}", xa, min(xa + seg, x1), y0, y1, z0, z1, "oxide" if k % 2 == 0 else "bone", 0.0)


def pad_with_trench(S, pad, tr, ytop, ydoor, lip=True):
    """Pad slab with a ramp trench cut out of it (trench x range `tr`, from ytop north to the mouth at ydoor)."""
    px0, px1, py0, py1 = pad
    S.bb("pad", px0, tr[0], py0, py1, 0.0, 0.14, "pad", 0.02)
    S.bb("pad", tr[1], px1, py0, py1, 0.0, 0.14, "pad", 0.02)
    S.bb("pad", tr[0], tr[1], ydoor, py1, 0.0, 0.14, "pad", 0.02)
    S.bb("pad", tr[0], tr[1], py0, ytop, 0.0, 0.14, "pad", 0.02)
    if lip:
        S.bb("padlip", px0, px1, py0, py0 + 0.1, 0.14, 0.2, "concrete2", 0.0)


# ------------------------------------------------------------------------------------------------ the ramp mouth
def ramp_entrance(S, tr, ytop, ydoor, bot, dmg, mouth_top=1.95, doors=True, glow=True, nst=7):
    """Trench cheeks, stepped ramp, dark mouth with a deep ember glow, blast doors swung to the sides.
    tr = (x0, x1); the ramp runs from the pad (z .14) at y=ytop down to z=bot at the doors (ydoor)."""
    YS = S.YS
    yb = ydoor - 0.12
    rz = ramp_z_fn(ytop, yb, bot)
    for xa, xb in ((tr[0] - 0.25, tr[0]), (tr[1], tr[1] + 0.25)):
        for k in range(7):
            ya = ytop + (yb - ytop) * k / 7
            yb_ = ytop + (yb - ytop) * (k + 1) / 7 + (0.1 if k == 6 else 0.0)
            zb = max(rz(yb_) - 0.12, 0.0) if k < 1 else rz(yb_) - 0.12
            S.bb("cheek", xa, xb, ya, yb_, zb, 0.2, "concrete2", 0.0)
    S.bb("landing", tr[0], tr[1], ydoor - 0.5, ydoor, bot - 0.45, bot, "dark", 0.0)
    tilted(S, "ramp", tr[0], tr[1], ytop, 0.14 - 0.06, yb, bot - 0.06, 0.12, "rampfloor")
    mats_a = ("white", "hull", "concrete", "concrete2", "hull2", "rampfloor", "rampfloor")
    mats_b = ("concrete2", "hull2", "rampfloor", "dark", "dark", "dark", "dark")
    for k in range(nst):
        ya, yb_ = ytop + (yb - ytop) * k / nst, ytop + (yb - ytop) * (k + 1) / nst - 0.07
        zt_ = rz(ya + (yb_ - ya) / 2) + 0.03
        kk = min(6, int(k * 7 / nst))
        S.bb(f"tread{k}", tr[0] + 0.15, tr[1] - 0.15, ya, yb_, zt_ - 0.2, zt_, mats_a[kk] if k % 2 == 0 else mats_b[kk], 0.0)
    for xa in (tr[0] + 0.1, tr[1] - 0.3):
        tilted(S, "rampline", xa, xa + 0.2, ytop, 0.14, yb, bot, 0.1, "hull", nocast=True)
    S.bb("mouth", tr[0], tr[1], ydoor - 0.04, ydoor, bot - 0.05, mouth_top, "dark", 0.0)
    g = "oxide2" if dmg else "redglow"
    if glow:
        wdt = (tr[1] - tr[0]) / 2 - 0.5
        cx = (tr[0] + tr[1]) / 2
        S.bb("glowb", cx - wdt * 0.8, cx + wdt * 0.8, ydoor - 0.1, ydoor - 0.04, bot + 0.02, bot + 0.4, g, 0.0, nocast=True)
        S.bb("glowfloor", cx - wdt * 0.8, cx + wdt * 0.8, ydoor - 0.3, ydoor - 0.1, bot, bot + 0.04, g, 0.0, nocast=True)
    S.bb("mouthlip", tr[0] - 0.1, tr[1] + 0.1, ydoor - 0.06, ydoor, mouth_top, mouth_top + 0.15, "concrete2", 0.0)
    if not doors:
        return
    for k, (xa, xb) in enumerate(((tr[0], tr[0] + 0.1), (tr[1] - 0.1, tr[1]))):
        yz0, yz1 = ydoor - 1.9, ydoor - 0.1
        if dmg and k == 1:
            S.box("doorB", (0.1, 1.2, 0.8), (xb - 0.25, (ydoor - 0.9) * YS, bot + 0.5), "door", 0.0, rot=(0, 0, 18), stretch=False)
            continue
        for q in range(5):
            qa, qb = yz0 + (yz1 - yz0) * q / 5, yz0 + (yz1 - yz0) * (q + 1) / 5
            S.bb(f"door{k}", xa, xb, qa, qb, rz(qb) - 0.04, rz(qa) + 0.85, "door", 0.0)
        S.bb(f"doorband{k}", xa - 0.01 if k == 0 else xa, xb if k == 0 else xb + 0.01, yz0 + 0.1, yz1 - 0.1, bot + 0.45, bot + 0.6, "oxide", 0.0, nocast=True)


def flank(S, tag, xa, xb, yface, z0=0.5, z1=1.95, stripes=3):
    """Wall panel beside a mouth: umber slab, oxide band, bone stencil plate with dark stencil bars."""
    S.bb(f"flank{tag}", xa, xb, yface - 0.06, yface, z0, z1, "concrete2", 0.0)
    S.bb(f"flankband{tag}", xa, xb, yface - 0.1, yface - 0.06, z0 + 0.05, z0 + 0.3, "oxide", 0.0)
    S.bb(f"flankplate{tag}", xa + 0.05, xb - 0.05, yface - 0.1, yface - 0.06, z0 + 0.45, z1 - 0.15, "bone", 0.0)
    for k in range(stripes):
        S.bb(f"sten_f{tag}{k}", xa + 0.1, xb - 0.1, yface - 0.12, yface - 0.1, z0 + 0.55 + k * 0.25, z0 + 0.67 + k * 0.25, "dark", 0.0, nocast=True)


def hood(S, x0, x1, y0, y1, dmg, z0=1.95, z1=2.3, band_z=(1.98, 2.27), collapse=2.6):
    """Cantilevered concrete lintel with a magenta house-colour band across its front.  Damaged: the right end has fallen."""
    YS = S.YS
    if dmg:
        xe = x1 - collapse
        S.bb("hood", x0, xe, y0, y1, z0, z1, "concrete2", 0.04)
        S.box("hoodslab", (1.2, 0.9, 0.35), (xe + 1.2, (y0 + 0.1) * YS, 0.0), "concrete2", 0.02, rot=(0, 0, 14), stretch=False)
        S.box("hoodslab2", (0.7, 0.6, 0.3), (xe + 1.5, (y0 - 0.5) * YS, 0.35), "concrete2", 0.0, rot=(0, 0, -30), stretch=False)
        for k, (a_, b_, c_, d_) in enumerate(((xe, xe + 0.4, y0 + 0.1, y0 + 0.5), (xe + 0.1, xe + 0.6, y0 + 0.7, y0 + 0.9))):
            L.beam(S, f"rbar{k}", (a_, c_, 2.1), (b_, d_, 2.6 + 0.2 * k), 0.05, "rebar")
        S.bb("banding", x0 + 0.1, xe - 0.05, y0 - 0.06, y0 - 0.02, band_z[0], band_z[1], "house", 0.0)
        for k, (x, y, w, h) in enumerate(((xe + 0.6, y0 - 0.3, 0.4, 0.3), (xe + 1.8, y0 - 0.1, 0.35, 0.28), (xe + 0.3, y0 + 0.3, 0.3, 0.25),
                                           (xe + 2.1, y0 + 0.5, 0.3, 0.22), (xe + 1.0, y0 - 1.0, 0.5, 0.35))):
            S.box(f"rub{k}", (w, w * 0.8, h), (x, y * YS, 0.14 + h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k), stretch=False)
    else:
        S.bb("hood", x0, x1, y0, y1, z0, z1, "concrete2", 0.04)
        S.bb("banding", x0 + 0.1, x1 - 0.1, y0 - 0.06, y0 - 0.02, band_z[0], band_z[1], "house", 0.0)


# ------------------------------------------------------------------------------------------------ stacks, speakers, vents
def chimney(S, sx, sy, z_base, top, dmg, puffs, i, rise=1.9, lean_len=1.6):
    """Banded concrete smelter stack with an ash plume (idle) or a snapped stump and leaning section (damaged)."""
    YS = S.YS
    S.bb("chbase", sx - 0.7, sx + 0.7, sy - 0.7, sy + 0.7, z_base, z_base + 0.35, "concrete2", 0.03)
    h0 = z_base + 0.35
    S.cone("chimney", 0.62, 0.42, top - h0, (sx, sy, (top + h0) / 2), "concrete", verts=16, nocast=False)
    zz = h0 + 0.8
    k = 0
    while zz < top - 0.15:
        S.cone(f"cband{k}", 0.58, 0.55, 0.22, (sx, sy, zz), "bone" if k % 2 == 0 else "oxide", verts=16, nocast=False)
        zz += 0.7
        k += 1
    if not dmg:
        S.cyl("ccollar", 0.55, 0.2, (sx, sy, top + 0.1), "steel", verts=16)
        S.cyl("cmouth", 0.4, 0.08, (sx, sy, top + 0.21), "dark", verts=12)
        if puffs:
            for q in range(3):
                ph = ((i / 4) + q / 3) % 1
                r = 0.14 + 0.42 * math.sin(math.pi * ph) ** 0.7
                S.sphere(f"puff{q}", r, (sx + ph * 1.1, sy, top + 0.45 + ph * rise + r * 0.3), "smoke2" if q != 1 else "smoke", squash=0.85)
    else:
        S.cyl("cstump", 0.45, 0.18, (sx, sy, top + 0.05), "concrete2", verts=12)
        S.cyl("cmouthd", 0.32, 0.06, (sx, sy, top + 0.15), "dark", verts=10)
        tl = math.radians(-52)
        o = S.cone("chleanA", 0.5, 0.4, lean_len, (0, 0, 0), "concrete", verts=14, nocast=False)
        o.location = (sx - 0.1 + (lean_len / 2) * math.sin(tl), sy * YS, top + 0.12 + (lean_len / 2) * math.cos(tl))
        o.rotation_euler = (0, tl, 0)


def loudspeaker(S, px, py, zb, dmg, height=2.3):
    """Pole with a pair of flared trumpets pointing out to the sides, bone rims, dark throats."""
    YS = S.YS
    if dmg:
        S.cyl("hpole", 0.07, 1.3, (px, py, zb + 0.65), "steel", verts=8)
        S.box("hpoleF", (1.6, 0.08, 0.08), (px - 0.9, (py + 0.5) * YS, zb + 0.12), "steel", 0.0, rot=(0, 0, 25), stretch=False)
        S.cone("hornF", 0.08, 0.3, 0.8, (px - 0.3, py + 0.9, zb + 0.3), "door", verts=10, rot=(0, 90, 25), nocast=False)
        return
    zt = zb + height
    S.cyl("hpole", 0.07, height, (px, py, zb + height / 2), "steel", verts=8)
    S.bb("hpoleb", px - 0.2, px + 0.2, py - 0.2, py + 0.2, zb, zb + 0.3, "concrete2", 0.02)
    S.bb("harm", px - 0.35, px + 0.35, py - 0.05, py + 0.05, zt - 0.05, zt + 0.05, "steel", 0.0)
    for sgn in (-1, 1):
        S.cyl(f"hdrv{sgn}", 0.13, 0.28, (px + sgn * 0.45, py, zt), "oxide", axis="X", verts=10)
        S.cone(f"horn{sgn}", 0.07, 0.36, 0.9, (px + sgn * 0.98, py, zt), "door", verts=14, rot=(0, 90 * sgn, 0), nocast=False)
        S.cyl(f"hrim{sgn}", 0.38, 0.05, (px + sgn * 1.45, py, zt), "bone", axis="X", verts=14)
        S.cyl(f"hthr{sgn}", 0.29, 0.06, (px + sgn * 1.46, py, zt), "dark", axis="X", verts=12)


def roof_vents(S, zr, specs):
    """Short louvred roof vents: specs = [(x, y, h)]."""
    for k, (vx, vy, vh) in enumerate(specs):
        S.cyl(f"vent{k}", 0.26, vh, (vx, vy, zr + vh / 2), "hull2", verts=12)
        S.cyl(f"venth{k}", 0.38, 0.12, (vx, vy, zr + vh + 0.06), "steel", verts=12)
        S.cyl(f"ventd{k}", 0.2, 0.05, (vx, vy, zr + vh + 0.14), "dark", verts=10)


def ash_drift(S, specs):
    for k, (ax_, ay_, aw) in enumerate(specs):
        S.bb(f"ash{k}", ax_ - aw / 2, ax_ + aw / 2, ay_ - 0.3, ay_ + 0.3, 0.14, 0.17, "smoke", 0.0, nocast=True)


def furnace_slits(S, x0, y, z0, z1, n, i, lit_on, step=0.55, w=0.3):
    """Row of small ember slits in a wall facing south; they flicker with the frame index."""
    for k in range(n):
        lit = lit_on and ((k + i) % 3 != 0)
        S.bb(f"glow{k}", x0 + k * step, x0 + k * step + w, y, y + 0.1, z0, z1, "redglow" if lit else "oxide2", 0.0, nocast=True)
