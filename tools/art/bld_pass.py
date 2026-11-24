"""Batch entry point for the building art pass (session 43).

    python3 tools/art/bld_pass.py <asset_id> --out <dir> [--samples 24] [--depth]
    python3 tools/art/bld_pass.py <asset_id> --out <scratch_dir> --preview [--samples 8] [--preview-frames 0 2 4 6 8 10]

One asset id per invocation, deterministic, no machine-specific paths.  Renders every frame of tools/art/structures/<id>.py with
bpy (5.3 or Debian Blender 4.3 via a runpy runner), post-processes with structpost, and writes <out>/<id>.png + <out>/<id>.sprite.toml.
Work files go to art/pilot/work/struct_<id> (one folder per id, so parallel ids never collide).  Prints SECONDS_PER_ASSET.
The ids this covers are listed in tools/art/bld_pass_ids.txt.  Only auth_power has the 8-frame animated idle so far; the other ids
re-render with their current module (animation is added per module as the owner approves the pilot).

--preview (session 43 fan-out): colour pass only, 2x supersample, a few frames (default idle 0,2,4,6 and damaged 8,10 for the
8-frame loops), low samples.  Copies <out>/<id>_preview_XX.png; writes no sheet and no .sprite.toml.  Keep under ~60 s per asset.
"""
import argparse, glob, os, shutil, subprocess, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--out", required=True)
    ap.add_argument("--samples", type=int, default=None)
    ap.add_argument("--depth", action="store_true")
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--preview-frames", type=int, nargs="*", default=[0, 2, 4, 6, 8, 10])
    a = ap.parse_args()
    ids = open(os.path.join(HERE, "bld_pass_ids.txt")).read().split()
    if a.id not in ids:
        sys.exit(f"unknown asset id {a.id!r}; see tools/art/bld_pass_ids.txt")
    out = os.path.abspath(a.out)
    t0 = time.time()
    if a.preview:
        os.makedirs(out, exist_ok=True)
        cmd = [sys.executable, os.path.join(HERE, "build_structure.py"), a.id, "--preview", "--samples", str(a.samples or 8),
               "--frames", *[str(f) for f in a.preview_frames]]
        subprocess.run(cmd, check=True)
        import build_structure as B
        wd = B.work_dir(a.id, False)
        for f in glob.glob(os.path.join(wd, "preview_*.png")):
            shutil.copy(f, os.path.join(out, f"{a.id}_" + os.path.basename(f)))
        print(f"SECONDS_PER_ASSET {a.id} {time.time() - t0:.1f} (preview)")
        return
    cmd = [sys.executable, os.path.join(HERE, "build_structure.py"), a.id, "--samples", str(a.samples or 24), "--out-dir", out]
    if a.depth:
        cmd.append("--depth")
    subprocess.run(cmd, check=True)
    print(f"SECONDS_PER_ASSET {a.id} {time.time() - t0:.1f}")


if __name__ == "__main__":
    main()
