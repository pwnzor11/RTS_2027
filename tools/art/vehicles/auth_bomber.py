"""Authority Heavy Bomber (twin-boom high-wing bomber, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

Civil-engineering look, 1960s freight-aircraft logic: a stubby, deep-bellied cargo-pod fuselage with a glazed flight deck, slung under a
single straight HIGH wing; two prop engines on the wing drive two tail booms that carry a tailplane between them and a tall fin
each.  The bomb bay is the whole belly: dark opening with hazard-striped doors.  Olive hull panels, pale-grey wing, yellow
survey-style tips and walkways, a white stencilled number on the pod.  Nothing like the Expedition bomber's long four-engine cigar:
this one is short, wide, and reads as a pod with a bridge of wing and two tails.
AIR convention: see dir_dirigible.py (body baked at ALT, no shadow; separate _shadow sheet).
Team colour: both tail fins (large flash), the wing band outboard of each engine, and the stripe along the pod.
"""
import math

ID = "auth_bomber"
TITLE = "Authority Heavy Bomber (twin-boom high-wing bomber)"
CANVAS = 320     # exp_bomber uses 288; the shadow of the wide wing at ALT needs the extra room on the right
ALT = 2.6
TARGET = (0.0, 0.0, 2.5)      # a hair lower than exp_bomber: keeps the 30-unit ground plane edge out of the shadow frame
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
    # The pipeline's ground plane is 30 units: with a 320 px canvas its far edge lands inside the top row of the shadow frame
    # (a dark line across the sheet).  Grow it; only the shadow pass shows the ground.  (Pipeline note for the lead.)
    import bpy
    g = bpy.data.objects.get("ground")
    if g is not None:
        g.scale = (3.0, 3.0, 1.0)
    S.begin("body")
    z0 = ALT + 0.6                  # pod axis height
    zw = z0 + 1.05                  # wing mid-plane (high wing: sits on top of the pod)
    EX = 2.2                        # engine / boom x

    # ------------------------------------------------------------------ pod fuselage
    cyl("pod", 0.92, 3.8, (0, 0.5, z0), "hull", "Y", 20)
    cyl("podbelly", 0.84, 3.4, (0, 0.5, z0 - 0.25), "hull2", "Y", 16)
    sph("nose", 0.92, (0, 2.45, z0 + 0.05), "hull", scale=(1.0, 1.15, 0.95))
    S.cone("tailtaper", 0.92, 0.3, 1.5, (0, -2.15, z0 + 0.12), "hull2", rot=(90, 0, 0), verts=18)
    box("tailramp", (0.7, 0.7, 0.1), (0, -2.85, z0 + 0.25), "dark", 0.0)
    # flight deck: stepped glazing on top of the nose, window band, frames
    box("deck", (0.95, 1.1, 0.34), (0, 1.7, z0 + 0.78), "hull", 0.1)
    box("glass", (0.8, 0.5, 0.26), (0, 2.2, z0 + 0.82), "glass", 0.04, rot=(-18, 0, 0))
    for s in (-1, 1):
        box(f"dglass{s}", (0.05, 0.8, 0.2), (s * 0.49, 1.55, z0 + 0.74), "glass", 0.0)
        box(f"nosewin{s}", (0.05, 0.5, 0.22), (s * 0.74, 1.75, z0 + 0.15), "glass", 0.0)
        box(f"cargowin{s}", (0.05, 0.22, 0.18), (s * 0.78, 0.55, z0 + 0.25), "glass", 0.0)
        box(f"cargowinb{s}", (0.05, 0.22, 0.18), (s * 0.78, 0.0, z0 + 0.25), "glass", 0.0)
    box("framebar", (0.82, 0.05, 0.05), (0, 2.15, z0 + 0.96), "steel", 0.0)
    # team stripe along each flank + white stencil number
    for s in (-1, 1):
        box(f"podstripe{s}", (0.05, 3.0, 0.16), (s * 0.79, 0.1, z0 - 0.15), "house", 0.0)
        box(f"podseam{s}", (0.05, 3.1, 0.04), (s * 0.79, 0.1, z0 - 0.3), "yellow", 0.0)
    for i, x0 in enumerate((-0.22, 0.0, 0.22)):
        box(f"num{i}", (0.12, 0.34, 0.03), (x0, -0.2, z0 + 0.93), "white", 0.0)
    # spine walkway + dorsal beacon
    box("walk", (0.3, 1.6, 0.05), (0, -1.0, z0 + 0.93), "yellow", 0.0)
    cyl("beacon", 0.07, 0.12, (0, -1.9, z0 + 0.98), "lens", "Z", 8)

    # ------------------------------------------------------------------ bomb bay on the belly
    box("bay", (0.95, 2.1, 0.12), (0, 0.45, z0 - 0.72), "dark", 0.02)
    for i in range(7):
        box(f"bayhaz{i}", (0.95, 0.12, 0.06), (0, -0.45 + i * 0.3, z0 - 0.78), "yellow" if i % 2 == 0 else "dark", 0.0)
    for s in (-1, 1):
        box(f"bayrail{s}", (0.08, 2.2, 0.14), (s * 0.5, 0.45, z0 - 0.7), "steel", 0.0)

    # ------------------------------------------------------------------ high wing, struts
    box("wing", (7.0, 1.25, 0.2), (0, -0.05, zw), "wing", 0.05)
    box("wlead", (7.0, 0.14, 0.22), (0, 0.62, zw + 0.0), "bone", 0.0)
    box("wtrail", (7.0, 0.25, 0.12), (0, -0.55, zw - 0.02), "wing2", 0.0)
    box("wroot", (1.9, 1.5, 0.26), (0, -0.05, zw + 0.02), "hull", 0.06)
    for s in (-1, 1):
        # outboard team band + yellow tips
        box(f"wband{s}", (0.9, 0.95, 0.04), (s * 3.0, -0.05, zw + 0.12), "house", 0.0)
        box(f"wbandb{s}", (0.9, 0.95, 0.04), (s * 3.0, -0.05, zw - 0.12), "house", 0.0)
        box(f"wtip{s}", (0.3, 1.25, 0.22), (s * 3.4, -0.05, zw), "yellow", 0.0)
        box(f"wrib{s}", (0.04, 1.5, 0.04), (s * 1.6, 0.15, zw + 0.12), "wing2", 0.0)
        # struts from pod to wing and from pod flank to wing underside
        box(f"pylon{s}", (0.14, 1.2, 0.5), (s * 0.9, -0.05, z0 + 0.7), "hull2", 0.02)
        box(f"strut{s}", (0.07, 0.1, 1.5), (s * 1.55, -0.05, zw - 0.8), "steel", 0.0, rot=(0, -s * 38, 0))

    # ------------------------------------------------------------------ engines (prop), tail booms, tailplane, twin fins
    for s, nm in ((-1, "L"), (1, "R")):
        x = s * EX
        cyl(f"nac{nm}", 0.34, 1.7, (x, 0.9, zw - 0.45), "hull", "Y", 14)
        cyl(f"nacb{nm}", 0.36, 0.1, (x, 0.12, zw - 0.45), "bone", "Y", 14)
        cyl(f"cowl{nm}", 0.37, 0.16, (x, 1.7, zw - 0.45), "steel", "Y", 14)
        S.cone(f"spin{nm}", 0.2, 0.04, 0.4, (x, 2.0, zw - 0.45), "yellow", rot=(-90, 0, 0), verts=10)
        for j in range(3):
            box(f"blade{nm}{j}", (0.1, 0.04, 1.5), (x, 1.82, zw - 0.45), "prop", 0.0, rot=(0, 60 * j + (12 if nm == "L" else 30), 0))
        # boom: runs back from the nacelle, up to the fin
        cyl(f"boom{nm}", 0.24, 3.6, (x, -1.9, zw - 0.45), "hull2", "Y", 12)
        S.cone(f"boomend{nm}", 0.24, 0.1, 0.5, (x, -3.95, zw - 0.45), "hull2", rot=(90, 0, 0), verts=12)
        box(f"boomstripe{nm}", (0.5, 1.6, 0.05), (x, -2.2, zw - 0.18), "house", 0.0)
        # fin: wide, tall, with the team flash covering most of it
        box(f"fin{nm}", (0.14, 1.25, 1.45), (x, -3.45, zw + 0.3), "hull2", 0.04, rot=(-8, 0, 0))
        box(f"finfl{nm}", (0.17, 0.95, 1.0), (x, -3.47, zw + 0.42), "house", 0.0, rot=(-8, 0, 0))
        box(f"fintip{nm}", (0.16, 0.5, 0.1), (x, -3.65, zw + 1.04), "yellow", 0.0)
        box(f"rud{nm}", (0.09, 0.26, 1.3), (x, -4.08, zw + 0.25), "wing", 0.0, rot=(-8, 0, 0))
    box("tailplane", (4.9, 0.95, 0.12), (0, -3.7, zw + 0.55), "wing", 0.04)
    box("tailplanetr", (4.9, 0.22, 0.1), (0, -4.15, zw + 0.55), "wing2", 0.0)
    for s in (-1, 1):
        box(f"tpyellow{s}", (0.4, 0.95, 0.14), (s * 2.4, -3.7, zw + 0.55), "yellow", 0.0)
