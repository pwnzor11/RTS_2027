"""Contact sheet for M2-20 (D-072 art polish): python3 tools/art/polish_contact.py [--out polish_contact.png]
1 roof panels before/after (conyard, power, silo) at 1x and 2x | 2 every wall variant at 2x (clean and damaged) |
3 in-context 1x ash patch: a walled square, an L, a T, a line with a damaged block, beside the turret |
4 rocketeer fire frames before/after at 1x and 3x.   'Before' images live in art/pilot/before_m2_20/."""
import argparse
import os

from PIL import Image

import common as C
import structures_contact_b as B
from contact_sheet import hcat, vcat, BG

BEFORE = os.path.join(C.PILOT, "before_m2_20")
INF = os.path.join(C.ROOT, "data", "sprites", "infantry")


def mask_name(m, damaged):
    t = ["post", "end_n", "end_e", "corner_ne", "end_s", "v", "corner_se", "t_e", "end_w", "corner_nw", "h", "t_n", "corner_sw", "t_w", "t_s", "cross"]
    return t[m] + ("_damaged" if damaged else "")


def frame_of(img, fw, fh, i):
    cols = img.width // fw
    return img.crop(((i % cols) * fw, (i // cols) * fh, (i % cols + 1) * fw, (i // cols + 1) * fh))


def up(img, z):
    return img.resize((img.width * z, img.height * z), Image.NEAREST) if z > 1 else img


def flat(img, bg=B.GROUND):
    out = Image.new("RGBA", img.size, bg + (255,))
    out.alpha_composite(img)
    return out.convert("RGB")


def roofs():
    rows = []
    for sid in ("auth_conyard", "auth_power", "auth_silo"):
        S = B.Sheet(sid)
        old = Image.open(os.path.join(BEFORE, sid + ".png")).convert("RGBA")
        o = frame_of(old, S.fw, S.fh, 0)
        n = S.st("idle", 0, 1)[0]
        rows.append(hcat([flat(o), flat(n), up(flat(o), 2), up(flat(n), 2)], 12))
    return vcat(rows, 12)


def wall_variants(W):
    names = list(W.mod.VARIANTS)
    out = []
    for dmg in (False, True):
        cells = [up(flat(W.st(n + ("_damaged" if dmg else ""))[0]), 2) for n in names]
        half = len(cells) // 2
        out.append(hcat(cells[:half], 4))
        out.append(hcat(cells[half:], 4))
    return vcat(out, 8)


def patch(W, T, w=17, h=8):
    """1x ash patch: layout by wall cells; each wall's state from the same mask rule as WallTiling.cs."""
    sq = {(1, 1), (2, 1), (3, 1), (1, 2), (3, 2), (1, 3), (2, 3), (3, 3)}
    ell = {(6, 1), (6, 2), (6, 3), (7, 3), (8, 3)}
    tee = {(11, 1), (12, 1), (13, 1), (12, 2), (12, 3)}
    line = {(1, 5), (2, 5), (3, 5), (4, 5), (5, 5), (6, 5)}
    walls = sq | ell | tee | line
    damaged = {(4, 5), (3, 1), (6, 2)}
    img = B.ash(w * 64, h * 64 + 40, 5).convert("RGBA")
    for (x, y) in sorted(walls, key=lambda c: (c[1], c[0])):
        m = 0
        m |= 1 if (x, y - 1) in walls else 0
        m |= 2 if (x + 1, y) in walls else 0
        m |= 4 if (x, y + 1) in walls else 0
        m |= 8 if (x - 1, y) in walls else 0
        spr = W.st(mask_name(m, (x, y) in damaged))[0]
        B.put(img, spr, x, y, W.ox, W.oy)
    # turret beside the square
    t = T.st("idle", 0, 1)[0]
    B.put(img, t, 5, 6, T.ox, T.oy)
    B.put(img, t, 10, 5, T.ox, T.oy)
    return img.convert("RGB").crop((0, 0, 15 * 64, 8 * 64 + 20))


def rocket_frames(sheet, fx, fy, facings):
    img = Image.open(sheet).convert("RGBA")
    first = 8 + 48              # idle 8 x 1, walk 8 x 6
    return [flat(frame_of(img, 32, 32, first + f * 2 + s), (74, 66, 58)) for f in facings for s in (0, 1)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="polish_contact.png")
    a = ap.parse_args()
    W, T = B.Sheet("auth_wall"), B.Sheet("auth_turret")
    blocks = [
        ("ROOFS: before 1x | after 1x | before 2x | after 2x   (rows: construction yard, power plant, silo; frame 0 idle)", roofs()),
        ("WALL VARIANTS at 2x: h v end_n end_e end_s end_w corner_ne corner_nw | corner_se corner_sw t_n t_e t_s t_w cross post; then the same damaged", wall_variants(W)),
        ("WALLS IN CONTEXT at 1x: a walled square, an L, a T, a line with a damaged block (4th), two turrets", patch(W, T)),
    ]
    facings = (2, 0, 4)
    old = rocket_frames(os.path.join(BEFORE, "auth_rocketeer.png"), 0, 0, facings)
    new = rocket_frames(os.path.join(INF, "auth_rocketeer.png"), 0, 0, facings)
    for z in (1, 3):
        blocks.append((f"ROCKETEER fire at {z}x: facings E, N, S x (aim, launch) ; top row before, bottom row after",
                       vcat([hcat([up(i, z) for i in old], 6 * z), hcat([up(i, z) for i in new], 6 * z)], 8)))
    ims = []
    for t, im in blocks:
        ims += [B.label(t), im]
    out = vcat(ims, 10)
    out.save(os.path.join(C.PILOT, a.out))
    print("wrote", os.path.join(C.PILOT, a.out), out.size)


if __name__ == "__main__":
    main()
