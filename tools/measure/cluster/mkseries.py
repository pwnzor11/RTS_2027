#!/usr/bin/env python3
"""mkseries.py out.sh tag:commit:factions[,..] ...  factions '+'-joined (e.g. directorate+expedition, or all). Writes a rows x 4-chunk series script."""
import sys
ROWS=["rust_belt 1 authority directorate","rust_belt 1 authority left_behind","rust_belt 1 authority expedition","rust_belt 1 directorate left_behind","rust_belt 1 directorate expedition","rust_belt 1 left_behind expedition","glass_coast 1 expedition authority","glass_coast 1 expedition directorate","glass_coast 1 expedition left_behind"]
lines=[]
for spec in sys.argv[2:]:
  t,c,f=spec.split(':'); fs=f.split('+')
  for r in ROWS:
    if f=='all' or any(x in r.split()[2:] for x in fs): lines.append(f"{c} {t} {r}")
tpl=open(sys.path[0]+'/series.sh').read()
head,rest=tpl.split("mapfile -t L <<'R'\n",1); body=rest.split("\nR\n",1)[1]
open(sys.argv[1],'w').write(head+"mapfile -t L <<'R'\n"+"\n".join(lines)+"\nR\n"+body)
print(len(lines)*4)
