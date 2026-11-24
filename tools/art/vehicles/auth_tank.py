"""Authority medium tank for tools/art/build_vehicle.py: the geometry of tools/art/build_tank.py (the approved v2 tank) plus a "move" state.
Contract: see auth_jeep.py.

States: idle (32 facings, 1 frame; geometry identical to build_tank.py) and move (32 facings x 4 frames).  In "move" the road wheels
carry three dark bolts that roll (one cycle = 120 deg, 30 deg per frame) and a ring of track links (cleats) runs round the track loop
(13 links; one cycle = one link pitch, a quarter of it per frame).  The hull, skirts, turret and ground origin are identical in every
frame, so nothing jitters; only wheels and links change.  STEP_PX = ground distance (sheet px) of one move frame: the renderer advances
a frame per that much travel, so the links do not slide on the ground.
"""
import math

ID = "auth_tank"
TITLE = "Authority medium tank"
CANVAS = 96                       # 120 px at 80 px per cell (1.5 cells), as the approved tank
TARGET = (0.0, 0.5, 0.7)
STATES = [("idle", 16, 1, 1), ("move", 16, 4, 1)]
MATERIALS = {"link": ("#3A3E33", dict(rough=0.8))}

WHEEL_R = 0.3
LINKS = 13
RUN = 2.7                         # straight run between the idler centres
IDLER_R = 0.4
LOOP = 2 * RUN + 2 * math.pi * IDLER_R
PITCH = LOOP / LINKS              # world units of one link
CELL = 3.4
STEP_PX = {"move": round(PITCH / 4 / CELL * 80, 2)}   # sheet px of ground travel per move frame (80 px per cell)


def link_pose(s):
    """Point and outward tangent angle of arc length s along the track loop, (y, z, tilt_deg).  Loop: top run front-to-back is
    skipped; s=0 at the top of the front idler, going over the nose, down the bottom run to the rear idler and back over its top."""
    r, h, zc = IDLER_R, RUN / 2, 0.4
    arc = math.pi * r
    s %= LOOP
    if s < arc:                                   # front idler, top -> bottom
        a = s / r
        return h + r * math.sin(a), zc + r * math.cos(a), math.degrees(a)
    s -= arc
    if s < RUN:                                   # bottom run, front -> rear
        return h - s, zc - r, 180.0
    s -= RUN
    if s < arc:                                   # rear idler, bottom -> top
        a = s / r
        return -h - r * math.sin(a), zc - r * math.cos(a), 180.0 + math.degrees(a)
    s -= arc                                      # top run, rear -> front
    return -h + s, zc + r, 0.0


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    move = state == "move"
    for s, side in ((-1, "L"), (1, "R")):
        x = s * 1.07
        box("track" + side, (0.62, 2.7, 0.7), (x, 0, 0.4), "track", 0.12)
        cyl("trkF" + side, 0.4, 0.62, (x, 1.35, 0.4), "track", "X")
        cyl("trkR" + side, 0.4, 0.62, (x, -1.35, 0.4), "track", "X")
        for i in range(6):
            y = -1.25 + i * 0.5
            cyl(f"wheel{side}{i}", 0.3, 0.1, (x + s * 0.33, y, 0.38), "steel", "X", 18)
            cyl(f"hub{side}{i}", 0.12, 0.08, (x + s * 0.39, y, 0.38), "rust", "X", 10)
            cyl(f"hubc{side}{i}", 0.045, 0.1, (x + s * 0.4, y, 0.38), "dark", "X", 6)
            if move:      # three rolling bolts on the wheel face; the wheel rolls forward (+y), top towards the front
                for j in range(3):
                    a = math.radians(120 * j + 30 * k)
                    cyl(f"bolt{side}{i}{j}", 0.06, 0.05, (x + s * 0.375, y + 0.2 * math.sin(a), 0.38 + 0.2 * math.cos(a)), "dark", "X", 6)
        cyl("sprocket" + side, 0.3, 0.1, (x + s * 0.33, 1.4, 0.45), "rust", "X", 12)
        box("skirt" + side, (0.08, 2.9, 0.44), (x + s * 0.34, 0.0, 0.84), "hull", 0.03)
        box("stripe" + side, (0.1, 2.6, 0.3), (x + s * 0.355, 0.0, 0.86), "house", 0.0)
        box("fender" + side, (0.66, 3.0, 0.07), (x, 0, 1.03), "hull2", 0.03)
        if move:      # track links: thin plates round the loop, the whole ring shifted forward by k/4 of a pitch
            for i in range(LINKS):
                y, z, tilt = link_pose(i * PITCH + k * PITCH / 4)
                box(f"link{side}{i}", (0.6, PITCH * 0.55, 0.05), (x, y, z), "link", 0.0, rot=(-tilt, 0, 0))
    box("hull", (1.78, 3.0, 0.62), (0, 0, 0.82), "hull", 0.1)
    box("glacis", (1.78, 1.05, 0.13), (0, 1.5, 1.02), "hull", 0.05, rot=(-30, 0, 0))
    box("nose", (1.78, 0.5, 0.4), (0, 1.62, 0.72), "hull2", 0.06)
    box("rearslope", (1.7, 0.55, 0.1), (0, -1.5, 1.05), "hull2", 0.04, rot=(22, 0, 0))
    box("rearplate", (1.78, 0.08, 0.5), (0, -1.52, 0.8), "hull2", 0.02)
    box("deck", (1.5, 2.2, 0.16), (0, -0.15, 1.2), "hull2", 0.05)
    for i in range(4):
        box(f"vent{i}", (0.6, 0.07, 0.04), (0, -1.38 + i * 0.12, 1.3), "dark", 0.0)
    box("toolbox", (0.34, 0.5, 0.18), (-0.58, -0.7, 1.37), "rust", 0.03)
    box("shovel", (0.16, 0.9, 0.04), (0.62, -0.6, 1.32), "steel", 0.0)
    cyl("slbase", 0.07, 0.15, (0.58, 0.95, 1.38), "dark", "Z", 8)
    cyl("slbody", 0.13, 0.22, (0.58, 1.02, 1.54), "steel", "Y", 12)
    cyl("sllens", 0.1, 0.04, (0.58, 1.14, 1.54), "lens", "Y", 12)
    ry = -1.15
    cyl("react", 0.4, 0.78, (0, ry, 1.6), "light", "Z", 28)
    for i in range(5):
        cyl(f"rib{i}", 0.5, 0.05, (0, ry, 1.48 + i * 0.1), "steel", "Z", 28)
    cyl("rcap", 0.3, 0.06, (0, ry, 2.02), "dark", "Z", 20)
    cyl("rpipe", 0.05, 0.3, (0.22, ry, 2.1), "steel", "Z", 8)
    cyl("rband", 0.42, 0.05, (0, ry, 1.3), "rust", "Z", 28)
    ty = -0.1
    box("turret", (1.5, 1.6, 0.5), (0, ty, 1.54), "hull", 0.07)
    box("turtop", (1.1, 1.1, 0.06), (0, ty - 0.05, 1.8), "hull2", 0.03)
    for s in (-1, 1):
        box("panel" + ("L" if s < 0 else "R"), (0.05, 1.32, 0.34), (s * 0.765, ty, 1.53), "house", 0.0)
    box("mantlet", (0.62, 0.36, 0.38), (0, ty + 0.9, 1.6), "steel", 0.07)
    cyl("gunsleeve", 0.18, 0.7, (0, ty + 1.3, 1.6), "steel", "Y", 16)
    cyl("gun", 0.12, 1.2, (0, ty + 1.6, 1.6), "dark", "Y", 16)
    box("brake", (0.34, 0.5, 0.3), (0, ty + 2.2, 1.6), "steel", 0.05)
    for i in range(3):
        box(f"slot{i}", (0.36, 0.05, 0.16), (0, ty + 2.06 + i * 0.12, 1.6), "dark", 0.0)
    cyl("cupolaring", 0.23, 0.12, (-0.38, ty - 0.4, 1.88), "hull2", "Z", 16)
    cyl("cupola", 0.17, 0.06, (-0.38, ty - 0.4, 1.95), "steel", "Z", 16)
    cyl("cupolahandle", 0.04, 0.12, (-0.38, ty - 0.4, 2.02), "dark", "Z", 6)
    box("hatchrim", (0.46, 0.46, 0.06), (0.38, ty - 0.15, 1.85), "hull2", 0.02)
    box("hatch", (0.36, 0.36, 0.05), (0.38, ty - 0.15, 1.89), "steel", 0.02)
    box("hatchbar", (0.2, 0.05, 0.04), (0.38, ty - 0.15, 1.93), "dark", 0.0)
