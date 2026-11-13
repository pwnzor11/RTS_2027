#!/usr/bin/env python3
"""Resolve data/rules/*.toml the way src/Data/RulesLoader.cs does and write rules_resolved.json.

Mirrors (read on 2026-11-11, repo main):
  RulesInheritance.cs  one-level `base = "id"` inside one file; derived keys override base keys
  RulesFields.FactionOrDefault / SimRules.FactionFromId   faction key, else id prefix (dir_/lb_/exp_, anything else = authority)
  UnitRulesLoader / StructureRulesLoader / WeaponRulesLoader / UpgradeRulesLoader   key lists and defaults
  WeaponRulesLoader.ParseVersus   5..7 entry versus lists padded with 100
  TierSugar.cs   tier >= 2 types get the faction's tier2/tier3 provides name appended to prerequisites
  SimRules.BuildTicks  build_time > 0 ? build_time : max(1, cost * build_ticks_per_100_credits / 100)
  SupplyRules.SupplyOf  explicit supply >= 0, else cost band
  RulesDeriver.cs  upgrade effects (set / add / mul per mille, truncating)

Usage:  python3 resolve_rules.py [repo_root] [out_json]
Import: from resolve_rules import load_rules  -> dict (same content as the JSON)
"""
import json
import os
import sys
import tomllib

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ARMOR = ["unarmored", "light", "heavy", "structure", "fortified", "ship_light", "ship_heavy", "submerged"]
FACTIONS = ["authority", "directorate", "left_behind", "expedition"]
LEPTONS_PER_CELL = 256

# ---- defaults, copied from the loaders (optional keys only; required keys have no default) -------------------
MOBILE_V3 = dict(aura_radius=0, aura_rof_permille=1000, aura_speed_permille=1000, aura_targets=[], rad_resist_permille=0,
                 irradiated=False, stealth=False, death_weapon="", jam_radius=0, seismic_radius=0)
VEHICLE_DEFAULTS = dict(MOBILE_V3, harvest_capacity=0, build_time=0, tunnel_size="none", passengers=0, deploys_into="", tier=1,
                        unpacks_into="", pack_ticks=0, salvage_rate=0, raider=False, raider_one_use=False, drops_blackout_ticks=0,
                        aura_repair_permille=0, tow_bonus_permille=0, supply=-1, draw_scale_permille=1000, deep_only=False,
                        submerges=False, sonar_radius=0, aircraft_slots=0, sortie_ticks=0, ammo=0, rearm_ticks=0, unload_ticks=0,
                        vehicle_slots=0, one_use=False, buildable=True)   # D-205 (M13-11) keys, as UnitRulesLoader
INFANTRY_DEFAULTS = dict(MOBILE_V3, build_time=0, tunnel_size="S", engineer=False, tier=1, salvage_rate=0, raider=False,
                         raider_one_use=False, ash_stealth_delay_ticks=0, ash_stealth_threshold=0, aura_repair_permille=0, supply=-1)
BUILDING_DEFAULTS = dict(category="structure", produces="", build_time=0, storage=0, refinery=False, dock=[0, 0], free_unit="",
                         wall=False, needs_power=False, buildable=True, entrance="none", ticks_per_point=20, researches=False,
                         gate=False, seismic_radius=0, sees_through_blackout=False, jam_radius=0, superweapon="",
                         superweapon_charge_ticks=0, superweapon_entrance_range=0, morphs_into="", morph_ticks=0, tier=1,
                         garrison=0, garrison_range_bonus=256, garrison_rof_permille=750, packs_into="", pack_ticks=0,
                         supply_provided=0, supply_unstocked=-1, water_building=False, beachhead_source=False,
                         beachhead_radius=0, shipment_dock=False, sonar_radius=0)
WARHEAD_DEFAULTS = dict(contaminate=0, decontaminate=0, bonus_vs_irradiated_percent=100, blackout_ticks=0, hits_burrowed=False,
                        bonus_vs_entrance_percent=100, foam_ticks=0, garrison_damage_percent=0, storm_radius=0, storm_ticks=0,
                        storm_contaminate_step=0, storm_contaminate_cap=64, storm_speed_permille=0, salvo_missiles=0,
                        salvo_ticks=0, salvo_radius=0, salvo_damage=0, salvo_warn_ticks=0, salvo_splash=1)
WEAPON_DEFAULTS = dict(targets="ground")
UPGRADE_DEFAULTS = dict(build_time=0, prerequisites=[], effect="", tier=1, effects=[], pack_ticks_permille=0, salvage_permille=0,
                        plunder_permille=0, repairs_vehicles_at_range=False)

VEHICLE_KEYS = {"speed", "max_health", "locomotion", "turn_rate", "armor", "sight", "primary_weapon", "has_turret",
                "turret_turn_rate", "crusher", "cost", "prerequisites", "faction", "veteran"} | set(VEHICLE_DEFAULTS)
INFANTRY_KEYS = {"speed", "max_health", "armor", "sight", "primary_weapon", "cost", "prerequisites", "faction", "veteran",
                 "can_garrison"} | set(INFANTRY_DEFAULTS)
BUILDING_KEYS = {"footprint_w", "footprint_h", "max_health", "armor", "sight", "primary_weapon", "cost", "power", "prerequisites",
                 "provides", "faction", "exit", "water"} | set(BUILDING_DEFAULTS)
WARHEAD_KEYS = {"versus", "splash", "hits_allies"} | set(WARHEAD_DEFAULTS)
WEAPON_KEYS = {"damage", "range", "min_range", "rate_of_fire", "projectile_speed", "warhead", "targets"}
UPGRADE_KEYS = {"faction", "cost"} | set(UPGRADE_DEFAULTS)

# Attributes RulesDeriver actually applies, per target kind (anything else is silently ignored by the sim).
DERIVER_ATTRS = {
    "vehicle": ["max_health", "speed", "sight", "cost", "build_time", "rad_resist_permille", "vet_threshold_permille", "harvest_capacity"],
    "infantry": ["max_health", "speed", "sight", "cost", "build_time", "rad_resist_permille", "vet_threshold_permille"],
    "building": ["max_health", "sight", "power", "cost", "ticks_per_point", "superweapon_charge_ticks"],
    "weapon": ["damage", "range", "rate_of_fire"],
    "warhead": ["contaminate", "blackout_ticks"],
    "player": ["shipment_interval"],
}


def faction_from_id(i):
    return "directorate" if i.startswith("dir_") else "left_behind" if i.startswith("lb_") else "expedition" if i.startswith("exp_") else "authority"


def _read(rules_dir, name):
    with open(os.path.join(rules_dir, name), "rb") as f:
        return tomllib.load(f)


def _inherit(root, problems, file):
    """RulesInheritance.Resolve: returns {id: (values, own_keys, base_id)}."""
    out = {}
    for k, table in root.items():
        if not isinstance(table, dict):
            problems.append(f"{file}:{k}: expected a [table]")
            continue
        vals, base = {}, table.get("base")
        if base is not None:
            b = root.get(base)
            if not isinstance(b, dict):
                problems.append(f"{file}:{k}.base: base '{base}' not found in file")
                continue
            if "base" in b:
                problems.append(f"{file}:{k}.base: base '{base}' is itself derived")
                continue
            vals.update(b)
        own = [x for x in table if x != "base"]
        vals.update({x: table[x] for x in own})
        vals.pop("base", None)
        out[k] = (vals, own, base)
    return out


def _fill(vals, defaults, keys, problems, where):
    r = {}
    for k, d in defaults.items():
        r[k] = list(d) if isinstance(d, list) else d
    for k, v in vals.items():
        if k not in keys:
            problems.append(f"{where}: unknown key '{k}' (loader CheckKeys would reject)")
        r[k] = v
    return r


def build_ticks(cost, build_time, per100):
    return build_time if build_time > 0 else max(1, cost * per100 // 100)


def supply_of(cost, explicit, s):
    if explicit >= 0:
        return explicit
    return s["band_values"][0] if cost <= s["band1_max"] else s["band_values"][1] if cost <= s["band2_max"] else s["band_values"][2]


def load_rules(repo=REPO):
    rd = os.path.join(repo, "data", "rules")
    problems = []
    game = _read(rd, "game.toml")
    tick = game["game"]["tick_rate"]
    per100 = game["economy"]["build_ticks_per_100_credits"]
    sup = game["supply"]
    kits = {f: game.get("faction", {}).get(f, {}) for f in FACTIONS}

    raw = {n: _inherit(_read(rd, n + ".toml"), problems, n + ".toml") for n in ["warheads", "weapons", "vehicles", "infantry", "buildings", "upgrades"]}
    seen = {}
    for n, tab in raw.items():
        for i in tab:
            if i in seen:
                problems.append(f"{n}.toml:{i}: duplicate id, already in {seen[i]}.toml")
            seen[i] = n

    warheads = {}
    for i, (v, own, base) in raw["warheads"].items():
        w = _fill(v, WARHEAD_DEFAULTS, WARHEAD_KEYS, problems, f"warheads.toml:{i}")
        vs = list(w["versus"])
        if 5 <= len(vs) < len(ARMOR):
            vs = vs + [100] * (len(ARMOR) - len(vs))
        w["versus_list"] = vs
        w["versus"] = dict(zip(ARMOR, vs))
        w.update(id=i, faction=faction_from_id(i), base=base, splash_cells=w["splash"])
        warheads[i] = w

    weapons = {}
    for i, (v, own, base) in raw["weapons"].items():
        w = _fill(v, WEAPON_DEFAULTS, WEAPON_KEYS, problems, f"weapons.toml:{i}")
        if w["warhead"] not in warheads:
            problems.append(f"weapons.toml:{i}.warhead: unknown warhead '{w['warhead']}'")
        w.update(id=i, faction=faction_from_id(i), base=base,
                 range_cells=round(w["range"] / LEPTONS_PER_CELL, 2), min_range_cells=round(w["min_range"] / LEPTONS_PER_CELL, 2),
                 reload_seconds=round(w["rate_of_fire"] / tick, 3),
                 raw_dps=round(w["damage"] * tick / w["rate_of_fire"], 3),
                 instant=w["projectile_speed"] <= 0, users=[], death_weapon_of=[])
        weapons[i] = w

    def tier_sugar(e, provides=None):
        e["prerequisites_written"] = list(e["prerequisites"])
        kit = kits.get(e["faction"])
        if e["tier"] >= 2 and kit:
            name = kit.get("tier3" if e["tier"] >= 3 else "tier2", "")
            if name and name not in e["prerequisites"] and not (provides is not None and name in provides):
                e["prerequisites"] = e["prerequisites"] + [name]

    units = {}
    for kind, defaults, keys in (("vehicle", VEHICLE_DEFAULTS, VEHICLE_KEYS), ("infantry", INFANTRY_DEFAULTS, INFANTRY_KEYS)):
        fname = "vehicles" if kind == "vehicle" else "infantry"
        for i, (v, own, base) in raw[fname].items():
            u = _fill(v, defaults, keys, problems, f"{fname}.toml:{i}")
            u.setdefault("faction", faction_from_id(i))
            u.update(id=i, kind=kind, base=base, file=fname + ".toml")
            if kind == "infantry":
                u["locomotion"] = "foot"
                u.setdefault("can_garrison", not u["engineer"])
                u.setdefault("harvest_capacity", 0)
                u.setdefault("passengers", 0)
            armed = bool(u["primary_weapon"])
            u["veteran_resolved"] = u.get("veteran", armed)   # VET-1: absent = armed types rank
            u["domain"] = "air" if u["locomotion"] == "air" else "naval" if u["locomotion"] == "naval" else "ground"
            u["production_category"] = "infantry" if kind == "infantry" else "aircraft" if u["locomotion"] == "air" else "vehicle"
            tier_sugar(u)
            u["build_ticks"] = build_ticks(u["cost"], u["build_time"], per100)
            u["build_seconds"] = round(u["build_ticks"] / tick, 2)
            u["supply_resolved"] = supply_of(u["cost"], u["supply"], sup)
            u["speed_cells_per_second"] = round(u["speed"] * tick / LEPTONS_PER_CELL, 3)
            for key, lst in (("primary_weapon", "users"), ("death_weapon", "death_weapon_of")):
                wid = u[key]
                if wid:
                    if wid in weapons:
                        weapons[wid][lst].append(i)
                    else:
                        problems.append(f"{fname}.toml:{i}.{key}: unknown weapon '{wid}'")
            units[i] = u

    buildings = {}
    for i, (v, own, base) in raw["buildings"].items():
        b = _fill(v, BUILDING_DEFAULTS, BUILDING_KEYS, problems, f"buildings.toml:{i}")
        b.setdefault("faction", faction_from_id(i))
        b.update(id=i, kind="building", base=base, file="buildings.toml")
        if b["gate"]:
            b["wall"] = True
        tier_sugar(b, b["provides"])
        b["build_ticks"] = build_ticks(b["cost"], b["build_time"], per100)
        b["build_seconds"] = round(b["build_ticks"] / tick, 2)
        wid = b["primary_weapon"]
        if wid:
            if wid in weapons:
                weapons[wid]["users"].append(i)
            else:
                problems.append(f"buildings.toml:{i}.primary_weapon: unknown weapon '{wid}'")
        buildings[i] = b

    techs_root = _read(rd, "techs.toml")
    effects = {}
    for e in techs_root.get("effect", []):
        e = dict(e)
        t = e["target"]
        if t.startswith("weapon:"):
            tk, ids = "weapon", [t[7:]] if t[7:] in weapons else []
        elif t.startswith("warhead:"):
            tk, ids = "warhead", [t[8:]] if t[8:] in warheads else []
        elif t == "class:player":
            tk, ids = "player", []
        elif t == "class:building":
            tk, ids = "building", sorted(buildings)
        elif t == "class:vehicle":
            tk, ids = "vehicle", sorted(k for k, u in units.items() if u["kind"] == "vehicle")
        elif t == "class:infantry":
            tk, ids = "infantry", sorted(k for k, u in units.items() if u["kind"] == "infantry")
        elif t in units:
            tk, ids = units[t]["kind"], [t]
        else:
            ids = sorted(k for k, b in buildings.items() if k == t or t in b["provides"])
            tk = "building" if ids else "unresolved"
        e.update(target_kind=tk, target_ids=ids, used_by_upgrades=[],
                 attr_read_by_sim=(e["op"] != "enable" and e["attr"] in DERIVER_ATTRS.get(tk, [])))
        if e["id"] in effects or e["id"] in seen:
            problems.append(f"techs.toml:{e['id']}: duplicate id")
        effects[e["id"]] = e

    upgrades = {}
    order = sorted(raw["upgrades"])   # ordinal sort = upgrade mask bit index
    for i, (v, own, base) in raw["upgrades"].items():
        u = _fill(v, UPGRADE_DEFAULTS, UPGRADE_KEYS, problems, f"upgrades.toml:{i}")
        u.setdefault("faction", faction_from_id(i))
        u.update(id=i, kind="upgrade", base=base, file="upgrades.toml", mask_bit=order.index(i))
        tier_sugar(u)
        u["build_ticks"] = build_ticks(u["cost"], u["build_time"], per100)
        u["build_seconds"] = round(u["build_ticks"] / tick, 2)
        for eid in u["effects"]:
            if eid in effects:
                effects[eid]["used_by_upgrades"].append(i)
            else:
                problems.append(f"upgrades.toml:{i}.effects: unknown effect '{eid}'")
        upgrades[i] = u

    provides = {}
    for i, b in buildings.items():
        for p in b["provides"]:
            provides.setdefault(p, []).append(i)

    return dict(
        meta=dict(source=rd, tick_rate=tick, leptons_per_cell=LEPTONS_PER_CELL, armor_classes=ARMOR, factions=FACTIONS,
                  build_ticks_per_100_credits=per100,
                  notes=["times are sim ticks unless the key ends in _seconds; distances are leptons unless the key ends in _cells",
                         "prerequisites has tier sugar applied; prerequisites_written is the TOML list"]),
        game=game, start_kits=kits, warheads=warheads, weapons=weapons, units=units, buildings=buildings, upgrades=upgrades,
        effects=effects, provides=provides, loader_problems=problems)


def damage(dmg, warhead, armor):
    """Simulation.ComputeDamage: damage * versus% / 100, integer, minimum 1."""
    return max(1, dmg * warhead["versus"][armor] // 100)


if __name__ == "__main__":
    repo = sys.argv[1] if len(sys.argv) > 1 else REPO
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "rules_resolved.json")
    r = load_rules(repo)
    with open(out, "w") as f:
        json.dump(r, f, indent=1, sort_keys=True)
    print(f"wrote {out}: {len(r['units'])} units, {len(r['buildings'])} buildings, {len(r['upgrades'])} upgrades, "
          f"{len(r['effects'])} effects, {len(r['weapons'])} weapons, {len(r['warheads'])} warheads; "
          f"{len(r['loader_problems'])} loader problems")
    for p in r["loader_problems"]:
        print("  PROBLEM", p)
