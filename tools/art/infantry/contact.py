"""Owner contact sheet for an infantry sprite: python3 tools/art/infantry/contact.py [id] [--out infantry_contact.png]
All states / facings at 1x and 3x, five soldiers sharing one cell next to the tank and the refinery on ash and on clear
ground, and the house remap (exact-match ramp replacement; asserts only ramp pixels change)."""
import argparse, os, sys
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import common as C
from contact_sheet import ash_patch, BG, FG
import remap as R
import build_infantry as BI

ASH = (74, 66, 58)
SLOTS = [(32, 36), (15, 25), (49, 25), (15, 53), (49, 53)]   # sub-slot centres (feet) inside a 64 px cell, 5 per cell
CLEAR = (126, 116, 96)


def clear_patch(cell, seed):
    rng = np.random.default_rng(seed)
    w, h = 7 * cell, 4 * cell
    n = rng.normal(0, 3.0, (h, w, 1))
    blotch = np.asarray(Image.fromarray(rng.integers(0, 255, (h // 8 + 1, w // 8 + 1), dtype=np.uint8)).resize((w, h), Image.BILINEAR), dtype=float)[..., None]
    arr = np.clip(np.array(CLEAR, dtype=float) + n + (blotch - 128) * 0.05, 0, 255).astype(np.uint8)
    return Image.fromarray(arr, "RGB")


def ash(cell, seed):
    rng = np.random.default_rng(seed)
    w, h = 7 * cell, 4 * cell
    n = rng.normal(0, 3.0, (h, w, 1))
    blotch = np.asarray(Image.fromarray(rng.integers(0, 255, (h // 8 + 1, w // 8 + 1), dtype=np.uint8)).resize((w, h), Image.BILINEAR), dtype=float)[..., None]
    arr = np.clip(np.array([52, 47, 44], dtype=float) + n + (blotch - 128) * 0.04, 0, 255).astype(np.uint8)
    return Image.fromarray(arr, "RGB")


def zoom(im, z):
    return im.resize((im.width * z, im.height * z), Image.NEAREST) if z > 1 else im


def hcat(ims, gap=20):
    W = sum(i.width for i in ims) + gap * (len(ims) - 1)
    out = Image.new("RGB", (W, max(i.height for i in ims)), BG)
    x = 0
    for i in ims:
        out.paste(i, (x, 0))
        x += i.width + gap
    return out


def grid(frames, rows, cols, z, gap=2):
    """frames[r][c] RGBA frames -> RGB image on ash ground."""
    w, h = frames[0][0].size
    out = Image.new("RGB", (cols * (w * z + gap) - gap, rows * (h * z + gap) - gap), ASH)
    for r in range(rows):
        for c in range(cols):
            f = zoom(frames[r][c], z)
            out.paste(f, (c * (w * z + gap), r * (h * z + gap)), f)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id", nargs="?", default="auth_rifleman")
    ap.add_argument("--out", default="infantry_contact.png")
    a = ap.parse_args()
    mod = BI.load_module(a.id)
    w, h = mod.CANVAS
    ox, oy = mod.ORIGIN
    sheet = Image.open(os.path.join(C.ROOT, "data", "sprites", "infantry", f"{a.id}.png")).convert("RGBA")
    fr = C.split_sheet(sheet, BI.COLS, (sheet.height // h))
    _frames, firsts = BI.frame_list(mod)
    S = {name: (nf, n, firsts[name]) for name, nf, n, _t in mod.STATES}

    def get(state, facing, step=0):
        nf, n, first = S[state]
        return fr[first + (facing if nf > 1 else 0) * n + step]

    blocks = []
    N = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"]
    for state in ("idle", "walk", "fire"):
        nf, n, _ = S[state]
        g = [[get(state, f, k) for f in range(8)] for k in range(n)]
        blocks.append((f"{state.upper()}: columns = 8 facings (N NE E SE S SW W NW, clockwise), rows = {n} frame(s).  Left 1x true size, right 3x nearest",
                       hcat([grid(g, n, 8, 1), grid(g, n, 8, 3)], 28)))
    nd = S["die"][1]
    gd = [[get("die", 0, k) for k in range(nd)]]
    blocks.append(("DIE: 6 frames, one facing (forward fall to the east).  1x then 3x", hcat([grid(gd, 1, nd, 1), grid(gd, 1, nd, 3)], 28)))

    # in-context patches 7x4 cells of 64 px
    cell = 64
    tank = C.split_sheet(Image.open(os.path.join(C.ROOT, "data", "sprites", "vehicles", "tank_v2.png")).convert("RGBA"), 4, 4)
    ref = Image.open(os.path.join(C.ROOT, "data", "sprites", "buildings", "auth_refinery.png")).convert("RGBA")
    refinery = ref.crop((0, 0, 192, 224))
    tax, tay = C.anchor_px(64)

    def context(ground, house=None):
        img = ground.convert("RGBA")
        sprites = []                                   # (foot_y, image, x, y)

        def put(f, fx, fy):
            f = f if house is None else R.remap(f, house)[0]
            sprites.append((fy, f, fx - ox, fy - oy))
        cx, cy = 0 * cell, 2 * cell                   # cell (0,2): five riflemen, mixed states and facings
        picks = [get("idle", 4), get("fire", 3, 1), get("walk", 2, 1), get("idle", 6), get("walk", 5, 4)]
        for (sx, sy), f in zip(SLOTS, picks):
            put(f, cx + sx, cy + sy)
        for k in range(3):                             # a squad marching east along row 1 (walk frames out of step)
            put(get("walk", 2, (k * 2) % 6), cell + 20 + k * 40 + 0, cell + 36 + (k % 2) * 6)
        for k in range(2):                             # two firing at the tank's cell from the south
            put(get("fire", 0, k), 2 * cell + 20 + k * 24, 3 * cell + 40)
        t = tank[2]
        sprites.append((2 * cell + 40, t, int(2.5 * cell - tax), int(2.5 * cell - tay)))
        sprites.append((1 * cell + 192 + 16, refinery, 4 * cell - 0, 1 * cell - 32))
        sprites.sort(key=lambda s: s[0])
        for _y, f, x, y in sprites:
            img.alpha_composite(f, (x, y))
        d = ImageDraw.Draw(img)
        for gx in range(0, img.width, cell):
            for gy in range(0, img.height, cell):
                d.point((gx, gy), fill=(150, 140, 120))
        # the five-soldier cell outline (corner dots)
        for px, py in ((0, 2 * cell), (cell - 1, 2 * cell), (0, 3 * cell - 1), (cell - 1, 3 * cell - 1)):
            d.point((px, py), fill=(230, 200, 60))
        return img.convert("RGB")
    ash_img, clear_img = context(ash(cell, 2027)), context(clear_patch(cell, 11))
    blocks.append(("IN CONTEXT 1x, ash ground: five riflemen share the bottom-left cell (yellow corner dots), a squad marches east, two fire at the v2 tank, refinery frame 0 at right",
                   ash_img))
    blocks.append(("IN CONTEXT 1x, clear (cleaned) ground: same layout", clear_img))
    blocks.append(("IN CONTEXT 3x crop of the five-soldier cell and tank, ash ground",
                   zoom(ash_img.crop((0, 2 * cell - 8, 3 * cell, 4 * cell)), 3)))

    # remap: five riflemen in one cell, each house, at 3x
    R.TOL = 0
    ramp_set = {tuple(c) for c in C.HOUSE}
    variants = [("original", None)] + list(R.HOUSES.items())
    imgs = []
    picks = [get("idle", 4), get("fire", 3, 1), get("walk", 2, 1), get("idle", 6), get("walk", 5, 4)]
    for name, ramp in variants:
        cellimg = Image.new("RGBA", (cell, cell), ASH + (255,))
        for (sx, sy), f in sorted(zip(SLOTS, picks), key=lambda t: t[0][1]):
            g = f
            if ramp is not None:
                g, _cnt = R.remap(f, ramp)
                a0, a1 = np.asarray(f).reshape(-1, 4), np.asarray(g).reshape(-1, 4)
                ch = (a0 != a1).any(axis=1)
                assert all(tuple(p[:3]) in ramp_set for p in a0[ch]), "remap touched a non-ramp pixel"
                assert (a0[:, 3] == a1[:, 3]).all()
            cellimg.alpha_composite(g, (sx - ox, sy - oy))
        imgs.append(zoom(cellimg.convert("RGB"), 3))
    blocks.append(("HOUSE REMAP at 3x, five riflemen per cell: magenta original, Authority green, Directorate rust #8B2E1A, blue ally #2E5A8B", hcat(imgs, 10)))

    W = max(b[1].width for b in blocks) + 20
    total = 10 + sum(b[1].height + 26 for b in blocks)
    out = Image.new("RGB", (W, total), BG)
    dr = ImageDraw.Draw(out)
    y = 10
    for label, im in blocks:
        dr.text((10, y), label, fill=FG)
        out.paste(im, (10, y + 16))
        y += im.height + 26
    out.save(os.path.join(C.PILOT, a.out), optimize=True)
    print("wrote", a.out, out.size)


if __name__ == "__main__":
    main()
