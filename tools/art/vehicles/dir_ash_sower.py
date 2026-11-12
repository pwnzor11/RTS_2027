"""Directorate Ash Sower (radiation-laying wheeled sprayer) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A heavy four-wheel truck: a small bone cab forward with a lead-apron shield plate and a slit window, behind it a big tapered
slag hopper (oxide red, glowing amber seam, bone stencil band) with a hatch lid, and across the tail a wide spray bar on
drop-arms with a row of downward nozzles and amber lamps.  No weapon, no turret.
House paint: both hopper side panels, the cab roof stripe and the spray-bar end caps.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dir_common as D

ID = "dir_ash_sower"
TITLE = "Directorate Ash Sower (radiation-laying sprayer)"
CANVAS = 144
TARGET = (0.0, -0.1, 0.9)
STATES = [("idle", 16, 1, 1)]
PALETTE = D.PALETTE
MATERIALS = D.MATERIALS


def build(S, state, k, n):
    box, cyl, cone = S.box, S.cyl, S.cone
    X = 0.92
    axles = (1.15, -0.95)
    for s, side in ((-1, "L"), (1, "R")):
        for i, y in enumerate(axles):
            cyl(f"tyre{side}{i}", 0.43, 0.34, (s * X, y, 0.43), "track", "X", 16)
            cyl(f"hub{side}{i}", 0.22, 0.37, (s * X, y, 0.43), "steel", "X", 10)
            cyl(f"nut{side}{i}", 0.07, 0.4, (s * X, y, 0.43), "dark", "X", 6)
        box(f"guard{side}F", (0.46, 0.95, 0.06), (s * X, 1.15, 0.92), "hull2", 0.02)
        box(f"guard{side}R", (0.46, 0.95, 0.06), (s * X, -0.95, 0.92), "hull2", 0.02)
    for i, y in enumerate(axles):
        box(f"axle{i}", (1.8, 0.12, 0.12), (0, y, 0.43), "dark", 0)
    box("frame", (1.5, 4.3, 0.22), (0, -0.2, 0.72), "hull2", 0.03)
    # ---- cab: shielded, bone with oxide bands, lead-apron plate over the windscreen
    box("cab", (1.5, 1.05, 0.95), (0, 1.35, 1.3), "bone", 0.08)
    box("cabroof", (1.56, 1.1, 0.07), (0, 1.35, 1.8), "hull2", 0.03)
    box("roofhouse", (0.5, 0.85, 0.03), (0, 1.35, 1.85), "house", 0.0)
    box("shield", (1.3, 0.1, 0.5), (0, 1.9, 1.5), "steel", 0.03, rot=(-12, 0, 0))
    box("slit", (1.0, 0.06, 0.1), (0, 1.97, 1.52), "glass", 0.0, rot=(-12, 0, 0))
    box("grille", (0.9, 0.05, 0.28), (0, 1.9, 0.98), "dark", 0.0)
    box("bumper", (1.6, 0.14, 0.18), (0, 1.95, 0.72), "oxide", 0.03)
    for sx in (-1, 1):
        box(f"cabstripe{sx}", (0.06, 0.95, 0.12), (sx * 0.77, 1.35, 1.05), "oxide", 0.0)
        box(f"lamp{sx}", (0.2, 0.06, 0.14), (sx * 0.55, 1.97, 0.8), "lens", 0.0)
    # ---- slag hopper: lower tapered body, wide upper box, lid, glow seam
    box("hopL", (1.55, 2.35, 0.55), (0, -0.55, 1.05), "oxide2", 0.07)
    box("hopU", (1.9, 2.35, 0.7), (0, -0.55, 1.62), "oxide", 0.09)
    box("hoprim", (1.96, 2.4, 0.08), (0, -0.55, 2.0), "oxide2", 0.03)
    box("lid", (1.3, 1.5, 0.07), (0, -0.4, 2.06), "hull2", 0.02)
    box("hatch", (0.5, 0.5, 0.1), (0, -0.2, 2.13), "dark", 0.02)
    cyl("vent", 0.12, 0.45, (0, -1.05, 2.3), "steel", "Z", 8)
    box("seam", (1.94, 0.1, 0.1), (0, 0.3, 1.5), "glow", 0.0)
    box("band", (1.94, 0.18, 0.28), (0, -1.15, 1.6), "bone", 0.0)
    for sx in (-1, 1):
        box(f"hside{sx}", (0.06, 1.7, 0.5), (sx * 0.96, -0.6, 1.62), "house", 0.0)
        box(f"hsideb{sx}", (0.07, 0.2, 0.55), (sx * 0.96, 0.2, 1.62), "bone", 0.0)
        box(f"ladder{sx}", (0.05, 0.05, 0.9), (sx * 0.5, -1.78, 1.5), "steel", 0.0)
    # ---- chute and spray bar across the tail
    cone("chute", 0.5, 0.18, 0.7, (0, -1.85, 0.95), "dark", 12, rot=(180, 0, 0))
    for sx in (-1, 1):
        box(f"arm{sx}", (0.12, 0.12, 0.7), (sx * 1.2, -2.0, 1.0), "dark", 0.02)
        box(f"endcap{sx}", (0.22, 0.4, 0.4), (sx * 1.72, -2.35, 0.78), "house", 0.0)
    box("bar", (3.2, 0.22, 0.2), (0, -2.35, 0.78), "hull2", 0.03)
    box("barstripe", (2.4, 0.24, 0.07), (0, -2.35, 0.9), "bone", 0.0)
    for i in range(7):
        x = -1.35 + i * 0.45
        cone(f"nozzle{i}", 0.09, 0.03, 0.26, (x, -2.35, 0.55), "brass", 8, rot=(180, 0, 0))
    for sx in (-1, 1):
        box(f"taillamp{sx}", (0.16, 0.06, 0.12), (sx * 0.62, -2.52, 0.95), "lens", 0.0)
