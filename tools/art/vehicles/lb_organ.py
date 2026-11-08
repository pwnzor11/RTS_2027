"""Left Behind Organ (rocket-rack artillery) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Mismatched tracks under a low patched flatbed hull, a small faded-teal cab hunched at the front and, on a turret layer
(pivot on the middle of the deck), a tilted rack of rocket tubes of different lengths like organ pipes in rust, ash, bone and
teal, a canvas bonnet over the breech, a house-coloured side panel and a lashed spare tube.
Layers: body (hull + whole-vehicle shadow), turret (the rack).  Footprint about 3.0 x 5.0 units (M).
House paint: hull flank panels, the rack side panels, the cab roof stripe.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as L

ID = "lb_organ"
TITLE = "Left Behind Organ (rocket rack)"
CANVAS = 192
TARGET = (0.0, 0.0, 1.2)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT = (0.0, -0.6, 1.3)
MOUNT = dict(deck_z=1.18, r_lip=0.85, r_well=0.0, r_shadow=1.1, soft_r=1.0, lip="rust2", well="dark")
PALETTE = L.PALETTE
MATERIALS = L.MATERIALS
TILT = 32.0


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    L.tracks(S, -1.3, -2.4, 2.2, "L", "hull2", n=5)
    L.tracks(S, 1.3, -2.1, 2.6, "R", "rust", n=5)
    box("hull", (3.0, 4.9, 0.3), (0, -0.1, 1.15), "ash2", 0.04)
    box("deck", (2.7, 2.9, 0.12), (0, -1.0, 1.28), "wood", 0.03)
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"flank{nm}", (0.08, 2.4, 0.5), (s * 1.45, -1.0, 1.35), "rust" if s < 0 else "hull", 0.03)
        box(f"flankpanel{nm}", (0.06, 1.4, 0.35), (s * 1.5, -1.0, 1.38), "house", 0.0)
    box("flankpatch", (0.07, 0.7, 0.3), (-1.5, -2.0, 1.4), "ochre", 0.0)
    box("rearplate", (2.9, 0.1, 0.6), (0, -2.55, 1.3), "rust2", 0.03)
    # cab front
    L.cab(S, -0.5, 1.5, 1.3, w=1.7, d=1.4, h=1.0, body="hull", roof="rust2")
    box("cabstripe", (1.0, 0.9, 0.04), (-0.5, 1.5, 2.37), "house", 0.0)
    box("nose", (2.8, 0.9, 0.7), (0, 2.4, 1.4), "rust", 0.08)
    box("noselip", (2.9, 0.12, 0.2), (0, 2.88, 1.15), "hazard", 0.02)
    box("grille", (1.2, 0.05, 0.3), (0, 2.88, 1.5), "dark", 0.0)
    L.lamp(S, "L", -0.9, 2.9, 1.7)
    L.lamp(S, "R", 0.8, 2.9, 1.7)
    L.stack(S, "s", 0.7, 1.3, 2.3, 1.1)
    L.barrel(S, "a", 1.0, -2.0, 1.7, "hull", r=0.3, h=0.7, band="rust")
    L.flag(S, "m", -1.2, -2.4, 1.35, h=1.5)
    # ---- turret: tilted organ rack
    S.mount(PIVOT, **MOUNT)
    S.begin("turret", PIVOT)
    px, py, pz = PIVOT
    a = math.radians(TILT)
    ca, sa = math.cos(a), math.sin(a)

    def Q(lx, ly, lz):
        return (px + lx, py + ly * ca - lz * sa, pz + ly * sa + lz * ca)

    cyl("cradle", 0.8, 0.12, (px, py, pz + 0.06), "ash2", "Z", 20)
    box("hinge", (1.7, 0.3, 0.3), Q(0, -0.8, 0.15), "dark", 0.03, rot=(TILT, 0, 0))
    box("base", (1.6, 1.8, 0.3), Q(0, 0.0, 0.3), "rust2", 0.05, rot=(TILT, 0, 0))
    mats = ("rust", "ash", "bone", "hull", "rust", "ochre")
    lens = (1.9, 2.3, 2.7, 2.2, 2.5, 1.8)
    for i in range(6):
        for j in range(2):
            lx = -0.6 + i * 0.24
            ln = lens[(i + j * 3) % 6]
            cyl(f"pipe{i}{j}", 0.115, ln, Q(lx, 0.1 + ln / 2 - 0.6, 0.55 + j * 0.25), mats[(i + j * 2) % 6], rot=(TILT - 90, 0, 0), verts=8)
            cyl(f"mouth{i}{j}", 0.09, 0.05, Q(lx, 0.1 + ln - 0.6 + 0.02, 0.55 + j * 0.25), "dark", rot=(TILT - 90, 0, 0), verts=8)
    box("bonnet", (1.6, 0.9, 0.7), Q(0, -0.7, 0.75), "canvas", 0.18, rot=(TILT, 0, 0))
    box("bonnetstripe", (0.5, 0.92, 0.72), Q(0.5, -0.7, 0.75), "house", 0.1, rot=(TILT, 0, 0))
    for s in (-1, 1):
        box(f"rside{s}", (0.05, 1.0, 0.45), Q(s * 0.82, -0.1, 0.65), "house", 0.0, rot=(TILT, 0, 0))
        box(f"strap{s}", (0.06, 0.1, 0.8), Q(s * 0.82, 0.5, 0.65), "rope", 0.0, rot=(TILT, 0, 0))
    cyl("spare", 0.1, 2.0, Q(0.0, 0.7, 1.05), "steel", rot=(TILT - 90, 0, 0), verts=8)
