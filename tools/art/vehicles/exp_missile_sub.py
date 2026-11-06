"""Expedition Missile Submarine (surfaced look; the game fades it when dived) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Cigar hull in navy with a charcoal boot-top and red waterline, a white 1960s liner-style fin (sail) with a glass band, a team-colour
band round the fin and a white hull number 41, a periscope and mast, a row of six white missile hatches with copper rims on the aft deck,
a thin chrome deck rail.  Drawn low on water with a small bow wave.  No turret.  State "dived" (frames after idle) is a periscope-only
frame: only the periscope and a short wake above the water.  No draw scale (1.0), about 1.3 cells long.  Original design.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E
import exp_parts_art_exp_c as P

ID = "exp_missile_sub"
TITLE = "Expedition Missile Submarine"
K = 1.3 * 3.4 / 8.0
CANVAS = 128
TARGET = (0.0, 0.0, 0.3 * K)
STATES = [("idle", 16, 1, 1), ("dived", 16, 1, 1)]
PALETTE = P.PALETTE
MATERIALS = P.MATERIALS
L, R = 8.0, 0.62


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    if state == "dived":
        cyl("peri", 0.04, 0.5, (0, 0.8, 0.1), "chrome2", "Z", 8)
        box("perihead", (0.1, 0.16, 0.08), (0, 0.84, 0.36), "chrome", 0.01)
        sph("perieye", 0.04, (0, 0.93, 0.36), "glass")
        sph("pfoam", 1.0, (0, 0.8, 0.02), "foam", (0.2, 0.16, 0.025), nocast=True)
        sph("pwake", 1.0, (0, 0.2, 0.02), "foam", (0.14, 0.6, 0.02), nocast=True)
        return
    ln = L - 2 * R * 1.0
    sph("hullbow", 1.0, (0, L / 2 - R * 1.5, 0.12), "navy", (R, R * 1.5, R * 0.62))
    sph("hullst", 1.0, (0, -L / 2 + R * 1.6, 0.12), "navy", (R, R * 1.6, R * 0.62))
    cyl("hullmid", R, L - 3.1 * R, (0, 0.05, 0.12), "navy", "Y", 24)
    # boot-top and red waterline
    sph("bootbow", 1.0, (0, L / 2 - R * 1.5, 0.04), "orange2", (R * 1.02, R * 1.5, 0.06))
    sph("bootst", 1.0, (0, -L / 2 + R * 1.6, 0.04), "orange2", (R * 1.02, R * 1.6, 0.06))
    box("boot", (R * 2.02, L - 3.1 * R, 0.1), (0, 0.05, 0.04), "orange2", 0.03)
    # upper casing: flat grey deck with chrome rail and six missile hatches (aft)
    box("casing", (R * 1.1, 4.8, 0.06), (0, -0.4, 0.46), "deck", 0.02)
    for i in range(6):
        yy = -2.5 + i * 0.5
        cyl(f"msl{i}", 0.17, 0.05, (0, yy, 0.5), "white", "Z", 14)
        cyl(f"mslr{i}", 0.2, 0.03, (0, yy, 0.485), "copper", "Z", 14)
    for s in (-1, 1):
        box(f"rail{s}", (0.025, 4.6, 0.06), (s * R * 0.55, -0.4, 0.52), "chrome", 0.0)
        box(f"hpstripe{s}", (0.02, 3.2, 0.1), (s * (R * 0.96 + 0.01), 0.0, 0.2), "white", 0.0)
    # sail (fin)
    box("sail", (0.46, 1.5, 0.66), (0, 1.1, 0.78), "white", 0.14)
    box("sailglass", (0.5, 1.0, 0.1), (0, 1.1, 0.88), "glass", 0.0)
    box("sailband", (0.5, 1.54, 0.12), (0, 1.1, 1.0), "house", 0.05)
    box("sailtop", (0.34, 1.3, 0.05), (0, 1.1, 1.13), "white2", 0.02)
    for s in (-1, 1):
        box(f"plane{s}", (0.55, 0.3, 0.04), (s * 0.43, 1.45, 0.82), "navy2", 0.01)               # sail planes
    cyl("peri", 0.035, 0.5, (0, 1.45, 1.35), "chrome2", "Z", 8)
    cyl("mast", 0.03, 0.4, (0, 0.8, 1.3), "chrome2", "Z", 8)
    sph("masttip", 0.05, (0, 0.8, 1.52), "orange")
    box("masthead", (0.3, 0.04, 0.1), (0, 0.8, 1.4), "chrome", 0.0)
    # hull number on the sail sides
    P.side_digits(S, "s", "41", 1.1, 0.7, 0.245, 0.06)
    # tail fins + prop wash
    box("fin", (0.04, 0.6, 0.55), (0, -L / 2 + 0.55, 0.38), "navy2", 0.01)
    box("tailplane", (1.3, 0.5, 0.04), (0, -L / 2 + 0.6, 0.15), "navy2", 0.01)
    # bow wave and wake
    sph("bowfoam", 1.0, (0, L / 2 - 0.05, 0.03), "foam", (0.26, 0.22, 0.03), nocast=True)
    for s in (-1, 1):
        box(f"bowarm{s}", (0.08, 1.2, 0.03), (s * 0.5, L / 2 - 0.9, 0.025), "foam", 0.0, rot=(0, 0, s * 24), nocast=True)
        box(f"side{s}", (0.06, 3.0, 0.03), (s * (R + 0.02), 0.5, 0.02), "foam", 0.0, nocast=True)
    sph("wake", 1.0, (0, -L / 2 - 0.3, 0.02), "foam", (0.4, 0.35, 0.025), nocast=True)
