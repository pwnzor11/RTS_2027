"""M10-06b kitbash pilot: the Authority Bulwark with Warzone 2100 weapon geometry, for tools/art/build_vehicle.py.  Contract: see auth_jeep.py.

NOT a replacement of auth_bulwark (that sprite stays untouched); this is a candidate rendered beside it for the owner's review.
Everything of auth_bulwark is kept except the cannon tube: its sleeve, barrel and slotted brake are skipped and the Warzone hyper-velocity
cannon barrel (components/weapons/gnhvcan.pie) takes their place in front of our own mantlet.  With MG = True (this module) a Warzone heavy
machine gun (components/weapons/gnmmg1.pie) also sits on the commander's cupola; auth_bulwark_wz_c.py is the cannon-only variant.
Licence (D-189): the .pie geometry is GPL-2.0-or-later; the weapon texture page (page-17-droid-weapons.png) is CC BY-SA 3.0 and is NOT
used: the Warzone parts are painted with our own flat materials.
"""
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("vehicles_auth_bulwark_base", os.path.join(HERE, "auth_bulwark.py"))
B = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(B)

ID = "auth_bulwark_wz"
TITLE = "Authority Bulwark heavy tank (Warzone weapon kitbash, candidate)"
CANVAS, TARGET, STATES, LAYERS, PIVOT, MOUNT = B.CANVAS, B.TARGET, B.STATES, B.LAYERS, B.PIVOT, B.MOUNT
MG = True

COMP = "/home/claude/ref/wz2100/data/base/components/"
SKIP = {"sleeve", "barrel", "brake", "slot0", "slot1", "slot2"}


class _Filter:
    """Wraps build_vehicle's scene namespace S and drops the parts named in SKIP; everything else passes through."""

    def __init__(self, S):
        self._S = S

    def __getattr__(self, k):
        v = getattr(self._S, k)
        if k in ("box", "cyl", "sphere", "cone"):
            def call(name, *a, **kw):
                if name in SKIP:
                    return None
                return v(name, *a, **kw)
            return call
        return v


def build(S, state, k, n):
    B.build(_Filter(S), state, k, n)          # ends inside the turret group, which is where the Warzone parts go
    sys.path.insert(0, os.path.join(HERE, "..", "wz"))
    import pie_import as P
    comp = os.environ.get("WZ_COMPONENTS", COMP)
    px, py, pz = PIVOT
    gz = pz + 0.45
    # cannon: gnhvcan spans depth 18..64 (barrel root .. muzzle) and height 2..15.  Root against the mantlet front (py + 1.5), muzzle where
    # the old brake ended (py + 3.48), so the gun keeps its length; cross-section about 0.5 wide, a little heavier than the old 0.3 tube.
    pie = P.parse_pie(os.path.join(comp, "weapons/gnhvcan.pie"))
    sc = 0.036
    ystr = (3.48 - 1.5) / ((64 - 18) * sc)
    ob = P.build_object("wzcannon", pie, sc, offset=(px, py + 1.5 - 18 * sc * ystr, gz - 8.5 * sc), ystretch=ystr)
    S.mesh("wzcannon", ob, "steel")
    if MG:
        # cupola machine gun: gnmmg1 spans depth 15..40, height 0..10; breech over the cupola centre, muzzle forward.
        pie = P.parse_pie(os.path.join(comp, "weapons/gnmmg1.pie"))
        sc = 0.028
        ob = P.build_object("wzmg", pie, sc, offset=(px - 0.5, py - 0.5 - 15 * sc, pz + 1.02))
        S.mesh("wzmg", ob, "dark")
