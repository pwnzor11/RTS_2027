"""Act-4 opener still for the Expedition (exp_04 The Fuel): red dusk on the Ash Reckoning.

Usage:  flock -o /home/claude/wt/blender.lock python3 tools/art/stills/still_expedition_act4.py
        (STILL_PREVIEW=1 for a 640x360 look).  Writes data/images/stills/still_expedition_act4.png.
Scene: the last shipment lands on the south shore.  The Command Ship's bow is run up on the beach with its ramp down,
crews walk onto the grey ash field, a Dredge Works rises on the plain with survey hovers skimming ahead, and far inland
the Directorate redoubt burns under an ash-choked red sky (gate, Shaft headframe, Sunrise vent glowing).
Low camera, falling ash, hard red side light.  Original scene, no text.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import still_expedition_kit as E  # noqa: E402
import still_kit as K  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

DARK, DARK_L, MAROON = "#2E2A2C", "#4A4446", "#6B2A2A"


def redoubt(R):
    """Far inland Directorate redoubt on a ridge: dark hills, a walled gate, the Shaft headframe, the Sunrise vent."""
    for x, y, r, h in ((-90, 235, 55, 30), (-20, 250, 60, 36), (60, 240, 55, 32), (130, 225, 50, 24), (-150, 220, 45, 22)):
        K.cyl("rhill", (x, y, h / 2 - 1), r, h, E.m("#3A3436", 1.0), verts=8, r2=r * 0.35)
    # curtain wall and gate
    for x0, x1 in ((-46, -9), (9, 46)):
        K.box("wall", ((x0 + x1) / 2, 200, 6.0), (x1 - x0, 3.0, 12.0), E.m(DARK_L, 0.95))
        for k in range(int((x1 - x0) / 6)):
            K.box("merlon", (x0 + 3 + k * 6, 200, 12.5), (3.0, 3.4, 1.4), E.m(DARK_L, 0.95))
    K.box("gate", (0, 200.5, 5.0), (18, 3.4, 10.0), E.m("#120E0E", 0.9))
    for x in (-10.5, 10.5):
        K.box("gatetower", (x, 200, 11.0), (4.5, 5.0, 22.0), E.m(DARK, 0.95))
        K.box("tstripe", (x, 197.4, 15.0), (4.7, 0.2, 1.0), E.m(MAROON, 0.8))
        K.box("tlamp", (x, 197.3, 19.0), (1.2, 0.2, 1.0), E.m("#FFB060", 0.3, emit="#FF8A30", estr=12.0))
    # Shaft headframe: A-frame tower with a wheel
    sx, sy = -34, 214
    for s in (-1, 1):
        K.cyl("leg", (sx + s * 3.0, sy, 11), 0.5, 24, E.m(DARK, 0.9), rot=(0, s * 0.14, 0), verts=6)
    K.box("wheel", (sx, sy - 0.3, 22.5), (4.2, 0.8, 4.2), E.m(MAROON, 0.8))
    K.box("shedshaft", (sx, sy - 6, 2.5), (10, 8, 5), E.m(DARK_L, 0.95))
    # Sunrise Device: concrete collar, glowing vent and a light shaft
    dx, dy = 30, 214
    K.cyl("collar", (dx, dy, 2.5), 8, 5, E.m(DARK_L, 0.95), verts=14)
    K.cyl("vent", (dx, dy, 5.2), 5.4, 0.6, E.m("#FFB060", 0.3, emit="#FF7A20", estr=26.0), verts=14)
    K.cyl("beam", (dx, dy, 40), 2.4, 70, E.m("#FFB060", 0.3, emit="#FF6A18", estr=2.5), verts=10, r2=0.4)
    K.point_light((dx, dy - 6, 8), 9.0e5, (1.0, 0.45, 0.16), 6.0)
    for k in range(5):
        a = k * 1.26
        K.box("cradle", (dx + 6.8 * math.cos(a), dy + 6.8 * math.sin(a), 6.0), (1.0, 1.0, 3.5), E.m(MAROON, 0.8))
    # pillboxes and rocket nests ringing the approach
    for x, y in ((-26, 170), (-8, 176), (14, 172), (32, 168), (-44, 160), (50, 178), (4, 160)):
        K.box("pill", (x, y, 1.2), (4.4, 3.8, 2.4), E.m(DARK_L, 0.95))
        K.box("slit", (x, y - 1.95, 1.5), (2.4, 0.1, 0.35), E.m("#FFB060", 0.3, emit="#FF8A30", estr=7.0))
    for x, y in ((-18, 186), (22, 184), (-38, 178)):
        K.box("nest", (x, y, 1.0), (3.0, 3.0, 2.0), E.m(DARK, 0.95))
        for k in range(4):
            K.cyl("rocket", (x - 1.0 + k * 0.7, y - 0.2, 2.6), 0.18, 2.0, E.m("#8C3A2A", 0.7), rot=(0.9, 0, 0), verts=6)
    K.fire((-26, 190), size=6.0, rng=R)
    K.fire((22, 188), size=5.0, rng=R)
    K.fire((60, 216), size=5.0, rng=R)
    K.fire((-60, 226), size=6.0, rng=R)
    K.smoke_column((-6, 208), h=80, r0=4.0, n=14, rng=R)
    K.smoke_column((60, 218), h=60, r0=3.2, n=12, rng=R)
    K.smoke_column((-60, 228), h=70, r0=3.6, n=12, rng=R)


def build():
    R = random.Random(4404)
    E.ground_with(E.ash_mat())
    # the inlet the Command Ship sails in by (sea to the south and east of the landing)
    mt = E.sea_mat(shore_y=4.0, dir_y=-1.0, width=110, shallow="#3E8E92", deep="#0A1E30")
    bpy.ops.mesh.primitive_plane_add(size=1, location=(160, -150, 0.05))
    o = bpy.context.active_object
    o.scale = (314, 308, 1)
    o.data.materials.append(mt)
    K.nolines(o)
    shore = [(3.0 + 0.8 * math.sin(i * 1.4), -80 + i * 12) for i in range(8)]
    E.wet_sand([(x - 2.0, y) for x, y in shore], width=4.0)
    E.foam_line([(x, y) for x, y in shore], width=1.2, rng=R)
    E.foam_line([(x, y) for x, y in zip((3, 3, 3, 3), (-10, 2, 4, 4))], width=1.0, rng=R)
    # far ships riding in the bay behind the Command Ship
    E.destroyer((60, -60), 0.25, 1.2)
    E.tender((105, -20), -0.3, 1.1)
    E.carrier((170, -120), 0.15, 1.2, wake_on=False, jets=3)

    # foreground right: the Command Ship's bow run up on the shore, ramp down
    E.command_ship((15.5, -28), 0.0, 0.85, wake_on=False, bow_down=True)
    # foreground left: beached landing craft and crew filing onto the ash
    E.landing_craft((-17, -1), 0.25, 1.15, ramp_down=True, rng=R)
    for i, (x, y) in enumerate(((-12.5, 8.0), (-10.0, 12.0), (-13.5, 12.5), (-6.0, 5.0), (-3.0, 9.0), (-8.0, 17.0))):
        E.xfigure((x, y), 0.15 + 0.05 * i, kneel=(i == 2), hold="crate" if i % 3 == 1 else "rifle")
    E.crate_stack_x((-1.5, 2.0), 0.2, rng=R, n=5)
    E.crate_stack_x((-24, 8.0), -0.3, rng=R, n=4)
    K.barrel_drum((-9, -1.5), E.m("#C26A1E"))
    K.barrel_drum((-8.2, -1.8), E.m("#C26A1E"))
    K.rubble_pile((-5.0, -1.2), r=1.2, n=12, rng=R, h=0.6)
    E.tent((-27, 20), 0.3, 1.1, col="#BDB8AA")

    # midground: Dredge Works going up on the ash, survey hovers skimming ahead
    E.dredge_works((-16, 70), 0.28, 1.35)
    E.hover_skiff((9, 33), 0.15, 1.2, z=0.4)
    E.hover_skiff((-17, 44), -0.1, 1.0, z=0.4)
    E.hover_skiff((20, 52), 0.3, 0.9, z=0.4)
    E.depot((46, 70), 0.3, 1.1)
    # cooling ash heaps, old craters, drifts
    K.crater((6.0, 15.0), R=2.6, rng=R, rim=0.3)
    K.crater((-24, 40), R=3.2, rng=R, rim=0.3)
    K.debris_field(R, n=90, xr=(-30, 30), yr=(-4, 60))
    for k in range(7):
        x, y = R.uniform(-45, 55), R.uniform(10, 120)
        o = K.cyl("drift", (x, y, 0.5), R.uniform(5, 12), 1.4, E.m("#6F6860", 1.0), verts=9, r2=R.uniform(1, 3))
        K.nolines(o)
    E.ash_flakes(R, n=260, xr=(-26, 26), yr=(-26, 60), zr=(0.4, 14), size=0.03)
    E.glass_shards(R, n=6, xr=(30, 80), yr=(40, 100), h=(2, 6), tint="#6A8C88")
    redoubt(R)

    # sky: blood-red dusk choked with ash
    az, el = math.radians(-62.0), math.radians(4.0)
    sunv = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
    E.sky_custom(sunv, [(0.0, "#FF8040"), (0.05, "#EE5A3E"), (0.14, "#AE3E5C"), (0.35, "#52306E"), (1.0, "#150F36")],
                 glow="#FFB070", cloud_stops=[(0.04, "#FF9A58"), (0.2, "#B8445A"), (0.45, "#4C2E5C")],
                 strength=1.0, cloud_cov=(0.33, 0.50), cloud_gain=1.8, glow_gain=1.6, cloud_band=(0.0, 0.2, 0.65, 0.95))
    key = K.sun((0, 0, 0), float(os.environ.get("STILL_SUN", "10.0")), colour=(1.0, 0.42, 0.20), angle=1.2)
    key.rotation_euler = (-sunv).to_track_quat("-Z", "Y").to_euler()
    K.area_light((34, -30, 34), (0, 15, 2), 60, 14000.0, (0.50, 0.50, 1.0))
    K.haze(float(os.environ.get("STILL_HAZE", "0.0016")), top=50.0, colour="#E0A89C")
    K.camera((-3.0, -30.0, 3.0), (8.0, 70.0, 6.0), lens=24)


if __name__ == "__main__":
    E.run(build, "still_expedition_act4")
