"""Expedition Gyro Scout (small rotary spotter, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A tiny navy bubble-canopy helicopter: round glass pod over a navy hull with a white belly, an amber designator lamp under the nose, a
slim chrome tail boom with a canted fin (team colour flash) and a small tail rotor, a chrome mast with a copper hub and a two-blade
rotor (white blades, orange tips), chrome skids.  Footprint about 3 x 2.4 units, smaller than the other aircraft.
AIR convention: see dir_dirigible.py (body baked at ALT, no shadow; separate _shadow sheet).  Team colour: tail fin flash and pod stripe.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_gyro"
TITLE = "Expedition Gyro Scout (rotary spotter)"
CANVAS = 160
ALT = 1.8
TARGET = (0.0, 0.0, 1.9)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "shadow"]
PALETTE = E.PALETTE
MATERIALS = E.MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    z0 = ALT
    box("hull", (0.85, 1.5, 0.55), (0, 0.0, z0 + 0.5), "navy", 0.14)
    box("belly", (0.7, 1.3, 0.12), (0, 0.0, z0 + 0.2), "white", 0.04)
    sph("bubble", 0.55, (0, 0.5, z0 + 0.75), "glass", scale=(0.95, 1.1, 0.85))
    box("pstripe0", (0.05, 1.0, 0.12), (0.44, -0.1, z0 + 0.5), "house", 0.0)
    box("pstripe1", (0.05, 1.0, 0.12), (-0.44, -0.1, z0 + 0.5), "house", 0.0)
    box("cowl", (0.7, 0.7, 0.3), (0, -0.55, z0 + 0.9), "navy2", 0.1)
    box("stripe", (0.72, 0.07, 0.04), (0, -0.2, z0 + 1.06), "orange", 0.0)
    cyl("lampbody", 0.1, 0.3, (0, 0.85, z0 + 0.25), "chrome2", "Y", 10)                  # designator lamp under the nose
    cyl("lamp", 0.085, 0.05, (0, 1.02, z0 + 0.25), "lens", "Y", 10)
    cyl("ring", 0.115, 0.06, (0, 0.92, z0 + 0.25), "copper", "Y", 10)
    cyl("boom", 0.09, 2.0, (0, -1.6, z0 + 0.7), "chrome", "Y", 10)
    cyl("boomring", 0.105, 0.09, (0, -0.95, z0 + 0.7), "copper", "Y", 10)
    box("fin", (0.07, 0.55, 0.8), (0, -2.45, z0 + 1.05), "navy", 0.02, rot=(0, 0, 0))
    box("finfl", (0.09, 0.32, 0.3), (0, -2.45, z0 + 1.22), "house", 0.0)
    box("fintip", (0.1, 0.2, 0.06), (0, -2.45, z0 + 1.45), "orange", 0.0)
    box("stab", (0.9, 0.3, 0.05), (0, -2.3, z0 + 0.7), "white", 0.0)
    cyl("trdisc", 0.34, 0.03, (0.13, -2.5, z0 + 0.95), "dark", "X", 14)
    box("trblade", (0.05, 0.1, 0.62), (0.15, -2.5, z0 + 0.95), "chrome2", 0.0, rot=(25, 0, 0))
    # mast + main rotor
    cyl("mast", 0.07, 0.5, (0, 0.0, z0 + 1.18), "chrome2", "Z", 8)
    cyl("hub", 0.15, 0.1, (0, 0.0, z0 + 1.48), "copper", "Z", 12)
    for i, a in enumerate((20, 200)):
        box(f"blade{i}", (3.1, 0.2, 0.04), (0, 0, z0 + 1.52), "white", 0.0, rot=(0, 0, a))
        x, y = 1.35 * math.cos(math.radians(a)), 1.35 * math.sin(math.radians(a))
        box(f"tip{i}", (0.5, 0.21, 0.05), (x, y, z0 + 1.53), "orange", 0.0, rot=(0, 0, a))
    # skids
    for s in (-1, 1):
        cyl(f"skid{s}", 0.04, 1.7, (s * 0.5, 0.1, z0 - 0.02), "chrome", "Y", 6)
        for y in (0.5, -0.3):
            box(f"strut{s}{y}", (0.05, 0.05, 0.3), (s * 0.45, y, z0 + 0.12), "chrome2", 0.0, rot=(0, s * 15, 0))
