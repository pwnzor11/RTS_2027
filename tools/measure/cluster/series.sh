# s28 multi-variant series: line = "commit tag biome layout A B"; index = line*CHUNKS + chunk; 8 seeds per chunk
SEEDS=8; CHUNKS=4
mapfile -t L <<'R'
0bff6c24a863f1455f1cc38e767557d192613b76 x1 rust_belt 1 authority expedition
0bff6c24a863f1455f1cc38e767557d192613b76 x1 rust_belt 1 directorate expedition
0bff6c24a863f1455f1cc38e767557d192613b76 x1 rust_belt 1 left_behind expedition
0bff6c24a863f1455f1cc38e767557d192613b76 x1 glass_coast 1 expedition authority
0bff6c24a863f1455f1cc38e767557d192613b76 x1 glass_coast 1 expedition directorate
0bff6c24a863f1455f1cc38e767557d192613b76 x1 glass_coast 1 expedition left_behind
d9fce072219d8e1d2db2ff796ddd737e90a78ab2 x2 rust_belt 1 authority expedition
d9fce072219d8e1d2db2ff796ddd737e90a78ab2 x2 rust_belt 1 directorate expedition
d9fce072219d8e1d2db2ff796ddd737e90a78ab2 x2 rust_belt 1 left_behind expedition
d9fce072219d8e1d2db2ff796ddd737e90a78ab2 x2 glass_coast 1 expedition authority
d9fce072219d8e1d2db2ff796ddd737e90a78ab2 x2 glass_coast 1 expedition directorate
d9fce072219d8e1d2db2ff796ddd737e90a78ab2 x2 glass_coast 1 expedition left_behind
0af2671dd919040a388537392b799c45af863638 x3 rust_belt 1 authority expedition
0af2671dd919040a388537392b799c45af863638 x3 rust_belt 1 directorate expedition
0af2671dd919040a388537392b799c45af863638 x3 rust_belt 1 left_behind expedition
0af2671dd919040a388537392b799c45af863638 x3 glass_coast 1 expedition authority
0af2671dd919040a388537392b799c45af863638 x3 glass_coast 1 expedition directorate
0af2671dd919040a388537392b799c45af863638 x3 glass_coast 1 expedition left_behind
fb77de8e7eb1afa471ad3abd87274f22754b32f9 x4 rust_belt 1 authority directorate
fb77de8e7eb1afa471ad3abd87274f22754b32f9 x4 rust_belt 1 directorate left_behind
fb77de8e7eb1afa471ad3abd87274f22754b32f9 x4 rust_belt 1 directorate expedition
fb77de8e7eb1afa471ad3abd87274f22754b32f9 x4 glass_coast 1 expedition directorate
76fa0e1b33bc0a589ce7967106f18d6d4b96aef4 x5 rust_belt 1 authority expedition
76fa0e1b33bc0a589ce7967106f18d6d4b96aef4 x5 rust_belt 1 directorate expedition
76fa0e1b33bc0a589ce7967106f18d6d4b96aef4 x5 rust_belt 1 left_behind expedition
76fa0e1b33bc0a589ce7967106f18d6d4b96aef4 x5 glass_coast 1 expedition authority
76fa0e1b33bc0a589ce7967106f18d6d4b96aef4 x5 glass_coast 1 expedition directorate
76fa0e1b33bc0a589ce7967106f18d6d4b96aef4 x5 glass_coast 1 expedition left_behind
6fb6f84183147002f4457983b78730a48df333ef x6 rust_belt 1 authority directorate
6fb6f84183147002f4457983b78730a48df333ef x6 rust_belt 1 directorate left_behind
6fb6f84183147002f4457983b78730a48df333ef x6 rust_belt 1 directorate expedition
6fb6f84183147002f4457983b78730a48df333ef x6 glass_coast 1 expedition directorate
9528629d9e747884218bba75d961f0a449258d1d x7 rust_belt 1 authority expedition
9528629d9e747884218bba75d961f0a449258d1d x7 rust_belt 1 directorate expedition
9528629d9e747884218bba75d961f0a449258d1d x7 rust_belt 1 left_behind expedition
9528629d9e747884218bba75d961f0a449258d1d x7 glass_coast 1 expedition authority
9528629d9e747884218bba75d961f0a449258d1d x7 glass_coast 1 expedition directorate
9528629d9e747884218bba75d961f0a449258d1d x7 glass_coast 1 expedition left_behind
R
LN=$((IDX / CHUNKS)); CH=$((IDX % CHUNKS)); FIRST=$((1 + CH * SEEDS))
read COMMIT TAG BIOME LAYOUT FA FB <<< "${L[$LN]}"
NAME="${TAG}_${BIOME}_l${LAYOUT}_${FA}_v_${FB}"
mkdir -p "$OUT/$TAG"
for i in $(seq 60); do [ -f /work/bin/rts/$COMMIT/Tools.dll ] && break; sleep 30; done
cd /work/src/rts/$COMMIT && dotnet /work/bin/rts/$COMMIT/Tools.dll ai match --a tiered:3 --b tiered:3 --faction-a "$FA" --faction-b "$FB" \
  --seeds $SEEDS --first $FIRST --size 96 --layout "$LAYOUT" --biome "$BIOME" --ticks 30000 --income 1 \
  --out "$OUT/$TAG/$NAME.c$CH.md" > "$OUT/$TAG/$NAME.c$CH.log" 2>&1
