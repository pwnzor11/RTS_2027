#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Check data/audio/PROVENANCE.toml against the .wav / .ogg files under data/audio and data/music (M13-47).

Exit 1 and list every problem when: a file has no entry; an entry has no file; an entry is missing path, source or
licence; an entry is listed twice; the source or licence is "unknown" or not in the allowed list; or the licence is
not the one docs/CREDITS.md gives for that source. Otherwise print a one-line summary with the counts per source.

Usage:  python3 tools/audio/check_provenance.py
"""

import sys
import tomllib
from collections import Counter
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from make_provenance import LICENCE, UNKNOWN, UNKNOWN_LICENCE  # noqa: E402

ROOT = HERE.parents[1]
DATA = ROOT / "data"
PROVENANCE = DATA / "audio" / "PROVENANCE.toml"
REQUIRED = ("path", "source", "licence")
ALLOWED_SOURCES = set(LICENCE)
ALLOWED_LICENCES = set(LICENCE.values())


def media_files() -> set[str]:
    """Every .wav / .ogg under data/audio and data/music, relative to data/."""
    found = set()
    for top in ("audio", "music"):
        for path in (DATA / top).rglob("*"):
            if path.is_file() and path.suffix in (".wav", ".ogg"):
                found.add(path.relative_to(DATA).as_posix())
    return found


def main() -> int:
    problems: list[str] = []
    if not PROVENANCE.is_file():
        print(f"FAIL: {PROVENANCE.relative_to(ROOT).as_posix()} is missing (run tools/audio/make_provenance.py)")
        return 1

    entries = tomllib.loads(PROVENANCE.read_text(encoding="utf-8")).get("file", [])
    seen: Counter[str] = Counter()
    per_source: Counter[str] = Counter()
    for index, item in enumerate(entries, start=1):
        for key in REQUIRED:
            if key not in item:
                problems.append(f"entry {index}: missing key '{key}' ({item.get('path', '?')})")
        path = item.get("path", "")
        source = item.get("source", "")
        licence = item.get("licence", "")
        seen[path] += 1
        per_source[source or "?"] += 1
        if path and not (DATA / path).is_file():
            problems.append(f"{path}: entry has no file under data/")
        if source == UNKNOWN or source not in ALLOWED_SOURCES:
            problems.append(f"{path}: source '{source}' is not an allowed source")
        if licence == UNKNOWN_LICENCE or licence not in ALLOWED_LICENCES:
            problems.append(f"{path}: licence '{licence}' is not an allowed licence")
        elif source in LICENCE and licence != LICENCE[source]:
            problems.append(f"{path}: licence does not match source '{source}'")

    for path, count in sorted(seen.items()):
        if count > 1:
            problems.append(f"{path}: {count} entries (need exactly one)")

    files = media_files()
    for path in sorted(files - set(seen)):
        problems.append(f"{path}: audio file has no entry in PROVENANCE.toml")

    for problem in problems:
        print(f"FAIL: {problem}")
    counts = ", ".join(f"{k} {per_source[k]}" for k in sorted(per_source))
    status = "ok" if not problems else f"{len(problems)} problem(s)"
    print(f"provenance: {len(files)} files, {len(entries)} entries, {status} ({counts})")
    return 1 if problems else 0


if __name__ == "__main__":
    raise SystemExit(main())
