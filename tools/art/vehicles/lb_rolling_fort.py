"""Left Behind Rolling Fort (heavy patchwork tank) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

The big one: mismatched tracks (right longer) under a boxy hull welded from bus roofs, road signs and boiler plate in four metal
tones (teal, rust, ash, ochre), hazard-yellow rivet strips, chain-and-rope lashed barrels and a canvas bedroll on the rear deck,
a stovepipe and a flag mast.  On the turret layer (pivot on the hull centre): a blocky cupola made from a water tank and a bus
cab, a long cannon with a pipe muzzle brake, and house-coloured plates on both sides.
Layers: body (hull + whole-vehicle shadow), turret.  Footprint about 3.8 x 5.8 units (L).
House paint: hull flank panels, the turret side plates and roof panel.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as L

ID = "lb_rolling_fort"
TITLE = "Left Behind Rolling Fort"
CANVAS = 240
TARGET = (0.0, 0.0, 1.5)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT = (0.0, 0.2, 2.25)
MOUNT = dict(deck_z=2.1, r_lip=1.25, r_well=0.0, r_shadow=1.5, soft_r=1.35, lip="rust2", well="dark")
PALETTE = L.PALETTE
MATERIALS = L.MATERIALS


def build(S, state, k, n):
    box, cyl = S.box, S.cyl
    S.begin("body")
    L.tracks(S, -1.7, -2.7, 2.7, "L", "rust", n=6, w=1.0, h=1.0)
    L.tracks(S, 1.7, -2.4, 3.2, "R", "hull2", n=6, w=1.0, h=1.0)
    box("hull", (3.3, 5.3, 0.9), (0, 0.1, 1.55), "hull", 0.1)
    box("glacis", (3.3, 1.0, 0.8), (0, 2.9, 1.45), "ash2", 0.12, rot=(-14, 0, 0))
    box("deck", (3.3, 3.8, 0.12), (0, -0.3, 2.06), "ash", 0.03)
    box("rearplate", (3.3, 0.12, 0.9), (0, -2.6, 1.55), "rust2", 0.03)
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"flank{nm}", (0.08, 3.2, 0.6), (s * 1.68, 0.0, 1.65), "house", 0.0)
    box("sign1", (0.1, 1.2, 0.6), (-1.69, 1.9, 1.7), "hazard", 0.0)
    box("sign2", (0.1, 0.8, 0.5), (-1.69, -1.8, 1.8), "bone", 0.0)
    box("patch1", (0.1, 1.0, 0.5), (1.69, 2.0, 1.75), "rust", 0.0)
    box("patch2", (0.1, 0.9, 0.5), (1.69, -1.8, 1.7), "ochre", 0.0)
    box("patch3", (0.1, 0.8, 0.35), (1.69, -0.9, 1.35), "ash", 0.0)
    box("frontstrip", (3.3, 0.1, 0.18), (0, 3.38, 1.75), "hazard", 0.0, rot=(-14, 0, 0))
    for s in (-1, 1):
        L.lamp(S, f"hl{s}", s * 1.2, 3.45, 1.6)
        box(f"fender{s}", (0.2, 5.0, 0.12), (s * 1.9, 0.2, 2.15), "hull2" if s > 0 else "rust", 0.02)
    # driver hatch + MG nub in the glacis, front left
    box("drvhatch", (0.8, 0.7, 0.25), (-1.0, 2.2, 2.25), "hull2", 0.06)
    box("drvslit", (0.6, 0.05, 0.1), (-1.0, 2.57, 2.3), "glass", 0.0)
    # rear deck: lashed barrels, bedroll, stovepipe, mast flag
    L.barrel(S, "a", -1.0, -2.0, 2.55, "rust", r=0.4, h=0.9, band="hazard")
    L.barrel(S, "b", -0.1, -2.1, 2.55, "hull", r=0.4, h=0.9)
    L.barrel(S, "c", 0.85, -2.0, 2.55, "ash", r=0.4, h=0.9, band="rust")
    cyl("bedroll", 0.32, 1.9, (0.0, -1.5, 3.2), "canvas", "X", 12)
    cyl("bedstrap", 0.34, 0.08, (-0.5, -1.5, 3.2), "rust", "X", 12)
    cyl("bedstrap2", 0.34, 0.08, (0.5, -1.5, 3.2), "rust", "X", 12)
    cyl("rope", 0.03, 2.8, (0, -2.45, 2.5), "rope", "X", 5)
    L.stack(S, "s", 1.35, -1.0, 2.8, 1.3, 0.16)
    L.flag(S, "m", -1.45, -2.4, 2.1, h=2.0)
    # ---- turret
    S.mount(PIVOT, **MOUNT)
    S.begin("turret", PIVOT)
    px, py, pz = PIVOT
    cyl("ring", 1.15, 0.14, (px, py, pz + 0.07), "ash2", "Z", 20)
    box("tbase", (1.9, 1.9, 0.7), (px, py, pz + 0.5), "hull", 0.09)
    box("tfront", (1.8, 0.7, 0.7), (px, py + 1.0, pz + 0.45), "rust", 0.1, rot=(-10, 0, 0))
    box("troof", (2.0, 2.0, 0.1), (px, py - 0.1, pz + 0.9), "rust2", 0.03)
    box("troofpanel", (1.0, 1.2, 0.04), (px - 0.3, py - 0.2, pz + 0.96), "house", 0.0)
    box("tbustwin", (1.5, 0.05, 0.25), (px, py + 1.38, pz + 0.7), "glass", 0.0)
    for s in (-1, 1):
        box(f"tplate{s}", (0.07, 1.5, 0.55), (px + s * 0.97, py - 0.1, pz + 0.55), "house", 0.0)
    box("tpatch", (0.08, 0.6, 0.4), (px - 0.98, py + 0.7, pz + 0.7), "ochre", 0.0)
    cyl("tank", 0.38, 1.2, (px + 0.5, py - 0.85, pz + 1.3), "ochre", "Y", 12)
    cyl("tankband", 0.4, 0.1, (px + 0.5, py - 0.6, pz + 1.3), "rust", "Y", 12)
    cyl("tankband2", 0.4, 0.1, (px + 0.5, py - 1.2, pz + 1.3), "rust", "Y", 12)
    box("mantlet", (0.8, 0.3, 0.6), (px, py + 1.45, pz + 0.5), "ash2", 0.05)
    cyl("cannon", 0.2, 2.8, (px, py + 2.7, pz + 0.5), "steel", "Y", 12)
    cyl("cannonband", 0.25, 0.2, (px, py + 2.0, pz + 0.5), "hazard", "Y", 12)
    cyl("brake", 0.3, 0.5, (px, py + 4.0, pz + 0.5), "rust2", "Y", 12)
    cyl("mouth", 0.2, 0.08, (px, py + 4.27, pz + 0.5), "dark", "Y", 12)
    box("cupola", (0.5, 0.5, 0.25), (px + 0.55, py + 0.1, pz + 1.05), "ash2", 0.05)
    cyl("whip", 0.025, 1.8, (px - 0.85, py - 0.85, pz + 1.8), "dark", "Z", 5)
