"""Authority Power Plant: a compact atomic plant (2x2 cells). Original design, no franchise references.

Squat containment dome (house band), control annex with glowing amber tube windows, a tapered cooling stack that puffs,
and a fenced transformer yard with insulators.  Session 43 art pass: panel seams, rivets, pipe runs, vents, rails, a stack
catwalk, ground clutter, and an 8-frame idle loop (spinning cooling-fan turbine, pulsing reactor glow band, blinking beacons,
window chase, transformer arcs, three-puff smoke loop).  Damaged frames share the same loop.  Damage is structural only (D-069): cracked dome with a breach, snapped stack, a toppled transformer,
caved annex roof, dark windows; fire / smoke are FX anchors.
Contract: see auth_refinery.py; build-site helpers come from auth_conyard.py.
Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-3.4, 3.4].
"""
import importlib.util
import math
import os

_HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structlib_auth_conyard", os.path.join(_HERE, "auth_conyard.py"))
L = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(L)
R = L.R

ID = "auth_power"
TITLE = "Authority Power Plant"
FOOTPRINT = (2, 2)
CANVAS = (128, 176)
ORIGIN = (0, 48)
DOCK = (0, 0)           # no dock (not a refinery)
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True        # D-072: roof house2 panels pinned to mid ramp shades (structpost)
# 8-frame idle/damaged loops.  The renderer runs building idle loops 4x slower than ticks_per_frame (SpriteStates.BuildingIdleSlowdown),
# so tpf 1 = 4 ticks = 0.2 s per frame, a 1.6 s loop.  (Was 4 frames at tpf 5 = 1 s per frame.)
STATES = [("idle", 8, 1), ("damaged", 8, 1), ("build", 8, 5)]
MATERIALS = dict(L.MATERIALS)
MATERIALS.update({       # reactor-glow pulse levels, dim to bright (the fixed palette snaps these to yellows / rust)
    "glow0": ("#4A2E12", dict(rough=0.5)),
    "glow1": ("#8A5A22", dict(rough=0.4, emit="#8A5A22")),
    "glow2": ("#FFB43A", dict(rough=0.3, emit="#E8A030")),
    "glow3": ("#FFE9A0", dict(rough=0.3, emit="#FFE9A0")),
})
SOOT_SKIN = L.SOOT_SKIN
PRIMER_SKIN = L.PRIMER_SKIN

DOME = (-1.4, 1.5)
DOME_R = 1.35
STACK = (2.1, 1.8)
ANNEX = (-3.0, 0.3, -0.9, 0.5)
ANNEX_H = 1.9
YARD = (0.9, 3.0, -3.0, -0.4)


def transformer(S, name, x, y, dmg=False, spark=False):
    S.bb(f"{name}", x - 0.5, x + 0.5, y - 0.4, y + 0.4, 0.2, 1.0, "hull2", 0.04)
    S.bb(f"{name}top", x - 0.55, x + 0.55, y - 0.45, y + 0.45, 1.0, 1.12, "steel", 0.03)
    for k in range(5):
        S.bb(f"{name}fin", x - 0.42 + k * 0.21, x - 0.34 + k * 0.21, y - 0.43, y - 0.38, 0.28, 0.92, "dark", 0.0, nocast=True)
    S.bb(f"{name}plate", x - 0.3, x + 0.3, y - 0.43, y - 0.4, 0.62, 0.88, "house", 0.0, nocast=True)
    for k, ix in enumerate((-0.32, 0.0, 0.32)):
        if dmg and k == 1:
            S.cyl(f"{name}ins", 0.08, 0.22, (x + ix, y, 1.23), "white", verts=8)
            continue
        S.cyl(f"{name}ins", 0.08, 0.55, (x + ix, y, 1.38), "white", verts=8)
        for dz in (1.25, 1.42):
            S.cyl(f"{name}insd", 0.15, 0.05, (x + ix, y, dz), "white", verts=10)
        S.cyl(f"{name}inst", 0.1, 0.07, (x + ix, y, 1.68), "steel", verts=8)
        if spark and k == 1:
            S.bb("sparkcore", x + ix - 0.1, x + ix + 0.1, y - 0.1, y + 0.1, 1.76, 1.96, "spark", 0.0, nocast=True)
            S.bb("sparkarm", x + ix + 0.12, x + ix + 0.34, y - 0.04, y + 0.04, 1.86, 1.92, "spark", 0.0, nocast=True)
            S.bb("sparkarm", x + ix - 0.3, x + ix - 0.1, y - 0.04, y + 0.04, 1.78, 1.84, "spark", 0.0, nocast=True)


def fan(S, name, cx, cy, z, r, i, spin, step=30.0):
    """Three-blade rotor at (cx, cy) game-ground units, hub height z.  Blade k sits at angle i*step + 120k; 120-degree symmetry
    means step 30 gives a clean 4-pose forward spin (less than half the symmetry period, so no wagon-wheel reversal)."""
    YS = S.YS
    S.cyl(f"{name}hub", r * 0.24, 0.1, (cx, cy, z), "steel", verts=10, nocast=True)
    a0 = i * step if spin else 20.0
    for k in range(3):
        a = a0 + 120.0 * k
        ar = math.radians(a)
        m = r * 0.56
        S.box(f"{name}blade{k}", (r * 0.9, r * 0.42, 0.035), (cx + m * math.cos(ar), cy * YS + m * math.sin(ar), z + 0.02 + 0.012 * k),
              "white", 0.0, rot=(0, 0, a), stretch=False, nocast=True)


def cooling_fan(S, x, y, i, dmg, idle):
    """Ground-standing ducted cooling-fan unit: plinth, shroud, grille rim, spinning rotor, stencil, cable run."""
    S.bb("cfplinth", x - 0.75, x + 0.75, y - 0.62, y + 0.62, 0.14, 0.26, "concrete2", 0.02)
    S.cyl("cfshroud", 0.56, 0.34, (x, y, 0.43), "hull2", verts=20)
    S.cyl("cfrim", 0.62, 0.07, (x, y, 0.62), "steel", verts=20)
    S.cyl("cfwell", 0.52, 0.04, (x, y, 0.6), "dark", verts=20, nocast=True)
    fan(S, "cf", x, y, 0.66, 0.5, i, idle)
    S.bb("cfbarX", x - 0.6, x + 0.6, y - 0.025 * S.YS, y + 0.025 * S.YS, 0.74, 0.77, "steel", 0.0, nocast=True)
    for dx_ in (-0.5, 0.5):
        S.cyl("cfbolt", 0.05, 0.1, (x + dx_ * 1.12, y - 0.38, 0.32), "steel", verts=6, nocast=True)
    S.bb("cfstripe", x - 0.3, x + 0.3, y - 0.52, y - 0.5, 0.17, 0.23, "yellow", 0.0, nocast=True)
    S.bb("cfbox", x + 0.85, x + 1.2, y - 0.2, y + 0.2, 0.14, 0.62, "hull", 0.02)       # motor control box
    S.bb("cfboxlt", x + 0.91, x + 0.99, y - 0.22, y - 0.2, 0.42, 0.5, "amber" if (idle and not dmg and i % 4 < 2) else "amber_dim", 0.0, nocast=True)
    S.bb("cfcable", x + 0.05, x + 0.1, y - 0.95, y - 0.5, 0.14, 0.2, "dark", 0.0, nocast=True)


def rail_arc(S, cx, cy, r, z0, z1, a0, a1, n, tag):
    """Railing on a circle (game units): posts plus a top and mid rail made of short straight boxes (a0..a1 in degrees)."""
    YS = S.YS
    pts = []
    for k in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * k / n)
        pts.append((cx + r * math.cos(a), cy * YS + r * math.sin(a)))
    for (px, py) in pts:
        S.box(f"{tag}post", (0.05, 0.05, z1 - z0), (px, py, (z0 + z1) / 2), "steel", 0.0, stretch=False, nocast=True)
    for (ax, ay), (bx, by) in zip(pts[:-1], pts[1:]):
        ln = math.hypot(bx - ax, by - ay)
        yaw = math.degrees(math.atan2(by - ay, bx - ax))
        for zz in (z1 - 0.03, (z0 + z1) / 2):
            S.box(f"{tag}rail", (ln + 0.02, 0.035, 0.035), ((ax + bx) / 2, (ay + by) / 2, zz), "steel", 0.0, rot=(0, 0, yaw), stretch=False, nocast=True)


def rail_line(S, x0, x1, y, z0, z1, n, tag):
    for k in range(n + 1):
        x = x0 + (x1 - x0) * k / n
        S.bb(f"{tag}post", x - 0.025, x + 0.025, y - 0.025, y + 0.025, z0, z1, "steel", 0.0, nocast=True)
    S.bb(f"{tag}rail", x0, x1, y - 0.02, y + 0.02, z1 - 0.05, z1 - 0.01, "steel", 0.0, nocast=True)
    S.bb(f"{tag}rail", x0, x1, y - 0.02, y + 0.02, (z0 + z1) / 2, (z0 + z1) / 2 + 0.04, "steel", 0.0, nocast=True)


def finished(S, i, dmg, mode):
    idle = mode == "idle"
    if dmg:
        S.skin.update(SOOT_SKIN)
    YS = S.YS
    dx, dy = DOME
    sx, sy = STACK
    # ---- pad, hazard stripes, yard slab
    S.bb("pad", -3.3, 3.3, -3.3, 3.3, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -3.3, 3.3, -3.3, -3.2, 0.14, 0.2, "concrete2", 0.0)
    for k in range(12):
        S.bb(f"haz{k}", -3.2 + k * 0.5, -3.2 + (k + 1) * 0.5, -3.18, -2.78, 0.14, 0.2, "yellow" if k % 2 == 0 else "dark", 0.0)
    S.bb("yard", YARD[0], YARD[1], YARD[2] + 0.25, YARD[3], 0.14, 0.2, "concrete2", 0.0)
    # ---- control annex
    ax0, ax1, ay0, ay1 = ANNEX
    S.bb("annex", ax0, ax1, ay0, ay1, 0.14, ANNEX_H, "concrete", 0.04)
    S.bb("aplinth", ax0 - 0.08, ax1 + 0.08, ay0 - 0.08, ay1 + 0.08, 0.14, 0.4, "concrete2", 0.03)
    yw = ay0 - 0.02
    if dmg:
        S.bb("aroofE", -1.2, ax1 + 0.15, ay0 - 0.12, ay1 + 0.1, ANNEX_H, ANNEX_H + 0.2, "roof", 0.04)
        S.bb("aroofW", ax0 - 0.15, -1.2, ay0 - 0.12, ay1 + 0.1, ANNEX_H - 0.6, ANNEX_H - 0.4, "roof", 0.04, rot=(0, 16, 4))
        S.bb("apit", ax0 + 0.3, -1.3, ay0 + 0.35, ay1 - 0.05, ANNEX_H - 0.3, ANNEX_H, "dark", 0.0)
        S.bb("awallj", ax0, ax0 + 0.5, ay0, ay0 + 0.25, ANNEX_H, ANNEX_H + 0.35, "concrete", 0.02)
        S.bb("awallj", -1.7, -1.35, ay0, ay0 + 0.25, ANNEX_H, ANNEX_H + 0.2, "concrete", 0.02)
        S.bb("arubble", -3.2, -2.0, -1.5, -0.8, 0.14, 0.45, "concrete2", 0.05, rot=(0, 0, 18))
        S.bb("arubble", -2.2, -1.5, -1.3, -0.8, 0.14, 0.7, "concrete", 0.05, rot=(0, 0, -22))
    else:
        S.bb("aroof", ax0 - 0.15, ax1 + 0.15, ay0 - 0.12, ay1 + 0.1, ANNEX_H, ANNEX_H + 0.2, "roof", 0.04)
        S.bb("aparapet", ax0 - 0.15, ax1 + 0.15, ay0 - 0.12, ay0 - 0.02, ANNEX_H + 0.2, ANNEX_H + 0.38, "concrete2", 0.02)
    hx0 = -1.15 if dmg else -2.85
    S.bb("aroofhouse", hx0, 0.1, ay0 + 0.2, ay1 - 0.15, ANNEX_H + 0.2, ANNEX_H + 0.24, "house2", 0.0)
    # tube windows: a row of tall amber slots, pulsing along the row
    S.bb("winframe", ax0 + 0.2, ax1 - 0.2, yw - 0.06, yw + 0.04, 0.95, 1.7, "dark", 0.0)
    for k in range(5):
        x = ax0 + 0.42 + k * 0.58
        if dmg:
            m = "amber_dim" if k in (1, 3) else "glass"
        elif idle:
            m = "amber" if ((i - 1.6 * k) % 8) < 3.6 else "amber_dim"
        else:
            m = "amber"
        S.bb(f"awin{k}", x, x + 0.4, yw - 0.1, yw - 0.04, 1.02, 1.62, m, 0.0)
    S.bb("apanel", ax0 + 0.2, ax1 - 0.2, yw - 0.12, yw - 0.06, 0.45, 0.85, "house", 0.0)
    S.bb("adoor", ax1 - 0.05, ax1 + 0.0, ay0 + 0.1, ay0 + 0.7, 0.14, 0.9, "dark", 0.0)
    # panel seams / pilasters / cornice / conduit / meters (never over the house plate)
    for xx in (ax0 + 0.02, ax1 - 0.2):
        S.bb("apil", xx, xx + 0.18, yw - 0.08, yw + 0.02, 0.4, ANNEX_H, "concrete2", 0.0)
    S.bb("acornice", ax0 - 0.04, ax1 + 0.04, yw - 0.1, yw, ANNEX_H - 0.16, ANNEX_H - 0.02, "concrete2", 0.0)
    S.bb("aseam", ax0 + 0.2, ax1 - 0.2, yw - 0.07, yw - 0.04, 0.88, 0.92, "crack", 0.0, nocast=True)
    for k in range(1, 6):
        S.bb(f"aseamv{k}", ax0 + 0.2 + k * 0.5, ax0 + 0.23 + k * 0.5, yw - 0.07, yw - 0.04, 0.4, 0.45, "crack", 0.0, nocast=True)
    S.cyl("aconduit", 0.045, ax1 - ax0 - 0.5, ((ax0 + ax1) / 2, yw - 0.14, 0.38), "pipeblue", axis="X", verts=8, nocast=True)
    for k in range(5):
        S.bb(f"aclamp{k}", ax0 + 0.5 + k * 0.62, ax0 + 0.56 + k * 0.62, yw - 0.18, yw - 0.1, 0.34, 0.43, "steel", 0.0, nocast=True)
    S.bb("agauge", ax1 - 0.62, ax1 - 0.3, yw - 0.1, yw - 0.06, 1.74, 1.88, "gauge", 0.0, nocast=True)
    S.bb("agaugeN", ax1 - 0.52, ax1 - 0.48, yw - 0.12, yw - 0.1, 1.76, 1.86, "dark", 0.0, nocast=True)
    if not dmg:
        rail_line(S, ax0 - 0.1, ax1 + 0.1, ay0 - 0.1, ANNEX_H + 0.38, ANNEX_H + 0.62, 12, "arail")
        for k, vx in enumerate((ax0 + 0.2, ax0 + 0.8, ax1 - 0.4)):
            S.bb(f"avent{k}", vx, vx + 0.34, ay1 - 0.12, ay1 + 0.04, ANNEX_H + 0.2, ANNEX_H + 0.46, "hull2", 0.01)
            for q in range(3):
                S.bb(f"aventl{k}{q}", vx + 0.04, vx + 0.3, ay1 - 0.14, ay1 - 0.11, ANNEX_H + 0.25 + q * 0.07, ANNEX_H + 0.28 + q * 0.07, "dark", 0.0, nocast=True)
    else:
        rail_line(S, -1.2, ax1 + 0.1, ay0 - 0.1, ANNEX_H + 0.38, ANNEX_H + 0.6, 4, "arail")
    for k in range(4):
        S.bb(f"sten{k}", ax0 + 0.4 + k * 0.4, ax0 + 0.58 + k * 0.4, yw - 0.1, yw - 0.06, 0.2, 0.36, "white", 0.0, nocast=True)
    # ---- containment dome: plinth, drum with house band, dome, cap
    S.cyl("dplinth", DOME_R + 0.2, 0.3, (dx, dy, 0.29), "concrete2", verts=28)
    S.cyl("drum", DOME_R, 1.0, (dx, dy, 0.94), "hull", verts=28)
    S.cyl("hband", DOME_R + 0.03, 0.42, (dx, dy, 0.8), "house", verts=28)
    S.cyl("hring", DOME_R + 0.06, 0.08, (dx, dy, 1.0 + 0.4), "steel", verts=28)
    S.sphere("dome", DOME_R + 0.02, (dx, dy, 1.44), "hull", nocast=False, squash=0.78)
    S.cyl("dcap", 0.3, 0.25, (dx, dy, 2.42), "steel", verts=12)
    lit = idle and i % 4 < 2
    S.cyl("dlamp", 0.1, 0.14, (dx, dy, 2.62), "lamp" if (lit and not dmg) else "dark", verts=8)
    # reactor glow band between the house band and the ring: pulses over the 8-frame loop (dim and ragged when damaged)
    lvl = round(1.5 + 1.5 * math.sin(2 * math.pi * i / 8)) if idle else 1
    if dmg:
        lvl = (0, 1, 0, 2, 0, 1, 1, 0)[i % 8] if idle else 0
    S.cyl("dglow", 1.1, 0.1, (dx, dy, 2.08), f"glow{lvl}", verts=28, nocast=True)
    S.cyl("dglow2", 0.62, 0.08, (dx, dy, 2.36), f"glow{lvl}", verts=20, nocast=True)
    # shell seams (thin dark strips) and rivet rows on the south half of the drum; stay clear of the house band (z 0.59..1.01)
    for k in range(13):
        a = math.radians(195 + k * 12.5)
        if k % 2 == 0:
            S.box(f"dseam{k}", (0.05, 0.05, 0.3), (dx + (DOME_R + 0.01) * math.cos(a), (dy * YS + (DOME_R + 0.01) * math.sin(a)), 1.2),
                  "crack", 0.0, rot=(0, 0, math.degrees(a) - 90), stretch=False, nocast=True)
        S.cyl(f"drivet{k}", 0.04, 0.05, (dx + (DOME_R + 0.05) * math.cos(a), dy + (DOME_R + 0.05) * math.sin(a) / YS, 1.12), "steel", verts=6, nocast=True)
        S.cyl(f"drivetb{k}", 0.04, 0.05, (dx + (DOME_R + 0.03) * math.cos(a), dy + (DOME_R + 0.03) * math.sin(a) / YS, 0.5), "steel", verts=6, nocast=True)
    if dmg:
        # impact breach on the south-east shoulder: dark hole ringed by torn shell, a chunk lying below
        S.bb("breach", dx + 0.2, dx + 1.0, dy - 1.0, dy - 0.7, 1.15, 1.75, "dark", 0.0, nocast=True)
        S.bb("brim1", dx + 0.1, dx + 1.1, dy - 1.06, dy - 0.74, 1.7, 1.9, "concrete2", 0.0, rot=(0, 0, 8))
        S.bb("brim2", dx + 1.0, dx + 1.3, dy - 1.0, dy - 0.6, 1.1, 1.7, "concrete2", 0.0)
        S.bb("brim3", dx + 0.1, dx + 0.4, dy - 1.0, dy - 0.7, 1.1, 1.45, "concrete2", 0.0)
        S.bb("chunk", dx + 0.9, dx + 1.7, dy - 2.0, dy - 1.4, 0.14, 0.55, "hull", 0.05, rot=(0, 0, 25))
        S.bb("chunk2", dx + 0.2, dx + 0.7, dy - 1.9, dy - 1.6, 0.14, 0.4, "hull2", 0.05, rot=(0, 0, -15))
        for k, (cx, cz, h) in enumerate(((-0.8, 1.2, 0.9), (-0.3, 1.5, 0.6), (0.4, 1.9, 0.5))):
            S.bb(f"crack{k}", dx + cx, dx + cx + 0.06, dy - 1.25, dy - 1.19, cz, cz + h, "crack", 0.0, nocast=True, rot=(0, 0, 0))
        S.bb("crackh", dx - 1.0, dx - 0.2, dy - 1.2, dy - 1.14, 0.7, 0.76, "crack", 0.0, nocast=True)
    # ---- cooling stack
    if dmg:
        S.cone("stack", 0.85, 0.7, 2.1, (sx, sy, 0.14 + 1.05), "concrete", verts=20, nocast=False)
        for nm, dxx, dzz, rot in (("jagA", -0.45, 2.3, (0, 25, 0)), ("jagB", 0.4, 2.35, (0, -20, 12)), ("jagC", 0.0, 2.2, (15, 0, 0))):
            S.box(nm, (0.4, 0.4, 0.55), (sx + dxx, sy, dzz), "concrete2", 0.0, rot=rot, stretch=False)
        S.cyl("sdark", 0.5, 0.06, (sx, sy, 2.26), "dark", verts=12)
        # the top section snapped and lies across the yard edge to the south-east
        tl = math.radians(72)
        piece = S.cone("fallen", 0.7, 0.55, 1.6, (0, 0, 0), "concrete", verts=20, nocast=False)
        piece.location = (sx + 1.0, (sy - 1.6) * YS, 0.62)
        piece.rotation_euler = (math.radians(-62), 0, math.radians(22))
        S.bb("srubble", sx - 0.6, sx + 0.6, sy - 1.2, sy - 0.7, 0.14, 0.4, "concrete2", 0.05, rot=(0, 0, 14))
        S.bb("scrack", sx - 0.5, sx - 0.44, sy - 0.8, sy - 0.76, 0.5, 1.9, "crack", 0.0, nocast=True)
    else:
        S.cone("stack", 0.85, 0.6, 3.4, (sx, sy, 0.14 + 1.7), "concrete", verts=20, nocast=False)
        for zz, rr in ((0.7, 0.8), (1.5, 0.74), (2.3, 0.68)):
            S.cyl(f"sband{zz}", rr + 0.05, 0.12, (sx, sy, zz), "rust", verts=20)
        S.cyl("slip", 0.66, 0.14, (sx, sy, 3.5), "concrete2", verts=20)
        S.cyl("sdark", 0.5, 0.06, (sx, sy, 3.57), "dark", verts=16)
        # service catwalk with railing around the upper stack, rivet studs on the bands
        S.cyl("scat", 0.86, 0.06, (sx, sy, 2.75), "steel", verts=20)
        rail_arc(S, sx, sy, 0.86, 2.78, 3.2, 175, 365, 12, "srail")
        for k in range(10):
            a = math.radians(190 + k * 17)
            for zz, rr in ((0.7, 0.8), (1.5, 0.74), (2.3, 0.68)):
                S.cyl(f"sstud{k}", 0.035, 0.05, (sx + (rr + 0.07) * math.cos(a), sy + (rr + 0.07) * math.sin(a) / S.YS, zz), "steel", verts=6, nocast=True)
        S.cyl("sbeacon", 0.09, 0.14, (sx - 0.5, sy - 0.28, 3.68), "red" if (idle and i % 4 >= 2) else "dark", verts=8, nocast=True)
        if idle:
            for k in range(2):     # two staggered puffs rising and drifting east, one loop per 8 frames
                ph = ((i / 8) + k / 2) % 1
                r = 0.08 + 0.32 * math.sin(math.pi * ph) ** 0.7
                S.sphere(f"puff{k}", r, (sx + 0.1 + ph * 0.6, sy, 3.75 + ph * 1.25 + r * 0.3), "smoke" if k == 0 else "smoke2", squash=0.85)
    S.bb("sladder", sx - 0.86, sx - 0.8, sy - 0.12, sy + 0.12, 0.14, 1.8 if not dmg else 1.2, "dark", 0.0, nocast=True)
    S.bb("sbase", sx - 1.0, sx + 1.0, sy - 1.0, sy + 1.0, 0.14, 0.3, "concrete2", 0.03)
    # ---- pipes dome -> stack, dome -> annex
    S.cyl("pipeH", 0.15, 2.0, (0.55, dy, 1.0), "steel", axis="X", verts=12)
    for k, xx in enumerate((-0.05, 1.1)):
        S.cyl(f"fl{k}", 0.22, 0.1, (xx, dy, 1.0), "rust", axis="X", verts=12)
    S.cyl("pipeV", 0.15, 0.9, (-0.05 + 0.0, dy, 0.6), "steel", verts=10)
    for k, xx in enumerate((0.1, 0.55, 1.0)):
        S.bb(f"pbrk{k}", xx - 0.04, xx + 0.04, dy - 0.04, dy + 0.04, 0.14, 0.86, "steel", 0.0, nocast=True)
    S.cyl("pvalve", 0.2, 0.12, (0.55, dy, 1.2), "rust", verts=10, nocast=True)
    S.cyl("pwheel", 0.14, 0.04, (0.55, dy, 1.3), "steel", verts=10, nocast=True)
    S.cyl("pipeN", 0.09, 1.5, (0.2, 0.55, 0.7), "pipeblue", axis="X", verts=8)
    S.cyl("pipeN2", 0.09, 1.0, (-0.1, 0.1, 0.45), "steel", verts=8)
    # ---- ground: spinning cooling fan unit, cable runs, clutter at the footprint edge
    cooling_fan(S, -0.95, -2.0, i, dmg, idle)
    S.bb("cableA", -3.0, -1.7, -2.33, -2.28, 0.14, 0.18, "dark", 0.0, nocast=True)
    S.bb("cableB", -1.55, -0.4, -2.33, -2.28, 0.14, 0.18, "dark", 0.0, nocast=True)
    S.cyl("reel", 0.34, 0.22, (-2.45, -2.05, 0.46), "timber", axis="X", verts=14)
    S.cyl("reelc", 0.08, 0.3, (-2.45, -2.05, 0.46), "steel", axis="X", verts=8, nocast=True)
    S.cyl("drumA", 0.24, 0.55, (-2.15, -2.8, 0.42), "rust", verts=12)
    S.cyl("drumB", 0.24, 0.55, (-1.65, -2.55, 0.42), "steel", verts=12)
    S.cyl("drumBh", 0.25, 0.05, (-1.65, -2.55, 0.62), "yellow", verts=12, nocast=True)
    S.bb("jbox", 0.1, 0.45, -1.7, -1.35, 0.14, 0.55, "hull", 0.02)
    S.bb("jboxl", 0.14, 0.2, -1.72, -1.7, 0.4, 0.48, "amber" if (idle and not dmg and i % 4 < 2) else "amber_dim", 0.0, nocast=True)
    for k in range(6):
        S.cyl(f"bol{k}", 0.065, 0.46, (3.18, 0.35 + k * 0.45, 0.37), "yellow", verts=6)
    for k in range(4):
        S.cyl(f"bolw{k}", 0.065, 0.46, (-3.2, -0.95 - k * 0.45, 0.37), "yellow", verts=6)
    for k in range(3):
        S.bb(f"curb{k}", 2.95 - k * 0.0, 3.3, 2.0 + k * 0.4 - 0.01, 2.0 + k * 0.4 + 0.01, 0.14, 0.17, "crack", 0.0, nocast=True)
    # ---- transformer yard: fence, two transformers, busbar
    fx0, fx1, fy0, fy1 = 0.7, 3.2, -3.05, -0.3
    for xx in (fx0, 1.5, 2.3, fx1):
        if dmg and xx in (1.5, 2.3):
            continue
        S.bb("fpost", xx - 0.04, xx + 0.04, fy0 - 0.04, fy0 + 0.04, 0.2, 1.0, "steel", 0.0)
    for yy in (-1.95, fy1):
        S.bb("fpost", fx1 - 0.04, fx1 + 0.04, yy - 0.04, yy + 0.04, 0.2, 1.0, "steel", 0.0)
    if dmg:
        S.bb("frail", fx0, 1.2, fy0 - 0.02, fy0 + 0.02, 0.85, 0.89, "steel", 0.0, nocast=True)
        S.bb("frailfall", 1.3, 2.2, fy0 - 0.3, fy0 - 0.26, 0.2, 0.24, "steel", 0.0, nocast=True, rot=(0, 0, 8))
    else:
        for z0 in (0.55, 0.9):
            S.bb("frail", fx0, fx1, fy0 - 0.02, fy0 + 0.02, z0, z0 + 0.04, "steel", 0.0, nocast=True)
        S.bb("frailE", fx1 - 0.02, fx1 + 0.02, fy0, fy1, 0.9, 0.94, "steel", 0.0, nocast=True)
    transformer(S, "trA", 1.55, -1.3, dmg=dmg, spark=idle and i in (1, 2, 5) and not dmg)
    if dmg:
        # second transformer knocked over onto its side, insulators snapped off
        S.box("trB", (1.0, 0.8, 0.8), (2.55, -2.0 * YS, 0.62), "hull2", 0.04, rot=(0, 0, 22), stretch=False)
        S.box("trBtop", (1.1, 0.12, 0.12), (2.6, -2.45 * YS, 0.25), "steel", 0.0, rot=(0, 0, 22), stretch=False)
        for k in range(3):
            S.cyl(f"insF{k}", 0.08, 0.5, (2.9 + 0.1 * k, -2.5 - 0.3 * k, 0.28), "white", axis="X", verts=8)
    else:
        transformer(S, "trB", 2.55, -1.3, spark=idle and i in (0, 4, 5))
    if not dmg:
        L.beam(S, "bus", (1.55, -1.3, 1.62), (2.55, -1.3, 1.62), 0.05, "dark", nocast=True)
    # ---- side details: lamp mast, stencils
    S.cyl("lmast", 0.05, 2.2, (-3.0, -1.7, 1.2), "steel", verts=8)
    S.bb("lhead", -3.12, -2.88, -1.76, -1.64, 2.25, 2.4, "dark", 0.0)
    S.bb("lamp", -3.1, -2.9, -1.78, -1.76, 2.27, 2.38, "lamp" if (not dmg and idle) else "dark", 0.0, nocast=True)
    S.cyl("beacon", 0.1, 0.12, (-3.0, -1.7, 2.5), "red" if (idle and i % 4 < 2) else "dark", verts=8, nocast=True)
    if dmg:
        R.scorch(S, "scA", -1.5, yw - 0.16, 1.1, 1.5, 0.6)
        R.scorch(S, "scB", 1.3, -1.1, 0.23, 1.6, 1.2, 0.03, plane="T")
        R.scorch(S, "scC", dx + 0.5, dy - 1.3, 1.6, 1.0, 0.7)


def _a(x, y, z):
    return L.anchor(FOOTPRINT, ORIGIN, x, y, z)


FX = [
    dict(state="damaged", kind="fire_large", x=_a(-0.9, 0.0, 1.5)[0], y=_a(-0.9, 0.0, 1.5)[1], phase=0),
    dict(state="damaged", kind="fire_small", x=_a(-0.3, 0.5, 2.0)[0], y=_a(-0.3, 0.5, 2.0)[1], phase=4),
    dict(state="damaged", kind="fire_small", x=_a(2.6, -2.0, 0.6)[0], y=_a(2.6, -2.0, 0.6)[1], phase=2),
    dict(state="damaged", kind="fire_small", x=_a(2.1, 1.8, 2.3)[0], y=_a(2.1, 1.8, 2.3)[1], phase=6),
    dict(state="damaged", kind="smoke_plume", x=_a(-0.3, 0.5, 1.9)[0], y=_a(-0.3, 0.5, 1.9)[1], phase=0),
    dict(state="damaged", kind="smoke_plume", x=_a(2.1, 1.8, 2.3)[0], y=_a(2.1, 1.8, 2.3)[1], phase=5),
    dict(state="damaged", kind="smoke_plume", x=_a(2.6, -2.0, 0.8)[0], y=_a(2.6, -2.0, 0.8)[1], phase=3),
]


def _clutter(S, i):
    if i <= 1:
        S.bb("crate", 2.2, 3.0, -2.9, -2.1, 0.14, 0.8, "crate", 0.03)
        S.bb("crate", 2.35, 2.9, -2.8, -2.2, 0.8, 1.3, "crate", 0.03, rot=(0, 0, 12))
    if i <= 2:
        for z in (0.3, 0.65):
            S.cyl("lpipe", 0.14, 2.2, (1.5, -2.0, z), "steel", axis="X", verts=10)
        S.bb("rebarpile", -2.8, -0.8, -2.4, -2.2, 0.14, 0.3, "rebar", 0.0)
        S.cyl("barrel", 0.3, 0.7, (-3.0, -1.6, 0.5), "rust", verts=12)
    S.bb("planks", 0.8, 2.6, 2.7, 3.1, 0.14, 0.5, "timber", 0.0)
    S.bb("cabin", -3.2, -2.0, 2.5, 3.2, 0.14, 1.1, "primer", 0.03)
    S.bb("cabdoor", -2.9, -2.6, 2.45, 2.5, 0.14, 0.8, "dark", 0.0)


def _left(S):
    S.bb("planks", -2.8, -1.4, -2.9, -2.5, 0.14, 0.3, "timber", 0.0)
    S.cyl("lpipe", 0.14, 2.0, (1.4, -2.4, 0.3), "steel", axis="X", verts=10)
    S.bb("crate", 2.3, 3.0, -2.9, -2.2, 0.14, 0.8, "crate", 0.03)
    L.spark(S, -1.0, -0.9, 2.0, 3, 6)


SPEC = dict(
    pad=(-3.3, 3.3, -3.3, 3.3), env=(-3.1, 3.1, -1.0, 3.0), H=2.0, boxes=[(-3.0, 0.3, -0.9, 0.5)], rings=[(DOME[0], DOME[1], DOME_R), (STACK[0], STACK[1], 0.85)],
    clips=(1.0, 1.9), scaf=[(-2.9, -1.6, -2.1, -1.4), (-1.0, 0.2, -2.1, -1.4)],
    derrick=(3.0, 0.4, 4.0, (0.3, 1.3, 4.4)), sparks=[(-2.0, -0.9, 1.9), (0.3, 0.5, 1.9), (-1.4, 2.8, 2.0)],
    hide=("sten", "sparkcore", "sparkarm", "dlamp", "lamp", "beacon", "sbeacon", "dglow", "cfblade", "cfhub"), clutter=_clutter, left=_left)


def build(S, state, i, n):
    L.stages(S, state, i, SPEC, finished)
