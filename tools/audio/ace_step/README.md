# ACE-Step batch for the soundtrack (M10-09)
Copy `rts_batch.ps1` and the `rts_jobs` folder into the ACE-Step 1.5 folder (e.g. `C:\ACE-Step-1.5`), close the ACE-Step browser UI,
then in PowerShell there: `powershell -ExecutionPolicy Bypass -File .\rts_batch.ps1` (take 1 of all 20 tracks) or `-Takes 2` to add a
second take. Finished takes are skipped, so it can be stopped and restarted. Output: `output\rts\<track>\take<n>\*.wav`; progress in
`rts_batch.log`. Job files are generated from `docs/audio/music_brief.md` (prompts, lengths in design 21 §3). On a CPU-only laptop
(no NVIDIA GPU) one pass of all 20 tracks takes about 9-10 hours (about 5 minutes per 30 seconds of music).
