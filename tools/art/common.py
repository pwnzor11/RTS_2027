"""Shared helpers for the art pilot: palette, house ramp, raw-layer loading, sheet packing."""
import os
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PILOT = os.path.join(ROOT, "art", "pilot")
WORK = os.path.join(PILOT, "work")


def hx(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


# 8-shade magenta house ramp, #400040 -> #FF80FF
HOUSE = [(round(64 + (255 - 64) * i / 7), round(128 * i / 7), round(64 + (255 - 64) * i / 7)) for i in range(8)]

# 24 faction colours; with the 8 house shades this is the fixed 32-colour palette
OUTLINE = hx("#0D0F0B")
PAL = [OUTLINE] + [hx(c) for c in (
    "#1E2419 #2C3326 #3B4533 #4B5742 #5C6A51 #6F7E62 #859577 #9DAC8D "      # grey-green ramp
    "#24272A #3A3F44 #555B61 #737A80 #979EA3 "                              # steel
    "#3D2214 #5E3420 #85502F #B07445 "                                      # rust
    "#C9CFC0 #E8EAE0 #F8F8F0 "                                              # highlights / stencil white
    "#C9A640 #FFE79A #8A8478").split()]
assert len(PAL) == 24
PALETTE32 = PAL + HOUSE

# Directorate palette (M3-ART-PILOT): 24 faction colours + the same 8 house shades. The Authority palette has no red and
# no bone tones, so the Directorate selects this one by setting PALETTE = C.PAL_DIR in its module.  Authority output is
# untouched (frame_b defaults to PAL).  Index 0 outline and index 1 (shadow colour) keep their roles.
PAL_DIR = [OUTLINE] + [hx(c) for c in (
    "#1F1B19 #2E2926 "                              # warm ash darks (index 1 = shadow)
    "#44403B #5C5750 #766F66 #928A7E #ADA597 "      # brutalist concrete, warm greys
    "#C9BFA6 #E3DAC2 #F6F0DE "                      # bone / stencil white
    "#3E1412 #63201A #8E2E22 #B8402F #D9664C "      # oxide red ramp (fixed body red, not house colour)
    "#3A3F44 #555B61 "                              # dark steel (no light steel: it pulled warm greys cold)
    "#5E3420 "                                      # rust
    "#4A3829 #6B523B #8F7150 #B39470 "              # dusty umber / ash-brown ramp (conscript coat)
    "#C9A640").split()]                             # brass / lamp
assert len(PAL_DIR) == 24
PALETTE32_DIR = PAL_DIR + HOUSE

# house remap targets: name -> 8-shade ramp (dark -> light)


def make_ramp(dark, mid, light):
    out = []
    for i in range(8):
        t = i / 7
        if t < 0.5:
            a, b, u = dark, mid, t / 0.5
        else:
            a, b, u = mid, light, (t - 0.5) / 0.5
        out.append(tuple(round(a[k] + (b[k] - a[k]) * u) for k in range(3)))
    return out


CELL_SIZES = (48, 64)
TAG = os.environ.get("ART_TAG", "")  # "_v2" selects the v2 raw renders
SS = 4  # supersample factor


def canvas(cell):
    return int(cell * 1.5)


def raw_dir(cell):
    return os.path.join(WORK, f"raw_{canvas(cell) * SS}{TAG}")


def house_index(vals, lo, hi):
    return np.clip(np.rint((vals - lo) / max(hi - lo, 1e-6) * 7), 0, 7).astype(int)


def calibration():
    """Luminance range of the magenta house paint across all 4x frames (cached)."""
    p = os.path.join(WORK, f"house_cal{TAG}.txt")
    if os.path.exists(p):
        lo, hi = open(p).read().split()
        return float(lo), float(hi)
    d = raw_dir(64)
    vals = []
    for f in range(16):
        c = np.asarray(Image.open(os.path.join(d, f"col_{f:02d}.png")).convert("RGBA"), dtype=float)
        m = np.asarray(Image.open(os.path.join(d, f"mask_{f:02d}.png")).convert("RGBA"), dtype=float)
        sel = (m[..., 0] > 127) & (c[..., 3] > 250)
        vals.append((c[..., 0][sel] + c[..., 2][sel]) / 2)
    v = np.concatenate(vals)
    lo, hi = np.percentile(v, 2), np.percentile(v, 98)
    open(p, "w").write(f"{lo} {hi}")
    return lo, hi


def load_layers(cell, f):
    """4x layers for frame f: rgb float, alpha 0..1, house bool (alpha-covered), house_idx int, shadow 0..1."""
    d = raw_dir(cell)
    c = np.asarray(Image.open(os.path.join(d, f"col_{f:02d}.png")).convert("RGBA"), dtype=float)
    m = np.asarray(Image.open(os.path.join(d, f"mask_{f:02d}.png")).convert("RGBA"), dtype=float)
    s = np.asarray(Image.open(os.path.join(d, f"shadow_{f:02d}.png")).convert("RGBA"), dtype=float)
    a = c[..., 3] / 255
    house = (m[..., 0] > 127) & (a > 0.5)
    lo, hi = calibration()
    idx = house_index((c[..., 0] + c[..., 2]) / 2, lo, hi)
    lum = s[..., :3].mean(axis=2) / 255
    lit = np.percentile(lum, 99.5)
    sh = np.clip(1 - lum / max(lit, 1e-6), 0, 1)
    sh = np.asarray(Image.fromarray((sh * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(SS * 0.8)), dtype=float) / 255
    sh = np.clip((sh - 0.05) / 0.8, 0, 1)
    return dict(rgb=c[..., :3], a=a, house=house, idx=idx, shadow=sh)


def block_mean(x, k):
    h, w = x.shape[:2]
    return x.reshape(h // k, k, w // k, k, *x.shape[2:]).mean(axis=(1, 3))


def downscale_layers(L, canvas_px):
    """Area-downscale 4x layers to canvas_px. Returns (base_rgb, alpha, house_frac, house_idx, shadow)."""
    k = L["a"].shape[0] // canvas_px
    a, hs = L["a"], L["house"]
    w_base = a * (~hs)
    w_house = a * hs
    A = block_mean(a, k)
    Wb = block_mean(w_base, k)
    Wh = block_mean(w_house, k)
    base = block_mean(L["rgb"] * w_base[..., None], k) / np.maximum(Wb[..., None], 1e-6)
    hidx = block_mean(L["idx"] * w_house, k) / np.maximum(Wh, 1e-6)
    hfrac = Wh / np.maximum(A, 1e-6)
    sh = block_mean(L["shadow"], k)
    return base, A, hfrac, np.clip(np.rint(hidx), 0, 7).astype(int), sh


def pack_sheet(frames, cols, cw, ch, bg=(0, 0, 0, 0)):
    rows = (len(frames) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cw, rows * ch), bg)
    for i, fr in enumerate(frames):
        sheet.paste(fr, ((i % cols) * cw, (i // cols) * ch))
    return sheet


def split_sheet(img, cols, rows):
    cw, ch = img.width // cols, img.height // rows
    return [img.crop(((i % cols) * cw, (i // cols) * ch, (i % cols + 1) * cw, (i // cols + 1) * ch)) for i in range(cols * rows)]


def anchor_px(cell=64, elev_deg=40.0, cell_world=3.4, target=(0.0, 0.5, 0.7)):
    """Pixel of the tank's ground origin (cell centre) within the frame, from the camera set-up in build_tank.py."""
    import math
    el = math.radians(elev_deg)
    rel = (-target[0], -target[1], -target[2])
    up = rel[1] * math.sin(el) + rel[2] * math.cos(el)
    cv = canvas(cell)
    return cv // 2, round(cv / 2 - up / (cell_world * 1.5) * cv)
