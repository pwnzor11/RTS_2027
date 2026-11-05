"""Shared Left Behind vehicle parts (PAL_LB) for tools/art/build_vehicle.py modules.  Import with
sys.path.insert(0, <this dir>); `import lb_common as L`.  Same look as lb_mother_rig_packed: mismatched patch plating,
canvas, rust, faded teal bus panels, lashed barrels, house colour on flags and panels.  Original design."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C

PALETTE = C.PAL_LB
MATERIALS = {
    "hull": ("#4F746E", dict(rough=0.8)), "hull2": ("#34524E", dict(rough=0.8)),
    "dust": ("#B08B52", dict(rough=1.0)), "canvas": ("#D6C8A2", dict(rough=1.0)), "canvas2": ("#B5A57C", dict(rough=1.0)),
    "ochre": ("#B38A44", dict(rough=0.9)), "rust": ("#8A3E24", dict(rough=0.95)), "rust2": ("#5A2A1A", dict(rough=0.95)),
    "ash": ("#7A766F", dict(rough=0.8)), "ash2": ("#55524D", dict(rough=0.8)), "bone": ("#E6DDC4", dict(rough=0.85)),
    "hazard": ("#DDB93A", dict(rough=0.8)), "yellow": ("#DDB93A", dict(rough=0.8)), "dark": ("#1A140E", dict(rough=0.9)),
    "track": ("#2A2320", dict(rough=0.95)), "steel": ("#8E8A82", dict(rough=0.5, metal=0.5)), "wood": ("#8A6A3E", dict(rough=0.95)),
    "rope": ("#C8B88A", dict(rough=1.0)), "glass": ("#2F4A47", dict(rough=0.25, metal=0.3)),
    "lens": ("#FFE08A", dict(rough=0.3, emit="#FFE08A")), "heap": ("#6B6762", dict(rough=1.0)),
}


def tracks(S, x, y0, y1, tag, skirt, n=5, w=0.85, h=0.9):
    """One track unit: belt, two idlers, road wheels (one mismatched), side skirt plus a patch."""
    ln, yc = y1 - y0, (y0 + y1) / 2
    sx = math.copysign(1, x)
    S.box(f"trk{tag}", (w, ln - 0.7, h), (x, yc, h / 2 + 0.1), "track", 0.12)
    for yy, nm in ((y1 - 0.4, "A"), (y0 + 0.4, "B")):
        S.cyl(f"id{tag}{nm}", h * 0.55, w, (x, yy, h / 2 + 0.1), "track", "X", 16)
    for k in range(n):
        y = y0 + 0.55 + k * (ln - 1.1) / max(n - 1, 1)
        r = h * 0.38 if k != n // 2 else h * 0.3
        S.cyl(f"wh{tag}{k}", r, 0.1, (x + sx * (w / 2 + 0.03), y, h / 2 + 0.05), "steel" if k % 2 == 0 else "rust", "X", 14)
    S.box(f"skirt{tag}", (0.08, ln - 0.5, 0.4), (x + sx * (w / 2 + 0.02), yc, h + 0.15), skirt, 0.02)
    S.box(f"skpatch{tag}", (0.1, 0.8, 0.28), (x + sx * (w / 2 + 0.03), yc + ln * 0.2, h + 0.18), "ochre" if tag == "L" else "ash", 0.0)


def wheel(S, tag, x, y, r, tw=0.34, hub="rust"):
    S.cyl(f"tyre{tag}", r, tw, (x, y, r), "track", "X", 16)
    S.cyl(f"hub{tag}", r * 0.5, tw + 0.04, (x, y, r), hub, "X", 10)
    S.cyl(f"nut{tag}", r * 0.18, tw + 0.08, (x, y, r), "dark", "X", 6)


def barrel(S, tag, x, y, z, mat="rust", r=0.4, h=0.85, band=None, axis="Z"):
    S.cyl(f"drum{tag}", r, h, (x, y, z), mat, axis, 12)
    if band:
        S.cyl(f"dband{tag}", r + 0.02, 0.1, (x, y, z), band, axis, 12)


def stack(S, tag, x, y, z, h=1.2, r=0.13):
    S.cyl(f"pipe{tag}", r, h, (x, y, z), "ash2", "Z", 10)
    S.cyl(f"pipecap{tag}", r * 1.4, 0.07, (x, y, z + h / 2), "rust2", "Z", 10)


def flag(S, tag, x, y, z, h=1.4, w=0.7):
    """Mast with a house flag (flat panel, edge-on to the east/west so it reads from above-front)."""
    S.cyl(f"mast{tag}", 0.05, h, (x, y, z + h / 2), "wood", "Z", 8)
    S.box(f"flag{tag}", (0.04, w, 0.45), (x, y - w / 2, z + h - 0.25), "house", 0.0)


def lamp(S, tag, x, y, z):
    S.cyl(f"lamp{tag}", 0.17, 0.1, (x, y, z), "lens", "Y", 10)


def cab(S, x, y, z0, w=1.7, d=1.5, h=1.3, body="hull", roof="rust2", win=True):
    """Bus-panel cab: faded teal box, patched roof, glass front and side."""
    S.box("cab", (w, d, h), (x, y, z0 + h / 2), body, 0.08)
    S.box("cabroof", (w + 0.1, d + 0.1, 0.1), (x, y, z0 + h + 0.03), roof, 0.03)
    if win:
        S.box("cabwin", (w - 0.3, 0.06, 0.45), (x, y + d / 2 + 0.02, z0 + h * 0.65), "glass", 0.0)
        S.box("cabswinR", (0.06, d * 0.55, 0.45), (x + w / 2 + 0.02, y + d * 0.1, z0 + h * 0.65), "glass", 0.0)
        S.box("cabswinL", (0.06, d * 0.55, 0.45), (x - w / 2 - 0.02, y + d * 0.1, z0 + h * 0.65), "glass", 0.0)
