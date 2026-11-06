"""Expedition ship parts for package art-exp-c (command ship, destroyer, cruiser, carrier, missile sub, minelayer).  Imports exp_common.
Ships point along +Y (bow), waterline at z = 0; every function takes a Scaled ctx `S` (nominal model: ship length 8 units).
Design 07 sec. 8: 1960s ocean-liner superstructures with gun turrets, white upper works, navy hull, red or charcoal boot-top,
white hull numbers, team colour on the funnel band.  Original design."""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import exp_common as E

PALETTE = E.PALETTE
MATERIALS = dict(E.MATERIALS)
MATERIALS["foam"] = ("#F4FAF7", dict(rough=0.9))
MATERIALS["deck"] = ("#27355A", dict(rough=0.6))
MATERIALS["fdeck"] = ("#30343A", dict(rough=0.8))


def side_digits(S, tag, text, y, z, x, px=0.06, mat="white"):
    """Hull number on both hull sides (reads bow-ward on the starboard side, +X; mirrored on the port side)."""
    w = 4 * px
    n = len(text)
    for s in (1, -1):
        y0 = y - s * (n * w - px) / 2
        for ci, ch in enumerate(text):
            g = E.FONT[ch]
            for r in range(5):
                for c in range(3):
                    if g[r * 3 + c] == "1":
                        yy = y0 + s * (ci * w + c * px)
                        S.box(f"sd{tag}{s}{ci}{r}{c}", (0.014, px, px), (s * x, yy, z + (2 - r) * px), mat, 0.0)


def hull(S, L, B, H, boot="orange2", band=0.14, bowfrac=0.3, num=None, numy=0.2, stripe=True):
    """Hull from stern to bow: a boot-top band at the waterline, navy topsides with a white sheer line, a grey deck, bow wave and wake."""
    bl = bowfrac * L
    yb = L / 2 - bl
    ym = (yb - L / 2) / 2 + 0.0                       # centre of the straight part (relative placement below)
    ln = yb + L / 2
    cy = (yb - L / 2) / 2
    S.box("boot", (B + 0.02, ln, band), (0, cy, band / 2), boot, 0.02)
    S.sphere("bootbow", 1.0, (0, yb, band / 2), boot, (B / 2 + 0.01, bl, band / 2))
    S.box("topsides", (B + 0.06, ln, H - band), (0, cy, band + (H - band) / 2), "navy", 0.07)
    S.sphere("topbow", 1.0, (0, yb, band + (H - band) / 2), "navy", (B / 2 + 0.03, bl, (H - band) / 2))
    S.box("deckslab", (B - 0.12, ln, 0.04), (0, cy, H + 0.005), "deck", 0.01)
    S.sphere("deckbow", 1.0, (0, yb, H + 0.005), "deck", (B / 2 - 0.06, bl - 0.03, 0.025))
    if stripe:
        for s in (-1, 1):
            S.box(f"sheer{s}", (0.03, ln, 0.045), (s * (B / 2 + 0.05), cy, H - 0.05), "white", 0.0)
            S.box(f"rail{s}", (0.025, ln - 0.1, 0.07), (s * (B / 2 - 0.1), cy, H + 0.06), "white2", 0.0)
    if num:
        side_digits(S, "h", num, L * 0.5 - bl * 0.9 - 0.2 + numy, band + 0.05, B / 2 + 0.07)
    # bow wave and wake (flat foam, no shadow)
    tip = L / 2 + 0.03
    S.sphere("bowfoam", 1.0, (0, tip + 0.05, 0.03), "foam", (0.34, 0.26, 0.03), nocast=True)
    ph = math.radians(26)
    ln_w = 1.6
    for s in (-1, 1):
        cx = tip - 0.1
        S.box(f"bowarm{s}", (0.09, ln_w, 0.035), (s * (math.sin(ph) * ln_w / 2 + 0.06), cx - math.cos(ph) * ln_w / 2, 0.025), "foam", 0.0,
              rot=(0, 0, s * math.degrees(ph)), nocast=True)
        S.box(f"sidefoam{s}", (0.07, ln * 0.7, 0.03), (s * (B / 2 + 0.03), cy + 0.5, 0.02), "foam", 0.0, nocast=True)
    for k, (dx, w) in enumerate(((0.0, 0.5), (0.45, 0.3), (-0.45, 0.3))):
        S.sphere(f"wake{k}", 1.0, (dx, -L / 2 - 0.2 - 0.25 * k, 0.02), "foam", (w * (1 if k == 0 else 0.7), 0.25, 0.025), nocast=True)


def block(S, tag, x, y, z, w, ln, h, mat="white", bevel=0.07, windows=True, win_mat="glass"):
    """Superstructure block on the deck (z = base): rounded white box with a dark glass window band on the front and both sides."""
    S.box(f"{tag}", (w, ln, h), (x, y, z + h / 2), mat, bevel)
    if windows:
        S.box(f"{tag}wf", (w * 0.84, 0.03, h * 0.28), (x, y + ln / 2 + 0.005, z + h * 0.58), win_mat, 0.0)
        for s in (-1, 1):
            S.box(f"{tag}ws{s}", (0.03, ln * 0.8, h * 0.28), (x + s * (w / 2 + 0.005), y, z + h * 0.58), win_mat, 0.0)


def funnel(S, tag, x, y, z, w=0.6, ln=0.9, h=0.8, band=0.2, cap="dark", body="white", rake=0.0):
    """Oval liner funnel: body, team-colour band (house), dark cap."""
    S.box(f"fn{tag}", (w, ln, h), (x, y, z + h / 2), body, min(w, ln) * 0.4, rot=(rake, 0, 0))
    S.box(f"fnband{tag}", (w + 0.03, ln + 0.03, band), (x, y, z + h * 0.62), "house", min(w, ln) * 0.4, rot=(rake, 0, 0))
    S.box(f"fncap{tag}", (w * 0.9, ln * 0.9, 0.07), (x, y, z + h + 0.01), cap, min(w, ln) * 0.35, rot=(rake, 0, 0))


def mast(S, tag, x, y, z, h=1.1, radar=True):
    S.cyl(f"mast{tag}", 0.025, h, (x, y, z + h / 2), "chrome2", "Z", 6)
    if radar:
        S.box(f"radar{tag}", (0.55, 0.05, 0.12), (x, y, z + h), "chrome", 0.01)
        S.sphere(f"radarknob{tag}", 0.05, (x, y, z + h + 0.1), "orange")


def dome(S, tag, x, y, z, r=0.28):
    S.sphere(f"dome{tag}", r, (x, y, z + r * 0.4), "white", (1, 1, 0.9))


def fixed_turret(S, tag, x, y, z, barrels=2, yaw=0, r=0.36, gl=1.1):
    """Decorative (non-rotating) gun turret on the hull; yaw 0 = forward, 180 = aft."""
    c = 1 if yaw == 0 else -1
    S.cyl(f"fth{tag}", r + 0.04, 0.05, (x, y, z + 0.025), "chrome2", "Z", 16)
    S.box(f"ftb{tag}", (r * 1.7, r * 1.9, 0.26), (x, y, z + 0.17), "navy", 0.1)
    S.box(f"ftt{tag}", (r * 1.35, r * 1.2, 0.03), (x, y - c * 0.02, z + 0.31), "white", 0.0)
    for i in range(barrels):
        bx = x + (i - (barrels - 1) / 2) * 0.17
        S.cyl(f"ftg{tag}{i}", 0.04, gl, (bx, y + c * (r + gl / 2 - 0.15), z + 0.2), "chrome", "Y", 8)


def turret_parts(S, px, py, pz, barrels=2, r=0.4, gl=1.3, house_flash=True):
    """Rotating gun turret (turret layer): chrome ring, navy shield with white roof, chrome barrels with copper collars, team flash."""
    S.cyl("tring", r + 0.05, 0.08, (px, py, pz + 0.04), "chrome", "Z", 20)
    S.box("tbody", (r * 1.8, r * 2.0, 0.3), (px, py - 0.02, pz + 0.23), "navy", 0.11)
    S.box("tfront", (r * 1.6, r * 0.9, 0.26), (px, py + r * 0.8, pz + 0.22), "navy", 0.09, rot=(-26, 0, 0))
    S.box("troof", (r * 1.3, r * 1.2, 0.03), (px, py - 0.12, pz + 0.39), "white", 0.01)
    if house_flash:
        for s in (-1, 1):
            S.box(f"tflash{s}", (0.04, r * 1.3, 0.1), (px + s * r * 0.9, py - 0.02, pz + 0.25), "house", 0.0)
    for i in range(barrels):
        bx = px + (i - (barrels - 1) / 2) * 0.2
        S.cyl(f"gun{i}", 0.045, gl, (bx, py + r + gl / 2 - 0.1, pz + 0.24), "chrome", "Y", 10)
        S.cyl(f"gcol{i}", 0.065, 0.1, (bx, py + r + 0.18, pz + 0.24), "copper", "Y", 10)
        S.cyl(f"gmouth{i}", 0.035, 0.03, (bx, py + r + gl - 0.1, pz + 0.24), "dark", "Y", 8)


def mount_for(S0, K, px, py, pz_deck, lip=0.1, r=0.45, rs=0.6):
    """Turret mount in the body layer; returns PIVOT (scaled coordinates)."""
    piv = (px * K, py * K, (pz_deck + lip) * K)
    S0.mount(piv, deck_z=pz_deck * K, r_lip=r * K, r_well=0.0, r_shadow=rs * K, soft_r=r * K * 0.95, lip="chrome2", well="dark")
    return piv
