"""Expedition Jet Fighter (air superiority fighter, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A sleek swept-wing jet: chrome needle nose, navy fuselage with a white spine and belly, a glass bubble canopy, swept navy wings with
white leading edges and a pair of air-to-air rockets (white body, orange nose) under them, two turbine exhausts, and twin canted
tail fins with team-colour flashes and orange tips.  Footprint about 4.2 x 5.6 units.
AIR convention: see dir_dirigible.py (body baked at ALT, no shadow; separate _shadow sheet).  Team colour: fin flashes and wing stripes.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_jet"
TITLE = "Expedition Jet Fighter"
CANVAS = 192
ALT = 2.4
TARGET = (0.0, 0.0, 2.5)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "shadow"]
PALETTE = E.PALETTE
MATERIALS = E.MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    z0 = ALT + 0.4
    cyl("fus", 0.42, 3.8, (0, -0.2, z0), "navy", "Y", 18)
    cyl("fusbelly", 0.3, 3.4, (0, -0.2, z0 - 0.22), "white", "Y", 14)
    S.cone("nose", 0.42, 0.06, 1.5, (0, 2.45, z0), "chrome", rot=(-90, 0, 0), verts=18)
    cyl("noseband", 0.43, 0.08, (0, 1.75, z0), "copper", "Y", 18)
    sph("canopy", 0.36, (0, 0.85, z0 + 0.34), "glass", scale=(0.9, 1.8, 0.7))
    box("spine", (0.2, 2.0, 0.07), (0, -1.1, z0 + 0.42), "white", 0.0)
    box("spinestripe", (0.22, 0.07, 0.09), (0, -0.5, z0 + 0.43), "orange", 0.0)
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"wing{nm}", (2.0, 1.35, 0.1), (s * 1.35, -0.45, z0 - 0.05), "navy", 0.03, rot=(0, 0, -s * 32))
        box(f"wlead{nm}", (2.0, 0.1, 0.12), (s * 1.5, 0.15, z0 - 0.04), "white", 0.0, rot=(0, 0, -s * 32))
        box(f"wstripe{nm}", (0.7, 0.35, 0.12), (s * 1.9, -0.75, z0 - 0.03), "house", 0.0, rot=(0, 0, -s * 32))
        box(f"wtip{nm}", (0.14, 0.35, 0.14), (s * 2.3, -0.9, z0 - 0.03), "orange", 0.0, rot=(0, 0, -s * 32))
        cyl(f"rocket{nm}", 0.07, 1.1, (s * 1.0, 0.0, z0 - 0.3), "white2", "Y", 8)
        S.cone(f"rocketnose{nm}", 0.07, 0.01, 0.25, (s * 1.0, 0.65, z0 - 0.3), "orange", rot=(-90, 0, 0), verts=8)
        box(f"pylon{nm}", (0.06, 0.4, 0.2), (s * 1.0, 0.0, z0 - 0.2), "chrome2", 0.0)
        # intake / exhaust
        cyl(f"exh{nm}", 0.2, 0.6, (s * 0.25, -2.25, z0 - 0.02), "chrome", "Y", 14)
        cyl(f"exhin{nm}", 0.16, 0.05, (s * 0.25, -2.56, z0 - 0.02), "dark", "Y", 12)
        cyl(f"exhring{nm}", 0.22, 0.06, (s * 0.25, -2.0, z0 - 0.02), "copper", "Y", 14)
        box(f"stab{nm}", (1.0, 0.7, 0.07), (s * 0.85, -2.0, z0 - 0.04), "navy", 0.02, rot=(0, 0, -s * 25))
        box(f"fin{nm}", (0.08, 0.9, 1.0), (s * 0.5, -1.85, z0 + 0.55), "navy", 0.03, rot=(0, s * 14, 0))
        box(f"finfl{nm}", (0.1, 0.55, 0.38), (s * 0.54, -1.9, z0 + 0.65), "house", 0.0, rot=(0, s * 14, 0))
        box(f"fintip{nm}", (0.11, 0.3, 0.07), (s * 0.62, -1.85, z0 + 1.02), "orange", 0.0, rot=(0, s * 14, 0))
        box(f"intake{nm}", (0.18, 0.3, 0.3), (s * 0.4, 0.7, z0 - 0.12), "dark", 0.04)
    E.lamp(S, "n", 0.0, 3.1, z0, 0.06)
