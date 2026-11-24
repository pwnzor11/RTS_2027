"""Batch entry point for the building art pass (session 43).

    python3 tools/art/bld_pass.py <asset_id> --out <dir> [--samples 24] [--depth] [--preview]

One asset id per invocation, deterministic, no machine-specific paths.  Renders every frame of tools/art/structures/<id>.py with
bpy (5.3 or Debian Blender 4.3 via a runpy runner), post-processes with structpost, and writes <out>/<id>.png + <out>/<id>.sprite.toml.
Work files go to art/pilot/work/struct_<id> (one folder per id, so parallel ids never collide).  Prints SECONDS_PER_ASSET.
The ids this covers are listed in tools/art/bld_pass_ids.txt.  Only auth_power has the 8-frame animated idle so far; the other ids
re-render with their current module (animation is added per module as the owner approves the pilot).
"""
import argparse, os, subprocess, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))


# --preview: one facing, the animated frames only (idle 0,2,4,6 and damaged 0,4 in frame-list order), colour pass, low samples.
# Frame indices are build_structure.frame_list() indices (idle 0..7 then damaged 0..7).  Output goes to --out/preview_<fi>.png.
PREVIEW_FRAMES = [0, 2, 4, 6, 8, 12]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--out", required=True)
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--depth", action="store_true")
    ap.add_argument("--preview", action="store_true", help="cheap preview: one facing, PREVIEW_FRAMES only, writes <out>/preview_XX.png")
    a = ap.parse_args()
    ids = open(os.path.join(HERE, "bld_pass_ids.txt")).read().split()
    if a.id not in ids:
        sys.exit(f"unknown asset id {a.id!r}; see tools/art/bld_pass_ids.txt")
    cmd = [sys.executable, os.path.join(HERE, "build_structure.py"), a.id, "--samples", str(a.samples), "--out-dir", os.path.abspath(a.out)]
    if a.depth:
        cmd.append("--depth")
    if a.preview:
        cmd = [sys.executable, os.path.join(HERE, "build_structure.py"), a.id, "--samples", str(a.samples or 6), "--preview",
               "--frames", *map(str, PREVIEW_FRAMES)]
    t0 = time.time()
    subprocess.run(cmd, check=True)
    if a.preview:
        import shutil
        sys.path.insert(0, HERE)
        import common as C
        wd = os.path.join(C.WORK, f"struct_{a.id}")
        os.makedirs(a.out, exist_ok=True)
        for fi in PREVIEW_FRAMES:
            shutil.copy(os.path.join(wd, f"preview_{fi:02d}.png"), os.path.join(a.out, f"preview_{fi:02d}.png"))
    print(f"SECONDS_PER_ASSET {a.id} {time.time() - t0:.1f}")


if __name__ == "__main__":
    main()
