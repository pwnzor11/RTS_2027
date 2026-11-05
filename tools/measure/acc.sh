#!/usr/bin/env bash
# M6-12 acceptance measurement driver (resumable). Run from the repo root after a Release build of tests/AI.Tests.
#   tools/measure/acc.sh soak <lane 0|1> <seconds>     soak cells (pair x size x tier), 4 games each, lane = cell index mod 2
#   tools/measure/acc.sh income <lane 0|1> <seconds>   income cells (faction x size), 20 games each
# Output: scratch/acc/soak/<pair>_<size>_t<tier>.txt and scratch/acc/income/<faction>_<size>.txt (one line per game).
# A cell is skipped when its file already holds all its games, so rerunning continues where a timed-out call stopped.
set -u
mode=$1; lane=$2; budget=$3
root=$(pwd); dll=$root/tests/AI.Tests/bin/Release/net10.0/AI.Tests.dll
mkdir -p "$root/scratch/acc/soak" "$root/scratch/acc/income"
end=$((SECONDS + budget)); i=0
run() { /usr/bin/dotnet vstest "$dll" --Tests:"$TESTNAME" >/dev/null 2>&1; }
if [ "$mode" = soak ]; then
  for size in 64 96 128; do for pair in authority-authority directorate-directorate leftbehind-leftbehind authority-directorate authority-leftbehind directorate-leftbehind; do for tier in 1 3 5; do
    i=$((i+1)); [ $((i % 2)) -eq "$lane" ] || continue
    f=$root/scratch/acc/soak/${pair}_${size}_t${tier}.txt
    [ "$(cat "$f" 2>/dev/null | wc -l)" -ge 4 ] && continue
    [ $SECONDS -ge $end ] && exit 0
    rm -f "$f" "$f.tmp"
    TESTNAME=ALeftBehindGameReplaysWithoutDesync RTS_SOAK=1 RTS_SOAK_PAIRS=$pair RTS_SOAK_TIERS=$tier RTS_SOAK_SIZES=$size RTS_SOAK_SEEDS=1-2 RTS_SOAK_TICKS=30000 RTS_SOAK_OUT=$f.tmp run
    [ -f "$f.tmp" ] && mv "$f.tmp" "$f"
  done; done; done
else
  for size in 64 96 128; do for fac in authority directorate leftbehind; do
    i=$((i+1)); [ $((i % 2)) -eq "$lane" ] || continue
    f=$root/scratch/acc/income/${fac}_${size}.txt
    [ "$(cat "$f" 2>/dev/null | wc -l)" -ge 20 ] && continue
    [ $SECONDS -ge $end ] && exit 0
    rm -f "$f" "$f.tmp"
    TESTNAME=AMirrorGameRecordsHarvestedCredits RTS_INCOME=1 RTS_INCOME_FACTIONS=$fac RTS_INCOME_SIZES=$size RTS_INCOME_SEEDS=1-10 RTS_INCOME_TIER=2 RTS_INCOME_OUT=$f.tmp run
    [ -f "$f.tmp" ] && mv "$f.tmp" "$f"
  done; done
fi
