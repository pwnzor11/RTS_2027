"""Authority sealed personnel carrier for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Tracked, lead-lined slab-sided box (same hull greens, house panels and finish as the scraper): fully sealed roof with periscope
cluster and a driver's vision slit, gasket-banded hatches, a rear decon hatch with hazard chevrons, filtered air intake on the
roof rear.  No weapon, no turret: one body layer with the whole-vehicle shadow.  House paint: big flank panels, roof stripe.
"""
ID = "auth_carrier"
TITLE = "Authority sealed carrier"
CANVAS = 112
TARGET = (0.0, 0.0, 1.0)
STATES = [("idle", 16, 1, 1)]


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    # ---- tracks with road wheels and idlers
    for s, side in ((-1, "L"), (1, "R")):
        x = s * 1.0
        box("track" + side, (0.62, 3.0, 0.78), (x, -0.05, 0.42), "track", 0.14)
        cyl("idlF" + side, 0.42, 0.62, (x, 1.42, 0.44), "track", "X")
        cyl("idlR" + side, 0.42, 0.62, (x, -1.52, 0.44), "track", "X")
        for i in range(5):
            y = -1.2 + i * 0.58
            cyl(f"wh{side}{i}", 0.28, 0.1, (x + s * 0.32, y, 0.4), "steel", "X", 14)
            cyl(f"hb{side}{i}", 0.1, 0.08, (x + s * 0.38, y, 0.4), "rust", "X", 8)
        box("skirt" + side, (0.08, 3.0, 0.3), (x + s * 0.34, -0.05, 0.86), "hull2", 0.03)
    # ---- sealed hull: tall slab box, sloped glacis (faked with a stepped nose), roof
    box("hull", (1.8, 3.1, 0.95), (0, -0.05, 1.05), "hull", 0.1)
    box("nose", (1.8, 0.5, 0.7), (0, 1.5, 0.98), "hull2", 0.07)
    box("glacis", (1.5, 0.5, 0.14), (0, 1.62, 1.42), "hull2", 0.04, rot=(-24, 0, 0))
    box("roof", (1.82, 2.5, 0.12), (0, -0.3, 1.57), "hull2", 0.05)
    box("rearplate", (1.8, 0.1, 0.8), (0, -1.65, 1.05), "hull2", 0.03)
    for s in (-1, 1):
        # big house flank panels + gasket seam ribs
        box(f"flank{s}", (0.06, 2.2, 0.55), (s * 0.92, -0.2, 1.1), "house", 0.0)
        for i in range(3):
            box(f"rib{s}{i}", (0.07, 0.07, 0.7), (s * 0.93, -1.1 + i * 1.0, 1.12), "dark", 0.0)
        box(f"leadplate{s}", (0.05, 2.7, 0.14), (s * 0.92, -0.1, 1.5), "steel", 0.0)
    # roof house stripe
    box("roofstripe", (0.7, 2.0, 0.04), (0, -0.4, 1.65), "house", 0.0)
    # ---- driver vision slit and headlamps
    box("slit", (1.0, 0.05, 0.1), (0, 1.76, 1.02), "glass", 0.0)
    for s in (-1, 1):
        cyl(f"lamp{s}", 0.08, 0.06, (s * 0.62, 1.77, 0.86), "lens", "Y", 10)
        box(f"lampgd{s}", (0.2, 0.05, 0.2), (s * 0.62, 1.74, 0.86), "dark", 0.0)
    # ---- periscope cluster on the roof, front right/left
    for s in (-1, 1):
        cyl(f"peri{s}", 0.1, 0.34, (s * 0.38, 0.8, 1.8), "steel", "Z", 8)
        box(f"periw{s}", (0.18, 0.1, 0.1), (s * 0.38, 0.88, 1.9), "glass", 0.0)
    cyl("periC", 0.12, 0.42, (0.0, 0.55, 1.84), "hull", "Z", 8)
    box("periCw", (0.2, 0.1, 0.12), (0.0, 0.64, 1.96), "glass", 0.0)
    # ---- roof hatches with gasket rings
    for i, y in enumerate((-0.15, -0.75)):
        cyl(f"hatch{i}", 0.3, 0.07, (0.0, y, 1.67), "steel", "Z", 14)
        cyl(f"gasket{i}", 0.34, 0.04, (0.0, y, 1.64), "dark", "Z", 14)
        cyl(f"hwheel{i}", 0.06, 0.07, (0.0, y, 1.73), "yellow", "Z", 8)
    # ---- filtered air intake box, roof rear
    box("filter", (1.0, 0.5, 0.34), (0, -1.28, 1.78), "light", 0.05)
    for i in range(4):
        box(f"louv{i}", (0.9, 0.04, 0.05), (0, -1.5, 1.68 + i * 0.08), "dark", 0.0)
    cyl("filtcap", 0.14, 0.06, (0.0, -1.28, 1.97), "yellow", "Z", 10)
    # ---- rear decon hatch, hazard chevrons and a spare marker
    box("rhatch", (1.0, 0.07, 0.68), (0, -1.71, 1.0), "steel", 0.02)
    box("rhatchf", (1.14, 0.05, 0.8), (0, -1.69, 1.0), "dark", 0.0)
    for i in range(5):
        box(f"chev{i}", (0.14, 0.05, 0.56), (-0.42 + i * 0.21, -1.76, 1.0), "yellow" if i % 2 == 0 else "dark", 0.0, rot=(0, 0, 30))
    # ---- whip antenna with pennant, left rear
    box("antbase", (0.1, 0.1, 0.1), (-0.7, -1.2, 1.67), "dark", 0.0)
    cyl("antenna", 0.025, 1.0, (-0.7, -1.2, 2.15), "dark", "Z", 6)
    box("pennant", (0.02, 0.14, 0.08), (-0.7, -1.28, 2.6), "white", 0.0)
