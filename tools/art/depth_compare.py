"""Side-by-side contact sheet for the depth pass (D-243, M10-08).
Usage: python3 tools/art/depth_compare.py [ids ...] [--frames 0 ...] [--depth-dir scratch/art_s24/depth] [--out scratch/art_s24/depth_compare.png]
For each id: today's committed frame (data/sprites/buildings/<id>.png) on the left, the --depth frame on the right, same frame number
(= same state and animation step), both at 2x on the flat ground colour.  A partial depth sheet (a smoke run with --frames) holds only the
rendered frames, in order; a full sheet is indexed by frame number."""
import argparse, os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import common as C
import build_structure as BS

GROUND = (74, 66, 58)
Z = 2


def frame_of(sheet, mod, idx):
    fw, fh = mod.CANVAS
    cols = sheet.width // fw
    return sheet.crop(((idx % cols) * fw, (idx // cols) * fh, (idx % cols + 1) * fw, (idx // cols + 1) * fh))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("ids", nargs="*", default=["auth_conyard", "dir_shaft"])
    ap.add_argument("--frames", type=int, nargs="*", default=[0])
    ap.add_argument("--depth-dir", default=os.path.join(C.ROOT, "scratch", "art_s24", "depth"))
    ap.add_argument("--out", default=os.path.join(C.ROOT, "scratch", "art_s24", "depth_compare.png"))
    a = ap.parse_args()
    tiles = []   # one row per (id, frame): [committed, depth]
    for sid in a.ids:
        mod = BS.load_module(sid)
        fw, fh = mod.CANVAS
        old = Image.open(os.path.join(C.ROOT, "data", "sprites", "buildings", f"{sid}.png")).convert("RGBA")
        new = Image.open(os.path.join(a.depth_dir, f"{sid}.png")).convert("RGBA")
        full = new.height >= old.height
        for pos, fi in enumerate(a.frames):
            tiles.append((sid, fi, frame_of(old, mod, fi), frame_of(new, mod, fi if full else pos)))
    pad, lab = 6, 14
    cw = max(t[2].width for t in tiles) * Z
    rows = [max(t[2].height, t[3].height) * Z + lab for t in tiles]
    W = 2 * cw + 3 * pad
    H = sum(rows) + pad * (len(rows) + 1)
    out = Image.new("RGB", (W, H), GROUND)
    d = ImageDraw.Draw(out)
    y = pad
    for (sid, fi, o, n), rh in zip(tiles, rows):
        for k, (im, name) in enumerate(((o, "committed"), (n, "--depth"))):
            x = pad + k * (cw + pad)
            d.text((x, y), f"{sid} f{fi} {name}", fill=(235, 235, 220))
            big = im.resize((im.width * Z, im.height * Z), Image.NEAREST)
            out.paste(big, (x, y + lab), big)
        y += rh + pad
    os.makedirs(os.path.dirname(a.out), exist_ok=True)
    out.save(a.out)
    print("wrote", a.out)


if __name__ == "__main__":
    main()
