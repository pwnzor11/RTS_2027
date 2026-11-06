"""Contact sheet for package art-exp-e: python3 tools/art/structures/exp_sheet_art_exp_e.py <out.png> <id> [<id> ...]
One row per asset: idle 0, idle 2, damaged 0, build mid, build last at 2x on sand (team Crimson), then idle 0 at 2x on ash/water (team Lime).
Bottom strip: 1x true size next to the Landing Depot and the Glide Tank."""
import os, sys
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import common as C
import remap as R
import exp_contact as X

TEAMS = X.TEAMS
WATER = {"exp_shipyard": (128,), "exp_drydock": (128,)}   # frame y (px) where the sea row starts: origin_y + 2 cells
BLD = X.BLD


def water(w, h, seed):
    rng = np.random.default_rng(seed)
    base = np.array([38, 78, 104], dtype=float)
    arr = np.clip(base + rng.normal(0, 4, (h, w, 1)), 0, 255)
    return Image.fromarray(arr.astype(np.uint8), "RGB")


def main():
    out, ids = sys.argv[1], sys.argv[2:]
    rows = []
    for ri, sid in enumerate(ids):
        F, (ox, oy, fw, fh) = X.frames(sid)
        picks = [("idle", 0, "idle 0", "sand", "Crimson"), ("idle", 2, "idle 2", "sand", "Crimson"), ("damaged", 0, "damaged", "sand", "Crimson"),
                 ("build", 3, "build 3", "sand", "Crimson"), ("build", 7 if "build" and True else 0, "build last", "sand", "Crimson"),
                 ("idle", 0, "idle 0 ash/water", "ash", "Lime"), ("damaged", 2, "damaged 2 ash", "ash", "Lime")]
        sw, sh = fw * 2 // 2 + 6, fh // 2 * 2 // 2
        S = 1  # draw at 1x half-size slots, pasted at 2x only for single column fit
        slot_w, slot_h = fw + 8, fh + 22
        im = Image.new("RGB", (slot_w * len(picks), slot_h), (30, 28, 34))
        d = ImageDraw.Draw(im)
        for i, (st, k, lab, kind, team) in enumerate(picks):
            bg = X.ground(kind, slot_w, slot_h, 3 + i)
            if "water" in lab or st == "idle" and kind == "ash":
                pass
            im.paste(bg, (i * slot_w, 0))
            wy = WATER.get(sid)
            if wy:
                wb = water(slot_w, slot_h - (4 + oy + wy[0]), 9 + i)
                im.paste(wb, (i * slot_w, 4 + oy + wy[0]))
            try:
                fr = R.remap(F(st, k), TEAMS[team])[0]
            except KeyError:
                continue
            im.paste(fr, (i * slot_w + 4, 4), fr)
            d.text((i * slot_w + 4, slot_h - 14), f"{sid[4:]} {lab}", fill=(255, 255, 255))
        rows.append(im)
    W = max(r.width for r in rows)
    strip_h = 280
    strip = Image.new("RGB", (W, strip_h), (74, 66, 58))
    ImageDraw.Draw(strip).text((4, 2), "1x true size: Landing Depot | " + " | ".join(i[4:] for i in ids), fill=(255, 255, 255))
    x = 8
    ld, _ = X.frames("exp_landing_depot")
    im0 = R.remap(ld("idle", 0), TEAMS["Crimson"])[0]
    strip.paste(im0, (x, 16), im0); x += im0.width + 6
    gt = os.path.join(C.ROOT, "data", "sprites", "vehicles", "exp_glide_tank.png")
    if os.path.exists(gt):
        g = Image.open(gt).convert("RGBA").crop((0, 0, 128, 128))
        strip.paste(g, (x, 150), g); x += 134
    for sid in ids:
        F, (_, _, fw, fh) = X.frames(sid)
        im1 = R.remap(F("idle", 0), TEAMS["Crimson"])[0]
        if x + im1.width > W:
            break
        strip.paste(im1, (x, 16), im1); x += im1.width + 6
    out_im = Image.new("RGB", (W, sum(r.height for r in rows) + strip_h), (30, 28, 34))
    y = 0
    for r in rows + [strip]:
        out_im.paste(r, (0, y)); y += r.height
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    out_im.save(out)
    print("wrote", out, out_im.size)


if __name__ == "__main__":
    main()
