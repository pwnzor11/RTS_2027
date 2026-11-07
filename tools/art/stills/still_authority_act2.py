"""Act-2 opener still for the Authority campaign (auth_02 The Junction): dawn, Rust Belt road junction.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_authority_act2.py [game|poster|mix|both|all]
Writes scratch/stills/authority_act2_{A_game,B_poster,C_mix}.png (1280x720).
"both" = game+poster; "all" = game+poster+mix.  Original scene, no text in the image.
Build the scene twice (once per style) from the same code, so camera and layout are identical.
"""
import math
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


def mix_extras():
    """Layers added only to the 'mix' still: foreground clutter, detail props, wires, grass, far skyline, fire."""
    R = random.Random(9001)

    def road_avoid(x, y):
        return (abs(x) < 4.4 and y > -22) or abs(y) < 4.4

    # background silhouettes: skyline then hills (the haze lightens and blues them)
    K.skyline(R, 190, n=11, xr=(-150, 150), h=(8, 26), tint="#3E4262")
    K.skyline(R, 120, n=6, xr=(-90, 90), h=(6, 15), tint="#4A4560")
    K.ridge(430, R, n=9, h=(28, 60), tint="#4A4A72")
    # midground detail
    K.burnt_car((-8.2, -4.0), yaw=0.55, rng=R)
    K.crate_stack((-5.4, -1.6), yaw=0.3, rng=R)
    K.drum_cluster((-6.4, 2.6), rng=R, n=4)
    K.crate_stack((9.6, 12.5), yaw=1.1, rng=R)
    K.drum_cluster((10.8, 14.5), rng=R, n=3)
    K.crate((-10.2, 3.0, 0), 0.8, 0.4, "#5A5C3E")
    K.broken_fence((5.6, -1.5), (5.2, -12.0), rng=R)
    K.broken_fence((-14, 22), (-24, 24), rng=R)
    K.road_sign((-4.6, 14.0), yaw=0.2)
    K.road_sign((4.9, 40.0), yaw=-0.3, lean=-0.1)
    # foreground layer (close to the lens): rubble left, shell crater right, debris and dust
    K.rubble_pile((-4.7, -9.6), r=1.5, n=16, rng=R, h=0.9)
    K.rubble_pile((-6.3, -7.0), r=1.0, n=8, rng=R, h=0.5)
    K.crater((3.0, -8.2), R=2.4, rng=R, rim=0.3)
    K.debris_field(R, n=90, xr=(-24, 24), yr=(-14, 48))
    K.puddle((2.2, -2.3), 1.5, 0.7, 0.1)
    K.puddle((-1.8, -5.4), 1.1, 0.5, -0.2)
    K.puddle((-2.4, 12.0), 2.2, 1.0, 0.05)
    K.puddle((1.6, 24.0), 2.6, 1.0, 0.0)
    K.tire_tracks([(4.4, 6), (4.7, 12), (5.0, 18), (5.2, 24), (5.4, 30)])
    K.tire_tracks([(-4.5, 4), (-4.9, -2), (-5.3, -8)], col="#352B20")
    K.tire_tracks([(0.4, -18), (0.2, -10), (0.0, -2)], gap=1.9, width=0.28, col="#26241F", z=0.052)
    # power line along the north road
    pl = [(-9, 22), (-9, 45), (-9, 70), (-9, 98), (-9, 130)]
    for p in pl:
        if p != (-9, 70):
            K.pylon(p, 12)
    K.power_line(pl, 12, sag=1.6)
    # grass and weeds
    K.tufts(R, n=1500, xr=(-50, 50), yr=(-16, 90), avoid=road_avoid)
    K.tufts(R, n=350, xr=(-9, 9), yr=(-14, 9), avoid=road_avoid, scale=1.2)
    # fires at the smoke bases
    K.fire((75, 128), size=8.0, rng=R)
    K.fire((52, 93), size=4.5, rng=R)


def build(style):
    K.clear()
    K.STYLE = style
    rng = random.Random(2027)
    poster = style == "poster"
    mix = style == "mix"

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

    # light: low dawn sun from the east
    if mix:
        az, el = math.radians(58.0), math.radians(7.0)      # well off the right edge (side-lit, rim light), 7 deg up
        sunv = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
        K.sky_dawn(sunv, strength=float(os.environ.get("STILL_SKY", "1.05")))
        key = K.sun((0, 0, 0), float(os.environ.get("STILL_SUN", "10.0")), colour=(1.0, 0.60, 0.34), angle=1.2)
        key.rotation_euler = (-sunv).to_track_quat("-Z", "Y").to_euler()
        K.area_light((-34, -30, 34), (0, 15, 2), 60, 16000.0, (0.50, 0.56, 1.0))     # cool blue-violet fill
        K.haze(float(os.environ.get("STILL_HAZE", "0.0013")), top=40.0)
        mix_extras()
    else:
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
    preview = bool(os.environ.get("STILL_PREVIEW"))
    name = {"game": "authority_act2_A_game.png", "poster": "authority_act2_B_poster.png",
            "mix": "authority_act2_C_mix.png"}[style]
    if preview:
        name = "preview_" + name
    raw = os.path.join(OUT, "raw_" + name)
    final = os.path.join(OUT, name)
    build(style)
    res = (640, 360) if preview else (1280, 720)
    if style == "mix":
        samples = int(os.environ.get("STILL_CYCLES_SAMPLES", "24" if preview else "96"))
    K.setup_render(raw, style, samples=samples, res=res)
    t = time.time()
    bpy.ops.render.render(write_still=True)
    dt = time.time() - t
    from PIL import Image
    im = Image.open(raw).convert("RGB")
    if style == "game":
        # slight pixel-art feel: 2x down (box), nearest back up
        im = im.resize((640, 360), Image.BOX).resize((1280, 720), Image.NEAREST)
    elif style == "mix":
        im = K.finish_mix(im)  # unpixelated so the thin outlines stay crisp
    else:
        # limited palette, hard bands, no dither
        from PIL import ImageEnhance
        im = ImageEnhance.Color(im).enhance(1.5)
        im = ImageEnhance.Contrast(im).enhance(1.15)
        im = im.quantize(colors=18, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    im.save(final)
    if style == "mix" and not preview:
        # the shipped campaign still (plain PNG, optimised, kept under 2.5 MB)
        out = os.path.join(ROOT, "data", "images", "stills", "still_authority_act2.png")
        os.makedirs(os.path.dirname(out), exist_ok=True)
        im.save(out, optimize=True)
        if os.path.getsize(out) > 2_400_000:
            im.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG).save(out, optimize=True)
    os.remove(raw)
    print(f"RENDER {style}: {dt:.0f}s -> {final}")
    return final


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    which = sys.argv[1] if len(sys.argv) > 1 else "both"
    samples = int(os.environ.get("STILL_SAMPLES", "32"))
    for st in {"both": ("game", "poster"), "all": ("game", "poster", "mix")}.get(which, (which,)):
        render(st, samples)
