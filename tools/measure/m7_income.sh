#!/bin/bash
# M7-14 income matrix. Usage: m7_income.sh <bindir> <datadir-parent> <outdir> <seeds> [sizes...]
# Runs Expedition (A) vs each other faction (tier 2 both) on ocean maps for 12100 ticks, one raw log per cell.
BIN=$1; WD=$2; OUT=$3; SEEDS=$4; shift 4
SIZES=${@:-64 96 128}
mkdir -p "$OUT"
cd "$WD" || exit 1
for sz in $SIZES; do for f in authority directorate left_behind; do
  "$BIN/Tools" ai match --a tiered:2 --b tiered:2 --faction-a expedition --faction-b $f --size $sz --seeds $SEEDS --ticks 12100 > "$OUT/s${sz}_$f.txt" 2>&1
done; done
