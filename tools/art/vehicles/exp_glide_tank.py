"""Expedition Glide Tank, VARIANT A (fast hover medium tank, stabilised guided cannon) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

A = low, wide hover hull with twin tail fins.  A long rubber skirt (grey ash tidemark) carries a navy hull with a lagoon-white rear deck,
copper edge trim, a sloped prow with signal-orange landing lights and a chrome bumper rail; two turbine nacelles and two canted fins at
the tail.  Team colour: the long hull-side stripes and the flash on each fin.  The white hull number is on the prow.
On the turret layer (pivot on the deck): a flat wedge turret with a chrome guided cannon (copper collars), an orange guidance pod and
a small glass hatch bubble.  Footprint about 2.5 x 3.9 units.  Original design.
Layers: body (hull + whole-vehicle shadow), turret (own sheet).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_glide_tank"
TITLE = "Expedition Glide Tank (variant A)"
K = 0.86          # whole-model scale: skirt footprint ~ Authority tank +10 %
LIFT = 0.14       # hull floats this far above the skirt
CANVAS = 128
TARGET = (0.0, 0.0, 0.9 * K)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT_U = (0.0, 0.2, 1.22 + LIFT)                                  # unscaled model coordinates (turret parts are built from these)
PIVOT = tuple(v * K for v in PIVOT_U)
MOUNT = dict(deck_z=(1.17 + LIFT) * K, r_lip=0.72 * K, r_well=0.0, r_shadow=0.95 * K, soft_r=0.9 * K, lip="chrome2", well="dark")
PALETTE = E.PALETTE
MATERIALS = E.MATERIALS


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    E.skirt(S, 2.5, 3.8, 0.0, 0.42, LIFT)
    S.lift(LIFT)
    box("hullL", (2.2, 3.4, 0.46), (0, 0.0, 0.66), "navy2", 0.16)
    box("hullU", (1.9, 2.9, 0.34), (0, -0.05, 1.0), "navy", 0.15)
    box("prow", (1.9, 1.0, 0.42), (0, 1.6, 0.9), "navy", 0.14, rot=(-24, 0, 0))
    box("prowlow", (2.1, 0.9, 0.34), (0, 1.75, 0.6), "navy2", 0.12, rot=(-16, 0, 0))
    box("bumper", (2.0, 0.1, 0.1), (0, 2.02, 0.52), "chrome", 0.03)
    for s in (-1, 1):
        box(f"trim{s}", (0.06, 2.8, 0.06), (s * 0.97, -0.05, 1.02), "copper", 0.0)
        box(f"stripe{s}", (0.05, 2.7, 0.15), (s * 1.12, -0.05, 0.7), "house", 0.0)           # team colour hull stripe
        box(f"sill{s}", (0.07, 3.0, 0.05), (s * 1.12, -0.05, 0.9), "copper", 0.0)
        E.lamp(S, f"L{s}", s * 0.55, 2.05, 0.72)                                             # landing lights
        box(f"vent{s}", (0.35, 0.5, 0.04), (s * 0.55, -0.9, 1.19), "chrome2", 0.0)           # intake grilles
    box("deckw", (1.0, 1.5, 0.04), (0, -0.85, 1.18), "white", 0.01)
    box("deckstripe", (1.0, 0.07, 0.045), (0, -0.2, 1.18), "orange", 0.0)
    E.digits(S, "hn", "04", 0, 0.78, 1.18, 0.075, "white")
    for s in (-1, 1):
        E.nacelle(S, f"{s}", s * 0.7, -1.82, 1.05, 0.38, 0.95)
        box(f"fin{s}", (0.09, 1.15, 1.05), (s * 1.0, -1.5, 1.6), "navy", 0.03, rot=(0, s * 9, 0))
        box(f"finfl{s}", (0.1, 0.7, 0.35), (s * 1.045, -1.55, 1.7), "house", 0.0, rot=(0, s * 9, 0))
        box(f"fintip{s}", (0.11, 0.3, 0.08), (s * 1.09, -1.5, 2.12), "orange", 0.0, rot=(0, s * 9, 0))
        box(f"finrib{s}", (0.13, 0.9, 0.05), (s * 0.99, -1.5, 1.14), "chrome", 0.0)
    box("tailbar", (1.9, 0.12, 0.08), (0, -2.0, 1.0), "chrome2", 0.0)
    # radio whip
    cyl("whip", 0.02, 1.4, (-0.55, -1.2, 1.8), "chrome2", "Z", 6)
    sph("whipknob", 0.045, (-0.55, -1.2, 2.5), "orange")
    S.lift(0)
    S0.mount(PIVOT, **MOUNT)
    S0.begin("turret", PIVOT)
    px, py, pz = PIVOT_U
    cyl("ring", 0.72, 0.1, (px, py, pz + 0.05), "chrome", "Z", 24)
    box("tb", (1.45, 1.6, 0.42), (px, py - 0.05, pz + 0.3), "navy", 0.16)
    box("tfront", (1.35, 0.7, 0.38), (px, py + 0.75, pz + 0.3), "navy", 0.14, rot=(-26, 0, 0))
    box("troof", (1.0, 1.0, 0.04), (px, py - 0.25, pz + 0.53), "white", 0.01)
    box("troofstripe", (1.0, 0.05, 0.045), (px, py + 0.1, pz + 0.53), "orange", 0.0)
    for s in (-1, 1):
        box(f"tflash{s}", (0.05, 1.0, 0.16), (px + s * 0.74, py - 0.05, pz + 0.3), "house", 0.0)    # team colour turret flash
        box(f"ttrim{s}", (0.05, 1.35, 0.05), (px + s * 0.72, py - 0.05, pz + 0.5), "copper", 0.0)
    box("mantlet", (0.5, 0.25, 0.34), (px, py + 1.08, pz + 0.28), "chrome2", 0.06)
    cyl("cannon", 0.085, 2.0, (px, py + 2.05, pz + 0.28), "chrome", "Y", 14)
    cyl("collar1", 0.13, 0.16, (px, py + 1.4, pz + 0.28), "copper", "Y", 14)
    cyl("collar2", 0.115, 0.1, (px, py + 2.3, pz + 0.28), "copper", "Y", 14)
    cyl("mouth", 0.07, 0.04, (px, py + 3.06, pz + 0.28), "dark", "Y", 10)
    box("pod", (0.34, 0.55, 0.3), (px + 0.5, py + 0.35, pz + 0.68), "orange", 0.09)                  # guidance pod
    sph("podeye", 0.1, (px + 0.5, py + 0.66, pz + 0.7), "glass")
    sph("hatch", 0.3, (px - 0.35, py - 0.45, pz + 0.55), "glass", scale=(1, 1, 0.65))
    cyl("hatchring", 0.31, 0.06, (px - 0.35, py - 0.45, pz + 0.54), "chrome", "Z", 16)
