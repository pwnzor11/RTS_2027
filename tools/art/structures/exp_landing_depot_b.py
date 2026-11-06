"""Expedition Landing Depot, VARIANT B (3x3): the Command Ship unpacked into a base.  Original design.

B = butterfly hull with a tall bubble-bridge tower.  The ship's two navy hull halves lie folded open to either side (ash tidemark, copper
rail, team-colour stripes) around a central white liner tower topped with a big glass bubble bridge, a radome on a mast and twin chrome
funnels.  South of it a painted orange landing roundel; the telex hut with aerial, supply crates, a jib crane on the east wing and the
flagpole with the team flag.  Idle only (pilot comparison variant): smoke, hook, flag, lamps, radar bar.  Contract: see auth_refinery.py.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K

ID = "exp_landing_depot_b"
TITLE = "Expedition Landing Depot (variant B)"
FOOTPRINT = (3, 3)
CANVAS = (192, 256)
ORIGIN = (0, 64)
DOCK = (1, 3)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5)]


def build(S, state, i, n):
    K.pad(S, 5.0, 5.0)
    for k, (x, y) in enumerate(((-4.7, -4.7), (4.7, -4.7), (-4.7, 4.7), (4.7, 4.7))):
        K.lamp_post(S, str(k), x, y, (i + k) % 2 == 0)
    S.cyl("roundel", 1.5, 0.05, (0, -2.3, 0.14), "orange", verts=32, nocast=True)
    S.cyl("roundel2", 1.2, 0.06, (0, -2.3, 0.14), "pad", verts=32, nocast=True)
    S.bb("hbar1", -0.7, -0.5, -2.9, -1.7, 0.17, 0.2, "white", 0.0, nocast=True)
    S.bb("hbar2", 0.5, 0.7, -2.9, -1.7, 0.17, 0.2, "white", 0.0, nocast=True)
    S.bb("hbar3", -0.5, 0.5, -2.4, -2.2, 0.17, 0.2, "white", 0.0, nocast=True)
    for s, (x0, x1) in ((-1, (-4.8, -1.7)), (1, (1.7, 4.8))):
        S.bb(f"wing{s}", x0, x1, -0.4, 3.6, 0.12, 1.0, "navy", 0.22)
        S.bb(f"wtide{s}", x0 - 0.03, x1 + 0.03, -0.43, 3.63, 0.12, 0.3, "ash", 0.1)
        S.bb(f"wdeck{s}", x0 + 0.2, x1 - 0.2, 0.2, 3.3, 1.0, 1.06, "white2", 0.02)
        S.bb(f"wrail{s}", x0, x1, -0.4, -0.3, 0.96, 1.06, "copper", 0.0)
        S.bb(f"wstripe{s}", x0 + 0.1, x1 - 0.1, -0.44, -0.4, 0.5, 0.74, "house", 0.0)
        S.cyl(f"funnel{s}", 0.42, 2.0, (s * 2.7, 2.2, 2.1), "chrome", verts=20)
        S.cyl(f"funband{s}", 0.45, 0.14, (s * 2.7, 2.2, 1.5), "copper", verts=20)
        S.cyl(f"funhouse{s}", 0.45, 0.28, (s * 2.7, 2.2, 2.9), "house", verts=20)
        S.cyl(f"funcap{s}", 0.38, 0.06, (s * 2.7, 2.2, 3.12), "dark", verts=16)
    K.puffs(S, "fun", 2.7, 2.2, 3.3, i)
    K.EV.digits(S, "hn", "01", -3.2, -0.45, 0.62, 0.12, "white", flat=False)
    # central tower
    S.bb("pod", -1.6, 1.6, 0.4, 3.4, 0.12, 2.4, "white", 0.15)
    S.bb("podband", -1.62, 1.62, 0.38, 3.42, 0.9, 1.1, "navy", 0.0)
    S.bb("podwin", -1.3, 1.3, 0.35, 0.4, 1.4, 1.9, "glass", 0.0, nocast=True)
    S.bb("podtrim", -1.6, 1.6, 0.36, 0.42, 2.3, 2.4, "copper", 0.0)
    S.bb("pdoor", -0.3, 0.3, 0.35, 0.4, 0.12, 0.85, "orange", 0.0, nocast=True)
    S.bb("tier", -1.15, 1.15, 0.8, 3.0, 2.4, 3.4, "white", 0.12)
    S.bb("tierwin", -1.0, 1.0, 0.77, 0.8, 2.7, 3.1, "glass", 0.0, nocast=True)
    S.bb("tierstripe", -1.15, 1.15, 0.76, 0.8, 2.5, 2.62, "house", 0.0, nocast=True)
    S.sphere("bubble", 1.0, (0, 1.9, 3.7), "glass", nocast=False, squash=0.8)
    S.cyl("bubblering", 1.04, 0.1, (0, 1.9, 3.4), "chrome", verts=24)
    for a in (0, 60, 120):
        S.box(f"rib{a}", (2.0, 0.07, 0.07), (0, 1.9 * S.YS, 4.5), "navy2", 0.0, rot=(0, 0, a), stretch=False, nocast=True)
    K.post(S, "radmast", 0, 1.9, 4.4, 5.5, 0.1, "chrome2")
    S.sphere("radome", 0.42, (0, 1.9, 5.8), "white", nocast=False)
    S.cyl("radband", 0.43, 0.08, (0, 1.9, 5.8), "orange", verts=16)
    S.box("radbar", (1.1, 0.08, 0.08), (0, 1.9 * S.YS, 5.2), "chrome", 0.0, rot=(0, 0, i * 45), stretch=False, nocast=True)
    # telex hut, crates
    S.bb("hut", -4.2, -1.8, -4.4, -2.6, 0.12, 1.4, "white", 0.1)
    S.bb("hutroof", -4.3, -1.7, -4.5, -2.5, 1.4, 1.52, "navy", 0.03)
    S.bb("hutstripe", -4.2, -1.8, -4.43, -4.4, 0.95, 1.2, "house", 0.0, nocast=True)
    S.bb("hutwin", -4.0, -3.2, -4.43, -4.4, 0.4, 0.9, "glass", 0.0, nocast=True)
    S.bb("hutdoor", -2.8, -2.2, -4.43, -4.4, 0.12, 1.1, "orange", 0.0, nocast=True)
    K.post(S, "ant", -2.0, -2.8, 1.52, 3.4, 0.06, "chrome2")
    S.bb("dipole", -2.5, -1.5, -2.85, -2.75, 3.1, 3.16, "chrome", 0.0, nocast=True)
    for k in range(2):
        S.bb(f"crate{k}", 3.0 + k * 0.8, 3.7 + k * 0.8, -4.2, -3.5, 0.12, 0.8, "navy" if k == 0 else "orange2", 0.05)
    for k in range(2):
        S.cyl(f"drum{k}", 0.3, 0.8, (2.2 + k * 0.62, -3.9, 0.52), "orange2" if k == 0 else "white2", verts=12)
    # crane on the east wing
    cx, cy = 4.0, 0.2
    for ax in (-0.22, 0.22):
        for ay in (-0.22, 0.22):
            K.post(S, "tw", cx + ax, cy + ay, 1.06, 3.4, 0.08, "chrome2")
    K.beam(S, "jib", (cx, cy, 3.4), (cx - 0.1, cy - 2.6, 3.7), 0.17, "copper")
    sw = 0.18 * math.sin(i * math.pi / 2)
    K.beam(S, "cable", (cx - 0.1, cy - 2.4, 3.65), (cx - 0.1 + sw, cy - 2.4, 1.8), 0.04, "dark", nocast=True)
    S.bb("hook", cx - 0.2 + sw, cx + sw, cy - 2.5, cy - 2.3, 1.6, 1.8, "orange", 0.0)
    K.flag(S, "flag", 0.0, -4.3, 4.4, i)
