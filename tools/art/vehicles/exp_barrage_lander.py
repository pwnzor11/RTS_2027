"""Expedition Barrage Lander (tracked rocket artillery) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

The one tracked Expedition vehicle: two chunky rubber track pods (chrome wheels, copper-trimmed navy fenders), a low navy hull with a lagoon-white
deck, a sloped ramp-friendly prow with orange lights, and a big chrome pontoon-style nose bar for loading onto a landing craft.  Turret layer
(pivot on the deck): a wide navy launcher base with a tilted chrome pod of 2 x 3 rocket tubes with orange nose caps and copper bands, a spotter
glass bubble.  Team colour: hull stripe on the track fenders, turret flashes.  Hull number "02".  Footprint about 2.6 x 4.0 units.
Layers: body (hull + shadow), turret (own sheet).
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E
import exp_parts_art_exp_b as P

ID = "exp_barrage_lander"
TITLE = "Expedition Barrage Lander"
K = 0.82
CANVAS = 128
TARGET = (0.0, 0.0, 0.9 * K)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT_U = (0.0, -0.2, 1.1)
PIVOT = tuple(v * K for v in PIVOT_U)
MOUNT = dict(deck_z=1.05 * K, r_lip=0.75 * K, r_well=0.0, r_shadow=1.0 * K, soft_r=0.9 * K, lip="chrome2", well="dark")
PALETTE = E.PALETTE
MATERIALS = dict(E.MATERIALS)


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    P.tracks(S, 2.6, 4.0, 0.0, 0.55)
    box("hullL", (1.7, 3.6, 0.5), (0, 0, 0.62), "navy2", 0.15)
    box("hullU", (1.8, 3.0, 0.3), (0, -0.2, 0.88), "navy", 0.14)
    box("prow", (1.7, 1.0, 0.4), (0, 1.7, 0.72), "navy", 0.13, rot=(-26, 0, 0))
    box("nosebar", (1.9, 0.14, 0.14), (0, 2.1, 0.5), "chrome", 0.04)
    for s in (-1, 1):
        box(f"stripe{s}", (0.3, 2.6, 0.04), (s * 1.05, 0.0, 0.7), "house", 0.0)      # team colour strip on each track fender
        box(f"trim{s}", (0.06, 2.8, 0.06), (s * 0.9, -0.2, 1.04), "copper", 0.0)
        E.lamp(S, f"L{s}", s * 0.45, 2.17, 0.68)
    box("deckw", (1.5, 1.2, 0.04), (0, 0.6, 1.04), "white", 0.01)
    box("deckst", (1.5, 0.07, 0.045), (0, 0.15, 1.04), "orange", 0.0)
    E.digits(S, "hn", "02", 0, 0.85, 1.04, 0.07, "white")
    box("tail", (1.7, 0.2, 0.34), (0, -2.0, 0.85), "navy2", 0.06)
    box("tailbar", (1.6, 0.1, 0.07), (0, -2.12, 0.98), "orange", 0.0)
    box("deckhouse", (1.5, 0.35, 0.045), (0, 1.5, 0.98), "house", 0.0, rot=(-26, 0, 0))
    cyl("whip", 0.02, 1.3, (0.7, -1.7, 1.7), "chrome2", "Z", 6)
    sph("whipknob", 0.045, (0.7, -1.7, 2.35), "orange")
    S0.mount(PIVOT, **MOUNT)
    S0.begin("turret", PIVOT)
    px, py, pz = PIVOT_U
    cyl("ring", 0.75, 0.1, (px, py, pz + 0.05), "chrome", "Z", 22)
    box("base", (1.6, 1.8, 0.4), (px, py, pz + 0.28), "navy", 0.15)
    box("baseroof", (1.2, 1.4, 0.04), (px, py - 0.1, pz + 0.5), "white", 0.01)
    for s in (-1, 1):
        box(f"tflash{s}", (0.05, 1.2, 0.16), (px + s * 0.82, py, pz + 0.28), "house", 0.0)
        box(f"ttrim{s}", (0.05, 1.5, 0.05), (px + s * 0.8, py, pz + 0.48), "copper", 0.0)
        box(f"yoke{s}", (0.12, 1.2, 0.55), (px + s * 0.66, py + 0.1, pz + 0.7), "chrome2", 0.03)
    # rocket pod: tilted block of 2 x 3 tubes, pointing forward and up
    t = 20
    box("pod", (1.2, 2.2, 0.8), (px, py + 0.5, pz + 0.95), "chrome2", 0.08, rot=(t, 0, 0))
    for c in range(3):
        for r in range(2):
            xx = px + (c - 1) * 0.36
            zz = pz + 0.78 + r * 0.42
            cyl(f"tube{c}{r}", 0.15, 0.1, (xx, py + 1.62, zz + 0.35), "dark", "Y", 12, rot=(90 + t, 0, 0))
            cyl(f"cap{c}{r}", 0.11, 0.07, (xx, py + 1.55, zz + 0.33), "orange", "Y", 12, rot=(90 + t, 0, 0))
    box("podtop", (1.1, 1.9, 0.05), (px, py + 0.4, pz + 1.38), "copper", 0.0, rot=(t, 0, 0))
    box("podband", (1.25, 0.12, 0.84), (px, py + 0.0, pz + 0.92), "copper", 0.03, rot=(t, 0, 0))
    sph("spotter", 0.26, (px - 0.5, py - 0.65, pz + 0.55), "glass", scale=(1, 1, 0.7))
    cyl("spring", 0.27, 0.06, (px - 0.5, py - 0.65, pz + 0.5), "chrome", "Z", 14)
