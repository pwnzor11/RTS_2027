"""Expedition Landing Craft (beachable transport) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

A boat: sits IN the water (foam collar, bow wave, short stern wake, no skirt).  Blunt shallow-draft navy hull with a grey ash tidemark, an open
cargo well with orange deck chevrons (room for two vehicles), a raised chrome-hinged bow ramp, and an armoured crew compartment aft with a
white roof, glass slits, a small radome mast and a chrome stack.  Team colour: hull stripe, compartment flashes.  Hull number "07" on the
compartment roof.  Draw on water only.  Footprint about 2.6 x 5.2 units.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E
import exp_parts_art_exp_b as P

ID = "exp_landing_craft"
TITLE = "Expedition Landing Craft"
CANVAS = 192
TARGET = (0.0, 0.0, 0.5)
STATES = [("idle", 16, 1, 1)]
PALETTE = E.PALETTE
MATERIALS = P.BOAT_MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    P.waterline(S, 2.6, 5.0, 0.0, bow=0.1, stern=0.5)
    P.hull_bottom_band(S, 2.5, 4.9)
    box("hull", (2.5, 4.9, 0.7), (0, 0.0, 0.5), "navy2", 0.14)
    for s in (-1, 1):
        box(f"wall{s}", (0.22, 4.6, 0.5), (s * 1.12, -0.05, 0.98), "navy", 0.07)
        box(f"coam{s}", (0.1, 4.6, 0.06), (s * 1.12, -0.05, 1.25), "copper", 0.0)
        box(f"stripe{s}", (0.05, 3.9, 0.17), (s * 1.27, -0.1, 0.66), "house", 0.0)
        box(f"sill{s}", (0.06, 4.5, 0.05), (s * 1.27, -0.05, 0.9), "copper", 0.0)
        box(f"bowcheek{s}", (0.8, 0.9, 0.66), (s * 0.85, 2.3, 0.5), "navy2", 0.14, rot=(0, 0, -s * 24))
    # cargo well
    box("well", (2.0, 3.0, 0.06), (0, 0.5, 0.86), "dark", 0.0)
    for q in range(3):
        for s in (-1, 1):
            box(f"chev{q}{s}", (0.7, 0.12, 0.02), (s * 0.4, 0.95 + q * 0.75 - 0.0, 0.9), "orange", 0.0, rot=(0, 0, s * 25))
    for s in (-1, 1):
        box(f"lash{s}", (0.05, 2.8, 0.05), (s * 0.9, 0.5, 0.92), "chrome2", 0.0)
    # bow ramp (raised door)
    box("ramp", (2.25, 0.16, 1.1), (0, 2.38, 1.15), "navy", 0.05, rot=(-8, 0, 0))
    box("rampband", (2.0, 0.05, 0.2), (0, 2.46, 1.2), "orange", 0.0, rot=(-8, 0, 0))
    box("rampnum", (1.0, 0.04, 0.05), (0, 2.47, 1.5), "white", 0.0, rot=(-8, 0, 0))
    box("rampbar", (2.3, 0.1, 0.08), (0, 2.4, 1.72), "chrome", 0.02)
    for s in (-1, 1):
        cyl(f"hinge{s}", 0.1, 0.2, (s * 1.1, 2.35, 1.0), "chrome", "X", 10)
        cyl(f"latch{s}", 0.07, 0.14, (s * 0.8, 2.45, 1.72), "copper", "X", 8)
    # crew compartment aft
    box("cabin", (2.3, 1.3, 0.95), (0, -1.75, 1.3), "navy", 0.12)
    box("cabroof", (2.1, 1.1, 0.05), (0, -1.75, 1.8), "white", 0.01)
    box("cabroofs", (2.1, 0.07, 0.055), (0, -1.45, 1.8), "orange", 0.0)
    E.digits(S, "hn", "07", 0, -1.85, 1.83, 0.075, "white")
    box("cabfront", (2.1, 0.1, 0.6), (0, -1.1, 1.3), "navy2", 0.04, rot=(-8, 0, 0))
    box("slit", (1.6, 0.05, 0.2), (0, -1.09, 1.45), "glass", 0.0)
    for s in (-1, 1):
        box(f"cabflash{s}", (0.05, 1.0, 0.24), (s * 1.17, -1.75, 1.3), "house", 0.0)
        box(f"cabslit{s}", (0.05, 0.9, 0.14), (s * 1.17, -1.75, 1.52), "glass", 0.0)
    # mast with radome, stack, lamps
    cyl("mast", 0.04, 1.0, (0.7, -2.0, 2.3), "chrome2", "Z", 6)
    sph("radome", 0.24, (0.7, -2.0, 2.85), "white", scale=(1, 1, 0.8))
    cyl("stack", 0.18, 0.5, (-0.7, -2.2, 2.05), "chrome", "Z", 14)
    cyl("stackband", 0.2, 0.08, (-0.7, -2.2, 2.2), "orange", "Z", 14)
    cyl("stacktop", 0.15, 0.05, (-0.7, -2.2, 2.3), "dark", "Z", 12)
    E.lamp(S, "bow", 0, 2.42, 0.62, 0.1)
    cyl("beacon", 0.07, 0.12, (-0.2, -2.0, 1.9), "lens", "Z", 8)
