"""Motion pass batch entry point: render one ground vehicle with its idle and move states.

Usage:  python3 tools/art/mot_pass.py <asset_id> --out <dir> [--samples 12] [--states idle move] [--facings ...]
        python3 tools/art/mot_pass.py <asset_id> --out <dir> --preview   (one facing, idle + move, 8 samples, 64 px cell; colour frames only)
Writes <dir>/<id>.png and <dir>/<id>.sprite.toml (nothing else is touched in the repo; work files go to <dir>/work/).
Deterministic, one asset per invocation, no machine-specific paths: it only calls tools/art/build_vehicle.py.  Under Debian Blender 4.3
use the runpy runner:  blender -b --python run.py -- tools/art/mot_pass.py <id> --out <dir>   (the module import of bpy is the only Blender need).
The asset's module tools/art/vehicles/<id>.py must define a "move" state (see auth_tank.py, auth_jeep.py); ids: tools/art/mot_pass_ids.txt.
"""
import argparse, os, subprocess, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--out", required=True)
    ap.add_argument("--samples", type=int, default=12)
    ap.add_argument("--states", nargs="*", default=None)
    ap.add_argument("--facings", type=int, nargs="*", default=None)
    ap.add_argument("--preview", action="store_true", help="one facing (--preview-facing, 16 = south), idle + move, low samples, half-res colour frames")
    ap.add_argument("--preview-facing", type=int, default=16)
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:])
    out = os.path.abspath(a.out)
    os.makedirs(out, exist_ok=True)
    env = dict(os.environ, ART_WORK=os.path.join(out, "work"))
    cmd = [sys.executable, os.path.join(HERE, "build_vehicle.py"), a.id, "--samples", str(a.samples), "--out", out]
    if a.preview:
        a.samples = min(a.samples, 8) if a.samples != 12 else 8
        cmd += ["--preview", "--facings", str(a.preview_facing), "--px-cell", "64"]
        a.states = a.states or ["idle", "move"]
    if a.states:
        cmd += ["--states", *a.states]
    if a.facings:
        cmd += ["--facings", *map(str, a.facings)]
    t0 = time.time()
    rc = subprocess.call(cmd, env=env)
    print(f"MOT_PASS {a.id} seconds={time.time() - t0:.0f} rc={rc}")
    sys.exit(rc)


if __name__ == "__main__":
    main()
