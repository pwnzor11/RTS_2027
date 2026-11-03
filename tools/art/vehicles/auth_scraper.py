"""Authority armoured scraper (ashfall harvester) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Tracked boxy hull, front scraper blade + inclined conveyor up to a rear hopper full of grey-black ash, left-hand cab,
reactor exhaust stack, beacon.  Big house panels: hull flanks, cab doors, hopper sides, blade top plate.
States: idle (16 facings x 1) and harvest (16 facings x 4: conveyor cleats run, blade shudders, ash puff, beacon blinks).
The frame is 1.75 cells wide so the blade and stack may overhang the cell; the scale is still 64 px per cell.
"""
import math

ID = "auth_scraper"
TITLE = "Authority armoured scraper (ashfall harvester)"
CANVAS = 112
TARGET = (0.0, 0.2, 0.9)
STATES = [("idle", 16, 1, 1), ("harvest", 16, 4, 3)]


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    harvest = state == "harvest"
    ph = k / n if harvest else 0.0
    shake = (0.0, 0.035, 0.0, -0.035)[k % 4] if harvest else 0.0
    # ---- tracks: wide, with road wheels and big idlers
    for s, side in ((-1, "L"), (1, "R")):
        x = s * 1.22
        box("track" + side, (0.72, 3.1, 0.82), (x, -0.05, 0.45), "track", 0.14)
        cyl("idlF" + side, 0.45, 0.72, (x, 1.5, 0.47), "track", "X")
        cyl("idlR" + side, 0.45, 0.72, (x, -1.6, 0.47), "track", "X")
        for i in range(6):
            y = -1.3 + i * 0.52
            cyl(f"wh{side}{i}", 0.3, 0.1, (x + s * 0.37, y, 0.42), "steel", "X", 16)
            cyl(f"hb{side}{i}", 0.11, 0.08, (x + s * 0.43, y, 0.42), "rust", "X", 8)
        box("skirt" + side, (0.08, 3.1, 0.36), (x + s * 0.38, -0.05, 0.92), "hull2", 0.03)
    # ---- hull with big house flank panels
    box("hull", (2.0, 3.2, 0.7), (0, -0.05, 1.0), "hull", 0.1)
    box("deck", (2.0, 3.2, 0.1), (0, -0.05, 1.37), "hull2", 0.04)
    box("nose", (2.0, 0.4, 0.5), (0, 1.62, 0.95), "hull2", 0.06)
    box("rearplate", (2.0, 0.1, 0.62), (0, -1.65, 1.0), "hull2", 0.03)
    for s in (-1, 1):
        box(f"flank{s}", (0.06, 2.3, 0.5), (s * 1.02, -0.1, 1.06), "house", 0.0)
        for i in range(3):
            box(f"rivet{s}{i}", (0.08, 0.08, 0.5), (s * 1.02, -1.1 + 0.9 * i + 0.6, 1.06), "hull2", 0.0)
    # ---- cab (left) with house doors, glass, roof, beacon
    box("cab", (0.78, 1.1, 0.62), (-0.68, 0.75, 1.68), "hull", 0.07)
    box("cabroof", (0.84, 1.16, 0.07), (-0.68, 0.75, 2.02), "hull2", 0.03)
    box("cabdoor", (0.05, 0.8, 0.46), (-1.08, 0.7, 1.64), "house", 0.0)
    box("cabdoorR", (0.05, 0.8, 0.4), (-0.28, 0.7, 1.66), "house", 0.0)
    box("cabglass", (0.64, 0.04, 0.34), (-0.68, 1.31, 1.74), "glass", 0.0)
    box("cabglassS", (0.04, 0.6, 0.26), (-1.08, 1.0, 1.82), "glass", 0.0)
    box("cabroofstripe", (0.5, 0.7, 0.03), (-0.68, 0.75, 2.07), "house", 0.0)
    cyl("bcnbase", 0.07, 0.1, (-0.68, 0.45, 2.1), "dark", "Z", 8)
    cyl("beacon", 0.1, 0.14, (-0.68, 0.45, 2.2), "lens" if (not harvest or k % 2 == 0) else "lamp_off", "Z", 10)
    # ---- conveyor: front-low to rear-high between cab and stack
    p0, p1 = (0.0, 1.9, 1.0), (0.0, 0.0, 2.2)
    dy, dz = p1[1] - p0[1], p1[2] - p0[2]
    ln = math.hypot(dy, dz)
    ang = math.degrees(math.atan2(dz, -dy))            # pitch, front end down
    nrm = (0.0, math.sin(math.radians(ang)), math.cos(math.radians(ang)))
    cx, cy, cz = 0.05, (p0[1] + p1[1]) / 2, (p0[2] + p1[2]) / 2
    box("belt", (1.0, ln, 0.1), (cx, cy, cz), "dark", 0.02, rot=(-ang, 0, 0))
    for sx in (-1, 1):
        box(f"rail{sx}", (0.08, ln, 0.26), (cx + sx * 0.54, cy + nrm[1] * 0.1, cz + nrm[2] * 0.1), "steel", 0.02, rot=(-ang, 0, 0))
    for sx in (-1, 1):
        box(f"strut{sx}", (0.1, 0.12, 0.5), (cx + sx * 0.54, 0.55, 1.62), "steel", 0.0)
    ncl = 6
    for j in range(ncl):
        t = ((j + ph) % ncl) / ncl
        pos = (cx, p0[1] + t * dy, p0[2] + t * dz)
        box(f"cleat{j}", (0.9, 0.07, 0.1), (pos[0], pos[1] + nrm[1] * 0.1, pos[2] + nrm[2] * 0.1), "rust", 0.0, rot=(-ang, 0, 0))
        if harvest or j % 2 == 0:       # ash riding the belt
            tl = t + 0.5 / ncl
            if tl < 1.0:
                pl = (cx + (0.15 if j % 2 else -0.15), p0[1] + tl * dy, p0[2] + tl * dz)
                box(f"clump{j}", (0.26, 0.2, 0.14), (pl[0], pl[1] + nrm[1] * 0.17, pl[2] + nrm[2] * 0.17), "ash", 0.04, rot=(-ang, 0, 0))
    # ---- scraper blade / bucket at the front
    for dz_, nm in ((shake, "bl"),):
        box("hopmouth", (1.2, 0.5, 0.45), (0.05, 1.85, 0.62), "hull2", 0.06)
        box("chute", (1.05, 1.0, 0.1), (0.05, 2.1, 0.78 + dz_), "steel", 0.02, rot=(-48, 0, 0))
        box("blade", (2.7, 0.2, 0.62), (0, 2.2, 0.5 + dz_), "steel", 0.04, rot=(-18, 0, 0))
        box("bladeplate", (2.5, 0.05, 0.3), (0, 2.12, 0.78 + dz_), "house", 0.0, rot=(-18, 0, 0))
        box("edge", (2.7, 0.3, 0.07), (0, 2.38, 0.12 + dz_), "rust", 0.02, rot=(-18, 0, 0))
        for s in (-1, 1):
            box(f"bside{s}", (0.1, 0.9, 0.55), (s * 1.3, 1.95, 0.5 + dz_), "hull2", 0.03)
            box(f"barm{s}", (0.2, 1.3, 0.18), (s * 0.82, 1.65, 0.62), "steel", 0.03, rot=(-10, 0, 0))
    # ---- rear hopper bin full of ash
    for s in (-1, 1):
        box(f"hwall{s}", (0.1, 1.3, 0.75), (s * 0.95, -0.9, 1.78), "hull", 0.03)
        box(f"hside{s}", (0.04, 1.0, 0.45), (s * 1.02, -0.9, 1.72), "house", 0.0)
    box("hrear", (2.0, 0.1, 0.75), (0, -1.55, 1.78), "hull", 0.03)
    box("hfront", (1.9, 0.1, 0.5), (0, -0.25, 1.65), "hull2", 0.03)
    sph("ashmound", 1.0, (0, -0.9, 1.8), "ash", scale=(0.95, 0.62, 0.38))
    for i, (x, y, r) in enumerate(((-0.4, -1.1, 0.3), (0.35, -0.7, 0.28), (0.1, -1.3, 0.25), (-0.3, -0.55, 0.22))):
        sph(f"lump{i}", r, (x, y, 2.04 + 0.02 * i), "ash2", scale=(1, 1, 0.7))
    # ---- reactor stack with ribbed collar (right side), exhaust cap
    sx_, sy_ = 0.8, 0.5
    cyl("stk", 0.2, 1.3, (sx_, sy_, 2.0), "light", "Z", 18)
    for i in range(3):
        cyl(f"srib{i}", 0.25, 0.05, (sx_, sy_, 1.55 + i * 0.12), "steel", "Z", 18)
    cyl("scap", 0.25, 0.08, (sx_, sy_, 2.68), "dark", "Z", 18)
    cyl("sband", 0.22, 0.06, (sx_, sy_, 2.4), "rust", "Z", 18)
    box("shield", (0.5, 0.5, 0.06), (sx_, sy_, 1.46), "hull2", 0.02)
    # ---- ash puff (harvest): three puffs rising from the blade edge, drifting back
    if harvest:
        for i in range(3):
            t = (ph + i / 3.0) % 1.0
            r = 0.16 + 0.3 * t
            sph(f"puff{i}", r, (-0.5 + 0.5 * i + 0.1 * math.sin(t * 6), 2.5 - 0.35 * t, 0.35 + 1.0 * t), "puff", nocast=True)
    # ---- tool box on the deck
    box("toolbox", (0.34, 0.5, 0.2), (0.55, -0.15, 1.52), "rust", 0.03)
