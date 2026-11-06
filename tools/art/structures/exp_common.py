"""Shared kit for the Expedition structure modules (not an asset itself).  Original design.

Atompunk 1962 (design 07 sec. 8): deep navy, lagoon white, brushed chrome with copper trim, signal orange markings and landing lights, team
colour ("house") on stripes and flags.  Materials in PAL_EXP tones (the palette lives in tools/art/vehicles/exp_common.py), a soot skin
for the damaged state, a primer skin for the second-to-last build frame, geometry helpers and the build-stage driver.
Units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0).  Generic helpers (beam, post, fx anchors) are reused from lb_common.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
import importlib.util
sys.path.insert(0, HERE)
_sp = importlib.util.spec_from_file_location("exp_vehicle_common", os.path.join(os.path.dirname(HERE), "vehicles", "exp_common.py"))
EV = importlib.util.module_from_spec(_sp)      # vehicle kit (PAL_EXP, hull-number digits); loaded under another name, both files are called exp_common
_sp.loader.exec_module(EV)
import lb_common as K              # generic beam / post / fx anchor maths

PALETTE = EV.PAL_EXP
beam, post, fx = K.beam, K.post, K.fx

MATERIALS = {
    "navy": ("#2B4A82", dict(rough=0.4, metal=0.1)), "navy2": ("#1B2D52", dict(rough=0.45)),
    "white": ("#E6F0EC", dict(rough=0.5)), "white2": ("#B2C6C8", dict(rough=0.5)),
    "chrome": ("#A9B3BA", dict(rough=0.3, metal=0.8)), "chrome2": ("#6D767E", dict(rough=0.4, metal=0.7)),
    "copper": ("#B7692F", dict(rough=0.35, metal=0.7)), "orange": ("#F2751F", dict(rough=0.5)), "orange2": ("#B8481A", dict(rough=0.55)),
    "glass": ("#2B7F95", dict(rough=0.12, metal=0.4)), "ash": ("#5E5C55", dict(rough=1.0)), "dark": ("#1B2233", dict(rough=0.9)),
    "pad": ("#5E6B78", dict(rough=0.95)), "pad2": ("#4A5663", dict(rough=0.95)),
    "lamp": ("#FFB45E", dict(rough=0.3, emit="#FFB45E")), "lamp_off": ("#8A4A22", dict(rough=0.6)),
    "timber": ("#A25F30", dict(rough=0.9)), "rope": ("#D8C8A0", dict(rough=1.0)),
    "smoke": ("#B8C4C6", dict(rough=1.0)), "smoke2": ("#79838C", dict(rough=1.0)),
    "flame": ("#F2A030", dict(rough=0.5, emit="#F2A030")), "spark": ("#FFF0B0", dict(rough=0.3, emit="#FFF0B0")),
    "char": ("#15161A", dict(rough=1.0)), "primer": ("#8A949C", dict(rough=0.9)),
    "navy_s": ("#18253F", dict(rough=0.9)), "navy2_s": ("#101A2E", dict(rough=0.9)), "white_s": ("#8D9A9A", dict(rough=0.9)),
    "chrome_s": ("#5B636A", dict(rough=0.8)), "copper_s": ("#6A4026", dict(rough=0.9)), "orange_s": ("#8A4A22", dict(rough=0.9)),
    "pad_s": ("#363F49", dict(rough=1.0)), "glass_s": ("#1B3C46", dict(rough=0.5)),
}
SOOT_SKIN = {"navy": "navy_s", "navy2": "navy2_s", "white": "white_s", "white2": "white_s", "chrome": "chrome_s", "chrome2": "chrome_s",
             "copper": "copper_s", "orange": "orange_s", "pad": "pad_s", "glass": "glass_s"}
PRIMER_SKIN = {"house": "primer", "navy": "primer", "navy2": "primer", "white": "primer", "white2": "primer", "orange": "primer",
               "copper": "primer", "lamp": "dark", "glass": "dark", "chrome": "primer"}


def pad(S, hx, hy, z=0.12):
    """Poured landing apron with orange edge line and a few weathering scuffs (never clean)."""
    S.bb("pad", -hx, hx, -hy, hy, 0.0, z, "pad", 0.03)
    for sx in (-1, 1):
        S.bb("edgeX", sx * (hx - 0.25) - 0.06, sx * (hx - 0.25) + 0.06, -hy + 0.2, hy - 0.2, z, z + 0.02, "orange", 0.0, nocast=True)
    S.bb("edgeS", -hx + 0.2, hx - 0.2, -hy + 0.2 - 0.06, -hy + 0.2 + 0.06, z, z + 0.02, "orange", 0.0, nocast=True)
    S.bb("scuff1", -hx + 0.5, -hx * 0.1, -hy + 0.6, -hy + 1.0, z, z + 0.02, "pad2", 0.0, nocast=True)
    S.bb("scuff2", hx * 0.2, hx - 0.6, -hy + 1.2, -hy + 1.5, z, z + 0.02, "pad2", 0.0, nocast=True)


def lamp_post(S, tag, x, y, on):
    post(S, f"lp{tag}", x, y, 0.12, 0.9, 0.1, "chrome2")
    S.sphere(f"lamp{tag}", 0.14, (x, y, 1.0), "lamp" if on else "lamp_off")


def flag(S, name, mx, my, ztop, anim, w=1.5, h=0.9, dmg=False):
    """House-colour pennant flag on a chrome mast (copper finial); flaps with anim 0..3."""
    ph = anim * math.pi / 2
    post(S, name + "mast", mx, my, 0.12, ztop, 0.09, "chrome")
    S.sphere(name + "knob", 0.1, (mx, my, ztop + 0.08), "copper")
    if dmg:
        S.box(name, (w * 0.55, 0.05, h), (mx + 0.1 + w * 0.28, (my - 0.08) * S.YS, ztop - h / 2 - 0.1), "house", 0.0, stretch=False)
        return
    fw = w + 0.18 * math.sin(ph)
    zc = ztop - h / 2 - 0.08
    S.box(name, (fw, 0.05, h), (mx + 0.06 + fw / 2, (my - 0.08 + 0.05 * math.cos(ph)) * S.YS, zc), "house", 0.0, rot=(0, 0, 5 * math.sin(ph)), stretch=False)
    S.box(name + "s", (fw, 0.06, 0.1), (mx + 0.06 + fw / 2, (my - 0.1) * S.YS, zc - h / 2 + 0.06), "orange", 0.0, rot=(0, 0, 5 * math.sin(ph)), stretch=False, nocast=True)


def puffs(S, name, x, y, z, i, n=3, drift=0.9):
    for q in range(n):
        ph = ((i / 4) + q / n) % 1
        rr = 0.14 + 0.34 * math.sin(math.pi * ph) ** 0.7
        S.sphere(f"{name}puff{q}", rr, (x + ph * drift, y, z + ph * 0.9), "smoke2" if q != 1 else "smoke", squash=0.85)


def stakes_site(S, hx, hy):
    """Build frame 0: surveyed plot with orange-flagged stakes, survey line and a signboard."""
    S.bb("pad", -hx - 0.1, hx + 0.1, -hy - 0.1, hy + 0.1, 0.0, 0.1, "pad2", 0.03)
    for (sx, sy) in ((-hx, -hy), (hx, -hy), (-hx, hy), (hx, hy)):
        post(S, "stake", sx, sy, 0.1, 1.0, 0.12, "timber")
        S.bb("stakeflag", sx, sx + 0.35, sy - 0.02, sy + 0.02, 0.7, 1.0, "orange", 0.0, nocast=True)
    S.bb("line", -hx, hx, -hy - 0.04, -hy + 0.04, 0.88, 0.92, "rope", 0.0, nocast=True)
    S.bb("line", -hx, hx, hy - 0.04, hy + 0.04, 0.88, 0.92, "rope", 0.0, nocast=True)
    post(S, "boardp", -0.5, -hy - 0.2, 0.1, 1.2, 0.1, "timber")
    post(S, "boardp", 0.5, -hy - 0.2, 0.1, 1.2, 0.1, "timber")
    S.bb("board", -0.8, 0.8, -hy - 0.3, -hy - 0.15, 0.7, 1.5, "navy2", 0.0)
    S.bb("boardc", -0.6, 0.6, -hy - 0.32, -hy - 0.3, 0.95, 1.3, "white", 0.0, nocast=True)
    S.cyl("kitroll", 0.3, 1.4, (-hx * 0.4, hy * 0.3, 0.4), "white2", axis="X", verts=12)
    S.bb("kitcrate", hx * 0.2, hx * 0.7, hy * 0.1, hy * 0.6, 0.1, 0.6, "navy", 0.06)


def stage_driver(S, state, i, n, groups, site_fn, dmg_fn=None):
    """idle/damaged: every group.  build: frame 0 = surveyed site, last = finished, last-1 = finished in primer minus the top group,
    frames between add groups progressively."""
    building = state == "build"
    dmg = state == "damaged"
    if dmg:
        S.skin.update(SOOT_SKIN)
    if not building:
        for g in groups:
            g(S, dmg, i)
        if dmg and dmg_fn:
            dmg_fn(S, i)
        return
    last = n - 1
    if i == 0:
        site_fn(S)
        return
    if i == last:
        for g in groups:
            g(S, False, 0)
        return
    k = max(1, round(i * len(groups) / last))
    if i == last - 1:
        k = len(groups) - 1 if len(groups) > 1 else 1
        S.skin.update(PRIMER_SKIN)
    for g in groups[:k]:
        g(S, False, 0)
