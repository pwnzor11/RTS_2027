# Resource and Economy Systems in RTS Games

**Summary**

- RTS economies fall into a few families: a single harvested field (Command & Conquer), worker-saturated patches (StarCraft), several gathered goods with drop-off buildings (Age of Empires), flow-based income and expenditure (Supreme Commander, Beyond All Reason), territory-linked income (Company of Heroes), and fixed or scripted budgets.
- In the C&C family the real economic variable is harvester trip time, not harvester count. Income scales roughly linearly with harvesters until the nearby field is worked out or the trips get long.
- OpenRA's open source shows a clean, small implementation: per-cell resource density with a cap, a harvester that docks at a refinery, and a player ledger that charges production costs progressively over build time.
- Renewability is a map-level knob. Tiberium that regrows keeps games going and makes stalemates less likely; finite minerals push toward expansion races.
- Modern games mostly keep one or two resources. The trend in 2015 to 2026 titles is fewer resource types, with the tension moved to map control and timing.
- Recommendation for RTS_2027: a single field resource with C&C-style harvesters and progressive payment, with power as a gating secondary resource rather than something to gather. Details are under Implications.

## 1. Taxonomy of RTS economies

| Family | Examples | How it works | Feel | Decisions it creates |
|---|---|---|---|---|
| Single harvested field | C&C Tiberian Dawn, Red Alert (ore), Tempest Rising (Tempest) | Vehicles load from ore cells and drive to a refinery | Logistics-heavy, base spread out | Refinery placement, harvester count, defending trips, claiming new fields |
| Two resources, worker saturation | StarCraft (minerals and gas) | Workers are assigned to fixed patches or geysers; each patch has a useful worker count | Macro-heavy, production-focused | Expansion timing, worker production rate, harassing mineral lines |
| Several gathered goods | Age of Empires II and IV (food, wood, gold, stone) | Villagers gather by type and carry to drop-off buildings; farms are a renewable food source | Broad, sprawling economy | Villager allocation, drop-off placement, tech timing |
| Flow economy | Supreme Commander, Beyond All Reason | Income and expenditure are rates; storage buffers the difference; excess is wasted | Spreadsheet-like, efficiency-driven | Matching spending to income, avoiding stalls, when to upgrade |
| Territory-linked | Company of Heroes, Dawn of War (capture points) | Controlling points produces income; supply lines to headquarters can be cut | Map-control-driven, anti-turtle | Where infantry goes, cutting enemy supply, forward positions |
| Fixed or scripted | Many campaigns, RTS prototypes | A set budget, scripted deliveries, or no gathering at all | Story-first | Mostly spending choices; no expansion race |

**Harvested field (C&C).** A harvester fills at a field, drives to the nearest refinery, unloads, and goes back. The field is a map resource that the player does not control directly. Denying the field or the path is the main economic attack. Income is high in the early game and then depends on how far the remaining ore is from the refinery.

**Worker saturation (StarCraft).** A Blizzard guide for StarCraft II says to keep 2 to 3 workers on each mineral patch and 3 on each geyser, with the ideal range of 16 to 24 mineral workers plus 6 on gas. It also notes that a base is fully saturated above 24 workers on minerals and gas together. The Tentonhammer StarCraft II beta guide (2010) gives the mechanism: a worker carries 5 minerals or 4 gas per trip, and once a worker leaves a patch another worker can mine there, which is why two per patch works. Rich clusters carry 7 per trip in the Blizzard guide. Past saturation, extra workers add little. The Tentonhammer article reports that a second geyser did not double gas income, with each worker's gas pull cut by 50 percent. That number is from the 2010 beta and is not verified for the current game.

**Several gathered goods (Age of Empires).** Villagers gather one good at a time. Drop-off buildings (town centers, mills, storehouses or camps) shorten walking. The Age of Empires II farm is the renewable food source: the Age of Empires wiki says a farm yields 175 food base, rising to 550 with all Imperial Age farming upgrades, and must be reseeded after it is exhausted. Only one villager can work a farm at a time at 0.4 food per second, so farms cap food income and force players to build more farms. In Age of Empires IV, the early economy scenario ("Early Economy") teaches the same principle: keep the town center producing villagers and put them on the nearest good. Gathering rates for wood, gold and stone were not verified in this research.

**Flow economy (Supreme Commander, Beyond All Reason).** Each resource has an income rate, a spend rate, and a storage buffer. Mass comes from extractors on fixed spots and from reclaimed wrecks. Energy comes from generators that do not need expansion. The Supreme Commander wiki says a basic solar always produces 20 energy, an advanced solar 75, and a T1 geothermal plant 300. The Beyond All Reason guide says extractors produce "between 1-3 metal per second, depending on the map," and that wrecks return "1/3 to 2/3 of their metal cost." This family produces the feeling of managing a balance sheet. A stall (spending faster than income) is the main risk. The Supreme Commander developer analysis says the goal is "to keep expenses as closely tied to income as possible."

**Territory-linked (Company of Heroes).** The map is split into territories, each with one control point. Owning points produces income, and resources only flow from territories with a supply link to headquarters. A developer article on Company of Heroes describes the system as "rules-light and possibilities-heavy" and says cutting an enemy's connection to headquarters can disrupt their economy. The design aim, as described, is to pull players out of their bases: the economy is "aggressively anti-chuffa" (anti-turtle). The cost is that the economy depends on infantry being on the map.

**Fixed or scripted.** Campaign missions often give a fixed budget, no base, or scripted deliveries. This makes pacing easy to control, because the designer decides when resources arrive. It is the best fit for a story-driven campaign and needs no harvesting AI.

## 2. Harvester mechanics in detail

**Capacity and trip time.** The real economic variable is trip time, which equals the time to fill, plus the drive to the refinery, plus the unload time, plus the drive back. Capacity matters only as a multiplier on trip frequency. A rough model:

    income per harvester = capacity x value_per_unit / (fill_time + 2 x distance / speed + unload_time)

A harvester 5 cells from a refinery and a harvester 40 cells away can have very different incomes even if they are the same unit. The Blizzard guide for StarCraft II notes that distant bases slow gathering for the same reason. (The formula is a design model, not a sourced figure.)

**OpenRA implementation (Tiberian Dawn rules).** OpenRA is GPL, and its open source is a good reference. Its `mods/cnc/rules/vehicles.yaml` gives the Harvester `Capacity: 20` (counted in resource units), a `SearchFromProcRadius: 15` and `SearchFromHarvesterRadius: 8`, a `BaleLoadDelay: 12` and `BaleUnloadDelay: 6`, and `EmptyCondition: no-tiberium`. The Harvester trait (`OpenRA.Mods.Common/Traits/Harvester.cs`, as summarized in this research) has these parts:

- `CanHarvestCell` accepts a cell only if it has a harvestable resource on ground layer 0 that the harvester is allowed to take.
- A player order picks the nearest free cell through `mobile.NearestCell` and claims it through `claimLayer.TryClaimCell`. Claiming is the only shown mechanism for keeping harvesters off the same cell.
- `IsFull` and `Fullness` drive the return decision; a full harvester slows to 85 percent speed (`FullyLoadedSpeed`).
- Docking uses a `DockClientManager`. `OnDockTick` unloads in bales, with a delay between each bale. Only same-owner or allied refineries accept deliveries.
- After a dock, `OnDockCompleted` re-queues `FindAndDeliverResources`, so the harvester goes back to work automatically.

Two parts were not verified. The search logic that picks a field and a refinery is in `FindAndDeliverResources`, which was not in the excerpt read. The `ResourceRefineryDirectionPenalty: 200` value suggests refinery direction matters when choosing a trip, but how it is used was not confirmed.

**Field representation.** OpenRA's `ResourceLayer.cs` stores each cell's resource type and a byte density. `MaxDensity` is set per resource type (the default is 10; the Tiberium entries in `mods/cnc/rules/world.yaml` use 12). `AddResource` clamps to the cap, and `RemoveResource` clears a cell when density reaches zero. Neighbour counting runs once at load time to compute density when `RecalculateResourceDensity` is set. The file has no runtime growth or spread logic, so Tiberium regrowth must live in another component. That component (`ResourceSpawner`) was not read.

**Depletion.** Depletion is per cell. A harvester keeps working until the cell is empty, then looks for the nearest remaining resource within its search radius. When nearby fields run out, the OpenRA Tiberian Dawn guide recommends building a new refinery close to the new deposit, and calls this a vulnerable moment.

**Refinery queueing and capacity.** The OpenRA guide says a refinery "can typically support a certain number of Harvesters (often two or three)". Over-production clogs the refinery. Tempest Rising's refinery has a base storage of 500 credits, with a Storage Pod upgrade raising it to 1,000, and its harvester (SG10) holds 800 credits of Tempest and gathers 100 per second. The Tempest Rising refinery's drop-off rate was lowered from 260 to 220 credits per second in patch 1.7.1. A refinery that is full holds extra Tempest in a silo. This is the same two-stage idea (drop-off rate plus storage) that Supreme Commander uses with storage.

**The refinery sweet spot.** Placing the refinery next to the field minimizes trip time. Placing it farther away protects it and the harvesters from raids at the field. The Tempest Rising preview says a refinery lets you harvest, and the guide recommends placing refineries near the field to shorten harvester trips. The trade-off between safety and speed is the real "sweet spot" decision, not a single distance.

**Scaling.** In a C&C economy, income scales roughly linearly with harvesters until (a) the field's cells are depleted at the rate harvesters take them, or (b) trips get long enough that more harvesters queue or wander. The OpenRA guide says the Tiberian Dawn economy is competitive with "at least 2 refineries (or 2 harvesters) early." Tiberium spread (where it grows back) is what keeps the field from running out. How that spread works in the original games was not verified.

## 3. Spending side

**Lump sum, drain, or upfront.** Three models are used:

- **Upfront (StarCraft):** the full cost is paid when production starts. The player sees a clear go or no-go decision.
- **Drain over build time (C&C):** cost is paid gradually while the item builds. OpenRA implements this as the default. In `ProductionQueue.cs`, each tick charges `TotalCost x RemainingTime / TotalTime` owed, through `TakeCash(..., true)`. If the player has no money, production stalls with no separate "waiting for funds" state, and progress resumes when money arrives. This means money and build time both matter, and the player can see cash being used up.
- **Lump sum with a ledger (OpenRA `PlayerResources.cs`):** a separate `Resources` pool (spent first) and `Cash` pool, with `ResourceCapacity` set by storage. `TakeCash` fails in full or succeeds in full, and refunds are a simple wrapper. OpenRA's `PayUpFront` option switches a queue to upfront payment.

**Build time vs money.** Which one is the bottleneck decides how the game feels. With drain-over-build-time, a fast economy makes build time the limit, and an expensive unit makes money the limit. The Supreme Commander developer analysis says the goal is to match spending to income, and that underspending wastes resources while overspending slows production. Build power (the rate at which constructors work) is a separate limit there.

**Power as a secondary resource.** In classic C&C, power is a gate rather than a gathered resource: a building with insufficient power stops working. The Tiberian Dawn strategy guide treats power the same way: "without enough power, your base grinds to a halt." Tempest Rising makes power a requirement for efficient operation, and the Dynasty can overwork plants to boost production. Stormgate's Celestial power system is a global pool fed by power banks; a MassivelyOP review says buildings are "severely limited" until power is restored, and the reviewer found it hard to understand. The lesson is that a gate is easier to read than a pool that also drains units.

**Supply and population.** Population caps (houses, pylons, supply depots) are a form of resource gating. The Blizzard guide's supply example (building a supply depot at nine SCVs so it finishes at 10/10) shows how supply timing becomes part of macro.

**Unit upkeep, repair and refunds.** The Tempest Rising refinery is refunded only at its building value when sold, not for the harvester it spawned. Rapid Deployment (a doctrine) returns a 50 percent credit bonus on sale. Repair cost is shown as a separate figure (2,000 credits for the Tempest Rising refinery). Upkeep in the flow games is part of the drain model; in Supreme Commander, shields and radar draw continuous energy.

## 4. Pacing and balance

**Time to first attack.** Designers tune starting money, harvester count and build times together. The Tempest Rising harvester was adjusted several times (harvest rate 100, then 110, then back to 100; HP from 1,000 to 900). This shows how sensitive the early curve is to a single number.

**Starting money.** Starting cash sets the opening. OpenRA exposes it as a lobby option (`startingcash`, with `DefaultCash: 5000`). A high starting budget speeds the first attack; a low one favours expansion.

**Skirmish vs campaign.** Skirmish economies are open-ended: the player's choices set the income. Campaigns typically use fixed budgets, scripted fields, or no base. This is easier to pace and easier for the AI, but it makes a campaign feel different from skirmish. A 2010 analysis of the Tiberian campaign is not available here; this is an inference from general practice.

**The AI's economy.** Several RTS games give the AI a resource advantage on higher difficulties. A player forum thread about Sins of a Solar Empire reports that the AI's bonus is larger than a human could match, and that developers chose this to make fights harder. These are player claims, not designer statements, and no source was found that confirms exact multipliers for C&C or StarCraft. Cheating has two costs: it is hard to notice from the outside, and players who learn the trick feel cheated. The alternatives are (a) a transparent AI harvest multiplier that is shown, (b) more AI harvesters or expansions rather than more income per unit, and (c) a better AI that uses the same rules. Unverified: whether the original C&C AI gets a multiplier at higher difficulties.

## 5. Renewability

**Tiberium is renewable in some configurations.** The Twisted Insurrection wiki (a C&C mod) describes Tiberium that regrows: a field with a blossom tree or large crystal regenerates every minute and is indestructible, and the wiki says "Tiberium is an infinite resource." Fields without those sources do not regrow by default. This is a mod, so it does not show the base game's rule.

**Finite minerals.** StarCraft minerals are finite: a patch collapses when empty, and geysers collapse too. The Blizzard guide notes that depleted clusters disappear, so workers must move to fresh fields to keep income steady. This makes map geography decisive: every base has a clock.

**Game length and stalemates.** A renewable field makes a stalemate less likely, because even a player holding a small area keeps earning. A finite field forces expansion and fights over the next base. The OpenRA guide's advice to build refineries near new deposits, and to treat that as a vulnerable moment, is a finite-field behaviour. The Tempest Rising preview mentions harvest being central to the war effort, but does not discuss regrowth.

**Map-level knobs.** Each family has a few: field density (`MaxDensity` in OpenRA), field count and spacing, regrowth rate and seed, field richness (Twisted Insurrection uses several richness levels), and whether the map has renewable sources at all.

## 6. Modern takes (2015 to 2026)

- **Beyond All Reason (Recoil engine, 2019 onward; Steam release announced for June 2026).** Two resources, energy and metal. Metal comes from extractors on deposits and from reclaiming wrecks. The guide calls the design a "streaming economy where resources on the map are not finite," which lets players field more expensive units over time. The reclaim loop is a strong comeback mechanic.
- **Tempest Rising (2025, Slipgate Ironworks and 2B Games).** A C&C-style game with one gathered resource, Tempest, and a power requirement. The GDF harvester and refinery are close to the classic loop: the refinery spawns a harvester, the harvester works the nearest field, and a silo stores excess. Reviewers praised it for returning to the C&C feel. The design choice that matters here is that the field is readable, so the player can see where harvesters go.
- **Stormgate (Frost Giant, early access 2024, full release August 2025).** Power is a global energy pool fed by banks; the author of a MassivelyOP review called the Celestials' power system a "learning cliff" and said the power resource was hard to understand. Worker automation (BuddyBot) was optional and judged "very slow and very dumb." The lesson is that an economy can feel busy without being fun when most of its work is automated.
- **Age of Mythology Retold and Age of Empires IV.** Not reviewed in depth in this research. The Age of Empires II farm and the AoE IV early economy show the gathered-goods model; details of later patches were not verified.
Patterns in the newer games: one or two resources instead of four; automation that runs without the player; economy tied to control points or power; and a readable field so players can see the economy's shape at a glance. Regretted choices in the reviews are opaque power systems and economy micro that feels like busywork.

## Implications for RTS_2027

- **Economy family for the C&C feel.** A single harvested field (Tiberium-like ore) with harvesters is the closest match to "classic C&C." Its design levers are fill time, capacity, refinery distance and field density, and they map directly onto a simple implementation.
- **Power as a gate, not a gathered resource.** Following C&C and Tempest Rising, power limits buildings but is not a second resource to mine. This keeps the economy to one number the player must track, and avoids the Stormgate confusion.
- **Progressive payment.** Use OpenRA's drain-over-build-time model (cost owed = total cost x remaining time / total time, stall when funds are short). It makes build time and money both matter and gives a visible cash drain. Trade-off: it needs a careful refund path for cancelled items.
- **Per-cell resource layer.** Store density per cell with a `MaxDensity` cap (OpenRA uses 10 by default and 12 for Tiberium). Trade-off: cells cost memory and make the map a tile grid; for a 2D sprite game this is cheap.
- **Harvester state machine.** States: idle, seek, load, return, dock, unload. Dock to a refinery with a per-refinery queue. Trade-off: the state machine is the most AI-sensitive part, and bugs there look like broken economy.
- **Renewable fields for campaign and skirmish alike.** A slow regrowth rule (for example, a cell with density at or above a threshold seeds an adjacent clear cell on a timer) keeps stalemates rare. Trade-off: unbounded regrowth can make maps grow without limit, so cap it. This is a design proposal, not a sourced figure.
- **Campaign budgets as scripted resources.** Missions can use fixed budgets or scripted deliveries and skip the harvester AI. Trade-off: two economy paths in the code. Keep the same ledger so the rest of the game does not care which path is used.
- **AI economy.** Prefer fair AI economics (same rules, more harvesters or expansions) over hidden income bonuses. If a difficulty bonus is needed, make it visible and keep it modest. Trade-off: fair AI is harder to write, but it is the one the owner can learn from.
- **Implementation primitives any option needs:** (1) per-cell resource type and density with cap, (2) harvester state machine with a claim on each cell, (3) an income ledger with `Cash` (spendable) and `Resources` (storage-capped) separated, (4) refinery or drop-off objects with a capacity and a unload rate, (5) a progressive cost tick in the production queue, (6) a regrowth timer if renewable fields are chosen.
- **Recommendation:** Option A, below. It gives the most C&C feel for the least code, and the campaign can use the same ledger with scripted fields.

**Candidate designs for RTS_2027**

| | A. Harvested field (recommended) | B. Two resources with saturation | C. Flow economy with territory |
|---|---|---|---|
| Resources | One ore-like field; power as a gate | Minerals (patches) and gas (geysers); power optional | Metal (extractors) and energy (generators) |
| Feel | Closest to classic C&C | StarCraft-style macro | Supreme Commander or BAR |
| Campaign fit | High: scripted fields, fixed budgets | Medium: patches are easy to script | Low: needs a lot of rate balancing |
| AI simplicity | Medium: harvester state machine is the hard part | Low: AI must manage saturation | Low: AI must match spend to income |
| Code | Per-cell layer, harvester FSM, ledger | Patch and worker assignment, ledger | Two rate ledgers, storage, stall logic |
| Main risk | Harvester pathing and jams | Dull macro if saturation is too strict | Stalls and balance tuning |

**Recommendation: Option A.** Use one harvested field with progressive payment and power as a gate. Start with a regrowth rule that is simple to tune. Keep the campaign on the same ledger with scripted fields and fixed budgets.

## Open questions for the owner

1. **How close to classic C&C should the economy feel?** Options: (a) faithful, with harvester trips and refinery defence as the main economic play; (b) simplified, with harvesters that auto-return and fewer decisions; (c) hybrid, with harvesters and a second resource for the campaign.
2. **Should fields regrow?** Options: (a) yes, to keep stalemates rare; (b) no, to force expansion and fights; (c) only in skirmish, with finite campaign fields.
3. **Should power be a gate, a resource to mine, or both?** Options: (a) gate only, as in C&C; (b) a gathered resource with a plant on each field; (c) a global pool that drains units.
4. **How should the AI get its economy on harder difficulties?** Options: (a) same rules, more harvesters; (b) a visible harvest multiplier; (c) a hidden income bonus.
5. **Should build cost be paid progressively (drain) or upfront?** Options: (a) progressive, as in C&C; (b) upfront, as in StarCraft; (c) progressive for buildings, upfront for units.

## Sources

**Code (OpenRA, GPL, read through fetched excerpts; line numbers approximate)**

- [OpenRA `mods/cnc/rules/vehicles.yaml` (HARV definition)](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/mods/cnc/rules/vehicles.yaml)
- [OpenRA `mods/cnc/rules/world.yaml` (Tiberium resource layer)](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/mods/cnc/rules/world.yaml)
- [OpenRA `OpenRA.Mods.Common/Traits/Harvester.cs`](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/Harvester.cs)
- [OpenRA `OpenRA.Mods.Common/Traits/World/ResourceLayer.cs`](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/World/ResourceLayer.cs)
- [OpenRA `OpenRA.Mods.Common/Traits/Player/PlayerResources.cs`](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/Player/PlayerResources.cs)
- [OpenRA `OpenRA.Mods.Common/Traits/Player/ProductionQueue.cs`](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/Player/ProductionQueue.cs)

**Web (read)**

- [Beyond All Reason, "In depth look at economy"](https://www.beyondallreason.info/guide/in-depth-look-at-economy)
- [Beyond All Reason, Wikipedia](https://en.wikipedia.org/wiki/Beyond_All_Reason)
- [Supreme Commander Wiki, Energy](https://supcom.fandom.com/wiki/Energy)
- [Supreme Commander: Forged Alliance dev journal analysis](https://forums.wincustomize.com/487549)
- [Blizzard, StarCraft II game guide: resources](https://news.blizzard.com/en-us/article/4488900/game-guide-resources)
- [Tentonhammer, StarCraft II new economy features and basics (2010 beta)](https://www.tentonhammer.com/guides/starcraft-ii-new-economy-features-and-basics)
- [Liquipedia, StarCraft Macromanagement](https://www.liquipedia.net/starcraft/Macro) (read; no numeric figures)
- [Twisted Insurrection Wiki, Tiberium](https://twistedinsurrection.fandom.com/wiki/Tiberium?oldid=5350) (a mod, not the base game)
- [C&C Wiki, Harvester (Rivals)](https://cnc.fandom.com/wiki/Harvester_(Rivals)) (Rivals, not the base game)
- [Wikipedia, Command & Conquer (1995)](https://en.wikipedia.org/wiki/Command_%26_Conquer)
- [ludo.guide, C&C Remastered: ore refineries and harvesters](https://www.ludo.guide/guide/command-and-conquer-remastered-collection/game-systems-and-mechanics/resource-gathering-and-economy/ore-refineries-and-harvesters)
- [ludo.guide, C&C Remastered: economic structures and expansion](https://www.ludo.guide/guide/command-and-conquer-remastered-collection/getting-started/core-gameplay-mechanics/base-building/economic-structures-and-expansion)
- [OpenRA Tiberian Dawn strategy guide (mirror)](https://github-wiki-see.page/m/UberWaffe/OpenRA/wiki/Tiberian-Dawn-Strategy-Guide)
- [Tempest Rising Wiki, GDF Harvester (SG10)](https://tempestrising.wiki.gg/wiki/GDF_Harvester)
- [Tempest Rising Wiki, GDF Refinery](https://tempestrising.wiki.gg/wiki/GDF_Refinery)
- [GodIsAGeek, Tempest Rising hands-on preview (Jan 2025)](https://godisageek.com/2025/01/tempest-rising-harks-back-to-the-golden-age-of-the-rts-hands-on-preview/)
- [Wikipedia, Tempest Rising](https://en.wikipedia.org/wiki/Tempest_Rising)
- [Wikipedia, Stormgate](https://en.wikipedia.org/wiki/Stormgate)
- [MassivelyOP, "Not so massively: Stormgate was not ready for early access" (Aug 2024)](https://massivelyop.com/2024/08/02/not-so-massively-stormgate-was-not-ready-for-early-access/)
- [Game Developer, "On Company of Heroes"](https://www.gamedeveloper.com/design/on-company-of-heroes)
- [Age of Empires Wiki, Farm (Age of Empires II)](https://ageofempires.fandom.com/wiki/Farm_(Age_of_Empires_II))
- [Age of Empires Wiki, Early Economy (Age of Empires IV)](https://ageofempires.fandom.com/wiki/Early_Economy_(Age_of_Empires_IV))
- [Player forum thread on AI resource advantages (Sins of a Solar Empire)](https://forums.wincustomize.com/427622) (player opinion, not designer statements)

## Gaps

- Original Tiberian Dawn and Red Alert harvester capacity in credits, trip times, and the exact spread algorithm were not verified from a primary source.
- No verified source for Red Alert or Tiberian Dawn AI economy cheats.
- Company of Heroes, Dawn of War, Age of Mythology Retold and ZeroSpace economies were only partly covered.
- Supreme Commander storage capacities and income rates were not given in the sources read.
