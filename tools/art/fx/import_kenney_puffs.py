"""Builds data/sprites/fx/puffs_dust.png and puffs_smoke.png (4 frames of 32 px, grey, hard alpha) from Kenney's CC0 "Smoke particles" (S15).

Usage:  python3 tools/art/fx/import_kenney_puffs.py [--pack DIR]   # default: $RTS_KENNEY_SMOKE or /tmp/claude-0/s15-packs/kenney_smoke-particles/PNG
dust  = 'White puff' shapes (vehicle dust, tinted by the biome's dust colour at draw time); smoke = 'Black smoke' shapes (damage smoke, tinted dark).
Same recipe as the procedural PuffArt: a blob that grows over four frames (diameters 14, 20, 26, 30), lit from the upper left, three grey shades
(255/228/198, the texture's own light/dark picks the shade), the last two frames Bayer-thinned (25 % / 50 % holes). Alpha is 0 or 255: the sources' soft
fringes are cut at 50 % coverage, which is what keeps them in the hard-pixel language. The colour tint multiplies the grey, as before.
"""
import argparse, glob, os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "data", "sprites", "fx"))
SIZES = (14, 20, 26, 30)
SHADES = (255, 228, 198)


def frame(path, d, f):
    im = Image.open(path).convert("RGBA")
    pm = np.asarray(im.convert("RGBa").resize((d, d), Image.BOX)).astype(float) / 255.0
    a = pm[..., 3]
    lum = np.where(a > 1e-4, pm[..., :3].sum(axis=2) / 3.0 / np.maximum(a, 1e-4), 0.0)
    solid = a >= 0.5
    if solid.any():
        lo, hi = np.percentile(lum[solid], 8), np.percentile(lum[solid], 92)
        lum = np.clip((lum - lo) / max(hi - lo, 1e-3), 0, 1)
    out = np.zeros((32, 32, 4), dtype=np.uint8)
    o = (32 - d) // 2
    for y in range(d):
        for x in range(d):
            if not solid[y, x]: continue
            if f == 2 and x % 2 == 0 and y % 2 == 0: continue
            if f == 3 and (x + y) % 2 == 0: continue
            light = -0.6 * (x / d - 0.5) * 2 - 0.6 * (y / d - 0.5) * 2          # -1.2 .. 1.2, lit from the upper left
            v = 0.55 * lum[y, x] + 0.45 * (0.5 + 0.5 * np.clip(light, -1, 1))
            g = SHADES[0] if v > 0.62 else SHADES[1] if v > 0.38 else SHADES[2]
            out[o + y, o + x] = (g, g, g, 255)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pack", default=os.environ.get("RTS_KENNEY_SMOKE", "/tmp/claude-0/s15-packs/kenney_smoke-particles/PNG"))
    ns = ap.parse_args()
    for name, folder, stem, picks in (("puffs_dust", "White puff", "whitePuff", (3, 8, 5, 12)), ("puffs_smoke", "Black smoke", "blackSmoke", (1, 7, 9, 3))):
        sheet = np.zeros((32, 128, 4), dtype=np.uint8)
        for f in range(4):
            p = os.path.join(ns.pack, folder, f"{stem}{picks[f]:02d}.png")
            sheet[:, f * 32:(f + 1) * 32] = frame(p, SIZES[f], f)
        Image.fromarray(sheet, "RGBA").save(os.path.join(OUT, name + ".png"))
        print("wrote", name)


if __name__ == "__main__":
    main()
