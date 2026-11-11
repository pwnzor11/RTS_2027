FILE: not written. The harness refused writing /home/claude/audit/ai/findings.md ("subagents should return findings as text"), so the full findings are below in the brief's format. My evidence scripts and outputs are in /home/claude/audit/ai/ (ids.py, sym.py, spawn.py, regions.py, snapcheck.py, ship_probe.txt, gc_trace1.txt, maps/).
COUNTS: critical 2 / major 6 / minor 5 / idea 0
TOP 5:
- AI-01 major SIM: Glass Coast 55 v 5 explained. Supply tenders spawn from the top-left of the edge list, so start 0 gets its shipments and start 1 waits (7 520 v 0 credits at 3 min, measured).
- AI-02 critical SIM: Expedition on Mountains layout 1 at size 96 still gets no Command Ship in 3 of 8 seeds (sea is 19 cells away, fallback stops at 16). HANDOFF says this is fixed.
- AI-04 critical CODE: a ready structure that can't be placed blocks the AI structure lane for good. Glass Coast seed 4: both Expedition AIs stop at 2 buildings.
- AI-05 major CODE: tiers 4-5 build no vehicles while an aircraft is in production.
- AI-07 major CODE: the one stored API key is sent to whatever provider or URL is set, so an Anthropic key can go to an OpenAI-compatible (often plain HTTP) server.
NOT COVERED: PersonaCatalog, SummaryHistory, most of BattlefieldSummary, the carrier/strike half of ExpeditionTactics (skimmed), the raid and salvage bodies of LeftBehindTactics, GarrisonManager/HarassManager/ScatterManager logic (only their snapshots checked), tests/AI.Tests and tests/Llm.Tests.

# ai: src/AI, src/Llm, data/ai, data/llm, Difficulty* — findings
Scope read: everything listed above except the NOT COVERED items. I followed into PlayerView, Simulation.Shipments, Simulation.M5, Simulation.BurrowMove, SkirmishStart, MapGenerator (landing markers), CommanderSet, LlmSettingsModel, SimHost and AiCommands.
Evidence runs used the prebuilt Release Tools.dll (main e059f0f, built 2026-11-11 02:12): `map gen`/`map ascii` and short niced `ai match` games of at most 6 000 ticks. I ran no build or test.

## Summary
The Expedition start skew is the sim's tender spawn rule (AI-01). Expedition setup still breaks on Mountains: no Command Ship in some seeds (AI-02), or a ship stuck in an inland lake (AI-03). There are three ways the AI can stall: an unplaceable ready structure (AI-04), the aircraft lane blocking vehicles (AI-05), and a failed Bore dig (AI-06). The API key can leave for the wrong provider (AI-07). For the Mountains Directorate/Left Behind skew I can only offer a hypothesis: row-major tie-breaks (AI-08). D-051 holds: no AI code reads the sim, the view is fog-honest for actors, and there are no cheats.

## Findings
### AI-01 · major · SIM · confidence high
**What:** Supply tenders spawn on the map-edge sea list in cell-index order. The start index is a global counter that goes up by 1 per tender, so every tender appears on the top row next to the north-west start. The other start's tenders sail the length of the map. This explains Glass Coast layout 1 Expedition 55 v 5 (HANDOFF step 3) and the rust_belt skew.
**Where:** src/Sim/Simulation.Shipments.cs:206-251. The edge list is built rows-then-columns at :211-218, the region filter is at :220-240, and `start = NextTenderId % edge.Count` is at :241.
**Evidence:** Glass Coast seed 1: starts are at (13,13) and (82,82) and the whole ring of ocean is one region, so the list begins with row 0, x = 0..95. Over a game of about 40 tenders per player the spawn point never leaves that row's north-west part.
Run: `ai match --a tiered:3 --b tiered:3 --faction-a expedition --faction-b expedition --biome glass_coast --layout 1 --size 96 --starts slot --income 1 --ticks 6000` (ship_probe.txt). Shipment credits at 3 / 5 min, start-0 side v start-1 side:
- Seeds 1, 2, 3: 7520/14080 v 0/5760, the same whichever AI or slot sits on start 0.
- Seed 4: no shipments on either side (that is AI-04).
In the seed 1 trace, at t1500 and t3000 the start-1 side has one, then three tenders at sea (6 000 credits each, which is why its army value reads 7 400 and 19 400). Income at 3 min is 8 320 v 1 600.
- rust_belt (start 1's region filter leaves only the south row, which is walked from its far end): start 0 leads at 5 min in all 4 seeds, 14 080 v 9 280-10 920.
- ash_plains (both starts at mid-x): no consistent lead.
This ranking matches the s17 baseline Expedition start shares: glass 92 %, rust 61 %, ash 52 %. Design 07 §2.2 only asks for "a deterministic map-edge sea cell", so this rule was not an owner decision.
**Impact:** One start gets its main income 3-5x later. All layout 1 Expedition balance numbers on glass_coast and rust_belt are biased by start.
**Fix idea:** Spawn at the edge cell nearest the dock within the dock's sea region, ties by cell index; rotate among the k nearest if variety is wanted. This moves hashes, goldens and the Expedition baselines. S.

### AI-02 · critical · SIM · confidence high
**What:** Expedition on Mountains layout 1 at size 96 gets no Command Ship and no Landing Craft in 3 of 8 seeds, so it never builds anything. HANDOFF and s17_layout1_baseline ("After the fix") say this stall is gone.
**Where:** src/Data/SkirmishStart.cs:26 (`MaxFallbackRing = 16`) and :109-127. The game's skirmish screen uses the same code.
**Evidence:** In seed 1 (starts at (13,47) and (82,47)) the nearest Water or Shallow cell is 19 cells away for both starts. `ai match --biome mountains --layout 1 --size 96 --seeds 8 --ticks 1500 --starts slot`: seeds 1, 5 and 6 end with "structures 0, units 4" (3 Marines and a Skimmer) on both sides. Seed 1 run to 6 000 ticks: income 0 and cash still 5 000.
**Impact:** On those maps an Expedition player or AI cannot play at all, and AI games run to a draw.
**Fix idea:** Search the whole map for the nearest sea that connects to the map edge, or have the generator guarantee such sea near each start. Add the Mountains test HANDOFF already asks for; seeds 1, 5 and 6 are ready-made cases. S.

### AI-03 · major · SIM · confidence high
**What:** The Command Ship spawns on the first naval-passable cell the ring scan finds, which can be an inland lake. The base and Anchorage then go on the lake, and tenders can never reach the dock.
**Where:**
- src/Data/SkirmishStart.cs:92-106 (any naval-passable cell is accepted).
- src/AI/Recruit/ExpeditionTactics.cs:400-438: `ChooseSite` stays in the ship's naval region and never checks that the region reaches the map edge.
- Simulation.Shipments.cs:237-240: when no edge cell shares the dock's region, the whole edge is kept.
**Evidence:** Mountains seed 2: the nearest sea is a 31-cell inland lake 12 cells away; the edge sea is 31 cells away. The ships spawn at offsets (-8,-12) and (6,-12), which are not mirror images. In the game both sides have an Anchorage at t752, no shipment income by 5 min, and two then three tenders stuck at sea.
**Impact:** No shipment income for the whole game, and any warships are built into a pond.
**Fix idea:** In SkirmishStart, prefer sea whose region touches the map edge. In ChooseSite and NoSite, require the Anchorage's water to share a region with a `CopyConvoyEdge` cell. S.

### AI-04 · critical · CODE · confidence high for the stall, medium for the exact cause
**What:** If the structure lane holds a Ready structure that cannot be placed, the lane stays busy for the rest of the game. Nothing cancels the structure, so no later step is ever built, including the Beacons that would grow the Expedition's build zone.
**Where:** src/AI/Recruit/BuildOrderManager.cs:105-117 (a ready item sets `laneBusy`, which stops all new starts) and :246-256 (`TryPlace` simply returns when no site is found).
**Evidence:** Glass Coast seed 4, `--trace 2000`: both sides build a Pile House and then nothing. At t2000 and t4000 both show 2 buildings, cash 2950, income 0. 2 950 = 5 000 - 250 - 1 800, so the Dredge Works was paid for and finished but never placed. I inferred this from the cash; the AI's `Unplaceable` counter is not printed.
**Impact:** The AI does nothing for the whole game. Any cramped base can trigger it; the Expedition is most exposed because its build zone around the depot starts small.
**Fix idea:** After N decisions with no site, cancel the ready structure and hold that step. For the Expedition, put a Beacon first when the next structure has no legal cell. This changes AI results. S.

### AI-05 · major · CODE · confidence high
**What:** With the Air flag (tiers 4-5), if an aircraft is wanted while the aircraft lane is already busy, the vehicle lane returns without building anything.
**Where:** ProductionManager.cs:117-129 (`if (aircraft && k.Queue(lane).Items.Count > 0) return;`). `WantedAircraft` (TierPlanner.cs:215-243) counts queued aircraft, so with one queued and fewer than the cap of 2 alive it keeps asking for another.
**Evidence:** Read from the code. The path runs on every decision once the air building stands.
**Impact:** Tier 4/5 vehicle production stops for every aircraft build. The fix moves the tier 4/5 baselines.
**Fix idea:** When the aircraft lane is busy, ignore the aircraft wish and build from the normal vehicle mix. S.

### AI-06 · major · CODE · confidence medium
**What:** Each Bore gets exactly one Deploy order. If the dig fails, the sim surfaces it as a unit (BurrowMove.cs:153, :231-238). The idle Bore is never ordered again, and since a Bore still exists no new one is bought, so the Directorate's forward tunnels stop for the whole game.
**Where:** DirectorateTactics.cs:150-162 (`_deployOrdered`) and :142 (`k.Bores.Count > 0`). `Site()` at :166-178 does not check the terrain, so it can pick rock, cliff or water.
**Evidence:** Read from the code; I did not measure how often it happens.
**Impact:** Fewer or no forward hatches. A failure rate that differs by start could also feed AI-08.
**Fix idea:** If a Bore is idle again about 300 ticks after its order, clear it and pick another site. S.

### AI-07 · major · CODE · confidence high
**What:** One key store serves both providers. Switching provider keeps the key, and the game sends it to whichever provider and base URL is set. An Anthropic key therefore goes as a Bearer token to an OpenAI-compatible endpoint, often an http:// server on the LAN, or to any custom base URL.
**Where:** Game/Sandbox/CommanderSet.cs:38, Llm/OpenAiCompatibleProvider.cs:49, Game/Ui/Llm/LlmSettingsModel.cs:55-58 (CycleProvider resets URL and model but not the key). The connection test sends the same key.
**Evidence:** Read from the code. The DPAPI store itself is sound and the key is never logged.
**Impact:** The paid key leaves the machine, in clear text over http, and lands in another server's logs.
**Fix idea:** Keep one key per provider, send no key to OpenAI-compatible unless one was entered for it, and send no key over plain http except to localhost. S.

### AI-08 · major · CODE · confidence low (hypothesis)
**What:** The Mountains start skews: Directorate 15 v 33 on layout 1 and 30 v 16 on layout 2 v5; Left Behind 29 v 13 on layout 2. `ai match` alternates which slot gets which start (AiCommands.cs:770-772) and the starts are exact images, so slot order cannot cause a start skew. What remains is code whose choice changes under the map's symmetry. These tie-breaks all favour north-west because they scan rows first and sort by Y, then X:
1. SkirmishStart.cs:92-106, start units. The 4 Directorate Conscripts always spawn north of the yard; the Left Behind packed rig spawns at (-1,-1) of the start.
2. LeftBehindTactics.cs:319-340, the Mother Rig's unpack site. Together with (1), the LB base always sits north-west of its start: toward the edge for one player, toward the centre for the other.
3. PlacementSearch.cs:50-60, every AI structure. Candidates often tie on distance to the yard and ties go north-west, so every base grows north-west first.
4. DirectorateTactics.cs:174-176, the third and later forward hatches are offset to the side. This is fair under rot2, but under mirror_x (layout 1) the west player digs south and the east player north.
5. DirectorateTactics `NearestClean` (:250-261); ExpeditionTactics :418 and :465-468; MapGenerator.cs:207-218 (landing markers take the first coast cell in index order).
6. Spiral searches in the sim, such as `OrderToDock` "first of equals wins" (not audited).
Authority has no start units and no tunnel tactics, and it is not skewed on Mountains (24 v 33).
**Evidence:** Read from the code only. The skew changes direction between layouts, which fits a north-west bias whose effect depends on where each start sits. I could not change and rebuild the code to measure it.
**Impact:** The D-197 limit (start side at most 60 %) is partly measuring AI and setup geometry, not just the map.
**Fix idea:** Break ties in each player's own frame: map candidates through the transform that takes this start to start 0, or prefer the cell toward the enemy or the centre. Cheapest diagnostic: change (1) and (3) only and run 32 seeds on the laptop. M.

### AI-09 · minor · CODE · confidence medium
**What:** RepairManager treats "health did not rise for 4 decisions" as "repair stopped" and toggles Repair again. A structure under fire that loses health faster than it repairs gets repair switched off, then back on 4 decisions later, and so on.
**Where:** RepairManager.cs:43-50.
**Impact:** About half the repair time is lost during sieges, when repair matters most.
**Fix idea:** Toggle again only when cash is short or health is unchanged, not when it is falling. S.

### AI-10 · minor · CODE · confidence high
**What:** The LLM commander's calls made, next call tick and history are not saved. After a load the call cap (default 120) starts again from 0 and a call fires immediately.
**Where:** Llm/Commander.cs:39-41. Only the slot's Commander flag is saved (GameSaveHeader.cs:123).
**Fix idea:** Save those fields with the AI state, or document the cap as per session. S.

### AI-11 · minor · TEXT · confidence high
**What:** Two difficulty keys do nothing for most tiered AIs:
- `focus_fire` (on at tiers 3-5) is read only by SkirmisherAi and ExpeditionTactics.cs:732.
- `scout_info_age_ticks` is read only by HarassManager (tiers 4-5), LeftBehindTactics.cs:514 and SkirmisherAi.
For Authority and Directorate at tiers 1-3 both are ignored without warning.
**Where:** data/ai/difficulty.toml:9-10, 105, 128, 154.
**Fix idea:** Note in the file header who reads each key, or wire focus fire into MilitaryManager. S.

### AI-12 · minor · DESIGN · confidence medium
**What:** Text the player types to an LLM commander goes into that commander's prompt word for word (CommanderChatModel.cs:85-98, BattlefieldSummary.cs:63-64). The player can talk an enemy commander into turtling. Damage is limited because the parser and the sim clamp everything (Simulation.M5.cs:20-27).
**Fix idea:** Mark the chat clearly as untrusted quoted data, or allow chat only with allied commanders. S.

### AI-13 · minor · TEST · confidence high (still open from the 2026-11-03 review, item A-4)
**What:** The determinism analyzer still scans only src/Sim (tests/Sim.Tests/Determinism/DeterminismRulesTests.cs:18-22). src/AI is clean today (no clock, no System.Random, no decision driven by dictionary or hash-set order), but nothing keeps it that way. This matters for reproducible AI baselines, not for desyncs.
**Fix idea:** Run the analyzer over src/AI as well. S.

## Open questions
- AI-01: should convoys rotate around the coast at all?
- AI-02/03: should the map generator guarantee sea that reaches the map edge near each Expedition start (D-124)?
- Why does HANDOFF's "After the fix" check (4 seeds) show no empty side on Mountains layout 1, when seed 1 at size 96 fails here?
- The TierPlanner.cs:301 comment says "tiers 2 and 3", but the tier 5 hold is included.

## Things checked and found sound
- **D-051:** the AI runs before Step and only adds commands. Only AiDriver and AiMatchRunner touch the Simulation, and only for public map starts. Directive and AI commands are recorded (SimHost.cs:255-261), so replays do not depend on the AI or the LLM.
- **PlayerView:** enemy units appear only when visible and not hidden; enemy buildings by vision plus ghosts. Storms are listed for the whole map, but the AI uses only its own. Wrecks are public by decision (D-102).
- **Difficulty:** no cheats (D-061). The loader is strict about keys and ranges. Per-faction tier overrides apply. `unit_reserve` is wired in (ProductionManager.cs:98), just unused in the data. All four factions have their keys. All 100 ids the AI uses exist with the right faction prefix.
- **Snapshot/restore:** every long-lived manager field is saved (checked with snapcheck.py), map keys are written sorted, and the RNG state is restored after Init. A blob that is only half read keeps the half-restored AI (AiDriver.cs:137-144); a fresh AI would be cleaner (minor).
- **Mop-up:** the code is dead because both switches are const false (MilitaryManager.cs:191-197).
- **LLM:** the reply parser never throws and clamps every value. Calls run off the game thread with a 20 s timeout, one at a time, and stale replies are dropped. With no key the commander goes offline cleanly. Error texts never carry the key. The DPAPI store is sound. Cost is bounded by the cap; a cap of 0 means unbounded, and that is a documented setting.

Memory: nothing new worth saving. I used no memory content.