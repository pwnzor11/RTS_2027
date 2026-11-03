"""Authority scout jeep for tools/art/build_vehicle.py.

Module contract: ID, TITLE, CANVAS (square frame px, 64 px per cell), TARGET (camera look-at, world units; fixes the ground
origin pixel), STATES [(name, facings, frames, ticks_per_frame)], optional MATERIALS {name: (hex, {rough, metal, emit})},
and build(S, state, k, n) which adds primitives with S.box / S.cyl / S.sphere / S.cone.  x east, y north (front), z up.
House paint = material "house" (rendered magenta, remapped later).  The turret/MG is baked into the hull in M2.
"""
import math

ID = "auth_jeep"
TITLE = "Authority scout jeep"
CANVAS = 88
TARGET = (0.0, 0.1, 0.6)
STATES = [("idle", 16, 1, 1)]


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    # wheels: 4, big knobbly tyres with steel hubs
    for s, side in ((-1, "L"), (1, "R")):
        for y, tag in ((0.78, "F"), (-0.72, "R")):
            x = s * 0.66
            cyl(f"tyre{side}{tag}", 0.31, 0.24, (x, y, 0.31), "track", "X", 16)
            cyl(f"hub{side}{tag}", 0.15, 0.27, (x, y, 0.31), "steel", "X", 12)
            cyl(f"nut{side}{tag}", 0.05, 0.3, (x, y, 0.31), "dark", "X", 6)
    box("axleF", (1.2, 0.1, 0.1), (0, 0.78, 0.31), "dark", 0)
    box("axleR", (1.2, 0.1, 0.1), (0, -0.72, 0.31), "dark", 0)
    # tub, hood, rear deck
    box("tub", (1.12, 1.7, 0.5), (0, -0.2, 0.68), "hull", 0.06)
    box("hood", (1.0, 0.95, 0.42), (0, 0.9, 0.74), "hull2", 0.07)
    box("hoodtop", (0.82, 0.8, 0.05), (0, 0.92, 0.96), "house", 0.0)           # big house panel on the bonnet
    box("grille", (0.7, 0.05, 0.22), (0, 1.4, 0.74), "dark", 0.01)
    for s in (-1, 1):
        cyl(f"lamp{s}", 0.08, 0.06, (s * 0.38, 1.42, 0.84), "lens", "Y", 10)
        box(f"fender{s}", (0.3, 1.0, 0.05), (s * 0.66, 0.78, 0.7), "hull2", 0.02)       # wheel arches
        box(f"fenderr{s}", (0.3, 0.9, 0.05), (s * 0.66, -0.72, 0.7), "hull2", 0.02)
        box(f"door{s}", (0.05, 1.0, 0.4), (s * 0.585, -0.2, 0.74), "house", 0.0)         # big house door panels
        box(f"sill{s}", (0.1, 1.5, 0.08), (s * 0.58, -0.2, 0.46), "dark", 0.0)
    box("rearplate", (1.12, 0.06, 0.34), (0, -1.07, 0.68), "hull2", 0.02)
    # open cabin: seats, folded windscreen frame, dash
    box("dash", (1.0, 0.14, 0.2), (0, 0.4, 1.02), "hull2", 0.03)
    box("wsframe", (1.0, 0.04, 0.05), (0, 0.45, 1.3), "steel", 0.0)
    for s in (-1, 1):
        box(f"wspost{s}", (0.05, 0.05, 0.3), (s * 0.5, 0.45, 1.16), "steel", 0.0)
        box(f"seat{s}", (0.4, 0.34, 0.14), (s * 0.27, -0.1, 0.98), "rust", 0.04)
        box(f"seatb{s}", (0.4, 0.07, 0.28), (s * 0.27, -0.3, 1.14), "rust", 0.03)
    box("benchr", (0.9, 0.3, 0.1), (0, -0.78, 0.97), "hull2", 0.03)
    cyl("wheel_steer", 0.1, 0.03, (-0.27, 0.33, 1.14), "dark", "Z", 10, rot=(-40, 0, 0))
    # spare wheel on the tail
    cyl("spare", 0.3, 0.13, (0, -1.17, 0.98), "track", "Y", 16)
    cyl("sparehub", 0.14, 0.15, (0, -1.17, 0.98), "rust", "Y", 10)
    # MG on a post behind the seats, with a shield
    cyl("post", 0.05, 0.62, (0.0, -0.45, 1.24), "steel", "Z", 8)
    box("mgbody", (0.14, 0.5, 0.14), (0, -0.2, 1.58), "dark", 0.02)
    cyl("mgbarrel", 0.035, 0.7, (0, 0.28, 1.59), "steel", "Y", 8)
    box("mgbox", (0.12, 0.22, 0.14), (0.14, -0.28, 1.52), "yellow", 0.02)
    box("mgshield", (0.5, 0.04, 0.34), (0, 0.04, 1.6), "steel", 0.02)
    # antenna: whip on the left rear corner with a base block and a white pennant
    box("antbase", (0.1, 0.1, 0.1), (-0.5, -0.98, 0.98), "dark", 0.0)
    cyl("antenna", 0.025, 1.0, (-0.5, -0.98, 1.5), "dark", "Z", 6)
    box("pennant", (0.02, 0.14, 0.08), (-0.5, -1.06, 1.96), "white", 0.0)
    box("jerry", (0.08, 0.3, 0.3), (0.5, -0.98, 1.02), "yellow", 0.02)
