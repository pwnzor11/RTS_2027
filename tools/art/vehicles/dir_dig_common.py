"""Shared helpers for the Directorate vehicle modules (M3-ART-VEH1).  Not a vehicle itself; imported by dir_*.py.
Palette: modules set PALETTE = C.PAL_DIR; the MATERIALS below sit on or near PAL_DIR entries so the quantiser keeps them."""
import math, os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import common as C

PALETTE = C.PAL_DIR

MATERIALS = {
    "conc": ("#766F66", dict(rough=0.9)), "conc2": ("#5C5750", dict(rough=0.9)), "conc3": ("#44403B", dict(rough=0.9)),
    "bone": ("#E3DAC2", dict(rough=0.85)), "red": ("#8E2E22", dict(rough=0.85)), "red2": ("#63201A", dict(rough=0.85)),
    "redhi": ("#B8402F", dict(rough=0.8)),
    "steel": ("#555B61", dict(rough=0.5, metal=0.3)), "steel2": ("#3A3F44", dict(rough=0.5, metal=0.3)),
    "track": ("#2E2926", dict(rough=0.95)), "dark": ("#1F1B19", dict(rough=0.9)), "rust": ("#5E3420", dict(rough=0.95)),
    "brass": ("#C9A640", dict(rough=0.5, metal=0.3)), "glass": ("#1F1B19", dict(rough=0.3)),
    "earth": ("#6B523B", dict(rough=1.0)), "earth2": ("#4A3829", dict(rough=1.0)), "dust": ("#928A7E", dict(rough=1.0)),
    "lens": ("#FFE79A", dict(rough=0.3, emit="#FFE08A")),
}


def tracks(S, x, ln, w=0.7, h=0.75, y0=0.0, nwheel=5):
    """Pair of tracked runs with idlers, road wheels and a skirt strip."""
    for s, side in ((-1, "L"), (1, "R")):
        cx = s * x
        S.box("track" + side, (w, ln, h), (cx, y0, h / 2 + 0.04), "track", 0.14)
        for yy, tag in ((y0 + ln / 2 - 0.05, "F"), (y0 - ln / 2 + 0.05, "R")):
            S.cyl("idl" + side + tag, h * 0.56, w, (cx, yy, h * 0.56 + 0.02), "track", "X", 18)
        for i in range(nwheel):
            yy = y0 - ln / 2 + 0.35 + i * (ln - 0.7) / max(1, nwheel - 1)
            S.cyl(f"wh{side}{i}", h * 0.4, 0.1, (cx + s * (w / 2 + 0.02), yy, h * 0.5), "steel2", "X", 14)
            S.cyl(f"hb{side}{i}", h * 0.15, 0.08, (cx + s * (w / 2 + 0.07), yy, h * 0.5), "rust", "X", 8)
        S.box("skirt" + side, (0.08, ln * 0.92, h * 0.45), (cx + s * (w / 2 + 0.0), y0, h + 0.1), "conc3", 0.03)


def bands(S, name, x, y0, y1, z, h, n, thick=0.05):
    """Hazard stripes (alternating red / bone) along y on a flank at x."""
    seg = (y1 - y0) / n
    for i in range(n):
        S.box(f"{name}{i}", (thick, seg, h), (x, y0 + seg * (i + 0.5), z), "bone" if i % 2 else "redhi", 0.0)


def vbands(S, name, y, x0, x1, z, h, n, thick=0.05):
    """Hazard stripes across x on a front/rear face at y."""
    seg = (x1 - x0) / n
    for i in range(n):
        S.box(f"{name}{i}", (seg, thick, h), (x0 + seg * (i + 0.5), y, z), "bone" if i % 2 else "redhi", 0.0)


def drill(S, y_base, length, r_base, z, n=5, tip_r=0.05, mats=("redhi", "bone")):
    """Conical auger along +Y built from banded cone segments (barber-pole reads at 1x), base at y_base."""
    seg = length / n
    for i in range(n):
        r1 = r_base + (tip_r - r_base) * i / n
        r2 = r_base + (tip_r - r_base) * (i + 1) / n
        S.cone(f"drill{i}", r1, r2, seg, (0, y_base + seg * (i + 0.5), z), mats[i % 2], 20, rot=(-90, 0, 0))


DIVE = [(10.0, 0.12), (24.0, 0.6), (36.0, 1.35), (46.0, 2.5)]


def dive_pre(S, k, pivot, depth=1.0):
    pitch, dz = DIVE[k % 4]
    S.set_pose(pitch, dz * depth, pivot)


def dive_post(S, k, halfx, halfy, y0=0.0):
    """Ground clip + churned-earth ring and dark pit around the hole, spoil mounds and dust puffs (after the vehicle parts)."""
    S.set_pose()
    S.clip_below_ground()
    grow = (0.8, 0.92, 1.0, 1.0)[k % 4]
    S.sphere("crater", 1.0, (0, y0, 0.0), "earth", scale=(halfx * grow, halfy * grow, 0.1))
    S.sphere("pit", 1.0, (0, y0, 0.0), "earth2", scale=(halfx * grow * 0.82, halfy * grow * 0.82, 0.125))
    spoil = ((-0.95, 0.5, 0.3), (1.0, 0.25, 0.26), (-0.8, -0.75, 0.24), (0.85, -0.85, 0.28), (0.15, 1.0, 0.22), (-0.2, -1.0, 0.2))
    for i, (x, y, r) in enumerate(spoil):
        rr = r * (0.6 + 0.2 * (k % 4))
        S.sphere(f"spoil{i}", rr, (x * halfx / 1.6, y0 + y * halfy / 1.8, rr * 0.45), "earth" if i % 2 else "earth2", scale=(1, 1, 0.6))
    t = (k % 4) / 3.0
    for i, (x, y) in enumerate(((-0.85, 0.7), (0.9, 0.45), (-0.5, -0.8), (0.6, -0.65), (0.0, 0.95))):
        r = (0.16 + 0.26 * t) * (1.0 - 0.1 * i)
        S.sphere(f"dust{i}", r, (x * halfx / 1.6, y0 + y * halfy / 1.9, 0.2 + 0.45 * t + 0.06 * i), "dust", nocast=True)
