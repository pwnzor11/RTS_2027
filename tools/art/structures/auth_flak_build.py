"""Build the Authority Flak Emplacement sheet: python3 tools/art/structures/auth_flak_build.py [--samples 32] [--frames 0 5 ...] [--preview] [--post-only]

Same recipe as turret_build.py (rotating head, 16 facings; BS.render + structpost.run unchanged), for the module auth_flak.py.
Manifest layout: frame of facing f, animation step k in a state = first + f * frames + k.  Sheet: 4 columns, 80 x 112, 70 frames.
"""
import argparse
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import build_structure as BS   # noqa: E402
import structpost              # noqa: E402
import common as C             # noqa: E402

_spec = importlib.util.spec_from_file_location("structures_turret_build", os.path.join(HERE, "turret_build.py"))
TB = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(TB)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--samples", type=int, default=32)
    ap.add_argument("--frames", type=int, nargs="*", default=None)
    ap.add_argument("--post-only", action="store_true")
    ap.add_argument("--render-only", action="store_true")
    ap.add_argument("--preview", action="store_true")
    a = ap.parse_args()
    mod = BS.load_module("auth_flak")
    wd = BS.work_dir(mod.ID)
    if not a.post_only:
        BS.render(mod.ID, mod, a.samples, set(a.frames or []), a.preview)
    if not (a.render_only or a.preview):
        structpost.run(mod.ID, mod, wd)
        mp = os.path.join(C.ROOT, "data", "sprites", "buildings", f"{mod.ID}.sprite.toml")
        TB.write_manifest(mod, mp)
        t = open(mp).read()
        if "[house]" not in t:      # the house ramp block the other manifests carry (write_manifest omits it)
            hb = ("# House paint is the 8-shade magenta ramp, darkest first (HouseRamp.Standard). Exact RGB matches are house pixels.\n[house]\n"
                  'ramp = ["#400040", "#5B125B", "#772577", "#923792", "#AD49AD", "#C85BC8", "#E46EE4", "#FF80FF"]\n\n')
            t = t.replace("[[state]]", hb + "[[state]]", 1)
            open(mp, "w").write(t)


if __name__ == "__main__":
    main()
