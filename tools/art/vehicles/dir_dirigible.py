"""Directorate Dirigible (small armoured airship, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Cigar envelope (bone, oxide-red bands, dark armour-plate belly strip, nose cap) over a concrete gondola with slit windows, side
drop hatches and twin rear propellers; cruciform tail fins whose big flat panels carry the house colour.
AIR convention: the body is baked at ALT = 2.0 world units (0.59 cell) above the ground origin, WITHOUT a shadow; the shadow is a
separate sheet <id>_shadow.png (same frame, origin, facings, 1-bit dithered) cast on the ground by the sun for that altitude.  The
game draws the shadow first at the unit's ground position and the body on top (the body sprite already floats).
"""
import math

ID = "dir_dirigible"
TITLE = "Directorate Dirigible (armoured airship)"
CANVAS = 224
ALT = 2.0
TARGET = (0.0, 0.0, 1.8)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "shadow"]
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_common as D
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    z0 = ALT                       # gondola underside
    ez = z0 + 1.85                  # envelope axis height
    RY, RR = 2.3, 0.92             # half-length, radius
    sph("envelope", RR, (0, 0, ez), "bone", scale=(1, RY / RR, 1))

    def rad(y):
        return RR * math.sqrt(max(0.0, 1 - (y / RY) ** 2))
    for y, m in ((-1.35, "oxide"), (-0.45, "oxide"), (0.45, "oxide"), (1.35, "oxide")):
        cyl(f"band{y}", rad(y) + 0.025, 0.28, (0, y, ez), m, "Y", 20)
    cyl("beltband", rad(0.9) + 0.02, 0.12, (0, 0.9, ez), "bone", "Y", 20)
    S.sphere("nosecap", 0.4, (0, 2.0, ez), "oxide2", scale=(1, 0.9, 1))
    box("armour", (0.5, 3.0, 0.1), (0, 0.1, ez - RR + 0.05), "dark", 0.02)
    box("spine", (0.14, 3.4, 0.1), (0, 0.0, ez + RR - 0.02), "hull2", 0.02)
    # ---- tail: cruciform fins, house-coloured flat panels with oxide edges
    fy = -1.75
    box("finTop", (0.07, 1.1, 1.0), (0, fy, ez + 0.95), "house", 0.0)
    box("finBot", (0.07, 1.1, 0.7), (0, fy, ez - 0.85), "hull2", 0.02)
    for sx in (-1, 1):
        box(f"finSide{sx}", (1.0, 1.1, 0.07), (sx * 0.95, fy, ez), "house", 0.0)
        box(f"finEdge{sx}", (1.0, 0.08, 0.09), (sx * 0.95, fy - 0.58, ez), "oxide", 0.0)
    box("finEdgeT", (0.09, 0.08, 1.0), (0, fy - 0.58, ez + 0.95), "oxide", 0.0)
    # ---- gondola, struts
    box("gondola", (1.15, 2.3, 0.8), (0, 0.15, z0 + 0.4), "hull", 0.1)
    box("gnose", (0.8, 0.3, 0.5), (0, 1.25, z0 + 0.35), "hull2", 0.1)
    box("gglass", (0.7, 0.06, 0.2), (0, 1.42, z0 + 0.42), "glass", 0.0)
    for sx in (-1, 1):
        box(f"gstripe{sx}", (0.06, 1.2, 0.14), (sx * 0.6, 0.1, z0 + 0.55), "oxide", 0.0)
        # drop hatch doors (open, dark) low on both flanks, oxide frame
        box(f"hatchframe{sx}", (0.06, 0.8, 0.3), (sx * 0.6, -0.55, z0 + 0.25), "oxide", 0.0)
        box(f"hatch{sx}", (0.07, 0.6, 0.2), (sx * 0.6, -0.55, z0 + 0.25), "dark", 0.0)
        for y in (0.45, -0.1):
            cyl(f"strutF{sx}{y}", 0.025, 0.7, (sx * 0.38, y, z0 + 0.95), "dark", "Z", 5)
    for y in (0.45, -0.1, -0.65):
        box(f"cradle{y}", (0.9, 0.08, 0.08), (0, y, z0 + 0.65), "dark", 0.0)
    # ---- twin rear propellers on pylons
    for sx in (-1, 1):
        box(f"pylon{sx}", (0.5, 0.14, 0.12), (sx * 0.65, -0.95, z0 + 0.35), "hull2", 0.02)
        cyl(f"pod{sx}", 0.14, 0.4, (sx * 0.9, -1.05, z0 + 0.35), "steel", "Y", 10)
        cyl(f"prop{sx}", 0.4, 0.03, (sx * 0.9, -1.3, z0 + 0.35), "dark", "Y", 16)
        cyl(f"spin{sx}", 0.07, 0.1, (sx * 0.9, -1.28, z0 + 0.35), "oxide", "Y", 8)
    box("ventral", (0.1, 1.2, 0.1), (0, -0.1, z0 - 0.04), "dark", 0.0)
