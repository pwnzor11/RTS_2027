"""S43 infantry readability pass, batch entry point (one asset id per invocation, deterministic, no laptop-only paths).

    python3 tools/art/inf_pass.py <asset_id> --out DIR [--variant A|B|C] [--samples 24] [--work DIR] [--post-only]

Runs tools/art/build_infantry.py with the pilot preset: boosted silhouette (infantry/boost.py: head x1.25, rifle 1.12 long x 1.45 thick,
stance x1.3, limbs x1.12, house panels x1.45), 8 walk frames (ticks_per_frame 2), deaths included, house calibration lifted by 0.15 span,
contrast 1.15, muzzle-flash star on the last fire frame, and the variant's outline option:
    A = keyline (dark line where a house panel meets cloth), B = rim light (1 px lit upper-left rim), C = both (default).
Writes DIR/<id>.png + DIR/<id>.sprite.toml.  Work files go to --work (default DIR/_work/) so parallel jobs never collide.
Needs `bpy` (5.3) or Blender 4.3 through a runpy runner; no OpenImageDenoise is used.  The caller wraps it in flock/nice when sharing a box.
Only modules that import infantry/boost.py react to the preset (done: auth_rifleman, dir_conscript); the others render unchanged geometry
but still get 8 walk frames, outline/rim, brighter house shades and the flash star.  ids: tools/art/inf_pass_ids.txt.
"""
import argparse, os, subprocess, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))
STYLE = {"A": "keyline", "B": "rim", "C": "keyline,rim"}
NO_FLASH = {"auth_engineer", "dir_assessor"}      # S43 fan: unarmed units (tool bag, ledger) get no muzzle-flash star
# --preview (S43 fan): one south-ish facing (16 facings, south = 8), every walk step, both fire steps, every die step,
# no deaths, 6 samples. Sheet indices: idle 8; walk 80..87; fire 160..161; die 176..181 (the module's STATES layout, 16 facings).
PREVIEW_FRAMES = [8] + list(range(80, 88)) + [160, 161] + list(range(176, 182))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--out", required=True)
    ap.add_argument("--variant", default="C", choices=sorted(STYLE))
    ap.add_argument("--samples", type=int, default=None, help="default 24, or 6 with --preview")
    ap.add_argument("--work", default=None)
    ap.add_argument("--post-only", action="store_true")
    ap.add_argument("--preview", action="store_true", help="cheap check render: one facing, low samples, no deaths")
    a = ap.parse_args()
    out = os.path.abspath(a.out)
    os.makedirs(out, exist_ok=True)
    env = dict(os.environ, ART_WORK=os.path.abspath(a.work or os.path.join(out, "_work")))
    samples = a.samples if a.samples is not None else (6 if a.preview else 24)
    flash = "" if a.id in NO_FLASH else ",flash"
    cmd = [sys.executable, os.path.join(HERE, "build_infantry.py"), a.id, "--boost", "pilot", "--walk-frames", "8",
           "--samples", str(samples), "--style", STYLE[a.variant] + ",contrast=1.15" + flash, "--house-lift", "0.15", "--out", out]
    if not a.preview:
        cmd.append("--deaths")
    else:
        cmd += ["--frames"] + [str(i) for i in PREVIEW_FRAMES]
    if a.post_only:
        cmd.append("--post-only")
    t0 = time.time()
    subprocess.run(cmd, check=True, env=env)
    print(f"INF_PASS {a.id} variant {a.variant} {time.time() - t0:.0f} s")


if __name__ == "__main__":
    main()
