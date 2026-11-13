# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) 2026 RTS_2027 contributors
"""Generate the campaign radio voice lines with Kokoro-82M (Apache-2.0), on the CPU (M8-18).

Offline build tool: never run by the game. Reads every data/missions/*/locale.toml [voice] table
(key -> "voice/<leader>/<key>.ogg"; tools/voice/voice_slots.py fills the tables), looks up the leader's voice in
tools/voice/leaders.json and writes
  tools/voice/out/<leader>/<key>.wav        (24 kHz mono, git-ignored)
  data/audio/voice/<leader>/<key>.ogg       (mono 24 kHz Vorbis, -18 LUFS, optional radio band-pass 300-3400 Hz)

Setup (laptop): tools\\audio\\make_audio.ps1 -Setup   (or: pip install kokoro soundfile imageio-ffmpeg numpy, plus espeak-ng)

Usage (from the repo root):
  python tools/voice/kokoro_batch.py --audition                 # 6-8 candidate voices per leader -> tools/voice/audition/index.html
  python tools/voice/kokoro_batch.py --audition --only leader_dir_larkin
  python tools/voice/kokoro_batch.py                            # every line of every leader that has a voice in leaders.json
  python tools/voice/kokoro_batch.py --only leader_dir_larkin   # one leader
  python tools/voice/kokoro_batch.py --force                    # redo files that already exist
  python tools/voice/kokoro_batch.py --no-radio                 # skip the radio band-pass
  python tools/voice/kokoro_batch.py --dry-run                  # list what would be made, load no model

leaders.json per leader: "voice" (Kokoro voice id such as bm_george; empty = not voiced yet), "speed" (1.0 default), "who".
The first letter of the voice id picks the accent: a* American (lang 'a'), b* British (lang 'b').
"""
import argparse
import glob
import html
import json
import os
import re
import shutil
import subprocess
import sys
import tomllib

HERE = os.path.dirname(os.path.abspath(__file__))


def _find_root():
    for start in (HERE, os.getcwd()):
        d = start
        while True:
            if os.path.isdir(os.path.join(d, "data", "missions")):
                return d
            parent = os.path.dirname(d)
            if parent == d:
                break
            d = parent
    sys.exit("run this from the RTS_2027 folder (data/missions not found)")


ROOT = _find_root()
SAMPLE_RATE = 24000
RADIO_FILTER = "highpass=f=300,lowpass=f=3400"
TARGET_LUFS = -18.0

# Audition candidates per leader (Kokoro v1.0 voices, VOICES.md on hexgrad/Kokoro-82M: af_/am_ American, bf_/bm_ British).
# Chosen by gender, accent and manner from each leader's "who" line. Overall grades on the model card run A to F+; the
# better-graded voices (af_heart, af_bella, af_nicole, bf_emma, am_michael, am_fenrir, am_puck, bm_george, bm_fable) come first
# where they fit. Edit freely; any voice id from the model card works.
CANDIDATES = {
    # older woman officer, short dry sentences
    "leader_auth_wrenfield":   ["bf_emma", "af_nicole", "bf_isabella", "af_kore", "af_sarah", "af_bella", "bf_alice", "af_heart"],
    # soft-spoken exact man
    "leader_auth_pell":        ["bm_george", "bm_fable", "am_michael", "am_fenrir", "am_puck", "bm_daniel", "am_echo", "am_adam"],
    # young awkward engineer, quick
    "leader_auth_dray":        ["am_puck", "am_michael", "am_liam", "am_echo", "am_eric", "bm_lewis", "am_fenrir", "am_adam"],
    # blunt woman
    "leader_auth_fenn":        ["af_nicole", "af_alloy", "af_kore", "af_sarah", "af_nova", "af_bella", "bf_isabella", "af_river"],
    # warm, reassuring radio announcer, woman
    "leader_dir_larkin":       ["af_heart", "af_bella", "af_sky", "af_nicole", "af_aoede", "bf_emma", "af_sarah", "af_jessica"],
    # courteous, unhurried, menacing man
    "leader_dir_thrall":       ["bm_george", "bm_fable", "bm_daniel", "bm_lewis", "am_onyx", "am_michael", "am_fenrir", "am_adam"],
    # polite precise woman, former auditor
    "leader_exp_strand":       ["bf_emma", "bf_isabella", "bf_alice", "bf_lily", "af_nicole", "af_sarah", "af_kore", "af_heart"],
    # formal older man, convoy officer
    "leader_exp_aldersey":     ["bm_george", "bm_fable", "bm_daniel", "bm_lewis", "am_michael", "am_fenrir", "am_onyx", "am_adam"],
    # gentle, distracted man
    "leader_exp_roque":        ["bm_fable", "am_puck", "am_michael", "bm_daniel", "am_echo", "am_liam", "am_eric", "bm_lewis"],
    # loud, affectionate woman
    "leader_exp_kell":         ["af_bella", "af_nova", "af_alloy", "af_heart", "bf_isabella", "af_aoede", "af_jessica", "af_kore"],
    # loud rough man
    "leader_lb_calloway_ross": ["am_fenrir", "am_onyx", "am_michael", "am_adam", "am_echo", "am_eric", "bm_lewis", "am_santa"],
    # unnamed tutorial instructor, calm and patient; the text gives no gender, so the list mixes men and women
    "leader_tut_instructor":   ["bm_george", "bf_emma", "af_heart", "am_michael", "bm_daniel", "af_sarah", "bf_isabella", "am_adam"],
}

# A real line of each leader to audition with (falls back to the leader's line nearest 110 characters).
AUDITION_KEYS = {
    "leader_auth_wrenfield": "a1.radio.start",
    "leader_auth_pell": "a2.radio.pell",
    "leader_auth_dray": "a4.radio.dray",
    "leader_auth_fenn": "a3.radio.cache",
    "leader_dir_thrall": "a4.radio.thrall",
    "leader_exp_aldersey": "e3.radio.aldersey",
    "leader_exp_roque": "e2.radio.roque",
    "leader_exp_kell": "e3.radio.kell",
    "leader_lb_calloway_ross": "lb2.radio.start",
    "leader_tut_instructor": "intro",
}


def load_lines():
    """[(leader, key, text, ogg_rel_path)] from every [voice] table, in a stable order."""
    lines = []
    for path in sorted(glob.glob(os.path.join(ROOT, "data", "missions", "*", "locale.toml"))):
        with open(path, "rb") as f:
            data = tomllib.load(f)
        for key, rel in sorted(data.get("voice", {}).items()):
            text = data.get(key)
            parts = rel.split("/")
            if not isinstance(text, str) or len(parts) != 3:
                print(f"skip {key} in {path}: no text or odd voice path {rel!r}")
                continue
            lines.append((parts[1], key, text, rel))
    return lines


def clean(text):
    """Light text clean-up for the speech engine (kept the same for every line)."""
    text = re.sub(r"\s+", " ", text).strip()
    return text.replace("--", ", ").replace("—", ", ").replace("–", "-")


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


def lang_of(voice):
    return "b" if voice.startswith("b") else "a"


class Speaker:
    """Lazy Kokoro pipelines, one per language code."""

    def __init__(self, device):
        self.device = device
        self.pipes = {}

    def say(self, text, voice, speed):
        """float32 numpy mono audio at 24 kHz."""
        import numpy as np
        lang = lang_of(voice)
        if lang not in self.pipes:
            from kokoro import KPipeline
            print(f"loading Kokoro-82M (lang '{lang}', device {self.device}); the first run downloads the model")
            self.pipes[lang] = KPipeline(lang_code=lang, device=self.device)
        chunks = []
        for _gs, _ps, audio in self.pipes[lang](clean(text), voice=voice, speed=speed, split_pattern=r"\n+"):
            if audio is not None:
                a = audio.detach().cpu().numpy() if hasattr(audio, "detach") else np.asarray(audio)
                chunks.append(a.astype("float32"))
        if not chunks:
            raise RuntimeError("Kokoro returned no audio")
        return np.concatenate(chunks)


def write_wav(path, audio):
    import soundfile as sf
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sf.write(path, audio, SAMPLE_RATE, subtype="PCM_16")


def to_ogg(ffmpeg, wav, ogg, radio):
    """Mono OGG Vorbis, two-pass loudnorm to -18 LUFS, optional 300-3400 Hz radio band-pass."""
    pre = (RADIO_FILTER + ",") if radio else ""
    ln = f"loudnorm=I={TARGET_LUFS}:TP=-1.5:LRA=11"
    r = subprocess.run([ffmpeg, "-hide_banner", "-nostats", "-i", wav, "-af", f"{pre}{ln}:print_format=json", "-f", "null", "-"],
                       capture_output=True, text=True)
    m = re.search(r"\{[^{}]*\"input_i\"[^{}]*\}", r.stderr, re.S)
    if m:
        s = json.loads(m.group(0))
        ln += (f":measured_I={s['input_i']}:measured_TP={s['input_tp']}:measured_LRA={s['input_lra']}"
               f":measured_thresh={s['input_thresh']}:offset={s['target_offset']}:linear=true")
    os.makedirs(os.path.dirname(ogg), exist_ok=True)
    subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-i", wav, "-af", pre + ln, "-ac", "1", "-ar", str(SAMPLE_RATE),
                    "-c:a", "libvorbis", "-q:a", "4", "-map_metadata", "-1", ogg], check=True)


def audition(a, leaders, lines, speaker):
    out_root = os.path.join(HERE, "audition")
    by_leader = {}
    for leader, key, text, _ in lines:
        by_leader.setdefault(leader, []).append((key, text))
    sections = []
    for leader, cfg in leaders.items():
        if a.only and leader != a.only:
            continue
        cands = CANDIDATES.get(leader)
        pool = by_leader.get(leader)
        if not cands or not pool:
            print(f"skip {leader}: no candidates or no lines")
            continue
        want = AUDITION_KEYS.get(leader)
        pick = next(((k, t) for k, t in pool if k == want), None) or min(pool, key=lambda kt: abs(len(kt[1]) - 110))
        key, text = pick
        speed = float(cfg.get("speed", 1.0))
        rows = []
        for voice in cands:
            wav = os.path.join(out_root, leader, f"{voice}.wav")
            if a.dry_run:
                print(f"would write {wav}")
            else:
                if not os.path.isfile(wav) or a.force:
                    write_wav(wav, speaker.say(text, voice, speed))
                print(f"wrote {wav}")
            rows.append(voice)
        cur = cfg.get("voice") or "(none yet)"
        players = "\n".join(
            f'<tr><td><code>{v}</code></td><td><audio controls preload="none" src="{leader}/{v}.wav"></audio></td></tr>' for v in rows)
        sections.append(
            f"<section><h2>{html.escape(leader)}</h2><p class='who'>{html.escape(cfg.get('who', ''))}</p>"
            f"<p class='line'>Line <code>{html.escape(key)}</code>: &ldquo;{html.escape(text)}&rdquo;</p>"
            f"<table>{players}</table>"
            f"<p>Current voice in leaders.json: <code>{html.escape(cur)}</code>. To choose one, set "
            f"<code>\"voice\": \"&lt;voice id&gt;\"</code> for <code>{html.escape(leader)}</code> in tools/voice/leaders.json.</p></section>")
    if a.dry_run or not sections:
        return
    page = ("<!doctype html><html lang='en'><meta charset='utf-8'><title>Voice audition</title>"
            "<style>body{font:15px/1.4 sans-serif;max-width:60em;margin:1em auto;padding:0 1em}section{border-top:2px solid #888;margin-top:1.5em}"
            ".who{color:#555}.line{font-style:italic}td{padding:2px 8px}</style>"
            "<h1>RTS_2027 voice audition (Kokoro-82M)</h1>"
            "<p>One real line per leader in each candidate voice. Pick one voice per leader and put its id in tools/voice/leaders.json.</p>"
            + "\n".join(sections) + "</html>")
    os.makedirs(out_root, exist_ok=True)
    idx = os.path.join(out_root, "index.html")
    with open(idx, "w", encoding="utf-8") as f:
        f.write(page)
    print(f"wrote {idx}  (open it in a browser)")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--audition", action="store_true", help="candidate voices per leader -> tools/voice/audition/")
    ap.add_argument("--only", help="only this leader id")
    ap.add_argument("--force", action="store_true", help="redo files that already exist")
    ap.add_argument("--radio", dest="radio", action="store_true", default=True, help="radio band-pass 300-3400 Hz (default)")
    ap.add_argument("--no-radio", dest="radio", action="store_false", help="no band-pass")
    ap.add_argument("--device", default="cpu", help="torch device for Kokoro (default cpu)")
    ap.add_argument("--dry-run", action="store_true", help="list the work; load no model, write nothing")
    a = ap.parse_args()

    with open(os.path.join(HERE, "leaders.json"), encoding="utf-8") as f:
        leaders = json.load(f)
    lines = load_lines()
    speaker = Speaker(a.device)

    if a.audition:
        audition(a, leaders, lines, speaker)
        return

    todo = [(l, k, t, r) for l, k, t, r in lines
            if (not a.only or l == a.only) and leaders.get(l, {}).get("voice")]
    unvoiced = sorted({l for l, *_ in lines if not leaders.get(l, {}).get("voice") and (not a.only or l == a.only)})
    if unvoiced:
        print("no voice set yet in leaders.json for: " + ", ".join(unvoiced) + "  (run --audition, then set \"voice\")")
    ffmpeg = None if a.dry_run else find_ffmpeg()
    done = skipped = 0
    for leader, key, text, rel in todo:
        cfg = leaders[leader]
        wav = os.path.join(HERE, "out", leader, f"{key}.wav")
        ogg = os.path.join(ROOT, "data", "audio", *rel.split("/"))
        if os.path.isfile(ogg) and not a.force:
            skipped += 1
            continue
        if a.dry_run:
            print(f"would make {rel}  ({cfg['voice']}, speed {cfg.get('speed', 1.0)})")
            done += 1
            continue
        if not os.path.isfile(wav) or a.force:
            write_wav(wav, speaker.say(text, cfg["voice"], float(cfg.get("speed", 1.0))))
        to_ogg(ffmpeg, wav, ogg, a.radio)
        done += 1
        print(f"[{done}/{len(todo) - skipped}] {leader} {key}")
    print(f"done: {done} made, {skipped} already existed, {len(unvoiced)} leader(s) without a voice")


if __name__ == "__main__":
    main()
