#!/bin/bash
# S12 (M7-19): only the Left Behind income cells of m7_income.sh. Usage: s12_lb_cells.sh <bindir> <datadir-parent> <outdir> <seeds> [sizes...]
BIN=$1; WD=$2; OUT=$3; SEEDS=$4; shift 4
SIZES=${@:-64 96 128}
mkdir -p "$OUT"; cd "$WD" || exit 1
for sz in $SIZES; do
  "$BIN/Tools" ai match --a tiered:2 --b tiered:2 --faction-a expedition --faction-b left_behind --size $sz --seeds $SEEDS --ticks 12100 > "$OUT/s${sz}_left_behind.txt" 2>&1
done
