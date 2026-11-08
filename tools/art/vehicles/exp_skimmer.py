"""Expedition Hover Skimmer (amphibious ash harvester) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Same hover style as the approved Glide Tank (D-149): ONE rubber skirt with a grey ash tidemark, a lifted navy hull, copper trim, signal-orange
lights, team colour on the hull stripes and the tail-fin flash, white hull number.  Harvester features: a wide flared copper suction scoop on
the prow, a chrome suction pipe along the deck into a big lagoon-white lead-lined hopper (orange band, chrome lid, dished top), one
rear turbine nacelle on a pylon with a single tall fin, an orange beacon.  No turret.  Footprint about 2.2 x 3.7 units before the model scale.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_skimmer"
TITLE = "Expedition Hover Skimmer"
K = 0.78
LIFT = 0.14
CANVAS = 128
TARGET = (0.0, 0.0, 0.8 * K)
STATES = [("idle", 16, 1, 1)]
PALETTE = E.PALETTE
MATERIALS = dict(E.MATERIALS)


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph, cone = S.box, S.cyl, S.sphere, S.cone
    S.begin("body")
    E.skirt(S, 2.2, 3.7, 0.0, 0.42, LIFT)
    S.lift(LIFT)
    box("hullL", (1.95, 3.3, 0.44), (0, 0.0, 0.66), "navy2", 0.16)
    box("hullU", (1.7, 2.8, 0.3), (0, -0.1, 0.98), "navy", 0.14)
    box("prow", (1.7, 0.9, 0.38), (0, 1.55, 0.88), "navy", 0.13, rot=(-22, 0, 0))
    box("bumper", (1.9, 0.1, 0.1), (0, 1.98, 0.52), "chrome", 0.03)
    for s in (-1, 1):
        box(f"trim{s}", (0.06, 2.7, 0.06), (s * 0.86, -0.1, 1.0), "copper", 0.0)
        box(f"stripe{s}", (0.05, 2.5, 0.14), (s * 1.0, -0.05, 0.68), "house", 0.0)
        box(f"sill{s}", (0.07, 2.9, 0.05), (s * 1.0, -0.05, 0.88), "copper", 0.0)
        E.lamp(S, f"L{s}", s * 0.62, 2.0, 0.7)
    for s in (-1, 1):                                                                        # S16 team colour: top stripes on the foredeck
        box(f"topstripe{s}", (0.14, 1.3, 0.045), (s * 0.62, 0.7, 1.14), "house", 0.0)
    # suction scoop: flared copper trough on the prow with a dark mouth and a ribbed hose to the hopper
    box("scoop", (1.5, 0.75, 0.3), (0, 2.1, 0.5), "copper", 0.1, rot=(14, 0, 0))
    box("scoopmouth", (1.3, 0.1, 0.2), (0, 2.5, 0.46), "dark", 0.03, rot=(14, 0, 0))
    box("scooplip", (1.6, 0.1, 0.07), (0, 2.46, 0.36), "chrome", 0.02)
    for s in (-1, 1):
        box(f"scoopfin{s}", (0.06, 0.7, 0.24), (s * 0.76, 2.1, 0.58), "copper", 0.0)
    cyl("hose", 0.2, 1.9, (0, 1.1, 1.22), "chrome2", "Y", 14)
    for q in range(5):
        cyl(f"hosering{q}", 0.225, 0.07, (0, 0.35 + q * 0.36, 1.22), "copper", "Y", 14)
    box("hoseblend", (0.5, 0.5, 0.3), (0, 1.75, 1.0), "chrome2", 0.1, rot=(-20, 0, 0))
    # lead-lined hopper
    cyl("hopper", 0.78, 0.7, (0, -0.55, 1.5), "white", "Z", 24)
    cyl("hopband", 0.8, 0.12, (0, -0.55, 1.35), "orange", "Z", 24)
    cyl("hopband2", 0.8, 0.07, (0, -0.55, 1.66), "copper", "Z", 24)
    sph("hoplid", 0.74, (0, -0.55, 1.86), "chrome", scale=(1, 1, 0.42))
    cyl("hophatch", 0.26, 0.08, (0, -0.55, 2.0), "chrome2", "Z", 14)
    sph("hopnub", 0.07, (0, -0.55, 2.06), "orange")
    for s in (-1, 1):
        box(f"hopflash{s}", (0.05, 0.9, 0.22), (s * 0.79, -0.55, 1.5), "house", 0.0)       # team colour flash on the hopper wall
        box(f"hopstrap{s}", (0.1, 0.1, 0.78), (s * 0.77, 0.02, 1.5), "chrome2", 0.0)
    E.digits(S, "hn", "02", 0, 1.45, 0.98, 0.07, "white")
    # tail: pylon, nacelle and one tall fin with a team flash
    box("pylon", (0.5, 0.9, 0.7), (0, -1.55, 1.25), "navy2", 0.1)
    E.nacelle(S, "", 0, -1.78, 1.55, 0.42, 1.0)
    box("fin", (0.09, 1.1, 1.2), (0, -1.45, 2.15), "navy", 0.03)
    box("finfl", (0.1, 0.65, 0.4), (0, -1.5, 2.3), "house", 0.0)
    box("fintip", (0.11, 0.28, 0.08), (0, -1.45, 2.75), "orange", 0.0)
    box("tailbar", (1.7, 0.12, 0.08), (0, -1.85, 0.98), "chrome2", 0.0)
    cyl("beacon", 0.09, 0.14, (0.55, -0.95, 1.95), "lens", "Z", 10)
    S.lift(0)
