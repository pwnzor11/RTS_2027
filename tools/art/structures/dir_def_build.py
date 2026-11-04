"""Driver for rotating-head Directorate structures (pillbox): same sheet layout as turret_build.py (which hard-codes auth_turret).
  python3 tools/art/structures/dir_def_build.py dir_pillbox [--samples 32] [--frames 0 5 ...] [--preview] [--post-only]
Renders with build_structure.render + structpost.run unchanged and writes the manifest with turret_build.write_manifest."""
import argparse
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, HERE)
import build_structure as BS   # noqa: E402
import structpost              # noqa: E402
import common as C             # noqa: E402
import turret_build as TB      # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--frames", type=int, nargs="*", default=None)
    ap.add_argument("--post-only", action="store_true")
    ap.add_argument("--preview", action="store_true")
    a = ap.parse_args()
    mod = BS.load_module(a.id)
    wd = BS.work_dir(mod.ID)
    if not a.post_only:
        BS.render(mod.ID, mod, a.samples, set(a.frames or []), a.preview)
    if not a.preview:
        structpost.run(mod.ID, mod, wd)
        TB.write_manifest(mod, os.path.join(C.ROOT, "data", "sprites", "buildings", f"{mod.ID}.sprite.toml"))


if __name__ == "__main__":
    main()
