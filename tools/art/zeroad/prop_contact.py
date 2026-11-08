"""Contact sheets: procedural placeholder props vs 0 A.D. renders per biome, true scale (zoom 1) beside a tank and a rifleman.
Usage: python3 -I tools/art/zeroad/prop_contact.py <proc_dir with proc_<theater>.rgba> <out_dir>
(the .rgba dumps come from a throwaway test that writes ProceduralPropAtlas.Rgba)."""
import os, sys
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
KINDS = ["Boulder", "Rubble", "GlassCrest", "IceSlab", "DeadTree", "WreckHulk", "ConcreteBlock", "BrokenPylon", "Snag"]
ZK = {"Boulder": 0, "Rubble": 1, "ConcreteBlock": 2, "Snag": 3}
BIOMES = [("rustbelt", "rustbelt", "#8E8371"), ("glass_coast", "glasscoast", "#E4DABC"), ("mountains", "mountains", "#918572"), ("ash_plains", "ashplains", "#827D52")]
SCALE = 2


def hx(h):
    h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def frame(img, x, y, w, h):
    return img.crop((x, y, x + w, y + h))


def main(procdir, out):
    os.makedirs(out, exist_ok=True)
    tank = Image.open(os.path.join(ROOT, "data/sprites/vehicles/auth_tank.png")).convert("RGBA")
    inf = Image.open(os.path.join(ROOT, "data/sprites/infantry/auth_rifleman.png")).convert("RGBA")
    tk = frame(tank, 2 * 96, 1 * 96, 96, 96)      # facing 6 of 16 (south-east)
    ik = frame(inf, 3 * 48, 0, 48, 48)             # idle facing 3 (south-east)
    sheets = []
    for theater, zid, ground in BIOMES:
        proc = Image.frombytes("RGBA", (192, 576), open(os.path.join(procdir, f"proc_{theater}.rgba"), "rb").read())
        zero = Image.open(os.path.join(ROOT, "data/sprites/props/zeroad", f"{zid}.png")).convert("RGBA")
        RH, OY = 110, 78
        cols = [("tank", 10), ("rifleman", 110), ("placeholder v0 v1 v2", 170), ("0 A.D. v0 v1 v2", 400)]
        W = 640
        im = Image.new("RGBA", (W, 24 + RH * len(KINDS)), hx(ground) + (255,))
        d = ImageDraw.Draw(im)
        for name, x in cols:
            d.text((x, 6), name, fill=(20, 20, 20, 255))
        for r, kind in enumerate(KINDS):
            y0 = 24 + r * RH
            d.text((4, y0 + 2), kind, fill=(20, 20, 20, 255))
            oy = y0 + OY
            im.alpha_composite(tk, (10 + 40 - 48, oy - 64)) if False else None
            im.alpha_composite(tk, (60 - 48, oy - 64))
            im.alpha_composite(ik, (134 - 24, oy - 42))
            for v in range(3):
                f = frame(proc, v * 64, r * 64, 64, 64)
                im.alpha_composite(f, (170 + v * 70, oy - 32))
                if kind in ZK:
                    z = frame(zero, v * 64, ZK[kind] * 64, 64, 64)
                    im.alpha_composite(z, (400 + v * 70, oy - 40))
                else:
                    d.text((400 + v * 70, oy - 8), "(placeholder)", fill=(60, 60, 60, 255))
        im = im.resize((im.width * SCALE, im.height * SCALE), Image.NEAREST)
        p = os.path.join(out, f"props_{theater}.png"); im.convert("RGB").save(p); sheets.append(im)
        print(p)


main(sys.argv[1], sys.argv[2])
