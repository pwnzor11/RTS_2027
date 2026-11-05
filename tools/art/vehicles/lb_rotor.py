"""Left Behind Rotor (autogyro light gunship, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A scrap autogyro: a bus-panel pod fuselage (faded-teal skin, rust nose, patched belly) with a glass bubble, a skeletal tail boom of
lashed pipe carrying twin rust-and-hazard fins and a rear pusher propeller, a tall pylon with a big two-blade rotor in sailcloth
and wood, three spindly wheels, a light gun slung under the nose and a hazard-banded fuel drum strapped behind the seat.
AIR convention: see dir_dirigible.py: body baked at ALT above the ground origin with no shadow, separate _shadow sheet.
House paint: both rotor blade tips, the pod flank panels, the tail fins.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as L

ID = "lb_rotor"
TITLE = "Left Behind Rotor (autogyro)"
CANVAS = 224
ALT = 2.0
TARGET = (0.0, 0.0, 2.2)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "shadow"]
PALETTE = L.PALETTE
MATERIALS = L.MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    z0 = ALT
    # pod fuselage
    box("pod", (1.2, 2.0, 0.9), (0, 0.3, z0 + 0.6), "hull", 0.14)
    box("podbelly", (1.0, 1.7, 0.2), (0, 0.3, z0 + 0.12), "rust2", 0.06)
    box("nose", (0.9, 0.7, 0.7), (0, 1.5, z0 + 0.55), "rust", 0.16)
    sph("bubble", 0.5, (0, 0.8, z0 + 1.0), "glass", scale=(1.0, 1.1, 0.8))
    box("podcowl", (1.0, 0.9, 0.4), (0, -0.4, z0 + 1.15), "rust2", 0.1)
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"flank{nm}", (0.05, 1.1, 0.4), (s * 0.62, 0.0, z0 + 0.6), "house", 0.0)
        box(f"patch{nm}", (0.06, 0.5, 0.3), (s * 0.62, 0.9, z0 + 0.65), "ochre" if s < 0 else "bone", 0.0)
    box("strip", (0.9, 0.12, 0.1), (0, 1.88, z0 + 0.5), "hazard", 0.0)
    L.lamp(S, "n", 0.0, 1.88, z0 + 0.7)
    # tail boom: lashed pipes, fins, pusher prop
    for s in (-1, 1):
        cyl(f"boom{s}", 0.07, 3.0, (s * 0.2, -2.2, z0 + 0.85), "ash", "Y", 8)
    for i, y in enumerate((-1.2, -2.2, -3.2)):
        box(f"brace{i}", (0.5, 0.06, 0.06), (0, y, z0 + 0.85), "rope", 0.0)
    box("stab", (1.6, 0.5, 0.06), (0, -3.5, z0 + 0.9), "hull2", 0.0)
    for s in (-1, 1):
        box(f"fin{s}", (0.06, 0.6, 0.9), (s * 0.8, -3.5, z0 + 1.3), "house", 0.0)
        box(f"finedge{s}", (0.08, 0.08, 0.9), (s * 0.8, -3.82, z0 + 1.3), "hazard", 0.0)
    cyl("pusherhub", 0.12, 0.3, (0, -0.95, z0 + 0.95), "steel", "Y", 8)
    cyl("pusherdisc", 0.6, 0.04, (0, -1.2, z0 + 0.95), "dark", "Y", 16)
    box("pusherblade", (1.2, 0.05, 0.12), (0, -1.15, z0 + 0.95), "wood", 0.0)
    # pylon + rotor
    box("pylon", (0.14, 0.14, 1.4), (0, 0.0, z0 + 1.7), "ash2", 0.0, rot=(-8, 0, 0))
    box("pylonB1", (0.08, 0.08, 1.3), (-0.3, -0.3, z0 + 1.55), "steel", 0.0, rot=(-6, -12, 0))
    box("pylonB2", (0.08, 0.08, 1.3), (0.3, -0.3, z0 + 1.55), "steel", 0.0, rot=(-6, 12, 0))
    cyl("rotorhub", 0.17, 0.2, (0, -0.15, z0 + 2.55), "rust2", "Z", 10)
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"blade{nm}", (2.1, 0.32, 0.06), (s * 1.25, -0.15, z0 + 2.5), "canvas", 0.0, rot=(0, -s * 3, 0))
        box(f"bladeedge{nm}", (2.1, 0.06, 0.08), (s * 1.25, 0.04, z0 + 2.51), "wood", 0.0, rot=(0, -s * 3, 0))
        box(f"bladetip{nm}", (0.5, 0.33, 0.07), (s * 2.1, -0.15, z0 + 2.5), "house", 0.0, rot=(0, -s * 3, 0))
    # landing gear
    cyl("wheelF", 0.2, 0.14, (0, 1.3, z0 - 0.15), "track", "X", 12)
    box("legF", (0.07, 0.07, 0.5), (0, 1.3, z0 + 0.15), "steel", 0.0)
    for s in (-1, 1):
        cyl(f"wheel{s}", 0.2, 0.14, (s * 0.7, -0.2, z0 - 0.15), "track", "X", 12)
        box(f"leg{s}", (0.07, 0.07, 0.55), (s * 0.55, -0.2, z0 + 0.15), "steel", 0.0, rot=(0, s * 20, 0))
    # light gun slung under the nose, drum behind the seat
    box("gunmount", (0.2, 0.4, 0.15), (0.0, 1.5, z0 - 0.02), "ash2", 0.02)
    cyl("gunbarrel", 0.05, 0.9, (0, 2.1, z0 - 0.02), "steel", "Y", 6)
    L.barrel(S, "a", 0.0, -0.3, z0 + 1.55, "rust", r=0.28, h=0.6, band="hazard")
