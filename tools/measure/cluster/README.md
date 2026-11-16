# Pi cluster scripts for `ai match` variant series (session 28)

Run from the laptop's k3s-cluster folder (`claude/`), see the `pi-cluster` skill and k3s-cluster-code `claude/examples/rts27/`.
1. Build each commit once: `build-any.sh` (k3s-cluster-code) with COMMIT set, `kjob.py submit b-<tag> ... --n 1`.
2. `python3 mkseries.py out.sh <tag>:<full sha>:<factions>[ ...]` writes a series over the nine cross-faction rows that involve the
   named factions (`directorate+expedition`, `all`); `series.sh` is the template (its line list is replaced). 4 chunks x 8 seeds = 64 games
   per row; set `CHUNKS=8` for 128 games. `kjob.py submit <job> out.sh --n <printed count x chunks/4> --par 24 --cpu 1 --mem 1Gi`.
3. `kjob.py fetch <job> <dir>` and `python3 sum28.py <dir>/<job>` prints W-L-D and A share per tag and row against the baseline in the script.
Pods wait up to 30 min for the build; submit the series after the build has finished if the build is slow.

Session 29: `cmd29.sh` (M5-13 random-directive control, one game per index, seeds 1-64 x 2 swaps) and `cmdbase29.sh` (its plain baseline, 8 seeds
per index). When writing variant data with a script, insert keys after the line that IS the table header (`lines.index("[faction.x]")`), never after
a text match: the D-167 comment line also contains `[faction.expedition]` and a key written there fails to load ("unknown tier id").
