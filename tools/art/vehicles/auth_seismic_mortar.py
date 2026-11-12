"""Authority seismic mortar for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Heavy tracked carriage (scraper track/hull family): low armoured hull with house flank panels, rear seismograph drum + geophone
spike cradle, front driver block.  The turret layer is a round shielded mortar mount carrying one short fat tube elevated high,
with a recoil sleeve, a bursting-charge breech and a stack of shell rounds on the left.  The tube points along the turret facing.
Layers: body (hull + whole-vehicle shadow) + turret (mount and tube).  House paint: hull flanks, rear drum band, mount collar ring.
"""
import math

ID = "auth_seismic_mortar"
TITLE = "Authority seismic mortar"
CANVAS = 128
TARGET = (0.0, 0.0, 1.4)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT = (0.0, -0.2, 1.55)     # = top of the mount collar
MOUNT = dict(deck_z=1.4, r_lip=0.72, r_well=0.0, r_shadow=0.85, shadow_z=1.5, soft_r=0.8, lip="hull2")


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    # ---- tracks
    for s, side in ((-1, "L"), (1, "R")):
        x = s * 1.1
        box("track" + side, (0.66, 3.2, 0.78), (x, -0.05, 0.42), "track", 0.14)
        cyl("idlF" + side, 0.42, 0.66, (x, 1.5, 0.44), "track", "X")
        cyl("idlR" + side, 0.42, 0.66, (x, -1.6, 0.44), "track", "X")
        for i in range(6):
            y = -1.35 + i * 0.54
            cyl(f"wh{side}{i}", 0.28, 0.1, (x + s * 0.34, y, 0.4), "steel", "X", 14)
            cyl(f"hb{side}{i}", 0.1, 0.08, (x + s * 0.4, y, 0.4), "rust", "X", 8)
        box("skirt" + side, (0.08, 3.2, 0.32), (x + s * 0.36, -0.05, 0.88), "hull2", 0.03)
    # ---- hull
    box("hull", (2.0, 3.3, 0.6), (0, -0.05, 0.95), "hull", 0.1)
    box("deck", (2.0, 3.3, 0.1), (0, -0.05, 1.28), "hull2", 0.04)
    box("nose", (2.0, 0.5, 0.45), (0, 1.62, 0.92), "hull2", 0.07)
    box("rearplate", (2.0, 0.1, 0.55), (0, -1.7, 0.95), "hull2", 0.03)
    for s in (-1, 1):
        box(f"flank{s}", (0.06, 2.4, 0.4), (s * 1.02, -0.1, 0.98), "house", 0.0)
        for i in range(3):
            box(f"rib{s}{i}", (0.07, 0.07, 0.5), (s * 1.03, -1.0 + i * 0.9, 1.0), "dark", 0.0)
    # ---- driver block, front left, with vision slit
    box("drv", (0.8, 0.8, 0.45), (-0.55, 1.15, 1.55), "hull", 0.07)
    box("drvroof", (0.86, 0.86, 0.06), (-0.55, 1.15, 1.8), "hull2", 0.03)
    box("drvslit", (0.6, 0.05, 0.12), (-0.55, 1.57, 1.62), "glass", 0.0)
    cyl("drvhatch", 0.2, 0.06, (-0.55, 1.05, 1.86), "steel", "Z", 10)
    for s in (-1, 1):
        cyl(f"lamp{s}", 0.08, 0.06, (s * 0.78, 1.88, 0.9), "lens", "Y", 10)
    cyl("beacon", 0.08, 0.14, (-0.8, 1.45, 1.88), "yellow", "Z", 8)
    # ---- right-front: shell rack under a lid
    box("rack", (0.7, 0.9, 0.3), (0.7, 1.05, 1.48), "hull2", 0.05)
    for i in range(3):
        cyl(f"rnd{i}", 0.09, 0.8, (0.52 + i * 0.18, 1.05, 1.7), "light", "Y", 8)
        cyl(f"rndtip{i}", 0.07, 0.06, (0.52 + i * 0.18, 1.5, 1.7), "yellow", "Y", 8)
    # ---- rear: seismograph drum and geophone cradle
    box("drumcradle", (1.5, 0.9, 0.12), (0, -1.15, 1.36), "hull2", 0.04)
    cyl("drum", 0.38, 1.3, (0, -1.15, 1.76), "light", "X", 16)
    cyl("drumband", 0.4, 0.42, (0, -1.15, 1.76), "house", "X", 16)
    for s in (-1, 1):
        cyl(f"drumcap{s}", 0.3, 0.08, (s * 0.69, -1.15, 1.76), "steel", "X", 12)
        box(f"drumleg{s}", (0.12, 0.7, 0.3), (s * 0.55, -1.15, 1.44), "hull2", 0.02)
    cyl("stylus", 0.03, 0.5, (0.0, -1.15, 2.2), "dark", "Z", 6)
    # geophone spikes folded on the tail (yellow-tipped), plus tow shackle
    for i, x in enumerate((-0.7, -0.35, 0.35, 0.7)):
        cyl(f"spike{i}", 0.06, 0.55, (x, -1.88, 1.0), "steel", "Y", 6)
        S.cone(f"spiketip{i}", 0.06, 0.01, 0.2, (x, -2.2, 1.0), "yellow", rot=(90, 0, 0), verts=6)
    # ---- mount collar (hull side, the turret seats on it)
    S.mount(PIVOT, **MOUNT)
    # ---- turret layer: shielded mortar mount
    S.begin("turret", PIVOT)
    px, py, pz = PIVOT
    cyl("ring", 0.68, 0.1, (px, py, pz + 0.05), "house", "Z", 18)
    cyl("base", 0.6, 0.3, (px, py, pz + 0.25), "hull", "Z", 18)
    box("cheekL", (0.14, 1.0, 0.7), (px - 0.4, py + 0.1, pz + 0.55), "hull2", 0.04)
    box("cheekR", (0.14, 1.0, 0.7), (px + 0.4, py + 0.1, pz + 0.55), "hull2", 0.04)
    box("cheekLH", (0.16, 0.6, 0.3), (px - 0.4, py + 0.05, pz + 0.65), "house", 0.0)
    box("cheekRH", (0.16, 0.6, 0.3), (px + 0.4, py + 0.05, pz + 0.65), "house", 0.0)
    box("shield", (1.0, 0.1, 0.42), (px, py + 0.55, pz + 0.7), "hull2", 0.04, rot=(-18, 0, 0))
    cyl("trunnion", 0.1, 1.0, (px, py + 0.1, pz + 0.7), "steel", "X", 8)
    # tube elevated ~62 deg: base at the trunnion, running forward and up
    ang = 62.0
    a = math.radians(ang)
    ln = 1.5
    cx, cy_, cz = px, py + 0.1 + (ln / 2) * math.cos(a) - 0.1, pz + 0.7 + (ln / 2) * math.sin(a) - 0.1
    cyl("tube", 0.2, ln, (cx, cy_, cz), "steel", rot=(ang - 90, 0, 0), verts=14)
    cyl("sleeve", 0.26, 0.5, (cx, cy_ - 0.1, cz - 0.15), "hull", rot=(ang - 90, 0, 0), verts=14)
    ex, ey, ez = cx, cy_ + (ln / 2) * math.cos(a), cz + (ln / 2) * math.sin(a)
    cyl("muzzle", 0.26, 0.12, (ex, ey, ez), "yellow", rot=(ang - 90, 0, 0), verts=14)
    cyl("bore", 0.13, 0.04, (ex, ey, ez + 0.02), "dark", rot=(ang - 90, 0, 0), verts=10)
    sph("breech", 0.24, (cx, cy_ - (ln / 2) * math.cos(a), cz - (ln / 2) * math.sin(a)), "hull2")
    box("hatch", (0.34, 0.34, 0.06), (px - 0.05, py - 0.35, pz + 0.55), "steel", 0.02)
