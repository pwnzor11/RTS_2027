## v-ai-map hand-back

I confirmed AI-01, -02, -03, -04, -06, -09, FACTION-LE-02, DATA-TOOLS-01 and DATA-TOOLS-06 with tests. AI-05 and AI-08 are only partly right (details below). Nothing is fixed: everything here changes match outcomes. The branch has tests and the laptop script only, no `src/` or `data/` change. The biggest result: Expedition on Mountains layout 1 (the default layout) is broken on about 80 % of maps, not 3 of 8 as the review said.

**COMMITS** (branch `wp/s18-v-ai-map`, worktree `/home/claude/wt/s18-v-ai-map`)
- 3c2a496 test(sim): pin north-west tender spawn on mirrored docks [S18-AI-01]
- f376747 test(ai): pin Expedition start kit gaps on Mountains and non-mirrored start kits [S18-AI-02, AI-03, FACTION-LE-02, DATA-TOOLS-01]
- adb4ae8 test(ai): pin AI stalls: unplaceable ready structure, aircraft lane, idle Bore, repair toggle [S18-AI-04, AI-05, AI-06, AI-09]
- 535faf6 test(ai): pin north-west placement tie-break for mirrored yards [S18-AI-08]
- 72870b5 tools(measure): laptop series for the S18 start-skew and tender-spawn audit [S18-AI-01, AI-08]
- c21def0 test(ai): pin layout-2 maps returned with failed layout checks [S18-DATA-TOOLS-06]

Every "pin" test is an `Audit_` test that passes today, with a skipped twin holding the wanted assertion.

**VERDICT TABLE**

| Finding | Verdict | Test | Fixed? | Note |
|---|---|---|---|---|
| AI-01 | CONFIRMED | `Rts.Sim.Tests.S18.ShipmentsSpawnAuditTests.Audit_MirroredDocks_TheNorthWestDockIsSuppliedFirst`, `Audit_SwappedDocks_TheAdvantageFollowsTheCornerNotTheSlot` | no: awaiting owner | Mirrored docks: first tender paid at tick 107 v 243; both tenders spawn at (0,0)/(1,0). Swapping the docks swaps the advantage, so it follows the corner, not the slot. |
| AI-01 (CLI) | CONFIRMED | ai match, 4 seeds, `--starts slot` and `swap` | — | Shipment credits at 3/5 min, start 0 v start 1: glass_coast 7520/14080 v 0/5760; rust_belt 7520/14080 v 3880-7520/9280-12440. Same in both slot orders. ash_plains has no consistent lead. |
| AI-02 | CONFIRMED, and much wider than reported | `AI.Tests.S18.StartPlacementAuditTests.Audit_Expedition_MountainsLayout1_Medium_GetsNoCommandShip` (seeds 1, 5, 6) | no: awaiting owner | Nearest sea is 18-21 cells away, beyond the ring-16 fallback; each side gets only 3 Marines and a Skimmer. Sweep below. |
| AI-03 | CONFIRMED | `Audit_Expedition_MountainsLayout1_Seed2_CommandShipSpawnsOnAnInlandLake` | no: awaiting owner | Ships at offsets (-8,-12) and (6,-12) on a lake 12 cells away; the nearest sea that reaches the map edge is 31 cells away. |
| FACTION-LE-02 | CONFIRMED | `Audit_Expedition_WithNoBuildingEver_IsNeverDefeated_EvenWithNothingLeft` | no: awaiting owner | Mountains seed 1: all units killed, 3000 ticks later the player is still not defeated, so the game never ends. |
| DATA-TOOLS-01 | CONFIRMED | `Audit_StartKit_IsNotTheHalfTurnImage_OnMountainsLayout2` (Directorate, Left Behind, Expedition) | no: awaiting owner | Example: Directorate Conscripts sit at y-2 from both starts; the image would be y+2 for start 1. The Authority yard alone is an exact image (control test `Authority_StartYard_IsTheHalfTurnImage_OnMountainsLayout2`). |
| AI-04 | CONFIRMED | `AiStallAuditTests.Audit_GlassCoastSeed4_ExpeditionAi_HoldsAnUnplaceableReadyStructureForTheWholeGame` | no: awaiting owner | Both AIs hold a finished `exp_dredge_works` from tick 661/931 to 4000; 2 buildings, cash 2950. `Unplaceable` stays 0 because the search finds no site at all, and that counter only counts sites that `CanPlace` refuses. |
| AI-05 | PARTLY | `Audit_Tier4_BuildsNoVehicleWhileItsFirstAircraftIsInProduction` | no: awaiting owner | The mechanism is real, but the block lasts only while the AI has no aircraft alive and one queued (1 < cap 2). Measured: vehicle lane idle about 300 ticks during the first dirigible's build, against about 21 ticks between vehicles otherwise. It does not happen on every aircraft build. |
| AI-06 | CONFIRMED (AI half; how often digs fail is not measured) | `Audit_IdleBore_IsDeployedOnce_AndNeverReordered_NorReplaced` | no: awaiting owner | An idle Bore gets one Deploy order ever, and no new Bore is wanted while it exists. The control without a Bore does buy one. |
| AI-08 (3) placement | PARTLY | `PlacementTieBreakAuditTests.Audit_MirroredYards_PlaceTheFirstStructureAtTheSameOffset_NotTheMirrorImage` | no: awaiting owner | Mirrored yards both put the first power plant at offset (0,-3): a translation, not a mirror image. The experiment below suggests this tie-break, not start-unit placement, carries most of the Mountains Directorate skew; small sample. |
| AI-09 | CONFIRMED | `Audit_RepairManager_TogglesRepairAgain_WhileHealthKeepsFalling` | no: awaiting owner | With health falling, Repair orders go out at decisions 0, 4, 8 (on, off, on). |
| DATA-TOOLS-06 | CONFIRMED | `Layout2RetryAuditTests.Audit_Layout2_ReturnsAMapThatFailsItsOwnChecks_WithoutAnError` | no: awaiting owner | glass_coast medium 3p and 4p seed 10, mountains small 3p seed 7: each ships with 1 failed layout check and no error. |

**What session 17 fixed, and what is still broken (AI-02 / AI-03 sweep)**

Session 17 added a second placement pass out to ring 16 (`SkirmishStart.SpawnUnits`). That covers sea 9-16 cells from the start. Sweep: seeds 1-50, every biome x size x layout, with `ai match`'s ocean (north and south) and with north only (the game's default when no ocean is chosen). Results:
- Layout 1 rust_belt, glass_coast, ash_plains and all of layout 2: 0 failures at every size.
- **Mountains layout 1** is broken on most maps. "No ship" means no Command Ship at all; "lake" means the ship is on water that never reaches the map edge, so no shipment can arrive.

| Ocean | Size | No ship | Ship on a lake | Fully OK (of 50) |
|---|---|---:|---:|---:|
| North + south | Small | 11 | 33 | 6 |
| North + south | Medium | 13 | 27 | 10 |
| North + south | Large | 22 | 21 | 7 |
| North only (game default) | Small | 12 | 30 | 8 |
| North only (game default) | Medium | 22 | 24 | 4 |
| North only (game default) | Large | 24 | 25 | 1 |

This matches the session 17 baseline: on Mountains, 100 of 128 Expedition sides had no income at 3 minutes. The session 17 "after the fix" check (16 seeds, no side built nothing) probably counted the lake maps as working, because a base does get built there. I can't explain how seeds 1, 5 and 6 passed that check; the clone has no history to look back at. Sweep tool and output: `/home/claude/audit/v-ai-map/sweep/Program.cs`, `/home/claude/audit/v-ai-map/runs/sweep_ns.txt`, `/home/claude/audit/v-ai-map/runs/sweep_n.txt`.

**EXPERIMENT TABLE (part B, reduced scope)**

One experimental build (experimental worktree `/home/claude/wt/s18-exp-skew`, not committed). Switches are read once at start: `RTS_EXP_TENDER`, `RTS_EXP_START`, `RTS_EXP_PLACE`. The three switches are:
- **(i) tender:** a supply tender spawns at the edge sea cell nearest the dock.
- **(ii) start:** start 1 gets the symmetry image of start 0's yard and units. Checked: images come out exact on Mountains layouts 1 and 2 and on Glass Coast.
- **(iii) place:** AI placement breaks distance ties toward the map centre.

Each cell: tier 3 mirror, size 96, seeds 1-8 x 2 = 16 games, 30 000 ticks, same seeds for every switch setting.

| Cell | Switches | Start 0 wins | Start 1 wins | Draws | Notes |
|---|---|---:|---:|---:|---|
| Expedition glass_coast layout 1 | off | 12 | 1 | 3 | shipments 3/5 min: start 0 6580/12290, start 1 0/5040 |
| | tender | 2 | 9 | 5 | shipments equal: 6580/12305 v 6580/11728 |
| | start | 13 | 1 | 2 | |
| | place | 14 | 0 | 2 | |
| | all | 8 | 6 | 2 | shipments equal: 6580/12312 v 6580/12028 |
| Directorate mountains layout 1 | off | 3 | 11 | 2 | income 3/5 min: start 0 7512/17267, start 1 8447/19441 |
| | start | 5 | 8 | 3 | |
| | place | 7 | 6 | 3 | income 8640/19133 v 8490/19471 |
| | all | 8 | 6 | 2 | income 8863/20026 v 9046/20206 |

How to read 16-game cells: one cell's start share has a standard error of about ±12-14 points (fewer decisive games than 16 makes it worse).
- **What it supports:** the Glass Coast skew comes from the tender spawn. Off: 12 v 1 with start 1 getting no shipment by 3 minutes. Tender switch: shipment credits become equal at 3 and 5 minutes. All on: 8 v 6. The start and place switches alone leave 13-14 v 0-1. That is far outside the noise.
- **What is suggestive only:** on Mountains the place switch alone takes 3 v 11 to 7 v 6, and all on gives 8 v 6, with 3- and 5-minute income evening out. The start switch alone (5 v 8) is inside the noise.
- **What it cannot show:**
  - Whether tender alone over-corrects. 2 v 9 is only about p = 0.07, and I can't tell it apart from fair.
  - Any remaining skew under about 15 points.
  - The Mountains layout 2 and Left Behind cells, which were cut from this run.

The laptop series (main only) re-measures the baselines at 64 games; the switches themselves would need the same 64-game treatment before any fix is chosen.

Patch for the lead: `/home/claude/audit/v-ai-map/skew_experiment.patch` (155 lines, git diff, not committed). Raw output is in `/home/claude/audit/v-ai-map/runs/exp/`.

**CHECK** (each command run under the dotnet lock)
- `dotnet test tests/AI.Tests -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AI.Tests.S18"`: 14 passed, 4 skipped.
- `dotnet test tests/AI.Tests ... --filter "FullyQualifiedName~AI.Tests.S18.Layout2RetryAuditTests"`: 3 passed (added after the run above).
- `dotnet test tests/Sim.Tests ... --filter "FullyQualifiedName~Rts.Sim.Tests.S18.ShipmentsSpawnAuditTests"`: 2 passed, 1 skipped.

**OWNER'S LAPTOP COMMAND** (from the RTS_2027 root, after `git pull` of this branch once it is merged)
```
powershell -ExecutionPolicy Bypass -File tools\measure\run_s18_audit.ps1
```
- 17 rows x 64 games, tier 3, size 96, 30 000 ticks, about 95 minutes (the handoff's 16-row series took about 90).
- Results go to `balance_runs\s18audit\`; the file to send back is `s18audit_summary.md`.
- `-Groups 1,2` runs part of it. `-DryRun` lists the command lines without building or running. The old scripts had no dry-run, so I added one rather than a test; there is no PowerShell on this box, so the script has never been run. I checked its income and shipment patterns in Python against real CLI output, with CRLF line endings.
- The rows: (1) Expedition mirror on glass_coast, rust_belt, ash_plains, mountains layout 1; (2) Directorate and Left Behind mirrors on mountains layouts 1 and 2; (3) the six cross-faction pairs on rust_belt layout 1; (4) Expedition v Authority, Directorate and Left Behind on glass_coast.
- The summary keeps the hard-coded "start 0 (4,4) … start 1 (mirror)" label regex. It adds per-start income at 3/5 minutes, 10-minute income over games still running at tick 12 000 (D-201), and per-start shipment credits at 3/5 minutes.

**NEW FINDINGS**
- **S18-AI-NEW-1 (major, SIM):** the AI-02/AI-03 scale above. About 80-98 % of Mountains layout 1 maps (it depends on size and ocean setting) give an Expedition no Command Ship, or one on a lake.
- **S18-AI-NEW-2 (minor, CODE):** `BuildOrderManager.Unplaceable` does not count the case where the search finds no site at all, which is exactly the AI-04 stall. The counter can't be used to detect it.
- **S18-AI-NEW-3 (minor, CODE):** PlacementSearch lanes always run south of factory exits and refinery docks (absolute direction). Under a half turn this is a further non-image asymmetry. Not measured.

**QUESTIONS FOR THE OWNER**
1. **Tender spawn (AI-01):** (a) nearest edge cell to the dock; (b) rotate among the k nearest; (c) keep as it is. Recommend (a). Measured: it evens the shipment credits exactly. It moves hashes, goldens and the Expedition baselines.
2. **Expedition sea on Mountains (AI-02/03):** (a) the generator guarantees sea that reaches the map edge near each start; (b) `SkirmishStart` searches the whole map for such sea; (c) block the Expedition on Mountains layout 1. Recommend (a), with (b) as a safety net.
3. **Start-side tie-breaks (AI-08, DATA-TOOLS-01):** apply the placement tie-break toward the map centre and the mirrored start kit? Recommend confirming both on the laptop with the patch first; the place switch looks like the larger effect.
4. **FACTION-LE-02:** count the Expedition start kit as "had a base", so a side with nothing left is defeated? Recommend yes.

**USER CHECK:** none; nothing was fixed.

**Memory:** nothing new worth saving.