"""Expedition Supply Tender (convoy freighter) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

A boat: sits IN the water (foam collar, bow wave, stern wake, no skirt).  Long navy hull with a pointed bow, grey ash tidemark, copper sheer line,
two cargo hatches with orange crate stacks, a copper-and-chrome derrick, and a 1960s ocean-liner style white two-tier bridge aft with glass
strips, a squat chrome funnel (orange band + team stripe) and a radar.  Team colour: hull stripe, funnel stripe, bridge flash.  White hull number
"09" on the bow plate.  Draw on water only.  Footprint about 2.3 x 6.4 units.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E
import exp_parts_art_exp_b as P

ID = "exp_supply_tender"
TITLE = "Expedition Supply Tender"
CANVAS = 192
TARGET = (0.0, 0.0, 0.7)
STATES = [("idle", 16, 1, 1)]
PALETTE = E.PALETTE
MATERIALS = P.BOAT_MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    P.waterline(S, 2.3, 5.6, -0.2, bow=0.3, stern=0.7)
    P.hull_bottom_band(S, 2.2, 5.4, -0.2)
    box("hull", (2.2, 4.4, 0.8), (0, -0.7, 0.55), "navy2", 0.14)
    for s in (-1, 1):
        box(f"bow{s}", (1.4, 1.9, 0.8), (s * 0.5, 2.0, 0.55), "navy2", 0.14, rot=(0, 0, s * 28))
        box(f"bowdeck{s}", (1.15, 1.6, 0.05), (s * 0.42, 1.95, 0.97), "navy", 0.01, rot=(0, 0, s * 28))
    box("deck", (2.0, 4.0, 0.06), (0, -0.7, 0.97), "navy", 0.02)
    box("prowtip", (0.5, 0.9, 0.8), (0, 2.75, 0.55), "navy2", 0.14)
    for s in (-1, 1):
        box(f"stripe{s}", (0.05, 4.0, 0.17), (s * 1.12, -0.7, 0.72), "house", 0.0)
        box(f"sheer{s}", (0.06, 4.2, 0.05), (s * 1.12, -0.7, 1.0), "copper", 0.0)
        box(f"bulw{s}", (0.07, 4.2, 0.22), (s * 1.0, -0.7, 1.1), "navy", 0.02)
    E.digits(S, "hn", "09", 0, 2.1, 1.02, 0.07, "white")
    # cargo hatches and crates
    for q, yy in enumerate((1.2, -0.45)):
        box(f"hatch{q}", (1.5, 1.2, 0.2), (0, yy, 1.12), "white2", 0.04)
        box(f"hatchc{q}", (1.5, 0.06, 0.23), (0, yy, 1.12), "orange", 0.0)
    box("crateA", (0.55, 0.55, 0.45), (-0.35, -0.45, 1.5), "copper", 0.05)
    box("crateB", (0.5, 0.5, 0.4), (0.4, -0.5, 1.48), "orange", 0.05)
    box("crateC", (0.5, 0.5, 0.35), (-0.3, 1.2, 1.45), "orange2", 0.05)
    box("crateD", (0.45, 0.5, 0.3), (0.35, 1.2, 1.42), "chrome2", 0.05)
    # derrick between the hatches
    cyl("post", 0.12, 1.9, (0, 0.4, 1.95), "chrome", "Z", 10)
    cyl("postcollar", 0.16, 0.1, (0, 0.4, 1.12), "copper", "Z", 10)
    box("boom", (0.1, 2.1, 0.1), (0, 1.35, 2.55), "copper", 0.0, rot=(-22, 0, 0))
    box("boomtie", (0.04, 1.9, 0.04), (0, 0.15, 2.4), "chrome2", 0.0, rot=(18, 0, 0))
    sph("blocktop", 0.09, (0, 2.35, 2.2), "orange")
    # aft bridge (two tiers) and funnel
    box("deck1", (1.9, 1.7, 0.6), (0, -2.1, 1.3), "white", 0.1)
    box("deck2", (1.6, 1.2, 0.5), (0, -2.0, 1.85), "white", 0.1)
    box("bridgeglass", (1.45, 0.06, 0.18), (0, -1.43, 1.95), "glass", 0.0)
    box("glass1", (1.6, 0.06, 0.16), (0, -1.26, 1.4), "glass", 0.0)
    box("roof", (1.5, 1.1, 0.05), (0, -2.0, 2.13), "navy", 0.01)
    box("roofstripe", (1.5, 0.06, 0.055), (0, -1.6, 2.13), "orange", 0.0)
    for s in (-1, 1):
        box(f"brflash{s}", (0.05, 1.0, 0.18), (s * 0.82, -2.0, 1.85), "house", 0.0)
        box(f"brglass{s}", (0.05, 0.9, 0.12), (s * 0.82, -2.0, 1.99), "glass", 0.0)
        box(f"wing{s}", (0.4, 0.6, 0.05), (s * 1.05, -1.7, 1.99), "chrome2", 0.0)
    cyl("funnel", 0.3, 0.9, (0, -2.7, 2.25), "chrome", "Z", 16)
    cyl("funband", 0.32, 0.18, (0, -2.7, 2.5), "orange", "Z", 16)
    cyl("funstripe", 0.325, 0.07, (0, -2.7, 2.28), "house", "Z", 16)
    cyl("funtop", 0.26, 0.05, (0, -2.7, 2.72), "dark", "Z", 14)
    cyl("mast", 0.04, 0.9, (0, -1.95, 2.6), "chrome2", "Z", 6)
    box("radar", (0.8, 0.07, 0.14), (0, -1.95, 3.1), "chrome", 0.02)
    cyl("beacon", 0.07, 0.12, (0, -1.95, 3.2), "lens", "Z", 8)
    E.lamp(S, "bow", 0, 3.2, 0.8, 0.09)
