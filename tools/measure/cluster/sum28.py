#!/usr/bin/env python3
"""sum28.py <dir with tag subfolders> -> W-L-D and A % per tag x row, against the main (VE, D-254) baseline."""
import re,glob,sys,os
D=re.compile(r"^\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|")
BASE={('rust_belt','authority','directorate'):(19,31,14),('rust_belt','authority','left_behind'):(39,20,5),('rust_belt','authority','expedition'):(25,36,3),
('rust_belt','directorate','left_behind'):(29,30,5),('rust_belt','directorate','expedition'):(35,13,16),('rust_belt','left_behind','expedition'):(31,24,9),
('glass_coast','expedition','authority'):(34,25,5),('glass_coast','expedition','directorate'):(24,22,18),('glass_coast','expedition','left_behind'):(27,18,19)}
pct=lambda a,b: round(100*a/max(1,a+b))
for d in sorted(glob.glob(sys.argv[1]+'/*/')):
    tag=os.path.basename(d.rstrip('/')); rows={}
    for f in glob.glob(d+'*.c?.md'):
        m=re.match(r'\w+?_(rust_belt|glass_coast|mountains|ash_plains)_l\d_(\w+)_v_(\w+)\.c\d\.md',os.path.basename(f))
        r=rows.setdefault((m[1],m[2],m[3]),[0,0,0,0,0])
        for l in open(f):
            x=D.match(l)
            if x: r[0]+=int(x[1]); r[1]+=int(x[2]); r[2]+=int(x[3]); r[3]+=int(x[4]); r[4]+=1; break
    dist=0; bdist=0
    print(f'== {tag}')
    for k in sorted(rows):
        g,a,b,dr,ch=rows[k]; ba=BASE.get(k); p=pct(a,b); bp=pct(ba[0],ba[1]) if ba else 50
        if g>=64: dist+=abs(p-50); bdist+=abs(bp-50)
        print(f"  {k[1]:11s} v {k[2]:11s} {k[0]:11s} {a:2d}-{b:2d}-{dr:2d} ({p:3d} %) games {g:2d}   main {ba[0]}-{ba[1]}-{ba[2]} ({bp} %)")
    print(f'  distance from 50 over complete rows: {dist} (main {bdist})')
