import json,tomllib,html,subprocess
R='./'; B='tools/booklet/pilot/'
names={}
for f in ['units_authority','structures_authority','upgrades']:
    for e in tomllib.load(open(R+f'data/archive/{f}.toml','rb'))['entry']: names[e['id']]=e['title']
T=lambda i:html.escape(names.get(i,i))
d=json.load(open('tools/booklet/data/techtree_authority.json'));n=d['nodes']
prov={'auth_tier2':'auth_engineering','auth_tier3':'auth_signals_hq'}
col={1:'#dfe0c8',2:'#efd9a0',3:'#e9bba4'}
blds=[k for k,v in n.items() if v['kind']=='building']
kids={b:[] for b in blds}
for k,v in n.items():
    if v['kind'] in ('infantry','vehicle'): kids[v['producer'][0]].append(k)
def node(b,ghost=False):
    v=n[b]; c='#f3eedf' if ghost else col[v.get('tier',1)]
    rows='' if ghost else ''.join(f'<TR><TD ALIGN="LEFT" BGCOLOR="{"#f7f2e4" if n[u]["tier"]==1 else col[n[u]["tier"]]}"><FONT POINT-SIZE="9">{T(u)}</FONT></TD><TD ALIGN="RIGHT" BGCOLOR="{"#f7f2e4" if n[u]["tier"]==1 else col[n[u]["tier"]]}"><FONT POINT-SIZE="8">{n[u]["cost"]}</FONT></TD></TR>' for u in sorted(kids[b],key=lambda u:(n[u]['tier'],n[u]['cost'])))
    hdr=f'<I>{T(b)} (see opposite)</I>' if ghost else f'<B>{T(b).upper()}</B>'
    cost=" " if ghost else str(v["cost"])
    return f'"{b}" [label=<<TABLE BORDER="1" CELLBORDER="0" CELLSPACING="0" CELLPADDING="3" COLOR="{"#8a8270" if ghost else "#2b2a24"}" BGCOLOR="{c}" STYLE="{"dashed" if ghost else ""}"><TR><TD ALIGN="LEFT"><FONT POINT-SIZE="10">{hdr}</FONT></TD><TD ALIGN="RIGHT"><FONT POINT-SIZE="8">{cost}</FONT></TD></TR>{rows}</TABLE>>];'
def graph(members, ghosts, out, rd='TB'):
    L=['digraph T {',f'rankdir={rd}; nodesep=0.22; ranksep=0.34; bgcolor="transparent";',
       'node [shape=plain, fontname="TeX Gyre Heros Cn"]; edge [color="#4d4a40", arrowsize=0.55, penwidth=1.1];']
    L+= [node(b) for b in members]+[node(g,True) for g in ghosts]
    allm=set(members)|set(ghosts)
    for b in members:
        for r in n[b]['requires']:
            s=prov.get(r,r)
            if s in allm and s!=b: L.append(f'"{s}" -> "{b}";')
    L.append('}'); open(B+out+'.dot','w').write('\n'.join(L))
    subprocess.run(['dot','-Tsvg','-o',B+'img/'+out+'.svg',B+out+'.dot'],check=True)
left=['auth_conyard','auth_power','auth_refinery','auth_silo','auth_factory','auth_engineering','auth_airfield','auth_signals_hq','auth_atomic_battery','auth_gate','auth_wall']
right=[b for b in blds if b not in left]
graph(left,[], 'tree_left')
graph(right,['auth_power','auth_engineering'],'tree_right','LR')
print(right)
ups=[k for k,v in n.items() if v['kind']=='upgrade']
json.dump({'ups':[(T(u),n[u]['cost'],n[u]['tier']) for u in sorted(ups,key=lambda u:(n[u]['tier'],n[u]['cost']))]},open(B+'ups.json','w'))
