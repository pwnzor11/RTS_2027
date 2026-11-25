"""Batch entry point for the building art pass (session 43).

    python3 tools/art/bld_pass.py <asset_id> --out <dir> [--samples 24] [--depth]
    python3 tools/art/bld_pass.py <asset_id> --preview --out <scratch_dir> [--samples 6]

One asset id per invocation, deterministic, no machine-specific paths.  Renders every frame of tools/art/structures/<id>.py with
bpy (5.3 or Debian Blender 4.3 via a runpy runner), post-processes with structpost, and writes <out>/<id>.png + <out>/<id>.sprite.toml.
Work files go to art/pilot/work/struct_<id> (one folder per id, so parallel ids never collide).  Prints SECONDS_PER_ASSET.
The ids this covers are listed in tools/art/bld_pass_ids.txt.

--preview  cheap look at the module: one facing, only the first idle frame, the half-way idle frame and the first damaged frame,
           colour pass at 2x supersample and --samples 6 (default).  Writes <out>/<id>_preview_<state><index>.png and no sheet.
           The render itself is done by build_structure.py --preview; this script only picks the frames and copies the results.
"""
import argparse, importlib.util, os, shutil, subprocess, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))


def preview_frames(sid):
    """Sheet-order indices of the preview frames: idle 0, idle half-loop, damaged 0 (or the states the module has)."""
    spec = importlib.util.spec_from_file_location(f"preview_{sid}", os.path.join(HERE, "structures", f"{sid}.py"))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    picks, base = [], 0
    for name, n, _tpf in m.STATES:
        if name == "idle":
            picks += [(base, name, 0), (base + n // 2, name, n // 2) if n > 1 else None]
        elif name == "damaged":
            picks.append((base, name, 0))
        base += n
    return [p for p in picks if p]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--out", required=True)
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--depth", action="store_true")
    ap.add_argument("--preview", action="store_true")
    a = ap.parse_args()
    ids = open(os.path.join(HERE, "bld_pass_ids.txt")).read().split()
    if a.id not in ids:
        sys.exit(f"unknown asset id {a.id!r}; see tools/art/bld_pass_ids.txt")
    out = os.path.abspath(a.out)
    os.makedirs(out, exist_ok=True)
    t0 = time.time()
    if a.preview:
        picks = preview_frames(a.id)
        cmd = [sys.executable, os.path.join(HERE, "build_structure.py"), a.id, "--preview", "--samples", str(a.samples),
               "--frames", *[str(p[0]) for p in picks]]
        subprocess.run(cmd, check=True)
        sys.path.insert(0, HERE)
        from common import WORK  # noqa: E402  (work folder of the preview render)
        wd = os.path.join(WORK, f"struct_{a.id}")
        for fi, _name, idx in picks:
            shutil.copy(os.path.join(wd, f"preview_{fi:02d}.png"), os.path.join(out, f"{a.id}_preview_{_name}{idx}.png"))
        print(f"SECONDS_PER_PREVIEW {a.id} {time.time() - t0:.1f}")
        return
    cmd = [sys.executable, os.path.join(HERE, "build_structure.py"), a.id, "--samples", str(a.samples), "--out-dir", out]
    if a.depth:
        cmd.append("--depth")
    subprocess.run(cmd, check=True)
    print(f"SECONDS_PER_ASSET {a.id} {time.time() - t0:.1f}")


if __name__ == "__main__":
    main()
