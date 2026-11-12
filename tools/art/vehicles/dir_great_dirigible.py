"""Directorate Great Dirigible (heavy radiation bomber airship, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

The big, mean relative of dir_dirigible: a vast bone-canvas envelope (oxide bands, darker patched panels, an armour-plate belly
strip) carrying a long slab gondola with a glowing reactor block amidships, a row of radiation bombs racked under the keel
(brass casings, amber nose lamps, bone bands), four rear propeller pods and a cruciform tail whose flat panels carry the house colour.
AIR convention: see dir_dirigible.py (body baked at ALT, no shadow; separate _shadow sheet).
House paint: the four tail-fin panels (large) and a stripe on each flank of the envelope.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_common as D

ID = "dir_great_dirigible"
TITLE = "Directorate Great Dirigible (heavy radiation bomber)"
CANVAS = 320
ALT = 1.9
TARGET = (0.0, 0.0, 3.0)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "shadow"]
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS


def build(S, state, k, n):
    box, cyl, sph, cone = S.box, S.cyl, S.sphere, S.cone
    # The pipeline's 30-unit ground plane edge would land in the top row of the shadow frame; grow it (see auth_bomber.py).
    import bpy
    g = bpy.data.objects.get("ground")
    if g is not None:
        g.scale = (3.0, 3.0, 1.0)
    S.begin("body")
    z0 = ALT                        # gondola underside
    ez = z0 + 3.1                  # envelope axis height
    RY, RR = 3.9, 1.5               # half-length, radius
    sph("envelope", RR, (0, 0, ez), "bone", scale=(1, RY / RR, 1))

    def rad(y):
        return RR * math.sqrt(max(0.0, 1 - (y / RY) ** 2))
    for y in (-2.4, -0.6, 1.2):
        cyl(f"band{y}", rad(y) + 0.03, 0.4, (0, y, ez), "oxide", "Y", 24)
    for y in (-1.5, 0.3):          # patched canvas panels between the bands
        cyl(f"patch{y}", rad(y) + 0.02, 0.2, (0, y, ez), "umber", "Y", 24)
    sph("nosecap", 0.62, (0, 3.35, ez), "oxide2", scale=(1, 0.9, 1))
    box("armour", (0.9, 5.2, 0.14), (0, 0.1, ez - RR + 0.07), "dark", 0.03)
    box("spine", (0.2, 5.8, 0.14), (0, 0.0, ez + RR - 0.03), "hull2", 0.03)
    for y in (-2.2, -0.8, 0.6, 2.0):
        box(f"rib{y}", (0.36, 0.14, 0.12), (0, y, ez + RR + 0.01), "steel", 0.0)
    for sx in (-1, 1):                         # house stripe along each flank of the envelope
        box(f"flankstripe{sx}", (0.05, 3.6, 0.2), (sx * (RR - 0.02), 0.2, ez - 0.05), "house", 0.0)
        box(f"flankedge{sx}", (0.06, 3.6, 0.05), (sx * (RR - 0.02), 0.2, ez - 0.22), "oxide", 0.0)
    # ---- tail: cruciform fins, large house panels with oxide edges
    fy = -3.0
    box("finTop", (0.1, 1.6, 1.7), (0, fy, ez + 1.4), "house", 0.0)
    box("finBot", (0.1, 1.6, 1.1), (0, fy, ez - 1.25), "hull2", 0.02)
    for sx in (-1, 1):
        box(f"finSide{sx}", (1.7, 1.6, 0.1), (sx * 1.55, fy, ez), "house", 0.0)
        box(f"finEdge{sx}", (1.7, 0.1, 0.13), (sx * 1.55, fy - 0.83, ez), "oxide", 0.0)
    box("finEdgeT", (0.13, 0.1, 1.7), (0, fy - 0.83, ez + 1.4), "oxide", 0.0)
    # ---- slab gondola, reactor block, bridge
    box("gondola", (1.5, 4.2, 0.95), (0, 0.2, z0 + 0.5), "hull", 0.12)
    box("gnose", (1.1, 0.5, 0.65), (0, 2.45, z0 + 0.45), "hull2", 0.1)
    box("gglass", (0.95, 0.07, 0.26), (0, 2.73, z0 + 0.52), "glass", 0.0)
    box("gdeck", (1.0, 1.0, 0.3), (0, 1.6, z0 + 1.1), "hull2", 0.06)
    box("gdeckglass", (0.8, 0.07, 0.14), (0, 2.12, z0 + 1.14), "glass", 0.0)
    box("reactor", (1.0, 1.0, 0.45), (0, -0.7, z0 + 1.2), "steel", 0.05)
    box("reactglow", (0.8, 0.06, 0.2), (0, -0.7, z0 + 1.28), "glow", 0.0)
    for sx in (-1, 1):
        box(f"reactglow{sx}", (0.06, 0.8, 0.2), (sx * 0.51, -0.7, z0 + 1.28), "glow", 0.0)
    cyl("stack", 0.12, 0.5, (0, -1.35, z0 + 1.65), "dark", "Z", 8)
    for sx in (-1, 1):
        box(f"gstripe{sx}", (0.06, 2.6, 0.16), (sx * 0.76, 0.3, z0 + 0.65), "oxide", 0.0)
        box(f"gwin{sx}", (0.06, 0.9, 0.16), (sx * 0.76, 1.7, z0 + 0.62), "glass", 0.0)
        for y in (1.0, 0.1):
            cyl(f"strutF{sx}{y}", 0.035, 1.9, (sx * 0.55, y, z0 + 1.5), "dark", "Z", 5)
        for y in (-0.6, -1.5):
            cyl(f"strutB{sx}{y}", 0.035, 1.9, (sx * 0.55, y, z0 + 1.5), "dark", "Z", 5)
    for y in (1.0, 0.1, -0.8, -1.7):
        box(f"cradle{y}", (1.3, 0.1, 0.1), (0, y, z0 + 0.98), "dark", 0.0)
    # ---- radiation bombs racked under the keel: dark bay, brass casings, bone bands, amber nose lamps
    box("bay", (1.1, 3.2, 0.14), (0, 0.2, z0 - 0.04), "dark", 0.02)
    for i in range(5):
        y = -1.1 + i * 0.7
        cyl(f"bomb{i}", 0.19, 0.5, (0, y, z0 - 0.26), "brass", "Y", 10)
        sph(f"bombn{i}", 0.19, (0, y + 0.27, z0 - 0.26), "glow", scale=(1, 0.8, 1))
        cyl(f"bombband{i}", 0.2, 0.08, (0, y - 0.05, z0 - 0.26), "bone", "Y", 10)
        box(f"bombfin{i}", (0.34, 0.12, 0.04), (0, y - 0.22, z0 - 0.26), "dark", 0.0)
    for sx in (-1, 1):
        box(f"bayrail{sx}", (0.07, 3.2, 0.12), (sx * 0.5, 0.2, z0 - 0.1), "steel", 0.0)
    # ---- four rear propeller pods on pylons
    for sx in (-1, 1):
        for j, (px, pz) in enumerate(((1.0, 0.45), (1.55, 1.15))):
            x = sx * px
            box(f"pylon{sx}{j}", (0.5, 0.16, 0.14), (sx * (px - 0.3), -1.65, z0 + pz), "hull2", 0.02)
            cyl(f"pod{sx}{j}", 0.17, 0.5, (x, -1.75, z0 + pz), "steel", "Y", 10)
            cyl(f"prop{sx}{j}", 0.5, 0.03, (x, -2.05, z0 + pz), "dark", "Y", 16)
            cyl(f"spin{sx}{j}", 0.08, 0.1, (x, -2.03, z0 + pz), "oxide", "Y", 8)
