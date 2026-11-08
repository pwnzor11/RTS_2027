"""Expedition Destroyer (all-round warship) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Slim navy hull with a red boot-top and white hull number 12, one tier of white bridge with glass band, a raked funnel with the team-colour
band (S16 pilot: plus house deck bands, a turret roof band), lattice mast with radar, a fixed aft twin gun and a rotating twin gun turret on the fore deck (turret layer).  No draw scale (1.0),
so about 1.3 cells long.  Water only, small bow wave.  Original design.  Layers: body (hull + shadow), turret.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E
import exp_parts_art_exp_c as P

ID = "exp_destroyer"
TITLE = "Expedition Destroyer"
K = 1.3 * 3.4 / 8.0
CANVAS = 128
TARGET = (0.0, 0.0, 0.45 * K)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PALETTE = P.PALETTE
MATERIALS = P.MATERIALS
L, B, H = 8.0, 1.4, 0.75
D = H + 0.03
PIVOT_U = (0.0, 2.3, D + 0.1)
PIVOT = tuple(v * K for v in PIVOT_U)
MOUNT = dict(deck_z=D * K, r_lip=0.45 * K, r_well=0.0, r_shadow=0.6 * K, soft_r=0.43 * K, lip="chrome2", well="dark")


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    P.hull(S, L, B, H, boot="orange2", band=0.08, num="12", numy=0.3, bowfrac=0.34)
    P.fixed_turret(S, "a", 0, -2.5, D, 2, 180, r=0.3, gl=0.8)
    # S16 pilot: team colour must read from above on every facing -> house deck bands (bow, aft)
    for nm, yy, ww, ll in (("bow", 3.2, 0.8, 0.3), ("aft", -2.0, 1.0, 0.26)):
        box(f"deckband_{nm}", (ww, ll, 0.04), (0, yy, D + 0.012), "house", 0.0)
    P.block(S, "br", 0, 0.9, D, 0.95, 1.5, 0.4, bevel=0.08)
    P.block(S, "br2", 0, 1.0, D + 0.4, 0.75, 0.8, 0.28, bevel=0.07)
    box("wing", (1.3, 0.16, 0.05), (0, 1.35, D + 0.55), "white2", 0.01)
    P.funnel(S, "a", 0, -0.2, D, w=0.6, ln=0.9, h=0.95, band=0.24, rake=-8)
    P.mast(S, "m", 0, 0.95, D + 0.68, 0.95)
    box("deckhouse", (0.8, 1.0, 0.3), (0, -1.3, D + 0.15), "white", 0.06)
    for s in (-1, 1):
        box(f"tube{s}", (0.14, 0.7, 0.12), (s * 0.4, -0.95, D + 0.36), "chrome2", 0.03)         # depth-charge racks
        box(f"boatd{s}", (0.2, 0.6, 0.14), (s * 0.5, 0.35, D + 0.2), "orange", 0.05)
    S0.mount(PIVOT, **MOUNT)
    S0.begin("turret", PIVOT)
    P.turret_parts(S, PIVOT_U[0], PIVOT_U[1], PIVOT_U[2], 2, 0.4, 1.2)
    px, py, pz = PIVOT_U
    box("troofband", (0.52, 0.3, 0.035), (px, py - 0.22, pz + 0.41), "house", 0.0)                 # S16 pilot: team band on the turret roof
    for s in (-1, 1):
        box(f"tflashx{s}", (0.05, 0.62, 0.17), (px + s * 0.37, py - 0.02, pz + 0.25), "house", 0.0)  # taller side flash
