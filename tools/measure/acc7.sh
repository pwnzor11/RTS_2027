#!/usr/bin/env bash
# M7-14 soak driver (resumable). Run from the repo root after a Release build of tests/AI.Tests.
#   tools/measure/acc7.sh <lane 0|1> <nlanes> <seconds>
# Cells = pair x size x tier (10 pairs of the four factions, sizes 64/96/128, tiers 1..5), seed 1. Pairs with an Expedition side play both
# slot orders (2 games); the other pairs play one game, the slot order alternating with the cell index. Output: scratch/acc7/<pair>_<size>_t<tier>.txt.
# A cell is skipped when its file already holds all its games, so rerunning continues where a timed-out call stopped.
set -u
lane=$1; nlanes=$2; budget=$3
root=$(pwd); dll=$root/tests/AI.Tests/bin/Release/net10.0/AI.Tests.dll
mkdir -p "$root/scratch/acc7"
end=$((SECONDS + budget)); i=0
pairs="authority-authority directorate-directorate leftbehind-leftbehind expedition-expedition authority-directorate authority-leftbehind directorate-leftbehind expedition-authority expedition-directorate expedition-leftbehind"
for size in 64 96 128; do for pair in $pairs; do for tier in 1 2 3 4 5; do
  i=$((i+1)); [ $((i % nlanes)) -eq "$lane" ] || continue
  case $pair in expedition-*) games="0,1"; want=2;; *) games=$((i % 2)); want=1;; esac
  f=$root/scratch/acc7/${pair}_${size}_t${tier}.txt
  [ "$(cat "$f" 2>/dev/null | wc -l)" -ge $want ] && continue
  [ $SECONDS -ge $end ] && exit 0
  rm -f "$f" "$f.tmp"
  RTS_SOAK=1 RTS_SOAK_PAIRS=$pair RTS_SOAK_TIERS=$tier RTS_SOAK_SIZES=$size RTS_SOAK_SEEDS=1-1 RTS_SOAK_GAMES=$games RTS_SOAK_TICKS=30000 RTS_SOAK_OUT=$f.tmp \
    /usr/bin/dotnet vstest "$dll" --Tests:ALeftBehindGameReplaysWithoutDesync >/dev/null 2>&1
  [ -f "$f.tmp" ] && mv "$f.tmp" "$f"
done; done; done
