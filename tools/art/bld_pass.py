"""Batch entry point for the building art pass (session 43).

    python3 tools/art/bld_pass.py <asset_id> --out <dir> [--samples 24] [--depth]

One asset id per invocation, deterministic, no machine-specific paths.  Renders every frame of tools/art/structures/<id>.py with
bpy (5.3 or Debian Blender 4.3 via a runpy runner), post-processes with structpost, and writes <out>/<id>.png + <out>/<id>.sprite.toml.
Work files go to art/pilot/work/struct_<id> (one folder per id, so parallel ids never collide).  Prints SECONDS_PER_ASSET.
The ids this covers are listed in tools/art/bld_pass_ids.txt.  Only auth_power has the 8-frame animated idle so far; the other ids
re-render with their current module (animation is added per module as the owner approves the pilot).
"""
import argparse, os, subprocess, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--out", required=True)
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--depth", action="store_true")
    a = ap.parse_args()
    ids = open(os.path.join(HERE, "bld_pass_ids.txt")).read().split()
    if a.id not in ids:
        sys.exit(f"unknown asset id {a.id!r}; see tools/art/bld_pass_ids.txt")
    cmd = [sys.executable, os.path.join(HERE, "build_structure.py"), a.id, "--samples", str(a.samples), "--out-dir", os.path.abspath(a.out)]
    if a.depth:
        cmd.append("--depth")
    t0 = time.time()
    subprocess.run(cmd, check=True)
    print(f"SECONDS_PER_ASSET {a.id} {time.time() - t0:.1f}")


if __name__ == "__main__":
    main()
