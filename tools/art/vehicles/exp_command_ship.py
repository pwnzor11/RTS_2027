"""Expedition Command Ship (start unit; unfolds into the Landing Depot) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

A 1960s ocean-liner flagship: long navy hull with a charcoal-and-red boot-top and white hull number 01, three tiers of white upper works
with glass window bands, a tall bridge, a fat oval funnel carrying the team-colour band, radar dome and mast, a fixed twin gun turret on
the fore deck, an orange helipad ring aft and copper-trimmed folding ramp doors at the stern.  Drawn on water with a small bow wave.
Rules: draw_scale_permille = 1800 (D-124/D-138), so the sprite is built ~1.4 cells long and the game draws it 1.8x (about 2.5 cells).
No turret layer (has_turret = false).  Original design.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E
import exp_parts_art_exp_c as P

ID = "exp_command_ship"
TITLE = "Expedition Command Ship"
K = 1.4 * 3.4 / 8.0
CANVAS = 128
TARGET = (0.0, 0.0, 0.5 * K)
STATES = [("idle", 16, 1, 1)]
PALETTE = P.PALETTE
MATERIALS = P.MATERIALS
L, B, H = 8.0, 2.0, 0.92
D = H + 0.03            # deck level


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    P.hull(S, L, B, H, boot="dark", band=0.09, num="01", numy=0.4)
    for s in (-1, 1):                                                  # red boot-top pin stripe
        box(f"redline{s}", (0.03, L * 0.62, 0.04), (s * (B / 2 + 0.035), -0.5, 0.13), "orange2", 0.0)
    # fore deck: hatch, fixed twin gun, anchor winch
    P.fixed_turret(S, "f", 0, 2.55, D, 2, 0, r=0.34, gl=0.9)
    box("hatch1", (0.7, 0.5, 0.07), (0, 1.75, D + 0.035), "white2", 0.02)
    cyl("winch", 0.13, 0.12, (0, 3.2, D + 0.06), "copper", "Z", 10)
    # superstructure: three tiers, bridge, wings
    P.block(S, "t1", 0, -0.35, D, 1.7, 3.5, 0.5, bevel=0.1)
    P.block(S, "t2", 0, -0.45, D + 0.5, 1.45, 2.9, 0.46, bevel=0.1)
    P.block(S, "t3", 0, 0.1, D + 0.96, 1.15, 1.7, 0.42, bevel=0.09)
    P.block(S, "br", 0, 0.75, D + 1.38, 1.3, 0.55, 0.3, "white", bevel=0.06, win_mat="glass")
    box("brwing", (1.9, 0.18, 0.05), (0, 0.78, D + 1.4), "white2", 0.01)                    # open bridge wings
    box("roof", (1.0, 1.4, 0.05), (0, -0.05, D + 1.38), "white2", 0.02)
    for s in (-1, 1):
        box(f"deckcopper{s}", (0.04, 3.4, 0.04), (s * 0.86, -0.35, D + 0.5), "copper", 0.0)
        box(f"boat{s}", (0.22, 0.9, 0.16), (s * 0.95, -1.05, D + 0.58), "orange", 0.06)      # lifeboats
        box(f"davit{s}", (0.05, 0.1, 0.3), (s * 0.95, -0.55, D + 0.68), "chrome", 0.0)
    P.funnel(S, "a", 0, -0.95, D + 0.96, w=0.85, ln=1.15, h=0.85, band=0.26, cap="dark", rake=-4)
    P.mast(S, "m", 0, 0.9, D + 1.53, 0.9)
    P.dome(S, "d", 0, -1.7, D + 0.96, 0.3)
    # aft: helipad ring and stern ramp doors
    cyl("padring", 0.62, 0.025, (0, -3.0, D + 0.02), "orange", "Z", 24, nocast=True)
    cyl("padin", 0.5, 0.03, (0, -3.0, D + 0.025), "deck", "Z", 24, nocast=True)
    box("padH1", (0.08, 0.5, 0.035), (-0.18, -3.0, D + 0.04), "white", 0.0, nocast=True)
    box("padH2", (0.08, 0.5, 0.035), (0.18, -3.0, D + 0.04), "white", 0.0, nocast=True)
    box("padH3", (0.36, 0.08, 0.035), (0, -3.0, D + 0.04), "white", 0.0, nocast=True)
    box("ramp", (1.5, 0.14, 0.5), (0, -L / 2 + 0.02, 0.45), "copper", 0.03)
    box("rampseam", (0.04, 0.16, 0.5), (0, -L / 2 + 0.02, 0.45), "dark", 0.0)
    box("rampbar", (1.6, 0.1, 0.06), (0, -L / 2 + 0.04, H - 0.02), "chrome", 0.0)
