"""Coverage report for the Field Manual: every unit, aircraft, ship and building id in rules_resolved.json.

For each id prints whether a display name exists in data/archive, a hero render exists at
art/booklet/<faction>/<id>.png, and a sprite exists at data/sprites/<folder>/<id>.png.
It also runs the stat-card and "damage against" logic from pilot/prep.py and pilot/build.py over every
id (not just the pilot ids) and lists the ids that raise. Report only: exit code is always 0.

Run from the repo root: python tools/booklet/data/check_coverage.py
"""
import glob, json, os, tomllib

R = './'
RULES = R + 'tools/booklet/data/rules_resolved.json'
ARM = ['unarmored', 'light', 'heavy', 'structure', 'fortified']
ARMN = {'unarmored': 'infantry', 'light': 'light vehicles', 'heavy': 'armour',
        'structure': 'structures', 'fortified': 'defences'}
LAB = {'unarmored': 'Infantry', 'light': 'Light', 'heavy': 'Armour', 'structure': 'Bldg', 'fortified': 'Defence'}

rr = json.load(open(RULES, encoding='utf-8'))

# Names: every archive file (display names). Pilot prep.py loads only three of these files; see below.
all_names = {}
for f in sorted(glob.glob(R + 'data/archive/*.toml')):
    for e in tomllib.load(open(f, 'rb')).get('entry', []):
        all_names[e['id']] = e
PREP_FILES = ['units_authority', 'structures_authority', 'upgrades']
prep_names = {}
for f in PREP_FILES:
    for e in tomllib.load(open(R + f'data/archive/{f}.toml', 'rb'))['entry']:
        prep_names[e['id']] = e


def hero_path(entry_id, faction):
    return R + f'art/booklet/{faction}/{entry_id}.png'


def sprite_path(entry_id):
    hits = sorted(glob.glob(R + f'data/sprites/**/{entry_id}.png', recursive=True))
    return hits[0] if hits else None


ids = []
for kind_name, table in (('unit', rr['units']), ('building', rr['buildings'])):
    for uid, u in table.items():
        ids.append((uid, kind_name, u))

rows = []
for uid, kind_name, u in ids:
    name_entry = all_names.get(uid)
    faction = u.get('faction', '')
    hero = os.path.exists(hero_path(uid, faction))
    spr = sprite_path(uid)
    domain = u.get('domain', '') if kind_name == 'unit' else ''
    if kind_name == 'unit' and domain == 'air':
        cat = 'aircraft'
    elif kind_name == 'unit' and domain == 'sea':
        cat = 'ship'
    elif kind_name == 'unit':
        cat = 'unit'
    else:
        cat = 'building'
    rows.append((uid, cat, faction, name_entry is not None, hero, spr is not None, spr))

print(f"{'id':<28} {'category':<9} {'faction':<12} {'name':<5} {'hero':<5} {'sprite':<6}")
for uid, cat, faction, has_name, hero, has_spr, _ in rows:
    print(f"{uid:<28} {cat:<9} {faction:<12} {'yes' if has_name else 'no':<5} "
          f"{'yes' if hero else 'no':<5} {'yes' if has_spr else 'no':<6}")

n = len(rows)
print()
print(f"TOTALS: ids={n}  name yes={sum(r[3] for r in rows)} no={sum(not r[3] for r in rows)}  "
      f"hero yes={sum(r[4] for r in rows)} no={sum(not r[4] for r in rows)}  "
      f"sprite yes={sum(r[5] for r in rows)} no={sum(not r[5] for r in rows)}")
print()
print('Missing a display name:', ', '.join(r[0] for r in rows if not r[3]) or 'none')
print('Missing a hero render:', ', '.join(r[0] for r in rows if not r[4]) or 'none')
print('Missing a sprite:', ', '.join(r[0] for r in rows if not r[5]) or 'none')
print()


# Stat-card logic as in pilot/prep.py card() and pilot/build.py stat_rows()/versus(), run for every id.
def stat_card(uid, names):
    # prep.card() is written for units only (it reads speed, sight, locomotion); buildings get only the
    # weapon and "damage against" part, so buildings are checked on that path.
    is_unit = uid in rr['units']
    u = rr['units'][uid] if is_unit else rr['buildings'][uid]
    pw = u.get('primary_weapon')
    w = rr['weapons'].get(pw) if pw else None
    e = names[uid]
    c = {'id': uid, 'title': e['title'], 'subtitle': e['subtitle'], 'note': e['field_note'],
         'body': e['body'].strip(), 'cost': u['cost'], 'hp': u['max_health'], 'armor': u['armor'],
         'build': u['build_seconds'], 'tier': u.get('tier', 1),
         'prereq': [p for p in u['prerequisites'] if 'tier' not in p]}
    if is_unit:
        c['speed'] = round(u['speed'] * 20 / 256, 2)
        c['sight'] = u['sight']
        c['loco'] = u['locomotion']
    if w:
        vs = rr['warheads'][w['warhead']]['versus']
        c['weapon'] = {'range': w['range_cells'], 'damage': w['damage'], 'reload': w['reload_seconds'],
                       'targets': w['targets'], 'vs': {a: vs[a] for a in ARM}}
        c['strong'] = [ARMN[a] for a in ARM if vs[a] >= 75]
        c['weak'] = [ARMN[a] for a in ARM if vs[a] <= 40]
        strip = [(LAB[k], min(v, 100)) for k, v in c['weapon']['vs'].items()]
        assert len(strip) == 5
    return c


def try_all(names, label):
    bad = []
    ok = 0
    for uid, _cat, _f, _n, _h, _s, _p in rows:
        try:
            stat_card(uid, names)
            ok += 1
        except Exception as ex:  # report, do not stop
            bad.append((uid, f'{type(ex).__name__}: {ex}'))
    print(f'Stat-card check [{label}]: ok={ok} failed={len(bad)} of {len(rows)}')
    for uid, err in bad:
        print(f'  {uid} -> {err}')
    print()
    return bad


try_all(prep_names, 'as prep.py loads names (authority files only)')
try_all(all_names, 'with every archive file loaded')

# Stat-card fields that prep.py's card() reads from the archive entry: flag entries lacking them.
print('Archive entries missing a field that prep.py reads (subtitle, field_note, body):')
for uid, _cat, _f, has_name, *_ in rows:
    if not has_name:
        continue
    e = all_names[uid]
    miss = [k for k in ('subtitle', 'field_note', 'body') if k not in e]
    if miss:
        print(f'  {uid} -> missing {", ".join(miss)}')
print()
print('Loader problems from resolve_rules.py:', len(rr.get('loader_problems', [])))
for p in rr.get('loader_problems', []):
    print('  ' + p)
raise SystemExit(0)
