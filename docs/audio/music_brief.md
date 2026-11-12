# Music brief for ACE-Step (D-080; 20-track list from design 21, M10-09)

Music is generated on the owner's PC with ACE-Step 1.5 (MIT; D-080 named the older Apache-2.0 release) and ships as CC0 / no rights claimed, with the tool credited in
`docs/CREDITS.md`. The game plays whatever `*.ogg` files sit in `data/music/` and stays silent if there are none, so tracks can
arrive one at a time. Voice is text-only: **every track is instrumental**.

## The four sounds (M10 design 21 §3: style mixed by faction)

- **Authority**: 1960s atomic-age orchestral. Bold brass, sweeping strings, snare and timpani, a little vibraphone and organ; optimistic
  civil-engineering pride, major and Lydian colour. (Replaces the earlier martial-electronic sound; owner answer 2026-11-12.)
- **Directorate**: industrial, underground, brass and choir, radioactive. Heavy metallic percussion, low brass, male choir pads (wordless
  "ooh/ahh"), detuned drones, Geiger-like ticks and sub-bass, minor and Phrygian colour, slow menace.
- **Left Behind**: raw survivor folk and percussion. Scrap-metal and oil-drum percussion, hand drums, bowed saw, harmonica drones,
  detuned acoustic guitar and fiddle, gritty and hostile, minor and Dorian colour.
- **Expedition**: naval brass over electronics. Brass fanfares with a sea-shanty lilt, pulsing synths, sonar-like pings, deep low
  strings, disciplined and far from home, minor and Mixolydian colour.
- **Shared tracks** (menu, credits, victory, defeat) blend all four: orchestra plus a little industrial percussion.

Do not name or imitate any existing game, film, band or composer in a prompt, and do not paste lyrics. Every track is instrumental.

## How to run ACE-Step

Setup: `docs/audio/ace_step_setup.md` (ACE-Step 1.5, MIT).

1. Open the ACE-Step UI. Paste the **prompt** into the caption/tags field. Put `[instrumental]` in the lyrics field.
2. Set the duration (below). Leave other settings at their defaults. Generate 4 to 6 takes per track (batch size up to 8), keep the best,
   and note its seed in your own log, not in the game file name.
3. For battle, tension and menu tracks pick a take whose ending settles, so a loop is not jarring (the game fades over about 2 seconds).
   Victory and defeat play once.
4. Export (see "Loudness and export") and save with the exact file name into `data/music/`.

## Tracks (20)

`<faction>` in file names is `authority`, `directorate`, `leftbehind` or `expedition` (the game's faction names in lower case).

| # | File in `data/music/` | Used when | Length | Played today? |
|---|---|---|---|---|
| 1 | `menu.ogg` | menus, setup, LAN lobby, briefing (until 17-20 are wired) | 2:30-3:00 | yes |
| 2 | `credits.ogg` | end credits roll | 3:00-4:00 | after M10-20 |
| 3 | `victory.ogg` | your team won (once) | 0:30-0:45 | yes |
| 4 | `defeat.ogg` | you were defeated (once) | 0:30-0:45 | yes |
| 5-12 | `battle_<faction>_1.ogg`, `battle_<faction>_2.ogg` | fighting, you play that faction | 2:30-3:30 | yes |
| 13-16 | `tension_<faction>.ogg` | in a game, no fighting | 3:00-4:00 | yes, but today all four are picked at random for every faction (see note) |
| 17-20 | `briefing_<faction>.ogg` | that faction's mission briefings | 1:30-2:30 | after the music code change |

Note for the code phase (after M9): the player picks tension tracks by the prefix `tension`, so per-faction calm tracks need a small
change to prefer `tension_<faction>`; the generic `battle` fallback also only excludes Authority and Directorate files. Briefing and credits
states do not exist yet. Until then, make the tension tracks quiet and close enough in feel that any of them works anywhere.

### Shared
1. **menu.ogg**: `instrumental, cinematic orchestral, slow build, warm strings and distant brass swells, soft timpani, subtle metallic percussion pulse, low synth drone, expectant, dignified, 86 BPM, D minor, wide clean mix, no vocals`
2. **credits.ogg**: `instrumental, reflective orchestral suite, gentle strings, solo horn melody, soft piano, light industrial percussion in the middle section, hopeful rebuilding mood, resolving ending, 92 BPM, F major, warm mix, no vocals`
3. **victory.ogg**: `instrumental, triumphant orchestral fanfare, bright brass, big timpani and snare, rising strings, resolving to a strong final held chord, celebratory, 120 BPM, C major, ends cleanly, no vocals`
4. **defeat.ogg**: `instrumental, somber, slow low brass and deep drone, muted war drums, descending minor melody on solo cello, fading choir pad, bleak, final, 60 BPM, D minor, sparse mix, ends on a long fading note, no vocals`

### Authority
5. **battle_authority_1.ogg**: `instrumental, 1960s heroic orchestral march, bold brass fanfares, driving snare and timpani, sweeping strings, bright vibraphone accents, confident advance, optimistic, 124 BPM, B-flat major, punchy cinematic mix, no vocals`
6. **battle_authority_2.ogg**: `instrumental, fast orchestral action, urgent string ostinato, stabbing brass, rolling timpani, cymbal swells, organ chords under the brass, precise and relentless, 140 BPM, E Lydian, bright full mix, no vocals`
7. **tension_authority.ogg**: `instrumental, quiet orchestral suspense, low sustained strings, soft muted horns, distant snare roll, ticking woodblock, sparse vibraphone notes, watchful, 72 BPM, G minor, wide soft mix, no melody, no vocals`
8. **briefing_authority.ogg**: `instrumental, 1960s newsreel orchestral theme, proud brass melody, light snare march, strings, a hint of organ, civic optimism, measured, 100 BPM, D major, clean mix, no vocals`

### Directorate
9. **battle_directorate_1.ogg**: `instrumental, industrial, dark ambient meets heavy percussion, pounding metallic anvil hits, low brass ostinato, male choir pads singing ooh and ahh, detuned sub-bass drone, Geiger counter ticks, radioactive, underground, ominous, 96 BPM, C Phrygian, gritty distorted mix, no lyrics`
10. **battle_directorate_2.ogg**: `instrumental, industrial metal-electronic, relentless factory drum loop, minor-key brass fanfare, wordless choir chanting, distorted bass pulse, siren-like synth swells, clanking chains, toxic and claustrophobic, 118 BPM, F# minor, dense heavy mix, no vocals`
11. **tension_directorate.ogg**: `instrumental, sparse industrial ambient, distant machinery hum, slow metallic drips, low choir drone, faint Geiger ticks, deep sub-bass swells, underground unease, 68 BPM, C minor, wide reverb, no melody, no vocals`
12. **briefing_directorate.ogg**: `instrumental, solemn industrial anthem, slow low brass chorale, wordless male choir, heavy measured drum hits, metallic resonance, stern and grim, 80 BPM, E-flat minor, dark mix, no vocals`

### Left Behind
13. **battle_leftbehind_1.ogg**: `instrumental, raw post-apocalyptic folk rock, pounding oil-drum and scrap-metal percussion, distorted acoustic guitar riff, screeching fiddle, harmonica wail, hostile and wild, 132 BPM, A Dorian, gritty lo-fi mix, no vocals`
14. **battle_leftbehind_2.ogg**: `instrumental, tribal junkyard percussion ensemble, hand drums and hammered pipes, bowed saw, droning bass strings, chaotic stomps and claps, feral and relentless, 112 BPM, D minor, dusty raw mix, no vocals`
15. **tension_leftbehind.ogg**: `instrumental, sparse survivor ambient, wind through ruins, slow bowed saw, faint harmonica drone, distant metal clanks, lonely detuned guitar notes, wary, 66 BPM, E minor, wide dry mix, no melody, no vocals`
16. **briefing_leftbehind.ogg**: `instrumental, campfire war council, slow hand drum, detuned acoustic guitar, low fiddle drone, harmonica, scrap-metal accents, grim determination, 84 BPM, A minor, raw intimate mix, no vocals`

### Expedition
17. **battle_expedition_1.ogg**: `instrumental, naval action anthem, brass fanfare with a sea-shanty rhythm, pulsing synth bass, snare and big drums, low strings, sonar-like synth pings, disciplined assault, 126 BPM, D Mixolydian, bright punchy mix, no vocals`
18. **battle_expedition_2.ogg**: `instrumental, fast electronic naval battle, driving synth arpeggios, heavy brass stabs, rolling toms like waves, rising string swells, alarm-like synth motif, relentless, 138 BPM, G minor, dense modern mix, no vocals`
19. **tension_expedition.ogg**: `instrumental, quiet naval suspense, slow sonar pings, low string drone, distant foghorn-like brass, soft synth pulse, creaking hull textures, far from home, 70 BPM, B minor, wide reverb, no melody, no vocals`
20. **briefing_expedition.ogg**: `instrumental, maritime command theme, stately brass melody with a shanty lilt, warm low strings, gentle synth pad, light snare, resolute, 96 BPM, F major, clean mix, no vocals`

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

"Music generated with ACE-Step 1.5 (MIT). Released CC0 / no rights claimed (D-080)."

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
