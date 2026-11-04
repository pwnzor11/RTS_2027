"""Authority decontamination truck for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Olive four-wheeled truck (same hull greens, house panels and finish as the jeep / scraper): cab with house doors, a horizontal
decontaminant tank on the bed (light shell, house band, yellow filler cap), hazard chevrons on the tail and the tank ends, and a
roof-mounted spray boom with a flared nozzle that swings on its own turret layer (pivot on top of the tank).
Layers: body (truck + whole-vehicle shadow) + turret (boom).  House paint: cab doors, bonnet panel, tank band, boom collar.
"""
import math

ID = "auth_decon_truck"
TITLE = "Authority decontamination truck"
CANVAS = 112
TARGET = (0.0, 0.0, 0.9)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT = (0.0, -0.75, 1.9)


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    for s, side in ((-1, "L"), (1, "R")):
        for y, tag in ((1.05, "F"), (-1.0, "R")):
            x = s * 0.8
            cyl(f"tyre{side}{tag}", 0.38, 0.28, (x, y, 0.38), "track", "X", 16)
            cyl(f"hub{side}{tag}", 0.18, 0.31, (x, y, 0.38), "steel", "X", 12)
            cyl(f"nut{side}{tag}", 0.06, 0.34, (x, y, 0.38), "dark", "X", 6)
        box(f"guardF{side}", (0.4, 0.95, 0.05), (s * 0.8, 1.05, 0.86), "hull2", 0.02)
        box(f"guardR{side}", (0.4, 0.95, 0.05), (s * 0.8, -1.0, 0.86), "hull2", 0.02)
    box("axleF", (1.5, 0.1, 0.1), (0, 1.05, 0.38), "dark", 0)
    box("axleR", (1.5, 0.1, 0.1), (0, -1.0, 0.38), "dark", 0)
    box("frame", (1.3, 3.2, 0.24), (0, 0, 0.68), "hull2", 0.04)
    # ---- cab
    box("hood", (1.35, 0.8, 0.5), (0, 1.35, 0.98), "hull2", 0.07)
    box("hoodtop", (0.95, 0.55, 0.04), (0, 1.35, 1.24), "house", 0.0)
    box("grille", (0.8, 0.05, 0.26), (0, 1.76, 0.98), "dark", 0.01)
    box("cab", (1.4, 0.9, 0.82), (0, 0.65, 1.31), "hull", 0.07)
    box("cabroof", (1.46, 0.96, 0.07), (0, 0.65, 1.74), "hull2", 0.03)
    box("ws", (1.1, 0.05, 0.36), (0, 1.12, 1.4), "glass", 0.0)
    for sx in (-1, 1):
        box(f"door{sx}", (0.05, 0.7, 0.48), (sx * 0.72, 0.6, 1.32), "house", 0.0)
        box(f"sglass{sx}", (0.05, 0.36, 0.26), (sx * 0.72, 0.92, 1.5), "glass", 0.0)
        cyl(f"lamp{sx}", 0.08, 0.06, (sx * 0.5, 1.78, 0.98), "lens", "Y", 10)
    cyl("beacon", 0.09, 0.14, (0, 0.4, 1.85), "yellow", "Z", 10)
    # ---- bed: deck, cradle, tank (horizontal cylinder) with end domes, bands, filler cap, ladder
    box("deck", (1.5, 2.1, 0.1), (0, -0.78, 0.82), "hull2", 0.03)
    for sy in (-1.45, -0.05):
        box(f"saddle{sy}", (1.0, 0.16, 0.42), (0, sy, 1.05), "hull2", 0.03)
    cyl("tank", 0.62, 2.0, (0, -0.75, 1.42), "light", "Y", 20)
    cyl("tankband", 0.645, 0.5, (0, -0.75, 1.42), "house", "Y", 20)
    cyl("tankbandF", 0.64, 0.12, (0, -0.2, 1.42), "hull2", "Y", 20)
    cyl("tankbandR", 0.64, 0.12, (0, -1.3, 1.42), "hull2", "Y", 20)
    S.sphere("domeF", 0.6, (0, 0.24, 1.42), "light", scale=(1, 0.3, 1))
    S.sphere("domeR", 0.6, (0, -1.74, 1.42), "light", scale=(1, 0.3, 1))
    cyl("filler", 0.12, 0.1, (0.0, -1.35, 2.05), "yellow", "Z", 10)
    for sx in (-1, 1):
        box(f"ladder{sx}", (0.04, 0.5, 0.04), (sx * 0.62, -1.25, 1.0), "steel", 0.0)
    # hazard chevrons on the tail plate
    box("tail", (1.5, 0.08, 0.5), (0, -1.88, 0.98), "hull2", 0.02)
    for i in range(7):
        box(f"chev{i}", (0.17, 0.05, 0.42), (-0.6 + i * 0.2, -1.94, 0.98), "yellow" if i % 2 == 0 else "dark", 0.0, rot=(0, 0, 30))
    for sx in (-1, 1):
        box(f"sidehaz{sx}", (0.05, 0.9, 0.16), (sx * 0.76, -1.2, 0.98), "yellow", 0.0)
        box(f"sidehaz2{sx}", (0.06, 0.16, 0.16), (sx * 0.76, -1.0, 0.98), "dark", 0.0)
        box(f"sidehaz3{sx}", (0.06, 0.16, 0.16), (sx * 0.76, -1.4, 0.98), "dark", 0.0)
    # ---- turret layer: spray boom
    S.begin("turret", PIVOT)
    px, py, pz = PIVOT
    cyl("swivel", 0.3, 0.14, (px, py, pz + 0.07), "steel", "Z", 12)
    cyl("collar", 0.34, 0.08, (px, py, pz + 0.18), "house", "Z", 12)
    box("pylon", (0.3, 0.3, 0.3), (px, py - 0.05, pz + 0.3), "hull2", 0.03)
    ang = 14.0
    a = math.radians(ang)
    ln = 1.45
    cy_, cz_ = py + 0.1 + (ln / 2) * math.cos(a), pz + 0.42 + (ln / 2) * math.sin(a)
    box("boom", (0.22, ln, 0.22), (px, cy_, cz_), "hull", 0.03, rot=(ang, 0, 0))
    ty_, tz_ = py + 0.1 + ln * math.cos(a), pz + 0.42 + ln * math.sin(a)
    S.sphere("elbow", 0.2, (px, ty_, tz_), "steel")
    cyl("nozzle", 0.22, 0.4, (px, ty_ + 0.16, tz_ + 0.05), "steel", rot=(ang - 90 - 20, 0, 0), verts=12)
    cyl("nozzlering", 0.27, 0.08, (px, ty_ + 0.3, tz_ + 0.1), "yellow", rot=(ang - 90 - 20, 0, 0), verts=12)
    box("counter", (0.24, 0.3, 0.2), (px, py - 0.3, pz + 0.38), "hull2", 0.04)
    cyl("hose", 0.06, 0.5, (px + 0.12, py - 0.05, pz + 0.55), "dark", "Y", 6)
