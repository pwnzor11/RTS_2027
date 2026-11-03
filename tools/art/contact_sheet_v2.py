"""v2 contact sheet: B v1 vs v2 vs v2-dither at 1x and 3x, remap row, 10x6 ash patch at 64 px (true size)."""
import os
import numpy as np
from PIL import Image, ImageDraw
import common as C
from contact_sheet import strip, ash_patch, hcat, vcat, BG, FG

ARMS = (("B v1", "B_pixel_64.png"), ("B v2", "B_pixel_64_v2.png"), ("B v2 dither", "B_dither_64_v2.png"))


def frames(fn):
    return C.split_sheet(Image.open(os.path.join(C.PILOT, fn)).convert("RGBA"), 4, 4)


def patch(fn, seed=2027):
    cell = 64
    img = ash_patch(cell, seed)
    ax, ay = C.anchor_px(cell)
    fr = frames(fn)
    for (cx, cy), fi in zip([(2, 2), (5, 3), (8, 2), (3, 4), (7, 4)], [2, 6, 11, 4, 13]):
        f = fr[fi]
        img.paste(f, (int((cx + 0.5) * cell - ax), int((cy + 0.5) * cell - ay)), f)
    return img


def main():
    blocks = []
    for z, sel, label in ((1, range(16), "1x, all 16 facings (frame 0 = N, clockwise)"),):
        for name, fn in ARMS:
            blocks.append((f"{name} - 64 px cell, {label}", strip([frames(fn)[i] for i in sel], z)))
    zoom = []
    for name, fn in ARMS:
        fr = frames(fn)
        zoom.append(strip([fr[2], fr[4]], 3, 6))
    blocks.append(("3x nearest, frames 2 (45 deg) and 4 (east). Left to right: B v1 | B v2 | B v2 dither", hcat(zoom, 24)))
    zoom2 = []
    for name, fn in ARMS:
        fr = frames(fn)
        zoom2.append(strip([fr[0], fr[8]], 3, 6))
    blocks.append(("3x nearest, frames 0 (north) and 8 (south). Left to right: B v1 | B v2 | B v2 dither", hcat(zoom2, 24)))
    blocks.append(("HOUSE-COLOUR REMAP v2, frame 2 @64 px, 3x. Top row B v2, bottom row B v2 dither. Columns: original magenta, Authority green, Directorate rust-red, blue ally",
                   Image.open(os.path.join(C.PILOT, "remap_test_v2.png")).convert("RGB")))
    p = [patch(fn) for _, fn in ARMS]
    blocks.append(("IN CONTEXT, true 1x size: 10x6 cell ash patch at 64 px. Left to right: B v1 | B v2 | B v2 dither", hcat(p, 16)))
    W = max(b[1].width for b in blocks) + 20
    H = 10 + sum(b[1].height + 26 for b in blocks)
    sheet = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(sheet)
    y = 10
    for label, im in blocks:
        d.text((10, y), label, fill=FG)
        sheet.paste(im, (10, y + 16))
        y += im.height + 26
    sheet.save(os.path.join(C.PILOT, "contact_sheet_v2.png"), optimize=True)
    print("wrote contact_sheet_v2.png", sheet.size)


if __name__ == "__main__":
    main()
