"""Extra parts for the art-exp-d structures (power, supply, refinery, barracks, yard, quay ...).  Original design.  Imports the shared exp kit."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K


def piles(S, x0, x1, y0, y1, n_x, n_y, z1=0.6, r=0.12):
    """Timber piles in a grid under a deck (visible stubs below the skirt board)."""
    for a in range(n_x):
        for b in range(n_y):
            x = x0 + (x1 - x0) * (a + 0.5) / n_x
            y = y0 + (y1 - y0) * (b + 0.5) / n_y
            S.cyl("pile", r, z1, (x, y, z1 / 2), "timber", verts=8)


def hazard(S, x0, x1, y, z0, z1, n=6):
    """Orange / navy diagonal-ish hazard blocks on a north-facing (south-looking) wall."""
    w = (x1 - x0) / n
    for k in range(n):
        if k % 2 == 0:
            S.bb("haz", x0 + k * w, x0 + (k + 1) * w, y - 0.03, y, z0, z1, "orange", 0.0, nocast=True)


def ladder(S, x, y, z0, z1):
    for s in (-0.12, 0.12):
        K.post(S, "lad", x + s, y, z0, z1, 0.04, "chrome2")
    z = z0 + 0.2
    while z < z1:
        S.bb("rung", x - 0.12, x + 0.12, y - 0.02, y + 0.02, z, z + 0.03, "chrome2", 0.0, nocast=True)
        z += 0.25


def tag(S, name, text, x, y, z, px=0.07):
    """White hull-style number on a south-facing plate (flat=False)."""
    K.EV.digits(S, name, text, x, y, z, px, "white", flat=False)


def bubble_lamp(S, name, x, y, z, on):
    S.sphere(name, 0.14, (x, y, z), "lamp" if on else "lamp_off")
