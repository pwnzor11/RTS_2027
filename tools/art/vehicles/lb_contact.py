"""Left Behind unit contact sheet: python3 tools/art/vehicles/lb_contact.py <out.png> [--veh id ...] [--inf id ...]
1x row (facings 2 and 6 of each vehicle, idle facings 2/6 of each soldier) with one Directorate unit for scale on each row, then a 2x row.
Vehicles composite hull + turret + air shadow like tools/art/veh_contact.py."""
import argparse, os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(HERE)
sys.path.insert(0, ART)
import common as C
import veh_contact as VC
from contact_sheet import ash_patch

INF = os.path.join(C.ROOT, "data", "sprites", "infantry")


def inf_frame(sid, state_first, facing):
    im = Image.open(os.path.join(INF, sid + ".png")).convert("RGBA")
    i = state_first + facing
    return im.crop(((i % 6) * 32, (i // 6) * 32, (i % 6) * 32 + 32, (i // 6) * 32 + 32))


def trim(im, pad=1):
    bb = im.getchannel("A").getbbox()
    return im.crop((max(bb[0] - pad, 0), max(bb[1] - pad, 0), min(bb[2] + pad, im.width), min(bb[3] + pad, im.height)))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--veh", nargs="*", default=[])
    ap.add_argument("--inf", nargs="*", default=[])
    ap.add_argument("--ref-veh", default="dir_rocket_truck")
    ap.add_argument("--ref-inf", default="dir_conscript")
    a = ap.parse_args()
    items = []                                         # (label, [images])
    for v in [a.ref_veh] + a.veh:
        u = VC.Unit(v)
        items.append((v, [trim(u.comp(f)) for f in (2, 6)]))
    for s in [a.ref_inf] + a.inf:
        items.append((s, [trim(inf_frame(s, 0, f)) for f in (2, 6)]))
    W = sum(max(i.width for i in ims) + 14 for _l, ims in items if ims) + 20
    H1 = max(sum(i.height + 6 for i in ims) for _l, ims in items) + 24
    rows = []
    for z in (1, 2):
        h = max(sum(i.height * z + 6 for i in ims) for _l, ims in items) + 24
        w = sum(max(i.width * z for i in ims) + 14 for _l, ims in items) + 20
        img = ash_patch(64, 2027 + z).crop((0, 0, 1, 1)).resize((w, h)) if False else Image.new("RGB", (w, h), (74, 66, 58))
        d = ImageDraw.Draw(img)
        x = 10
        for lab, ims in items:
            cw = max(i.width * z for i in ims)
            y = 16
            d.text((x, 2), lab, fill=(230, 220, 200))
            for im in ims:
                im2 = im.resize((im.width * z, im.height * z), Image.NEAREST) if z > 1 else im
                img.paste(im2, (x + (cw - im2.width) // 2, y), im2)
                y += im2.height + 6
            x += cw + 14
        rows.append(img)
    out = Image.new("RGB", (max(r.width for r in rows), sum(r.height for r in rows) + 8), (30, 28, 34))
    y = 0
    for r in rows:
        out.paste(r, (0, y))
        y += r.height + 8
    out.save(a.out)
    print("wrote", a.out, out.size)


if __name__ == "__main__":
    main()
