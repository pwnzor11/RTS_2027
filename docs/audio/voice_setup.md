# Local voice generation for RTS_2027: recommendation and Windows setup

Status: research note, late 2026. Written from web sources (listed in section 7). Items marked **[UNVERIFIED]** were not confirmed against a live page or a test run.

## 1. Recommendation

- **Use Kokoro-82M** (`hexgrad/Kokoro-82M`) with its Python package `kokoro`. Code is Apache-2.0, weights are Apache-2.0, and the card says training used only permissive or non-copyrighted audio.
- It ships **54 preset voices** (v1.0), so each leader can get a fixed preset with no cloning, no consent question and no watermark.
- **Fallback: Piper** (`OHF-Voice/piper1-gpl`, GPL-3.0 code). Check each voice's own licence on the voices page before shipping it, because the voice-model licence is set per voice.
- Why licence first: Kokoro's Apache-2.0 weights can sit in a GPL-3.0 repo and their output can be shipped without a non-commercial clause. I found no output restrictions on the model card.

## 2. Comparison

| Tool | Code licence | Weights licence | Output terms | Windows | GPU needed | Voices | Notes |
|---|---|---|---|---|---|---|---|
| **Kokoro-82M** (recommended) | Apache-2.0 ([repo](https://github.com/hexgrad/kokoro)) | Apache-2.0 ([model card](https://huggingface.co/hexgrad/Kokoro-82M)) | None stated on the model card | Yes (pip + espeak-ng MSI) | Not stated. Small model (82M); CPU use **[UNVERIFIED]** | 54 presets, 8 languages (v1.0) | No cloning. Deterministic per voice and text **[UNVERIFIED]**. Training data described as permissive only. |
| **Piper** (fallback) | GPL-3.0 for `piper1-gpl`; the older `rhasspy/piper` was MIT ([release notes](https://github.com/OHF-Voice/piper1-gpl/releases)) | Per voice. The `rhasspy/piper-voices` repo is labelled MIT at repo level, but the page does not say whether individual voices carry their own licence ([voices README](https://huggingface.co/rhasspy/piper-voices/blob/main/README.md)) | Check per voice | Yes (prebuilt binaries not confirmed; source build for MSVC is in release notes) | No (CPU, fast) | Many, 35 languages | Good for CPU-only machines. Voice licences must be checked one by one. |
| Chatterbox (Resemble AI) | MIT per Resemble page ([page](https://www.resemble.ai/chatterbox/?id=ChatterboxTTS)) | MIT per Resemble FAQ | Every output carries a PerTh watermark; removing it is against intended use | Not confirmed | Yes in the example (`device="cuda"`); no VRAM figure given | No presets seen; needs a 5 to 20 second reference clip | Cloning-first. Consent needed for every reference voice. Not recommended for leader voices. |
| Coqui XTTS-v2 | Coqui Public Model Licence (HF metadata: `coqui-public-model-license`) ([HF](https://huggingface.co/coqui/XTTS-v2)) | Coqui Public Model Licence | Treat as non-commercial **[UNVERIFIED wording]**. Discussion thread says commercial use is unclear after the company closed; the company's status was not confirmed by a primary source. | Yes, but heavy | Yes in practice | Cloning from a sample | **Exclude.** Licence unclear for shipping. |
| F5-TTS | MIT ([repo](https://github.com/SWivid/F5-TTS)) | CC-BY-NC (pretrained checkpoints trained on Emilia) | Non-commercial | Yes | Yes | Cloning from a sample | **Exclude.** Non-commercial weights. |

Notes on the table:
- The Piper row says "GPL-3.0" for `piper1-gpl` because the v1.3.0 release notes say "Change license to GPLv3". I did not read the LICENSE file itself, so confirm it before you copy the licence text. **[UNVERIFIED]**
- Chatterbox's own page calls the models "open and free to build on under Voice AI Research" but also says they are "no longer offered as commercial paid products". That line is about Resemble's paid service, not the licence. Read the model card before relying on MIT.

## 3. Windows setup for Kokoro

Assumes Windows 10 or 11, an NVIDIA GPU optional, PowerShell. Nothing here has been run on the owner's PC.

1. **Install Python 3.11** (64-bit) from python.org. Tick "Add to PATH" or use the `py` launcher. The Kokoro example uses Python 3.9, so 3.10 to 3.12 should work **[UNVERIFIED]**.
2. **Install espeak-ng.** Download the latest `*.msi` from the espeak-ng releases page and run it. Kokoro uses it for out-of-vocabulary words and some non-English languages.
3. **Create a virtual environment** in the project's tools folder (not inside the game source):
   ```powershell
   cd C:\path\to\rts_2027
   py -3.11 -m venv tools\voice\.venv
   tools\voice\.venv\Scripts\Activate.ps1
   ```
   If activation is blocked: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`.
4. **Install PyTorch.** For an NVIDIA GPU, pick the CUDA command from the selector at pytorch.org (the tag below is an example and must be checked). **[UNVERIFIED]**
   ```powershell
   pip install torch --index-url https://download.pytorch.org/whl/cu124
   ```
   For CPU only, use the CPU index URL the selector gives.
5. **Install Kokoro and an audio writer:**
   ```powershell
   pip install "kokoro>=0.9.4" soundfile
   ```
   The version floor comes from the repo's usage examples.
6. **Model download.** The first run pulls `hexgrad/Kokoro-82M` from Hugging Face into the user cache (`%USERPROFILE%\.cache\huggingface`). Allow a few hundred MB. Copy it into the repo only if you want an offline copy, and record its licence (Apache-2.0). **[UNVERIFIED size]**
7. **First test.** Save as `tools\voice\first_test.py`:
   ```python
   from kokoro import KPipeline
   import soundfile as sf

   pipeline = KPipeline(lang_code="a")   # "a" = American English
   text = "Commander, the enemy has crossed the river."
   for i, (graphemes, phonemes, audio) in enumerate(pipeline(text, voice="af_heart")):
       sf.write(f"tools/voice/test_{i}.wav", audio, 24000)
   ```
   Run with `python tools\voice\first_test.py`. The 24000 sample rate is from the Kokoro examples **[UNVERIFIED]**; use the rate the array actually reports if it differs.
8. **Where files go:** test WAVs stay under `tools\voice\` (git-ignored). Final OGG files go under `data\audio\voice\` (section 4).

If step 1 or 4 fails on the owner's PC, record the error in the session notes; do not change the recommendation without a new decision in `docs/DECISIONS.md`.

## 4. Consistent voice per leader

Recommended approach: **preset voices, one per faction leader.**

- Pick one preset per leader from the 54 voices (the voice list is in the repo's `VOICES.md` **[UNVERIFIED]**). Choose by tone, gender and accent, and listen to the same sentence in each.
- Keep these fixed for every line of that leader: `voice=` name, `lang_code`, speaking speed (use 1.0), and the text-cleanup step. Do not mix voices for a leader.
- Keep a `tools/voice/leaders.json` file that maps each leader ID to its voice, speed and lang code. Treat it as the source of truth.
- Check consistency by generating one fixed test sentence per leader on two separate runs and comparing them by ear. **[UNVERIFIED that Kokoro output is bit-identical between runs.]**

Naming scheme for final files:

```
data/audio/voice/<leader_id>/<line_key>.ogg
```

Example: `data/audio/voice/chief_marlow/brief_m08_01_intro.ogg`

Convert WAV to OGG with ffmpeg (install via winget or from ffmpeg.org):

```powershell
ffmpeg -i tools\voice\out\brief_m08_01_intro.wav -c:a libvorbis -q:a 5 data\audio\voice\chief_marlow\brief_m08_01_intro.ogg
```

Whether the game's audio layer reads OGG has not been checked. Check the engine before generating many files. **[UNVERIFIED]**

Do not generate audio inside simulation code. Generation is an offline build step, so the deterministic-sim rule is not affected.

## 5. How it plugs into the game later

- For every briefing line key in the locale file, the text stays as it is today. The locale file path was not found in the repo (only the design notes mention briefings), so confirm the path before editing. **[UNVERIFIED]**
- Optional voice file per key: `data/audio/voice/<leader_id>/<line_key>.ogg`.
- Runtime rule: if the file exists, play it with the text on screen; if not, show text only. A missing file must never be an error.
- Add a setting to turn voice on or off, default on, and a mute for voice when text is read aloud.

## 6. Licence checklist for each generated file

Record one row per generated file in `docs/CREDITS.md`, with:

- [ ] Output file path (for example `data/audio/voice/chief_marlow/brief_m08_01_intro.ogg`)
- [ ] Generator: Kokoro-82M, weights source URL, weights licence (Apache-2.0), package version (`pip show kokoro`)
- [ ] Voice preset name and speed
- [ ] Source text (the locale line key, not the file)
- [ ] Date generated and the name of the person who ran it
- [ ] Statement: "Generated locally from Kokoro-82M (Apache-2.0). No cloned or third-party voice."
- [ ] If a voice is ever changed to Piper, add its voice licence from the voices page, checked per voice

Also: AI-assisted art and audio go in CREDITS as the project already requires for AI-assisted artwork. Do not use any celebrity or real-person voice.

## 7. Sources

- Kokoro model card (licence, training data, voices): https://huggingface.co/hexgrad/Kokoro-82M
- Kokoro code repo (Apache-2.0, install notes): https://github.com/hexgrad/kokoro
- Kokoro on PyPI: https://pypi.org/project/kokoro/
- Piper code, moved to OHF-Voice (release notes, GPL-3.0 change): https://github.com/OHF-Voice/piper1-gpl/releases
- Piper voices (MIT at repo level): https://huggingface.co/rhasspy/piper-voices
- Chatterbox (Resemble AI page, MIT, watermark, reference clip): https://www.resemble.ai/chatterbox/?id=ChatterboxTTS
- Coqui XTTS-v2 model page (licence metadata): https://huggingface.co/coqui/XTTS-v2
- XTTS-v2 discussion on licence after company closure: https://huggingface.co/coqui/XTTS-v2/discussions/65
- F5-TTS code (MIT) and weights note (CC-BY-NC): https://github.com/SWivid/F5-TTS

Blocked and not read: the Chatterbox GitHub repo, the Piper repo front page and the XTTS-v2 licence file. Their provider permission request timed out, so the claims for those rows come from secondary or search-snippet sources.
