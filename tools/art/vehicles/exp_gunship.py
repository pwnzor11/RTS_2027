"""Expedition Gunship (heavy rotary ground attack, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A heavy navy attack helicopter: a chunky fuselage with a white belly and spine, a stepped glass cockpit, a chrome chin turret with a
twin gun, short stub wings with a white rocket pod each (orange warheads, team-colour band), a tall mast with a four-blade white rotor
with orange tips, a copper-ringed engine cowl with two turbine exhausts, a tail boom with a team-colour fin flash and a tail rotor,
and chrome skids.  Footprint about 4.4 x 5.5 units (rotor 4.6 wide).
AIR convention: see dir_dirigible.py (body baked at ALT, no shadow; separate _shadow sheet).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_gunship"
TITLE = "Expedition Gunship (rotary ground attack)"
CANVAS = 224
ALT = 2.0
TARGET = (0.0, 0.0, 2.2)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "shadow"]
PALETTE = E.PALETTE
MATERIALS = E.MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    z0 = ALT
    box("hull", (1.2, 2.6, 0.8), (0, 0.2, z0 + 0.75), "navy2", 0.2)
    box("hullU", (1.0, 1.9, 0.5), (0, -0.2, z0 + 1.25), "navy", 0.16)
    box("belly", (0.95, 2.3, 0.1), (0, 0.2, z0 + 0.38), "white", 0.03)
    box("nose", (0.9, 0.9, 0.55), (0, 1.5, z0 + 0.65), "navy", 0.18, rot=(-12, 0, 0))
    sph("cockpit", 0.55, (0, 1.05, z0 + 1.2), "glass", scale=(0.85, 1.25, 0.7))
    sph("cockpit2", 0.4, (0, 0.2, z0 + 1.5), "glass", scale=(0.85, 1.1, 0.55))
    box("spine", (0.5, 1.4, 0.05), (0, -0.7, z0 + 1.52), "white", 0.0)
    box("spinestripe", (0.52, 0.07, 0.07), (0, -0.2, z0 + 1.53), "orange", 0.0)
    for s in (-1, 1):
        box(f"stripe{s}", (0.05, 1.5, 0.18), (s * 0.62, 0.0, z0 + 0.8), "house", 0.0)
        box(f"sill{s}", (0.05, 1.8, 0.05), (s * 0.6, 0.0, z0 + 1.1), "copper", 0.0)
    # chin turret and gun
    sph("chin", 0.28, (0, 1.65, z0 + 0.25), "chrome2")
    for s in (-1, 1):
        cyl(f"gun{s}", 0.04, 0.9, (s * 0.09, 2.15, z0 + 0.2), "chrome", "Y", 8)
        cyl(f"gunring{s}", 0.06, 0.06, (s * 0.09, 1.9, z0 + 0.2), "copper", "Y", 8)
        cyl(f"muzzle{s}", 0.035, 0.04, (s * 0.09, 2.62, z0 + 0.2), "dark", "Y", 8)
    # stub wings and rocket pods
    box("wing", (3.1, 0.7, 0.1), (0, 0.0, z0 + 0.95), "navy", 0.03)
    for s in (-1, 1):
        cyl(f"pod{s}", 0.26, 1.3, (s * 1.45, 0.2, z0 + 0.7), "white", "Y", 12)
        cyl(f"podband{s}", 0.28, 0.25, (s * 1.45, -0.1, z0 + 0.7), "house", "Y", 12)
        S.cone(f"podnose{s}", 0.26, 0.1, 0.3, (s * 1.45, 0.98, z0 + 0.7), "orange", rot=(-90, 0, 0), verts=12)
        for a in range(4):
            cyl(f"podtube{s}{a}", 0.045, 0.06, (s * 1.45 + 0.1 * math.cos(a * 1.57 + 0.78), 0.86, z0 + 0.7 + 0.1 * math.sin(a * 1.57 + 0.78)), "dark", "Y", 6)
        box(f"podpylon{s}", (0.07, 0.5, 0.3), (s * 1.45, 0.2, z0 + 0.92), "chrome2", 0.0)
    # engine cowl, exhausts
    box("cowl", (0.9, 1.0, 0.45), (0, -0.55, z0 + 1.62), "navy2", 0.14)
    for s in (-1, 1):
        cyl(f"exh{s}", 0.12, 0.3, (s * 0.3, -1.1, z0 + 1.62), "chrome", "Y", 10)
        cyl(f"exhring{s}", 0.14, 0.05, (s * 0.3, -1.0, z0 + 1.62), "copper", "Y", 10)
    # mast + rotor (4 blades)
    cyl("mast", 0.1, 0.4, (0, -0.35, z0 + 1.95), "chrome2", "Z", 8)
    cyl("hub", 0.2, 0.12, (0, -0.35, z0 + 2.2), "copper", "Z", 12)
    for i, a in enumerate((12, 102, 192, 282)):
        box(f"blade{i}", (2.3, 0.2, 0.04), (0, -0.35, z0 + 2.24), "white", 0.0, rot=(0, 0, a))
        x, y = 0.0 + 1.0 * math.cos(math.radians(a)) * 1.0, -0.35 + 1.0 * math.sin(math.radians(a))
        box(f"tip{i}", (0.5, 0.21, 0.05), (x * 1.0, y, z0 + 2.25), "orange", 0.0, rot=(0, 0, a))
    # tail
    box("boom", (0.42, 2.0, 0.4), (0, -2.1, z0 + 1.35), "navy", 0.1)
    box("stab", (1.3, 0.4, 0.06), (0, -2.8, z0 + 1.4), "white", 0.01)
    box("fin", (0.08, 0.8, 1.0), (0, -3.0, z0 + 1.85), "navy", 0.03, rot=(-8, 0, 0))
    box("finfl", (0.1, 0.5, 0.36), (0, -3.0, z0 + 1.95), "house", 0.0, rot=(-8, 0, 0))
    box("fintip", (0.11, 0.25, 0.07), (0, -3.02, z0 + 2.36), "orange", 0.0)
    cyl("trdisc", 0.42, 0.03, (0.1, -3.05, z0 + 1.85), "dark", "X", 14)
    box("trblade", (0.05, 0.1, 0.8), (0.12, -3.05, z0 + 1.85), "chrome2", 0.0, rot=(25, 0, 0))
    # skids
    for s in (-1, 1):
        cyl(f"skid{s}", 0.05, 2.1, (s * 0.65, 0.2, z0 - 0.05), "chrome", "Y", 6)
        for y in (0.7, -0.4):
            box(f"strut{s}{y}", (0.06, 0.06, 0.45), (s * 0.58, y, z0 + 0.16), "chrome2", 0.0, rot=(0, s * 14, 0))
