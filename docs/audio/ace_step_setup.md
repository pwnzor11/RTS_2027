# ACE-Step 1.5 on Windows: setup for the RTS_2027 soundtrack (M10-09)

Status: guide written 2026-11-12 from the project page and model card (sources at the end). Nothing here has been run on the owner's PC yet.
Items marked **[UNVERIFIED]** need a check on the first run. The prompts are in `docs/audio/music_brief.md`.

## 1. What changed since D-080
- D-080 named ACE-Step under Apache-2.0. The current version, **ACE-Step 1.5** (XL models added 2026-04-02), is **MIT** for code and
  weights, and its model card says the output may be used commercially. That is compatible with shipping the tracks CC0 in a GPL repo.
- Credit line for `docs/CREDITS.md`: "Music generated with ACE-Step 1.5 (MIT). Released CC0 / no rights claimed (D-080)."

## 2. Pick a model for your GPU
Check your graphics card's memory first (Task Manager → Performance → GPU → "Dedicated GPU memory").

| GPU memory | Use |
|---|---|
| up to 6 GB | 2B turbo, no LM, INT8, CPU offload |
| 6–8 GB | 2B turbo with the 0.6B LM |
| 8–16 GB | 2B turbo or sft with the 0.6B or 1.7B LM |
| 16–24 GB | XL turbo with the 1.7B LM |
| 24 GB or more | XL sft with the 4B LM |

No NVIDIA card: it also runs on CPU, but slowly **[UNVERIFIED how slow]**. Use the laptop or desktop with the bigger GPU.

## 3. Install (pick A or B)
**A. Portable package (easiest).** Download `ACE-Step-1.5.7z` from the project's Windows link
(`https://files.acemusic.ai/acemusic/win/ACE-Step-1.5.7z`), unpack it with 7-Zip to a short path such as `C:\ACE-Step`, and run
`start_gradio_ui.bat`. The dependencies are included.

**B. From source with uv** (PowerShell; needs Git):
```powershell
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
cd C:\
git clone https://github.com/ACE-Step/ACE-Step-1.5.git
cd ACE-Step-1.5
uv sync
uv run acestep
```
If PowerShell then says `uv` is not recognised, the window was opened before uv was installed and has the old PATH: run
`$env:Path = "$env:USERPROFILE\.local\bin;$env:Path"` in that window (or open a new one) and repeat `uv sync`.
Python 3.11 or 3.12 (uv fetches it). Keep it **outside** the RTS_2027 folder; it is a tool, not part of the game.

The first launch downloads the model weights (several GB; the 2B model is about 4.7 GB in memory, XL about 9 GB) into the user cache.
Then open **http://localhost:7860** in a browser.

## 4. First test (5 minutes)
1. Caption/tags: `instrumental, test, short orchestral fanfare, brass, snare, 110 BPM, C major, no vocals`
2. Lyrics: `[instrumental]`
3. Duration 30 s, batch size 2, default settings. Generate.
4. If both takes play and sound like brass, the setup works. If you get an out-of-memory error, drop one row in the table in section 2.

## 5. Making the tracks
Follow `docs/audio/music_brief.md`: one prompt per track, 4–6 takes per track (batch size up to 8), keep the best, export to OGG at
-16 LUFS with the ffmpeg line in the brief, and save under the exact file name in `data/music/`. Keep a small log next to the WAVs
(outside the repo) with the take's seed, in case a track needs redoing.

When the first few tracks are in, tell the lead whether the UI or the REST API (`uv run acestep-api`, port 8001) suits you better; a batch
script for the API follows once its parameters are checked on your machine **[UNVERIFIED parameter names]**.

## 6. Rules
- Every track is instrumental. Never name or imitate an existing game, film, band or composer in a prompt, and never paste lyrics.
- Do not commit WAVs or the ACE-Step folder; only the final `.ogg` files go into `data/music/`.

## Sources
- [ACE-Step 1.5 on GitHub](https://github.com/ACE-Step/ACE-Step-1.5) (install, VRAM table, UI and API launch, XL release date)
- [ACE-Step 1.5 model card on Hugging Face](https://huggingface.co/ACE-Step/Ace-Step1.5) (MIT licence, commercial use of output)
