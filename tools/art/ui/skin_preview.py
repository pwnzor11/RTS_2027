# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Compose a mock sidebar, panel, buttons and card from the skin PNGs using their 9-slice margins (Pillow).

  python tools/art/ui/skin_preview.py [--skin DIR] [--out PNG]

Sizes follow SidebarLayout at a 1280x720 window, UI scale 1 (sidebar 256 wide; credits 24, rows 20, minimap square).
"""
import argparse
import os
import sys

from PIL import Image, ImageDraw, ImageFont

try:
    import tomllib
except ImportError:  # pragma: no cover
    import tomli as tomllib

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))


def nine(img, size, sl):
    """9-slice `img` to `size` (w, h); `sl` = (l, t, r, b) margins kept unscaled."""
    w, h = size
    l, t, r, b = sl
    sw, sh = img.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    xs_src = [0, l, sw - r, sw]
    ys_src = [0, t, sh - b, sh]
    xs_dst = [0, l, w - r, w]
    ys_dst = [0, t, h - b, h]
    for j in range(3):
        for i in range(3):
            sbox = (xs_src[i], ys_src[j], xs_src[i + 1], ys_src[j + 1])
            dw, dh = xs_dst[i + 1] - xs_dst[i], ys_dst[j + 1] - ys_dst[j]
            if dw <= 0 or dh <= 0 or sbox[2] <= sbox[0] or sbox[3] <= sbox[1]:
                continue
            piece = img.crop(sbox)
            if piece.size != (dw, dh):
                piece = piece.resize((dw, dh), Image.NEAREST)
            out.paste(piece, (xs_dst[i], ys_dst[j]))
    return out


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--skin", default=os.path.join(ROOT, "data", "ui", "skins", "authority"))
    ap.add_argument("--out", default=os.path.join(ROOT, "scratch", "art_s24", "skin_preview.png"))
    ap.add_argument("--zoom", type=int, default=2)
    a = ap.parse_args(argv)
    with open(os.path.join(a.skin, "skin.toml"), "rb") as f:
        cfg = tomllib.load(f)
    imgs = {k: Image.open(os.path.join(a.skin, v["file"])).convert("RGBA") for k, v in cfg.items() if isinstance(v, dict)}

    def piece(key, size):
        sl = cfg[key].get("slice")
        return imgs[key].resize(size, Image.NEAREST) if sl is None else nine(imgs[key], size, tuple(sl))

    W, H, SB = 1280, 720, 256
    canvas = Image.new("RGBA", (W, H), (46, 40, 34, 255))  # stand-in ash terrain
    d = ImageDraw.Draw(canvas)
    font = ImageFont.load_default()
    tc, td, ac = cfg["text_color"], cfg["text_dim"], cfg["accent"]
    x0 = W - SB
    canvas.alpha_composite(piece("sidebar_back", (SB, H)), (x0, 0))
    pad, gap = 4, 3
    ix, iw = x0 + pad, SB - 2 * pad
    y = pad
    # credits text sits directly on the stencil plate baked into sidebar_back
    d.text((ix + 8, y + 7), "CREDITS  12500", fill=tc, font=font)
    y += 24 + gap
    # sell / repair buttons: normal, hover, pressed, disabled shown across the row for judging
    states = ["button", "button_hover", "button_pressed", "button_disabled"]
    bw = (iw - gap * 3) // 4
    for i, k in enumerate(states):
        canvas.alpha_composite(piece(k, (bw, 20)), (ix + i * (bw + gap), y))
        d.text((ix + i * (bw + gap) + 6, y + 5), ["Sell", "Repair", "Sell", "Repair"][i], fill=(td if k == "button_disabled" else tc), font=font)
    y += 20 + gap
    # tabs
    tw = (iw - gap * 3) // 4
    for i, nm in enumerate(["Struct", "Defence", "Infantry", "Vehicle"]):
        k = "tab_active" if i == 0 else "tab"
        canvas.alpha_composite(piece(k, (tw, 20)), (ix + i * (tw + gap), y))
        d.text((ix + i * (tw + gap) + 5, y + 5), nm, fill=(ac if i == 0 else tc), font=font)
    y += 20 + gap
    # cameo grid 3 x 3 (flat slots; cameos are sprites, not skin)
    cw = (iw - gap * 2) // 3
    for r in range(3):
        for c in range(3):
            canvas.alpha_composite(piece("button", (cw, 52)), (ix + c * (cw + gap), y + r * (52 + gap)))
            d.text((ix + c * (cw + gap) + 6, y + r * (52 + gap) + 20), f"cameo {r * 3 + c + 1}", fill=td, font=font)
    y += 3 * (52 + gap)
    # minimap at the bottom
    ms = iw
    my = H - pad - ms
    d.rectangle([ix + 4, my + 4, ix + ms - 5, my + ms - 5], fill=(30, 36, 28, 255))
    canvas.alpha_composite(piece("minimap_frame", (ms, ms)), (ix, my))
    # selection card and a loose panel in the map area
    cx, cy = 40, 470
    canvas.alpha_composite(piece("card_frame", (300, 190)), (cx, cy))
    d.text((cx + 16, cy + 14), "Heavy Tank", fill=tc, font=font)
    d.text((cx + 16, cy + 30), "HP 400 / 400    Armor: heavy", fill=td, font=font)
    d.text((cx + 16, cy + 46), "Order: Attack-move", fill=ac, font=font)
    canvas.alpha_composite(piece("panel", (260, 150)), (40, 40))
    d.text((56, 56), "Options panel (9-slice)", fill=tc, font=font)
    canvas.alpha_composite(piece("panel", (120, 70)), (340, 40))
    canvas.alpha_composite(piece("button", (120, 28)), (56, 100))
    d.text((70, 108), "Resume", fill=tc, font=font)
    canvas.alpha_composite(piece("button_hover", (120, 28)), (56, 136))
    d.text((70, 144), "Options", fill=tc, font=font)
    os.makedirs(os.path.dirname(a.out), exist_ok=True)
    canvas.convert("RGB").resize((W * a.zoom, H * a.zoom), Image.NEAREST).save(a.out)
    print("wrote", a.out)


if __name__ == "__main__":
    main(sys.argv[1:])
