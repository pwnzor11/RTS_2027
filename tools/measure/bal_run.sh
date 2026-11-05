#!/bin/bash
# Usage: bal_run.sh <cfgdir> <label> <factionA> <factionB> <firstSeed> <seeds> [outdir]
# Runs the BalProbe binary (built with: dotnet build tools/measure/bal_probe/BalProbe.csproj -c Release -o $BIN) for one chunk of one
# cell and appends TSV rows to <outdir>/<label>_<fa>_<fb>.tsv. cfgdir is a full copy of data/ with game.toml edited.
BIN=${BIN:-/tmp/claude-0/balbin}
cfg=$1; label=$2; fa=$3; fb=$4; first=$5; seeds=$6; out=${7:-./bal_out}
mkdir -p "$out"; f="$out/${label}_${fa}_${fb}.tsv"
hdr=""; [ -s "$f" ] || hdr="--header 1"
"$BIN/BalProbe" $hdr --rules "$cfg/rules" --fa "$fa" --fb "$fb" --tier 3 --size 96 --first "$first" --seeds "$seeds" --ticks 30000 >> "$f"
