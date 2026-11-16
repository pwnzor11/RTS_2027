# M5-13 baseline: plain tier 3 v tier 3, same seeds (1-64, both starts), size 64; 8 seeds per index
COMMIT=0f467f2ca78cb09dd52b56508c1c5c72b770db6c
FIRST=$((1 + IDX * 8))
for i in $(seq 60); do [ -f /work/bin/rts/$COMMIT/Tools.dll ] && break; sleep 30; done
cd /work/src/rts/$COMMIT && dotnet /work/bin/rts/$COMMIT/Tools.dll ai match --a tiered:3 --b tiered:3 --seeds 8 --first $FIRST --size 64 --ticks 30000 \
  --out "$OUT/base.c$IDX.md" > "$OUT/base.c$IDX.log" 2>&1
