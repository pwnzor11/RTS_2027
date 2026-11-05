"""Left Behind Kite (one-use glider bomber, air unit) for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.  Original design.

A hang-glider delta of patched sailcloth on a wooden spar frame (swept wing panels with a house-coloured centre panel and rust and
teal patches, a bamboo keel, rope bracing), a rust-red pilot-less gondola of bus-panel sheet slung beneath, and a big
hazard-banded barrel bomb under the keel.  Small, flimsy and bright from above so it reads as an expendable.
AIR convention: see dir_dirigible.py (body baked at ALT, separate _shadow sheet).  Footprint about 3.8 x 2.8 units (S).
House paint: the centre wing panel, the tail pennant, the gondola band.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lb_common as L

ID = "lb_kite"
TITLE = "Left Behind Kite (glider bomber)"
CANVAS = 192
ALT = 2.4
TARGET = (0.0, 0.0, 2.4)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "shadow"]
PALETTE = L.PALETTE
MATERIALS = L.MATERIALS


def build(S, state, k, n):
    box, cyl, sph = S.box, S.cyl, S.sphere
    S.begin("body")
    z0 = ALT
    zw = z0 + 1.15
    # delta wing: two swept panels + a centre panel, slight dihedral
    for s, nm in ((-1, "L"), (1, "R")):
        box(f"wing{nm}", (1.7, 1.9, 0.05), (s * 1.0, -0.15, zw + 0.1), "canvas", 0.0, rot=(0, -s * 7, -s * 28))
        box(f"wingpatch{nm}", (0.7, 0.7, 0.07), (s * 1.3, -0.3, zw + 0.25), "rust" if s < 0 else "hull", 0.0, rot=(0, -s * 7, -s * 28))
        box(f"wingedge{nm}", (0.08, 2.1, 0.1), (s * 1.75, -0.3, zw + 0.3), "wood", 0.0, rot=(0, -s * 7, -s * 28))
    box("wingmid", (1.3, 2.2, 0.06), (0, 0.0, zw), "canvas2", 0.0)
    box("wingmidhouse", (0.9, 1.1, 0.08), (0, -0.35, zw + 0.03), "house", 0.0)
    box("wingstripe", (1.3, 0.1, 0.08), (0, 0.8, zw + 0.03), "hazard", 0.0)
    cyl("keel", 0.06, 2.9, (0, -0.1, zw - 0.1), "wood", "Y", 6)
    cyl("nosecap", 0.1, 0.2, (0, 1.4, zw - 0.1), "rust2", "Y", 8)
    for s in (-1, 1):
        cyl(f"spar{s}", 0.05, 1.9, (s * 0.95, -0.2, zw + 0.0), "wood", "X", 6, rot=(0, 90 + s * 0, 0))
    # tail: boom, rudder, pennant
    cyl("tailboom", 0.05, 1.0, (0, -1.8, zw - 0.2), "wood", "Y", 6)
    box("rudder", (0.05, 0.6, 0.7), (0, -2.15, zw + 0.1), "rust", 0.0)
    box("elevator", (0.9, 0.4, 0.05), (0, -2.1, zw - 0.2), "canvas", 0.0)
    box("pennant", (0.04, 0.5, 0.25), (0, -2.35, zw + 0.35), "house", 0.0)
    # gondola + barrel bomb slung beneath
    for s in (-1, 1):
        for y in (0.5, -0.4):
            cyl(f"rope{s}{y}", 0.025, 0.95, (s * 0.25, y, zw - 0.55), "rope", "Z", 4)
    box("gondola", (0.6, 1.4, 0.4), (0, 0.1, z0 + 0.35), "hull2", 0.1)
    box("gondolaband", (0.62, 0.5, 0.2), (0, 0.1, z0 + 0.4), "house", 0.0)
    box("gcowl", (0.5, 0.4, 0.3), (0, 0.8, z0 + 0.45), "rust", 0.08)
    cyl("bomb", 0.3, 1.2, (0, 0.0, z0 - 0.05), "rust2", "Y", 12)
    cyl("bombband", 0.32, 0.2, (0, 0.2, z0 - 0.05), "hazard", "Y", 12)
    cyl("bombband2", 0.32, 0.12, (0, -0.35, z0 - 0.05), "bone", "Y", 12)
    S.cone("bombnose", 0.3, 0.05, 0.35, (0, 0.78, z0 - 0.05), "ash", rot=(-90, 0, 0), verts=12)
    box("bombfin", (0.5, 0.2, 0.05), (0, -0.7, z0 - 0.05), "ash2", 0.0)
    box("bombfin2", (0.05, 0.2, 0.5), (0, -0.7, z0 - 0.05), "ash2", 0.0)
