"""Expedition Cruiser (large gun ship, shore bombardment) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Broad navy hull with a charcoal boot-top, red pin stripe and white hull number 21, a two-tier liner superstructure with glass bands, a
fat funnel with the team-colour band, radar dome and mast, a fixed triple gun forward of the bridge and aft, and a rotating triple
gun turret on the bow (turret layer).  draw_scale_permille = 2000 (D-124/D-138): built about 1.2 cells long, drawn 2x in game.
Original design.  Layers: body (hull + shadow), turret.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E
import exp_parts_art_exp_c as P

ID = "exp_cruiser"
TITLE = "Expedition Cruiser"
K = 1.0 * 3.4 / 8.0
CANVAS = 128
TARGET = (0.0, 0.0, 0.5 * K)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PALETTE = P.PALETTE
MATERIALS = P.MATERIALS
L, B, H = 8.0, 2.4, 0.9
D = H + 0.03
PIVOT_U = (0.0, 2.65, D + 0.1)
PIVOT = tuple(v * K for v in PIVOT_U)
MOUNT = dict(deck_z=D * K, r_lip=0.66 * K, r_well=0.0, r_shadow=0.8 * K, soft_r=0.6 * K, lip="chrome2", well="dark")


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    P.hull(S, L, B, H, boot="dark", band=0.09, num="21", numy=0.3, bowfrac=0.3)
    for s in (-1, 1):
        box(f"redline{s}", (0.03, L * 0.6, 0.04), (s * (B / 2 + 0.035), -0.4, 0.13), "orange2", 0.0)
    P.fixed_turret(S, "f2", 0, 1.55, D, 3, 0, r=0.38, gl=1.0)
    P.fixed_turret(S, "a1", 0, -2.0, D, 3, 180, r=0.38, gl=1.0)
    P.fixed_turret(S, "a2", 0, -3.0, D, 2, 180, r=0.32, gl=0.8)
    P.block(S, "t1", 0, 0.0, D, 1.8, 2.4, 0.5, bevel=0.09)
    P.block(S, "t2", 0, 0.2, D + 0.5, 1.1, 1.4, 0.4, bevel=0.08)
    P.block(S, "br", 0, 0.65, D + 0.9, 0.85, 0.5, 0.28, bevel=0.06)
    box("wing", (1.5, 0.16, 0.05), (0, 0.7, D + 0.95), "white2", 0.01)
    P.funnel(S, "a", 0, -0.6, D + 0.5, w=0.75, ln=1.0, h=0.8, band=0.26, rake=-5)
    P.mast(S, "m", 0, 0.7, D + 1.18, 1.0)
    P.dome(S, "d", 0, -1.2, D + 0.5, 0.26)
    for s in (-1, 1):
        box(f"boat{s}", (0.2, 0.8, 0.14), (s * 0.55, -0.4, D + 0.57), "orange", 0.05)
    S0.mount(PIVOT, **MOUNT)
    S0.begin("turret", PIVOT)
    P.turret_parts(S, PIVOT_U[0], PIVOT_U[1], PIVOT_U[2], 3, 0.58, 1.6)
    px, py, pz = PIVOT_U
    box("troofband", (0.62, 0.3, 0.035), (px, py - 0.3, pz + 0.41), "house", 0.0)                  # S16 team colour: turret roof band
    for s in (-1, 1):
        box(f"tflashx{s}", (0.05, 0.8, 0.16), (px + s * 0.53, py - 0.02, pz + 0.25), "house", 0.0)
