"""Contact sheet for rotating-head defences (pillbox) next to auth_turret: python3 tools/art/structures/dir_def_contact.py dir_pillbox
Rows: facings at 1x (idle 0..15), fire flash k0/k1 + damaged + build at 2x, and in-context at 1x with conscripts."""
import os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import common as C
BG, FG, ASH = (24, 24, 30), (225, 225, 230), (74, 66, 58)


def frames(sid, w, h):
    im = Image.open(os.path.join(C.ROOT, "data", "sprites", "buildings", f"{sid}.png")).convert("RGBA")
    n = (im.height // h) * 4
    return [im.crop(((k % 4) * w, (k // 4) * h, (k % 4) * w + w, (k // 4) * h + h)) for k in range(n)]


def row(fs, z, gap=3):
    w, h = fs[0].size
    o = Image.new("RGB", (len(fs) * (w * z + gap), h * z), ASH)
    for j, f in enumerate(fs):
        g = f.resize((w * z, h * z), Image.NEAREST)
        o.paste(g, (j * (w * z + gap), 0), g)
    return o


def main(sid):
    d, a = frames(sid, 80, 104), frames("auth_turret", 80, 104)
    blocks = [("IDLE, 16 facings at 1x (true size): Directorate pillbox (top) | Authority turret (bottom)", [row(d[0:16], 1), row(a[0:16], 1)]),
              ("FIRE k0/k1 (facing N, E), IDLE N, DAMAGED N, DAMAGED E at 3x: pillbox | turret", [row([d[16], d[17], d[0], d[48], d[52]], 3), row([a[16], a[17], a[0], a[48], a[52]], 3)]),
              ("BUILD, 6 frames at 2x: pillbox | turret", [row(d[64:70], 2), row(a[64:70], 2)])]
    W = max(b.width for _t, bs in blocks for b in bs) + 20
    H = 10 + sum(26 + sum(b.height + 6 for b in bs) for _t, bs in blocks)
    out = Image.new("RGB", (W, H), BG)
    dr = ImageDraw.Draw(out)
    y = 10
    for t, bs in blocks:
        dr.text((10, y), t, fill=FG)
        y += 16
        for b in bs:
            out.paste(b, (10, y))
            y += b.height + 6
        y += 4
    p = os.path.join(C.PILOT, f"{sid}_contact.png")
    out.save(p, optimize=True)
    print("wrote", p, out.size)


if __name__ == "__main__":
    main(sys.argv[1])
