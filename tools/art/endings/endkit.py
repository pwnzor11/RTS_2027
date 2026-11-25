"""Shared harness for the campaign ending slide pictures (M16-C-01).  Sits on the act-still kits in tools/art/stills (import only).

Usage from an ending script:  sys.path.insert(0, STILLS); import endkit as EK;  EK.run(build, "ending_expedition_01", finish=False)
STILL_PREVIEW=1 -> 640x360 into scratch/endings_prev/<id>.png; otherwise 1280x720 into data/images/endings/<id>.png.
finish=True applies still_kit.finish_mix (the authority / left_behind act scripts do; the expedition / directorate ones do not).
"""
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
STILLS = os.path.abspath(os.path.join(HERE, "..", "stills"))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
for p in (STILLS, HERE):
    if p not in sys.path:
        sys.path.insert(0, p)
import bpy  # noqa: E402
import still_kit as K  # noqa: E402
from mathutils import Vector  # noqa: E402


def sunvec(az_deg, el_deg):
    az, el = math.radians(az_deg), math.radians(el_deg)
    return Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))


def aim_sun(energy, sunv, colour, angle=1.4):
    key = K.sun((0, 0, 0), energy, colour=colour, angle=angle)
    key.rotation_euler = (-sunv).to_track_quat("-Z", "Y").to_euler()
    return key


def run(build, still_id, finish=False):
    from PIL import Image
    preview = bool(os.environ.get("STILL_PREVIEW"))
    outdir = os.path.join(ROOT, "scratch", "endings_prev") if preview else os.path.join(ROOT, "data", "images", "endings")
    os.makedirs(outdir, exist_ok=True)
    os.makedirs(os.path.join(ROOT, "scratch", "raw"), exist_ok=True)
    raw = os.path.join(ROOT, "scratch", "raw", "raw_" + still_id + ".png")
    final = os.path.join(outdir, still_id + ".png")
    K.clear()
    K.STYLE = "mix"
    build()
    res = (640, 360) if preview else (1280, 720)
    samples = int(os.environ.get("STILL_CYCLES_SAMPLES", "24" if preview else "96"))
    K.setup_render(raw, "mix", samples=samples, res=res)
    t = time.time()
    bpy.ops.render.render(write_still=True)
    dt = time.time() - t
    im = Image.open(raw).convert("RGB")
    if finish:
        im = K.finish_mix(im)
    im.save(final, optimize=True)
    if not preview and os.path.getsize(final) > 2_400_000:
        im.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG).convert("RGB").save(final, optimize=True)
    os.remove(raw)
    print(f"RENDER {still_id}: {dt:.0f}s -> {final} ({os.path.getsize(final) // 1024} KB)")
    return final


def closed_hatch(loc, yaw=0.0, s=1.0):
    """A shut Directorate hatch (no glow, no beam): concrete collar, flat steel lid with a hazard band, four hazard blocks."""
    x, y = loc
    K.cyl("collar", (x, y, 0.35 * s), 2.6 * s, 0.7 * s, K.mat("dust", "#7B776B"), verts=14)
    K.cyl("lid", (x, y, 0.85 * s), 2.1 * s, 0.3 * s, K.mat("steel", "#555B61"), verts=14)
    K.box("lidband", (x, y, 1.05 * s), (3.4 * s, 0.5 * s, 0.1), K.mat("yellow", "#C9A640"), rot=(0, 0, -yaw))
    for i in range(4):
        a = i * math.tau / 4
        K.box("hazard", (x + math.cos(a) * 2.3 * s, y + math.sin(a) * 2.3 * s, 0.5 * s), (0.5, 0.3, 0.3), K.mat("yellow", "#C9A640"), rot=(0, 0, a))
