"""Expedition Landing Depot, VARIANT A (3x3): the Command Ship unpacked into a base.  Original design.

A = long beached ship hull with a liner superstructure.  The navy hull lies across the north half of the pad on its grey ash tidemark
with copper rail and a team-colour hull stripe; its south side deck has folded open into a chrome ramp; on it a lagoon-white 1960s liner
stack of decks, a glass bridge with a bubble, a chrome funnel (copper and team-colour bands, steaming) and a radar mast with a
turning bar.  On the pad: the telex hut with its aerial, supply crates and drums, a jib crane with a swinging hook, orange landing lamps
at the corners and the flagpole with the team flag.  Idle loop: smoke, hook, flag, lamps, radar bar.  Damage (structural, D-069): bow
and bridge torn open, crane bent, flag in rags; fire and smoke are FX anchors.  Build: surveyed site, apron, hull, decks, annex, crane.
Contract: see auth_refinery.py.  Units: 3.4 per cell, footprint centred on (0, 0): x, y in [-5.1, 5.1].
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as K

ID = "exp_landing_depot"
TITLE = "Expedition Landing Depot (variant A)"
FOOTPRINT = (3, 3)
CANVAS = (192, 256)
ORIGIN = (0, 64)
DOCK = (1, 3)
CAP_MATERIAL = "pad2"
PALETTE = K.PALETTE
MATERIALS = K.MATERIALS
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]
FX = [
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_large", 3.4, 2.1, 1.7, 0),      # torn bow
    K.fx(FOOTPRINT, ORIGIN, "damaged", "fire_small", -2.4, 2.2, 2.4, 4),     # deck stack
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", 3.4, 2.1, 2.0, 0),
    K.fx(FOOTPRINT, ORIGIN, "damaged", "smoke_plume", -2.4, 2.2, 2.7, 5),
]


def g_ground(S, dmg, i):
    K.pad(S, 5.0, 5.0)
    for k, (x, y) in enumerate(((-4.7, -4.7), (4.7, -4.7), (-4.7, 4.7), (4.7, 4.7))):
        K.lamp_post(S, str(k), x, y, (i + k) % 2 == 0 and not dmg)


def g_hull(S, dmg, i):
    S.bb("hull", -4.6, 3.4, 0.6, 3.6, 0.12, 1.5, "navy", 0.25)
    S.bb("tide", -4.63, 3.43, 0.57, 3.63, 0.12, 0.34, "ash", 0.12)
    S.cyl("bow", 1.5, 1.3, (3.4, 2.1, 0.77), "navy", verts=24)
    S.cyl("bowtide", 1.53, 0.22, (3.4, 2.1, 0.23), "ash", verts=24)
    S.bb("rail", -4.6, 3.4, 0.6, 0.7, 1.46, 1.56, "copper", 0.0)
    S.bb("hstripe", -4.5, 3.3, 0.56, 0.6, 0.78, 1.02, "house", 0.0)
    K.EV.digits(S, "hn", "01", -3.6, 0.55, 0.9, 0.13, "white", flat=False)
    S.bb("hatch1", -0.4, 1.0, 1.5, 2.2, 1.5, 1.6, "chrome2", 0.04)
    # folded-open south deck: chrome ramp down to the apron with orange edge rails
    run, rise = 1.9, 1.0
    ang = math.degrees(math.atan2(rise, run * S.YS))
    ln = math.hypot(run * S.YS, rise) / S.YS
    S.box("ramp", (3.4, ln, 0.1), (-1.2, 0.6 - run / 2, 0.12 + rise / 2 + 0.1), "chrome2", 0.02, rot=(ang, 0, 0))
    for s in (-1, 1):
        S.box(f"ramprail{s}", (0.12, ln, 0.16), (-1.2 + s * 1.65, 0.6 - run / 2, 0.12 + rise / 2 + 0.2), "orange", 0.0, rot=(ang, 0, 0))


def g_super(S, dmg, i):
    S.bb("deck1", -3.4, 0.6, 1.2, 3.2, 1.5, 2.2, "white", 0.12)
    S.bb("deck2", -2.8, 0.0, 1.5, 3.0, 2.2, 2.8, "white", 0.1)
    S.bb("trim1", -3.4, 0.6, 1.17, 1.23, 2.1, 2.2, "copper", 0.0)
    S.bb("win1", -3.2, 0.4, 1.17, 1.2, 1.75, 2.0, "glass", 0.0, nocast=True)
    S.bb("win2", -2.6, -0.2, 1.47, 1.5, 2.4, 2.62, "glass", 0.0, nocast=True)
    S.bb("dstripe", -3.4, 0.6, 1.17, 1.2, 1.55, 1.65, "house", 0.0, nocast=True)
    if not dmg:
        S.bb("bridge", -2.2, -0.4, 1.7, 2.8, 2.8, 3.3, "white2", 0.1)
        S.bb("bwin", -2.1, -0.5, 1.67, 1.7, 2.95, 3.2, "glass", 0.0, nocast=True)
        S.sphere("bubble", 0.36, (-1.3, 2.25, 3.4), "glass", nocast=False, squash=0.8)
        K.post(S, "radmast", -2.5, 2.5, 3.3, 4.3, 0.1, "chrome2")
        S.sphere("radome", 0.34, (-2.5, 2.5, 4.55), "white", nocast=False)
        a = math.radians(i * 45)
        S.box("radbar", (1.0, 0.1, 0.1), (-2.5 + 0.0, 2.5 * S.YS, 4.0), "chrome", 0.0, rot=(0, 0, math.degrees(a)), stretch=False, nocast=True)
    else:
        S.bb("bridge", -2.2, -0.4, 1.7, 2.8, 2.8, 3.0, "white2", 0.1)
        S.box("bridgetorn", (1.2, 0.8, 0.12), (-1.0, 2.2 * S.YS, 3.1), "navy2", 0.0, rot=(0, 18, 20), stretch=False)
    top = 3.8 if not dmg else 3.1
    S.cyl("funnel", 0.5, top - 2.2, (-1.2, 2.5, 2.2 + (top - 2.2) / 2), "chrome", verts=20)
    S.cyl("funband", 0.53, 0.14, (-1.2, 2.5, 2.4), "copper", verts=20)
    S.cyl("funhouse", 0.53, 0.3, (-1.2, 2.5, top - 0.35), "house", verts=20)
    S.cyl("funcap", 0.46, 0.06, (-1.2, 2.5, top + 0.02), "dark", verts=16)
    if not dmg:
        K.puffs(S, "fun", -1.2, 2.5, top + 0.2, i)


def g_annex(S, dmg, i):
    S.bb("hut", 1.6, 4.2, -4.4, -2.2, 0.12, 1.5, "white", 0.1)
    S.bb("hutroof", 1.5, 4.3, -4.5, -2.1, 1.5, 1.62, "navy", 0.03)
    S.bb("hutstripe", 1.6, 4.2, -4.43, -4.4, 1.0, 1.25, "house", 0.0, nocast=True)
    S.bb("hutwin", 1.8, 2.6, -4.43, -4.4, 0.45, 0.95, "glass", 0.0, nocast=True)
    S.bb("hutdoor", 3.2, 3.8, -4.43, -4.4, 0.12, 1.15, "orange", 0.0, nocast=True)
    K.post(S, "ant", 3.9, -2.6, 1.62, 3.5, 0.06, "chrome2")
    if not dmg:
        S.bb("dipole", 3.4, 4.4, -2.65, -2.55, 3.2, 3.26, "chrome", 0.0, nocast=True)
        S.bb("dipole2", 3.5, 4.3, -2.65, -2.55, 2.8, 2.86, "chrome", 0.0, nocast=True)
    for k, (x, y) in enumerate(((-3.9, -3.4), (-3.1, -3.4), (-3.5, -3.4))):
        z0 = 0.12 if k < 2 else 0.8
        S.bb(f"crate{k}", x - 0.35, x + 0.35, y - 0.35, y + 0.35, z0, z0 + 0.68, "navy" if k != 1 else "orange2", 0.05)
    for k in range(3):
        S.cyl(f"drum{k}", 0.3, 0.8, (-2.2 + k * 0.62, -4.1, 0.52), "orange2" if k != 1 else "white2", verts=12)


def g_extras(S, dmg, i):
    cx, cy = -4.2, -1.6
    for ax in (-0.25, 0.25):
        for ay in (-0.25, 0.25):
            K.post(S, "tw", cx + ax, cy + ay, 0.12, 3.6, 0.09, "chrome2")
    for z in (1.0, 2.0, 3.0):
        S.bb("brace", cx - 0.25, cx + 0.25, cy - 0.27, cy - 0.23, z, z + 0.06, "chrome2", 0.0, nocast=True)
    top = 3.6
    if dmg:
        K.beam(S, "jib", (cx, cy, top), (cx + 1.5, cy + 0.5, top - 1.3), 0.18, "copper")
    else:
        K.beam(S, "jib", (cx, cy, top), (cx + 2.8, cy, top + 0.3), 0.18, "copper")
        K.beam(S, "cw", (cx, cy, top), (cx - 0.6, cy, top - 0.1), 0.34, "navy")
        sw = 0.18 * math.sin(i * math.pi / 2)
        K.beam(S, "cable", (cx + 2.6, cy, top + 0.28), (cx + 2.6 + sw, cy, 2.0), 0.04, "dark", nocast=True)
        S.bb("hook", cx + 2.5 + sw, cx + 2.7 + sw, cy - 0.1, cy + 0.1, 1.8, 2.0, "orange", 0.0)
    K.flag(S, "flag", 0.9, -4.1, 4.6 if not dmg else 3.4, i, dmg=dmg)


GROUPS = [g_ground, g_hull, g_super, g_annex, g_extras]


def site(S):
    K.stakes_site(S, 4.4, 4.2)


def dmg_extra(S, i):
    S.bb("scorch1", 2.6, 3.8, 0.5, 0.52, 0.9, 1.4, "char", 0.0, nocast=True)
    S.bb("scorch2", -2.5, -1.2, -0.4, 0.2, 0.12, 0.14, "char", 0.0, nocast=True)


def build(S, state, i, n):
    K.stage_driver(S, state, i, n, GROUPS, site, dmg_extra)
