#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Seeded, deterministic sfxr-style sound generator (D-080). Our own code; the output is CC0.

Reads the sound presets from presets.toml and writes 16-bit mono PCM WAV files. Same preset and seed give the same
bytes: the only randomness is ``random.Random(seed)`` (noise and crackle), the WAV header has no timestamps, and the
synthesis uses plain Python float maths (standard library only, no numpy).

Usage:
  python tools/audio/sfxr.py generate [--presets FILE] [--out DIR] [--only NAME ...]
  python tools/audio/sfxr.py list [--presets FILE]

Preset keys (all optional except ``wave``; times in seconds, frequencies in Hz):
  wave          square | saw | sine | triangle | noise
  seed          integer, default 1 (noise / crackle stream)
  freq          start frequency
  slide         pitch slide in octaves per second (negative falls)
  slide_accel   change of the slide per second
  min_freq      the sound stops sliding below this (default 20)
  duty, duty_sweep   square duty cycle 0..1 and its change per second
  vib_depth, vib_rate   vibrato: fraction of the frequency and its rate in Hz
  arp_mult, arp_time    multiply the frequency once after arp_time seconds
  attack, sustain, decay   amplitude envelope; punch adds a boost (fraction) at the start of the sustain
  lpf, lpf_sweep, hpf   one-pole filters (cutoff Hz, 0 = off); lpf_sweep multiplies the cutoff per second
  noise_mix     0..1 white noise blended into any wave
  am_depth, am_rate     amplitude modulation depth 0..1 and rate in Hz
  crackle_rate  random pops per second (noise wave only); crackle_decay is the pop length in seconds
  repeat        retrigger the whole voice every N seconds (sfxr "repeat speed"), 0 = off
  gain          output level 0..1 (default 0.8)
  rate          sample rate (default from [meta])
"""
from __future__ import annotations

import argparse
import math
import random
import sys
import tomllib
import wave
from pathlib import Path

DEFAULT_RATE = 22050
HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
DEFAULT_PRESETS = HERE / "presets.toml"
DEFAULT_OUT = REPO / "data" / "audio" / "sfx"
WAVES = ("square", "saw", "sine", "triangle", "noise")


def _osc(kind: str, phase: float, duty: float, held_noise: float) -> float:
    """One oscillator sample for a phase in [0, 1)."""
    if kind == "square":
        return 1.0 if phase < duty else -1.0
    if kind == "saw":
        return 1.0 - 2.0 * phase
    if kind == "sine":
        return math.sin(2.0 * math.pi * phase)
    if kind == "triangle":
        return 4.0 * phase - 1.0 if phase < 0.5 else 3.0 - 4.0 * phase
    return held_noise


def synth(p: dict, default_rate: int = DEFAULT_RATE) -> list[int]:
    """Renders one preset to a list of signed 16-bit samples."""
    kind = p.get("wave", "square")
    if kind not in WAVES:
        raise ValueError(f"unknown wave {kind!r}")
    rate = int(p.get("rate", default_rate))
    rng = random.Random(int(p.get("seed", 1)))
    attack = float(p.get("attack", 0.0))
    sustain = float(p.get("sustain", 0.1))
    decay = float(p.get("decay", 0.1))
    punch = float(p.get("punch", 0.0))
    total = attack + sustain + decay
    n = max(1, int(total * rate))

    freq0 = float(p.get("freq", 440.0))
    slide = float(p.get("slide", 0.0))
    accel = float(p.get("slide_accel", 0.0))
    min_freq = float(p.get("min_freq", 20.0))
    duty0 = float(p.get("duty", 0.5))
    duty_sweep = float(p.get("duty_sweep", 0.0))
    vib_depth = float(p.get("vib_depth", 0.0))
    vib_rate = float(p.get("vib_rate", 0.0))
    arp_mult = float(p.get("arp_mult", 1.0))
    arp_time = float(p.get("arp_time", 0.0))
    lpf = float(p.get("lpf", 0.0))
    lpf_sweep = float(p.get("lpf_sweep", 1.0))
    hpf = float(p.get("hpf", 0.0))
    noise_mix = float(p.get("noise_mix", 0.0))
    am_depth = float(p.get("am_depth", 0.0))
    am_rate = float(p.get("am_rate", 0.0))
    crackle_rate = float(p.get("crackle_rate", 0.0))
    crackle_decay = max(0.001, float(p.get("crackle_decay", 0.01)))
    repeat = float(p.get("repeat", 0.0))
    gain = float(p.get("gain", 0.8))
    repeat_n = int(repeat * rate) if repeat > 0 else 0

    phase = 0.0
    held = rng.uniform(-1.0, 1.0)
    lp_state = 0.0
    hp_state = 0.0
    hp_prev = 0.0
    pop = 0.0
    pop_decay = math.exp(-1.0 / (crackle_decay * rate))
    out: list[int] = []
    for i in range(n):
        t = i / rate
        if repeat_n and i % repeat_n == 0 and i > 0:
            phase = 0.0
        tt = t if not repeat_n else (i % repeat_n) / rate
        # Pitch.
        octaves = slide * tt + 0.5 * accel * tt * tt
        f = freq0 * (2.0 ** max(-12.0, min(12.0, octaves)))
        if arp_time > 0.0 and tt >= arp_time:
            f *= arp_mult
        if vib_depth:
            f *= 1.0 + vib_depth * math.sin(2.0 * math.pi * vib_rate * t)
        f = max(f, min_freq)
        # Oscillator.
        phase += f / rate
        if phase >= 1.0:
            phase -= math.floor(phase)
            held = rng.uniform(-1.0, 1.0)
        duty = min(0.95, max(0.05, duty0 + duty_sweep * tt))
        s = _osc(kind, phase, duty, held)
        if crackle_rate > 0.0:
            if rng.random() < crackle_rate / rate:
                pop = rng.uniform(0.4, 1.0)
            s = rng.uniform(-1.0, 1.0) * pop
            pop *= pop_decay
        if noise_mix:
            s = s * (1.0 - noise_mix) + rng.uniform(-1.0, 1.0) * noise_mix
        # Filters (one pole).
        if lpf > 0.0:
            cutoff = min(rate * 0.45, lpf * (lpf_sweep ** t))
            a = 1.0 - math.exp(-2.0 * math.pi * cutoff / rate)
            lp_state += a * (s - lp_state)
            s = lp_state
        if hpf > 0.0:
            a = math.exp(-2.0 * math.pi * hpf / rate)
            hp_state = a * (hp_state + s - hp_prev)
            hp_prev = s
            s = hp_state
        # Envelope.
        if t < attack:
            env = t / attack
        elif t < attack + sustain:
            env = 1.0 + punch * (1.0 - (t - attack) / max(sustain, 1e-9))
        else:
            env = max(0.0, 1.0 - (t - attack - sustain) / max(decay, 1e-9))
            env *= env
        if am_depth:
            env *= 1.0 - am_depth * (0.5 + 0.5 * math.sin(2.0 * math.pi * am_rate * t))
        v = max(-1.0, min(1.0, s * env * gain))
        out.append(int(round(v * 32767.0)))
    return out


def write_wav(path: Path, samples: list[int], rate: int) -> None:
    """Writes 16-bit mono PCM. The ``wave`` module writes a fixed 44-byte header: no timestamps, so output is reproducible."""
    path.parent.mkdir(parents=True, exist_ok=True)
    data = b"".join(int(s).to_bytes(2, "little", signed=True) for s in samples)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(data)


def load_presets(path: Path) -> tuple[int, dict[str, dict]]:
    with open(path, "rb") as f:
        doc = tomllib.load(f)
    rate = int(doc.get("meta", {}).get("sample_rate", DEFAULT_RATE))
    sounds = doc.get("sound", {})
    return rate, {name: sounds[name] for name in sorted(sounds)}


def generate(presets: Path, out: Path, only: list[str] | None = None) -> list[Path]:
    rate, sounds = load_presets(presets)
    written = []
    for name, p in sounds.items():
        if only and name not in only:
            continue
        target = out / f"{name}.wav"
        write_wav(target, synth(p, rate), int(p.get("rate", rate)))
        written.append(target)
    return written


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    g = sub.add_parser("generate", help="write the WAV files")
    g.add_argument("--presets", type=Path, default=DEFAULT_PRESETS)
    g.add_argument("--out", type=Path, default=DEFAULT_OUT)
    g.add_argument("--only", nargs="*")
    ls = sub.add_parser("list", help="list the preset names")
    ls.add_argument("--presets", type=Path, default=DEFAULT_PRESETS)
    args = ap.parse_args(argv)
    if args.cmd == "list":
        for name in load_presets(args.presets)[1]:
            print(name)
        return 0
    files = generate(args.presets, args.out, args.only)
    for f in files:
        print(f"{f.name}  {f.stat().st_size} bytes")
    print(f"{len(files)} sounds, {sum(f.stat().st_size for f in files)} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
