# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) 2026 RTS_2027 contributors
"""Add a [voice] slot to every data/missions/*/locale.toml for each radio line a leader speaks (M8-18).

A "radio line" is a key passed to Dialogue.Say(speaker, portrait, key) in data/missions/*/mission.lua whose speaker is a leader
listed in tools/voice/leaders.json. For each one this script appends
    "<key>" = "voice/<speaker>/<key>.ogg"
to the locale file's [voice] table (created at the end of the file when missing). Existing [voice] entries and all text lines are
never changed or reordered; running the script twice changes nothing.

How the speaker and key are found (plain text scan of the Lua, no Lua runtime):
  - speaker: a quoted id, or a name declared in the same file as  local NAME = "leader_xxx"
  - key: a quoted literal, or a quoted prefix followed by  ..  (e.g. "lb2.radio.post" .. n): every locale key that starts with the
    prefix and has no further dot is used. A key held in a variable (tut_01's wrapper `Say(key)`) cannot be resolved by the scan;
    such a mission is listed by hand in EXPLICIT_KEYS, and a wrapper call missing from that list is reported.
  - a key with no text line in the locale file is reported and skipped (the game would show "[key]").

Usage (from the repo root, Python 3.11+):
  python tools/voice/voice_slots.py            # write the slots
  python tools/voice/voice_slots.py --check    # change nothing; exit 1 when a slot is missing (CI-style)
"""
import argparse
import glob
import json
import os
import re
import sys
import tomllib

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

SAY_RE = re.compile(r'Dialogue\.Say\(\s*(?P<spk>"[^"]*"|[A-Za-z_]\w*)\s*,\s*(?P<por>"[^"]*"|[A-Za-z_]\w*)\s*,\s*'
                    r'(?P<key>"(?P<lit>[^"]*)"(?P<cat>\s*\.\.)?|[A-Za-z_]\w*)')
CONST_RE = re.compile(r'^\s*local\s+([A-Za-z_]\w*)\s*=\s*"([^"]*)"\s*(?:--.*)?$', re.M)
# A mission's own wrapper, e.g. tut_01's  function Say(key) Dialogue.Say("instructor", "instructor", key) end  called as Say("intro").
# Its literal keys are checked against EXPLICIT_KEYS below; a call with a run-time suffix (Say("hint." .. StepId(n))) is a prefix.
WRAPPER_CALL_RE = re.compile(r'(?<![.\w])Say\(\s*"(?P<lit>[^"]*)"(?P<cat>\s*\.\.)?')

# Keys a mission passes to Dialogue.Say through a variable, which the scan above cannot resolve. Each entry lists the speaker (a
# leader id in leaders.json) and every key the script can speak, written out by hand from mission.lua. The keys are checked
# against the locale text, and a wrapper call whose key is not covered here is reported.
EXPLICIT_KEYS = {
    # tut_01: the instructor is unnamed on purpose (SpeakerNames shows "Instructor"); the Lua speaker id stays "instructor".
    "tut_01": ("leader_tut_instructor", [
        "intro", "done",
        "step.move", "step.attack", "step.power", "step.barracks", "step.train", "step.harvest", "step.fight",
        "hint.move", "hint.attack", "hint.power", "hint.barracks", "hint.train", "hint.harvest", "hint.fight",
    ]),
}


def locale_keys(text):
    """(all text keys, existing voice keys) for one locale file."""
    data = tomllib.loads(text)
    voice = data.get("voice", {})
    keys = [k for k, v in data.items() if isinstance(v, str)]
    return keys, set(voice)


def collect(mission_dir, leaders, problems):
    """[(speaker, key)] for one mission folder, in source order."""
    lua_path = os.path.join(mission_dir, "mission.lua")
    loc_path = os.path.join(mission_dir, "locale.toml")
    if not (os.path.isfile(lua_path) and os.path.isfile(loc_path)):
        return []
    lua = open(lua_path, encoding="utf-8").read()
    consts = dict(CONST_RE.findall(lua))
    with open(loc_path, "rb") as f:
        text_keys = [k for k, v in tomllib.load(f).items() if isinstance(v, str)]
    mission = os.path.basename(mission_dir)
    out = []
    explicit = EXPLICIT_KEYS.get(mission)
    if explicit:
        spk, keys = explicit
        if spk not in leaders:
            problems.append(f"{mission}: EXPLICIT_KEYS speaker '{spk}' is not in leaders.json (no voice slot)")
        for k in keys:
            if k not in text_keys:
                problems.append(f"{mission}: key '{k}' has no text line (skipped)")
            else:
                out.append((spk, k))
        for m in WRAPPER_CALL_RE.finditer(lua):
            lit = m.group("lit")
            covered = any(k.startswith(lit) for k in keys) if m.group("cat") else lit in keys
            if not covered:
                problems.append(f"{mission}: Say(\"{lit}\") is not in EXPLICIT_KEYS (no voice slot)")
    for m in SAY_RE.finditer(lua):
        if explicit and m.group("lit") is None:
            continue   # a variable key, listed in EXPLICIT_KEYS above
        spk = m.group("spk")
        spk = spk.strip('"') if spk.startswith('"') else consts.get(spk)
        if spk is None:
            problems.append(f"{mission}: speaker {m.group('spk')} is not a quoted id or a local constant")
            continue
        if spk not in leaders:
            problems.append(f"{mission}: speaker '{spk}' is not in leaders.json (no voice slot)")
            continue
        lit = m.group("lit")
        if lit is None:
            problems.append(f"{mission}: key is a variable ({m.group('key')}); cannot add a slot for speaker {spk}")
            continue
        if m.group("cat"):
            keys = [k for k in text_keys if k.startswith(lit) and "." not in k[len(lit):]]
            if not keys:
                problems.append(f"{mission}: no locale key starts with '{lit}'")
        else:
            keys = [lit]
        for k in keys:
            if k not in text_keys:
                problems.append(f"{mission}: key '{k}' has no text line (skipped)")
            else:
                out.append((spk, k))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="change nothing; exit 1 if a slot is missing")
    a = ap.parse_args()

    with open(os.path.join(HERE, "leaders.json"), encoding="utf-8") as f:
        leaders = json.load(f)
    problems, added_total, missing_total = [], 0, 0
    per_leader = {}
    for mdir in sorted(glob.glob(os.path.join(ROOT, "data", "missions", "*"))):
        wanted = collect(mdir, leaders, problems)
        if not wanted:
            continue
        loc_path = os.path.join(mdir, "locale.toml")
        with open(loc_path, "rb") as f:
            raw = f.read()
        text = raw.decode("utf-8")
        _, have = locale_keys(text)
        new = []
        for spk, key in wanted:
            if key not in have and key not in (k for _, k, _ in new):
                new.append((spk, key, f'"{key}" = "voice/{spk}/{key}.ogg"'))
        for spk, key in wanted:
            per_leader[spk] = per_leader.get(spk, 0) + 1
        if not new:
            continue
        missing_total += len(new)
        name = os.path.basename(mdir)
        print(f"{name}: +{len(new)} " + ", ".join(k for _, k, _ in new))
        if a.check:
            continue
        nl = "\r\n" if "\r\n" in text else "\n"
        if not text.endswith("\n"):
            text += nl
        if not re.search(r"^\[voice\]\s*$", text, re.M):
            text += nl + "[voice]" + nl
        else:
            # [voice] must be the last table so appended lines land inside it.
            tail = text[re.search(r"^\[voice\]\s*$", text, re.M).end():]
            if re.search(r"^\[", tail, re.M):
                problems.append(f"{name}: another table follows [voice]; not edited")
                continue
        text += nl.join(line for _, _, line in new) + nl
        tomllib.loads(text)  # must still parse
        with open(loc_path, "wb") as f:
            f.write(text.encode("utf-8"))
        added_total += len(new)

    print("\nradio lines per leader (found in Lua):")
    for spk, n in sorted(per_leader.items()):
        print(f"  {spk:28} {n}")
    for p in problems:
        print("note:", p)
    if a.check:
        print(f"\n{missing_total} slot(s) missing")
        return 1 if missing_total else 0
    print(f"\nadded {added_total} slot(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
