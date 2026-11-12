"""Authority quick-set foam layer (support truck) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Six-wheeled olive tanker (same greens, house panels and finish as the decon truck): cab with house doors, a short horizontal
foam tank on the bed with a pale shell and a house band, a mixing pump housing, and a fixed rear spray boom: a cross-pipe
with four downward nozzles that lays foam behind the truck.  Hazard chevrons on the tail.  No turret: one body layer.
House paint: cab doors, bonnet panel, tank band.
"""
ID = "auth_foam_truck"
TITLE = "Authority foam layer"
CANVAS = 112
TARGET = (0.0, 0.0, 1.0)
STATES = [("idle", 16, 1, 1)]


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    # ---- wheels: front axle + tandem rear
    for s, side in ((-1, "L"), (1, "R")):
        for y, tag in ((1.15, "F"), (-0.45, "M"), (-1.2, "R")):
            x = s * 0.82
            cyl(f"tyre{side}{tag}", 0.38, 0.3, (x, y, 0.38), "track", "X", 16)
            cyl(f"hub{side}{tag}", 0.18, 0.33, (x, y, 0.38), "steel", "X", 12)
            cyl(f"nut{side}{tag}", 0.06, 0.36, (x, y, 0.38), "dark", "X", 6)
        box(f"guardF{side}", (0.4, 0.9, 0.05), (s * 0.82, 1.15, 0.86), "hull2", 0.02)
        box(f"guardR{side}", (0.4, 1.9, 0.05), (s * 0.82, -0.82, 0.86), "hull2", 0.02)
    for y in (1.15, -0.45, -1.2):
        box(f"axle{y}", (1.55, 0.1, 0.1), (0, y, 0.38), "dark", 0)
    box("frame", (1.3, 3.4, 0.24), (0, -0.1, 0.68), "hull2", 0.04)
    # ---- cab
    box("hood", (1.35, 0.8, 0.5), (0, 1.5, 0.98), "hull2", 0.07)
    box("hoodtop", (0.95, 0.55, 0.04), (0, 1.5, 1.24), "house", 0.0)
    box("grille", (0.8, 0.05, 0.26), (0, 1.91, 0.98), "dark", 0.01)
    box("cab", (1.4, 0.9, 0.82), (0, 0.8, 1.31), "hull", 0.07)
    box("cabroof", (1.46, 0.96, 0.07), (0, 0.8, 1.74), "hull2", 0.03)
    box("ws", (1.1, 0.05, 0.36), (0, 1.27, 1.4), "glass", 0.0)
    for sx in (-1, 1):
        box(f"door{sx}", (0.05, 0.7, 0.48), (sx * 0.72, 0.75, 1.32), "house", 0.0)
        box(f"sglass{sx}", (0.05, 0.36, 0.26), (sx * 0.72, 1.07, 1.5), "glass", 0.0)
        cyl(f"lamp{sx}", 0.08, 0.06, (sx * 0.5, 1.93, 0.98), "lens", "Y", 10)
    cyl("beacon", 0.09, 0.14, (0, 0.55, 1.85), "yellow", "Z", 10)
    # ---- bed: deck, saddles, foam tank, pump housing
    box("deck", (1.5, 2.3, 0.1), (0, -0.7, 0.82), "hull2", 0.03)
    for sy in (-1.2, 0.0):
        box(f"saddle{sy}", (1.0, 0.16, 0.4), (0, sy, 1.05), "hull2", 0.03)
    cyl("tank", 0.6, 1.5, (0, -0.55, 1.4), "light", "Y", 20)
    cyl("tankband", 0.63, 0.45, (0, -0.55, 1.4), "house", "Y", 20)
    cyl("tankbandF", 0.62, 0.1, (0, 0.05, 1.4), "hull2", "Y", 20)
    cyl("tankbandR", 0.62, 0.1, (0, -1.15, 1.4), "hull2", "Y", 20)
    sph("domeF", 0.58, (0, 0.2, 1.4), "light", scale=(1, 0.3, 1))
    sph("domeR", 0.58, (0, -1.3, 1.4), "light", scale=(1, 0.3, 1))
    cyl("filler", 0.12, 0.1, (0.0, -0.55, 2.0), "yellow", "Z", 10)
    cyl("filler2", 0.2, 0.05, (0.0, -0.55, 1.98), "steel", "Z", 10)
    box("pump", (0.9, 0.55, 0.5), (0, -1.65, 1.12), "hull", 0.06)
    cyl("pumpdrum", 0.2, 0.5, (0.25, -1.65, 1.5), "steel", "Y", 10)
    cyl("pumpgauge", 0.07, 0.05, (-0.25, -1.65, 1.45), "yellow", "Z", 8)
    # ---- fixed rear spray boom: cross pipe on two arms, four nozzles pointing down, hose
    for sx in (-1, 1):
        box(f"arm{sx}", (0.1, 0.5, 0.1), (sx * 0.55, -2.0, 0.95), "steel", 0.0)
    cyl("boom", 0.09, 1.8, (0, -2.25, 0.95), "steel", "X", 8)
    cyl("boomhouse", 0.12, 0.5, (0, -2.25, 0.95), "house", "X", 8)
    for i, x in enumerate((-0.8, -0.27, 0.27, 0.8)):
        cyl(f"noz{i}", 0.09, 0.28, (x, -2.25, 0.8), "yellow", "Z", 8)
        S.cone(f"nozc{i}", 0.12, 0.05, 0.18, (x, -2.25, 0.62), "dark", rot=(180, 0, 0), verts=8)
    cyl("hose", 0.05, 0.9, (0.25, -2.0, 1.1), "dark", "Y", 6)
    # ---- tail plate chevrons + side hazard bars
    box("tail", (1.5, 0.08, 0.3), (0, -1.88, 0.78), "hull2", 0.02)
    for i in range(7):
        box(f"chev{i}", (0.17, 0.05, 0.24), (-0.6 + i * 0.2, -1.93, 0.78), "yellow" if i % 2 == 0 else "dark", 0.0, rot=(0, 0, 30))
    for sx in (-1, 1):
        box(f"sidehaz{sx}", (0.05, 0.9, 0.14), (sx * 0.72, -1.0, 0.98), "yellow", 0.0)
        box(f"ladder{sx}", (0.04, 0.5, 0.04), (sx * 0.62, -1.4, 1.0), "steel", 0.0)
