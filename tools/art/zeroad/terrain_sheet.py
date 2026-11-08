"""Comparison sheet of 0 A.D. terrain textures (CC-BY-SA 3.0) downsampled to our texel scale (16 texels per cell, 4 px per texel at zoom 1).
No game data is produced: this only makes a review sheet. Usage: python3 -I tools/art/zeroad/terrain_sheet.py <out.png>"""
import sys
import numpy as np
from PIL import Image, ImageDraw

T = "/home/claude/ref/zeroad/binaries/data/mods/public/art/textures/terrain/types/"
CANDS = [("dirt_cracked_b.dds", "cracked dirt"), ("desert_dirt_cracks.dds", "cracked earth"), ("dirt_hard.dds", "hard dirt"),
         ("temp_dirt_gravel_b.png", "gravel"), ("dirt_burned.dds", "burned dirt"), ("desert_lakebed_dry.dds", "dry lakebed"),
         ("steppe_dirt_rocks_a.dds", "dirt + rocks"), ("dirt_rugged.dds", "rugged dirt")]
GROUND = {"rust belt clear": "#8E8371", "rust belt rough": "#9E684C", "ash plains clear": "#827D52", "mountains clear": "#918572"}
TEXELS_PER_TILE = 32      # a tile covers 2 cells
SHOW = 4                  # px per texel at zoom 1


def hx(h):
    h = h.lstrip("#"); return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=float)


def grade(img, base, amp=0.55):
    """Recolour toward our ground: keep the texture's luminance variation (scaled), centre it on the biome base colour."""
    a = np.asarray(img.convert("RGB"), dtype=float)
    lum = a @ [0.3, 0.55, 0.15]
    d = (lum - lum.mean()) / max(lum.std(), 1e-6) * 14 * amp
    out = base[None, None, :] * (1 + d[..., None] / 140) + (a - a.mean((0, 1))) * 0.18
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


def main(out):
    W = 4 * 2 * TEXELS_PER_TILE * SHOW // 2
    tile = TEXELS_PER_TILE * SHOW          # 128
    cols = len(CANDS)
    sheet = Image.new("RGB", (cols * (tile * 2 + 8), 3 * (tile * 2 + 24) + 20), (30, 30, 30))
    d = ImageDraw.Draw(sheet)
    for i, (f, label) in enumerate(CANDS):
        im = Image.open(T + f).convert("RGB")
        small = im.resize((TEXELS_PER_TILE, TEXELS_PER_TILE), Image.BOX)
        rows = [("raw", small), ("rust belt clear", grade(small, hx(GROUND["rust belt clear"]))), ("ash plains clear", grade(small, hx(GROUND["ash plains clear"])))]
        for r, (name, s) in enumerate(rows):
            big = s.resize((tile, tile), Image.NEAREST)
            x0, y0 = i * (tile * 2 + 8), 20 + r * (tile * 2 + 24)
            for ty in range(2):
                for tx in range(2):
                    sheet.paste(big, (x0 + tx * tile, y0 + ty * tile))
            d.text((x0 + 2, y0 + tile * 2 + 2), f"{f.split('.')[0]} ({label}) {name}", fill=(230, 230, 230))
    d.text((4, 4), "0 A.D. terrain candidates at game texel scale: 32 texels per tile, tile = 2 cells, 4 px per texel (zoom 1), tiled 2x2. CC-BY-SA 3.0", fill=(255, 255, 255))
    sheet.save(out)


main(sys.argv[1])
