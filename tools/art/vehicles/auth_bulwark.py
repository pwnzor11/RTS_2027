"""Authority Bulwark heavy tank for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

The heaviest Authority tank: a wide, slab-sided hull plated like a bank vault (flat armour plates bolted over each other, rivet rows,
a big vault-wheel hatch on the turret roof) on very broad tracks, about a third wider and longer than auth_tank_wz.  Slab turret with a
thick long-recoil cannon (auth_tank_cannon) and a slotted brake.  Tracks are built procedurally the way the olive pilot tank builds them
(box + end rollers + road wheels, house stripe on the skirt); the M10 plan will swap them for Warzone tracks.
Layers: body (hull, tracks, mount, whole-vehicle shadow) + turret (turret, cannon).  Turret contract: build_vehicle.py header.
House paint: the skirt stripe on each side, the turret flank panels, a glacis flash.
"""
import math

ID = "auth_bulwark"
TITLE = "Authority Bulwark heavy tank"
CANVAS = 160
TARGET = (0.0, 0.1, 1.5)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
DECK = 1.62
PIVOT = (0.0, -0.15, 1.7)
MOUNT = dict(deck_z=DECK, r_lip=0.85, r_well=0.0, r_shadow=1.15, shadow_z=DECK + 0.02, soft_r=0.9, lip="hull2")


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    # ---- tracks: very broad, long, with raised front idler and rear sprocket, 7 road wheels, skirt plate with house stripe
    for s, side in ((-1, "L"), (1, "R")):
        x = s * 1.55
        box("track" + side, (0.95, 3.5, 0.85), (x, 0, 0.48), "track", 0.14)
        cyl("trkF" + side, 0.48, 0.95, (x, 1.75, 0.5), "track", "X")
        cyl("trkR" + side, 0.48, 0.95, (x, -1.75, 0.5), "track", "X")
        for i in range(7):
            y = -1.5 + i * 0.5
            cyl(f"wheel{side}{i}", 0.34, 0.1, (x + s * 0.48, y, 0.44), "steel", "X", 18)
            cyl(f"hub{side}{i}", 0.14, 0.08, (x + s * 0.54, y, 0.44), "rust", "X", 10)
        cyl("sprocket" + side, 0.34, 0.1, (x + s * 0.48, -1.78, 0.5), "rust", "X", 12)
        box("skirt" + side, (0.1, 3.3, 0.62), (x + s * 0.5, 0.0, 1.0), "hull", 0.04)
        box("stripe" + side, (0.12, 2.7, 0.3), (x + s * 0.53, 0.0, 1.02), "house", 0.0)
        for i in range(8):
            cyl(f"rivet{side}{i}", 0.04, 0.05, (x + s * 0.56, -1.45 + i * 0.4, 1.22), "steel", "X", 6)
        box("fender" + side, (0.98, 3.5, 0.08), (x, 0, 1.36), "hull2", 0.03)
    # ---- hull: slab-sided, shallow glacis, thick stacked plates (vault plating)
    box("hull", (2.4, 3.3, 0.95), (0, 0, 0.98), "hull", 0.1)
    box("deck", (2.3, 3.0, 0.2), (0, -0.05, DECK - 0.1), "hull2", 0.05)
    box("glacis", (2.4, 1.0, 0.16), (0, 1.55, 1.32), "hull", 0.06, rot=(-24, 0, 0))
    box("glaciscap", (2.2, 0.16, 0.5), (0, 1.8, 0.95), "hull2", 0.05)
    box("flash", (1.0, 0.6, 0.04), (0, 1.52, 1.5), "house", 0.0, rot=(-24, 0, 0))
    box("rearplate", (2.4, 0.1, 0.8), (0, -1.68, 1.0), "hull2", 0.03)
    for i in range(5):
        box(f"vent{i}", (0.9, 0.07, 0.04), (0, -1.35 + i * 0.12, DECK + 0.01), "dark", 0.0)
    for s in (-1, 1):
        box(f"plate{s}", (0.08, 2.4, 0.5), (s * 1.2, -0.1, 1.0), "hull2", 0.03)       # applied armour slab on the flank
        box(f"plate2{s}", (0.08, 1.6, 0.34), (s * 1.24, -0.1, 1.0), "hull", 0.02)
        box(f"tow{s}", (0.14, 0.3, 0.14), (s * 0.7, 1.82, 0.9), "steel", 0.02)
        cyl(f"lamp{s}", 0.1, 0.07, (s * 0.95, 1.8, 1.25), "lens", "Y", 10)
    box("toolbox", (0.5, 0.6, 0.22), (-0.75, -1.1, DECK + 0.1), "rust", 0.03)
    box("shovel", (0.2, 1.0, 0.04), (0.8, -0.9, DECK + 0.03), "steel", 0.0)
    # turret mount (collar + contact shadow)
    S.mount(PIVOT, **MOUNT)
    # ---- turret layer: slab turret, house flank panels, vault-wheel hatch, thick cannon
    S.begin("turret", PIVOT)
    px, py, pz = PIVOT
    box("turret", (1.95, 2.0, 0.78), (px, py + 0.05, pz + 0.39), "hull", 0.1)
    box("turfront", (1.95, 0.3, 0.7), (px, py + 1.1, pz + 0.38), "hull2", 0.1, rot=(-12, 0, 0))
    box("turtop", (1.5, 1.4, 0.07), (px, py - 0.05, pz + 0.8), "hull2", 0.03)
    box("turrear", (1.7, 0.4, 0.55), (px, py - 1.15, pz + 0.35), "hull2", 0.06)
    for s in (-1, 1):
        box(f"panel{s}", (0.06, 1.5, 0.5), (px + s * 0.99, py + 0.05, pz + 0.4), "house", 0.0)
        box(f"tplate{s}", (0.07, 0.8, 0.3), (px + s * 1.0, py - 0.9, pz + 0.55), "hull2", 0.02)
        cyl(f"smoke{s}", 0.09, 0.3, (px + s * 0.8, py + 0.85, pz + 0.95), "steel", "Y", 8)
    # vault-wheel hatch
    cyl("vaultring", 0.34, 0.1, (px + 0.4, py - 0.3, pz + 0.89), "steel", "Z", 20)
    cyl("vaultdisc", 0.26, 0.06, (px + 0.4, py - 0.3, pz + 0.96), "hull2", "Z", 20)
    for j in range(3):
        box(f"spoke{j}", (0.5, 0.05, 0.05), (px + 0.4, py - 0.3, pz + 1.0), "dark", 0.0, rot=(0, 0, 60 * j))
    cyl("cupola", 0.2, 0.14, (px - 0.5, py - 0.5, pz + 0.89), "steel", "Z", 14)
    cyl("cupolahandle", 0.04, 0.12, (px - 0.5, py - 0.5, pz + 1.0), "dark", "Z", 6)
    # cannon: massive mantlet, sleeve, barrel, slotted brake
    gz = pz + 0.45
    box("mantlet", (0.95, 0.4, 0.7), (px, py + 1.3, gz), "steel", 0.08)
    cyl("sleeve", 0.24, 0.9, (px, py + 1.85, gz), "steel", "Y", 16)
    cyl("barrel", 0.15, 1.6, (px, py + 2.5, gz), "dark", "Y", 16)
    box("brake", (0.4, 0.55, 0.36), (px, py + 3.2, gz), "steel", 0.05)
    for i in range(3):
        box(f"slot{i}", (0.42, 0.05, 0.2), (px, py + 3.06 + i * 0.13, gz), "dark", 0.0)
