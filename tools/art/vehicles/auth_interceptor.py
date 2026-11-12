"""Authority Interceptor (single-seat propeller fighter, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Same family as auth_bomber (olive hull, pale-grey straight wing, yellow tips, white stencil) but small and quick: a slim
round-nosed fuselage with a bubble canopy, one straight low wing, a single tall fin and a tailplane, three-blade prop on a
yellow spinner.  A pair of cannon barrels poke out of the wing leading edges (anti-air).  Tally marks on the cowling.
AIR convention: see dir_dirigible.py / auth_bomber.py (body baked at ALT, no shadow; separate _shadow sheet).
Team colour: the cowling ring, the stripe along each fuselage flank, the band outboard on each wing (top and underside) and the fin flash.
"""
import math

ID = "auth_interceptor"
TITLE = "Authority Interceptor (single-seat prop fighter)"
CANVAS = 224     # wingspan 4.4 + shadow offset at ALT to the lower right
ALT = 2.6
TARGET = (0.0, 0.0, 2.5)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "shadow"]
MATERIALS = {
    "wing": ("#8A9582", dict(rough=0.7)),
    "wing2": ("#6E7A64", dict(rough=0.7)),
    "bone": ("#B9B7A2", dict(rough=0.8)),
    "prop": ("#1D1F19", dict(rough=0.8)),
}


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    # pipeline note (see auth_bomber.py): grow the ground plane so its far edge stays out of the shadow frame
    import bpy
    g = bpy.data.objects.get("ground")
    if g is not None:
        g.scale = (3.0, 3.0, 1.0)
    S.begin("body")
    z0 = ALT + 0.5

    # ---- fuselage: round barrel, cowl, spinner, tail cone
    cyl("fus", 0.44, 2.7, (0, -0.1, z0), "hull", "Y", 18)
    cyl("fusbelly", 0.36, 2.4, (0, -0.1, z0 - 0.18), "hull2", "Y", 14)
    cyl("cowl", 0.5, 0.75, (0, 1.55, z0), "steel", "Y", 18)
    cyl("cowlring", 0.52, 0.16, (0, 1.32, z0), "house", "Y", 18)
    cyl("cowlfront", 0.46, 0.1, (0, 1.95, z0), "dark", "Y", 18)
    S.cone("spin", 0.22, 0.04, 0.5, (0, 2.2, z0), "yellow", rot=(-90, 0, 0), verts=10)
    for j in range(3):
        box(f"blade{j}", (0.12, 0.04, 1.75), (0, 2.02, z0), "prop", 0.0, rot=(0, 60 * j + 20, 0))
    S.cone("tail", 0.44, 0.08, 1.7, (0, -2.2, z0 + 0.04), "hull2", rot=(90, 0, 0), verts=14)
    # tally marks on the cowl flanks, exhaust stubs
    for s in (-1, 1):
        for i in range(3):
            box(f"tally{s}{i}", (0.03, 0.05, 0.14), (s * 0.5, 1.45 + i * 0.1, z0 + 0.12), "white", 0.0)
        for i in range(3):
            cyl(f"exh{s}{i}", 0.04, 0.16, (s * 0.5, 1.35 + i * 0.12, z0 - 0.18), "dark", "X", 6)
    # ---- canopy: glazed bubble with a frame and a rear fairing
    sph("canopy", 0.34, (0, 0.25, z0 + 0.4), "glass", scale=(0.9, 1.7, 0.85))
    box("canframe", (0.62, 0.05, 0.05), (0, 0.4, z0 + 0.62), "steel", 0.0)
    box("fairing", (0.4, 1.0, 0.2), (0, -0.7, z0 + 0.38), "hull", 0.08, rot=(-6, 0, 0))
    box("headrest", (0.3, 0.3, 0.16), (0, -0.25, z0 + 0.5), "rust", 0.04)
    # ---- flank stripe + stencil number
    for s in (-1, 1):
        box(f"flank{s}", (0.05, 1.5, 0.13), (s * 0.45, -0.75, z0 + 0.05), "house", 0.0)
        box(f"flankseam{s}", (0.05, 1.6, 0.04), (s * 0.45, -0.75, z0 - 0.1), "yellow", 0.0)
    # ---- wing: low straight wing, yellow tips, team band outboard (top and underside), cannon barrels
    zw = z0 - 0.22
    box("wing", (4.4, 1.0, 0.14), (0, 0.15, zw), "wing", 0.04)
    box("wlead", (4.4, 0.1, 0.16), (0, 0.62, zw), "bone", 0.0)
    box("wtrail", (4.4, 0.2, 0.09), (0, -0.33, zw - 0.01), "wing2", 0.0)
    box("wroot", (1.3, 1.25, 0.22), (0, 0.1, zw + 0.03), "hull", 0.06)
    for s in (-1, 1):
        box(f"wband{s}", (0.6, 0.5, 0.04), (s * 1.5, 0.15, zw + 0.09), "house", 0.0)
        box(f"wbandb{s}", (0.6, 0.5, 0.04), (s * 1.5, 0.15, zw - 0.09), "house", 0.0)
        box(f"wtip{s}", (0.3, 1.0, 0.16), (s * 2.05, 0.15, zw), "yellow", 0.0)
        cyl(f"gun{s}", 0.045, 0.7, (s * 0.95, 0.9, zw + 0.04), "steel", "Y", 6)
        box(f"gunfair{s}", (0.14, 0.4, 0.1), (s * 0.95, 0.5, zw + 0.06), "hull2", 0.02)
    for i, x0 in enumerate((-0.2, 0.0, 0.2)):
        box(f"num{i}", (0.1, 0.3, 0.03), (-1.1 + x0 * 0.0 + i * 0.16, 0.1, zw + 0.1), "white", 0.0)
    # ---- tail: tailplane and one tall fin with the team flash
    box("tplane", (1.9, 0.6, 0.09), (0, -2.45, z0 + 0.06), "wing", 0.03)
    box("tptr", (1.9, 0.14, 0.07), (0, -2.76, z0 + 0.06), "wing2", 0.0)
    for s in (-1, 1):
        box(f"tptip{s}", (0.22, 0.6, 0.1), (s * 0.98, -2.45, z0 + 0.06), "yellow", 0.0)
    box("fin", (0.12, 0.95, 1.0), (0, -2.45, z0 + 0.58), "hull2", 0.04, rot=(-10, 0, 0))
    box("finflash", (0.15, 0.7, 0.62), (0, -2.46, z0 + 0.52), "house", 0.0, rot=(-10, 0, 0))
    box("fintip", (0.14, 0.4, 0.09), (0, -2.58, z0 + 1.08), "yellow", 0.0)
    box("rudder", (0.08, 0.22, 0.85), (0, -2.95, z0 + 0.52), "wing", 0.0, rot=(-10, 0, 0))
