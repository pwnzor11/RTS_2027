"""Left Behind Mother Rig, packed (for tools/art/build_vehicle.py).  Contract: see auth_jeep.py.  Original design.

The same object folded up: a big lopsided tracked flatbed crawler.  The teal bus body sits low on the deck (house panels on the
flanks and the roof), the rust-red truck cab has swung round to the front right, the lean-to canvas is rolled into a bolster along
the left of the roof, the awning is rolled across the nose of the bus, the stovepipes are lowered and lashed along the roof, the
antenna bundle lies in a rack on the left with its dish folded flat, the mast and its flag (house colour, furled) are strapped to
the right flank, and the barrels and crates ride at the back.  The tracks are mismatched: the right one is longer and set forward.
Footprint: 6.6 world units long and 4.0 wide (1.9 x 1.2 cells); at any of the 16 facings it fits a 3x3-cell box; frame 176 px.
House paint: both flank panels, the roof panel, the furled flag and a stripe on the rear crate.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C

ID = "lb_mother_rig_packed"
TITLE = "Left Behind Mother Rig (packed)"
CANVAS = 176
TARGET = (0.0, 0.0, 1.4)
STATES = [("idle", 16, 1, 1)]
PALETTE = C.PAL_LB
MATERIALS = {
    "hull": ("#4F746E", dict(rough=0.8)), "hull2": ("#34524E", dict(rough=0.8)),
    "dust": ("#B08B52", dict(rough=1.0)), "canvas": ("#D6C8A2", dict(rough=1.0)), "canvas2": ("#B5A57C", dict(rough=1.0)),
    "ochre": ("#B38A44", dict(rough=0.9)), "rust": ("#8A3E24", dict(rough=0.95)), "rust2": ("#5A2A1A", dict(rough=0.95)),
    "ash": ("#7A766F", dict(rough=0.8)), "ash2": ("#55524D", dict(rough=0.8)), "bone": ("#E6DDC4", dict(rough=0.85)),
    "hazard": ("#DDB93A", dict(rough=0.8)), "yellow": ("#DDB93A", dict(rough=0.8)), "dark": ("#1A140E", dict(rough=0.9)),
    "track": ("#2A2320", dict(rough=0.95)), "steel": ("#8E8A82", dict(rough=0.5, metal=0.5)), "wood": ("#8A6A3E", dict(rough=0.95)),
    "rope": ("#C8B88A", dict(rough=1.0)), "glass": ("#2F4A47", dict(rough=0.25, metal=0.3)),
    "lens": ("#FFE08A", dict(rough=0.3, emit="#FFE08A")),
}


def track(S, x, y0, y1, tag, m_skirt):
    ln, yc = y1 - y0, (y0 + y1) / 2
    S.box(f"trk{tag}", (0.85, ln - 0.7, 0.9), (x, yc, 0.55), "track", 0.12)
    for yy, nm in ((y1 - 0.4, "A"), (y0 + 0.4, "B")):
        S.cyl(f"id{tag}{nm}", 0.5, 0.85, (x, yy, 0.55), "track", "X", 16)
    n = 5
    for k in range(n):
        y = y0 + 0.55 + k * (ln - 1.1) / (n - 1)
        r = 0.34 if k != 2 else 0.28                     # mismatched road wheel
        S.cyl(f"wh{tag}{k}", r, 0.1, (x + math.copysign(0.45, x), y, 0.5), "steel" if k % 2 == 0 else "rust", "X", 14)
    S.box(f"skirt{tag}", (0.08, ln - 0.5, 0.4), (x + math.copysign(0.44, x), yc, 1.05), m_skirt, 0.02)
    S.box(f"skirtpatch{tag}", (0.1, 0.9, 0.3), (x + math.copysign(0.45, x), yc + 0.6, 1.08), "ochre" if tag == "L" else "ash", 0.0)


def build(S, state, k, n):
    box, cyl, cone, sphere = S.box, S.cyl, S.cone, S.sphere
    S.begin("body")
    track(S, -1.55, -2.9, 2.9, "L", "rust")
    track(S, 1.55, -2.2, 3.5, "R", "hull2")
    # flatbed deck, bumpers, hitch
    box("deck", (4.4, 6.4, 0.2), (0, 0.15, 1.12), "wood", 0.03)
    box("frameL", (0.2, 6.4, 0.3), (-2.2, 0.15, 1.0), "ash2", 0.02)
    box("frameR", (0.2, 6.2, 0.3), (2.2, 0.35, 1.0), "ash2", 0.02)
    box("bumper", (4.2, 0.2, 0.34), (0, 3.45, 1.0), "hazard", 0.02)
    box("hitch", (0.7, 0.4, 0.3), (0.2, -3.3, 1.0), "dark", 0.02)
    # bus body, low and folded, teal with mismatched patches
    box("bus", (2.9, 4.2, 1.5), (-0.35, -0.7, 2.0), "hull", 0.08)
    box("busroof", (3.0, 4.3, 0.12), (-0.35, -0.7, 2.8), "ash", 0.03)
    box("busstripe", (3.0, 4.0, 0.12), (-0.35, -0.7, 2.25), "bone", 0.0)
    box("panelL", (0.06, 2.4, 0.7), (-1.83, -0.7, 2.0), "house", 0.0)           # house panel, left flank
    box("panelR", (0.06, 2.0, 0.7), (1.13, -0.9, 2.0), "house", 0.0)            # house panel, right flank
    box("panelroof", (1.5, 1.7, 0.05), (-0.5, -1.7, 2.88), "house", 0.0)         # house panel on the roof (seen from above)
    box("patch1", (0.07, 1.0, 0.5), (-1.84, 1.0, 2.3), "rust", 0.0)
    box("patch2", (0.07, 0.8, 0.45), (1.14, 0.8, 1.7), "ash", 0.0)
    box("patch3", (0.07, 0.7, 0.4), (-1.84, -2.4, 1.65), "rust2", 0.0)
    box("rearplate", (2.9, 0.08, 1.2), (-0.35, -2.82, 2.0), "hull2", 0.0)
    for i, y in enumerate((-2.0, -1.0, 0.0, 1.0)):
        box(f"slitL{i}", (0.07, 0.6, 0.3), (-1.84, y + 0.55, 2.55), "glass" if i != 2 else "wood", 0.0)
    # truck cab swung to the front right, rust red, with a lamp and a stack
    box("cab", (1.7, 1.7, 1.4), (0.75, 2.4, 1.9), "rust", 0.08)
    box("cabroof", (1.8, 1.8, 0.1), (0.75, 2.4, 2.65), "ash2", 0.03)
    box("cabwin", (1.4, 0.06, 0.5), (0.75, 3.27, 2.15), "glass", 0.0)
    box("cabsidewin", (0.06, 0.9, 0.5), (1.62, 2.4, 2.15), "glass", 0.0)
    box("hood", (1.5, 0.8, 0.8), (0.75, 3.2, 1.65), "rust2", 0.06)
    cyl("lampR", 0.17, 0.1, (1.25, 3.62, 1.7), "lens", "Y", 10)
    cyl("lampL", 0.17, 0.1, (0.25, 3.62, 1.7), "lens", "Y", 10)
    cyl("cabstack", 0.12, 1.0, (1.35, 1.8, 3.1), "ash2", "Z", 10)
    # lean-to canvas rolled into a bolster along the left of the roof; awning rolled across the nose of the bus
    cyl("leanroll", 0.46, 3.8, (-1.15, -0.7, 3.25), "canvas", "Y", 14)
    cyl("leanroll2", 0.3, 3.4, (-1.15, -0.5, 3.75), "canvas2", "Y", 12)
    for y in (-2.1, -0.9, 0.3, 1.1):
        cyl(f"strap{y}", 0.5, 0.08, (-1.15, y, 3.25), "rope", "Y", 14)
    cyl("awnroll", 0.28, 3.1, (-0.4, 1.55, 2.0), "canvas2", "X", 12)
    cyl("awnstripe", 0.3, 0.3, (-1.4, 1.55, 2.0), "rust", "X", 12)
    cyl("awnstripe2", 0.3, 0.3, (0.7, 1.55, 2.0), "hull2", "X", 12)
    # stovepipes lowered and lashed along the roof (right), water tank across the back of the roof
    cyl("pipe1", 0.18, 3.2, (0.6, -0.5, 3.05), "ash", "Y", 10)
    cyl("pipe1band", 0.22, 0.12, (0.6, 0.6, 3.05), "rust", "Y", 10)
    cyl("pipe2", 0.14, 2.2, (0.2, -1.2, 3.0), "rust", "Y", 10)
    cyl("tank", 0.5, 2.4, (-0.4, -2.2, 3.35), "ochre", "X", 14)
    cyl("tankband", 0.53, 0.14, (-1.1, -2.2, 3.35), "rust", "X", 14)
    cyl("tankband2", 0.53, 0.14, (0.3, -2.2, 3.35), "rust", "X", 14)
    # antenna bundle lying in a rack, dish folded flat, mast and furled house flag strapped to the right flank
    box("rack", (0.5, 2.6, 0.12), (1.45, -1.4, 2.88), "ash2", 0.0)
    for i, dx in enumerate((-0.12, 0.0, 0.12, 0.06)):
        cyl(f"whip{i}", 0.04, 3.4 - 0.4 * i, (1.45 + dx, -1.4 - 0.2 * i, 3.0 + 0.1 * (i % 2)), "dark", "Y", 6)
    cyl("dish", 0.55, 0.07, (1.45, 0.05, 3.15), "bone", "Z", 16)
    cyl("mast", 0.07, 4.8, (2.0, -0.5, 1.9), "wood", "Y", 8)
    cyl("flagroll", 0.2, 1.5, (2.0, 1.2, 1.97), "house", "Y", 12)
    for y in (-1.2, 0.4):
        cyl(f"mstrap{y}", 0.12, 0.08, (2.0, y, 1.95), "rope", "Y", 8)
    sphere("masttip", 0.1, (2.0, -2.9, 1.9), "hazard")
    # barrels and crates at the back of the deck, lashed; one hazard-banded
    cyl("drum0", 0.42, 0.9, (1.3, -2.6, 1.67), "rust", "Z", 12)
    cyl("drum1", 0.42, 0.9, (1.3, -3.0, 1.67), "hull", "Z", 12)
    cyl("drumband", 0.44, 0.1, (1.3, -2.6, 1.7), "hazard", "Z", 12)
    box("crate", (0.9, 0.9, 0.7), (-1.55, -3.0, 1.55), "wood", 0.03)
    box("crateh", (0.7, 0.04, 0.3), (-1.55, -3.46, 1.55), "house", 0.0)
    box("tarp", (1.0, 0.8, 0.35), (-0.35, -3.1, 1.5), "canvas2", 0.12)
    # lamp on a pole at the rear, ash-grey exhaust pipe
    cyl("exh", 0.1, 1.4, (-1.95, -2.2, 3.3), "ash2", "Z", 8)
    cyl("exhcap", 0.16, 0.08, (-1.95, -2.2, 4.02), "rust2", "Z", 8)
