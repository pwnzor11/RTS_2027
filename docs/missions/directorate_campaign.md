# Directorate campaign (M8-12)

Hero on the radio throughout: the Voice (Odette Larkin, `leader_dir_larkin`). Cameos: Chief Surveyor Fenn (`leader_auth_fenn`, dir_03, dir_04),
Commissioner Strand (`leader_exp_strand`, dir_03), Commissar Thrall (`leader_dir_thrall`, dir_04: Order Seven). No new names.
All four missions: schema 2, 2-3 primary objectives, one hidden bonus, visible timers, enemy pressure by unit count and tier only (D-061).

## Missions
- **dir_01 The First Hatch** (Rust Belt, act 1): open a Hatch on the marked ground and hold it against two Authority waves. (pilot)
- **dir_02 The Convoy Roads** (Rust Belt, act 2): raid the three Authority road posts (searchlight mast and a rifleman each) and destroy both convoys; the first fallen post calls the road reserve. Bonus: the road depot.
- **dir_03 The Launch Sites** (Glass Coast, act 3): two fronts. Destroy each pad's watch light and stand a citizen on the pad for 300 ticks (Pad Seven centre, Pad Nine north-east), then break both Authority relief columns (timers 3600 and +1500). Fenn warns on the radio, Strand asks the fleet be left alone. Bonus: the ridge watch post.
- **dir_04 The Sunrise** (Ash Reckoning, act 4): the pre-placed Sunrise Device charges 3000 ticks from tick 0 (timer at 3050). Hold the Device and the tunnel-head Hatch (south bank, tag `head`) against three Authority waves and a last `AI.Wave`, then fire the Device (Special command) at the Authority Ash Ridge command (tag `hq`, an Engineering building). The Device fires only with a finished hatch within 8 cells of the target. Losing the shaft, Device or head is a loss. Bonus: the west bank post. Ends in victory, then `directorate_ending`.

## Tech climb
| Mission | Starts with (new in bold) | Unlocks on win |
|---|---|---|
| dir_01 | vent, muster hatch, hatch, pillbox, conscript, ashthrower | sapper |
| dir_02 | + **sapper, smelter vent, assembly bore, scraper, rocket truck** | isotope works (tier-2 gate), mole, rocket nest |
| dir_03 | + **isotope works, mole, rocket nest** | commissar, rad mortar, mast, mooring, dirigible |
| dir_04 | + **commissar, rad mortar, mast, mooring, dirigible** | committee hall (tier-3 gate), Sunrise, reactor crawler, great dirigible, landtrain |

## Notes for balance and design
- Conscripts (40 hp) lose badly to Authority riflemen (dir_01 note). Waves and garrisons are small and scale by tier; the bot tests use mass conscripts and stage before each assault.
- The Ashthrower's ash (contaminate 40) burns the player's own infantry: a bot with Ashthrowers lost 25 conscripts to its own radiation in dir_02. The briefings warn the player; the bot tests train conscripts only.
- A supply cap (shaft 20 + 10 per bunk gallery) caps the army; every mission pre-places two galleries (40 units).
- The glass_coast and ash_reckoning maps are sea-wrapped or river-split: arrivals use `Reinforce.Zone`.

## Open questions
1. Are the pre-placed bases in dir_02..04 (shaft, vents, hatch, bore) the right start, or should the player rebuild from the shaft each act (like dir_01)?
2. dir_04: the Authority base to the south only builds and sends waves (`AI.Defend`); should the player be able to destroy it for a bonus? Win is firing the Sunrise (Order Seven, D-086).
3. Ending text is DRAFT for owner review (D-086), including the Dray/Fenn/Left Behind/Expedition lines.
4. Voice lines for the new keys are listed in each `[voice]` table but no audio exists.
