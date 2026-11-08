"""Expedition Sparker (hover anti-air) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

Glide Tank hover style hull (same skirt, stripes, fins and nacelles, scaled 0.8) with a flat deck and a tall chrome radar mast.  Turret layer
(pivot on the deck): a navy swivel base carrying a chrome elevated rack of four orange-nosed guided missiles (two over two) with copper bands,
a guidance dish and a team-colour flash on each side of the base.  Hull number "03".
Layers: body (hull + whole-vehicle shadow), turret (own sheet).
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_sparker"
TITLE = "Expedition Sparker"
K = 0.82
LIFT = 0.14
CANVAS = 128
TARGET = (0.0, 0.0, 0.9 * K)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT_U = (0.0, 0.1, 1.2 + LIFT)
PIVOT = tuple(v * K for v in PIVOT_U)
MOUNT = dict(deck_z=(1.15 + LIFT) * K, r_lip=0.62 * K, r_well=0.0, r_shadow=0.85 * K, soft_r=0.8 * K, lip="chrome2", well="dark")
PALETTE = E.PALETTE
MATERIALS = dict(E.MATERIALS)


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    E.skirt(S, 2.4, 3.7, 0.0, 0.42, LIFT)
    S.lift(LIFT)
    box("hullL", (2.1, 3.3, 0.46), (0, 0, 0.66), "navy2", 0.16)
    box("hullU", (1.85, 2.8, 0.3), (0, -0.05, 0.98), "navy", 0.14)
    box("prow", (1.85, 0.95, 0.4), (0, 1.55, 0.88), "navy", 0.14, rot=(-24, 0, 0))
    box("bumper", (1.95, 0.1, 0.1), (0, 2.0, 0.52), "chrome", 0.03)
    for s in (-1, 1):
        box(f"trim{s}", (0.06, 2.7, 0.06), (s * 0.93, -0.05, 1.0), "copper", 0.0)
        box(f"stripe{s}", (0.05, 2.6, 0.15), (s * 1.07, -0.05, 0.7), "house", 0.0)
        box(f"sill{s}", (0.07, 2.9, 0.05), (s * 1.07, -0.05, 0.9), "copper", 0.0)
        E.lamp(S, f"L{s}", s * 0.55, 2.03, 0.72)
    for s in (-1, 1):                                                                        # S16 team colour: top stripes
        box(f"topstripe{s}", (0.14, 1.9, 0.045), (s * 0.82, -0.1, 1.17), "house", 0.0)
    box("deckw", (1.5, 2.3, 0.04), (0, -0.1, 1.16), "white", 0.01)
    box("deckst", (1.5, 0.07, 0.045), (0, 1.0, 1.16), "orange", 0.0)
    E.digits(S, "hn", "03", 0, 1.35, 1.16, 0.07, "white")
    for s in (-1, 1):
        E.nacelle(S, f"{s}", s * 0.65, -1.8, 1.05, 0.36, 0.9)
        box(f"fin{s}", (0.08, 1.0, 0.95), (s * 0.98, -1.5, 1.55), "navy", 0.03, rot=(0, s * 9, 0))
        box(f"finfl{s}", (0.09, 0.6, 0.3), (s * 1.02, -1.55, 1.65), "house", 0.0, rot=(0, s * 9, 0))
        box(f"fintip{s}", (0.1, 0.26, 0.07), (s * 1.06, -1.5, 2.02), "orange", 0.0, rot=(0, s * 9, 0))
    box("tailbar", (1.85, 0.12, 0.08), (0, -2.0, 1.0), "chrome2", 0.0)
    cyl("mast", 0.025, 1.0, (0.6, -1.25, 1.65), "chrome2", "Z", 6)
    box("radar", (0.5, 0.06, 0.16), (0.6, -1.25, 2.2), "chrome", 0.02)
    sph("mastknob", 0.05, (0.6, -1.25, 2.3), "orange")
    S.lift(0)
    S0.mount(PIVOT, **MOUNT)
    S0.begin("turret", PIVOT)
    px, py, pz = PIVOT_U
    cyl("ring", 0.62, 0.1, (px, py, pz + 0.05), "chrome", "Z", 22)
    box("base", (1.3, 1.2, 0.34), (px, py, pz + 0.26), "navy", 0.14)
    box("troofband", (1.0, 0.5, 0.04), (px, py - 0.3, pz + 0.44), "house", 0.0)                 # S16 team colour: base roof band
    for s in (-1, 1):
        box(f"bflash{s}", (0.05, 0.8, 0.14), (px + s * 0.66, py, pz + 0.26), "house", 0.0)
        box(f"btrim{s}", (0.05, 1.0, 0.05), (px + s * 0.65, py, pz + 0.42), "copper", 0.0)
        box(f"yoke{s}", (0.1, 0.9, 0.5), (px + s * 0.52, py + 0.05, pz + 0.65), "house", 0.03)
    # rack: tilted cradle with four missiles (2 x 2) pointing forward
    cr = (18, 0, 0)
    box("cradle", (1.0, 1.9, 0.12), (px, py + 0.4, pz + 0.72), "chrome", 0.03, rot=cr)
    for sx in (-1, 1):
        for sz, zz in ((0, 0.87), (1, 1.1)):
            mx, mz = px + sx * 0.25, pz + zz
            cyl(f"mis{sx}{sz}", 0.11, 1.75, (mx, py + 0.45, mz), "white", "Y", 12, rot=(90 + 18, 0, 0))
            cone = S.cone
            cone(f"nose{sx}{sz}", 0.11, 0.0, 0.34, (mx, py + 1.37, mz + 0.3), "orange", 12, rot=(-90 + 18, 0, 0))
            cyl(f"band{sx}{sz}", 0.125, 0.07, (mx, py + 0.65, mz - 0.07), "copper", "Y", 12, rot=(90 + 18, 0, 0))
            cyl(f"tail{sx}{sz}", 0.12, 0.06, (mx, py - 0.4, mz - 0.24), "dark", "Y", 12, rot=(90 + 18, 0, 0))
    cyl("dishm", 0.04, 0.4, (px + 0.55, py - 0.35, pz + 0.65), "chrome2", "Z", 6)
    cyl("dish", 0.22, 0.06, (px + 0.55, py - 0.35, pz + 0.9), "copper", "Y", 14, rot=(60, 0, 0))
    sph("dishhub", 0.05, (px + 0.55, py - 0.3, pz + 0.92), "orange")
