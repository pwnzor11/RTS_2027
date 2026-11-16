# Pi cluster scripts for `ai match` variant series (session 28)

Run from the laptop's k3s-cluster folder (`claude/`), see the `pi-cluster` skill and k3s-cluster-code `claude/examples/rts27/`.
1. Build each commit once: `build-any.sh` (k3s-cluster-code) with COMMIT set, `kjob.py submit b-<tag> ... --n 1`.
2. `python3 mkseries.py out.sh <tag>:<full sha>:<factions>[ ...]` writes a series over the nine cross-faction rows that involve the
   named factions (`directorate+expedition`, `all`); `series.sh` is the template (its line list is replaced). 4 chunks x 8 seeds = 64 games
   per row; set `CHUNKS=8` for 128 games. `kjob.py submit <job> out.sh --n <printed count x chunks/4> --par 24 --cpu 1 --mem 1Gi`.
3. `kjob.py fetch <job> <dir>` and `python3 sum28.py <dir>/<job>` prints W-L-D and A share per tag and row against the baseline in the script.
Pods wait up to 30 min for the build; submit the series after the build has finished if the build is slow.
