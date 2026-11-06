"""Expedition Bomber (heavy strategic bomber, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A long four-engined navy aircraft: a cigar fuselage with a lagoon-white spine and belly, a glazed nose, a dark bomb-bay with chrome
doors on the belly, long straight wings with four chrome turbine nacelles (copper rings), a tall vertical fin with a team-colour
flash and orange tip, tailplanes, orange wing-tip lights, a white hull number on the spine.  The biggest sprite of the roster:
about 9 x 8 units, drawn at 64 px per cell like everything else.
AIR convention: see dir_dirigible.py (body baked at ALT, no shadow; separate _shadow sheet).  Team colour: the fin flash and the
fuselage stripe.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_bomber"
TITLE = "Expedition Bomber (heavy strategic bomber)"
CANVAS = 288
ALT = 2.6
TARGET = (0.0, 0.0, 2.8)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "shadow"]
PALETTE = E.PALETTE
MATERIALS = E.MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    z0 = ALT + 0.5
    cyl("fus", 0.6, 6.4, (0, 0.0, z0), "navy", "Y", 20)
    cyl("fusbelly", 0.5, 5.6, (0, 0.0, z0 - 0.3), "white", "Y", 16)
    S.cone("tailcone", 0.6, 0.18, 1.7, (0, -4.0, z0 + 0.12), "navy2", rot=(90, 0, 0), verts=18)
    sph("nose", 0.6, (0, 3.2, z0), "glass", scale=(1.0, 1.1, 0.9))
    cyl("nosering", 0.62, 0.08, (0, 2.85, z0), "copper", "Y", 20)
    for i, y in enumerate((3.4, 3.15)):
        box(f"noseframe{i}", (0.06, 0.06, 0.9), (0, y, z0), "chrome2", 0.0)
    box("spine", (0.5, 4.2, 0.07), (0, -0.2, z0 + 0.6), "white", 0.0)
    box("spinestripe", (0.52, 0.08, 0.09), (0, 1.2, z0 + 0.61), "orange", 0.0)
    E.digits(S, "hn", "01", 0, -0.7, z0 + 0.64, 0.085, "navy2")
    for s in (-1, 1):
        box(f"stripe{s}", (0.05, 4.0, 0.2), (s * 0.62, 0.0, z0 + 0.05), "house", 0.0)
        box(f"cheat{s}", (0.05, 4.2, 0.06), (s * 0.6, 0.0, z0 + 0.3), "copper", 0.0)
    # bomb bay on the belly
    box("bay", (0.7, 2.2, 0.12), (0, 0.4, z0 - 0.6), "dark", 0.02)
    for s in (-1, 1):
        box(f"bay{s}", (0.3, 2.3, 0.06), (s * 0.35, 0.4, z0 - 0.67), "chrome2", 0.0)
    # wings, nacelles
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"wing{nm}", (3.9, 1.5, 0.14), (s * 2.5, -0.1, z0 - 0.05), "navy", 0.04, rot=(0, 0, -s * 10))
        box(f"wlead{nm}", (3.9, 0.12, 0.16), (s * 2.55, 0.62, z0 - 0.04), "white", 0.0, rot=(0, 0, -s * 10))
        box(f"wstripe{nm}", (1.0, 0.5, 0.16), (s * 3.3, -0.15, z0 - 0.03), "house", 0.0, rot=(0, 0, -s * 10))
        box(f"wtip{nm}", (0.16, 0.5, 0.18), (s * 4.45, -0.4, z0 - 0.03), "orange", 0.0, rot=(0, 0, -s * 10))
        for j, x in enumerate((1.5, 2.9)):
            E.nacelle(S, f"{nm}{j}", s * x, 0.6 - 0.26 * x * 0.3, z0 - 0.35, 0.28, 1.3)
        box(f"stab{nm}", (1.5, 0.8, 0.09), (s * 1.0, -3.9, z0 + 0.1), "navy", 0.03, rot=(0, 0, -s * 12))
    box("fin", (0.1, 1.3, 1.5), (0, -3.7, z0 + 1.0), "navy", 0.04, rot=(-6, 0, 0))
    box("finfl", (0.12, 0.8, 0.55), (0, -3.72, z0 + 1.15), "house", 0.0, rot=(-6, 0, 0))
    box("finstripe", (0.12, 0.8, 0.07), (0, -3.72, z0 + 1.55), "orange", 0.0, rot=(-6, 0, 0))
    box("fintip", (0.13, 0.4, 0.08), (0, -3.75, z0 + 1.76), "orange", 0.0)
    cyl("whip", 0.02, 0.8, (0.2, 1.8, z0 + 0.95), "chrome2", "Z", 6)
