# faction-math: computed audit of data/rules (session 18)

Python 3.11+ (`tomllib`), no third-party packages. Writes only into this folder (outputs are git-ignored). Run from this folder:

```
python3 resolve_rules.py     # -> rules_resolved.json           (booklet stat pages)
python3 techtree.py          # -> techtree_<faction>.json/.dot, techtree_report.md   (dot -Tsvg techtree_authority.dot -o a.svg)
```
`resolve_rules.py [repo_root] [out.json]` takes another checkout; the other scripts import `load_rules()` from it (repo path = `REPO` at the top).

## rules_resolved.json
Mirrors `src/Data/RulesLoader.cs` as read on 2026-11-11: one-level `base` inheritance, loader defaults for every optional key, faction from the
`faction` key or the id prefix, 5-7 entry `versus` lists padded with 100, tier sugar, cost-derived build time, supply bands. The loader's own
checks that the script repeats are listed in `loader_problems` (empty today).

Units: **times are sim ticks (20 per second) unless the key ends in `_seconds`; distances are leptons (256 per cell) unless the key ends in `_cells`.**

| Section | Keyed by | Fields added to the TOML keys |
|---|---|---|
| `meta` | | tick_rate, leptons_per_cell, armor_classes (order of every `versus_list`), factions |
| `game`, `start_kits` | | game.toml as written; kit per faction (conyard, units, tier2 / tier3 provides names) |
| `warheads` | id | `versus` = {armour: percent}, `versus_list` (padded), `faction`, every optional key at its loader default |
| `weapons` | id | `range_cells`, `min_range_cells`, `reload_seconds`, `raw_dps` (damage x 20 / rate_of_fire before armour), `instant`, `users` (units and buildings with it as primary weapon), `death_weapon_of` |
| `units` | id | `kind` (vehicle / infantry), `faction`, `domain` (ground / air / naval), `production_category` (infantry / vehicle / aircraft: which `produces` building makes it), `prerequisites` (tier sugar applied) and `prerequisites_written`, `build_ticks`, `build_seconds`, `supply_resolved`, `speed_cells_per_second`, `veteran_resolved` (can it rank) |
| `buildings` | id | same prerequisite and build-time fields; `wall` is true for gates too (as in the loader) |
| `upgrades` | id | `mask_bit` (index in the player's upgrade mask), build time, prerequisites with tier sugar |
| `effects` | effect id (techs.toml) | `target_kind`, `target_ids` (every type the effect rewrites), `used_by_upgrades`, `attr_read_by_sim` (false = RulesDeriver ignores it) |
| `provides` | provides name | buildings that provide it |

Damage as the sim computes it (`Simulation.ComputeDamage`): `max(1, weapon.damage * warhead.versus[target armour] // 100)`; a splash-1 warhead
does full damage in the impact cell and half in the 8 cells around it; a shot is fired every `rate_of_fire` ticks. Veterancy (game.toml
`[veterancy]`) multiplies damage dealt, damage taken and reload per rank. Upgrades are NOT applied in this file: `effects` says what each would change.

## Other outputs
- `techtree_<faction>.json`: `nodes[id]` = kind, tier, cost, build_ticks, `producer`, `requires`, `chain` (cheapest set of buildings needed first),
  `min_cost`, `min_ticks` / `min_seconds`, `obtainable`; `edges` (prereq / built at / free unit / deploys / packs / morphs); `cycles`; `dead_ends`.
