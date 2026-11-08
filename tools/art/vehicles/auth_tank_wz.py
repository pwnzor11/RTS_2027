"""Candidate Authority medium tank built from Warzone 2100 parts, for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

NOT a replacement of auth_tank (that sprite stays untouched); this is the art-wz pilot.  Parts (all GPL-2.0-or-later, see docs/CREDITS.md):
  body       components/bodies/drmbod05.pie  + texture page-14-droid-hubs.png (re-graded olive) + page-14_tcmask.png -> magenta house ramp
  tracks     components/prop/prmtrk2.pie     + texture page-16-droid-drives.png (re-graded track grey)
  turret     components/weapons/trhcan.pie (mount) + gnhvcan.pie (barrel): geometry only, painted with our own flat materials,
             because page-17-droid-weapons.png is CC BY-SA 3.0 and not compatible with our GPL-3.0-or-later
Assembly follows the engine: the weapon sits at body connector 0, the propulsion at the model origin.  Axes and scale: tools/art/wz/pie_import.py.
Layers: body (hull + tracks + whole-vehicle shadow) + turret (mount and barrel, rotates about the connector).
"""
import os
import sys

ID = "auth_tank_wz"
TITLE = "Authority medium tank (Warzone parts, candidate)"
CANVAS = 128
TARGET = (0.0, 0.0, 1.2)
STATES = [("idle", 16, 1, 1)]
LAYERS = ["body", "turret"]

SC = 0.039                           # world units per Warzone unit: 82 wide x 80 long tracks -> 3.2 x 3.1 (the old tank sprite is 71 px = 1.1 cells wide)
_C0 = (0, 1, 31)                     # body connector 0 (x, depth z, height y) of drmbod05
PIVOT = (_C0[0] * SC, -_C0[1] * SC, _C0[2] * SC)
MOUNT = dict(deck_z=30 * SC, r_lip=0.5, r_well=0.0, r_shadow=0.95, shadow_z=30 * SC + 0.02, soft_r=0.75, lip="hull2")

# house-colour triangles (indices into the POLYGONS list): drmbod05 sloped hull-top side panels; trhcan sloped mount side panels
BODY_HOUSE = {22, 23, 30, 31}
MOUNT_HOUSE = {0, 1, 8, 9}

COMP = "/home/claude/ref/wz2100/data/base/components/"


def build(S, state, k, n):
    here = os.path.dirname(os.path.abspath(__file__))
    sys.path.insert(0, os.path.join(here, "..", "wz"))
    import pie_import as P
    import regrade as R
    comp = os.environ.get("WZ_COMPONENTS", COMP)

    def part(name, rel, material, **kw):
        pie = P.parse_pie(os.path.join(comp, rel))
        ob = P.build_object(name, pie, SC)
        return pie, ob

    S.begin("body")
    # hull.  The Warzone body pages carry (almost) no team-colour mask, so the house paint is assigned by us: the sloped side panels of the
    # hull top (the triangles listed in BODY_HOUSE), like the side panels of the old tank.
    pie, ob = part("wzbody", "bodies/drmbod05.pie", None)
    page = pie["texture"]
    m, _ = P.image_material("wz_hull", R.graded(page, "hull"), None)
    S.mesh("wzbody", P.build_object("wzbody", pie, SC, skip=BODY_HOUSE), m)
    S.mesh("wzbody_house", P.build_object("wzbody_house", pie, SC, only=BODY_HOUSE), "house")
    # tracks
    pie, ob = part("wztracks", "prop/prmtrk2.pie", None)
    m, _ = P.image_material("wz_track", R.graded(pie["texture"], "track"), None, rough=0.9)
    S.mesh("wztracks", ob, m)
    # turret mount (collar + contact shadow) and turret parts
    S.mount(PIVOT, **MOUNT)
    S.begin("turret", PIVOT)
    off = PIVOT
    pie, _ = part("wzmount", "weapons/trhcan.pie", None)
    for nm, kw, mat in (("wzmount", dict(skip=MOUNT_HOUSE), "hull"), ("wzmount_house", dict(only=MOUNT_HOUSE), "house")):
        ob = P.build_object(nm, pie, SC, **kw)
        ob.location = off
        S.mesh(nm, ob, mat)
    pie, ob = part("wzgun", "weapons/gnhvcan.pie", None)
    ob.location = off
    S.mesh("wzgun", ob, "steel")
