# Music brief for ACE-Step (D-080)

Music is generated on the owner's PC with ACE-Step (Apache-2.0) and ships as CC0 / no rights claimed, with the tool credited in
`docs/CREDITS.md`. The game plays whatever `*.ogg` files sit in `data/music/` and stays silent if there are none, so tracks can
arrive one at a time. Voice is text-only: **every track is instrumental**.

## The two sounds

- **Authority**: ordered, bright, martial-electronic. Crisp programmed drums, bright saw and pulse synth leads, clean arpeggios,
  marching snare, major or Mixolydian colour, confident and forward-moving.
- **Directorate**: industrial, underground, brass and choir, radioactive. Heavy metallic percussion, low brass, male choir
  pads (no words, "ooh/ahh" only), detuned drones, Geiger-like ticks and sub-bass, minor and Phrygian colour, slow menace.

Do not name or imitate any existing game, film or composer in a prompt, and do not paste lyrics.

## How to run ACE-Step

1. Open the ACE-Step UI. Paste the **prompt** into the tags/caption field. Put `[instrumental]` in the lyrics field.
2. Set the duration (below). Leave the other settings at their defaults, except as noted. Generate 4 to 6 candidates per track
   (different seeds), keep the best one, and note its seed in the file name of the project log, not in the game file name.
3. For the battle, tension and menu tracks, pick a take whose ending settles, so a loop is not jarring. The game fades between
   tracks over about 2 seconds and loops these three kinds. Victory and defeat play once.
4. Export (see "Loudness and export") and save with the exact file name into `data/music/`.

## Tracks

| # | File in `data/music/` | Used when | Length |
|---|---|---|---|
| 1 | `menu.ogg` | menus, setup, briefing | 2:30 to 3:00 |
| 2 | `battle_authority_1.ogg` | fighting, you play Authority | 2:30 to 3:30 |
| 3 | `battle_authority_2.ogg` | fighting, you play Authority | 2:30 to 3:30 |
| 4 | `battle_directorate_1.ogg` | fighting, you play Directorate | 2:30 to 3:30 |
| 5 | `battle_directorate_2.ogg` | fighting, you play Directorate | 2:30 to 3:30 |
| 6 | `tension.ogg` | in a game, no fighting (also tunnels surfacing) | 3:00 to 4:00 |
| 7 | `victory.ogg` | your team won (plays once) | 0:30 to 0:45 |
| 8 | `defeat.ogg` | you were defeated (plays once) | 0:30 to 0:45 |

If a faction has no battle file, the game uses `battle*.ogg`, then `tension.ogg`. One file per name is enough; extra takes may be
added as `battle_authority_3.ogg` and so on and are picked at random.

### 1. menu.ogg

Prompt:
`instrumental, cinematic electronic, slow build, warm analog synth pads, soft pulsing bass, distant brass swells, subtle snare roll, expectant, dignified, 86 BPM, D minor, clean modern production, no vocals`

Lyrics field: `[instrumental]`. Mood: calm before a campaign; a theme that works for both factions.

### 2. battle_authority_1.ogg

Prompt:
`instrumental, martial electronic, bright saw synth lead, crisp programmed drums, marching snare, driving sixteenth-note bass arpeggio, heroic brass stabs, ordered and disciplined, uplifting, 128 BPM, A major, tight punchy mix, no vocals`

Mood: confident advance in formation.

### 3. battle_authority_2.ogg

Prompt:
`instrumental, military techno, pulse synth ostinato, gated reverb snare, steady four-on-the-floor kick, shimmering high arpeggios, rising string synth, precise and relentless, triumphant, 140 BPM, E Mixolydian, bright clean mix, no vocals`

Mood: faster, more urgent than track 2.

### 4. battle_directorate_1.ogg

Prompt:
`instrumental, industrial, dark ambient meets heavy percussion, pounding metallic anvil hits, low brass ostinato, male choir pads singing ooh and ahh, detuned sub-bass drone, Geiger counter ticks, radioactive, underground, ominous, 96 BPM, C Phrygian, gritty distorted mix, no lyrics`

Mood: slow, heavy, grinding advance.

### 5. battle_directorate_2.ogg

Prompt:
`instrumental, industrial metal-electronic, relentless factory drum loop, brass fanfare in minor key, choir chanting wordless, distorted bass pulse, siren-like synth swells, clanking chains, toxic and claustrophobic, 118 BPM, F# minor, dense heavy mix, no vocals`

Mood: faster, more violent than track 4.

### 6. tension.ogg

Prompt:
`instrumental, tense ambient, sparse, low string drone, slow heartbeat kick, ticking percussion, distant metallic scrapes, rising dissonant pad, unsettling, suspense, 70 BPM, B minor, wide reverb, no melody, no vocals`

Mood: nothing is happening yet. Must not overpower the sound effects; keep it sparse and low in the mix. Both factions share it.

### 7. victory.ogg

Prompt:
`instrumental, triumphant electronic fanfare, bright brass and synth lead, big drums, resolving to a strong final chord, celebratory, 120 BPM, C major, clean mix, ends cleanly, no vocals`

A clear ending on a held chord, with a short natural tail (no fade-out needed).

### 8. defeat.ogg

Prompt:
`instrumental, somber, slow low brass and deep drone, muted taiko hits, descending minor melody on solo synth, fading choir pad, bleak, final, 60 BPM, D minor, sparse mix, ends on a long fading note, no vocals`

## Loudness and export

- **Target**: -16 LUFS integrated (music sits under the effects), true peak no higher than -1.0 dBTP. Victory and defeat may sit at
  -14 LUFS. Do not normalise each track harder than that; keep the tension track the quietest in feel.
- **Format**: OGG Vorbis, 44.1 kHz, stereo, quality 5 (about 160 kbps). One file per track, mono is not needed.
- **ffmpeg** (two passes are better, this single pass is fine for a first set):

```
ffmpeg -i in.wav -af loudnorm=I=-16:TP=-1.0:LRA=11 -ar 44100 -c:a libvorbis -q:a 5 -map_metadata -1 battle_authority_1.ogg
```

- Strip tags (`-map_metadata -1`); the credit lives in `docs/CREDITS.md`.
- A track should be well under 5 MB. Check with `ls -l data/music`.

## Credit line to add to docs/CREDITS.md

"Music generated with ACE-Step (Apache-2.0). Released CC0 / no rights claimed (D-080)."

## CC0 sound-effect sources for the owner to fetch

The generated sounds in `data/audio/sfx/` come from `tools/audio/sfxr.py` and need no download. For real recordings, use only
CC0 files and **check the licence on each download page** before adding anything (some sites mix licences). Put them under
`data/audio/sfx/`, add a `[sound.<id>]` entry to `data/audio/sounds.toml`, and list the source in `docs/CREDITS.md`. Convert to
22 or 44.1 kHz 16-bit mono WAV.

1. **Kenney** (kenney.nl, Assets, Audio): Impact Sounds, Interface Sounds, Sci-fi Sounds. Whole packs are CC0.
2. **OpenGameArt** (opengameart.org): filter Art Type "Sound Effect" and License "CC0". Good for explosions, guns, UI blips and
   retro effects.
3. **Freesound** (freesound.org): search with the licence filter "Creative Commons 0". Needs a free account to download. Good for
   engines, rumbles, machinery, Geiger-like crackle, brass and choir one-shots.
4. **BigSoundBank** (bigsoundbank.com): a recordings library published under CC0. Good for mechanical and ambient sounds.
5. **Juhani Junkala's retro sound effect collections** on OpenGameArt (search that name): chiptune-style effects published CC0.
   Useful to compare against or replace the generated blips.
