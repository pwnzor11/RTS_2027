"""Expedition Grapeshot (exp_canister_skiff, D-236): light hover skiff with a turret-mounted canister gun, for tools/art/build_vehicle.py.
Contract: see auth_jeep.py.

Same fleet as the Glide Tank and Hoverlander (shared E.skirt rubber skirt with ash tidemark, navy / lagoon white / chrome / copper / signal
orange, white grotesque hull number), but smaller, lower and open: a narrow navy tub on the skirt with a lagoon-white gunwale and a deck of
white planking, two open bench seats with a chrome roll hoop behind a short windscreen, orange canister crates, and ONE big ducted stern
fan (no nacelle pair, no fins).  A raised foredeck carries the turret.
Team colour: the two long gunwale stripes (top-facing, readable on every facing), the stern duct flash and the turret band.
Turret layer (pivot on the foredeck): a low navy drum with a white cap, a side-mounted orange canister magazine drum, a chrome breech and
a STUBBY barrel ending in a wide copper-collared flared mouth (the canister gun).  Footprint about 1.5 x 2.4 units.  Original design.
Layers: body (hull + whole-vehicle shadow), turret (own sheet).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

ID = "exp_canister_skiff"
TITLE = "Expedition Grapeshot (canister skiff)"
K = 0.78          # whole-model scale: smaller than the Glide Tank (0.86) and the Hoverlander (0.84); 0.72 read too small at 1x
LIFT = 0.14       # hull floats this far above the skirt
CANVAS = 128
TARGET = (0.0, 0.0, 0.8 * K)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]
PIVOT_U = (0.0, 0.55, 1.02 + LIFT)                                  # unscaled model coordinates (turret parts are built from these)
PIVOT = tuple(v * K for v in PIVOT_U)
MOUNT = dict(deck_z=(0.97 + LIFT) * K, r_lip=0.62 * K, r_well=0.0, r_shadow=0.8 * K, soft_r=0.75 * K, lip="chrome2", well="dark")
PALETTE = E.PALETTE
MATERIALS = E.MATERIALS


def build(S0, state, k, n):
    S = E.Scaled(S0, K)
    box, cyl, sph, cone = S.box, S.cyl, S.sphere, S.cone
    S.begin("body")
    E.skirt(S, 2.1, 3.3, 0.0, 0.36, LIFT)
    S.lift(LIFT)
    # open tub: floor, navy sides, lagoon-white gunwale, sloped prow
    box("hullL", (1.8, 3.0, 0.3), (0, 0.0, 0.62), "navy2", 0.12)
    box("floor", (1.5, 1.5, 0.05), (0, -0.7, 0.8), "white2", 0.01)                       # white planking in the open cockpit
    for s in (-1, 1):
        box(f"side{s}", (0.14, 2.9, 0.4), (s * 0.82, -0.05, 0.9), "navy", 0.06)
        box(f"gun{s}", (0.2, 2.9, 0.06), (s * 0.82, -0.05, 1.12), "white", 0.02)          # gunwale cap
        box(f"topstripe{s}", (0.1, 2.5, 0.045), (s * 0.82, -0.05, 1.16), "house", 0.0)    # team colour, top-facing
        box(f"sill{s}", (0.05, 2.8, 0.04), (s * 0.92, -0.05, 0.78), "copper", 0.0)
        box(f"seat{s}", (0.5, 0.5, 0.18), (s * 0.4, -0.55, 0.92), "navy2", 0.07)
        box(f"seatback{s}", (0.5, 0.1, 0.34), (s * 0.4, -0.8, 1.06), "navy2", 0.04)
        E.lamp(S, f"L{s}", s * 0.5, 1.6, 0.72, 0.09)
    box("prow", (1.7, 0.9, 0.32), (0, 1.35, 0.92), "navy", 0.12, rot=(-22, 0, 0))
    box("prowcap", (1.5, 0.7, 0.05), (0, 1.33, 1.1), "white", 0.01, rot=(-22, 0, 0))
    box("bumper", (1.6, 0.1, 0.09), (0, 1.76, 0.62), "chrome", 0.03)
    # foredeck under the turret
    box("foredeck", (1.5, 1.3, 0.14), (0, 0.55, 0.9), "navy", 0.07)
    box("foredeckw", (1.2, 1.0, 0.03), (0, 0.55, 0.985), "white", 0.01)
    # windscreen + roll hoop
    box("screen", (1.3, 0.05, 0.3), (0, -0.2, 1.1), "glass", 0.0, rot=(-18, 0, 0))
    for s in (-1, 1):
        cyl(f"hooppost{s}", 0.035, 0.7, (s * 0.62, -1.05, 1.15), "chrome", "Z", 8)
    cyl("hoopbar", 0.035, 1.3, (0, -1.05, 1.5), "chrome", "X", 8)
    # ammo crates and hull number
    for s in (-1, 1):
        box(f"crate{s}", (0.34, 0.5, 0.22), (s * 0.5, -1.2, 0.93), "orange", 0.05)
        box(f"cratestrap{s}", (0.36, 0.06, 0.24), (s * 0.5, -1.2, 0.93), "dark", 0.0)
    E.digits(S, "hn", "06", 0, -0.2, 1.0, 0.065, "white")
    # one big ducted stern fan
    cyl("duct", 0.62, 0.34, (0, -1.72, 1.12), "chrome", "Y", 24)
    cyl("ductring", 0.66, 0.08, (0, -1.9, 1.12), "copper", "Y", 24)
    cyl("ductin", 0.5, 0.05, (0, -1.9, 1.12), "dark", "Y", 22)
    cyl("ducthub", 0.12, 0.09, (0, -1.93, 1.12), "chrome2", "Y", 10)
    for q in range(3):
        box(f"vane{q}", (0.9, 0.04, 0.1), (0, -1.9, 1.12), "chrome", 0.0, rot=(0, q * 60, 0))
    box("ductflash", (0.9, 0.12, 0.1), (0, -1.55, 1.58), "house", 0.0)
    box("ducttopband", (0.18, 0.5, 0.05), (0, -1.72, 1.45), "house", 0.0)
    box("ductpad", (1.0, 0.5, 0.1), (0, -1.5, 0.9), "navy", 0.04)
    cyl("whip", 0.02, 1.0, (0.7, -1.2, 1.5), "chrome2", "Z", 6)
    sph("whipknob", 0.04, (0.7, -1.2, 2.0), "orange")
    S.lift(0)
    S0.mount(PIVOT, **MOUNT)
    S0.begin("turret", PIVOT)
    px, py, pz = PIVOT_U
    cyl("drum", 0.52, 0.26, (px, py, pz + 0.13), "navy", "Z", 24)
    cyl("cap", 0.4, 0.04, (px, py - 0.05, pz + 0.28), "white", "Z", 20)
    box("tband", (0.12, 0.7, 0.05), (px, py - 0.05, pz + 0.31), "house", 0.0)              # team colour turret band
    for s in (-1, 1):
        box(f"tflash{s}", (0.05, 0.5, 0.12), (px + s * 0.5, py - 0.05, pz + 0.16), "house", 0.0)
    box("breech", (0.44, 0.5, 0.3), (px, py + 0.4, pz + 0.24), "chrome2", 0.07)
    cyl("barrel", 0.13, 0.55, (px, py + 0.85, pz + 0.24), "chrome", "Y", 14)               # stubby
    cyl("collar", 0.17, 0.1, (px, py + 1.08, pz + 0.24), "copper", "Y", 14)
    cone("flare", 0.15, 0.38, 0.46, (px, py + 1.3, pz + 0.24), "chrome", 18, rot=(-90, 0, 0))   # wide flared canister mouth
    cyl("mouthring", 0.4, 0.05, (px, py + 1.52, pz + 0.24), "copper", "Y", 18)
    cyl("mouth", 0.32, 0.04, (px, py + 1.54, pz + 0.24), "dark", "Y", 16)
    cyl("mag", 0.2, 0.32, (px + 0.5, py + 0.1, pz + 0.3), "orange", "Y", 14)               # side canister magazine drum
    cyl("magband", 0.215, 0.06, (px + 0.5, py + 0.1, pz + 0.3), "copper", "Y", 14)
    box("shield", (0.5, 0.05, 0.26), (px, py - 0.5, pz + 0.38), "glass", 0.0, rot=(-15, 0, 0))
