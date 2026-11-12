# Laptop audio: music export, radio voices, sound effects (M10-09, M8-18)

Everything runs on your Windows laptop, CPU only, from the **root of the RTS_2027 folder**, in Windows PowerShell 5.1.
All commands go through one script, `tools\audio\make_audio.ps1`. It keeps the PC awake, logs to `tools\audio\make_audio.log`, and
prints the `git add` / `git commit` lines at the end. Nothing here has been run on your laptop yet; see the UNVERIFIED notes.

## Commands in order

| # | Command (prefix: `powershell -ExecutionPolicy Bypass -File tools\audio\make_audio.ps1`) | What it does | Rough time |
|---|---|---|---|
| 1 | `-Setup` | Creates `.venv-audio` (Python 3.11 via `py`), installs kokoro, soundfile, imageio-ffmpeg, numpy | 5-15 min (torch is large) |
| 1b | `winget install --id eSpeak-NG.eSpeak-NG -e` (only if `-Setup` says espeak-ng is missing; then open a new PowerShell) | Speech helper Kokoro needs | 1 min |
| 2 | `-VoiceAudition` | 8 candidate voices x 11 leaders -> `tools\voice\audition\index.html` | model download (a few hundred MB) + about 10-20 min |
| 3 | Open `index.html`, listen, put one voice id per leader in `tools\voice\leaders.json` (`"voice": "bm_george"`; optional `"speed": 0.95`) | Your choice | your ears |
| 4 | `-Voice` | Every `[voice]` line of every leader with a voice -> `data\audio\voice\<leader>\<key>.ogg` (164 lines, radio band-pass on) | about 1-2 s per second of speech on CPU; roughly 20-40 min for all [UNVERIFIED] |
| 5 | `-Music` | Take 1 of the 20 tracks from `C:\ACE-Step-1.5\output\rts` -> `data\music\*.ogg` | about 1 min per track |
| 6 | `git add data\music data\audio\voice tools\voice\leaders.json ; git commit -m "data(audio): music and voices" ; git push` | | |

`-All` is Music + Voice (+ Sfx if `tools\audio\sfx\make_sfx.ps1` exists). `-Sfx` runs that script alone. Finished voice files are skipped,
so a stopped run can be started again; `-Force` redoes them. `-Only leader_dir_larkin` limits `-Voice` and `-VoiceAudition` to one leader.

## Swap a music take
ACE-Step keeps every take in `output\rts\<track>\take<N>`. Export a different one for some tracks, the rest stay on take 1:

    ... -Music -Pick "menu=2,victory=3"
    ... -Music -Pick "battle_authority_1=2" -AceDir "D:\ACE-Step-1.5"

The file `data\music\<track>.ogg` is overwritten; commit it. Track names are the 20 in `docs\audio\music_brief.md`. To make more takes run
`rts_batch.ps1 -Takes 2` in the ACE-Step folder first. Loudness: -16 LUFS (victory and defeat -14), true peak -1 dBTP.

## Swap a voice
1. Edit `"voice"` (and `"speed"`) for the leader in `tools\voice\leaders.json`. Candidate ids per leader are in `tools\voice\kokoro_batch.py` (`CANDIDATES`); any Kokoro v1.0 id works (`af_*`/`am_*` American, `bf_*`/`bm_*` British).
2. `... -Voice -Only <leader_id> -Force` redoes that leader's lines.
3. Commit `data\audio\voice\<leader_id>` and `leaders.json`.

## Adding a radio line later
Write the `Dialogue.Say(speaker, speaker, key)` call and the locale text, then run `python tools\voice\voice_slots.py` (adds the `[voice]` slot), then `-Voice`.
The game plays a voice file only if it exists; with no file the line is text only, silently.

## Notes
- The `instructor` in `tut_01` is not a leader and has no voice slot.
- UNVERIFIED: the winget id (`eSpeak-NG.eSpeak-NG`, listed on winstall.app, not run), whether Kokoro finds espeak-ng by itself on Windows (the script sets `PHONEMIZER_ESPEAK_LIBRARY` when it finds the DLL), the run times above.
