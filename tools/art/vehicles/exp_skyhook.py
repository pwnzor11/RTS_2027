"""Expedition Skyhook (VTOL troop transport, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A squat navy fuselage with a lagoon-white belly and spine, a glass cockpit bubble, stub wings carrying two big tilt-rotor nacelles
(chrome shells, copper rings, a horizontal three-blade rotor disc each, white blades with orange tips), twin canted tail fins with
team-colour flashes, and a chrome cargo cradle slung beneath the belly with a white crate and a hook.  Footprint about 5 x 4 units.
AIR convention: see dir_dirigible.py (body baked at ALT, no shadow; separate _shadow sheet).  Team colour: the long fuselage stripe,
the fin flashes.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_skyhook"
TITLE = "Expedition Skyhook (VTOL transport)"
CANVAS = 224
ALT = 2.2
TARGET = (0.0, 0.0, 2.0)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "shadow"]
PALETTE = E.PALETTE
MATERIALS = E.MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    z0 = ALT
    # cargo cradle beneath
    for s in (-1, 1):
        box(f"cradle{s}", (0.08, 2.2, 0.08), (s * 0.55, 0.0, z0 + 0.05), "chrome", 0.0)
        for y in (0.9, -0.9):
            box(f"cstrut{s}{y}", (0.07, 0.07, 0.5), (s * 0.5, y, z0 + 0.3), "chrome2", 0.0)
    box("cratebar", (1.15, 0.08, 0.08), (0, 1.0, z0 + 0.05), "chrome", 0.0)
    box("cratebar2", (1.15, 0.08, 0.08), (0, -1.0, z0 + 0.05), "chrome", 0.0)
    box("crate", (0.85, 1.4, 0.5), (0, 0.0, z0 + 0.38), "white2", 0.05)
    box("cratestripe", (0.88, 0.2, 0.4), (0, 0.0, z0 + 0.38), "orange", 0.0)
    # fuselage
    box("fusL", (1.55, 3.4, 0.7), (0, 0.0, z0 + 0.95), "navy2", 0.2)
    box("fusU", (1.35, 3.0, 0.5), (0, -0.1, z0 + 1.4), "navy", 0.18)
    box("belly", (1.2, 3.0, 0.1), (0, 0.0, z0 + 0.62), "white", 0.03)
    box("spine", (0.7, 2.0, 0.05), (0, -0.4, z0 + 1.66), "white", 0.01)
    box("spinestripe", (0.7, 0.07, 0.06), (0, 0.2, z0 + 1.66), "orange", 0.0)
    box("nosecone", (1.2, 0.8, 0.6), (0, 1.9, z0 + 1.0), "navy", 0.2, rot=(-14, 0, 0))
    sph("cockpit", 0.55, (0, 1.2, z0 + 1.55), "glass", scale=(1.0, 1.15, 0.7))
    cyl("cockring", 0.58, 0.05, (0, 1.2, z0 + 1.48), "chrome", "Z", 14)
    E.lamp(S, "n", 0.0, 2.3, z0 + 0.85, 0.1)
    box("ramp", (0.9, 0.9, 0.08), (0, -2.0, z0 + 0.85), "chrome2", 0.0, rot=(-24, 0, 0))
    for s in (-1, 1):
        box(f"stripe{s}", (0.05, 2.4, 0.2), (s * 0.79, -0.1, z0 + 1.0), "house", 0.0)
        box(f"sill{s}", (0.06, 2.8, 0.05), (s * 0.78, -0.1, z0 + 1.28), "copper", 0.0)
        box(f"door{s}", (0.04, 0.8, 0.45), (s * 0.79, -0.9, z0 + 1.2), "chrome2", 0.0)
    E.digits(S, "hn", "06", 0, -0.7, z0 + 1.68, 0.075, "white")
    # stub wings + tilt-rotor nacelles
    box("wing", (4.0, 0.9, 0.12), (0, 0.1, z0 + 1.25), "navy", 0.04)
    for s in (-1, 1):
        box(f"wingedge{s}", (0.06, 0.9, 0.08), (s * 2.0, 0.1, z0 + 1.25), "orange", 0.0)
        E.nacelle(S, f"{s}", s * 2.05, 0.1, z0 + 1.45, 0.3, 1.2)
        cyl(f"rotdisc{s}", 0.9, 0.025, (s * 2.05, 0.8, z0 + 1.45), "dark", "Y", 24)
        for i, a in enumerate((0, 60, 120)):
            box(f"rb{s}{i}", (1.8, 0.04, 0.12), (s * 2.05, 0.82, z0 + 1.45), "white", 0.0, rot=(0, a, 0))
        cyl(f"rotcap{s}", 0.12, 0.1, (s * 2.05, 0.9, z0 + 1.45), "copper", "Y", 10)
        box(f"tipmark{s}", (0.2, 0.05, 0.14), (s * 2.05 + 0.85, 0.82, z0 + 1.45), "orange", 0.0)
    for s in (-1, 1):                                                                        # S16 team colour: wing and tailplane patches
        box(f"wingpatch{s}", (0.7, 0.55, 0.04), (s * 1.25, 0.1, z0 + 1.32), "house", 0.0)
        box(f"tailpatch{s}", (0.6, 0.34, 0.04), (s * 0.65, -2.55, z0 + 1.64), "house", 0.0)
    # tail
    box("boom", (0.8, 1.3, 0.55), (0, -2.1, z0 + 1.4), "navy", 0.14)
    box("tailplane", (2.2, 0.5, 0.07), (0, -2.55, z0 + 1.6), "white", 0.01)
    for s in (-1, 1):
        box(f"fin{s}", (0.08, 0.8, 1.1), (s * 1.05, -2.55, z0 + 2.05), "navy", 0.03, rot=(0, s * 10, 0))
        box(f"finfl{s}", (0.1, 0.5, 0.4), (s * 1.1, -2.55, z0 + 2.15), "house", 0.0, rot=(0, s * 10, 0))
        box(f"fintip{s}", (0.11, 0.25, 0.07), (s * 1.17, -2.55, z0 + 2.6), "orange", 0.0, rot=(0, s * 10, 0))
    cyl("whip", 0.02, 0.9, (0.3, -1.6, z0 + 2.1), "chrome2", "Z", 6)
