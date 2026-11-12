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
| `-Takes 5`, `-Steps 60`, `-SecondsScale 0.5`, `-Rate 22050`, `-Threads 6` | Optional: more takes, fewer steps, shorter clips, smaller files, limit CPU use. |

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
- copies the chosen takes to `data\audio\sfx\<id>.wav` (mono 44.1 kHz 16-bit; ambience stereo 32 kHz);
- adds a `[sound.<id>]` entry for each, in a marked block at the end of `data\audio\sounds.toml`;
- adds the sounds the code can play today to their events as **extra random variants**, keeping the old sfxr sounds (`-ReplaceSfxr` removes the old ones from those events);
- points the menu sounds (`ui_move`, `ui_select`, `ui_back`, `ui_deny`) at the new files (the old files stay on disk; the line notes `# was ...`);
- with `-Mix` also adds the faction and class sounds to the generic events so you hear them now, in random order, until the game can choose by faction (see below).
It only references files that exist, so the game and `SoundBankTests` keep loading after a partial run. It is safe to run again.
Then start the game and listen, and commit with the lines it prints. Undo a bad pick with `git checkout data/audio`.

## What the game plays today, and what waits for code
The code (`src/Game/Audio`) looks sounds up **by event only**: 21 event kinds, one random variant per play, no weapon, faction, unit-class or biome key.
- **Plays today** (the sound is wired as a variant): fire_bullet, fire_shell (also played for every rocket), explosion_small/large, promotion, tunnel_enter,
  surface_rumble, sold, harvester_unload, upgrade_complete, low_power, low_funds, under_attack, radiation_crackle, blackout_hum, the four menu sounds.
- **Generated for later** (file and `[sound]` entry exist, nothing plays them yet): per-faction weapons, building place/complete/destroyed, unit deaths
  (infantry, vehicle, ship, aircraft), production ready per faction, flame, spray, depth charge, flyby, rig deploy, sonar, ship horn, water splash,
  five ambience loops. `-Mix` lets the faction ones play as random variants meanwhile.
- `fire_missile` is never raised by the event deriver today (rockets sound as fire_shell), so rocket sounds only play through fire_shell.

## Files and licence
**Test to relax before the first pick is committed:** `SoundBankTests.AllWavFilesAreSmall16BitMono` (tests/Game.Tests/Audio) allows only mono, at most 22.05 kHz,
under 100 KB per file and 1 MB in total, which is the sfxr size budget. Checked in the cloud with stand-in files: every other Sound/Audio test passed, this one fails.
The lead must change the test (e.g. check only the sfxr ids, or raise the caps) in the same PR as the first picked files.

About 30 MB of WAV if everything is picked (80 files). `-Rate 22050` halves it. See `docs/CREDITS.md` for the licence line.
Stability AI Community License: free under USD 1M annual revenue, you own the outputs, commercial use is allowed under the Acceptable Use Policy.
Do not use the sounds to train another audio model without reading the licence first.
