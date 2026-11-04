"""Directorate Rocket Truck for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Six-wheeled truck (three axles) with a bone cab striped in oxide red, and on the bed a tilted box rack of rocket pods that
swings on its own turret layer (pivot on the middle of the bed).  Layers: body (truck + whole-vehicle shadow), turret (the rack).
House paint: cab door panels, cab roof stripe, rack side panels and pod-block top.
"""
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_common as D

ID = "dir_rocket_truck"
TITLE = "Directorate Rocket Truck"
CANVAS = 112
TARGET = (0.0, 0.0, 0.9)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT = (0.0, -0.85, 0.9)
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS
TILT = 34.0


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    X = 0.82
    axles = (1.15, -0.55, -1.3)
    for s, side in ((-1, "L"), (1, "R")):
        for i, y in enumerate(axles):
            cyl(f"tyre{side}{i}", 0.37, 0.3, (s * X, y, 0.37), "track", "X", 16)
            cyl(f"hub{side}{i}", 0.18, 0.33, (s * X, y, 0.37), "steel", "X", 10)
            cyl(f"nut{side}{i}", 0.06, 0.36, (s * X, y, 0.37), "dark", "X", 6)
        box(f"guardF{side}", (0.42, 0.9, 0.06), (s * X, 1.15, 0.8), "hull2", 0.02)
        box(f"guardR{side}", (0.42, 1.9, 0.06), (s * X, -0.92, 0.8), "hull2", 0.02)
    for i, y in enumerate(axles):
        box(f"axle{i}", (1.5, 0.1, 0.1), (0, y, 0.37), "dark", 0)
    box("frame", (1.25, 3.9, 0.2), (0, -0.1, 0.66), "hull2", 0.03)
    # ---- cab-over: bone box, red stripes, glass, house door panels
    box("cab", (1.55, 1.15, 1.0), (0, 1.1, 1.25), "bone", 0.08)
    box("cabroof", (1.6, 1.2, 0.07), (0, 1.1, 1.78), "hull2", 0.03)
    box("roofhouse", (0.6, 0.9, 0.03), (0, 1.1, 1.83), "house", 0.0)
    box("ws", (1.2, 0.05, 0.42), (0, 1.69, 1.45), "glass", 0.0)
    box("grille", (0.9, 0.05, 0.26), (0, 1.69, 0.95), "dark", 0.0)
    for sx in (-1, 1):
        box(f"door{sx}", (0.05, 0.8, 0.5), (sx * 0.79, 1.05, 1.3), "house", 0.0)
        box(f"sglass{sx}", (0.05, 0.4, 0.28), (sx * 0.79, 1.55, 1.5), "glass", 0.0)
        box(f"stripeA{sx}", (0.06, 1.1, 0.12), (sx * 0.79, 1.1, 1.0), "oxide", 0.0)
        box(f"lamp{sx}", (0.2, 0.06, 0.14), (sx * 0.55, 1.7, 0.78), "lens", 0.0)
        box(f"mirror{sx}", (0.1, 0.06, 0.3), (sx * 0.9, 1.62, 1.5), "dark", 0.0)
    box("stripeF", (1.55, 0.06, 0.12), (0, 1.69, 1.12), "oxide", 0.0)
    box("stripeF2", (1.55, 0.06, 0.12), (0, 1.69, 1.7), "oxide", 0.0)
    # ---- bed: deck, side rails, fuel drum, exhaust stack behind the cab
    box("deck", (1.6, 2.5, 0.12), (0, -0.78, 0.8), "hull2", 0.03)
    for sx in (-1, 1):
        box(f"rail{sx}", (0.06, 2.5, 0.2), (sx * 0.78, -0.78, 0.92), "hull", 0.0)
        cyl(f"stack{sx}", 0.07, 0.9, (sx * 0.78, 0.48, 1.5), "steel", "Z", 8)
    box("rearbump", (1.5, 0.12, 0.14), (0, -2.0, 0.72), "oxide", 0.02)
    # ---- turret layer: the tilted rocket rack
    S.begin("turret", PIVOT)
    px, py, pz = PIVOT
    a = math.radians(TILT)
    ca, sa = math.cos(a), math.sin(a)

    def L(lx, ly, lz):
        return (px + lx, py + ly * ca - lz * sa, pz + ly * sa + lz * ca)

    box("cradle", (1.5, 1.5, 0.14), (px, py, pz + 0.08), "hull2", 0.03)
    box("hinge", (1.5, 0.3, 0.3), L(0, -0.95, 0.15), "dark", 0.03, rot=(TILT, 0, 0))
    box("rack", (1.36, 2.0, 0.84), L(0, 0.05, 0.48), "hull", 0.06, rot=(TILT, 0, 0))
    box("racktop", (1.2, 1.7, 0.05), L(0, 0.05, 0.92), "hull2", 0.0, rot=(TILT, 0, 0))
    box("racktopstripe", (0.5, 1.2, 0.04), L(0, 0.05, 0.95), "oxide", 0.0, rot=(TILT, 0, 0))
    for sx in (-1, 1):
        box(f"rside{sx}", (0.05, 1.1, 0.4), L(sx * 0.7, 0.2, 0.5), "house", 0.0, rot=(TILT, 0, 0))
        box(f"rband{sx}", (0.06, 0.3, 0.7), L(sx * 0.7, -0.75, 0.48), "oxide", 0.0, rot=(TILT, 0, 0))
    # pod mouths: 3 x 2 tubes on the front face, rocket noses poking out
    for i in range(3):
        for j in range(2):
            lx, lz = -0.42 + i * 0.42, 0.27 + j * 0.42
            cyl(f"tube{i}{j}", 0.16, 0.12, L(lx, 1.07, lz), "dark", rot=(TILT - 90, 0, 0), verts=10)
            cyl(f"rkt{i}{j}", 0.1, 0.22, L(lx, 1.14, lz), "bone", rot=(TILT - 90, 0, 0), verts=8)
            cyl(f"tip{i}{j}", 0.07, 0.1, L(lx, 1.27, lz), "oxide", rot=(TILT - 90, 0, 0), verts=8)
    box("sight", (0.2, 0.3, 0.2), L(0.4, -0.6, 1.05), "dark", 0.02, rot=(TILT, 0, 0))
