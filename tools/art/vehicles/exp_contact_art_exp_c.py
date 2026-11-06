"""Ship contact sheet (package art-exp-c): python3 tools/art/vehicles/exp_contact_art_exp_c.py <out.png> <id> [<id> ...] [--facings ...]
One row per ship id: water, team Crimson, then water, team Lime (2x), then a 1x strip next to the Glide Tank and the Authority tank."""
import argparse, os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import exp_contact as X
import veh_contact as VC
import remap as R


SCALE = {"exp_command_ship": 1800, "exp_cruiser": 2000, "exp_carrier": 2400}   # draw_scale_permille from data/rules/vehicles.toml


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("ids", nargs="+")
    ap.add_argument("--facings", type=int, nargs="*", default=[2, 6, 10, 14])
    ap.add_argument("--rowh", type=int, default=230)
    a = ap.parse_args()
    X.spray = lambda *args, **kw: None                               # no hover spray ring for ships
    z = 2
    units = [X.unit(i) for i in a.ids]
    teams = ("Crimson", "Lime")
    frames, bbs = {}, {}
    for n, u in enumerate(units):
        for i, f in enumerate(a.facings):
            fr = X.R.remap(u.comp(f), X.TEAMS[teams[i % 2]])[0]      # alternate the two team colours along the row
            frames[(n, f)] = fr.crop(fr.getchannel("A").getbbox())
    slot = max(fr.width for fr in frames.values()) * z + 24
    rows = []
    for n, u in enumerate(units):
        bh = max(frames[(n, f)].height for f in a.facings) * z + 24
        im = X.water(slot * len(a.facings), bh, 20 + n)
        for i, f in enumerate(a.facings):
            fr = frames[(n, f)]
            big = fr.resize((fr.width * z, fr.height * z), Image.NEAREST)
            im.paste(big, (i * slot + (slot - big.width) // 2, (bh - big.height) // 2), big)
        ImageDraw.Draw(im).text((6, 4), f"{a.ids[n]} | water | facings {a.facings} | team Crimson / Lime alternating", fill=(255, 255, 255))
        rows.append(im)
    W = rows[0].width
    strip = Image.new("RGB", (max(W, 1000), 190), (40, 100, 130))
    ImageDraw.Draw(strip).text((6, 4), "1x IN-GAME size (draw_scale applied, nearest-neighbour): ships facing 2, then Glide Tank, Authority tank", fill=(255, 255, 255))
    x = 10
    for u_id, u in list(zip(a.ids, units)) + [("exp_glide_tank", X.unit("exp_glide_tank")), ("auth_tank", VC.Unit("auth_tank"))]:
        fr = u.comp(2)
        fr = fr.crop(fr.getchannel("A").getbbox())
        ds = SCALE.get(u_id, 1000) / 1000
        if ds != 1:
            fr = fr.resize((round(fr.width * ds), round(fr.height * ds)), Image.NEAREST)
        strip.paste(fr, (x, 24 + (160 - fr.height) // 2), fr)
        x += fr.width + 12
    W = strip.width
    out = Image.new("RGB", (W, sum(r.height for r in rows) + strip.height), (40, 100, 130))
    y = 0
    for r in rows + [strip]:
        out.paste(r, (0, y)); y += r.height
    out.save(a.out); print("wrote", a.out, out.size)


if __name__ == "__main__":
    main()
