"""Owner contact sheet for the vehicle fan-out: python3 tools/art/vehicle_contact.py [--out vehicles_contact.png]
All 16 facings at 1x per vehicle, 2x close-ups (incl. scraper harvest frames), in-context patch with the refinery,
and the exact-match house remap (green / rust #8B2E1A / blue #2E5A8B)."""
import argparse, os
import numpy as np
from PIL import Image, ImageDraw
import common as C
from contact_sheet import ash_patch, hcat, vcat, BG, FG
import remap as R

VEH = os.path.join(C.ROOT, "data", "sprites", "vehicles")
GROUND = (74, 66, 58)
IDS = ["auth_tank", "auth_scraper", "auth_jeep"]


def read_manifest(vid):
    d = {}
    for ln in open(os.path.join(VEH, vid + ".sprite.toml")):
        ln = ln.strip()
        if "=" in ln and not ln.startswith(("#", "[", "ramp")):
            k, v = [x.strip() for x in ln.split("=", 1)]
            d.setdefault(k, []).append(v.strip('"'))
    return d


class Spr:
    def __init__(self, vid):
        m = read_manifest(vid)
        self.fw, self.fh = int(m["frame_w"][0]), int(m["frame_h"][0])
        self.ox, self.oy = int(m["origin_x"][0]), int(m["origin_y"][0])
        img = Image.open(os.path.join(VEH, vid + ".png")).convert("RGBA")
        self.cols = img.width // self.fw
        self.img = img
        self.states = {n: (int(f), int(fr), int(fi)) for n, f, fr, fi in zip(m["name"], m["facings"], m["frames"], m["first"])}

    def frame(self, state, facing, k=0):
        _f, fr, first = self.states[state]
        i = first + facing * fr + k
        x, y = (i % self.cols) * self.fw, (i // self.cols) * self.fh
        return self.img.crop((x, y, x + self.fw, y + self.fh))


def grid(frames, cols, z=1, gap=2):
    w, h = frames[0].width * z, frames[0].height * z
    rows = (len(frames) + cols - 1) // cols
    out = Image.new("RGB", (cols * (w + gap) - gap, rows * (h + gap) - gap), GROUND)
    for i, f in enumerate(frames):
        im = f.resize((w, h), Image.NEAREST) if z > 1 else f
        out.paste(im, ((i % cols) * (w + gap), (i // cols) * (h + gap)), im)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="vehicles_contact.png")
    a = ap.parse_args()
    S = {v: Spr(v) for v in IDS}
    blocks = []
    for v in IDS:
        s = S[v]
        blocks.append((f"{v}: idle, all 16 facings at 1x (frame 0 = N, clockwise), {s.fw} px frames", grid([s.frame("idle", f) for f in range(16)], 8 if s.fw > 96 else 16, 1)))
    close = [S["auth_tank"].frame("idle", 2), S["auth_scraper"].frame("idle", 2), S["auth_jeep"].frame("idle", 2),
             S["auth_scraper"].frame("idle", 10), S["auth_jeep"].frame("idle", 6)]
    blocks.append(("2x nearest close-up: tank f2 | scraper f2 | jeep f2 | scraper f10 | jeep f6", hcat([grid([c], 1, 2) for c in close], 8)))
    sc = S["auth_scraper"]
    blocks.append(("scraper HARVEST at 2x: facing 4 (east) frames 0-3 and facing 14 frames 0-3 (conveyor runs, blade shudders, ash puff, beacon blinks)",
                   hcat([grid([sc.frame("harvest", 4, k) for k in range(4)], 4, 2), grid([sc.frame("harvest", 14, k) for k in range(4)], 4, 2)], 16)))
    blocks.append(("scraper HARVEST at 1x, true size: facings 0, 4, 8, 12 x frames 0-3",
                   grid([sc.frame("harvest", f, k) for f in (0, 4, 8, 12) for k in range(4)], 8, 1)))
    # in-context patch
    cell = 64
    img = ash_patch(cell, 2027).convert("RGBA")
    ref = Image.open(os.path.join(C.ROOT, "data", "sprites", "buildings", "auth_refinery.png")).convert("RGBA").crop((0, 0, 192, 224))
    fx, fy = 1, 1
    img.alpha_composite(ref, (fx * cell, fy * cell - 32))
    spots = [("auth_tank", 3, 4, 1), ("auth_tank", 7, 2, 4), ("auth_scraper", 2, 4, 6), ("auth_scraper", 6, 4, 2),
             ("auth_jeep", 5, 5, 3), ("auth_jeep", 8, 3, 12), ("auth_jeep", 8, 5, 5), ("auth_scraper", 1, 4, 12)]
    spots = [("auth_scraper", 2, 4, 8), ("auth_tank", 5, 4, 4), ("auth_jeep", 7, 5, 6), ("auth_jeep", 8, 3, 2),
             ("auth_scraper", 6, 2, 10), ("auth_tank", 3, 5, 14)]
    for v, cx, cy, f in spots:
        s = S[v]
        img.alpha_composite(s.frame("idle", f), (int((cx + 0.5) * cell) - s.ox, int((cy + 0.5) * cell) - s.oy))
    d = ImageDraw.Draw(img)
    for gx in range(0, img.width, cell):
        for gy in range(0, img.height, cell):
            d.point((gx, gy), fill=(100, 94, 88))
    blocks.append(("IN CONTEXT 1x on an ash patch (10x6 cells of 64 px): refinery, scraper at the dock cell, tanks, jeeps. Cell ticks every 64 px", img.convert("RGB")))
    # remap
    R.TOL = 0
    ramp_set = {tuple(c) for c in C.HOUSE}
    rows = []
    for v, f in (("auth_tank", 2), ("auth_scraper", 2), ("auth_jeep", 2)):
        base = S[v].frame("idle", f)
        vs = [base]
        for n, rp in R.HOUSES.items():
            out, _c = R.remap(base, rp)
            a0, a1 = np.asarray(base).reshape(-1, 4), np.asarray(out).reshape(-1, 4)
            ch = (a0 != a1).any(axis=1)
            assert all(tuple(p[:3]) in ramp_set for p in a0[ch]), "remap touched a non-ramp pixel"
            assert (a0[:, 3] == a1[:, 3]).all()
            print(v, n, "pixels changed:", int(ch.sum()))
            vs.append(out)
        rows.append(grid(vs, 4, 2, 4))
    blocks.append(("HOUSE REMAP at 2x, facing 2: magenta original | Authority green | Directorate rust #8B2E1A | blue ally #2E5A8B (tank, scraper, jeep)", vcat(rows, 6)))
    W = max(b[1].width for b in blocks) + 20
    out = Image.new("RGB", (W, 10 + sum(b[1].height + 26 for b in blocks)), BG)
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
