"""Expedition Glide Tank, VARIANT B (fast hover medium tank, stabilised guided cannon) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

B = rounder, shorter hull with side turbine pods and a single tall tail fin, carrying a TALLER bubble-canopy turret with a radome:
a white drum under a navy-framed glass bubble, a radome ball on a mast at the rear and a slim long chrome cannon.  Same palette,
skirt, tidemark and hull number as A.  Team colour: hull-side stripes, the fin flash and the turret drum band.  Original design.
Layers: body (hull + whole-vehicle shadow), turret (own sheet).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_glide_tank_b"
TITLE = "Expedition Glide Tank (variant B)"
K = 0.86
LIFT = 0.14
CANVAS = 128
TARGET = (0.0, 0.0, 1.1 * K)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT_U = (0.0, 0.0, 1.32 + LIFT)
PIVOT = tuple(v * K for v in PIVOT_U)
MOUNT = dict(deck_z=(1.27 + LIFT) * K, r_lip=0.68 * K, r_well=0.0, r_shadow=0.9 * K, soft_r=0.85 * K, lip="chrome2", well="dark")
PALETTE = E.PALETTE
MATERIALS = E.MATERIALS


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    E.skirt(S, 2.4, 3.5, 0.0, 0.44, LIFT)
    S.lift(LIFT)
    box("hullL", (2.0, 3.1, 0.5), (0, 0.0, 0.7), "navy2", 0.22)
    box("hullU", (1.75, 2.7, 0.4), (0, 0.0, 1.07), "navy", 0.2)
    box("prow", (1.65, 0.9, 0.46), (0, 1.5, 0.98), "navy", 0.2, rot=(-20, 0, 0))
    box("bumper", (1.7, 0.1, 0.1), (0, 1.88, 0.56), "chrome", 0.03)
    for s in (-1, 1):
        box(f"stripe{s}", (0.05, 2.4, 0.16), (s * 1.02, -0.05, 0.74), "house", 0.0)
        box(f"trim{s}", (0.06, 2.5, 0.06), (s * 0.89, 0.0, 1.1), "copper", 0.0)
        E.lamp(S, f"L{s}", s * 0.5, 1.92, 0.76)
        E.nacelle(S, f"{s}", s * 1.3, -0.25, 0.95, 0.4, 1.6)                                  # side turbine pods
        box(f"pylon{s}", (0.5, 0.5, 0.12), (s * 1.05, -0.25, 0.92), "chrome2", 0.03)
    box("deckw", (1.1, 1.2, 0.04), (0, -0.85, 1.28), "white", 0.01)
    E.digits(S, "hn", "04", 0, 0.62, 1.28, 0.07, "white")
    box("fin", (0.1, 1.2, 1.3), (0, -1.45, 1.8), "navy", 0.04, rot=(-8, 0, 0))
    box("finfl", (0.12, 0.7, 0.4), (0, -1.5, 1.95), "house", 0.0, rot=(-8, 0, 0))
    box("fintip", (0.13, 0.3, 0.08), (0, -1.38, 2.5), "orange", 0.0)
    box("tailbar", (1.75, 0.12, 0.08), (0, -1.78, 1.05), "chrome2", 0.0)
    S.lift(0)
    S0.mount(PIVOT, **MOUNT)
    S0.begin("turret", PIVOT)
    px, py, pz = PIVOT_U
    cyl("ring", 0.68, 0.1, (px, py, pz + 0.05), "chrome", "Z", 24)
    cyl("drum", 0.6, 0.5, (px, py, pz + 0.35), "white", "Z", 24)
    cyl("drumband", 0.62, 0.1, (px, py, pz + 0.3), "house", "Z", 24)
    cyl("drumtrim", 0.62, 0.05, (px, py, pz + 0.52), "copper", "Z", 24)
    sph("bubble", 0.58, (px, py + 0.05, pz + 0.85), "glass", scale=(1, 1, 1.05))
    for a in (0, 60, 120):                                                                   # navy canopy frame ribs
        box(f"rib{a}", (0.05, 1.12, 0.05), (px, py + 0.05, pz + 1.35), "navy2", 0.0, rot=(0, 0, a))
    cyl("mastp", 0.04, 0.9, (px - 0.35, py - 0.55, pz + 1.0), "chrome2", "Z", 8)
    sph("radome", 0.3, (px - 0.35, py - 0.55, pz + 1.6), "white")
    cyl("radband", 0.3, 0.05, (px - 0.35, py - 0.55, pz + 1.6), "orange", "Z", 16)
    box("mantlet", (0.4, 0.3, 0.3), (px, py + 0.72, pz + 0.38), "chrome2", 0.06)
    cyl("cannon", 0.07, 2.4, (px, py + 1.9, pz + 0.38), "chrome", "Y", 14)
    cyl("extract", 0.12, 0.5, (px, py + 1.7, pz + 0.38), "copper", "Y", 14)
    cyl("mouth", 0.06, 0.04, (px, py + 3.12, pz + 0.38), "dark", "Y", 10)
