"""Contact sheet for a Directorate vehicle (M3-ART-VEH1), shown next to the Authority tank/scraper/jeep for scale.
  python3 tools/art/dir_veh_contact.py <id> [--out art/pilot/<id>_contact.png]
1x is true game size; 2x is nearest-neighbour.  Shows: scale row (Authority tank, scraper, jeep + the new unit at 4 facings),
all 16 idle facings at 1x and 2x, every extra state, and the exact-match house remap."""
import argparse, os
import numpy as np
from PIL import Image, ImageDraw
import common as C
import remap as R
from vehicle_contact import Spr, grid, hcat, vcat, BG, FG


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--out", default=None)
    a = ap.parse_args()
    me = Spr(a.id)
    ref = {v: Spr(v) for v in ("auth_tank", "auth_scraper", "auth_jeep")}
    blocks = []
    # scale row: ground-aligned by origin so sizes compare honestly
    row = []
    for f in (2, 6):
        for v, s in list(ref.items()) + [(a.id, me)]:
            row.append((s.frame("idle", f), s.ox, s.oy))
    W = sum(r[0].width for r in row) + 8 * len(row)
    H = max(r[0].height for r in row)
    base_y = max(r[2] for r in row)
    sc = Image.new("RGB", (W, base_y + max(r[0].height - r[2] for r in row) + 4), (74, 66, 58))
    x = 0
    for im, ox, oy in row:
        sc.paste(im, (x, base_y - oy), im)
        x += im.width + 8
    blocks.append(("SCALE at 1x, origins aligned: Authority tank | scraper | jeep | NEW (facing 2), then the same at facing 6", sc))
    idle16 = [me.frame("idle", f) for f in range(16)]
    blocks.append((f"{a.id} idle, 16 facings at 1x (frame 0 = N, clockwise 22.5 deg), {me.fw} px frames", grid(idle16, 16, 1)))
    blocks.append(("idle, 16 facings at 2x", grid(idle16, 8, 2)))
    for name, (nf, fr, first) in me.states.items():
        if name == "idle":
            continue
        if fr > 1:
            blocks.append((f"state '{name}' at 2x: facings 4 (E) and 12 (W) x frames 0-{fr - 1} (one facing = {fr} frames)",
                           grid([me.frame(name, f, k) for f in (4, 12) for k in range(fr)], 8, 2)))
            blocks.append((f"state '{name}' at 1x true size: facings 0,2,4..14 x frames 0-{fr - 1}",
                           grid([me.frame(name, f, k) for f in range(0, 16, 2) for k in range(fr)], 16, 1)))
        else:
            blocks.append((f"state '{name}' at 1x, 16 facings", grid([me.frame(name, f) for f in range(nf)], 16, 1)))
            blocks.append((f"state '{name}' at 2x, 16 facings", grid([me.frame(name, f) for f in range(nf)], 8, 2)))
    if "turret" in me.states:
        comp = []
        for hf, tf in ((0, 0), (0, 4), (4, 12), (8, 2), (12, 12), (3, 9), (6, 6), (10, 15)):
            im = me.frame("hull", hf).copy()
            im.alpha_composite(me.frame("turret", tf))
            comp.append(im)
        blocks.append(("hull + turret composite at 2x (hull facing, turret facing): (0,0) (0,4) (4,12) (8,2) (12,12) (3,9) (6,6) (10,15)", grid(comp, 8, 2)))
    # remap
    R.TOL = 0
    ramp_set = {tuple(c) for c in C.HOUSE}
    st = "idle"
    base = me.frame(st, 2)
    vs = [base]
    for n, rp in R.HOUSES.items():
        out, _c = R.remap(base, rp)
        a0, a1 = np.asarray(base).reshape(-1, 4), np.asarray(out).reshape(-1, 4)
        ch = (a0 != a1).any(axis=1)
        assert all(tuple(p[:3]) in ramp_set for p in a0[ch]), "remap touched a non-ramp pixel"
        print(a.id, n, "house pixels changed:", int(ch.sum()))
        vs.append(out)
    rm = grid(vs, 4, 2, 4)
    lab, sc0 = blocks[0]
    blocks[0] = (lab + "   ||   RIGHT: house remap at 2x, facing 2: magenta | Authority green | Directorate rust | blue ally", hcat([sc0, rm], 30))
    Wd = max(b[1].width for b in blocks) + 20
    out = Image.new("RGB", (Wd, 10 + sum(b[1].height + 26 for b in blocks)), BG)
    dr = ImageDraw.Draw(out)
    y = 10
    for label, im in blocks:
        dr.text((10, y), label, fill=FG)
        out.paste(im, (10, y + 16))
        y += im.height + 26
    path = a.out or os.path.join(C.PILOT, a.id + "_contact.png")
    out.save(path, optimize=True)
    print("wrote", path, out.size)


if __name__ == "__main__":
    main()
