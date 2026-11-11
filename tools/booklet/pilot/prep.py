"""Prepare booklet pilot assets: team-coloured sprite frames, tech tree SVG, unit stat data."""
import json, tomllib, subprocess, html
from PIL import Image
import os, shutil
R='./'; B='tools/booklet/pilot/'
os.makedirs(B+'img', exist_ok=True)
shutil.copy(R+'data/images/stills/still_authority_act2.png', B+'img/')
MAG=["#400040","#5B125B","#772577","#923792","#AD49AD","#C85BC8","#E46EE4","#FF80FF"]
AMBER=["#3a2606","#5a3c0a","#7a520f","#9c6b14","#be841a","#e0a020","#ecbd52","#f6d888"]
hx=lambda s:tuple(int(s[i:i+2],16) for i in (1,3,5))
MAP={hx(a):hx(b) for a,b in zip(MAG,AMBER)}
def frame(path,fw,fh,idx,scale,out):
    im=Image.open(R+path).convert('RGBA'); cols=im.width//fw
    x=(idx%cols)*fw; y=(idx//cols)*fh
    f=im.crop((x,y,x+fw,y+fh)); f=f.crop(f.getbbox()); px=f.load()
    for j in range(f.height):
        for i in range(f.width):
            r,g,b,a=px[i,j]
            if a and (r,g,b) in MAP: px[i,j]=MAP[(r,g,b)]+(a,)
    f=f.resize((f.width*scale,f.height*scale),Image.NEAREST); f.save(B+'img/'+out)
frame('data/sprites/vehicles/auth_tank.png',96,96,6,4,'auth_tank.png')
frame('data/sprites/vehicles/auth_jeep.png',88,88,6,4,'auth_jeep.png')
frame('data/sprites/vehicles/auth_scraper.png',112,112,6,3,'auth_scraper.png')
frame('data/sprites/infantry/auth_rocketeer.png',48,48,3,6,'auth_rocketeer.png')
frame('data/sprites/infantry/auth_rifleman.png',48,48,3,6,'auth_rifleman.png')
frame('data/sprites/buildings/auth_conyard.png',192,248,0,2,'auth_conyard.png')

rr=json.load(open('tools/booklet/data/rules_resolved.json'))
names={}
for f in ['units_authority','structures_authority','upgrades']:
    for e in tomllib.load(open(R+f'data/archive/{f}.toml','rb'))['entry']: names[e['id']]=e
def title(i): return names[i]['title'] if i in names else i
# unit stat cards
ARM=['unarmored','light','heavy','structure','fortified']
ARMN={'unarmored':'infantry','light':'light vehicles','heavy':'armour','structure':'structures','fortified':'defences'}
def card(uid):
    u=rr['units'][uid]; w=rr['weapons'].get(u['primary_weapon']) if u.get('primary_weapon') else None
    c={'id':uid,'title':title(uid),'subtitle':names[uid]['subtitle'],'note':names[uid]['field_note'],
       'body':names[uid]['body'].strip(),'cost':u['cost'],'hp':u['max_health'],'armor':u['armor'],
       'speed':round(u['speed']*20/256,2),'sight':u['sight'],'build':u['build_seconds'],'tier':u.get('tier',1),
       'prereq':[title(p) for p in u['prerequisites'] if 'tier' not in p],'loco':u['locomotion']}
    if w:
        vs=rr['warheads'][w['warhead']]['versus']
        c['weapon']={'range':w['range_cells'],'damage':w['damage'],'reload':w['reload_seconds'],
                     'targets':w['targets'],'vs':{a:vs[a] for a in ARM}}
        best=[ARMN[a] for a in ARM if vs[a]>=75]; weak=[ARMN[a] for a in ARM if vs[a]<=40]
        c['strong']=best; c['weak']=weak
    return c
cards={u:card(u) for u in ['auth_tank','auth_rifleman','auth_rocketeer','auth_scraper','auth_jeep']}
json.dump(cards,open(B+'cards.json','w'),indent=1)

# tech tree
tt=json.load(open('tools/booklet/data/techtree_authority.json'))
nodes=tt['nodes']
col={1:'#d9dcc4',2:'#ecd8a0',3:'#e6b8a2'}
lines=['digraph T {','rankdir=LR; nodesep=0.12; ranksep=0.42; bgcolor="transparent"; splines=ortho;',
 'node [fontname="TeX Gyre Heros Cn", fontsize=11, style="filled", color="#2b2a24", penwidth=1.2, margin="0.08,0.03"];',
 'edge [color="#5b5a50", arrowsize=0.5, penwidth=0.9];']
for nid,n in nodes.items():
    if nid.endswith(('tier2','tier3')) or not n.get('obtainable',True): continue
    k=n['kind']; t=html.escape(title(nid)).upper() if k=='building' else html.escape(title(nid))
    shape={'building':'box','unit':'box','upgrade':'hexagon'}.get(k,'box')
    style='filled' if k=='building' else ('filled,rounded' if k=='unit' else 'filled')
    fs=11 if k=='building' else 10
    lab=f'<<B>{t}</B><BR/><FONT POINT-SIZE="8">{n["cost"]} cr</FONT>>' if k=='building' else f'<{t}<BR/><FONT POINT-SIZE="8">{n["cost"]} cr</FONT>>'
    lines.append(f'"{nid}" [label={lab}, shape={shape}, style="{style}", fillcolor="{col.get(n.get("tier",1),"#ddd")}", fontsize={fs}];')
for e in tt['edges']:
    s,d=e['src'],e['dst']
    if any(x.endswith(('tier2','tier3')) for x in (s,d)): continue
    if s in nodes and d in nodes: lines.append(f'"{s}" -> "{d}";')
lines.append('}')
open(B+'techtree.dot','w').write('\n'.join(lines))
subprocess.run(['dot','-Tsvg','-o',B+'img/techtree.svg',B+'techtree.dot'],check=True)
print('ok', len(nodes))
