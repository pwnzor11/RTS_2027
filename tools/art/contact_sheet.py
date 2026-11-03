"""Contact sheet: all arms at 1x and 3x, remap row, and an in-context 10x6 cell ash patch per arm/cell size."""
import os
import numpy as np
from PIL import Image, ImageDraw
import common as C

BG = (30, 30, 34)
FG = (225, 225, 215)
W = 1620


def load(arm, cell):
    fn = {"A": f"A_render_{cell}.png", "B": f"B_pixel_{cell}.png", "C": "C_hand_48.png"}[arm]
    img = Image.open(os.path.join(C.PILOT, fn)).convert("RGBA")
    return C.split_sheet(img, 4, 1) if arm == "C" else C.split_sheet(img, 4, 4)


def strip(frames, z, gap=0, ground=(74, 66, 58)):
    if not frames:
        return Image.new("RGB", (1, 1))
    cw = frames[0].width * z
    out = Image.new("RGB", (len(frames) * (cw + gap), frames[0].height * z), ground)
    for i, f in enumerate(frames):
        big = f.resize((cw, frames[0].height * z), Image.NEAREST) if z > 1 else f
        out.paste(big, (i * (cw + gap), 0), big)
    return out


def ash_patch(cell, seed):
    rng = np.random.default_rng(seed)
    w, h = 10 * cell, 6 * cell
    base = np.array([52, 47, 44], dtype=float)
    n = rng.normal(0, 3.0, (h, w, 1))
    blotch = np.asarray(Image.fromarray(rng.integers(0, 255, (h // 8 + 1, w // 8 + 1), dtype=np.uint8)).resize((w, h), Image.BILINEAR), dtype=float)[..., None]
    arr = np.clip(base + n + (blotch - 128) * 0.04, 0, 255).astype(np.uint8)
    return Image.fromarray(arr, "RGB")


def patch_with_tanks(arm, cell):
    frames = load(arm, cell if arm != "C" else 48)
    img = ash_patch(cell, 2027)
    spots = [(2, 2), (5, 3), (8, 2)]
    pick = [2, 6, 11] if arm != "C" else [1, 0, 3]
    for (cx, cy), fi in zip(spots, pick):
        f = frames[fi]
        px = int((cx + 0.5) * cell - f.width / 2)
        py = int((cy + 0.5) * cell - f.height / 2)
        img.paste(f, (px, py), f)
    d = ImageDraw.Draw(img)
    for gx in range(0, img.width, cell):          # faint cell ticks at the corners only
        for gy in range(0, img.height, cell):
            d.point((gx, gy), fill=(90, 84, 78))
    return img


def main():
    blocks = []  # (label, image)
    for arm, desc in (("A", "ARM A  Blender render, smooth downscale"), ("B", "ARM B  pixel post-process, 32-colour palette, outline, hard alpha"),
                      ("C", "ARM C  hand-placed pixels, 48 px, N/E/S/W only (diagonals would be drawn by hand later)")):
        if arm == "C":
            f48 = load("C", 48)
            blocks.append((f"{desc} - 1x", strip(f48, 1)))
            blocks.append(("ARM C 3x nearest (N, E, S, W)", strip(f48, 3, 6)))
            continue
        f48, f64 = load(arm, 48), load(arm, 64)
        blocks.append((f"{desc} - 48 px cell, 1x, all 16 facings (frame 0 = N, clockwise)", strip(f48, 1)))
        blocks.append((f"ARM {arm} - 64 px cell, 1x, all 16 facings", strip(f64, 1)))
        sel = [0, 2, 4, 6]
        blocks.append((f"ARM {arm} - 3x nearest: 48 px cell frames 0,2,4,6 (left) and 64 px cell frames 0,2,4 (right)",
                       hcat([strip([f48[i] for i in sel], 3, 4), strip([f64[i] for i in (0, 2, 4)], 3, 4)], 16)))
    rm = Image.open(os.path.join(C.PILOT, "remap_test.png")).convert("RGB")
    blocks.append(("HOUSE-COLOUR REMAP, frame 2 @64 px, 3x. Top row ARM A, bottom row ARM B. Columns: original magenta, Authority green, Directorate rust-red, blue ally", rm))
    ctx = []
    for arm in "AB":
        ctx += [patch_with_tanks(arm, 48), patch_with_tanks(arm, 64)]
    ctx.append(patch_with_tanks("C", 48))
    blocks.append(("IN CONTEXT: 10x6 cell ash patch, 3 tanks each. Row 1: A@48, A@64. Row 2: B@48, B@64. Row 3: C@48 (1x, true size)",
                   vcat([hcat(ctx[0:2], 20), hcat(ctx[2:4], 20), ctx[4]], 14)))
    total_h = 10 + sum(b[1].height + 26 for b in blocks)
    sheet = Image.new("RGB", (max(W, max(b[1].width for b in blocks) + 20), total_h), BG)
    d = ImageDraw.Draw(sheet)
    y = 10
    for label, im in blocks:
        d.text((10, y), label, fill=FG)
        sheet.paste(im, (10, y + 16))
        y += im.height + 26
    sheet.save(os.path.join(C.PILOT, "contact_sheet.png"), optimize=True)
    print("wrote contact_sheet.png", sheet.size)


def hcat(ims, gap):
    out = Image.new("RGB", (sum(i.width for i in ims) + gap * (len(ims) - 1), max(i.height for i in ims)), BG)
    x = 0
    for i in ims:
        out.paste(i, (x, 0))
        x += i.width + gap
    return out


def vcat(ims, gap):
    out = Image.new("RGB", (max(i.width for i in ims), sum(i.height for i in ims) + gap * (len(ims) - 1)), BG)
    y = 0
    for i in ims:
        out.paste(i, (0, y))
        y += i.height + gap
    return out


if __name__ == "__main__":
    main()
