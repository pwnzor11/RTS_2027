"""Expedition Patrol Launch (light scout boat) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

A boat: sits IN the water (foam collar, bow wave, big stern wake, no skirt).  A slim, fast navy hull with a sharp bow, ash tidemark band, copper sheer,
a small white wheelhouse with glass, a searchlight on the roof, a twin machine gun on a fixed pintle forward (no turret layer; the rules give the
Launch no turret), a depth-charge rack on the stern with orange charges, a whip aerial.  Team colour: hull stripe, wheelhouse flash.  Hull number
"06" on the foredeck.  Draw on water only.  Footprint about 1.4 x 4.2 units.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E
import exp_parts_art_exp_b as P

ID = "exp_launch"
TITLE = "Expedition Patrol Launch"
CANVAS = 128
TARGET = (0.0, 0.0, 0.5)
STATES = [("idle", 16, 1, 1)]
PALETTE = E.PALETTE
MATERIALS = P.BOAT_MATERIALS


def build(S0, state, k, n):
    S = E.Scaled(S0, 0.9)
    box, cyl, sph, cone = S.box, S.cyl, S.sphere, S.cone
    S.begin("body")
    P.waterline(S, 1.5, 3.6, -0.2, bow=0.2, stern=0.5)
    P.hull_bottom_band(S, 1.45, 3.5, -0.2, 0.2)
    box("hull", (1.4, 3.4, 0.55), (0, -0.2, 0.42), "navy2", 0.12)
    for s in (-1, 1):
        box(f"bow{s}", (0.9, 1.5, 0.55), (s * 0.28, 1.9, 0.42), "navy2", 0.12, rot=(0, 0, s * 26))
        box(f"stripe{s}", (0.05, 3.0, 0.14), (s * 0.71, -0.3, 0.5), "house", 0.0)
        box(f"sheer{s}", (0.06, 3.2, 0.05), (s * 0.71, -0.2, 0.7), "copper", 0.0)
    box("deck", (1.3, 3.0, 0.06), (0, -0.25, 0.7), "navy", 0.02)
    box("bowdeck", (0.8, 1.6, 0.06), (0, 1.6, 0.7), "navy", 0.02)
    box("bowtip", (0.4, 0.8, 0.06), (0, 2.5, 0.68), "navy", 0.02)
    box("foredeck", (0.7, 0.9, 0.05), (0, 1.2, 0.72), "white", 0.01)
    E.digits(S, "hn", "06", 0, 1.2, 0.76, 0.07, "white")
    # wheelhouse
    box("house", (1.1, 1.1, 0.55), (0, -0.2, 1.0), "white", 0.1)
    box("houseroof", (1.0, 1.0, 0.05), (0, -0.2, 1.3), "navy", 0.01)
    box("glassF", (0.9, 0.06, 0.2), (0, 0.37, 1.05), "glass", 0.0)
    for s in (-1, 1):
        box(f"glassS{s}", (0.05, 0.7, 0.18), (s * 0.56, -0.2, 1.08), "glass", 0.0)
        box(f"hflash{s}", (0.05, 0.9, 0.12), (s * 0.56, -0.2, 0.86), "house", 0.0)
    # searchlight + whip
    cyl("lampbase", 0.1, 0.18, (0.0, -0.1, 1.42), "chrome2", "Z", 10)
    cyl("search", 0.17, 0.3, (0.0, 0.05, 1.57), "chrome", "Y", 12)
    cyl("searchlens", 0.14, 0.03, (0.0, 0.21, 1.57), "lens", "Y", 12)
    cyl("whip", 0.02, 1.3, (0.4, -0.6, 1.95), "chrome2", "Z", 6)
    sph("whipknob", 0.04, (0.4, -0.6, 2.6), "orange")
    # twin machine gun forward on a pintle
    cyl("pintle", 0.08, 0.3, (0, 0.95, 0.86), "chrome2", "Z", 8)
    box("mgblock", (0.3, 0.3, 0.2), (0, 0.98, 1.05), "chrome2", 0.04)
    for s in (-1, 1):
        cyl(f"mg{s}", 0.035, 0.75, (s * 0.07, 1.4, 1.07), "chrome", "Y", 8)
        cyl(f"mgm{s}", 0.05, 0.1, (s * 0.07, 1.8, 1.07), "dark", "Y", 8)
    # stern depth-charge rack
    box("rack", (0.9, 0.7, 0.1), (0, -1.65, 0.8), "chrome2", 0.02)
    for s in (-1, 1):
        cyl(f"dc{s}", 0.17, 0.5, (s * 0.25, -1.65, 0.98), "orange", "Y", 12)
        cyl(f"dcb{s}", 0.18, 0.07, (s * 0.25, -1.65, 0.98), "copper", "Y", 12)
    box("sternplate", (1.3, 0.08, 0.3), (0, -1.95, 0.7), "navy", 0.02)
