"""Contact sheet for the effects: python3 tools/art/fx/fx_contact.py  ->  art/pilot/fx_contact.png"""
import os, re, sys
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_fx as B

ROOT = B.ROOT
GROUND = (74, 66, 58)
BG = (30, 32, 34)
FG = (220, 220, 210)


def load(name):
    sheet = Image.open(os.path.join(B.OUT, f"{name}.png")).convert("RGBA")
    toml = open(os.path.join(B.OUT, f"{name}.sprite.toml")).read()
    g = lambda k: int(re.search(rf"^{k} = (\d+)", toml, re.M).group(1))
    w, h, n, ox, oy = g("frame_w"), g("frame_h"), g("frames"), g("origin_x"), g("origin_y")
    return dict(frames=[sheet.crop((i * w, 0, (i + 1) * w, h)) for i in range(n)], w=w, h=h, n=n, ox=ox, oy=oy,
                tpf=g("ticks_per_frame"), loop="loop = false" not in toml)


def frame_at(fx, tick, phase=0):
    step = tick // fx["tpf"] + phase
    return fx["frames"][step % fx["n"] if fx["loop"] else min(step, fx["n"] - 1)]


def strip(frames, z, gap=3, bg=GROUND):
    cw, ch = frames[0].width * z, frames[0].height * z
    out = Image.new("RGB", (len(frames) * (cw + gap) - gap, ch), bg)
    for i, f in enumerate(frames):
        big = f.resize((cw, ch), Image.NEAREST) if z > 1 else f
        out.paste(big, (i * (cw + gap), 0), big)
    return out


def scene(base, comps, z):
    """comps: [(fx, anchor_x, anchor_y, tick, phase)] composited on the base frame, then scaled."""
    img = base.copy().convert("RGBA")
    for fx, ax, ay, tick, phase in comps:
        fr = frame_at(fx, tick, phase)
        img.alpha_composite(fr, (ax - fx["ox"], ay - fx["oy"]))
    if z > 1:
        img = img.resize((img.width * z, img.height * z), Image.NEAREST)
    bg = Image.new("RGB", img.size, GROUND)
    bg.paste(img, (0, 0), img)
    return bg


def main():
    fxs = {k: load(k) for k in B.EFFECTS}
    blocks = []
    # 1x and 3x of the first frames side by side
    for name, fx in fxs.items():
        mid = fx["frames"][fx["n"] // 3]
        kind = "loops" if fx["loop"] else "one-shot"
        row = Image.new("RGB", (fx["w"] * 3 + fx["w"] + 40, fx["h"] * 3), GROUND)
        big = mid.resize((fx["w"] * 3, fx["h"] * 3), Image.NEAREST)
        row.paste(big, (0, 0), big)
        row.paste(mid, (fx["w"] * 3 + 20, 0), mid)
        blocks.append((f"{name}  {fx['w']}x{fx['h']}  {fx['n']} frames  {fx['tpf']} ticks/frame  {kind}: frame {fx['n'] // 3} at 3x (left) and 1x (right)", row))
    for name, fx in fxs.items():
        z = 3 if fx["w"] <= 32 else 2
        blocks.append((f"{name}: all {fx['n']} frames at {z}x", strip(fx["frames"], z)))
    # refinery damaged with fire + smoke at the manifest anchors
    toml = open(os.path.join(ROOT, "data", "sprites", "buildings", "auth_refinery.sprite.toml")).read()
    anchors = []
    for blk in toml.split("[[fx]]")[1:]:
        d = dict(re.findall(r'(\w+) = "?([\w]+)"?', blk))
        anchors.append((d["kind"], int(d["x"]), int(d["y"]), int(d["phase"])))
    sheet = Image.open(os.path.join(ROOT, "data", "sprites", "buildings", "auth_refinery.png")).convert("RGBA")
    fw, fh = 192, 224
    dmg = [sheet.crop(((4 + i) % 4 * fw, (4 + i) // 4 * fh, ((4 + i) % 4 + 1) * fw, ((4 + i) // 4 + 1) * fh)) for i in range(4)]
    order = ["fire_large", "fire_small", "smoke_plume"]
    anchors.sort(key=lambda a: order.index(a[0]) if a[0] in order else 9)      # smoke last = on top
    for z in (1, 2):
        frames = []
        for t in (0, 12, 24, 36) if z == 2 else (0, 6, 12, 18, 24, 30):
            comps = [(fxs[k], x, y, t, ph) for k, x, y, ph in anchors]
            frames.append(scene(dmg[(t // 5) % 4], comps, z))
        row = Image.new("RGB", (sum(f.width + 6 for f in frames), frames[0].height), GROUND)
        xo = 0
        for f in frames:
            row.paste(f, (xo, 0)); xo += f.width + 6
        blocks.append((f"REFINERY damaged + fire/smoke at the manifest anchors, {z}x, animated ticks " + ("0,12,24,36" if z == 2 else "0..30"), row))
    W = max(b[1].width for b in blocks) + 20
    total = 10 + sum(b[1].height + 26 for b in blocks)
    out = Image.new("RGB", (W, total), BG)
    d = ImageDraw.Draw(out)
    y = 10
    for label, im in blocks:
        d.text((10, y), label, fill=FG)
        out.paste(im, (10, y + 16))
        y += im.height + 26
    p = os.path.join(ROOT, "art", "pilot", "fx_contact.png")
    out.save(p, optimize=True)
    print("wrote", p, out.size)


if __name__ == "__main__":
    main()
