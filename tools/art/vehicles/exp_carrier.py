"""Expedition Carrier (floating airbase) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Wide navy hull with a charcoal boot-top and white hull number 31 carrying a long grey flight deck with white centreline and orange edge
stripes, an angled-deck marking, an island on the starboard side (white bridge, funnel with the team-colour band, radar dome and mast),
and a rotating twin AA mount on the bow (turret layer).  draw_scale_permille = 2400 (D-124/D-138): built about 1.2 cells long, drawn
2.4x in game.  Original design.  Layers: body (hull + shadow), turret.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E
import exp_parts_art_exp_c as P

ID = "exp_carrier"
TITLE = "Expedition Carrier"
K = 1.08 * 3.4 / 8.0
CANVAS = 128
TARGET = (0.0, 0.0, 0.5 * K)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PALETTE = P.PALETTE
MATERIALS = P.MATERIALS
L, B, H = 8.0, 2.4, 0.95
D = H + 0.03
FD = D + 0.38                                   # flight deck height
PIVOT_U = (0.0, 3.0, D + 0.1)
PIVOT = tuple(v * K for v in PIVOT_U)
MOUNT = dict(deck_z=D * K, r_lip=0.42 * K, r_well=0.0, r_shadow=0.55 * K, soft_r=0.4 * K, lip="chrome2", well="dark")


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    P.hull(S, L, B, H, boot="dark", band=0.09, num="31", numy=0.0, bowfrac=0.22)
    for s in (-1, 1):
        box(f"redline{s}", (0.03, L * 0.6, 0.04), (s * (B / 2 + 0.035), -0.4, 0.13), "orange2", 0.0)
    # hangar sides and flight deck with sponsons
    box("hangar", (B - 0.1, 5.4, 0.38), (0, -0.7, D + 0.19), "navy2", 0.05)
    box("deck", (B + 0.5, 5.9, 0.07), (0, -0.7, FD + 0.035), "fdeck", 0.03)
    box("deckface", (B + 0.5, 0.05, 0.1), (0, -3.5, FD - 0.02), "dark", 0.0)
    for s in (-1, 1):
        box(f"edge{s}", (0.05, 5.7, 0.03), (s * (B / 2 + 0.17), -0.5, FD + 0.08), "orange", 0.0, nocast=True)
        box(f"spons{s}", (0.3, 5.0, 0.12), (s * (B / 2 + 0.08), -0.5, FD - 0.09), "navy2", 0.04)
    box("cline", (0.07, 5.4, 0.03), (0, -0.5, FD + 0.08), "white", 0.0, nocast=True)
    box("aline", (0.07, 3.4, 0.03), (-0.2, -1.1, FD + 0.08), "white", 0.0, nocast=True, rot=(0, 0, -7))
    for yy in (-3.0, -1.0, 0.8):
        box(f"stripe{yy}", (1.2, 0.07, 0.03), (0, yy, FD + 0.08), "white", 0.0, nocast=True)
    for yy, ww in ((-3.25, 1.5), (1.45, 1.3)):                                                    # S16 team colour: flight-deck bands
        box(f"fdband{yy}", (ww, 0.3, 0.03), (0, yy, FD + 0.075), "house", 0.0, nocast=True)
    for s in (-1, 1):
        box(f"sponstripe{s}", (0.12, 4.6, 0.03), (s * (B / 2 + 0.2), -0.5, FD + 0.075), "house", 0.0, nocast=True)
    cyl("padring", 0.45, 0.02, (0, 1.9, FD + 0.075), "orange", "Z", 20, nocast=True)
    box("lift", (0.8, 0.9, 0.03), (-0.1, -2.3, FD + 0.08), "orange2", 0.0, nocast=True)
    # island, starboard side
    xi = B / 2 - 0.05
    P.block(S, "isl", xi + 0.1, -0.6, FD, 0.65, 1.7, 0.5, bevel=0.07)
    P.block(S, "isl2", xi + 0.1, -0.35, FD + 0.5, 0.55, 0.9, 0.3, bevel=0.06)
    P.funnel(S, "a", xi + 0.1, -1.1, FD + 0.5, w=0.42, ln=0.7, h=0.45, band=0.17, rake=-5)
    P.mast(S, "m", xi + 0.1, -0.3, FD + 0.8, 0.85)
    P.dome(S, "d", xi + 0.1, -1.4, FD + 0.5, 0.2)
    S0.mount(PIVOT, **MOUNT)
    S0.begin("turret", PIVOT)
    P.turret_parts(S, PIVOT_U[0], PIVOT_U[1], PIVOT_U[2], 2, 0.34, 0.8)
    px, py, pz = PIVOT_U
    box("troofband", (0.5, 0.34, 0.035), (px, py - 0.17, pz + 0.41), "house", 0.0)                # S16 team colour: turret roof band
    for s in (-1, 1):
        box(f"tflashx{s}", (0.06, 0.55, 0.2), (px + s * 0.31, py - 0.02, pz + 0.25), "house", 0.0)
