# M5-13 random-directive control: index = (seed-1)*2 + swap, seeds 1-64, size 64, 30000 ticks (s12 Test 1 setup)
COMMIT=0f467f2ca78cb09dd52b56508c1c5c72b770db6c
S=$((IDX / 2 + 1)); W=$((IDX % 2))
for i in $(seq 60); do [ -f /work/bin/rts/$COMMIT/Tools.dll ] && break; sleep 30; done
mkdir -p "$OUT/s${S}_w${W}"
cd /work/src/rts/$COMMIT && dotnet /work/bin/rts/$COMMIT/Tools.dll ai commander --random-directives --a tiered:3 --b tiered:3 --seed $S --swap $W \
  --size 64 --ticks 30000 --out "$OUT/s${S}_w${W}" > "$OUT/s${S}_w${W}/run.log" 2>&1
