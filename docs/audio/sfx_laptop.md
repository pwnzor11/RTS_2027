# Sound effects on the laptop (M10-10)

The SFX pass uses **Stable Audio Open 1.0** (Stability AI) on your laptop's CPU. About 80 sounds, 2 to 3 takes each, listed with their
prompts in `tools/audio/sfx/sfx_jobs.toml`. The model weights stay on your machine; only the WAV files we pick go into the repo.
No GPU is needed.

## One-time setup
Run everything from the root of your RTS_2027 clone, Windows PowerShell 5.1, on the branch with this work.

1. `powershell -ExecutionPolicy Bypass -File tools\audio\sfx\make_sfx.ps1 -Setup` creates `.venv-sfx` (Python 3.11 through the `py` launcher) and installs
   torch (CPU build), diffusers, transformers, soundfile, scipy, numpy. About 3 GB.
2. Accept the model terms: open <https://huggingface.co/stabilityai/stable-audio-open-1.0> while logged in to Hugging Face and agree to the
   Stability AI Community License. Then create a **read** token at <https://huggingface.co/settings/tokens>.
3. `... make_sfx.ps1 -Login` and paste the token (stored by Hugging Face in your user profile, never in the repo).
4. The first generation downloads the model (several GB; size UNVERIFIED) into the Hugging Face cache.

## Generate
| Command | What it does |
|---|---|
| `make_sfx.ps1 -List` | Lists the jobs, how many takes exist, and the time estimate. Runs nothing. |
| `make_sfx.ps1 -Generate -Draft` | Quick preview: 30 steps, take 1 only, saved as `draft1.wav`. Use it first to check the prompts. |
| `make_sfx.ps1 -Generate -Only wpn_,exp_` | Full quality (100 steps, 3 takes) for ids containing `wpn_` or `exp_`. A category name also works: `weapons explosions buildings deaths production special alerts ui ambience`. |
| `make_sfx.ps1 -Generate` | Everything. Stop with Ctrl+C any time; finished takes are skipped on the next run. |
| `-Takes 5`, `-Steps 60`, `-SecondsScale 0.5`, `-Threads 6` | Optional: more takes, fewer steps, shorter clips, smaller files, limit CPU use. |

The seed of each take is a hash of the sound id and take number, so a rerun makes the same sound. To get *different* alternatives use `-Takes`
with a larger number (take 4 and up are new). To redo a take, delete its file in `tools\audio\sfx\out\<id>\`.
Output goes to `tools\audio\sfx\out\<id>\take<N>.wav` (git-ignored) with a `.json` beside it holding the prompt, seed and steps.
The log is `tools\audio\sfx\out\make_sfx.log`. The script keeps Windows awake while it runs.

### How long
**UNVERIFIED**: nobody has timed the real model on a laptop yet (the weights are gated, so the cloud session could only test the code with a
tiny random model). The estimate the tool prints assumes 140 to 350 GFLOPS of CPU throughput: roughly **1 to 3 minutes per short take**
(1 to 3 s), 2 to 6 minutes for a 5 s explosion, 10 to 30 minutes for a 24 s ambience loop, at 100 steps. All 80 sounds with 3 takes (2 for
the loops) is therefore **about 8 to 19 hours**; `-Draft` is roughly a third of one take. The `[n/total] ... eta` lines show the real pace after the
first sound: if it is slower than the range, use `-Draft`, `-Steps 50` or `-Only` for the sounds you care about first.
RAM: expect about 6 to 8 GB while the model is loaded.

Why "short latent": the stock diffusers pipeline always computes the full 47 s window, which is 20 to 25 times more work than a 2 s shot needs.
`make_sfx.py` hands the pipeline noise sized to the clip, as stable-audio-tools does with `sample_size`. The call path was checked with a
random-weight miniature pipeline (shapes and cropping are right); how *good* short windows sound is UNVERIFIED. If shots sound strange, try
`-LatentFull` on one sound (slow) and tell Claude.

## Choose by ear
`make_sfx.ps1 -Audition` writes and opens `tools\audio\sfx\out\index.html`: one player per take, grouped by category, with the prompt, the
event the sound plays for today, and the one the code still needs. Where a sound is already in the game there is an "in game now" player for comparison.
Tick the take you like; **Make picks.toml** shows text to save as `tools\audio\sfx\picks.toml`. Untouched sounds use take 1.

## Put the picks into the game
`make_sfx.ps1 -Pick` (uses `picks.toml` if it exists), or `-Pick wpn_flame=2,gui_back=d1` (`d1` means draft 1). It:
- copies the chosen takes to `data\audio\sfx\<id>.wav` (mono 22.05 kHz 16-bit; ambience loops stereo 22.05 kHz);
- adds a `[sound.<id>]` entry for each, in a marked block at the end of `data\audio\sounds.toml`;
- adds the sounds the code can play today to their events as **extra random variants**, keeping the old sfxr sounds (`-ReplaceSfxr` removes the old ones from those events);
- points the menu sounds (`ui_move`, `ui_select`, `ui_back`, `ui_deny`) at the new files (the old files stay on disk; the line notes `# was ...`);
- with `-Mix` also adds the faction and class sounds to the generic events so you hear them now, in random order, until the game can choose by faction (see below).
It only references files that exist, so the game and `SoundBankTests` keep loading after a partial run. It is safe to run again.
Then start the game and listen, and commit with the lines it prints. Undo a bad pick with `git checkout data/audio`.

## What the game plays (S22: the code reads the new keys)
`src/Game/Audio` looks sounds up by event **and** an optional class and faction, most specific first, and falls back to the plain event, so the game
runs with any subset of the files (today's 26 sfxr ones included). The order is written at the top of `data/audio/sounds.toml`:
`event.class.faction` then `event.class` then `event.faction` then `event`; and when none exists, the event's stand-in (`unit_died` plays `explosion_small`,
`building_destroyed` plays `explosion_large`, `water_impact` an explosion, `ship_horn` plays `unit_ready`).

| Key | Raised when |
|---|---|
| `building_placed.<faction>`, `construction_complete.<faction>` | your building is placed / a queue item is ready |
| `building_destroyed.<faction>` | a building is removed with no health left |
| `unit_died.<infantry|vehicle|ship|air>.<faction>` | a unit is removed with no health left |
| `unit_ready.<faction>` | your new unit appears; a new ship raises `ship_horn` instead |
| `fire_bullet|fire_shell|fire_missile.<weapon class>.<faction>` | a shot. Projectile weapons are named by the projectile's weapon; instant weapons by the nearest armed enemy in range of the hit (a guess when several shoot at once). Classes (the `[weapon]` table maps every weapon): `small_arms sniper mg flame spray` / `cannon rail energy mortar howitzer flak naval_shell naval_heavy depth_charge` / `rocket salvo sam harpoon`. `silent` (death weapons) and `bomb` make no shot sound |
| `explosion_large.bomb` | a blast within 4 cells of a bomber |
| `water_impact`, `water_impact.large` | a blast on an empty water cell (3+ cells = large) |
| `sonar` | an enemy submarine comes into view |
| `air_flyby` / `air_hover` | aircraft of speed 40+ while moving / speed 20 to 39; one sound per aircraft every 6 s / 4 s (airships make none) |
| `rig_deploy`, `rig_pack` | a packed rig unfolds / a rig packs (the one `rig_deploy_lb` sound serves both) |
| `[ambience.<glass_coast|rust_belt|ash_plains|mountains|open_sea>]` | looping stereo ambience while a game is on screen; the map's theater picks the biome, `open_sea` when over 60% of the cells around the camera are water; cross-fades; off in menus; quiet (0.5) and scaled by the new **Ambience volume** setting |

Each sound job in `sfx_jobs.toml` lists its keys in `events` (or `ambience`); `--pick` writes the `[event.<key>]` sections (copying volume, positional and
cooldown from the plain event) and the `[ambience.<key>]` entries into the marked block of `sounds.toml`. Not wired: `exp_rad_burst` (nothing in the view tells a radiation burst apart).
A big battle cannot stack voices: at most 8 fight sounds (shots, blasts, deaths) start per sim step, each entry has its own cooldown, at most `max_voices` copies of one sound play,
and at most 40 one-shots at once.

## Files and licence
Storage (S22): one-shots are **22.05 kHz mono 16-bit** (about 44 KB per second), ambience loops **22.05 kHz stereo** (about 88 KB per second; 1.9 MB for 22 s). All of `data/audio/**/*.wav` and `data/music/*.ogg`
go through **Git LFS** (`.gitattributes`); after `-Pick` use `git add data/audio` as usual and LFS takes the files. `SoundBankTests.AllWavFilesAreSmall16Bit_MonoOneShotsAndStereoLoops` allows
270 KB per one-shot, 2.7 MB per `amb_*` loop and 40 MB in all; `-Pick` warns about a file over the cap. All 80 sounds are about 15 to 20 MB.
Licence line (docs/CREDITS.md): "Generated with Stable Audio Open 1.0 (Stability AI Community License); outputs owned by the project, shipped under the game's GPL v3." Not declared CC0.
Do not use the sounds to train another audio model without reading the licence first.

## Python on the laptop
`-Setup` needs Python 3.11 (checked here; 3.13 is UNVERIFIED for the pinned torch / diffusers, so it is not accepted). It tries `py -3.11`, then `uv venv --python 3.11`, then
`%LOCALAPPDATA%\Programs\Python\Python311\python.exe`. If none exists: `winget install --id Python.Python.3.11 -e`, open a new PowerShell window, run `-Setup` again.
