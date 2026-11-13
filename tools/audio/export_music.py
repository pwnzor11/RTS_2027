# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) 2026 RTS_2027 contributors
"""Export the ACE-Step takes to the game's music files (M10-09).

Looks for  <ace-dir>\\output\\rts\\<track>\\take<N>\\*.wav  (made by tools/audio/ace_step/rts_batch.ps1), takes the newest WAV when a
take folder holds several, and writes  data/music/<track>.ogg :
  two-pass ffmpeg loudnorm (measure pass, then linear apply) to -16 LUFS / -1 dBTP / LRA 11 (victory and defeat: -14 LUFS),
  44.1 kHz stereo OGG Vorbis quality 5, tags stripped (-map_metadata -1).
ffmpeg comes from the imageio-ffmpeg pip package (preferred) or PATH.

Usage (from the repo root, Python 3.11+):
  python tools/audio/export_music.py                          # take 1 of all 20 tracks
  python tools/audio/export_music.py --pick menu=2,victory=3  # other takes for some tracks (the rest stay on --take)
  python tools/audio/export_music.py --only battle            # only tracks whose name contains "battle"
  python tools/audio/export_music.py --ace-dir D:\\ACE-Step-1.5 --take 2
Prints a table: track | take | LUFS in -> out | size, and warns about files over 5 MB and missing tracks.
"""
import argparse
import glob
import json
import os
import re
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DEFAULT_ACE_DIR = r"C:\ACE-Step-1.5"
MAX_BYTES = 5 * 1024 * 1024

FACTIONS = ("authority", "directorate", "leftbehind", "expedition")
TRACKS = (["menu", "credits", "victory", "defeat"]
          + [f"battle_{f}_{n}" for f in FACTIONS for n in (1, 2)]
          + [f"tension_{f}" for f in FACTIONS]
          + [f"briefing_{f}" for f in FACTIONS])
LOUD_14 = {"victory", "defeat"}  # play once, may sit louder (docs/audio/music_brief.md)


def find_ffmpeg():
    """imageio-ffmpeg's bundled binary first, then PATH."""
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        pass
    exe = shutil.which("ffmpeg")
    if exe:
        return exe
    sys.exit("ffmpeg not found: pip install imageio-ffmpeg (make_audio.ps1 -Setup does this)")


def parse_pick(text):
    picks = {}
    for part in filter(None, (p.strip() for p in (text or "").split(","))):
        m = re.fullmatch(r"([a-z0-9_]+)=(\d+)", part)
        if not m or m.group(1) not in TRACKS:
            sys.exit(f"bad --pick entry {part!r}: use track=N with a track name from the list of 20")
        picks[m.group(1)] = int(m.group(2))
    return picks


def newest_wav(folder):
    wavs = glob.glob(os.path.join(folder, "*.wav"))
    return max(wavs, key=os.path.getmtime) if wavs else None


def measure(ffmpeg, wav, target):
    """First loudnorm pass: the measured values as a dict (None when ffmpeg printed nothing parseable)."""
    r = subprocess.run([ffmpeg, "-hide_banner", "-nostats", "-i", wav, "-af",
                        f"loudnorm=I={target}:TP=-1:LRA=11:print_format=json", "-f", "null", "-"], capture_output=True, text=True)
    m = re.search(r"\{[^{}]*\"input_i\"[^{}]*\}", r.stderr, re.S)
    return json.loads(m.group(0)) if m else None


def export(ffmpeg, wav, ogg, target):
    """Two-pass loudnorm to OGG. Returns (input LUFS, output LUFS as predicted by the measure pass)."""
    s = measure(ffmpeg, wav, target)
    if not s:
        raise RuntimeError("loudnorm measure pass printed no statistics")
    af = (f"loudnorm=I={target}:TP=-1:LRA=11:measured_I={s['input_i']}:measured_TP={s['input_tp']}:"
          f"measured_LRA={s['input_lra']}:measured_thresh={s['input_thresh']}:offset={s['target_offset']}:linear=true")
    os.makedirs(os.path.dirname(ogg), exist_ok=True)
    subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-i", wav, "-af", af, "-ar", "44100", "-ac", "2",
                    "-c:a", "libvorbis", "-q:a", "5", "-map_metadata", "-1", ogg], check=True)
    # Output loudness: measure the file we wrote.
    o = measure(ffmpeg, ogg, target)
    return float(s["input_i"]), float(o["input_i"]) if o else float("nan")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--ace-dir", default=DEFAULT_ACE_DIR, help=f"ACE-Step folder (default {DEFAULT_ACE_DIR}); takes are in <dir>\\output\\rts")
    ap.add_argument("--take", type=int, default=1, help="take used for every track not named in --pick (default 1)")
    ap.add_argument("--pick", default="", help="per-track takes, e.g. menu=2,victory=3")
    ap.add_argument("--only", default="", help="only tracks whose name contains this text")
    ap.add_argument("--out", default=os.path.join(ROOT, "data", "music"), help="output folder (default data/music)")
    a = ap.parse_args()

    picks = parse_pick(a.pick)
    ffmpeg = find_ffmpeg()
    base = os.path.join(a.ace_dir, "output", "rts")
    rows, missing, big, failed = [], [], [], []
    for track in TRACKS:
        if a.only and a.only not in track:
            continue
        take = picks.get(track, a.take)
        wav = newest_wav(os.path.join(base, track, f"take{take}"))
        if not wav:
            missing.append(f"{track} (take {take})")
            rows.append((track, take, "missing", ""))
            continue
        ogg = os.path.join(a.out, track + ".ogg")
        target = -14.0 if track in LOUD_14 else -16.0
        try:
            lin, lout = export(ffmpeg, wav, ogg, target)
        except Exception as e:  # keep going so one bad file does not stop the other 19
            failed.append(f"{track}: {e}")
            rows.append((track, take, "FAILED", ""))
            continue
        size = os.path.getsize(ogg)
        if size > MAX_BYTES:
            big.append(f"{track} ({size / 1048576:.1f} MB)")
        rows.append((track, take, f"{lin:6.1f} -> {lout:6.1f}", f"{size / 1048576:5.2f} MB"))

    w = max(len(r[0]) for r in rows) if rows else 5
    print(f"{'track':<{w}} | take | LUFS in -> out    | size")
    for t, k, l, s in rows:
        print(f"{t:<{w}} | {k:>4} | {l:<17} | {s}")
    if missing:
        print(f"\nWARNING: no WAV found for {len(missing)} track(s): {', '.join(missing)}\n  (looked in {base})")
    if big:
        print("WARNING: larger than 5 MB: " + ", ".join(big))
    for f in failed:
        print("FAILED:", f)
    made = sum(1 for r in rows if r[2] not in ("missing", "FAILED"))
    print(f"\n{made} track(s) written to {a.out}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
