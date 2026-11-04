"""Helpers shared by the Directorate storage / defence structures of package art-str3 (cistern, bunker wall, pillbox,
rocket nest, radiation mortar).  Not an asset itself.  Imports the approved kit (dir_struct_common as D) read-only and adds:
extra materials (umber sacks, a small sickly-green glow), a true-3D placement helper for tilted parts (racks, tubes), sandbags,
and a generic build-stage spec for the stages() driver.  Units: 3.4 per cell, x east, y NORTH, z up.
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import dir_struct_common as D

L = D.L

MATERIALS = dict(D.MATERIALS)
MATERIALS.update({
    "sack": ("#8A7660", dict(rough=1.0)), "sack2": ("#6A5846", dict(rough=1.0)), "sack_s": ("#4A4036", dict(rough=1.0)),
    "sickly": ("#A8D040", dict(rough=0.5, emit="#8CC030")), "sickly_dim": ("#4A5A26", dict(rough=0.7)),
    "gunmetal": ("#2A2E31", dict(rough=0.35, metal=0.7)), "steel2": ("#979EA3", dict(rough=0.4, metal=0.5)),
    "flash": ("#FFF2B0", dict(rough=0.3, emit="#FFE9A0")), "flash2": ("#FFD36A", dict(rough=0.3, emit="#FFC850")),
})
SOOT_SKIN = dict(D.SOOT_SKIN)
SOOT_SKIN.update({"sack": "sack_s", "sack2": "sack_s", "sickly": "sickly_dim", "steel2": "steel_s"})
PRIMER_SKIN = dict(D.PRIMER_SKIN)
PRIMER_SKIN.update({"sack": "primer", "sack2": "primer", "sickly": "dark", "sickly_dim": "dark", "gunmetal": "dark", "steel2": "primer",
                    "flash": "dark", "flash2": "dark"})


def l2w(S, base, c, pitch=0.0, th=0.0):
    """Local (right, forward, up) -> world.  The part is pitched (+ lifts the forward end) then yawed th degrees clockwise from
    north, about base = (x, y_game, z).  Offsets are true 3D (unstretched); only the base y is the stretched ground position."""
    r, f, u = c
    p, t = math.radians(pitch), math.radians(th)
    y1 = f * math.cos(p) - u * math.sin(p)
    z1 = f * math.sin(p) + u * math.cos(p)
    return (base[0] + r * math.cos(t) + y1 * math.sin(t), base[1] * S.YS + (-r * math.sin(t) + y1 * math.cos(t)), base[2] + z1)


def p3(S, name, size, base, c, m, pitch=0.0, th=0.0, bevel=0.03, nocast=False):
    """Box (width, length, height) at local position c of a part group pitched / yawed about `base`."""
    return S.box(name, size, l2w(S, base, c, pitch, th), m, bevel, rot=(pitch, 0, -th), stretch=False, nocast=nocast)


def c3(S, name, r, h, base, c, m, th=0.0, axis="Z", verts=14):
    """Upright cylinder (axis Z) or a cylinder lying along the world X / Y axis, true 3D."""
    return S.cyl(name, r, h, l2w(S, base, c, 0.0, th), m, axis=axis, verts=verts, stretch=False)


def sack_ring(S, cx, cy, rx, ry, courses, gaps=(), dmg=False, n=14, bag=(0.62, 0.34, 0.26), tag="sb"):
    """Ring of umber sandbags (ground-stretched boxes, so they sit on the grid like the pad); `gaps` = indices left open."""
    YS = S.YS
    zb = 0.14
    for ci in range(courses):
        off = (ci % 2) * 0.5
        for k in range(n):
            if k in gaps:
                continue
            a = (k + off) * 2 * math.pi / n
            x, y = cx + rx * math.sin(a), cy + ry * math.cos(a)
            h = 0.26
            z0 = zb + ci * h
            if dmg and ci == courses - 1 and k % 3 == 0:
                continue
            S.box(f"{tag}{ci}_{k}", (bag[0], bag[1] * 1.0, h), (x, y * YS, z0 + h / 2), "sack" if (k + ci) % 2 == 0 else "sack2", 0.07,
                  rot=(0, 0, -math.degrees(a)), stretch=False)


def spill(S, pts, dmg=True):
    """Burst sandbags / rubble on the ground: pts = [(x, y, w, h)]."""
    YS = S.YS
    for k, (x, y, w, h) in enumerate(pts):
        S.box(f"spl{k}", (w, w * 0.75, h), (x, y * YS, 0.14 + h / 2), "sack" if k % 2 else "concrete_s", 0.06, rot=(0, 0, 37 * k), stretch=False)


def stage_spec(pad, env, H, boxes, rings, scaf, derrick, sparks, clips, hide=()):
    """Generic build-stage spec for L.stages(): clutter and leftovers go on the pad corners (south-east / south-west)."""
    px0, px1, py0, py1 = pad

    def _clutter(S, i):
        if i <= 1:
            S.bb("crate", px1 - 1.0, px1 - 0.3, py0 + 0.45, py0 + 1.1, 0.14, 0.8, "crate", 0.03)
        if i <= 2:
            for z in (0.3, 0.6):
                S.cyl("lpipe", 0.13, 1.4, (px0 + 1.0, py0 + 0.55, z), "steel", axis="X", verts=10)
            S.bb("rebarpile", px0 + 0.2, px0 + 1.4, py0 + 0.85, py0 + 1.0, 0.14, 0.26, "rebar", 0.0)
        S.bb("planks", px1 - 1.3, px1 - 0.2, py1 - 0.7, py1 - 0.25, 0.14, 0.45, "timber", 0.0)

    def _left(S):
        S.bb("planks", px0 + 0.2, px0 + 1.2, py0 + 0.5, py0 + 0.9, 0.14, 0.3, "timber", 0.0)
        S.cyl("lpipe", 0.13, 1.4, (px1 - 1.0, py0 + 0.6, 0.3), "steel", axis="X", verts=10)
        S.bb("crate", px1 - 0.9, px1 - 0.3, py0 + 0.9, py0 + 1.4, 0.14, 0.7, "crate", 0.03)
        L.spark(S, 0.0, py0 + 0.9, 1.2, 3, 6)

    return dict(pad=pad, env=env, H=H, boxes=boxes, rings=rings, clips=clips, scaf=scaf, derrick=derrick, sparks=sparks,
                hide=D.HIDE_ANIM + tuple(hide), clutter=_clutter, left=_left)


# ---------------------------------------------------------------------------------------------- rotating heads (pillbox)
def part(S, name, size, c, m, th, bevel=0.03, pitch=0.0, nocast=False):
    """Box in head-local coordinates: c = (right, forward, z); th = head facing in degrees clockwise from north."""
    t = math.radians(th)
    wx = c[0] * math.cos(t) + c[1] * math.sin(t)
    wy = -c[0] * math.sin(t) + c[1] * math.cos(t)
    return S.box(name, size, (wx, wy, c[2]), m, bevel, rot=(pitch, 0, -th), stretch=False, nocast=nocast)


def cpart(S, name, r, h, c, m, th, axis="Z", verts=16):
    """Cylinder in head-local coordinates (axis Z, or Y = along the gun)."""
    t = math.radians(th)
    wx = c[0] * math.cos(t) + c[1] * math.sin(t)
    wy = -c[0] * math.sin(t) + c[1] * math.cos(t)
    o = S.cyl(name, r, h, (wx, wy, c[2]), m, axis="Z", verts=verts, stretch=False)
    if axis == "Y" and o is not None:
        o.rotation_euler = (math.radians(90), 0, -t)
    return o
