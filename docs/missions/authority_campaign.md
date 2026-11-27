# Authority campaign, acts 1-4 (M8-12, D-163; auth_07 added s47, D-333/D-337)

Hero on the radio: Brigadier Wrenfield (`leader_auth_wrenfield`), as in auth_01. Cameos (existing names only): Administrator Pell (a2, a4), Chief Surveyor Fenn (a3), war-captain Calloway-Ross (a3, Left Behind), Lieutenant Dray (a4), Commissar Thrall (a4). Locale prefixes `a1.*` .. `a4.*`.

## Missions
- **auth_01 Survey Convoy** (act 1, Rust Belt): escort scrapers to the survey site, hold two raids, plant a Refinery on the pit. (Exists.)
- **auth_02 The Junction** (act 2, rustbelt_m2): hold a road junction for a relief column (visible timer, 4800 ticks); fortify with two Turrets; four Directorate raids from the east and north (counts scale with tier); hidden bonus: the listening mast on the ridge. Lose: the forward yard dies, or the column turns back at an unfortified junction.
- **auth_07 The Defector** (act 2, rustbelt_m2, play position 5 between auth_02 and auth_03; S47 pilot of the campaign expansion, design 22 section 6): no base, a sealed column (one carrier, one decon truck, two jeeps, six riflemen, two marksmen, two rocketeers); reach the Shaft Four spoil heaps before the 4800-tick "Rendezvous" timer, pick up Lieutenant Dray (unarmed engineer, `leader_auth_dray`) and bring him home alive; 30 s later the built-in AI wave (`AI.Wave` on the north-east bridge) and hunters from the east and south come for him; hidden bonus: Dray's reactor log in the culvert. Lose: Dray dies, the timer runs out, or the column is destroyed. Locale prefix `a7.`; par 300 s (sw47a, D-289); unlocks `auth_seismic_mortar` on win. The campaign order is auth_01, auth_02, auth_07, auth_03, auth_04 (auth_03 now requires auth_07).
- **auth_03 The Coast Road** (act 3, glass_coast_e1): escort two carriers across the dunes to the shore (landing-window timer 9000 ticks) while Directorate raiders hit the yard behind (two fronts); ambush on the road, a Left Behind cameo band in the dunes, a six-structure picket and a garrison counter-attack at the beach; hidden bonus: Fenn's survey cache (+600 cash).
- **auth_04 The Sunrise Device** (act 4, ash_reckoning, finale): cross the river (six units on the south bank), break the built-up Directorate base (built-in AI at the mission tier via `AI.SetTier`, `AI.Defend`, `AI.Wave` on the crossing) and destroy `dir_sunrise` before the "Sunrise primed" timer (16000 ticks) ends; two raids on the yard from the west; hidden bonus: the reactor vent that feeds the device (Dray). Ends in the Authority's victory.

## Tech climb
| mission | starts_with adds | unlocks_on_win |
|---|---|---|
| auth_01 | auth_power, auth_refinery, auth_barracks, auth_rifleman, auth_jeep, auth_wall | auth_turret |
| auth_02 | auth_turret, auth_factory, auth_tank, auth_engineer, auth_ration_hall | auth_engineering (tier-2 gate), auth_searchlight, auth_decon_truck, auth_marksman, auth_carrier |
| auth_07 | auth_02's list | auth_seismic_mortar |
| auth_03 | the above | auth_signals_hq (tier-3 gate), auth_howitzer, auth_bulwark, auth_gate, auth_flak, auth_airfield |
| auth_04 | the above | auth_atomic_battery (superweapon) |

## Notes
- The supply cap is on (SUP-1): every mission from act 2 pre-places `auth_ration_hall`s, or the player cannot field the force.
- The AI does not run in `Scripting.Tests`, so tests use the scripted raids and static enemy bases; real play adds the built-in AI (`AI.Defend` keeps it home in acts 2-3; act 4 lets it build and defend at the mission tier).
- Bulwarks and Howitzers need the tier-3 gate (`auth_signals_hq`); auth_04 pre-places two of each so the player has them.
- Win ticks with the test bots: auth_07 about 2060 (bot home by tick ~2060, every tier, sw47a), auth_02 about 4800 (timer), auth_03 about 2000-3000, auth_04 about 4400.

## Open questions
1. auth_04 unlocks `auth_atomic_battery` (the Authority superweapon) as the finale reward. Fine for a faction whose lore calls the Sunrise "a second Exchange"? Alternative: no unlock.
2. The pre-placed `dir_sunrise` is a real superweapon, so the built-in AI may fire it (3000-tick charge) before the 16000-tick timer; intended as a visible threat, confirm.
3. The ending (owner-approved, D-164) hints at Wrenfield's list and at the Tern Point names; her brother is not named (classified entry).
4. Wave sizes were tuned with MissionBot only; a human playtest of tiers 1 and 5 is needed.
