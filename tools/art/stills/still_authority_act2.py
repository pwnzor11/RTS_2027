"""Act-2 opener still for the Authority campaign (auth_02 The Junction): dawn, Rust Belt road junction.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_authority_act2.py [game|poster|both]
Writes scratch/stills/authority_act2_{A_game,B_poster}.png (1280x720).  Original scene, no text in the image.
Build the scene twice (once per style) from the same code, so camera and layout are identical.
"""
import os
import random
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import still_kit as K  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(ROOT, "scratch", "stills")


def build(style):
    K.clear()
    K.STYLE = style
    rng = random.Random(2027)
    poster = style == "poster"

    K.ground()
    K.dirt_patches(rng)
    K.junction((0, 0), 7)

    # ruined shells flanking the junction, and a pylon line along the east road
    K.ruin((-17, 11), 9, 4, 6.5, rng)
    K.ruin((15, 22), 8, 4, 5, rng, rot=0.2)
    K.ruin((-30, 38), 12, 5, 9, rng)
    K.ruin((24, 60), 14, 5, 7, rng)
    for i, px in enumerate((24, 52, 80)):
        K.pylon((px, -6 - i * 0.5), 13 - i)
    K.pylon((-9, 70), 12)

    # defences: two turrets at the north-east and north-west corners, facing east/north, sandbag arcs
    K.turret((5.8, 32.0), yaw=1.0)
    K.turret((-9.0, 9.0), yaw=0.5)
    K.sandbag_wall((8.6, 29.0), (8.6, 36.0), rows=3, rng=rng)
    K.sandbag_wall((-12.5, 5.0), (-12.5, 13.0), rows=3, rng=rng)
    K.sandbag_wall((5.0, 17.5), (11, 17.5), rows=2, rng=rng)

    # tank on the road, rifle figures at the bags and by the camera
    K.tank((5.8, 3.0), yaw=0.8)
    for p, yw, kn in (((7.6, 31.0), 1.3, True), ((7.8, 34.0), 1.2, False), ((-11.0, 8.0), 0.6, True),
                      ((6.0, 16.0), 1.0, True), ((-5.2, 3.0), 0.5, False), ((-9.5, 3.0), 0.6, True)):
        K.figure(p, yw, kn)
    for p in ((10.5, -2.6), (11.3, -2.0), (-12, -4)):
        K.barrel_drum(p)

    # relief column on the north road, far away, with its dust
    K.column_trucks(start_y=62, n=5, x=0.0, gap=8)
    K.dust_cloud((0, 150), n=30, length=75, rng=rng, tint="#F0DDB0" if poster else "#CDAE7E")
    # smoke from the east
    K.smoke_column((75, 130), h=60, r0=2.6, n=14, rng=rng)
    K.smoke_column((52, 95), h=32, r0=1.6, n=9, rng=rng)

    # light: low dawn sun from the east, slightly behind the scene so it rakes across the camera's right
    if poster:
        K.sky_world("#4C5F66", "#E8B070", "#F2C98A", strength=1.0)
    else:
        K.sky_world("#1E3A55", "#F0B070", "#F6C890", strength=1.0)
    s = K.sun((0, 0, 0), 4.0 if not poster else 3.0, colour=(1.0, 0.70, 0.46), angle=0.3 if poster else 2.5)
    d = Vector((-0.80, -0.25, -0.30)).normalized()
    s.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    if not poster:
        # cool sky fill so shadows are not black
        f = K.sun((0, 0, 0), 0.6, colour=(0.55, 0.65, 0.85), angle=20)
        f.rotation_euler = Vector((0.4, 0.5, -0.8)).normalized().to_track_quat("-Z", "Y").to_euler()
        K.fog(0.0011)
    K.camera((-1.8, -15.0, 1.35), (1.0, 40.0, 5.0), lens=26)


def render(style, samples):
    name = {"game": "authority_act2_A_game.png", "poster": "authority_act2_B_poster.png"}[style]
    raw = os.path.join(OUT, "raw_" + name)
    final = os.path.join(OUT, name)
    build(style)
    K.setup_render(raw, style, samples=samples)
    t = time.time()
    bpy.ops.render.render(write_still=True)
    dt = time.time() - t
    from PIL import Image
    im = Image.open(raw).convert("RGB")
    if style == "game":
        # slight pixel-art feel: 2x down (box), nearest back up
        im = im.resize((640, 360), Image.BOX).resize((1280, 720), Image.NEAREST)
    else:
        # limited palette, hard bands, no dither
        from PIL import ImageEnhance
        im = ImageEnhance.Color(im).enhance(1.5)
        im = ImageEnhance.Contrast(im).enhance(1.15)
        im = im.quantize(colors=18, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    im.save(final)
    os.remove(raw)
    print(f"RENDER {style}: {dt:.0f}s -> {final}")
    return final


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    which = sys.argv[1] if len(sys.argv) > 1 else "both"
    samples = int(os.environ.get("STILL_SAMPLES", "32"))
    for st in (("game", "poster") if which == "both" else (which,)):
        render(st, samples)
