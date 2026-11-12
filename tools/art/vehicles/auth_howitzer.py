"""Authority Field Howitzer (tracked long-range artillery) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A low, open-topped tracked carriage (same olive hulls, tracks, house stripe and finish as the pilot tank, smaller than the Bulwark) that
carries one very long gun.  The turret layer is the gun: a rotating gun platform with a thick recoil cradle, a long slender barrel
raised about 25 degrees with a bulb muzzle brake, a low splinter shield with house panels, and an ammo crate on the back.  Hull: engine
deck at the back, a crew bench, a rear recoil spade folded up, a stencilled white range tally.
Layers: body (carriage, tracks, mount, whole-vehicle shadow) + turret (gun).  Turret contract: build_vehicle.py header.
House paint: the skirt stripe on each side, the platform ring, the platform side wings, the gun-shield panels, a stripe on the cradle.
"""
import math

ID = "auth_howitzer"
TITLE = "Authority Field Howitzer"
CANVAS = 160
TARGET = (0.0, 0.3, 1.6)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
DECK = 1.22
PIVOT = (0.0, -0.1, 1.3)
MOUNT = dict(deck_z=DECK, r_lip=0.6, r_well=0.0, r_shadow=0.85, shadow_z=DECK + 0.02, soft_r=0.65, lip="hull2")


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    for s, side in ((-1, "L"), (1, "R")):
        x = s * 1.12
        box("track" + side, (0.66, 2.9, 0.7), (x, 0, 0.4), "track", 0.12)
        cyl("trkF" + side, 0.4, 0.66, (x, 1.45, 0.4), "track", "X")
        cyl("trkR" + side, 0.4, 0.66, (x, -1.45, 0.4), "track", "X")
        for i in range(6):
            y = -1.25 + i * 0.5
            cyl(f"wheel{side}{i}", 0.3, 0.1, (x + s * 0.35, y, 0.38), "steel", "X", 18)
            cyl(f"hub{side}{i}", 0.12, 0.08, (x + s * 0.41, y, 0.38), "rust", "X", 10)
        cyl("sprocket" + side, 0.3, 0.1, (x + s * 0.35, -1.45, 0.45), "rust", "X", 12)
        box("skirt" + side, (0.08, 2.7, 0.4), (x + s * 0.36, 0.0, 0.8), "hull", 0.03)
        box("stripe" + side, (0.1, 2.3, 0.26), (x + s * 0.38, 0.0, 0.82), "house", 0.0)
        box("fender" + side, (0.7, 3.0, 0.07), (x, 0, 1.0), "hull2", 0.03)
    # carriage: low hull, sloped nose, engine deck at the rear, crew bench at the front
    box("hull", (1.9, 2.8, 0.5), (0, 0, 0.8), "hull", 0.08)
    box("deck", (1.8, 2.6, 0.12), (0, -0.05, DECK - 0.06), "hull2", 0.04)
    box("glacis", (1.9, 0.8, 0.12), (0, 1.35, 1.02), "hull", 0.05, rot=(-26, 0, 0))
    box("rearplate", (1.9, 0.08, 0.5), (0, -1.45, 0.82), "hull2", 0.02)
    for i in range(5):
        box(f"vent{i}", (0.8, 0.07, 0.04), (0, -1.2 + i * 0.12, DECK + 0.0), "dark", 0.0)
    box("engine", (1.2, 0.6, 0.2), (0, -1.15, DECK + 0.12), "hull", 0.05)
    box("bench", (1.3, 0.4, 0.14), (0, 0.95, DECK + 0.08), "rust", 0.04)
    box("benchb", (1.3, 0.08, 0.3), (0, 0.78, DECK + 0.2), "rust", 0.03)
    for s in (-1, 1):
        box(f"num{s}", (0.04, 0.5, 0.12), (s * 0.97, 0.9, DECK - 0.05), "white", 0.0)
        cyl(f"lamp{s}", 0.09, 0.06, (s * 0.7, 1.7, 0.9), "lens", "Y", 10)
    box("spade", (1.3, 0.12, 0.6), (0, -1.58, 1.15), "steel", 0.04, rot=(40, 0, 0))
    box("spadearm", (0.2, 0.5, 0.12), (0.5, -1.45, 1.0), "hull2", 0.02)
    box("spadearm2", (0.2, 0.5, 0.12), (-0.5, -1.45, 1.0), "hull2", 0.02)
    S.mount(PIVOT, **MOUNT)
    # ---- turret layer: gun platform, cradle, long raised barrel, shield
    S.begin("turret", PIVOT)
    px, py, pz = PIVOT
    cyl("platform", 0.7, 0.14, (px, py, pz + 0.07), "hull2", "Z", 20)
    cyl("platring", 0.73, 0.07, (px, py, pz + 0.1), "house", "Z", 20)
    box("trunnionL", (0.16, 0.8, 0.6), (px - 0.4, py + 0.1, pz + 0.45), "hull", 0.04)
    box("trunnionR", (0.16, 0.8, 0.6), (px + 0.4, py + 0.1, pz + 0.45), "hull", 0.04)
    ang = 25.0
    a = math.radians(ang)
    cy0, cz0 = py + 0.3, pz + 0.7                    # breech end pivot
    def along(d, dz=0.0):
        return (px, cy0 + d * math.cos(a), cz0 + d * math.sin(a))
    box("breech", (0.46, 0.7, 0.46), along(-0.2), "steel", 0.06, rot=(ang, 0, 0))
    box("cradle", (0.5, 1.2, 0.4), along(0.5), "hull", 0.05, rot=(ang, 0, 0))
    box("cradlestripe", (0.54, 0.9, 0.12), along(0.5, 0.0), "house", 0.0, rot=(ang, 0, 0))
    cyl("recoil", 0.12, 1.3, along(0.75), "steel", "Y", 12, rot=(ang - 90, 0, 0))
    cyl("barrel", 0.11, 2.8, along(2.0), "dark", "Y", 12, rot=(ang - 90, 0, 0))
    cyl("brake", 0.19, 0.4, along(3.4), "steel", "Y", 12, rot=(ang - 90, 0, 0))
    for i in range(3):
        cyl(f"brakering{i}", 0.21, 0.05, along(3.25 + i * 0.13), "dark", "Y", 12, rot=(ang - 90, 0, 0))
    # splinter shield on the front of the platform with house panels
    box("shield", (1.5, 0.1, 0.62), (px, py + 0.7, pz + 0.35), "hull", 0.04, rot=(-12, 0, 0))
    for s in (-1, 1):
        box(f"shpanel{s}", (0.5, 0.06, 0.4), (px + s * 0.5, py + 0.74, pz + 0.38), "house", 0.0, rot=(-12, 0, 0))
        box(f"sidewing{s}", (0.08, 0.9, 0.34), (px + s * 0.72, py + 0.2, pz + 0.24), "house", 0.0)
    box("crate", (0.6, 0.5, 0.34), (px, py - 0.55, pz + 0.3), "rust", 0.04)
    box("crateband", (0.62, 0.1, 0.36), (px, py - 0.55, pz + 0.3), "yellow", 0.0)
    cyl("hatch", 0.14, 0.05, (px - 0.4, py - 0.3, pz + 0.17), "steel", "Z", 10)
