"""Left Behind Scrap Wall: salvaged-sheet wall pieces (1x1 cell) with tyre pillars, wooden posts and a rag-and-wire top.  Original design.

Same tiling contract as auth_wall (M2-20 / D-072): one sheet with the wall as STATES chosen by the neighbours (the game picks from a
4-neighbour mask): h (east-west run), v (north-south run), end_n/e/s/w, corner_ne/nw/se/sw, t_n/e/s/w, cross and post, each with a
`_damaged` twin; `idle` / `damaged` (= h) for icons; `build` is generic.  The letters name the DIRECTIONS THE WALL CONTINUES.
Arms run from the hub to the cell edge (+-1.7) with the same three-panel pattern (rust plate, faded-teal bus sheet, ash road sign) so
neighbouring pieces meet seamlessly; pieces that are not straight get a pillar of stacked tyres with a pole and a house-colour pennant,
straight runs carry a house-colour painted patch.  Damage is chipping only: panels torn down to stubs, holes, soot.
Contract: see auth_wall.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-1.7, 1.7].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as K

ID = "lb_scrap_wall"
TITLE = "Left Behind Scrap Wall"
FOOTPRINT = (1, 1)
CANVAS = (64, 100)
ORIGIN = (0, 32)
DOCK = (0, 1)
CAP_MATERIAL = "dust"
PALETTE = K.PALETTE
MATERIALS = dict(K.MATERIALS)
MATERIALS.update({           # S22 detail pass: painted-scrap red, bleached paint
    "paintr": ("#A8412B", dict(rough=0.85)),
    "paintw": ("#D9D2BC", dict(rough=0.85)),
    "plate2": ("#6A625A", dict(rough=0.55, metal=0.4)),      # bare scavenged steel plate
})
SOOT_SKIN = dict(K.SOOT_SKIN)
SOOT_SKIN.update({"rust2": "rust_s", "dust2": "dust_s", "rope": "canvas_s", "paintr": "rust_s", "paintw": "bone_s", "plate2": "ash_s"})
HOUSE2_MID = False
FX = []

VARIANTS = {
    "h": "EW", "v": "NS",
    "end_n": "N", "end_e": "E", "end_s": "S", "end_w": "W",
    "corner_ne": "NE", "corner_nw": "NW", "corner_se": "SE", "corner_sw": "SW",
    "t_n": "NEW", "t_e": "NES", "t_s": "ESW", "t_w": "NSW",
    "cross": "NESW", "post": "",
}
STATES = [("idle", 1, 1), ("damaged", 1, 1)]
for _v in VARIANTS:
    STATES.append((_v, 1, 1))
    STATES.append((_v + "_damaged", 1, 1))
STATES.append(("build", 4, 5))

HX = 1.7
WT = 0.34
# (u0, u1, material, damaged material, height, damaged height)
PANELS = ((0.0, 0.62, "rust", "rust_s", 1.5, 1.5), (0.62, 1.15, "teal", "teal_s", 1.8, 1.0), (1.15, HX, "ash", "ash_s", 1.4, 0.7))


def T(d, u, v):
    return {"E": (u, v), "W": (-u, v), "N": (v, u), "S": (v, -u)}[d]


def ab(S, name, d, u0, u1, v0, v1, z0, z1, mat, bevel=0.0, **kw):
    x0, y0 = T(d, u0, v0)
    x1, y1 = T(d, u1, v1)
    return S.bb(name, min(x0, x1), max(x0, x1), min(y0, y1), max(y0, y1), z0, z1, mat, bevel, **kw)


def pillar(S, dmg):
    for c in range(3):
        a = c * 35
        S.cyl(f"tyre{c}", 0.55, 0.34, (0, 0, 0.0 + 0.17 + c * 0.34), "rubber", verts=14, stretch=False)
        S.cyl(f"tyreh{c}", 0.3, 0.36, (0, 0, 0.17 + c * 0.34), "dust2", verts=10, stretch=False, nocast=True)
    top = 1.6 if not dmg else 1.1
    K.post(S, "pole", 0.0, 0.0, 0.9, 2.5 if not dmg else 1.7, 0.1, "wood")
    if not dmg:
        S.box("pennant", (0.62, 0.04, 0.4), (0.36, -0.05 * S.YS, 2.25), "house", 0.0, stretch=False)
        S.sphere("knob", 0.09, (0.0, 0.0, 2.55), "hazard")
    else:
        S.box("pennant", (0.3, 0.04, 0.4), (0.15, -0.05 * S.YS, 1.5), "house", 0.0, stretch=False)


def detail(S, d, dmg):
    """S22 detail pass for one arm: mismatched bolted plates, painted scraps, rust streaks, rope and wire lashings at the posts,
    rags on the top wire, ground-in dirt at the foot.  Flat cladding on the existing panel layout: no silhouette change."""
    f = -WT - 0.02
    lim = (1.5, 1.0, 0.7) if dmg else (1.5, 1.8, 1.4)

    def put(name, u0, u1, z0, z1, mat, k, th=0.04, off=0.0):
        if z1 <= lim[k]:
            ab(S, name + d, d, u0, u1, f - th - off, f - off, z0, z1, mat, 0.0, nocast=True)

    # dirt band at the foot of every panel
    for k, (u0, u1, *_r) in enumerate(PANELS):
        put("dirt", u0, u1, 0.0, 0.3, "dust2", k, 0.03)
    # panel 0 (rust sheet): a bare steel plate bolted on, rivets, a painted red scrap with a bone mark
    put("plt", 0.06, 0.5, 0.35, 0.85, "plate2", 0, 0.05)
    for (uu, zz) in ((0.1, 0.4), (0.46, 0.4), (0.1, 0.8), (0.46, 0.8)):
        put("rv", uu - 0.03, uu + 0.03, zz - 0.03, zz + 0.03, "dark", 0, 0.08)
    put("scrap", 0.1, 0.52, 0.95, 1.35, "paintr", 0, 0.04)
    put("scrapm", 0.2, 0.42, 1.08, 1.22, "paintw", 0, 0.06)
    # panel 1 (teal bus sheet): faded paint fragment, bullet-dent cluster, rust streaks from the rib tops
    put("frag", 0.72, 1.05, 1.15, 1.55, "paintw", 1, 0.04)
    put("fragb", 0.72, 1.05, 1.15, 1.25, "paintr", 1, 0.06)
    for (uu, zz) in ((0.76, 0.5), (0.86, 0.62), (0.95, 0.46), (0.82, 0.74)):
        put("dent", uu - 0.04, uu + 0.04, zz - 0.04, zz + 0.04, "dark", 1, 0.06)
    for (uu, z0, z1) in ((0.7, 0.55, 1.45), (1.0, 0.7, 1.7)):
        put("streak", uu - 0.04, uu + 0.04, z0, z1, "rust", 1, 0.06)
    # panel 2 (ash road sign): a mismatched ochre sheet and stencil bars
    put("och", 1.2, 1.62, 0.2, 0.62, "ochre", 2, 0.05)
    for (uu, zz) in ((1.24, 0.24), (1.58, 0.24), (1.24, 0.58), (1.58, 0.58)):
        put("rv", uu - 0.03, uu + 0.03, zz - 0.03, zz + 0.03, "dark", 2, 0.08)
    for j in range(3):
        put("stn", 1.22 + 0.15 * j, 1.28 + 0.15 * j, 1.06, 1.3, "dark", 2, 0.05) if not dmg else None
    # rope lashings round the wooden posts (bands across the face) with a wire loop
    for u, k in ((0.62, 1), (1.15, 2)):
        for zz in (0.5, 0.95, 1.4):
            if zz + 0.1 <= lim[k]:
                ab(S, f"lash{d}", d, u - 0.1, u + 0.1, f - 0.07, f - 0.02, zz, zz + 0.1, "rope", 0.0, nocast=True)
                ab(S, f"lashw{d}", d, u - 0.1, u + 0.1, f - 0.08, f - 0.07, zz + 0.04, zz + 0.07, "dark", 0.0, nocast=True)
    # rags hanging from the top wire
    if not dmg:
        for (uu, m) in ((0.3, "canvas"), (0.95, "paintr"), (1.45, "canvas2")):
            ab(S, f"rag{d}", d, uu - 0.07, uu + 0.07, -0.05, 0.0, 1.68, 1.98, m, 0.0, nocast=True)
    else:
        ab(S, f"rag{d}", d, 0.3, 0.44, -0.05, 0.0, 0.95, 1.22, "canvas", 0.0, nocast=True)


def arm(S, d, dmg, patch):
    for k, (u0, u1, m, ms, h, hs) in enumerate(PANELS):
        hh = hs if dmg else h
        mm = ms if dmg else m
        ab(S, f"panel{k}{d}", d, u0, u1, -WT, WT, 0.0, hh, mm, 0.0)
        if k == 1:
            ab(S, f"rib{k}{d}", d, u0 + 0.05, u1 - 0.05, -WT - 0.04, -WT, 0.2, hh - 0.1, "teal2" if not dmg else "teal2_s", 0.0, nocast=True)
        if k == 2:
            ab(S, f"stripe{d}", d, u0, u1, -WT - 0.03, -WT, (0.8 if not dmg else 0.35), 1.0 if not dmg else 0.6, "bone" if not dmg else "bone_s", 0.0, nocast=True)
        if k == 0:
            ab(S, f"rivet{d}", d, 0.2, 0.3, -WT - 0.04, -WT, 1.3, 1.45, "dark", 0.0, nocast=True)
    # wooden posts at the panel joints, rope-and-wire along the top
    for u in (0.62, 1.15):
        K.post(S, f"wpost{d}", *T(d, u, 0.0), 0.0, 2.05 if not dmg else 1.3, 0.14, "wood")
    zt = 2.0 if not dmg else 1.25
    ab(S, f"wire{d}", d, 0.0, HX, -0.03, 0.03, zt, zt + 0.05, "dark", 0.0, nocast=True)
    if not dmg:
        ab(S, f"wire2{d}", d, 0.0, HX, -0.03, 0.03, zt - 0.3, zt - 0.25, "rope", 0.0, nocast=True)
    # lying tyre at the foot, one house patch on the arm that carries it
    ab(S, f"foot{d}", d, 0.75, 1.1, -WT - 0.5, -WT - 0.05, 0.0, 0.3, "rubber", 0.0)
    if patch:
        ab(S, f"patch{d}", d, 0.68, 1.1, -WT - 0.05, -WT - 0.0, (1.0 if not dmg else 0.45), 1.45 if not dmg else 0.9, "house", 0.0)
    if dmg:
        ab(S, f"hole{d}", d, 0.75, 1.0, -WT - 0.04, -WT, 0.5, 0.9, "char", 0.0, nocast=True)
    detail(S, d, dmg)


def finished(S, arms, dmg):
    straight = arms in ("EW", "NS")
    if not straight:
        pillar(S, dmg)
    else:
        pass
    for d in arms:
        arm(S, d, dmg, patch=(d in ("E", "S")) and (straight or len(arms) < 3))
    if not arms:
        S.bb("pad", -0.9, 0.9, -0.9, 0.9, 0.0, 0.1, "dust", 0.02)


def build_site(S, i):
    if i == 0:
        S.bb("trench", -HX, HX, -0.5, 0.5, 0.0, 0.05, "dust2", 0.0)
        S.bb("mound", -HX, 0.3, 0.7, 1.2, 0.0, 0.4, "dust", 0.15)
        for sx in (-1.6, 1.6):
            K.post(S, "stake", sx, -0.9, 0.0, 0.8, 0.1, "wood")
            K.post(S, "stake", sx, 0.9, 0.0, 0.8, 0.1, "wood")
        S.bb("line", -1.6, 1.6, -0.92, -0.88, 0.7, 0.74, "rope", 0.0, nocast=True)
    elif i == 1:
        for k in range(3):
            S.cyl(f"tyre{k}", 0.55, 0.34, (-1.1 + k * 1.1, 0.0, 0.17), "rubber", verts=14, stretch=False)
        for u in (-0.9, 0.0, 0.9):
            K.post(S, "wpost", u, 0.0, 0.0, 2.0, 0.14, "wood")
        S.bb("planks", -1.2, 0.2, 0.7, 1.1, 0.0, 0.25, "wood", 0.0)
    else:
        for u in (-1.15, -0.62, 0.62, 1.15):
            K.post(S, "wpost", u, 0.0, 0.0, 2.3, 0.14, "wood")
        S.skin.update(K.PRIMER_SKIN)
        for k, (u0, u1, m, ms, h, hs) in enumerate(PANELS):
            S.bb(f"panel{k}", u0, u1, -WT, WT, 0.0, h, m, 0.0)
            if i == 2:
                S.hide = lambda nm: nm == "panel2"
        S.bb("pile", -1.4, -0.7, 0.7, 1.3, 0.0, 0.4, "ash2", 0.1)


def build(S, state, i, n):
    building = state == "build"
    if building and i < 3:
        build_site(S, i)
        return
    dmg = state.endswith("damaged")
    base = state[:-len("_damaged")] if state.endswith("_damaged") else state
    arms = "EW" if (building or base in ("idle", "damaged")) else VARIANTS[base]
    if dmg:
        S.skin.update(SOOT_SKIN)
    finished(S, arms, dmg)
