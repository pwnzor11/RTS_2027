# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) 2026 RTS_2027 contributors
"""Generate the campaign radio voice lines through a Wyoming Piper server (e.g. wyoming-piper on a NAS).

Offline build tool: never run by the game. Reads every data/missions/*/locale.toml [voice] table (key -> "voice/<leader>/<key>.ogg"),
looks up the leader's voice in tools/voice/leaders.json, asks the server to speak the line, and writes
  tools/voice/out/<leader>/<key>.wav            (always; git-ignored)
  data/audio/voice/<leader>/<key>.ogg           (with --ogg; needs ffmpeg on PATH)

Usage (from the RTS_2027 folder, Python 3.11+, `pip install wyoming`):
  python tools/voice/wyoming_batch.py --host NAS_IP --list                      # voices the server offers
  python tools/voice/wyoming_batch.py --host NAS_IP --audition en_GB-alba-medium en_US-amy-medium ...
                                                                               # one sample line per voice -> tools/voice/audition/
  python tools/voice/wyoming_batch.py --host NAS_IP                             # all lines, WAV only
  python tools/voice/wyoming_batch.py --host NAS_IP --ogg                       # all lines, WAV + game OGG
  python tools/voice/wyoming_batch.py --host NAS_IP --only leader_dir_larkin    # one leader
"""
import argparse
import asyncio
import glob
import json
import os
import shutil
import subprocess
import sys
import tomllib
import wave

try:
    from wyoming.audio import AudioChunk, AudioStart, AudioStop
    from wyoming.client import AsyncTcpClient
    from wyoming.info import Describe, Info
    from wyoming.tts import Synthesize, SynthesizeVoice
except ImportError:
    sys.exit("Missing package: run  pip install wyoming")

def _find_root():
    """The RTS_2027 folder: the first folder above this script (or the current folder) that holds data/missions."""
    for start in (os.path.dirname(os.path.abspath(__file__)), os.getcwd()):
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
VOICE_DIR = os.path.dirname(os.path.abspath(__file__))
AUDITION_TEXT = ("Wrenfield. Directorate raiders are working the roads. "
                 "Put two Turrets on the crossing and keep the yard standing, Commander.")


def load_lines():
    """[(leader, key, text, ogg_rel_path)] from every mission locale file, in a stable order."""
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


async def synth(host, port, voice, text):
    """(pcm bytes, rate, width, channels) for one line."""
    client = AsyncTcpClient(host, port)
    await client.connect()
    try:
        await client.write_event(Synthesize(text=text, voice=SynthesizeVoice(name=voice)).event())
        pcm, fmt = bytearray(), None
        while True:
            event = await client.read_event()
            if event is None:
                raise RuntimeError("server closed the connection")
            if AudioStart.is_type(event.type):
                s = AudioStart.from_event(event)
                fmt = (s.rate, s.width, s.channels)
            elif AudioChunk.is_type(event.type):
                c = AudioChunk.from_event(event)
                fmt = fmt or (c.rate, c.width, c.channels)
                pcm += c.audio
            elif AudioStop.is_type(event.type):
                break
        if not fmt or not pcm:
            raise RuntimeError("no audio returned")
        return bytes(pcm), *fmt
    finally:
        await client.disconnect()


def write_wav(path, pcm, rate, width, channels):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(channels)
        w.setsampwidth(width)
        w.setframerate(rate)
        w.writeframes(pcm)


def to_ogg(wav_path, ogg_path):
    os.makedirs(os.path.dirname(ogg_path), exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav_path, "-c:a", "libvorbis", "-q:a", "5", ogg_path], check=True)


async def list_voices(host, port):
    client = AsyncTcpClient(host, port)
    await client.connect()
    try:
        await client.write_event(Describe().event())
        while True:
            event = await client.read_event()
            if event is None:
                sys.exit("server closed the connection")
            if Info.is_type(event.type):
                break
    finally:
        await client.disconnect()
    info = Info.from_event(event)
    for tts in info.tts:
        for v in tts.voices:
            langs = ",".join(v.languages or [])
            if langs.startswith("en"):
                print(f"{v.name:40} {langs:10} installed={v.installed}")


async def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--host", required=True)
    ap.add_argument("--port", type=int, default=10200)
    ap.add_argument("--list", action="store_true", help="list English voices on the server")
    ap.add_argument("--audition", nargs="+", metavar="VOICE", help="speak one sample line in each voice")
    ap.add_argument("--only", help="only this leader id")
    ap.add_argument("--ogg", action="store_true", help="also write the game's OGG files (needs ffmpeg)")
    a = ap.parse_args()

    if a.list:
        await list_voices(a.host, a.port)
        return
    if a.audition:
        for v in a.audition:
            out = os.path.join(VOICE_DIR, "audition", f"{v}.wav")
            write_wav(out, *await synth(a.host, a.port, v, AUDITION_TEXT))
            print(f"wrote {out}")
        return

    cfg_path = os.path.join(VOICE_DIR, "leaders.json")
    with open(cfg_path, encoding="utf-8") as f:
        leaders = json.load(f)
    if a.ogg and not shutil.which("ffmpeg"):
        sys.exit("--ogg needs ffmpeg on PATH (winget install ffmpeg)")
    lines = load_lines()
    missing = sorted({l for l, *_ in lines if not leaders.get(l, {}).get("voice")})
    if missing:
        sys.exit(f"set a voice in {cfg_path} for: {', '.join(missing)}")
    done = 0
    for leader, key, text, rel in lines:
        if a.only and leader != a.only:
            continue
        voice = leaders[leader]["voice"]
        wav = os.path.join(VOICE_DIR, "out", leader, f"{key}.wav")
        write_wav(wav, *await synth(a.host, a.port, voice, text))
        if a.ogg:
            to_ogg(wav, os.path.join(ROOT, "data", "audio", *rel.split("/")))
        done += 1
        print(f"[{done}] {leader} {key}")
    print(f"done: {done} lines (voices from {cfg_path})")


if __name__ == "__main__":
    asyncio.run(main())
