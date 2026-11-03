"""Owner contact sheet for a structure: python3 tools/art/structure_contact.py <id> [--out refinery_contact.png]
Idle at 1x/2x, damaged, animation strips, build strip, in-context patch next to the v2 tank, house remap (exact-match)."""
import argparse, os, sys
import numpy as np
from PIL import Image, ImageDraw
import common as C
from contact_sheet import ash_patch, hcat, vcat, BG, FG
import remap as R
import build_structure as BS

TANK = os.path.join(C.ROOT, "data", "sprites", "vehicles", "tank_v2.png")
GROUND = (74, 66, 58)


def strip(frames, z, gap=4):
    cw, ch = frames[0].width * z, frames[0].height * z
    out = Image.new("RGB", (len(frames) * (cw + gap) - gap, ch), GROUND)
    for i, f in enumerate(frames):
        big = f.resize((cw, ch), Image.NEAREST) if z > 1 else f
        out.paste(big, (i * (cw + gap), 0), big)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--out", default="refinery_contact.png")
    ap.add_argument("--before", default=os.path.join(C.PILOT, "refinery_contact_before_sheet.png"),
                    help="the pilot (pass 1) sprite sheet, for the before/after block")
    a = ap.parse_args()
    mod = BS.load_module(a.id)
    fw, fh = mod.CANVAS
    ox, oy = mod.ORIGIN
    sheet = Image.open(os.path.join(C.ROOT, "data", "sprites", "buildings", f"{a.id}.png")).convert("RGBA")
    fr = C.split_sheet(sheet, 4, 4)
    st = {}
    k = 0
    for name, n, _t in mod.STATES:
        st[name] = fr[k:k + n]
        k += n
    blocks = []
    blocks.append(("IDLE frame 0: 1x (left), 2x nearest (middle)  |  DAMAGED frame 0 at 2x (right)",
                   hcat([strip([st["idle"][0]], 1), strip([st["idle"][0]], 2), strip([st["damaged"][0]], 2)], 24)))
    blocks.append(("ANIMATION at 1x, true size: idle frames 0-3 (drum turns, tube lamps alternate, stack puffs)  |  damaged frames 0-3 (fire, plume, embers)",
                   hcat([strip(st["idle"], 1), strip(st["damaged"], 1)], 24)))
    blocks.append(("BUILD, 8 frames at 1x: 0 surveyed plot, 1 foundation + rebar, 2 first steel, 3 steel frame, 4 trusses + first panels, 5 cladding + half roof, 6 primer + scaffold down, 7 finished",
                   strip(st["build"], 1)))
    blocks.append(("BUILD at 2x (frames 0-3)", strip(st["build"][:4], 2)))
    blocks.append(("BUILD at 2x (frames 4-7)", strip(st["build"][4:], 2)))
    blocks.append(("DAMAGED at 2x (loops: fire flicker, plume rises and grows)", strip(st["damaged"], 2)))
    if os.path.exists(a.before):
        old_fr = C.split_sheet(Image.open(a.before).convert("RGBA"), 4, 4)
        pair = lambda b_, a_, lab: hcat([strip([b_], 2), strip([a_], 2)], 24)
        blocks.append(("BEFORE (pilot, left) vs AFTER (this pass, right): BUILD frame 3", pair(old_fr[8 + 3], st["build"][3], "")))
        blocks.append(("BEFORE (pilot, left) vs AFTER (this pass, right): DAMAGED frame 0", pair(old_fr[4], st["damaged"][0], "")))
    # in-context patch: 10x6 cells at 64 px, footprint top-left at cell (2, 1), dock cell below the middle
    cell = 64
    img = ash_patch(cell, 2027).convert("RGBA")
    fx, fy = 2, 1
    img.alpha_composite(st["idle"][0], (fx * cell - ox, fy * cell - oy))
    tank = C.split_sheet(Image.open(TANK).convert("RGBA"), 4, 4)
    ax, ay = C.anchor_px(64)
    dock = (fx + mod.DOCK[0], fy + mod.DOCK[1])
    for (cx, cy), t in (((dock[0], dock[1]), 0), ((7, 2), 3), ((8, 4), 5), ((6, 5), 12)):
        f = tank[t]
        img.alpha_composite(f, (int((cx + 0.5) * cell - ax), int((cy + 0.5) * cell - ay)))
    d = ImageDraw.Draw(img)
    for gx in range(0, img.width, cell):
        for gy in range(0, img.height, cell):
            d.point((gx, gy), fill=(100, 94, 88))
    # mark the dock cell corners
    x0, y0 = dock[0] * cell, dock[1] * cell
    for px, py in ((x0, y0), (x0 + cell - 1, y0), (x0, y0 + cell - 1), (x0 + cell - 1, y0 + cell - 1)):
        d.point((px, py), fill=(230, 200, 60))
    blocks.append(("IN CONTEXT 1x on an ash patch (10x6 cells, 64 px): 3x3 refinery, v2 tank in the dock cell (yellow corner dots) and more tanks. Cell ticks every 64 px",
                   img.convert("RGB")))
    # remap
    R.TOL = 0
    ramp_set = {tuple(c) for c in C.HOUSE}
    base = st["idle"][0]
    variants = [("original", base)]
    for n, rp in R.HOUSES.items():
        out, cnt = R.remap(base, rp)
        a0, a1 = np.asarray(base).reshape(-1, 4), np.asarray(out).reshape(-1, 4)
        ch = (a0 != a1).any(axis=1)
        assert all(tuple(p[:3]) in ramp_set for p in a0[ch]), "remap touched a non-ramp pixel"
        assert (a0[:, 3] == a1[:, 3]).all()
        print(n, "pixels changed:", int(ch.sum()))
        variants.append((n, out))
    blocks.append(("HOUSE REMAP (exact-match ramp replacement) at 2x: magenta original, Authority green, Directorate rust #8B2E1A, blue ally #2E5A8B",
                   strip([v for _n, v in variants], 2, 8)))
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
