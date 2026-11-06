"""Contact sheet for the art-exp-b assets: python3 tools/art/vehicles/exp_contact_art_exp_b.py <out.png> <id>... [--facings ...] [--water id,id]
One block per id: row on sand (team 1) or water (boats, no spray ring), second team colour, at 2x; then a 1x strip with the Glide Tank + Authority tank."""
import argparse, os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE); sys.path.insert(0, os.path.dirname(HERE))
import exp_contact as X
import remap as R
import veh_contact as VC

BOATS = ("exp_launch", "exp_landing_craft", "exp_supply_tender")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("ids", nargs="+")
    ap.add_argument("--facings", type=int, nargs="*", default=[0, 3, 4, 8, 11, 13])
    ap.add_argument("--rowh", type=int, default=210)
    a = ap.parse_args()
    z, slot = 2, 260
    rows, units = [], []
    for i, uid in enumerate(a.ids):
        u = X.unit(uid); units.append(u)
        boat = uid in BOATS
        plan = [("water", "Lime"), ("water", "Crimson")] if boat else [("sand", "Crimson"), ("water", "Lime")]
        for j, (g, t) in enumerate(plan):
            lab = "A"
            if g == "water" and not boat:
                lab = "A"
            if boat:
                X.spray = lambda *args, **kw: None
            im = X.row(u, g, t, a.facings, z, slot, a.rowh, 10 + i * 3 + j, lab)
            ImageDraw.Draw(im).text((6, 4), f"{uid} | {g} | {t}", fill=(255, 255, 255))
            rows.append(im)
    W = rows[0].width
    strip = Image.new("RGB", (W, 130), (74, 66, 58))
    ImageDraw.Draw(strip).text((6, 4), "1x true size (facing 2, 6): " + ", ".join(a.ids) + ", Glide Tank, Authority tank", fill=(255, 255, 255))
    x = 10
    for u in units + [X.unit("exp_glide_tank"), VC.Unit("auth_tank")]:
        for f in (2, 6):
            fr = u.comp(f); fr = fr.crop(fr.getchannel("A").getbbox())
            if x + fr.width > W: break
            strip.paste(fr, (x, 20 + (100 - fr.height) // 2), fr); x += fr.width + 10
        x += 20
    out = Image.new("RGB", (W, sum(r.height for r in rows) + strip.height), (30, 28, 34))
    y = 0
    for r in rows + [strip]:
        out.paste(r, (0, y)); y += r.height
    out.save(a.out); print("wrote", a.out, out.size)


if __name__ == "__main__":
    main()
