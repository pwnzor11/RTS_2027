"""Unit portraits (S24, M10-11, D-243): 96 x 96 RGBA close-ups for the selection card, rendered with the booklet hero hook.

Usage (repo root, with bpy installed, i.e. the .venv-art of D-196):
    python tools/booklet/render_portraits.py --pilot                       # 4 ids: auth_rifleman, auth_tank, dir_conscript, dir_rocket_truck
    python tools/booklet/render_portraits.py --ids auth_rifleman,lb_scavenger
    options: --samples 64  --force  --out data/portraits  --contact <png>  --no-denoise  --threads N  --accent #RRGGBB
On the owner's laptop use tools/art/render_s24_portraits.ps1.  On the shared cloud box wrap the call in
`flock -o /home/claude/wt/blender.lock ...` (the script does not lock by itself).

Contract (read by code package s24-card): data/portraits/<id>.png, 96 x 96, RGBA, transparent background; infantry = head and
shoulders, vehicles and buildings = the whole model, square crop, 3/4 view (the south-west quarter of the booklet heroes); the house
paint is the faction's default accent from tools/booklet/render_manifest.toml (the same colour as the booklet hero), not the magenta ramp.

How it works: render_heroes.py's hook is reused unchanged (same builders, same recolouring, same camera angle and sun, same
transparent film).  Only its camera framing is swapped (render_heroes.frame_camera -> portrait_camera below) and the image is
rendered at 2 x 96 = 192 px, then box-filtered down to 96 px in premultiplied alpha (no dark fringes on the edge).
Infantry framing: the camera is aimed at the top 38 % of the figure (helmet and shoulders) and squared to the larger of the two
extents; vehicles and buildings: the bounding box of the whole model, squared.  Missing portrait = the card is unchanged.
"""
import argparse
import os
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import render_heroes as H  # noqa: E402

OUT_PX = 96
SUPERSAMPLE = 2
PILOT_IDS = ["auth_rifleman", "auth_tank", "dir_conscript", "dir_rocket_truck"]   # one infantry and one vehicle per base faction
BAND = 0.38        # infantry: the top fraction of the figure's height that counts as head and shoulders
PAD = {"infantry": 0.10, "vehicle": 0.07, "building": 0.07}


class Portrait:
    """Kind of the id being rendered (the camera hook reads it)."""
    kind = "vehicle"


# ------------------------------------------------------------------------------------------------ Blender side
def portrait_camera(long_side, pad=None):
    """Replacement for render_heroes.frame_camera: same view direction, square frame of long_side px around the portrait subject."""
    import bpy
    import numpy as np
    from mathutils import Vector
    sc = bpy.context.scene
    bpy.context.view_layer.update()
    cam = sc.camera
    mw = cam.matrix_world
    right, up = np.array(mw.col[0][:3]), np.array(mw.col[1][:3])
    origin = np.array(mw.col[3][:3])
    pts = []
    for o in sc.objects:
        if o.type != "MESH" or o.hide_render or not o.visible_camera or o.name.startswith("ground"):
            continue
        me = o.data
        nv = len(me.vertices)
        if nv == 0:
            continue
        co = np.empty(nv * 3, dtype=np.float32)
        me.vertices.foreach_get("co", co)
        co = co.reshape(nv, 3).astype(np.float64)
        W = np.array([list(r) for r in o.matrix_world])
        pts.append(co @ W[:3, :3].T + W[:3, 3])
    if not pts:
        raise RuntimeError("nothing visible to frame")
    world = np.concatenate(pts)
    if Portrait.kind == "infantry":
        top, bot = world[:, 2].max(), world[:, 2].min()
        world = world[world[:, 2] >= top - BAND * (top - bot)]
    p = world - origin
    xy = np.stack([p @ right, p @ up], axis=1)
    lo, hi = xy.min(axis=0), xy.max(axis=0)
    pad = PAD.get(Portrait.kind, 0.07) if pad is None else pad
    span = float((hi - lo).max()) * (1 + 2 * pad)
    centre = (lo + hi) / 2
    cam.location = cam.location + Vector(tuple(float(v) for v in (right * centre[0] + up * centre[1])))
    cam.data.sensor_fit = "AUTO"
    cam.data.shift_x = cam.data.shift_y = 0.0
    cam.data.ortho_scale = span
    sc.render.resolution_x = sc.render.resolution_y = int(long_side)
    sc.render.resolution_percentage = 100


def downscale(src, dst):
    """192 px render -> 96 px RGBA, box filter in premultiplied alpha."""
    from PIL import Image
    im = Image.open(src).convert("RGBA")
    im = im.convert("RGBa").resize((OUT_PX, OUT_PX), Image.BOX).convert("RGBA")
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    im.save(dst)


def contact_sheet(ids, out_dir, path, cols=8, cell=192):
    from PIL import Image, ImageDraw
    have = [i for i in ids if os.path.exists(os.path.join(out_dir, i + ".png"))]
    if not have:
        return None
    rows = (len(have) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * cell, rows * (cell + 16)), (232, 223, 200))
    d = ImageDraw.Draw(sheet)
    for n, rid in enumerate(have):
        im = Image.open(os.path.join(out_dir, rid + ".png")).convert("RGBA").resize((cell - 16, cell - 16), Image.NEAREST)
        x, y = (n % cols) * cell, (n // cols) * (cell + 16)
        sheet.paste(im, (x + 8, y + 4), im)
        d.text((x + 8, y + cell - 8), rid, fill=(40, 36, 28))
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    sheet.save(path)
    return path, len(have)


# ------------------------------------------------------------------------------------------------ main
def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--pilot", action="store_true", help="4 ids: " + ", ".join(PILOT_IDS))
    g.add_argument("--ids", nargs="+", help="comma separated ids (several allowed)")
    ap.add_argument("--samples", type=int, default=64)
    ap.add_argument("--no-denoise", action="store_true")
    ap.add_argument("--threads", type=int, default=0, help="0 = all cores")
    ap.add_argument("--force", action="store_true", help="re-render ids whose PNG already exists (default: skip them)")
    ap.add_argument("--out", default=os.path.join(H.ROOT, "data", "portraits"))
    ap.add_argument("--contact", default=None, help="write a contact sheet of the rendered portraits to this path")
    ap.add_argument("--accent", default=None, help="override the faction accent for this run, e.g. #E0A020")
    a = ap.parse_args()
    if not (a.pilot or a.ids):
        ap.error("give --pilot or --ids a,b,c")

    accents, heroes = H.load_manifest()
    ids = PILOT_IDS if a.pilot else [s for part in a.ids for s in part.split(",") if s]
    bad = [i for i in ids if i not in heroes]
    if bad:
        sys.exit(f"unknown id(s) (not in render_manifest.toml): {', '.join(bad)}")
    skipped = [i for i in ids if heroes[i]["status"] != "ok"]
    if skipped:
        print(f"SKIPPED (status no_model, nothing to render): {', '.join(skipped)}", flush=True)
    todo = [i for i in ids if heroes[i]["status"] == "ok"]
    dst = {i: os.path.join(a.out, i + ".png") for i in todo}
    existing = [] if a.force else [i for i in todo if os.path.exists(dst[i])]
    if existing:
        print(f"already rendered (use --force to redo): {', '.join(existing)}", flush=True)
    todo = [i for i in todo if i not in existing]
    print(f"{len(todo)} to render, {len(existing)} already done, {len(skipped)} no_model", flush=True)

    failed, t_all = [], time.time()
    if todo:
        import bpy  # noqa: F401  (fails here, early, if the venv has no bpy)
        H.frame_camera = portrait_camera          # hero_render looks the name up in its module at call time
        H.install_hook()
        H.Hero.long_side, H.Hero.samples, H.Hero.denoise, H.Hero.threads = OUT_PX * SUPERSAMPLE, a.samples, not a.no_denoise, a.threads
        tmp = tempfile.mkdtemp(prefix="portrait_")
        for n, rid in enumerate(todo, 1):
            h = heroes[rid]
            Portrait.kind = h["kind"]
            H.Hero.accent = a.accent or accents[h["faction"]]["color"]
            H.Hero.path = os.path.join(tmp, rid + ".png")
            H.Hero.yaw = H.STRUCTURE_YAW.get(rid, 0.0) if h["kind"] == "building" else 0.0
            t0 = time.time()
            print(f"[{n}/{len(todo)}] {rid} ({h['faction']} {h['kind']}, house {H.Hero.accent}) ...", flush=True)
            try:
                H.render_id(rid, h)
                downscale(H.Hero.path, dst[rid])
                print(f"[{n}/{len(todo)}] {rid} done in {time.time() - t0:.1f} s -> {os.path.relpath(dst[rid], H.ROOT)}", flush=True)
            except Exception as e:      # one bad model must not stop the run
                failed.append(rid)
                import traceback
                traceback.print_exc()
                print(f"[{n}/{len(todo)}] {rid} FAILED: {e}", flush=True)
    if a.contact:
        r = contact_sheet(ids, a.out, a.contact)
        if r:
            print(f"contact sheet: {r[0]} ({r[1]} portraits)", flush=True)
    print(f"ELAPSED {time.time() - t_all:.1f} s; rendered {len(todo) - len(failed)}, failed {len(failed)}", flush=True)
    if failed:
        print(f"FAILED: {', '.join(failed)}", flush=True)
        sys.exit(1)


if __name__ == "__main__":
    main()
