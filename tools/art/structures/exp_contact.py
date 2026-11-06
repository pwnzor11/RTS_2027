"""Expedition structure pilot contact sheet: python3 tools/art/structures/exp_contact.py <id_A> <id_B> <out.png>
Rows: sand ground with team Crimson, ash ground with team Lime.  Each row: A idle 0, A idle 2, A damaged 0, A build stage 4, then B idle 0, B idle 2.
Footprint outline (3x3 cells) drawn as corner ticks; below, a 1x true-size strip next to the Authority conyard for scale."""
import argparse, os, sys
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(HERE)
sys.path.insert(0, ART)
import common as C
import remap as R

BLD = os.path.join(C.ROOT, "data", "sprites", "buildings")
ALT = os.path.join(C.PILOT, "exp_variant_b")
TEAMS = {"Crimson": C.make_ramp(C.hx("#2A0A10"), C.hx("#B5202F"), C.hx("#F6A0A8")),
         "Lime": C.make_ramp(C.hx("#10260A"), C.hx("#5FA82A"), C.hx("#D8F59A"))}


def frames(sid):
    folder = ALT if os.path.exists(os.path.join(ALT, sid + ".png")) else BLD
    m = {}
    for ln in open(os.path.join(folder, sid + ".sprite.toml")):
        ln = ln.strip()
        if "=" in ln and not ln.startswith(("#", "[", "ramp")):
            k, v = [x.strip() for x in ln.split("=", 1)]
            m.setdefault(k, []).append(v.strip('"'))
    fw, fh = int(m["frame_w"][0]), int(m["frame_h"][0])
    ox, oy = int(m["origin_x"][0]), int(m["origin_y"][0])
    img = Image.open(os.path.join(folder, sid + ".png")).convert("RGBA")
    cols = img.width // fw
    first = {n: int(f) for n, f in zip(m["name"], m["first"])}
    def fr(state, i):
        k = first[state] + i
        return img.crop(((k % cols) * fw, (k // cols) * fh, (k % cols) * fw + fw, (k // cols) * fh + fh))
    return fr, (ox, oy, fw, fh)


def ground(kind, w, h, seed):
    rng = np.random.default_rng(seed)
    base = np.array([150, 132, 98] if kind == "sand" else [74, 68, 64], dtype=float)
    blot = np.asarray(Image.fromarray(rng.integers(0, 255, (h // 16 + 2, w // 16 + 2), dtype=np.uint8)).resize((w, h), Image.BICUBIC), dtype=float)[..., None]
    arr = np.clip(base + rng.normal(0, 5, (h, w, 1)) + (blot - 128) * 0.12, 0, 255)
    return Image.fromarray(arr.astype(np.uint8), "RGB")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("a"); ap.add_argument("b"); ap.add_argument("out")
    a = ap.parse_args()
    FA, (ox, oy, fw, fh) = frames(a.a)
    FB, _ = frames(a.b)
    picks = [(FA, "idle", 0, "A idle 0"), (FA, "idle", 2, "A idle 2"), (FA, "damaged", 0, "A damaged"), (FA, "build", 4, "A build 4/8"),
             (FB, "idle", 0, "B idle 0"), (FB, "idle", 2, "B idle 2")]
    slot_w, slot_h = fw + 24, fh + 40
    rows = []
    for ri, (kind, team) in enumerate((("sand", "Crimson"), ("ash", "Lime"))):
        im = ground(kind, slot_w * len(picks), slot_h, 5 + ri)
        d = ImageDraw.Draw(im)
        d.text((4, 2), f"{kind} ground | team {team} | footprint ticks = 3x3 cells", fill=(255, 255, 255))
        for i, (F, st, k, lab) in enumerate(picks):
            x0, y0 = i * slot_w + 12, 28
            fr = R.remap(F(st, k), TEAMS[team])[0]
            for cx in (0, 192):
                for cy in (0, 192):
                    d.rectangle((x0 + ox + cx - 1, y0 + oy + cy - 1, x0 + ox + cx + 1, y0 + oy + cy + 1), outline=(20, 20, 20))
            im.paste(fr, (x0, y0), fr)
            d.text((x0 + 4, slot_h - 14), lab, fill=(255, 255, 255))
        rows.append(im)
    # 1x scale strip with the Authority conyard
    cy_img = Image.open(os.path.join(BLD, "auth_conyard.png")).convert("RGBA")
    cfr = cy_img.crop((0, 0, 192, 248))
    strip = Image.new("RGB", (rows[0].width, 270), (74, 66, 58))
    ImageDraw.Draw(strip).text((4, 2), "1x true size: Authority conyard (3x3), Landing Depot A, Landing Depot B", fill=(255, 255, 255))
    for i, im in enumerate((cfr, R.remap(FA("idle", 0), TEAMS["Crimson"])[0], R.remap(FB("idle", 0), TEAMS["Crimson"])[0])):
        strip.paste(im, (10 + i * 220, 16), im)
    out = Image.new("RGB", (rows[0].width, sum(r.height for r in rows) + strip.height), (30, 28, 34))
    y = 0
    for r in rows + [strip]:
        out.paste(r, (0, y)); y += r.height
    out.save(a.out)
    print("wrote", a.out, out.size)


if __name__ == "__main__":
    main()
