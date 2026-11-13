#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Sound-effect generator for RTS_2027 (M10-10): Stable Audio Open 1.0 on the CPU, plus pick / audition helpers.

Reads tools/audio/sfx/sfx_jobs.toml. The model code and weights are NOT in the repository: only the WAV files we generate are.

  python tools/audio/sfx/make_sfx.py --list                       # the jobs, the time estimate (no model needed)
  python tools/audio/sfx/make_sfx.py [--only SUBSTR] [--takes N] [--steps N | --draft] [--seconds-scale F]
  python tools/audio/sfx/make_sfx.py --audition                   # writes tools/audio/sfx/out/index.html (listen, choose)
  python tools/audio/sfx/make_sfx.py --pick [id=2 id=d1 ...] [--picks-file picks.toml] [--mix] [--replace-sfxr]

Generation writes tools/audio/sfx/out/<id>/take<N>.wav (git-ignored; draft runs write draft<N>.wav) after post-processing: trim
silence, fade, mono downmix where asked, resample, peak / loudness normalise, 16-bit PCM. Takes that exist are skipped, so a
stopped run can simply be started again. The seed of a take is a hash of id and take number, so a rerun gives the same sound.

--pick copies the chosen take (default take 1; ``id=2`` for take 2, ``id=d1`` for draft 1; or a picks.toml with a [picks] table)
to data/audio/sfx/<id>.wav and edits data/audio/sounds.toml:
  * each picked sound gets a [sound.<id>] entry inside a managed block at the end of the file;
  * the events it lists in ``events`` get the sound as one more random variant (the sfxr sounds stay unless --replace-sfxr); a qualified key
    such as ``building_placed.authority`` or ``fire_shell.cannon.authority`` becomes its own [event.<key>] section, which the game prefers
    over the plain event (lookup order at the top of data/audio/sounds.toml); an ``ambience = "glass_coast"`` job becomes [ambience.glass_coast];
  * ``--mix`` also adds the sounds listed in ``mix_into`` (extra random variants of generic events; no job needs it any more);
  * a ``repoint`` job switches that existing menu sound ([sound.ui_move] ...) to the new file.
Only files that exist are referenced, so the game and SoundBankTests keep loading. Running --pick again is safe (idempotent).

The "short latent" trick: diffusers' StableAudioPipeline always denoises the full 47.55 s window, however short the clip, which
costs 20 to 25 times too much on a CPU for a one-second shot. We pass our own noise tensor sized to the clip instead (like
stable-audio-tools' sample_size), seeded per take. ``--latent-mode full`` uses the stock behaviour (much slower).
"""
from __future__ import annotations

import argparse
import hashlib
import html
import json
import math
import re
import shutil
import sys
import time
import tomllib
import wave
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent.parent
DEFAULT_JOBS = HERE / "sfx_jobs.toml"
DEFAULT_OUT = HERE / "out"
DEFAULT_SFX_DIR = REPO / "data" / "audio" / "sfx"
DEFAULT_SOUNDS = REPO / "data" / "audio" / "sounds.toml"
DEFAULT_PICKS = HERE / "picks.toml"
MODEL_ID = "stabilityai/stable-audio-open-1.0"
MODEL_RATE = 44100
OUTPUT_RATE = 22050            # S22 (owner decision A): one-shots are 22.05 kHz mono, 16-bit; ambience loops 22.05 kHz stereo (jobs may override `rate`)
MAX_ONESHOT_BYTES = 270_000    # a 6 s 22.05 kHz mono 16-bit clip; SoundBankTests.AllWavFilesAreSmall16Bit enforces the same caps
MAX_LOOP_BYTES = 2_700_000     # a 30 s 22.05 kHz stereo 16-bit loop
VAE_HOP = 2048                 # audio samples per latent frame
MODEL_MAX_SECONDS = 47.0       # the model's window is 47.55 s
MIN_WINDOW_SECONDS = 2.0       # shorter windows are padded: very short latents give poor sounds (UNVERIFIED, conservative)
DEFAULT_STEPS = 100
DRAFT_STEPS = 30
# UNVERIFIED CPU cost model: about 4.2 GFLOP per latent frame per denoising step (1.06 B-parameter DiT, batch 2 for guidance) on a
# CPU that sustains 140 to 350 GFLOPS in fp32. Seconds per (latent frame x step):
SEC_PER_FRAME_STEP = (0.012, 0.030)
FIXED_SEC_PER_CLIP = (6.0, 20.0)   # text encoder, VAE decode, post-processing
MARK_BEGIN = "# BEGIN sfx-generated (tools/audio/sfx/make_sfx.py --pick; do not edit by hand, rerun the tool)"
MARK_END = "# END sfx-generated"
MAX_VOICES_BY_CATEGORY = {"weapons": 4, "explosions": 3, "ambience": 1}
GATE_HELP = """\
Cannot download the model. stabilityai/stable-audio-open-1.0 is gated on Hugging Face:
  1. open https://huggingface.co/stabilityai/stable-audio-open-1.0 while logged in and accept the terms (Community License);
  2. create a read token at https://huggingface.co/settings/tokens and run:  make_sfx.ps1 -Login   (once)
Then start again."""


# ------------------------------------------------------------------------------------------------------------------ jobs
def load_jobs(path: Path = DEFAULT_JOBS) -> dict:
    """Parse the job file. Returns {"meta": {...}, "jobs": [resolved job dicts in file order]}."""
    with open(path, "rb") as f:
        raw = tomllib.load(f)
    defaults = raw.get("defaults", {})
    cats = raw.get("category", {})
    jobs = []
    for jid, j in raw.get("job", {}).items():
        if not re.fullmatch(r"[a-z][a-z0-9_]*", jid):
            raise ValueError(f"job id '{jid}' must be lower snake_case")
        if "prompt" not in j:
            raise ValueError(f"job '{jid}' has no prompt")
        cat = j.get("category", "misc")
        r = {**defaults, **cats.get(cat, {}), **j}
        r["id"] = jid
        r["category"] = cat
        r["category_label"] = cats.get(cat, {}).get("label", cat)
        neg = r.get("negative", "")
        extra = r.get("negative_extra", "")
        r["negative"] = (neg + (", " + extra if extra else "")).strip(", ")
        r["file"] = f"sfx/{jid}.wav"
        r.setdefault("volume", 1.0)
        r.setdefault("events", [])
        r.setdefault("mix_into", [])
        r.setdefault("planned", "")
        r.setdefault("repoint", "")
        r.setdefault("ambience", "")
        r.setdefault("loop", False)
        r.setdefault("max_voices", MAX_VOICES_BY_CATEGORY.get(cat, 2))
        if r["seconds"] > MODEL_MAX_SECONDS:
            raise ValueError(f"job '{jid}': seconds {r['seconds']} exceeds the model window {MODEL_MAX_SECONDS}")
        if r["channels"] not in (1, 2):
            raise ValueError(f"job '{jid}': channels must be 1 or 2")
        if r["loop"] and r.get("loop_xfade", 2.0) * 2 >= r["seconds"]:
            raise ValueError(f"job '{jid}': loop_xfade too long for seconds")
        jobs.append(r)
    ids = [j["id"] for j in jobs]
    if len(ids) != len(set(ids)):
        raise ValueError("duplicate job ids")
    for j in jobs:
        for ev in j["events"] + j["mix_into"]:
            if not re.fullmatch(r"[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*){0,2}", ev):
                raise ValueError(f"job '{j['id']}': event key '{ev}' must be snake_case parts joined by dots (event.class.faction)")
    return {"meta": raw.get("defaults", {}), "jobs": jobs, "event_defaults": raw.get("event_defaults", {})}


def seed_for(job_id: str, take: int) -> int:
    """Deterministic seed for a take (31-bit)."""
    h = hashlib.sha256(f"rts2027-sfx:{job_id}:{take}".encode()).digest()
    return int.from_bytes(h[:4], "big") & 0x7FFFFFFF


def window_seconds(job: dict, scale: float) -> float:
    """Length of the generation window for a job."""
    return min(MODEL_MAX_SECONDS, max(MIN_WINDOW_SECONDS, job["seconds"] * scale))


def latent_frames(window: float) -> int:
    """Latent frames for a window (2048 samples each at 44.1 kHz)."""
    return int(math.ceil(window * MODEL_RATE / VAE_HOP))


def estimate_seconds(jobs: list[dict], takes: int | None, steps: int, scale: float, full: bool = False) -> tuple[float, float]:
    """(low, high) CPU seconds for generating every take of every job. UNVERIFIED constants, see SEC_PER_FRAME_STEP."""
    lo = hi = 0.0
    for j in jobs:
        n = takes if takes is not None else j["takes"]
        frames = 1024 if full else latent_frames(window_seconds(j, scale))
        lo += n * (frames * steps * SEC_PER_FRAME_STEP[0] + FIXED_SEC_PER_CLIP[0])
        hi += n * (frames * steps * SEC_PER_FRAME_STEP[1] + FIXED_SEC_PER_CLIP[1])
    return lo, hi


def fmt_hms(sec: float) -> str:
    sec = int(sec)
    return f"{sec // 3600}h{(sec % 3600) // 60:02d}m" if sec >= 3600 else f"{sec // 60}m{sec % 60:02d}s"


# ------------------------------------------------------------------------------------------------------------ audio maths
def db_to_lin(db: float) -> float:
    return 10.0 ** (db / 20.0)


def resample(x: np.ndarray, src: int, dst: int) -> np.ndarray:
    """Resample (channels, n) float audio from src to dst Hz (polyphase; linear interpolation if SciPy is missing)."""
    if src == dst:
        return x
    try:
        from math import gcd
        from scipy.signal import resample_poly
        g = gcd(src, dst)
        return resample_poly(x, dst // g, src // g, axis=1).astype(np.float32)
    except ImportError:  # pragma: no cover - the laptop venv installs SciPy
        n = int(round(x.shape[1] * dst / src))
        t = np.linspace(0, x.shape[1] - 1, n)
        return np.stack([np.interp(t, np.arange(x.shape[1]), c) for c in x]).astype(np.float32)


def make_loop(x: np.ndarray, rate: int, xfade_s: float) -> np.ndarray:
    """Seamless loop: the last xfade seconds are cross-faded (equal power) into the first, and dropped from the end."""
    xf = int(round(xfade_s * rate))
    n = x.shape[1]
    if n <= 2 * xf:
        raise ValueError("clip too short for the loop cross-fade")
    out = x[:, : n - xf].copy()
    w = np.linspace(0.0, 1.0, xf, dtype=np.float32)
    out[:, :xf] = x[:, n - xf:] * np.cos(w * math.pi / 2) + x[:, :xf] * np.sin(w * math.pi / 2)
    return out


def trim_silence(x: np.ndarray, rate: int, trim_db: float, tail_ms: float = 25.0, head_ms: float = 5.0) -> np.ndarray:
    """Cut leading and trailing audio quieter than ``trim_db`` below the peak (keeps a few ms around the sound)."""
    env = np.max(np.abs(x), axis=0)
    peak = float(env.max()) if env.size else 0.0
    if peak <= 0.0:
        return x
    above = np.nonzero(env >= peak * db_to_lin(-trim_db))[0]
    a = max(0, int(above[0]) - int(head_ms * rate / 1000))
    b = min(x.shape[1], int(above[-1]) + 1 + int(tail_ms * rate / 1000))
    return x[:, a:b]


def apply_fades(x: np.ndarray, rate: int, in_ms: float, out_ms: float) -> np.ndarray:
    """Raised-cosine fade in and out (in place on a copy)."""
    x = x.copy()
    n = x.shape[1]
    fi = min(n // 2, int(in_ms * rate / 1000))
    fo = min(n // 2, int(out_ms * rate / 1000))
    if fi > 1:
        x[:, :fi] *= (0.5 - 0.5 * np.cos(np.linspace(0, math.pi, fi, dtype=np.float32)))
    if fo > 1:
        x[:, n - fo:] *= (0.5 + 0.5 * np.cos(np.linspace(0, math.pi, fo, dtype=np.float32)))
    return x


def postprocess(audio: np.ndarray, in_rate: int, job: dict, scale: float = 1.0, rate_override: int | None = None) -> tuple[np.ndarray, int]:
    """Model output (channels, n) float -> (n, channels) int16 and the sample rate, following the job's rules."""
    x = np.asarray(audio, dtype=np.float32)
    if x.ndim == 1:
        x = x[None, :]
    x = x - x.mean(axis=1, keepdims=True)                       # DC offset
    rate = int(rate_override or job.get("rate", OUTPUT_RATE))
    x = resample(x, in_rate, rate)
    if job["channels"] == 1 and x.shape[0] > 1:
        x = x.mean(axis=0, keepdims=True)
    elif job["channels"] == 2 and x.shape[0] == 1:
        x = np.repeat(x, 2, axis=0)
    if job.get("loop"):
        x = make_loop(x, rate, float(job.get("loop_xfade", 2.0)))
    elif job.get("trim", True):
        x = trim_silence(x, rate, float(job.get("trim_db", 45)))
        limit = int(job["seconds"] * scale * rate)
        if x.shape[1] > limit > 0:
            x = x[:, :limit]
        x = apply_fades(x, rate, float(job.get("fade_in_ms", 2)), float(job.get("fade_out_ms", 60)))
    peak = float(np.max(np.abs(x))) if x.size else 0.0
    if peak < 1e-4:
        raise ValueError("generated audio is silent")
    gain = db_to_lin(float(job.get("peak_db", -1.5))) / peak
    if "rms_db" in job:
        rms = float(np.sqrt(np.mean(x.astype(np.float64) ** 2)))
        if rms > 0:
            gain = min(gain, db_to_lin(float(job["rms_db"])) / rms)
    x = np.clip(x * gain, -1.0, 1.0)
    return np.round(x.T * 32767.0).astype("<i2"), rate


def write_wav(path: Path, pcm: np.ndarray, rate: int) -> None:
    """16-bit PCM WAV, (n, channels) int16. The header carries no timestamp, so equal audio gives equal bytes."""
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(".tmp")
    with wave.open(str(tmp), "wb") as w:
        w.setnchannels(pcm.shape[1])
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm.tobytes())
    tmp.replace(path)


def read_wav(path: Path) -> tuple[np.ndarray, int]:
    with wave.open(str(path), "rb") as w:
        n, ch, rate = w.getnframes(), w.getnchannels(), w.getframerate()
        data = np.frombuffer(w.readframes(n), dtype="<i2").reshape(-1, ch)
    return data, rate


# ----------------------------------------------------------------------------------------------------------- generation
class Generator:
    """Loads Stable Audio Open once and renders takes."""

    def __init__(self, model: str, threads: int | None, latent_mode: str) -> None:
        import torch
        self.torch = torch
        if threads:
            torch.set_num_threads(threads)
        from diffusers import StableAudioPipeline
        try:
            self.pipe = StableAudioPipeline.from_pretrained(model, torch_dtype=torch.float32).to("cpu")
        except Exception as e:  # noqa: BLE001 - huggingface_hub raises several types for the gate
            text = f"{type(e).__name__} {e}"
            if any(k in text for k in ("GatedRepo", "401", "403", "gated", "restricted", "Unauthorized")):
                print(GATE_HELP, file=sys.stderr)
                raise SystemExit(3) from e
            raise
        self.latent_mode = latent_mode
        self.rate = int(getattr(self.pipe.vae.config, "sampling_rate", MODEL_RATE))

    def render(self, job: dict, take: int, steps: int, scale: float) -> np.ndarray:
        """One take, (channels, n) float32 at the model rate."""
        torch = self.torch
        window = window_seconds(job, scale)
        gen = torch.Generator("cpu").manual_seed(seed_for(job["id"], take))
        latents = None
        if self.latent_mode == "short":
            channels = int(self.pipe.transformer.config.in_channels)
            latents = torch.randn((1, channels, latent_frames(window)), generator=gen, dtype=torch.float32)
        out = self.pipe(
            prompt=job["prompt"],
            negative_prompt=job["negative"],
            audio_end_in_s=window,
            num_inference_steps=steps,
            guidance_scale=float(job.get("cfg", 7.0)),
            num_waveforms_per_prompt=1,
            generator=gen,
            latents=latents,
        )
        return out.audios[0].detach().cpu().float().numpy()


def run_generate(args, data: dict) -> int:
    jobs = select_jobs(data["jobs"], args.only)
    if not jobs:
        print("no job matches --only", args.only)
        return 1
    steps = DRAFT_STEPS if args.draft and args.steps is None else (args.steps or DEFAULT_STEPS)
    takes_override = args.takes if args.takes is not None else (1 if args.draft else None)
    prefix = "draft" if args.draft else "take"
    todo = []
    for j in jobs:
        n = takes_override if takes_override is not None else j["takes"]
        for t in range(1, n + 1):
            if not (args.out / j["id"] / f"{prefix}{t}.wav").exists():
                todo.append((j, t))
    lo, hi = estimate_seconds([j for j, _ in todo], 1, steps, args.seconds_scale, args.latent_mode == "full")
    print(f"{len(jobs)} jobs, {len(todo)} takes to render ({prefix}, {steps} steps, scale {args.seconds_scale}); "
          f"UNVERIFIED estimate {fmt_hms(lo)} to {fmt_hms(hi)} on a laptop CPU")
    if args.dry_run or not todo:
        return 0
    gen = Generator(args.model, args.threads, args.latent_mode)
    failed = []
    t0 = time.time()
    for i, (j, t) in enumerate(todo, 1):
        started = time.time()
        try:
            audio = gen.render(j, t, steps, args.seconds_scale)
            pcm, rate = postprocess(audio, gen.rate, j, args.seconds_scale, args.rate)
            path = args.out / j["id"] / f"{prefix}{t}.wav"
            write_wav(path, pcm, rate)
            meta = {"id": j["id"], "take": t, "prompt": j["prompt"], "negative": j["negative"], "seed": seed_for(j["id"], t),
                    "steps": steps, "cfg": j.get("cfg", 7.0), "latent_mode": args.latent_mode, "model": args.model,
                    "seconds": round(pcm.shape[0] / rate, 3), "rate": rate}
            path.with_suffix(".json").write_text(json.dumps(meta, indent=1), encoding="utf-8")
            status = f"ok {pcm.shape[0] / rate:.2f}s"
        except Exception as e:  # noqa: BLE001 - one bad take must not stop the batch
            failed.append(f"{j['id']}#{t}")
            status = f"FAILED {type(e).__name__}: {e}"
        done = time.time() - t0
        eta = done / i * (len(todo) - i)
        print(f"[{i}/{len(todo)}] {j['id']} {prefix}{t}  {status}  ({time.time() - started:.0f}s; elapsed {fmt_hms(done)}, eta {fmt_hms(eta)})",
              flush=True)
    print("failed:", ", ".join(failed) if failed else "none")
    return 1 if failed else 0


def select_jobs(jobs: list[dict], only: list[str] | None) -> list[dict]:
    if not only:
        return jobs
    keys = [k for o in only for k in o.split(",") if k]
    return [j for j in jobs if any(k in j["id"] or k == j["category"] for k in keys)]


# ------------------------------------------------------------------------------------------------------------ picking
def parse_picks(items: list[str], picks_file: Path | None) -> dict[str, str]:
    """{id: "1" | "2" | "d1"}; items win over the file."""
    picks: dict[str, str] = {}
    if picks_file and picks_file.exists():
        with open(picks_file, "rb") as f:
            for k, v in tomllib.load(f).get("picks", {}).items():
                picks[k] = str(v)
    for it in items or []:
        if "=" not in it:
            raise ValueError(f"pick '{it}' must look like id=2")
        k, v = it.split("=", 1)
        picks[k.strip()] = v.strip()
    for k, v in picks.items():
        if not re.fullmatch(r"d?\d+", v):
            raise ValueError(f"pick {k}={v}: use a take number (2) or a draft (d1)")
    return picks


def take_path(out: Path, job_id: str, sel: str) -> Path:
    return out / job_id / (f"draft{sel[1:]}.wav" if sel.startswith("d") else f"take{sel}.wav")


def run_pick(args, data: dict) -> int:
    jobs = select_jobs(data["jobs"], args.only)
    picks = parse_picks(args.picks, args.picks_file)
    unknown = [k for k in picks if k not in {j["id"] for j in data["jobs"]}]
    if unknown:
        print("unknown ids in picks:", ", ".join(unknown))
        return 1
    args.sfx_dir.mkdir(parents=True, exist_ok=True)
    copied, missing = [], []
    for j in jobs:
        src = take_path(args.out, j["id"], picks.get(j["id"], "1"))
        if not src.exists():
            missing.append(j["id"])
            continue
        shutil.copyfile(src, args.sfx_dir / f"{j['id']}.wav")
        copied.append(j["id"])
        cap = MAX_LOOP_BYTES if j["loop"] else MAX_ONESHOT_BYTES
        if src.stat().st_size > cap:
            print(f"warning: {j['id']} is {src.stat().st_size} bytes, over the {cap} byte cap SoundBankTests allows; regenerate with a lower --rate or --seconds-scale")
    present = [j["id"] for j in data["jobs"] if (args.sfx_dir / f"{j['id']}.wav").exists()]
    text = args.sounds.read_text(encoding="utf-8")
    new, notes = update_sounds_toml(text, data["jobs"], set(present), args.mix, args.replace_sfxr, data.get("event_defaults"))
    if new != text:
        args.sounds.write_text(new, encoding="utf-8", newline="\n")
    print(f"copied {len(copied)} file(s); {len(missing)} job(s) without a generated take (skipped); {len(present)} generated sound(s) in {args.sfx_dir}")
    for n in notes:
        print("note:", n)
    print("sounds.toml", "updated" if new != text else "unchanged")
    return 0


def _section_span(lines: list[str], header: str) -> tuple[int, int] | None:
    """Line range [start, end) of the TOML table whose header line is ``header``."""
    for i, ln in enumerate(lines):
        if ln.strip() == header:
            j = i + 1
            while j < len(lines) and not lines[j].lstrip().startswith("["):
                j += 1
            return i, j
    return None


GENERIC_EVENT = {"volume": 1.0, "positional": True, "cooldown_ms": 0}


def _section_values(lines: list[str], header: str) -> dict:
    """volume / positional / cooldown_ms of an existing [event.x] section (empty dict when there is none)."""
    span = _section_span(lines, header)
    out: dict = {}
    if span is None:
        return out
    for k in range(span[0] + 1, span[1]):
        m = re.match(r"^\s*(volume|positional|cooldown_ms)\s*=\s*([^#\s]+)", lines[k])
        if m:
            v = m.group(2)
            out[m.group(1)] = (v == "true") if m.group(1) == "positional" else (float(v) if m.group(1) == "volume" else int(float(v)))
    return out


def update_sounds_toml(text: str, jobs: list[dict], present: set[str], mix: bool = False, replace_sfxr: bool = False,
                       event_defaults: dict | None = None) -> tuple[str, list[str]]:
    """Return (new sounds.toml text, notes). Idempotent; references only sounds whose file is in ``present``.

    An event key in a job's ``events`` is either an existing generic ``[event.x]`` (the sound is added as one more random variant) or a
    new/qualified key such as ``unit_died.infantry.directorate`` (S22): the tool writes ``[event.unit_died.infantry.directorate]`` into the managed
    block, with volume / positional / cooldown_ms copied from the plain event (``[event.unit_died]`` or ``[event_defaults.unit_died]`` of the jobs file).
    A job with ``ambience = "glass_coast"`` gets an ``[ambience.glass_coast]`` entry. The game looks events up most-specific-first, see the top of sounds.toml.
    """
    notes: list[str] = []
    event_defaults = event_defaults or {}
    # 1. drop the managed block
    lines = text.split("\n")
    if MARK_BEGIN in lines and MARK_END in lines:
        a, b = lines.index(MARK_BEGIN), lines.index(MARK_END)
        del lines[a:b + 1]
        while lines and lines[-1] == "":
            lines.pop()
    job_ids = {j["id"] for j in jobs}
    # 2. events
    wanted: dict[str, list[str]] = {}
    for j in jobs:
        if j["id"] not in present or j["repoint"]:
            continue
        evs = list(j["events"]) + (list(j["mix_into"]) if mix else [])
        for e in evs:
            wanted.setdefault(e, []).append(j["id"])
    all_events = {e for j in jobs for e in list(j["events"]) + list(j["mix_into"])}
    new_events: dict[str, list[str]] = {}
    for e in sorted(all_events):
        span = _section_span(lines, f"[event.{e}]")
        if span is None:
            if e in wanted:
                new_events[e] = wanted[e]
            continue
        s, t = span
        for k in range(s + 1, t):
            m = re.match(r"^(\s*sounds\s*=\s*)\[(.*)\](.*)$", lines[k])
            if not m:
                continue
            ids = re.findall(r'"([^"]*)"', m.group(2))
            keep = [i for i in ids if i not in job_ids]
            gen = wanted.get(e, [])
            if replace_sfxr and gen:
                keep = []
            new_ids = keep + gen
            if not new_ids:
                new_ids = ids          # never leave an event empty
            lines[k] = m.group(1) + "[" + ", ".join(f'"{i}"' for i in new_ids) + "]" + m.group(3)
            break
    # 3. repoint menu sounds
    for j in jobs:
        if not j["repoint"] or j["id"] not in present:
            continue
        span = _section_span(lines, f"[sound.{j['repoint']}]")
        if span is None:
            notes.append(f"[sound.{j['repoint']}] not found; {j['id']} not wired")
            continue
        s, t = span
        for k in range(s + 1, t):
            m = re.match(r'^(\s*file\s*=\s*)"([^"]*)"(.*)$', lines[k])
            if m:
                was = re.search(r"#\s*was\s+(\S+)", m.group(3))
                old = was.group(1) if was else m.group(2)
                if old.startswith(f"sfx/{j['id']}"):
                    old = ""
                lines[k] = f'{m.group(1)}"{j["file"]}"' + (f"   # was {old} (sfxr, M10-10)" if old else m.group(3))
    # 4. managed block
    block = [MARK_BEGIN, "# Stable Audio Open sounds (M10-10). Generated by --pick from tools/audio/sfx/sfx_jobs.toml.", ""]
    for j in jobs:
        if j["id"] not in present or j["repoint"]:
            continue
        block += [f"[sound.{j['id']}]", f'file = "{j["file"]}"', f"volume = {float(j['volume']):g}", f"max_voices = {int(j['max_voices'])}", ""]
    for e, ids in new_events.items():
        base = e.split(".")[0]
        vals = {**GENERIC_EVENT, **event_defaults.get(base, {}), **_section_values(lines, f"[event.{base}]")}
        block += [f"[event.{e}]", "sounds = [" + ", ".join(f'"{i}"' for i in ids) + "]", f"volume = {float(vals['volume']):g}",
                  f"positional = {'true' if vals['positional'] else 'false'}", f"cooldown_ms = {int(vals['cooldown_ms'])}", ""]
    for j in jobs:
        if j.get("ambience") and j["id"] in present:
            block += [f"[ambience.{j['ambience']}]", f'sound = "{j["id"]}"', f"volume = {float(event_defaults.get('ambience', {}).get('volume', 0.5)):g}", ""]
    block.append(MARK_END)
    while lines and lines[-1] == "":
        lines.pop()
    out = "\n".join(lines) + "\n\n" + "\n".join(block) + "\n"
    return out, notes


# ---------------------------------------------------------------------------------------------------------- audition
def run_audition(args, data: dict) -> int:
    args.out.mkdir(parents=True, exist_ok=True)
    rel_sfx = Path("..") / ".." / ".." / "data" / "audio" / "sfx"
    cats: dict[str, list[dict]] = {}
    for j in data["jobs"]:
        cats.setdefault(j["category"], []).append(j)
    parts = []
    for cat, js in cats.items():
        parts.append(f'<h2 id="{html.escape(cat)}">{html.escape(js[0]["category_label"])} <small>({len(js)})</small></h2>')
        for j in js:
            takes = sorted((args.out / j["id"]).glob("take*.wav")) + sorted((args.out / j["id"]).glob("draft*.wav"))
            old = ""
            if (args.sfx_dir / f"{j['id']}.wav").exists():
                old = f'<div class="t"><b>in game now</b><audio controls preload="none" src="{rel_sfx.as_posix()}/{html.escape(j["id"])}.wav"></audio></div>'
            cells = []
            for tk in takes:
                sel = ("d" + tk.stem[5:]) if tk.stem.startswith("draft") else tk.stem[4:]
                cells.append(
                    f'<div class="t"><label><input type="radio" name="{html.escape(j["id"])}" value="{sel}"> {html.escape(tk.stem)}</label>'
                    f'<audio controls preload="none" src="{html.escape(j["id"])}/{html.escape(tk.name)}"></audio></div>')
            if not cells:
                cells.append('<div class="t none">not generated yet</div>')
            meta = []
            if j["events"]:
                meta.append("plays as: " + ", ".join(j["events"]))
            if j.get("ambience"):
                meta.append("ambience loop: " + j["ambience"])
            if j["mix_into"]:
                meta.append("can mix into: " + ", ".join(j["mix_into"]))
            if j["planned"]:
                meta.append("note: " + j["planned"])
            if j["repoint"]:
                meta.append("replaces menu sound " + j["repoint"])
            parts.append(
                f'<section class="job"><h3>{html.escape(j["id"])}</h3><p class="p">{html.escape(j["prompt"])}</p>'
                f'<p class="m">{html.escape(" | ".join(meta))}</p><div class="row">{old}{"".join(cells)}</div></section>')
    page = f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>RTS_2027 SFX audition</title>
<style>
:root{{--bg:#fff;--fg:#1d1d1f;--mut:#666;--line:#ddd;--card:#f6f6f7}}
@media(prefers-color-scheme:dark){{:root{{--bg:#16171a;--fg:#e8e8ea;--mut:#9a9aa0;--line:#33353a;--card:#202226}}}}
body{{background:var(--bg);color:var(--fg);font:15px/1.4 system-ui,sans-serif;margin:0 auto;max-width:1100px;padding:16px}}
h2{{margin:28px 0 8px;border-bottom:1px solid var(--line)}}h2 small{{color:var(--mut);font-weight:normal}}
.job{{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:10px 12px;margin:10px 0}}
.job h3{{margin:0;font:600 15px ui-monospace,monospace}}.p{{margin:4px 0}}.m{{margin:2px 0 6px;color:var(--mut);font-size:13px}}
.row{{display:flex;flex-wrap:wrap;gap:10px}}.t{{display:flex;flex-direction:column;gap:2px}}.none{{color:var(--mut)}}
audio{{height:34px;max-width:100%}}textarea{{width:100%;height:9em;font:13px ui-monospace,monospace;background:var(--card);color:var(--fg)}}
nav a{{margin-right:10px}}button{{padding:6px 12px}}
</style></head><body>
<h1>RTS_2027 SFX audition</h1>
<p>Listen, tick the take you like for each sound, then press <b>Make picks.toml</b> and save the text as
<code>tools/audio/sfx/picks.toml</code> (or run <code>make_sfx.ps1 -Pick</code> with the <code>id=2</code> form). Untouched sounds use take 1.</p>
<nav>{"".join(f'<a href="#{html.escape(c)}">{html.escape(c)}</a>' for c in cats)}</nav>
{"".join(parts)}
<h2>picks.toml</h2><button id="mk">Make picks.toml</button><textarea id="out" readonly></textarea>
<script>
document.getElementById('mk').onclick=function(){{
  var seen={{}},lines=['[picks]'];
  document.querySelectorAll('input[type=radio]:checked').forEach(function(r){{lines.push(r.name+' = "'+r.value+'"');}});
  document.getElementById('out').value=lines.join('\\n')+'\\n';
}};
document.addEventListener('play',function(e){{document.querySelectorAll('audio').forEach(function(a){{if(a!==e.target)a.pause();}});}},true);
</script></body></html>
"""
    (args.out / "index.html").write_text(page, encoding="utf-8")
    print("wrote", args.out / "index.html")
    return 0


def run_list(args, data: dict) -> int:
    jobs = select_jobs(data["jobs"], args.only)
    steps = DRAFT_STEPS if args.draft else (args.steps or DEFAULT_STEPS)
    cats: dict[str, int] = {}
    for j in jobs:
        cats[j["category"]] = cats.get(j["category"], 0) + 1
        have = len(list((args.out / j["id"]).glob("take*.wav")))
        print(f"{j['id']:24} {j['category']:11} {j['seconds'] * args.seconds_scale:5.1f}s x{j['takes']}  have {have}  "
              f"{','.join(j['events']) or '-':24} {j['planned']}")
    lo, hi = estimate_seconds(jobs, args.takes, steps, args.seconds_scale, args.latent_mode == "full")
    total = sum(args.takes if args.takes is not None else j["takes"] for j in jobs)
    print(f"\n{len(jobs)} jobs, {total} takes; per category: {cats}")
    print(f"UNVERIFIED CPU estimate at {steps} steps: {fmt_hms(lo)} to {fmt_hms(hi)} (all takes, none skipped)")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--jobs", type=Path, default=DEFAULT_JOBS)
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT, help="take folder (git-ignored)")
    ap.add_argument("--only", nargs="+", help="job ids containing this text, or a category name (several allowed)")
    ap.add_argument("--takes", type=int, help="takes per job (default: the job's own, normally 3)")
    ap.add_argument("--steps", type=int, help=f"denoising steps (default {DEFAULT_STEPS})")
    ap.add_argument("--draft", action="store_true", help=f"fast preview: {DRAFT_STEPS} steps, 1 take, files named draft<N>.wav")
    ap.add_argument("--seconds-scale", type=float, default=1.0, help="multiply every job's length (0.5 = half)")
    ap.add_argument("--latent-mode", choices=("short", "full"), default="short", help="short = noise sized to the clip (fast); full = stock 47 s window (slow)")
    ap.add_argument("--rate", type=int, help=f"override the output sample rate (default {OUTPUT_RATE} for every job; e.g. 32000 for a crisper ambience loop)")
    ap.add_argument("--threads", type=int, help="torch CPU threads (default: all)")
    ap.add_argument("--model", default=MODEL_ID)
    ap.add_argument("--dry-run", action="store_true", help="count and estimate, do not load the model")
    ap.add_argument("--list", action="store_true", help="print the jobs and the estimate, then exit")
    ap.add_argument("--audition", action="store_true", help="write out/index.html")
    ap.add_argument("--pick", action="store_true", help="copy chosen takes to data/audio/sfx and update sounds.toml")
    ap.add_argument("--picks", nargs="*", default=[], metavar="id=N", help="with --pick: choices such as wpn_flame=2 gui_back=d1")
    ap.add_argument("--picks-file", type=Path, default=DEFAULT_PICKS)
    ap.add_argument("--mix", action="store_true", help="with --pick: also add the mix_into sounds as random variants of the generic events")
    ap.add_argument("--replace-sfxr", action="store_true", help="with --pick: drop the sfxr variants from events that got a generated sound")
    ap.add_argument("--sfx-dir", type=Path, default=DEFAULT_SFX_DIR)
    ap.add_argument("--sounds", type=Path, default=DEFAULT_SOUNDS)
    args = ap.parse_args(argv)
    data = load_jobs(args.jobs)
    if args.list:
        return run_list(args, data)
    if args.audition:
        return run_audition(args, data)
    if args.pick:
        return run_pick(args, data)
    return run_generate(args, data)


if __name__ == "__main__":
    sys.exit(main())
