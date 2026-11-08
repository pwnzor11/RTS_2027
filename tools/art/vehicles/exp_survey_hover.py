"""Expedition Survey Hover (fast scout) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Glide Tank hover style, much smaller (model scale 0.62): a slim pointed navy hull with a bubble canopy for the crew of two, a chart-table deck
in lagoon white, a tall sensor mast with a copper dish and orange beacon, a sampling boom with a probe at the prow, and a single tail fin with a
team flash.  Team colour: hull stripe, fin flash.  Hull number "04".  Footprint about 1.7 x 3.2 units before the scale.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_survey_hover"
TITLE = "Expedition Survey Hover"
K = 0.68
LIFT = 0.14
CANVAS = 128
TARGET = (0.0, 0.0, 0.9 * K)
STATES = [("idle", 16, 1, 1)]
PALETTE = E.PALETTE
MATERIALS = dict(E.MATERIALS)


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    E.skirt(S, 1.8, 3.3, 0.0, 0.42, LIFT)
    S.lift(LIFT)
    box("hullL", (1.55, 3.0, 0.4), (0, 0, 0.64), "navy2", 0.15)
    box("hullU", (1.3, 2.3, 0.28), (0, -0.2, 0.94), "navy", 0.13)
    box("prow", (1.3, 0.9, 0.34), (0, 1.5, 0.84), "navy", 0.12, rot=(-24, 0, 0))
    box("bumper", (1.5, 0.09, 0.09), (0, 1.9, 0.5), "chrome", 0.03)
    for s in (-1, 1):
        box(f"stripe{s}", (0.05, 2.5, 0.13), (s * 0.79, 0, 0.66), "house", 0.0)
        box(f"sill{s}", (0.06, 2.6, 0.05), (s * 0.79, 0, 0.84), "copper", 0.0)
        E.lamp(S, f"L{s}", s * 0.45, 1.93, 0.68, 0.09)
    for s in (-1, 1):                                                                        # S16 team colour: top stripes
        box(f"topstripe{s}", (0.12, 1.7, 0.04), (s * 0.58, -0.1, 1.1), "house", 0.0)
    box("deckw", (1.1, 1.0, 0.04), (0, -0.7, 1.1), "white", 0.01)           # chart table deck
    box("deckst", (1.1, 0.06, 0.045), (0, -0.2, 1.1), "orange", 0.0)
    E.digits(S, "hn", "04", 0, 0.75, 1.1, 0.065, "white")
    sph("canopy", 0.52, (0, 0.2, 1.2), "glass", scale=(1, 1.3, 0.75))
    cyl("canring", 0.5, 0.06, (0, 0.2, 1.1), "chrome", "Z", 16)
    # mast + dish + beacon
    cyl("mast", 0.05, 1.5, (0.3, -0.7, 1.85), "chrome2", "Z", 8)
    cyl("dishs", 0.34, 0.07, (0.3, -0.7, 2.4), "copper", "Y", 16, rot=(65, 0, 0))
    sph("dishhub", 0.07, (0.3, -0.7, 2.4), "chrome")
    cyl("mast2", 0.025, 0.7, (-0.35, -0.8, 1.5), "chrome", "Z", 6)
    sph("beacon", 0.07, (-0.35, -0.8, 1.88), "orange")
    # sampling boom
    box("boom", (0.07, 1.0, 0.07), (0, 2.3, 0.6), "chrome2", 0.0)
    cone = S.cone
    cone("probe", 0.09, 0.0, 0.35, (0, 2.95, 0.6), "orange", 8, rot=(-90, 0, 0))
    # tail
    E.nacelle(S, "", 0, -1.7, 1.05, 0.33, 0.9)
    box("fin", (0.08, 1.0, 0.95), (0, -1.35, 1.55), "navy", 0.03)
    box("finfl", (0.09, 0.6, 0.3), (0, -1.4, 1.65), "house", 0.0)
    box("fintip", (0.1, 0.26, 0.07), (0, -1.35, 2.0), "orange", 0.0)
    S.lift(0)
