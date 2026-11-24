"""Before/after contact sheet + game-speed GIF for a building pilot.
python3 tools/art/bld_pilot_sheet.py <before.png> <after.png> <out_sheet.png> <out.gif> [--w 128 --h 176 --idle 8]
Ground colours: rustbelt land.clear base #8E8371 and land.ash base #42394D.  Idle runs at 4 ticks/frame in game (tpf 1 x BuildingIdleSlowdown 4
at 20 ticks/s) = 200 ms per frame."""
import argparse
from PIL import Image

ap = argparse.ArgumentParser()
for k in ("before", "after", "sheet", "gif"):
    ap.add_argument(k)
ap.add_argument("--w", type=int, default=128)
ap.add_argument("--h", type=int, default=176)
ap.add_argument("--idle", type=int, default=8)
ap.add_argument("--before-damaged", type=int, default=4)
a = ap.parse_args()
CLEAR, ASH = (0x8E, 0x83, 0x71, 255), (0x42, 0x39, 0x4D, 255)
fw, fh = a.w, a.h
B, A = Image.open(a.before).convert("RGBA"), Image.open(a.after).convert("RGBA")


def fr(sh, n):
    c = sh.width // fw
    return sh.crop(((n % c) * fw, (n // c) * fh, (n % c) * fw + fw, (n // c) * fh + fh))


def on(im, col, z=1):
    bg = Image.new("RGBA", im.size, col)
    bg.alpha_composite(im)
    return bg.resize((im.width * z, im.height * z), Image.NEAREST)


Wd = 1620
sheet = Image.new("RGBA", (Wd, 560 + 2 * (3 * fh) + 80), (24, 24, 24, 255))
x = 8
for col in (CLEAR, ASH):       # 3x: before idle0, after idle0, after damaged0
    pass
items = [(fr(B, 0), CLEAR), (fr(A, 0), CLEAR), (fr(A, a.idle), CLEAR)]
for k, (im, col) in enumerate(items):
    sheet.alpha_composite(on(im, col, 3), (8 + k * (3 * fw + 8), 8))
# 1x column (true size) on clear and ash: before | after | damaged
for r, col in enumerate((CLEAR, ASH)):
    for k, im in enumerate((fr(B, 0), fr(A, 0), fr(A, a.idle))):
        sheet.alpha_composite(on(im, col, 1), (3 * (3 * fw + 8) + 24 + k * (fw + 8), 8 + r * (fh + 8)))
# the idle frames, 3x, 4 per row
y0 = 3 * fh + 24
for n in range(a.idle):
    sheet.alpha_composite(on(fr(A, n), CLEAR, 3), (8 + (n % 4) * (3 * fw + 8), y0 + (n // 4) * (3 * fh + 8)))
sheet = sheet.crop((0, 0, Wd, y0 + 2 * (3 * fh + 8)))
sheet.convert("RGB").save(a.sheet)
# GIF: after-idle | after-damaged at 2x, 200 ms per frame
g = []
for n in range(a.idle):
    im = Image.new("RGBA", (2 * fw * 2 + 8, 2 * fh), CLEAR)
    im.alpha_composite(on(fr(A, n), CLEAR, 2), (0, 0))
    im.alpha_composite(on(fr(A, a.idle + n), CLEAR, 2), (2 * fw + 8, 0))
    g.append(im.convert("RGB"))
g[0].save(a.gif, save_all=True, append_images=g[1:], duration=200, loop=0)
print("wrote", a.sheet, a.gif)
