"""Expedition Minelayer (sea mine layer) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Deep-bellied navy hull with a red boot-top and white hull number 52, a small white liner-style bridge forward with a short funnel
carrying the team-colour band, a long open mine deck aft with two rails of spiked orange-and-charcoal mines and a copper-trimmed stern
ramp, a cargo derrick, mast and radar.  No turret (unarmed).  No draw scale (1.0), about 1.3 cells long.  Water only, small bow wave.
Original design.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E
import exp_parts_art_exp_c as P

ID = "exp_minelayer"
TITLE = "Expedition Minelayer"
K = 1.3 * 3.4 / 8.0
CANVAS = 128
TARGET = (0.0, 0.0, 0.4 * K)
STATES = [("idle", 16, 1, 1)]
PALETTE = P.PALETTE
MATERIALS = P.MATERIALS
L, B, H = 8.0, 1.7, 0.8
D = H + 0.03


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    P.hull(S, L, B, H, boot="orange2", band=0.09, num="52", numy=0.5, bowfrac=0.3)
    # bridge forward
    P.block(S, "br", 0, 1.9, D, 1.1, 1.4, 0.45, bevel=0.08)
    P.block(S, "br2", 0, 2.0, D + 0.45, 0.85, 0.8, 0.28, bevel=0.07)
    box("wing", (1.5, 0.16, 0.05), (0, 2.35, D + 0.58), "white2", 0.01)
    P.funnel(S, "a", 0, 1.0, D, w=0.6, ln=0.8, h=0.85, band=0.24, rake=-6)
    P.mast(S, "m", 0, 2.2, D + 0.73, 0.9)
    # mine deck: low bulwark, two rails of mines, centre track
    for s in (-1, 1):
        box(f"bw{s}", (0.05, 4.3, 0.18), (s * (B / 2 - 0.1), -1.3, D + 0.09), "navy2", 0.02)
        box(f"rail{s}", (0.06, 4.0, 0.04), (s * 0.38, -1.3, D + 0.04), "chrome2", 0.0)
    box("trk", (0.12, 4.0, 0.03), (0, -1.3, D + 0.03), "dark", 0.0)
    for i in range(7):
        yy = -3.0 + i * 0.55
        for s in (-1, 1):
            x = s * 0.38
            sph(f"mine{i}_{s}", 0.17, (x, yy, D + 0.2), "orange" if i % 2 == 0 else "orange2")
            cyl(f"minec{i}_{s}", 0.05, 0.12, (x, yy, D + 0.36), "chrome", "Z", 6)
            for a in range(4):
                an = a * math.pi / 2
                sph(f"spk{i}_{s}_{a}", 0.04, (x + math.cos(an) * 0.19, yy + math.sin(an) * 0.19, D + 0.2), "chrome", nocast=True)
    for nm, yy, ww, ll in (("bow", 3.1, 0.9, 0.3), ("aft", -3.65, 1.1, 0.26), ("mid", 0.25, 1.2, 0.26)):   # S16 team colour: deck bands
        box(f"deckband_{nm}", (ww, ll, 0.04), (0, yy, D + 0.012), "house", 0.0)
    box("ramp", (1.2, 0.2, 0.34), (0, -L / 2 + 0.05, D + 0.12), "copper", 0.03)
    box("rampbar", (1.3, 0.08, 0.05), (0, -L / 2 + 0.0, D + 0.3), "chrome", 0.0)
    # derrick
    cyl("dpost", 0.06, 0.8, (0.0, -0.35, D + 0.4), "chrome2", "Z", 8)
    box("dboom", (0.06, 1.3, 0.06), (0, -0.85, D + 0.8), "chrome2", 0.0, rot=(-28, 0, 0))
